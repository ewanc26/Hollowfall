using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Localization;
using Hollowfall.Core.Physics;
using Hollowfall.Core.Relations;
using Xunit;

namespace Hollowfall.Tests;

/// <summary>
/// The gap captions, fall banners and standing toasts are built in the string table, so every language goes through <see cref="Loc"/>.
/// English must read exactly as it did before, and Gàidhlig must show English until a speaker has checked a line.
/// </summary>
public class HudTextRoutingTests
{
    [Fact]
    public void GapCaptionsKeepTheirEnglish()
    {
        Assert.Equal("A gap. Fire a portal across it (Q), then another beside you (E), and step through.", GapHints.AtTheEdge(LinkKind.PortalGap, "Q", "E"));
        Assert.Equal("A high alcove. Fire a portal up into it (Q), then another beside you (E), and step through.", GapHints.AtTheEdge(LinkKind.ShaftGap, "Q", "E"));
        Assert.Equal("You are set back at the edge. Fire a portal across the gap instead (Q, then E).", GapHints.AfterTheFirstFall("Q", "E"));
    }

    [Theory]
    [InlineData(GapHazard.Bare, "FELL  -8")]
    [InlineData(GapHazard.Spikes, "SPIKES  -8")]
    [InlineData(GapHazard.Water, "SPLASH  climbing out")]
    public void FallBannersKeepTheirEnglish(GapHazard hazard, string expected)
    {
        Assert.Equal(expected, GapHints.FallBanner(new PlayerFell(hazard, 8, true)));
    }

    [Fact]
    public void StandingToastKeepsItsEnglish()
    {
        Assert.Equal("The Brownies now regard you as Wary", StandingBoard.Toast(new ReputationChanged(Faction.Brownies, ReputationTier.Neutral, ReputationTier.Wary), new Loc()));
        Assert.Equal("The Selkies think better of you: Friendly", StandingBoard.Toast(new ReputationChanged(Faction.Selkies, ReputationTier.Neutral, ReputationTier.Friendly), new Loc()));
    }

    [Fact]
    public void GaelicShowsTheEnglishForLinesNotYetChecked()
    {
        var gaelic = new Loc(Language.Gaelic);
        foreach (var key in new[] { "toast.omen", "toast.boon", "standing.better", "standing.worse", "gap.edge", "gap.alcove", "gap.afterFall", "gap.fellBare", "gap.spikes", "gap.splash" })
        {
            var entry = LocTable.Find(key);
            Assert.NotNull(entry);
            Assert.Null(entry!.Gaelic);
            Assert.Equal(new Loc(Language.English).Tr(key, "x", "y"), gaelic.Tr(key, "x", "y"));
        }
    }

    [Fact]
    public void EveryRoutedKeyIsInTheTable()
    {
        Assert.DoesNotContain(new[] { "toast.omen", "toast.boon", "standing.better", "standing.worse", "gap.edge", "gap.alcove", "gap.afterFall", "gap.fellBare", "gap.spikes", "gap.splash" },
            key => !LocTable.Has(key));
    }

    [Fact]
    public void ALanguageChangeReachesTheCaptions()
    {
        var loc = new Loc(Language.English);
        var english = GapHints.AtTheEdge(LinkKind.PortalGap, "Q", "E", loc);
        loc.Language = Language.Gaelic;
        Assert.Equal(english, GapHints.AtTheEdge(LinkKind.PortalGap, "Q", "E", loc));
    }
}
