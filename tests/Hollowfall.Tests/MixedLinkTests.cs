using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Physics;
using Hollowfall.Core.Portals;
using Hollowfall.Core.Story;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

/// <summary>
/// Rooms are joined by a mix of walkable corridors and gaps that need a portal. These tests pin the guarantees: every room has an exit
/// gap and an entrance hallway, every gap can really be crossed with the real portal placement and walking body, and every dungeon can
/// be finished.
/// </summary>
public class MixedLinkTests
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

    private static IEnumerable<FloorLayout> ManyLayouts(int count = 240)
    {
        for (int s = 1; s <= count; s++)
        {
            int ex = 12 + (s * 7) % 73, ez = 12 + (s * 13) % 73;
            yield return FloorLayout.Generate(s, s % 4, ex, ez, lastFloor: s % 4 == 3);
        }
    }

    private static IEnumerable<(DungeonSite Site, int Floor, FloorLayout Layout)> AllFloors() =>
        Sites.Value.SelectMany(s => s.Site.Floors.Select((f, k) => (s.Site, k, f)));

    // ---- the guarantees -----------------------------------------------------------------------

    [Fact]
    public void EveryRoomHasAnExitGapAndAnEntranceHallway()
    {
        foreach (var l in ManyLayouts())
        {
            Assert.True(l.GapsComplete, $"floor {l.Floor}: {l.GapFailure}");
            foreach (var room in l.Rooms)
            {
                var mine = l.Links.Select((link, i) => (link, i)).Where(x => !x.link.Secret && (x.link.A == room.Id || x.link.B == room.Id)).ToList();
                Assert.NotEmpty(mine);
                Assert.Contains(mine, x => x.link.Kind == LinkKind.PortalGap);

                // The gap is a pair of hallways, and this room's end is a walkable hallway that runs up to the pit.
                var gap = l.Gaps.First(g => g.Kind == LinkKind.PortalGap && (l.Links[g.LinkIndex].A == room.Id || l.Links[g.LinkIndex].B == room.Id));
                var hall = gap.RoomFrom == room.Id ? gap.HallFrom : gap.HallTo;
                Assert.True(hall.Count >= LinkMix.HallMin, $"room {room.Id}: hallway only {hall.Count} cells");
                Assert.All(hall, c => Assert.True(FloorLayout.IsWalkable(l.CellAt(c.X, c.Z))));
            }
        }
    }

    [Fact]
    public void ARoomWithOneWayOutLeavesThroughAGap()
    {
        int leaves = 0;
        foreach (var l in ManyLayouts())
            foreach (var room in l.Rooms)
            {
                var mine = l.Links.Where(x => !x.Secret && (x.A == room.Id || x.B == room.Id)).ToList();
                if (mine.Count != 1) continue;
                leaves++;
                Assert.Equal(LinkKind.PortalGap, mine[0].Kind);
            }
        Assert.True(leaves > 100, $"only {leaves} leaf rooms were checked");
    }

    [Fact]
    public void TheFirstWayOutOfTheEntranceIsAShortGap()
    {
        foreach (var l in ManyLayouts())
        {
            var gaps = l.Gaps.Where(g => g.Kind == LinkKind.PortalGap && (l.Links[g.LinkIndex].A == l.EntranceRoom.Id || l.Links[g.LinkIndex].B == l.EntranceRoom.Id)).ToList();
            Assert.NotEmpty(gaps);
            Assert.Contains(gaps, g => g.SpanLength is >= LinkMix.FirstSpanMin and <= LinkMix.FirstSpanMax && g.Hazard == GapHazard.Bare);
        }
    }

    [Fact]
    public void GapsAreBuiltTheWayTheyAreDescribed()
    {
        foreach (var (site, k, l) in AllFloors())
            foreach (var gap in l.Gaps)
            {
                Assert.Equal(gap.Kind, l.Links[gap.LinkIndex].Kind);
                Assert.Contains(gap.Kind, new[] { LinkKind.PortalGap, LinkKind.ShaftGap });

                bool portal = gap.Kind == LinkKind.PortalGap;
                if (portal)
                {
                    Assert.InRange(gap.SpanLength, LinkMix.SpanMin, LinkMix.SpanMax);
                    Assert.InRange(gap.HallFrom.Count - 1, LinkMix.HallMin, LinkMix.HallMax);
                    Assert.InRange(gap.HallTo.Count - 1, LinkMix.HallMin, LinkMix.HallMax);
                    var pit = gap.Hazard switch { GapHazard.Spikes => CellKind.PitSpikes, GapHazard.Water => CellKind.PitWater, _ => CellKind.Pit };
                    foreach (var c in gap.Span)
                        for (int lat = -1; lat <= 1; lat++)
                        {
                            int x = c.X - gap.DirZ * lat, z = c.Z + gap.DirX * lat;
                            Assert.Equal(pit, l.CellAt(x, z));
                            Assert.Equal(0, l.HeightAt(x, z));
                        }
                    // The platforms either side climb three steps from a landing and stay up at the pit's lip.
                    foreach (var hall in new[] { gap.Hall(true), gap.Hall(false) })
                    {
                        Assert.Equal(new[] { 0, 0, 1, 2, 3 }, hall.Take(5).Select(c => l.HeightAt(c.X, c.Z)));
                        Assert.All(hall.Skip(4), c => Assert.Equal(LinkMix.Rise, l.HeightAt(c.X, c.Z)));
                        Assert.Equal(LinkMix.Rise, l.HeightAt(hall[^1].X, hall[^1].Z));
                    }
                }
                else
                {
                    Assert.InRange(gap.SpanLength, LinkMix.TunnelMin, LinkMix.TunnelMax);
                    Assert.All(gap.Span, c => Assert.Equal(LinkMix.Rise, l.HeightAt(c.X, c.Z)));
                    Assert.All(gap.HallFrom.Concat(gap.HallTo), c => Assert.Equal(0, l.HeightAt(c.X, c.Z)));
                }

                // Three wide, walled on both sides.
                foreach (var c in gap.HallFrom.Concat(gap.Span).Concat(gap.HallTo))
                {
                    Assert.Equal(CellKind.Rock, l.CellAt(c.X + gap.DirZ * 2, c.Z - gap.DirX * 2));
                    Assert.Equal(CellKind.Rock, l.CellAt(c.X - gap.DirZ * 2, c.Z + gap.DirX * 2));
                }
            }
    }

    [Fact]
    public void ThePitsAreThreeDeepAndTheHazardIsInTheBlocks()
    {
        int spikes = 0, water = 0, bare = 0;
        foreach (var (site, k, l) in AllFloors())
            foreach (var gap in l.Gaps.Where(g => g.Kind == LinkKind.PortalGap))
            {
                var mid = gap.Span[gap.Span.Count / 2];
                int x = site.OriginX + mid.X, z = site.OriginZ + mid.Z;
                int y0 = DungeonSite.FloorY(k);
                var lip = gap.EdgeFrom;
                Assert.Equal(BlockType.BrickFloor, site.BlockAt(site.OriginX + lip.X, y0 + LinkMix.Rise, site.OriginZ + lip.Z));
                Assert.Equal(BlockType.Air, site.BlockAt(site.OriginX + lip.X, y0 + LinkMix.Rise + 1, site.OriginZ + lip.Z));
                Assert.Equal(BlockType.Air, site.BlockAt(x, y0 + LinkMix.Rise, z)); // the pit is open down to its floor
                switch (gap.Hazard)
                {
                    case GapHazard.Spikes:
                        spikes++;
                        Assert.Equal(BlockType.Spikes, site.BlockAt(x, y0, z));
                        Assert.False(BlockInfo.Portalable(BlockType.Spikes));
                        break;
                    case GapHazard.Water:
                        water++;
                        Assert.Equal(BlockType.BrickFloor, site.BlockAt(x, y0, z));
                        Assert.Equal(BlockType.Water, site.BlockAt(x, y0 + 1, z));
                        break;
                    default:
                        bare++;
                        Assert.Equal(BlockType.BrickFloor, site.BlockAt(x, y0, z));
                        Assert.Equal(BlockType.Air, site.BlockAt(x, y0 + 1, z));
                        break;
                }
                // The walls of the channel are portalable stone, so there is always something to aim at.
                var wall = site.BlockAt(site.OriginX + mid.X - gap.DirZ * 2, y0 + 2, site.OriginZ + mid.Z + gap.DirX * 2);
                Assert.True(wall is { } w && BlockInfo.Portalable(w));
            }
        Assert.True(spikes > 10 && water > 10 && bare > 10, $"hazards: {bare} bare, {spikes} spikes, {water} water");
    }

    [Fact]
    public void EveryGapCanBeCrossedFromEitherSideWithTheRealPortals()
    {
        int crossed = 0;
        foreach (var (site, k, l) in AllFloors())
            foreach (var gap in l.Gaps)
                foreach (bool fromSide in new[] { true, false })
                {
                    GapCrossing.LastRejection = null;
                    var plan = GapCrossing.Plan(site, k, gap, fromSide);
                    Assert.True(plan != null, $"floor {k} {gap.Kind} (span {gap.SpanLength}, {gap.Hazard}) cannot be crossed from {(fromSide ? "the first" : "the second")} end: {GapCrossing.LastRejection}");
                    VerifyIndependently(site, k, plan!);
                    crossed++;
                }
        Assert.True(crossed > 1000, $"only {crossed} crossings were checked");
    }

    /// <summary>Fires the planned portals again from scratch and walks a fresh body through them, checking the far side.</summary>
    private static void VerifyIndependently(DungeonSite site, int floor, GapPlan plan)
    {
        var blocks = GapCrossing.Blocks(site);
        var portals = new PortalSystem();
        var teal = portals.Place(PortalColor.Teal, plan.Eye, plan.FarAim, plan.Facing, blocks);
        var violet = portals.Place(PortalColor.Violet, plan.Eye, plan.NearAim, plan.Facing, blocks);
        Assert.True(teal.Placed && violet.Placed, "the planned aims must place both portals");
        Assert.True(portals.Linked);

        var body = new PlayerBody(plan.Feet.X, plan.Feet.Y, plan.Feet.Z);
        for (int i = 0; i < 20; i++) body.Step(1.0 / 60, 0, 0, false, blocks);
        bool crossed = false;
        for (int i = 0; i < 60 * 5 && !crossed; i++)
        {
            var to = portals.Violet!.Frame.Center - PortalWalker.Centre(body);
            double len = Math.Sqrt(to.X * to.X + to.Z * to.Z);
            double mx = len < 0.15 ? -portals.Violet.Frame.Normal.X : to.X / len, mz = len < 0.15 ? -portals.Violet.Frame.Normal.Z : to.Z / len;
            crossed = PortalWalker.Step(body, portals, 1.0 / 60, mx, mz, false, blocks, plan.Facing) != null;
        }
        Assert.True(crossed, "walking into the near portal must go through it");
        GapCrossing.SettleAfterCrossing(body, portals, portals.Teal!, plan.Facing, blocks);
        Assert.True(body.OnGround);

        var layout = site.Floors[floor];
        var cell = ((int)Math.Floor(body.X) - site.OriginX, (int)Math.Floor(body.Z) - site.OriginZ);
        var gap = plan.Gap;
        var farHall = gap.Kind == LinkKind.ShaftGap ? gap.Span : plan.FromSide ? gap.HallTo : gap.HallFrom;
        Assert.True(gap.Within(farHall, cell, 1), $"landed at {cell}, outside the far side");
        Assert.True(FloorLayout.IsWalkable(layout.CellAt(cell.Item1, cell.Item2)), "the landing cell must be walkable ground");
        // The body may straddle two cells (a step edge), so it stands at the surface of one of the cells it touches.
        var surfaces = new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) }.Select(d => DungeonSite.FloorY(floor) + 1.0 + layout.HeightAt(cell.Item1 + d.Item1, cell.Item2 + d.Item2));
        Assert.Contains(surfaces, y => Math.Abs(y - body.Y) < 0.01);
    }

    [Fact]
    public void WithoutPortalsAPortalGapCannotBeCrossedOnFoot()
    {
        // Walking on over the lip drops the body into the pit, and it cannot climb out: the pit is three deep and the step is one.
        int checkedGaps = 0;
        foreach (var (site, k, l) in AllFloors().Take(60))
            foreach (var gap in l.Gaps.Where(g => g.Kind == LinkKind.PortalGap))
            {
                var feet = GapCrossing.FeetAt(site, k, gap.EdgeFrom);
                var blocks = GapCrossing.Blocks(site);
                var (dx, dz) = gap.Toward(true);
                var body = new PlayerBody(feet.X, feet.Y, feet.Z);
                for (int i = 0; i < 60 * 8; i++) body.Step(1.0 / 60, dx, dz, jump: i % 30 == 0, blocks);
                Assert.True(body.Y < feet.Y - 1.9, $"floor {k}: the body is still up at y {body.Y:F2} (started {feet.Y:F2})");
                Assert.True(gap.Within(gap.Span, ((int)Math.Floor(body.X) - site.OriginX, (int)Math.Floor(body.Z) - site.OriginZ), 1), "it should be in the pit");
                checkedGaps++;
            }
        Assert.True(checkedGaps > 40);
    }

    [Fact]
    public void AllRoomsExitStairsAndBossAreReachedWithValidatedHops()
    {
        foreach (var (_, site) in Sites.Value.Take(40))
        {
            var start = new Spot(site.EntranceX + DungeonSite.PlazaRadius, site.SurfaceY + 1, site.EntranceZ);
            var reach = DungeonReach.Analyse(site, start);
            for (int k = 0; k < site.FloorCount; k++)
            {
                var floor = site.Floors[k];
                foreach (var room in floor.Rooms)
                    Assert.True(DungeonReach.HopsToRoom(site, k, room, reach) is not null, $"floor {k} room {room.Id} ({room.Role}) unreachable");
                if (floor.ExitStairs is { } e)
                {
                    var (ex, ey, ez) = site.ToWorld(k, e.X, e.Z + 2);
                    Assert.Contains(new Spot(ex, ey, ez), reach.Keys);
                }
            }
            var boss = site.Floors[^1].BossRoom!;
            Assert.True(DungeonReach.HopsToRoom(site, site.FloorCount - 1, boss, reach) is not null);
        }
    }

    // ---- the mix --------------------------------------------------------------------------------

    private static (int Corridor, int Portal, int Shaft, int Secret) Count(FloorLayout l) =>
        (l.Links.Count(x => x.Kind == LinkKind.Corridor), l.Links.Count(x => x.Kind == LinkKind.PortalGap),
         l.Links.Count(x => x.Kind == LinkKind.ShaftGap), l.Links.Count(x => x.Kind == LinkKind.SecretDoor));

    [Fact]
    public void TheMixFollowsTheFloorsDepth()
    {
        double Share(int floor)
        {
            int gaps = 0, ordinary = 0;
            foreach (var l in ManyLayouts(400).Where(l => l.Floor == floor))
            {
                var c = Count(l);
                gaps += c.Portal;
                ordinary += c.Corridor + c.Portal + c.Shaft;
            }
            return (double)gaps / ordinary;
        }

        double f0 = Share(0), f1 = Share(1), f2 = Share(2);
        // Every room needs a gap, so even the first floor is about half gaps; deeper floors only add to that.
        Assert.InRange(f0, 0.30, 0.70);
        Assert.True(f1 > f0 + 0.03 && f2 > f1 + 0.03, $"gap share by floor: {f0:F2}, {f1:F2}, {f2:F2}");
    }

    [Fact]
    public void ShaftGapsAreRareAndNeverOnTheFirstFloor()
    {
        int shafts = 0, links = 0;
        foreach (var l in ManyLayouts(400))
        {
            var c = Count(l);
            if (l.Floor == 0) Assert.Equal(0, c.Shaft);
            shafts += c.Shaft;
            links += c.Corridor + c.Portal + c.Shaft;
        }
        Assert.InRange(shafts, 10, links / 8);
    }

    [Fact]
    public void SecretLinksStaySecretDoorsAndKeepTheirDoors()
    {
        foreach (var l in ManyLayouts())
            foreach (var link in l.Links)
                Assert.Equal(link.Secret, link.Kind == LinkKind.SecretDoor);
    }

    [Fact]
    public void LayoutsAreDeterministicAndKeepTheirRoomsMostOfTheTime()
    {
        var a = FloorLayout.Generate(7, 1, 30, 60, false);
        var b = FloorLayout.Generate(7, 1, 30, 60, false);
        Assert.True(a.Cells.SequenceEqual(b.Cells));
        Assert.True(a.Heights.SequenceEqual(b.Heights));
        Assert.Equal(a.Links, b.Links);
        Assert.Equal(a.Gaps.Select(g => (g.LinkIndex, g.Kind, g.Hazard, g.SpanLength)), b.Gaps.Select(g => (g.LinkIndex, g.Kind, g.Hazard, g.SpanLength)));
    }

    // ---- cells around the gaps ------------------------------------------------------------------

    [Fact]
    public void DoorsStandAtTheRoomEndOfEveryHallwayAndNothingSpawnsInTheGaps()
    {
        foreach (var (site, k, l) in AllFloors())
        {
            foreach (var gap in l.Gaps)
            {
                // Where a hallway meets a room, its first cell is a door.
                foreach (bool first in new[] { true, false })
                {
                    var hall = gap.Hall(first);
                    var (dx, dz) = gap.Toward(first);
                    var behind = (X: hall[0].X - dx, Z: hall[0].Z - dz);
                    if (l.RoomIdAt(behind.X, behind.Z) >= 0) Assert.Equal(CellKind.Door, l.CellAt(hall[0].X, hall[0].Z));
                }
            }

            var hallCells = new HashSet<(int, int)>();
            foreach (var gap in l.Gaps)
                foreach (var c in gap.HallFrom.Concat(gap.Span).Concat(gap.HallTo))
                    for (int lat = -1; lat <= 1; lat++)
                        hallCells.Add((c.X - gap.DirZ * lat, c.Z + gap.DirX * lat));
            foreach (var sp in site.Spawns.Where(s => s.Floor == k))
                Assert.DoesNotContain((sp.X - site.OriginX, sp.Z - site.OriginZ), hallCells);
        }
    }

    [Fact]
    public void RoomsKeepTheirShapeAndAreStillOneWholePiece()
    {
        foreach (var l in ManyLayouts())
            foreach (var room in l.Rooms)
            {
                int cells = 0, original = 0;
                for (int z = room.Bounds.Z; z < room.Bounds.Bottom; z++)
                    for (int x = room.Bounds.X; x < room.Bounds.Right; x++)
                    {
                        if (room.Contains(x, z)) original++;
                        if (l.RoomIdAt(x, z) == room.Id) cells++;
                    }
                Assert.True(cells >= original * 0.6, $"room {room.Id} ({room.Shape}) has only {cells} of {original} cells left");
                Assert.True(l.CellAt(room.CenterX, room.CenterZ) is CellKind.Floor, "a room's centre stays open floor");
            }
    }

    // ---- props, map and notes -----------------------------------------------------------------

    [Fact]
    public void EveryPitEdgeIsLitAndTorchesStayOnTheWalls()
    {
        foreach (var (site, k, l) in AllFloors().Take(80))
        {
            var torches = site.Torches.Where(t => t.Floor == k).ToList();
            foreach (var gap in l.Gaps)
                foreach (bool first in new[] { true, false })
                {
                    var edge = gap.Edge(first);
                    Assert.Contains(torches, t => t.RoomId == -1 && Math.Abs(t.CellX - edge.X) + Math.Abs(t.CellZ - edge.Z) <= 1);
                }
            foreach (var t in torches.Where(t => t.RoomId == -1))
            {
                Assert.Equal(CellKind.Floor, l.CellAt(t.CellX, t.CellZ));
                Assert.Equal(CellKind.Rock, l.CellAt(t.CellX - t.FaceX, t.CellZ - t.FaceZ));
                Assert.NotNull(l.GapAt(t.CellX, t.CellZ));
                // The flame sits above the hallway's own floor, which is up on the platform.
                Assert.Equal(DungeonSite.FloorY(k) + 1 + l.HeightAt(t.CellX, t.CellZ) + PropPlacer.Height, t.Y, 3);
            }
        }
    }

    [Fact]
    public void TheMinimapShowsHallwaysAndPitsAndSeesAcrossTheGap()
    {
        foreach (var (site, k, l) in AllFloors().Take(40))
            foreach (var gap in l.Gaps.Where(g => g.Kind == LinkKind.PortalGap))
            {
                var map = new FloorMap(site);
                var edge = gap.EdgeFrom;
                map.Reveal(k, site.OriginX + edge.X + 0.5, site.OriginZ + edge.Z + 0.5);
                Assert.Equal(MapCell.Hall, map.At(k, edge.X, edge.Z));
                Assert.Equal(MapCell.Pit, map.At(k, gap.Span[0].X, gap.Span[0].Z));
                // The far hallway is in line of sight across the pit, however long it is.
                Assert.True(map.IsSeen(k, gap.EdgeTo.X, gap.EdgeTo.Z), $"floor {k}: span {gap.SpanLength}: the far edge was not revealed");
            }
    }

    [Fact]
    public void NotesCountThePortalHopsAndStayReachable()
    {
        int behindGaps = 0;
        foreach (var (seed, site) in Sites.Value.Take(30))
        {
            var spots = NotePlacer.Place(site, seed);
            foreach (var spot in spots)
            {
                var hops = NotePlacer.HopsToRooms(site.Floors[spot.Floor]);
                Assert.True(hops.TryGetValue(spot.RoomId, out int n), "a note is in a room that cannot be reached");
                if (n > 0) behindGaps++;
            }
        }
        Assert.True(behindGaps > 5, $"only {behindGaps} notes lie behind a gap");
    }

    // ---- falling in, and the first hints --------------------------------------------------------

    private const double Dt = 1.0 / 60;

    private static (VoxelWorld World, DungeonSite Site, int Floor, GapSpec Gap) PitCase(GapHazard hazard, LinkKind kind = LinkKind.PortalGap)
    {
        for (long seed = 1; seed <= 60; seed++)
        {
            var world = new VoxelWorld(seed);
            for (int rx = -2; rx <= 1; rx++)
                for (int rz = -2; rz <= 1; rz++)
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is { } site)
                        for (int k = 0; k < site.FloorCount; k++)
                            foreach (var gap in site.Floors[k].Gaps.Where(g => g.Kind == kind && g.Hazard == hazard))
                                return (world, site, k, gap);
        }
        throw new InvalidOperationException($"no {kind} with {hazard} found");
    }

    private static PlayerView Standing(DungeonSite site, int floor, (int X, int Z) cell, bool airborne = false)
    {
        var feet = GapCrossing.FeetAt(site, floor, cell);
        return new PlayerView(feet, feet + new Vec3(0, 1.62, 0), new Vec3(0, 0, -1), airborne, false);
    }

    private static PlayerView InPit(DungeonSite site, int floor, GapSpec gap)
    {
        var mid = gap.Span[gap.Span.Count / 2];
        var feet = new Vec3(site.OriginX + mid.X + 0.5, DungeonSite.FloorY(floor) + 1, site.OriginZ + mid.Z + 0.5);
        return new PlayerView(feet, feet + new Vec3(0, 1.62, 0), new Vec3(0, 0, -1), true, false);
    }

    private static List<CombatEvent> Run(CombatSim sim, PlayerView view, double seconds)
    {
        var events = new List<CombatEvent>();
        for (int i = 0; i < (int)(seconds / Dt); i++)
        {
            sim.Tick(Dt, view, default);
            events.AddRange(sim.DrainEvents());
        }
        return events;
    }

    /// <summary>Holds the player in a pit until the sim says it has put them back (the host would move the body then), up to a few seconds.</summary>
    private static List<CombatEvent> Fall(CombatSim sim, PlayerView inPit)
    {
        var events = new List<CombatEvent>();
        for (int i = 0; i < 60 * 4 && !events.OfType<PlayerRecovered>().Any(); i++)
        {
            sim.Tick(Dt, inPit, default);
            events.AddRange(sim.DrainEvents());
        }
        return events;
    }

    [Theory]
    [InlineData(GapHazard.Bare)]
    [InlineData(GapHazard.Spikes)]
    [InlineData(GapHazard.Water)]
    public void FallingInCostsAModestAmountAndPutsYouBackOnYourSide(GapHazard hazard)
    {
        var (world, site, k, gap) = PitCase(hazard);
        var sim = new CombatSim(world, site);
        var safe = gap.Hall(true)[^4]; // a few cells back from the lip
        Run(sim, Standing(site, k, safe), 0.2);
        sim.DrainEvents();

        var events = Fall(sim, InPit(site, k, gap));
        var fell = Assert.Single(events.OfType<PlayerFell>());
        Assert.Equal(hazard, fell.Hazard);
        Assert.True(fell.First);
        Assert.Equal(CombatSim.FallDamage(hazard), fell.Damage);
        Assert.Equal(PlayerVitals.MaxHealth - fell.Damage, sim.Vitals.Health);

        // Put back on a walkable cell of the side the player came from, and not at the lip.
        var recovered = Assert.Single(events.OfType<PlayerRecovered>());
        var cell = ((int)Math.Floor(recovered.Position.X) - site.OriginX, (int)Math.Floor(recovered.Position.Z) - site.OriginZ);
        var layout = site.Floors[k];
        Assert.True(FloorLayout.IsWalkable(layout.CellAt(cell.Item1, cell.Item2)));
        Assert.True(gap.Within(gap.HallFrom, cell, 1), "recovered on the wrong side");
        Assert.Equal(DungeonSite.FloorY(k) + 1 + layout.HeightAt(cell.Item1, cell.Item2), recovered.Position.Y, 3);
        Assert.NotEqual(gap.EdgeFrom, cell);
        // Water is slow, stone and spikes are quick.
        Assert.Equal(hazard == GapHazard.Water, events.OfType<PlayerRecovered>().Count() == 1 && CombatSim.RecoverySeconds(hazard) > 1);
    }

    [Fact]
    public void AFallNeverKillsAndOnlyTheFirstOneIsFlagged()
    {
        var (world, site, k, gap) = PitCase(GapHazard.Spikes);
        var sim = new CombatSim(world, site);
        sim.Vitals.Damage(97); // three health left
        Run(sim, Standing(site, k, gap.Hall(true)[^4]), 0.2);
        var first = Fall(sim, InPit(site, k, gap));
        Assert.False(sim.Dead);
        Assert.Equal(1, sim.Vitals.Health);
        Assert.True(Assert.Single(first.OfType<PlayerFell>()).First);

        Run(sim, Standing(site, k, gap.Hall(true)[^4]), 0.2);
        var second = Fall(sim, InPit(site, k, gap));
        Assert.False(sim.Dead);
        Assert.False(Assert.Single(second.OfType<PlayerFell>()).First);
        Assert.Single(second.OfType<PlayerRecovered>());
    }

    [Fact]
    public void ClimbingOutBeforeTheTimerEndsCancelsTheRecovery()
    {
        var (world, site, k, gap) = PitCase(GapHazard.Water);
        var sim = new CombatSim(world, site);
        Run(sim, Standing(site, k, gap.Hall(true)[^4]), 0.2);
        Run(sim, InPit(site, k, gap), 0.5);
        var after = Run(sim, Standing(site, k, gap.EdgeTo), 2.5); // a portal carried them to the far lip
        Assert.Empty(after.OfType<PlayerRecovered>());
    }

    [Fact]
    public void WithNoSafePlaceRecordedThePlayerIsPutInThePitsOwnHallway()
    {
        var (world, site, k, gap) = PitCase(GapHazard.Bare);
        var sim = new CombatSim(world, site);
        var events = Fall(sim, InPit(site, k, gap));
        var recovered = Assert.Single(events.OfType<PlayerRecovered>());
        var cell = ((int)Math.Floor(recovered.Position.X) - site.OriginX, (int)Math.Floor(recovered.Position.Z) - site.OriginZ);
        Assert.True(FloorLayout.IsWalkable(site.Floors[k].CellAt(cell.Item1, cell.Item2)));
        Assert.True(gap.Within(gap.HallFrom, cell, 1) || gap.Within(gap.HallTo, cell, 1));
    }

    [Fact]
    public void EveryPitHasAWalkableRecoveryPositionForEveryCellOfIt()
    {
        foreach (var (site, k, l) in AllFloors().Take(30))
        {
            var world = new VoxelWorld(site.RegionX * 31 + site.RegionZ + 1);
            var sim = new CombatSim(world, site);
            foreach (var gap in l.Gaps.Where(g => g.Kind == LinkKind.PortalGap))
                foreach (var c in gap.Span)
                {
                    var feet = new Vec3(site.OriginX + c.X + 0.5, DungeonSite.FloorY(k) + 1, site.OriginZ + c.Z + 0.5);
                    var events = Fall(sim, new PlayerView(feet, feet + new Vec3(0, 1.62, 0), new Vec3(0, 0, 1), true, false));
                    var recovered = events.OfType<PlayerRecovered>().Single();
                    var cell = ((int)Math.Floor(recovered.Position.X) - site.OriginX, (int)Math.Floor(recovered.Position.Z) - site.OriginZ);
                    Assert.True(FloorLayout.IsWalkable(l.CellAt(cell.Item1, cell.Item2)), $"recovery {cell} is not walkable");
                    Assert.False(sim.Dead);
                    Run(sim, Standing(site, k, gap.Hall(true)[^4]), 0.7); // walk back to safety for the next one
                }
        }
    }

    [Fact]
    public void TheFirstTimeYouStandAtAPitEdgeIsAnnouncedOnce()
    {
        var (world, site, k, gap) = PitCase(GapHazard.Bare);
        var sim = new CombatSim(world, site);
        Run(sim, Standing(site, k, gap.Hall(true)[^3]), 0.2);
        Assert.Empty(sim.DrainEvents().OfType<PitEdgeReached>());
        var at = Run(sim, Standing(site, k, gap.EdgeFrom), 0.3);
        Assert.Equal(LinkKind.PortalGap, Assert.Single(at.OfType<PitEdgeReached>()).Kind);
        Assert.Empty(Run(sim, Standing(site, k, gap.EdgeTo), 0.3).OfType<PitEdgeReached>()); // once per run, not per gap
        sim.Respawn();
        Assert.Empty(Run(sim, Standing(site, k, gap.EdgeFrom), 0.3).OfType<PitEdgeReached>());
    }

    [Fact]
    public void EnemiesStopAtThePitEdgeAndNeverFallIn()
    {
        var (world, site, k, gap) = PitCase(GapHazard.Spikes);
        var sim = new CombatSim(world, site);
        var layout = site.Floors[k];
        // On the far lip, across the pit from the player, with nothing between them but the drop.
        var enemy = sim.SpawnEnemy(EnemyKind.Husk, GapCrossing.FeetAt(site, k, gap.EdgeTo), k);
        var player = Standing(site, k, gap.HallFrom[^3]);
        double lip = DungeonSite.FloorY(k) + 1 + LinkMix.Rise;
        for (int i = 0; i < 60 * 12; i++)
        {
            sim.Tick(Dt, player, default);
            var cell = ((int)Math.Floor(enemy.Feet.X) - site.OriginX, (int)Math.Floor(enemy.Feet.Z) - site.OriginZ);
            Assert.False(layout.CellAt(cell.Item1, cell.Item2) is CellKind.Pit or CellKind.PitSpikes or CellKind.PitWater, $"the enemy walked into the pit at tick {i}");
            Assert.True(enemy.Feet.Y > lip - 0.1, $"the enemy fell to y {enemy.Feet.Y:F2}");
        }
        // It ended up at the lip, as close to the player as it could get.
        Assert.True(gap.Within(gap.HallTo, ((int)Math.Floor(enemy.Feet.X) - site.OriginX, (int)Math.Floor(enemy.Feet.Z) - site.OriginZ), 1));
    }
}
