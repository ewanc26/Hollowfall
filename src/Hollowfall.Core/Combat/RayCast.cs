using Hollowfall.Core.World;

namespace Hollowfall.Core.Combat;

public readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public double Dot(Vec3 o) => X * o.X + Y * o.Y + Z * o.Z;

    public Vec3 Normalized()
    {
        double l = Length;
        return l < 1e-12 ? new Vec3(0, 0, -1) : new Vec3(X / l, Y / l, Z / l);
    }

    public double DistanceTo(Vec3 o) => (this - o).Length;
}

public readonly record struct Box(Vec3 Min, Vec3 Max)
{
    public static Box AroundFeet(Vec3 feet, double halfWidth, double height) =>
        new(new Vec3(feet.X - halfWidth, feet.Y, feet.Z - halfWidth), new Vec3(feet.X + halfWidth, feet.Y + height, feet.Z + halfWidth));

    public bool Contains(Vec3 p) =>
        p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;
}

public static class RayCast
{
    /// <summary>Distance along the ray to the first solid voxel within <paramref name="maxDistance"/>, or null (DDA traversal).</summary>
    public static double? FirstSolid(Func<int, int, int, BlockType> blockAt, Vec3 origin, Vec3 direction, double maxDistance)
    {
        var d = direction.Normalized();
        int x = (int)Math.Floor(origin.X), y = (int)Math.Floor(origin.Y), z = (int)Math.Floor(origin.Z);
        if (BlockInfo.IsSolid(blockAt(x, y, z))) return 0;

        int sx = Math.Sign(d.X), sy = Math.Sign(d.Y), sz = Math.Sign(d.Z);
        double tx = Next(origin.X, d.X, x), ty = Next(origin.Y, d.Y, y), tz = Next(origin.Z, d.Z, z);
        double dx = d.X == 0 ? double.PositiveInfinity : Math.Abs(1 / d.X);
        double dy = d.Y == 0 ? double.PositiveInfinity : Math.Abs(1 / d.Y);
        double dz = d.Z == 0 ? double.PositiveInfinity : Math.Abs(1 / d.Z);

        while (true)
        {
            double t;
            if (tx <= ty && tx <= tz) { t = tx; x += sx; tx += dx; }
            else if (ty <= tz) { t = ty; y += sy; ty += dy; }
            else { t = tz; z += sz; tz += dz; }

            if (t > maxDistance) return null;
            if (BlockInfo.IsSolid(blockAt(x, y, z))) return t;
        }
    }

    private static double Next(double o, double d, int cell)
    {
        if (d == 0) return double.PositiveInfinity;
        return d > 0 ? (cell + 1 - o) / d : (o - cell) / -d;
    }

    /// <summary>Distance along the ray to a box (slab method), or null if missed or beyond range.</summary>
    public static double? HitBox(Vec3 origin, Vec3 direction, Box box, double maxDistance)
    {
        var d = direction.Normalized();
        double tMin = 0, tMax = maxDistance;
        foreach (var (o, dd, lo, hi) in new[] { (origin.X, d.X, box.Min.X, box.Max.X), (origin.Y, d.Y, box.Min.Y, box.Max.Y), (origin.Z, d.Z, box.Min.Z, box.Max.Z) })
        {
            if (Math.Abs(dd) < 1e-12)
            {
                if (o < lo || o > hi) return null;
                continue;
            }
            double t1 = (lo - o) / dd, t2 = (hi - o) / dd;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax) return null;
        }
        return tMin;
    }

    /// <summary>True if nothing solid lies between two points.</summary>
    public static bool HasLineOfSight(Func<int, int, int, BlockType> blockAt, Vec3 from, Vec3 to)
    {
        var delta = to - from;
        double dist = delta.Length;
        return dist < 1e-6 || FirstSolid(blockAt, from, delta, dist) is null;
    }
}
