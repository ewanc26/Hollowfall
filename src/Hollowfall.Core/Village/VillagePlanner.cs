using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Village;

/// <summary>
/// Decides where villages stand. The world is cut into square cells; each cell holds at most one village, chosen by its seed. A
/// village needs open plains or forest with ground that is nearly level, and no dungeon under its yard. Plans are pure functions of
/// the seed and the cell, so every chunk that touches a village computes the same one.
/// </summary>
public sealed class VillagePlanner
{
    public const int CellSize = 192;
    private const double CellChance = 0.4;
    private const int MaxRelief = 3;
    private const int SampleSpacing = 6;
    private const int MinHouses = 3, MaxHouses = 6;
    private const int HouseClearance = 2;
    private const int PlazaClearance = 3;

    private readonly TerrainGenerator _terrain;

    internal VillagePlanner(TerrainGenerator terrain) => _terrain = terrain;

    /// <summary>The villages whose yard touches the chunk.</summary>
    public IEnumerable<VillagePlan> Overlapping(int chunkX, int chunkZ)
    {
        int minX = chunkX * Chunk.Size - VillagePlan.Yard, maxX = chunkX * Chunk.Size + Chunk.Size - 1 + VillagePlan.Yard;
        int minZ = chunkZ * Chunk.Size - VillagePlan.Yard, maxZ = chunkZ * Chunk.Size + Chunk.Size - 1 + VillagePlan.Yard;
        for (int cx = FloorDiv(minX, CellSize); cx <= FloorDiv(maxX, CellSize); cx++)
            for (int cz = FloorDiv(minZ, CellSize); cz <= FloorDiv(maxZ, CellSize); cz++)
                if (PlanFor(cx, cz) is { } plan) yield return plan;
    }

    public VillagePlan? PlanFor(int cellX, int cellZ)
    {
        var rng = new SeededRandom(_terrain.Seed, cellX, cellZ, 500);
        if (!rng.Chance(CellChance)) return null;

        int cx = cellX * CellSize + rng.Range(48, CellSize - 48);
        int cz = cellZ * CellSize + rng.Range(48, CellSize - 48);
        if (!Level(cx, cz, out int ground) || NearDungeon(cx, cz)) return null;

        var houses = new List<House>();
        var roads = new HashSet<(int X, int Z)>();
        int target = rng.Range(MinHouses, MaxHouses);
        for (int attempt = 0; attempt < 40 && houses.Count < target; attempt++)
        {
            double angle = rng.NextDouble() * Math.PI * 2;
            double distance = 9 + rng.NextDouble() * 11;
            int width = rng.Range(5, 7), depth = rng.Range(5, 6);
            int x0 = (int)Math.Round(cx + Math.Cos(angle) * distance - width / 2.0);
            int z0 = (int)Math.Round(cz + Math.Sin(angle) * distance - depth / 2.0);
            var house = new House(x0, z0, width, depth, DoorSideToward(x0, z0, width, depth, cx, cz));
            TryAdd(house, cx, cz, houses, roads);
        }

        if (houses.Count < MinHouses) return null;
        return new VillagePlan(cx, cz, ground, houses, roads);
    }

    /// <summary>Accepts a house if it sits in the yard, clear of the plaza, the other houses and their roads, and it has a road to the plaza.</summary>
    private static void TryAdd(House house, int cx, int cz, List<House> houses, HashSet<(int X, int Z)> roads)
    {
        int reach = VillagePlan.Yard - PlazaClearance;
        if (house.X < cx - reach || house.Z < cz - reach || house.X + house.Width - 1 > cx + reach || house.Z + house.Depth - 1 > cz + reach) return;
        if (Overlaps(house, cx - PlazaClearance, cz - PlazaClearance, cx + PlazaClearance, cz + PlazaClearance, 1)) return;
        foreach (var other in houses)
            if (Overlaps(house, other.X, other.Z, other.X + other.Width - 1, other.Z + other.Depth - 1, HouseClearance)) return;
        foreach (var (rx, rz) in roads)
            if (Overlaps(house, rx, rz, rx, rz, 1)) return;

        var start = house.Doorstep;
        foreach (var route in new[] { Route(start, cx, cz, horizontalFirst: true), Route(start, cx, cz, horizontalFirst: false) })
        {
            if (route.Any(cell => houses.Any(h => Overlaps(h, cell.X, cell.Z, cell.X, cell.Z, 1)))) continue;
            houses.Add(house);
            foreach (var cell in route) roads.Add(cell);
            return;
        }
    }

