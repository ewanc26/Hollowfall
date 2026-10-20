using System.Collections.Concurrent;
using VoxelGame.Core.Terrain;

namespace VoxelGame.Core.Dungeon;

/// <summary>
/// Decides where dungeons go. The world is cut into 384-block regions (24 chunks); each holds
/// at most one dungeon, always entirely inside its region, so a chunk needs only its own
/// region's site. Sites are pure functions of the seed and cached.
/// </summary>
public sealed class DungeonMap
{
    public const int RegionSize = 384;
    public const int ChunksPerRegion = RegionSize / 16;

    private const double PresenceChance = 0.7;
    private const int Attempts = 12;
    private const int MinSurfaceAboveFloors = 52; // top floor ceiling is y=44: keep rock overhead

    private readonly TerrainGenerator _terrain;
    private readonly long _seed;
    private readonly ConcurrentDictionary<(int, int), Lazy<DungeonSite?>> _sites = new();

    public DungeonMap(TerrainGenerator terrain)
    {
        _terrain = terrain;
        _seed = terrain.Seed;
    }

    public static int RegionOfChunk(int chunkCoord) => (int)Math.Floor(chunkCoord / (double)ChunksPerRegion);

    public DungeonSite? SiteForChunk(int chunkX, int chunkZ) =>
        GetSite(RegionOfChunk(chunkX), RegionOfChunk(chunkZ));

    public DungeonSite? GetSite(int regionX, int regionZ) =>
        _sites.GetOrAdd((regionX, regionZ), key => new Lazy<DungeonSite?>(() => Create(key.Item1, key.Item2))).Value;

    private DungeonSite? Create(int rx, int rz)
    {
        if (!new SeededRandom(_seed, rx, rz, 399).Chance(PresenceChance)) return null;

        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var rng = new SeededRandom(_seed, rx, rz, 400 + attempt);
            int margin = 16, room = RegionSize - FloorLayout.Size - 2 * margin;
            int originX = rx * RegionSize + margin + rng.Next(room);
            int originZ = rz * RegionSize + margin + rng.Next(room);
            int localX = rng.Range(16, 80), localZ = rng.Range(16, 80);
            int floors = rng.Range(2, 4);

            if (!SuitableTerrain(originX, originZ, localX, localZ, out int surfaceY)) continue;

            long siteSeed = (long)SeedHash.Hash(_seed, rx, rz, 450);
            return new DungeonSite(siteSeed, rx, rz, originX, originZ, localX, localZ, surfaceY, floors);
        }
        return null;
    }

    private bool SuitableTerrain(int originX, int originZ, int localX, int localZ, out int surfaceY)
    {
        int ex = originX + localX, ez = originZ + localZ;
        var entrance = _terrain.SampleColumn(ex, ez);
        surfaceY = entrance.Height;
        if (entrance.Biome is Biome.Ocean or Biome.River or Biome.Beach) return false;
        if (surfaceY < TerrainGenerator.SeaLevel + 3 || surfaceY + 10 >= Core.World.Chunk.Height) return false;

        // The plaza must be fairly flat and dry.
        foreach (var (dx, dz) in new[] { (-3, -3), (3, -3), (-3, 3), (3, 3), (-3, 0), (3, 0), (0, -3), (0, 3) })
        {
            var c = _terrain.SampleColumn(ex + dx, ez + dz);
            if (Math.Abs(c.Height - surfaceY) > 4 || c.Height <= TerrainGenerator.SeaLevel) return false;
        }

        // There must be solid ground over the whole footprint, so floors never poke out of hills or lakes.
        for (int x = 0; x <= FloorLayout.Size; x += 8)
            for (int z = 0; z <= FloorLayout.Size; z += 8)
                if (_terrain.SampleColumn(originX + Math.Min(x, FloorLayout.Size - 1), originZ + Math.Min(z, FloorLayout.Size - 1)).Height < MinSurfaceAboveFloors)
                    return false;
        return true;
    }
}
