using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class CollapseSimTests
{
    private const double Dt = 1.0 / 60;

    private sealed record Upper(VoxelWorld World, DungeonSite Site, CombatSim Sim, List<Room> Arenas);

    /// <summary>The first dungeon with an upper floor that has at least two encounter rooms.</summary>
    private static Upper FindUpperFloorWithArenas()
    {
        for (long seed = 1; seed < 60; seed++)
        {
            var world = new VoxelWorld(seed);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                {
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is not { } site || site.FloorCount < 2) continue;
                    var ids = site.Spawns.Where(s => s.Floor == 0 && s.Kind is SpawnKind.Monster or SpawnKind.Boss)
                        .Select(s => s.RoomId).Distinct().ToList();
                    if (ids.Count < 2) continue;
                    var arenas = ids.Select(id => site.Floors[0].Rooms.First(r => r.Id == id)).ToList();
                    return new Upper(world, site, new CombatSim(world, site), arenas);
                }
        }
        throw new InvalidOperationException("no upper floor with two arenas");
    }

    private static PlayerView StandIn(DungeonSite site, Room room)
    {
        var f = site.Floors[0];
        for (int r = 0; r < 6; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                    if (f.CellAt(room.CenterX + dx, room.CenterZ + dz) == CellKind.Floor && f.RoomIdAt(room.CenterX + dx, room.CenterZ + dz) == room.Id)
                    {
                        var (wx, wy, wz) = site.ToWorld(0, room.CenterX + dx, room.CenterZ + dz);
                        var feet = new Vec3(wx + 0.5, wy, wz + 0.5);
                        return new PlayerView(feet, feet + new Vec3(0, 1.62, 0), new Vec3(1, 0, 0), false, false);
                    }
        throw new InvalidOperationException("no standing spot");
    }

    /// <summary>Stands in the arena until it spawns, then kills everything in it.</summary>
    private static void ClearArena(CombatSim sim, PlayerView view)
    {
        for (int i = 0; i < 120 && sim.Enemies.Count == 0; i++) sim.Tick(Dt, view, default);
        foreach (var e in sim.Enemies.ToList()) e.SetHealth(0.01);
        for (int i = 0; i < 2000 && sim.Enemies.Any(e => !e.Dead); i++)
        {
            var target = sim.Enemies.First(e => !e.Dead);
            var dir = target.Feet + new Vec3(0, 1.0, 0) - view.Eye;
            sim.Tick(Dt, view with { Forward = dir.Normalized() }, new PlayerActions(i % 20 == 0, false, false));
        }
    }

    private static int BricksIn(VoxelWorld world, DungeonSite site, Room room)
    {
        int count = 0, y = DungeonSite.FloorY(0);
        for (int x = room.Bounds.X; x < room.Bounds.X + room.Bounds.W; x++)
            for (int z = room.Bounds.Z; z < room.Bounds.Z + room.Bounds.H; z++)
                if (world.GetBlock(site.OriginX + x, y, site.OriginZ + z) == BlockType.BrickFloor) count++;
        return count;
    }

    [Fact]
    public void ClearingTheLastArenaGivesTheFloorAway()
    {
        var u = FindUpperFloorWithArenas();
        foreach (var arena in u.Arenas.Take(u.Arenas.Count - 1)) ClearArena(u.Sim, StandIn(u.Site, arena));

        var last = u.Arenas[^1];
        var view = StandIn(u.Site, last);
        int before = BricksIn(u.World, u.Site, last);
        ClearArena(u.Sim, view);
        Assert.Contains(u.Sim.DrainEvents(), e => e is EncounterCleared);

        for (int i = 0; i < 180; i++) u.Sim.Tick(Dt, view, default);
        Assert.True(BricksIn(u.World, u.Site, last) < before, "the arena's floor should have started to fall away");
    }

    [Fact]
    public void RespawnPutsTheCollapsedFloorBack()
    {
        var u = FindUpperFloorWithArenas();
        foreach (var arena in u.Arenas.Take(u.Arenas.Count - 1)) ClearArena(u.Sim, StandIn(u.Site, arena));

        var last = u.Arenas[^1];
        var view = StandIn(u.Site, last);
        int before = BricksIn(u.World, u.Site, last);
        ClearArena(u.Sim, view);
        for (int i = 0; i < 180; i++) u.Sim.Tick(Dt, view, default);
        Assert.True(BricksIn(u.World, u.Site, last) < before);

        u.Sim.Respawn();
        Assert.Equal(before, BricksIn(u.World, u.Site, last));
    }

    [Fact]
    public void NothingCollapsesWhileAnArenaIsStillStanding()
    {
        var u = FindUpperFloorWithArenas();
        foreach (var arena in u.Arenas.Take(u.Arenas.Count - 1)) ClearArena(u.Sim, StandIn(u.Site, arena));

        var last = u.Arenas[^1];
        var view = StandIn(u.Site, last);
        for (int i = 0; i < 120 && u.Sim.Enemies.Count == 0; i++) u.Sim.Tick(Dt, view, default);
        int before = BricksIn(u.World, u.Site, last);
        for (int i = 0; i < 180; i++) u.Sim.Tick(Dt, view, default);
        Assert.Equal(before, BricksIn(u.World, u.Site, last));
    }
}
