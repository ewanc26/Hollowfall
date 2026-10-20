using VoxelGame.Core.World;

namespace VoxelGame.Tests;

internal static class TestHelpers
{
    /// <summary>FNV-1a over a chunk's blocks; stable fingerprint for golden tests.</summary>
    public static ulong Checksum(Chunk chunk)
    {
        ulong h = 1469598103934665603UL;
        foreach (var b in chunk.RawBlocks)
        {
            h ^= (byte)b;
            h *= 1099511628211UL;
        }
        return h;
    }
}
