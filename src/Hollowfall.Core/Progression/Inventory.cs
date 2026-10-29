namespace Hollowfall.Core.Progression;

/// <summary>What you can carry. Appended to, never reordered, since the save file stores these by name.</summary>
public enum ItemKind
{
    /// <summary>A jar of warm cream the Brownie is owed. Using one in a run heals <see cref="Inventory.CreamHeal"/>.</summary>
    BrownieCream,

    /// <summary>A charm of rowan and thorn. Worn in the charm slot, it turns aside part of every blow (<see cref="Inventory.ThornWard"/>).</summary>
    ThornCharm,

    /// <summary>A drop of clean water from a hidden spring. It washes away all hard damage.</summary>
    CleansingDrop,

    /// <summary>A shard of bright glass. Using one bursts into style.</summary>
    StyleShard,

    /// <summary>A bitter draught. Close hits heal more for a while.</summary>
    BloodlustDraught,

    /// <summary>A sharp tonic. The parry window stays open longer for a while.</summary>
    OpenGuardTonic,

    /// <summary>A Warden's seal, worn in the relic slot. It raises the run's maximum health (<see cref="Inventory.WardenSealHealth"/>).</summary>
    WardenSeal,

    /// <summary>A lucky hare's foot, worn in the relic slot. It raises the run's damage (<see cref="Inventory.HareFootDamage"/>).</summary>
    HareFoot,

    /// <summary>A vial of blood from a chest. Using one in a run heals <see cref="Inventory.VialHeal"/>.</summary>
    VialOfBlood,
}

/// <summary>How an item is used.</summary>
public enum ItemUse
{
    Consumable,
    Charm,
    Relic,
}

/// <summary>
/// A bag with a fixed number of slots, each holding up to a set number of one kind of item, one charm slot and one relic slot.
/// Pure and deterministic. A run reads its quick item and worn equipment once, when it starts, and records them in its replay.
/// </summary>
public sealed class Inventory
{
    public const int Slots = 12;
    public const int MaxStack = 9;

    /// <summary>Health a jar of cream gives back when it is used.</summary>
    public const double CreamHeal = 40;

    /// <summary>Health a vial of blood gives back when it is used.</summary>
    public const double VialHeal = 30;

    /// <summary>The share of each blow the thorn charm turns aside.</summary>
    public const double ThornWard = 0.1;

    /// <summary>Maximum health the Warden's seal adds to a run.</summary>
    public const double WardenSealHealth = 20;

    /// <summary>The extra damage the hare's foot adds to a run.</summary>
    public const double HareFootDamage = 0.1;

    /// <summary>Style multiplier a shard applies to a parry's points.</summary>
    public const double StyleShardMultiplier = 2;

    /// <summary>Blood from hits multiplier, and how long it lasts, from one bloodlust draught.</summary>
    public const double BloodlustScale = 1.5;
    public const double BloodlustSeconds = 20;

    /// <summary>Seconds added to the parry window, and how long it lasts, from one open guard tonic.</summary>
    public const double OpenGuardBonus = 0.1;
    public const double OpenGuardSeconds = 25;

    private readonly Dictionary<ItemKind, int> _counts = [];

    /// <summary>The charm you are wearing, or null.</summary>
    public ItemKind? Charm { get; private set; }

    /// <summary>The relic you are wearing, or null.</summary>
    public ItemKind? Relic { get; private set; }

    /// <summary>The consumable a run will use with the quick-use button. Defaults to cream, so old saves behave as before.</summary>
    public ItemKind Ready { get; private set; } = ItemKind.BrownieCream;

    public int Count(ItemKind kind) => _counts.GetValueOrDefault(kind);

    /// <summary>Slots in use: each kind takes ceil(count / <see cref="MaxStack"/>) slots.</summary>
    public int SlotsUsed => _counts.Values.Sum(c => (c + MaxStack - 1) / MaxStack);

    /// <summary>Puts items in the bag. Returns how many fitted; the rest do not fit in the slots left.</summary>
    public int Add(ItemKind kind, int amount = 1)
    {
        int added = 0;
        while (added < amount)
        {
            int have = Count(kind);

            // A partly filled stack takes another item for free; a full one (or none) needs a free slot.
            bool room = have % MaxStack != 0 || SlotsUsed < Slots;
            if (!room) break;
            _counts[kind] = have + 1;
            added++;
        }
        return added;
    }

    /// <summary>Takes items out of the bag. Returns how many were taken (fewer if you did not have that many).</summary>
    public int Remove(ItemKind kind, int amount = 1)
    {
        int have = Count(kind);
        int taken = Math.Clamp(amount, 0, have);
        if (taken == 0) return 0;
        if (have - taken == 0) _counts.Remove(kind);
        else _counts[kind] = have - taken;
        return taken;
    }

