using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Village;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Terrain;

public readonly record struct ColumnInfo(int Height, Biome Biome);

/// <summary>
/// Deterministic, seed-driven terrain. Every query is a pure function of
/// (seed, world x, world z[, y]), so chunks can be generated in any order,
/// on any thread, and always line up at their borders.
/// </summary>
public sealed class TerrainGenerator
{
    public const int SeaLevel = 48;
    private const int BedrockLayers = 2;
    private const int TreeMargin = 2; // canopy radius
    private const double RiverWidth = 0.03;

    public long Seed { get; }

    private readonly PerlinNoise _continent;
    private readonly PerlinNoise _hills;
    private readonly PerlinNoise _ridge;
    private readonly PerlinNoise _temperature;
    private readonly PerlinNoise _humidity;
    private readonly PerlinNoise _caveA;
    private readonly PerlinNoise _caveB;
    private readonly PerlinNoise _detail;
    private readonly PerlinNoise _river;
    private readonly (OreSpec Spec, PerlinNoise Noise)[] _ores;

    /// <summary>Where dungeons sit in this world, or null if dungeons are disabled.</summary>
    public DungeonMap? Dungeons { get; }

    /// <summary>Where villages stand in this world, and the plan of each.</summary>
    public VillagePlanner Villages { get; }

    public TerrainGenerator(long seed, bool enableDungeons = true)
    {
        Seed = seed;
        Dungeons = enableDungeons ? new DungeonMap(this) : null;
        _continent = new PerlinNoise(SeedHash.Derive(seed, 1));
        _hills = new PerlinNoise(SeedHash.Derive(seed, 2));
        _ridge = new PerlinNoise(SeedHash.Derive(seed, 3));
        _temperature = new PerlinNoise(SeedHash.Derive(seed, 4));
        _humidity = new PerlinNoise(SeedHash.Derive(seed, 5));
        _caveA = new PerlinNoise(SeedHash.Derive(seed, 6));
        _caveB = new PerlinNoise(SeedHash.Derive(seed, 7));
        _detail = new PerlinNoise(SeedHash.Derive(seed, 8));
        _river = new PerlinNoise(SeedHash.Derive(seed, 9));
        _ores = OreSpec.Defaults.Select(o => (o, new PerlinNoise(SeedHash.Derive(seed, o.Salt)))).ToArray();
        Villages = new VillagePlanner(this);
    }

    /// <summary>Convenience: accept a text seed like Minecraft does.</summary>
    public static long SeedFromString(string text)
    {
        if (long.TryParse(text, out var n)) return n;
        ulong h = 1469598103934665603UL; // FNV-1a
        foreach (char c in text) { h ^= c; h *= 1099511628211UL; }
        return (long)SeedHash.Mix(h);
    }

