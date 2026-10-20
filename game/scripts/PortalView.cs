using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Portals;

namespace Hollowfall.Game;

/// <summary>
/// Draws one portal. The inside of the opening is exactly what a camera standing at the other portal would see: a second
/// camera is placed where the viewer would be if the pair were one doorway, and its image is shown unaltered by screen
/// coordinates, so it lines up with the room behind it. Nothing is drawn over that image: the glowing frame sits entirely
/// outside the opening, on the wall around it. The shader is built in code because the project is C# only.
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

    /// <summary>How far the glow frame reaches outside the opening, in blocks.</summary>
    public const double FrameWidth = 0.16;

    /// <summary>Seconds a new portal takes to open.</summary>
    public const double OpenSeconds = 0.28;

    private const string ShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_back, depth_draw_always;

        uniform sampler2D view_tex : source_color, filter_linear;
        uniform vec4 rim : source_color = vec4(0.1, 0.83, 0.77, 1.0);
        uniform float linked = 0.0;
        uniform float open_amount = 1.0;
        uniform float frame_width = 0.16;

        // The quad covers the opening (1 x 2 blocks) plus the frame around it. Coordinates are in blocks, centred, and the
        // opening is a rectangle with its corners cut by CHAMFER.
        const float CHAMFER = 0.3;

        float opening_edge(vec2 p) {
            vec2 a = abs(p);
            return min(min(0.5 - a.x, 1.0 - a.y), (0.5 + 1.0 - CHAMFER - a.x - a.y) * 0.7071);
        }

        void fragment() {
            vec2 p = (UV - vec2(0.5)) * vec2(1.0 + 2.0 * frame_width, 2.0 + 2.0 * frame_width);
            p /= max(open_amount, 0.001); // a new portal grows from its centre
            float edge = opening_edge(p);
            if (edge < -frame_width) discard;

            if (edge >= 0.0) {
                // Inside the opening: the other camera's image, exactly as rendered, with nothing laid over it.
                vec3 seen = texture(view_tex, SCREEN_UV).rgb;
                vec3 dark = rim.rgb * 0.12; // before it is linked there is nothing to see yet
                ALBEDO = linked > 0.5 ? seen : dark;
            } else {
                // The frame, outside the opening: a bright inner lip fading to a soft glow, with a slow pulse.
                float t = -edge / frame_width; // 0 at the opening's edge, 1 at the outer edge
                float pulse = 0.85 + 0.15 * sin(TIME * 2.2 + (p.x + p.y) * 6.0);
                float lip = smoothstep(0.35, 0.0, t);
                vec3 colour = rim.rgb * (1.0 + 0.9 * lip) * pulse;
                ALBEDO = mix(colour, rim.rgb * 0.45, smoothstep(0.55, 1.0, t));
            }
        }
        """;

    private readonly Color _colour;
    private MeshInstance3D _quad = null!;
    private MeshInstance3D _frameQuad = null!;
    private ShaderMaterial _material = null!;
    private SubViewport _viewport = null!;
    private Camera3D _camera = null!;
    private double _age;

    public Portal Portal { get; }

    public PortalView(Portal portal)
    {
        Portal = portal;
        _colour = portal.Color == PortalColor.Teal ? new Color(0.1f, 0.83f, 0.77f) : new Color(0.64f, 0.29f, 1f);
    }

    /// <summary>The portal's colour, for UI that refers to it.</summary>
    public Color Colour => _colour;

    public override void _Ready()
    {
        var f = Portal.Frame;
        // Quad faces +Z with width along X and height along Y; (Right, Up, Normal) is exactly that basis.
        var basis = new Basis(V(f.Right), V(f.Up), V(f.Normal));
        var size = new Vector2((float)(Portal.Width + 2 * FrameWidth), (float)(Portal.Height + 2 * FrameWidth));

        _quad = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = size },
            Transform = new Transform3D(basis, V(f.Center + f.Normal * 0.012)),
            Layers = PortalLayer,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        _material.SetShaderParameter("rim", _colour);
        _material.SetShaderParameter("frame_width", (float)FrameWidth);
        _material.SetShaderParameter("open_amount", 0.0f);
        _quad.MaterialOverride = _material;
        AddChild(_quad);

        // What portal cameras see instead: the frame and a dark opening, never another portal's image.
        var frameMaterial = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        frameMaterial.SetShaderParameter("rim", _colour);
        frameMaterial.SetShaderParameter("frame_width", (float)FrameWidth);
        frameMaterial.SetShaderParameter("linked", 0.0f);
        _frameQuad = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = size },
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
        _camera = new Camera3D { Current = true, CullMask = 1 | FrameLayer, Far = 400, Fov = 80 };
        _viewport.AddChild(_camera);
        _material.SetShaderParameter("view_tex", _viewport.GetTexture());
    }

    public override void _Process(double delta)
    {
        _age += delta;
        double t = Math.Clamp(_age / OpenSeconds, 0, 1);
        double eased = 1 - Math.Pow(1 - t, 3); // fast start, soft landing
        _material.SetShaderParameter("open_amount", (float)eased);
    }

    private static Vector3 V(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    /// <summary>
    /// The opening's outline on screen as seen by <paramref name="viewer"/>, or null if it is behind the camera or entirely
    /// off screen. Used to keep the HUD from drawing over the view through the portal.
    /// </summary>
    public Rect2? ScreenRect(Camera3D viewer)
    {
        var f = Portal.Frame;
        bool any = false;
        Rect2 rect = default;
        foreach (var (sr, su) in new[] { (-0.5, -1.0), (0.5, -1.0), (0.5, 1.0), (-0.5, 1.0) })
        {
            var corner = V(f.Center + f.Right * sr + f.Up * su);
            if (viewer.IsPositionBehind(corner)) return null;
            var screen = viewer.UnprojectPosition(corner);
            rect = any ? rect.Expand(screen) : new Rect2(screen, Vector2.Zero);
            any = true;
        }
        var window = viewer.GetViewport().GetVisibleRect();
        return any && rect.Intersects(window) ? rect : null;
    }

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

        // Match the viewer's lens and the main view's size and anti-aliasing, so the picture inside the opening is
        // indistinguishable from the world around it.
        _camera.Fov = viewer.Fov;
        _camera.KeepAspect = viewer.KeepAspect;
        var mainViewport = viewer.GetViewport();
        _viewport.Msaa3D = mainViewport.Msaa3D;
        _viewport.ScreenSpaceAA = mainViewport.ScreenSpaceAA;
        // The real pixels of the window, not the visible rect: the interface can be scaled, and the portal image must not be.
        var pixels = mainViewport.GetWindow()?.Size ?? new Vector2I((int)mainViewport.GetVisibleRect().Size.X, (int)mainViewport.GetVisibleRect().Size.Y);
        var size = new Vector2I(Math.Max(64, pixels.X), Math.Max(64, pixels.Y));
        if (_viewport.Size != size) _viewport.Size = size;
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
    }
}
