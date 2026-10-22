namespace Hollowfall.Core.Dungeon;

public sealed partial class FloorLayout
{
    private readonly bool[] _plate = new bool[Size * Size * DungeonSite.FloorSpacing];

    /// <summary>Whether the rock wall cell's block in this row (counted up from the floor) is a rift plate.</summary>
    public bool IsPlate(int x, int z, int row) =>
        (uint)x < Size && (uint)z < Size && (uint)row < DungeonSite.FloorSpacing && _plate[(z * Size + x) * DungeonSite.FloorSpacing + row];

    /// <summary>On the deep floors, a portal takes only on a plate, so each room and gap hallway gets plates to cross by.</summary>
    private void PlacePlates()
    {
        if (Floor < DungeonSite.RiftPlateFloor) return;
        foreach (var room in _rooms) PlateRoom(room);
        foreach (var gap in _gaps) PlateGap(gap);
    }

    /// <summary>Two plateable walls on the room's edge, as far apart as the edge allows, so a portal in one crosses the room to the other.</summary>
    private void PlateRoom(Room room)
    {
        var b = room.Bounds;
        if (b.W < 5 || b.H < 5) return;
        var edge = new List<(int X, int Z)>();
        for (int x = b.X; x < b.Right; x++)
        {
            edge.Add((x, b.Z - 1));
            edge.Add((x, b.Bottom));
        }
        for (int z = b.Z; z < b.Bottom; z++)
        {
            edge.Add((b.X - 1, z));
            edge.Add((b.Right, z));
        }
        var walls = Walls(edge);
        if (walls.Count < 2) return;
        var first = walls[0];
        var far = walls.MaxBy(c => Math.Abs(c.X - first.X) + Math.Abs(c.Z - first.Z));
        PlateWall(first.X, first.Z, 1, 3);
        PlateWall(far.X, far.Z, 1, 3);
        if (walls.Count > 2)
        {
            var high = walls[^1] == far ? walls[^2] : walls[^1];
            PlateWall(high.X, high.Z, DungeonSite.FloorSpacing - 3, DungeonSite.FloorSpacing - 2);
        }
    }

    /// <summary>The walls beside a gap hallway. A hallway is three cells wide, so its walls are two cells out either side.</summary>
    private void PlateGap(GapSpec gap)
    {
        var lat = (X: -gap.DirZ, Z: gap.DirX);
        foreach (var (x, z) in gap.HallFrom.Concat(gap.HallTo).Concat(gap.Span))
        {
            int h = HeightAt(x, z);
            foreach (int side in new[] { 2, -2 })
                PlateWall(x + lat.X * side, z + lat.Z * side, h + 1, h + 3);
        }
    }

    private List<(int X, int Z)> Walls(IEnumerable<(int X, int Z)> cells) =>
        cells.Where(c => CellAt(c.X, c.Z) == CellKind.Rock && IsWallCell(c.X, c.Z)).ToList();

    private void PlateWall(int x, int z, int rowFrom, int rowTo)
    {
        if (CellAt(x, z) != CellKind.Rock || !IsWallCell(x, z)) return;
        for (int r = rowFrom; r <= rowTo && r < DungeonSite.FloorSpacing - 2; r++)
            _plate[(z * Size + x) * DungeonSite.FloorSpacing + r] = true;
    }
}
