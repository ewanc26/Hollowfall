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
/// <see cref="BreakWindowSeconds"/> breaks a 3 x 3 hole. Only brick floor is ever removed, and nothing breaks where a
/// stair shaft is open.
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

        for (int dx = -HoleRadius; dx <= HoleRadius; dx++)
            for (int dz = -HoleRadius; dz <= HoleRadius; dz++)
                if (IsBrick(world.GetBlock(x + dx, y, z + dz)))
                    world.SetBlock(x + dx, y, z + dz, BlockType.Air);
        _cracked = null;
        return FloorSlam.Broken;
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
