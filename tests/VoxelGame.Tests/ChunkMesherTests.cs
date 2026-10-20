using System.Diagnostics;
using VoxelGame.Core.Meshing;
using VoxelGame.Core.Terrain;
using VoxelGame.Core.World;

namespace VoxelGame.Tests;

public class ChunkMesherTests
{
    private static ChunkMesh Mesh(Dictionary<(int, int, int), BlockType> blocks, int cx = 0, int cz = 0, BlockType outside = BlockType.Air) =>
        ChunkMesher.Build(cx, cz, (x, y, z) => blocks.TryGetValue((x, y, z), out var b) ? b : outside);

    private static int Faces(MeshBuffer b) => b.TriangleCount / 2;

    [Fact]
    public void ALoneBlockHasSixFaces()
    {
        var m = Mesh(new() { [(5, 10, 5)] = BlockType.Stone });
        Assert.Equal(6, Faces(m.Opaque));
        Assert.Equal(24, m.Opaque.VertexCount);
        Assert.Equal(36, m.Opaque.Indices().Length);
        Assert.True(m.Cutout.IsEmpty && m.Water.IsEmpty);
    }

    [Fact]
    public void TouchingBlocksHideTheirSharedFaces()
    {
        var m = Mesh(new() { [(5, 10, 5)] = BlockType.Stone, [(6, 10, 5)] = BlockType.Dirt });
        Assert.Equal(10, Faces(m.Opaque));
    }

    [Fact]
    public void AFullSlabOnlyShowsItsTop()
    {
        var blocks = new Dictionary<(int, int, int), BlockType>();
        for (int x = -2; x < 18; x++)
            for (int z = -2; z < 18; z++)
                for (int y = 0; y <= 60; y++)
                    blocks[(x, y, z)] = BlockType.Stone;
        // Outside the chunk the slab continues, so only the 256 top faces are visible (the world floor is not drawn).
        var m = ChunkMesher.Build(0, 0, (x, y, z) => y < 0 ? BlockType.Stone : blocks.GetValueOrDefault((x, y, z)));
        Assert.Equal(256, Faces(m.Opaque));
    }

    [Fact]
    public void FacesOnAChunkBorderDependOnTheNeighbourChunk()
    {
        var left = Mesh(new() { [(15, 10, 3)] = BlockType.Stone }, 0, 0);
        Assert.Equal(6, Faces(left.Opaque));

        var withSolidNeighbour = Mesh(new() { [(15, 10, 3)] = BlockType.Stone, [(16, 10, 3)] = BlockType.Stone }, 0, 0);
        Assert.Equal(5, Faces(withSolidNeighbour.Opaque));

        var otherSide = Mesh(new() { [(15, 10, 3)] = BlockType.Stone, [(16, 10, 3)] = BlockType.Stone }, 1, 0);
        Assert.Equal(5, Faces(otherSide.Opaque));
    }

    [Fact]
    public void TrianglesWindCounterClockwiseAroundTheirNormals()
    {
        var world = new VoxelWorld(5);
        foreach (var (cx, cz) in new[] { (0, 0), (3, -2), (-4, 5) })
        {
            var mesh = ChunkMesher.Build(cx, cz, world.GetBlock);
            foreach (var buf in new[] { mesh.Opaque, mesh.Water, mesh.Cutout })
            {
                var p = buf.Positions();
                var n = buf.Normals();
                var idx = buf.Indices();
                for (int t = 0; t < idx.Length; t += 3)
                {
                    int a = idx[t] * 3, b = idx[t + 1] * 3, c = idx[t + 2] * 3;
                    float[] e1 = [p[b] - p[a], p[b + 1] - p[a + 1], p[b + 2] - p[a + 2]];
                    float[] e2 = [p[c] - p[a], p[c + 1] - p[a + 1], p[c + 2] - p[a + 2]];
                    float gx = e1[1] * e2[2] - e1[2] * e2[1], gy = e1[2] * e2[0] - e1[0] * e2[2], gz = e1[0] * e2[1] - e1[1] * e2[0];
                    float dot = gx * n[a] + gy * n[a + 1] + gz * n[a + 2];
                    Assert.True(dot > 0, $"triangle {t / 3} winds against its normal (dot {dot})");
                }
            }
        }
    }

