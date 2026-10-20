using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Story;

/// <summary>
/// Where the Wulver stands. <see cref="X"/>, <see cref="Y"/>, <see cref="Z"/> are his feet (the middle of the top of the ground block,
/// one above <see cref="GroundY"/>), and <see cref="Yaw"/> turns a model that faces -Z toward the dungeon entrance he is found from.
/// </summary>
public readonly record struct WulverSpot(int BlockX, int BlockZ, int GroundY, double X, double Y, double Z, double Yaw, int AnchorX, int AnchorZ, double Distance);

/// <summary>
/// Places the Wulver on a hilltop. A pure function of the seed: among the surface columns 45 to 110 blocks from the dungeon entrance the
/// player spawns beside, it takes the highest one that has a free, level 3 by 3 patch of solid ground (not water, snow, leaves, logs or
/// cactus) with headroom, so he can be found with a little exploring. The same seed always gives the same spot.
/// </summary>
public static class WulverPlacer
{
    public const int MinDistance = 45;
    public const int MaxDistance = 110;
    public const int Headroom = 4;

    private const int Step = 2;
    private const int MaxChecks = 600;

    /// <summary>The nearest dungeon to the origin, which is where the player spawns beside; null if there is none.</summary>
    public static DungeonSite? NearestSite(TerrainGenerator generator)
    {
        DungeonSite? best = null;
        double bestDist = double.MaxValue;
        for (int rx = -3; rx <= 2; rx++)
            for (int rz = -3; rz <= 2; rz++)
                if (generator.Dungeons?.GetSite(rx, rz) is { } site)
                {
                    double d = (double)site.EntranceX * site.EntranceX + (double)site.EntranceZ * site.EntranceZ;
                    if (d < bestDist) { best = site; bestDist = d; }
                }
        return best;
    }

    public static WulverSpot? Place(VoxelWorld world) => Place(world, NearestSite(world.Generator));

    /// <summary>Places him relative to a site's entrance (or the origin when the world has no dungeon).</summary>
    public static WulverSpot? Place(VoxelWorld world, DungeonSite? site)
    {
        var gen = world.Generator;
        int ax = site?.EntranceX ?? 0, az = site?.EntranceZ ?? 0;

        var candidates = new List<(int X, int Z, int Height, ulong Tie)>();
        for (int x = ax - MaxDistance; x <= ax + MaxDistance; x += Step)
            for (int z = az - MaxDistance; z <= az + MaxDistance; z += Step)
            {
                double d = Math.Sqrt((double)(x - ax) * (x - ax) + (double)(z - az) * (z - az));
                if (d < MinDistance || d > MaxDistance) continue;
                var col = gen.SampleColumn(x, z);
                if (col.Height <= TerrainGenerator.SeaLevel + 2 || col.Biome is Biome.Ocean or Biome.River or Biome.Beach) continue;
                candidates.Add((x, z, col.Height, SeedHash.Hash(gen.Seed, x, z, 995)));
            }
        candidates.Sort((a, b) => a.Height != b.Height ? b.Height.CompareTo(a.Height) : a.Tie.CompareTo(b.Tie));

        int checks = 0;
        foreach (var c in candidates)
        {
            if (checks++ >= MaxChecks) break;
            if (!Suitable(world, c.X, c.Z, c.Height)) continue;
            double dx = ax - c.X, dz = az - c.Z;
            double len = Math.Sqrt(dx * dx + dz * dz);
            double yaw = Math.Atan2(-dx / len, -dz / len);
            return new WulverSpot(c.X, c.Z, c.Height, c.X + 0.5, c.Height + 1, c.Z + 0.5, yaw, ax, az, len);
        }
        return null;
    }

    /// <summary>A level, dry, solid 3 by 3 patch centred here, clear of any dungeon footprint, with headroom above it.</summary>
    private static bool Suitable(VoxelWorld world, int cx, int cz, int centreHeight)
    {
        var gen = world.Generator;
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int x = cx + dx, z = cz + dz;
                var col = gen.SampleColumn(x, z);
                if (col.Height != centreHeight) return false;
                if (col.Biome is Biome.Ocean or Biome.River or Biome.Beach) return false;
                var ground = world.GetBlock(x, col.Height, z);
                if (!BlockInfo.IsSolid(ground) || ground is BlockType.Snow or BlockType.Water or BlockType.Leaves or BlockType.Log or BlockType.Cactus) return false;
                for (int h = 1; h <= Headroom; h++)
                {
                    var above = world.GetBlock(x, col.Height + h, z);
                    if (above != BlockType.Air && !BlockInfo.IsPlant(above)) return false;
                }
            }

        // Keep clear of any dungeon footprint (and a margin), so he never stands on a plaza.
        for (int rx = DungeonMap.RegionOfChunk((cx - 8) >> 4); rx <= DungeonMap.RegionOfChunk((cx + 8) >> 4); rx++)
            for (int rz = DungeonMap.RegionOfChunk((cz - 8) >> 4); rz <= DungeonMap.RegionOfChunk((cz + 8) >> 4); rz++)
                if (gen.Dungeons?.GetSite(rx, rz) is { } s && cx >= s.OriginX - 8 && cx < s.OriginX + FloorLayout.Size + 8 && cz >= s.OriginZ - 8 && cz < s.OriginZ + FloorLayout.Size + 8)
                    return false;
        return true;
    }
}
