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
    /// <summary>How close the player must be for him to turn and look, in blocks.</summary>
    public const double NoticeRange = 14;

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
        SetGiftTaken(giftTaken);
    }

    /// <summary>The fish appears once the gift is given.</summary>
    public void SetGiftTaken(bool taken)
    {
        if (_rig.Part("fish") is { } fish) fish.Visible = taken;
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

        _light.LightEnergy = 0.5f + 0.06f * Mathf.Sin(t * 2.3f);
    }
}
