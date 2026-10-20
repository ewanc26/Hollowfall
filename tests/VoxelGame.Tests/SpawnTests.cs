using VoxelGame.Core.Dungeon;
using VoxelGame.Core.Terrain;
using VoxelGame.Core.World;

namespace VoxelGame.Tests;

public class SpawnTests
{
    private static IEnumerable<(long Seed, DungeonSite Site)> Sites()
    {
        for (long seed = 1; seed <= 8; seed++)
        {
            var g = new TerrainGenerator(seed);
            for (int rx = -2; rx <= 1; rx++)
                for (int rz = -2; rz <= 1; rz++)
                    if (g.Dungeons!.GetSite(rx, rz) is { } s)
                        yield return (seed, s);
        }
    }

    [Fact]
    public void SpawnsAreDeterministic()
    {
        var site = Sites().First();
        var again = new TerrainGenerator(site.Seed).Dungeons!.GetSite(site.Site.RegionX, site.Site.RegionZ)!;
        Assert.NotEmpty(site.Site.Spawns);
        Assert.Equal(site.Site.Spawns, again.Spawns);
    }

    [Fact]
    public void EverySpawnStandsOnOpenFloorInsideItsRoom()
    {
        foreach (var (_, s) in Sites())
            foreach (var sp in s.Spawns)
            {
                var floor = s.Floors[sp.Floor];
                int lx = sp.X - s.OriginX, lz = sp.Z - s.OriginZ;
                Assert.Equal(CellKind.Floor, floor.CellAt(lx, lz));
                Assert.Equal(sp.RoomId, floor.RoomIdAt(lx, lz));
                Assert.Equal(DungeonSite.FloorY(sp.Floor) + 1, sp.Y);
                Assert.Equal(BlockType.Air, s.BlockAt(sp.X, sp.Y, sp.Z));
                Assert.Equal(BlockType.BrickFloor, s.BlockAt(sp.X, sp.Y - 1, sp.Z));
            }
    }

    [Fact]
    public void SpawnsNeverSitOnStairsOrShareACell()
    {
        foreach (var (_, s) in Sites())
        {
            Assert.Equal(s.Spawns.Count, s.Spawns.Select(sp => (sp.Floor, sp.X, sp.Z)).Distinct().Count());
            foreach (var sp in s.Spawns)
            {
                var f = s.Floors[sp.Floor];
                int lx = sp.X - s.OriginX, lz = sp.Z - s.OriginZ;
                Assert.True(Math.Abs(lx - f.EntranceX) > 3 || Math.Abs(lz - f.EntranceZ) > 3);
                if (f.ExitStairs is { } e) Assert.True(Math.Abs(lx - e.X) > 3 || Math.Abs(lz - e.Z) > 3);
            }
        }
    }

    [Fact]
    public void RoomRolesDecideWhatSpawns()
    {
        foreach (var (_, s) in Sites())
        {
            var roleOf = (Spawn sp) => s.Floors[sp.Floor].Rooms[sp.RoomId].Role;
            foreach (var sp in s.Spawns)
            {
                var role = roleOf(sp);
                Assert.NotEqual(RoomRole.Entrance, role);
                Assert.NotEqual(RoomRole.Safe, role);
                if (sp.Kind == SpawnKind.Boss) Assert.Equal(RoomRole.Boss, role);
                if (sp.Kind == SpawnKind.Trap) Assert.Equal(RoomRole.Trap, role);
            }
            Assert.Single(s.Spawns, sp => sp.Kind == SpawnKind.Boss);
            Assert.True(s.Spawns.Count(sp => sp.Kind == SpawnKind.Boss && sp.Floor == s.FloorCount - 1) == 1);
        }
    }

    [Fact]
    public void ThereIsOneKeyPerFloorWithAnExitAndItIsAwayFromTheEntrance()
    {
        foreach (var (_, s) in Sites())
        {
            var keys = s.Spawns.Where(sp => sp.Kind == SpawnKind.Key).ToList();
            Assert.True(keys.Count <= s.FloorCount - 1);
            Assert.Equal(keys.Count, keys.Select(k => k.Floor).Distinct().Count());
            foreach (var k in keys)
            {
                Assert.True(k.Floor < s.FloorCount - 1, "the last floor has no exit to unlock");
                Assert.True(s.Floors[k.Floor].Rooms[k.RoomId].Depth >= 1);
            }
        }
    }

    [Fact]
    public void DifficultyRisesWithDepth()
    {
        var byFloor = new Dictionary<int, List<int>>();
        foreach (var (_, s) in Sites())
            foreach (var sp in s.Spawns.Where(sp => sp.Kind == SpawnKind.Monster))
            {
                Assert.True(sp.Tier >= sp.Floor + 1);
                if (!byFloor.TryGetValue(sp.Floor, out var l)) byFloor[sp.Floor] = l = [];
                l.Add(sp.Tier);
            }
        Assert.True(byFloor[0].Average() < byFloor[1].Average());
        Assert.True(byFloor[1].Average() < byFloor[2].Average());
    }

    [Fact]
    public void DeeperFloorsHaveMoreMonstersPerRoom()
    {
        double Density(int floor)
        {
            int monsters = 0, rooms = 0;
            foreach (var (_, s) in Sites().Where(x => x.Site.FloorCount > floor))
            {
                monsters += s.Spawns.Count(sp => sp.Floor == floor && sp.Kind == SpawnKind.Monster);
                rooms += s.Floors[floor].Rooms.Count;
            }
            return monsters / (double)rooms;
        }
        Assert.True(Density(2) > Density(0));
    }
}
