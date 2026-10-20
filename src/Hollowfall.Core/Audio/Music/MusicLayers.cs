using System.Diagnostics;

namespace Hollowfall.Core.Audio.Music;

/// <summary>
/// Renders each layer of a score once, as a mono loop of exactly <see cref="MusicTuning.LoopSamples"/> samples. Every layer is
/// the same length, so they stay in step however they are mixed, and notes are written with wrap-around so a tail that rings out
/// of bar 8 carries on into bar 1.
/// </summary>
public static class MusicLayers
{
    private const double Tau = Math.PI * 2;
    private static readonly double BeatSeconds = 60.0 / MusicTuning.Bpm;

    // Frame drum patterns on a sixteenth-note grid: (slot 0..15, velocity).
    private static readonly (int Slot, double Velocity)[][] LightPatterns =
    [
        [(0, 1), (3, 0.4), (6, 0.75), (8, 0.9), (11, 0.4), (12, 0.7), (14, 0.5)],
        [(0, 1), (4, 0.5), (6, 0.8), (7, 0.35), (8, 0.9), (10, 0.5), (13, 0.45), (14, 0.75)],
        [(0, 1), (2, 0.4), (5, 0.5), (8, 0.85), (9, 0.35), (12, 0.75), (15, 0.5)],
    ];

    private static readonly (int Slot, double Velocity)[][] HeavyPatterns =
    [
        [(0, 1), (6, 0.7), (8, 0.9), (12, 0.8)],
        [(0, 1), (4, 0.7), (8, 0.9), (14, 0.8)],
        [(0, 1), (5, 0.65), (8, 0.9), (12, 0.8), (15, 0.5)],
    ];

    public static float[][] RenderAll(MusicScore score)
    {
        var layers = Enum.GetValues<MusicLayer>();
        var result = new float[layers.Length][];
        foreach (var layer in layers) result[(int)layer] = Render(score, layer);
        return result;
    }

    public static float[] Render(MusicScore score, MusicLayer layer)
    {
        var buffer = new LoopBuffer(MusicTuning.LoopSamples);
        switch (layer)
        {
            case MusicLayer.Drone: Drone(buffer, score); break;
            case MusicLayer.Harp: Harp(buffer, score); break;
            case MusicLayer.Whistle: Whistle(buffer, score); break;
            case MusicLayer.Pulse: Pulse(buffer, score); break;
            case MusicLayer.DrumLight: Drums(buffer, score, heavy: false); break;
            case MusicLayer.DrumHeavy: Drums(buffer, score, heavy: true); break;
            case MusicLayer.Shimmer: Shimmer(buffer, score); break;
            case MusicLayer.Choir: Choir(buffer, score); break;
            case MusicLayer.Heartbeat: Heartbeat(buffer); break;
            default: throw new ArgumentOutOfRangeException(nameof(layer));
        }
        buffer.Normalise(MusicTuning.Peak(layer));
        return buffer.Data;
    }

    private static void Drone(LoopBuffer buffer, MusicScore score)
    {
        // Root, fifth, octave and a sub. Every frequency fits whole cycles into the loop so the bed joins up with itself.
        (int Offset, double Amp)[] tones = [(-12, 0.45), (0, 1.0), (7, 0.55), (12, 0.4)];
        var data = buffer.Data;
        foreach (var (offset, amp) in tones)
        {
            double hz = Instruments.SnapToLoop(Modes.Hertz(score.Key.RootMidi + offset));
            double hzHigh = Instruments.SnapToLoop(hz * 1.0035), hzLow = Instruments.SnapToLoop(hz / 1.0035);
            for (int i = 0; i < data.Length; i++)
            {
                double t = (double)i / MusicTuning.SampleRate;
                double saw = Saw(hzHigh * t) + Saw(hzLow * t);
                double swell = 0.85 + 0.15 * Math.Sin(Tau * 2 * t / MusicTuning.LoopSeconds);
                data[i] += (float)((saw * 0.25 + Math.Sin(Tau * hz * t) * 0.5) * amp * swell);
            }
        }
        Instruments.LowPassLoop(data, score.PadCutoff * 0.8);

        var padNotes = score.PadNotes;
        for (int i = 0; i < padNotes.Count; i++)
        {
            var n = padNotes[i];
            var voice = Instruments.PadVoice(Modes.Hertz(n.Midi), n.Velocity, n.Length * BeatSeconds, score.PadCutoff, 1.6, 2.2);
            buffer.Add(Instruments.BeatToSample(n.Beat), voice, 0.35);
        }
    }

    private static double Saw(double phase) => 2 * (phase - Math.Floor(phase + 0.5));

    private static void Harp(LoopBuffer buffer, MusicScore score)
    {
        double brightness = Math.Clamp(0.25 + 0.5 * score.PadCutoff / 1000, 0.3, 0.8);
        for (int i = 0; i < score.HarpNotes.Count; i++)
        {
            var n = score.HarpNotes[i];
            double decay = Math.Clamp(7 - (n.Midi - 48) * 0.06, 2.5, 6);
            buffer.Add(Instruments.BeatToSample(n.Beat), Instruments.Pluck(Modes.Hertz(n.Midi), n.Velocity, decay, brightness, 1000 + i));
        }
    }

