using Hollowfall.Core.Relations;

namespace Hollowfall.Tests;

public class FactionGraphTests
{
    private static IEnumerable<Faction> All => Enum.GetValues<Faction>();

    [Fact]
    public void EveryFactionHasInfoInEnumOrderAndAUniqueName()
    {
        Assert.Equal(All.Count(), FactionGraph.All.Count);
        for (int i = 0; i < FactionGraph.All.Count; i++) Assert.Equal((Faction)i, FactionGraph.All[i].Faction);
        Assert.Equal(FactionGraph.All.Count, FactionGraph.All.Select(f => f.Name).Distinct().Count());
        Assert.All(FactionGraph.All, f => Assert.False(string.IsNullOrWhiteSpace(f.Blurb)));
    }

    [Fact]
    public void EveryFactionIsAllyOfItself()
    {
        foreach (var f in All) Assert.Equal(Stance.Ally, FactionGraph.Between(f, f));
    }

    [Fact]
    public void AllyIsMutualAndOtherStancesAreNeverWildlyOneSided()
    {
        foreach (var a in All)
            foreach (var b in All)
            {
                var ab = FactionGraph.Between(a, b);
                var ba = FactionGraph.Between(b, a);
                Assert.True((ab == Stance.Ally) == (ba == Stance.Ally), $"{a}->{b} is {ab} but {b}->{a} is {ba}");
                Assert.True(Math.Abs((int)ab - (int)ba) <= 3, $"{a} and {b} disagree too much: {ab} vs {ba}");
            }
    }

    [Fact]
    public void TheHollowStandsTogetherAgainstTheDelverAndIsFixed()
    {
        var hollow = new[] { Faction.Husks, Faction.Spitters, Faction.Wardens };
        foreach (var a in hollow)
        {
            Assert.True(FactionGraph.Info(a).Fixed);
            Assert.Equal(Stance.Enemy, FactionGraph.Info(a).TowardPlayer);
            Assert.Equal(Stance.Enemy, FactionGraph.Between(a, Faction.Delver));
            foreach (var b in hollow) Assert.Equal(Stance.Ally, FactionGraph.Between(a, b));
        }
        Assert.All(new[] { Faction.Brownies, Faction.Selkies, Faction.Kelpies, Faction.Wanderers, Faction.Vermin }, f => Assert.False(FactionGraph.Info(f).Fixed));
    }

    [Fact]
    public void TheFolkFearTheHollow()
    {
        foreach (var folk in new[] { Faction.Brownies, Faction.Selkies })
            foreach (var h in new[] { Faction.Husks, Faction.Spitters, Faction.Wardens })
                Assert.True(FactionGraph.Between(folk, h) <= Stance.Hostile);
    }

    [Fact]
    public void StartingStandingMatchesTheStanceAndTier()
    {
        Assert.Equal(ReputationTier.Hated, ReputationTiers.Plain(FactionGraph.Info(Faction.Husks).StartStanding));
        Assert.Equal(ReputationTier.Hostile, ReputationTiers.Plain(FactionGraph.Info(Faction.Kelpies).StartStanding));
        Assert.Equal(ReputationTier.Neutral, ReputationTiers.Plain(FactionGraph.Info(Faction.Brownies).StartStanding));
        var stances = Enum.GetValues<Stance>();
        double last = double.MinValue;
        foreach (var s in stances)
        {
            Assert.True(FactionGraph.StartStanding(s) > last, "warmer stances start higher");
            last = FactionGraph.StartStanding(s);
        }
    }

    [Theory]
    [InlineData(RelationAct.Kill)]
    [InlineData(RelationAct.Harm)]
    [InlineData(RelationAct.Help)]
    public void RipplesAreBoundedKnownAndNeverTouchTheDelver(RelationAct act)
    {
        foreach (var target in FactionGraph.Standable)
        {
            Assert.True(FactionGraph.HasRipples(act, target), $"{act} {target}");
            var ripples = FactionGraph.Ripples(act, target);
            Assert.Equal(ripples.Count, ripples.Select(r => r.Affected).Distinct().Count());
            foreach (var r in ripples)
            {
                Assert.True(Enum.IsDefined(r.Affected));
                Assert.NotEqual(Faction.Delver, r.Affected);
                Assert.InRange(r.Weight, -1.0, 1.0);
                Assert.True(Math.Abs(FactionGraph.Size(act) * r.Weight) <= 20, "no single ripple exceeds the size of a kill");
            }
        }
        Assert.Empty(FactionGraph.Ripples(act, Faction.Delver));
    }

