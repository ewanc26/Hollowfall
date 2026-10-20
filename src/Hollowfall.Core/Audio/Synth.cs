using Hollowfall.Core.Terrain;

namespace Hollowfall.Core.Audio;

public enum SoundId
{
    RevolverShot,
    ShotgunShot,
    Hit,
    Headshot,
    Kill,
    Hurt,
    Parry,
    PortalTeal,
    PortalViolet,
    PortalFail,
    PortalCross,
    Sever,
    Telegraph,
    EnemyShot,
    Explosion,
    KeyPickup,
    ChestOpen,
    ChestLocked,
    TrapStrike,
    DoorsLock,
    DoorsOpen,
    FloorComplete,
    Death,
    UiMove,
    UiSelect,
    Ambience,
    Meteor,
    StepStone,
    StepBrick,
    StepDirt,
    StepGrass,
    StepSand,
    StepGravel,
    StepSnow,
    StepWood,
    StepWater,

    // ---- story (appended; new sounds go below this line) ----
    NoteRead,
    TitleCard,
}

/// <summary>
/// Every sound in the game, made from arithmetic: sine sweeps, filtered noise and envelopes, rendered to mono samples.
/// There are no audio files to license or ship, the sounds are the same on every machine, and they are tested like any
/// other pure function. The host turns the samples into an engine stream.
/// </summary>
public static class Synth
{
    public const int SampleRate = 22050;

    /// <summary>Length of the looping ambience, in seconds. Every tone in it has a whole number of cycles in this time.</summary>
    public const double AmbienceSeconds = 4.0;

    private const double Tau = Math.PI * 2;

