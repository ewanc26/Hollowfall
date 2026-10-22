using Hollowfall.Core.Combat;
using Hollowfall.Core.Physics;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Dungeon;

/// <summary>
/// One way across a gap, found with the real <see cref="PortalSystem"/> and the real walking body: where to stand, where to aim the two
/// portals, and where the body ends up. <see cref="Far"/> is fired first (at the far hallway, or high in a shaft's tunnel) and
/// <see cref="Near"/> second (beside the player); then the player walks into the near one. The same plan drives the tests that
/// prove a gap can be crossed, the graph proof that every room is reachable, and the smoke bot.
/// </summary>
public sealed record GapPlan(GapSpec Gap, bool FromSide, Vec3 Feet, Vec3 Eye, Vec3 Facing, Vec3 FarAim, Vec3 NearAim, Vec3 Landing, Vec3 Arrival)
{
    /// <summary>The layout cell the player ends up in on the far side (the far hallway, or for a shaft gap the far hallway after walking off the tunnel).</summary>
    public (int X, int Z) ArrivalCell(DungeonSite site) => ((int)Math.Floor(Arrival.X) - site.OriginX, (int)Math.Floor(Arrival.Z) - site.OriginZ);
}

public static class GapCrossing
{
    public const double EyeHeight = 1.62;
    private const double Dt = 1.0 / 60;

    /// <summary>Why the most recent candidate was turned down, for test messages.</summary>
    [ThreadStatic]
    public static string? LastRejection;

    [ThreadStatic]
    public static List<string>? RejectionLog;

    /// <summary>The world as bodies and rays see it: dungeon blocks, and solid stone where the dungeon leaves the terrain alone.</summary>
    public static Func<int, int, int, BlockType> Blocks(DungeonSite site) => (x, y, z) => site.BlockAt(x, y, z) ?? BlockType.Stone;

    /// <summary>The feet position of a player standing in the middle of a layout cell on its floor.</summary>
    public static Vec3 FeetAt(DungeonSite site, int floor, (int X, int Z) cell) =>
        new(site.OriginX + cell.X + 0.5, DungeonSite.FloorY(floor) + 1 + site.Floors[floor].HeightAt(cell.X, cell.Z), site.OriginZ + cell.Z + 0.5);

    /// <summary>Finds a way across from one side of a gap, or null if the portals cannot be placed or the crossing does not end on the far side.</summary>
    /// <remarks>A portal gap is crossed from the pit edge. A shaft gap's tunnel can only be seen from further back, so the stand cell may be a few cells up the hallway.</remarks>
    public static GapPlan? Plan(DungeonSite site, int floor, GapSpec gap, bool fromSide)
    {
        var layout = site.Floors[floor];
        var (dx, dz) = gap.Toward(fromSide);
        var dir = new Vec3(dx, 0, dz);
        var lat = new Vec3(-dz, 0, dx);
        double floorY = DungeonSite.FloorY(floor);
        bool shaft = gap.Kind == LinkKind.ShaftGap;
        double surface = floorY + 1 + (shaft ? 0 : gap.Rise);
        double tunnelSurface = floorY + 1 + gap.Rise;

        var near = gap.Hall(fromSide); // door cell first, pit edge last
        var far = fromSide ? gap.HallTo : [.. gap.HallFrom.Reverse()]; // pit edge first
        var span = fromSide ? gap.Span : [.. gap.Span.Reverse()];

        Vec3 Centre((int X, int Z) c, double y) => new(site.OriginX + c.X + 0.5, y, site.OriginZ + c.Z + 0.5);
        double Surface((int X, int Z) c) => floorY + 1 + layout.HeightAt(c.X, c.Z);

        // Far candidates, best first.
        var farTargets = new List<Vec3>();
        if (shaft)
        {
            foreach (var y in new[] { tunnelSurface + 0.8, tunnelSurface + 1.2, tunnelSurface + 1.6 })
                for (int j = 0; j < span.Count; j++)
                    foreach (int side in new[] { 1, -1 })
                        farTargets.Add(Centre(span[j], y) + lat * (1.5 * side));
        }
        else
        {
            // The wall beside the flat ground at the lip first, then its floor, then the steps further back.
            for (int j = 0; j < far.Count; j++)
                if (Surface(far[j]) == surface)
                    foreach (int side in new[] { 1, -1 })
                        farTargets.Add(Centre(far[j], surface + 1.5) + lat * (1.5 * side));
            for (int j = 0; j < far.Count - 1; j++)
                if (Surface(far[j]) == surface && Surface(far[j + 1]) == surface)
                    farTargets.Add(Centre(far[j], surface));
            for (int j = 1; j < far.Count; j++)
                if (Surface(far[j]) != surface)
                    foreach (int side in new[] { 1, -1 })
                        farTargets.Add(Centre(far[j], Surface(far[j]) + 1.5) + lat * (1.5 * side));
        }

        int standOffsets = shaft ? Math.Max(1, near.Count - 1) : 1;
        for (int back = 0; back < standOffsets; back++)
        {
            int standIndex = near.Count - 1 - back;
            var feet = FeetAt(site, floor, near[standIndex]);
            var eye = feet + new Vec3(0, EyeHeight, 0);

            // Near candidates: the wall beside the player, then the floor around.
            var nearTargets = new List<Vec3>();
            for (int k = standIndex; k >= Math.Max(0, standIndex - 2); k--)
                foreach (int side in new[] { 1, -1 })
                    nearTargets.Add(Centre(near[k], Surface(near[k]) + 1.5) + lat * (1.5 * side));
            for (int k = standIndex - 1; k >= 1; k--)
                if (Surface(near[k]) == surface && Surface(near[k - 1]) == surface) nearTargets.Add(Centre(near[k], surface));
            if (nearTargets.Count == 0 && standIndex >= 1) nearTargets.Add(Centre(near[standIndex - 1], Surface(near[standIndex - 1])));

            foreach (var farTarget in farTargets)
            {
                var farAim = (farTarget - eye).Normalized();
                foreach (var nearTarget in nearTargets)
                {
                    var nearAim = (nearTarget - eye).Normalized();
                    if (Simulate(site, floor, gap, fromSide, feet, eye, dir, farAim, nearAim, out var landing, out var arrival))
                        return new GapPlan(gap, fromSide, feet, eye, dir, farAim, nearAim, landing, arrival);
                }
            }
        }
        return null;
    }

