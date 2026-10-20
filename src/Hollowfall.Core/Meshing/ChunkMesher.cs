using Hollowfall.Core.Terrain;
using Hollowfall.Core.Textures;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Meshing;

/// <summary>
/// Turns a chunk into triangles: hidden faces are dropped (including across chunk borders), each vertex
/// gets ambient occlusion and a simple directional shade baked into its colour, and quads are split along
/// the diagonal that avoids AO artefacts. Pure and thread-safe, so it can run on worker threads.
/// </summary>
public static class ChunkMesher
{
    // Face order: +X, -X, +Y, -Y, +Z, -Z. u x v = normal, so corners (-1,-1),(1,-1),(1,1),(-1,1) wind counter-clockwise.
    private static readonly (int Nx, int Ny, int Nz, int Ux, int Uy, int Uz, int Vx, int Vy, int Vz, float Shade)[] Faces =
    [
        (1, 0, 0, 0, 1, 0, 0, 0, 1, 0.70f),
        (-1, 0, 0, 0, 0, 1, 0, 1, 0, 0.70f),
        (0, 1, 0, 0, 0, 1, 1, 0, 0, 1.00f),
        (0, -1, 0, 1, 0, 0, 0, 0, 1, 0.50f),
        (0, 0, 1, 1, 0, 0, 0, 1, 0, 0.85f),
        (0, 0, -1, 0, 1, 0, 1, 0, 0, 0.85f),
    ];

