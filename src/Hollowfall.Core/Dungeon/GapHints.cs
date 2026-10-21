using Hollowfall.Core.Combat;
using Hollowfall.Core.Localization;

namespace Hollowfall.Core.Dungeon;

/// <summary>The lines that teach the player what a gap is for. Text lives in the string table so it can be tested; the host only shows it.</summary>
public static class GapHints
{
    /// <summary>
    /// What to say the first time the player stands at the lip of a gap, with the names of their real portal bindings
    /// (<paramref name="teal"/> fires the portal across, <paramref name="violet"/> the one beside them).
    /// </summary>
    public static string AtTheEdge(LinkKind kind, string teal, string violet, Loc? loc = null) => (loc ?? new Loc()).Tr(
        kind == LinkKind.ShaftGap ? "gap.alcove" : "gap.edge", teal, violet);

    /// <summary>What to say the first time the player falls in: it is not the end, and there is a better way.</summary>
    public static string AfterTheFirstFall(string teal, string violet, Loc? loc = null) => (loc ?? new Loc()).Tr("gap.afterFall", teal, violet);

    /// <summary>The short banner for a fall.</summary>
    public static string FallBanner(PlayerFell fell, Loc? loc = null)
    {
        var text = loc ?? new Loc();
        return fell.Hazard switch
        {
            GapHazard.Spikes => text.Tr("gap.spikes", $"{fell.Damage:F0}"),
            GapHazard.Water => text.Tr("gap.splash"),
            _ => text.Tr("gap.fellBare", $"{fell.Damage:F0}"),
        };
    }
}