    private static bool Simulate(DungeonSite site, int floor, GapSpec gap, bool fromSide, Vec3 feet, Vec3 eye, Vec3 dir, Vec3 farAim, Vec3 nearAim,
        out Vec3 landing, out Vec3 arrival)
    {
        landing = arrival = feet;
        var blocks = Blocks(site);
        var portals = new PortalSystem();
        bool platesOnly = floor >= DungeonSite.RiftPlateFloor;
        if (!portals.Place(PortalColor.Teal, eye, farAim, dir, blocks, platesOnly).Placed) return Reject("far portal not placed");
        var nearPlaced = portals.Place(PortalColor.Violet, eye, nearAim, dir, blocks, platesOnly);
        if (!nearPlaced.Placed) return Reject("near portal not placed");
        var nearPortal = nearPlaced.Portal!;
        var farPortal = portals.Teal!;
        bool shaft = gap.Kind == LinkKind.ShaftGap;

        // The far portal has to be on the far side of the pit, and the near one on this side of it.
        if (!OnSide(site, gap, farPortal, fromSide, shaft)) return Reject($"far portal on the wrong side: hole {farPortal.HoleCells[0].X - site.OriginX},{farPortal.HoleCells[0].Z - site.OriginZ} n {farPortal.NormalCell}");
        if (!OnSide(site, gap, nearPortal, !fromSide, false)) return Reject($"near portal on the wrong side: hole {nearPortal.HoleCells[0].X - site.OriginX},{nearPortal.HoleCells[0].Z - site.OriginZ} n {nearPortal.NormalCell}");

        // In a shaft the far portal has to be up in the tunnel, not on the sheer face of the block under it.
        if (shaft && farPortal.HoleCells.Any(h => h.Y < DungeonSite.FloorY(floor) + 1 + gap.Rise)) return Reject($"far portal below the tunnel: {string.Join(" ", farPortal.HoleCells.Select(h => (h.X - site.OriginX, h.Y - DungeonSite.FloorY(floor), h.Z - site.OriginZ)))} stand {feet}");

        var body = new PlayerBody(feet.X, feet.Y, feet.Z);
        for (int i = 0; i < 20; i++) body.Step(Dt, 0, 0, false, blocks); // settle on the ground

        // Walk into the near portal.
        bool crossed = false;
        Crossing? crossing = null;
        for (int i = 0; i < 60 * 5 && !crossed; i++)
        {
            var to = nearPortal.Frame.Center - PortalWalker.Centre(body);
            double mx = to.X, mz = to.Z;
            double len = Math.Sqrt(mx * mx + mz * mz);
            if (len < 0.15) { mx = -nearPortal.Frame.Normal.X; mz = -nearPortal.Frame.Normal.Z; len = Math.Sqrt(mx * mx + mz * mz); }
            if (len > 1e-9) { mx /= len; mz /= len; }
            if (PortalWalker.Step(body, portals, Dt, mx, mz, false, blocks, dir) is { } c)
            {
                crossed = true;
                crossing = c;
            }
        }
        if (!crossed || crossing is not { } cr || cr.Exited != farPortal) return Reject("never crossed");

        SettleAfterCrossing(body, portals, farPortal, dir, blocks);
        landing = new Vec3(body.X, body.Y, body.Z);
        if (!body.OnGround) return Reject($"not on ground after crossing at {body.X:F2},{body.Y:F2},{body.Z:F2} v {body.VelocityX:F1},{body.VelocityY:F1},{body.VelocityZ:F1} far {farPortal.Frame.Center} n {farPortal.Frame.Normal} near {nearPortal.Frame.Center} n {nearPortal.Frame.Normal}");

        var cell = (X: (int)Math.Floor(body.X) - site.OriginX, Z: (int)Math.Floor(body.Z) - site.OriginZ);
        if (!shaft)
        {
            arrival = landing;
            if (!InCells(gap, fromSide ? gap.HallTo : gap.HallFrom, cell)) return Reject($"landed outside the far hallway at {cell} y {body.Y:F2} far {farPortal.Frame.Center - new Vec3(site.OriginX, DungeonSite.FloorY(floor), site.OriginZ)} n {farPortal.Frame.Normal} near {nearPortal.Frame.Center - new Vec3(site.OriginX, DungeonSite.FloorY(floor), site.OriginZ)} n {nearPortal.Frame.Normal}");
            // Standing on the ground of that cell (or of the cell it is half over, at a step's edge).
            var surfaces = new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) }.Select(d => DungeonSite.FloorY(floor) + 1.0 + site.Floors[floor].HeightAt(cell.X + d.Item1, cell.Z + d.Item2));
            if (surfaces.Any(y => Math.Abs(y - body.Y) < 0.01)) return true;
            return Reject($"not standing on the ground of the far hallway: y {body.Y:F2}");
        }

        // A shaft gap: the body stands in the tunnel. Walk to the far end and step off onto the far hallway.
        if (!InCells(gap, gap.Span, cell) || Math.Abs(body.Y - (DungeonSite.FloorY(floor) + 1 + gap.Rise)) > 0.05) return Reject($"not in the tunnel: {cell} y {body.Y:F2} holes {string.Join(" ", farPortal.HoleCells.Select(h => (h.X - site.OriginX, h.Y - DungeonSite.FloorY(floor), h.Z - site.OriginZ)))} n {farPortal.Frame.Normal} at {body.X - site.OriginX:F2},{body.Z - site.OriginZ:F2}");
        double tunnelTop = DungeonSite.FloorY(floor) + 1 + gap.Rise;
        for (int i = 0; i < 60 * 6 && body.Y > tunnelTop - 0.5; i++) body.Step(Dt, dir.X, dir.Z, false, blocks);
        for (int i = 0; i < 60; i++) body.Step(Dt, 0, 0, false, blocks);
        arrival = new Vec3(body.X, body.Y, body.Z);
        var end = (X: (int)Math.Floor(body.X) - site.OriginX, Z: (int)Math.Floor(body.Z) - site.OriginZ);
        if (body.OnGround && InCells(gap, fromSide ? gap.HallTo : gap.HallFrom, end) && Math.Abs(body.Y - (DungeonSite.FloorY(floor) + 1)) < 0.05) return true;
        return Reject($"did not step off onto the far hallway: {end} y {body.Y:F2}");
    }

    /// <summary>
    /// What a player does after coming out of a portal: walks on, facing away from the wall (or, out of a floor, the way they were going),
    /// which also steps them clear of the wall so they can turn along it; then stands still while the body settles.
    /// </summary>
    public static void SettleAfterCrossing(PlayerBody body, PortalSystem portals, Portal exit, Vec3 dir, Func<int, int, int, BlockType> blocks)
    {
        bool floorExit = exit.Frame.Normal.Y > 0.5;
        double ax = floorExit ? dir.X : exit.Frame.Normal.X, az = floorExit ? dir.Z : exit.Frame.Normal.Z;
        for (int i = 0; i < 24; i++) PortalWalker.Step(body, portals, Dt, ax, az, false, blocks, dir);
        double sx = floorExit ? dir.X : 0, sz = floorExit ? dir.Z : 0;
        for (int i = 0; i < 60 * 3; i++) PortalWalker.Step(body, portals, Dt, sx, sz, false, blocks, dir);
    }

    private static bool Reject(string why)
    {
        LastRejection = why;
        RejectionLog?.Add(why);
        return false;
    }

    private static bool InCells(GapSpec gap, IReadOnlyList<(int X, int Z)> centreLine, (int X, int Z) cell, int halfWidth = 1) =>
        gap.Within(centreLine, cell, halfWidth);

    /// <summary>
    /// Whether a portal opens onto the given side: the cell in front of it (where a body comes out, or stands to go in) is part of the
    /// hallway (or, for a shaft's far portal, the tunnel), never the pit or the sheer face above it.
    /// </summary>
    private static bool OnSide(DungeonSite site, GapSpec gap, Portal portal, bool toSide, bool shaft)
    {
        var cells = shaft ? gap.Span : toSide ? gap.HallTo : gap.HallFrom;
        var hole = portal.HoleCells[0];
        var n = portal.NormalCell;
        var front = (X: hole.X + n.X - site.OriginX, Z: hole.Z + n.Z - site.OriginZ);
        return gap.Within(cells, front, 1);
    }
}
