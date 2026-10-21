namespace Hollowfall.Core.Audio.Music;

/// <summary>One step of a scripted listening test: from <see cref="At"/> seconds, hold this state, and play this stinger once at that moment if given.</summary>
public readonly record struct MusicScriptStep(double At, MusicState State, Stinger? Play = null);

/// <summary>
/// Mixes the music offline, the way the engine does it live: the same <see cref="MusicMixer"/>, the same rendered loops, the same
/// stingers, summed to a mono buffer. Used for listening tests (a WAV a person can play) and for checks that the sum never clips.
/// </summary>
public static class MusicOffline
{
    public const double FrameSeconds = 1.0 / 30;

    /// <param name="droneScale">What the Ambience slider does to the drone layer (slider times <see cref="MusicTuning.DroneBoost"/>; 1 at the default).</param>
    public static float[] Render(MusicBank bank, IReadOnlyList<MusicScriptStep> script, double totalSeconds, double droneScale = 1, double musicVolume = 1)
    {
        var mixer = new MusicMixer();
        int total = (int)(totalSeconds * MusicTuning.SampleRate);
        var output = new float[total];
        var contexts = Enum.GetValues<MusicContext>();
        var layers = Enum.GetValues<MusicLayer>();
        var stingers = new List<(int Start, float[] Data, double Gain)>();
        int frame = (int)Math.Round(FrameSeconds * MusicTuning.SampleRate);
        var gains = new double[contexts.Length * layers.Length];
        var previous = new double[gains.Length];
        int step = 0;
        var state = script[0].State;

        for (int pos = 0; pos < total; pos += frame)
        {
            double now = (double)pos / MusicTuning.SampleRate;
            while (step < script.Count && script[step].At <= now)
            {
                state = script[step].State;
                if (script[step].Play is { } s)
                    stingers.Add((pos, MusicStingers.Render(s, bank.Seed), MusicEvents.Gain(s)));
                step++;
            }

            mixer.Update(state, FrameSeconds);
            Array.Copy(gains, previous, gains.Length);
            foreach (var c in contexts)
                foreach (var l in layers)
                    gains[(int)c * layers.Length + (int)l] = Math.Min(1, mixer.Gain(c, l) * (l == MusicLayer.Drone ? droneScale : 1)) * musicVolume;

            int end = Math.Min(total, pos + frame);
            foreach (var c in contexts)
                foreach (var l in layers)
                {
                    int g = (int)c * layers.Length + (int)l;
                    if (gains[g] <= 1e-6 && previous[g] <= 1e-6) continue;
                    var loop = bank.Layer(c, l);
                    for (int i = pos; i < end; i++)
                    {
                        double k = (double)(i - pos + 1) / frame; // glide across the frame, as the engine's own volume ramp would
                        output[i] += (float)(loop[i % loop.Length] * (previous[g] + (gains[g] - previous[g]) * k));
                    }
                }
        }

        foreach (var (start, data, gain) in stingers)
            for (int i = 0; i < data.Length && start + i < total; i++) output[start + i] += (float)(data[i] * gain * musicVolume);
        return output;
    }

    /// <summary>Little-endian 16-bit samples, the form the engine keeps. Values outside -1 to 1 are clamped.</summary>
    public static byte[] ToPcm16(float[] mono)
    {
        var bytes = new byte[mono.Length * 2];
        for (int i = 0; i < mono.Length; i++)
        {
            short v = (short)Math.Clamp((int)Math.Round(mono[i] * short.MaxValue), short.MinValue, short.MaxValue);
            bytes[i * 2] = (byte)(v & 0xFF);
            bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return bytes;
    }

    /// <summary>The bytes of a mono 16-bit PCM WAV file.</summary>
    public static byte[] ToWav(float[] mono, int sampleRate = MusicTuning.SampleRate)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int dataBytes = mono.Length * 2;
        w.Write("RIFF"u8);
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(sampleRate);
        w.Write(sampleRate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(dataBytes);
        foreach (var f in mono) w.Write((short)Math.Clamp((int)Math.Round(f * short.MaxValue), short.MinValue, short.MaxValue));
        return ms.ToArray();
    }

    /// <summary>The listening script: calm exploration, a fight that builds, the boss, and a death.</summary>
    public static (List<MusicScriptStep> Steps, double Seconds) ListeningScript()
    {
        var calm = new MusicState { Underground = true, Floor = 0 };
        var steps = new List<MusicScriptStep>
        {
            new(0, new MusicState()),
            new(12, calm),
            new(30, calm with { EnemiesNear = 2, StyleRank = 2, EncounterActive = true }),
            new(40, calm with { EnemiesNear = 5, StyleRank = 5, EncounterActive = true }),
            new(50, calm with { EnemiesNear = 3, StyleRank = 5, EncounterActive = true, HealthFraction = 0.3 }),
            new(58, calm with { HealthFraction = 0.3 }),
            new(70, new MusicState { Underground = true, Floor = 2, EnemiesNear = 1, BossActive = true, EncounterActive = true, StyleRank = 4 }, Stinger.BossIntro),
            new(90, new MusicState { Underground = true, Floor = 2, EnemiesNear = 1, BossActive = true, EncounterActive = true, HealthFraction = 0.08, Dead = true }, Stinger.Death),
        };
        return (steps, 100);
    }
}
