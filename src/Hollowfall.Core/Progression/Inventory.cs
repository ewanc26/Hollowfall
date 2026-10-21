namespace Hollowfall.Core.Progression;

/// <summary>What you can carry. Appended to, never reordered, since the save file stores these by name.</summary>
public enum ItemKind
{
    /// <summary>A jar of warm cream the Brownie is owed. Kept for later; using it is not in the game yet.</summary>
    BrownieCream,

    /// <summary>A charm of rowan and thorn. Worn in the one charm slot; its effect is not in the game yet.</summary>
    ThornCharm,
}

/// <summary>How an item is used.</summary>
public enum ItemUse
{
    Consumable,
    Charm,
}

/// <summary>
/// A bag with a fixed number of slots, each holding up to a set number of one kind of item, and one charm slot you can wear.
/// Pure and deterministic. Nothing here changes a run yet: a run reads the character's stats, not the bag.
/// </summary>
public sealed class Inventory
{
    public const int Slots = 12;
    public const int MaxStack = 9;

    private readonly Dictionary<ItemKind, int> _counts = [];

    /// <summary>The charm you are wearing, or null.</summary>
    public ItemKind? Charm { get; private set; }

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

    /// <summary>Wears a charm from the bag. Wearing one puts the charm you had back in the bag. Returns false if you do not have it, or it is not a charm.</summary>
    public bool Wear(ItemKind kind)
    {
        if (Info(kind).Use != ItemUse.Charm) return false;
        if (Charm == kind) return true;
        if (Count(kind) == 0) return false;
        Remove(kind);
        if (Charm is { } old) Add(old);
        Charm = kind;
        return true;
    }

    /// <summary>Takes the charm off and puts it back in the bag, if there is room. Returns false if there is no charm or no room.</summary>
    public bool TakeOff()
    {
        if (Charm is not { } worn) return false;
        if (Add(worn) == 0) return false;
        Charm = null;
        return true;
    }

    /// <summary>The name and use of an item.</summary>
    public static (string Name, ItemUse Use, string Blurb) Info(ItemKind kind) => kind switch
    {
        ItemKind.BrownieCream => ("Brownie's cream", ItemUse.Consumable, "A warm jar of cream, owed to you by a brownie."),
        ItemKind.ThornCharm => ("Thorn charm", ItemUse.Charm, "Rowan and thorn, bound with red thread."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Everything in the bag, for saving: each kind with its count, in a fixed order.</summary>
    public IEnumerable<(ItemKind Kind, int Count)> Contents() =>
        Enum.GetValues<ItemKind>().Where(k => Count(k) > 0).Select(k => (k, Count(k)));

    /// <summary>Restores a saved bag. Unknown or impossible values are dropped rather than trusted.</summary>
    public void Restore(IEnumerable<(ItemKind Kind, int Count)> items, ItemKind? charm)
    {
        _counts.Clear();
        Charm = null;
        foreach (var (kind, count) in items)
            Add(kind, Math.Clamp(count, 0, MaxStack * Slots));
        // A worn charm is not in the bag, so it is restored on its own.
        if (charm is { } worn && Info(worn).Use == ItemUse.Charm) Charm = worn;
    }
}
