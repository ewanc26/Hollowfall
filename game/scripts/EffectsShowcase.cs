using Godot;
using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Meshing;
using Hollowfall.Core.Models;
using Hollowfall.Core.Physics;
using Hollowfall.Core.World;

namespace Hollowfall.Game;

/// <summary>
/// The "effects" page of the showcase (HOLLOWFALL_SHOWCASE=effects): a lake beside snow and gravel (to prove the water animation
/// stays inside its tile), three enemies that are hit and then burst on a loop, dust motes in the air, and a walker that crosses
/// every surface so the footstep choice is printed (sounds cannot be rendered, so the log is the evidence).
/// </summary>
public partial class EffectsShowcase : Node3D
{
    private const double Cycle = 6.0;

    private readonly Camera3D _camera;
    private readonly List<(EnemyView View, EnemyKind Kind, int Id)> _enemies = [];
    private EffectsView _effects = null!;
    private AudioDirector _audio = null!;
    private PlayerBody _walker = new(0.5, 1, 0.5);
    private double _time;
    private int _stage;
    private SoundId? _lastLogged;

    public EffectsShowcase(Camera3D camera) => _camera = camera;

    private static BlockType Terrain(int x, int y, int z)
    {
        if (x < 0 || x >= 16 || z < 0 || z >= 16 || y > 2) return BlockType.Air;
        if (y == 0) return BlockType.Stone;
        bool pool = x is >= 4 and <= 11 && z is >= 4 and <= 9;
        if (y == 1) return pool ? BlockType.Water : BlockType.Stone;
        if (x == 3 && z is >= 4 and <= 9) return BlockType.Snow;
        if (x == 12 && z is >= 4 and <= 9) return BlockType.Gravel;
        return BlockType.Air;
    }

    /// <summary>A long flat strip with a different surface every few blocks, for the footstep walker.</summary>
    private static BlockType Strip(int x, int y, int z)
    {
        if (y > 0) return BlockType.Air;
        BlockType[] surfaces = [BlockType.Stone, BlockType.BrickFloor, BlockType.Dirt, BlockType.Grass, BlockType.Sand, BlockType.Gravel, BlockType.Snow, BlockType.Log];
        return surfaces[Math.Clamp(x / 6, 0, surfaces.Length - 1)];
    }

    public override void _Ready()
    {
        var mesh = ChunkMesher.Build(0, 0, Terrain);
        AddChild(new MeshInstance3D { Name = "Terrain", Mesh = ChunkMeshBuilder.ToArrayMesh(mesh) });

        _effects = new EffectsView { Name = "Effects" };
        _effects.Initialize(Terrain);
        AddChild(_effects);

        AddChild(new EffectsDust { Name = "Dust", Active = true, Target = () => _camera.GlobalPosition });

        _audio = new AudioDirector { Name = "Audio" };
        AddChild(_audio);

        EnemyKind[] kinds = [EnemyKind.Husk, EnemyKind.Spitter, EnemyKind.Warden];
        for (int i = 0; i < kinds.Length; i++)
        {
            var view = new EnemyView(kinds[i]) { Position = new Vector3(5.5f + i * 3f, 2f, 11.5f), Rotation = new Vector3(0, Mathf.Pi, 0) };
            view.ForcePose(Motion.Idle);
            AddChild(view);
            _enemies.Add((view, kinds[i], i + 1));
        }

        // HOLLOWFALL_SHOT=water looks closely at the lake's edge, beside the snow, instead of at the fight.
        bool water = System.Environment.GetEnvironmentVariable("HOLLOWFALL_SHOT") == "water";
        _camera.Position = water ? new Vector3(6.5f, 3.2f, 10.5f) : new Vector3(8.5f, 3.3f, 17f);
        _camera.LookAt(water ? new Vector3(4.3f, 1.9f, 6.8f) : new Vector3(8.5f, 1.9f, 9.5f), Vector3.Up);
        _camera.Fov = water ? 40 : 50;
        if (water) foreach (var (view, _, _) in _enemies) view.Visible = false;
        if (GetParent().GetNodeOrNull<Node3D>("Floor") is { } floor) floor.Visible = false;
    }

    private static Vec3 Point(Node3D n, double up) => new(n.Position.X, n.Position.Y + up, n.Position.Z);

    public override void _Process(double delta)
    {
        _time += delta;
        double t = _time % Cycle;
        foreach (var (view, _, _) in _enemies) view.Animate(EnemyState.Chasing, delta);

        // 0..: a body hit, a head hit, then three kills. Each stage runs once per cycle.
        (double At, Action Do)[] script =
        [
            (0.2, ResetEnemies),
            (1.2, () => Hit(0, HitZone.Torso, false)),
            (1.4, () => Hit(1, HitZone.Head, false)),
            (1.6, () => Hit(2, HitZone.Torso, false)),
            (2.4, () => Kill(0)),
            (2.6, () => Kill(1)),
            (2.8, () => Kill(2)),
        ];
        if (t < 0.1) _stage = 0;
        while (_stage < script.Length && t >= script[_stage].At) script[_stage++].Do();
    }

    private void ResetEnemies()
    {
        foreach (var (view, _, _) in _enemies) view.Visible = true;
    }

    private void Hit(int i, HitZone zone, bool killed)
    {
        var (view, _, id) = _enemies[i];
        double up = zone == HitZone.Head ? EnemySpec.Of(_enemies[i].Kind).Height * 0.85 : EnemySpec.Of(_enemies[i].Kind).Height * 0.5;
        _effects.OnEvent(new EnemyHit(id, zone, 10, killed, Point(view, up)));
    }

    private void Kill(int i)
    {
        var (view, kind, id) = _enemies[i];
        Hit(i, HitZone.Torso, true);
        _effects.Gib(kind, view.Position, id);
        view.Visible = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        // The walker steps like the player and the audio director chooses the sound; print it when it changes surface or lands.
        _walker.Step(delta, 1, 0, jump: _walker.OnGround && ((int)(_walker.X * 2) % 29 == 0), Strip);
        _audio.Walk(delta, _walker, notWalking: false, Strip);
        if (_audio.LastStep != _lastLogged)
        {
            _lastLogged = _audio.LastStep;
            GD.Print($"[footsteps] x={_walker.X:F1} {_audio.LastStep}");
        }
        if (_walker.X > 47) _walker = new PlayerBody(0.5, 1, 0.5);
    }
}
