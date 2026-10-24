using Hollowfall.Core.Combat;
using Hollowfall.Core.Physics;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

/// <summary>One fixed step of scripted input, as a tool-assisted run feeds it to the game.</summary>
public readonly record struct TasInput(double MoveX = 0, double MoveZ = 0, bool Jump = false, bool Slam = false, PlayerActions Actions = default);

/// <summary>
/// Plays a frame-by-frame input script through <see cref="PlayerBody"/>, the voxel world and <see cref="CombatSim"/>, in the
/// order the game loop uses (slam request, body step, slam landing, sim tick). No engine, so a script is fully deterministic.
/// </summary>
public sealed class TasRun
{
    private const double Dt = 1.0 / 60;
    private const double EyeHeight = 1.62;

    private readonly VoxelWorld _world;

    public TasRun(VoxelWorld world, CombatSim sim, double x, double y, double z)
    {
        _world = world;
        Sim = sim;
        Body = new PlayerBody(x, y, z);
    }

    public PlayerBody Body { get; }
    public CombatSim Sim { get; }

    public static IEnumerable<TasInput> Wait(int ticks) => Enumerable.Repeat(default(TasInput), ticks);

    public void Play(IEnumerable<TasInput> script)
    {
        foreach (var input in script)
        {
            if (input.Slam) Body.BeginSlam();
            Body.Step(Dt, input.MoveX, input.MoveZ, input.Jump, _world.GetBlock);
            if (Body.TakeSlamLanding() is { } fall) Sim.Slam(Feet, fall);
            Sim.Tick(Dt, View(), input.Actions);
            Sim.DrainEvents();
            Sim.TakeDirtyChunks();
        }
    }

    private Vec3 Feet => new(Body.X, Body.Y, Body.Z);

    private PlayerView View()
    {
        var feet = Feet;
        return new PlayerView(feet, feet + new Vec3(0, EyeHeight, 0), new Vec3(1, 0, 0), !Body.OnGround, false);
    }
}

public class TasPlaytestTests
{
    [Fact]
    public void SlamOnTheSameBrickTwiceBreaksAHoleThePlayerFallsThrough()
    {
        var a = CombatSimTests.MakeArena();
        int bx = (int)Math.Floor(a.X), bz = (int)Math.Floor(a.Z), by = (int)Math.Floor(a.Y) - 1;
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                a.World.SetBlock(bx + dx, by, bz + dz, BlockType.BrickFloor);
        var husk = a.Sim.SpawnEnemy(EnemyKind.Husk, new Vec3(a.X + 2, a.Y, a.Z));
        double huskBefore = husk.Health;
        double scoreBefore = a.Sim.Style.TotalScore;

        var run = new TasRun(a.World, a.Sim, a.X, a.Y + 3, a.Z);
        run.Play([new TasInput(Slam: true), .. TasRun.Wait(40)]);

        Assert.True(run.Body.OnGround);
        Assert.Equal(BlockType.CrackedFloor, a.World.GetBlock(bx, by, bz));
        Assert.True(husk.Health < huskBefore, "the first slam's shockwave should reach a husk 2 blocks away");

        run.Play([new TasInput(Jump: true), .. TasRun.Wait(10), new TasInput(Slam: true), .. TasRun.Wait(120)]);

        Assert.Equal(BlockType.Air, a.World.GetBlock(bx, by, bz));
        Assert.False(BlockHole.IsSolid(BlockType.BrickFloor, a.World.HoleAt(bx + 1, by, bz + 1), 0, 5, 0));
        Assert.True(run.Body.Y < a.Y - 0.5, $"the player should fall through the hole, ended at y={run.Body.Y:F2}");
        Assert.True(a.Sim.Style.TotalScore > scoreBefore);
    }

    [Fact]
    public void ABreachQueuesTheChunksItChangedForRemeshing()
    {
        var a = CombatSimTests.MakeArena();
        int bx = (int)Math.Floor(a.X), bz = (int)Math.Floor(a.Z), by = (int)Math.Floor(a.Y) - 1;
        a.World.SetBlock(bx, by, bz, BlockType.BrickFloor);
        var feet = new Vec3(bx + 0.5, by + 1, bz + 0.5);

        a.Sim.Slam(feet, 2);
        a.Sim.TakeDirtyChunks();
        a.Sim.Slam(feet, 2);

        Assert.Equal(BlockType.Air, a.World.GetBlock(bx, by, bz));
        Assert.Contains((bx >> 4, bz >> 4), a.Sim.TakeDirtyChunks());
    }

    [Fact]
    public void SlamOnTheGroundDoesNothing()
    {
        var a = CombatSimTests.MakeArena();
        var run = new TasRun(a.World, a.Sim, a.X, a.Y, a.Z);
        run.Play(TasRun.Wait(60));
        double restY = run.Body.Y;

        run.Play([new TasInput(Slam: true), .. TasRun.Wait(30)]);

        Assert.False(run.Body.Slamming);
        Assert.Equal(restY, run.Body.Y, 6);
    }
}
