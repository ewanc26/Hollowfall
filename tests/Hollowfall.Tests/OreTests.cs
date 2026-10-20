using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class OreTests
{
    private static Dictionary<BlockType, long> Census(TerrainGenerator g, int radius, Action<Chunk, int, int, int, BlockType>? visit = null)
    {
        var counts = new Dictionary<BlockType, long>();
        for (int cx = -radius; cx <= radius; cx++)
            for (int cz = -radius; cz <= radius; cz++)
            {
                var c = g.GenerateChunk(cx, cz);
                for (int x = 0; x < Chunk.Size; x++)
                    for (int y = 0; y < Chunk.Height; y++)
                        for (int z = 0; z < Chunk.Size; z++)
                        {
                            var b = c[x, y, z];
                            counts[b] = counts.GetValueOrDefault(b) + 1;
                            visit?.Invoke(c, x, y, z, b);
                        }
            }
        return counts;
    }

    [Fact]
    public void EveryOreAppearsInAReasonableArea()
    {
        var counts = Census(new TerrainGenerator(11), 4);
        foreach (var spec in OreSpec.Defaults)
            Assert.True(counts.GetValueOrDefault(spec.Block) > 100, $"{spec.Block} missing");
    }

    [Fact]
    public void OresStayWithinTheirDepthBand()
    {
        var bands = OreSpec.Defaults.ToDictionary(o => o.Block);
        Census(new TerrainGenerator(5), 3, (_, _, y, _, b) =>
        {
            if (bands.TryGetValue(b, out var spec))
                Assert.InRange(y, spec.MinY, spec.MaxY);
        });
    }

    [Fact]
    public void OresAreRarerThanStoneAndOrderedByRarity()
    {
        var counts = Census(new TerrainGenerator(99), 4);
        long stone = counts[BlockType.Stone];
        long coal = counts[BlockType.CoalOre], iron = counts[BlockType.IronOre], gold = counts[BlockType.GoldOre];
        Assert.True(coal + iron + gold < stone * 0.06, "ores should be a small fraction of rock");
        Assert.True(coal > iron && iron > gold, $"expected coal>iron>gold, got {coal}/{iron}/{gold}");
    }

    [Fact]
    public void BedrockFloorSurvivesOreGeneration()
    {
        var g = new TerrainGenerator(31);
        Census(g, 2, (_, _, y, _, b) =>
        {
            if (y == 0) Assert.Equal(BlockType.Bedrock, b);
        });
    }

    [Fact]
    public void StoneOrOreIsPureAndSeeded()
    {
        var a = new TerrainGenerator(8);
        var b = new TerrainGenerator(8);
        var other = new TerrainGenerator(9);
        bool differs = false;
        for (int i = 0; i < 3000; i++)
        {
            int x = i * 7 - 9000, y = 5 + i % 60, z = i * 13 - 20000;
            Assert.Equal(a.StoneOrOre(x, y, z), b.StoneOrOre(x, y, z));
            if (a.StoneOrOre(x, y, z) != other.StoneOrOre(x, y, z)) differs = true;
        }
        Assert.True(differs);
    }
}
