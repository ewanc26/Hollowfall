using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Textures;

/// <summary>The distinct pictures on the texture atlas. Several blocks and faces share one.</summary>
public enum Tile
{
    Stone,
    Dirt,
    GrassTop,
    GrassSide,
    Sand,
    Sandstone,
    Snow,
    Water,
    Gravel,
    LogSide,
    LogEnd,
    Leaves,
    Cactus,
    CoalOre,
    IronOre,
    GoldOre,
    TallGrass,
    Flower,
    StoneBrick,
    BrickFloor,
    Pillar,
    Stairs,
    Gate,
    Door,
    Bedrock,
}

/// <summary>
/// The world's surface art, drawn by code: a 16 by 16 pixel tile for every kind of surface, packed into one atlas. Tiles are
/// patterns of light and shade, not finished colours; each block's palette colour (and its ambient occlusion) tints them, so a
/// biome can recolour grass without a new picture and the dungeon's stone keeps its mood. The art is chunky on purpose, to sit
/// with the low-poly models, and every pattern wraps at its edges so neighbouring blocks join without seams.
/// </summary>
public static class BlockTextures
{
    public const int TileSize = 16;
    public const int Columns = 8;

    /// <summary>How much brighter than 1.0 the material should multiply the atlas, because tiles average a little below white.</summary>
    public const float Gain = 1.1f;

    private static readonly Lazy<byte[]> Pixels = new(Build);

    public static int TileCount => Enum.GetValues<Tile>().Length;
    public static int Rows => (TileCount + Columns - 1) / Columns;
    public static int Width => Columns * TileSize;

    /// <summary>Atlas height, rounded up to a power of two as graphics hardware likes.</summary>
    public static int Height
    {
        get
        {
            int h = 1;
            while (h < Rows * TileSize) h <<= 1;
            return h;
        }
    }

    /// <summary>The atlas as RGBA bytes, row by row from the top left. Shared: do not change it.</summary>
    public static byte[] Rgba => Pixels.Value;

    /// <summary>Whether a tile has see-through parts (plants) and so needs an alpha-tested material.</summary>
    public static bool HasAlpha(Tile t) => t is Tile.TallGrass or Tile.Flower;

    /// <summary>Which picture a block's face wears. Face numbers are the mesher's: +X, -X, +Y, -Y, +Z, -Z.</summary>
    public static Tile TileFor(BlockType b, int face) => b switch
    {
        BlockType.Bedrock => Tile.Bedrock,
        BlockType.Stone => Tile.Stone,
        BlockType.Dirt => Tile.Dirt,
        BlockType.Grass => face == 2 ? Tile.GrassTop : face == 3 ? Tile.Dirt : Tile.GrassSide,
        BlockType.Sand => Tile.Sand,
        BlockType.Sandstone => Tile.Sandstone,
        BlockType.Snow => Tile.Snow,
        BlockType.Water => Tile.Water,
        BlockType.Gravel => Tile.Gravel,
        BlockType.Log => face is 2 or 3 ? Tile.LogEnd : Tile.LogSide,
        BlockType.Leaves => Tile.Leaves,
        BlockType.Cactus => Tile.Cactus,
        BlockType.CoalOre => Tile.CoalOre,
        BlockType.IronOre => Tile.IronOre,
        BlockType.GoldOre => Tile.GoldOre,
        BlockType.TallGrass => Tile.TallGrass,
        BlockType.Flower => Tile.Flower,
        BlockType.StoneBrick or BlockType.SecretDoor => Tile.StoneBrick, // a secret door must look exactly like the wall it hides in
        BlockType.BrickFloor => Tile.BrickFloor,
        BlockType.Pillar => Tile.Pillar,
        BlockType.Stairs => Tile.Stairs,
        BlockType.Gate => Tile.Gate,
        BlockType.Door => Tile.Door,
        _ => Tile.Stone,
    };

    /// <summary>A tile's place on the atlas in 0..1 texture space, pulled in by half a pixel so filtering never reads a neighbour.</summary>
    public static (float U0, float V0, float U1, float V1) Rect(Tile t)
    {
        int col = (int)t % Columns, row = (int)t / Columns;
        float w = Width, h = Height;
        const float inset = 0.5f;
        return ((col * TileSize + inset) / w, (row * TileSize + inset) / h, ((col + 1) * TileSize - inset) / w, ((row + 1) * TileSize - inset) / h);
    }

