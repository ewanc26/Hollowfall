using Hollowfall.Core.World;

namespace Hollowfall.Core.Dungeon;

/// <summary>A place a body can stand: the block its feet are in.</summary>
public readonly record struct Spot(int X, int Y, int Z);

/// <summary>One validated way over a gap: stand at <see cref="Stand"/>, fire the portals as <see cref="Plan"/> says, and end up at <see cref="Arrival"/>.</summary>
public sealed record Hop(int Floor, GapSpec Gap, bool FromSide, Spot Stand, Spot Arrival, GapPlan Plan);

/// <summary>
/// Where a player can get to: walking (stepping up a block at a time, dropping a few) and, over gaps, portal hops that have been
/// checked with the real portal placement and walking physics (<see cref="GapCrossing.Plan"/>). A gap with no valid hop is simply
/// not crossable here, so a layout that cannot be finished shows up as rooms that are not reached. Pits are never stood in.
/// </summary>
public static class DungeonReach
{
    /// <summary>The furthest a body will drop off a ledge on purpose (a shaft gap's tunnel is three up).</summary>
    public const int MaxDrop = 4;

    /// <summary>One step of a route: walk to <see cref="To"/>, or (when <see cref="Via"/> is set) cross a gap by that hop and arrive there.</summary>
    public readonly record struct Step(Spot To, Hop? Via);

    public static Spot SpotOf(double x, double y, double z) => new((int)Math.Floor(x), (int)Math.Floor(y + 1e-6), (int)Math.Floor(z));

    /// <summary>Every gap, in both directions, that can really be crossed.</summary>
    public static IReadOnlyList<Hop> Hops(DungeonSite site)
    {
        var hops = new List<Hop>();
        for (int f = 0; f < site.FloorCount; f++)
            foreach (var gap in site.Floors[f].Gaps)
                foreach (bool fromSide in new[] { true, false })
                    if (GapCrossing.Plan(site, f, gap, fromSide) is { } plan)
                        hops.Add(new Hop(f, gap, fromSide, SpotOf(plan.Feet.X, plan.Feet.Y, plan.Feet.Z), SpotOf(plan.Arrival.X, plan.Arrival.Y, plan.Arrival.Z), plan));
        return hops;
    }

    /// <summary>The fewest portal hops needed to reach each spot from <paramref name="start"/>; spots not in the result cannot be reached.</summary>
    public static Dictionary<Spot, int> Analyse(DungeonSite site, Spot start, IReadOnlyList<Hop>? hops = null) =>
        Search(site, start, hops, null);

    /// <summary>The route that uses the fewest hops from one spot to another, or null if there is none.</summary>
    public static List<Step>? Route(DungeonSite site, Spot start, Spot goal, IReadOnlyList<Hop>? hops = null)
    {
        var parents = new Dictionary<Spot, (Spot Previous, Hop? Via)>();
        var cost = Search(site, start, hops, parents);
        if (!cost.ContainsKey(goal)) return null;
        var route = new List<Step>();
        for (var at = goal; at != start; at = parents[at].Previous) route.Add(new Step(at, parents[at].Via));
        route.Reverse();
        return route;
    }

    private static Dictionary<Spot, int> Search(DungeonSite site, Spot start, IReadOnlyList<Hop>? hops, Dictionary<Spot, (Spot Previous, Hop? Via)>? parents)
    {
        hops ??= Hops(site);
        var blocks = GapCrossing.Blocks(site);
        var byStand = hops.GroupBy(h => h.Stand).ToDictionary(g => g.Key, g => g.ToList());

        var cost = new Dictionary<Spot, int> { [start] = 0 };
        var current = new Queue<Spot>([start]);
        var next = new Queue<Spot>();
        int level = 0;
        while (current.Count > 0 || next.Count > 0)
        {
            if (current.Count == 0)
            {
                (current, next) = (next, current);
                level++;
            }
            var spot = current.Dequeue();
            if (cost[spot] != level) continue;

            foreach (var n in WalkNeighbours(site, blocks, spot))
                if (!cost.TryGetValue(n, out int c) || c > level)
                {
                    cost[n] = level;
                    if (parents != null) parents[n] = (spot, null);
                    current.Enqueue(n);
                }
            if (byStand.TryGetValue(spot, out var here))
                foreach (var hop in here)
                    if (!cost.TryGetValue(hop.Arrival, out int c) || c > level + 1)
                    {
                        cost[hop.Arrival] = level + 1;
                        if (parents != null) parents[hop.Arrival] = (spot, hop);
                        next.Enqueue(hop.Arrival);
                    }
        }
        return cost;
    }

    /// <summary>The fewest hops to any standable cell of a room (or null if it cannot be reached).</summary>
    public static int? HopsToRoom(DungeonSite site, int floor, Room room, Dictionary<Spot, int> reach)
    {
        int? best = null;
        var layout = site.Floors[floor];
        for (int z = room.Bounds.Z; z < room.Bounds.Bottom; z++)
            for (int x = room.Bounds.X; x < room.Bounds.Right; x++)
            {
                if (layout.RoomIdAt(x, z) != room.Id || layout.CellAt(x, z) != CellKind.Floor) continue;
                var (wx, wy, wz) = site.ToWorld(floor, x, z);
                if (reach.TryGetValue(new Spot(wx, wy, wz), out int h) && (best == null || h < best)) best = h;
            }
        return best;
    }

    private static bool Solid(Func<int, int, int, BlockType> blocks, int x, int y, int z) => BlockInfo.IsSolid(blocks(x, y, z));

    private static bool InPit(DungeonSite site, int x, int y, int z)
    {
        int f = site.FloorIndexAt(x + 0.5, y + 0.5, z + 0.5);
        if (f < 0) return false;
        return site.Floors[f].CellAt(x - site.OriginX, z - site.OriginZ) is CellKind.Pit or CellKind.PitSpikes or CellKind.PitWater;
    }

    /// <summary>Whether a two-block-tall body fits at the spot and has something solid under it (and it is not in a pit).</summary>
    public static bool CanStand(DungeonSite site, Func<int, int, int, BlockType> blocks, Spot s) =>
        !Solid(blocks, s.X, s.Y, s.Z) && !Solid(blocks, s.X, s.Y + 1, s.Z) && Solid(blocks, s.X, s.Y - 1, s.Z) && !InPit(site, s.X, s.Y, s.Z);

    private static IEnumerable<Spot> WalkNeighbours(DungeonSite site, Func<int, int, int, BlockType> blocks, Spot s)
    {
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            int nx = s.X + dx, nz = s.Z + dz;
            if (nx < site.OriginX || nx >= site.OriginX + FloorLayout.Size || nz < site.OriginZ || nz >= site.OriginZ + FloorLayout.Size) continue;

            // Level, or one step up (the step needs headroom over the current cell too).
            if (CanStand(site, blocks, new Spot(nx, s.Y, nz))) { yield return new Spot(nx, s.Y, nz); continue; }
            if (CanStand(site, blocks, new Spot(nx, s.Y + 1, nz)) && !Solid(blocks, s.X, s.Y + 2, s.Z)) { yield return new Spot(nx, s.Y + 1, nz); continue; }

            // Walk off a ledge and drop: the cells in the way must be open.
            if (Solid(blocks, nx, s.Y, nz) || Solid(blocks, nx, s.Y + 1, nz)) continue;
            for (int drop = 1; drop <= MaxDrop; drop++)
            {
                var below = new Spot(nx, s.Y - drop, nz);
                if (Solid(blocks, below.X, below.Y, below.Z)) break;
                if (CanStand(site, blocks, below)) { yield return below; break; }
            }
        }
    }
}
