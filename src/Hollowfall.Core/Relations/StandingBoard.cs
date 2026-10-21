using Hollowfall.Core.Combat;
using Hollowfall.Core.Localization;

namespace Hollowfall.Core.Relations;

/// <summary>One line of the Standing screen.</summary>
/// <param name="Fraction">Where the standing sits on the bar, 0 (Hated) to 1 (Revered).</param>
public sealed record StandingRow(Faction Faction, string Name, string Group, double Value, ReputationTier Tier, double Fraction, string Blurb, bool Fixed);

/// <summary>
/// What the host shows about relationships, decided here so it is tested: the rows of the Standing screen, the text of the toast
/// when a tier changes, the recent-changes lines and the colour and visibility of the little pip over a creature.
/// </summary>
public static class StandingBoard
{
    /// <summary>The pip fades out beyond this many blocks.</summary>
    public const double PipRange = 22;

    /// <summary>Every faction the player has a standing with, in enum order (the Hollow first, then the Folk, wanderers and vermin).</summary>
    public static IReadOnlyList<StandingRow> Rows(Reputation reputation) =>
        FactionGraph.Standable.Select(f =>
        {
            var info = FactionGraph.Info(f);
            double v = reputation.Value(f);
            return new StandingRow(f, info.Name, info.Group, v, reputation.Tier(f), (v - ReputationTiers.Min) / (ReputationTiers.Max - ReputationTiers.Min), info.Blurb, info.Fixed);
        }).ToList();

    /// <summary>"The Brownies now regard you as Wary", or "You are now Revered by the Selkies" for the good ones.</summary>
    public static string Toast(ReputationChanged e, Loc? loc = null)
    {
        string name = Capital(FactionGraph.Info(e.Faction).Name);
        return (loc ?? new Loc()).Tr(e.New > e.Old ? "standing.better" : "standing.worse", name, ReputationTiers.Label(e.New));
    }

    /// <summary>The most recent standing changes, newest first, as short lines ("-20 the Brownies: Killed a Brownie").</summary>
    public static IReadOnlyList<string> RecentLines(Reputation reputation) =>
        reputation.RecentChanges.Select(n => $"{(n.Delta >= 0 ? "+" : "")}{n.Delta:0.#} {FactionGraph.Info(n.Faction).Name}: {n.Reason}").ToList();

    /// <summary>The pip colour (hex RGB) for a cue, or null for no pip. Green friendly, yellow wary, red hostile, blue allied.</summary>
    public static uint? PipColour(Cue cue) => cue switch
    {
        Cue.Friendly => 0x58d36b,
        Cue.Wary => 0xf0c73c,
        Cue.Hostile => 0xe5483c,
        Cue.Allied => 0x4aa8ff,
        _ => null,
    };

    /// <summary>Whether a creature shows a pip: it must have been noticed, have a cue, be alive and be within range.</summary>
    public static bool ShowsPip(Enemy e, double distance) =>
        !e.Dead && e.Mind is { } m && m.Cue != Cue.None && distance <= PipRange;

    /// <summary>How the creature feels, in a few words (for the debug overlay and the screen reader).</summary>
    public static string Describe(Mind m) => m.Disposition switch
    {
        Disposition.Passive => "shy",
        Disposition.Skittish => "skittish",
        Disposition.Neutral => "calm",
        Disposition.Territorial => "guarding its ground",
        Disposition.Curious => "curious",
        Disposition.Aggressive => "hostile",
        _ => "on your side",
    };

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
