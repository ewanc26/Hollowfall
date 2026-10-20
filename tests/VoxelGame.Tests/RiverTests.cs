using VoxelGame.Core.Terrain;
using VoxelGame.Core.World;

namespace VoxelGame.Tests;

public class RiverTests
{
    private static IEnumerable<(int X, int Z, ColumnInfo Col)> Grid(TerrainGenerator g, int half = 1500, int step = 6)
    {
        for (int x = -half; x < half; x += step)
            for (int z = -half; z < half; z += step)
                yield return (x, z, g.SampleColumn(x, z));
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(2L)]
    [InlineData(3L)]
    [InlineData(4L)]
    public void RiversCoverASmallButNonTrivialShareOfTheMap(long seed)
    {
        var cols = Grid(new TerrainGenerator(seed)).ToList();
        double share = cols.Count(c => c.Col.Biome == Biome.River) / (double)cols.Count;
        Assert.InRange(share, 0.01, 0.10);
    }

    [Fact]
    public void RiverColumnsAreWaterFilledAndNotBelowTheBed()
    {
        var g = new TerrainGenerator(6);
        foreach (var (_, _, col) in Grid(g))
        {
            if (col.Biome != Biome.River) continue;
            Assert.InRange(col.Height, TerrainGenerator.SeaLevel - 3, TerrainGenerator.SeaLevel);
        }
    }

    [Fact]
    public void GeneratedRiverChunksHaveGravelBedAndSurfaceWater()
    {
        var g = new TerrainGenerator(6);
        var (x, z, _) = Grid(g).First(c => c.Col.Biome == Biome.River && c.Col.Height < TerrainGenerator.SeaLevel);
        int cx = (int)Math.Floor(x / (double)Chunk.Size), cz = (int)Math.Floor(z / (double)Chunk.Size);
        var chunk = g.GenerateChunk(cx, cz);
        int lx = x - cx * Chunk.Size, lz = z - cz * Chunk.Size;
        int h = g.SampleColumn(x, z).Height;
        Assert.Equal(BlockType.Gravel, chunk[lx, h, lz]);
        Assert.Equal(BlockType.Water, chunk[lx, TerrainGenerator.SeaLevel, lz]);
        Assert.Equal(BlockType.Air, chunk[lx, TerrainGenerator.SeaLevel + 1, lz]);
    }

    [Fact]
    public void RiversAreContinuousAlongTheirLength()
    {
        // Neighbouring river columns should differ by at most a block or two: no cliffs in the bed.
        var g = new TerrainGenerator(8);
        int checkedPairs = 0;
        for (int x = -1000; x < 1000; x++)
        {
            var a = g.SampleColumn(x, 40);
            var b = g.SampleColumn(x + 1, 40);
            if (a.Biome == Biome.River && b.Biome == Biome.River)
            {
                checkedPairs++;
                Assert.True(Math.Abs(a.Height - b.Height) <= 2, $"bed jump at x={x}");
            }
        }
        Assert.True(checkedPairs > 5, "expected the sample line to cross a river");
    }

    [Fact]
    public void RiversDoNotCutThroughHighMountains()
    {
        var g = new TerrainGenerator(10);
        foreach (var (_, _, col) in Grid(g))
            if (col.Biome == Biome.River)
                Assert.True(col.Height < TerrainGenerator.SeaLevel + 5);
    }

    [Fact]
    public void RiverGenerationIsDeterministic()
    {
        var a = new TerrainGenerator(12).SampleColumn(321, -654);
        var b = new TerrainGenerator(12).SampleColumn(321, -654);
        Assert.Equal(a, b);
    }
}