    [Fact]
    public void HurtingYourTargetAlwaysCostsStandingAndHelpingAlwaysEarnsIt()
    {
        foreach (var target in FactionGraph.Standable)
        {
            foreach (var act in new[] { RelationAct.Kill, RelationAct.Harm })
            {
                var own = FactionGraph.Ripples(act, target).Single(r => r.Affected == target);
                Assert.Equal(1.0, own.Weight);
                Assert.True(FactionGraph.Size(act) * own.Weight < 0);
            }
            var help = FactionGraph.Ripples(RelationAct.Help, target);
            if (help.Count > 0) Assert.True(FactionGraph.Size(RelationAct.Help) * help.Single(r => r.Affected == target).Weight > 0);
        }
        Assert.True(Math.Abs(FactionGraph.KillSize) > Math.Abs(FactionGraph.HarmSize));
    }

    [Fact]
    public void RipplesFollowTheSocialGraph()
    {
        // Killing a Husk: the Hollow takes offence, the Folk are glad.
        double husk = FactionGraph.KillSize;
        Assert.True(husk * FactionGraph.Ripples(RelationAct.Kill, Faction.Husks).Single(r => r.Affected == Faction.Spitters).Weight < 0);
        Assert.True(husk * FactionGraph.Ripples(RelationAct.Kill, Faction.Husks).Single(r => r.Affected == Faction.Brownies).Weight > 0);
        // Killing a brownie: the Folk are furious, the Hollow does not care.
        var brownie = FactionGraph.Ripples(RelationAct.Kill, Faction.Brownies);
        Assert.True(brownie.Single(r => r.Affected == Faction.Selkies).Weight > 0.5);
        Assert.DoesNotContain(brownie, r => r.Affected is Faction.Husks or Faction.Spitters or Faction.Wardens);
    }

    [Fact]
    public void EveryFactionIsReachableThroughTheRipplesOrIsTheirTarget()
    {
        var reached = new HashSet<Faction>();
        foreach (var act in Enum.GetValues<RelationAct>())
            foreach (var t in FactionGraph.Standable)
                foreach (var r in FactionGraph.Ripples(act, t)) reached.Add(r.Affected);
        foreach (var f in FactionGraph.Standable) Assert.Contains(f, reached);
    }
}

public class ReputationTierTests
{
    [Fact]
    public void TiersCoverTheScaleInOrder()
    {
        var tiers = Enum.GetValues<ReputationTier>();
        for (int i = 1; i < tiers.Length; i++) Assert.True(ReputationTiers.LowerBound(tiers[i]) > ReputationTiers.LowerBound(tiers[i - 1]));
        Assert.Equal(ReputationTier.Hated, ReputationTiers.Plain(-100));
        Assert.Equal(ReputationTier.Revered, ReputationTiers.Plain(100));
        Assert.Equal(ReputationTier.Neutral, ReputationTiers.Plain(0));
        for (double v = -100; v <= 100; v += 0.5)
        {
            var t = ReputationTiers.Plain(v);
            Assert.True(v >= ReputationTiers.LowerBound(t));
            if (t < ReputationTier.Revered) Assert.True(v < ReputationTiers.LowerBound(t + 1));
        }
    }

    [Fact]
    public void RisingNeedsTheMarginAndFallingNeedsItToo()
    {
        double edge = ReputationTiers.LowerBound(ReputationTier.Friendly);
        Assert.Equal(ReputationTier.Neutral, ReputationTiers.Resolve(edge + 1, ReputationTier.Neutral));
        Assert.Equal(ReputationTier.Neutral, ReputationTiers.Resolve(edge + ReputationTiers.Hysteresis - 0.01, ReputationTier.Neutral));
        Assert.Equal(ReputationTier.Friendly, ReputationTiers.Resolve(edge + ReputationTiers.Hysteresis, ReputationTier.Neutral));
        Assert.Equal(ReputationTier.Friendly, ReputationTiers.Resolve(edge - 1, ReputationTier.Friendly));
        Assert.Equal(ReputationTier.Friendly, ReputationTiers.Resolve(edge - ReputationTiers.Hysteresis, ReputationTier.Friendly));
        Assert.Equal(ReputationTier.Neutral, ReputationTiers.Resolve(edge - ReputationTiers.Hysteresis - 0.01, ReputationTier.Friendly));
    }

