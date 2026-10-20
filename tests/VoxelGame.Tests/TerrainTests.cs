using VoxelGame.Core.Terrain;
using VoxelGame.Core.World;

namespace VoxelGame.Tests;

public class TerrainTests
{
    private static bool Same(Chunk a, Chunk b) => a.RawBlocks.SequenceEqual(b.RawBlocks);

    [Fact]
    public void SameSeedProducesIdenticalChunks()
    {
        var a = new TerrainGenerator(12345).GenerateChunk(3, -7);
        var b = new TerrainGenerator(12345).GenerateChunk(3, -7);
        Assert.True(Same(a, b));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentChunks()
    {
        var a = new TerrainGenerator(1).GenerateChunk(0, 0);
        var b = new TerrainGenerator(2).GenerateChunk(0, 0);
        Assert.False(Same(a, b));
    }

    [Fact]
    public void GenerationOrderDoesNotMatter()
    {
        var g1 = new TerrainGenerator(99);
        var first = g1.GenerateChunk(5, 5);
        var g2 = new TerrainGenerator(99);
        g2.GenerateChunk(-2, 9); g2.GenerateChunk(4, 5);
        Assert.True(Same(first, g2.GenerateChunk(5, 5)));
    }

    [Fact]
    public void ChunkBordersAreContinuous()
    {
        var g = new TerrainGenerator(7);
        // Column heights come from a pure function, so neighbours must agree with SampleColumn.
        var left = g.GenerateChunk(0, 0);
        var right = g.GenerateChunk(1, 0);
        for (int z = 0; z < Chunk.Size; z++)
        {
            int hl = g.SampleColumn(15, z).Height;
            int hr = g.SampleColumn(16, z).Height;
            Assert.True(Math.Abs(hl - hr) <= 6, $"cliff at z={z}: {hl} vs {hr}");
            Assert.NotEqual(BlockType.Air, left[15, 2, z]);
            Assert.NotEqual(BlockType.Air, right[0, 2, z]);
        }
    }

    [Fact]
    public void BedrockFloorAndSeaLevelWater()
    {
        var g = new TerrainGenerator(2024);
        for (int cx = -4; cx < 4; cx++)
        {
            var c = g.GenerateChunk(cx, 0);
            for (int x = 0; x < Chunk.Size; x++)
            for (int z = 0; z < Chunk.Size; z++)
            {
                Assert.Equal(BlockType.Bedrock, c[x, 0, z]);
                if (g.SampleColumn(c.WorldMinX + x, z).Height < TerrainGenerator.SeaLevel)
                    Assert.Equal(BlockType.Water, c[x, TerrainGenerator.SeaLevel, z]);
            }
        }
    }

    [Fact]
    public void NoiseIsBoundedAndSeeded()
    {
        var n = new PerlinNoise(5);
        var same = new PerlinNoise(5);
        var other = new PerlinNoise(6);
        bool differs = false;
        for (int i = 0; i < 2000; i++)
        {
            double x = i * 0.37, y = i * 0.11;
            double v = n.Fbm(x, y, 4);
            Assert.InRange(v, -1.0, 1.0);
            Assert.Equal(v, same.Fbm(x, y, 4));
            if (v != other.Fbm(x, y, 4)) differs = true;
            Assert.InRange(n.Sample(x, y, i * 0.2), -1.2, 1.2);
        }
        Assert.True(differs);
    }

    [Fact]
    public void TextSeedsAreStable()
    {
        Assert.Equal(TerrainGenerator.SeedFromString("voxel"), TerrainGenerator.SeedFromString("voxel"));
        Assert.Equal(42, TerrainGenerator.SeedFromString("42"));
    }
}
