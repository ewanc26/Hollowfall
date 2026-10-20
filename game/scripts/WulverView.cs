using Godot;
using Hollowfall.Core.Models;
using Hollowfall.Core.Story;

namespace Hollowfall.Game;

/// <summary>
/// Draws the Wulver standing on his rock and gives him a quiet idle: slow breathing, a head that sweeps the horizon and turns to look
/// at you when you come near, a branch of yew that sways, and a faint warm light. A fish lies at his feet once his gift has been given.
/// Where he stands is decided in Core (<see cref="WulverPlacer"/>); this only draws him.
/// </summary>
public partial class WulverView : Node3D
{
    /// <summary>How close the player must be for him to turn and look, in blocks (the same range at which he greets them).</summary>
    public const double NoticeRange = WulverEncounter.NoticeRange;

    private ModelRig _rig = null!;
    private OmniLight3D _light = null!;
    private WulverSpot _spot;
    private double _time;
    private float _look;

    public WulverSpot Spot => _spot;

    /// <summary>The head's turn about the vertical, in radians (for tests and screenshots).</summary>
    public float HeadYaw => _rig.Part("head")?.Rotation.Y ?? 0;

    public bool FishVisible => _rig.Part("fish")?.Visible ?? false;

    public ModelRig Rig => _rig;

    public void Initialize(WulverSpot spot, bool giftTaken)
    {
        _spot = spot;
        _rig = ModelFactory.Build(ModelCatalog.Wulver);
        _rig.Root.Position = new Vector3((float)spot.X, (float)spot.Y, (float)spot.Z);
        _rig.Root.Rotation = new Vector3(0, (float)spot.Yaw, 0);
        AddChild(_rig.Root);

        _light = new OmniLight3D
        {
            Name = "Glow",
            LightColor = new Color(1f, 0.72f, 0.38f),
            LightEnergy = 0.55f,
            OmniRange = 6f,
            ShadowEnabled = false,
            Position = new Vector3((float)spot.X, (float)spot.Y + 0.9f, (float)spot.Z),
        };
        AddChild(_light);
        ProcessMode = ProcessModeEnum.Always;
        _fishBase = _rig.Part("fish")?.Position ?? Vector3.Zero;
        SetGiftTaken(giftTaken);
    }

    /// <summary>How long the fish takes to pop into being, and how long the warm light he gives off lingers, in seconds.</summary>
    public const double PopSeconds = 0.55, PulseSeconds = 1.6;

    private double _fishAge = -1, _pulseAge = -1;
    private Vector3 _fishBase;
    private float _idleEnergy = 0.55f;

    /// <summary>True while the fish is still popping into place.</summary>
    public bool FishPopping => _fishAge >= 0;

    /// <summary>The fish's current scale (0 before it appears, 1 at rest), for tests and screenshots.</summary>
    public float FishScale => _rig.Part("fish")?.Scale.X ?? 0;

    /// <summary>How strong the warm pulse is now, from 0 (none) to 1 (just given).</summary>
    public float Pulse => _pulseAge < 0 ? 0 : (float)Math.Max(0, 1 - _pulseAge / PulseSeconds);

    /// <summary>
    /// The fish appears once the gift is given. With <paramref name="animate"/> it pops in at his feet with a little hop and a soft
    /// warm pulse of light swells round him; without it (a saved state, a return visit) it simply lies there.
    /// </summary>
    public void SetGiftTaken(bool taken, bool animate = false)
    {
        if (_rig.Part("fish") is not { } fish) return;
        if (taken && animate && !fish.Visible)
        {
            fish.Visible = true;
            fish.Scale = Vector3.Zero;
            _fishAge = 0;
            _pulseAge = 0;
            return;
        }
        if (!taken || !FishPopping)
        {
            fish.Visible = taken;
            fish.Scale = Vector3.One;
            fish.Position = _fishBase;
        }
    }

    public override void _Process(double delta)
    {
        // The pop and the pulse run on real time, since the game is paused behind the conversation when the gift is given.
        if (_rig == null) return;
        if (_fishAge >= 0 && _rig.Part("fish") is { } fish)
        {
            _fishAge += delta;
            double f = Math.Clamp(_fishAge / PopSeconds, 0, 1);
            // Ease out with a slight overshoot, and a small hop on the way down.
            double c1 = 1.9, c3 = c1 + 1;
            double scale = 1 + c3 * Math.Pow(f - 1, 3) + c1 * Math.Pow(f - 1, 2);
            fish.Scale = Vector3.One * (float)Math.Max(scale, 0);
            fish.Position = _fishBase + new Vector3(0, (float)(0.35 * Math.Sin(Math.PI * f) * (1 - f)), 0);
            if (f >= 1)
            {
                _fishAge = -1;
                fish.Scale = Vector3.One;
                fish.Position = _fishBase;
            }
        }
        if (_pulseAge >= 0)
        {
            _pulseAge += delta;
            if (_pulseAge >= PulseSeconds) _pulseAge = -1;
        }
        ApplyLight();
    }

    private void ApplyLight()
    {
        float pulse = Pulse;
        _light.LightEnergy = _idleEnergy + 2.4f * pulse;
        _light.OmniRange = 6f + 3f * pulse;
    }

    /// <summary>Advances the idle. <paramref name="player"/> is where the player's feet are.</summary>
    public void Update(double delta, Vector3 player)
    {
        _time += delta;
        float t = (float)_time;

        if (_rig.Part("body") is { } body)
        {
            body.Scale = new Vector3(1 + 0.01f * Mathf.Sin(t * 1.5f), 1 + 0.018f * Mathf.Sin(t * 1.5f), 1 + 0.01f * Mathf.Sin(t * 1.5f));
            body.Rotation = new Vector3(0.03f * Mathf.Sin(t * 0.75f), 0, 0.012f * Mathf.Sin(t * 0.5f));
        }

        // The head sweeps slowly across the view, or follows the player when they are near.
        var toPlayer = player - new Vector3((float)_spot.X, 0, (float)_spot.Z);
        toPlayer.Y = 0;
        float wanted = 0.35f * Mathf.Sin(t * 0.4f) + 0.1f * Mathf.Sin(t * 1.1f);
        float pitch = 0.04f * Mathf.Sin(t * 0.6f);
        if (toPlayer.Length() < NoticeRange)
        {
            // Local angle of the player relative to where he faces (-Z turned by the yaw).
            float world = Mathf.Atan2(-toPlayer.X, -toPlayer.Z);
            float local = Mathf.Wrap(world - (float)_spot.Yaw, -Mathf.Pi, Mathf.Pi);
            wanted = Mathf.Clamp(local, -1.15f, 1.15f);
            pitch = 0.08f;
        }
        _look += (wanted - _look) * (1f - Mathf.Exp(-2.2f * (float)delta));
        if (_rig.Part("head") is { } head) head.Rotation = new Vector3(pitch, _look, 0);

        if (_rig.Part("arm_r") is { } arm) arm.Rotation = new Vector3(0.02f * Mathf.Sin(t * 0.9f), 0, 0.025f * Mathf.Sin(t * 0.7f));
        if (_rig.Part("branch") is { } branch) branch.Rotation = new Vector3(0.05f * Mathf.Sin(t * 1.3f + 1f), 0, 0.07f * Mathf.Sin(t * 1.0f));
        if (_rig.Part("arm_l") is { } left) left.Rotation = new Vector3(0.03f * Mathf.Sin(t * 0.8f + 2f), 0, -0.02f * Mathf.Sin(t * 0.6f));

        _idleEnergy = 0.5f + 0.06f * Mathf.Sin(t * 2.3f);
        ApplyLight();
    }
}
