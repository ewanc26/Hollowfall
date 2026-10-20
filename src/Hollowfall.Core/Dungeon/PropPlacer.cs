namespace Hollowfall.Core.Dungeon;

/// <summary>A wall torch: the floor cell it lights, the way it faces into the room, and its world position.</summary>
public readonly record struct TorchSpot(int Floor, int RoomId, int CellX, int CellZ, int FaceX, int FaceZ, double X, double Y, double Z);

/// <summary>
/// Decides where wall torches go. Like terrain and spawns it is a pure function of the seed and the layout, so every
/// run of a seed lights the same dungeon. Torches stand on room walls, spaced apart, a few per room scaled to its size.
/// </summary>
public static class PropPlacer
{
    /// <summary>Least distance in cells between two torches in a room.</summary>
    public const int MinSpacing = 5;

    /// <summary>How high above the floor a torch hangs, in blocks.</summary>
    public const double Height = 1.7;

    private static readonly (int X, int Z)[] Directions = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static IReadOnlyList<TorchSpot> Torches(DungeonSite site, long seed)
    {
        var spots = new List<TorchSpot>();
        foreach (var floor in site.Floors)
        {
            var rng = new SeededRandom(seed, floor.Floor, 0, 700);
            foreach (var room in floor.Rooms)
            {
                var candidates = new List<(int X, int Z, int Dx, int Dz)>();
                for (int z = room.Bounds.Z; z < room.Bounds.Bottom; z++)
                    for (int x = room.Bounds.X; x < room.Bounds.Right; x++)
                    {
                        if (floor.CellAt(x, z) != CellKind.Floor || floor.RoomIdAt(x, z) != room.Id) continue;
                        // A torch needs solid wall behind it, so it faces away from a rock neighbour.
                        foreach (var (dx, dz) in Directions)
                            if (floor.CellAt(x - dx, z - dz) == CellKind.Rock)
                            {
                                candidates.Add((x, z, dx, dz));
                                break;
                            }
                    }
                if (candidates.Count == 0) continue;

                int wanted = Math.Clamp(room.Bounds.W * room.Bounds.H / 40, 1, 5);
                rng.Shuffle(candidates);
                var placed = new List<(int X, int Z)>();
                foreach (var c in candidates)
                {
                    if (placed.Count >= wanted) break;
                    if (placed.Any(p => Math.Abs(p.X - c.X) + Math.Abs(p.Z - c.Z) < MinSpacing)) continue;
                    placed.Add((c.X, c.Z));
                    var (wx, wy, wz) = site.ToWorld(floor.Floor, c.X, c.Z);
                    // The torch sits on the wall face: half a block from the cell centre toward the wall, facing into the room.
                    spots.Add(new TorchSpot(floor.Floor, room.Id, c.X, c.Z, c.Dx, c.Dz,
                        wx + 0.5 - c.Dx * 0.5, wy + Height, wz + 0.5 - c.Dz * 0.5));
                }
            }
        }
        return spots;
    }
}
