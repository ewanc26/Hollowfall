using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Portals;

public enum PortalColor
{
    Teal,
    Violet,
}

/// <summary>
/// An orthonormal frame in world space: <c>Right x Up = Normal</c>, with Normal pointing out of the surface
/// into open space. Portal geometry is axis-aligned, so these are always unit vectors along the world axes.
/// </summary>
public readonly record struct Frame(Vec3 Center, Vec3 Right, Vec3 Up, Vec3 Normal)
{
    public (double Right, double Up, double Normal) ToLocal(Vec3 p)
    {
        var d = p - Center;
        return (d.Dot(Right), d.Dot(Up), d.Dot(Normal));
    }

    public (double Right, double Up, double Normal) VectorToLocal(Vec3 v) => (v.Dot(Right), v.Dot(Up), v.Dot(Normal));
}

/// <summary>A 1 x 2 block opening on a flat surface, with the two wall blocks behind it that a body may sink into.</summary>
public sealed class Portal
{
    public const double Width = 1.0;
    public const double Height = 2.0;

    public PortalColor Color { get; }
    public Frame Frame { get; }

    /// <summary>The two solid blocks the portal is set into. The opening treats these as empty so bodies can pass through.</summary>
    public IReadOnlyList<(int X, int Y, int Z)> HoleCells { get; }

    public (int X, int Y, int Z) NormalCell { get; }

    internal Portal(PortalColor color, Frame frame, (int X, int Y, int Z)[] holes, (int, int, int) normal)
    {
        Color = color;
        Frame = frame;
        HoleCells = holes;
        NormalCell = normal;
    }

    public bool IsHole(int x, int y, int z) => HoleCells.Any(c => c.X == x && c.Y == y && c.Z == z);

    /// <summary>Whether a world position lies on the portal's face, within its rectangle (plus a margin).</summary>
    public bool Covers(Vec3 p, double margin = 0)
    {
        var (a, b, _) = Frame.ToLocal(p);
        return Math.Abs(a) <= Width / 2 + margin && Math.Abs(b) <= Height / 2 + margin;
    }

    public bool Overlaps(Portal other) => other.HoleCells.Any(c => IsHole(c.X, c.Y, c.Z) && other.NormalCell == NormalCell);
}

/// <summary>Maps positions and directions through a portal pair: in at one, out of the other, turned half way round.</summary>
public static class PortalTransform
{
    /// <summary>
    /// A point seen in the entrance's frame as (a, b, c) comes out of the exit at (-a, b, -c): mirrored across the
    /// portal plane and turned 180 degrees about the portal's up axis, so walking in means walking out.
    /// </summary>
    public static Vec3 Point(Portal entrance, Portal exit, Vec3 p) => Point(entrance.Frame, exit.Frame, p);

    /// <summary>Directions and velocities rotate the same way but have no position.</summary>
    public static Vec3 Vector(Portal entrance, Portal exit, Vec3 v) => Vector(entrance.Frame, exit.Frame, v);

    public static Vec3 Point(Frame entrance, Frame exit, Vec3 p)
    {
        var (a, b, c) = entrance.ToLocal(p);
        return exit.Center + exit.Right * -a + exit.Up * b + exit.Normal * -c;
    }

    public static Vec3 Vector(Frame entrance, Frame exit, Vec3 v)
    {
        var (a, b, c) = entrance.VectorToLocal(v);
        return exit.Right * -a + exit.Up * b + exit.Normal * -c;
    }
}
