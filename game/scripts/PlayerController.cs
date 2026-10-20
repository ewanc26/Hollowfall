using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Physics;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Game;

/// <summary>
/// First-person walker. Movement and collision come from Core's <see cref="PlayerBody"/>, so the game feels
/// exactly like what the tests verify, and portals work through Core's <see cref="PortalWalker"/>.
/// WASD move, Space jump, mouse look (click to capture, Esc to release), left mouse revolver, right mouse
/// shotgun, V parry, Q blue portal, E orange portal, R respawn, F free-fly.
/// </summary>
public partial class PlayerController : Node3D
{
    private const double EyeHeight = 1.62;
    private const float MouseSensitivity = 0.0025f;
    private const double FlySpeed = 14;

    private readonly HashSet<Key> _held = [];
    private readonly HashSet<Key> _pressedThisFrame = [];
    private Camera3D _camera = null!;
    private OmniLight3D _torch = null!;
    private VoxelWorld _world = null!;
    private double _yaw, _pitch;

    public PlayerBody Body { get; private set; } = null!;
    public bool Flying { get; private set; }
    public Camera3D Camera => _camera;

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
        _camera = new Camera3D { Current = true, Fov = 80, Far = 400 };
        AddChild(_camera);
        _torch = new OmniLight3D { OmniRange = 16, LightEnergy = 1.3f, LightColor = new Color(1f, 0.85f, 0.65f) };
        _camera.AddChild(_torch);
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey key)
        {
            if (key.Pressed && !key.Echo)
            {
                _held.Add(key.Keycode);
                _pressedThisFrame.Add(key.Keycode);
                if (key.Keycode == Key.Escape) Input.MouseMode = Input.MouseModeEnum.Visible;
                if (key.Keycode == Key.F) Flying = !Flying;
            }
            else if (!key.Pressed)
            {
                _held.Remove(key.Keycode);
            }
        }
        else if (e is InputEventMouseButton { Pressed: true } && Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else if (e is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw -= motion.Relative.X * MouseSensitivity;
            _pitch = Math.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -1.5, 1.5);
        }
    }

    private bool Pressed(Key k) => _pressedThisFrame.Contains(k);

    private static bool MouseDown(MouseButton b) => Input.MouseMode == Input.MouseModeEnum.Captured && Input.IsMouseButtonPressed(b);

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

    /// <summary>True the frame R was pressed.</summary>
    public bool RespawnRequested => Pressed(Key.R);

    /// <summary>
    /// Advances the player one fixed step and returns this step's button presses. Portals, if any, are walked through.
    /// Called by the director so movement always happens before combat.
    /// </summary>
    public PlayerActions Step(double delta, PortalSystem? portals, bool dead)
    {
        var actions = new PlayerActions(
            FireRevolver: !dead && MouseDown(MouseButton.Left),
            FireShotgun: !dead && MouseDown(MouseButton.Right),
            Parry: !dead && Pressed(Key.V),
            PortalBlue: !dead && Pressed(Key.Q),
            PortalOrange: !dead && Pressed(Key.E));

        if (!dead) Move(delta, portals);
        UpdateCamera();
        _pressedThisFrame.Clear();
        return actions;
    }

    private void Move(double delta, PortalSystem? portals)
    {
        double forward = (_held.Contains(Key.W) ? 1 : 0) - (_held.Contains(Key.S) ? 1 : 0);
        double strafe = (_held.Contains(Key.D) ? 1 : 0) - (_held.Contains(Key.A) ? 1 : 0);
        double sin = Math.Sin(_yaw), cos = Math.Cos(_yaw);
        // Yaw 0 looks down -Z.
        double mx = strafe * cos - forward * sin;
        double mz = -strafe * sin - forward * cos;

        if (Flying)
        {
            double len = Math.Sqrt(mx * mx + mz * mz);
            if (len > 1) { mx /= len; mz /= len; }
            double vertical = (_held.Contains(Key.Space) ? 1 : 0) - (_held.Contains(Key.Shift) ? 1 : 0);
            Body.X += mx * FlySpeed * delta;
            Body.Z += mz * FlySpeed * delta;
            Body.Y = Math.Clamp(Body.Y + vertical * FlySpeed * delta, 1, Chunk.Height - 3);
            return;
        }

        bool jump = _held.Contains(Key.Space);
        if (portals != null && PortalWalker.Step(Body, portals, delta, mx, mz, jump, _world.GetBlock, Forward) is { } crossing)
        {
            // Turn to face where the portal sends us, keeping the view upright.
            var f = crossing.Forward.Normalized();
            _yaw = Math.Atan2(-f.X, -f.Z);
            _pitch = Math.Clamp(Math.Asin(Math.Clamp(f.Y, -1, 1)), -1.5, 1.5);
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
