using Hollowfall.Core.Combat;
using Hollowfall.Core.Input;
using Hollowfall.Core.Replay;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class MortarTests
{
    private const double Dt = 1.0 / 60;

    [Fact]
    public void CarvesASphereOfBreakableBlocksAndLeavesTheRest()
    {
        var world = new VoxelWorld(7);
        const int x = 500, y = 40, z = 500;
        for (int dx = -4; dx <= 4; dx++)
            for (int dy = -4; dy <= 4; dy++)
                for (int dz = -4; dz <= 4; dz++)
                    world.SetBlock(x + dx, y + dy, z + dz, BlockType.Stone);
        world.SetBlock(x + 1, y, z, BlockType.Gate);

        int removed = MortarBlast.Carve(world, new Vec3(x + 0.5, y + 0.5, z + 0.5));

        Assert.Equal(BlockType.Air, world.GetBlock(x, y, z));
        Assert.Equal(BlockType.Air, world.GetBlock(x + 2, y, z));
        Assert.Equal(BlockType.Stone, world.GetBlock(x + 3, y, z));
        Assert.Equal(BlockType.Gate, world.GetBlock(x + 1, y, z));
        Assert.True(removed > 0 && removed <= 90, $"removed {removed}");
    }

    [Theory]
    [InlineData(BlockType.Stone, true)]
    [InlineData(BlockType.StoneBrick, true)]
    [InlineData(BlockType.BrickFloor, true)]
    [InlineData(BlockType.Dirt, true)]
    [InlineData(BlockType.Sand, true)]
    [InlineData(BlockType.Bedrock, false)]
    [InlineData(BlockType.Gate, false)]
    [InlineData(BlockType.Door, false)]
    [InlineData(BlockType.Pillar, false)]
    [InlineData(BlockType.SecretDoor, false)]
    [InlineData(BlockType.Air, false)]
    public void OnlyBrickEarthAndStoneCanBeDug(BlockType block, bool breakable) =>
        Assert.Equal(breakable, BlockInfo.Breakable(block));

    [Fact]
    public void DamageFallsFromTheCentreToTheEdge()
    {
        Assert.Equal(MortarBlast.CenterDamage, MortarBlast.DamageAt(0), 6);
        Assert.Equal(MortarBlast.EdgeDamage, MortarBlast.DamageAt(MortarBlast.Radius), 6);
        Assert.True(MortarBlast.DamageAt(1) > MortarBlast.DamageAt(2));
    }

    [Fact]
    public void ASecondShotWithinTheCooldownIsRefused()
    {
        var a = CombatSimTests.MakeArena();
        var view = new PlayerView(new Vec3(a.X, a.Y, a.Z), new Vec3(a.X, a.Y + 1.62, a.Z), new Vec3(1, 0, 0), false, false);
        var fire = new PlayerActions(false, false, false, FireMortar: true);

        a.Sim.Tick(Dt, view, fire);
        a.Sim.Tick(Dt, view, fire);

        Assert.Single(a.Sim.Shells);
    }

    [Fact]
    public void AShellDigsTheFloorAndHurtsAnEnemyNearTheImpact()
    {
        var a = CombatSimTests.MakeArena();
        int bx = (int)Math.Floor(a.X), bz = (int)Math.Floor(a.Z), by = (int)Math.Floor(a.Y) - 1;
        for (int dx = -3; dx <= 3; dx++)
            for (int dz = -3; dz <= 3; dz++)
                a.World.SetBlock(bx + dx, by, bz + dz, BlockType.Stone);
        var husk = a.Sim.SpawnEnemy(EnemyKind.Husk, new Vec3(a.X + 1, a.Y, a.Z));
        double huskBefore = husk.Health;

        // Lob the shell straight down onto the floor, from over the player's head.
        var down = new PlayerView(new Vec3(a.X, a.Y, a.Z), new Vec3(a.X, a.Y + 1.62, a.Z), new Vec3(0, -1, 0), false, false);
        a.Sim.Tick(Dt, down, new PlayerActions(false, false, false, FireMortar: true));
        for (int i = 0; i < 60 && a.Sim.Shells.Count > 0; i++) a.Sim.Tick(Dt, down, default);

        Assert.Empty(a.Sim.Shells);
        Assert.Equal(BlockType.Air, a.World.GetBlock(bx, by, bz));
        Assert.Contains((bx >> 4, bz >> 4), a.Sim.TakeDirtyChunks());
        Assert.Equal(BlockType.Air, a.World.GetBlock(bx + 2, by, bz));
        Assert.Contains(a.Sim.DrainEvents(), e => e is ProjectileGone { Exploded: true });
        Assert.True(husk.Health < huskBefore, "the blast should reach a husk a block away");
    }

    [Fact]
    public void TheFireMortarFlagSurvivesAReplayFrame()
    {
        var view = new PlayerView(new Vec3(1, 2, 3), new Vec3(1, 3.6, 3), new Vec3(1, 0, 0), false, false);

        var frame = ReplayFrame.From(view, new PlayerActions(false, false, false, FireMortar: true));

        Assert.True(frame.InRange);
        Assert.True(frame.ToActions().FireMortar);
        Assert.False(ReplayFrame.From(view, default).ToActions().FireMortar);
    }
}

public class MortarBindingTests
{
    [Fact]
    public void MortarHasADefaultOnKeyboardAndGamepadThatDoNotClash()
    {
        var m = new BindingMap();
        m.ResetToDefaults();

        Assert.Equal(Binding.Key("T"), m.Get(GameAction.FireMortar, Slot.KeyboardMouse));
        Assert.Equal(Binding.Button("DpadLeft"), m.Get(GameAction.FireMortar, Slot.Gamepad));
        Assert.Null(m.FindConflict(Binding.Key("T"), GameAction.FireMortar));
        Assert.Null(m.FindConflict(Binding.Button("DpadLeft"), GameAction.FireMortar));
        Assert.Equal(ActionGroup.Combat, ActionInfo.GroupOf(GameAction.FireMortar));
    }
}