    [Fact]
    public void JitterAroundABoundaryNeverFlickers()
    {
        foreach (var tier in Enum.GetValues<ReputationTier>().Skip(1))
        {
            double edge = ReputationTiers.LowerBound(tier);
            var current = ReputationTiers.Plain(edge + 3); // just inside, within the margin
            int changes = 0;
            for (int i = 0; i < 200; i++)
            {
                double v = edge + (i % 2 == 0 ? 3 : -3);
                var next = ReputationTiers.Resolve(v, current);
                if (next != current) changes++;
                current = next;
            }
            Assert.Equal(0, changes);
        }
    }

    [Fact]
    public void ResolveIsIdempotentAndMovesAcrossSeveralTiersAtOnce()
    {
        for (double v = -100; v <= 100; v += 1)
            foreach (var start in Enum.GetValues<ReputationTier>())
            {
                var once = ReputationTiers.Resolve(v, start);
                Assert.Equal(once, ReputationTiers.Resolve(v, once));
            }
        Assert.Equal(ReputationTier.Revered, ReputationTiers.Resolve(100, ReputationTier.Hated));
        Assert.Equal(ReputationTier.Hated, ReputationTiers.Resolve(-100, ReputationTier.Revered));
    }

    [Fact]
    public void TierMapsToStances()
    {
        Assert.Equal(Stance.Enemy, ReputationTiers.ToStance(ReputationTier.Hated));
        Assert.Equal(Stance.Neutral, ReputationTiers.ToStance(ReputationTier.Neutral));
        Assert.Equal(Stance.Ally, ReputationTiers.ToStance(ReputationTier.Revered));
        var last = Stance.Enemy;
        foreach (var t in Enum.GetValues<ReputationTier>())
        {
            Assert.True(ReputationTiers.ToStance(t) >= last);
            last = ReputationTiers.ToStance(t);
        }
    }
}

public class ReputationTests
{
    [Fact]
    public void StartsAtEachFactionsStartingStandingAndResets()
    {
        var rep = new Reputation();
        foreach (var info in FactionGraph.All) Assert.Equal(info.StartStanding, rep.Value(info.Faction));
        rep.Apply(RelationAct.Kill, Faction.Brownies);
        Assert.NotEqual(0, rep.Value(Faction.Brownies));
        rep.Reset();
        Assert.Equal(0, rep.Value(Faction.Brownies));
        Assert.Empty(rep.RecentChanges);
    }

    [Fact]
    public void KillingABrownieSendsTheFolkDownAndLeavesTheHollowAlone()
    {
        var rep = new Reputation();
        var husks = rep.Value(Faction.Husks);
        var changes = rep.Apply(RelationAct.Kill, Faction.Brownies, 1, "test");
        Assert.Equal(-20, rep.Value(Faction.Brownies), 6);
        Assert.Equal(-16, rep.Value(Faction.Selkies), 6);
        Assert.Equal(husks, rep.Value(Faction.Husks));
        Assert.DoesNotContain(changes, c => c.Faction is Faction.Husks or Faction.Spitters or Faction.Wardens);
        Assert.Equal(ReputationTier.Wary, rep.Tier(Faction.Brownies));
    }

    [Fact]
    public void KillingAHuskWarmsTheFolkALittleButNeverPastTheCeiling()
    {
        var rep = new Reputation();
        rep.Apply(RelationAct.Kill, Faction.Husks);
        Assert.Equal(5, rep.Value(Faction.Brownies), 6);
        for (int i = 0; i < 200; i++) rep.Apply(RelationAct.Kill, Faction.Husks);
        Assert.Equal(FactionGraph.RippleCeiling, rep.Value(Faction.Brownies), 6);
        Assert.True(rep.Tier(Faction.Brownies) < ReputationTier.Revered);
        Assert.Equal(-100, rep.Value(Faction.Husks), 6);
    }

    [Fact]
    public void ARippleCeilingDoesNotPullDownSomeoneAlreadyAboveIt()
    {
        var rep = new Reputation();
        rep.Set(Faction.Brownies, 70);
        rep.Apply(RelationAct.Kill, Faction.Husks);
        Assert.Equal(70, rep.Value(Faction.Brownies), 6);
    }

    [Fact]
    public void HurtingSomeoneNeverMakesAnyoneLikeYouMore()
    {
        var rep = new Reputation();
        rep.Apply(RelationAct.Harm, Faction.Husks);
        Assert.Equal(0, rep.Value(Faction.Brownies));
        Assert.True(rep.Value(Faction.Spitters) < FactionGraph.Info(Faction.Spitters).StartStanding);
    }

