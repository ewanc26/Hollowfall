using Hollowfall.Core.Combat;
using Hollowfall.Core.Input;
using Hollowfall.Core.Physics;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class SlamTests
{
    private const double Dt = 1.0 / 60;

    /// <summary>A flat stone floor whose top face is at y=10.</summary>
    private static Func<int, int, int, BlockType> Floor() => (_, y, _) => y <= 9 ? BlockType.Stone : BlockType.Air;

    private static PlayerBody Settled()
    {
        var body = new PlayerBody(0.5, 10, 0.5);
        body.Step(Dt, 0, 0, false, Floor());
        return body;
    }

    [Fact]
    public void SlamIsRefusedOnTheGround()
    {
        var body = Settled();

        Assert.True(body.OnGround);
        Assert.False(body.BeginSlam());
        Assert.False(body.Slamming);
    }

    [Fact]
    public void SlamDropsStraightDownAtSlamSpeedAndIgnoresSteering()
    {
        var body = new PlayerBody(0.5, 30, 0.5);

        Assert.True(body.BeginSlam());
        body.Step(Dt, 1, 0, false, Floor());

        Assert.Equal(-PlayerBody.SlamSpeed, body.VelocityY, 6);
        Assert.Equal(0.0, body.VelocityX, 6);
        Assert.Equal(0.0, body.VelocityZ, 6);
        Assert.Equal(0.5, body.X, 6);
    }

    [Fact]
    public void LandingReportsTheHeightDroppedOnce()
    {
        var body = new PlayerBody(0.5, 30, 0.5);
        body.BeginSlam();
        for (int i = 0; i < 300 && !body.OnGround; i++) body.Step(Dt, 0, 0, false, Floor());

        Assert.True(body.OnGround);
        Assert.False(body.Slamming);
        Assert.Equal(20.0, body.TakeSlamLanding()!.Value, 6);
        Assert.Null(body.TakeSlamLanding());
    }

    [Fact]
    public void TeleportEndsASlam()
    {
        var body = new PlayerBody(0.5, 30, 0.5);
        body.BeginSlam();

        body.Teleport(0.5, 30, 0.5, 0, 0, 0);

        Assert.False(body.Slamming);
    }

    private static BlockType Block(VoxelWorld world, int x, int y, int z) => world.GetBlock(x, y, z);

    private static VoxelWorld Brick3x3(int x, int y, int z)
    {
        var world = new VoxelWorld(7);
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                world.SetBlock(x + dx, y, z + dz, BlockType.BrickFloor);
        return world;
    }

    [Fact]
    public void FirstSlamCracksAndASecondWithinThreeSecondsBreaksARoundHole()
    {
        const int x = 500, y = 40, z = 500;
        var world = Brick3x3(x, y, z);
        var breach = new SlamBreach();

        Assert.Equal(FloorSlam.Cracked, breach.Land(world, x, y, z, 0));
        Assert.Equal(BlockType.CrackedFloor, Block(world, x, y, z));

        Assert.Equal(FloorSlam.Broken, breach.Land(world, x, y, z, 2.9));
        Assert.Equal(BlockType.Air, Block(world, x, y, z));
        Assert.False(BlockHole.IsSolid(BlockType.BrickFloor, world.HoleAt(x + 1, y, z + 1), 0, 5, 0), "the corner nearest the impact is dug");
        Assert.True(BlockHole.IsSolid(BlockType.BrickFloor, world.HoleAt(x + 1, y, z + 1), 9, 5, 9), "the far corner is still floor");
        Assert.Equal(BlockType.BrickFloor, Block(world, x - 1, y, z));
        Assert.False(BlockHole.IsSolid(BlockType.BrickFloor, world.HoleAt(x - 1, y, z), 0, 5, 4), "the edge beside the impact is dug");
    }

    [Fact]
    public void ASecondSlamAfterTheWindowOnlyCracksAgain()
    {
        const int x = 500, y = 40, z = 500;
        var world = Brick3x3(x, y, z);
        var breach = new SlamBreach();
        breach.Land(world, x, y, z, 0);

        Assert.Equal(FloorSlam.Cracked, breach.Land(world, x, y, z, 3.1));
        Assert.Equal(BlockType.CrackedFloor, Block(world, x, y, z));
        Assert.Equal(BlockType.BrickFloor, Block(world, x + 1, y, z));
    }

    [Fact]
    public void NoHoleOpensOverAStairShaft()
    {
        const int x = 500, y = 40, z = 500;
        var world = Brick3x3(x, y, z);
        world.SetBlock(x + 1, y + 1, z, BlockType.Stairs);
        var breach = new SlamBreach();

        Assert.Equal(FloorSlam.None, breach.Land(world, x, y, z, 0));
        Assert.Equal(FloorSlam.None, breach.Land(world, x, y, z, 1));
        Assert.Equal(BlockType.BrickFloor, Block(world, x, y, z));
    }

    [Fact]
    public void SlamDoesNothingOnNonFloor()
    {
        const int x = 500, y = 40, z = 500;
        var world = Brick3x3(x, y, z);
        world.SetBlock(x, y, z, BlockType.Stone);

        Assert.Equal(FloorSlam.None, new SlamBreach().Land(world, x, y, z, 0));
        Assert.Equal(BlockType.Stone, Block(world, x, y, z));
    }

    [Fact]
    public void TheHoleOnlyTakesBrickFloor()
    {
        const int x = 500, y = 40, z = 500;
        var world = Brick3x3(x, y, z);
        world.SetBlock(x + 1, y, z, BlockType.Pillar);
        var breach = new SlamBreach();
        breach.Land(world, x, y, z, 0);
        breach.Land(world, x, y, z, 1);

        Assert.Equal(BlockType.Pillar, Block(world, x + 1, y, z));
        Assert.NotNull(world.HoleAt(x - 1, y, z - 1));
    }
}

public class SlamBindingTests
{
    [Fact]
    public void SlamHasADefaultOnKeyboardAndGamepadThatDoNotClash()
    {
        var m = new BindingMap();
        m.ResetToDefaults();

        Assert.Equal(Binding.Key("X"), m.Get(GameAction.Slam, Slot.KeyboardMouse));
        Assert.Equal(Binding.Button("DpadUp"), m.Get(GameAction.Slam, Slot.Gamepad));
        Assert.Null(m.FindConflict(Binding.Key("X"), GameAction.Slam));
        Assert.Null(m.FindConflict(Binding.Button("DpadUp"), GameAction.Slam));
        Assert.Equal(ActionGroup.Movement, ActionInfo.GroupOf(GameAction.Slam));
    }
}
