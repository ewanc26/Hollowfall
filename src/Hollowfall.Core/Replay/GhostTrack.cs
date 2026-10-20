using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Replay;

/// <summary>
/// Where a recorded run stood and which way it looked on every step of one floor, ready to play back as a ghost.
/// It reads the recorded feet directly, so it needs no simulation.
/// </summary>
public sealed class GhostTrack
{
    private readonly ReplayFrame[] _frames;

    public GhostTrack(RunReplay replay)
    {
        Floor = replay.Floor;
        int start = replay.StartTick, end = Math.Min(replay.EndTick, replay.Frames.Count - 1);
        _frames = new ReplayFrame[Math.Max(0, end - start + 1)];
        for (int i = 0; i < _frames.Length; i++) _frames[i] = replay.Frames[start + i];
        Seconds = replay.Result.Seconds;
    }

    public int Floor { get; }

    /// <summary>How long the recorded run took on this floor.</summary>
    public double Seconds { get; }

    public int Count => _frames.Length;

    /// <summary>The step to show when the player has spent <paramref name="elapsedSeconds"/> on the floor; null once the ghost has finished.</summary>
    public int? IndexAt(double elapsedSeconds)
    {
        if (_frames.Length == 0 || !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0) return null;
        int i = (int)Math.Round(elapsedSeconds / RunReplay.Dt);
        return i < _frames.Length ? i : null;
    }

    public Vec3 FeetAt(int index) => _frames[Math.Clamp(index, 0, _frames.Length - 1)].Feet;

    /// <summary>The heading at a step, as a rotation about Y that is zero when facing -Z (the convention of the enemy views).</summary>
    public double YawAt(int index)
    {
        var f = _frames[Math.Clamp(index, 0, _frames.Length - 1)];
        return f.Dx == 0 && f.Dz == 0 ? 0 : Math.Atan2(-f.Dx, -f.Dz);
    }

    public bool AirborneAt(int index) => (_frames[Math.Clamp(index, 0, _frames.Length - 1)].Flags & ReplayFrame.AirborneBit) != 0;
}
