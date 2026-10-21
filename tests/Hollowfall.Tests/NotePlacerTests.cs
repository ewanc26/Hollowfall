using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Story;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Tests;

public class NotePlacerTests
{
    private static readonly Lazy<List<(long Seed, DungeonSite Site)>> Sites = new(() =>
    {
        var list = new List<(long, DungeonSite)>();
        for (long seed = 1; seed <= 14; seed++)
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

    /// <summary>Every cell a player can reach from the floor's stairs without opening a secret door, walking and crossing gaps by portal.</summary>
    private static HashSet<(int X, int Z)> Flood(FloorLayout floor)
    {
        Assert.True(LayoutWalk.Walkable(floor, floor.EntranceX, floor.EntranceZ), "the flood fill must start on a walkable cell");
        return LayoutWalk.Reach(floor, floor.EntranceX, floor.EntranceZ, crossGaps: true);
    }

    [Fact]
    public void ThereAreSitesAndNotesToTest()
    {
        Assert.True(Sites.Value.Count >= 10, $"only {Sites.Value.Count} sites");
        int notes = Sites.Value.Sum(s => NotePlacer.Place(s.Site, s.Seed).Count);
        Assert.True(notes >= Sites.Value.Count * 2, $"only {notes} notes in {Sites.Value.Count} sites");
    }

    [Fact]
    public void PlacementIsDeterministic()
    {
        foreach (var (seed, site) in AllSites)
        {
            var a = NotePlacer.Place(site, seed);
            var b = NotePlacer.Place(site, seed);
            Assert.Equal(a, b);
        }
        // And from a dungeon generated afresh.
        var (s0, site0) = Sites.Value[0];
        var fresh = new TerrainGenerator(s0).Dungeons!.GetSite(site0.RegionX, site0.RegionZ)!;
        Assert.Equal(NotePlacer.Place(site0, s0), NotePlacer.Place(fresh, s0));
    }

    [Fact]
    public void EveryFloorHoldsOneToThreeNotes()
    {
        foreach (var (seed, site) in AllSites)
        {
            var spots = NotePlacer.Place(site, seed);
            for (int f = 0; f < site.FloorCount; f++)
                Assert.InRange(spots.Count(s => s.Floor == f), 1, StoryPlan.MaxNotesPerFloor);
        }
    }

    [Fact]
    public void NotesLieOnFloorCellsBesideAWallInReachableRoomsThatAreNotEntranceBossOrTrapRooms()
    {
        foreach (var (seed, site) in AllSites)
            foreach (var spot in NotePlacer.Place(site, seed))
            {
                var floor = site.Floors[spot.Floor];
                Assert.Equal(CellKind.Floor, floor.CellAt(spot.CellX, spot.CellZ));
                Assert.Equal(spot.RoomId, floor.RoomIdAt(spot.CellX, spot.CellZ));
                Assert.Equal(CellKind.Rock, floor.CellAt(spot.CellX - spot.FaceX, spot.CellZ - spot.FaceZ));
                Assert.Equal(1, Math.Abs(spot.FaceX) + Math.Abs(spot.FaceZ));

                var role = floor.Rooms.Single(r => r.Id == spot.RoomId).Role;
                Assert.DoesNotContain(role, new[] { RoomRole.Entrance, RoomRole.Boss, RoomRole.Trap });

                // The drawn position is inside the cell, on the floor of that level.
                Assert.InRange(spot.X, site.OriginX + spot.CellX, site.OriginX + spot.CellX + 1);
                Assert.InRange(spot.Z, site.OriginZ + spot.CellZ, site.OriginZ + spot.CellZ + 1);
                Assert.Equal(site.ToWorld(spot.Floor, spot.CellX, spot.CellZ).Y, spot.Y);
            }
    }

    [Fact]
    public void NotesKeepAwayFromStairsDoorsAndEverythingSpawned()
    {
        foreach (var (seed, site) in AllSites)
            foreach (var spot in NotePlacer.Place(site, seed))
            {
                var floor = site.Floors[spot.Floor];
                Assert.False(Math.Abs(spot.CellX - floor.EntranceX) <= 3 && Math.Abs(spot.CellZ - floor.EntranceZ) <= 3);
                if (floor.ExitStairs is { } e) Assert.False(Math.Abs(spot.CellX - e.X) <= 3 && Math.Abs(spot.CellZ - e.Z) <= 3);

                for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                        Assert.DoesNotContain(floor.CellAt(spot.CellX + dx, spot.CellZ + dz), new[] { CellKind.Door, CellKind.SecretDoor });

                foreach (var sp in site.Spawns.Where(s => s.Floor == spot.Floor))
                {
                    int cheb = Math.Max(Math.Abs(sp.X - site.OriginX - spot.CellX), Math.Abs(sp.Z - site.OriginZ - spot.CellZ));
                    Assert.True(cheb >= 2, $"a note shares a cell neighbourhood with a {sp.Kind}");
                    if (sp.Kind == SpawnKind.Loot)
                    {
                        double d = Math.Sqrt(Math.Pow(sp.X - site.OriginX - spot.CellX, 2) + Math.Pow(sp.Z - site.OriginZ - spot.CellZ, 2));
                        Assert.True(d >= NotePlacer.ChestClearance, "a note is within reach of a chest, so one press would do both");
                    }
                }
            }
    }

    [Fact]
    public void NotesOnAFloorAreSpacedApart()
    {
        foreach (var (seed, site) in AllSites)
            foreach (var group in NotePlacer.Place(site, seed).GroupBy(s => s.Floor))
            {
                var list = group.ToList();
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                        Assert.True(Math.Abs(list[i].CellX - list[j].CellX) + Math.Abs(list[i].CellZ - list[j].CellZ) >= NotePlacer.MinSpacing);
            }
    }

    [Fact]
    public void EveryNoteCanBeReachedFromTheFloorStartWithoutASecretDoor()
    {
        foreach (var (seed, site) in AllSites)
        {
            var spots = NotePlacer.Place(site, seed);
            foreach (var floor in site.Floors)
            {
                var reachable = Flood(floor);
                foreach (var spot in spots.Where(s => s.Floor == floor.Floor))
                    Assert.Contains((spot.CellX, spot.CellZ), reachable);
            }
        }
    }

    [Fact]
    public void NotesReadInOrderAsYouWalkInAndUseTheFloorsOwnRun()
    {
        foreach (var (seed, site) in AllSites)
        {
            var plan = StoryPlan.For(seed, site.FloorCount);
            var spots = NotePlacer.Place(site, seed);
            foreach (var group in spots.GroupBy(s => s.Floor))
            {
                var list = group.ToList();
                var run = plan.RunFor(group.Key);
                Assert.Equal(run.Take(list.Count).Select(n => n.Id), list.Select(s => s.Note.Id));
                var depths = list.Select(s => site.Floors[s.Floor].Rooms.Single(r => r.Id == s.RoomId).Depth).ToList();
                Assert.Equal(depths.Order(), depths);
            }
            var ids = spots.Select(s => s.Note.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.Equal(Enumerable.Range(0, spots.Count), spots.Select(s => s.Index));
        }
    }

    [Fact]
    public void EveryPlacedNoteHasRealText()
    {
        foreach (var (seed, site) in AllSites)
            foreach (var spot in NotePlacer.Place(site, seed))
            {
                Assert.False(string.IsNullOrWhiteSpace(spot.Note.Title));
                Assert.InRange(spot.Note.Text.Length, 20, StoryBook.MaxNoteLength + 40);
                Assert.DoesNotContain('{', spot.Note.Text + spot.Note.Title);
            }
    }

    [Fact]
    public void DifferentSeedsPutDifferentNotesInDifferentPlaces()
    {
        // The same dungeon layout told with two story seeds differs in its words; different dungeons differ in their places.
        var (seed, site) = Sites.Value[0];
        var a = NotePlacer.Place(site, seed);
        var b = NotePlacer.Place(site, seed + 1000);
        Assert.NotEqual(a.Select(s => (s.CellX, s.CellZ, s.Note.Id)), b.Select(s => (s.CellX, s.CellZ, s.Note.Id)));
        var places = Sites.Value.Select(s => string.Join(";", NotePlacer.Place(s.Site, s.Seed).Select(n => $"{n.Floor}:{n.CellX},{n.CellZ}"))).Distinct().Count();
        Assert.True(places > Sites.Value.Count / 2);
    }

    [Fact]
    public void AcrossSeedsTheWholeBookCanBePlaced()
    {
        var placed = new HashSet<string>();
        foreach (var (seed, site) in AllSites)
            foreach (var spot in NotePlacer.Place(site, seed))
                placed.Add(StoryBook.Notes.Single(d => spot.Note.Id == d.Id).Id);
        // A handful of sites place only some of each thread, but the first note of every band's threads is always reachable.
        foreach (var band in Enum.GetValues<DepthBand>())
            Assert.Contains(StoryBook.Notes.Where(n => n.Band == band && n.Order == 1), n => placed.Contains(n.Id));
    }

    [Fact]
    public void NearestFindsTheNoteWithinReachAndOnTheSameLevel()
    {
        var (seed, site) = Sites.Value[0];
        var spots = NotePlacer.Place(site, seed);
        var spot = spots[0];
        Assert.Equal(spot, NotePlacer.Nearest(spots, spot.X + 1, spot.Y, spot.Z));
        Assert.Equal(spot, NotePlacer.Nearest(spots, spot.X, spot.Y + 1.5, spot.Z));
        Assert.Null(NotePlacer.Nearest(spots, spot.X + NotePlacer.Reach + 0.5, spot.Y, spot.Z));
        Assert.Null(NotePlacer.Nearest(spots, spot.X, spot.Y + 8, spot.Z)); // the floor above or below
        Assert.Null(NotePlacer.Nearest([], 0, 0, 0));
    }
}
