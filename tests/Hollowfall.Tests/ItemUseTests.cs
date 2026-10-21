using Hollowfall.Core.Combat;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Replay;

namespace Hollowfall.Tests;

public class ItemUseTests
{
    private const double Dt = 1.0 / 60;
    private static readonly PlayerActions UseCream = new(false, false, false, UseCream: true);

    [Fact]
    public void CreamHealsWhenTheRunIsHurtAndIsUsedUp()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Creams: 2));
        sim.Vitals.Damage(60);
        double before = sim.Vitals.Health;

        sim.Tick(Dt, a.View(), UseCream);
        var used = sim.DrainEvents().OfType<CreamUsed>().ToList();

        Assert.Single(used);
        Assert.True(used[0].Healed > 0);
        Assert.Equal(before + used[0].Healed, sim.Vitals.Health, 6);
        Assert.Equal(1, sim.CreamsLeft);
    }

    [Fact]
    public void CreamHealsAFixedAmountUpToTheCeiling()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Creams: 1));
        sim.Vitals.Damage(30);
        double room = sim.Vitals.Ceiling - sim.Vitals.Health;
        Assert.True(room < Inventory.CreamHeal, "the test needs less room than a whole cream");

        sim.Tick(Dt, a.View(), UseCream);
        var used = sim.DrainEvents().OfType<CreamUsed>().Single();

        Assert.Equal(room, used.Healed, 6);
        Assert.Equal(sim.Vitals.Ceiling, sim.Vitals.Health, 6);
    }

    [Fact]
    public void CreamIsNotUsedAtFullHealth()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Creams: 2));

        sim.Tick(Dt, a.View(), UseCream);

        Assert.Equal(2, sim.CreamsLeft);
        Assert.DoesNotContain(sim.DrainEvents(), e => e is CreamUsed);
    }

    [Fact]
    public void CreamsAreClampedToTheBag()
    {
        var a = CombatSimTests.MakeArena();
        var sim = new CombatSim(a.World, a.Site, new RunStats(Creams: 10_000));

        Assert.Equal(Inventory.MaxStack * Inventory.Slots, sim.CreamsLeft);
    }

    [Fact]
    public void TheThornCharmWardsTenPercentOfDamage()
    {
        Assert.Equal(Inventory.ThornWard, new RunStats(Charm: ItemKind.ThornCharm).WardFraction);
        Assert.Equal(0, new RunStats(Charm: ItemKind.BrownieCream).WardFraction);
        Assert.Equal(0, new RunStats().WardFraction);
    }

    [Fact]
    public void CreamsAndTheWornCharmSurviveTheReplayFile()
    {
        var frames = new List<ReplayFrame> { new(102400, 20480, -51200, 0, 1659, 0, 1000, 0, 32767, 0, 0) };
        var stats = new RunStats(Vigour: 1, Might: 2, Creams: 3, Charm: ItemKind.ThornCharm);
        var replay = new RunReplay("items", 0, 0, 0, 0, new FloorRecord(OverallGrade.C, 1, 1), frames) { Stats = stats };

        Assert.True(ReplayCodec.TryDecode(ReplayCodec.Encode(replay), out var back));
        Assert.Equal(stats, back!.Stats);
    }

    [Fact]
    public void AReplayFromBeforeItemsLoadsWithNoCreamOrCharm()
    {
        var frames = new List<ReplayFrame> { new(102400, 20480, -51200, 0, 1659, 0, 1000, 0, 32767, 0, 0) };
        var replay = new RunReplay("v3", 0, 0, 0, 0, new FloorRecord(OverallGrade.C, 1, 1), frames) { Stats = new RunStats(Vigour: 4, Might: 1) };
        byte[] v4 = ReplayCodec.Encode(replay);

        // Rebuild the same run in format 3: drop the creams and charm bytes (after Vigour and Might), set the version and re-checksum.
        var payload = new List<byte>(v4[..^4]);
        payload[4] = RunReplay.FormatVersionWithoutItems;
        payload.RemoveRange(8, 2);
        uint sum = ReplayCodec.Fnv32(payload);
        payload.AddRange(BitConverter.GetBytes(sum));

        Assert.True(ReplayCodec.TryDecode(payload.ToArray(), out var old));
        Assert.Equal(new RunStats(Vigour: 4, Might: 1), old!.Stats);
    }
}
