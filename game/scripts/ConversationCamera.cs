using Godot;

namespace Hollowfall.Game;

/// <summary>
/// Frames the speaker while a conversation panel is open. The panel sits low on the screen and is as tall as its text needs, so the
/// camera eases back a little and turns to put the speaker's whole figure in the clear part of the screen above it, a little to one side,
/// and eases home when the conversation ends. It only moves the camera, never the player, and it runs while the game is paused. The
/// pose it blends toward is worked out from where the panel's top edge actually is, so it holds at any window size and for any
/// amount of text.
/// </summary>
public partial class ConversationCamera : Node
{
    /// <summary>How tall the framed figure is, in blocks, including ears, branch and rock.</summary>
    public const double FigureHeight = 3.0;

    /// <summary>Where the focus point sits across the screen, from -1 (left edge) to 1, so the panel and the figure do not share a column.</summary>
    public const double ScreenX = -0.26;

    private const double Margin = 0.04, PanelGap = 0.05;
    private const double FadeSeconds = 0.55;
    private const double MaxDolly = 8.0;

    private PlayerController _player = null!;
    private StoryUi _ui = null!;
    private Vector3? _focus;
    private double _amount, _dolly, _ndcY = 0.3;
    private bool _applied;

    /// <summary>How far into the framing the camera is, from 0 (the player's own view) to 1.</summary>
    public double Amount => _amount;

    /// <summary>True from the moment framing starts until the camera is fully home again.</summary>
    public bool Active => _focus != null || _amount > 0.001;

    /// <summary>How far back (in blocks) the camera is drawn at full framing, as of now.</summary>
    public double Dolly => _dolly;

    /// <summary>Where the focus sits on screen vertically at full framing, in normalised device coordinates (1 is the top).</summary>
    public double ScreenY => _ndcY;

    public void Initialize(PlayerController player, StoryUi ui)
    {
        _player = player;
        _ui = ui;
        ProcessMode = ProcessModeEnum.Always;
    }

    /// <summary>Starts framing the point <paramref name="focus"/> (the middle of the speaker's body, in world space).</summary>
    public void Begin(Vector3 focus) => _focus = focus;

    /// <summary>Lets the camera go home.</summary>
    public void End() => _focus = null;

    public override void _Process(double delta)
    {
        if (_player == null || !IsInstanceValid(_player)) return;
        double target = _focus != null ? 1 : 0;
        double step = delta / FadeSeconds;
        _amount = _amount < target ? Math.Min(target, _amount + step) : Math.Max(target, _amount - step);
        if (_amount <= 0.0001 && _focus == null)
        {
            if (_applied) Restore();
            return;
        }

        var camera = _player.Camera;
        var focus = _focus ?? _lastFocus;
        _lastFocus = focus;
        var eye = _player.GlobalPosition;

        // The clear part of the screen is above the panel; the figure must fit in it with a margin.
        double viewH = Math.Max(_ui.GetViewport().GetVisibleRect().Size.Y, 1);
        double viewW = Math.Max(_ui.GetViewport().GetVisibleRect().Size.X, 1);
        double top = Math.Clamp(_ui.DialoguePanelTop, 0.35, 1.0) - PanelGap;
        double avail = Math.Max(top - Margin, 0.2);
        double tanV = Math.Tan(Mathf.DegToRad(camera.Fov) / 2);
        double tanH = tanV * viewW / viewH;

        var flat = new Vector3(eye.X - focus.X, 0, eye.Z - focus.Z);
        double near = flat.Length();
        var away = near > 0.01 ? flat / (float)near : new Vector3(Mathf.Sin((float)_player.Yaw), 0, Mathf.Cos((float)_player.Yaw));
        double seen = Math.Sqrt(near * near + Math.Pow(eye.Y - focus.Y, 2));

        // Back off until the whole figure takes about 90% of the clear space, but never come closer than the player already is.
        double wantedDistance = FigureHeight / (tanV * avail * 2 * 0.88);
        double wantedDolly = Math.Clamp(wantedDistance - seen, 0, MaxDolly);
        double wantedY = 1 - 2 * (Margin + avail / 2);
        double ease = 1 - Math.Exp(-6 * delta);
        if (!_applied) { _dolly = wantedDolly; _ndcY = wantedY; }
        else if (_ui.DialogueOpen) { _dolly += (wantedDolly - _dolly) * ease; _ndcY += (wantedY - _ndcY) * ease; } // once it closes, hold the pose and just ease home

        var posF = eye + away * (float)_dolly + new Vector3(0, 0.15f, 0);
        var w = (focus - posF).Normalized();
        double yawW = Math.Atan2(-w.X, -w.Z), pitchW = Math.Asin(Math.Clamp(w.Y, -1, 1));
        double yawF = yawW + Math.Atan(ScreenX * tanH);
        double pitchF = pitchW - Math.Atan(_ndcY * tanV);

        float e = (float)(_amount * _amount * (3 - 2 * _amount)); // smoothstep
        var pos = eye.Lerp(posF, e);
        double yaw0 = _player.Yaw, pitch0 = _player.Pitch;
        double yaw = yaw0 + Mathf.Wrap(yawF - yaw0, -Math.PI, Math.PI) * e;
        double pitch = pitch0 + (pitchF - pitch0) * e;
        camera.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3((float)pitch, (float)yaw, 0)), pos);
        _applied = true;
    }

    private Vector3 _lastFocus;

    private void Restore()
    {
        _applied = false;
        var camera = _player.Camera;
        camera.Position = Vector3.Zero;
        camera.Rotation = new Vector3((float)_player.Pitch, 0, 0);
    }
}
