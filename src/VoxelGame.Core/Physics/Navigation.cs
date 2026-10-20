using VoxelGame.Core.World;

namespace VoxelGame.Core.Physics;

/// <summary>
/// A coarse walking rule for checking that places are playable and for planning routes: a two-block-tall
/// space on top of a solid block, moving in four directions and stepping up or down one block.
/// </summary>
public static class Navigation
{
    public const int MaxNodes = 1_500_000;

    public readonly record struct Cell(int X, int Y, int Z);

    public static bool CanStand(Func<int, int, int, BlockType> blockAt, int x, int y, int z) =>
        !BlockInfo.IsSolid(blockAt(x, y, z)) && !BlockInfo.IsSolid(blockAt(x, y + 1, z)) && BlockInfo.IsSolid(blockAt(x, y - 1, z));

    /// <summary>Shortest walk from <paramref name="start"/> to <paramref name="goal"/>, or null if there is none.</summary>
    public static List<Cell>? FindPath(Func<int, int, int, BlockType> blockAt, Cell start, Cell goal, Func<int, int, bool> inBounds)
    {
        if (!CanStand(blockAt, start.X, start.Y, start.Z) || !CanStand(blockAt, goal.X, goal.Y, goal.Z)) return null;

        var cameFrom = new Dictionary<Cell, Cell> { [start] = start };
        var queue = new Queue<Cell>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == goal) break;
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = cur.X + dx, nz = cur.Z + dz;
                if (!inBounds(nx, nz)) continue;
                foreach (int dy in new[] { 0, 1, -1 })
                {
                    var next = new Cell(nx, cur.Y + dy, nz);
                    if (cameFrom.ContainsKey(next) || !CanStand(blockAt, next.X, next.Y, next.Z)) continue;
                    if (cameFrom.Count >= MaxNodes) return null;
                    cameFrom[next] = cur;
                    queue.Enqueue(next);
                }
            }
        }

        if (!cameFrom.ContainsKey(goal)) return null;
        var path = new List<Cell>();
        for (var c = goal; c != start; c = cameFrom[c]) path.Add(c);
        path.Add(start);
        path.Reverse();
        return path;
    }
}
