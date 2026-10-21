using System.Text;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Progression;

namespace Hollowfall.Core.Replay;

/// <summary>
/// A recorded run: the seed it was played on, the dungeon site, and every fixed step of input from the first tick up to the tick
/// a floor was completed. The prefix from tick 0 is kept (not only the floor) because the simulation's state depends on all that
/// came before. Replaying it through a fresh <c>CombatSim</c> built from the same seed gives the same run.
/// </summary>
public sealed class RunReplay
{
    /// <summary>The file format. Bump when the byte layout changes.</summary>
    public const int FormatVersion = 3;

    /// <summary>The format before the run's stats were recorded. Those files still load, as a character with nothing spent.</summary>
    public const int FormatVersionWithoutStats = 2;

    /// <summary>
    /// The simulation rules the run was recorded under. Bump this whenever <c>CombatSim</c> behaviour changes in a way that alters a
    /// replay's outcome; older files still load (a ghost only needs the recorded positions) but are flagged as unverifiable.
    /// </summary>
    public const int SimVersion = 4;

    public const int TicksPerSecond = 60;
    public const double Dt = 1.0 / TicksPerSecond;

    /// <summary>Twenty minutes of play. A run past this stops recording and is not saved.</summary>
    public const int MaxTicks = TicksPerSecond * 60 * 20;

    public const int MaxSeedLength = 100;
    public const int MaxFloors = RunRecords.MaxFloors;

    public RunReplay(string seed, int regionX, int regionZ, int floor, int endTick, FloorRecord result, IReadOnlyList<ReplayFrame> frames, int simVersion = SimVersion)
    {
        Seed = seed;
        RegionX = regionX;
        RegionZ = regionZ;
        Floor = floor;
        EndTick = endTick;
        Result = result;
        Frames = frames;
        SimVersionRecorded = simVersion;
    }

    /// <summary>The seed text the player chose (the same key <c>RunRecords</c> uses).</summary>
    public string Seed { get; }

    /// <summary>The dungeon site's region, so a replay is never played against a different dungeon.</summary>
    public int RegionX { get; }
    public int RegionZ { get; }

    /// <summary>The floor this run completed.</summary>
    public int Floor { get; }

    /// <summary>The index of the tick on which the floor was completed (the last stored tick).</summary>
    public int EndTick { get; }

    public FloorRecord Result { get; }
    public IReadOnlyList<ReplayFrame> Frames { get; }
    public int SimVersionRecorded { get; }

    /// <summary>The character's stats the run was played with. A replay re-simulates with these, so a run with Vigour verifies with Vigour.</summary>
    public Progression.RunStats Stats { get; init; }

    public int TickCount => Frames.Count;

    /// <summary>False if the file was made by a different version of the combat rules, so a replay would not reproduce it.</summary>
    public bool IsVerifiable => SimVersionRecorded == SimVersion;

    /// <summary>The first tick spent on <see cref="Floor"/>, found from the time the floor took (the sim steps at a fixed rate).</summary>
    public int StartTick => Math.Clamp(EndTick - (int)Math.Round(Result.Seconds / Dt), 0, EndTick);
}

/// <summary>Reads and writes the compact run format: header, then runs of identical steps stored as small deltas.</summary>
public static class ReplayCodec
{
    private static readonly byte[] Magic = [(byte)'H', (byte)'F', (byte)'R', (byte)'P'];
    private const int MaxPoints = 1_000_000;
    private const long MaxMillis = 10L * 24 * 3600 * 1000;

    /// <summary>The largest file we will read or write.</summary>
    public const int MaxFileBytes = 2 * 1024 * 1024;

    // ---- encoding ---------------------------------------------------------------------------

