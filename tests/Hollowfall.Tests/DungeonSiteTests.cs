using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class DungeonSiteTests
{
    private static readonly Lazy<List<(long Seed, DungeonSite Site)>> Sites = new(() =>
    {
        var list = new List<(long, DungeonSite)>();
        for (long seed = 1; seed <= 12; seed++)
        {
            var g = new TerrainGenerator(seed);
            for (int rx = -2; rx <= 1; rx++)
                for (int rz = -2; rz <= 1; rz++)
                    if (g.Dungeons!.GetSite(rx, rz) is { } s)
                        list.Add((seed, s));
        }
        return list;
    });

    private static BlockType Solid(DungeonSite site, int x, int y, int z) => site.BlockAt(x, y, z) ?? BlockType.Stone;

    private static bool CanStand(DungeonSite site, int x, int y, int z) =>
        !BlockInfo.IsSolid(Solid(site, x, y, z)) && !BlockInfo.IsSolid(Solid(site, x, y + 1, z)) && BlockInfo.IsSolid(Solid(site, x, y - 1, z));

    /// <summary>Everything a player could walk to from a start position: 4-way moves, up or down one block.</summary>
    private static HashSet<(int X, int Y, int Z)> Walk(DungeonSite site, (int X, int Y, int Z) start)
    {
        var seen = new HashSet<(int, int, int)> { start };
        var stack = new Stack<(int X, int Y, int Z)>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var (x, y, z) = stack.Pop();
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                foreach (int dy in new[] { 0, 1, -1 })
                {
                    var n = (x + dx, y + dy, z + dz);
                    if (n.Item1 < site.OriginX || n.Item1 >= site.OriginX + FloorLayout.Size ||
                        n.Item3 < site.OriginZ || n.Item3 >= site.OriginZ + FloorLayout.Size) continue;
                    if (seen.Contains(n) || !CanStand(site, n.Item1, n.Item2, n.Item3)) continue;
                    seen.Add(n);
                    stack.Push(n);
                }
        }
        return seen;
    }

    private static bool InShaft(FloorLayout f, int x, int z) =>
        (Math.Abs(x - f.EntranceX) <= 1 && Math.Abs(z - f.EntranceZ) <= 1) ||
        (f.ExitStairs is { } e && Math.Abs(x - e.X) <= 1 && Math.Abs(z - e.Z) <= 1);

    [Fact]
    public void WorldsContainAReasonableNumberOfDungeons()
    {
        Assert.True(Sites.Value.Count >= 30, $"only {Sites.Value.Count} dungeons across 12 seeds x 16 regions");
        Assert.Contains(Sites.Value, s => s.Site.FloorCount == 2);
        Assert.Contains(Sites.Value, s => s.Site.FloorCount == 4);
    }

    [Fact]
    public void DungeonsAppearInRoughlyAFifthOfRegions()
    {
        int regions = 0, sites = 0;
        foreach (long seed in new long[] { 1, 2, 3, 4 })
        {
            var map = new TerrainGenerator(seed).Dungeons!;
            for (int rx = -4; rx < 4; rx++)
                for (int rz = -4; rz < 4; rz++, regions++)
                    if (map.GetSite(rx, rz) != null) sites++;
        }
        Assert.InRange(sites / (double)regions, 0.10, 0.35);
    }

    [Fact]
    public void ParallelGenerationMatchesSequentialGeneration()
    {
        var (seed, site) = Sites.Value[0];
        int cx0 = (site.OriginX >> 4) - 1, cz0 = (site.OriginZ >> 4) - 1;
        var coords = Enumerable.Range(0, 64).Select(i => (X: cx0 + i % 8, Z: cz0 + i / 8)).ToArray();

        var sequential = new TerrainGenerator(seed);
        var expected = coords.Select(c => TestHelpers.Checksum(sequential.GenerateChunk(c.X, c.Z))).ToArray();

        // A fresh generator, so the lazy dungeon cache is first filled by racing threads.
        var parallel = new TerrainGenerator(seed);
        var actual = new ulong[coords.Length];
        Parallel.For(0, coords.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 },
            i => actual[i] = TestHelpers.Checksum(parallel.GenerateChunk(coords[i].X, coords[i].Z)));

        Assert.Equal(expected, actual);
        Assert.Same(parallel.Dungeons!.GetSite(site.RegionX, site.RegionZ), parallel.Dungeons.GetSite(site.RegionX, site.RegionZ));
    }

    [Fact]
    public void SitesAreDeterministicAndCached()
    {
        var a = new TerrainGenerator(5).Dungeons!;
        var b = new TerrainGenerator(5).Dungeons!;
        for (int rx = -3; rx <= 2; rx++)
            for (int rz = -3; rz <= 2; rz++)
            {
                var sa = a.GetSite(rx, rz);
                var sb = b.GetSite(rx, rz);
                Assert.Equal(sa == null, sb == null);
                if (sa == null) continue;
                Assert.Equal((sa.OriginX, sa.OriginZ, sa.EntranceX, sa.EntranceZ, sa.SurfaceY, sa.FloorCount),
                    (sb!.OriginX, sb.OriginZ, sb.EntranceX, sb.EntranceZ, sb.SurfaceY, sb.FloorCount));
                Assert.Same(sa, a.GetSite(rx, rz));
                Assert.True(sa.Floors.SelectMany((f, i) => f.Cells.ToArray()).SequenceEqual(sb.Floors.SelectMany(f => f.Cells.ToArray())));
            }
    }

    [Fact]
    public void FootprintsStayInsideTheirRegionAndOnSolidGround()
    {
        foreach (var (seed, s) in Sites.Value)
        {
            var g = new TerrainGenerator(seed);
            Assert.InRange(s.OriginX, s.RegionX * DungeonMap.RegionSize, (s.RegionX + 1) * DungeonMap.RegionSize - FloorLayout.Size);
            Assert.InRange(s.OriginZ, s.RegionZ * DungeonMap.RegionSize, (s.RegionZ + 1) * DungeonMap.RegionSize - FloorLayout.Size);
            Assert.InRange(s.SurfaceY, TerrainGenerator.SeaLevel + 3, DungeonMap.MaxEntranceY);
            Assert.Equal(g.SampleColumn(s.EntranceX, s.EntranceZ).Height, s.SurfaceY);
            Assert.True(s.LowestY >= 6);
        }
    }

    [Fact]
    public void FloorsAreChainedThroughStairs()
    {
        foreach (var (_, s) in Sites.Value)
            for (int k = 0; k + 1 < s.FloorCount; k++)
            {
                var exit = s.Floors[k].ExitStairs!.Value;
                Assert.Equal(exit, (s.Floors[k + 1].EntranceX, s.Floors[k + 1].EntranceZ));
            }
    }

    [Fact]
    public void BlockAtIsPureAndNullOutsideTheFootprint()
    {
        var (_, s) = Sites.Value[0];
        Assert.Null(s.BlockAt(s.OriginX - 1, 40, s.OriginZ + 5));
        Assert.Null(s.BlockAt(s.OriginX + FloorLayout.Size, 40, s.OriginZ + 5));
        for (int i = 0; i < 2000; i++)
        {
            int x = s.OriginX + i % 96, z = s.OriginZ + (i * 7) % 96, y = (i * 13) % 100;
            Assert.Equal(s.BlockAt(x, y, z), s.BlockAt(x, y, z));
        }
    }

    [Fact]
    public void ChunksAgreeWithBlockAtAcrossBorders()
    {
        var (seed, s) = Sites.Value[0];
        var g = new TerrainGenerator(seed);
        int cx0 = (int)Math.Floor(s.EntranceX / 16.0) - 1, cz0 = (int)Math.Floor(s.EntranceZ / 16.0) - 1;
        for (int cx = cx0; cx <= cx0 + 2; cx++)
            for (int cz = cz0; cz <= cz0 + 2; cz++)
            {
                var chunk = g.GenerateChunk(cx, cz);
                for (int x = 0; x < 16; x++)
                    for (int z = 0; z < 16; z++)
                        for (int y = 0; y < Chunk.Height; y++)
                            if (s.BlockAt(chunk.WorldMinX + x, y, chunk.WorldMinZ + z) is { } expected)
                                Assert.Equal(expected, chunk[x, y, z]);
            }
    }

    [Fact]
    public void DungeonsOnlyChangeChunksInsideTheirFootprint()
    {
        var (seed, s) = Sites.Value[0];
        var with = new TerrainGenerator(seed);
        var without = new TerrainGenerator(seed, enableDungeons: false);
        int outside = (int)Math.Floor((s.OriginX - 40) / 16.0);
        int cz = (int)Math.Floor(s.EntranceZ / 16.0);
        Assert.True(with.GenerateChunk(outside, cz).RawBlocks.SequenceEqual(without.GenerateChunk(outside, cz).RawBlocks));

        int inside = (int)Math.Floor(s.EntranceX / 16.0);
        Assert.False(with.GenerateChunk(inside, cz).RawBlocks.SequenceEqual(without.GenerateChunk(inside, cz).RawBlocks));
    }

    [Fact]
    public void PlayerCanWalkFromTheSurfaceToTheBossOnFoot()
    {
        foreach (var (_, s) in Sites.Value.Take(25))
        {
            var start = (s.EntranceX + DungeonSite.PlazaRadius, s.SurfaceY + 1, s.EntranceZ);
            Assert.True(CanStand(s, start.Item1, start.Item2, start.Item3), "plaza edge must be standable");

            var reach = Walk(s, start);
            for (int k = 0; k < s.FloorCount; k++)
            {
                var floor = s.Floors[k];
                foreach (var room in floor.Rooms)
                {
                    var (wx, wy, wz) = s.ToWorld(k, room.CenterX, room.CenterZ);
                    if (floor.CellAt(room.CenterX, room.CenterZ) != CellKind.Floor) continue;
                    // A room's centre can fall inside a stair shaft; its steps are solid, so it is not a standing spot.
                    if (InShaft(floor, room.CenterX, room.CenterZ)) continue;
                    Assert.True(reach.Contains((wx, wy, wz)), $"floor {k} room {room.Id} ({room.Role}) cannot be reached on foot");
                }
            }
            var boss = s.Floors[^1].BossRoom!;
            var (bx, by, bz) = s.ToWorld(s.FloorCount - 1, boss.CenterX, boss.CenterZ);
            Assert.Contains((bx, by, bz), reach);
        }
    }

    [Fact]
    public void SecretDoorsBlockWalkingButDoorsDoNot()
    {
        Assert.True(BlockInfo.IsSolid(BlockType.SecretDoor));
        Assert.False(BlockInfo.IsSolid(BlockType.Door));
        Assert.True(BlockInfo.IsSolid(BlockType.Stairs));
        Assert.True(BlockInfo.IsSolid(BlockType.Pillar));
    }

    [Fact]
    public void RoomsAreSealedFromCaves()
    {
        // Every open interior voxel must be enclosed by dungeon blocks: no neighbour may be a terrain-owned void.
        foreach (var (_, s) in Sites.Value.Take(8))
            for (int k = 0; k < s.FloorCount; k++)
            {
                int y = DungeonSite.FloorY(k) + 2;
                for (int lx = 1; lx < FloorLayout.Size - 1; lx++)
                    for (int lz = 1; lz < FloorLayout.Size - 1; lz++)
                    {
                        if (s.BlockAt(s.OriginX + lx, y, s.OriginZ + lz) != BlockType.Air) continue;
                        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                            Assert.NotNull(s.BlockAt(s.OriginX + lx + dx, y, s.OriginZ + lz + dz));
                        Assert.NotNull(s.BlockAt(s.OriginX + lx, DungeonSite.FloorY(k), s.OriginZ + lz));
                        Assert.NotNull(s.BlockAt(s.OriginX + lx, DungeonSite.FloorY(k) + 6, s.OriginZ + lz));
                    }
            }
    }
}
