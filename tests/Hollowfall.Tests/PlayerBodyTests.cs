using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Physics;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class PlayerBodyTests
{
    private const double Dt = 1.0 / 60;

    /// <summary>A flat stone floor whose top face is at y=10, plus any extra blocks.</summary>
    private static Func<int, int, int, BlockType> Floor(params (int X, int Y, int Z)[] extra)
    {
        var set = extra.ToHashSet();
        return (x, y, z) => y <= 9 || set.Contains((x, y, z)) ? BlockType.Stone : BlockType.Air;
    }

    private static void Run(PlayerBody body, int ticks, double mx, double mz, Func<int, int, int, BlockType> world, bool jumpFirst = false)
    {
        for (int i = 0; i < ticks; i++) body.Step(Dt, mx, mz, jumpFirst && i == 0, world);
    }

    private static IEnumerable<(int, int, int)> Wall(int x, int y0, int y1, int z0, int z1) =>
        from y in Enumerable.Range(y0, y1 - y0 + 1) from z in Enumerable.Range(z0, z1 - z0 + 1) select (x, y, z);

    [Fact]
    public void FallsAndLandsOnTheGround()
    {
        var world = Floor();
        var body = new PlayerBody(0.5, 30, 0.5);
        Run(body, 120, 0, 0, world);
        Assert.True(body.OnGround);
        Assert.Equal(10.0, body.Y, 3);
        Assert.Equal(0, body.VelocityY);
    }

    [Fact]
    public void LargeTimeStepsDoNotTunnelThroughTheFloor()
    {
        var world = Floor();
        var body = new PlayerBody(0.5, 80, 0.5);
        for (int i = 0; i < 40; i++) body.Step(0.25, 0, 0, false, world);
        Assert.Equal(10.0, body.Y, 3);
    }

    [Fact]
    public void WalksAtTheWalkingSpeed()
    {
        var world = Floor();
        var body = new PlayerBody(0.5, 10, 0.5);
        Run(body, 60, 1, 0, world);
        Assert.Equal(0.5 + PlayerBody.WalkSpeed, body.X, 1);
        Run(body, 60, 1, 1, world); // diagonal input is normalised
        double travelled = Math.Sqrt(Math.Pow(body.X - (0.5 + PlayerBody.WalkSpeed), 2) + Math.Pow(body.Z - 0.5, 2));
        Assert.Equal(PlayerBody.WalkSpeed, travelled, 1);
    }

    [Fact]
    public void StopsAtAWallAndSlidesAlongIt()
    {
        var world = Floor(Wall(5, 10, 12, -20, 20).ToArray());
        var body = new PlayerBody(2.5, 10, 0.5);
        Run(body, 120, 1, 0.5, world);
        Assert.True(body.X < 5 - PlayerBody.HalfWidth + 1e-3, $"x={body.X}");
        Assert.True(body.X > 5 - PlayerBody.HalfWidth - 0.01, "should be flush against the wall");
        Assert.True(body.Z > 3, "should have slid sideways along the wall");
    }

    [Fact]
    public void StepsUpAOneBlockLedgeButNotATwoBlockWall()
    {
        var oneHigh = Floor(Enumerable.Range(5, 40).SelectMany(x => Wall(x, 10, 10, -20, 20)).ToArray());
        var body = new PlayerBody(2.5, 10, 0.5);
        Run(body, 120, 1, 0, oneHigh);
        Assert.True(body.X > 7, $"should have climbed on, x={body.X}");
        Assert.Equal(11.0, body.Y, 3);

        var twoHigh = Floor(Wall(5, 10, 11, -20, 20).ToArray());
        var blocked = new PlayerBody(2.5, 10, 0.5);
        Run(blocked, 120, 1, 0, twoHigh);
        Assert.True(blocked.X < 5);
        Assert.Equal(10.0, blocked.Y, 3);
    }

    [Fact]
    public void JumpClearsOneBlockButNotTwo()
    {
        var world = Floor();
        var body = new PlayerBody(0.5, 10, 0.5);
        double peak = 10;
        Run(body, 10, 0, 0, world); // settle onto the floor first: you cannot jump from mid-air
        body.Step(Dt, 0, 0, true, world);
        for (int i = 0; i < 90; i++)
        {
            body.Step(Dt, 0, 0, false, world);
            peak = Math.Max(peak, body.Y);
        }
        Assert.InRange(peak - 10, 1.1, 1.5);
        Assert.True(body.OnGround);
    }

    [Fact]
    public void CannotJumpInMidAir()
    {
        var world = Floor();
        var body = new PlayerBody(0.5, 20, 0.5);
        body.Step(Dt, 0, 0, true, world);
        Assert.True(body.VelocityY < 0);
    }

    [Fact]
    public void HitsItsHeadOnACeiling()
    {
        var ceiling = Floor(Enumerable.Range(-3, 7).SelectMany(x => Enumerable.Range(-3, 7).Select(z => (x, 12, z))).ToArray());
        var body = new PlayerBody(0.5, 10, 0.5);
        body.Step(Dt, 0, 0, true, ceiling);
        for (int i = 0; i < 30; i++) body.Step(Dt, 0, 0, false, ceiling);
        Assert.True(body.Y <= 12 - PlayerBody.Height + 1e-3 || body.OnGround);
        Assert.True(body.Y + PlayerBody.Height <= 12 + 1e-3, $"head at {body.Y + PlayerBody.Height}");
    }

    [Fact]
    public void FitsThroughAOneBlockWideGap()
    {
        var gap = Floor(Wall(5, 10, 12, -20, -1).Concat(Wall(5, 10, 12, 1, 20)).ToArray()); // opening at z=0
        var body = new PlayerBody(2.5, 10, 0.5);
        Run(body, 150, 1, 0, gap);
        Assert.True(body.X > 7, $"x={body.X}");
    }

    [Fact]
    public void WaterAndPlantsAreNotObstacles()
    {
        var world = (int x, int y, int z) => y <= 9 ? BlockType.Stone : y == 10 && x == 3 ? BlockType.Flower : y == 10 && x == 4 ? BlockType.Water : BlockType.Air;
        var body = new PlayerBody(0.5, 10, 0.5);
        Run(body, 100, 1, 0, world);
        Assert.True(body.X > 5);
    }

    [Fact]
    public void IsDeterministic()
    {
        var world = Floor(Wall(5, 10, 10, -20, 20).ToArray());
        var a = new PlayerBody(0.5, 20, 0.5);
        var b = new PlayerBody(0.5, 20, 0.5);
        for (int i = 0; i < 300; i++)
        {
            a.Step(Dt, 0.7, 0.2, i % 90 == 0, world);
            b.Step(Dt, 0.7, 0.2, i % 90 == 0, world);
        }
        Assert.Equal((a.X, a.Y, a.Z), (b.X, b.Y, b.Z));
    }

    [Fact]
    public void FindPathRoutesAroundWalls()
    {
        var world = Floor(Wall(5, 10, 12, -4, 6).ToArray());
        var path = Navigation.FindPath(world, new(2, 10, 0), new(8, 10, 0), (x, z) => Math.Abs(x) < 30 && Math.Abs(z) < 30);
        Assert.NotNull(path);
        Assert.True(path!.Count > 7, "must detour around the wall");
        Assert.All(path.Zip(path.Skip(1)), p => Assert.Equal(1, Math.Abs(p.First.X - p.Second.X) + Math.Abs(p.First.Z - p.Second.Z)));
        Assert.Null(Navigation.FindPath(world, new(2, 10, 0), new(8, 20, 0), (_, _) => true)); // goal in mid-air
    }

    [Fact]
    public void ARealPlayerWalksFromTheSurfaceToTheBossThroughRealCollision()
    {
        int tested = 0;
        for (long seed = 1; seed <= 6; seed++)
        {
            var world = new VoxelWorld(seed);
            for (int rx = -1; rx <= 1 && tested < 6; rx++)
                for (int rz = -1; rz <= 1 && tested < 6; rz++)
                {
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is not { } site) continue;
                    tested++;
                    WalkToBoss(world, site);
                }
        }
        Assert.True(tested >= 5, $"only {tested} dungeons found");
    }

    private static void WalkToBoss(VoxelWorld world, DungeonSite site)
    {
        var start = new Spot(site.EntranceX + DungeonSite.PlazaRadius, site.SurfaceY + 1, site.EntranceZ);
        var boss = site.Floors[^1].BossRoom!;
        var (bx, by, bz) = BossSpot(site, boss);

        // Walks, and over each gap the portal hop that GapCrossing proved with the real portals and body.
        var route = DungeonReach.Route(site, start, new Spot(bx, by, bz));
        Assert.NotNull(route);

        var body = new PlayerBody(start.X + 0.5, start.Y, start.Z + 0.5);
        int waypoint = 0, stuck = 0;
        double lastProgress = double.MaxValue;
        for (int tick = 0; tick < 60 * 900 && waypoint < route!.Count; tick++)
        {
            var step = route[waypoint];
            if (step.Via is { } hop)
            {
                // Standing where the hop starts: the crossing itself is checked in MixedLinkTests, so arrive where it ends.
                body.Teleport(hop.Plan.Arrival.X, hop.Plan.Arrival.Y, hop.Plan.Arrival.Z, 0, 0, 0);
                waypoint++;
                stuck = 0;
                lastProgress = double.MaxValue;
                continue;
            }

            var target = step.To;
            double dx = target.X + 0.5 - body.X, dz = target.Z + 0.5 - body.Z;
            double dist = Math.Sqrt(dx * dx + dz * dz);
            if (dist < 0.12 && Math.Abs(body.Y - target.Y) < 0.6) { waypoint++; stuck = 0; lastProgress = double.MaxValue; continue; }

            body.Step(1.0 / 60, dx, dz, jump: false, world.GetBlock);

            stuck = dist < lastProgress - 1e-4 || Math.Abs(body.Y - target.Y) >= 0.6 && !body.OnGround ? 0 : stuck + 1;
            lastProgress = Math.Min(lastProgress, dist);
            Assert.True(stuck < 120, $"stuck at waypoint {waypoint}/{route.Count} ({target}) body ({body.X:F2},{body.Y:F2},{body.Z:F2})");
        }
        Assert.Equal(route!.Count, waypoint);
        Assert.True(Math.Abs(body.Y - by) < 0.6, $"ended at y={body.Y}, expected near {by}");
    }

    private static (int X, int Y, int Z) BossSpot(DungeonSite site, Room boss)
    {
        var floor = site.Floors[^1];
        for (int r = 0; r <= 4; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                    if (floor.CellAt(boss.CenterX + dx, boss.CenterZ + dz) == CellKind.Floor)
                        return site.ToWorld(site.FloorCount - 1, boss.CenterX + dx, boss.CenterZ + dz);
        throw new InvalidOperationException("boss room has no floor");
    }
}
