namespace Hollowfall.Core.World;

/// <summary>A fixed-size column of voxels. Local coords: x,z in [0,Size), y in [0,Height).</summary>
public sealed class Chunk
{
    public const int Size = 16;
    public const int Height = 128;

    public int ChunkX { get; }
    public int ChunkZ { get; }
    private readonly BlockType[] _blocks = new BlockType[Size * Height * Size];
    private readonly Dictionary<int, BlockHole> _holes = [];

    public Chunk(int chunkX, int chunkZ)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
    }

    /// <summary>True once something has changed this chunk after generation; edited chunks are never silently dropped from the cache.</summary>
    public bool Edited { get; internal set; }

    public int WorldMinX => ChunkX * Size;
    public int WorldMinZ => ChunkZ * Size;

    private static int Index(int x, int y, int z) => (y * Size + z) * Size + x;

    public static bool InBounds(int x, int y, int z) =>
        (uint)x < Size && (uint)y < Height && (uint)z < Size;

    public BlockType this[int x, int y, int z]
    {
        get => InBounds(x, y, z) ? _blocks[Index(x, y, z)] : BlockType.Air;
        set
        {
            if (!InBounds(x, y, z)) return;
            _blocks[Index(x, y, z)] = value;
            _holes.Remove(Index(x, y, z));
        }
    }

    /// <summary>The dug cubes of a block, or null while the block is whole.</summary>
    public BlockHole? HoleAt(int x, int y, int z) => _holes.GetValueOrDefault(Index(x, y, z));

    /// <summary>Digs one 10 cm cube out of a solid block. A block whose last cube goes becomes Air.</summary>
    internal bool DigCube(int x, int y, int z, int sx, int sy, int sz)
    {
        if (!BlockInfo.Occludes(this[x, y, z])) return false;
        int i = Index(x, y, z);
        if (!_holes.TryGetValue(i, out var hole)) _holes[i] = hole = new BlockHole();
        if (!hole.Dig(sx, sy, sz)) return false;
        Edited = true;
        if (hole.Count == BlockHole.Cubes)
        {
            _holes.Remove(i);
            _blocks[i] = BlockType.Air;
        }
        return true;
    }

    /// <summary>Highest non-air voxel in a column, or -1.</summary>
    public int TopY(int x, int z)
    {
        for (int y = Height - 1; y >= 0; y--)
            if (_blocks[Index(x, y, z)] != BlockType.Air) return y;
        return -1;
    }

    public ReadOnlySpan<BlockType> RawBlocks => _blocks;
}