    /// <summary>Wears a charm or relic from the bag. Wearing one puts the item you had in that slot back in the bag. Returns false if you do not have it, or it is not equipment.</summary>
    public bool Wear(ItemKind kind)
    {
        var use = Info(kind).Use;
        if (use is not (ItemUse.Charm or ItemUse.Relic)) return false;
        if (Worn(use) == kind) return true;
        if (Count(kind) == 0) return false;
        Remove(kind);
        if (Worn(use) is { } old && Add(old) == 0)
        {
            // The old piece has nowhere to go: put the new one back and leave the slot as it was.
            Add(kind);
            return false;
        }
        if (use == ItemUse.Charm) Charm = kind;
        else Relic = kind;
        return true;
    }

    /// <summary>Takes the equipment in a slot off and puts it back in the bag, if there is room. Returns false if the slot is empty or the bag is full.</summary>
    public bool TakeOff(ItemUse slot = ItemUse.Charm)
    {
        if (Worn(slot) is not { } worn) return false;
        if (Add(worn) == 0) return false;
        if (slot == ItemUse.Charm) Charm = null;
        else Relic = null;
        return true;
    }

    /// <summary>The equipment in a slot, or null.</summary>
    public ItemKind? Worn(ItemUse slot) => slot switch
    {
        ItemUse.Charm => Charm,
        ItemUse.Relic => Relic,
        _ => null,
    };

    /// <summary>Chooses the consumable the quick-use button will use. Returns false if it is not a consumable or you have none.</summary>
    public bool SetReady(ItemKind kind)
    {
        if (Info(kind).Use != ItemUse.Consumable || Count(kind) == 0) return false;
        Ready = kind;
        return true;
    }

    /// <summary>The name, use and blurb of an item.</summary>
    public static (string Name, ItemUse Use, string Blurb) Info(ItemKind kind) => kind switch
    {
        ItemKind.BrownieCream => ("Brownie's cream", ItemUse.Consumable, "A warm jar of cream, owed to you by a brownie or a villager. Eat it to heal 40."),
        ItemKind.ThornCharm => ("Thorn charm", ItemUse.Charm, "Rowan and thorn, bound with red thread. Takes 10 percent off every blow."),
        ItemKind.CleansingDrop => ("Cleansing drop", ItemUse.Consumable, "A drop of clean water. It washes away all hard damage."),
        ItemKind.StyleShard => ("Style shard", ItemUse.Consumable, "A shard of bright glass. Using it bursts into style."),
        ItemKind.BloodlustDraught => ("Bloodlust draught", ItemUse.Consumable, "A bitter draught. Close hits heal more for 20 seconds."),
        ItemKind.OpenGuardTonic => ("Open guard tonic", ItemUse.Consumable, "A sharp tonic. The parry window stays open longer for 25 seconds."),
        ItemKind.WardenSeal => ("Warden's seal", ItemUse.Relic, "A heavy seal from a Warden's breastplate. Raises the run's maximum health by 20."),
        ItemKind.HareFoot => ("Hare's foot", ItemUse.Relic, "A lucky hare's foot. Raises the run's damage by 10 percent."),
        ItemKind.VialOfBlood => ("Vial of blood", ItemUse.Consumable, "A stoppered vial of warm blood. Drink it to heal 30."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Everything in the bag, for saving: each kind with its count, in a fixed order.</summary>
    public IEnumerable<(ItemKind Kind, int Count)> Contents() =>
        Enum.GetValues<ItemKind>().Where(k => Count(k) > 0).Select(k => (k, Count(k)));

    /// <summary>Restores a saved bag. Unknown or impossible values are dropped rather than trusted.</summary>
    public void Restore(IEnumerable<(ItemKind Kind, int Count)> items, ItemKind? charm, ItemKind? relic = null, ItemKind? ready = null)
    {
        _counts.Clear();
        Charm = null;
        Relic = null;
        Ready = ItemKind.BrownieCream;
        foreach (var (kind, count) in items)
            Add(kind, Math.Clamp(count, 0, MaxStack * Slots));
        // Worn equipment is not in the bag, so each is restored on its own.
        if (charm is { } wornCharm && Info(wornCharm).Use == ItemUse.Charm) Charm = wornCharm;
        if (relic is { } wornRelic && Info(wornRelic).Use == ItemUse.Relic) Relic = wornRelic;
        if (ready is { } quick && Info(quick).Use == ItemUse.Consumable) Ready = quick;
    }
}
