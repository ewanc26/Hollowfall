namespace Hollowfall.Core.Textures;

// The tiles beyond the first set: the dungeon's three themes, their decals, and the natural surface variants.
// Every one is a pattern of light and shade (with a hint of hue) that the block's palette colour tints, like the rest.
public static partial class BlockTextures
{
    private readonly record struct Rgb(float R, float G, float B)
    {
        public Rgb Scale(float f) => new(R * f, G * f, B * f);
        public Rgb Mul(float r, float g, float b) => new(R * r, G * g, B * b);
        public static Rgb Mix(Rgb a, Rgb b, float t) => new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    }

    private static (float, float, float, float) Out(Rgb c) => (c.R, c.G, c.B, 1f);

    private static Rgb Gray(float v) => new(v, v, v);

    private static (float, float, float, float) PaintVariant(Tile t, int x, int y)
    {
        switch (t)
        {
            // ---- the surface ----
            case Tile.MossyStone:
                {
                    var stone = Gray(0.84f + Tone(1, x, y, 4, 0.10f) + Tone(2, x, y, 2, 0.06f) - (Speck(3, x, y, 0.05f) ? 0.14f : 0f));
                    float m = MossField(60, x, y);
                    return Out(Rgb.Mix(stone, MossColour(x, y, 0.85f), Soft(m, 0.52f)));
                }
            case Tile.PackedSnow:
                {
                    // Hard-pressed layers, bluish in the creases, with the odd glint of ice.
                    float layer = y % 8 == 5 ? -0.07f : y % 8 == 0 ? 0.02f : 0f;
                    float glint = Speck(61, x, y, 0.05f) ? 0.04f : 0f;
                    return Out(Gray(0.92f + layer + glint + Tone(62, x, y, 4, 0.03f)).Mul(0.92f, 0.96f, 1f));
                }
            case Tile.DryDirt:
                {
                    // Sun-baked earth: broad plates parted by thin cracks.
                    const int cell = 8, n = TileSize / cell;
                    float best = float.MaxValue, second = float.MaxValue;
                    int bx = 0, by = 0;
                    for (int cy = -1; cy <= n; cy++)
                        for (int cx = -1; cx <= n; cx++)
                        {
                            int wx = ((cx % n) + n) % n, wy = ((cy % n) + n) % n;
                            float px = cx * cell + 1f + Hash(93, wx, wy) * (cell - 2);
                            float py = cy * cell + 1f + Hash(94, wx, wy) * (cell - 2);
                            float d = (px - x) * (px - x) + (py - y) * (py - y);
                            if (d < best) { second = best; best = d; bx = wx; by = wy; }
                            else if (d < second) second = d;
                        }
                    bool crack = MathF.Sqrt(second) - MathF.Sqrt(best) < 1.1f;
                    float v = 0.90f + (Hash(65, bx, by) - 0.5f) * 0.10f + Tone(66, x, y, 2, 0.04f) - (crack ? 0.34f : 0f);
                    return Out(Gray(v).Mul(1f, 0.93f, 0.8f));
                }

            // ---- floor 0: plain brick with a little history ----
            case Tile.BrickCracked:
                {
                    var c = BrickColour(x, y, DungeonTheme.Plain);
                    return Out(Crack(c, x, y, 70, 3, 14, 6 + (int)(Hash(70, 0, 0) * 4), 0.4f));
                }
            case Tile.BrickWeb:
                return Out(Web(BrickColour(x, y, DungeonTheme.Plain), x, y));
            case Tile.FloorCracked:
                return Out(Crack(FloorColour(x, y, DungeonTheme.Plain), x, y, 71, 0, 15, 5, 0.45f));
            case Tile.FloorBones:
                return Out(Bones(FloorColour(x, y, DungeonTheme.Plain), x, y));

            // ---- floor 1: damp ----
            case Tile.MossBrick:
                return Out(Mossy(BrickColour(x, y, DungeonTheme.Mossy), x, y, 72, 0.74f));
            case Tile.MossBrickHeavy:
                return Out(Mossy(BrickColour(x, y, DungeonTheme.Mossy), x, y, 73, 0.46f));
            case Tile.MossBrickVine:
                {
                    var c = Mossy(BrickColour(x, y, DungeonTheme.Mossy), x, y, 72, 0.74f);
                    // Strands hang from the top edge, each its own length.
                    for (int i = 0; i < 5; i++)
                    {
                        int vx = 1 + i * 3 + (int)(Hash(74, i, 0) * 2f);
                        int len = 5 + (int)(Hash(75, i, 0) * 8f);
                        if (x == vx && y < len) return Out(MossColour(x, y, 0.7f + 0.3f * y / len));
                        if (x == vx + 1 && y < 2) return Out(MossColour(x, y, 0.9f));
                    }
                    return Out(c);
                }
            case Tile.MossFloor:
                return Out(Mossy(FloorColour(x, y, DungeonTheme.Mossy), x, y, 76, 0.68f));
            case Tile.MossFloorPuddle:
                return Out(Puddle(Mossy(FloorColour(x, y, DungeonTheme.Mossy), x, y, 76, 0.68f), x, y));
            case Tile.MossPillar:
                {
                    // Moss climbs the shaft from its foot.
                    var c = Gray(PillarTone(x, y)).Mul(0.93f, 1f, 0.88f);
                    float climb = (y / 15f) * 0.35f;
                    return Out(Rgb.Mix(c, MossColour(x, y, 0.9f), Soft(MossField(77, x, y) + climb, 0.78f)));
                }
            case Tile.MossStairs:
                {
                    var c = Gray(StairsTone(x, y, out bool edge)).Mul(0.93f, 1f, 0.88f);
                    return Out(Rgb.Mix(c, MossColour(x, y, 0.85f), edge ? 0f : Soft(MossField(78, x, y), 0.55f)));
                }

            // ---- floor 2 and deeper: ash and embers ----
            case Tile.AshBrick:
                return Out(Ashen(BrickColour(x, y, DungeonTheme.Ashen), x, y));
            case Tile.AshBrickCracked:
                return Out(Crack(Ashen(BrickColour(x, y, DungeonTheme.Ashen), x, y), x, y, 79, 1, 14, 7, 0f, ember: true));
            case Tile.AshBrickScorch:
                return Out(Scorch(Ashen(BrickColour(x, y, DungeonTheme.Ashen), x, y), x, y));
            case Tile.AshFloor:
                return Out(Ashen(FloorColour(x, y, DungeonTheme.Ashen), x, y));
            case Tile.AshFloorBones:
                return Out(Bones(Ashen(FloorColour(x, y, DungeonTheme.Ashen), x, y), x, y));
            case Tile.AshFloorEmbers:
                {
                    var c = Ashen(FloorColour(x, y, DungeonTheme.Ashen), x, y);
                    // Clumps of glowing coals scattered in the grey.
                    if (Hash(80, x / 2, y / 2) < 0.11f && Hash(81, x, y) < 0.72f)
                        return Out(new Rgb(1f, 0.42f + 0.25f * Hash(82, x, y), 0.1f));
                    return Out(c);
                }
            case Tile.AshPillar:
                {
                    var c = Ashen(Gray(PillarTone(x, y)), x, y);
                    bool vein = x == 6 + (int)(Hash(83, y / 3, 0) * 2f) && y is > 3 and < 13;
                    return Out(vein ? new Rgb(1f, 0.45f, 0.12f) : Scorch(c, x, y));
                }
            case Tile.AshStairs:
                {
                    var c = Ashen(Gray(StairsTone(x, y, out bool edge)), x, y);
                    return Out(edge && Hash(84, x, y / 4) < 0.28f ? new Rgb(1f, 0.46f, 0.12f) : c);
                }

            // ---- gap pits ----
            case Tile.Spikes:
                {
                    // A 4 by 4 field of iron spikes seen from above: each a little pyramid with a bright tip, lit from the top left.
                    int cx = x % 4, cy = y % 4;
                    bool tip = cx is 1 or 2 && cy is 1 or 2;
                    float v = tip ? 0.98f : cx == 0 || cy == 0 ? 0.42f : 0.68f;
                    return Out(Gray(v + Tone(120, x, y, 2, 0.03f)).Mul(0.95f, 0.95f, 1f));
                }

            // ---- village cottages ----
            case Tile.Plank:
                {
                    // Four boards a tile tall, each a slightly different tone, with a dark seam and a few knots.
                    int board = y / 4;
                    bool seam = y % 4 == 0;
                    float v = 0.86f + (Hash(121, board, 0) - 0.5f) * 0.12f + Tone(122, x, y, 8, 0.05f) - (seam ? 0.22f : 0f);
                    return Tint(v - (Speck(123, x, y, 0.03f) ? 0.14f : 0f), 1f, 0.92f, 0.78f);
                }

            case Tile.Thatch:
                {
                    // Loose straw: diagonal strands in light and shade, with the odd dark gap between them.
                    float strand = (x + y) % 3 == 0 ? 0.08f : (x - y + 16) % 5 == 0 ? -0.1f : 0f;
                    float v = 0.82f + strand + Tone(124, x, y, 3, 0.07f) - (Speck(125, x, y, 0.06f) ? 0.2f : 0f);
                    return Tint(v, 1f, 0.95f, 0.72f);
                }

            // ---- rift plates and deep obsidian ----
            case Tile.RiftPlate:
                {
                    // A grey-gold plate cut with a ring and a cross, so a portal's place on it reads at a glance.
                    bool ring = Math.Abs(Math.Max(Math.Abs(x - 7.5), Math.Abs(y - 7.5)) - 5) < 0.6;
                    bool cross = (Math.Abs(x - 7.5) < 0.6 || Math.Abs(y - 7.5) < 0.6) && Math.Max(Math.Abs(x - 7.5), Math.Abs(y - 7.5)) < 4;
                    float v = 0.62f + Tone(126, x, y, 4, 0.05f) + (ring || cross ? 0.22f : 0f);
                    return Tint(v, 1f, 0.92f, 0.66f);
                }

            case Tile.Obsidian:
                {
                    // Dark glass with a few conchoidal sheens and the odd bright fleck.
                    float sheen = Smooth(127, x, y, 3) > 0.7f ? 0.1f : 0f;
                    float v = 0.62f + sheen + Tone(128, x, y, 2, 0.04f) + (Speck(129, x, y, 0.02f) ? 0.25f : 0f);
                    return Tint(v, 0.92f, 0.9f, 1f);
                }
        }
        return Grey(0.8f);
    }

