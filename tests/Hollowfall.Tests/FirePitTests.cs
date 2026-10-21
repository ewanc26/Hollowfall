using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Models;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Tests;

public class FirePitPlacerTests
{
    private static readonly Lazy<List<(long Seed, DungeonSite Site)>> Sites = new(() =>
    {
        var list = new List<(long, DungeonSite)>();
        for (long seed = 1; seed <= 30; seed++)
        {
            var g = new TerrainGenerator(seed);
            for (int rx = -2; rx <= 1; rx++)
                for (int rz = -2; rz <= 1; rz++)
                    if (g.Dungeons!.GetSite(rx, rz) is { } s)
                        list.Add((seed, s));
        }
        return list;
    });

    public static IEnumerable<(long Seed, DungeonSite Site)> AllSites => Sites.Value;

    [Fact]
    public void ThereAreDungeonsToTest() => Assert.True(AllSites.Count() >= 30);

    [Fact]
    public void TheSameDungeonAndSeedGiveTheSamePits()
    {
        foreach (var (seed, site) in AllSites.Take(40))
            Assert.Equal(FirePitPlacer.Place(site, seed), FirePitPlacer.Place(site, seed));
    }

    [Fact]
    public void EveryDungeonHasAtLeastOnePitOnFloorZeroOrOne()
    {
        foreach (var (seed, site) in AllSites)
            Assert.True(FirePitPlacer.Place(site, seed).Any(p => p.Floor <= 1), $"seed {seed} site {site.RegionX},{site.RegionZ} has no pit on floor 0 or 1");
    }

    [Fact]
    public void PitsStandInSuitableRoomsOnFloorWithAFreeRing()
    {
        foreach (var (seed, site) in AllSites)
            foreach (var p in FirePitPlacer.Place(site, seed))
            {
                var floor = site.Floors[p.Floor];
                var room = floor.Rooms[p.RoomId];
                Assert.True(FirePitPlacer.SuitableRole(room.Role), $"pit in a {room.Role} room");
                Assert.NotEqual(RoomRole.Entrance, room.Role);
                Assert.NotEqual(RoomRole.Boss, room.Role);
                Assert.NotEqual(RoomRole.Trap, room.Role);
                Assert.True(FirePitPlacer.ClearRing(floor, room, p.CellX, p.CellZ), "all eight neighbours are floor in the same room");
                var (wx, wy, wz) = site.ToWorld(p.Floor, p.CellX, p.CellZ);
                Assert.Equal(wx + 0.5, p.X, 6);
                Assert.Equal(wy, p.Y, 6);
                Assert.Equal(wz + 0.5, p.Z, 6);
            }
    }

    [Fact]
    public void PitsKeepClearOfDoorsStairsChestsKeysAndNotes()
    {
        foreach (var (seed, site) in AllSites)
        {
            var notes = Hollowfall.Core.Story.NotePlacer.Place(site, seed);
            foreach (var p in FirePitPlacer.Place(site, seed))
            {
                var floor = site.Floors[p.Floor];
                for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                        Assert.False(floor.CellAt(p.CellX + dx, p.CellZ + dz) is CellKind.Door or CellKind.SecretDoor, "near a door");
                int Cheb(int x, int z) => Math.Max(Math.Abs(x - p.CellX), Math.Abs(z - p.CellZ));
                Assert.True(Cheb(floor.EntranceX, floor.EntranceZ) > 2);
                if (floor.ExitStairs is { } e) Assert.True(Cheb(e.X, e.Z) > 2);
                foreach (var s in site.Spawns.Where(s => s.Floor == p.Floor))
                    Assert.True(Cheb(s.X - site.OriginX, s.Z - site.OriginZ) > 2, $"near a {s.Kind}");
                foreach (var n in notes.Where(n => n.Floor == p.Floor))
                    Assert.True(Cheb(n.CellX, n.CellZ) > 2, "near a note");
            }
        }
    }

    [Fact]
    public void PitsAreSpacedApartAndNeverMoreThanTwoPerRoom()
    {
        foreach (var (seed, site) in AllSites)
        {
            var pits = FirePitPlacer.Place(site, seed);
            foreach (var group in pits.GroupBy(p => (p.Floor, p.RoomId))) Assert.True(group.Count() <= 2);
            foreach (var a in pits)
                foreach (var b in pits)
                    if (a != b && a.Floor == b.Floor)
                        Assert.True(Math.Abs(a.CellX - b.CellX) + Math.Abs(a.CellZ - b.CellZ) >= FirePitPlacer.MinSpacing);
        }
    }

    [Fact]
    public void ThePitsNeverCutAFloorOffFromItsStairs()
    {
        foreach (var (seed, site) in AllSites)
        {
            var pits = FirePitPlacer.Place(site, seed);
            foreach (var floor in site.Floors)
            {
                var blocked = pits.Where(p => p.Floor == floor.Floor).Select(p => (p.CellX, p.CellZ)).ToHashSet();
                var reach = Flood(floor, blocked);
                var reachWithout = Flood(floor, []);
                // Some links are portal gaps, so the stairs are not always reachable on foot: what matters is that the pits change nothing.
                if (floor.ExitStairs is { } e) Assert.Equal(reachWithout.Contains((e.X, e.Z)), reach.Contains((e.X, e.Z)));
                Assert.Equal(reachWithout.Count - blocked.Count(b => reachWithout.Contains(b)), reach.Count);
            }
        }
    }

    private static HashSet<(int X, int Z)> Flood(FloorLayout floor, HashSet<(int, int)> blocked)
    {
        var seen = new HashSet<(int, int)> { (floor.EntranceX, floor.EntranceZ) };
        var stack = new Stack<(int X, int Z)>([(floor.EntranceX, floor.EntranceZ)]);
        while (stack.Count > 0)
        {
            var (x, z) = stack.Pop();
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = (x + dx, z + dz);
                if (FloorLayout.IsWalkable(floor.CellAt(n.Item1, n.Item2)) && !blocked.Contains(n) && seen.Add(n)) stack.Push(n);
            }
        }
        return seen;
    }
}

public class FirePitModelTests
{
    private static LowPolyModel M => ModelCatalog.FirePit;

    [Fact]
    public void IsAStoneRingWithLogsAndAFlameOfGlowingGems()
    {
        foreach (var name in new[] { "stones", "logs", "embers", "flame", "tongue_a", "tongue_b" }) Assert.True(M.HasPart(name), name);
        Assert.False(M.Part("flame").Glow.IsEmpty);
        Assert.True(M.Part("flame").Solid.IsEmpty, "the flame is entirely emissive");
        Assert.False(M.Part("stones").Solid.IsEmpty);
        Assert.InRange(M.TriangleCount, 180, 320);
    }

    [Fact]
    public void IsLowAndAboutHalfABlockAcross()
    {
        Assert.InRange(M.Width, 0.8, 1.15);
        Assert.InRange(M.Height, 0.4, 0.8);
        Assert.True(M.Min.Y >= -1e-6, "sits on the floor");
    }
}
