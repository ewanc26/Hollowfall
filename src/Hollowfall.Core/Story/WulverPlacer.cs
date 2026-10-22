using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.Village;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Story;

/// <summary>
/// Where the Wulver stands. <see cref="X"/>, <see cref="Y"/>, <see cref="Z"/> are his feet (the middle of the top of the ground block,
/// one above <see cref="GroundY"/>), and <see cref="Yaw"/> turns a model that faces -Z toward the dungeon entrance he is found from.
/// <see cref="Prominence"/> is how far his ground stands above the mean of the land 8 to 16 blocks around it.
/// </summary>
public readonly record struct WulverSpot(int BlockX, int BlockZ, int GroundY, double X, double Y, double Z, double Yaw, int AnchorX, int AnchorZ, double Distance, double Prominence = 0);

/// <summary>
/// Places the Wulver on a rise you can spot. A pure function of the seed: among the surface columns 45 to 140 blocks from the dungeon
/// entrance the player spawns beside, it scores each by its prominence (its height above the mean of the ring of land 8 to 16 blocks
/// around it) as well as its height, so a knoll standing out of a plain beats a plateau that is higher but flat. The best few that have a
/// free, level 3 by 3 patch of solid ground (not water, snow, leaves, logs or cactus) with headroom, and no tree or cactus within 2
/// blocks, are then judged on whether his head can be seen along the way back to the entrance, and the best of those stands there.
/// The same seed always gives the same spot.
/// </summary>
public static class WulverPlacer
{
    public const int MinDistance = 45;
    public const int MaxDistance = 140;
    public const int Headroom = 4;

    /// <summary>The ring (in blocks from the spot) whose mean height prominence is measured against.</summary>
    public const int RingMin = 8, RingMax = 16;

    /// <summary>No leaves, logs or cactus within this many blocks of him, so his shape is not lost in foliage.</summary>
    public const int ClearRadius = 2;

    /// <summary>How far from him, toward the entrance, an explorer stands when the sight line is tried.</summary>
    public static readonly int[] SightDistances = [14, 28, 42];

    private const int Step = 2;
    private const int MaxChecks = 500;
    private const int Shortlist = 24;

    /// <summary>The square (half-width in blocks) in which foliage is counted against a spot.</summary>
    private const int FoliageRadius = 7;
    private const double HeadHeight = 2.5, EyeHeight = 1.7;

    private static readonly (int X, int Z)[] RingOffsets = BuildRing();

    private static (int, int)[] BuildRing()
    {
        var list = new List<(int, int)>();
        for (int ox = -RingMax; ox <= RingMax; ox += Step)
            for (int oz = -RingMax; oz <= RingMax; oz += Step)
            {
                double d = Math.Sqrt(ox * ox + oz * oz);
                if (d >= RingMin && d <= RingMax) list.Add((ox, oz));
            }
        return [.. list];
    }

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

    /// <summary>
    /// How good a rise is: its prominence counts in full and its height above the sea a little (so among equal bumps the higher wins),
    /// each block of distance beyond the near edge of the band costs a little, and every sight distance from which his head can be seen
    /// adds a bonus. Every leaf, log or cactus block within 7 blocks of him counts a little against the spot, so he is seen against open
    /// sky and grass rather than lost in a wood.
    /// </summary>
    public static double Score(int height, double prominence, double distance = MinDistance, int sightsClear = 0, int foliage = 0) =>
        prominence + 0.35 * (height - TerrainGenerator.SeaLevel) - 0.03 * Math.Max(0, distance - MinDistance) + 3.0 * sightsClear - 0.06 * foliage;

    /// <summary>How many leaf, log and cactus blocks stand within 7 blocks of a spot, from just above the ground to six blocks up.</summary>
    public static int Foliage(VoxelWorld world, int cx, int cz, int groundY)
    {
        int count = 0;
        for (int dx = -FoliageRadius; dx <= FoliageRadius; dx++)
            for (int dz = -FoliageRadius; dz <= FoliageRadius; dz++)
                for (int y = groundY + 1; y <= groundY + 6; y++)
                    if (world.GetBlock(cx + dx, y, cz + dz) is BlockType.Leaves or BlockType.Log or BlockType.Cactus) count++;
        return count;
    }

    /// <summary>A column's height above the mean height of the land 8 to 16 blocks around it (the ring is sampled every other block).</summary>
    public static double Prominence(TerrainGenerator gen, int x, int z)
    {
        double sum = 0;
        foreach (var (ox, oz) in RingOffsets) sum += gen.SampleColumn(x + ox, z + oz).Height;
        return gen.SampleColumn(x, z).Height - sum / RingOffsets.Length;
    }

    public static WulverSpot? Place(VoxelWorld world) => Place(world, NearestSite(world.Generator));

