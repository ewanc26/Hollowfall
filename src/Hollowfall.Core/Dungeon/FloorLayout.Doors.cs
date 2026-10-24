namespace Hollowfall.Core.Dungeon;

/// <summary>
/// One opening of a rift door: a wall cell whose two blocks above the floor (rows 1 and 2) are a fixed portal face. Its open side
/// is the cell at <c>(X + NormalX, Z + NormalZ)</c>.
/// </summary>
public readonly record struct RiftDoor(int X, int Z, int NormalX, int NormalZ);

public sealed partial class FloorLayout
{
    private const double RiftDoorChance = 0.5;
    private const int MinRiftSpan = 30;

    private readonly List<(RiftDoor A, RiftDoor B)> _riftDoors = [];

    /// <summary>Fixed portal pairs that join two rooms which are not linked to each other. They are optional shortcuts.</summary>
    public IReadOnlyList<(RiftDoor A, RiftDoor B)> RiftDoors => _riftDoors;

    private void PlaceRiftDoors(SeededRandom rng)
    {
        if (Floor < DungeonSite.RiftPlateFloor || !rng.Chance(RiftDoorChance)) return;

        var doors = new List<RiftDoor>[_rooms.Count];
        for (int i = 0; i < _rooms.Count; i++) doors[i] = RiftDoorCandidates(_rooms[i]);

        var order = Enumerable.Range(0, _rooms.Count).ToList();
        rng.Shuffle(order);
        foreach (int a in order)
        {
            if (doors[a].Count == 0) continue;
            var from = doors[a][rng.Next(doors[a].Count)];
            foreach (int b in order)
            {
                if (a == b || doors[b].Count == 0 || LinkedDirectly(a, b)) continue;
                var far = doors[b].Where(d => RiftSpan(from, d) >= MinRiftSpan).ToList();
                if (far.Count == 0) continue;
                _riftDoors.Add((from, far[rng.Next(far.Count)]));
                return;
            }
        }
    }

    private List<RiftDoor> RiftDoorCandidates(Room room)
    {
        var found = new List<RiftDoor>();
        var b = room.Bounds;
        for (int z = b.Z; z < b.Bottom; z++)
            for (int x = b.X; x < b.Right; x++)
            {
                if (!room.Contains(x, z) || NearAnchor(x, z) || _reserved[z * Size + x]) continue;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int wx = x + dx, wz = z + dz;
                    if ((uint)wx >= Size || (uint)wz >= Size || _reserved[wz * Size + wx]) continue;
                    if (CellAt(wx, wz) != CellKind.Rock || !IsWallCell(wx, wz)) continue;
                    var door = new RiftDoor(wx, wz, -dx, -dz);
                    if (!found.Contains(door)) found.Add(door);
                }
            }
        return found;
    }

    private bool LinkedDirectly(int a, int b) => Links.Any(l => (l.A == a && l.B == b) || (l.A == b && l.B == a));

    private static int RiftSpan(RiftDoor a, RiftDoor b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);
}
