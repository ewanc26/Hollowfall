using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Relations;
using Hollowfall.Core.World;
using Xunit;

namespace Hollowfall.Tests;

public class QuestsAndMerchantTests
{
    private static DungeonSite FindSite()
    {
        for (long seed = 1; seed < 40; seed++)
        {
            var world = new VoxelWorld(seed);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is { } site) return site;
        }
        throw new InvalidOperationException("no dungeon found");
    }

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
    public void FinishingAQuestPaysItsRewardOnce()
    {
        var character = new Progression();
        character.Quests.Take(QuestId.ClearTheHusks);

        for (int i = 1; i <= 5; i++) character.RecordKill(new EnemyKilled(i, EnemyKind.Husk));
        Assert.Equal(40, character.Experience);
        Assert.Equal(30, character.Coin); // 5 Husks' coin plus the quest's 15
        Assert.Equal(1, character.Items.Count(ItemKind.BrownieCream));

        character.RecordKill(new EnemyKilled(6, EnemyKind.Husk));
        Assert.Equal(40, character.Experience);
        Assert.Equal(33, character.Coin);
        Assert.Equal(1, character.Items.Count(ItemKind.BrownieCream));
    }

    [Fact]
    public void AQuestInProgressSurvivesASave()
    {
        var character = new Progression();
        character.Quests.Take(QuestId.ClearTheSpitters);
        character.RecordKill(new EnemyKilled(1, EnemyKind.Spitter));
        character.RecordKill(new EnemyKilled(2, EnemyKind.Spitter));

        var loaded = ProgressionStore.FromJson(ProgressionStore.ToJson(character));
        Assert.Equal(2, loaded.Quests.Progress(QuestId.ClearTheSpitters));
        Assert.Equal(8, loaded.Coin);
        Assert.Contains(QuestId.ClearTheSpitters, loaded.Quests.Active);

        loaded.RecordKill(new EnemyKilled(3, EnemyKind.Spitter));
        Assert.True(loaded.Quests.IsDone(QuestId.ClearTheSpitters));
        Assert.Equal(60, loaded.Experience);
        Assert.Equal(37, loaded.Coin); // two Spitters' coin plus the third and the quest's 25
        Assert.Equal(1, loaded.Items.Count(ItemKind.ThornCharm));
    }

    [Fact]
    public void AFinishedQuestStaysFinishedAndUnknownQuestsAreDropped()
    {
        const string json = """
            {"quests": {"ClearTheHusks": {"progress": 99, "done": true}, "NoSuchQuest": {"progress": 1, "done": false}}}
            """;
        var loaded = ProgressionStore.FromJson(json);
        Assert.True(loaded.Quests.IsDone(QuestId.ClearTheHusks));
        Assert.Equal(5, loaded.Quests.Progress(QuestId.ClearTheHusks));
        Assert.Empty(loaded.Quests.Active);
        Assert.False(loaded.Quests.Take(QuestId.ClearTheHusks));
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
            Assert.True(def.Coin > 0);
            Assert.NotEqual(Hollowfall.Core.Relations.Faction.Delver, def.Giver);
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

    [Fact]
    public void BuyingSpendsThePriceAndPutsTheItemInTheBag()
    {
        var character = new Progression();
        character.GainCoin(100);

        Assert.Equal(Purchase.Bought, Merchant.Buy(character, ItemKind.BrownieCream, ReputationTier.Neutral));
        Assert.Equal(90, character.Coin);
        Assert.Equal(1, character.Items.Count(ItemKind.BrownieCream));
    }

    [Fact]
    public void FriendsPayLessAtTheCounterToo()
    {
        var character = new Progression();
        character.GainCoin(100);

        Assert.Equal(Purchase.Bought, Merchant.Buy(character, ItemKind.BrownieCream, ReputationTier.Revered));
        Assert.Equal(93, character.Coin);
    }

    [Fact]
    public void NoCoinMeansNoGoodsAndNoCharge()
    {
        var character = new Progression();

        Assert.Equal(Purchase.NoCoin, Merchant.Buy(character, ItemKind.BrownieCream, ReputationTier.Neutral));
        Assert.Equal(0, character.Coin);
        Assert.Equal(0, character.Items.Count(ItemKind.BrownieCream));
    }

    [Fact]
    public void AFullBagIsNotCharged()
    {
        var character = new Progression();
        character.GainCoin(100);
        character.Items.Add(ItemKind.BrownieCream, Inventory.Slots * Inventory.MaxStack);

        Assert.Equal(Purchase.BagFull, Merchant.Buy(character, ItemKind.ThornCharm, ReputationTier.Neutral));
        Assert.Equal(100, character.Coin);
        Assert.Equal(0, character.Items.Count(ItemKind.ThornCharm));
    }

    [Fact]
    public void TheHatedAreRefusedAtTheCounter()
    {
        var character = new Progression();
        character.GainCoin(100);

        Assert.Equal(Purchase.Refused, Merchant.Buy(character, ItemKind.ThornCharm, ReputationTier.Hated));
        Assert.Equal(100, character.Coin);
        Assert.Equal(0, character.Items.Count(ItemKind.ThornCharm));
    }

    [Fact]
    public void EveryWareIsForSaleAndHasAPrice()
    {
        foreach (var item in Merchant.Wares)
        {
            Assert.True(Merchant.BasePrice(item) > 0);
            Assert.NotNull(Merchant.PriceFor(item, ReputationTier.Neutral));
        }
    }

    [Fact]
    public void ThePedlarWaitsOnThePlazaOppositeThePlayer()
    {
        var site = FindSite();
        var spot = Merchant.PedlarSpot(site);
        Assert.Equal(site.EntranceX - 1.5, spot.X);
        Assert.Equal(site.EntranceZ + 2.5, spot.Z);
        Assert.Equal(site.SurfaceY + 1, spot.Y);
        Assert.InRange(Math.Abs(spot.X - site.EntranceX), 1, DungeonSite.PlazaRadius);
        Assert.InRange(Math.Abs(spot.Z - site.EntranceZ), 1, DungeonSite.PlazaRadius);
    }
}
