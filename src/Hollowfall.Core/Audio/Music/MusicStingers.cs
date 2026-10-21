using Hollowfall.Core.Combat;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Core.Audio.Music;

/// <summary>Chooses the stinger for what just happened, so the rule is one tested place and the host only plays.</summary>
public static class MusicEvents
{
    /// <summary>
    /// The stinger for a combat event, if it has one. <paramref name="bossFloor"/> and <paramref name="bossRoom"/> say where the boss is
    /// (-1 when unknown): the boss floor's completion is the ending, which has its own stinger, so it gets none here.
    /// </summary>
    public static Stinger? For(CombatEvent e, int bossFloor = -1, int bossRoom = -1) => e switch
    {
        PlayerDied => Stinger.Death,
        FloorCompleted f when f.Floor == bossFloor => null,
        FloorCompleted => Stinger.FloorComplete,
        EncounterStarted s when bossFloor >= 0 && s.Floor == bossFloor && s.RoomId == bossRoom => Stinger.BossIntro,
        _ => null,
    };

    /// <summary>The stinger that goes with a sound the story asked for: the Wulver's greeting is also a motif in the music.</summary>
    public static Stinger? For(SoundId sound) => sound == SoundId.WulverGreeting ? Stinger.WulverGreeting : null;

    /// <summary>How loud a stinger is against the music at full volume, 0 to 1.</summary>
    public static double Gain(Stinger s) => s switch
    {
        Stinger.BossIntro or Stinger.Death => 0.9,
        Stinger.Ending => 0.8,
        _ => 0.7,
    };
}

/// <summary>
/// The stingers: short one-shots written for the world's key and rendered in code from the same instruments as the layers. Each is a
/// pure function of the stinger and the seed, mono at <see cref="MusicTuning.SampleRate"/>, and ends in silence.
/// </summary>
public static class MusicStingers
{
    public const double Peak = 0.6;

    public static float[] Render(Stinger stinger, long seed)
    {
        int tonic = MusicScores.TonicPitchClass(seed);
        var mix = new Mix(stinger switch { Stinger.FloorComplete => 4.2, Stinger.BossIntro => 5.5, Stinger.Death => 5.0, Stinger.Ending => 10.0, _ => 3.6 });
        switch (stinger)
        {
            case Stinger.FloorComplete: FloorComplete(mix, tonic); break;
            case Stinger.BossIntro: BossIntro(mix, tonic); break;
            case Stinger.Death: Death(mix, tonic); break;
            case Stinger.Ending: Ending(mix, tonic); break;
            case Stinger.WulverGreeting: WulverGreeting(mix, tonic, seed); break;
            default: throw new ArgumentOutOfRangeException(nameof(stinger));
        }
        mix.Finish(Peak);
        return mix.Data;
    }

    private sealed class Mix(double seconds)
    {
        public float[] Data { get; } = new float[(int)(seconds * MusicTuning.SampleRate)];

        public void Add(double atSeconds, float[] note, double gain = 1)
        {
            int at = (int)(atSeconds * MusicTuning.SampleRate);
            for (int i = 0; i < note.Length && at + i < Data.Length; i++) Data[at + i] += (float)(note[i] * gain);
        }

        /// <summary>Scales to <paramref name="peak"/> and fades the last 0.2 s so it ends in silence.</summary>
        public void Finish(double peak)
        {
            int fade = (int)(0.2 * MusicTuning.SampleRate);
            for (int i = 0; i < fade; i++) Data[Data.Length - 1 - i] *= (float)i / fade;
            float max = Data.Max(Math.Abs);
            if (max > 1e-9f)
                for (int i = 0; i < Data.Length; i++) Data[i] *= (float)(peak / max);
        }
    }

    private static void FloorComplete(Mix mix, int tonic)
    {
        // A rising run up the major pentatonic that rings out, with glass on the top.
        var key = new MusicKey(48 + tonic, MusicMode.MajorPentatonic);
        int[] degrees = [0, 1, 2, 4, 5, 7];
        for (int i = 0; i < degrees.Length; i++)
            mix.Add(i * 0.17, Instruments.Pluck(Modes.Hertz(key.Midi(degrees[i]) + 12), 0.9, 2.2, 0.55, 7000 + i));
        mix.Add(0.9, Instruments.Glass(Modes.Hertz(key.Midi(10) + 24), 0.8, 2.2), 0.6);
        mix.Add(0.0, Instruments.PadVoice(Modes.Hertz(key.Midi(0)), 0.6, 2.2, 800, 0.3, 1.5), 0.5);
    }

    private static void BossIntro(Mix mix, int tonic)
    {
        // A low brass fifth with a flattened second under it, and a frame-drum roll that speeds up into one huge stroke.
        int root = 36 + tonic;
        mix.Add(0.0, Instruments.BrassSwell(Modes.Hertz(root), 0.9, 3.4, 1.8, 1.4), 0.6);
        mix.Add(0.0, Instruments.BrassSwell(Modes.Hertz(root + 7), 0.8, 3.4, 2.0, 1.4), 0.45);
        mix.Add(1.2, Instruments.BrassSwell(Modes.Hertz(root + 13), 0.6, 2.2, 1.2, 1.4), 0.3);
        var drum = Instruments.FrameDrum(1, true, 7100);
        double t = 0.4, gap = 0.34;
        for (int i = 0; i < 9 && t < 3.3; i++)
        {
            mix.Add(t, drum, 0.3 + 0.07 * i);
            t += gap;
            gap = Math.Max(0.09, gap * 0.8);
        }
        mix.Add(3.4, drum, 1.0);
        mix.Add(3.4, Instruments.Thump(Modes.Hertz(root - 12), 1, 1.8, 3), 0.8);
    }

