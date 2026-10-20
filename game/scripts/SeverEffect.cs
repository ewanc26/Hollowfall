using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Portals;

namespace Hollowfall.Game;

/// <summary>
/// The look of Sever, drawn when a portal closes: a white-hot arc slashes across the opening, the opening snaps shut
/// to a line in the portal's colour, and flat shards fly off the edges. A second, smaller burst marks anything that was
/// cut. Everything is unshaded low-poly boxes, animated in code; the rules are in <see cref="SeverRules"/>.
/// </summary>
public partial class SeverEffect : Node3D
{
    private const double Life = 0.7;
    private const int ArcSegments = 12;
    private const double ArcRadius = 1.9;
    private const double ArcSweepSeconds = 0.09;
    private const double SnapSeconds = 0.2;

    private sealed class Shard
    {
        public MeshInstance3D Node = null!;
        public Vector3 Velocity;
        public Vector3 Spin;
    }

    private readonly Color _colour;
    private readonly bool _portal;
    private readonly Vector3 _right, _up, _normal;
    private readonly List<MeshInstance3D> _arc = [];
    private readonly List<Shard> _shards = [];
    private MeshInstance3D? _snap;
    private double _age;

    private SeverEffect(Color colour, Vector3 right, Vector3 up, Vector3 normal, bool portal)
    {
        _colour = colour;
        _right = right;
        _up = up;
        _normal = normal;
        _portal = portal;
    }

    public static Color ColourOf(PortalColor c) => c == PortalColor.Teal ? new Color(0.1f, 0.83f, 0.77f) : new Color(0.64f, 0.29f, 1f);

    /// <summary>The portal closing: a slash across the opening, then the opening snapping shut.</summary>
    public static SeverEffect ForPortal(PortalClosed e)
    {
        var up = V(e.Up).Normalized();
        var normal = V(e.Normal).Normalized();
        var right = up.Cross(normal).Normalized();
        return new SeverEffect(ColourOf(e.Color), right, up, normal, true) { Position = V(e.Position) };
    }

    /// <summary>A flash of shards where an enemy was cut in two.</summary>
    public static SeverEffect ForEnemy(EnemySevered e) =>
        new(ColourOf(e.Portal), Vector3.Right, Vector3.Up, Vector3.Back, false) { Position = V(e.Point) };

    public override void _Ready()
    {
        if (_portal) BuildPortalClosing();
        BuildShards(_portal ? 14 : 10);
    }

    private static StandardMaterial3D Glow(Color c) => new()
    {
        AlbedoColor = c,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    private void BuildPortalClosing()
    {
        var basis = new Basis(_right, _up, _normal);

        // The opening collapsing to a line.
        _snap = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2((float)Portal.Width, (float)Portal.Height) },
            MaterialOverride = Glow(_colour with { A = 0.9f }),
            Transform = new Transform3D(basis, _normal * 0.02f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_snap);

        // The cutting arc: a crescent of thin slabs from the lower left to the upper right, revealed in order.
        // It is centred off to one side so the sweep reads as a blade, tilted a little off vertical.
        float tilt = Mathf.DegToRad(-28f);
        var tiltBasis = new Basis(_normal, tilt);
        for (int i = 0; i < ArcSegments; i++)
        {
            double a0 = Mathf.DegToRad(-55 + 110.0 * i / ArcSegments), a1 = Mathf.DegToRad(-55 + 110.0 * (i + 1) / ArcSegments);
            var p0 = ArcPoint(a0);
            var p1 = ArcPoint(a1);
            var mid = (p0 + p1) / 2;
            float len = p0.DistanceTo(p1) * 1.15f;
            float taper = 1f - Mathf.Abs(i - ArcSegments / 2f + 0.5f) / (ArcSegments / 2f) * 0.6f; // thick in the middle, thin at the tips
            var dir = (p1 - p0).Normalized();
            var segBasis = new Basis(dir.Cross(_normal).Normalized(), dir, _normal);
            var node = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.14f * taper, len, 0.02f) },
                MaterialOverride = Glow(new Color(1f, 0.97f, 0.9f)),
                Transform = new Transform3D(tiltBasis * segBasis, tiltBasis * mid + _normal * 0.06f),
                Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(node);
            _arc.Add(node);
        }
    }

    /// <summary>A point on the blade's arc in the portal's plane, bulging toward the portal's right.</summary>
    private Vector3 ArcPoint(double angle) =>
        _right * (float)(Math.Cos(angle) * ArcRadius - ArcRadius * 0.82) + _up * (float)(Math.Sin(angle) * ArcRadius);

    private void BuildShards(int count)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(Position.X * 73856093 + Position.Y * 19349663 + Position.Z * 83492791) };
        for (int i = 0; i < count; i++)
        {
            var origin = _portal
                ? _right * (float)(rng.RandfRange(-0.5f, 0.5f)) + _up * (float)(rng.RandfRange(-1f, 1f)) + _normal * 0.05f
                : new Vector3(0, rng.RandfRange(-0.4f, 0.4f), 0);
            var dir = (_right * rng.RandfRange(-1f, 1f) + _up * rng.RandfRange(-0.4f, 1f) + _normal * rng.RandfRange(0.3f, 1.4f)).Normalized();
            float size = rng.RandfRange(0.05f, 0.11f);
            var node = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(size, size * 0.4f, size * 1.6f) },
                MaterialOverride = Glow(i % 3 == 0 ? new Color(1f, 0.97f, 0.9f) : _colour),
                Position = origin,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(node);
            _shards.Add(new Shard
            {
                Node = node,
                Velocity = dir * rng.RandfRange(2.2f, 5.0f),
                Spin = new Vector3(rng.RandfRange(-12f, 12f), rng.RandfRange(-12f, 12f), rng.RandfRange(-12f, 12f)),
            });
        }
    }

    public override void _Process(double delta)
    {
        _age += delta;
        if (_age >= Life)
        {
            QueueFree();
            return;
        }

        if (_portal)
        {
            // The blade sweeps along the arc, then burns out.
            double sweep = Math.Clamp(_age / ArcSweepSeconds, 0, 1);
            double burn = Math.Clamp((_age - ArcSweepSeconds) / 0.22, 0, 1);
            for (int i = 0; i < _arc.Count; i++)
            {
                var seg = _arc[i];
                seg.Visible = sweep * _arc.Count > i && burn < 1;
                if (seg.MaterialOverride is StandardMaterial3D m) m.AlbedoColor = new Color(m.AlbedoColor, (float)(1 - burn));
            }

            if (_snap != null)
            {
                double snap = Math.Clamp((_age - ArcSweepSeconds * 0.5) / SnapSeconds, 0, 1);
                double ease = snap * snap * snap;
                _snap.Scale = new Vector3((float)Math.Max(0.02, 1 - ease), 1, 1);
                if (_snap.MaterialOverride is StandardMaterial3D sm)
                    sm.AlbedoColor = new Color(_colour.Lerp(Colors.White, (float)(1 - snap)), (float)(snap < 1 ? 0.9 : Math.Max(0, 1 - (_age - ArcSweepSeconds * 0.5 - SnapSeconds) / 0.12)));
            }
        }

        float fade = (float)Math.Clamp(1 - _age / Life, 0, 1);
        foreach (var s in _shards)
        {
            s.Velocity += Vector3.Down * 9f * (float)delta;
            s.Node.Position += s.Velocity * (float)delta;
            s.Node.Rotation += s.Spin * (float)delta;
            if (s.Node.MaterialOverride is StandardMaterial3D m) m.AlbedoColor = new Color(m.AlbedoColor, fade);
        }
    }

    private static Vector3 V(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);
}