    [Fact]
    public void HelpingRaisesYourOwnStandingPastTheCeiling()
    {
        var rep = new Reputation();
        for (int i = 0; i < 10; i++) rep.Apply(RelationAct.Help, Faction.Selkies);
        Assert.Equal(100, rep.Value(Faction.Selkies), 6);
        Assert.Equal(ReputationTier.Revered, rep.Tier(Faction.Selkies));
        Assert.Equal(36, rep.Value(Faction.Brownies), 6); // a friend of a friend: second-hand, under the ceiling
    }

    [Fact]
    public void ValuesAreClampedAndBadInputIsIgnored()
    {
        var rep = new Reputation();
        for (int i = 0; i < 20; i++) rep.Apply(RelationAct.Kill, Faction.Brownies);
        Assert.Equal(-100, rep.Value(Faction.Brownies));
        Assert.Empty(rep.Apply(RelationAct.Kill, Faction.Delver));
        Assert.Empty(rep.Apply(RelationAct.Kill, Faction.Brownies, double.NaN));
        Assert.Null(rep.Set(Faction.Selkies, double.NaN));
        rep.Set(Faction.Selkies, 5000);
        Assert.Equal(100, rep.Value(Faction.Selkies));
        rep.Set(Faction.Selkies, -5000);
        Assert.Equal(-100, rep.Value(Faction.Selkies));
    }

    [Fact]
    public void ChangesReportTiersAndTierChangesAreHysteretic()
    {
        var rep = new Reputation();
        var c = rep.Apply(RelationAct.Help, Faction.Vermin).Single(x => x.Faction == Faction.Vermin);
        Assert.Equal(ReputationTier.Neutral, c.OldTier);
        Assert.Equal(12, c.NewValue, 6);
        Assert.False(c.TierChanged); // 12 < 15 + 4
        c = rep.Apply(RelationAct.Help, Faction.Vermin).Single(x => x.Faction == Faction.Vermin);
        Assert.True(c.TierChanged);
        Assert.Equal(ReputationTier.Friendly, c.NewTier);
        // Dropping to 14 does not undo it, dropping below 11 does.
        rep.Set(Faction.Vermin, 24);
        rep.Apply(RelationAct.Kill, Faction.Vermin, 0.1); // -2
        Assert.Equal(ReputationTier.Friendly, rep.Tier(Faction.Vermin));
    }

    [Fact]
    public void StandingsDriftTowardNeutralExceptForTheHatedAndTheFixed()
    {
        var rep = new Reputation();
        rep.Set(Faction.Selkies, 30);
        rep.Set(Faction.Kelpies, -60);
        rep.Set(Faction.Vermin, -90);
        rep.Tick(100);
        Assert.Equal(28, rep.Value(Faction.Selkies), 6);
        Assert.Equal(-60 + 2, rep.Value(Faction.Kelpies), 6);
        Assert.Equal(-90, rep.Value(Faction.Vermin)); // hated: no forgiveness
        Assert.Equal(-90, rep.Value(Faction.Husks)); // fixed
        rep.Tick(100000);
        Assert.Equal(0, rep.Value(Faction.Selkies));
        Assert.Equal(0, rep.Value(Faction.Kelpies));
    }

    [Fact]
    public void DecayDropsTheTierOnlyAfterCrossingTheMargin()
    {
        var rep = new Reputation();
        rep.Set(Faction.Brownies, 20);
        Assert.Equal(ReputationTier.Friendly, rep.Tier(Faction.Brownies));
        var changes = new List<ReputationChange>();
        for (int i = 0; i < 5000; i++) changes.AddRange(rep.Tick(0.1));
        var drop = Assert.Single(changes);
        Assert.Equal(ReputationTier.Neutral, drop.NewTier);
        Assert.True(drop.NewValue < ReputationTiers.LowerBound(ReputationTier.Friendly) - ReputationTiers.Hysteresis + 0.1);
    }

    [Fact]
    public void RecentChangesKeepTheNewestFirstAndAreBounded()
    {
        var rep = new Reputation();
        for (int i = 0; i < 20; i++) rep.Apply(RelationAct.Kill, Faction.Vermin, 0.1, "n" + i, i);
        Assert.Equal(Reputation.MaxNotes, rep.RecentChanges.Count);
        Assert.Equal("n19", rep.RecentChanges[0].Reason);
    }

    [Fact]
    public void TheSameActsGiveTheSameStandingsEveryTime()
    {
        Reputation Run()
        {
            var r = new Reputation();
            r.Apply(RelationAct.Kill, Faction.Husks);
            r.Apply(RelationAct.Help, Faction.Selkies);
            r.Apply(RelationAct.Harm, Faction.Brownies);
            r.Tick(12.5);
            return r;
        }
        Assert.Equal(Run().Snapshot(), Run().Snapshot());
    }
}