    private static void Death(Mix mix, int tonic)
    {
        // Three plucked notes falling down the Aeolian scale over a sinking low swell and one heartbeat.
        var key = new MusicKey(48 + tonic, MusicMode.Aeolian);
        int[] degrees = [7, 4, 2, 0];
        for (int i = 0; i < degrees.Length; i++)
            mix.Add(0.15 + i * 0.55, Instruments.Pluck(Modes.Hertz(key.Midi(degrees[i])), 0.9, 3, 0.35, 7200 + i));
        mix.Add(0.0, Instruments.BrassSwell(Modes.Hertz(key.Midi(-7)), 0.7, 3.0, 1.2, 1.6), 0.5);
        mix.Add(0.0, Instruments.Heartbeat(1), 0.9);
        mix.Add(0.9, Instruments.Heartbeat(0.7), 0.6);
    }

    private static void Ending(Mix mix, int tonic)
    {
        // A slow whistled phrase in the Dorian of the surface, answered by the harp over a warm pad: the tune comes home.
        var key = new MusicKey(48 + tonic, MusicMode.Dorian);
        (int Degree, double At, double Len)[] phrase = [(7, 0.0, 1.6), (9, 1.8, 1.0), (8, 2.9, 1.0), (6, 4.0, 1.6), (4, 5.8, 1.2), (7, 7.2, 2.2)];
        for (int i = 0; i < phrase.Length; i++)
            mix.Add(phrase[i].At, Instruments.Whistle(Modes.Hertz(key.Midi(phrase[i].Degree) + 12), 0.8, phrase[i].Len, 7300 + i));
        int[] chordRoots = [0, 3, 0, 4];
        for (int c = 0; c < chordRoots.Length; c++)
        {
            double at = c * 2.4;
            foreach (int o in new[] { 0, 2, 4 })
                mix.Add(at, Instruments.PadVoice(Modes.Hertz(key.Midi(chordRoots[c] + o)), 0.7, 2.6, 700, 0.7, 1.6), 0.4);
            mix.Add(at, Instruments.Pluck(Modes.Hertz(key.Midi(chordRoots[c])), 0.8, 3.5, 0.4, 7400 + c));
            mix.Add(at + 0.6, Instruments.Pluck(Modes.Hertz(key.Midi(chordRoots[c] + 4)), 0.6, 3, 0.4, 7500 + c));
        }
    }

    private static void WulverGreeting(Mix mix, int tonic, long seed)
    {
        // Three whistled notes of his major-pentatonic tune and a soft harp fifth underneath.
        var key = MusicScores.For(MusicContext.WulverHill, seed).Key;
        int[] degrees = [4, 6, 5];
        double[] at = [0.0, 0.9, 1.7];
        for (int i = 0; i < degrees.Length; i++)
            mix.Add(at[i], Instruments.Whistle(Modes.Hertz(key.Midi(degrees[i]) + 12), 0.75, i == 2 ? 1.5 : 0.75, 7600 + i));
        mix.Add(0.0, Instruments.Pluck(Modes.Hertz(key.Midi(0) + 12), 0.7, 3, 0.4, 7700), 0.8);
        mix.Add(0.5, Instruments.Pluck(Modes.Hertz(key.Midi(3) + 12), 0.5, 3, 0.4, 7701), 0.7);
    }
}

/// <summary>What the host knows about one enemy, as plain numbers.</summary>
public readonly record struct EnemyProbe(bool Alive, bool Boss, bool InEncounter, double Distance);

/// <summary>Builds the <see cref="MusicState"/> from plain facts the host reads each frame, so the rules for "near" and "boss" are tested in Core.</summary>
public static class MusicStateBuilder
{
    public static MusicState Build(
        int floor,
        IEnumerable<EnemyProbe> enemies,
        int styleRank,
        double health,
        bool dead,
        double wulverDistance,
        bool dialogueOpen,
        bool paused,
        bool nearHearth = false,
        bool passiveNearby = false,
        bool portalLinked = false)
    {
        int near = 0;
        bool encounter = false, boss = false;
        foreach (var e in enemies)
        {
            if (!e.Alive) continue;
            if (e.Distance <= MusicTuning.EnemyRange) near++;
            if (e.InEncounter) encounter = true;
            if (e.Boss && e.Distance <= MusicTuning.EnemyRange * 2) boss = true;
        }
        return new MusicState
        {
            Underground = floor >= 0,
            Floor = Math.Max(floor, 0),
            EncounterActive = encounter,
            EnemiesNear = near,
            StyleRank = Math.Clamp(styleRank, 0, 8),
            HealthFraction = double.IsNaN(health) ? 1 : Math.Clamp(health, 0, 1),
            BossActive = boss,
            Dead = dead,
            NearWulver = floor < 0 && wulverDistance <= MusicTuning.WulverHearRange,
            DialogueOpen = dialogueOpen,
            Paused = paused,
            PassiveEntityNearby = passiveNearby,
            PortalLinked = portalLinked,
            NearHearth = nearHearth,
        };
    }
}
