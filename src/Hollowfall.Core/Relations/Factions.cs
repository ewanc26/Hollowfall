namespace Hollowfall.Core.Relations;

/// <summary>Who an entity belongs to. The Delver is the player; every other faction has a standing with the player.</summary>
public enum Faction
{
    Delver,
    Husks,
    Spitters,
    Wardens,
    Brownies,
    Selkies,
    Kelpies,
    Wanderers,
    Vermin,
}

/// <summary>How one faction regards another, from worst to best. The numeric order is the order of warmth.</summary>
public enum Stance
{
    Enemy,
    Hostile,
    Wary,
    Neutral,
    Friendly,
    Ally,
}

/// <summary>A kind of thing the player does to a faction that other factions care about.</summary>
public enum RelationAct
{
    /// <summary>Hurt but did not kill.</summary>
    Harm,
    Kill,
    /// <summary>Healed, fed or gave something.</summary>
    Help,
}

/// <summary>Static facts about a faction. <see cref="Fixed"/> factions never change their stance with the player (the Hollow is always hostile).</summary>
public sealed record FactionInfo(Faction Faction, string Name, string Group, Stance TowardPlayer, bool Fixed, string Blurb)
{
    /// <summary>Where the player's standing with this faction begins, taken from its stance.</summary>
    public double StartStanding => FactionGraph.StartStanding(TowardPlayer);
}

/// <summary>One line of the ripple table: acting on a faction moves <see cref="Affected"/> by <see cref="Weight"/> times the act's size.</summary>
public readonly record struct Ripple(Faction Affected, double Weight);

/// <summary>
/// The faction graph, as data: the stance every faction takes toward every other, and the ripple table that says what harming,
/// killing or helping a faction does to everyone's opinion of the player. All numbers live here so tests can pin them and
/// playtesting changes constants, not logic.
/// </summary>
public static class FactionGraph
{
    // ---- tuning ---------------------------------------------------------------------------------

    /// <summary>Standing change for killing a member of a faction (before the ripple weight), for hurting one, and for helping one.</summary>
    public const double KillSize = -20;
    public const double HarmSize = -3;
    public const double HelpSize = 12;

    /// <summary>Opinion gained second-hand (through a ripple, not by directly helping) can never lift a faction above this.</summary>
    public const double RippleCeiling = 40;

    public static IReadOnlyList<FactionInfo> All { get; } =
    [
        new(Faction.Delver, "the Delver", "Delver", Stance.Ally, true, "You."),
        new(Faction.Husks, "the Husks", "Hollow", Stance.Enemy, true, "Hunched, hungry shells. They chase anything warm."),
        new(Faction.Spitters, "the Spitters", "Hollow", Stance.Enemy, true, "Floating bulbs that spit fire from a distance."),
        new(Faction.Wardens, "the Wardens", "Hollow", Stance.Enemy, true, "Armoured guardians of the Hollow's deepest rooms."),
        new(Faction.Brownies, "the Brownies", "Folk", Stance.Neutral, false, "Shy household spirits. They fear the Hollow and are kind to those who fight it."),
        new(Faction.Selkies, "the Selkies", "Folk", Stance.Neutral, false, "Seal-folk of still water. Wary, quick to flee, loyal once they trust you."),
        new(Faction.Kelpies, "the Kelpies", "Folk", Stance.Hostile, false, "Water-horses that wait calmly at the pool's edge, then drown whoever comes close."),
        new(Faction.Wanderers, "the Wanderers", "Wanderers", Stance.Neutral, false, "Lone travellers, like the Wulver, who owe the Hollow nothing."),
        new(Faction.Vermin, "the Vermin", "Vermin", Stance.Neutral, false, "Hares and strays. They keep the dungeon feeling lived in, and run from trouble."),
    ];

    /// <summary>The factions the player has a standing with (everyone but the player).</summary>
    public static IEnumerable<Faction> Standable => Enum.GetValues<Faction>().Where(f => f != Faction.Delver);

    public static FactionInfo Info(Faction f) => All[(int)f];

    // ---- stances --------------------------------------------------------------------------------

    private const Stance E = Stance.Enemy, H = Stance.Hostile, W = Stance.Wary, N = Stance.Neutral, F = Stance.Friendly, A = Stance.Ally;

    // Row = who is looking, column = who they look at. Order: Delver, Husks, Spitters, Wardens, Brownies, Selkies, Kelpies, Wanderers, Vermin.
    private static readonly Stance[,] Matrix =
    {
        /* Delver    */ { A, E, E, E, N, N, H, N, N },
        /* Husks     */ { E, A, A, A, H, H, N, W, N },
        /* Spitters  */ { E, A, A, A, H, H, N, W, N },
        /* Wardens   */ { E, A, A, A, H, H, N, W, N },
        /* Brownies  */ { N, E, E, E, A, F, W, N, F },
        /* Selkies   */ { N, E, E, E, F, A, W, F, N },
        /* Kelpies   */ { H, N, N, N, W, W, A, N, W },
        /* Wanderers */ { N, H, H, H, N, N, W, A, N },
        /* Vermin    */ { N, W, W, W, N, N, W, N, A },
    };

