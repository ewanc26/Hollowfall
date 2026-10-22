using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class RiftDoorTests
{
    private static IEnumerable<FloorLayout> DeepLayouts()
    {
        for (int s = 1; s <= 120; s++)
        {
            int ex = 12 + (s * 7) % 73, ez = 12 + (s * 13) % 73;
            yield return FloorLayout.Generate(s, 2 + s % 2, ex, ez, lastFloor: false);
        }
    }

    [Fact]
    public void ShallowFloorsNeverHaveRiftDoors()
    {
        for (int s = 1; s <= 120; s++)
        {
            int ex = 12 + (s * 7) % 73, ez = 12 + (s * 13) % 73;
            Assert.Empty(FloorLayout.Generate(s, s % 2, ex, ez, lastFloor: false).RiftDoors);
        }
    }

    [Fact]
    public void DeepFloorsSometimesHaveRiftDoors()
    {
        Assert.Contains(DeepLayouts(), l => l.RiftDoors.Count > 0);
    }

    [Fact]
    public void EachDoorJoinsTwoRoomsThatAreNotLinked()
    {
        foreach (var l in DeepLayouts())
            foreach (var (a, b) in l.RiftDoors)
            {
                int ra = l.RoomIdAt(a.X + a.NormalX, a.Z + a.NormalZ), rb = l.RoomIdAt(b.X + b.NormalX, b.Z + b.NormalZ);
                Assert.True(ra >= 0 && rb >= 0 && ra != rb, $"floor {l.Floor}: door opens onto rooms {ra} and {rb}");
                Assert.DoesNotContain(l.Links, k => (k.A == ra && k.B == rb) || (k.A == rb && k.B == ra));
            }
    }

    [Fact]
    public void EachOpeningIsAWallFacingAnOpenRoomCell()
    {
        foreach (var l in DeepLayouts())
            foreach (var (a, b) in l.RiftDoors)
                foreach (var d in new[] { a, b })
                {
                    Assert.Equal(CellKind.Rock, l.CellAt(d.X, d.Z));
                    Assert.True(l.IsWallCell(d.X, d.Z));
                    Assert.Equal(CellKind.Floor, l.CellAt(d.X + d.NormalX, d.Z + d.NormalZ));
                }
    }

    [Fact]
    public void DoorsJoinRoomsFarApart()
    {
        foreach (var l in DeepLayouts())
            foreach (var (a, b) in l.RiftDoors)
                Assert.True(Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z) >= 30);
    }

    [Fact]
    public void TheWorldHasObsidianWhereEachOpeningIs()
    {
        int checkedDoors = 0;
        for (int s = 1; s <= 40; s++)
        {
            var site = new DungeonSite(s, 0, 0, 0, 0, 40, 40, 64, 4);
            for (int floor = 2; floor < site.FloorCount; floor++)
                foreach (var (a, b) in site.Floors[floor].RiftDoors)
                    foreach (var d in new[] { a, b })
                    {
                        int wx = d.X, wz = d.Z, y = DungeonSite.FloorY(floor) + 1;
                        Assert.True(site.BlockAt(wx, y, wz) is BlockType.Obsidian or BlockType.RiftPlate);
                        Assert.True(site.BlockAt(wx, y + 1, wz) is BlockType.Obsidian or BlockType.RiftPlate);
                        checkedDoors++;
                    }
        }
        Assert.True(checkedDoors > 0);
    }

    [Fact]
    public void AFixedPairSendsABodyFromOneRoomToTheOther()
    {
        var a = PortalSystem.Fixed(10, 5, 10, (1, 0, 0));
        var b = PortalSystem.Fixed(30, 5, 10, (-1, 0, 0));
        var portals = new PortalSystem();
        portals.SetFixed([(a, b)]);

        var v = new Vec3(-1, 0, 0);
        var crossing = portals.TryCross(new Vec3(12, 6, 10.5), new Vec3(10.5, 6, 10.5), v, v);
        Assert.NotNull(crossing);
        Assert.Same(a, crossing!.Value.Entered);
        Assert.Same(b, crossing.Value.Exited);
        Assert.True(b.Frame.Normal.Dot(crossing.Value.Position - b.Frame.Center) > 0, "comes out on the open side of the far door");
    }

    [Fact]
    public void RiftOpeningsSurviveClearAndAreHoles()
    {
        var a = PortalSystem.Fixed(10, 5, 10, (1, 0, 0));
        var b = PortalSystem.Fixed(30, 5, 10, (-1, 0, 0));
        var portals = new PortalSystem();
        portals.SetFixed([(a, b)]);
        portals.Clear();

        Assert.Equal(2, portals.Openings.Count());
        Assert.Same(b, portals.Other(a));
        var view = portals.WithHoles((x, y, z) => BlockType.Stone);
        Assert.Equal(BlockType.Air, view(10, 5, 10));
        Assert.Equal(BlockType.Air, view(10, 6, 10));
        Assert.Equal(BlockType.Stone, view(10, 7, 10));
    }

    [Fact]
    public void SettingNoFixedPairsLeavesOnlyThePlacedPortals()
    {
        var portals = new PortalSystem();
        portals.SetFixed([]);
        Assert.Empty(portals.Openings);
        Assert.Null(portals.Other(PortalSystem.Fixed(10, 5, 10, (1, 0, 0))));
    }
}
