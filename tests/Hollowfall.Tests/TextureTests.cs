using System.IO.Compression;
using Hollowfall.Core.Meshing;
using Hollowfall.Core.Textures;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class BlockTextureTests
{
    public static TheoryData<Tile> Tiles => [.. Enum.GetValues<Tile>()];

    private static double Luma(Tile t, int x, int y)
    {
        var (r, g, b, _) = BlockTextures.Pixel(t, x, y);
        return 0.299 * r + 0.587 * g + 0.114 * b;
    }

    private static IEnumerable<double> Lumas(Tile t) => Enumerable.Range(0, 256).Select(i => Luma(t, i % 16, i / 16));

    [Fact]
    public void TheAtlasHasRoomForEveryTileAndIsPowerOfTwoSized()
    {
        Assert.Equal(BlockTextures.Width * BlockTextures.Height * 4, BlockTextures.Rgba.Length);
        Assert.Equal(0, BlockTextures.Height & (BlockTextures.Height - 1));
        Assert.True(BlockTextures.Rows * BlockTextures.Columns >= BlockTextures.TileCount);
    }

    [Fact]
    public void TheAtlasIsIdenticalEveryTime()
    {
        var a = BlockTextures.Rgba.ToArray();
        var b = BlockTextures.Pixel(Tile.Stone, 3, 4);
        Assert.Equal(b, BlockTextures.Pixel(Tile.Stone, 3, 4));
        Assert.Equal(a, BlockTextures.Rgba);
    }

    [Theory]
    [MemberData(nameof(Tiles))]
    public void SolidTilesAreFullyOpaqueAndPlantsHaveHoles(Tile t)
    {
        int clear = 0, solid = 0;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                if (BlockTextures.Pixel(t, x, y).A < 0.5f) clear++;
                else solid++;
            }
        if (BlockTextures.HasAlpha(t))
        {
            Assert.True(clear > 40 && solid > 20, $"{t}: {clear} clear, {solid} solid");
        }
        else
        {
            Assert.Equal(0, clear);
        }
    }

    [Theory]
    [MemberData(nameof(Tiles))]
    public void TilesAreTexturedButNotTooDarkOrBright(Tile t)
    {
        if (BlockTextures.HasAlpha(t)) return;
        var l = Lumas(t).ToList();
        double mean = l.Average();
        double sd = Math.Sqrt(l.Average(v => (v - mean) * (v - mean)));
        // With the gain, the average should land near the palette colour: not much darker or brighter.
        Assert.InRange(mean * BlockTextures.Gain, 0.55, 1.15);
        Assert.True(sd > 0.015, $"{t} is too flat (sd {sd:F4})");
        Assert.True(l.Max() <= 1.0 && l.Min() >= 0.0);
    }

    [Theory]
    [InlineData(Tile.Stone)]
    [InlineData(Tile.Dirt)]
    [InlineData(Tile.GrassTop)]
    [InlineData(Tile.Sand)]
    [InlineData(Tile.Snow)]
    [InlineData(Tile.Gravel)]
    [InlineData(Tile.Leaves)]
    [InlineData(Tile.Bedrock)]
    [InlineData(Tile.Water)]
    [InlineData(Tile.Sandstone)]
    public void NaturalTilesWrapSoNeighboursJoinWithoutSeams(Tile t)
    {
        // The step across the tile edge should be no worse, on average, than a step inside the tile.
        double inner = 0, wrap = 0;
        for (int i = 0; i < 16; i++)
        {
            for (int y = 0; y < 15; y++) inner += Math.Abs(Luma(t, i, y) - Luma(t, i, y + 1));
            wrap += Math.Abs(Luma(t, i, 15) - Luma(t, i, 16));
        }
        inner /= 15 * 16;
        wrap /= 16;
        Assert.True(wrap <= inner * 1.6 + 0.01, $"{t}: edge step {wrap:F3} vs interior {inner:F3} (vertical)");

        inner = wrap = 0;
        for (int i = 0; i < 16; i++)
        {
            for (int x = 0; x < 15; x++) inner += Math.Abs(Luma(t, x, i) - Luma(t, x + 1, i));
            wrap += Math.Abs(Luma(t, 15, i) - Luma(t, 16, i));
        }
        inner /= 15 * 16;
        wrap /= 16;
        Assert.True(wrap <= inner * 1.6 + 0.01, $"{t}: edge step {wrap:F3} vs interior {inner:F3} (horizontal)");
    }

    [Fact]
    public void EveryBlockHasATileAndASecretDoorMatchesItsWall()
    {
        foreach (var b in Enum.GetValues<BlockType>().Where(b => b != BlockType.Air))
            for (int face = 0; face < 6; face++)
                Assert.True(Enum.IsDefined(BlockTextures.TileFor(b, face)));
        for (int face = 0; face < 6; face++)
            Assert.Equal(BlockTextures.TileFor(BlockType.StoneBrick, face), BlockTextures.TileFor(BlockType.SecretDoor, face));
    }

    [Fact]
    public void GrassAndLogsWearDifferentFacesOnDifferentSides()
    {
        Assert.Equal(Tile.GrassTop, BlockTextures.TileFor(BlockType.Grass, 2));
        Assert.Equal(Tile.Dirt, BlockTextures.TileFor(BlockType.Grass, 3));
        Assert.Equal(Tile.GrassSide, BlockTextures.TileFor(BlockType.Grass, 0));
        Assert.Equal(Tile.LogEnd, BlockTextures.TileFor(BlockType.Log, 2));
        Assert.Equal(Tile.LogSide, BlockTextures.TileFor(BlockType.Log, 4));
    }

    [Fact]
    public void TheDungeonSurfacesLookDifferentFromOneAnother()
    {
        Tile[] dungeon = [Tile.StoneBrick, Tile.BrickFloor, Tile.Pillar, Tile.Stairs, Tile.Gate, Tile.Door];
        foreach (var a in dungeon)
            foreach (var b in dungeon)
                if (a < b)
                {
                    double diff = Enumerable.Range(0, 256).Average(i => Math.Abs(Luma(a, i % 16, i / 16) - Luma(b, i % 16, i / 16)));
                    Assert.True(diff > 0.03, $"{a} and {b} are too alike");
                }
    }

    [Fact]
    public void OreTilesCarryTheirColourInFlecksOnStone()
    {
        (Tile Ore, Func<(float R, float G, float B, float A), bool> Looks)[] ores =
        [
            (Tile.CoalOre, p => p.R < 0.3f && p.G < 0.3f),
            (Tile.IronOre, p => p.R > p.B + 0.25f),
            (Tile.GoldOre, p => p.R > p.B + 0.5f),
        ];
        foreach (var (ore, looks) in ores)
        {
            int flecks = Enumerable.Range(0, 256).Count(i => looks(BlockTextures.Pixel(ore, i % 16, i / 16)));
            Assert.InRange(flecks, 6, 80);
        }
    }

    [Fact]
    public void RectsStayInsideTheirOwnTileAndNeverOverlap()
    {
        var rects = Enum.GetValues<Tile>().ToDictionary(t => t, BlockTextures.Rect);
        foreach (var (t, r) in rects)
        {
            Assert.InRange(r.U0, 0, 1);
            Assert.InRange(r.V1, 0, 1);
            Assert.True(r.U1 > r.U0 && r.V1 > r.V0);
            int col = (int)t % BlockTextures.Columns;
            Assert.True(r.U0 * BlockTextures.Width > col * BlockTextures.TileSize);
            Assert.True(r.U1 * BlockTextures.Width < (col + 1) * BlockTextures.TileSize);
        }
        foreach (var a in rects)
            foreach (var b in rects)
                if (a.Key < b.Key) Assert.False(a.Value.U0 < b.Value.U1 && b.Value.U0 < a.Value.U1 && a.Value.V0 < b.Value.V1 && b.Value.V0 < a.Value.V1);
    }
}

