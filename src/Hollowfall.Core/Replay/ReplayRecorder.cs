using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Replay;

/// <summary>
/// Records a run one fixed step at a time. The host calls <see cref="Capture"/> with each step's view and buttons <i>before</i>
/// stepping the simulation, and feeds the simulation what it returns: the quantised values a replay will see, so a live run
/// and its replay cannot differ. Respawns happen outside the step, so the host reports them with <see cref="NoteRespawn"/>.
/// </summary>
public sealed class ReplayRecorder
{
    private readonly List<ReplayFrame> _frames = [];
    private int _pendingRespawns;

    /// <summary>True once the run outgrew <see cref="RunReplay.MaxTicks"/>; later floors are not saved.</summary>
    public bool Truncated { get; private set; }

    public int TickCount => _frames.Count;

    /// <summary>The ticks recorded so far, for a ghost of the run in progress.</summary>
    public IReadOnlyList<ReplayFrame> Frames => _frames;

    public (PlayerView View, PlayerActions Actions) Capture(PlayerView view, PlayerActions actions)
    {
        var frame = ReplayFrame.From(view, actions, _pendingRespawns);
        _pendingRespawns = 0;
        if (_frames.Count < RunReplay.MaxTicks) _frames.Add(frame);
        else Truncated = true;
        return (frame.ToView(), frame.ToActions());
    }

    /// <summary>Call when the host calls <c>CombatSim.Respawn()</c>; the next captured step carries it.</summary>
    public void NoteRespawn() => _pendingRespawns = Math.Min(_pendingRespawns + 1, ReplayFrame.MaxRespawns);

    /// <summary>
    /// The run so far as a finished replay of <paramref name="floor"/>, which must have been completed on the step just captured.
    /// Null if the run was truncated or nothing was recorded.
    /// </summary>
    public RunReplay? Finish(string seed, int regionX, int regionZ, int floor, FloorRecord result, Progression.RunStats stats = default)
    {
        if (Truncated || _frames.Count == 0 || string.IsNullOrEmpty(seed) || floor is < 0 or >= RunReplay.MaxFloors) return null;
        return new RunReplay(seed.Length > RunReplay.MaxSeedLength ? seed[..RunReplay.MaxSeedLength] : seed, regionX, regionZ, floor, _frames.Count - 1, result, [.. _frames]) { Stats = stats };
    }
}
