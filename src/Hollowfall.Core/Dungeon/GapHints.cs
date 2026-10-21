using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Dungeon;

/// <summary>The lines that teach the player what a gap is for. Text lives here so it can be tested; the host only shows it.</summary>
public static class GapHints
{
    /// <summary>
    /// What to say the first time the player stands at the lip of a gap, with the names of their real portal bindings
    /// (<paramref name="teal"/> fires the portal across, <paramref name="violet"/> the one beside them).
    /// </summary>
    public static string AtTheEdge(LinkKind kind, string teal, string violet) => kind == LinkKind.ShaftGap
        ? $"A high alcove. Fire a portal up into it ({teal}), then another beside you ({violet}), and step through."
        : $"A gap. Fire a portal across it ({teal}), then another beside you ({violet}), and step through.";

    /// <summary>What to say the first time the player falls in: it is not the end, and there is a better way.</summary>
    public static string AfterTheFirstFall(string teal, string violet) =>
        $"You are set back at the edge. Fire a portal across the gap instead ({teal}, then {violet}).";

    /// <summary>The short banner for a fall.</summary>
    public static string FallBanner(PlayerFell fell) => fell.Hazard switch
    {
        GapHazard.Spikes => $"SPIKES  -{fell.Damage:F0}",
        GapHazard.Water => "SPLASH  climbing out",
        _ => $"FELL  -{fell.Damage:F0}",
    };
}
