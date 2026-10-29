using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Core.Combat;

/// <summary>What a chest can hold. There is no ammo in Hollowfall, so loot becomes a healing vial, a cleansing drop, style, a short perk or a key.</summary>
public enum LootKind
{
    /// <summary>Heals, up to the current ceiling.</summary>
    Health,

    /// <summary>Washes away all hard damage, so the ceiling is back to full.</summary>
    Cleanse,

    /// <summary>An instant burst of style points.</summary>
    StyleBoost,

    /// <summary>Perk: hits at close range heal more, for a while.</summary>
    Bloodlust,

    /// <summary>Perk: the parry window stays open longer, for a while.</summary>
    WideParry,

    /// <summary>A spare key, which opens a locked chest or the stairs.</summary>
    Key,
}

/// <summary>How good a chest is. Ordinary chests are common; locked chests (treasure and boss rooms) are rare or epic.</summary>
public enum LootRarity
{
    Common,
    Rare,
    Epic,
}

/// <summary>
/// One chest's contents. <see cref="Amount"/> is a style multiplier, a heal scale, a parry window bonus in seconds or a key
/// count, by kind; <see cref="Seconds"/> is how long a perk lasts (zero for everything instant). The bag item each kind becomes
/// is <see cref="LootTable.ItemFor"/>; its effect lives on the item, not on this roll.
/// </summary>
public readonly record struct Loot(LootKind Kind, LootRarity Rarity, double Amount, double Seconds)
{
    public bool IsPerk => Kind is LootKind.Bloodlust or LootKind.WideParry;
}

/// <summary>
/// The seeded loot table. A chest's contents are a pure function of the seed and where the chest is, so the same dungeon always
/// holds the same loot and nothing depends on the order chests are opened. Better chests (higher <see cref="Spawn.Tier"/> for
/// their floor, which is what makes them locked) roll from a better table and get stronger, longer effects.
/// </summary>
public static class LootTable
{
    private const int Salt = 940;

    private static readonly LootKind[] Kinds =
        [LootKind.Health, LootKind.Cleanse, LootKind.StyleBoost, LootKind.Bloodlust, LootKind.WideParry, LootKind.Key];

    // Relative weights per rarity, in the order of Kinds. A key is net zero from a locked chest (it cost one), so only common chests give them.
    private static readonly double[][] Weights =
    [
        [38, 12, 16, 10, 10, 10],
        [22, 14, 22, 20, 22, 0],
        [12, 14, 26, 24, 24, 0],
    ];

    private static readonly double[] HealthAmount = [30, 60, 90];
    private static readonly double[] StyleMultiplier = [2, 4, 8]; // times a parry's base points
    private static readonly double[] BloodlustScale = [1.5, 2, 3];
    private static readonly double[] BloodlustSeconds = [20, 30, 45];
    private static readonly double[] ParryBonus = [0.10, 0.18, 0.28];
    private static readonly double[] ParrySeconds = [25, 40, 60];

    public static IReadOnlyList<LootKind> AllKinds => Kinds;

    /// <summary>The chest's rarity: floor + 1 is the ordinary tier, floor + 2 is a treasure room, floor + 3 or more a boss's hoard.</summary>
    public static LootRarity RarityOf(Spawn chest) =>
        chest.Tier >= chest.Floor + 3 ? LootRarity.Epic : chest.Tier >= chest.Floor + 2 ? LootRarity.Rare : LootRarity.Common;

    /// <summary>The relative chance of each kind for a rarity, for tests and for showing odds.</summary>
    public static double Weight(LootRarity rarity, LootKind kind) => Weights[(int)rarity][Array.IndexOf(Kinds, kind)];

    public static Loot Roll(long seed, Spawn chest)
    {
        var rarity = RarityOf(chest);
        double[] w = Weights[(int)rarity];
        double pick = SeedHash.ToUnit(SeedHash.Hash(seed, chest.X, chest.Z, Salt + chest.Floor)) * w.Sum();
        int i = 0;
        while (i < w.Length - 1 && (pick >= w[i] || w[i] == 0))
        {
            pick -= w[i];
            i++;
        }
        return Make(Kinds[i], rarity);
    }

