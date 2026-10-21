using Godot;
using Hollowfall.Core.Models;
using Hollowfall.Core.Story;

namespace Hollowfall.Game;

/// <summary>
/// Draws the cat and gives him his moods, all from state with no animation files. Far away he is asleep, curled on his rug with his tail
/// round him, breathing slowly and with his eyes shut. When you come within <see cref="CatEncounter.NoticeRange"/> he wakes and sits up, his
/// head follows you, his ears turn, his tail sways, and when you look at him he gives a slow blink. He is nowhere near the flames: he
/// has his own stone seat and rug beside the pit, and no part of him is a hit box. Where he rests is decided in Core (<see cref="CatPlacer"/>).
/// </summary>
public partial class CatView : Node3D
{
    private ModelRig _rig = null!;
    private CatSpot _spot;
    private double _time;
    private float _awake;
    private float _look;
    private float _lookPitch;
    private double _blinkAge = 99, _nextBlink = 3;
    private double _gazeCooldown;
    private bool _woke;

    /// <summary>Keeps him asleep whatever you do (for close-up screenshots).</summary>
    public bool ForceSleep { get; set; }

    public CatSpot Spot => _spot;
    public ModelRig Rig => _rig;

    /// <summary>0 asleep, 1 sitting up and watching.</summary>
    public float Awake => _awake;

    /// <summary>How open the eyes are, 0 to 1 (0 asleep or mid-blink).</summary>
    public float EyesOpen { get; private set; }

    /// <summary>The head's turn about the vertical, in radians, relative to the way he faces (for tests).</summary>
    public float HeadYaw => _look;

    /// <summary>How loud his purr should be, 0 to 1.</summary>
    public float PurrLevel { get; private set; }

    /// <summary>Raised once each time he wakes as you approach.</summary>
    public event Action? Woke;

    /// <summary>Raised when he gives a slow blink at someone looking at him.</summary>
    public event Action? SlowBlinked;

    public void Initialize(CatSpot spot)
    {
        _spot = spot;
        _rig = ModelFactory.Build(ModelCatalog.Cat);
        _rig.Root.Position = new Vector3((float)spot.X, (float)spot.Y, (float)spot.Z);
        _rig.Root.Rotation = new Vector3(0, (float)spot.Yaw, 0);
        AddChild(_rig.Root);
        Pose(0, new Vector3(1000, 0, 1000), Vector3.Forward);
    }

    /// <summary>Starts the slow blink now (also what happens when you look at him).</summary>
    public void SlowBlink()
    {
        if (_blinkAge < 2.0) return;
        _blinkAge = 0;
    }

    /// <summary>Advances the moods. <paramref name="player"/> is where the player's feet are and <paramref name="gaze"/> the way they look.</summary>
    public void Update(double delta, Vector3 player, Vector3 gaze) => Pose(delta, player, gaze);

