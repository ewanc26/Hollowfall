using VoxelGame.Core.Terrain;

namespace VoxelGame.Tests;

/// <summary>
/// Locks the exact world produced for known seeds. A failure here means generated
/// worlds changed: if that is intended, update the values and note it in the
/// changelog as a breaking change.
/// </summary>
public class GoldenTests
{
    [Theory]
    [InlineData(0L, 0, 0, 9171776403031787658UL)]
    [InlineData(12345L, 3, -7, 881491188614348562UL)]
    [InlineData(777L, -10, 20, 13216853530164507853UL)]
    [InlineData(-5L, 40, 40, 8595624587510398894UL)]
    public void ChunkChecksumsAreStable(long seed, int cx, int cz, ulong expected)
    {
        var actual = TestHelpers.Checksum(new TerrainGenerator(seed).GenerateChunk(cx, cz));
        Assert.Equal(expected, actual);
    }
}
