using Hollowfall.Core.Audio.Music;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.Textures;

namespace Hollowfall.Core.Audio;

/// <summary>
/// The looping bed of each place: wind or room tone under a sustained chord, with what belongs there scattered through it
/// (birdsong, surf, drips). Every sustained part fits whole cycles into <see cref="Seconds"/>, and every scattered sound wraps
/// round the end of the loop, so each bed joins without a click.
/// </summary>
public static class Beds
{
    public const double Seconds = 4.0;

    private const double Level = 0.3;

    private const double Tau = Math.PI * 2;

    private enum Ping { None, Chirp, Gull, Drip, Tick }

    private readonly record struct Scene(double Air, double AirHz, double Gust, double Drone, double Root,
        double Wash, double WashHz, int Surfs, int Pings, Ping Kind);

    private static readonly Dictionary<SoundId, Scene> Scenes = new()
    {
        [SoundId.BedOcean] = new(0.18, 900, 0.3, 0.3, 38, 0.5, 700, 1, 0, Ping.None),
        [SoundId.BedBeach] = new(0.12, 1400, 0.4, 0.05, 110, 0.45, 1100, 2, 2, Ping.Gull),
        [SoundId.BedPlains] = new(0.3, 1000, 0.5, 0.12, 98, 0, 0, 0, 3, Ping.Chirp),
        [SoundId.BedForest] = new(0.12, 600, 0.3, 0.15, 147, 0, 0, 0, 5, Ping.Chirp),
        [SoundId.BedDesert] = new(0.35, 2400, 0.6, 0.06, 65, 0, 0, 0, 3, Ping.Tick),
        [SoundId.BedMountains] = new(0.45, 500, 0.7, 0.2, 55, 0, 0, 0, 0, Ping.None),
        [SoundId.BedRiver] = new(0.06, 1500, 0.1, 0.04, 130, 0.35, 1200, 3, 3, Ping.Drip),
        [SoundId.BedDungeonPlain] = new(0.08, 300, 0.1, 0.4, 49, 0, 0, 0, 2, Ping.Drip),
        [SoundId.BedDungeonMossy] = new(0.06, 400, 0.1, 0.35, 62, 0, 0, 0, 4, Ping.Drip),
        [SoundId.BedDungeonAshen] = new(0.2, 1800, 0.4, 0.22, 73, 0, 0, 0, 3, Ping.Tick),
    };

    public static readonly SoundId[] All = Scenes.Keys.ToArray();

    public static bool IsBed(SoundId id) => Scenes.ContainsKey(id);

    /// <summary>The bed for the place the player is standing in: the dungeon theme underground, otherwise the surface biome.</summary>
    public static SoundId For(Biome biome, DungeonTheme? dungeon) => dungeon switch
    {
        DungeonTheme.Plain => SoundId.BedDungeonPlain,
        DungeonTheme.Mossy => SoundId.BedDungeonMossy,
        DungeonTheme.Ashen => SoundId.BedDungeonAshen,
        _ => biome switch
        {
            Biome.Ocean => SoundId.BedOcean,
            Biome.Beach => SoundId.BedBeach,
            Biome.Plains => SoundId.BedPlains,
            Biome.Forest => SoundId.BedForest,
            Biome.Desert => SoundId.BedDesert,
            Biome.Mountains => SoundId.BedMountains,
            Biome.River => SoundId.BedRiver,
            _ => throw new ArgumentOutOfRangeException(nameof(biome)),
        },
    };

