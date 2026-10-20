using VoxelGame.Core.World;

namespace VoxelGame.Core.Terrain;

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

    public long Seed { get; }

    private readonly PerlinNoise _continent;
    private readonly PerlinNoise _hills;
    private readonly PerlinNoise _ridge;
    private readonly PerlinNoise _temperature;
    private readonly PerlinNoise _humidity;
    private readonly PerlinNoise _caveA;
    private readonly PerlinNoise _caveB;
    private readonly PerlinNoise _detail;

    public TerrainGenerator(long seed)
    {
        Seed = seed;
        _continent = new PerlinNoise(SeedHash.Derive(seed, 1));
        _hills = new PerlinNoise(SeedHash.Derive(seed, 2));
        _ridge = new PerlinNoise(SeedHash.Derive(seed, 3));
        _temperature = new PerlinNoise(SeedHash.Derive(seed, 4));
        _humidity = new PerlinNoise(SeedHash.Derive(seed, 5));
        _caveA = new PerlinNoise(SeedHash.Derive(seed, 6));
        _caveB = new PerlinNoise(SeedHash.Derive(seed, 7));
        _detail = new PerlinNoise(SeedHash.Derive(seed, 8));
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

        int height = Math.Clamp((int)Math.Round(h), BedrockLayers + 1, Chunk.Height - 2);
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

        for (int lx = 0; lx < Chunk.Size; lx++)
            for (int lz = 0; lz < Chunk.Size; lz++)
            {
                int wx = ox + lx, wz = oz + lz;
                var col = SampleColumn(wx, wz);
                FillColumn(chunk, lx, lz, wx, wz, col);
            }

        PlaceVegetation(chunk);
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
                b = BlockType.Stone;
            else
                b = SurfaceBlock(col.Biome, y == h, y, h);

            // Carve caves, but keep a skin so they don't breach the surface.
            if (b != BlockType.Bedrock && y > BedrockLayers + 1 && y < h - 4 && IsCave(wx, y, wz))
                b = BlockType.Air;

            chunk[lx, y, lz] = b;
        }

        for (int y = h + 1; y <= SeaLevel; y++)
            chunk[lx, y, lz] = BlockType.Water;
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
            case Biome.Desert:
                return top ? BlockType.Sand : BlockType.Sandstone;
            case Biome.Mountains:
                return h >= SeaLevel + 58 ? BlockType.Snow : BlockType.Stone;
            default:
                return top ? (underwater ? BlockType.Dirt : BlockType.Grass) : BlockType.Dirt;
        }
    }

    private void PlaceVegetation(Chunk chunk)
    {
        int ox = chunk.WorldMinX, oz = chunk.WorldMinZ;

        // Scan beyond the border so canopies of neighbouring trees spill in consistently.
        for (int wx = ox - TreeMargin; wx < ox + Chunk.Size + TreeMargin; wx++)
            for (int wz = oz - TreeMargin; wz < oz + Chunk.Size + TreeMargin; wz++)
            {
                ulong h = SeedHash.Hash(Seed, wx, wz, 100);
                var col = SampleColumn(wx, wz);
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