    public static float[] Render(SoundId id)
    {
        var samples = id switch
        {
            SoundId.RevolverShot => Shot(0.35, 60, 220, 0.9, 38, 55),
            SoundId.ShotgunShot => Shot(0.6, 40, 130, 1.0, 15, 22),
            SoundId.Hit => Mix(0.12, (t, i) => Noise(i, 1) * Decay(t, 60) * 0.5 + Sine(t, 190) * Decay(t, 45) * 0.5),
            SoundId.Headshot => Mix(0.28, (t, i) => (Sine(t, 1320) + 0.6 * Sine(t, 1980)) * Decay(t, 14) * 0.45 + Noise(i, 2) * Decay(t, 90) * 0.4),
            SoundId.Kill => Mix(0.4, (t, i) => Sweep(t, 420, 70, 0.4) * Decay(t, 7) * 0.7 + Noise(i, 3) * Decay(t, 30) * 0.3),
            SoundId.Hurt => Mix(0.32, (t, i) => (Saw(t, Lerp(160, 70, t / 0.32)) * 0.5 + Noise(i, 4) * 0.35) * Decay(t, 9)),
            SoundId.Parry => Mix(0.5, (t, i) => (Sine(t, 880) + 0.7 * Sine(t, 1320) + 0.5 * Sine(t, 1760)) * Decay(t, 9) * 0.4 + Noise(i, 5) * Decay(t, 120) * 0.5),
            SoundId.PortalTeal => Portal(0.45, 320, 960),
            SoundId.PortalViolet => Portal(0.45, 240, 700),
            SoundId.PortalFail => Mix(0.2, (t, _) => Square(t, 110) * Decay(t, 16) * 0.5 + Square(t, 118) * Decay(t, 16) * 0.3),
            SoundId.PortalCross => Whoosh(0.35),
            SoundId.Sever => Mix(0.4, (t, i) => Sweep(t, 2400, 300, 0.25) * Decay(t, 11) * 0.5 + Noise(i, 11) * Decay(t, 45) * 0.3 + Sine(t, 70) * Decay(t, 14) * 0.4),
            SoundId.Telegraph => Mix(0.3, (t, _) => (t % 0.15 < 0.09 ? 1.0 : 0.0) * (Sine(t, t < 0.15 ? 740 : 988) * 0.5)),
            SoundId.EnemyShot => Mix(0.3, (t, i) => Sweep(t, 520, 180, 0.3) * Decay(t, 9) * 0.6 + Noise(i, 6) * Decay(t, 40) * 0.3),
            SoundId.Explosion => Explosion(0.9),
            SoundId.KeyPickup => Mix(0.4, (t, _) => (Sine(t, 1047) * Decay(t, 12) + (t > 0.08 ? Sine(t - 0.08, 1568) * Decay(t - 0.08, 10) : 0)) * 0.45),
            SoundId.ChestOpen => Mix(0.6, (t, _) => Saw(t, Lerp(180, 300, Math.Min(1, t / 0.3))) * 0.25 * Math.Sin(Math.PI * Math.Min(1, t / 0.3)) * (t < 0.3 ? 1 : 0)
                + (t > 0.28 ? (Sine(t - 0.28, 784) + 0.6 * Sine(t - 0.28, 1175)) * Decay(t - 0.28, 9) * 0.35 : 0)),
            SoundId.ChestLocked => Mix(0.22, (t, i) => (t < 0.1 ? Sine(t, 140) * Decay(t, 40) : Sine(t - 0.1, 120) * Decay(t - 0.1, 40)) * 0.7 + Noise(i, 7) * Decay(t % 0.1, 120) * 0.15),
            SoundId.TrapStrike => Mix(0.35, (t, i) => Noise(i, 8) * Decay(t, 22) * 0.45 + (Sine(t, 2100) + Sine(t, 3300)) * Decay(t, 18) * 0.2 + Sweep(t, 300, 140, 0.35) * Decay(t, 14) * 0.4),
            SoundId.DoorsLock => Mix(0.45, (t, i) => Sine(t, 62) * Decay(t, 9) * 0.8 + Noise(i, 9) * Decay(t, 20) * 0.25),
            SoundId.DoorsOpen => Mix(0.5, (t, _) => (Sine(t, 523) * Decay(t, 10) + (t > 0.12 ? Sine(t - 0.12, 784) * Decay(t - 0.12, 8) : 0)) * 0.4),
            SoundId.FloorComplete => Arpeggio(1.3, [523.25, 659.25, 783.99, 1046.5], 0.16),
            SoundId.Death => Mix(1.3, (t, i) => (Sweep(t, 220, 38, 1.3) * 0.7 + Noise(i, 10) * 0.2) * Decay(t, 2.6)),
            SoundId.UiMove => Mix(0.05, (t, _) => Sine(t, 920) * Decay(t, 70) * 0.5),
            SoundId.UiSelect => Mix(0.14, (t, _) => Sine(t, Lerp(700, 1050, t / 0.14)) * Decay(t, 22) * 0.5),
            SoundId.Meteor => Mix(0.55, (t, i) => Sweep(t, 220, 38, 0.55) * Decay(t, 7) * 0.8 + Noise(i, 11) * Decay(t, 38) * 0.45 + Sine(t, 70) * Decay(t, 12) * 0.4),
            SoundId.Ambience => Ambience(),
            SoundId.StepStone => Mix(0.13, (t, i) => Noise(i, 50) * Decay(t, 80) * 0.35 + Sine(t, 150) * Decay(t, 55) * 0.6),
            SoundId.StepBrick => Mix(0.17, (t, i) => Sine(t, 108) * Decay(t, 32) * 0.55 + Sine(t, 216) * Decay(t, 42) * 0.3 + Noise(i, 51) * Decay(t, 95) * 0.3),
            SoundId.StepDirt => Step(0.15, 52, 700, 38, 0.9, 85, 45),
            SoundId.StepGrass => StepHiss(0.18, 53, 1300, 24, 0.9),
            SoundId.StepSand => StepHiss(0.22, 54, 4200, 15, 0.6),
            SoundId.StepGravel => StepCrunch(0.2, 55, 2600, 16, 70, 0.9),
            SoundId.StepSnow => StepCrunch(0.18, 56, 1800, 22, 150, 0.7),
            SoundId.StepWood => Mix(0.16, (t, i) => Sine(t, 205) * Decay(t, 48) * 0.5 + Sine(t, 410) * Decay(t, 62) * 0.25 + Noise(i, 57) * Decay(t, 120) * 0.3),
            SoundId.StepWater => Splash(0.32),
            SoundId.NoteRead => Mix(0.4, (t, i) => Noise(i, 31) * Math.Sin(Math.PI * Math.Min(1, t / 0.4)) * (0.55 + 0.45 * Math.Sin(t * 70)) * 0.35 + Sine(t, 330) * Decay(t, 32) * 0.12),
            SoundId.TitleCard => Mix(2.2, (t, _) => (Sine(t, 110) * 0.5 * Decay(t, 2.2) + Sine(t, 165) * 0.3 * Decay(t, 3) + Sine(t, 277) * 0.18 * Decay(t, 5)) * Math.Min(1, t / 0.04)),
            _ => throw new ArgumentOutOfRangeException(nameof(id)),
        };

        if (id != SoundId.Ambience) Finish(samples);
        return samples;
    }

