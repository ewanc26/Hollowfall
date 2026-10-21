using Hollowfall.Core.Story;

namespace Hollowfall.Core.Dungeon;

/// <summary>
/// A fire pit: the floor cell it stands in (the pit is centred in it) and its world position. A pit is warmth and decoration; its
/// flames harm nobody.
/// </summary>
public readonly record struct FirePitSpot(int Floor, int RoomId, int CellX, int CellZ, double X, double Y, double Z);

/// <summary>
/// Decides where fire pits go. Like torches and notes it is a pure function of the dungeon and the seed, and it only reads the layout.
/// Pits stand in Safe, Standard and Treasure rooms (never the entrance, boss or trap rooms), on a floor cell with a free ring of eight
/// floor cells around it in the same room, clear of doors, stairs, chests, keys and notes, and spaced apart, so a pit never blocks a
/// corridor or a stair. Every dungeon gets at least one on its first two floors.
/// </summary>
public static class FirePitPlacer
{
    /// <summary>Least distance in cells (Manhattan) between two pits on a floor.</summary>
    public const int MinSpacing = 9;

    /// <summary>How far, in cells (Chebyshev), a pit keeps from a door, stairs, chest, key, note or any spawn.</summary>
    public const int Clearance = 3;

    public static bool SuitableRole(RoomRole role) => role is RoomRole.Safe or RoomRole.Standard or RoomRole.Treasure;

    public static IReadOnlyList<FirePitSpot> Place(DungeonSite site, long seed)
    {
        var notes = NotePlacer.Place(site, seed);
        var spots = new List<FirePitSpot>();
        foreach (var floor in site.Floors)
        {
            var pits = new List<(Room Room, int X, int Z)>();
            var avoid = AvoidCells(site, floor, notes);
            foreach (var room in floor.Rooms)
            {
                if (!SuitableRole(room.Role)) continue;
                var rng = new SeededRandom(seed, floor.Floor, room.Id, 905);
                var candidates = Candidates(floor, room, avoid);
                if (candidates.Count == 0) continue;

                bool big = room.Bounds.W * room.Bounds.H >= 140;
                double roll = rng.NextDouble();
                int wanted = room.Role switch
                {
                    RoomRole.Safe => big && rng.Chance(0.35) ? 2 : 1,
                    RoomRole.Treasure => roll < 0.4 ? 0 : 1,
                    _ => roll < 0.6 ? 0 : roll < 0.93 || !big ? 1 : 2,
                };
                rng.Shuffle(candidates);
                int placed = 0;
                foreach (var c in candidates)
                {
                    if (placed >= wanted) break;
                    if (pits.Any(p => Math.Abs(p.X - c.X) + Math.Abs(p.Z - c.Z) < MinSpacing)) continue;
                    pits.Add((room, c.X, c.Z));
                    placed++;
                }
            }

            // Every dungeon gets one on its first two floors: if this is one of them and the dungeon has none yet, the roomiest suitable room takes one.
            if (floor.Floor <= 1 && pits.Count == 0 && !spots.Any(s => s.Floor <= 1))
            {
                var rng = new SeededRandom(seed, floor.Floor, 0, 906);
                var best = floor.Rooms.Where(r => SuitableRole(r.Role))
                    .Select(r => (Room: r, Cells: Candidates(floor, r, avoid)))
                    .Where(t => t.Cells.Count > 0)
                    .OrderByDescending(t => t.Cells.Count).ThenBy(t => t.Room.Id)
                    .FirstOrDefault();
                if (best.Room != null)
                {
                    var c = best.Cells[rng.Next(best.Cells.Count)];
                    pits.Add((best.Room, c.X, c.Z));
                }
            }

            foreach (var (room, x, z) in pits.OrderBy(p => p.Room.Id).ThenBy(p => p.Z).ThenBy(p => p.X))
            {
                var (wx, wy, wz) = site.ToWorld(floor.Floor, x, z);
                spots.Add(new FirePitSpot(floor.Floor, room.Id, x, z, wx + 0.5, wy, wz + 0.5));
            }
        }
        return spots;
    }

    /// <summary>Floor cells where a pit may stand: in the room, with all eight neighbours floor in the same room, and not in the avoid set.</summary>
    private static List<(int X, int Z)> Candidates(FloorLayout floor, Room room, HashSet<(int, int)> avoid)
    {
        var list = new List<(int X, int Z)>();
        for (int z = room.Bounds.Z; z < room.Bounds.Bottom; z++)
            for (int x = room.Bounds.X; x < room.Bounds.Right; x++)
            {
                if (avoid.Contains((x, z)) || !ClearRing(floor, room, x, z)) continue;
                list.Add((x, z));
            }
        return list;
    }

    /// <summary>The cell and its eight neighbours are all plain floor belonging to the room.</summary>
    public static bool ClearRing(FloorLayout floor, Room room, int x, int z)
    {
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (floor.CellAt(x + dx, z + dz) != CellKind.Floor || floor.RoomIdAt(x + dx, z + dz) != room.Id) return false;
        return true;
    }

    private static HashSet<(int, int)> AvoidCells(DungeonSite site, FloorLayout floor, IReadOnlyList<NoteSpot> notes)
    {
        var avoid = new HashSet<(int, int)>();
        void Around(int cx, int cz)
        {
            for (int dz = -Clearance; dz <= Clearance; dz++)
                for (int dx = -Clearance; dx <= Clearance; dx++)
                    avoid.Add((cx + dx, cz + dz));
        }

        for (int z = 0; z < FloorLayout.Size; z++)
            for (int x = 0; x < FloorLayout.Size; x++)
                if (floor.CellAt(x, z) is CellKind.Door or CellKind.SecretDoor) Around(x, z);
        Around(floor.EntranceX, floor.EntranceZ);
        if (floor.ExitStairs is { } exit) Around(exit.X, exit.Z);
        foreach (var s in site.Spawns)
            if (s.Floor == floor.Floor) Around(s.X - site.OriginX, s.Z - site.OriginZ);
        foreach (var n in notes)
            if (n.Floor == floor.Floor) Around(n.CellX, n.CellZ);
        return avoid;
    }
}