    // ---- bases per theme ---------------------------------------------------------------------

    private static Rgb BrickColour(int x, int y, DungeonTheme theme)
    {
        float v = BrickTone(x, y, out _);
        return theme switch
        {
            DungeonTheme.Mossy => Gray(v).Mul(0.94f, 1f, 0.9f),
            DungeonTheme.Ashen => Gray(v * 0.93f).Mul(1f, 0.95f, 0.92f),
            _ => Gray(v),
        };
    }

    private static Rgb FloorColour(int x, int y, DungeonTheme theme)
    {
        float v = FloorTone(x, y, out _);
        return theme switch
        {
            DungeonTheme.Mossy => Gray(v).Mul(0.94f, 1f, 0.92f),
            DungeonTheme.Ashen => Gray(v * 0.93f).Mul(1f, 0.95f, 0.92f),
            _ => Gray(v),
        };
    }

    // ---- decals ------------------------------------------------------------------------------

    /// <summary>Blotchy 0..1 field that wraps at the tile edge.</summary>
    private static float MossField(int seed, int x, int y) => Smooth(seed, x, y, 4) * 0.72f + Smooth(seed + 1, x, y, 2) * 0.28f;

    /// <summary>0 below the threshold, rising to 1 a little above it, so patches have a soft edge.</summary>
    private static float Soft(float field, float threshold) => Math.Clamp((field - threshold) / 0.07f, 0f, 1f);