    /// <summary>One pixel of a tile (0 to 1 per channel).</summary>
    public static (float R, float G, float B, float A) Pixel(Tile t, int x, int y)
    {
        x = ((x % TileSize) + TileSize) % TileSize;
        y = ((y % TileSize) + TileSize) % TileSize;
        var (r, g, b, a) = Paint(t, x, y);
        return (Math.Clamp(r, 0f, 1f), Math.Clamp(g, 0f, 1f), Math.Clamp(b, 0f, 1f), Math.Clamp(a, 0f, 1f));
    }

    private static byte[] Build()
    {
        var data = new byte[Width * Height * 4];
        foreach (var t in Enum.GetValues<Tile>())
        {
            int ox = (int)t % Columns * TileSize, oy = (int)t / Columns * TileSize;
            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                {
                    var (r, g, b, a) = Pixel(t, x, y);
                    int i = ((oy + y) * Width + ox + x) * 4;
                    data[i] = Byte(r);
                    data[i + 1] = Byte(g);
                    data[i + 2] = Byte(b);
                    data[i + 3] = Byte(a);
                }
        }
        return data;
    }

    private static byte Byte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

    // ---- noise that wraps at the tile edge -------------------------------------------------------

    private static float Hash(int seed, int x, int y) => (float)SeedHash.ToUnit(SeedHash.Hash(seed, x, y, 77));

    /// <summary>Smooth value noise in 0..1 with a lattice every <paramref name="cell"/> pixels; it repeats every tile.</summary>
    private static float Smooth(int seed, int x, int y, int cell)
    {
        int n = TileSize / cell;
        float fx = (float)x / cell, fy = (float)y / cell;
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        float tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        float a = Hash(seed, x0 % n, y0 % n), b = Hash(seed, (x0 + 1) % n, y0 % n);
        float c = Hash(seed, x0 % n, (y0 + 1) % n), d = Hash(seed, (x0 + 1) % n, (y0 + 1) % n);
        return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * ty;
    }

    /// <summary>Noise centred on zero, scaled to ± <paramref name="amount"/>.</summary>
    private static float Tone(int seed, int x, int y, int cell, float amount) => (Smooth(seed, x, y, cell) - 0.5f) * 2f * amount;

    private static bool Speck(int seed, int x, int y, float density) => Hash(seed, x, y) < density;

    private static (float, float, float, float) Grey(float v) => (v, v, v, 1f);

    private static (float, float, float, float) Tint(float v, float r, float g, float b) => (v * r, v * g, v * b, 1f);

