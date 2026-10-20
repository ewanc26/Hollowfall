using Hollowfall.Core.Terrain;

namespace Hollowfall.Core.Audio.Music;

/// <summary>
/// A mono loop being written into. Notes are added with wrap-around, so a note that rings out past the last sample
/// carries on from the first one: the loop has no seam and the tail of bar 8 sounds under bar 1.
/// </summary>
public sealed class LoopBuffer
{
    public LoopBuffer(int length) => Data = new float[length];

    public float[] Data { get; }

    public void Add(int start, float[] note, double gain = 1)
    {
        int n = Data.Length;
        int at = ((start % n) + n) % n;
        for (int i = 0; i < note.Length; i++)
        {
            Data[at] += (float)(note[i] * gain);
            if (++at == n) at = 0;
        }
    }

    /// <summary>Scales the buffer so its loudest sample is <paramref name="peak"/>.</summary>
    public void Normalise(double peak)
    {
        float max = 0;
        foreach (var s in Data) max = Math.Max(max, Math.Abs(s));
        if (max <= 1e-9f) return;
        float scale = (float)(peak / max);
        for (int i = 0; i < Data.Length; i++) Data[i] *= scale;
    }
}

/// <summary>The instruments: every one turns a pitch into a short array of samples. All of it is arithmetic, in the spirit of <see cref="Synth"/>.</summary>
public static class Instruments
{
    private const int Rate = MusicTuning.SampleRate;
    private const double Tau = Math.PI * 2;

    public static double Noise(int index, int salt) => SeedHash.ToUnit(SeedHash.Hash(salt, index, 0)) * 2 - 1;

    public static int BeatToSample(double beat) => (int)Math.Round(beat * MusicTuning.SamplesPerBeat);

    /// <summary>Rounds a frequency so a whole number of cycles fits in the loop. A tone that sounds all the way round then joins up with itself.</summary>
    public static double SnapToLoop(double hz) => Math.Max(1, Math.Round(hz * MusicTuning.LoopSeconds)) / MusicTuning.LoopSeconds;

    /// <summary>
    /// A plucked string (Karplus-Strong): a burst of noise circulating in a delay line with a gentle low-pass in the loop. The
    /// line is tuned with an all-pass fractional delay, because a whole-sample delay would put the high notes tens of cents out.
    /// </summary>
    public static float[] Pluck(double hz, double velocity, double seconds, double brightness, int salt)
    {
        int count = (int)(seconds * Rate);
        var output = new float[count];
        double delay = Rate / hz - 0.5; // the averaging filter adds half a sample of delay
        int whole = (int)Math.Floor(delay);
        double frac = delay - whole;
        double c = (1 - frac) / (1 + frac);
        var line = new double[whole];
        double tone = 0;
        for (int i = 0; i < whole; i++)
        {
            tone += brightness * (Noise(i, salt) - tone); // a duller excitation for a duller string
            line[i] = tone;
        }

        // The loss per trip round the loop that brings the string down 60 dB in `seconds` (the loop's own low-pass takes the highs faster).
        double rho = Math.Exp(Math.Log(0.001) / (hz * seconds));
        double prev = 0, apIn = 0, apOut = 0;
        int pos = 0;
        for (int n = 0; n < count; n++)
        {
            double d = line[pos];
            double lp = 0.5 * (d + prev) * rho;
            prev = d;
            double y = c * (lp - apOut) + apIn;
            apIn = lp;
            apOut = y;
            line[pos] = y;
            if (++pos == whole) pos = 0;
            output[n] = (float)(y * velocity);
        }
        Fade(output, 0.15);
        return output;
    }

