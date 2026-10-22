using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Physics;
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
                    var edge = TouchingWalls(site, k, room)
                        .Where(c => Plated(site, k, c.Item1, c.Item2)).ToList();
                    Assert.True(edge.Count >= 2, $"floor {k}: the room at {room.CenterX},{room.CenterZ} has {edge.Count} plates on its edge");
                }
        Assert.True(arenas > 50, $"only {arenas} arenas");
    }

    [Fact]
    public void EveryDeepArenaHasAPlateAPlayerCanStandBeside()
    {
        int arenas = 0;
        foreach (var site in DeepSites())
            for (int k = DungeonSite.RiftPlateFloor; k < site.Floors.Count; k++)
                foreach (var room in site.Floors[k].Rooms)
                {
                    var b = room.Bounds;
                    if (b.W < 5 || b.H < 5) continue;
                    arenas++;

                    int floorY = DungeonSite.FloorY(k) + 1;
                    BlockType BlockAt(int x, int y, int z) => site.BlockAt(x, y, z) ?? BlockType.Air;
                    bool InRoom(int x, int z) => x >= site.OriginX + b.X && x < site.OriginX + b.Right
                        && z >= site.OriginZ + b.Z && z < site.OriginZ + b.Bottom;
                    var start = Enumerable.Range(b.X, b.W).SelectMany(x => Enumerable.Range(b.Z, b.H).Select(z => (x, z)))
                        .OrderBy(c => Math.Abs(c.x - room.CenterX) + Math.Abs(c.z - room.CenterZ))
                        .Select(c => new Navigation.Cell(site.OriginX + c.x, floorY, site.OriginZ + c.z))
                        .First(c => Navigation.CanStand(BlockAt, c.X, c.Y, c.Z));

                    int onFoot = 0;
                    foreach (var (px, pz) in TouchingWalls(site, k, room))
                    {
                        if (!Plated(site, k, px, pz)) continue;
                        var (fx, fz) = FrontOf(room, (px, pz));
                        var front = Enumerable.Range(fx - 2, 5).SelectMany(cx => Enumerable.Range(fz - 2, 5).Select(cz => (cx, cz)))
                            .Where(c => InRoom(site.OriginX + c.cx, site.OriginZ + c.cz))
                            .OrderBy(c => Math.Abs(c.cx - fx) + Math.Abs(c.cz - fz))
                            .Select(c => (Navigation.Cell?)new Navigation.Cell(site.OriginX + c.cx, floorY, site.OriginZ + c.cz))
                            .FirstOrDefault(c => c is { } s && Navigation.CanStand(BlockAt, s.X, s.Y, s.Z));
                        if (front is { } f && Navigation.FindPath(BlockAt, start, f, InRoom) is not null) onFoot++;
                    }
                    Assert.True(onFoot >= 2, $"floor {k}: only {onFoot} plates of the room at {room.CenterX},{room.CenterZ} can be reached on foot");
                }
        Assert.True(arenas > 50, $"only {arenas} arenas");
    }

    [Fact]
    public void AnArenaWithRoomForItHasAPlateHighOnAWall()
    {
        int high = 0, arenas = 0;
        foreach (var site in DeepSites())
            for (int k = DungeonSite.RiftPlateFloor; k < site.Floors.Count; k++)
                foreach (var room in site.Floors[k].Rooms)
                {
                    var b = room.Bounds;
                    if (b.W < 5 || b.H < 5) continue;
                    arenas++;
                    var edge = TouchingWalls(site, k, room);
                    if (edge.Any(c => Plated(site, k, c.Item1, c.Item2, DungeonSite.FloorSpacing - 3) || Plated(site, k, c.Item1, c.Item2, DungeonSite.FloorSpacing - 2)))
                        high++;
                }
        Assert.True(high * 2 > arenas, $"only {high} of {arenas} arenas have a high plate");
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

    private static List<(int X, int Z)> TouchingWalls(DungeonSite site, int floor, Room room)
    {
        var layout = site.Floors[floor];
        var b = room.Bounds;
        var walls = new List<(int X, int Z)>();
        for (int z = b.Z; z < b.Bottom; z++)
            for (int x = b.X; x < b.Right; x++)
            {
                if (!room.Contains(x, z)) continue;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var c = (X: x + dx, Z: z + dz);
                    if (layout.CellAt(c.X, c.Z) == CellKind.Rock && layout.IsWallCell(c.X, c.Z) && !walls.Contains(c)) walls.Add(c);
                }
            }
        return walls;
    }

    private static (int X, int Z) FrontOf(Room room, (int X, int Z) wall)
    {
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            if (room.Contains(wall.X + dx, wall.Z + dz)) return (wall.X + dx, wall.Z + dz);
        return wall;
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
