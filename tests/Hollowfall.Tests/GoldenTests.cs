using Hollowfall.Core.Terrain;

namespace Hollowfall.Tests;

/// <summary>
/// Locks the exact world produced for known seeds. A failure here means generated
/// worlds changed: if that is intended, update the values and note it in the
/// changelog as a breaking change.
/// </summary>
public class GoldenTests
{
    [Theory]
    [InlineData(0L, 0, 0, 15303666179145559636UL)]
    [InlineData(12345L, 3, -7, 3015010958834946408UL)]
    [InlineData(777L, -10, 20, 3320557014462838939UL)]
    [InlineData(-5L, 40, 40, 15200881904086125383UL)]
    public void ChunkChecksumsAreStable(long seed, int cx, int cz, ulong expected)
    {
        var actual = TestHelpers.Checksum(new TerrainGenerator(seed).GenerateChunk(cx, cz));
        Assert.Equal(expected, actual);
    }
}