public class MesherTextureTests
{
    private static ChunkMesh MeshOf(BlockType block)
    {
        BlockType At(int x, int y, int z) => x == 8 && y == 10 && z == 8 ? block : BlockType.Air;
        return ChunkMesher.Build(0, 0, At);
    }

    [Theory]
    [InlineData(BlockType.Stone)]
    [InlineData(BlockType.Grass)]
    [InlineData(BlockType.Log)]
    [InlineData(BlockType.StoneBrick)]
    public void EveryFaceVertexSamplesItsOwnTile(BlockType block)
    {
        var buf = MeshOf(block).Opaque;
        var uv = buf.Uvs();
        Assert.Equal(buf.VertexCount * 2, uv.Length);
        var allowed = Enumerable.Range(0, 6).Select(f => BlockTextures.Rect(BlockTextures.TileFor(block, f))).ToList();
        for (int i = 0; i < buf.VertexCount; i++)
        {
            float u = uv[i * 2], v = uv[i * 2 + 1];
            Assert.Contains(allowed, r => u >= r.U0 - 1e-6 && u <= r.U1 + 1e-6 && v >= r.V0 - 1e-6 && v <= r.V1 + 1e-6);
        }
    }

    [Fact]
    public void AGrassBlockUsesThreeDifferentTiles()
    {
        var uv = MeshOf(BlockType.Grass).Opaque.Uvs();
        var tiles = new HashSet<Tile>();
        for (int i = 0; i < uv.Length; i += 2)
            foreach (var t in Enum.GetValues<Tile>())
            {
                var r = BlockTextures.Rect(t);
                if (uv[i] >= r.U0 && uv[i] <= r.U1 && uv[i + 1] >= r.V0 && uv[i + 1] <= r.V1) tiles.Add(t);
            }
        Assert.Equal([Tile.Dirt, Tile.GrassTop, Tile.GrassSide], tiles.OrderBy(t => t switch { Tile.Dirt => 0, Tile.GrassTop => 1, _ => 2 }));
    }

