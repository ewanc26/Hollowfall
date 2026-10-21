using Hollowfall.Core.Dungeon;

namespace Hollowfall.Tests;

public class FloorLayoutTests
{
    private static FloorLayout Make(long seed, int floor = 0, int ex = 48, int ez = 48, bool last = false) =>
        FloorLayout.Generate(seed, floor, ex, ez, last);

    private static IEnumerable<FloorLayout> ManyLayouts(int count = 200)
    {
        for (int s = 0; s < count; s++)
        {
            int ex = 12 + (s * 7) % 73, ez = 12 + (s * 13) % 73;
            yield return Make(s, s % 4, ex, ez, last: s % 4 == 3);
        }
    }

    /// <summary>4-connected flood fill over cells accepted by <paramref name="passable"/>.</summary>
    private static HashSet<(int, int)> Flood(FloorLayout l, int sx, int sz, Func<CellKind, bool> passable)
    {
        var seen = new HashSet<(int, int)>();
        var stack = new Stack<(int, int)>();
        stack.Push((sx, sz));
        seen.Add((sx, sz));
        while (stack.Count > 0)
        {
            var (x, z) = stack.Pop();
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, nz = z + dz;
                if (seen.Contains((nx, nz)) || !passable(l.CellAt(nx, nz))) continue;
                seen.Add((nx, nz));
                stack.Push((nx, nz));
            }
        }
        return seen;
    }

    [Fact]
    public void SameInputsGiveIdenticalLayouts()
    {
        var a = Make(42, 1, 30, 60);
        var b = Make(42, 1, 30, 60);
        Assert.True(a.Cells.SequenceEqual(b.Cells));
        Assert.Equal(a.Rooms.Select(r => (r.Bounds, r.Shape, r.Role)), b.Rooms.Select(r => (r.Bounds, r.Shape, r.Role)));
        Assert.Equal(a.Links, b.Links);
    }

    [Fact]
    public void DifferentSeedsAndFloorsGiveDifferentLayouts()
    {
        Assert.False(Make(1).Cells.SequenceEqual(Make(2).Cells));
        Assert.False(Make(1, 0).Cells.SequenceEqual(Make(1, 1).Cells));
    }

    [Fact]
    public void LayoutsHaveEnoughVariedRooms()
    {
        var shapes = new HashSet<RoomShape>();
        foreach (var l in ManyLayouts())
        {
            Assert.InRange(l.Rooms.Count, 4, 20);
            foreach (var r in l.Rooms) shapes.Add(r.Shape);
        }
        Assert.Equal(4, shapes.Count);
    }

    [Fact]
    public void RoomsNeverOverlapAndStayInsideTheFloor()
    {
        foreach (var l in ManyLayouts())
        {
            var owner = new Dictionary<(int, int), int>();
            foreach (var r in l.Rooms)
            {
                Assert.InRange(r.Bounds.X, 1, FloorLayout.Size);
                Assert.InRange(r.Bounds.Right, 1, FloorLayout.Size - 1);
                Assert.InRange(r.Bounds.Bottom, 1, FloorLayout.Size - 1);
                for (int z = r.Bounds.Z; z < r.Bounds.Bottom; z++)
                    for (int x = r.Bounds.X; x < r.Bounds.Right; x++)
                        if (r.Contains(x, z))
                            Assert.True(owner.TryAdd((x, z), r.Id), $"rooms {owner[(x, z)]} and {r.Id} overlap");
            }
        }
    }

    [Fact]
    public void EveryRoomIsReachableWithoutSecretDoors()
    {
        // Walking and crossing gaps. That a gap can really be crossed is proved in MixedLinkTests, with the real portals.
        foreach (var l in ManyLayouts())
        {
            var reach = LayoutWalk.Reach(l, l.EntranceX, l.EntranceZ, crossGaps: true);
            foreach (var r in l.Rooms)
                Assert.True(reach.Contains((r.CenterX, r.CenterZ)), $"room {r.Id} ({r.Shape}) unreachable");
        }
    }

    [Fact]
    public void RoomCellsAreConnectedInsideEachRoom()
    {
        foreach (var l in ManyLayouts(120))
            foreach (var r in l.Rooms)
            {
                var inside = Flood(l, r.CenterX, r.CenterZ, k => FloorLayout.IsWalkable(k) || k == CellKind.Pillar)
                    .Where(c => l.RoomIdAt(c.Item1, c.Item2) == r.Id).Count();
                int total = 0;
                for (int z = r.Bounds.Z; z < r.Bounds.Bottom; z++)
                    for (int x = r.Bounds.X; x < r.Bounds.Right; x++)
                        if (l.RoomIdAt(x, z) == r.Id) total++;
                Assert.True(inside >= total * 0.9, $"{r.Shape} room {r.Id} is fragmented ({inside}/{total})");
            }
    }

    [Fact]
    public void RoomGraphIsConnectedByNonSecretLinksAlone()
    {
        foreach (var l in ManyLayouts())
        {
            var seen = new HashSet<int> { l.EntranceRoom.Id };
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var link in l.Links.Where(x => !x.Secret))
                {
                    if (seen.Contains(link.A) && seen.Add(link.B)) grew = true;
                    if (seen.Contains(link.B) && seen.Add(link.A)) grew = true;
                }
            }
            Assert.Equal(l.Rooms.Count, seen.Count);
        }
    }

    [Fact]
    public void RolesFollowTheRules()
    {
        foreach (var l in ManyLayouts())
        {
            Assert.Single(l.Rooms, r => r.Role == RoomRole.Entrance);
            Assert.Equal(0, l.EntranceRoom.Depth);
            if (l.IsLastFloor)
            {
                Assert.Single(l.Rooms, r => r.Role == RoomRole.Boss);
                Assert.Null(l.ExitStairs);
                Assert.DoesNotContain(l.Rooms, r => r.Role == RoomRole.Exit);
                Assert.Equal(l.Rooms.Max(r => r.Depth), l.BossRoom!.Depth);
            }
            else
            {
                Assert.Single(l.Rooms, r => r.Role == RoomRole.Exit);
                Assert.DoesNotContain(l.Rooms, r => r.Role == RoomRole.Boss);
                Assert.Equal((l.ExitRoom!.CenterX, l.ExitRoom.CenterZ), l.ExitStairs);
                Assert.Equal(l.Rooms.Max(r => r.Depth), l.ExitRoom.Depth);
            }
            Assert.True(l.Rooms.Count(r => r.Role == RoomRole.Treasure) <= 2);
        }
    }

    [Fact]
    public void SecretLinksAreExtraLoopsHiddenBehindSecretDoors()
    {
        int secretLayouts = 0;
        foreach (var l in ManyLayouts())
        {
            if (l.Links.Any(x => x.Secret)) secretLayouts++;
            if (!l.Links.Any(x => x.Secret)) continue;
            Assert.Contains(l.Cells.ToArray(), c => c == CellKind.SecretDoor);
        }
        Assert.True(secretLayouts > 10, $"only {secretLayouts} layouts had secrets");
    }

    [Fact]
    public void DoorsSitBetweenTwoWalkableCells()
    {
        foreach (var l in ManyLayouts(80))
            for (int z = 1; z < FloorLayout.Size - 1; z++)
                for (int x = 1; x < FloorLayout.Size - 1; x++)
                {
                    var k = l.CellAt(x, z);
                    if (k is not (CellKind.Door or CellKind.SecretDoor)) continue;
                    bool ns = FloorLayout.IsOpen(l.CellAt(x, z - 1)) && FloorLayout.IsOpen(l.CellAt(x, z + 1));
                    bool ew = FloorLayout.IsOpen(l.CellAt(x - 1, z)) && FloorLayout.IsOpen(l.CellAt(x + 1, z));
                    Assert.True(ns || ew, $"door at {x},{z} does not link two open cells");
                }
    }

    [Fact]
    public void EntranceAndExitStairAreasAreClear()
    {
        foreach (var l in ManyLayouts())
        {
            var anchors = new List<(int, int)> { (l.EntranceX, l.EntranceZ) };
            if (l.ExitStairs is { } e) anchors.Add(e);
            foreach (var (ax, az) in anchors)
                for (int dx = -2; dx <= 2; dx++)
                    for (int dz = -2; dz <= 2; dz++)
                        Assert.Equal(CellKind.Floor, l.CellAt(ax + dx, az + dz));
        }
    }

    [Fact]
    public void PillarsAreInsideRoomsAndNeverBlockAnything()
    {
        foreach (var l in ManyLayouts())
            for (int z = 0; z < FloorLayout.Size; z++)
                for (int x = 0; x < FloorLayout.Size; x++)
                    if (l.CellAt(x, z) == CellKind.Pillar)
                    {
                        Assert.True(l.RoomIdAt(x, z) >= 0);
                        Assert.Equal(CellKind.Floor, l.CellAt(x + 1, z));
                        Assert.Equal(CellKind.Floor, l.CellAt(x - 1, z));
                    }
    }

    [Fact]
    public void EntranceTooCloseToTheEdgeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(1, 0, 3, 48));
    }
}