    private static (float R, float G, float B, float A) Paint(Tile t, int x, int y)
    {
        switch (t)
        {
            case Tile.Stone:
                return Grey(0.84f + Tone(1, x, y, 4, 0.10f) + Tone(2, x, y, 2, 0.06f) - (Speck(3, x, y, 0.05f) ? 0.14f : 0f));

            case Tile.Bedrock:
                return Grey(0.62f + Tone(4, x, y, 4, 0.32f) + Tone(5, x, y, 2, 0.12f));

            case Tile.Dirt:
                return Tint(0.86f + Tone(6, x, y, 4, 0.08f) + Tone(7, x, y, 2, 0.06f) - (Speck(8, x, y, 0.07f) ? 0.2f : 0f), 1f, 0.97f, 0.93f);

            case Tile.GrassTop:
                {
                    // Short blades: each column has its own light and shade, with a little horizontal drift.
                    float blade = Hash(9, x, 0) * 0.14f - 0.07f;
                    return Tint(0.88f + blade + Tone(10, x, y, 2, 0.07f) + (Speck(11, x, y, 0.06f) ? 0.1f : 0f), 0.96f, 1f, 0.93f);
                }

            case Tile.GrassSide:
                {
                    // A ragged fringe of grass over earth.
                    int fringe = 3 + (int)(Hash(12, x, 0) * 4f);
                    if (y < fringe) return Tint(0.95f + Tone(13, x, y, 2, 0.06f), 0.95f, 1f, 0.9f);
                    return Tint(0.74f + Tone(14, x, y, 4, 0.07f) - (Speck(15, x, y, 0.06f) ? 0.15f : 0f), 1f, 0.95f, 0.88f);
                }

            case Tile.Sand:
                return Grey(0.93f + Tone(16, x, y, 2, 0.04f) - (Speck(17, x, y, 0.1f) ? 0.07f : 0f));

            case Tile.Sandstone:
                {
                    float strata = (y % 5 == 4) ? -0.12f : 0f;
                    return Grey(0.88f + strata + Tone(18, x, y, 4, 0.04f) + Tone(19, x, y, 2, 0.03f));
                }

            case Tile.Snow:
                return Tint(0.97f + Tone(20, x, y, 4, 0.03f) - (Speck(21, x, y, 0.06f) ? 0.05f : 0f), 0.96f, 0.98f, 1f);

            case Tile.Water:
                {
                    // Soft diagonal swells with a few bright glints.
                    float wave = MathF.Sin((x + y) * MathF.PI / 4f) * 0.05f + MathF.Sin((x - 2 * y) * MathF.PI / 8f) * 0.04f;
                    return Tint(0.88f + wave + (Speck(22, x, y, 0.04f) ? 0.1f : 0f), 0.92f, 0.98f, 1f);
                }

            case Tile.Gravel:
                return Gravel(x, y);

            case Tile.LogSide:
                {
                    // Vertical bark furrows.
                    float furrow = Hash(23, x, 0) < 0.35f ? -0.2f : 0f;
                    return Tint(0.82f + furrow + Tone(24, x, y, 8, 0.07f) - (Speck(25, x, y, 0.05f) ? 0.1f : 0f), 1f, 0.97f, 0.92f);
                }

            case Tile.LogEnd:
                {
                    float dx = x - 7.5f, dy = y - 7.5f;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    float rings = 0.5f + 0.5f * MathF.Sin(d * 2.1f);
                    float bark = Math.Max(MathF.Abs(dx), MathF.Abs(dy)) > 6.5f ? -0.25f : 0f;
                    return Tint(0.74f + rings * 0.18f + bark + Tone(26, x, y, 4, 0.03f), 1f, 0.96f, 0.88f);
                }

            case Tile.Leaves:
                return Tint(0.78f + Tone(27, x, y, 4, 0.14f) + Tone(28, x, y, 2, 0.10f) - (Speck(29, x, y, 0.08f) ? 0.22f : 0f), 0.95f, 1f, 0.95f);

            case Tile.Cactus:
                {
                    int rib = x % 4;
                    float shade = rib is 0 or 3 ? -0.16f : 0.05f;
                    float spine = Speck(30, x, y, 0.07f) ? 0.2f : 0f;
                    return Grey(0.85f + shade + spine);
                }

            case Tile.CoalOre:
                return Ore(x, y, 0.16f, 0.16f, 0.18f, 31);
            case Tile.IronOre:
                return Ore(x, y, 1f, 0.72f, 0.56f, 32);
            case Tile.GoldOre:
                return Ore(x, y, 1f, 0.86f, 0.18f, 33);

            case Tile.TallGrass:
                return Blades(x, y);

            case Tile.Flower:
                return Bloom(x, y);

            case Tile.StoneBrick:
                return Bricks(x, y);

            case Tile.BrickFloor:
                {
                    // Four flagstones with a thin grout line, each its own tone.
                    bool grout = x % 8 == 7 || y % 8 == 7;
                    float stone = 0.80f + (Hash(34, x / 8, y / 8) - 0.5f) * 0.14f;
                    return Grey(grout ? 0.5f : stone + Tone(35, x, y, 2, 0.04f) - (Speck(36, x, y, 0.05f) ? 0.1f : 0f));
                }

            case Tile.Pillar:
                {
                    // Fluted: soft vertical channels.
                    float flute = MathF.Sin(x * MathF.PI / 2f) * 0.07f;
                    bool band = y is 0 or 1 or 14 or 15;
                    return Grey(0.85f + flute + (band ? -0.08f : 0f) + Tone(37, x, y, 4, 0.03f));
                }

            case Tile.Stairs:
                {
                    bool edge = y % 4 == 3;
                    return Grey(0.82f + (edge ? -0.18f : 0.04f) + Tone(38, x, y, 4, 0.05f));
                }

            case Tile.Gate:
                {
                    // Iron bars with dark gaps, two cross bands and a few specks of rust.
                    bool bar = x % 4 is 1 or 2;
                    bool band = y is 3 or 4 or 11 or 12;
                    float v = bar || band ? 0.8f + Tone(39, x, y, 2, 0.07f) : 0.3f;
                    if ((bar || band) && Speck(40, x, y, 0.1f)) return Tint(0.8f, 1f, 0.8f, 0.7f);
                    return Grey(v);
                }

            case Tile.Door:
                {
                    bool seam = x % 4 == 0;
                    float grain = Tone(41, x, y, 8, 0.05f) + (Hash(42, x / 4, 0) - 0.5f) * 0.1f;
                    bool hinge = x is 1 or 2 && y is 3 or 4 or 11 or 12;
                    return Tint((seam ? 0.5f : 0.84f) + grain + (hinge ? -0.2f : 0f), 1f, 0.94f, 0.84f);
                }

            default:
                return Grey(0.8f);
        }
    }

