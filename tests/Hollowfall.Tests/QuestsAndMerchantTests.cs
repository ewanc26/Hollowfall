using Hollowfall.Core.Combat;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Relations;
using Xunit;

namespace Hollowfall.Tests;

public class QuestsAndMerchantTests
{
    [Fact]
    public void AQuestFinishesOnItsCountAndOnlyOnce()
    {
        var log = new QuestLog();
        Assert.True(log.Take(QuestId.ClearTheHusks));
        Assert.False(log.Take(QuestId.ClearTheHusks));

        for (int i = 1; i < 5; i++) Assert.Empty(log.Record(new EnemyKilled(i, EnemyKind.Husk)));
        Assert.Equal(4, log.Progress(QuestId.ClearTheHusks));

        var finished = Assert.Single(log.Record(new EnemyKilled(5, EnemyKind.Husk)));
        Assert.Equal(QuestId.ClearTheHusks, finished.Id);
        Assert.Equal(ItemKind.BrownieCream, finished.Item);
        Assert.True(log.IsDone(QuestId.ClearTheHusks));

        Assert.Empty(log.Record(new EnemyKilled(6, EnemyKind.Husk)));
        Assert.False(log.Take(QuestId.ClearTheHusks));
    }

    [Fact]
    public void KillsOfAnotherKindDoNotCount()
    {
        var log = new QuestLog();
        log.Take(QuestId.ClearTheHusks);
        Assert.Empty(log.Record(new EnemyKilled(1, EnemyKind.Spitter)));
        Assert.Equal(0, log.Progress(QuestId.ClearTheHusks));
    }

    [Fact]
    public void AQuestNotTakenDoesNotCount()
    {
        var log = new QuestLog();
        Assert.Empty(log.Record(new EnemyKilled(1, EnemyKind.Husk)));
        Assert.DoesNotContain(QuestId.ClearTheHusks, log.Active);
    }

    [Fact]
    public void EveryQuestInTheCatalogueIsWhatItSaysItIs()
    {
        foreach (var id in Enum.GetValues<QuestId>())
        {
            var def = QuestLog.Catalogue[id];
            Assert.Equal(id, def.Id);
            Assert.True(def.Count > 0);
            Assert.True(def.Experience > 0);
        }
    }

    [Fact]
    public void TheBasePriceIsWhatNeutralPays()
    {
        Assert.Equal(10, Merchant.PriceFor(ItemKind.BrownieCream, ReputationTier.Neutral));
        Assert.Equal(40, Merchant.PriceFor(ItemKind.ThornCharm, ReputationTier.Neutral));
    }

    [Fact]
    public void FriendsPayLessAndTheWaryMore()
    {
        Assert.Equal(9, Merchant.PriceFor(ItemKind.BrownieCream, ReputationTier.Friendly));
        Assert.Equal(13, Merchant.PriceFor(ItemKind.BrownieCream, ReputationTier.Wary));
    }

    [Fact]
    public void ThePriceNeverRisesAsStandingRises()
    {
        var order = new[] { ReputationTier.Hostile, ReputationTier.Wary, ReputationTier.Neutral, ReputationTier.Friendly, ReputationTier.Trusted, ReputationTier.Revered };
        int? last = int.MaxValue;
        foreach (var tier in order)
        {
            int price = Merchant.PriceFor(ItemKind.ThornCharm, tier)!.Value;
            Assert.True(price <= last);
            last = price;
        }
    }

    [Fact]
    public void TheHatedAreNotServed()
    {
        Assert.Null(Merchant.PriceFor(ItemKind.ThornCharm, ReputationTier.Hated));
    }
}
