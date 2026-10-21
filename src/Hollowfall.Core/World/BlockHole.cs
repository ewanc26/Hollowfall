namespace Hollowfall.Core.World;

/// <summary>
/// The dug-out 10 cm cubes of one 1 m block. Each block is a 10 x 10 x 10 grid of cubes; a set bit means the cube is gone.
/// A block with every cube gone is Air, so a hole never outlives its last cube.
/// </summary>
public sealed class BlockHole
{
    public const int Side = 10;
    public const int Cubes = Side * Side * Side;

    private readonly ulong[] _dug = new ulong[(Cubes + 63) / 64];

    public int Count { get; private set; }

    public bool IsDug(int sx, int sy, int sz)
    {
        int i = Index(sx, sy, sz);
        return (_dug[i >> 6] & (1UL << (i & 63))) != 0;
    }

    internal bool Dig(int sx, int sy, int sz)
    {
        if (IsDug(sx, sy, sz)) return false;
        int i = Index(sx, sy, sz);
        _dug[i >> 6] |= 1UL << (i & 63);
        Count++;
        return true;
    }

    /// <summary>Whether a cube at these sub-block coordinates is solid, given the block's type and its hole (if any).</summary>
    public static bool IsSolid(BlockType block, BlockHole? hole, int sx, int sy, int sz) =>
        BlockInfo.Occludes(block) && (hole is null || !hole.IsDug(sx, sy, sz));

    private static int Index(int sx, int sy, int sz) => (sz * Side + sy) * Side + sx;
}