    private static double Smooth(double e0, double e1, double x)
    {
        double t = Math.Clamp((x - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    public ColumnInfo SampleColumn(int wx, int wz)
    {
        // Large-scale shape: oceans vs. land.
        double continent = Math.Clamp(_continent.Fbm(wx / 600.0, wz / 600.0, 4) * 2.4 + 0.1, -1, 1);
        // Rolling hills on top.
        double hills = _hills.Fbm(wx / 90.0, wz / 90.0, 4);
        // Ridged noise gives mountain chains, only where the continent is high.
        double r = 1.0 - Math.Clamp(Math.Abs(_ridge.Fbm(wx / 220.0, wz / 220.0, 4)) * 3.0, 0, 1);
        double mountainMask = Smooth(0.25, 0.7, continent);
        double mountains = r * r * mountainMask;
        double detail = _detail.Sample(wx / 12.0, wz / 12.0);

        double h = SeaLevel
                   + continent * 26
                   + hills * 9 * (0.4 + 0.6 * Smooth(-0.3, 0.2, continent))
                   + mountains * 42
                   + detail * 1.5;

        // Rivers follow the zero-crossings of a noise field: |noise| small => channel.
        // They fade out toward the sea and thin out in mountains.
        double riverWidth = RiverWidth * (1 - 0.7 * mountainMask);
        double channel = (1 - Smooth(0, riverWidth, Math.Abs(_river.Fbm(wx / 300.0, wz / 300.0, 3))))
                         * Smooth(-0.05, 0.15, continent);
        const double bed = SeaLevel - 3;
        bool carved = false;
        if (channel > 0 && h > bed)
        {
            h += (bed - h) * channel;
            carved = channel > 0.5;
        }

        int height = Math.Clamp((int)Math.Round(h), BedrockLayers + 1, Chunk.Height - 2);
        if (carved && height <= SeaLevel)
            return new ColumnInfo(height, Biome.River);
        return new ColumnInfo(height, ClassifyBiome(wx, wz, height));
    }

    private Biome ClassifyBiome(int wx, int wz, int height)
    {
        if (height < SeaLevel - 1) return Biome.Ocean;
        if (height <= SeaLevel + 1) return Biome.Beach;
        if (height >= SeaLevel + 34) return Biome.Mountains;

        double temp = _temperature.Fbm(wx / 450.0, wz / 450.0, 3) * 2.4;
        double humid = _humidity.Fbm(wx / 380.0, wz / 380.0, 3) * 2.4;
        if (temp > 0.18 && humid < 0.05) return Biome.Desert;
        if (humid > 0.0) return Biome.Forest;
        return Biome.Plains;
    }

    /// <summary>The ore at a position inside solid stone, or Stone if none. Pure function of the seed.</summary>
    public BlockType StoneOrOre(int wx, int y, int wz)
    {
        foreach (var (spec, noise) in _ores)
        {
            if (y < spec.MinY || y > spec.MaxY) continue;
            if (noise.Sample(wx * spec.Scale, y * spec.Scale, wz * spec.Scale) > spec.Threshold)
                return spec.Block;
        }
        return BlockType.Stone;
    }

    private bool IsCave(int wx, int y, int wz)
    {
        // Intersection of two near-zero "sheets" of 3D noise makes winding tunnels.
        const double s = 1.0 / 28.0;
        double a = _caveA.Fbm(wx * s, y * s * 1.6, wz * s, 2);
        if (Math.Abs(a) > 0.09) return false;
        double b = _caveB.Fbm(wx * s, y * s * 1.6, wz * s, 2);
        return Math.Abs(b) < 0.09;
    }

    public Chunk GenerateChunk(int chunkX, int chunkZ)
    {
        var chunk = new Chunk(chunkX, chunkZ);
        int ox = chunk.WorldMinX, oz = chunk.WorldMinZ;

        // Sample each column once, including the margin that neighbouring trees reach over.
        const int span = Chunk.Size + 2 * TreeMargin;
        var cols = new ColumnInfo[span * span];
        for (int i = 0; i < span; i++)
            for (int j = 0; j < span; j++)
                cols[i * span + j] = SampleColumn(ox - TreeMargin + i, oz - TreeMargin + j);

        for (int lx = 0; lx < Chunk.Size; lx++)
            for (int lz = 0; lz < Chunk.Size; lz++)
                FillColumn(chunk, lx, lz, ox + lx, oz + lz, cols[(lx + TreeMargin) * span + lz + TreeMargin]);

        PlaceVegetation(chunk, cols, span);
        foreach (var village in Villages.Overlapping(chunkX, chunkZ))
            VillageBuilder.Apply(chunk, village);
        Dungeons?.SiteForChunk(chunkX, chunkZ)?.Apply(chunk);
        return chunk;
    }

    private void FillColumn(Chunk chunk, int lx, int lz, int wx, int wz, ColumnInfo col)
    {
        int h = col.Height;
        for (int y = 0; y <= h; y++)
        {
            BlockType b;
            if (y < BedrockLayers && (y == 0 || SeedHash.ToUnit(SeedHash.Hash(Seed, wx, wz, y)) < 0.5))
                b = BlockType.Bedrock;
            else if (y < h - 3 || (y < h && col.Biome == Biome.Mountains))
                b = StoneOrOre(wx, y, wz);
            else
                b = SurfaceBlock(col.Biome, y == h, y, h);

            // Carve caves, but keep a skin so they don't breach the surface.
            if (b != BlockType.Bedrock && y > BedrockLayers + 1 && y < h - 4 && IsCave(wx, y, wz))
                b = BlockType.Air;

            chunk[lx, y, lz] = b;
        }

        for (int y = h + 1; y <= SeaLevel; y++)
            chunk[lx, y, lz] = BlockType.Water;

        PlacePlant(chunk, lx, lz, wx, wz, col);
    }

    private void PlacePlant(Chunk chunk, int lx, int lz, int wx, int wz, ColumnInfo col)
    {
        if (col.Height <= SeaLevel || col.Height + 1 >= Chunk.Height) return;
        if (chunk[lx, col.Height, lz] != BlockType.Grass) return;

        double grass = col.Biome == Biome.Plains ? 0.22 : col.Biome == Biome.Forest ? 0.08 : 0;
        double flower = col.Biome == Biome.Plains ? 0.025 : col.Biome == Biome.Forest ? 0.008 : 0;
        double roll = SeedHash.ToUnit(SeedHash.Hash(Seed, wx, wz, 101));
        if (roll < flower)
            chunk[lx, col.Height + 1, lz] = BlockType.Flower;
        else if (roll < flower + grass)
            chunk[lx, col.Height + 1, lz] = BlockType.TallGrass;
    }

    private static BlockType SurfaceBlock(Biome biome, bool top, int y, int h)
    {
        bool underwater = h < SeaLevel;
        switch (biome)
        {
            case Biome.Ocean:
                return top ? (h < SeaLevel - 6 ? BlockType.Gravel : BlockType.Sand) : BlockType.Sand;
            case Biome.Beach:
                return top ? BlockType.Sand : BlockType.Sandstone;
            case Biome.River:
                return top ? BlockType.Gravel : BlockType.Dirt;
            case Biome.Desert:
                return top ? BlockType.Sand : BlockType.Sandstone;
            case Biome.Mountains:
                return h >= SeaLevel + 58 ? BlockType.Snow : BlockType.Stone;
            default:
                return top ? (underwater ? BlockType.Dirt : BlockType.Grass) : BlockType.Dirt;
        }
    }

    private void PlaceVegetation(Chunk chunk, ColumnInfo[] cols, int span)
    {
        int ox = chunk.WorldMinX, oz = chunk.WorldMinZ;

        // Scan beyond the border so canopies of neighbouring trees spill in consistently.
        for (int wx = ox - TreeMargin; wx < ox + Chunk.Size + TreeMargin; wx++)
            for (int wz = oz - TreeMargin; wz < oz + Chunk.Size + TreeMargin; wz++)
            {
                ulong h = SeedHash.Hash(Seed, wx, wz, 100);
                var col = cols[(wx - ox + TreeMargin) * span + (wz - oz + TreeMargin)];
                if (col.Height <= SeaLevel + 1 || col.Height + 10 >= Chunk.Height) continue;

                double roll = SeedHash.ToUnit(h);
                int treeHeight = 4 + (int)((h >> 3) % 3);

                switch (col.Biome)
                {
                    case Biome.Forest when roll < 0.018:
                    case Biome.Plains when roll < 0.0015:
                        PlaceTree(chunk, wx - ox, col.Height + 1, wz - oz, treeHeight);
                        break;
                    case Biome.Desert when roll < 0.004:
                        for (int i = 0; i < 1 + (int)((h >> 5) % 3); i++)
                            chunk[wx - ox, col.Height + 1 + i, wz - oz] = BlockType.Cactus;
                        break;
                }
            }
    }

    private static void PlaceTree(Chunk chunk, int lx, int baseY, int lz, int trunk)
    {
        int top = baseY + trunk;
        for (int dy = -2; dy <= 1; dy++)
        {
            int r = dy >= 0 ? 1 : 2;
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    if (r == 2 && Math.Abs(dx) == 2 && Math.Abs(dz) == 2) continue; // round corners
                    int x = lx + dx, y = top + dy, z = lz + dz;
                    if (Chunk.InBounds(x, y, z) && chunk[x, y, z] == BlockType.Air)
                        chunk[x, y, z] = BlockType.Leaves;
                }
        }
        for (int i = 0; i < trunk; i++)
            if (Chunk.InBounds(lx, baseY + i, lz))
                chunk[lx, baseY + i, lz] = BlockType.Log;
    }
}