    private static void Whistle(LoopBuffer buffer, MusicScore score)
    {
        for (int i = 0; i < score.Melody.Count; i++)
        {
            var n = score.Melody[i];
            buffer.Add(Instruments.BeatToSample(n.Beat), Instruments.Whistle(Modes.Hertz(n.Midi), n.Velocity, n.Length * BeatSeconds, 2000 + i));
        }
    }

    private static void Pulse(LoopBuffer buffer, MusicScore score)
    {
        double[] accents = [1.0, 0.45, 0.8, 0.45];
        double hz = Modes.Hertz(score.Key.RootMidi);
        var hit = Instruments.Thump(hz, 1, 1.1, 4.5);
        for (int beat = 0; beat < MusicTuning.Bars * MusicTuning.BeatsPerBar; beat++)
            buffer.Add(Instruments.BeatToSample(beat), hit, accents[beat % 4]);
    }

    private static void Drums(LoopBuffer buffer, MusicScore score, bool heavy)
    {
        var pattern = (heavy ? HeavyPatterns : LightPatterns)[score.DrumPattern];
        var hit = Instruments.FrameDrum(1, heavy, heavy ? 3100 : 3000);
        var ghost = Instruments.FrameDrum(1, false, 3200);
        for (int bar = 0; bar < MusicTuning.Bars; bar++)
        {
            double weight = heavy ? score.DrumWeight : 1;
            foreach (var (slot, velocity) in pattern)
                buffer.Add(Instruments.BeatToSample(bar * 4 + slot * 0.25), hit, velocity * weight);

            // The last bar of each half rolls into the next with a few quick strokes.
            if (bar % 4 == 3)
                for (int k = 0; k < 4; k++)
                    buffer.Add(Instruments.BeatToSample(bar * 4 + 3 + k * 0.25), heavy ? hit : ghost, (0.45 + 0.12 * k) * weight);
        }
    }

    private static void Shimmer(LoopBuffer buffer, MusicScore score)
    {
        foreach (var n in score.ShimmerNotes)
        {
            var glass = Instruments.Glass(Modes.Hertz(n.Midi), n.Velocity, 2.4);
            int at = Instruments.BeatToSample(n.Beat);
            buffer.Add(at, glass);
            buffer.Add(at + Instruments.BeatToSample(0.75), glass, 0.4);
            buffer.Add(at + Instruments.BeatToSample(1.5), glass, 0.18);
        }
    }

    private static void Choir(LoopBuffer buffer, MusicScore score)
    {
        foreach (var n in score.ChoirNotes)
            buffer.Add(Instruments.BeatToSample(n.Beat), Instruments.BrassSwell(Modes.Hertz(n.Midi), n.Velocity, n.Length * BeatSeconds, 2.4, 2.2), 0.5);
    }

    private static void Heartbeat(LoopBuffer buffer)
    {
        var beat = Instruments.Heartbeat(1);
        for (int b = 0; b < MusicTuning.Bars * MusicTuning.BeatsPerBar; b++)
            buffer.Add(Instruments.BeatToSample(b), beat, b % 4 == 0 ? 1.0 : 0.85);
    }
}

/// <summary>
/// Every layer of every context for one world seed, rendered and ready to mix. Rendering is a pure function of the seed, safe to
/// run on a background thread, and takes about <see cref="RenderMilliseconds"/> on this machine.
/// </summary>
public sealed class MusicBank
{
    private MusicBank(long seed, MusicScore[] scores, float[][][] layers, double milliseconds)
    {
        Seed = seed;
        Scores = scores;
        Layers = layers;
        RenderMilliseconds = milliseconds;
    }

    public long Seed { get; }

    public IReadOnlyList<MusicScore> Scores { get; }

    /// <summary>The rendered loops: context, then layer.</summary>
    public float[][][] Layers { get; }

    public double RenderMilliseconds { get; }

    public MusicScore Score(MusicContext context) => Scores[(int)context];

    public float[] Layer(MusicContext context, MusicLayer layer) => Layers[(int)context][(int)layer];

    /// <summary>The bytes the float loops take (4 per sample).</summary>
    public long FloatBytes => Layers.Sum(c => c.Sum(l => (long)l.Length * sizeof(float)));

    /// <summary>The bytes the same loops take as 16-bit samples, which is what the engine keeps.</summary>
    public long Pcm16Bytes => Layers.Sum(c => c.Sum(l => (long)l.Length * sizeof(short)));

    /// <summary>Renders every context. With <paramref name="parallel"/> the contexts are rendered on separate threads, and the result is the same either way.</summary>
    public static MusicBank Render(long seed, bool parallel = true, CancellationToken cancel = default)
    {
        var watch = Stopwatch.StartNew();
        var contexts = Enum.GetValues<MusicContext>();
        var scores = contexts.Select(c => MusicScores.For(c, seed)).ToArray();
        var layers = new float[contexts.Length][][];
        if (parallel)
            Parallel.For(0, contexts.Length, new ParallelOptions { CancellationToken = cancel }, i => layers[i] = MusicLayers.RenderAll(scores[i]));
        else
            for (int i = 0; i < contexts.Length; i++)
            {
                cancel.ThrowIfCancellationRequested();
                layers[i] = MusicLayers.RenderAll(scores[i]);
            }
        return new MusicBank(seed, scores, layers, watch.Elapsed.TotalMilliseconds);
    }
}
