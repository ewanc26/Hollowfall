namespace Hollowfall.Core.Village;

/// <summary>
/// A timber cottage with a pyramid thatch roof. (X, Z) is its lowest corner; its floor is the village ground and its walls
/// rise <see cref="VillagePlan.WallHeight"/> blocks above it. DoorSide: 0 north (-Z), 1 south (+Z), 2 west (-X), 3 east (+X).
/// </summary>
public readonly record struct House(int X, int Z, int Width, int Depth, int DoorSide)
{
    public bool Contains(int x, int z) => x >= X && x < X + Width && z >= Z && z < Z + Depth;

    public bool IsWall(int x, int z) => Contains(x, z) && (x == X || x == X + Width - 1 || z == Z || z == Z + Depth - 1);

    public (int X, int Z) Door => DoorSide switch
    {
        0 => (X + Width / 2, Z),
        1 => (X + Width / 2, Z + Depth - 1),
        2 => (X, Z + Depth / 2),
        _ => (X + Width - 1, Z + Depth / 2),
    };

    /// <summary>The cell a road starts from: two blocks out from the door, clear of the eaves.</summary>
    public (int X, int Z) Doorstep
    {
        get
        {
            var (dx, dz) = Door;
            var (ox, oz) = Outward;
            return (dx + 2 * ox, dz + 2 * oz);
        }
    }

    public (int X, int Z) Outward => DoorSide switch
    {
        0 => (0, -1),
        1 => (0, 1),
        2 => (-1, 0),
        _ => (1, 0),
    };
}

/// <summary>
/// One village, as pure data: where its ground is, its cottages, and the road cells that link them to the well at the centre.
/// The chunk builder turns it into blocks; every query here is a pure function of the plan.
/// </summary>
public sealed record VillagePlan(int CenterX, int CenterZ, int Ground, IReadOnlyList<House> Houses, IReadOnlySet<(int X, int Z)> Roads)
{
    /// <summary>Half the side of the square yard that is levelled around the village.</summary>
    public const int Yard = 26;
    public const int PlazaRadius = 2;
    public const int WallHeight = 3;
    public const int FoundationDepth = 8;

    public bool InYard(int x, int z) => Math.Abs(x - CenterX) <= Yard && Math.Abs(z - CenterZ) <= Yard;

    public bool IsPlaza(int x, int z) => Math.Abs(x - CenterX) <= PlazaRadius && Math.Abs(z - CenterZ) <= PlazaRadius;

    public bool IsRoad(int x, int z) => Roads.Contains((x, z));

    public House? HouseAt(int x, int z)
    {
        foreach (var h in Houses)
            if (h.Contains(x, z)) return h;
        return null;
    }

    /// <summary>The top block of a roof over this column (the roof starts at Ground + 4), or null where there is no roof.</summary>
    public int? RoofTop(int x, int z)
    {
        foreach (var h in Houses)
        {
            int a = Math.Min(x - (h.X - 1), h.X + h.Width - x);
            int b = Math.Min(z - (h.Z - 1), h.Z + h.Depth - z);
            if (a >= 0 && b >= 0) return Ground + WallHeight + 1 + Math.Min(a, b);
        }
        return null;
    }
}
