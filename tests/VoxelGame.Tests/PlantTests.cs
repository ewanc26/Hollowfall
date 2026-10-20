using VoxelGame.Core.Terrain;
using VoxelGame.Core.World;

namespace VoxelGame.Tests;

public class PlantTests
{
    private static List<Chunk> Area(TerrainGenerator g, int r)
    {
        var list = new List<Chunk>();
        for (int cx = -r; cx <= r; cx++)
            for (int cz = -r; cz <= r; cz++)
                list.Add(g.GenerateChunk(cx, cz));
        return list;
    }

    [Fact]
    public void PlantsAppearAndAreNotSolid()
    {
        Assert.False(BlockInfo.IsSolid(BlockType.TallGrass));
        Assert.False(BlockInfo.IsSolid(BlockType.Flower));
        Assert.True(BlockInfo.IsSolid(BlockType.Stone));

        long grass = 0, flower = 0;
        foreach (var c in Area(new TerrainGenerator(3), 8))
            foreach (var b in c.RawBlocks)
            {
                if (b == BlockType.TallGrass) grass++;
                if (b == BlockType.Flower) flower++;
            }
        Assert.True(grass > 500, $"grass={grass}");
        Assert.True(flower > 20, $"flowers={flower}");
        Assert.True(grass > flower);
    }

    [Fact]
    public void PlantsOnlySitOnGrassUnderAirOrCanopy()
    {
        foreach (var c in Area(new TerrainGenerator(14), 5))
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 1; y < Chunk.Height - 1; y++)
                    for (int z = 0; z < Chunk.Size; z++)
                    {
                        if (!BlockInfo.IsPlant(c[x, y, z])) continue;
                        Assert.Equal(BlockType.Grass, c[x, y - 1, z]);
                        // A neighbouring tree on higher ground may overhang the plant.
                        Assert.True(c[x, y + 1, z] is BlockType.Air or BlockType.Leaves);
                    }
    }

    [Fact]
    public void PlantsNeverGrowBelowTheWaterline()
    {
        var g = new TerrainGenerator(14);
        foreach (var c in Area(g, 4))
            for (int x = 0; x < Chunk.Size; x++)
                for (int z = 0; z < Chunk.Size; z++)
                    for (int y = 0; y <= TerrainGenerator.SeaLevel; y++)
                        Assert.False(BlockInfo.IsPlant(c[x, y, z]));
    }

    [Fact]
    public void DesertsAndMountainsHaveNoGroundCover()
    {
        var g = new TerrainGenerator(21);
        foreach (var c in Area(g, 6))
            for (int x = 0; x < Chunk.Size; x++)
                for (int z = 0; z < Chunk.Size; z++)
                {
                    var biome = g.SampleColumn(c.WorldMinX + x, c.WorldMinZ + z).Biome;
                    if (biome is Biome.Desert or Biome.Mountains or Biome.Ocean or Biome.River)
                        for (int y = 0; y < Chunk.Height; y++)
                            Assert.False(BlockInfo.IsPlant(c[x, y, z]));
                }
    }
}