    /// <summary>Whether a sound is meant to repeat seamlessly.</summary>
    public static bool Loops(SoundId id) => id == SoundId.Ambience;

    // ---- building blocks -----------------------------------------------------------------------

    private static float[] Mix(double seconds, Func<double, int, double> sample)
    {
        var data = new float[(int)(seconds * SampleRate)];
        for (int i = 0; i < data.Length; i++) data[i] = (float)sample((double)i / SampleRate, i);
        return data;
    }

    private static double Noise(int index, int salt) => SeedHash.ToUnit(SeedHash.Hash(salt, index, 0)) * 2 - 1;

    private static double Sine(double t, double hz) => Math.Sin(Tau * hz * t);

    private static double Saw(double t, double hz) => 2 * (t * hz - Math.Floor(t * hz + 0.5));

    private static double Square(double t, double hz) => Math.Sin(Tau * hz * t) >= 0 ? 1 : -1;

    private static double Decay(double t, double rate) => t < 0 ? 0 : Math.Exp(-rate * t);

    private static double Lerp(double a, double b, double f) => a + (b - a) * Math.Clamp(f, 0, 1);

    /// <summary>A sine whose pitch glides from <paramref name="from"/> to <paramref name="to"/> over <paramref name="length"/> seconds.</summary>
    private static double Sweep(double t, double from, double to, double length)
    {
        // Phase is the integral of the frequency, so the glide has no clicks.
        double phase = t <= length
            ? from * t + (to - from) * t * t / (2 * length)
            : from * length + (to - from) * length / 2 + to * (t - length);
        return Math.Sin(Tau * phase);
    }

    private static float[] OnePole(float[] input, double cutoff)
    {
        double a = 1 - Math.Exp(-Tau * cutoff / SampleRate), y = 0;
        var output = new float[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            y += a * (input[i] - y);
            output[i] = (float)y;
        }
        return output;
    }

    private static float[] Shot(double seconds, double thumpLow, double thumpHigh, double gain, double noiseDecay, double thumpDecay)
    {
        var crack = Mix(seconds, (t, i) => Noise(i, 20) * Decay(t, noiseDecay));
        var body = OnePole(crack, 3500);
        return Mix(seconds, (t, i) => (body[i] * 1.6 + crack[i] * Decay(t, 120) * 0.5 + Sweep(t, thumpHigh, thumpLow, 0.12) * Decay(t, thumpDecay * 0.5) * 0.8) * gain * 0.6);
    }

    private static float[] Portal(double seconds, double from, double to) =>
        Mix(seconds, (t, _) =>
        {
            double rise = Lerp(from, to, t / seconds);
            double vibrato = 1 + 0.02 * Math.Sin(Tau * 18 * t);
            return (Sine(t, rise * vibrato) * 0.55 + Sine(t, rise * 1.5) * 0.2) * Math.Sin(Math.PI * Math.Min(1, t / seconds)) * 0.8;
        });

    private static float[] Whoosh(double seconds)
    {
        var noise = Mix(seconds, (_, i) => Noise(i, 30));
        var low = OnePole(noise, 600);
        var high = OnePole(noise, 3500);
        return Mix(seconds, (t, i) =>
        {
            double f = Math.Clamp(t / seconds, 0, 1);
            double sweep = Math.Sin(Math.PI * f); // swells and fades
            return (low[i] * (1 - f) + high[i] * f) * sweep * 2.2;
        });
    }

    private static float[] Explosion(double seconds)
    {
        var noise = Mix(seconds, (t, i) => Noise(i, 40) * Decay(t, 5));
        var rumble = OnePole(noise, 900);
        return Mix(seconds, (t, i) => rumble[i] * 2.4 + Sweep(t, 90, 30, 0.5) * Decay(t, 5) * 0.9 + noise[i] * Decay(t, 40) * 0.5);
    }

