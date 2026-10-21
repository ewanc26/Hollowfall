using Hollowfall.Core.World;

namespace Hollowfall.Core.Topology;

/// <summary>
/// A small room whose X ends are glued into a loop, with its Z walls mirrored across the seam. The Log pillar on the left
/// of the spawn appears again on the right, turned to the other side, so a walker who goes round can see the seam.
/// </summary>
public static class GluedTestLevel
{
    public const int Width = 24;
    public const int Depth = 12;

    public static (double X, double Y, double Z) Spawn => (4.5, 1, 6.5);

    public static GluedBox Create() => new(Width, Depth, Interior, mirrored: true);

    /// <summary>The room's own blocks, read in canonical cells: the floor is the y = 0 layer.</summary>
    public static BlockType Interior(int x, int y, int z)
    {
        if (y == 0) return BlockType.Stone;
        if (y >= 1 && y <= 2 && x == 2 && z == 2) return BlockType.Log;
        if (y >= 1 && y <= 3 && x is 6 or 18 && z is 3 or 8) return BlockType.Stone;
        return BlockType.Air;
    }
}
