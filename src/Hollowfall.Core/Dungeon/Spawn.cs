namespace Hollowfall.Core.Dungeon;

public enum SpawnKind
{
    Monster,
    Boss,
    Loot,
    Key,
    Trap,
}

/// <summary>
/// Where something should appear. Data only: AI, loot tables and rendering are for later.
/// <see cref="Tier"/> scales difficulty with depth and room role; a <see cref="SpawnKind.Key"/>
/// opens the exit stairs of its floor.
/// </summary>
public readonly record struct Spawn(SpawnKind Kind, int X, int Y, int Z, int Floor, int RoomId, int Tier);

public static class SpawnPlanner
{
    public static IReadOnlyList<Spawn> Plan(DungeonSite site, long seed)
    {
        var spawns = new List<Spawn>();
        foreach (var floor in site.Floors)
        {
            var rng = new SeededRandom(seed, floor.Floor, 0, 600);
            var taken = new HashSet<(int, int)>();

            (int X, int Z)? Pick(Room room)
            {
                var cells = new List<(int X, int Z)>();
                for (int z = room.Bounds.Z; z < room.Bounds.Bottom; z++)
                    for (int x = room.Bounds.X; x < room.Bounds.Right; x++)
                        if (floor.CellAt(x, z) == CellKind.Floor && floor.RoomIdAt(x, z) == room.Id && !NearStairs(floor, x, z) && !taken.Contains((x, z)))
                            cells.Add((x, z));
                if (cells.Count == 0) return null;
                var c = cells[rng.Next(cells.Count)];
                taken.Add(c);
                return c;
            }

            void Add(SpawnKind kind, Room room, int tier)
            {
                if (Pick(room) is not { } c) return;
                var (wx, wy, wz) = site.ToWorld(floor.Floor, c.X, c.Z);
                spawns.Add(new Spawn(kind, wx, wy, wz, floor.Floor, room.Id, tier));
            }

            int baseTier = floor.Floor + 1;
            foreach (var room in floor.Rooms)
            {
                switch (room.Role)
                {
                    case RoomRole.Standard:
                        for (int i = rng.Range(1, 2 + floor.Floor); i > 0; i--) Add(SpawnKind.Monster, room, baseTier);
                        if (rng.Chance(0.3)) Add(SpawnKind.Loot, room, baseTier);
                        break;
                    case RoomRole.Trap:
                        for (int i = rng.Range(2, 4); i > 0; i--) Add(SpawnKind.Trap, room, baseTier);
                        Add(SpawnKind.Monster, room, baseTier);
                        break;
                    case RoomRole.Treasure:
                        for (int i = rng.Range(1, 2); i > 0; i--) Add(SpawnKind.Loot, room, baseTier + 1);
                        Add(SpawnKind.Monster, room, baseTier + 1);
                        break;
                    case RoomRole.Exit:
                        for (int i = rng.Range(1, 2); i > 0; i--) Add(SpawnKind.Monster, room, baseTier + 1);
                        break;
                    case RoomRole.Boss:
                        Add(SpawnKind.Boss, room, baseTier + 2);
                        Add(SpawnKind.Loot, room, baseTier + 2);
                        Add(SpawnKind.Loot, room, baseTier + 2);
                        break;
                        // Entrance and Safe rooms stay empty.
                }
            }

            // One key for every locked chest on the floor, left in rooms that are not treasure rooms and away from the
            // entrance, so each locked chest can be opened by someone who explores.
            int locked = spawns.Count(sp => sp.Kind == SpawnKind.Loot && sp.Floor == floor.Floor && sp.Tier >= sp.Floor + 2);
            if (locked > 0)
            {
                var keyRooms = floor.Rooms
                    .Where(r => r.Role is RoomRole.Standard or RoomRole.Trap && r.Depth >= 1)
                    .OrderBy(r => r.Id)
                    .ToList();
                if (keyRooms.Count == 0)
                    keyRooms = [.. floor.Rooms.Where(r => r.Role is RoomRole.Standard or RoomRole.Trap or RoomRole.Exit).OrderBy(r => r.Id)];
                if (keyRooms.Count > 0)
                    for (int i = 0; i < locked; i++) Add(SpawnKind.Key, keyRooms[rng.Next(keyRooms.Count)], baseTier);
            }
        }
        return spawns;
    }

    private static bool NearStairs(FloorLayout floor, int x, int z)
    {
        if (Math.Abs(x - floor.EntranceX) <= 3 && Math.Abs(z - floor.EntranceZ) <= 3) return true;
        return floor.ExitStairs is { } e && Math.Abs(x - e.X) <= 3 && Math.Abs(z - e.Z) <= 3;
    }
}
