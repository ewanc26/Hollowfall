using System.Text;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

if (args is ["--po", var poPath])
{
    File.WriteAllText(poPath, Hollowfall.Core.Localization.PoFiles.Export(Hollowfall.Core.Localization.LocTable.All, "gd"), new UTF8Encoding(false));
    return;
}

// Usage: dotnet run --project src/Hollowfall.Cli -- [seed] [chunksRadius]
string seedText = args.Length > 0 ? args[0] : "voxel";
int radius = args.Length > 1 && int.TryParse(args[1], out var r) ? Math.Clamp(r, 1, 12) : 5;

long seed = TerrainGenerator.SeedFromString(seedText);
var gen = new TerrainGenerator(seed);

if (args.Contains("--dungeon-stats"))
{
    // How common dungeons are and how deep their entrance shafts run, over a 16x16 block of regions.
    var surfaces = new List<int>();
    int regions = 0;
    for (int rx = -8; rx < 8; rx++)
        for (int rz = -8; rz < 8; rz++, regions++)
            if (gen.Dungeons!.GetSite(rx, rz) is { } s)
                surfaces.Add(s.SurfaceY);
    surfaces.Sort();
    Console.WriteLine($"dungeons in {regions} regions: {surfaces.Count} ({100.0 * surfaces.Count / regions:F0}%)");
    if (surfaces.Count > 0)
        Console.WriteLine($"entrance surface y: min {surfaces[0]}, median {surfaces[surfaces.Count / 2]}, p90 {surfaces[surfaces.Count * 9 / 10]}, max {surfaces[^1]}");
    return;
}

if (args.Contains("--dungeon"))
{
    PrintDungeon(gen, args.Contains("--spawns"));
    return;
}

if (args.Contains("--biomes"))
{
    // Coarse ASCII biome map: ~ ocean, ' beach, = river, " plains, T forest, : desert, ^ mountains.
    for (int z = -400; z < 400; z += 10)
    {
        var line = new StringBuilder();
        for (int x = -800; x < 800; x += 8)
            line.Append(gen.SampleColumn(x, z).Biome switch
            {
                Biome.Ocean => '~',
                Biome.Beach => '\'',
                Biome.River => '=',
                Biome.Plains => '"',
                Biome.Forest => 'T',
                Biome.Desert => ':',
                _ => '^',
            });
        Console.WriteLine(line);
    }
    return;
}

if (args.Contains("--stats"))
{
    // Biome / height distribution over a wide area, without generating voxels.
    var biomes = new Dictionary<Biome, int>();
    int min = int.MaxValue, max = int.MinValue, n = 0;
    for (int x = -2000; x < 2000; x += 8)
        for (int z = -2000; z < 2000; z += 8)
        {
            var c = gen.SampleColumn(x, z);
            biomes[c.Biome] = biomes.GetValueOrDefault(c.Biome) + 1;
            min = Math.Min(min, c.Height); max = Math.Max(max, c.Height); n++;
        }
    Console.WriteLine($"height {min}..{max}");
    foreach (var (b, k) in biomes.OrderByDescending(kv => kv.Value))
        Console.WriteLine($"  {b,-10} {100.0 * k / n,5:F1}%");
    return;
}
Console.WriteLine($"Seed \"{seedText}\" => {seed}");

var sw = System.Diagnostics.Stopwatch.StartNew();
int span = radius * 2 + 1;
var chunks = new Chunk[span, span];
for (int cx = 0; cx < span; cx++)
    for (int cz = 0; cz < span; cz++)
        chunks[cx, cz] = gen.GenerateChunk(cx - radius, cz - radius);
Console.WriteLine($"Generated {span * span} chunks in {sw.ElapsedMilliseconds} ms\n");

// Top-down map: colour = surface block, brightness = height.
var sb = new StringBuilder();
for (int wz = 0; wz < span * Chunk.Size; wz += 2)
{
    for (int wx = 0; wx < span * Chunk.Size; wx++)
    {
        var c = chunks[wx / Chunk.Size, wz / Chunk.Size];
        int lx = wx % Chunk.Size, lz = wz % Chunk.Size;
        int y = c.TopY(lx, lz);
        var b = c[lx, y, lz];
        var (cr, cg, cb) = Color(b);
        double shade = 0.65 + 0.35 * Math.Clamp((y - 30) / 70.0, 0, 1);
        sb.Append($"\e[48;2;{(int)(cr * shade)};{(int)(cg * shade)};{(int)(cb * shade)}m ");
    }
    sb.Append("\e[0m\n");
}
Console.Write(sb);

// Block census.
var counts = new Dictionary<BlockType, long>();
foreach (var c in chunks)
    foreach (var b in c.RawBlocks)
        counts[b] = counts.GetValueOrDefault(b) + 1;
Console.WriteLine("\nBlock counts:");
foreach (var (b, n) in counts.OrderByDescending(kv => kv.Value))
    Console.WriteLine($"  {b,-10} {n,10:N0}");

