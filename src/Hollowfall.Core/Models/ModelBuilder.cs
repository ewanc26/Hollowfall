using Hollowfall.Core.Combat;
using Hollowfall.Core.Meshing;

namespace Hollowfall.Core.Models;

/// <summary>A flat colour. Low-poly models use few colours, chosen from <see cref="Palette"/>.</summary>
public readonly record struct ModelColor(float R, float G, float B)
{
    public static ModelColor Hex(int rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

    /// <summary>Lighter (positive) or darker (negative) by a fraction.</summary>
    public ModelColor Shade(float amount) => amount >= 0
        ? new(R + (1 - R) * amount, G + (1 - G) * amount, B + (1 - B) * amount)
        : new(R * (1 + amount), G * (1 + amount), B * (1 + amount));
}

/// <summary>One mesh that moves as a unit (an arm, a head). Vertices are relative to <see cref="Pivot"/>, so rotating the part swings it about its joint.</summary>
public sealed class ModelPart
{
    public string Name { get; }

    /// <summary>The part this one hangs from (a head from its torso), or null for a part attached to the model itself. A child moves, leans and scales with its parent.</summary>
    public string? Parent { get; }

    /// <summary>Where the part's joint is, in the model's own space (y up, feet at 0, facing -Z).</summary>
    public Vec3 Pivot { get; }

    /// <summary>Solid, lit geometry.</summary>
    public MeshBuffer Solid { get; } = new();

    /// <summary>Glowing geometry (eyes, cores, gems, flames): drawn unlit and bright, and used to show telegraphs.</summary>
    public MeshBuffer Glow { get; } = new();

    internal ModelPart(string name, Vec3 pivot, string? parent)
    {
        Name = name;
        Parent = parent;
        Pivot = pivot;
    }

    public int TriangleCount => Solid.TriangleCount + Glow.TriangleCount;
}

/// <summary>A finished model: named parts and anchor points (muzzles, eyes, flame positions).</summary>
public sealed class LowPolyModel
{
    public string Id { get; }
    public IReadOnlyList<ModelPart> Parts { get; }
    public IReadOnlyDictionary<string, Vec3> Anchors { get; }

    /// <summary>Corners of the box around every vertex, in model space.</summary>
    public Vec3 Min { get; }

    public Vec3 Max { get; }

    internal LowPolyModel(string id, List<ModelPart> parts, Dictionary<string, Vec3> anchors)
    {
        Id = id;
        Parts = parts;
        Anchors = anchors;

        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        foreach (var part in parts)
            foreach (var buffer in new[] { part.Solid, part.Glow })
            {
                var p = buffer.Positions();
                for (int i = 0; i < p.Length; i += 3)
                {
                    double x = p[i] + part.Pivot.X, y = p[i + 1] + part.Pivot.Y, z = p[i + 2] + part.Pivot.Z;
                    x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); z0 = Math.Min(z0, z);
                    x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); z1 = Math.Max(z1, z);
                }
            }
        Min = new Vec3(x0, y0, z0);
        Max = new Vec3(x1, y1, z1);
    }

    public int TriangleCount => Parts.Sum(p => p.TriangleCount);

    public ModelPart Part(string name) => Parts.First(p => p.Name == name);

    public bool HasPart(string name) => Parts.Any(p => p.Name == name);

    public double Height => Max.Y - Min.Y;
    public double Width => Math.Max(Max.X - Min.X, Max.Z - Min.Z);
}

/// <summary>
/// Builds a low-poly model from a handful of flat-shaded convex primitives. Every face has its own normals, so the model
/// shows its facets under any lighting. Pure maths with no engine types, so models are deterministic, testable and can be
/// previewed anywhere.
/// </summary>
public sealed class ModelBuilder
{
    private readonly string _id;
    private readonly List<PartBuilder> _parts = [];
    private readonly Dictionary<string, Vec3> _anchors = [];

    public ModelBuilder(string id) => _id = id;