    private static Rgb MossColour(int x, int y, float lightness) =>
        new Rgb(0.46f, 0.84f, 0.34f).Scale(lightness * (0.78f + 0.22f * Hash(85, x, y)));

    private static Rgb Mossy(Rgb under, int x, int y, int seed, float threshold) =>
        Rgb.Mix(under, MossColour(x, y, 0.82f), Soft(MossField(seed, x, y), threshold));

    /// <summary>A jagged line that wanders down the tile between two rows, drawn dark (or glowing).</summary>
    private static Rgb Crack(Rgb under, int x, int y, int seed, int y0, int y1, int startX, float darken, bool ember = false)
    {
        int cx = startX;
        bool hit = false, near = false;
        for (int yy = y0; yy <= y1; yy++)
        {
            float r = Hash(seed, yy, 1);
            if (r < 0.3f) cx--;
            else if (r > 0.7f) cx++;
            cx = Math.Clamp(cx, 1, 14);
            if (yy == y && cx == x) hit = true;
            if (yy == y && Math.Abs(cx - x) == 1) near = true;
        }
        if (ember)
        {
            if (hit) return new Rgb(1f, 0.52f, 0.14f);
            return near ? Rgb.Mix(under, new Rgb(0.95f, 0.45f, 0.2f), 0.55f) : under;
        }
        return hit ? under.Scale(darken) : under;
    }

