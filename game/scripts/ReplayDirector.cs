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
    private MeshInstance3D? _tealMark;
    private MeshInstance3D? _violetMark;
    private double _tealMarkAt = -1, _violetMarkAt = -1;

    /// <summary>The ghost of the best earlier run on the current floor, if there is one.</summary>
    public GhostTrack? Track => _track;

    /// <summary>Whether the ghost is shown at all. Off hides the runner and its portal marks; recording is unaffected.</summary>
    public bool ShowGhost { get; set; } = true;

    /// <summary>How long a portal mark stays after the recorded run placed one, fading as it goes.</summary>
    private const double MarkSeconds = 1.2;

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

    /// <summary>Call when the offer after a completed floor is answered: the pact taken (or null, declined), like a respawn, is applied between steps when the replay is played.</summary>
    public void NotePact(int floor, PactKind? pact) => _recorder.NotePact(floor, pact);

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
            _tealMarkAt = _violetMarkAt = -1;
        }

        int? index = _track != null && floor >= 0 && !_sim.FloorCompleted(floor) ? _track.IndexAt(_sim.ElapsedOn(floor)) : null;
        if (index is not { } i || !ShowGhost)
        {
            if (_ghost != null) _ghost.Visible = false;
            HideMark(ref _tealMark);
            HideMark(ref _violetMark);
            _tealMarkAt = _violetMarkAt = -1;
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

        // Portal marks: a pulse where the recorded run pressed its portal buttons, so you can see where its portals were.
        double now = Time.GetTicksMsec() / 1000.0;
        if (_track.TealAt(i) && (i == 0 || !_track.TealAt(i - 1))) ShowMark(ref _tealMark, TealMarkColor, _track.EyeAt(i), now, ref _tealMarkAt);
        if (_track.VioletAt(i) && (i == 0 || !_track.VioletAt(i - 1))) ShowMark(ref _violetMark, VioletMarkColor, _track.EyeAt(i), now, ref _violetMarkAt);
        FadeMark(_tealMark, _tealMarkAt, now);
        FadeMark(_violetMark, _violetMarkAt, now);
    }

    private static readonly Color TealMarkColor = new(0.3f, 0.95f, 0.9f);
    private static readonly Color VioletMarkColor = new(0.75f, 0.4f, 1f);

    /// <summary>A small see-through sphere marking where a recorded portal was placed; it fades out over <see cref="MarkSeconds"/>.</summary>
    private void ShowMark(ref MeshInstance3D? mark, Color color, Vec3 eye, double now, ref double shownAt)
    {
        if (mark == null)
        {
            mark = new MeshInstance3D
            {
                Name = "PortalMark",
                Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 12, Rings = 8 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    EmissionEnabled = true,
                    Emission = color,
                    EmissionEnergyMultiplier = 0.9f,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(mark);
        }
        mark.Position = new Vector3((float)eye.X, (float)eye.Y, (float)eye.Z);
        ((StandardMaterial3D)mark.MaterialOverride).AlbedoColor = color;
        mark.Visible = true;
        shownAt = now;
    }

    private void FadeMark(MeshInstance3D? mark, double shownAt, double now)
    {
        if (mark == null || !mark.Visible) return;
        double age = now - shownAt;
        if (age >= MarkSeconds || age < 0)
        {
            mark.Visible = false;
            return;
        }
        var material = (StandardMaterial3D)mark.MaterialOverride!;
        var color = material.Emission;
        material.AlbedoColor = new Color(color.R, color.G, color.B, (float)(1 - age / MarkSeconds));
    }

    private static void HideMark(ref MeshInstance3D? mark)
    {
        if (mark != null) mark.Visible = false;
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
