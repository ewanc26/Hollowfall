using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Input;
using Hollowfall.Core.Physics;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Game;

/// <summary>
/// First-person walker. Movement and collision come from Core's <see cref="PlayerBody"/> and portals from
/// <see cref="PortalWalker"/>, so the game feels exactly like what the tests verify. Input comes from the action map
/// (<see cref="InputMapper"/>), so keyboard, mouse and gamepad all work and every binding can be changed; the look and
/// dead-zone maths is Core's <see cref="InputMath"/>.
/// </summary>
public partial class PlayerController : Node3D
{
    private const double EyeHeight = 1.62;
    private const double FlySpeed = 14;

    private Camera3D _camera = null!;
    private OmniLight3D _torch = null!;
    private VoxelWorld _world = null!;
    private double _yaw, _pitch;

    public PlayerBody Body { get; private set; } = null!;
    public bool Flying { get; private set; }
    public Camera3D Camera => _camera;
    public GameSettings Settings { get; set; } = new();

    /// <summary>True when the last thing the player touched was a gamepad, so hints can show its buttons.</summary>
    public bool UsingGamepad { get; private set; }

    /// <summary>Raised when the player goes through a portal, so the view can react.</summary>
    public event Action<Crossing>? Crossed;

    public void Initialize(VoxelWorld world, Vector3 feet, double yaw)
    {
        _world = world;
        Body = new PlayerBody(feet.X, feet.Y, feet.Z);
        _yaw = yaw;
    }

    public override void _Ready()
    {
        _camera = new Camera3D { Current = true, Fov = (float)Settings.FieldOfView, Far = 400 };
        AddChild(_camera);
        _torch = new OmniLight3D { OmniRange = 16, LightEnergy = 1.3f, LightColor = new Color(1f, 0.85f, 0.65f) };
        _camera.AddChild(_torch);
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseMotion motion when Input.MouseMode == Input.MouseModeEnum.Captured:
                UsingGamepad = false;
                var (yaw, pitch) = InputMath.MouseLook(motion.Relative.X, motion.Relative.Y, Settings.MouseSensitivity, Settings.InvertMouseY);
                _yaw += yaw;
                _pitch = InputMath.ClampPitch(_pitch + pitch);
                break;
            case InputEventMouseButton { Pressed: true } when Input.MouseMode != Input.MouseModeEnum.Captured:
                Input.MouseMode = Input.MouseModeEnum.Captured;
                UsingGamepad = false;
                break;
            case InputEventKey { Pressed: true }:
                UsingGamepad = false;
                break;
            case InputEventJoypadButton { Pressed: true }:
                UsingGamepad = true;
                break;
            case InputEventJoypadMotion { AxisValue: > 0.5f or < -0.5f }:
                UsingGamepad = true;
                break;
        }
    }

    public Vec3 Forward
    {
        get
        {
            double cp = Math.Cos(_pitch);
            return new Vec3(-Math.Sin(_yaw) * cp, Math.Sin(_pitch), -Math.Cos(_yaw) * cp);
        }
    }

    public Vec3 EyePosition => new(Body.X, Body.Y + EyeHeight, Body.Z);

    public PlayerView View => new(new Vec3(Body.X, Body.Y, Body.Z), EyePosition, Forward, !Body.OnGround, false);

    /// <summary>True the frame the respawn action was pressed.</summary>
    public bool RespawnRequested => InputMapper.JustPressed(GameAction.Respawn);

    /// <summary>
    /// Advances the player one fixed step and returns this step's button presses. Portals, if any, are walked through.
    /// Called by the director so movement always happens before combat.
    /// </summary>
    public PlayerActions Step(double delta, PortalSystem? portals, bool dead)
    {
        _camera.Fov = (float)Settings.FieldOfView;
        ApplyStickLook(delta);

        bool live = !dead && Input.MouseMode == Input.MouseModeEnum.Captured || !dead && UsingGamepad;
        var actions = new PlayerActions(
            FireRevolver: live && InputMapper.Pressed(GameAction.FirePrimary),
            FireShotgun: live && InputMapper.Pressed(GameAction.FireSecondary),
            Parry: live && InputMapper.JustPressed(GameAction.Parry),
            PortalTeal: live && InputMapper.JustPressed(GameAction.PortalTeal),
            PortalViolet: live && InputMapper.JustPressed(GameAction.PortalViolet));

        if (!dead)
        {
            if (InputMapper.JustPressed(GameAction.FlyToggle)) Flying = !Flying;
            Move(delta, portals);
        }
        UpdateCamera();
        return actions;
    }

    private void ApplyStickLook(double delta)
    {
        var stick = Input.GetVector(InputMapper.Name(GameAction.LookLeft), InputMapper.Name(GameAction.LookRight),
            InputMapper.Name(GameAction.LookUp), InputMapper.Name(GameAction.LookDown), (float)Settings.Deadzone);
        var (yaw, pitch) = InputMath.StickLook(stick.X, stick.Y, 0, Settings.PadLookSpeed, delta, Settings.InvertPadY);
        _yaw += yaw;
        _pitch = InputMath.ClampPitch(_pitch + pitch);
    }

    private void Move(double delta, PortalSystem? portals)
    {
        var stick = Input.GetVector(InputMapper.Name(GameAction.MoveLeft), InputMapper.Name(GameAction.MoveRight),
            InputMapper.Name(GameAction.MoveForward), InputMapper.Name(GameAction.MoveBack), (float)Settings.Deadzone);
        // Godot's vector has "up" negative; the maths wants forward positive.
        var (mx, mz) = InputMath.WorldMove(stick.X, -stick.Y, _yaw);

        if (Flying)
        {
            double vertical = (InputMapper.Pressed(GameAction.Jump) ? 1 : 0) - (InputMapper.Pressed(GameAction.FlyDown) ? 1 : 0);
            Body.X += mx * FlySpeed * delta;
            Body.Z += mz * FlySpeed * delta;
            Body.Y = Math.Clamp(Body.Y + vertical * FlySpeed * delta, 1, Chunk.Height - 3);
            return;
        }

        bool jump = InputMapper.Pressed(GameAction.Jump);
        if (portals != null && PortalWalker.Step(Body, portals, delta, mx, mz, jump, _world.GetBlock, Forward) is { } crossing)
        {
            // Turn to face where the portal sends us, keeping the view upright.
            var f = crossing.Forward.Normalized();
            _yaw = Math.Atan2(-f.X, -f.Z);
            _pitch = InputMath.ClampPitch(Math.Asin(Math.Clamp(f.Y, -1, 1)));
            Crossed?.Invoke(crossing);
        }
        else if (portals == null)
        {
            Body.Step(delta, mx, mz, jump, _world.GetBlock);
        }
    }

    private void UpdateCamera()
    {
        Position = new Vector3((float)Body.X, (float)(Body.Y + EyeHeight), (float)Body.Z);
        Rotation = new Vector3(0, (float)_yaw, 0);
        _camera.Rotation = new Vector3((float)_pitch, 0, 0);
    }

    /// <summary>Puts the player somewhere new (respawns, debug moves) and resets their velocity.</summary>
    public void PlaceAt(Vector3 feet, double yaw)
    {
        Body.Teleport(feet.X, feet.Y, feet.Z, 0, 0, 0);
        _yaw = yaw;
        _pitch = 0;
        UpdateCamera();
    }
}
