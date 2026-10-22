using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class CollapseTests
{
    private const int Floor = 2;
    private const int CentreX = 120, CentreZ = 120;
    private static readonly int Y = DungeonSite.FloorY(Floor);

    /// <summary>A square of brick floor on floor 2, 40 cells across, around the room centre.</summary>
    private static VoxelWorld Room(out CollapseSequence plan)
    {
        var world = new VoxelWorld(7);
        for (int x = CentreX - 20; x < CentreX + 20; x++)
            for (int z = CentreZ - 20; z < CentreZ + 20; z++)
                world.SetBlock(x, Y, z, BlockType.BrickFloor);
        plan = CollapseSequence.Plan(world, Floor, CentreX, CentreZ);
        return world;
    }

    private static int BricksIn(VoxelWorld world, int radius)
    {
        int count = 0;
        for (int x = CentreX - radius; x <= CentreX + radius; x++)
            for (int z = CentreZ - radius; z <= CentreZ + radius; z++)
                if (world.GetBlock(x, Y, z) == BlockType.BrickFloor) count++;
        return count;
    }

    [Fact]
    public void RingsAreSixAndCoverOnlyTwelveBlocksFromTheCentre()
    {
        var world = Room(out var plan);
        Assert.Equal(CollapseSequence.Rings, plan.RingSizes.Count);

        for (int i = 0; i < 1000 && !plan.Tick(1.0 / 60, world); i++) { }
        Assert.Equal(BlockType.BrickFloor, world.GetBlock(CentreX + 13, Y, CentreZ));
        Assert.Equal(BlockType.Air, world.GetBlock(CentreX, Y, CentreZ));
    }

    [Fact]
    public void RingsFallOneAfterAnotherEverySixTenthsOfASecond()
    {
        var world = Room(out var plan);

        plan.Tick(CollapseSequence.RingSeconds - 0.01, world);
        Assert.Equal(0, plan.RingsFired);
        Assert.Equal(BlockType.BrickFloor, world.GetBlock(CentreX, Y, CentreZ));

        plan.Tick(0.02, world);
        Assert.Equal(1, plan.RingsFired);
        Assert.Equal(BlockType.Air, world.GetBlock(CentreX, Y, CentreZ));
        Assert.Equal(BlockType.BrickFloor, world.GetBlock(CentreX + 2, Y, CentreZ));
    }

    [Fact]
    public void InnerRingsGoBeforeOuterRings()
    {
        var world = Room(out var plan);
        plan.Tick(CollapseSequence.RingSeconds, world);

        Assert.Equal(1, plan.RingsFired);
        Assert.Equal(BlockType.Air, world.GetBlock(CentreX + 1, Y, CentreZ));
        Assert.Equal(BlockType.BrickFloor, world.GetBlock(CentreX + 3, Y, CentreZ));
        Assert.Equal(BlockType.BrickFloor, world.GetBlock(CentreX + 9, Y, CentreZ));
    }

    [Fact]
    public void EachTickDigsAtMostTheCapEvenWhenRingsPileUp()
    {
        var world = Room(out var plan);
        int before = BricksIn(world, 20);
        plan.Tick(CollapseSequence.Rings * CollapseSequence.RingSeconds, world);
        int removed = before - BricksIn(world, 20);
        Assert.True(removed <= CollapseSequence.MaxBlocksPerTick, $"removed {removed} in one tick");
        Assert.True(removed > 0);

        bool done = false;
        for (int i = 0; i < 1000 && !done; i++)
        {
            int beforeTick = BricksIn(world, 20);
            done = plan.Tick(1.0 / 60, world);
            Assert.True(beforeTick - BricksIn(world, 20) <= CollapseSequence.MaxBlocksPerTick);
        }
        Assert.True(done);
    }

    [Fact]
    public void TheWholeRoomIsGoneOnceEveryRingHasFallen()
    {
        var world = Room(out var plan);
        for (int i = 0; i < 1000 && !plan.Tick(1.0 / 60, world); i++) { }
        for (int x = CentreX - 12; x <= CentreX + 12; x++)
            for (int z = CentreZ - 12; z <= CentreZ + 12; z++)
            {
                bool inside = Math.Sqrt((x - CentreX) * (x - CentreX) + (z - CentreZ) * (z - CentreZ)) < 12;
                if (inside) Assert.NotEqual(BlockType.BrickFloor, world.GetBlock(x, Y, z));
            }
    }

    [Fact]
    public void ColumnsBesideAStairShaftAreLeftStanding()
    {
        var world = new VoxelWorld(7);
        for (int x = CentreX - 20; x < CentreX + 20; x++)
            for (int z = CentreZ - 20; z < CentreZ + 20; z++)
                world.SetBlock(x, Y, z, BlockType.BrickFloor);
        world.SetBlock(CentreX, Y + 1, CentreZ, BlockType.Stairs);

        var plan = CollapseSequence.Plan(world, Floor, CentreX, CentreZ);
        for (int i = 0; i < 1000 && !plan.Tick(1.0 / 60, world); i++) { }

        Assert.Equal(BlockType.BrickFloor, world.GetBlock(CentreX, Y, CentreZ));
        Assert.Equal(BlockType.Stairs, world.GetBlock(CentreX, Y + 1, CentreZ));
        Assert.Equal(BlockType.Air, world.GetBlock(CentreX + 6, Y, CentreZ));
    }

    [Fact]
    public void TheEditedCallbackSeesEveryRemovedBlock()
    {
        var world = Room(out var plan);
        int seen = 0;
        for (int i = 0; i < 1000 && !plan.Tick(1.0 / 60, world, (_, _, _) => seen++); i++) { }
        Assert.Equal(plan.RingSizes.Sum(), seen);
    }
}
