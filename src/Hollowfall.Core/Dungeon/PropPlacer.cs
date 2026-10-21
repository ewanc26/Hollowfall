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
            GapTorches(site, floor, seed, spots);
        }
        return spots;
    }

    /// <summary>
    /// Torches for the gaps, which belong to no room (their <c>RoomId</c> is -1): one on the wall at each pit edge so the drop and the far
    /// side are lit, another further back in a long hallway, and one inside a shaft gap's tunnel so the alcove you aim at is visible.
    /// </summary>
    private static void GapTorches(DungeonSite site, FloorLayout floor, long seed, List<TorchSpot> spots)
    {
        var rng = new SeededRandom(seed, floor.Floor, 1, 700);

        void Add((int X, int Z) centre, int dirX, int dirZ, int side)
        {
            // The torch stands in the cell beside the centre line and faces back toward it, with solid wall behind.
            int latX = -dirZ * side, latZ = dirX * side;
            int x = centre.X + latX, z = centre.Z + latZ;
            if (floor.CellAt(x, z) != CellKind.Floor || floor.CellAt(x + latX, z + latZ) != CellKind.Rock) return;
            int height = floor.HeightAt(x, z);
            var (wx, wy, wz) = site.ToWorld(floor.Floor, x, z);
            spots.Add(new TorchSpot(floor.Floor, -1, x, z, -latX, -latZ, wx + 0.5 + latX * 0.5, wy + height + Height, wz + 0.5 + latZ * 0.5));
        }

        foreach (var gap in floor.Gaps)
        {
            foreach (bool first in new[] { true, false })
            {
                var hall = gap.Hall(first); // door first, pit edge last
                var (dx, dz) = gap.Toward(first);
                Add(hall[^1], dx, dz, rng.Chance(0.5) ? 1 : -1);
                int back = hall.Count - 1 - MinSpacing;
                if (back >= 1) Add(hall[back], dx, dz, rng.Chance(0.5) ? 1 : -1);
            }
            if (gap.Kind == LinkKind.ShaftGap) Add(gap.Span[gap.Span.Count / 2], gap.DirX, gap.DirZ, rng.Chance(0.5) ? 1 : -1);
        }
    }
}
