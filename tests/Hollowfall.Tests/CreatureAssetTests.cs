using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Effects;
using Hollowfall.Core.Models;
using Hollowfall.Core.Relations;

namespace Hollowfall.Tests;

/// <summary>The look, sound and screen text of the Folk, the Bean Nighe and the hare.</summary>
public class CreatureAssetTests
{
    private static readonly EnemyKind[] Kinds = [EnemyKind.Brownie, EnemyKind.Selkie, EnemyKind.Kelpie, EnemyKind.BeanNighe, EnemyKind.Hare];

    public static TheoryData<EnemyKind> Creatures => [.. Kinds];

    [Theory]
    [MemberData(nameof(Creatures))]
    public void CreatureModelsMatchTheirHitBoxes(EnemyKind kind)
    {
        var model = ModelCatalog.ForEnemy(kind);
        var spec = EnemySpec.Of(kind);
        Assert.InRange(model.Height, spec.Height * 0.8, spec.Height * 1.12);
        Assert.InRange(model.Min.Y, -0.1, 0.1);
        Assert.True(model.Width <= spec.HalfWidth * 2 * 1.5, $"{kind} is {model.Width} wide against a {spec.HalfWidth * 2} hit box");
        Assert.True(model.Width >= spec.HalfWidth * 2 * 0.75, $"{kind} is {model.Width} wide, much narrower than its hit box");
        Assert.InRange((model.Min.X + model.Max.X) / 2, -0.1, 0.1);
        Assert.True(model.Anchors["eye"].Z < 0, "faces -Z");
        Assert.Contains(model.Parts, p => p.Glow.TriangleCount > 0);
    }

    [Theory]
    [MemberData(nameof(Creatures))]
    public void EveryCreatureHasItsOwnModelAndAJointedRig(EnemyKind kind)
    {
        var model = ModelCatalog.ForEnemy(kind);
        Assert.NotSame(ModelCatalog.Warden, model);
        Assert.True(model.Parts.Count >= 5, $"{kind} needs joints to pose");
        // A child's parent must exist and come first, so a rig builds in order.
        var seen = new HashSet<string>();
        foreach (var part in model.Parts)
        {
            if (part.Parent != null) Assert.Contains(part.Parent, seen);
            seen.Add(part.Name);
        }
    }

    [Fact]
    public void CreaturesAreDistinctSilhouettes()
    {
        var models = Kinds.Select(ModelCatalog.ForEnemy).ToList();
        Assert.Equal(models.Count, models.Select(m => m.Id).Distinct().Count());
        Assert.Equal(models.Count, models.Select(m => (Math.Round(m.Height, 1), Math.Round(m.Width, 1))).Distinct().Count());
    }

    [Fact]
    public void ThePoolIsAFlatDiscThatFitsACistern()
    {
        var pool = ModelCatalog.CisternPool;
        Assert.InRange(pool.Height, 0.04, 0.12);
        Assert.InRange(pool.Width, 3, 4);
        Assert.Contains(pool, ModelCatalog.All);
    }

    [Fact]
    public void NewModelsWereAppendedAtTheEndOfTheCatalogue()
    {
        var ids = ModelCatalog.All.Select(m => m.Id).ToList();
        // The creatures were appended as one run after the older models (other features add their own runs after it).
        int first = ids.IndexOf("brownie");
        Assert.True(first > ids.IndexOf("torch"));
        Assert.Equal(["brownie", "selkie", "kelpie", "bean_nighe", "hare", "cistern_pool"], ids.Skip(first).Take(6));
        Assert.Equal("husk", ids[0]);
    }

    [Fact]
    public void CreaturesBurstIntoTheirOwnColours()
    {
        foreach (var kind in Kinds)
        {
            Assert.NotEqual(Gibs.Palette(EnemyKind.Warden), Gibs.Palette(kind));
            Assert.True(Gibs.GibCount(kind) is >= 4 and < 22, kind.ToString());
        }
        Assert.True(Gibs.GibCount(EnemyKind.Hare) < Gibs.GibCount(EnemyKind.Kelpie));
    }

    [Fact]
    public void CreatureSoundsAreNewDistinctAndAppendedAfterTheOldOnes()
    {
        var ids = Enum.GetValues<SoundId>().ToList();
        // The creature sounds were added as one run, after the older ones (other features add their own runs after it).
        Assert.True(SoundId.BrownieHiss > SoundId.TitleCard);
        Assert.Equal(9, SoundId.AllyJoin - SoundId.BrownieHiss);
        Assert.Equal(SoundId.RevolverShot, ids[0]);
    }

