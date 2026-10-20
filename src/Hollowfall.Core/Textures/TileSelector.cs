using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Textures;

/// <summary>
/// Picks the picture a block face wears from its place in the world: the dungeon's theme for its depth, a sparse hash-chosen
/// decal (cracks, cobwebs, moss, scorch marks, bones) and a few natural variants on the surface (mossy stone, packed snow,
/// dry cracked earth). A pure function of the position and the blocks around it, so chunks agree and meshes are repeatable.
/// A secret door is chosen as if it were a brick wall, so it can never be told from the wall around it.
/// </summary>
public static class TileSelector
{
    // Face normals in the mesher's order: +X, -X, +Y, -Y, +Z, -Z.
    private static readonly (int X, int Y, int Z)[] Normals = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];

    private static readonly (int X, int Y, int Z)[] Touching = [(0, 1, 0), (1, 0, 0), (-1, 0, 0), (0, 0, 1), (0, 0, -1)];

    private const int Top = 2, Bottom = 3;

    /// <summary>Two independent rolls in 0..1 for a block face, fixed by its place.</summary>
    public static (double A, double B) Rolls(int wx, int y, int wz, int face)
    {
        int key = y * 8 + face;
        return (SeedHash.ToUnit(SeedHash.Hash(0x71E5, wx, wz, key)), SeedHash.ToUnit(SeedHash.Hash(0x9A3C, wx, wz, key)));
    }

    public static Tile Pick(BlockType b, int face, int wx, int y, int wz, Func<int, int, int, BlockType> blockAt)
    {
        switch (b)
        {
            case BlockType.StoneBrick or BlockType.SecretDoor:
                return Wall(DungeonThemes.ForY(y), face, wx, y, wz);
            case BlockType.BrickFloor:
                return Floor(DungeonThemes.ForY(y), face, wx, y, wz);
            case BlockType.Pillar:
                return DungeonThemes.ForY(y) switch { DungeonTheme.Mossy => Tile.MossPillar, DungeonTheme.Ashen => Tile.AshPillar, _ => Tile.Pillar };
            case BlockType.Stairs:
                return DungeonThemes.ForY(y) switch { DungeonTheme.Mossy => Tile.MossStairs, DungeonTheme.Ashen => Tile.AshStairs, _ => Tile.Stairs };

            case BlockType.Stone:
                // Stone that touches growing things or water turns mossy where it shows.
                if (face != Bottom && Rolls(wx, y, wz, face).A < 0.6 && IsDamp(blockAt, wx, y, wz)) return Tile.MossyStone;
                break;
            case BlockType.Snow:
                // The sides of deep drifts are packed hard.
                if (face is not (Top or Bottom) && blockAt(wx, y + 1, wz) == BlockType.Snow) return Tile.PackedSnow;
                break;
            case BlockType.Dirt:
                {
                    // Earth open to the air dries out and cracks; earth under water stays dark.
                    if (face == Bottom || Rolls(wx, y, wz, face).A >= 0.45) break;
                    var (nx, ny, nz) = Normals[face];
                    if (blockAt(wx + nx, y + ny, wz + nz) == BlockType.Air) return Tile.DryDirt;
                    break;
                }
        }
        return BlockTextures.TileFor(b, face);
    }

    private static bool IsDamp(Func<int, int, int, BlockType> blockAt, int wx, int y, int wz)
    {
        foreach (var (dx, dy, dz) in Touching)
            if (blockAt(wx + dx, y + dy, wz + dz) is BlockType.Grass or BlockType.Dirt or BlockType.Leaves or BlockType.Log or BlockType.Water)
                return true;
        return false;
    }

    private static Tile Wall(DungeonTheme theme, int face, int wx, int y, int wz)
    {
        bool side = face is not (Top or Bottom);
        var (a, c) = Rolls(wx, y, wz, face);
        int layer = DungeonThemes.Layer(y);
        switch (theme)
        {
            case DungeonTheme.Mossy:
                if (!side) return Tile.MossBrick;
                if (layer >= 4 && c < 0.15) return Tile.MossBrickVine;
                return a < (layer <= 2 ? 0.28 : 0.14) ? Tile.MossBrickHeavy : Tile.MossBrick;
            case DungeonTheme.Ashen:
                if (!side) return Tile.AshBrick;
                if (a < 0.10) return Tile.AshBrickCracked;
                return c < (layer <= 2 ? 0.20 : 0.08) ? Tile.AshBrickScorch : Tile.AshBrick;
            default:
                if (!side) return Tile.StoneBrick;
                if (layer == 5 && c < 0.25) return Tile.BrickWeb; // cobwebs gather in the corner under the ceiling
                return a < 0.09 ? Tile.BrickCracked : Tile.StoneBrick;
        }
    }

    private static Tile Floor(DungeonTheme theme, int face, int wx, int y, int wz)
    {
        var (a, _) = Rolls(wx, y, wz, face);
        bool top = face == Top;
        switch (theme)
        {
            case DungeonTheme.Mossy:
                return top && a < 0.13 ? Tile.MossFloorPuddle : Tile.MossFloor;
            case DungeonTheme.Ashen:
                if (top && a < 0.06) return Tile.AshFloorBones;
                return top && a < 0.20 ? Tile.AshFloorEmbers : Tile.AshFloor;
            default:
                if (top && a < 0.06) return Tile.FloorCracked;
                return top && a < 0.085 ? Tile.FloorBones : Tile.BrickFloor;
        }
    }
}
