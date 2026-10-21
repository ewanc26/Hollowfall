using Hollowfall.Core.World;

namespace Hollowfall.Core.Combat;

public enum FloorSlam
{
    None,
    Cracked,
    Broken,
}

/// <summary>
/// What a slam does to the brick it lands on. The first slam cracks the tile; a second slam on the same tile within
/// <see cref="BreakWindowSeconds"/> breaks a round hole about three blocks across, cut in 10 cm cubes. Only brick floor
/// is ever removed, and nothing breaks where a stair shaft is open.
/// </summary>
public sealed class SlamBreach
{
    public const double BreakWindowSeconds = 3;
    public const int HoleRadius = 1;

    private (int X, int Y, int Z)? _cracked;
    private double _crackedAt;

    public FloorSlam Land(VoxelWorld world, int x, int y, int z, double time)
    {
        if (!IsBrick(world.GetBlock(x, y, z)) || ShaftOpen(world, x, y, z)) return FloorSlam.None;

        bool recentlyCracked = _cracked == (x, y, z) && time - _crackedAt < BreakWindowSeconds;
        if (!recentlyCracked)
        {
            world.SetBlock(x, y, z, BlockType.CrackedFloor);
            _cracked = (x, y, z);
            _crackedAt = time;
            return FloorSlam.Cracked;
        }

        BreakFloor(world, x, y, z);
        _cracked = null;
        return FloorSlam.Broken;
    }

    /// <summary>Digs the 10 cm cubes of the brick floor within a round hole around the impact, through the floor layer only.</summary>
    private static void BreakFloor(VoxelWorld world, int x, int y, int z)
    {
        const int n = BlockHole.Side;
        const double cube = 1.0 / n;
        double centreX = x + 0.5, centreZ = z + 0.5, reach = HoleRadius + 0.5;
        for (int dx = -HoleRadius; dx <= HoleRadius; dx++)
            for (int dz = -HoleRadius; dz <= HoleRadius; dz++)
            {
                int bx = x + dx, bz = z + dz;
                if (!IsBrick(world.GetBlock(bx, y, bz))) continue;
                for (int sz = 0; sz < n; sz++)
                    for (int sx = 0; sx < n; sx++)
                    {
                        double px = bx + (sx + 0.5) * cube - centreX, pz = bz + (sz + 0.5) * cube - centreZ;
                        if (px * px + pz * pz > reach * reach) continue;
                        for (int sy = 0; sy < n; sy++) world.DigCube(bx, y, bz, sx, sy, sz);
                    }
            }
    }

    private static bool IsBrick(BlockType b) => b is BlockType.BrickFloor or BlockType.CrackedFloor;

    private static bool ShaftOpen(VoxelWorld world, int x, int y, int z)
    {
        for (int dx = -HoleRadius; dx <= HoleRadius; dx++)
            for (int dz = -HoleRadius; dz <= HoleRadius; dz++)
                for (int dy = 0; dy <= 1; dy++)
                    if (world.GetBlock(x + dx, y + dy, z + dz) == BlockType.Stairs) return true;
        return false;
    }
}