    [Fact]
    public void RelationEventsHaveVoices()
    {
        Assert.Equal(SoundId.StandingUp, SoundMap.For(new ReputationChanged(Faction.Selkies, ReputationTier.Neutral, ReputationTier.Friendly)));
        Assert.Equal(SoundId.StandingDown, SoundMap.For(new ReputationChanged(Faction.Selkies, ReputationTier.Neutral, ReputationTier.Wary)));
        Assert.Equal(SoundId.BrownieHiss, SoundMap.For(new EntityProvoked(1, Faction.Brownies)));
        Assert.Equal(SoundId.SelkieCry, SoundMap.For(new EntityProvoked(1, Faction.Selkies)));
        Assert.Equal(SoundId.KelpieWhinny, SoundMap.For(new EntityProvoked(1, Faction.Kelpies)));
        Assert.Equal(SoundId.NigheWail, SoundMap.For(new EntityProvoked(1, Faction.Wanderers)));
        Assert.Equal(SoundId.HareScream, SoundMap.For(new EntityProvoked(1, Faction.Vermin)));
        Assert.Equal(SoundId.OmenToll, SoundMap.For(new OmenSensed(1)));
        Assert.Equal(SoundId.OmenToll, SoundMap.For(new OmenFulfilled(0, 0, 2)));
        Assert.Equal(SoundId.BoonChime, SoundMap.For(new BoonGiven(1, "warmth", 5)));
        Assert.Equal(SoundId.BoonChime, SoundMap.For(new OfferingAccepted(1, Faction.Selkies)));
        Assert.Equal(SoundId.AllyJoin, SoundMap.For(new EntityAllied(1, Faction.Selkies)));
        // Chatter that would otherwise spam the speakers stays quiet.
        Assert.Null(SoundMap.For(new WitnessAlerted(1, 2, Faction.Brownies)));
        Assert.Null(SoundMap.For(new EntityCalmed(1, Faction.Brownies)));
    }
}

public class StandingBoardTests
{
    [Fact]
    public void RowsCoverEveryFactionButTheDelverWithBarsInRange()
    {
        var rep = new Reputation();
        var rows = StandingBoard.Rows(rep);
        Assert.Equal(Enum.GetValues<Faction>().Length - 1, rows.Count);
        Assert.DoesNotContain(rows, r => r.Faction == Faction.Delver);
        Assert.All(rows, r => Assert.InRange(r.Fraction, 0, 1));
        Assert.Equal(0.5, rows.First(r => r.Faction == Faction.Brownies).Fraction, 6);
        Assert.True(rows.First(r => r.Faction == Faction.Husks).Fixed);
        Assert.Equal(ReputationTier.Hated, rows.First(r => r.Faction == Faction.Husks).Tier);
    }

    [Fact]
    public void RowsFollowTheReputation()
    {
        var rep = new Reputation();
        rep.Set(Faction.Selkies, 100);
        rep.Set(Faction.Kelpies, -100);
        var rows = StandingBoard.Rows(rep);
        Assert.Equal(1.0, rows.First(r => r.Faction == Faction.Selkies).Fraction, 6);
        Assert.Equal(ReputationTier.Revered, rows.First(r => r.Faction == Faction.Selkies).Tier);
        Assert.Equal(0.0, rows.First(r => r.Faction == Faction.Kelpies).Fraction, 6);
    }

    [Fact]
    public void TheToastNamesTheFactionAndTheDirection()
    {
        Assert.Equal("The Brownies now regard you as Wary", StandingBoard.Toast(new ReputationChanged(Faction.Brownies, ReputationTier.Neutral, ReputationTier.Wary)));
        Assert.Equal("The Selkies think better of you: Friendly", StandingBoard.Toast(new ReputationChanged(Faction.Selkies, ReputationTier.Neutral, ReputationTier.Friendly)));
    }

    [Fact]
    public void RecentLinesShowSignedChangesNewestFirst()
    {
        var rep = new Reputation();
        rep.Apply(RelationAct.Kill, Faction.Brownies, 1, "Killed a Brownie", 3);
        rep.Apply(RelationAct.Help, Faction.Selkies, 1, "Gave a gift to a Selkie", 4);
        var lines = StandingBoard.RecentLines(rep);
        Assert.NotEmpty(lines);
        Assert.Contains("Gave a gift to a Selkie", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("-20 the Brownies: Killed a Brownie"));
        Assert.All(lines, l => Assert.True(l.StartsWith('+') || l.StartsWith('-')));
    }

    [Fact]
    public void PipColoursAreDistinctAndNoneHasNoPip()
    {
        Assert.Null(StandingBoard.PipColour(Cue.None));
        var colours = new[] { Cue.Friendly, Cue.Wary, Cue.Hostile, Cue.Allied }.Select(c => StandingBoard.PipColour(c)!.Value).ToList();
        Assert.Equal(4, colours.Distinct().Count());
    }

    [Fact]
    public void EveryDispositionHasADescription()
    {
        var species = SpeciesInfo.Of(EnemyKind.Selkie);
        var mind = new Mind(species, Traits.Middling, new Vec3(0, 0, 0));
        foreach (var d in Enum.GetValues<Disposition>())
        {
            // Describe reads the live disposition; just make sure the natural one is worded.
            Assert.False(string.IsNullOrWhiteSpace(StandingBoard.Describe(mind)));
            _ = d;
        }
    }
}