    /// <summary>A cobweb strung across the top-left corner: spokes and three arcs.</summary>
    private static Rgb Web(Rgb under, int x, int y)
    {
        float d = MathF.Sqrt(x * x + y * y);
        if (d > 14f) return under;
        bool spoke = x == 0 || y == 0 || x == y || (x == 2 * y) || (y == 2 * x) || (x + 1 == 2 * y) || (y + 1 == 2 * x);
        bool arc = MathF.Abs(d - 4.2f) < 0.55f || MathF.Abs(d - 8.2f) < 0.55f || MathF.Abs(d - 12.2f) < 0.55f;
        if (!(spoke || arc)) return under;
        return Rgb.Mix(under, new Rgb(0.97f, 0.97f, 1f), 0.8f);
    }

    /// <summary>A skull above a long bone, pale on the flagstones.</summary>
    private static Rgb Bones(Rgb under, int x, int y)
    {
        string[] art =
        [
            "................",
            "................",
            "................",
            "................",
            "................",
            "......####......",
            ".....######.....",
            ".....#oo#o#.....",
            ".....######.....",
            "......#.##......",
            "......####......",
            "................",
            "..#..........#..",
            "..############..",
            "..#..........#..",
            "................",
        ];
        char c = art[y][x];
        if (c == '#') return new Rgb(0.95f, 0.92f, 0.8f).Scale(0.9f + 0.1f * Hash(86, x, y));
        if (c == 'o') return new Rgb(0.12f, 0.1f, 0.1f);
        return under;
    }

    /// <summary>A shallow pool in the middle of the flagstones, with a pale glint.</summary>
    private static Rgb Puddle(Rgb under, int x, int y)
    {
        float dx = (x - 7.5f) / 6.2f, dy = (y - 8f) / 4.4f;
        float d = dx * dx + dy * dy + (Smooth(87, x, y, 4) - 0.5f) * 0.7f;
        if (d > 1f) return under;
        if (d > 0.78f) return new Rgb(0.42f, 0.6f, 0.68f);
        bool glint = (x == 5 && y == 7) || (x == 6 && y == 7) || (x == 9 && y == 10);
        return glint ? new Rgb(0.85f, 0.95f, 1f) : new Rgb(0.62f, 0.86f, 1f).Scale(0.92f + 0.12f * Hash(88, x, y));
    }

    /// <summary>Grey ash: the stone dulled and speckled with pale flakes.</summary>
    private static Rgb Ashen(Rgb under, int x, int y)
    {
        var c = under.Mul(0.98f, 0.96f, 0.94f);
        if (Speck(89, x, y, 0.07f)) return c.Scale(1.14f);
        if (Speck(90, x, y, 0.05f)) return c.Scale(0.8f);
        return c;
    }

    /// <summary>A soot-black blotch, thickest toward the foot of the tile.</summary>
    private static Rgb Scorch(Rgb under, int x, int y)
    {
        float s = Smooth(91, x, y, 4) + y / 15f * 0.5f;
        float amount = Math.Clamp((s - 0.92f) / 0.25f, 0f, 1f);
        return Rgb.Mix(under, under.Scale(0.4f), amount * 0.9f);
    }
}