    [Fact]
    public void WallTexturesRunUpTheWall()
    {
        // On a side face, the bottom vertices have the larger v (image y grows downward).
        var buf = MeshOf(BlockType.StoneBrick).Opaque;
        var p = buf.Positions();
        var n = buf.Normals();
        var uv = buf.Uvs();
        for (int i = 0; i < buf.VertexCount; i++)
            for (int j = 0; j < buf.VertexCount; j++)
            {
                bool sideA = Math.Abs(n[i * 3 + 1]) < 0.5f && n[i * 3] == n[j * 3] && n[i * 3 + 2] == n[j * 3 + 2];
                if (sideA && p[i * 3 + 1] > p[j * 3 + 1] + 0.5f) Assert.True(uv[i * 2 + 1] < uv[j * 2 + 1], "higher on the wall means higher on the picture");
            }
    }

    [Fact]
    public void PlantsMapTheirWholeTile()
    {
        var uv = MeshOf(BlockType.TallGrass).Cutout.Uvs();
        var r = BlockTextures.Rect(Tile.TallGrass);
        Assert.Contains(uv, u => Math.Abs(u - r.U0) < 1e-6);
        Assert.Contains(uv, u => Math.Abs(u - r.U1) < 1e-6);
    }
}

public class PngWriterTests
{
    [Fact]
    public void EncodesAValidPngThatRoundTrips()
    {
        var rgba = new byte[4 * 3 * 4];
        for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i * 7);
        var png = PngWriter.Encode(rgba, 4, 3);

        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
        int pos = 8;
        var idat = new MemoryStream();
        int width = 0, height = 0;
        bool end = false;
        while (pos < png.Length)
        {
            int length = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
            string type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            var data = png[(pos + 8)..(pos + 8 + length)];
            if (type == "IHDR") { width = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]; height = (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]; }
            if (type == "IDAT") idat.Write(data);
            if (type == "IEND") end = true;
            pos += 12 + length;
        }
        Assert.True(end);
        Assert.Equal((4, 3), (width, height));

        idat.Position = 0;
        using var z = new ZLibStream(idat, CompressionMode.Decompress);
        var raw = new MemoryStream();
        z.CopyTo(raw);
        var bytes = raw.ToArray();
        Assert.Equal((4 * 4 + 1) * 3, bytes.Length);
        for (int y = 0; y < 3; y++)
        {
            Assert.Equal(0, bytes[y * 17]);
            Assert.Equal(rgba[(y * 16)..(y * 16 + 16)], bytes[(y * 17 + 1)..(y * 17 + 17)]);
        }
    }

    [Fact]
    public void UpscaleRepeatsPixelsWithoutBlending()
    {
        var (up, w, h) = PngWriter.Upscale([1, 2, 3, 4, 5, 6, 7, 8], 2, 1, 3);
        Assert.Equal((6, 3), (w, h));
        Assert.Equal([1, 2, 3, 4], up[..4]);
        Assert.Equal([1, 2, 3, 4], up[8..12]);
        Assert.Equal([5, 6, 7, 8], up[12..16]);
    }

    [Fact]
    public void MismatchedDataIsRefused() => Assert.Throws<ArgumentException>(() => PngWriter.Encode(new byte[10], 2, 2));
}
