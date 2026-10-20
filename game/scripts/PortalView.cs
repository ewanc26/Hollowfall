using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Portals;

namespace Hollowfall.Game;

/// <summary>
/// Draws one portal: a glowing 1 x 2 frame that, when linked, shows the world as seen from the other portal. A
/// second camera is placed where the viewer would be if the pair were one doorway (the main camera's position and
/// orientation carried through the pair), and its image is drawn on the portal using screen coordinates, so it
/// lines up with the room behind it. The shader is built in code because the project is C# only.
/// </summary>
public partial class PortalView : Node3D
{
    /// <summary>Portal quads live on their own render layer so portal cameras do not draw portals inside portals.</summary>
    public const uint PortalLayer = 1u << 1;

    /// <summary>
    /// A plain glowing frame, drawn only by portal cameras, so portals seen through other portals show up as frames
    /// instead of vanishing into the wall. (Full recursion would need each view to be drawn from its own camera.)
    /// </summary>
    public const uint FrameLayer = 1u << 2;

    private const string ShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_back;

        uniform sampler2D view_tex : source_color, filter_linear;
        uniform vec4 rim : source_color = vec4(0.2, 0.5, 1.0, 1.0);
        uniform float linked = 0.0;

        void fragment() {
            // Distance to the quad's edge, in blocks (the quad is 1 wide, 2 tall).
            float edge = min(min(UV.x, 1.0 - UV.x) * 1.0, min(UV.y, 1.0 - UV.y) * 2.0);
            float rim_amount = smoothstep(0.14, 0.02, edge);
            vec3 inside = linked > 0.5 ? texture(view_tex, SCREEN_UV).rgb : rim.rgb * 0.18;
            ALBEDO = mix(inside, rim.rgb * 1.6, rim_amount);
            ALPHA = 1.0;
        }
        """;

    private readonly Color _colour;
    private MeshInstance3D _quad = null!;
    private MeshInstance3D _frameQuad = null!;
    private ShaderMaterial _material = null!;
    private SubViewport _viewport = null!;
    private Camera3D _camera = null!;

    public Portal Portal { get; }

    public PortalView(Portal portal)
    {
        Portal = portal;
        _colour = portal.Color == PortalColor.Blue ? new Color(0.15f, 0.55f, 1f) : new Color(1f, 0.55f, 0.1f);
    }

    public override void _Ready()
    {
        var f = Portal.Frame;
        // Quad faces +Z with width along X and height along Y; (Right, Up, Normal) is exactly that basis.
        var basis = new Basis(V(f.Right), V(f.Up), V(f.Normal));
        _quad = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2((float)Portal.Width, (float)Portal.Height) },
            Transform = new Transform3D(basis, V(f.Center + f.Normal * 0.012)),
            Layers = PortalLayer,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        _material.SetShaderParameter("rim", _colour);
        _quad.MaterialOverride = _material;
        AddChild(_quad);

        var frameMaterial = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        frameMaterial.SetShaderParameter("rim", _colour);
        frameMaterial.SetShaderParameter("linked", 0.0f);
        _frameQuad = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2((float)Portal.Width, (float)Portal.Height) },
            Transform = new Transform3D(basis, V(f.Center + f.Normal * 0.008)),
            Layers = FrameLayer,
            MaterialOverride = frameMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_frameQuad);

        _viewport = new SubViewport
        {
            Size = new Vector2I(960, 540),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            HandleInputLocally = false,
            Msaa3D = Viewport.Msaa.Disabled,
        };
        AddChild(_viewport);
        _camera = new Camera3D { Current = true, CullMask = 1 | FrameLayer, Far = 300, Fov = 80 };
        _viewport.AddChild(_camera);
        _material.SetShaderParameter("view_tex", _viewport.GetTexture());
    }

    private static Vector3 V(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    /// <summary>Points the portal's camera where the viewer would be if the pair were one doorway, and switches it on when seen.</summary>
    public void UpdateView(Portal? other, Camera3D viewer)
    {
        bool linked = other != null;
        _material.SetShaderParameter("linked", linked ? 1.0f : 0.0f);
        if (!linked)
        {
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            return;
        }

        var eye = viewer.GlobalPosition;
        var eyeVec = new Vec3(eye.X, eye.Y, eye.Z);
        var (_, _, c) = Portal.Frame.ToLocal(eyeVec);

        // Only draw when the viewer is in front of this portal and it is roughly on screen.
        var toPortal = V(Portal.Frame.Center) - eye;
        bool inFront = c > 0.05;
        bool onScreen = !viewer.IsPositionBehind(V(Portal.Frame.Center)) || toPortal.Length() < 2;
        if (!inFront || !onScreen)
        {
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            return;
        }

        var forward = -viewer.GlobalTransform.Basis.Z;
        var up = viewer.GlobalTransform.Basis.Y;
        var exit = other!;
        var pos = PortalTransform.Point(Portal, exit, eyeVec);
        var fwd = PortalTransform.Vector(Portal, exit, new Vec3(forward.X, forward.Y, forward.Z));
        var upv = PortalTransform.Vector(Portal, exit, new Vec3(up.X, up.Y, up.Z));

        var origin = V(pos);
        var look = origin + V(fwd);
        _camera.GlobalPosition = origin;
        _camera.LookAt(look, V(upv));

        // The camera sits inside the wall behind the exit portal, so its near plane must start at the portal's opening.
        // Seen at an angle, the closest corner of the opening (measured along the view direction) is the right distance:
        // it clips the wall in front of the camera without cutting into what the opening shows.
        var fwdUnit = V(fwd).Normalized();
        double nearest = double.MaxValue;
        foreach (var (sr, su) in new[] { (-0.5, -1.0), (0.5, -1.0), (0.5, 1.0), (-0.5, 1.0) })
        {
            var corner = exit.Frame.Center + exit.Frame.Right * sr + exit.Frame.Up * su;
            double depth = (V(corner) - origin).Dot(fwdUnit);
            if (depth > 0) nearest = Math.Min(nearest, depth);
        }
        _camera.Near = (float)Math.Clamp(nearest == double.MaxValue ? 0.05 : nearest * 0.98, 0.05, 40);
        _camera.Fov = viewer.Fov;

        var size = new Vector2I(Math.Max(64, (int)viewer.GetViewport().GetVisibleRect().Size.X), Math.Max(64, (int)viewer.GetViewport().GetVisibleRect().Size.Y));
        if (_viewport.Size != size) _viewport.Size = size;
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
    }
}
