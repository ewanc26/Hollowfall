using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Tests;

/// <summary>
/// Locks the exact floor layouts for known seeds. See <see cref="GoldenTests"/> for how to update. These were updated on purpose when rooms
/// became joined by a mix of corridors and gaps: the checksum now covers floor heights and link kinds, and gap hallways and pits change the cells.
/// </summary>
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
        // The mixed links: how high each cell stands and how each link is joined.
        foreach (var height in l.Heights)
        {
            h ^= height;
            h *= 1099511628211UL;
        }
        foreach (var link in l.Links)
        {
            h ^= (ulong)((int)link.Kind * 7 + link.A * 131 + link.B);
            h *= 1099511628211UL;
        }
        return h;
    }

    [Theory]
    [InlineData(1L, 0, 40, 40, false, 6723512079382384547UL)]
    [InlineData(2L, 1, 20, 70, false, 16860518095922408380UL)]
    [InlineData(123456789L, 3, 60, 30, true, 3286166934086469416UL)]
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
        Assert.Equal(2952558081228442399UL, TestHelpers.Checksum(chunk));
    }
}
