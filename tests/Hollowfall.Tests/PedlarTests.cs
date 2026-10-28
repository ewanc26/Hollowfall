using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Relations;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class PedlarTests
{
    private const double Dt = 1.0 / 60;

    private static (VoxelWorld World, DungeonSite Site) FindSite()
    {
        for (long seed = 1; seed < 40; seed++)
        {
            var world = new VoxelWorld(seed);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is { } site) return (world, site);
        }
        throw new InvalidOperationException("no dungeon found");
    }

    private static PlayerView OnPlaza(DungeonSite site)
    {
        var feet = new Vec3(site.EntranceX + DungeonSite.PlazaRadius + 0.5, site.SurfaceY + 1, site.EntranceZ + 0.5);
        return new PlayerView(feet, feet + new Vec3(0, 1.62, 0), new Vec3(-1, 0, 0), false, false);
    }

    [Fact]
    public void ThePedlarAppearsOnThePlazaWhenYouComeUpIntoTheDaylight()
    {
        var (world, site) = FindSite();
        var sim = new CombatSim(world, site);
        sim.EnableRelations();
        var view = OnPlaza(site);

        sim.Tick(Dt, view, default);

        var pedlar = Assert.Single(sim.Enemies, e => e.Kind == EnemyKind.Pedlar);
        Assert.Equal(CombatSim.SurfaceFloor, pedlar.Floor);
        Assert.Equal(Merchant.PedlarSpot(site), pedlar.Feet);
        Assert.Equal(Faction.Villagers, pedlar.Mind!.Species.Faction);
    }

    [Fact]
    public void HeWaitsUndergroundUntilYouComeBackUp()
    {
        var (world, site) = FindSite();
        var sim = new CombatSim(world, site);
        sim.EnableRelations();
        var room = site.Floors[0].EntranceRoom;
        var (x, y, z) = site.ToWorld(0, room.CenterX, room.CenterZ);
        var below = new PlayerView(new Vec3(x + 0.5, y, z + 0.5), new Vec3(x + 0.5, y + 1.62, z + 0.5), new Vec3(1, 0, 0), false, false);

        for (int i = 0; i < 30; i++) sim.Tick(Dt, below, default);
        Assert.DoesNotContain(sim.Enemies, e => e.Kind == EnemyKind.Pedlar);

        for (int i = 0; i < 2; i++) sim.Tick(Dt, OnPlaza(site), default);
        Assert.Single(sim.Enemies, e => e.Kind == EnemyKind.Pedlar);
    }

    [Fact]
    public void HeStandsHisGroundWhileYouWalkUpToTrade()
    {
        var (world, site) = FindSite();
        var sim = new CombatSim(world, site);
        sim.EnableRelations();
        var spot = Merchant.PedlarSpot(site);
        var view = new PlayerView(spot + new Vec3(2.7, 0, 0), spot + new Vec3(2.7, 1.62, 0), new Vec3(-1, 0, 0), false, false);

        for (int i = 0; i < 120; i++) sim.Tick(Dt, view, default);

        var pedlar = Assert.Single(sim.Enemies, e => e.Kind == EnemyKind.Pedlar);
        Assert.InRange(pedlar.Feet.DistanceTo(spot), 0, 1.0);
        Assert.NotEqual(Intent.Flee, pedlar.Mind!.Intent);
    }

    [Fact]
    public void HeIsPassiveAndNotPartOfAnEncounter()
    {
        var (world, site) = FindSite();
        var sim = new CombatSim(world, site);
        sim.EnableRelations();
        sim.Tick(Dt, OnPlaza(site), default);

        var pedlar = sim.Enemies.Single(e => e.Kind == EnemyKind.Pedlar);
        Assert.Equal(Disposition.Passive, pedlar.Mind!.Species.Natural);
        Assert.Equal(-1, pedlar.RoomId);
        Assert.DoesNotContain(sim.DrainEvents(), e => e is EncounterStarted);
    }

    [Fact]
    public void ARespawnBringsHimBackToThePlaza()
    {
        var (world, site) = FindSite();
        var sim = new CombatSim(world, site);
        sim.EnableRelations();
        sim.Tick(Dt, OnPlaza(site), default);
        Assert.Single(sim.Enemies, e => e.Kind == EnemyKind.Pedlar);

        sim.Respawn();
        Assert.DoesNotContain(sim.Enemies, e => e.Kind == EnemyKind.Pedlar);

        sim.Tick(Dt, OnPlaza(site), default);
        Assert.Single(sim.Enemies, e => e.Kind == EnemyKind.Pedlar);
    }

    [Fact]
    public void WithoutRelationsThereIsNoPedlar()
    {
        var (world, site) = FindSite();
        var sim = new CombatSim(world, site);
        sim.Tick(Dt, OnPlaza(site), default);
        Assert.DoesNotContain(sim.Enemies, e => e.Kind == EnemyKind.Pedlar);
    }
}