    private static (float, float, float, float) Ore(int x, int y, float r, float g, float b, int seed)
    {
        float stone = 0.84f + Tone(1, x, y, 4, 0.10f) + Tone(2, x, y, 2, 0.06f);
        // Veins of chunky flecks: a few 2 by 2 clusters of the ore's colour.
        for (int i = 0; i < 6; i++)
        {
            int cx = (int)(Hash(seed, i, 0) * 15f), cy = (int)(Hash(seed, i, 1) * 15f);
            int dx = ((x - cx) % TileSize + TileSize) % TileSize, dy = ((y - cy) % TileSize + TileSize) % TileSize;
            if (dx < 2 && dy < 2 && !(dx == 1 && dy == 1 && i % 2 == 0)) return (r, g, b, 1f);
        }
        return (stone, stone, stone, 1f);
    }

    private static (float, float, float, float) Bricks(int x, int y)
    {
        int row = y / 4;
        int shifted = (x + (row % 2 == 0 ? 0 : 4)) % TileSize;
        bool mortar = y % 4 == 3 || shifted % 8 == 7;
        int brick = (x + (row % 2 == 0 ? 0 : 4)) / 8;
        float tone = 0.84f + (Hash(43, brick, row) - 0.5f) * 0.16f;
        if (mortar) return Grey(0.52f + Tone(44, x, y, 2, 0.04f));
        return Grey(tone + Tone(45, x, y, 2, 0.05f) - (Speck(46, x, y, 0.05f) ? 0.1f : 0f) + (y % 4 == 0 ? 0.05f : 0f));
    }

    /// <summary>Pebbles: each pixel belongs to the nearest of a jittered grid of points, tinted per pebble, with dark seams between them.</summary>
    private static (float, float, float, float) Gravel(int x, int y)
    {
        const int cell = 4, n = TileSize / cell;
        float best = float.MaxValue, second = float.MaxValue;
        int bx = 0, by = 0;
        for (int cy = -1; cy <= n; cy++)
            for (int cx = -1; cx <= n; cx++)
            {
                int wx = ((cx % n) + n) % n, wy = ((cy % n) + n) % n;
                float px = cx * cell + 0.5f + Hash(47, wx, wy) * (cell - 1);
                float py = cy * cell + 0.5f + Hash(48, wx, wy) * (cell - 1);
                float d = (px - x) * (px - x) + (py - y) * (py - y);
                if (d < best) { second = best; best = d; bx = wx; by = wy; }
                else if (d < second) second = d;
            }
        float seam = MathF.Sqrt(second) - MathF.Sqrt(best) < 0.9f ? -0.22f : 0f;
        return Grey(0.82f + (Hash(49, bx, by) - 0.5f) * 0.26f + seam);
    }

    /// <summary>Grass blades: tapering strokes rising from the bottom edge. Elsewhere the tile is see-through.</summary>
    private static (float, float, float, float) Blades(int x, int y)
    {
        // Seven blades at fixed columns, each its own height and lean; the wrap keeps edges tidy.
        for (int i = 0; i < 7; i++)
        {
            int baseX = 1 + i * 2 + (i % 2);
            float height = 7 + Hash(50, i, 0) * 8f;
            float lean = (Hash(51, i, 0) - 0.5f) * 3f;
            int rise = TileSize - 1 - y;
            if (rise < 0 || rise > height) continue;
            float cx = baseX + lean * rise / height;
            float halfWidth = 1.0f * (1f - rise / (height + 1f)) + 0.25f;
            if (MathF.Abs(x - cx) <= halfWidth)
                return Tint(0.55f + 0.45f * rise / height, 0.95f, 1f, 0.9f);
        }
        return (0, 0, 0, 0);
    }

    /// <summary>A stalk with a round bloom on top. The stalk is green already so the bloom takes the block colour.</summary>
    private static (float, float, float, float) Bloom(int x, int y)
    {
        // Stalk.
        if (x is 7 or 8 && y >= 7) return (0.35f, 0.75f, 0.3f, 1f);
        // Two leaves.
        if (y is 11 or 12 && (x is 5 or 6 || x is 9 or 10)) return (0.35f, 0.75f, 0.3f, 1f);
        // Bloom: a ring of petals around a pale heart.
        float dx = x - 7.5f, dy = y - 4.5f;
        float d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < 1.6f) return (1f, 0.95f, 0.6f, 1f);
        if (d < 3.6f) return Grey(0.9f + 0.1f * MathF.Sin(MathF.Atan2(dy, dx) * 5f));
        return (0, 0, 0, 0);
    }
}
