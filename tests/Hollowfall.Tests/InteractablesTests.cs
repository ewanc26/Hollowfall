using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;

namespace Hollowfall.Tests;

public class InteractablesTests
{
    private const double Dt = 1.0 / 60;

    private sealed class Rig
    {
        public double Health = 50;
        public double Damaged;
        public List<CombatEvent> Events = [];
        public Interactables Items;

        public Rig(params Spawn[] spawns) =>
            Items = new Interactables(spawns, 7, d => { Damaged += d; return d; }, h => { double got = Math.Min(h, 100 - Health); Health += got; return got; }, Events.Add);

        public void Tick(Vec3 feet, double seconds = Dt, bool interact = false)
        {
            var view = new PlayerView(feet, feet + new Vec3(0, 1.6, 0), new Vec3(0, 0, -1), false, false);
            for (double t = 0; t < seconds - 1e-9; t += Dt) Items.Tick(Dt, view, interact && t < Dt);
        }
    }

    private static Spawn At(SpawnKind kind, int x = 10, int z = 10, int tier = 1, int floor = 0) => new(kind, x, 40, z, floor, 1, tier);

    private static Vec3 Beside(int x = 10, int z = 10, double offset = 0.5) => new(x + 0.5 + offset, 40, z + 0.5);

    [Fact]
    public void WalkingOverAKeyPicksItUpOnce()
    {
        var r = new Rig(At(SpawnKind.Key));
        r.Tick(Beside(offset: 4));
        Assert.Equal(0, r.Items.KeysHeld);
        r.Tick(Beside(offset: 0.4), 0.2);
        Assert.Equal(1, r.Items.KeysHeld);
        Assert.True(r.Items.KeyTaken(0));
        Assert.Single(r.Events.OfType<KeyCollected>());
    }

    [Fact]
    public void AKeyOnAnotherFloorIsNotPickedUp()
    {
        var r = new Rig(At(SpawnKind.Key));
        r.Tick(new Vec3(10.5, 30, 10.5));
        Assert.Equal(0, r.Items.KeysHeld);
    }

    [Fact]
    public void AnOrdinaryChestOpensForFreeAndHeals()
    {
        // Use a chest whose seeded loot is health (others are covered in LootTests).
        int x = Enumerable.Range(10, 500).First(i => LootTable.Roll(7, At(SpawnKind.Loot, i, tier: 1)).Kind == LootKind.Health);
        var r = new Rig(At(SpawnKind.Loot, x, tier: 1));
        r.Tick(Beside(x, offset: 1.5), interact: true);
        var opened = Assert.Single(r.Events.OfType<ChestOpened>());
        Assert.False(opened.WasLocked);
        Assert.Equal(Interactables.ChestHeal, opened.Healed);
        Assert.Equal(LootKind.Health, opened.Loot!.Value.Kind);
        Assert.Equal(80, r.Health);
        Assert.True(r.Items.Chests[0].Opened);
    }

    [Fact]
    public void AChestOpensOnlyOnce()
    {
        var r = new Rig(At(SpawnKind.Loot));
        r.Tick(Beside(), interact: true);
        r.Tick(Beside(), interact: true);
        Assert.Single(r.Events.OfType<ChestOpened>());
    }

    [Fact]
    public void AChestNeedsAnInteractToOpen()
    {
        var r = new Rig(At(SpawnKind.Loot));
        r.Tick(Beside(), 0.5);
        Assert.Empty(r.Events.OfType<ChestOpened>());
    }

    [Fact]
    public void ChestsOutOfReachStayShut()
    {
        var r = new Rig(At(SpawnKind.Loot));
        r.Tick(Beside(offset: Interactables.ChestReach + 1), interact: true);
        Assert.Empty(r.Events);
    }

