using Hollowfall.Core.Dungeon;

namespace Hollowfall.Core.Story;

/// <summary>
/// A lore note lying on a dungeon floor. <see cref="Index"/> is unique within the dungeon; the cell is the floor cell it lies
/// in, the face is the way the wall beside it looks into the room, and X, Y, Z is where the scroll is drawn.
/// </summary>
public readonly record struct NoteSpot(int Index, int Floor, int RoomId, int CellX, int CellZ, int FaceX, int FaceZ, double X, double Y, double Z, StoryNote Note);

/// <summary>
/// Decides where lore notes lie. Like torches and spawns it is a pure function of the dungeon and the seed, so every run of a
/// seed puts the same notes in the same places. One to three notes lie on each floor, each beside a wall in a room the player
/// can reach on foot without finding a secret door. The entrance and boss rooms, trap rooms, the cells around stairs and doors,
/// and the cells near anything spawned (chests above all, since one press of Interact must never do two things) are left alone.
/// The notes on a floor are one voice, handed out in reading order, with the earliest in the room nearest the entrance.
/// </summary>
public static class NotePlacer
{
    /// <summary>Least distance in cells between two notes on a floor.</summary>
    public const int MinSpacing = 8;

    /// <summary>How close to a chest a note may not be, in cells.</summary>
    public const double ChestClearance = 4;

    /// <summary>How far the player may stand from a note and still read it, in blocks.</summary>
    public const double Reach = 2.2;

    /// <summary>How far above or below the note the player may be, in blocks.</summary>
    public const double ReachHeight = 2.2;

    private static readonly (int X, int Z)[] Directions = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static IReadOnlyList<NoteSpot> Place(DungeonSite site, long seed) => Place(site, seed, StoryPlan.For(seed, site.FloorCount));

    public static IReadOnlyList<NoteSpot> Place(DungeonSite site, long seed, StoryPlan plan)
    {
        var spots = new List<NoteSpot>();
        foreach (var floor in site.Floors)
        {
            var run = plan.RunFor(floor.Floor);
            if (run.Count == 0) continue;

            var rng = new SeededRandom(seed, floor.Floor, 0, 820);
            int wanted = Math.Min(rng.Range(1, StoryPlan.MaxNotesPerFloor), run.Count);
            var reachable = HopsToRooms(floor).Keys.ToHashSet();
            var spawns = site.Spawns.Where(s => s.Floor == floor.Floor).ToList();

            var candidates = new List<(Room Room, int X, int Z, int Dx, int Dz)>();
            foreach (var room in floor.Rooms)
            {
                if (room.Role is RoomRole.Entrance or RoomRole.Boss or RoomRole.Trap || !reachable.Contains(room.Id)) continue;
                for (int z = room.Bounds.Z; z < room.Bounds.Bottom; z++)
                    for (int x = room.Bounds.X; x < room.Bounds.Right; x++)
                    {
                        if (floor.CellAt(x, z) != CellKind.Floor || floor.RoomIdAt(x, z) != room.Id) continue;
                        if (NearStairs(floor, x, z) || NearDoor(floor, x, z) || NearSpawn(spawns, x, z, site)) continue;
                        // A note lies against a wall, so it faces away from a rock neighbour.
                        foreach (var (dx, dz) in Directions)
                            if (floor.CellAt(x - dx, z - dz) == CellKind.Rock)
                            {
                                candidates.Add((room, x, z, dx, dz));
                                break;
                            }
                    }
            }
            if (candidates.Count == 0) continue;

            rng.Shuffle(candidates);
            var picked = new List<(Room Room, int X, int Z, int Dx, int Dz)>();
            // First try for a different room each time, then settle for sharing one.
            foreach (bool distinctRooms in new[] { true, false })
                foreach (var c in candidates)
                {
                    if (picked.Count >= wanted) break;
                    if (picked.Contains(c)) continue;
                    if (distinctRooms && picked.Any(p => p.Room.Id == c.Room.Id)) continue;
                    if (picked.Any(p => Math.Abs(p.X - c.X) + Math.Abs(p.Z - c.Z) < MinSpacing)) continue;
                    picked.Add(c);
                }

            // The first note of the run is the one nearest the entrance, so the story reads in the order you walk it.
            picked.Sort((a, b) =>
            {
                int byDepth = a.Room.Depth.CompareTo(b.Room.Depth);
                if (byDepth != 0) return byDepth;
                int byRoom = a.Room.Id.CompareTo(b.Room.Id);
                return byRoom != 0 ? byRoom : (a.Z * FloorLayout.Size + a.X).CompareTo(b.Z * FloorLayout.Size + b.X);
            });
            for (int i = 0; i < picked.Count; i++)
            {
                var (room, x, z, dx, dz) = picked[i];
                var (wx, wy, wz) = site.ToWorld(floor.Floor, x, z);
                // The scroll lies a little toward the wall so it is out of the way of a doorway or a fight.
                spots.Add(new NoteSpot(spots.Count, floor.Floor, room.Id, x, z, dx, dz, wx + 0.5 - dx * 0.3, wy, wz + 0.5 - dz * 0.3, run[i]));
            }
        }
        return spots;
    }

