namespace Hollowfall.Core.Dungeon;

public readonly record struct Rect(int X, int Z, int W, int H)
{
    public int Right => X + W;
    public int Bottom => Z + H;
    public int CenterX => X + W / 2;
    public int CenterZ => Z + H / 2;
    public bool Contains(int x, int z) => x >= X && x < X + W && z >= Z && z < Z + H;
}

public enum CellKind : byte
{
    Rock,
    Floor,
    Door,
    SecretDoor,
    Pillar,
}

public enum RoomShape
{
    Rect,
    L,
    Circle,
    Cavern,
}

public enum RoomRole
{
    Standard,
    Entrance,
    Exit,
    Boss,
    Treasure,
    Trap,
    Safe,
}

/// <summary>A link between two rooms. Secret links are extra loops hidden behind secret doors.</summary>
public readonly record struct RoomLink(int A, int B, bool Secret);

public readonly record struct Blob(int X, int Z, int Rx, int Rz)
{
    public bool Contains(int x, int z)
    {
        long dx = x - X, dz = z - Z;
        return dx * dx * Rz * Rz + dz * dz * Rx * Rx <= (long)Rx * Rx * Rz * Rz;
    }
}

public sealed class Room
{
    public int Id { get; }
    public Rect Bounds { get; }
    public RoomShape Shape { get; }
    public RoomRole Role { get; internal set; } = RoomRole.Standard;

    /// <summary>Graph distance (over non-secret links) from the entrance room.</summary>
    public int Depth { get; internal set; }

    public int CenterX => Bounds.CenterX;
    public int CenterZ => Bounds.CenterZ;

    // L-shape: cells with sx*(x-cx) > a && sz*(z-cz) > b are removed.
    internal int CutSx, CutSz, CutA, CutB;
    internal Blob[] Blobs = [];

    internal Room(int id, Rect bounds, RoomShape shape)
    {
        Id = id;
        Bounds = bounds;
        Shape = shape;
    }

    /// <summary>Whether a cell belongs to this room's footprint. Every shape contains its centre and the 5x5 block around it.</summary>
    public bool Contains(int x, int z)
    {
        if (!Bounds.Contains(x, z)) return false;
        switch (Shape)
        {
            case RoomShape.Rect:
                return true;
            case RoomShape.L:
                return !(CutSx * (x - CenterX) > CutA && CutSz * (z - CenterZ) > CutB);
            case RoomShape.Circle:
                return new Blob(CenterX, CenterZ, Bounds.W / 2, Bounds.H / 2).Contains(x, z);
            default:
                foreach (var b in Blobs)
                    if (b.Contains(x, z)) return true;
                return false;
        }
    }
}