    [Fact]
    public void ALockedChestNeedsAKeyAndSpendsIt()
    {
        int x = Enumerable.Range(10, 500).First(i => LootTable.Roll(7, At(SpawnKind.Loot, i, tier: 2)).Kind == LootKind.Health);
        var r = new Rig(At(SpawnKind.Key, 20, 20), At(SpawnKind.Loot, x, tier: 2));
        r.Health = 10;
        Assert.True(r.Items.Chests[0].Locked);
        r.Tick(Beside(x), interact: true);
        Assert.Single(r.Events.OfType<ChestLocked>());
        Assert.False(r.Items.Chests[0].Opened);

        r.Tick(Beside(20, 20, 0.2), 0.2);
        Assert.Equal(1, r.Items.KeysHeld);
        r.Tick(Beside(x), interact: true);
        var opened = Assert.Single(r.Events.OfType<ChestOpened>());
        Assert.True(opened.WasLocked);
        Assert.Equal(LootRarity.Rare, opened.Loot!.Value.Rarity);
        Assert.Equal(Interactables.LockedChestHeal, opened.Healed);
        Assert.Equal(0, r.Items.KeysHeld);
    }

    [Fact]
    public void ATrapWarnsThenStrikesOncePerCycleAndHurts()
    {
        var r = new Rig(At(SpawnKind.Trap));
        var on = new Vec3(10.5, 40, 10.5);
        double hidden = 0, warned = 0, out_ = 0;
        for (double t = 0; t < Interactables.TrapPeriod * 2; t += Dt)
        {
            r.Tick(on);
            double e = r.Items.SpikeExtension(0);
            if (e == 0) hidden++;
            if (r.Items.Warning(0)) warned++;
            if (e > 0.99) out_++;
        }
        Assert.True(hidden > 0 && warned > 0 && out_ > 0, "hidden, warning and out all happen");
        int strikes = r.Events.OfType<TrapSprung>().Count();
        Assert.Equal(2, strikes);
        Assert.Equal(Interactables.TrapDamage * 2, r.Damaged, 6);
    }

    [Fact]
    public void ATrapOnlyHurtsWhoStandsOnIt()
    {
        var r = new Rig(At(SpawnKind.Trap));
        r.Tick(new Vec3(14.5, 40, 10.5), Interactables.TrapPeriod * 2);
        Assert.Empty(r.Events.OfType<TrapSprung>());
    }

    [Fact]
    public void JumpingOverATrapAvoidsIt()
    {
        var r = new Rig(At(SpawnKind.Trap));
        r.Tick(new Vec3(10.5, 41.5, 10.5), Interactables.TrapPeriod * 2);
        Assert.Empty(r.Events.OfType<TrapSprung>());
    }

    [Fact]
    public void TrapsInARoomDoNotAllStrikeTogether()
    {
        var spawns = Enumerable.Range(0, 6).Select(i => At(SpawnKind.Trap, 10 + i * 3, 10)).ToArray();
        var r = new Rig(spawns);
        var seen = new HashSet<double>();
        for (int i = 0; i < 6; i++) seen.Add(Math.Round(r.Items.SpikeExtension(i), 3));
        r.Tick(new Vec3(0, 40, 0), 1.0);
        var phases = Enumerable.Range(0, 6).Select(i => r.Items.Warning(i)).Distinct().Count();
        Assert.True(phases >= 1);
        var offsets = new HashSet<int>();
        for (double t = 0; t < Interactables.TrapPeriod && offsets.Count < 2; t += Dt)
        {
            r.Tick(new Vec3(0, 40, 0));
            offsets.Add(Enumerable.Range(0, 6).Count(i => r.Items.SpikeExtension(i) > 0.5));
        }
        Assert.True(offsets.Count >= 2, "spikes are not all out at the same instants");
    }

    [Fact]
    public void TheSimulationOpensARealChestOnInteract()
    {
        for (long seed = 1; seed < 30; seed++)
        {
            var world = new Hollowfall.Core.World.VoxelWorld(seed);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                {
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is not { } site) continue;
                    var chest = site.Spawns.FirstOrDefault(s => s.Kind == SpawnKind.Loot && s.Tier < s.Floor + 2);
                    if (chest.Kind != SpawnKind.Loot) continue;
                    var sim = new CombatSim(world, site);
                    var feet = new Vec3(chest.X + 1.5, chest.Y, chest.Z + 0.5);
                    var view = new PlayerView(feet, feet + new Vec3(0, 1.6, 0), new Vec3(0, 0, -1), false, false);
                    sim.Tick(Dt, view, new PlayerActions(false, false, false, Interact: true));
                    Assert.Contains(sim.DrainEvents(), e => e is ChestOpened);
                    Assert.True(sim.Items.Chests.First(c => c.Spawn == chest).Opened);
                    return;
                }
        }
        Assert.Fail("no dungeon with an ordinary chest was found");
    }
}
