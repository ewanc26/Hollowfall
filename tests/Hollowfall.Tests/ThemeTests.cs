using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Meshing;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.Textures;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class DungeonThemeTests
{
    private static readonly Dictionary<DungeonTheme, Tile[]> Family = new()
    {
        [DungeonTheme.Plain] = [Tile.StoneBrick, Tile.BrickCracked, Tile.BrickWeb, Tile.BrickFloor, Tile.FloorCracked, Tile.FloorBones, Tile.Pillar, Tile.Stairs],
        [DungeonTheme.Mossy] = [Tile.MossBrick, Tile.MossBrickHeavy, Tile.MossBrickVine, Tile.MossFloor, Tile.MossFloorPuddle, Tile.MossPillar, Tile.MossStairs],
        [DungeonTheme.Ashen] = [Tile.AshBrick, Tile.AshBrickCracked, Tile.AshBrickScorch, Tile.AshFloor, Tile.AshFloorBones, Tile.AshFloorEmbers, Tile.AshPillar, Tile.AshStairs],
    };

    private static readonly BlockType[] Themed = [BlockType.StoneBrick, BlockType.SecretDoor, BlockType.BrickFloor, BlockType.Pillar, BlockType.Stairs];

    private static BlockType Nothing(int x, int y, int z) => BlockType.Air;

    private static int YOnFloor(int floor, int layer) => DungeonSite.FloorY(floor) + layer;

    [Theory]
    [InlineData(0, DungeonTheme.Plain)]
    [InlineData(1, DungeonTheme.Mossy)]
    [InlineData(2, DungeonTheme.Ashen)]
    [InlineData(3, DungeonTheme.Ashen)]
    [InlineData(7, DungeonTheme.Ashen)]
    public void DeeperFloorsGetTheirOwnTheme(int floor, DungeonTheme theme)
    {
        Assert.Equal(theme, DungeonThemes.ForFloor(floor));
        for (int layer = 0; layer < DungeonSite.FloorSpacing; layer++)
            Assert.Equal(theme, DungeonThemes.ForY(YOnFloor(floor, layer)));
    }

    [Fact]
    public void TheSurfaceAndTheEntranceAreAlwaysPlain()
    {
        for (int y = DungeonThemes.TopOfFloors + 1; y < 128; y++)
        {
            Assert.Equal(-1, DungeonThemes.FloorAtY(y));
            Assert.Equal(DungeonTheme.Plain, DungeonThemes.ForY(y));
        }
    }

    [Fact]
    public void TheHeightRuleAgreesWithTheSitesOwnFloorIndex()
    {
        var site = new TerrainGenerator(1).Dungeons!.GetSite(0, 1)!;
        for (int floor = 0; floor < site.FloorCount; floor++)
            for (int layer = 0; layer < DungeonSite.FloorSpacing; layer++)
            {
                int y = YOnFloor(floor, layer);
                Assert.Equal(floor, DungeonThemes.FloorAtY(y));
                Assert.Equal(DungeonThemes.Layer(y), layer);
                if (layer < DungeonSite.FloorSpacing - 1)
                    Assert.Equal(floor, site.FloorIndexAt(site.OriginX + 1, y, site.OriginZ + 1));
            }
    }

    [Fact]
    public void EveryDungeonBlockOfASiteWearsItsFloorsTheme()
    {
        for (long seed = 1; seed <= 4; seed++)
        {
            var world = new VoxelWorld(seed);
            foreach (var site in SitesOf(seed).Take(2))
                for (int floor = 0; floor < site.FloorCount; floor++)
                {
                    var theme = DungeonThemes.ForFloor(floor);
                    int y0 = DungeonSite.FloorY(floor);
                    for (int lx = 0; lx < FloorLayout.Size; lx += 3)
                        for (int lz = 0; lz < FloorLayout.Size; lz += 3)
                            for (int dy = 0; dy < DungeonSite.FloorSpacing - 1; dy++)
                            {
                                int wx = site.OriginX + lx, wz = site.OriginZ + lz, y = y0 + dy;
                                var b = site.BlockAt(wx, y, wz) ?? BlockType.Air;
                                if (!DungeonThemes.IsThemed(b)) continue;
                                for (int face = 0; face < 6; face++)
                                {
                                    var tile = TileSelector.Pick(b, face, wx, y, wz, world.GetBlock);
                                    Assert.Contains(tile, Family[theme]);
                                }
                            }
                }
        }
    }

    private static IEnumerable<DungeonSite> SitesOf(long seed)
    {
        var g = new TerrainGenerator(seed);
        for (int rx = -2; rx <= 1; rx++)
            for (int rz = -2; rz <= 1; rz++)
                if (g.Dungeons!.GetSite(rx, rz) is { } s && s.FloorCount >= 3) yield return s;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void ASecretDoorMatchesItsWallExactlyInEveryTheme(int floor)
    {
        int y = YOnFloor(floor, 1);
        for (int x = -20; x < 20; x++)
            for (int z = -20; z < 20; z++)
                for (int yy = y; yy <= y + 4; yy++)
                    for (int face = 0; face < 6; face++)
                    {
                        Assert.Equal(TileSelector.Pick(BlockType.StoneBrick, face, x, yy, z, Nothing), TileSelector.Pick(BlockType.SecretDoor, face, x, yy, z, Nothing));
                        var theme = DungeonThemes.ForY(yy);
                        Assert.Equal(BlockPalette.Color(BlockType.StoneBrick, face, theme), BlockPalette.Color(BlockType.SecretDoor, face, theme));
                    }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ASecretDoorMeshesIdenticallyToAWallBlockInTheSamePlace(int floor)
    {
        int y = YOnFloor(floor, 2);
        for (int x = 0; x < 16; x++)
        {
            ChunkMesh Mesh(BlockType b) => ChunkMesher.Build(0, 0, (wx, wy, wz) => wx == x && wy == y && wz == 5 ? b : BlockType.Air);
            var wall = Mesh(BlockType.StoneBrick).Opaque;
            var door = Mesh(BlockType.SecretDoor).Opaque;
            Assert.Equal(wall.Uvs(), door.Uvs());
            Assert.Equal(wall.Colors(), door.Colors());
            Assert.Equal(wall.Positions(), door.Positions());
        }
    }

    [Fact]
    public void WallsShowTheirThemesDecalsAtASparseRate()
    {
        foreach (var (theme, plain, floorPlain) in new[] { (DungeonTheme.Plain, Tile.StoneBrick, Tile.BrickFloor), (DungeonTheme.Mossy, Tile.MossBrick, Tile.MossFloor), (DungeonTheme.Ashen, Tile.AshBrick, Tile.AshFloor) })
        {
            int floor = theme == DungeonTheme.Plain ? 0 : theme == DungeonTheme.Mossy ? 1 : 2;
            int walls = 0, decorated = 0, floors = 0, marked = 0;
            var seen = new HashSet<Tile>();
            for (int x = 0; x < 80; x++)
                for (int z = 0; z < 80; z++)
                    for (int layer = 1; layer <= 5; layer++)
                    {
                        var t = TileSelector.Pick(BlockType.StoneBrick, 4, x, YOnFloor(floor, layer), z, Nothing);
                        walls++;
                        if (t != plain) { decorated++; seen.Add(t); }
                        if (layer == 1)
                        {
                            var f = TileSelector.Pick(BlockType.BrickFloor, 2, x, YOnFloor(floor, 0), z, Nothing);
                            floors++;
                            if (f != floorPlain) { marked++; seen.Add(f); }
                        }
                    }
            double wallRate = (double)decorated / walls, floorRate = (double)marked / floors;
            Assert.InRange(wallRate, 0.08, 0.40);
            Assert.InRange(floorRate, 0.05, 0.25);
            Assert.True(seen.Count >= 3, $"{theme} shows only {seen.Count} decals");
        }
    }

    [Fact]
    public void CobwebsOnlyGatherInTheCornerBelowTheCeiling()
    {
        for (int x = 0; x < 60; x++)
            for (int z = 0; z < 60; z++)
                for (int layer = 1; layer <= 5; layer++)
                {
                    var t = TileSelector.Pick(BlockType.StoneBrick, 0, x, YOnFloor(0, layer), z, Nothing);
                    if (t == Tile.BrickWeb) Assert.Equal(5, layer);
                    // Floors and ceilings never get wall decals.
                    Assert.Equal(Tile.StoneBrick, TileSelector.Pick(BlockType.StoneBrick, 3, x, YOnFloor(0, layer), z, Nothing));
                }
    }

    [Fact]
    public void FloorDecalsOnlyLieOnTopOfTheFlagstones()
    {
        for (int floor = 0; floor < 3; floor++)
            for (int x = 0; x < 40; x++)
                for (int z = 0; z < 40; z++)
                    for (int face = 0; face < 6; face++)
                    {
                        var t = TileSelector.Pick(BlockType.BrickFloor, face, x, YOnFloor(floor, 0), z, Nothing);
                        if (face != 2) Assert.Contains(t, new[] { Tile.BrickFloor, Tile.MossFloor, Tile.AshFloor });
                    }
    }

    [Fact]
    public void SelectionIsDeterministicAndDependsOnlyOnPlaceAndNeighbours()
    {
        for (int i = 0; i < 500; i++)
        {
            int x = i * 7 - 900, z = i * 13 - 400, y = 8 + i % 50;
            foreach (var b in Enum.GetValues<BlockType>().Where(b => b != BlockType.Air))
                for (int face = 0; face < 6; face++)
                    Assert.Equal(TileSelector.Pick(b, face, x, y, z, Nothing), TileSelector.Pick(b, face, x, y, z, Nothing));
        }
    }

    [Fact]
    public void TerrainBlocksIgnoreTheDungeonThemesAtEveryDepth()
    {
        BlockType[] natural = [BlockType.Sand, BlockType.Gravel, BlockType.Grass, BlockType.Log, BlockType.Leaves, BlockType.CoalOre, BlockType.Bedrock, BlockType.Gate];
        foreach (var b in natural)
            for (int y = 1; y < 100; y++)
                for (int face = 0; face < 6; face++)
                {
                    Assert.Equal(BlockTextures.TileFor(b, face), TileSelector.Pick(b, face, 3, y, 4, Nothing));
                    Assert.Equal(BlockPalette.Color(b, face), BlockPalette.Color(b, face, DungeonThemes.ForY(y)));
                }
    }

    [Fact]
    public void ThemeTintsDarkenAndShiftTheDungeonPalette()
    {
        var plain = BlockPalette.Color(BlockType.StoneBrick, 4, DungeonTheme.Plain);
        var mossy = BlockPalette.Color(BlockType.StoneBrick, 4, DungeonTheme.Mossy);
        var ashen = BlockPalette.Color(BlockType.StoneBrick, 4, DungeonTheme.Ashen);
        Assert.Equal(BlockPalette.Color(BlockType.StoneBrick, 4), plain);
        Assert.True(mossy.G / mossy.R > plain.G / plain.R, "mossy leans green");
        Assert.True(ashen.R / ashen.B > plain.R / plain.B, "ashen leans warm");
        Assert.True(ashen.R + ashen.G + ashen.B < plain.R + plain.G + plain.B, "ashen is darker");
    }

    [Fact]
    public void EachThemeUsesItsOwnTilesInAMesh()
    {
        foreach (var floor in new[] { 0, 1, 2 })
        {
            int y = YOnFloor(floor, 2);
            var buf = ChunkMesher.Build(0, 0, (x, yy, z) => x == 4 && yy == y && z == 4 ? BlockType.StoneBrick : BlockType.Air).Opaque;
            var uv = buf.Uvs();
            var allowed = Family[DungeonThemes.ForFloor(floor)].Select(BlockTextures.Rect).ToList();
            for (int i = 0; i < buf.VertexCount; i++)
                Assert.Contains(allowed, r => uv[i * 2] >= r.U0 - 1e-6 && uv[i * 2] <= r.U1 + 1e-6 && uv[i * 2 + 1] >= r.V0 - 1e-6 && uv[i * 2 + 1] <= r.V1 + 1e-6);
        }
    }

    [Fact]
    public void ADungeonMeshesWithNothingButItsOwnFamiliesAndTerrain()
    {
        var world = new VoxelWorld(1);
        var site = world.Generator.Dungeons!.GetSite(0, 1)!;
        Assert.True(site.FloorCount >= 3);
        var mesh = ChunkMesher.Build((site.OriginX >> 4) + 2, (site.OriginZ >> 4) + 2, world.GetBlock);
        Assert.False(mesh.Opaque.IsEmpty);
        // Every vertex lands inside some tile of the atlas.
        var uv = mesh.Opaque.Uvs();
        var rects = Enum.GetValues<Tile>().Select(BlockTextures.Rect).ToList();
        for (int i = 0; i < uv.Length; i += 2)
            Assert.Contains(rects, r => uv[i] >= r.U0 - 1e-6 && uv[i] <= r.U1 + 1e-6 && uv[i + 1] >= r.V0 - 1e-6 && uv[i + 1] <= r.V1 + 1e-6);
    }
}

public class AtlasGrowthTests
{
    [Fact]
    public void TheAtlasStillFitsEveryTileAndStaysAPowerOfTwoHigh()
    {
        Assert.True(BlockTextures.TileCount <= BlockTextures.Rows * BlockTextures.Columns);
        Assert.Equal(0, BlockTextures.Height & (BlockTextures.Height - 1));
        Assert.True(BlockTextures.Height >= BlockTextures.Rows * BlockTextures.TileSize);
        Assert.True(BlockTextures.TileCount > 40, "the theme tiles are in the atlas");
    }

    [Fact]
    public void OriginalTilesKeepTheirPlacesOnTheAtlas()
    {
        // New tiles only ever go on the end, so a tile's rectangle never moves.
        Assert.Equal(0, (int)Tile.Stone);
        Assert.Equal(24, (int)Tile.Bedrock);
        Assert.Equal(25, (int)Tile.MossyStone);
    }

    [Fact]
    public void ThemedTilesDifferFromTheirPlainParents()
    {
        (Tile A, Tile B)[] pairs =
        [
            (Tile.StoneBrick, Tile.MossBrick), (Tile.StoneBrick, Tile.AshBrick), (Tile.BrickFloor, Tile.MossFloor), (Tile.BrickFloor, Tile.AshFloor),
            (Tile.Pillar, Tile.MossPillar), (Tile.Pillar, Tile.AshPillar), (Tile.Stairs, Tile.MossStairs), (Tile.Stairs, Tile.AshStairs),
            (Tile.StoneBrick, Tile.BrickCracked), (Tile.StoneBrick, Tile.BrickWeb), (Tile.BrickFloor, Tile.FloorBones),
            (Tile.Stone, Tile.MossyStone), (Tile.Snow, Tile.PackedSnow), (Tile.Dirt, Tile.DryDirt),
        ];
        foreach (var (a, b) in pairs)
        {
            double diff = 0;
            for (int i = 0; i < 256; i++)
            {
                var p = BlockTextures.Pixel(a, i % 16, i / 16);
                var q = BlockTextures.Pixel(b, i % 16, i / 16);
                diff += Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B);
            }
            Assert.True(diff / 256 > 0.03, $"{a} and {b} are too alike");
        }
    }

    [Fact]
    public void EmberTilesGlowAndMossTilesAreGreen()
    {
        int Count(Tile t, Func<(float R, float G, float B, float A), bool> f) => Enumerable.Range(0, 256).Count(i => f(BlockTextures.Pixel(t, i % 16, i / 16)));
        foreach (var t in new[] { Tile.AshBrickCracked, Tile.AshFloorEmbers, Tile.AshPillar, Tile.AshStairs })
            Assert.InRange(Count(t, p => p.R > 0.9f && p.G is > 0.3f and < 0.8f && p.B < 0.3f), 4, 120);
        foreach (var t in new[] { Tile.MossBrickHeavy, Tile.MossBrickVine, Tile.MossFloor, Tile.MossPillar, Tile.MossyStone })
            Assert.InRange(Count(t, p => p.G > p.R + 0.25f && p.G > p.B + 0.3f), 4, 200);
    }
}

public class SurfaceVariantTests
{
    private static Func<int, int, int, BlockType> World(Dictionary<(int, int, int), BlockType> blocks, BlockType fill = BlockType.Air) =>
        (x, y, z) => blocks.TryGetValue((x, y, z), out var b) ? b : fill;

    [Fact]
    public void DriftSidesArePackedButTheTopStaysPowdery()
    {
        var at = World(new() { [(0, 80, 0)] = BlockType.Snow, [(0, 81, 0)] = BlockType.Snow });
        Assert.Equal(Tile.PackedSnow, TileSelector.Pick(BlockType.Snow, 0, 0, 80, 0, at));
        Assert.Equal(Tile.PackedSnow, TileSelector.Pick(BlockType.Snow, 5, 0, 80, 0, at));
        Assert.Equal(Tile.Snow, TileSelector.Pick(BlockType.Snow, 2, 0, 80, 0, at));
        Assert.Equal(Tile.Snow, TileSelector.Pick(BlockType.Snow, 0, 0, 81, 0, at));
    }

    [Fact]
    public void ExposedEarthSometimesDriesAndCracksButEarthUnderWaterDoesNot()
    {
        int dry = 0, total = 0;
        for (int x = 0; x < 100; x++)
            for (int z = 0; z < 20; z++)
            {
                var open = World([]);
                var t = TileSelector.Pick(BlockType.Dirt, 0, x, 60, z, open);
                total++;
                if (t == Tile.DryDirt) dry++;
                else Assert.Equal(Tile.Dirt, t);
                Assert.Equal(Tile.Dirt, TileSelector.Pick(BlockType.Dirt, 0, x, 60, z, (_, _, _) => BlockType.Water));
                Assert.Equal(Tile.Dirt, TileSelector.Pick(BlockType.Dirt, 3, x, 60, z, open));
            }
        Assert.InRange((double)dry / total, 0.3, 0.6);
    }

    [Fact]
    public void StoneGrowsMossOnlyBesideLifeOrWater()
    {
        int dryMoss = 0, dampMoss = 0, n = 0;
        for (int x = 0; x < 100; x++)
            for (int z = 0; z < 20; z++)
            {
                n++;
                var alone = World([]);
                var wet = World(new() { [(x, 61, z)] = BlockType.Grass });
                if (TileSelector.Pick(BlockType.Stone, 4, x, 60, z, alone) == Tile.MossyStone) dryMoss++;
                if (TileSelector.Pick(BlockType.Stone, 4, x, 60, z, wet) == Tile.MossyStone) dampMoss++;
            }
        Assert.Equal(0, dryMoss);
        Assert.InRange((double)dampMoss / n, 0.45, 0.75);
    }
}

public class AtmosphereTests
{
    [Fact]
    public void EachFloorHasItsOwnFogAndTheSurfaceKeepsTheOriginalSky()
    {
        var surface = DungeonThemes.ForAtmosphere(-1);
        Assert.Equal(DungeonThemes.Surface, surface);
        var a = DungeonThemes.ForAtmosphere(0);
        var b = DungeonThemes.ForAtmosphere(1);
        var c = DungeonThemes.ForAtmosphere(2);
        Assert.Equal(c, DungeonThemes.ForAtmosphere(6));
        Assert.True(b.FogG > b.FogR && b.FogG > b.FogB, "mossy fog is green");
        Assert.True(c.FogR > c.FogG && c.FogG > c.FogB, "ashen fog is ember red");
        Assert.True(a.FogB >= a.FogR, "plain fog is cold");
        Assert.True(a.FogDensity > surface.FogDensity && c.FogDensity >= a.FogDensity, "underground is thicker than the open air");
        Assert.NotEqual(a, b);
        Assert.NotEqual(b, c);
    }

    [Fact]
    public void LerpMovesBetweenTwoAtmospheres()
    {
        var a = DungeonThemes.ForAtmosphere(0);
        var b = DungeonThemes.ForAtmosphere(2);
        Assert.Equal(a, DungeonThemes.Lerp(a, b, 0f));
        Assert.Equal(b, DungeonThemes.Lerp(a, b, 1f));
        var mid = DungeonThemes.Lerp(a, b, 0.5f);
        Assert.InRange(mid.FogR, Math.Min(a.FogR, b.FogR), Math.Max(a.FogR, b.FogR));
    }
}