static (int, int, int) Color(BlockType b) => b switch
{
    BlockType.Grass => (88, 160, 62),
    BlockType.Dirt => (134, 96, 67),
    BlockType.Stone => (125, 125, 130),
    BlockType.Sand => (226, 210, 150),
    BlockType.Sandstone => (205, 190, 130),
    BlockType.Snow => (245, 248, 250),
    BlockType.Water => (52, 100, 200),
    BlockType.Gravel => (140, 135, 130),
    BlockType.Log => (100, 72, 40),
    BlockType.Plank => (168, 118, 72),
    BlockType.Thatch => (200, 168, 92),
    BlockType.RiftPlate => (184, 164, 118),
    BlockType.Obsidian => (92, 84, 108),
    BlockType.Leaves => (40, 110, 40),
    BlockType.Cactus => (60, 130, 70),
    BlockType.CoalOre => (60, 60, 65),
    BlockType.IronOre => (190, 140, 110),
    BlockType.GoldOre => (240, 200, 60),
    BlockType.TallGrass => (110, 180, 70),
    BlockType.Flower => (230, 90, 120),
    BlockType.Bedrock => (40, 40, 40),
    _ => (0, 0, 0),
};

static void PrintDungeon(TerrainGenerator gen, bool showSpawns)
{
    // Nearest dungeon to the world origin, searching outward through nearby regions.
    DungeonSite? site = null;
    double best = double.MaxValue;
    var sw = System.Diagnostics.Stopwatch.StartNew();
    for (int rx = -4; rx <= 3; rx++)
        for (int rz = -4; rz <= 3; rz++)
            if (gen.Dungeons!.GetSite(rx, rz) is { } s)
            {
                double d = (double)s.EntranceX * s.EntranceX + (double)s.EntranceZ * s.EntranceZ;
                if (d < best) { best = d; site = s; }
            }

    if (site == null)
    {
        Console.WriteLine("No dungeon within 4 regions of the origin for this seed.");
        return;
    }
    Console.WriteLine($"Searched 64 regions and built their dungeons in {sw.ElapsedMilliseconds} ms");
    Console.WriteLine($"Dungeon in region ({site.RegionX},{site.RegionZ}): entrance at x={site.EntranceX} z={site.EntranceZ}, surface y={site.SurfaceY}, {site.FloorCount} floors\n");

    foreach (var floor in site.Floors)
    {
        Console.WriteLine($"Floor {floor.Floor} (y={DungeonSite.FloorY(floor.Floor)}): {floor.Rooms.Count} rooms, {floor.Links.Count} links ({floor.Links.Count(l => l.Secret)} secret)");
        var marks = new Dictionary<(int, int), char>();
        foreach (var room in floor.Rooms)
            marks[(room.CenterX, room.CenterZ)] = room.Role switch
            {
                RoomRole.Entrance => 'e',
                RoomRole.Exit => '>',
                RoomRole.Boss => 'B',
                RoomRole.Treasure => '$',
                RoomRole.Trap => '^',
                RoomRole.Safe => 'S',
                _ => room.Shape switch { RoomShape.Rect => 'r', RoomShape.L => 'l', RoomShape.Circle => 'c', _ => 'v' },
            };
        marks[(floor.EntranceX, floor.EntranceZ)] = '<';
        if (showSpawns)
            foreach (var sp in site.Spawns.Where(sp => sp.Floor == floor.Floor))
                marks[(sp.X - site.OriginX, sp.Z - site.OriginZ)] = sp.Kind switch
                {
                    SpawnKind.Monster => 'm',
                    SpawnKind.Boss => 'M',
                    SpawnKind.Loot => '*',
                    SpawnKind.Key => 'k',
                    SpawnKind.Creature => 'f',
                    _ => 't',
                };

        for (int z = 0; z < FloorLayout.Size; z += 2)
        {
            var line = new StringBuilder();
            for (int x = 0; x < FloorLayout.Size; x++)
            {
                if (marks.TryGetValue((x, z), out char m) || marks.TryGetValue((x, z + 1), out m)) { line.Append(m); continue; }
                line.Append(floor.CellAt(x, z) switch
                {
                    CellKind.Floor => '.',
                    CellKind.Door => '+',
                    CellKind.SecretDoor => 'S',
                    CellKind.Pillar => 'o',
                    _ => floor.IsWallCell(x, z) ? '#' : ' ',
                });
            }
            Console.WriteLine(line);
        }
        Console.WriteLine();
    }

    sw.Restart();
    int chunkCount = 0;
    for (int cx = site.OriginX >> 4; cx <= (site.OriginX + FloorLayout.Size - 1) >> 4; cx++)
        for (int cz = site.OriginZ >> 4; cz <= (site.OriginZ + FloorLayout.Size - 1) >> 4; cz++, chunkCount++)
            gen.GenerateChunk(cx, cz);
    Console.WriteLine($"Generated the {chunkCount} chunks under this dungeon (terrain + dungeon) in {sw.ElapsedMilliseconds} ms\n");

    var roles = site.Floors.SelectMany(f => f.Rooms).GroupBy(r => r.Role).OrderBy(g => g.Key);
    Console.WriteLine("Rooms by role: " + string.Join(", ", roles.Select(g => $"{g.Key}={g.Count()}")));
    Console.WriteLine("Spawns: " + string.Join(", ", site.Spawns.GroupBy(sp => sp.Kind).Select(g => $"{g.Key}={g.Count()}")));
    Console.WriteLine("Legend: # wall  . floor  + door  S secret door  o pillar  < entrance  > stairs down  B boss  $ treasure  ^ trap  S safe room  e entrance room centre  r/l/c/v rect/L/circle/cavern room (centre)"
    + (showSpawns ? "\nSpawns: m monster  M boss  * loot  k key  t trap  f creature (folk, wanderer, vermin)" : ""));
}