    /// <summary>
    /// Starts a part that rotates about <paramref name="pivot"/>. Everything added to the part is positioned relative to
    /// that pivot, so an arm's hand sits below its shoulder whichever way the arm is posed.
    /// </summary>
    public PartBuilder Part(string name, Vec3 pivot, string? parent = null)
    {
        if (parent != null && _parts.All(p => p.Result.Name != parent))
            throw new ArgumentException($"part {name} names a parent, {parent}, that has not been added yet", nameof(parent));
        var part = new PartBuilder(new ModelPart(name, pivot, parent));
        _parts.Add(part);
        return part;
    }

    public ModelBuilder Anchor(string name, Vec3 position)
    {
        _anchors[name] = position;
        return this;
    }

    public LowPolyModel Build() => new(_id, _parts.Select(p => p.Result).ToList(), _anchors);
}

public sealed class PartBuilder
{
    internal ModelPart Result { get; }

    internal PartBuilder(ModelPart part) => Result = part;


    /// <summary>An axis-aligned box (before rotation) with the given full size.</summary>
    public PartBuilder Box(Vec3 centre, Vec3 size, ModelColor colour, Vec3? rotation = null, bool glow = false)
    {
        double x = size.X / 2, y = size.Y / 2, z = size.Z / 2;
        Vec3[] v = [new(-x, -y, -z), new(x, -y, -z), new(x, y, -z), new(-x, y, -z), new(-x, -y, z), new(x, -y, z), new(x, y, z), new(-x, y, z)];
        int[][] f = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]];
        return Add(v, f, centre, rotation, colour, glow);
    }

    /// <summary>
    /// A prism or frustum standing on y. <paramref name="taper"/> scales the top: 1 is a prism, 0.5 a frustum, 0 a cone or
    /// pyramid. Four sides makes a square pyramid when tapered to a point (use <paramref name="spin"/> 45 to turn it square on).
    /// </summary>
    public PartBuilder Prism(Vec3 centre, double radius, double height, int sides, ModelColor colour, double taper = 1, Vec3? rotation = null, bool glow = false, double spin = 0)
    {
        if (sides < 3) throw new ArgumentOutOfRangeException(nameof(sides));
        var v = new List<Vec3>();
        for (int i = 0; i < sides; i++)
        {
            double a = (spin + 360.0 * i / sides) * Math.PI / 180;
            v.Add(new Vec3(Math.Cos(a) * radius, -height / 2, Math.Sin(a) * radius));
        }
        bool point = taper <= 1e-9;
        for (int i = 0; i < sides; i++)
        {
            double a = (spin + 360.0 * i / sides) * Math.PI / 180;
            if (!point) v.Add(new Vec3(Math.Cos(a) * radius * taper, height / 2, Math.Sin(a) * radius * taper));
        }
        if (point) v.Add(new Vec3(0, height / 2, 0));

        var faces = new List<int[]> { Enumerable.Range(0, sides).Reverse().ToArray() };
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            faces.Add(point ? [i, j, sides] : [i, j, sides + j, sides + i]);
        }
        if (!point) faces.Add(Enumerable.Range(sides, sides).ToArray());
        return Add([.. v], [.. faces], centre, rotation, colour, glow);
    }

    /// <summary>A ramp: a box whose top slopes from full height at the back (+z) down to nothing at the front (-z).</summary>
    public PartBuilder Wedge(Vec3 centre, Vec3 size, ModelColor colour, Vec3? rotation = null, bool glow = false)
    {
        double x = size.X / 2, y = size.Y / 2, z = size.Z / 2;
        Vec3[] v = [new(-x, -y, -z), new(x, -y, -z), new(x, -y, z), new(-x, -y, z), new(-x, y, z), new(x, y, z)];
        int[][] f = [[0, 1, 2, 3], [3, 2, 5, 4], [0, 4, 5, 1], [0, 3, 4], [1, 5, 2]];
        return Add(v, f, centre, rotation, colour, glow);
    }

    /// <summary>A diamond with six points: the classic low-poly gem, used for projectiles, flames and cores.</summary>
    public PartBuilder Gem(Vec3 centre, Vec3 radii, ModelColor colour, Vec3? rotation = null, bool glow = false)
    {
        Vec3[] v = [new(radii.X, 0, 0), new(-radii.X, 0, 0), new(0, radii.Y, 0), new(0, -radii.Y, 0), new(0, 0, radii.Z), new(0, 0, -radii.Z)];
        int[][] f = [[0, 2, 4], [4, 2, 1], [1, 2, 5], [5, 2, 0], [4, 3, 0], [1, 3, 4], [5, 3, 1], [0, 3, 5]];
        return Add(v, f, centre, rotation, colour, glow);
    }

    /// <summary>A twelve-point rounded blob (an icosahedron), for bodies that should read as round with only twenty faces.</summary>
    public PartBuilder Blob(Vec3 centre, Vec3 radii, ModelColor colour, Vec3? rotation = null, bool glow = false)
    {
        double t = (1 + Math.Sqrt(5)) / 2;
        double n = Math.Sqrt(1 + t * t);
        Vec3[] raw = [new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1)];
        var v = raw.Select(p => new Vec3(p.X / n * radii.X, p.Y / n * radii.Y, p.Z / n * radii.Z)).ToArray();
        int[][] f =
        [
            [0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11],
            [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6], [7, 1, 8],
            [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9],
            [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1],
        ];
        return Add(v, f, centre, rotation, colour, glow);
    }

    // ---- shared ---------------------------------------------------------------------------------

    private PartBuilder Add(Vec3[] local, int[][] faces, Vec3 centre, Vec3? rotation, ModelColor colour, bool glow)
    {
        var r = rotation ?? default;
        var world = local.Select(p => Rotate(p, r) + centre).ToArray();
        var middle = new Vec3(world.Average(p => p.X), world.Average(p => p.Y), world.Average(p => p.Z));
        var buffer = glow ? Result.Glow : Result.Solid;

        foreach (var face in faces)
        {
            var poly = face.Select(i => world[i]).ToArray();

            // Flat normal from the first triangle; flip the winding if it points into the shape.
            var normal = Cross(poly[1] - poly[0], poly[2] - poly[0]).Normalized();
            var faceCentre = new Vec3(poly.Average(p => p.X), poly.Average(p => p.Y), poly.Average(p => p.Z));
            if (normal.Dot(faceCentre - middle) < 0)
            {
                Array.Reverse(poly);
                normal = normal * -1;
            }

            int first = -1, previous = -1;
            for (int i = 0; i < poly.Length; i++)
            {
                var p = poly[i];
                int index = buffer.AddVertex((float)p.X, (float)p.Y, (float)p.Z, (float)normal.X, (float)normal.Y, (float)normal.Z, colour.R, colour.G, colour.B);
                if (i == 0) first = index;
                if (i >= 2) buffer.AddTriangle(first, previous, index);
                previous = index;
            }
        }
        return this;
    }

    private static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    /// <summary>Rotation in degrees, applied about z, then x, then y.</summary>
    private static Vec3 Rotate(Vec3 p, Vec3 degrees)
    {
        double rx = degrees.X * Math.PI / 180, ry = degrees.Y * Math.PI / 180, rz = degrees.Z * Math.PI / 180;
        double x = p.X * Math.Cos(rz) - p.Y * Math.Sin(rz), y = p.X * Math.Sin(rz) + p.Y * Math.Cos(rz), z = p.Z;
        double y2 = y * Math.Cos(rx) - z * Math.Sin(rx), z2 = y * Math.Sin(rx) + z * Math.Cos(rx);
        double x3 = x * Math.Cos(ry) + z2 * Math.Sin(ry), z3 = -x * Math.Sin(ry) + z2 * Math.Cos(ry);
        return new Vec3(x3, y2, z3);
    }
}