    /// <summary>How <paramref name="from"/> regards <paramref name="to"/> before the player is involved.</summary>
    public static Stance Between(Faction from, Faction to) => Matrix[(int)from, (int)to];

    /// <summary>Where a faction's standing with the player starts, by its stance toward them.</summary>
    public static double StartStanding(Stance stance) => stance switch
    {
        Stance.Ally => 55,
        Stance.Friendly => 22,
        Stance.Neutral => 0,
        Stance.Wary => -20,
        Stance.Hostile => -50,
        _ => -90,
    };

    // ---- ripples --------------------------------------------------------------------------------

    // Weight times the act's size is the change. A negative weight on a negative act is a gain: the Folk fear the Hollow,
    // so a dead Husk makes them a little warmer, while the rest of the Hollow takes offence.
    private static readonly Dictionary<Faction, Ripple[]> KillRipples = new()
    {
        [Faction.Husks] = [new(Faction.Husks, 1), new(Faction.Spitters, 0.4), new(Faction.Wardens, 0.4), new(Faction.Brownies, -0.25), new(Faction.Selkies, -0.25), new(Faction.Wanderers, -0.1)],
        [Faction.Spitters] = [new(Faction.Spitters, 1), new(Faction.Husks, 0.4), new(Faction.Wardens, 0.4), new(Faction.Brownies, -0.25), new(Faction.Selkies, -0.25), new(Faction.Wanderers, -0.1)],
        [Faction.Wardens] = [new(Faction.Wardens, 1), new(Faction.Husks, 0.5), new(Faction.Spitters, 0.5), new(Faction.Brownies, -0.4), new(Faction.Selkies, -0.4), new(Faction.Wanderers, -0.2)],
        // Harm a brownie and the Folk turn on you; the Hollow does not care.
        [Faction.Brownies] = [new(Faction.Brownies, 1), new(Faction.Selkies, 0.8), new(Faction.Kelpies, 0.2), new(Faction.Wanderers, 0.15)],
        [Faction.Selkies] = [new(Faction.Selkies, 1), new(Faction.Brownies, 0.8), new(Faction.Kelpies, 0.2), new(Faction.Wanderers, 0.15)],
        [Faction.Kelpies] = [new(Faction.Kelpies, 1), new(Faction.Brownies, -0.3), new(Faction.Selkies, -0.3), new(Faction.Wanderers, -0.1)],
        [Faction.Wanderers] = [new(Faction.Wanderers, 1), new(Faction.Brownies, 0.75), new(Faction.Selkies, 0.75), new(Faction.Kelpies, 0.2), new(Faction.Vermin, 0.1)],
        [Faction.Vermin] = [new(Faction.Vermin, 1), new(Faction.Wanderers, 0.1), new(Faction.Brownies, 0.05)],
    };

    // Helping someone warms their friends and cools nobody.
    private static readonly Dictionary<Faction, Ripple[]> HelpRipples = new()
    {
        [Faction.Husks] = [],
        [Faction.Spitters] = [],
        [Faction.Wardens] = [],
        [Faction.Brownies] = [new(Faction.Brownies, 1), new(Faction.Selkies, 0.3), new(Faction.Wanderers, 0.1)],
        [Faction.Selkies] = [new(Faction.Selkies, 1), new(Faction.Brownies, 0.3), new(Faction.Wanderers, 0.1)],
        [Faction.Kelpies] = [new(Faction.Kelpies, 1)],
        [Faction.Wanderers] = [new(Faction.Wanderers, 1), new(Faction.Brownies, 0.1), new(Faction.Selkies, 0.1)],
        [Faction.Vermin] = [new(Faction.Vermin, 1), new(Faction.Wanderers, 0.1)],
    };

    /// <summary>The size of one act, before ripples. Negative hurts standing.</summary>
    public static double Size(RelationAct act) => act switch
    {
        RelationAct.Kill => KillSize,
        RelationAct.Harm => HarmSize,
        _ => HelpSize,
    };

    /// <summary>
    /// Who is affected when the player does <paramref name="act"/> to a member of <paramref name="target"/>, and by how much of
    /// the act's size. A weight of 1 is the target faction itself; a negative weight means the faction approves of the act.
    /// Hurting and killing share one table (a hurt is a smaller kill).
    /// </summary>
    public static IReadOnlyList<Ripple> Ripples(RelationAct act, Faction target)
    {
        var table = act == RelationAct.Help ? HelpRipples : KillRipples;
        return table.TryGetValue(target, out var list) ? list : [];
    }

    /// <summary>Whether the ripple table has a row for a faction under an act (everyone but the player does).</summary>
    public static bool HasRipples(RelationAct act, Faction target) => (act == RelationAct.Help ? HelpRipples : KillRipples).ContainsKey(target);
}
