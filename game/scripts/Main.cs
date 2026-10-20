using Godot;
using VoxelGame.Core.Dungeon;
using VoxelGame.Core.Terrain;
using VoxelGame.Core.World;

namespace VoxelGame.Game;

/// <summary>
/// Entry scene: builds the world, light, sky, player and a small HUD in code. Game logic lives in
/// VoxelGame.Core; the scripts here only connect the engine to it. Set the seed with VOXELGAME_SEED.
/// </summary>
public partial class Main : Node3D
{
    private WorldView _view = null!;
    private PlayerController _player = null!;
    private Label _hud = null!;

    public VoxelWorld World { get; private set; } = null!;

    public override void _Ready()
    {
        string seedText = System.Environment.GetEnvironmentVariable("VOXELGAME_SEED") is { Length: > 0 and <= 100 } s ? s : "voxel";
        World = new VoxelWorld(TerrainGenerator.SeedFromString(seedText));

        AddChild(new WorldEnvironment { Environment = MakeEnvironment() });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, 35, 0),
            LightEnergy = 1.1f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 90,
        });

        var (feet, yaw, where) = FindSpawn(World);
        _view = new WorldView { Name = "World" };
        _view.Initialize(World);
        _view.Focus = feet;
        AddChild(_view);

        _player = new PlayerController { Name = "Player" };
        _player.Initialize(World, feet, yaw);
        AddChild(_player);

        var layer = new CanvasLayer();
        _hud = new Label { Position = new Vector2(12, 8) };
        _hud.AddThemeColorOverride("font_color", Colors.White);
        _hud.AddThemeColorOverride("font_shadow_color", Colors.Black);
        layer.AddChild(_hud);
        AddChild(layer);

        GD.Print($"VoxelGame: seed \"{seedText}\", spawn {where} at ({feet.X:F1}, {feet.Y:F1}, {feet.Z:F1})");
    }

    private static Godot.Environment MakeEnvironment()
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.30f, 0.52f, 0.85f),
            SkyHorizonColor = new Color(0.72f, 0.82f, 0.92f),
            GroundHorizonColor = new Color(0.72f, 0.82f, 0.92f),
            GroundBottomColor = new Color(0.25f, 0.28f, 0.32f),
        };
        return new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.6f, 0.7f),
            AmbientLightEnergy = 0.35f,
            FogEnabled = true,
            FogLightColor = new Color(0.72f, 0.82f, 0.92f),
            FogDensity = 0.004f,
        };
    }

    /// <summary>Spawns beside the nearest dungeon entrance, facing it; falls back to the surface at the origin.</summary>
    private static (Vector3 Feet, double Yaw, string Where) FindSpawn(VoxelWorld world)
    {
        DungeonSite? best = null;
        double bestDist = double.MaxValue;
        for (int rx = -3; rx <= 2; rx++)
            for (int rz = -3; rz <= 2; rz++)
                if (world.Generator.Dungeons!.GetSite(rx, rz) is { } site)
                {
                    double d = (double)site.EntranceX * site.EntranceX + (double)site.EntranceZ * site.EntranceZ;
                    if (d < bestDist) { best = site; bestDist = d; }
                }

        if (best != null && System.Environment.GetEnvironmentVariable("VOXELGAME_SPAWN_FLOOR") is { } f && int.TryParse(f, out int floor) && floor >= 0 && floor < best.FloorCount)
        {
            // Debug aid: start inside a dungeon floor, a few cells from its entrance stairs, facing the room centre.
            var layout = best.Floors[floor];
            for (int r = 3; r <= 8; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        int x = layout.EntranceX + dx, z = layout.EntranceZ + dz;
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r || layout.CellAt(x, z) != CellKind.Floor || layout.RoomIdAt(x, z) != layout.EntranceRoom.Id) continue;
                        var (wx, wy, wz) = best.ToWorld(floor, x, z);
                        double yaw = Math.Atan2(-(layout.EntranceRoom.CenterX - x), -(layout.EntranceRoom.CenterZ - z));
                        return (new Vector3(wx + 0.5f, wy, wz + 0.5f), yaw, $"inside dungeon floor {floor}");
                    }
        }

        if (best != null)
        {
            // Stand on the plaza edge looking toward the stair shaft (which lies to the -X side of the spawn).
            var feet = new Vector3(best.EntranceX + DungeonSite.PlazaRadius + 0.5f, best.SurfaceY + 1, best.EntranceZ + 0.5f);
            return (feet, Math.PI / 2, $"at the dungeon entrance in region ({best.RegionX},{best.RegionZ})");
        }

        int y = world.Generator.SampleColumn(0, 0).Height + 1;
        return (new Vector3(0.5f, y, 0.5f), 0, "on the surface at the origin");
    }

    public override void _Process(double delta)
    {
        _view.Focus = new Vector3((float)_player.Body.X, (float)_player.Body.Y, (float)_player.Body.Z);
        var b = _player.Body;
        int by = (int)Math.Floor(b.Y);
        _hud.Text =
            $"{Engine.GetFramesPerSecond()} fps   chunks {_view.ShownChunks} (+{_view.PendingChunks} loading)\n" +
            $"x {b.X:F1}  y {b.Y:F1}  z {b.Z:F1}   {(_player.Flying ? "FLYING" : b.OnGround ? "walking" : "airborne")}\n" +
            $"in {World.GetBlock((int)Math.Floor(b.X), by, (int)Math.Floor(b.Z))}, standing on {World.GetBlock((int)Math.Floor(b.X), by - 1, (int)Math.Floor(b.Z))}\n" +
            "WASD move  Space jump  F fly  Click capture mouse  Esc release";
    }
}