    private static float[] Arpeggio(double seconds, double[] notes, double step) =>
        Mix(seconds, (t, _) =>
        {
            double sum = 0;
            for (int n = 0; n < notes.Length; n++)
            {
                double local = t - n * step;
                if (local < 0) continue;
                sum += (Sine(local, notes[n]) + 0.3 * Sine(local, notes[n] * 2)) * Decay(local, 4.5);
            }
            return sum * 0.28;
        });

    /// <summary>A soft footfall: low-passed noise with a dull thump under it.</summary>
    private static float[] Step(double seconds, int salt, double cutoff, double decay, double gain, double thumpHz, double thumpDecay)
    {
        var body = OnePole(Mix(seconds, (_, i) => Noise(i, salt)), cutoff);
        return Mix(seconds, (t, i) => (body[i] * 2.6 * Decay(t, decay) + Sine(t, thumpHz) * Decay(t, thumpDecay) * 0.5) * gain);
    }

    /// <summary>Brushing through grass or sand: noise with the lows taken out and a soft attack, so it hisses instead of thumping.</summary>
    private static float[] StepHiss(double seconds, int salt, double highpass, double decay, double gain)
    {
        var raw = Mix(seconds, (_, i) => Noise(i, salt));
        var low = OnePole(raw, highpass);
        return Mix(seconds, (t, i) => (raw[i] - low[i]) * (1 - Math.Exp(-t * 220)) * Decay(t, decay) * gain * 0.9);
    }

    /// <summary>Gravel and snow: noise chopped into grains, each grain a little different, with a low crush under it.</summary>
    private static float[] StepCrunch(double seconds, int salt, double lowpass, double decay, int grain, double gain)
    {
        var raw = OnePole(Mix(seconds, (_, i) => Noise(i, salt)), lowpass);
        return Mix(seconds, (t, i) =>
        {
            double g = 0.35 + 0.65 * SeedHash.ToUnit(SeedHash.Hash(salt, i / grain, 0, 3));
            return (raw[i] * 3.0 * g * Decay(t, decay) + Sine(t, 95) * Decay(t, 50) * 0.25) * gain;
        });
    }

    /// <summary>A wading splash: a rush of mid noise and a couple of rising bubbles.</summary>
    private static float[] Splash(double seconds)
    {
        var body = OnePole(Mix(seconds, (_, i) => Noise(i, 58)), 1800);
        return Mix(seconds, (t, i) =>
            body[i] * 2.4 * (1 - Math.Exp(-t * 120)) * Decay(t, 11)
            + Sine(t, Lerp(300, 720, t / 0.12)) * Decay(t, 30) * 0.22
            + (t > 0.09 ? Sine(t - 0.09, Lerp(420, 900, (t - 0.09) / 0.12)) * Decay(t - 0.09, 28) * 0.16 : 0));
    }

    /// <summary>A low drone: three slow tones that beat against each other. Every frequency fits whole cycles into the loop, so it joins up without a click.</summary>
    private static float[] Ambience() =>
        Mix(AmbienceSeconds, (t, _) =>
        {
            double swell = 0.75 + 0.25 * Math.Sin(Tau * t / AmbienceSeconds);
            double drone = Sine(t, 55) * 0.5 + Sine(t, 55.25) * 0.4 + Sine(t, 82.5) * 0.25 + Sine(t, 110.25) * 0.12;
            return drone * swell * 0.5;
        });

    /// <summary>Normalises loud sounds to a safe peak and fades the first and last milliseconds so nothing clicks.</summary>
    private static void Finish(float[] samples)
    {
        float peak = 0;
        foreach (var s in samples) peak = Math.Max(peak, Math.Abs(s));
        float scale = peak > 0.9f ? 0.9f / peak : 1f;
        int fade = Math.Min(samples.Length / 2, (int)(0.004 * SampleRate));
        for (int i = 0; i < samples.Length; i++)
        {
            float f = i < fade ? (float)i / fade : i >= samples.Length - fade ? (float)(samples.Length - 1 - i) / fade : 1f;
            samples[i] *= scale * f;
        }
    }
}