    public static float[] Render(SoundId id)
    {
        var s = Scenes[id];
        int n = (int)(Seconds * Synth.SampleRate);
        int salt = 0xBED0 + (int)id * 101;
        var air = Filtered(n, salt, s.AirHz);
        var wash = s.Wash > 0 ? Band(n, salt + 1, s.WashHz) : null;
        var pings = Schedule(s.Pings, s.Kind, salt + 2);
        double root = Snap(s.Root), fifth = Snap(s.Root * 1.5), octave = Snap(s.Root * 2);

        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Synth.SampleRate;
            double gust = 1 - s.Gust + s.Gust * (0.5 - 0.5 * Math.Cos(Tau * t / Seconds));
            double v = air[i] * s.Air * gust;
            if (wash != null)
                v += wash[i] * s.Wash * (0.25 + 0.75 * (0.5 + 0.5 * Math.Sin(Tau * s.Surfs * t / Seconds)));
            v += (Math.Sin(Tau * root * t) * 0.5 + Math.Sin(Tau * fifth * t) * 0.25 + Math.Sin(Tau * octave * t) * 0.12) * s.Drone;
            foreach (var (start, hz) in pings) v += PingAt(t, s.Kind, start, hz);
            data[i] = (float)(v * Level);
        }
        return data;
    }

    /// <summary>A tone's frequency, moved to the nearest one that fits a whole number of cycles in the loop.</summary>
    private static double Snap(double hz) => Math.Max(1, Math.Round(hz * Seconds)) / Seconds;

    /// <summary>Unit-power noise with its top taken off, so it has no step at the loop's join.</summary>
    private static double[] Filtered(int n, int salt, double cutoff)
    {
        var x = new float[n];
        for (int i = 0; i < n; i++) x[i] = (float)Instruments.Noise(i, salt);
        Instruments.LowPassLoop(x, cutoff);
        return Unit(x);
    }

    /// <summary>A band of noise between a low cutoff and <paramref name="hz"/>: the difference of two low-passes.</summary>
    private static double[] Band(int n, int salt, double hz)
    {
        var high = Filtered(n, salt, hz);
        var low = Filtered(n, salt, hz / 3);
        for (int i = 0; i < n; i++) high[i] -= low[i];
        return high;
    }

    private static double[] Unit(float[] x)
    {
        double power = 0;
        foreach (var v in x) power += v * v;
        double scale = 1 / Math.Sqrt(power / x.Length + 1e-12);
        var y = new double[x.Length];
        for (int i = 0; i < x.Length; i++) y[i] = x[i] * scale;
        return y;
    }

    private static List<(double Start, double Hz)> Schedule(int count, Ping kind, int salt)
    {
        var list = new List<(double, double)>(count);
        for (int k = 0; k < count; k++)
        {
            double start = (k + 0.1 + 0.8 * SeedHash.ToUnit(SeedHash.Hash(salt, k, 0))) / count * Seconds;
            double u = SeedHash.ToUnit(SeedHash.Hash(salt, k, 1));
            double hz = kind switch
            {
                Ping.Chirp => 2800 + 1400 * u,
                Ping.Gull => 1900 + 300 * u,
                Ping.Drip => 700 + 1000 * u,
                _ => 2600 + 1800 * u,
            };
            list.Add((start, hz));
        }
        return list;
    }

    /// <summary>One scattered sound at <paramref name="start"/>. Copies one loop either side are summed in, so a sound that runs past the end wraps.</summary>
    private static double PingAt(double t, Ping kind, double start, double hz)
    {
        double sum = 0;
        for (int k = -1; k <= 1; k++)
        {
            double u = t - start - k * Seconds;
            if (u < 0) continue;
            sum += kind switch
            {
                Ping.Chirp when u < 0.12 => Math.Sin(Tau * (hz * u + 0.25 * hz * u * u / 0.24)) * Math.Sin(Math.PI * u / 0.12) * 0.12,
                Ping.Gull when u < 0.6 => Math.Sin(Tau * (hz * u - 0.15 * hz * u * u / 0.6)) * Math.Sin(Math.PI * u / 0.6) * 0.1,
                Ping.Drip when u < 0.4 => Math.Sin(Tau * hz * u) * Math.Exp(-22 * u) * Math.Min(1, u / 0.002) * 0.25,
                Ping.Tick when u < 0.05 => Math.Sin(Tau * hz * u) * Math.Exp(-120 * u) * 0.12,
                _ => 0,
            };
        }
        return sum;
    }
}