    private void Pose(double delta, Vector3 player, Vector3 gaze)
    {
        _time += delta;
        float t = (float)_time;
        var here = new Vector3((float)_spot.X, (float)_spot.Y, (float)_spot.Z);
        var toPlayer = player - here;
        float distance = new Vector2(toPlayer.X, toPlayer.Z).Length();
        bool onFloor = Math.Abs(toPlayer.Y) <= 4;

        // Wake when you come near; fall asleep again once you have gone.
        bool near = !ForceSleep && onFloor && distance <= CatEncounter.NoticeRange;
        bool far = !onFloor || distance > CatEncounter.LeaveRange;
        float target = near ? 1 : far ? 0 : _awake > 0.5f ? 1 : 0;
        float rate = target > _awake ? 1.1f : 0.35f;
        _awake = Mathf.MoveToward(_awake, target, rate * (float)delta);
        if (_awake > 0.55f && !_woke) { _woke = true; Woke?.Invoke(); }
        if (_awake < 0.05f) _woke = false;
        float a = Smooth(_awake);

        // Eyes: shut asleep; open awake; a slow blink now and then, and always one when you look at him.
        _gazeCooldown -= delta;
        bool looked = _awake > 0.9f && distance < CatEncounter.NoticeRange && toPlayer.Length() > 0.1f && gaze.Normalized().Dot((-toPlayer).Normalized()) > 0.93f;
        if (looked && _gazeCooldown <= 0 && _blinkAge > 2.0)
        {
            _blinkAge = 0;
            _gazeCooldown = 10;
            SlowBlinked?.Invoke();
        }
        _blinkAge += delta;
        if (_awake > 0.9f && _blinkAge > 2.0 && t > _nextBlink)
        {
            _blinkAge = 0;
            _nextBlink = t + 5 + 3 * Mathf.Abs(Mathf.Sin(t * 0.37f));
        }
        float blink = 1f;
        if (_blinkAge < 1.7)
        {
            // Close slowly over half a second, hold, then open over most of a second: the "slow blink".
            double b = _blinkAge;
            blink = b < 0.5 ? 1 - (float)(b / 0.5) : b < 0.85 ? 0f : (float)Math.Min(1, (b - 0.85) / 0.85);
        }
        EyesOpen = Mathf.Clamp((a - 0.35f) / 0.65f, 0, 1) * blink;
        if (_rig.Part("eyes") is { } eyes)
        {
            eyes.Visible = EyesOpen > 0.08f;
            eyes.Scale = new Vector3(1, Mathf.Max(0.05f, EyesOpen), 1);
        }

        // The body: a loaf while asleep (breathing slowly), tipped up on its haunches when awake.
        float breath = Mathf.Sin(t * Mathf.Lerp(1.1f, 1.7f, a));
        float pitch = Mathf.Lerp(0f, 0.72f, a);
        if (_rig.Part("body") is { } body)
        {
            body.Rotation = new Vector3(pitch + 0.008f * breath, 0, 0);
            float swell = 0.012f + 0.01f * (1 - a);
            body.Scale = new Vector3(1 + swell * breath, 1 + swell * 1.6f * breath, 1);
        }

        // The head turns to follow you (clamped, so he never wrings his neck), tilted a little to be curious; asleep it rests low and to the side.
        float world = Mathf.Atan2(-toPlayer.X, -toPlayer.Z);
        float local = Mathf.Wrap(world - (float)_spot.Yaw, -Mathf.Pi, Mathf.Pi);
        float wantedLook = near ? Mathf.Clamp(local, -1.1f, 1.1f) : 0.25f * Mathf.Sin(t * 0.3f);
        float wantedPitch = near ? Mathf.Clamp(Mathf.Atan2(toPlayer.Y + 1.2f - 0.5f, Math.Max(distance, 0.5f)) * 0.5f, -0.1f, 0.3f) : 0;
        _look += (wantedLook - _look) * (1f - Mathf.Exp(-3f * (float)delta));
        _lookPitch += (wantedPitch - _lookPitch) * (1f - Mathf.Exp(-3f * (float)delta));
        // The head sits in the body's tipped frame, so undo the tip to keep it looking level, then add the look.
        var undo = new Basis(Vector3.Right, -pitch);
        var awakeBasis = undo * new Basis(Vector3.Up, _look) * new Basis(Vector3.Right, _lookPitch - 0.05f) * new Basis(Vector3.Back, 0.06f * Mathf.Sin(t * 0.8f));
        var sleepBasis = new Basis(Vector3.Up, 0.7f) * new Basis(Vector3.Right, -0.62f) * new Basis(Vector3.Back, 0.28f);
        if (_rig.Part("head") is { } head) head.Basis = new Basis(sleepBasis.GetRotationQuaternion().Slerp(awakeBasis.GetRotationQuaternion(), a));

        // Ears: laid back and low while asleep, up and turning when awake, with the odd twitch.
        float twitch = Mathf.Max(0, Mathf.Sin(t * 0.9f + 1f) - 0.93f) * 9f;
        if (_rig.Part("ear_l") is { } el) el.Rotation = new Vector3(Mathf.Lerp(0.4f, 0f, a) + 0.1f * twitch, 0, Mathf.Lerp(0.5f, 0.08f, a) + 0.06f * Mathf.Sin(t * 0.6f) * a);
        if (_rig.Part("ear_r") is { } er) er.Rotation = new Vector3(Mathf.Lerp(0.4f, 0f, a), 0, -Mathf.Lerp(0.5f, 0.08f, a) - 0.05f * Mathf.Sin(t * 0.5f + 2f) * a);

        // Legs: front paws tucked forward asleep, straight down awake; hind legs fold under.
        float fx = Mathf.Lerp(1.25f, -pitch, a);
        if (_rig.Part("leg_fl") is { } fl) fl.Rotation = new Vector3(fx, 0, 0.03f);
        if (_rig.Part("leg_fr") is { } fr) fr.Rotation = new Vector3(fx, 0, -0.03f);
        float hx = Mathf.Lerp(0f, -0.35f, a);
        if (_rig.Part("leg_bl") is { } bl) bl.Rotation = new Vector3(hx, 0, 0);
        if (_rig.Part("leg_br") is { } br) br.Rotation = new Vector3(hx, 0, 0);

        // The tail: wrapped round him asleep; awake it hangs behind and sways, the tip lagging the root.
        if (_rig.Part("tail_1") is { } t1)
            t1.Rotation = new Vector3(Mathf.Lerp(0f, 0.25f, a), Mathf.Lerp(1.15f, 0.25f * Mathf.Sin(t * 1.1f), a), 0);
        if (_rig.Part("tail_2") is { } t2)
            t2.Rotation = new Vector3(Mathf.Lerp(0f, 0.18f, a), Mathf.Lerp(1.35f, 0.4f * Mathf.Sin(t * 1.1f - 0.9f), a), 0);

        // He purrs when you are close, softly even in his sleep.
        PurrLevel = onFloor ? Mathf.Clamp((7f - distance) / 5f, 0, 1) * Mathf.Lerp(0.45f, 1f, a) : 0;
    }

    private static float Smooth(float x) => x * x * (3 - 2 * x);
}
