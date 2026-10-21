using Hollowfall.Core.Dungeon;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

internal static class TestHelpers
{
    /// <summary>FNV-1a over a chunk's blocks; stable fingerprint for golden tests.</summary>
    public static ulong Checksum(Chunk chunk)
    {
        ulong h = 1469598103934665603UL;
        foreach (var b in chunk.RawBlocks)
        {
            h ^= (byte)b;
            h *= 1099511628211UL;
        }
        return h;
    }
}

/// <summary>Where a player can get to on one floor's layout: walking (up a block at a time, down as far as you like) and, if asked, over every gap.</summary>
internal static class LayoutWalk
{
    public static bool Walkable(FloorLayout floor, int x, int z) => floor.CellAt(x, z) is CellKind.Floor or CellKind.Door;

    /// <summary>
    /// Every cell reachable from a start. With <paramref name="crossGaps"/> a gap carries you from either hallway to the other (the layout
    /// cannot say whether the portals really work: <c>MixedLinkTests</c> and <see cref="DungeonReach"/> check that with the real physics).
    /// </summary>
    public static HashSet<(int X, int Z)> Reach(FloorLayout floor, int startX, int startZ, bool crossGaps)
    {
        var seen = new HashSet<(int X, int Z)> { (startX, startZ) };
        var stack = new Stack<(int X, int Z)>([(startX, startZ)]);

        void Flood()
        {
            while (stack.Count > 0)
            {
                var (x, z) = stack.Pop();
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = (X: x + dx, Z: z + dz);
                    if (!Walkable(floor, n.X, n.Z) || floor.HeightAt(n.X, n.Z) > floor.HeightAt(x, z) + 1) continue;
                    if (seen.Add(n)) stack.Push(n);
                }
            }
        }

        Flood();
        while (crossGaps)
        {
            bool grew = false;
            foreach (var gap in floor.Gaps)
            {
                bool hereFrom = gap.HallFrom.Any(seen.Contains), hereTo = gap.HallTo.Any(seen.Contains);
                if (hereFrom != hereTo)
                {
                    var arrival = hereFrom ? gap.EdgeTo : gap.EdgeFrom;
                    if (seen.Add(arrival)) stack.Push(arrival);
                    grew = true;
                }
            }
            if (!grew) break;
            Flood();
        }
        return seen;
    }
}
