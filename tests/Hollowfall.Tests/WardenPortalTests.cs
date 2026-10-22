using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class WardenPortalTests
{
    private const double Dt = 1.0 / 60;

    /// <summary>The stone box from the portal tests: floor top at y=10, walls at x=-6 and x=10.</summary>
    private static Func<int, int, int, BlockType> Room()
    {
        return (x, y, z) =>
        {
            if (y <= 9 || y >= 20) return BlockType.Stone;
            if (x >= 10 || x <= -6) return BlockType.Stone;
            if (z >= 20 || z <= -20) return BlockType.Stone;
            return BlockType.Air;
        };
    }

    private static readonly Func<BlockType, bool> Stone = b => b == BlockType.Stone;

    private static PortalSystem WithPlayerPair(Func<int, int, int, BlockType> world, out Portal teal, out Portal violet)
    {
        var s = new PortalSystem();
        teal = s.Place(PortalColor.Teal, new Vec3(0.5, 11.6, 0.5), new Vec3(1, 0, 0), new Vec3(1, 0, 0), world).Portal!;
        violet = s.Place(PortalColor.Violet, new Vec3(0.5, 11.6, 0.5), new Vec3(-1, 0, 0), new Vec3(1, 0, 0), world).Portal!;
        return s;
    }

    private static bool OpenBothWalls(PortalSystem s, Func<int, int, int, BlockType> world)
    {
        var eye = new Vec3(0.5, 11.6, 0.5);
        return s.OpenWarden([(eye, new Vec3(1, 0, 0)), (eye, new Vec3(-1, 0, 0))], Stone, world);
    }

    [Fact]
    public void HisGateSwitchesYourPairOffUntilItShuts()
    {
        var world = Room();
        var s = WithPlayerPair(world, out var teal, out var violet);
        Assert.Equal(2, s.Openings.Count());

        Assert.True(OpenBothWalls(s, world));
        Assert.True(s.WardenLive);
        Assert.DoesNotContain(teal, s.Openings);
        Assert.DoesNotContain(violet, s.Openings);
        Assert.Equal(2, s.Openings.Count());

        s.CloseWarden();
        Assert.False(s.WardenLive);
        Assert.Contains(teal, s.Openings);
        Assert.Contains(violet, s.Openings);
    }

    [Fact]
    public void HisGateNeedsTwoWallsToOpen()
    {
        var world = Room();
        var s = new PortalSystem();
        var eye = new Vec3(0.5, 11.6, 0.5);
        Assert.False(s.OpenWarden([(eye, new Vec3(1, 0, 0))], Stone, world));
        Assert.False(s.WardenLive);
    }

    [Fact]
    public void SwappingExchangesThePlacedPortalsPositionsAndKeepsTheirColours()
    {
        var world = Room();
        var s = WithPlayerPair(world, out var teal, out var violet);
        s.SwapPlaced();
        Assert.Equal(PortalColor.Teal, s.Teal!.Color);
        Assert.Equal(PortalColor.Violet, s.Violet!.Color);
        Assert.Equal(violet.Frame.Center, s.Teal.Frame.Center);
        Assert.Equal(teal.Frame.Center, s.Violet.Frame.Center);
    }

    [Fact]
    public void AShotLaunchedThroughHisGateComesOutOfTheOtherFacingTheTarget()
    {
        var world = Room();
        var s = new PortalSystem();
        Assert.True(OpenBothWalls(s, world));

        var target = new Vec3(3, 11.6, 0.5);
        var (entry, start, direction) = s.WardenLaunches(target, 0).Single(l => l.Entry.Frame.Normal.X < 0);

        const double speed = 13;
        var step = start + direction * 0.6;
        var crossed = s.TryCross(start, step, direction * speed, direction);
        Assert.NotNull(crossed);
        Assert.Equal(entry, crossed!.Value.Entered);
        var exit = crossed.Value.Exited;
        Assert.Equal(-5.0, exit.Frame.Center.X, 6);

        var heading = crossed.Value.Velocity.Normalized();
        Assert.True(heading.Dot((target - exit.Frame.Center).Normalized()) > 0.99);
    }

    // ---- the Warden in the simulation -------------------------------------------------------

    private static (VoxelWorld World, DungeonSite Site) FindSite()
    {
        for (long seed = 1; seed < 40; seed++)
        {
            var world = new VoxelWorld(seed);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is { FloorCount: >= 2 } site)
                        return (world, site);
        }
        throw new InvalidOperationException("no dungeon found");
    }

    /// <summary>A tick with the player kept alive, so long fights measure the Warden and not the player's death.</summary>
    private static void Step(CombatSim sim, PlayerView view, PlayerActions actions)
    {
        sim.Vitals.Heal(1000);
        sim.Tick(Dt, view, actions);
    }

    private static (CombatSim Sim, PlayerView View, Enemy Warden, VoxelWorld World) Duel(double healthFraction)
    {
        var (world, site) = FindSite();
        var sim = new CombatSim(world, site);
        var room = site.Floors[0].EntranceRoom;
        var (wx, wy, wz) = site.ToWorld(0, room.CenterX, room.CenterZ);
        var feet = new Vec3(wx + 0.5, wy, wz + 0.5);
        var view = new PlayerView(feet, feet + new Vec3(0, 1.62, 0), new Vec3(1, 0, 0), false, false);
        sim.Tick(Dt, view, default);
        var warden = sim.SpawnEnemy(EnemyKind.Warden, feet + new Vec3(4, 0, 0), 0, room.Id);
        warden.SetHealth(260 * healthFraction);
        return (sim, view, warden, world);
    }

    [Fact]
    public void PhaseTwoOpensHisGateAndYourPortalsStopWorking()
    {
        var (sim, view, _, world) = Duel(0.5);
        sim.Tick(Dt, view, default);
        Assert.True(sim.Portals.WardenLive);

        sim.Portals.Place(PortalColor.Teal, view.Eye, new Vec3(0, 0, 1), new Vec3(0, 0, 1), world.GetBlock);
        sim.Portals.Place(PortalColor.Violet, view.Eye, new Vec3(0, 0, -1), new Vec3(0, 0, 1), world.GetBlock);
        sim.Tick(Dt, view, default);
        Assert.DoesNotContain(sim.Portals.Openings, p => p.Color == PortalColor.Teal);

        sim.DrainEvents();
        sim.Tick(Dt, view, new PlayerActions(false, false, false, PortalTeal: true));
        Assert.Contains(sim.DrainEvents(), e => e is PortalPlaced { Success: false, Failure: PlaceFailure.Disabled });
    }

    [Fact]
    public void HisGateCyclesOpenAndShut()
    {
        var (sim, view, _, _) = Duel(0.5);
        Step(sim, view, default);
        Assert.True(sim.Portals.WardenLive);

        for (int i = 0; i < (int)((WardenGateSecondsForTest + 0.2) / Dt); i++) Step(sim, view, default);
        Assert.False(sim.Portals.WardenLive);

        for (int i = 0; i < (int)((WardenGateSecondsForTest + 0.2) / Dt); i++) Step(sim, view, default);
        Assert.True(sim.Portals.WardenLive);
    }

    private const double WardenGateSecondsForTest = CombatSim.WardenGateSeconds;

    [Fact]
    public void PhaseThreeSwapsYourPortalsOnATimer()
    {
        var (sim, view, _, world) = Duel(0.2);
        sim.Portals.Place(PortalColor.Teal, view.Eye, new Vec3(0, 0, 1), new Vec3(0, 0, 1), world.GetBlock);
        sim.Portals.Place(PortalColor.Violet, view.Eye, new Vec3(0, 0, -1), new Vec3(0, 0, 1), world.GetBlock);
        var teal = sim.Portals.Teal!.Frame.Center;
        var violet = sim.Portals.Violet!.Frame.Center;
        Assert.NotEqual(teal, violet);

        for (int i = 0; i < (int)((CombatSim.PortalSwapSeconds - 1) / Dt); i++) Step(sim, view, default);
        Assert.Equal(teal, sim.Portals.Teal!.Frame.Center);

        for (int i = 0; i < (int)(2 / Dt); i++) Step(sim, view, default);
        Assert.Equal(violet, sim.Portals.Teal!.Frame.Center);
        Assert.Equal(PortalColor.Teal, sim.Portals.Teal.Color);
    }

    [Fact]
    public void HisGateWaitsWhileYouStandInOneOfYourPortals()
    {
        var (sim, view, _, world) = Duel(0.5);
        sim.Portals.Place(PortalColor.Teal, view.Eye, new Vec3(0, 0, 1), new Vec3(0, 0, 1), world.GetBlock);
        sim.Portals.Place(PortalColor.Violet, view.Eye, new Vec3(0, 0, -1), new Vec3(0, 0, 1), world.GetBlock);
        var hole = sim.Portals.Teal!.HoleCells[0];
        var inside = new PlayerView(new Vec3(hole.X + 0.5, hole.Y, hole.Z + 0.5), new Vec3(hole.X + 0.5, hole.Y + 1.62, hole.Z + 0.5), new Vec3(0, 0, -1), false, false);

        for (int i = 0; i < 60 * 3; i++)
        {
            Step(sim, inside, default);
            Assert.False(sim.Portals.WardenLive);
        }

        for (int i = 0; i < 60; i++) Step(sim, view, default);
        Assert.True(sim.Portals.WardenLive);
    }

    [Fact]
    public void AReflectedShotThroughHisGateComesBackAndHurtsHim()
    {
        var (sim, view, warden, _) = Duel(0.5);
        double startHealth = warden.Health;
        bool reflected = false, crossedAfter = false;
        for (int i = 0; i < 60 * 40 && warden.Health >= startHealth; i++)
        {
            var incoming = sim.Projectiles.FirstOrDefault(p => !p.Reflected && p.Position.DistanceTo(view.Eye) <= CombatSim.ParryReach * 0.8);
            var actions = default(PlayerActions);
            var aimAt = incoming?.Position ?? warden.Feet + new Vec3(0, 1.0, 0);
            var aimed = Aim(view, aimAt);
            if (incoming != null) actions = new PlayerActions(false, false, true);
            Step(sim, aimed, actions);
            var events = sim.DrainEvents().ToList();
            if (events.OfType<ProjectileReflected>().Any()) reflected = true;
            if (reflected && events.OfType<PortalTraversed>().Any(e => e.What == "projectile")) crossedAfter = true;
        }
        Assert.True(reflected, "no incoming shot was parried");
        Assert.True(crossedAfter, "the reflected shot never went back through a gate");
        Assert.True(warden.Health < startHealth, "the reflected shot did not hurt the Warden");
    }

    private static PlayerView Aim(PlayerView v, Vec3 at) =>
        v with { Forward = (at - v.Eye).Normalized() };

    [Fact]
    public void HisSwapWaitsWhileYouStandInOneOfYourPortals()
    {
        var (sim, view, _, world) = Duel(0.2);
        sim.Portals.Place(PortalColor.Teal, view.Eye, new Vec3(0, 0, 1), new Vec3(0, 0, 1), world.GetBlock);
        sim.Portals.Place(PortalColor.Violet, view.Eye, new Vec3(0, 0, -1), new Vec3(0, 0, 1), world.GetBlock);
        var teal = sim.Portals.Teal!.Frame.Center;
        var hole = sim.Portals.Teal!.HoleCells[0];
        var inside = new PlayerView(new Vec3(hole.X + 0.5, hole.Y, hole.Z + 0.5), new Vec3(hole.X + 0.5, hole.Y + 1.62, hole.Z + 0.5), new Vec3(0, 0, -1), false, false);

        for (int i = 0; i < (int)((CombatSim.PortalSwapSeconds + 2) / Dt); i++) Step(sim, inside, default);
        Assert.Equal(teal, sim.Portals.Teal!.Frame.Center);

        bool swapped = false;
        for (int i = 0; i < 60 && !swapped; i++)
        {
            Step(sim, view, default);
            swapped = sim.Portals.Teal!.Frame.Center != teal;
        }
        Assert.True(swapped, "the swap did not happen once the player stepped out");
        Assert.NotEqual(teal, sim.Portals.Teal!.Frame.Center);
    }

    [Fact]
    public void AVolleyThroughHisGateReachesYou()
    {
        var (sim, view, _, _) = Duel(0.5);
        bool through = false, hurt = false;
        for (int i = 0; i < 60 * 40 && !(through && hurt); i++)
        {
            Step(sim, view, default);
            var events = sim.DrainEvents().ToList();
            through |= events.OfType<PortalTraversed>().Any(e => e.What == "projectile" && e.Entered == PortalColor.Rift);
            hurt |= events.OfType<PlayerHurt>().Any();
        }
        Assert.True(through, "no shot from the Warden went through his gate");
        Assert.True(hurt, "a shot through his gate never reached the player");
    }
}
