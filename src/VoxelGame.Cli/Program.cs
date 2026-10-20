using System.Text;
using VoxelGame.Core.Terrain;
using VoxelGame.Core.World;

// Usage: dotnet run --project src/VoxelGame.Cli -- [seed] [chunksRadius]
string seedText = args.Length > 0 ? args[0] : "voxel";
int radius = args.Length > 1 && int.TryParse(args[1], out var r) ? Math.Clamp(r, 1, 12) : 5;

long seed = TerrainGenerator.SeedFromString(seedText);
var gen = new TerrainGenerator(seed);

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
    BlockType.Leaves => (40, 110, 40),
    BlockType.Cactus => (60, 130, 70),
    BlockType.Bedrock => (40, 40, 40),
    _ => (0, 0, 0),
};
