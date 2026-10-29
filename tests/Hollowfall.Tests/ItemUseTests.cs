using Hollowfall.Core.Combat;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Replay;

namespace Hollowfall.Tests;

public class ItemUseTests
{
    private const double Dt = 1.0 / 60;
    private static readonly PlayerActions Use = new(false, false, false, UseItem: true);

    [Fact]
    public void CreamHealsWhenTheRunIsHurtAndIsUsedUp()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Uses: 2, Consumable: ItemKind.BrownieCream));
        sim.Vitals.Damage(60);
        double before = sim.Vitals.Health;

        sim.Tick(Dt, a.View(), Use);
        var used = sim.DrainEvents().OfType<ItemUsed>().ToList();

        Assert.Single(used);
        Assert.Equal(ItemKind.BrownieCream, used[0].Kind);
        Assert.True(used[0].Applied > 0);
        Assert.Equal(before + used[0].Applied, sim.Vitals.Health, 6);
        Assert.Equal(1, sim.UsesLeft);
    }

    [Fact]
    public void CreamHealsAFixedAmountUpToTheCeiling()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Uses: 1, Consumable: ItemKind.BrownieCream));
        sim.Vitals.Damage(30);
        double room = sim.Vitals.Ceiling - sim.Vitals.Health;
        Assert.True(room < Inventory.CreamHeal, "the test needs less room than a whole cream");

        sim.Tick(Dt, a.View(), Use);
        var used = sim.DrainEvents().OfType<ItemUsed>().Single();

        Assert.Equal(room, used.Applied, 6);
        Assert.Equal(sim.Vitals.Ceiling, sim.Vitals.Health, 6);
    }

    [Fact]
    public void CreamIsNotUsedAtFullHealth()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Uses: 2, Consumable: ItemKind.BrownieCream));

        sim.Tick(Dt, a.View(), Use);

        Assert.Equal(2, sim.UsesLeft);
        Assert.DoesNotContain(sim.DrainEvents(), e => e is ItemUsed);
    }

    [Fact]
    public void UsesAreClampedToTheBag()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Uses: 10_000, Consumable: ItemKind.BrownieCream));
        Assert.Equal(Inventory.MaxStack * Inventory.Slots, sim.UsesLeft);
    }

    [Fact]
    public void AVialOfBloodHealsThirty()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Uses: 1, Consumable: ItemKind.VialOfBlood));
        sim.Vitals.Damage(60);
        sim.Tick(Dt, a.View(), Use);
        Assert.Equal(Inventory.VialHeal, sim.DrainEvents().OfType<ItemUsed>().Single().Applied, 6);
        Assert.Equal(0, sim.UsesLeft);
    }

    [Fact]
    public void ACleansingDropWashesAwayHardDamageAndIsNotWasted()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Uses: 2, Consumable: ItemKind.CleansingDrop));
        sim.Vitals.Damage(60);
        Assert.True(sim.Vitals.HardDamage > 0);

        sim.Tick(Dt, a.View(), Use);
        Assert.Equal(0, sim.Vitals.HardDamage);
        Assert.Equal(1, sim.UsesLeft);
        Assert.Equal(ItemKind.CleansingDrop, sim.DrainEvents().OfType<ItemUsed>().Single().Kind);

        sim.Tick(Dt, a.View(), Use);
        Assert.Equal(1, sim.UsesLeft);
    }

    [Fact]
    public void AStyleShardBurstsIntoStyleAndIsSpent()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Uses: 1, Consumable: ItemKind.StyleShard));

        sim.Tick(Dt, a.View(), Use);

        Assert.True(sim.Style.Points > 0);
        Assert.Equal(0, sim.UsesLeft);
        Assert.Equal(ItemKind.StyleShard, sim.DrainEvents().OfType<ItemUsed>().Single().Kind);
    }

    [Fact]
    public void ABloodlustDraughtRaisesBloodFromHitsForItsTime()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Uses: 1, Consumable: ItemKind.BloodlustDraught));

        sim.Tick(Dt, a.View(), Use);

        Assert.Equal(Inventory.BloodlustScale, sim.Vitals.HitHealScale, 6);
        Assert.Equal(Inventory.BloodlustSeconds, sim.Items.Perks.BloodlustLeft, 6);
        Assert.Equal(0, sim.UsesLeft);
    }

    [Fact]
    public void AnOpenGuardTonicWidensTheParryWindowForItsTime()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Uses: 1, Consumable: ItemKind.OpenGuardTonic));

        sim.Tick(Dt, a.View(), Use);

        Assert.Equal(Inventory.OpenGuardBonus, sim.Items.Perks.ParryWindowBonus, 6);
        Assert.Equal(Inventory.OpenGuardSeconds, sim.Items.Perks.WideParryLeft, 6);
        Assert.Equal(0, sim.UsesLeft);
    }

    [Fact]
    public void AnItemIsNotUsedWithoutUsesOrAKind()
    {
        var a = CombatSimTests.MakeArena();
        var none = new CombatSim(a.World, a.Site, new RunStats(Consumable: ItemKind.BrownieCream));
        none.Vitals.Damage(40);
        none.Tick(Dt, a.View(), Use);
        Assert.Equal(0, none.UsesLeft);

        var empty = new CombatSim(a.World, a.Site, new RunStats(Uses: 2));
        empty.Vitals.Damage(40);
        empty.Tick(Dt, a.View(), Use);
        Assert.Equal(2, empty.UsesLeft);
        Assert.DoesNotContain(empty.DrainEvents(), e => e is ItemUsed);
    }

    [Fact]
    public void TheThornCharmWardsTenPercentOfDamage()
    {
        Assert.Equal(Inventory.ThornWard, new RunStats(Charm: ItemKind.ThornCharm).WardFraction);
        Assert.Equal(0, new RunStats(Charm: ItemKind.BrownieCream).WardFraction);
        Assert.Equal(0, new RunStats().WardFraction);
    }

    [Fact]
    public void ARelicRaisesHealthOrDamage()
    {
        Assert.Equal(PlayerVitals.MaxHealth + Inventory.WardenSealHealth, new RunStats(Relic: ItemKind.WardenSeal).MaxHealth);
        Assert.Equal(1 + Inventory.HareFootDamage, new RunStats(Relic: ItemKind.HareFoot).DamageScale);
        Assert.Equal(PlayerVitals.MaxHealth, new RunStats(Relic: ItemKind.ThornCharm).MaxHealth);
    }

    [Fact]
    public void TheQuickItemEquipmentAndRelicSurviveTheReplayFile()
    {
        var frames = new List<ReplayFrame> { new(102400, 20480, -51200, 0, 1659, 0, 1000, 0, 32767, 0, 0) };
        var stats = new RunStats(Vigour: 1, Might: 2, Uses: 3, Consumable: ItemKind.StyleShard, Charm: ItemKind.ThornCharm, Relic: ItemKind.WardenSeal);
        var replay = new RunReplay("items", 0, 0, 0, 0, new FloorRecord(OverallGrade.C, 1, 1), frames) { Stats = stats };

        Assert.True(ReplayCodec.TryDecode(ReplayCodec.Encode(replay), out var back));
        Assert.Equal(stats, back!.Stats);
    }

    [Fact]
    public void AnItemFlagSurvivesTheReplayFileWithTheOtherActions()
    {
        var actions = new PlayerActions(true, true, true, PortalTeal: true, PortalViolet: true, Interact: true,
            ClosePortals: true, FireMortar: true, UseItem: true);
        var frame = ReplayFrame.From(new PlayerView(new Vec3(1, 2, 3), new Vec3(1, 3.62, 3), new Vec3(0, 0, -1), false, false), actions);
        var replay = new RunReplay("flags", 0, 0, 0, 0, new FloorRecord(OverallGrade.C, 1, 1), [frame]);

        Assert.True(ReplayCodec.TryDecode(ReplayCodec.Encode(replay), out var back));
        Assert.Equal(actions, back!.Frames[0].ToActions());
        Assert.Equal(0, back.Frames[0].Respawns);
    }

    [Fact]
    public void AReplayFromBeforeItemsLoadsWithNoCreamOrCharm()
    {
        byte[] file = LegacyReplay.OneFrame(RunReplay.FormatVersionWithoutItems, "v3", new RunStats(Vigour: 4, Might: 1));
        Assert.True(ReplayCodec.TryDecode(file, out var old));
        Assert.Equal(new RunStats(Vigour: 4, Might: 1), old!.Stats);
    }

    [Fact]
    public void AReplayFromBeforeTheQuickItemLoadsWithCreamAndCharm()
    {
        byte[] file = LegacyReplay.OneFrame(RunReplay.FormatVersionWithCream, "v4", new RunStats(Vigour: 1, Uses: 2, Charm: ItemKind.ThornCharm));
        Assert.True(ReplayCodec.TryDecode(file, out var old));
        Assert.Equal(new RunStats(Vigour: 1, Uses: 2, Consumable: ItemKind.BrownieCream, Charm: ItemKind.ThornCharm), old!.Stats);
    }
}