public class RelationsStoreTests
{
    private static string Temp() => Path.Combine(Path.GetTempPath(), "hollowfall-relations-" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void RoundTripsStandingsPerSeed()
    {
        var rep = new Reputation();
        rep.Apply(RelationAct.Kill, Faction.Brownies);
        var store = new RelationsStore();
        Assert.True(store.Record("alpha", rep));
        var back = RelationsStore.FromJson(store.ToJson());
        Assert.Equal(1, back.SeedCount);
        foreach (var (f, v) in rep.Snapshot()) Assert.Equal(v, back.Standings("alpha")![f], 3);
        Assert.Equal(rep.Value(Faction.Brownies), back.Carry("alpha").Value(Faction.Brownies), 3);
        Assert.Equal(0, back.Carry("unknown").Value(Faction.Brownies));
    }

    [Fact]
    public void SavesAtomicallyToAnyPathAndLoadsItBack()
    {
        string path = Temp();
        try
        {
            var store = new RelationsStore();
            store.Record("s", new Reputation());
            Assert.True(store.TrySave(path));
            Assert.False(File.Exists(path + ".tmp"));
            Assert.Equal(1, RelationsStore.Load(path).SeedCount);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"seeds\": 5}")]
    [InlineData("{\"seeds\": {\"a\": 5}}")]
    [InlineData("{\"seeds\": {\"a\": {\"Brownies\": \"x\"}}}")]
    [InlineData("{\"seeds\": {\"a\": {\"Nobody\": 5, \"Delver\": 5}}}")]
    [InlineData("{\"seeds\": {\"a\": {\"Brownies\": 5, \"Brownies\": 6}}}")]
    [InlineData("{\"seeds\": {\"a\": {}, \"a\": {}}}")]
    [InlineData("{\"seeds\":")]
    public void DamagedFilesNeverThrowAndLoseOnlyTheBadParts(string json)
    {
        var store = RelationsStore.FromJson(json);
        Assert.Equal(0, store.SeedCount);
    }

    [Fact]
    public void ValuesAreClampedAndGoodEntriesSurviveBadNeighbours()
    {
        var store = RelationsStore.FromJson("{\"seeds\": {\"bad\": 3, \"good\": {\"Selkies\": 9999, \"Kelpies\": -9999, \"Husks\": 1e400, \"Vermin\": 4.5}}}");
        Assert.Equal(1, store.SeedCount);
        var s = store.Standings("good")!;
        Assert.Equal(100, s[Faction.Selkies]);
        Assert.Equal(-100, s[Faction.Kelpies]);
        Assert.Equal(4.5, s[Faction.Vermin]);
    }

    [Fact]
    public void SizesAreBounded()
    {
        var store = new RelationsStore();
        for (int i = 0; i < RelationsStore.MaxSeeds + 25; i++) store.Record("seed" + i, new Reputation());
        Assert.Equal(RelationsStore.MaxSeeds, store.SeedCount);
        Assert.Null(store.Standings("seed0"));
        Assert.NotNull(store.Standings("seed" + (RelationsStore.MaxSeeds + 24)));
        Assert.False(store.Record(new string('x', RelationsStore.MaxSeedLength + 1), new Reputation()));
        Assert.False(store.Record("", new Reputation()));
        Assert.Equal(RelationsStore.MaxSeeds, RelationsStore.FromJson(store.ToJson()).SeedCount);
        Assert.Equal(0, RelationsStore.FromJson(new string(' ', RelationsStore.MaxFileBytes + 1)).SeedCount);
    }

    [Fact]
    public void MissingAndUnreadableFilesGiveAnEmptyStore()
    {
        Assert.Equal(0, RelationsStore.Load(Temp()).SeedCount);
        string dir = Path.Combine(Path.GetTempPath(), "hollowfall-relations-dir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try { Assert.Equal(0, RelationsStore.Load(dir).SeedCount); }
        finally { Directory.Delete(dir); }
    }

    [Fact]
    public void TheRelationsPathComesFromTheDataFolderAndHonoursItsEnvironmentVariable()
    {
        var home = new Hollowfall.Core.Storage.DataHome("/data/hollowfall", n => n == "HOLLOWFALL_RELATIONS" ? "/tmp/r.json" : null);
        Assert.Equal("/tmp/r.json", home.RelationsPath);
        Assert.Equal(Path.Combine("/data/hollowfall", "relations.json"), new Hollowfall.Core.Storage.DataHome("/data/hollowfall", _ => null).RelationsPath);
    }
}
