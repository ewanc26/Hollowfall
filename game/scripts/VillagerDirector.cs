using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Relations;
using Hollowfall.Core.Story;
using Hollowfall.Core.Story.Dialogue;

namespace Hollowfall.Game;

/// <summary>
/// Connects the villagers in Core to the screen: "speak" when you stand beside one, and on Interact the crofters' conversation in the
/// choice panel. What they remember is saved per seed with the story progress. A plain object owned by Main, like <see cref="CatDirector"/>,
/// so the smoke test can drive it. The press that opens the panel is used up, so it never also opens a chest.
/// </summary>
public sealed class VillagerDirector
{
    private readonly VillagerConversation _conversation;
    private readonly StoryUi _ui;
    private readonly StoryProgress _progress;
    private readonly string _seedName, _savePath;
    private readonly Func<ReputationTier> _standing;

    /// <summary>The label of the Interact button as the player has bound it (set by the host).</summary>
    public Func<string> InteractLabel { get; set; } = () => "Interact";

    /// <summary>The label of the menu button (set by the host).</summary>
    public Func<string> MenuLabel { get; set; } = () => "Menu";

    public VillagerDirector(VillagerConversation conversation, StoryUi ui, StoryProgress progress, string seedName, string savePath, Func<ReputationTier> standing)
    {
        _conversation = conversation;
        _ui = ui;
        _progress = progress;
        _seedName = seedName;
        _savePath = savePath;
        _standing = standing;
        _ui.Closed += OnClosed;
    }

    /// <summary>Called every physics step. Returns the actions with the press used up if it spoke to a villager.</summary>
    public PlayerActions Filter(PlayerActions actions, Vec3 feet, bool alive, IEnumerable<Enemy> enemies)
    {
        if (!alive || _ui.Modal || !VillagerConversation.InReach(enemies, feet)) return actions;

        _ui.SetPrompt(_ui.Speaking ? null : $"{InteractLabel()}  speak");
        if (actions.Interact)
        {
            Talk();
            actions = actions with { Interact = false };
        }
        return actions;
    }

    private void Talk()
    {
        var runner = _conversation.Begin(_standing());
        _ui.ClearCaption();
        Save();
        _ui.ShowDialogue(VillagerDialogue.Name, runner.Text, Labels(runner), Choose, $"{InteractLabel()} or {MenuLabel()} to leave");
    }

    private static List<string> Labels(DialogueRunner runner) => [.. runner.Choices.Select(c => c.Text)];

    private void Choose(int index)
    {
        var runner = _conversation.Conversation;
        if (runner == null || !_conversation.Choose(index)) return;
        Save();
        if (runner.Finished)
        {
            _ui.Close();
            return;
        }
        _ui.UpdateDialogue(runner.Text, Labels(runner));
    }

    private void OnClosed()
    {
        if (_conversation.Conversation == null) return;
        _conversation.End();
        Save();
    }

    private void Save()
    {
        _progress.SetVillagers(_seedName, _conversation.Flags, _conversation.Visits);
        _progress.TrySave(_savePath);
    }
}
