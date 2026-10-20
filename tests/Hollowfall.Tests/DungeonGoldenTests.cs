using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Tests;

/// <summary>Locks the exact floor layouts for known seeds. See <see cref="GoldenTests"/> for how to update.</summary>
public class DungeonGoldenTests
{
    private static ulong Checksum(FloorLayout l)
    {
        ulong h = 1469598103934665603UL;
        foreach (var c in l.Cells)
        {
            h ^= (byte)c;
            h *= 1099511628211UL;
        }
        foreach (var r in l.Rooms)
        {
            h ^= (ulong)((int)r.Role * 31 + (int)r.Shape);
            h *= 1099511628211UL;
        }
        return h;
    }

    [Theory]
    [InlineData(1L, 0, 40, 40, false, 4601016794899972738UL)]
    [InlineData(2L, 1, 20, 70, false, 6711522605328283063UL)]
    [InlineData(123456789L, 3, 60, 30, true, 10741888673474822932UL)]
    public void FloorChecksumsAreStable(long seed, int floor, int ex, int ez, bool last, ulong expected)
    {
        Assert.Equal(expected, Checksum(FloorLayout.Generate(seed, floor, ex, ez, last)));
    }

    [Fact]
    public void ChunkContainingADungeonEntranceIsStable()
    {
        var g = new TerrainGenerator(1);
        var site = g.Dungeons!.GetSite(0, 1)!;
        var chunk = g.GenerateChunk(site.EntranceX >> 4, site.EntranceZ >> 4);
        Assert.Equal(6850862617830623050UL, TestHelpers.Checksum(chunk));
    }
}
