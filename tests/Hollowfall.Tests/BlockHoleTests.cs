using Hollowfall.Core.Meshing;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class BlockHoleTests
{
    private static VoxelWorld EmptyChunk()
    {
        var world = new VoxelWorld(7);
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Height; y++)
                for (int z = 0; z < Chunk.Size; z++)
                    world.SetBlock(x, y, z, BlockType.Air);
        return world;
    }

    [Fact]
    public void ADugCubeLeavesTheBlockStandingAndShowsItsWalls()
    {
        var world = EmptyChunk();
        world.SetBlock(5, 10, 5, BlockType.Stone);

        Assert.True(world.DigCube(5, 10, 5, 5, 9, 5));

        Assert.Equal(BlockType.Stone, world.GetBlock(5, 10, 5));
        Assert.False(BlockHole.IsSolid(BlockType.Stone, world.HoleAt(5, 10, 5), 5, 9, 5));
        // 600 outer cube faces, minus the dug top cube's outer face, plus its 5 walls into the rest of the block.
        var m = ChunkMesher.Build(0, 0, world.GetBlock, world.HoleAt);
        Assert.Equal(604, m.Opaque.TriangleCount / 2);
    }

    [Fact]
    public void ADugCubeIsNotDugTwice()
    {
        var world = EmptyChunk();
        world.SetBlock(5, 10, 5, BlockType.Stone);

        Assert.True(world.DigCube(5, 10, 5, 1, 2, 3));
        Assert.False(world.DigCube(5, 10, 5, 1, 2, 3));
        Assert.Equal(1, world.HoleAt(5, 10, 5)!.Count);
    }

    [Fact]
    public void SettingABlockClearsItsHole()
    {
        var world = EmptyChunk();
        world.SetBlock(5, 10, 5, BlockType.Stone);
        world.DigCube(5, 10, 5, 0, 0, 0);

        world.SetBlock(5, 10, 5, BlockType.Stone);
        Assert.NotNull(world.HoleAt(5, 10, 5));

        world.SetBlock(5, 10, 5, BlockType.Dirt);
        Assert.Null(world.HoleAt(5, 10, 5));
    }

    [Fact]
    public void AirAndNonSolidBlocksCannotBeDug()
    {
        var world = EmptyChunk();

        Assert.False(world.DigCube(5, 10, 5, 0, 0, 0));
        Assert.Null(world.HoleAt(5, 10, 5));
    }
}
