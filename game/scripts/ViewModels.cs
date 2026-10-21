using Godot;
using Hollowfall.Core.Models;

namespace Hollowfall.Game;

/// <summary>
/// The weapons you hold: a revolver in the right hand and a sawed-off shotgun in the left, drawn by a second camera in its own
/// small world and laid over the game. Because they have their own depth, they never poke into walls, and they are low-poly
/// models posed in code: they sway as you look, bob as you walk, kick on every shot (the revolver's cylinder turns and its hammer
/// falls), and each carries a gem that lights up in a portal's colour once that portal has been placed.
/// </summary>
public partial class ViewModels : SubViewportContainer
{
    private const float HandScale = 1.7f;

    private SubViewport _viewport = null!;
    private ModelRig _revolver = null!, _shotgun = null!;
    private Node3D _revolverHand = null!, _shotgunHand = null!;
    private OmniLight3D _flash = null!;
    private double _bob, _time;
    private double _revolverKick, _shotgunKick, _cylinder, _flashTime;
    private Vector2 _sway;
    private readonly List<Rect2> _openings = [];

    public ViewModels()
    {
        Stretch = true;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        _viewport = new SubViewport
        {
            TransparentBg = true,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            HandleInputLocally = false,
        };
        AddChild(_viewport);

        _viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0, 0, 0, 0),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.62f, 0.64f, 0.72f),
                AmbientLightEnergy = 0.75f,
            },
        });
        _viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35, -25, 0), LightEnergy = 1.25f });
        _viewport.AddChild(new Camera3D { Current = true, Fov = 58, Near = 0.02f, Far = 5 });

        _revolver = ModelFactory.Build(ModelCatalog.Revolver, castShadows: false);
        _revolverHand = new Node3D { Name = "RevolverHand" };
        _revolverHand.AddChild(_revolver.Root);
        _viewport.AddChild(_revolverHand);

        _shotgun = ModelFactory.Build(ModelCatalog.Shotgun, castShadows: false);
        _shotgunHand = new Node3D { Name = "ShotgunHand" };
        _shotgunHand.AddChild(_shotgun.Root);
        _viewport.AddChild(_shotgunHand);

        _flash = new OmniLight3D { OmniRange = 1.5f, LightEnergy = 0, LightColor = new Color(1f, 0.8f, 0.4f) };
        _viewport.AddChild(_flash);

        SetPortals(false, false);
    }

    /// <summary>Lights the weapons' gems to show which portals exist.</summary>
    public void SetPortals(bool teal, bool violet)
    {
        _revolver.SetGlow("gem", teal ? Colors.White * 1.5f : new Color(0.12f, 0.16f, 0.17f));
        _shotgun.SetGlow("gem", violet ? Colors.White * 1.5f : new Color(0.14f, 0.1f, 0.18f));
    }

    /// <summary>Kicks the weapon that fired and flashes its muzzle.</summary>
    public void Fire(string weapon)
    {
        if (weapon == "shotgun")
        {
            _shotgunKick = 1;
            Flash(ModelCatalog.Shotgun, _shotgunHand, 3.2f);
        }
        else
        {
            _revolverKick = 1;
            _cylinder += 60;
            Flash(ModelCatalog.Revolver, _revolverHand, 2.0f);
        }
    }

    private void Flash(LowPolyModel model, Node3D hand, float energy)
    {
        _flashTime = 0.07;
        _flash.LightEnergy = energy;
        if (model.Anchors.TryGetValue("muzzle", out var muzzle))
            _flash.Position = hand.Position + hand.Transform.Basis * (ModelFactory.ToVector(muzzle) * HandScale);
    }

    /// <summary>Weapons that would sit under a portal opening fade out, so nothing covers the view through it.</summary>
    public void SetOpenings(IReadOnlyList<Rect2> screenRects)
    {
        _openings.Clear();
        _openings.AddRange(screenRects.Select(r => r.Grow(20)));
    }

    /// <summary>Advances the pose. <paramref name="speed"/> is the player's speed, <paramref name="look"/> the turn this frame in radians.</summary>
    public void Pose(double delta, double speed, bool onGround, Vector2 look)
    {
        _time += delta;
        _bob += speed * (onGround ? 1.9 : 0.4) * delta;
        double walk = onGround ? Math.Clamp(speed / 4.3, 0, 1) : 0.15;

        // Sway lags behind the view, so turning feels weighty.
        _sway = _sway.Lerp(-look * 6f, (float)Math.Min(1, delta * 9));
        _sway = _sway.Clamp(new Vector2(-0.14f, -0.1f), new Vector2(0.14f, 0.1f));

        _revolverKick = Math.Max(0, _revolverKick - delta * 7);
        _shotgunKick = Math.Max(0, _shotgunKick - delta * 5);
        _flashTime -= delta;
        if (_flashTime <= 0) _flash.LightEnergy = 0;

        PoseHand(_revolverHand, new Vector3(0.2f, -0.17f, -0.42f), side: 1, _revolverKick, 11);
        PoseHand(_shotgunHand, new Vector3(-0.23f, -0.19f, -0.46f), side: -1, _shotgunKick, 16);

        // The cylinder turns a click on each shot and the hammer falls and rises with the kick.
        if (_revolver.Part("cylinder") is { } cylinder)
            cylinder.RotationDegrees = new Vector3(0, 0, (float)_cylinder);
        if (_revolver.Part("hammer") is { } hammer)
            hammer.RotationDegrees = new Vector3((float)(-_revolverKick * 30), 0, 0);

        _cylinder = Mathf.Lerp(_cylinder, Math.Round(_cylinder / 60) * 60, Math.Min(1, delta * 20));
        double breathe = Math.Sin(_time * 1.6) * 0.003;
        _ = breathe;

        double fade = _openings.Any(o => o.Intersects(WeaponArea(left: true)) || o.Intersects(WeaponArea(left: false))) ? 0 : 1;
        Modulate = new Color(1, 1, 1, Mathf.MoveToward(Modulate.A, (float)fade, (float)(delta * 10)));
    }

    private Rect2 WeaponArea(bool left)
    {
        var size = GetViewportRect().Size;
        float w = size.X * 0.34f, h = size.Y * 0.5f;
        return new Rect2(left ? 0 : size.X - w, size.Y - h, w, h);
    }

    private void PoseHand(Node3D hand, Vector3 rest, int side, double kick, double kickDegrees)
    {
        double walk = Math.Sin(_bob * side) * 0.012, step = Math.Abs(Math.Cos(_bob)) * 0.012;
        hand.Scale = Vector3.One * HandScale;
        hand.Position = rest + new Vector3(_sway.X * 0.5f + (float)walk, -_sway.Y * 0.4f - (float)step, (float)(kick * 0.07));
        hand.RotationDegrees = new Vector3((float)(kick * kickDegrees + _sway.Y * 20), (float)(-side * 3 + _sway.X * 25), (float)(-side * 2 + walk * 80));
    }
}