    public static byte[] Encode(RunReplay replay)
    {
        var o = new List<byte>(Math.Min(replay.TickCount * 8 + 64, MaxFileBytes));
        o.AddRange(Magic);
        o.Add(RunReplay.FormatVersion);
        o.Add((byte)replay.SimVersionRecorded);
        PutVar(o, (uint)replay.Stats.ClampedVigour);
        PutVar(o, (uint)replay.Stats.ClampedMight);
        var seed = Encoding.UTF8.GetBytes(replay.Seed.Length > RunReplay.MaxSeedLength ? replay.Seed[..RunReplay.MaxSeedLength] : replay.Seed);
        PutVar(o, (uint)seed.Length);
        o.AddRange(seed);
        PutVar(o, Zig(replay.RegionX));
        PutVar(o, Zig(replay.RegionZ));
        PutVar(o, (uint)replay.Floor);
        PutVar(o, (uint)replay.EndTick);
        o.Add((byte)replay.Result.Grade);
        PutVar(o, Zig(replay.Result.Points));
        PutVar(o, (uint)Math.Clamp(Math.Round(replay.Result.Seconds * 1000), 0, MaxMillis));
        PutVar(o, (uint)replay.TickCount);

        // Count runs first so a reader can bound its work, then write them.
        var runs = new List<(ReplayFrame Frame, int Count)>();
        foreach (var f in replay.Frames)
        {
            if (runs.Count > 0 && runs[^1].Frame == f && f.Respawns == 0) runs[^1] = (f, runs[^1].Count + 1);
            else runs.Add((f, 1));
        }
        PutVar(o, (uint)runs.Count);

        var prev = default(ReplayFrame);
        foreach (var (f, count) in runs)
        {
            Span<int> delta =
            [
                f.Fx - prev.Fx, f.Fy - prev.Fy, f.Fz - prev.Fz,
                f.Ex - prev.Ex, f.Ey - prev.Ey, f.Ez - prev.Ez,
                f.Dx - prev.Dx, f.Dy - prev.Dy, f.Dz - prev.Dz,
                f.Speed - prev.Speed,
            ];
            uint mask = 0;
            for (int i = 0; i < 10; i++) if (delta[i] != 0) mask |= 1u << i;
            PutVar(o, (uint)count);
            PutVar(o, (uint)f.Flags | (f.Respawns > 0 ? 1u << 10 : 0) | (mask << 11));
            if (f.Respawns > 0) PutVar(o, (uint)f.Respawns);
            for (int i = 0; i < 10; i++) if ((mask & (1u << i)) != 0) PutVar(o, Zig(delta[i]));
            prev = f;
        }

        uint sum = Fnv32(o);
        for (int i = 0; i < 4; i++) o.Add((byte)(sum >> (8 * i)));
        return [.. o];
    }

    private static uint Zig(int v) => (uint)((v << 1) ^ (v >> 31));

    private static void PutVar(List<byte> o, uint v)
    {
        while (v >= 0x80)
        {
            o.Add((byte)(v | 0x80));
            v >>= 7;
        }
        o.Add((byte)v);
    }

    /// <summary>FNV-1a over a byte sequence: catches truncation and damage, not tampering (a verified score is re-simulated instead).</summary>
    public static uint Fnv32(IEnumerable<byte> bytes)
    {
        uint h = 2166136261;
        foreach (var b in bytes)
        {
            h ^= b;
            h *= 16777619;
        }
        return h;
    }

    // ---- decoding ---------------------------------------------------------------------------

    /// <summary>Reads a run file. Never throws: anything damaged, oversized or out of range gives false.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> data, out RunReplay? replay)
    {
        replay = null;
        try
        {
            replay = Decode(data);
            return replay != null;
        }
        catch (Exception e) when (e is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            replay = null;
            return false;
        }
    }

    private ref struct Reader(ReadOnlySpan<byte> data)
    {
        private readonly ReadOnlySpan<byte> _data = data;
        public int Pos;
        public bool Failed;

        public readonly int Left => _data.Length - Pos;

        public byte Byte()
        {
            if (Pos >= _data.Length) { Failed = true; return 0; }
            return _data[Pos++];
        }

        /// <summary>A varint of at most five bytes (32 bits); longer ones fail.</summary>
        public uint Var()
        {
            uint v = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte b = Byte();
                if (Failed) return 0;
                if (shift == 28 && b > 0x0F) { Failed = true; return 0; }
                v |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return v;
            }
            Failed = true;
            return 0;
        }

