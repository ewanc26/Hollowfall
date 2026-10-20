using Godot;
using Hollowfall.Core.Physics;
using Hollowfall.Core.World;

namespace Hollowfall.Game;

/// <summary>
/// First-person walker. Movement and collision come from Core's <see cref="PlayerBody"/>, so the game feels
/// exactly like what the tests verify. WASD to move, Space to jump, mouse to look (click to capture,
/// Esc to release), F to toggle free-fly for inspecting the world.
/// </summary>
public partial class PlayerController : Node3D
{
    private const double EyeHeight = 1.62;
    private const float MouseSensitivity = 0.0025f;
    private const double FlySpeed = 14;

    private Camera3D _camera = null!;
    private OmniLight3D _torch = null!;
    private VoxelWorld _world = null!;
    private double _yaw, _pitch;

    public PlayerBody Body { get; private set; } = null!;
    public bool Flying { get; private set; }

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
        if (e is InputEventMouseButton { Pressed: true } && Input.MouseMode != Input.MouseModeEnum.Captured)
            Input.MouseMode = Input.MouseModeEnum.Captured;
        else if (e is InputEventKey { Pressed: true, Keycode: Key.Escape })
            Input.MouseMode = Input.MouseModeEnum.Visible;
        else if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F })
            Flying = !Flying;
        else if (e is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw -= motion.Relative.X * MouseSensitivity;
            _pitch = Math.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -1.5, 1.5);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Body == null) return;

        double forward = (Input.IsKeyPressed(Key.W) ? 1 : 0) - (Input.IsKeyPressed(Key.S) ? 1 : 0);
        double strafe = (Input.IsKeyPressed(Key.D) ? 1 : 0) - (Input.IsKeyPressed(Key.A) ? 1 : 0);
        double sin = Math.Sin(_yaw), cos = Math.Cos(_yaw);
        // Yaw 0 looks down -Z.
        double mx = strafe * cos - forward * sin;
        double mz = -strafe * sin - forward * cos;

        if (Flying)
        {
            double len = Math.Sqrt(mx * mx + mz * mz);
            if (len > 1) { mx /= len; mz /= len; }
            double vertical = (Input.IsKeyPressed(Key.Space) ? 1 : 0) - (Input.IsKeyPressed(Key.Shift) ? 1 : 0);
            Body.X += mx * FlySpeed * delta;
            Body.Z += mz * FlySpeed * delta;
            Body.Y = Math.Clamp(Body.Y + vertical * FlySpeed * delta, 1, Chunk.Height - 3);
        }
        else
        {
            Body.Step(delta, mx, mz, Input.IsKeyPressed(Key.Space), _world.GetBlock);
        }

        Position = new Vector3((float)Body.X, (float)(Body.Y + EyeHeight), (float)Body.Z);
        Rotation = new Vector3(0, (float)_yaw, 0);
        _camera.Rotation = new Vector3((float)_pitch, 0, 0);
    }
}