    /// <summary>The nearest note the player can read from here, or null.</summary>
    public static NoteSpot? Nearest(IReadOnlyList<NoteSpot> spots, double x, double y, double z)
    {
        NoteSpot? best = null;
        double bestDistance = Reach;
        foreach (var s in spots)
        {
            if (Math.Abs(y - s.Y) > ReachHeight) continue;
            double d = Math.Sqrt((x - s.X) * (x - s.X) + (z - s.Z) * (z - s.Z));
            if (d <= bestDistance)
            {
                best = s;
                bestDistance = d;
            }
        }
        return best;
    }

    /// <summary>
    /// The rooms joined to the entrance by ordinary links, with no secret door in the way, and how many portal hops (gaps) the way
    /// there crosses at least. Every gap can be crossed (the tests prove it with the real portals), so a room behind gaps is reachable.
    /// </summary>
    public static Dictionary<int, int> HopsToRooms(FloorLayout floor)
    {
        var hops = new Dictionary<int, int> { [floor.EntranceRoom.Id] = 0 };
        var current = new Queue<int>([floor.EntranceRoom.Id]);
        var next = new Queue<int>();
        int level = 0;
        while (current.Count > 0 || next.Count > 0)
        {
            if (current.Count == 0)
            {
                (current, next) = (next, current);
                level++;
            }
            int room = current.Dequeue();
            if (hops[room] != level) continue;
            foreach (var link in floor.Links)
            {
                if (link.Secret) continue;
                int other = link.A == room ? link.B : link.B == room ? link.A : -1;
                if (other < 0) continue;
                int cost = level + (link.Kind is LinkKind.PortalGap or LinkKind.ShaftGap ? 1 : 0);
                if (hops.TryGetValue(other, out int known) && known <= cost) continue;
                hops[other] = cost;
                (cost == level ? current : next).Enqueue(other);
            }
        }
        return hops;
    }

    private static bool NearStairs(FloorLayout floor, int x, int z)
    {
        if (Math.Abs(x - floor.EntranceX) <= 3 && Math.Abs(z - floor.EntranceZ) <= 3) return true;
        return floor.ExitStairs is { } e && Math.Abs(x - e.X) <= 3 && Math.Abs(z - e.Z) <= 3;
    }

    private static bool NearDoor(FloorLayout floor, int x, int z)
    {
        for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
                if (floor.CellAt(x + dx, z + dz) is CellKind.Door or CellKind.SecretDoor) return true;
        return false;
    }

    private static bool NearSpawn(List<Spawn> spawns, int x, int z, DungeonSite site)
    {
        foreach (var s in spawns)
        {
            int sx = s.X - site.OriginX, sz = s.Z - site.OriginZ;
            int chebyshev = Math.Max(Math.Abs(sx - x), Math.Abs(sz - z));
            if (chebyshev < 2) return true;
            if (s.Kind == SpawnKind.Loot && Math.Sqrt((double)(sx - x) * (sx - x) + (double)(sz - z) * (sz - z)) < ChestClearance) return true;
        }
        return false;
    }
}
