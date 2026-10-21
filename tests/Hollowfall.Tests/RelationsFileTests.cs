using Hollowfall.Core.Relations;

namespace Hollowfall.Tests;

/// <summary>relations.json is hand-editable and must never stop the game, whatever is in it.</summary>
public class RelationsFileTests
{
    [Theory]
    [InlineData("{\"seeds\":{\"a\":{\"Brownies\":10},\"a\":{\"Brownies\":-10}}}")]
    [InlineData("{\"seeds\":{\"a\":{\"Brownies\":10,\"Brownies\":-10}}}")]
    [InlineData("{\"seeds\":{},\"seeds\":{}}")]
    [InlineData("{\"version\":1,\"version\":2}")]
    public void DuplicateKeysNeverThrow(string json)
    {
        var store = RelationsStore.FromJson(json);
        Assert.NotNull(store);
        Assert.InRange(store.SeedCount, 0, RelationsStore.MaxSeeds);
        // And the store is still usable afterwards.
        var rep = new Reputation();
        rep.Set(Faction.Selkies, 30);
        Assert.True(store.Record("again", rep));
        Assert.Equal(30, store.Standings("again")![Faction.Selkies], 3);
    }

    [Fact]
    public void ACarriedStandingStartsTheNextSimAtTheSameRegard()
    {
        var rep = new Reputation();
        rep.Set(Faction.Brownies, 60);
        var store = new RelationsStore();
        store.Record("voxel", rep);
        var back = RelationsStore.FromJson(store.ToJson()).Carry("voxel");
        Assert.Equal(ReputationTier.Trusted, back.Tier(Faction.Brownies));
        Assert.Equal(60, back.Value(Faction.Brownies), 3);
    }
}
