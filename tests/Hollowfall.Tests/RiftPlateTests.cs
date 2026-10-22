using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class RiftPlateTests
{
    [Theory]
    [InlineData(BlockType.Stone)]
    [InlineData(BlockType.StoneBrick)]
    [InlineData(BlockType.BrickFloor)]
    [InlineData(BlockType.RiftPlate)]
    public void MostSurfacesAcceptAPortalOnTheShallowFloors(BlockType b) =>
        Assert.True(BlockInfo.Portalable(b));

    [Theory]
    [InlineData(BlockType.Obsidian)]
    [InlineData(BlockType.Bedrock)]
    [InlineData(BlockType.Air)]
    public void ObsidianAndTheRestRefuseAPortal(BlockType b) =>
        Assert.False(BlockInfo.Portalable(b));

    [Theory]
    [InlineData(BlockType.Stone)]
    [InlineData(BlockType.StoneBrick)]
    [InlineData(BlockType.BrickFloor)]
    [InlineData(BlockType.Obsidian)]
    public void DeepFloorsTakeAPortalOnlyOnAPlate(BlockType b) =>
        Assert.False(BlockInfo.Portalable(b, platesOnly: true));

    [Fact]
    public void ADeepPlateTakesAPortal() =>
        Assert.True(BlockInfo.Portalable(BlockType.RiftPlate, platesOnly: true));

    [Fact]
    public void EveryDeepArenaHasAPlatePairFacingAcrossTheRoom()
    {
        int arenas = 0;
        foreach (var site in DeepSites())
            for (int k = DungeonSite.RiftPlateFloor; k < site.Floors.Count; k++)
                foreach (var room in site.Floors[k].Rooms)
                {
                    var b = room.Bounds;
                    if (b.W < 5 || b.H < 5) continue;
                    arenas++;
                    var edge = Enumerable.Range(b.X - 1, b.W + 2).SelectMany(x => new[] { (x, b.Z - 1), (x, b.Bottom) })
                        .Concat(Enumerable.Range(b.Z, b.H).SelectMany(z => new[] { (b.X - 1, z), (b.Right, z) }))
                        .Where(c => Plated(site, k, c.Item1, c.Item2)).ToList();
                    Assert.True(edge.Count >= 2, $"floor {k}: the room at {room.CenterX},{room.CenterZ} has {edge.Count} plates on its edge");
                }
        Assert.True(arenas > 50, $"only {arenas} arenas");
    }

    [Fact]
    public void EveryGapHallwayHasPlatesBesideIt()
    {
        foreach (var site in DeepSites())
            for (int k = DungeonSite.RiftPlateFloor; k < site.Floors.Count; k++)
                foreach (var gap in site.Floors[k].Gaps)
                    foreach (var (x, z) in gap.HallFrom.Concat(gap.HallTo))
                    {
                        var lat = (X: -gap.DirZ, Z: gap.DirX);
                        int row = site.Floors[k].HeightAt(x, z) + 2;
                        Assert.True(Plated(site, k, x + 2 * lat.X, z + 2 * lat.Z, row) && Plated(site, k, x - 2 * lat.X, z - 2 * lat.Z, row),
                            $"floor {k}: no plates beside the hallway at {x},{z}");
                    }
    }

    private static IEnumerable<DungeonSite> DeepSites()
    {
        for (long seed = 1; seed <= 30; seed++)
        {
            var g = new TerrainGenerator(seed);
            for (int rx = -2; rx <= 1; rx++)
                for (int rz = -2; rz <= 1; rz++)
                    if (g.Dungeons!.GetSite(rx, rz) is { } s)
                        yield return s;
        }
    }

    private static bool Plated(DungeonSite site, int floor, int x, int z, int row = 2) =>
        site.BlockAt(site.OriginX + x, DungeonSite.FloorY(floor) + row, site.OriginZ + z) == BlockType.RiftPlate;

    [Fact]
    public void PlatesAndObsidianAreNotBrokenByAMortar()
    {
        Assert.False(BlockInfo.Breakable(BlockType.RiftPlate));
        Assert.False(BlockInfo.Breakable(BlockType.Obsidian));
    }
}