    /// <summary>The strength of a kind at a rarity.</summary>
    public static Loot Make(LootKind kind, LootRarity rarity)
    {
        int r = (int)rarity;
        return kind switch
        {
            LootKind.Health => new Loot(kind, rarity, HealthAmount[r], 0),
            LootKind.Cleanse => new Loot(kind, rarity, 1, 0),
            LootKind.StyleBoost => new Loot(kind, rarity, StyleMultiplier[r], 0),
            LootKind.Bloodlust => new Loot(kind, rarity, BloodlustScale[r], BloodlustSeconds[r]),
            LootKind.WideParry => new Loot(kind, rarity, ParryBonus[r], ParrySeconds[r]),
            _ => new Loot(kind, rarity, 1, 0),
        };
    }

    /// <summary>The bag item a chest's loot becomes, or null for a key, which is used on the spot.</summary>
    public static ItemKind? ItemFor(LootKind kind) => kind switch
    {
        LootKind.Health => ItemKind.VialOfBlood,
        LootKind.Cleanse => ItemKind.CleansingDrop,
        LootKind.StyleBoost => ItemKind.StyleShard,
        LootKind.Bloodlust => ItemKind.BloodlustDraught,
        LootKind.WideParry => ItemKind.OpenGuardTonic,
        _ => null,
    };

    /// <summary>How many items a chest holds. Better chests hold more of the same remedy; a cleansing drop is one whatever the chest.</summary>
    public static int ItemCount(Loot loot) => loot.Kind switch
    {
        LootKind.Cleanse or LootKind.Key => 1,
        _ => (int)loot.Rarity + 1,
    };

    public static string Name(LootKind kind) => kind switch
    {
        LootKind.Health => "Vial of blood",
        LootKind.Cleanse => "Cleansing drop",
        LootKind.StyleBoost => "Style shard",
        LootKind.Bloodlust => "Bloodlust",
        LootKind.WideParry => "Open guard",
        _ => "Spare key",
    };

    /// <summary>The line for the HUD banner. <paramref name="applied"/> is what the loot really did (health healed, hard damage removed, style points).</summary>
    public static string Announce(Loot loot, double applied) => loot.Kind switch
    {
        LootKind.Health => $"{Name(loot.Kind)}  +{applied:F0} health",
        LootKind.Cleanse => $"{Name(loot.Kind)}  hard damage cleansed",
        LootKind.StyleBoost => $"{Name(loot.Kind)}  +{applied:F0} style",
        LootKind.Bloodlust => $"{Name(loot.Kind)}  x{loot.Amount:0.#} blood for {loot.Seconds:F0}s",
        LootKind.WideParry => $"{Name(loot.Kind)}  +{loot.Amount * 1000:F0}ms parry for {loot.Seconds:F0}s",
        _ => $"{Name(loot.Kind)}  +{loot.Amount:F0} key",
    };
}

/// <summary>
/// Timed perks from loot. They only ever add: each is read through a small hook (<see cref="PlayerVitals.HitHealScale"/>, the parry
/// window) so the sim's own rules are untouched when none is active. Taking the same perk again refreshes it to the stronger and longer of the two.
/// </summary>
public sealed class Perks
{
    private double _bloodSeconds, _bloodScale = 1;
    private double _parrySeconds, _parryBonus;

    /// <summary>Multiplier on blood healed by hits (1 when no perk).</summary>
    public double HealScale => _bloodSeconds > 0 ? _bloodScale : 1;

    /// <summary>Seconds added to the parry window (0 when no perk).</summary>
    public double ParryWindowBonus => _parrySeconds > 0 ? _parryBonus : 0;

    public double BloodlustLeft => _bloodSeconds;
    public double WideParryLeft => _parrySeconds;

    public void Grant(Loot loot)
    {
        switch (loot.Kind)
        {
            case LootKind.Bloodlust:
                _bloodScale = _bloodSeconds > 0 ? Math.Max(_bloodScale, loot.Amount) : loot.Amount;
                _bloodSeconds = Math.Max(_bloodSeconds, loot.Seconds);
                break;
            case LootKind.WideParry:
                _parryBonus = _parrySeconds > 0 ? Math.Max(_parryBonus, loot.Amount) : loot.Amount;
                _parrySeconds = Math.Max(_parrySeconds, loot.Seconds);
                break;
        }
    }

    public void Tick(double dt)
    {
        _bloodSeconds = Math.Max(0, _bloodSeconds - dt);
        _parrySeconds = Math.Max(0, _parrySeconds - dt);
    }

    public void Clear() => _bloodSeconds = _parrySeconds = 0;
}