        public ReadOnlySpan<byte> Take(int n)
        {
            if (n < 0 || n > Left) { Failed = true; return default; }
            var s = _data.Slice(Pos, n);
            Pos += n;
            return s;
        }
    }

    private static int Unzig(uint v) => (int)(v >> 1) ^ -(int)(v & 1);

    private static RunReplay? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length is < 16 or > MaxFileBytes) return null;

        // The checksum covers everything before the last four bytes.
        uint stored = (uint)(data[^4] | data[^3] << 8 | data[^2] << 16 | data[^1] << 24);
        uint h = 2166136261;
        for (int i = 0; i < data.Length - 4; i++)
        {
            h ^= data[i];
            h *= 16777619;
        }
        if (h != stored) return null;

        var r = new Reader(data[..^4]);
        if (!r.Take(4).SequenceEqual(Magic)) return null;
        int format = r.Byte();
        if (format is not (RunReplay.FormatVersion or RunReplay.FormatVersionWithoutStats)) return null;
        int sim = r.Byte();
        var stats = new RunStats();
        if (format == RunReplay.FormatVersion)
        {
            uint vigour = r.Var(), might = r.Var();
            if (r.Failed || vigour > RunStats.MaxPoints || might > RunStats.MaxPoints) return null;
            stats = new RunStats((int)vigour, (int)might);
        }
        uint seedLen = r.Var();
        if (r.Failed || seedLen is 0 or > RunReplay.MaxSeedLength * 4) return null;
        string seed = Encoding.UTF8.GetString(r.Take((int)seedLen));
        if (seed.Length is 0 or > RunReplay.MaxSeedLength) return null;
        int regionX = Unzig(r.Var()), regionZ = Unzig(r.Var());
        uint floor = r.Var(), end = r.Var();
        byte grade = r.Byte();
        int points = Unzig(r.Var());
        uint millis = r.Var();
        uint ticks = r.Var(), runCount = r.Var();
        if (r.Failed || floor >= RunReplay.MaxFloors || ticks is 0 or > RunReplay.MaxTicks || end >= ticks || runCount == 0 || runCount > ticks) return null;
        if (!Enum.IsDefined((OverallGrade)grade) || Math.Abs(points) > MaxPoints || millis > MaxMillis) return null;

        var frames = new ReplayFrame[ticks];
        int n = 0;
        var cur = default(ReplayFrame);
        long[] v = new long[10];
        for (uint run = 0; run < runCount; run++)
        {
            uint repeat = r.Var(), control = r.Var();
            if (r.Failed || repeat == 0 || repeat > ticks - n) return null;
            int flags = (int)(control & 0x3FF);
            int respawns = 0;
            if ((control & (1u << 10)) != 0)
            {
                respawns = (int)r.Var();
                if (respawns is < 1 or > ReplayFrame.MaxRespawns || repeat != 1) return null;
            }
            uint mask = control >> 11;
            if (mask > 0x3FF) return null;

            v[0] = cur.Fx; v[1] = cur.Fy; v[2] = cur.Fz; v[3] = cur.Ex; v[4] = cur.Ey; v[5] = cur.Ez; v[6] = cur.Dx; v[7] = cur.Dy; v[8] = cur.Dz; v[9] = cur.Speed;
            for (int i = 0; i < 10; i++)
                if ((mask & (1u << i)) != 0) v[i] += Unzig(r.Var());
            if (r.Failed) return null;
            foreach (long x in v) if (Math.Abs(x) > ReplayFrame.MaxPosition) return null;

            cur = new ReplayFrame((int)v[0], (int)v[1], (int)v[2], (int)v[3], (int)v[4], (int)v[5], (int)v[6], (int)v[7], (int)v[8], flags, respawns, (int)v[9]);
            if (!cur.InRange) return null;
            for (uint k = 0; k < repeat; k++) frames[n++] = cur;
        }
        if (n != ticks || r.Left != 0) return null;

        return new RunReplay(seed, regionX, regionZ, (int)floor, (int)end, new FloorRecord((OverallGrade)grade, points, millis / 1000.0), frames, sim) { Stats = stats };
    }
}