    /// <summary>A breathy whistle: a sine with a trace of overtones, a late vibrato and a hiss of breath, with a soft attack.</summary>
    public static float[] Whistle(double hz, double velocity, double seconds, int salt)
    {
        double release = 0.22;
        int count = (int)((seconds + release) * Rate);
        var output = new float[count];
        double phase = 0, hiss = 0;
        for (int n = 0; n < count; n++)
        {
            double t = (double)n / Rate;
            double vibrato = 1 + 0.006 * Math.Min(1, Math.Max(0, t - 0.25) / 0.4) * Math.Sin(Tau * 5.2 * t);
            phase += Tau * hz * vibrato / Rate;
            double env = Math.Min(1, t / 0.08) * (t < seconds ? 1 : Math.Max(0, 1 - (t - seconds) / release));
            double tone = Math.Sin(phase) + 0.18 * Math.Sin(2 * phase) + 0.05 * Math.Sin(3 * phase);
            double white = Noise(n, salt);
            hiss += 0.35 * (white - hiss);
            double breath = (white - hiss) * 0.06 * (0.5 + 0.5 * Math.Min(1, t / 0.15));
            output[n] = (float)((tone * 0.7 + breath) * env * velocity);
        }
        return output;
    }

    /// <summary>A warm pad voice: three detuned saws through a two-stage low-pass, with a slow attack and release.</summary>
    public static float[] PadVoice(double hz, double velocity, double seconds, double cutoff, double attack, double release)
    {
        int count = (int)((seconds + release) * Rate);
        var output = new float[count];
        double[] ratios = [Math.Pow(2, -7 / 1200.0), 1, Math.Pow(2, 7 / 1200.0)];
        var phase = new double[3];
        double a = 1 - Math.Exp(-Tau * cutoff / Rate), y1 = 0, y2 = 0;
        for (int n = 0; n < count; n++)
        {
            double t = (double)n / Rate;
            double sum = 0;
            for (int v = 0; v < 3; v++)
            {
                phase[v] += hz * ratios[v] / Rate;
                if (phase[v] >= 1) phase[v] -= 1;
                sum += 2 * phase[v] - 1;
            }
            y1 += a * (sum / 3 - y1);
            y2 += a * (y1 - y2);
            double env = Math.Min(1, t / attack) * (t < seconds ? 1 : Math.Max(0, 1 - (t - seconds) / release));
            output[n] = (float)(y2 * env * velocity);
        }
        return output;
    }

    /// <summary>
    /// A frame drum (a bodhran): a short pitched thump that falls in pitch, and a rustle of skin noise on top.
    /// <paramref name="low"/> makes it a deeper, longer, heavier drum.
    /// </summary>
    public static float[] FrameDrum(double velocity, bool low, int salt)
    {
        double seconds = low ? 0.9 : 0.55;
        int count = (int)(seconds * Rate);
        var output = new float[count];
        double from = low ? 105 : 170, to = low ? 52 : 98, sweep = low ? 0.1 : 0.07, decay = low ? 7 : 13;
        double phase = 0, skin = 0;
        for (int n = 0; n < count; n++)
        {
            double t = (double)n / Rate;
            double hz = to + (from - to) * Math.Exp(-t / sweep * 3);
            phase += Tau * hz / Rate;
            double white = Noise(n, salt);
            skin += 0.25 * (white - skin);
            double rustle = (skin - 0.04 * white) * Math.Exp(-t * (low ? 30 : 45)) * (low ? 0.35 : 0.6);
            double body = Math.Sin(phase) * Math.Exp(-t * decay) + 0.35 * Math.Sin(phase * 1.58) * Math.Exp(-t * decay * 1.8);
            output[n] = (float)((body * 0.8 + rustle) * velocity);
        }
        Fade(output, 0.02);
        return output;
    }

    /// <summary>A soft low thump with a few overtones so it can be heard on small speakers, swelling in quickly and dying slowly.</summary>
    public static float[] Thump(double hz, double velocity, double seconds, double decay)
    {
        int count = (int)(seconds * Rate);
        var output = new float[count];
        for (int n = 0; n < count; n++)
        {
            double t = (double)n / Rate;
            double tone = Math.Sin(Tau * hz * t) + 0.4 * Math.Sin(Tau * hz * 2 * t) + 0.18 * Math.Sin(Tau * hz * 3 * t);
            output[n] = (float)(tone * (1 - Math.Exp(-t * 60)) * Math.Exp(-t * decay) * velocity);
        }
        Fade(output, 0.05);
        return output;
    }