    /// <summary>Places him relative to a site's entrance (or the origin when the world has no dungeon).</summary>
    public static WulverSpot? Place(VoxelWorld world, DungeonSite? site)
    {
        var gen = world.Generator;
        int ax = site?.EntranceX ?? 0, az = site?.EntranceZ ?? 0;

        // Heights of every other column around the band, sampled once; prominence reads the ring from this grid.
        int k = (MaxDistance + RingMax) / Step + 1, side = 2 * k + 1;
        var heights = new int[side * side];
        var biomes = new Biome[side * side];
        for (int i = 0; i < side; i++)
            for (int j = 0; j < side; j++)
            {
                var col = gen.SampleColumn(ax + (i - k) * Step, az + (j - k) * Step);
                heights[i * side + j] = col.Height;
                biomes[i * side + j] = col.Biome;
            }

        var candidates = new List<(int X, int Z, int Height, double Prominence, double Distance, double Score, ulong Tie)>();
        int span = MaxDistance / Step;
        for (int i = k - span; i <= k + span; i++)
            for (int j = k - span; j <= k + span; j++)
            {
                int x = ax + (i - k) * Step, z = az + (j - k) * Step;
                double d = Math.Sqrt((double)(x - ax) * (x - ax) + (double)(z - az) * (z - az));
                if (d < MinDistance || d > MaxDistance) continue;
                int h = heights[i * side + j];
                if (h <= TerrainGenerator.SeaLevel + 2 || biomes[i * side + j] is Biome.Ocean or Biome.River or Biome.Beach) continue;
                double sum = 0;
                foreach (var (ox, oz) in RingOffsets) sum += heights[(i + ox / Step) * side + j + oz / Step];
                double prominence = h - sum / RingOffsets.Length;
                candidates.Add((x, z, h, prominence, d, Score(h, prominence, d), SeedHash.Hash(gen.Seed, x, z, 995)));
            }
        candidates.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score) : a.Tie.CompareTo(b.Tie));

        // The best few that can hold him are judged on whether he can be seen on the way in.
        WulverSpot? best = null;
        double bestScore = double.MinValue;
        ulong bestTie = 0;
        int checks = 0, found = 0;
        foreach (var c in candidates)
        {
            if (checks++ >= MaxChecks || found >= Shortlist) break;
            if (!Suitable(world, c.X, c.Z, c.Height)) continue;
            found++;
            double dx = ax - c.X, dz = az - c.Z;
            double len = Math.Sqrt(dx * dx + dz * dz);
            int sights = SightsClear(gen, c.X, c.Z, c.Height, dx / len, dz / len, len);
            double score = Score(c.Height, c.Prominence, c.Distance, sights, Foliage(world, c.X, c.Z, c.Height));
            if (best == null || score > bestScore || (score == bestScore && c.Tie < bestTie))
            {
                double yaw = Math.Atan2(-dx / len, -dz / len);
                best = new WulverSpot(c.X, c.Z, c.Height, c.X + 0.5, c.Height + 1, c.Z + 0.5, yaw, ax, az, len, c.Prominence);
                bestScore = score;
                bestTie = c.Tie;
            }
        }
        return best;
    }

    /// <summary>
    /// How many of <see cref="SightDistances"/> (blocks back toward the entrance, at most as far as the entrance itself) have an
    /// unobstructed line from a standing explorer's eye to his head across the terrain between.
    /// </summary>
    public static int SightsClear(TerrainGenerator gen, int cx, int cz, int groundY, double dirX, double dirZ, double maxDistance)
    {
        int clear = 0;
        double headY = groundY + 1 + HeadHeight;
        foreach (int t in SightDistances)
        {
            if (t > maxDistance) continue;
            double vx = cx + 0.5 + dirX * t, vz = cz + 0.5 + dirZ * t;
            double eyeY = gen.SampleColumn((int)Math.Floor(vx), (int)Math.Floor(vz)).Height + 1 + EyeHeight;
            bool open = true;
            for (int s = 1; s < t && open; s++)
            {
                double f = (double)s / t; // 0 at his head, 1 at the viewer
                double px = cx + 0.5 + dirX * s, pz = cz + 0.5 + dirZ * s;
                double lineY = headY + (eyeY - headY) * f;
                // The top of the terrain there is its height plus one; leave half a block's slack so a bump the line grazes does not count.
                if (gen.SampleColumn((int)Math.Floor(px), (int)Math.Floor(pz)).Height + 1 > lineY + 0.5) open = false;
            }
            if (open) clear++;
        }
        return clear;
    }

    /// <summary>A level, dry, solid 3 by 3 patch centred here, with headroom above it, no foliage near and clear of any dungeon footprint.</summary>
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

        // No tree or cactus right beside him: his silhouette must not be lost in foliage.
        for (int dx = -ClearRadius; dx <= ClearRadius; dx++)
            for (int dz = -ClearRadius; dz <= ClearRadius; dz++)
            {
                int gy = gen.SampleColumn(cx + dx, cz + dz).Height;
                for (int y = gy + 1; y <= centreHeight + Headroom + 1; y++)
                    if (world.GetBlock(cx + dx, y, cz + dz) is BlockType.Leaves or BlockType.Log or BlockType.Cactus) return false;
            }

        // Keep clear of any dungeon footprint (and a margin), so he never stands on a plaza.
        for (int rx = DungeonMap.RegionOfChunk((cx - 8) >> 4); rx <= DungeonMap.RegionOfChunk((cx + 8) >> 4); rx++)
            for (int rz = DungeonMap.RegionOfChunk((cz - 8) >> 4); rz <= DungeonMap.RegionOfChunk((cz + 8) >> 4); rz++)
                if (gen.Dungeons?.GetSite(rx, rz) is { } s && cx >= s.OriginX - 8 && cx < s.OriginX + FloorLayout.Size + 8 && cz >= s.OriginZ - 8 && cz < s.OriginZ + FloorLayout.Size + 8)
                    return false;

        // And out of every village yard, so he never stands among the folk.
        const int VillageMargin = 8;
        for (int vx = -3; vx <= 3; vx++)
            for (int vz = -3; vz <= 3; vz++)
                foreach (var v in gen.Villages.Overlapping((cx >> 4) + vx, (cz >> 4) + vz))
                    if (Math.Abs(cx - v.CenterX) <= VillagePlan.Yard + VillageMargin && Math.Abs(cz - v.CenterZ) <= VillagePlan.Yard + VillageMargin)
                        return false;
        return true;
    }
}