    private static List<(int X, int Z)> Route((int X, int Z) from, int cx, int cz, bool horizontalFirst)
    {
        var cells = new List<(int X, int Z)>();
        if (horizontalFirst)
        {
            Walk(cells, from.X, from.Z, cx, from.Z, along: true);
            Walk(cells, cx, from.Z, cx, cz, along: false);
        }
        else
        {
            Walk(cells, from.X, from.Z, from.X, cz, along: false);
            Walk(cells, from.X, cz, cx, cz, along: true);
        }
        return cells;
    }

    private static void Walk(List<(int X, int Z)> cells, int fromX, int fromZ, int toX, int toZ, bool along)
    {
        int dx = Math.Sign(toX - fromX), dz = Math.Sign(toZ - fromZ);
        int x = fromX, z = fromZ;
        cells.Add((x, z));
        while (x != toX || z != toZ)
        {
            if (along) x += dx; else z += dz;
            cells.Add((x, z));
        }
    }

    private static bool Overlaps(House h, int minX, int minZ, int maxX, int maxZ, int margin) =>
        h.X - margin <= maxX && minX <= h.X + h.Width - 1 + margin && h.Z - margin <= maxZ && minZ <= h.Z + h.Depth - 1 + margin;

    private static int DoorSideToward(int x0, int z0, int width, int depth, int cx, int cz)
    {
        double dx = cx - (x0 + width / 2.0), dz = cz - (z0 + depth / 2.0);
        if (Math.Abs(dx) > Math.Abs(dz)) return dx > 0 ? 3 : 2;
        return dz > 0 ? 1 : 0;
    }

    /// <summary>True when the ground is open plains or forest, dry and nearly level across the yard. The centre's height is the village ground.</summary>
    private bool Level(int cx, int cz, out int ground)
    {
        ground = _terrain.SampleColumn(cx, cz).Height;
        int min = int.MaxValue, max = int.MinValue;
        for (int i = -4; i <= 4; i++)
            for (int j = -4; j <= 4; j++)
            {
                var col = _terrain.SampleColumn(cx + i * SampleSpacing, cz + j * SampleSpacing);
                if (col.Biome is not (Biome.Plains or Biome.Forest) || col.Height < TerrainGenerator.SeaLevel + 2) return false;
                min = Math.Min(min, col.Height);
                max = Math.Max(max, col.Height);
            }
        return max - min <= MaxRelief;
    }

    private bool NearDungeon(int cx, int cz)
    {
        if (_terrain.Dungeons is null) return false;
        int r = VillagePlan.Yard;
        int regionMinX = DungeonMap.RegionOfChunk(FloorDiv(cx - r - FloorLayout.Size, Chunk.Size)), regionMaxX = DungeonMap.RegionOfChunk(FloorDiv(cx + r, Chunk.Size));
        int regionMinZ = DungeonMap.RegionOfChunk(FloorDiv(cz - r - FloorLayout.Size, Chunk.Size)), regionMaxZ = DungeonMap.RegionOfChunk(FloorDiv(cz + r, Chunk.Size));
        for (int rx = regionMinX; rx <= regionMaxX; rx++)
            for (int rz = regionMinZ; rz <= regionMaxZ; rz++)
            {
                var site = _terrain.Dungeons.GetSite(rx, rz);
                if (site is not null && site.OriginX <= cx + r && cx - r < site.OriginX + FloorLayout.Size
                    && site.OriginZ <= cz + r && cz - r < site.OriginZ + FloorLayout.Size)
                    return true;
            }
        return false;
    }

    private static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);
}
