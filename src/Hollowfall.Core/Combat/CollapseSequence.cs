using Hollowfall.Core.Dungeon;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Combat;

/// <summary>
/// The floor gives way under a cleared room (docs/features.md 3.3). Floor columns are removed in rings out from the
/// room's centre, one ring every <see cref="RingSeconds"/>. Each column is dug down through the rock and the ceiling of
/// the floor below, so whoever stands on it drops a floor. Blocks are removed at most <see cref="MaxBlocksPerTick"/>
/// per tick so a large ring never edits the world in one frame. Columns beside an open stair shaft are left standing.
/// </summary>
public sealed class CollapseSequence
{
    public const int Rings = 6;
    public const double RingSeconds = 0.4;
    public const double RingWidth = 2;
    public const int MaxBlocksPerTick = 48;

    private readonly List<(int X, int Y, int Z)>[] _rings;
    private readonly Queue<(int X, int Y, int Z)> _due = new();
    private int _fired;
    private double _elapsed;

    private CollapseSequence(List<(int X, int Y, int Z)>[] rings) => _rings = rings;

    /// <summary>Plans the collapse of <paramref name="floor"/>'s floor around a room centre given in world cells.</summary>
    public static CollapseSequence Plan(VoxelWorld world, int floor, int centreX, int centreZ)
    {
        int y = DungeonSite.FloorY(floor);
        var rings = Enumerable.Range(0, Rings).Select(_ => new List<(int X, int Y, int Z)>()).ToArray();
        int reach = (int)Math.Ceiling(Rings * RingWidth);
        for (int x = centreX - reach; x <= centreX + reach; x++)
            for (int z = centreZ - reach; z <= centreZ + reach; z++)
            {
                double distance = Math.Sqrt((x - centreX) * (x - centreX) + (z - centreZ) * (z - centreZ));
                int ring = (int)(distance / RingWidth);
                if (ring >= Rings) continue;
                if (world.GetBlock(x, y, z) != BlockType.BrickFloor || SlamBreach.ShaftOpen(world, x, y, z)) continue;
                rings[ring].Add((x, y, z));
                rings[ring].Add((x, y - 1, z));
                rings[ring].Add((x, y - 2, z));
            }
        return new CollapseSequence(rings);
    }

    /// <summary>Cells planned in each ring, innermost first. A cell is three blocks: the floor and the two layers beneath it.</summary>
    public IReadOnlyList<int> RingSizes => _rings.Select(r => r.Count).ToArray();

    public int RingsFired => _fired;

    /// <summary>Every cell planned, in ring order.</summary>
    public IEnumerable<(int X, int Y, int Z)> Cells => _rings.SelectMany(r => r);

    /// <summary>Advances the collapse by <paramref name="dt"/> seconds. Returns true once every ring has fallen away.</summary>
    public bool Tick(double dt, VoxelWorld world, Action<int, int, int>? edited = null)
    {
        _elapsed += dt;
        while (_fired < Rings && _elapsed >= (_fired + 1) * RingSeconds)
        {
            foreach (var cell in _rings[_fired]) _due.Enqueue(cell);
            _fired++;
        }

        for (int i = 0; i < MaxBlocksPerTick && _due.Count > 0; i++)
        {
            var (x, y, z) = _due.Dequeue();
            world.SetBlock(x, y, z, BlockType.Air);
            edited?.Invoke(x, y, z);
        }
        return _fired == Rings && _due.Count == 0;
    }
}
