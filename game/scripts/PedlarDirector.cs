using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Relations;

namespace Hollowfall.Game;

/// <summary>
/// Connects the pedlar on the dungeon plaza to the screen: stand beside him and press Interact to open his pack. The pack
/// is the dialogue panel (a line and a button per ware), so the shop works with the keyboard, mouse and gamepad without a
/// second UI. Prices and access come from <see cref="Merchant"/> and the Villagers' standing; buying spends coin and puts
/// the item in the bag, then saves the character. A plain object owned by Main, like <see cref="VillagerDirector"/>.
/// </summary>
public sealed class PedlarDirector
{
    private readonly Progression _character;
    private readonly Func<ReputationTier> _standing;
    private readonly StoryUi _ui;
    private readonly Action _save;
    private string _notice = "";

    /// <summary>The label of the Interact button as the player has bound it (set by the host).</summary>
    public Func<string> InteractLabel { get; set; } = () => "Interact";

    /// <summary>The label of the menu button (set by the host).</summary>
    public Func<string> MenuLabel { get; set; } = () => "Menu";

    public PedlarDirector(Progression character, Func<ReputationTier> standing, StoryUi ui, Action save)
    {
        _character = character;
        _standing = standing;
        _ui = ui;
        _save = save;
    }

    /// <summary>Called every physics step. Returns the actions with the press used up if it opened the pack.</summary>
    public PlayerActions Filter(PlayerActions actions, Vec3 feet, bool alive, IEnumerable<Enemy> enemies)
    {
        if (_ui.SwallowInteract) return actions with { Interact = false };
        if (!alive || _ui.Modal || !InReach(enemies, feet)) return actions;

        _ui.SetPrompt($"{InteractLabel()}  trade");
        if (!actions.Interact) return actions;
        Open();
        return actions with { Interact = false };
    }

    /// <summary>Whether a living pedlar on the surface is near enough to trade with.</summary>
    public static bool InReach(IEnumerable<Enemy> enemies, Vec3 feet)
    {
        foreach (var e in enemies)
        {
            if (e.Kind != EnemyKind.Pedlar || e.Dead || e.Floor != CombatSim.SurfaceFloor) continue;
            double dx = e.Feet.X - feet.X, dz = e.Feet.Z - feet.Z;
            if (Math.Abs(e.Feet.Y - feet.Y) <= Merchant.ReachHeight && dx * dx + dz * dz <= Merchant.Reach * Merchant.Reach) return true;
        }
        return false;
    }

    private void Open()
    {
        _notice = "";
        _ui.ClearCaption();
        Show();
    }

    private void Show()
    {
        var tier = _standing();
        _ui.ShowDialogue("Pedlar", Text(tier), Labels(tier), Choose, $"{InteractLabel()} or {MenuLabel()} to leave");
    }

    private string Text(ReputationTier tier)
    {
        string notice = _notice.Length > 0 ? _notice + "\n\n" : "";
        if (!Merchant.WillServe(tier))
            return notice + "The pedlar looks at you, then at the road behind you, and does not open the pack. \"Not today.\"";
        return notice + $"The pedlar sets down a stained pack and waits. You carry {_character.Coin} coin.";
    }

    private List<string> Labels(ReputationTier tier)
    {
        var labels = new List<string>();
        if (Merchant.WillServe(tier))
            foreach (var item in Merchant.Wares)
                labels.Add($"{Inventory.Info(item).Name}  {Merchant.PriceFor(item, tier)} coin");
        labels.Add("Leave");
        return labels;
    }

    private void Choose(int index)
    {
        // When the pedlar will not serve you the only choice is Leave, so an index inside Wares would be read as a purchase.
        if (!Merchant.WillServe(_standing()) || index < 0 || index >= Merchant.Wares.Count)
        {
            _ui.Close();
            return;
        }

        var item = Merchant.Wares[index];
        switch (Merchant.Buy(_character, item, _standing()))
        {
            case Purchase.Bought:
                _save();
                _notice = $"You buy {Inventory.Info(item).Name}.";
                break;
            case Purchase.NoCoin:
                _notice = $"You cannot cover that. You carry {_character.Coin} coin.";
                break;
            case Purchase.BagFull:
                _notice = "There is no room in your bag for that.";
                break;
            case Purchase.Refused:
                _notice = "The pedlar shakes his head and closes the pack.";
                break;
        }
        Show();
    }
}
