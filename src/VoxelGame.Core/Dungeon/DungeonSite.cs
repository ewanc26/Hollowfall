using VoxelGame.Core.World;

namespace VoxelGame.Core.Dungeon;

/// <summary>
/// A dungeon placed in the world: a stack of <see cref="FloorLayout"/>s under a 96x96
/// footprint, reached by a spiral stair from a small plaza on the surface.
/// <see cref="BlockAt"/> is a pure function of world position, so any chunk can be carved
/// on its own and neighbouring chunks always agree.
/// </summary>
public sealed class DungeonSite
{
    public const int FloorSpacing = 8;
    public const int FirstFloorY = 38;
    public const int PlazaRadius = 3;

    private static readonly (int Dx, int Dz)[] Ring =
        [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)];

    private readonly record struct Stairwell(int LocalX, int LocalZ, int YBottom, int YTop);

    private readonly List<Stairwell> _stairwells = [];
    private readonly List<FloorLayout> _floors = [];

    public int RegionX { get; }
    public int RegionZ { get; }

    /// <summary>World position of the footprint's minimum corner.</summary>
    public int OriginX { get; }
    public int OriginZ { get; }

    /// <summary>World position of the surface stairwell's centre.</summary>
    public int EntranceX { get; }
    public int EntranceZ { get; }

    /// <summary>Y of the plaza paving: the terrain surface at the entrance.</summary>
    public int SurfaceY { get; }

    public IReadOnlyList<FloorLayout> Floors => _floors;
    public int FloorCount => _floors.Count;

    public IReadOnlyList<Spawn> Spawns { get; }

    public DungeonSite(long seed, int regionX, int regionZ, int originX, int originZ,
        int entranceLocalX, int entranceLocalZ, int surfaceY, int floorCount)
    {
        RegionX = regionX;
        RegionZ = regionZ;
        OriginX = originX;
        OriginZ = originZ;
        EntranceX = originX + entranceLocalX;
        EntranceZ = originZ + entranceLocalZ;
        SurfaceY = surfaceY;

        int ax = entranceLocalX, az = entranceLocalZ;
        _stairwells.Add(new Stairwell(ax, az, FloorY(0), surfaceY));
        for (int k = 0; k < floorCount; k++)
        {
            bool last = k == floorCount - 1;
            var floor = FloorLayout.Generate(seed, k, ax, az, last);
            _floors.Add(floor);
            if (floor.ExitStairs is { } exit)
            {
                _stairwells.Add(new Stairwell(exit.X, exit.Z, FloorY(k + 1), FloorY(k)));
                (ax, az) = exit;
            }
        }

        Spawns = SpawnPlanner.Plan(this, seed);
    }

    public static int FloorY(int floor) => FirstFloorY - FloorSpacing * floor;

    public int LowestY => FloorY(FloorCount - 1);

    public bool Contains(int wx, int wz) =>
        wx >= OriginX && wx < OriginX + FloorLayout.Size && wz >= OriginZ && wz < OriginZ + FloorLayout.Size;

    public (int X, int Y, int Z) ToWorld(int floor, int localX, int localZ) =>
        (OriginX + localX, FloorY(floor) + 1, OriginZ + localZ);

    /// <summary>The block the dungeon puts at a world position, or null to leave the terrain as it is.</summary>
    public BlockType? BlockAt(int wx, int y, int wz)
    {
        if (!Contains(wx, wz)) return null;
        int lx = wx - OriginX, lz = wz - OriginZ;

        foreach (var s in _stairwells)
        {
            int dx = lx - s.LocalX, dz = lz - s.LocalZ;
            if (Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1 && y > s.YBottom && y <= s.YTop)
                return Shaft(dx, dz, y, s.YBottom, s.YTop);
        }

        int topOfFloors = FloorY(0) + FloorSpacing - 1;
        if (y > topOfFloors)
            return PlazaAt(lx - (EntranceX - OriginX), lz - (EntranceZ - OriginZ), y);

        if (y < LowestY) return null;
        int k = (topOfFloors - y) / FloorSpacing;
        int yo = y - FloorY(k);
        return FloorBlock(_floors[k], lx, lz, yo);
    }

    private static BlockType? FloorBlock(FloorLayout floor, int lx, int lz, int yo)
    {
        if (yo == FloorSpacing - 1) return null; // rock between this ceiling and the floor above

        switch (floor.CellAt(lx, lz))
        {
            case CellKind.Rock:
                return floor.IsWallCell(lx, lz) ? BlockType.StoneBrick : null;
            case CellKind.Floor:
                return yo == 0 ? BlockType.BrickFloor : yo == 6 ? BlockType.StoneBrick : BlockType.Air;
            case CellKind.Pillar:
                return yo == 0 ? BlockType.BrickFloor : yo == 6 ? BlockType.StoneBrick : BlockType.Pillar;
            case CellKind.Door:
                return yo == 0 ? BlockType.BrickFloor : yo <= 2 ? BlockType.Door : BlockType.StoneBrick;
            default: // SecretDoor
                return yo == 0 ? BlockType.BrickFloor : yo <= 2 ? BlockType.SecretDoor : BlockType.StoneBrick;
        }
    }

    /// <summary>
    /// A spiral stair in a 3x3 shaft. Step n sits at <c>yBottom + 1 + n</c> on ring cell n mod 8,
    /// so one turn climbs exactly one floor. The top layer is floor except the ring cell after
    /// the last step and the centre column stays, and the other seven ring cells are open.
    /// </summary>
    private static BlockType Shaft(int dx, int dz, int y, int yBottom, int yTop)
    {
        if (dx == 0 && dz == 0) return y == yTop ? BlockType.BrickFloor : BlockType.Pillar;

        int c = Array.IndexOf(Ring, (dx, dz));
        int lastStep = yTop - yBottom - 2;
        if (y == yTop) return c == (lastStep + 1) % 8 ? BlockType.BrickFloor : BlockType.Air;

        int n = y - yBottom - 1;
        return n % 8 == c ? BlockType.Stairs : BlockType.Air;
    }

    private BlockType? PlazaAt(int dx, int dz, int y)
    {
        if (Math.Abs(dx) > PlazaRadius || Math.Abs(dz) > PlazaRadius) return null;
        if (y < SurfaceY) return BlockType.Stone;
        if (y == SurfaceY) return BlockType.BrickFloor;
        if (y > SurfaceY + 7) return null;
        bool corner = Math.Abs(dx) == PlazaRadius && Math.Abs(dz) == PlazaRadius;
        return corner && y <= SurfaceY + 3 ? BlockType.Pillar : BlockType.Air;
    }

    /// <summary>Stamps the dungeon onto a freshly generated terrain chunk.</summary>
    public void Apply(Chunk chunk)
    {
        for (int lx = 0; lx < Chunk.Size; lx++)
            for (int lz = 0; lz < Chunk.Size; lz++)
            {
                int wx = chunk.WorldMinX + lx, wz = chunk.WorldMinZ + lz;
                if (!Contains(wx, wz)) continue;
                for (int y = 0; y < Chunk.Height; y++)
                    if (BlockAt(wx, y, wz) is { } b) chunk[lx, y, lz] = b;
            }
    }
}
