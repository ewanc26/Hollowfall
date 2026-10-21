using Hollowfall.Core.Progression;
using Xunit;

namespace Hollowfall.Tests;

public class InventoryTests
{
    [Fact]
    public void AStackHoldsNineAndThenNeedsAnotherSlot()
    {
        var bag = new Inventory();
        Assert.Equal(9, bag.Add(ItemKind.BrownieCream, 9));
        Assert.Equal(1, bag.SlotsUsed);
        Assert.Equal(1, bag.Add(ItemKind.BrownieCream));
        Assert.Equal(10, bag.Count(ItemKind.BrownieCream));
        Assert.Equal(2, bag.SlotsUsed);
    }

    [Fact]
    public void APartlyFilledStackTakesMoreEvenWhenTheBagIsFull()
    {
        var bag = new Inventory();
        bag.Add(ItemKind.BrownieCream, 5);
        while (bag.SlotsUsed < Inventory.Slots) bag.Add(ItemKind.ThornCharm, Inventory.MaxStack);
        Assert.Equal(Inventory.Slots, bag.SlotsUsed);

        Assert.Equal(4, bag.Add(ItemKind.BrownieCream, 4));
        Assert.Equal(9, bag.Count(ItemKind.BrownieCream));
        Assert.Equal(Inventory.Slots, bag.SlotsUsed);
    }

    [Fact]
    public void AFullBagTakesNothingMore()
    {
        var bag = new Inventory();
        while (bag.SlotsUsed < Inventory.Slots) bag.Add(ItemKind.BrownieCream, Inventory.MaxStack);
        Assert.Equal(0, bag.Add(ItemKind.ThornCharm));
        Assert.Equal(0, bag.Count(ItemKind.ThornCharm));
    }

    [Fact]
    public void RemoveTakesOnlyWhatIsThere()
    {
        var bag = new Inventory();
        bag.Add(ItemKind.BrownieCream, 2);
        Assert.Equal(2, bag.Remove(ItemKind.BrownieCream, 5));
        Assert.Equal(0, bag.Count(ItemKind.BrownieCream));
        Assert.Equal(0, bag.SlotsUsed);
    }

    [Fact]
    public void OnlyACharmCanBeWorn()
    {
        var bag = new Inventory();
        bag.Add(ItemKind.BrownieCream);
        bag.Add(ItemKind.ThornCharm);
        Assert.False(bag.Wear(ItemKind.BrownieCream));
        Assert.True(bag.Wear(ItemKind.ThornCharm));
        Assert.Equal(ItemKind.ThornCharm, bag.Charm);
        Assert.Equal(0, bag.Count(ItemKind.ThornCharm));
    }

    [Fact]
    public void TakingTheCharmOffPutsItBackInTheBag()
    {
        var bag = new Inventory();
        bag.Add(ItemKind.ThornCharm, 2);
        bag.Wear(ItemKind.ThornCharm);
        Assert.Equal(1, bag.Count(ItemKind.ThornCharm));
        Assert.True(bag.TakeOff());
        Assert.Null(bag.Charm);
        Assert.Equal(2, bag.Count(ItemKind.ThornCharm));
        Assert.False(bag.TakeOff());
    }

    [Fact]
    public void YouCannotWearWithoutOwningOne()
    {
        var bag = new Inventory();
        Assert.False(bag.Wear(ItemKind.ThornCharm));
        Assert.Null(bag.Charm);
    }

    [Fact]
    public void ARestoredBagClampsCountsAndWearsTheSavedCharm()
    {
        var bag = new Inventory();
        bag.Restore([(ItemKind.BrownieCream, 9999)], ItemKind.ThornCharm);
        Assert.Equal(Inventory.MaxStack * Inventory.Slots, bag.Count(ItemKind.BrownieCream));
        Assert.Equal(ItemKind.ThornCharm, bag.Charm);
        Assert.Equal(0, bag.Count(ItemKind.ThornCharm));
    }
}

public class InventorySaveTests
{
    [Fact]
    public void TheBagAndTheCharmSaveAndLoad()
    {
        var p = new Progression();
        p.Items.Add(ItemKind.BrownieCream, 3);
        p.Items.Add(ItemKind.ThornCharm);
        p.Items.Wear(ItemKind.ThornCharm);

        var back = ProgressionStore.FromJson(ProgressionStore.ToJson(p));
        Assert.Equal(3, back.Items.Count(ItemKind.BrownieCream));
        Assert.Equal(ItemKind.ThornCharm, back.Items.Charm);
        Assert.Equal(0, back.Items.Count(ItemKind.ThornCharm));
    }

    [Fact]
    public void AVersionOneFileLoadsWithAnEmptyBag()
    {
        var p = ProgressionStore.FromJson("{\"version\":1,\"experience\":130,\"vigour\":1,\"might\":0}");
        Assert.Equal(130, p.Experience);
        Assert.Equal(1, p.Vigour);
        Assert.Equal(0, p.Items.SlotsUsed);
        Assert.Null(p.Items.Charm);
    }

    [Fact]
    public void UnknownItemsAreDropped()
    {
        var p = ProgressionStore.FromJson("{\"items\":{\"BrownieCream\":2,\"Gold\":50},\"charm\":\"Sword\"}");
        Assert.Equal(2, p.Items.Count(ItemKind.BrownieCream));
        Assert.Null(p.Items.Charm);
        Assert.Equal(1, p.Items.SlotsUsed);
    }

    [Fact]
    public void ABrownieBoonLeavesItsCreamUnlessTheBagIsFull()
    {
        var p = new Progression();
        Assert.True(p.ReceiveBoon());
        Assert.Equal(1, p.Items.Count(ItemKind.BrownieCream));

        var full = new Progression();
        while (full.Items.SlotsUsed < Inventory.Slots) full.Items.Add(ItemKind.ThornCharm, Inventory.MaxStack);
        Assert.False(full.ReceiveBoon());
    }
}