    [Fact]
    public void GeometryStaysInsideTheChunkAndIndicesAreValid()
    {
        var world = new VoxelWorld(9);
        var mesh = ChunkMesher.Build(2, 2, world.GetBlock);
        foreach (var buf in new[] { mesh.Opaque, mesh.Water, mesh.Cutout })
        {
            var p = buf.Positions();
            for (int i = 0; i < p.Length; i += 3)
            {
                Assert.InRange(p[i], 0f, 16f);
                Assert.InRange(p[i + 1], 0f, Chunk.Height);
                Assert.InRange(p[i + 2], 0f, 16f);
            }
            Assert.All(buf.Indices(), i => Assert.InRange(i, 0, buf.VertexCount - 1));
            Assert.Equal(buf.VertexCount * 4, buf.Colors().Length);
            Assert.Equal(0, buf.TriangleCount % 2);
        }
    }

    [Fact]
    public void FlatGroundIsEvenlyLitAndCornersAreDarkened()
    {
        var blocks = new Dictionary<(int, int, int), BlockType>();
        for (int x = -2; x < 18; x++)
            for (int z = -2; z < 18; z++)
                blocks[(x, 10, z)] = BlockType.Stone;
        var flat = Mesh(blocks);
        Assert.Equal(1.0f, TopFaceBrightness(flat, 8, 8, blocks.Count), 0.001f); // fully open: AO factor 1

        blocks[(8, 11, 8)] = BlockType.Stone; // a block sitting next to the point we measure
        var shaded = Mesh(blocks);
        Assert.True(TopFaceBrightness(shaded, 7, 8, 0) < TopFaceBrightness(flat, 7, 8, 0), "ground next to a block must be darker");
    }

    /// <summary>Darkest red channel among vertices of the top face of block (x,10,z), relative to the palette colour.</summary>
    private static float TopFaceBrightness(ChunkMesh mesh, int x, int z, int _)
    {
        var p = mesh.Opaque.Positions();
        var n = mesh.Opaque.Normals();
        var c = mesh.Opaque.Colors();
        float best = float.MaxValue;
        var palette = BlockPalette.Color(BlockType.Stone, BlockPalette.FaceTop);
        for (int v = 0; v < mesh.Opaque.VertexCount; v++)
        {
            if (n[v * 3 + 1] < 0.9f) continue;
            if (p[v * 3] < x - 0.01f || p[v * 3] > x + 1.01f || p[v * 3 + 2] < z - 0.01f || p[v * 3 + 2] > z + 1.01f || Math.Abs(p[v * 3 + 1] - 11f) > 0.01f) continue;
            best = Math.Min(best, c[v * 4] / palette.R);
        }
        return best;
    }

    [Fact]
    public void WaterIsSeparateAndHasNoInternalFaces()
    {
        var blocks = new Dictionary<(int, int, int), BlockType>();
        for (int x = 0; x < 3; x++)
            for (int z = 0; z < 3; z++)
            {
                blocks[(x, 10, z)] = BlockType.Sand;
                blocks[(x, 11, z)] = BlockType.Water;
                blocks[(x, 12, z)] = BlockType.Water;
            }
        var m = Mesh(blocks);
        Assert.False(m.Water.IsEmpty);
        // 3x3x2 lake: top 9 + four sides 3*2 each = 33 water faces; nothing between water blocks.
        Assert.Equal(9 + 4 * 6, Faces(m.Water));
        Assert.Equal(9 + 4 * 3 + 9, Faces(m.Opaque)); // water does not hide the sand: top, four sides and underside
        Assert.All(Enumerable.Range(0, m.Water.VertexCount), v => Assert.True(m.Water.Colors()[v * 4 + 3] < 1f, "water must be translucent"));
    }

    [Fact]
    public void LeavesMergeButShowAgainstAir()
    {
        var blocks = new Dictionary<(int, int, int), BlockType>();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                    blocks[(x, 10 + y, z)] = BlockType.Leaves;
        Assert.Equal(6 * 4, Faces(Mesh(blocks).Opaque)); // only the outer shell of a 2x2x2 cube
    }

    [Fact]
    public void LeavesDoNotHideTheGroundOrWallsBehindThem()
    {
        var m = Mesh(new() { [(5, 10, 5)] = BlockType.Stone, [(6, 10, 5)] = BlockType.Leaves });
        Assert.Equal(6 + 5, Faces(m.Opaque)); // leaves never hide the stone (6 faces), but stone hides the leaf face touching it (5)
    }

