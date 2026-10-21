using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Story;

/// <summary>
/// Where the cat rests: on a little stone seat beside one fire pit (never in it). X, Y, Z is the middle of the seat on the floor, and
/// <see cref="Yaw"/> turns a model that faces -Z toward the fire.
/// </summary>
public readonly record struct CatSpot(FirePitSpot Pit, int Dx, int Dz, double X, double Y, double Z, double Yaw);

/// <summary>
/// Places the one cat of a world. A pure function of the seed: it takes the nearest dungeon to the origin (the one the player spawns
/// beside) and one of its fire pits, preferring a pit in a Safe room on the first two floors, then any Safe room, then the deepest pit
/// on floor 1, then floor 0. The seat is on one of the four sides of the pit, in the free ring of floor around it.
/// </summary>
public static class CatPlacer
{
    /// <summary>How far the middle of the seat is from the middle of the pit, in blocks. The ring around a pit is one cell wide, so the seat fits inside it.</summary>
    public const double SeatDistance = 1.05;

    public static CatSpot? Place(VoxelWorld world) => Place(WulverPlacer.NearestSite(world.Generator), world.Seed);

    public static CatSpot? Place(DungeonSite? site, long seed)
    {
        if (site == null) return null;
        var pits = FirePitPlacer.Place(site, seed);
        if (pits.Count == 0) return null;

        bool IsSafe(FirePitSpot p) => site.Floors[p.Floor].Rooms[p.RoomId].Role == RoomRole.Safe;
        var pool = pits.Where(p => IsSafe(p) && p.Floor <= 1).ToList();
        if (pool.Count == 0) pool = [.. pits.Where(IsSafe)];
        if (pool.Count == 0)
        {
            int deepest = pits.Where(p => p.Floor <= 1).Select(p => p.Floor).DefaultIfEmpty(-1).Max();
            pool = [.. pits.Where(p => p.Floor == deepest)];
        }
        if (pool.Count == 0) pool = [.. pits];

        var pit = pool[(int)(SeedHash.Hash(seed, 0, 0, 911) % (ulong)pool.Count)];
        var sides = new (int Dx, int Dz)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        var (dx, dz) = sides[(int)(SeedHash.Hash(seed, pit.CellX, pit.CellZ, 912) % 4)];
        // The pit's ring is all floor, so any side is free; the seat looks back at the fire.
        double yaw = Math.Atan2(dx, dz);
        return new CatSpot(pit, dx, dz, pit.X + dx * SeatDistance, pit.Y, pit.Z + dz * SeatDistance, yaw);
    }
}
