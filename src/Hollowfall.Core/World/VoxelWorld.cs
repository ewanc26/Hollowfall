using System.Collections.Concurrent;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.Topology;

namespace Hollowfall.Core.World;

/// <summary>
/// A world as a chunk cache over the pure seeded generator. Edits are made in the cached chunks, so the
/// generator and its determinism guarantees are never touched. Reads are safe from many threads.
/// </summary>
public sealed class VoxelWorld
{
    private readonly ConcurrentDictionary<(int, int), Lazy<Chunk>> _chunks = new();

    public TerrainGenerator Generator { get; }
    public long Seed => Generator.Seed;

    public VoxelWorld(long seed) => Generator = new TerrainGenerator(seed);

    /// <summary>When set, the world is this glued room instead of generated terrain.</summary>
    public GluedBox? Glue { get; set; }

    public int CachedChunks => _chunks.Count;

    public Chunk GetChunk(int chunkX, int chunkZ) =>
        _chunks.GetOrAdd((chunkX, chunkZ), k => new Lazy<Chunk>(() => Generator.GenerateChunk(k.Item1, k.Item2))).Value;

    public bool HasChunk(int chunkX, int chunkZ) => _chunks.ContainsKey((chunkX, chunkZ));

    /// <summary>
    /// Forgets a chunk so it is regenerated on next use. Chunks that have been edited (a locked door, a placed block)
    /// are kept, because forgetting them would silently undo the edit; pass <paramref name="force"/> to drop them anyway.
    /// </summary>
    public bool Unload(int chunkX, int chunkZ, bool force = false)
    {
        if (!force && _chunks.TryGetValue((chunkX, chunkZ), out var lazy) && lazy.IsValueCreated && lazy.Value.Edited) return false;
        return _chunks.TryRemove((chunkX, chunkZ), out _);
    }

    public static bool InHeight(int y) => y >= 0 && y < Chunk.Height;

    /// <summary>Block at a world position; anything above or below the world is Air.</summary>
    public BlockType GetBlock(int x, int y, int z)
    {
        if (Glue is { } glue) return glue.BlockAt(x, y, z);
        return InHeight(y) ? GetChunk(x >> 4, z >> 4)[x & 15, y, z & 15] : BlockType.Air;
    }

    public BlockType SetBlock(int x, int y, int z, BlockType block)
    {
        if (!InHeight(y)) throw new ArgumentOutOfRangeException(nameof(y), $"y must be 0..{Chunk.Height - 1}");
        var chunk = GetChunk(x >> 4, z >> 4);
        var previous = chunk[x & 15, y, z & 15];
        if (previous != block)
        {
            chunk[x & 15, y, z & 15] = block;
            chunk.Edited = true;
        }
        return previous;
    }

    /// <summary>The dug cubes of a block, or null when it is whole (or outside the world).</summary>
    public BlockHole? HoleAt(int x, int y, int z) =>
        InHeight(y) ? GetChunk(x >> 4, z >> 4).HoleAt(x & 15, y, z & 15) : null;

    /// <summary>Digs one 10 cm cube out of the block at (x, y, z). Returns false if there was no solid cube there to dig.</summary>
    public bool DigCube(int x, int y, int z, int sx, int sy, int sz) =>
        InHeight(y) && GetChunk(x >> 4, z >> 4).DigCube(x & 15, y, z & 15, sx, sy, sz);

    /// <summary>Y of the highest non-air block in a column, or -1.</summary>
    public int TopY(int x, int z) => GetChunk(x >> 4, z >> 4).TopY(x & 15, z & 15);
}