    private static readonly (int Du, int Dv)[] Corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)];

    private static readonly float[] AoCurve = [0.45f, 0.65f, 0.82f, 1.0f];

    public static ChunkMesh Build(int chunkX, int chunkZ, Func<int, int, int, BlockType> blockAt)
    {
        var mesh = new ChunkMesh();
        int ox = chunkX * Chunk.Size, oz = chunkZ * Chunk.Size;

        for (int y = 0; y < Chunk.Height; y++)
            for (int lz = 0; lz < Chunk.Size; lz++)
                for (int lx = 0; lx < Chunk.Size; lx++)
                {
                    int wx = ox + lx, wz = oz + lz;
                    var b = blockAt(wx, y, wz);
                    if (b == BlockType.Air || b == BlockType.Door) continue;

                    if (BlockInfo.IsPlant(b))
                    {
                        AddPlant(mesh.Cutout, lx, y, lz, b);
                        continue;
                    }

                    var target = b == BlockType.Water ? mesh.Water : mesh.Opaque;
                    for (int f = 0; f < Faces.Length; f++)
                    {
                        var face = Faces[f];
                        var n = blockAt(wx + face.Nx, y + face.Ny, wz + face.Nz);
                        if (!FaceVisible(b, n)) continue;
                        AddFace(target, blockAt, wx, y, wz, lx, lz, f, b);
                    }
                }
        return mesh;
    }

    private static bool FaceVisible(BlockType self, BlockType neighbour)
    {
        if (BlockInfo.Occludes(neighbour)) return false;
        // Same-kind see-through blocks merge: no faces inside a lake or a canopy.
        if ((self == BlockType.Water || self == BlockType.Leaves) && neighbour == self) return false;
        return true;
    }

    private static int Occ(Func<int, int, int, BlockType> blockAt, int x, int y, int z) =>
        BlockInfo.Occludes(blockAt(x, y, z)) ? 1 : 0;

    private static void AddFace(MeshBuffer buf, Func<int, int, int, BlockType> blockAt, int wx, int y, int wz, int lx, int lz, int faceIndex, BlockType b)
    {
        var f = Faces[faceIndex];
        var colour = BlockPalette.Color(b, faceIndex, DungeonThemes.IsThemed(b) ? DungeonThemes.ForY(y) : DungeonTheme.Plain);
        bool water = b == BlockType.Water;

        // Water's top sits slightly lower so a lake looks like it fills its basin.
        float top = water && faceIndex == 2 && blockAt(wx, y + 1, wz) != BlockType.Water ? y + 0.9f : y + 1f;
        float cx = lx + 0.5f + 0.5f * f.Nx, cz = lz + 0.5f + 0.5f * f.Nz;
        float cy = (water ? (y + top) * 0.5f : y + 0.5f) + 0.5f * f.Ny * (water ? (top - y) : 1f);

        // Which picture, and how it is turned: a hash of the block's place flips tiles so repeats are less obvious.
        var rect = BlockTextures.Rect(TileSelector.Pick(b, faceIndex, wx, y, wz, blockAt));
        uint spin = (uint)SeedHash.Hash(0, wx, wz, y);
        bool flipU = !water && (spin & 1) != 0, flipV = !water && faceIndex is 2 or 3 && (spin & 2) != 0;
        bool sideFace = f.Ny == 0;

        Span<int> idx = stackalloc int[4];
        Span<int> ao = stackalloc int[4];
        for (int c = 0; c < 4; c++)
        {
            var (du, dv) = Corners[c];
            float px = cx + 0.5f * (du * f.Ux + dv * f.Vx);
            float py = cy + 0.5f * (du * f.Uy + dv * f.Vy) * (water ? (top - y) : 1f);
            float pz = cz + 0.5f * (du * f.Uz + dv * f.Vz);

            // Occlusion sampled in the layer just outside the face.
            int bx = wx + f.Nx, by = y + f.Ny, bz = wz + f.Nz;
            int s1 = Occ(blockAt, bx + du * f.Ux, by + du * f.Uy, bz + du * f.Uz);
            int s2 = Occ(blockAt, bx + dv * f.Vx, by + dv * f.Vy, bz + dv * f.Vz);
            int cn = Occ(blockAt, bx + du * f.Ux + dv * f.Vx, by + du * f.Uy + dv * f.Vy, bz + du * f.Uz + dv * f.Vz);
            ao[c] = s1 == 1 && s2 == 1 ? 0 : 3 - (s1 + s2 + cn);

            float shade = f.Shade * AoCurve[ao[c]];
            // Texture space: on walls up is up, and the other axis runs along the wall.
            float along, up;
            if (sideFace)
            {
                bool uVertical = f.Uy != 0;
                up = uVertical ? du * f.Uy : dv * f.Vy;
                along = uVertical ? dv : du;
            }
            else
            {
                along = du;
                up = dv;
            }
            float s = (along + 1) * 0.5f, t = sideFace ? 1f - (up + 1) * 0.5f : (up + 1) * 0.5f;
            if (flipU) s = 1f - s;
            if (flipV) t = 1f - t;
            idx[c] = buf.AddVertex(px, py, pz, f.Nx, f.Ny, f.Nz,
                colour.R * shade, colour.G * shade, colour.B * shade, water ? 0.72f : 1f,
                rect.U0 + (rect.U1 - rect.U0) * s, rect.V0 + (rect.V1 - rect.V0) * t);
        }

        if (ao[0] + ao[2] >= ao[1] + ao[3])
        {
            buf.AddTriangle(idx[0], idx[1], idx[2]);
            buf.AddTriangle(idx[0], idx[2], idx[3]);
        }
        else
        {
            buf.AddTriangle(idx[1], idx[2], idx[3]);
            buf.AddTriangle(idx[1], idx[3], idx[0]);
        }
    }

    /// <summary>Two crossed vertical quads, each emitted twice (front and back) so any material culls correctly.</summary>
    private static void AddPlant(MeshBuffer buf, int lx, int y, int lz, BlockType b)
    {
        var colour = BlockPalette.Color(b, 0);
        var rect = BlockTextures.Rect(BlockTextures.TileFor(b, 0));
        float h = b == BlockType.Flower ? 0.6f : 0.85f;
        const float inset = 0.1f;
        (float X0, float Z0, float X1, float Z1)[] diagonals =
        [
            (lx + inset, lz + inset, lx + 1 - inset, lz + 1 - inset),
            (lx + 1 - inset, lz + inset, lx + inset, lz + 1 - inset),
        ];
        foreach (var (x0, z0, x1, z1) in diagonals)
            for (int side = 0; side < 2; side++)
            {
                // Normal is perpendicular to the diagonal; the second pass flips it and the winding.
                float dx = x1 - x0, dz = z1 - z0;
                float len = MathF.Sqrt(dx * dx + dz * dz);
                float nx = -dz / len * (side == 0 ? 1 : -1), nz = dx / len * (side == 0 ? 1 : -1);
                int a = buf.AddVertex(x0, y, z0, nx, 0, nz, colour.R * 0.9f, colour.G * 0.9f, colour.B * 0.9f, 1f, rect.U0, rect.V1);
                int bb = buf.AddVertex(x1, y, z1, nx, 0, nz, colour.R * 0.9f, colour.G * 0.9f, colour.B * 0.9f, 1f, rect.U1, rect.V1);
                int c = buf.AddVertex(x1, y + h, z1, nx, 0, nz, colour.R, colour.G, colour.B, 1f, rect.U1, rect.V0);
                int d = buf.AddVertex(x0, y + h, z0, nx, 0, nz, colour.R, colour.G, colour.B, 1f, rect.U0, rect.V0);
                if (side == 0)
                {
                    buf.AddTriangle(a, bb, c);
                    buf.AddTriangle(a, c, d);
                }
                else
                {
                    buf.AddTriangle(bb, a, d);
                    buf.AddTriangle(bb, d, c);
                }
            }
    }
}