    /// <summary>One beat of a heart: a low "lub" and a quieter "dub" a little later.</summary>
    public static float[] Heartbeat(double velocity)
    {
        int count = (int)(0.65 * Rate);
        var output = new float[count];
        int dub = (int)(0.24 * Rate);
        for (int n = 0; n < count; n++)
        {
            double t = (double)n / Rate;
            double lub = Math.Sin(Tau * (46 + 24 * Math.Exp(-t * 30)) * t) * (1 - Math.Exp(-t * 400)) * Math.Exp(-t * 18);
            double td = (double)(n - dub) / Rate;
            double second = n >= dub ? Math.Sin(Tau * (52 + 20 * Math.Exp(-td * 30)) * td) * (1 - Math.Exp(-td * 400)) * Math.Exp(-td * 22) * 0.65 : 0;
            output[n] = (float)((lub + second) * velocity);
        }
        Fade(output, 0.03);
        return output;
    }

    /// <summary>A bright, glassy note for the shimmer: a sine and a quiet octave that ring out.</summary>
    public static float[] Glass(double hz, double velocity, double seconds)
    {
        int count = (int)(seconds * Rate);
        var output = new float[count];
        for (int n = 0; n < count; n++)
        {
            double t = (double)n / Rate;
            double tone = Math.Sin(Tau * hz * t) + 0.3 * Math.Sin(Tau * hz * 2.003 * t) + 0.1 * Math.Sin(Tau * hz * 3.01 * t);
            output[n] = (float)(tone * (1 - Math.Exp(-t * 300)) * Math.Exp(-t * 3.2) * velocity);
        }
        Fade(output, 0.1);
        return output;
    }

    /// <summary>
    /// A low brass- and voice-like swell: detuned saws whose low-pass opens as the note grows, with a faint vibrato.
    /// </summary>
    public static float[] BrassSwell(double hz, double velocity, double seconds, double attack, double release)
    {
        int count = (int)((seconds + release) * Rate);
        var output = new float[count];
        double[] ratios = [Math.Pow(2, -9 / 1200.0), 1, Math.Pow(2, 9 / 1200.0)];
        var phase = new double[3];
        double y1 = 0, y2 = 0;
        for (int n = 0; n < count; n++)
        {
            double t = (double)n / Rate;
            double rise = Math.Min(1, t / (seconds * 0.7));
            double cutoff = 220 + 1500 * rise * rise;
            double a = 1 - Math.Exp(-Tau * cutoff / Rate);
            double vibrato = 1 + 0.003 * Math.Sin(Tau * 4.8 * t) * Math.Min(1, t / 1.5);
            double sum = 0;
            for (int v = 0; v < 3; v++)
            {
                phase[v] += hz * ratios[v] * vibrato / Rate;
                if (phase[v] >= 1) phase[v] -= 1;
                sum += 2 * phase[v] - 1;
            }
            y1 += a * (sum / 3 - y1);
            y2 += a * (y1 - y2);
            double env = Math.Min(1, t / attack) * (t < seconds ? 1 : Math.Max(0, 1 - (t - seconds) / release));
            output[n] = (float)(y2 * env * velocity);
        }
        return output;
    }

    /// <summary>
    /// A low-pass over a signal that is going to loop. It runs round the loop several times and keeps the last pass, so the
    /// filter's state at the end is the state at the start and the seam is smooth.
    /// </summary>
    public static void LowPassLoop(float[] data, double cutoff, int stages = 2)
    {
        double a = 1 - Math.Exp(-Tau * cutoff / Rate);
        var state = new double[stages];
        for (int pass = 0; pass < 4; pass++)
            for (int i = 0; i < data.Length; i++)
            {
                double x = data[i];
                for (int s = 0; s < stages; s++)
                {
                    state[s] += a * (x - state[s]);
                    x = state[s];
                }
                if (pass == 3) data[i] = (float)x;
            }
    }

    /// <summary>A short fade at the end so a note that has not quite died away does not click when it is cut.</summary>
    private static void Fade(float[] data, double seconds)
    {
        int n = Math.Min(data.Length, (int)(seconds * Rate));
        for (int i = 0; i < n; i++) data[data.Length - 1 - i] *= (float)i / n;
    }
}
