using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;

namespace Hollowfall.Core.Relations;

/// <summary>What an individual does about the player right now.</summary>
public enum Disposition
{
    /// <summary>Flees, ignores, never attacks.</summary>
    Passive,

    /// <summary>Flees when hurt or when approached fast.</summary>
    Skittish,

    /// <summary>Ignores the player until provoked, then fights until it forgets.</summary>
    Neutral,

    /// <summary>Attacks anyone inside its own radius, and only there.</summary>
    Territorial,

    /// <summary>Follows at a distance and may become friendly.</summary>
    Curious,

    /// <summary>Attacks on sight.</summary>
    Aggressive,

    /// <summary>Fights on the player's side.</summary>
    Allied,
}

/// <summary>Personality, 0 to 1, drawn once from the seed so two of a kind differ.</summary>
public readonly record struct Traits(double Bravery, double Grudge, double Curiosity)
{
    public static readonly Traits Middling = new(0.5, 0.5, 0.5);

    /// <summary>Traits for the individual standing at a spawn point: a pure function of the seed, the kind and the place.</summary>
    public static Traits For(long seed, SpeciesInfo species, int x, int y, int z)
    {
        var rng = new SeededRandom(seed, x * 7919 + z, y * 31 + (int)species.Kind, 930);
        double Pick((double Lo, double Hi) r) => r.Lo + (r.Hi - r.Lo) * rng.NextDouble();
        return new Traits(Pick(species.Bravery), Pick(species.Grudge), Pick(species.Curiosity));
    }
}

/// <summary>What a kind of creature is by nature. The player's standing and the individual's memory move it away from this.</summary>
public sealed record SpeciesInfo(
    EnemyKind Kind,
    Faction Faction,
    string Name,
    Disposition Natural,
    bool Pacifist,
    bool CanAlly,
    bool CanFollow,
    (double Lo, double Hi) Bravery,
    (double Lo, double Hi) Grudge,
    (double Lo, double Hi) Curiosity,
    double AggroRange = 24,
    double TerritoryRadius = 6)
{
    /// <summary>
    /// The Hollow's own are aggressive by nature and by faction, with no range limit: they ignore standing and memory and behave
    /// exactly as enemies always have.
    /// </summary>
    public bool AlwaysHostile => Natural == Disposition.Aggressive && FactionGraph.Info(Faction).Fixed;

    private static readonly IReadOnlyList<SpeciesInfo> Table =
    [
        new(EnemyKind.Husk, Faction.Husks, "Husk", Disposition.Aggressive, false, false, false, (0.7, 1), (0.5, 1), (0, 0.2), double.PositiveInfinity),
        new(EnemyKind.Spitter, Faction.Spitters, "Spitter", Disposition.Aggressive, false, false, false, (0.7, 1), (0.5, 1), (0, 0.2), double.PositiveInfinity),
        new(EnemyKind.Warden, Faction.Wardens, "Warden", Disposition.Aggressive, false, false, false, (0.9, 1), (0.8, 1), (0, 0.1), double.PositiveInfinity),
        new(EnemyKind.Brownie, Faction.Brownies, "Brownie", Disposition.Passive, true, false, true, (0.0, 0.3), (0.5, 0.9), (0.5, 1)),
        new(EnemyKind.Selkie, Faction.Selkies, "Selkie", Disposition.Skittish, false, true, true, (0.3, 0.7), (0.3, 0.8), (0.3, 0.9)),
        new(EnemyKind.Kelpie, Faction.Kelpies, "Kelpie", Disposition.Territorial, false, false, false, (0.6, 1), (0.4, 0.9), (0, 0.3), 24, 7),
        new(EnemyKind.BeanNighe, Faction.Wanderers, "Bean Nighe", Disposition.Passive, true, false, false, (0.0, 0.2), (0.6, 1), (0, 0.3)),
        new(EnemyKind.Hare, Faction.Vermin, "Hare", Disposition.Skittish, true, false, false, (0.0, 0.3), (0.0, 0.3), (0.2, 0.8)),
    ];

    public static SpeciesInfo Of(EnemyKind kind) => Table[(int)kind];

    public static IReadOnlyList<SpeciesInfo> All => Table;
}
