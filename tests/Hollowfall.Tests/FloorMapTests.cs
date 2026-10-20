using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Tests;

public class FloorMapTests
{
    private static DungeonSite Site(long seed)
    {
        var g = new TerrainGenerator(seed);
        for (int rx = -3; rx <= 3; rx++)
            for (int rz = -3; rz <= 3; rz++)
                if (g.Dungeons!.GetSite(rx, rz) is { } s) return s;
        throw new InvalidOperationException();
    }

    private static (double X, double Z) Centre(DungeonSite site, int floor, Room room) => (site.OriginX + room.CenterX + 0.5, site.OriginZ + room.CenterZ + 0.5);

    [Fact]
    public void NothingIsKnownAtFirst()
    {
        var site = Site(3);
        var map = new FloorMap(site);
        Assert.Equal(0, map.Version);
        for (int f = 0; f < map.FloorCount; f++) Assert.Equal(0, map.SeenCount(f));
        Assert.Equal(MapCell.Unknown, map.At(0, 10, 10));
    }

    [Fact]
    public void StandingInARoomRevealsItsFloorAndWalls()
    {
        var site = Site(3);
        var map = new FloorMap(site);
        var layout = site.Floors[0];
        var room = layout.EntranceRoom;
        // A spot beside a wall, so a wall is certainly in sight.
        (int X, int Z)? spot = null;
        for (int z = room.Bounds.Z; z < room.Bounds.Bottom && spot == null; z++)
            for (int x = room.Bounds.X; x < room.Bounds.Right && spot == null; x++)
                if (layout.CellAt(x, z) == CellKind.Floor && layout.RoomIdAt(x, z) == room.Id && layout.CellAt(x + 1, z) == CellKind.Rock) spot = (x, z);
        Assert.NotNull(spot);
        map.Reveal(0, site.OriginX + spot!.Value.X + 0.5, site.OriginZ + spot.Value.Z + 0.5);
        Assert.True(map.Version > 0);
        Assert.Equal(MapCell.Floor, map.At(0, spot.Value.X, spot.Value.Z));
        Assert.Equal(MapCell.Wall, map.At(0, spot.Value.X + 1, spot.Value.Z));
        Assert.True(map.SeenCount(0) > 20);
        // Rock beyond that first wall is not seen through it.
        Assert.Equal(MapCell.Unknown, map.At(0, spot.Value.X + 3, spot.Value.Z));
    }

    private const int SightRange = FloorMap.SightRadius;

    [Fact]
    public void FarAwayPartsStayUnknown()
    {
        var site = Site(3);
        var map = new FloorMap(site);
        var room = site.Floors[0].EntranceRoom;
        var (x, z) = Centre(site, 0, room);
        map.Reveal(0, x, z);
        var (cx, cz) = map.CellOf(x, z);
        var far = site.Floors[0].Rooms.OrderByDescending(r => Math.Abs(r.CenterX - cx) + Math.Abs(r.CenterZ - cz)).First();
        Assert.Equal(MapCell.Unknown, map.At(0, far.CenterX, far.CenterZ));
    }

    [Fact]
    public void RockBlocksSightAndSecretDoorsLookLikeWall()
    {
        var site = Site(3);
        var map = new FloorMap(site);
        var layout = site.Floors[0];
        var (x, z) = Centre(site, 0, layout.EntranceRoom);
        map.Reveal(0, x, z);
        var (cx, cz) = map.CellOf(x, z);
        for (int dz = -SightRange; dz <= SightRange; dz++)
            for (int dx = -SightRange; dx <= SightRange; dx++)
            {
                if (layout.CellAt(cx + dx, cz + dz) == CellKind.SecretDoor && map.IsSeen(0, cx + dx, cz + dz))
                    Assert.Equal(MapCell.Wall, map.At(0, cx + dx, cz + dz));
                // A cell seen beyond rock would mean sight went through a wall: every seen open cell has an open neighbour that is seen.
                if (map.IsSeen(0, cx + dx, cz + dz) && layout.CellAt(cx + dx, cz + dz) == CellKind.Floor)
                {
                    bool linked = (dx == 0 && dz == 0) || new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1) }
                        .Any(d => map.IsSeen(0, cx + dx + d.Item1, cz + dz + d.Item2) && layout.CellAt(cx + dx + d.Item1, cz + dz + d.Item2) != CellKind.Rock);
                    Assert.True(linked);
                }
            }
    }

    [Fact]
    public void RevealingTheSamePlaceAgainChangesNothing()
    {
        var site = Site(3);
        var map = new FloorMap(site);
        var (x, z) = Centre(site, 0, site.Floors[0].EntranceRoom);
        map.Reveal(0, x, z);
        int version = map.Version, seen = map.SeenCount(0);
        map.Reveal(0, x + 0.3, z - 0.2); // same cell
        Assert.Equal(version, map.Version);
        Assert.Equal(seen, map.SeenCount(0));
    }

    [Fact]
    public void FloorsAreMappedSeparately()
    {
        var site = Site(3);
        if (site.FloorCount < 2) return;
        var map = new FloorMap(site);
        var (x, z) = Centre(site, 0, site.Floors[0].EntranceRoom);
        map.Reveal(0, x, z);
        Assert.Equal(0, map.SeenCount(1));
    }

    [Fact]
    public void WalkingAlongRevealsMore()
    {
        var site = Site(3);
        var map = new FloorMap(site);
        var layout = site.Floors[0];
        var link = layout.Links[0];
        var a = layout.Rooms[link.A];
        var b = layout.Rooms[link.B];
        map.Reveal(0, site.OriginX + a.CenterX + 0.5, site.OriginZ + a.CenterZ + 0.5);
        int before = map.SeenCount(0);
        map.Reveal(0, site.OriginX + b.CenterX + 0.5, site.OriginZ + b.CenterZ + 0.5);
        Assert.True(map.SeenCount(0) > before);
    }

    [Fact]
    public void TheSimulationMapsWhereThePlayerGoes()
    {
        var world = new Hollowfall.Core.World.VoxelWorld(5);
        DungeonSite? site = null;
        for (int rx = -3; rx <= 3 && site == null; rx++)
            for (int rz = -3; rz <= 3 && site == null; rz++) site = world.Generator.Dungeons!.GetSite(rx, rz);
        var sim = new Hollowfall.Core.Combat.CombatSim(world, site!);
        var room = site!.Floors[0].EntranceRoom;
        var feet = new Hollowfall.Core.Combat.Vec3(site.OriginX + room.CenterX + 0.5, DungeonSite.FloorY(0) + 1, site.OriginZ + room.CenterZ + 0.5);
        sim.Tick(1.0 / 60, new Hollowfall.Core.Combat.PlayerView(feet, feet + new Hollowfall.Core.Combat.Vec3(0, 1.6, 0), new Hollowfall.Core.Combat.Vec3(1, 0, 0), false, false), default);
        Assert.True(sim.Map.SeenCount(0) > 0);
    }
}