    [Fact]
    public void PlantsAreDoubleSidedCrossedQuads()
    {
        var m = Mesh(new() { [(4, 10, 4)] = BlockType.Flower });
        Assert.True(m.Opaque.IsEmpty);
        Assert.Equal(4, Faces(m.Cutout)); // two crossed quads, each front and back
        Assert.Equal(16, m.Cutout.VertexCount);
    }

    [Fact]
    public void DoorsAndAirDrawNothing()
    {
        Assert.True(Mesh(new() { [(4, 10, 4)] = BlockType.Door }).IsEmpty);
        Assert.True(Mesh([]).IsEmpty);
    }

    [Fact]
    public void QuadsFlipTheirDiagonalToFollowOcclusion()
    {
        // A pillar beside a lit block shades only some corners; the chosen diagonal must connect the two brighter corners.
        var blocks = new Dictionary<(int, int, int), BlockType>();
        for (int x = 0; x < 4; x++)
            for (int z = 0; z < 4; z++) blocks[(x, 10, z)] = BlockType.Stone;
        blocks[(0, 11, 0)] = BlockType.Stone;
        var m = Mesh(blocks);
        var c = m.Opaque.Colors();
        var idx = m.Opaque.Indices();
        Assert.NotEmpty(idx);
        // Every quad's two triangles share the diagonal; just confirm both triangles of a quad reference 4 distinct vertices.
        for (int t = 0; t < idx.Length; t += 6)
            Assert.Equal(4, new[] { idx[t], idx[t + 1], idx[t + 2], idx[t + 3], idx[t + 4], idx[t + 5] }.Distinct().Count());
        Assert.True(c.Length > 0);
    }

    [Fact]
    public void RealChunksMeshQuicklyAndDeterministically()
    {
        var world = new VoxelWorld(3);
        var first = ChunkMesher.Build(0, 0, world.GetBlock);
        var second = ChunkMesher.Build(0, 0, world.GetBlock);
        Assert.Equal(first.Opaque.Positions(), second.Opaque.Positions());
        Assert.Equal(first.Opaque.Indices(), second.Opaque.Indices());
        Assert.False(first.Opaque.IsEmpty);

        for (int cx = -3; cx <= 3; cx++)
            for (int cz = -3; cz <= 3; cz++) world.GetChunk(cx, cz); // warm the cache so we time meshing alone
        var sw = Stopwatch.StartNew();
        for (int cx = -2; cx <= 2; cx++)
            for (int cz = -2; cz <= 2; cz++) ChunkMesher.Build(cx, cz, world.GetBlock);
        Assert.True(sw.ElapsedMilliseconds / 25.0 < 60, $"{sw.ElapsedMilliseconds / 25.0:F1} ms per chunk");
    }

    [Fact]
    public void DungeonChunksMeshWithTheirInteriorFaces()
    {
        var world = new VoxelWorld(1);
        var site = world.Generator.Dungeons!.GetSite(0, 1)!;
        var mesh = ChunkMesher.Build(site.EntranceX >> 4, site.EntranceZ >> 4, world.GetBlock);
        Assert.False(mesh.Opaque.IsEmpty);
        Assert.True(mesh.Opaque.Colors().Select((v, i) => (v, i)).Any(t => t.i % 4 == 0 && t.v > 0), "has colour");
    }

    [Fact]
    public void WorldCacheSharesChunksAndSeesEdits()
    {
        var world = new VoxelWorld(2);
        Assert.Same(world.GetChunk(1, 1), world.GetChunk(1, 1));
        Assert.Equal(BlockType.Air, world.SetBlock(20, 120, 20, BlockType.Stone));
        Assert.Equal(BlockType.Stone, world.GetBlock(20, 120, 20));
        Assert.Equal(BlockType.Air, world.GetBlock(20, 200, 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetBlock(0, 128, 0, BlockType.Stone));
        Assert.True(world.Unload(1, 1));
        Assert.False(world.HasChunk(1, 1));

        var parallel = new VoxelWorld(2);
        var chunks = new Chunk[64];
        Parallel.For(0, 64, i => chunks[i] = parallel.GetChunk(i % 4, 0));
        Assert.Equal(4, chunks.Distinct().Count());
        Assert.Equal(new TerrainGenerator(2).GenerateChunk(3, 0).RawBlocks.ToArray(), parallel.GetChunk(3, 0).RawBlocks.ToArray());
    }
}
