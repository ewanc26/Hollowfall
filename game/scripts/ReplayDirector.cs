using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Replay;

namespace Hollowfall.Game;

/// <summary>
/// Connects the run recorder and the ghost to the game. Each physics step the host passes its input through <see cref="Capture"/>
/// before stepping the simulation; when a floor is completed the run is saved if it is the best for that seed and floor, and on
/// every floor the best earlier run for the same seed is shown as a translucent Husk-shaped runner. All rules are in
/// <c>Hollowfall.Core.Replay</c>; this only moves a node.
/// </summary>
public partial class ReplayDirector : Node3D
{
    private static readonly Color GhostTint = new(0.55f, 0.9f, 1f, 0.45f);

    private readonly ReplayRecorder _recorder = new();
    private CombatSim _sim = null!;
    private DungeonSite _site = null!;
    private ReplayStore _store = null!;
    private string _seed = "";

    /// <summary>The character's stats this run was played with, so its replay re-simulates the same way.</summary>
    private Hollowfall.Core.Progression.RunStats _stats;

    private bool _exactSteps = true;
    private int _trackFloor = -2;
    private GhostTrack? _track;
    private EnemyView? _ghost;
    private bool _ghostDressed;

    /// <summary>The ghost of the best earlier run on the current floor, if there is one.</summary>
    public GhostTrack? Track => _track;

    public bool GhostVisible => _ghost?.Visible ?? false;

    public void Initialize(CombatSim sim, DungeonSite site, string seed, string recordsPath)
    {
        _sim = sim;
        _site = site;
        _seed = seed;
        _stats = sim.Stats;
        _store = ReplayStore.Beside(recordsPath);
    }

    /// <summary>
    /// Records this step's input and returns the values to give the simulation (the recorded, rounded ones, so live play and replay
    /// cannot differ). A step that is not exactly 1/60 s cannot be replayed, so the run is not saved.
    /// </summary>
    public (PlayerView View, PlayerActions Actions) Capture(double delta, PlayerView view, PlayerActions actions)
    {
        if (delta != RunReplay.Dt && _exactSteps)
        {
            _exactSteps = false;
            GD.Print($"Hollowfall: physics step {delta} is not 1/60 s, so this run will not be saved as a replay");
        }
        return _recorder.Capture(view, actions);
    }

    /// <summary>Call whenever the host respawns the simulation.</summary>
    public void NoteRespawn() => _recorder.NoteRespawn();

    /// <summary>Saves the run that just completed a floor if it beats the stored one. Call from the floor-completed event.</summary>
    public void OnFloorCompleted(FloorCompleted e)
    {
        if (!_exactSteps) return;
        var replay = _recorder.Finish(_seed, _site.RegionX, _site.RegionZ, e.Floor, new FloorRecord(e.Result.Overall, e.Result.Points, e.Seconds), _stats);
        if (replay != null && _store.TrySaveIfBest(replay)) GD.Print($"Hollowfall: saved the best run of floor {e.Floor + 1} ({replay.TickCount} steps)");
    }

    public override void _Process(double delta)
    {
        int floor = _sim.CurrentFloor;
        if (floor != _trackFloor)
        {
            _trackFloor = floor;
            var stored = floor >= 0 ? _store.Load(_seed, floor) : null;
            _track = stored != null && stored.RegionX == _site.RegionX && stored.RegionZ == _site.RegionZ ? new GhostTrack(stored) : null;
        }

        int? index = _track != null && floor >= 0 && !_sim.FloorCompleted(floor) ? _track.IndexAt(_sim.ElapsedOn(floor)) : null;
        if (index is not { } i)
        {
            if (_ghost != null) _ghost.Visible = false;
            return;
        }

        if (_ghost == null)
        {
            _ghost = new EnemyView(EnemyKind.Husk) { Name = "Ghost" };
            AddChild(_ghost);
        }
        if (!_ghostDressed) Dress(_ghost);

        var feet = _track!.FeetAt(i);
        _ghost.Visible = true;
        _ghost.Position = new Vector3((float)feet.X, (float)feet.Y, (float)feet.Z);
        _ghost.Rotation = new Vector3(0, (float)_track.YawAt(i), 0);
        _ghost.Animate(EnemyState.Chasing, delta);
    }

    /// <summary>Gives the ghost its own see-through material. The shared enemy materials are left alone.</summary>
    private void Dress(EnemyView ghost)
    {
        if (ghost.Rig == null) return;
        var material = new StandardMaterial3D
        {
            AlbedoColor = GhostTint,
            EmissionEnabled = true,
            Emission = new Color(0.25f, 0.5f, 0.6f),
            EmissionEnergyMultiplier = 0.6f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.9f,
            CullMode = BaseMaterial3D.CullModeEnum.Back,
        };
        foreach (var node in ghost.Rig.Root.FindChildren("*", "MeshInstance3D", true, false))
            if (node is MeshInstance3D mesh)
            {
                mesh.MaterialOverride = material;
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        _ghostDressed = true;
    }
}
