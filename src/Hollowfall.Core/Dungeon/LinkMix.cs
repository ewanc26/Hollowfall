namespace Hollowfall.Core.Dungeon;

/// <summary>
/// How a floor's rooms are joined: the numbers behind the mix of walkable corridors and gaps that need a portal. Everything
/// the player can feel is here, in one place, so playtesting changes constants and not logic. See <c>docs/game-loop.md</c>.
/// </summary>
public static class LinkMix
{
    /// <summary>How high a gap hallway stands above the room floor, and the depth of the pit between two of them (blocks).</summary>
    public const int Rise = 3;

    /// <summary>
    /// Walkable cells of a gap hallway between its door and the pit edge: a flat landing (a body cannot step up under the door's lintel),
    /// three steps up (the top step is the lip of the pit), then up to two cells of flat ground.
    /// </summary>
    public const int HallMin = 4, HallMax = 6;

    /// <summary>Length of the pit in cells, along the way across.</summary>
    public const int SpanMin = 6, SpanMax = 16;

    /// <summary>The first gap out of the entrance room is short, so the player learns the trick on an easy one.</summary>
    public const int FirstSpanMin = 6, FirstSpanMax = 8;

    /// <summary>A shaft gap: flat hallways and a solid block between them with a tunnel through its top.</summary>
    public const int ShaftHallMin = 3, ShaftHallMax = 5;

    public const int TunnelMin = 3, TunnelMax = 5;

    /// <summary>
    /// The chance that a link which no room needs for its own exit gap is a gap anyway. Every room must have a gap, so a floor can never
    /// be mostly corridors (the first floor keeps only the ones the rule forces); deeper floors add more.
    /// </summary>
    public static double ExtraGapChance(int floor) => floor <= 0 ? 0.0 : floor == 1 ? 0.40 : 0.60;

    /// <summary>The chance that a link which is not a portal gap is a shaft gap instead (never on the first floor).</summary>
    public static double ShaftChance(int floor) => floor <= 0 ? 0.0 : floor == 1 ? 0.12 : 0.18;

    /// <summary>The smallest straight stretch of corridor a portal gap fits in, doors included.</summary>
    public const int MinGapLength = 2 + 2 * HallMin + SpanMin;

    public const int MinShaftLength = 2 + 2 * ShaftHallMin + TunnelMin;

    /// <summary>What lies at the bottom of a pit: bare stone is the rule on the first floor, spikes and water get commoner below.</summary>
    public static GapHazard PickHazard(int floor, bool first, SeededRandom rng)
    {
        if (first) return GapHazard.Bare;
        int roll = rng.Next(100);
        int bare = floor <= 0 ? 70 : floor == 1 ? 40 : 25;
        int water = floor <= 0 ? 20 : 30;
        if (roll < bare) return GapHazard.Bare;
        if (roll < bare + water) return GapHazard.Water;
        return GapHazard.Spikes;
    }
}

/// <summary>
/// A gap between two rooms, built along a straight stretch of one corridor. Cells are listed along the way across, from the
/// end nearer <see cref="RoomFrom"/> to the end nearer <see cref="RoomTo"/>. A portal gap is hallway, pit, hallway: each
/// hallway starts with a door cell at the room (so a fight can seal it), has a flat landing, climbs three steps and runs flat to the pit edge.
/// A shaft gap is hallway, a solid block with a tunnel through its top, hallway: the tunnel is out of reach on foot.
/// </summary>
public sealed class GapSpec
{
    public int LinkIndex { get; init; }
    public LinkKind Kind { get; init; }
    public GapHazard Hazard { get; init; }

    /// <summary>Unit step from the <see cref="RoomFrom"/> end to the <see cref="RoomTo"/> end.</summary>
    public int DirX { get; init; }
    public int DirZ { get; init; }

    /// <summary>The rooms at the two ends (-1 where the hallway opens on a plain corridor instead).</summary>
    public int RoomFrom { get; init; }
    public int RoomTo { get; init; }

    /// <summary>Centre-line cells of the first hallway, door cell first, pit edge last.</summary>
    public IReadOnlyList<(int X, int Z)> HallFrom { get; init; } = [];

    /// <summary>Centre-line cells of the pit (portal gap) or of the block with the tunnel through it (shaft gap).</summary>
    public IReadOnlyList<(int X, int Z)> Span { get; init; } = [];

    /// <summary>Centre-line cells of the second hallway, pit edge first, door cell last.</summary>
    public IReadOnlyList<(int X, int Z)> HallTo { get; init; } = [];

    /// <summary>The height of the hallway floors above the room floor (portal gap) or of the tunnel floor (shaft gap).</summary>
    public int Rise { get; init; }

    public bool IsPortalGap => Kind == LinkKind.PortalGap;

    public (int X, int Z) EdgeFrom => HallFrom[^1];
    public (int X, int Z) EdgeTo => HallTo[0];

    /// <summary>The cell a player stands in at the edge of the gap, on the given side.</summary>
    public (int X, int Z) Edge(bool fromSide) => fromSide ? EdgeFrom : EdgeTo;

    /// <summary>The hallway on the given side, door cell first.</summary>
    public IReadOnlyList<(int X, int Z)> Hall(bool fromSide) => fromSide ? HallFrom : [.. HallTo.Reverse()];

    /// <summary>The unit step along the way across, leaving the given side.</summary>
    public (int X, int Z) Toward(bool fromSide) => fromSide ? (DirX, DirZ) : (-DirX, -DirZ);

    /// <summary>Whether a layout cell lies in the strip (centre line plus <paramref name="halfWidth"/> cells either side) of the given centre-line cells.</summary>
    public bool Within(IReadOnlyList<(int X, int Z)> centreLine, (int X, int Z) cell, int halfWidth)
    {
        foreach (var c in centreLine)
        {
            int ax = (cell.X - c.X) * DirX + (cell.Z - c.Z) * DirZ;
            int lat = (cell.X - c.X) * -DirZ + (cell.Z - c.Z) * DirX;
            if (ax == 0 && Math.Abs(lat) <= halfWidth) return true;
        }
        return false;
    }

    /// <summary>The span of the pit in cells.</summary>
    public int SpanLength => Span.Count;
}
