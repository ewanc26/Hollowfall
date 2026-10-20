using Hollowfall.Core.Terrain;

namespace Hollowfall.Core.Audio.Music;

/// <summary>One note of the score: when it starts and how long it lasts, in beats from the start of the loop, its MIDI pitch and how hard it is played (0 to 1).</summary>
public readonly record struct ScoreNote(double Beat, double Length, int Midi, double Velocity);

/// <summary>
/// The written music for one context: a key, a chord for every bar and the notes of each melodic layer as plain data
/// (so tests can check them against the scale) plus a few choices that colour the drums and the pad. It is a pure
/// function of the context and the world seed (<see cref="MusicScores.For"/>).
/// </summary>
public sealed record MusicScore
{
    public required MusicContext Context { get; init; }
    public required MusicKey Key { get; init; }

    /// <summary>The scale degree each bar's chord is built on.</summary>
    public required IReadOnlyList<int> BarRoots { get; init; }

    /// <summary>The whistle's tune.</summary>
    public required IReadOnlyList<ScoreNote> Melody { get; init; }

    /// <summary>The harp's arpeggios.</summary>
    public required IReadOnlyList<ScoreNote> HarpNotes { get; init; }

    /// <summary>The high shimmering arpeggio.</summary>
    public required IReadOnlyList<ScoreNote> ShimmerNotes { get; init; }

    /// <summary>The slow pad chords that sit over the drone.</summary>
    public required IReadOnlyList<ScoreNote> PadNotes { get; init; }

    /// <summary>The long open chords of the boss swell.</summary>
    public required IReadOnlyList<ScoreNote> ChoirNotes { get; init; }

    /// <summary>Which of the frame drum's patterns this score uses (0 to 2).</summary>
    public required int DrumPattern { get; init; }

    /// <summary>How hard the heavy drum hits, 0 to 1: the ashen depths are the heaviest.</summary>
    public required double DrumWeight { get; init; }

    /// <summary>The pad's low-pass cutoff in hertz: darker places are duller.</summary>
    public required double PadCutoff { get; init; }

    public int LoopSamples => MusicTuning.LoopSamples;

    /// <summary>A number that changes when anything audible in the score changes, so tests can tell scores apart. Built from <see cref="SeedHash"/>, never from the runtime's randomised hashing.</summary>
    public ulong Signature()
    {
        ulong h = SeedHash.Mix((ulong)Context * 31UL + (ulong)Key.RootMidi * 131UL + (ulong)Key.Mode);
        foreach (var d in BarRoots) h = SeedHash.Mix(h ^ (ulong)(uint)d);
        foreach (var list in new[] { Melody, HarpNotes, ShimmerNotes, PadNotes, ChoirNotes })
            foreach (var n in list)
                h = SeedHash.Mix(h ^ (ulong)(uint)n.Midi ^ ((ulong)(uint)(n.Beat * 8) << 16) ^ ((ulong)(uint)(n.Length * 8) << 40));
        return SeedHash.Mix(h ^ (ulong)DrumPattern);
    }
}

/// <summary>Writes the score for a context from the world seed. The same seed always gives the same music.</summary>
public static class MusicScores
{
    // The tonic's pitch class (0 = C): the keys that sit well on a harp and a whistle.
    private static readonly int[] Tonics = [2, 4, 7, 9, 0, 5, 10, 11];

    // Chord roots for the eight bars, as scale degrees. Seven-note scales (stacked thirds) and the pentatonic (see ChordOffsets).
    private static readonly int[][] HeptatonicProgressions =
    [
        [0, 0, 3, 3, 0, 0, 6, 0],
        [0, 6, 0, 3, 0, 6, 3, 0],
        [0, 0, 2, 2, 3, 3, 6, 0],
    ];

    private static readonly int[][] PhrygianProgressions =
    [
        [0, 0, 1, 1, 0, 0, 6, 0],
        [0, 6, 0, 1, 0, 5, 1, 0],
        [0, 0, 5, 5, 6, 6, 1, 0],
    ];

    private static readonly int[][] PentatonicProgressions =
    [
        [0, 0, 4, 4, 3, 3, 0, 0],
        [0, 3, 4, 3, 0, 3, 4, 0],
        [0, 0, 2, 2, 4, 4, 3, 0],
    ];

    // Two-bar rhythms in beats (each sums to 8) and the scale degrees a phrase walks through.
    private static readonly double[][] Rhythms =
    [
        [3, 1, 2, 2],
        [2, 2, 1, 1, 2],
        [4, 2, 1, 1],
        [1.5, 0.5, 2, 4],
        [2, 1, 1, 2, 2],
    ];

    private static readonly int[][] Contours =
    [
        [4, 3, 2, 0, 0],
        [0, 2, 4, 2, 1],
        [2, 4, 3, 1, 0],
        [4, 2, 3, 1, 0],
        [0, 1, 2, 4, 2],
    ];

    // Harp patterns: (eighth-note slot in the bar 0..7, index of the chord tone to pluck).
    private static readonly (int Slot, int Tone)[][] DensePatterns =
    [
        [(0, 0), (1, 2), (2, 1), (3, 3), (4, 2), (6, 1)],
        [(0, 0), (2, 1), (3, 2), (4, 3), (6, 2)],
        [(0, 0), (1, 1), (3, 2), (5, 3), (6, 1)],
    ];

    private static readonly (int Slot, int Tone)[][] SparsePatterns =
    [
        [(0, 0), (4, 2)],
        [(0, 0), (3, 2)],
        [(0, 0), (5, 1)],
    ];

    private static readonly (int Slot, int Tone)[] GentlePattern = [(0, 0), (2, 1), (4, 2), (6, 1)];

    // Shimmer masks: sixteenth-note slots (0..15) in a bar.
    private static readonly int[][] ShimmerSlots =
    [
        [0, 3, 6, 10, 13],
        [1, 4, 8, 11],
        [0, 5, 7, 12, 14],
        [2, 6, 9, 15],
    ];

    private static int Pick(long seed, int salt, int count) => (int)(SeedHash.Hash(seed, salt, 0) % (ulong)count);

    private static double Jitter(long seed, int salt, int index) => SeedHash.ToUnit(SeedHash.Hash(seed, salt, index)) * 2 - 1;

    /// <summary>The tonic's pitch class for a world: shared by every context, so moving between them is a change of mode more than of key.</summary>
    public static int TonicPitchClass(long seed) => Tonics[Pick(SeedHash.Derive(seed, 0x6D75), 1, Tonics.Length)];

    public static MusicScore For(MusicContext context, long seed)
    {
        long s = SeedHash.Derive(seed, 0x6D75);
        int root = 36 + TonicPitchClass(seed);
        var key = context switch
        {
            MusicContext.Surface => new MusicKey(root, MusicMode.Dorian),
            MusicContext.Plain or MusicContext.Mossy => new MusicKey(root, MusicMode.Aeolian),
            MusicContext.Ashen => new MusicKey(root, MusicMode.Phrygian),
            // A minor third up: a major pentatonic there uses only notes the surface's Dorian already has.
            _ => new MusicKey(root + 3, MusicMode.MajorPentatonic),
        };
        bool pentatonic = key.Mode == MusicMode.MajorPentatonic;
        int[] roots = (pentatonic ? PentatonicProgressions : key.Mode == MusicMode.Phrygian ? PhrygianProgressions : HeptatonicProgressions)[Pick(s, 2, 3)];
        int[] chordOffsets = pentatonic ? [0, 2, 3, 5] : [0, 2, 4, 7];
        int ctx = (int)context;

        return new MusicScore
        {
            Context = context,
            Key = key,
            BarRoots = roots,
            Melody = BuildMelody(key, s, ctx),
            HarpNotes = BuildHarp(key, roots, chordOffsets, s, ctx, context),
            ShimmerNotes = BuildShimmer(key, roots, chordOffsets, s),
            PadNotes = BuildPad(key, roots, pentatonic),
            ChoirNotes = BuildChoir(key, roots, pentatonic),
            DrumPattern = Pick(s, 3, 3),
            DrumWeight = context switch { MusicContext.Ashen => 1.0, MusicContext.Mossy => 0.8, MusicContext.Plain => 0.85, _ => 0.7 },
            PadCutoff = context switch { MusicContext.Surface => 900, MusicContext.Plain => 650, MusicContext.Mossy => 480, MusicContext.Ashen => 420, _ => 1000 },
        };
    }

    private static List<ScoreNote> BuildMelody(MusicKey key, long seed, int ctx)
    {
        int rhythmA = Pick(seed, 10, Rhythms.Length), rhythmB = (rhythmA + 1 + Pick(seed, 11, Rhythms.Length - 1)) % Rhythms.Length;
        int contourA = Pick(seed, 12, Contours.Length), contourB = (contourA + 2) % Contours.Length;
        var notes = new List<ScoreNote>();
        for (int phrase = 0; phrase < 4; phrase++)
        {
            var rhythm = Rhythms[phrase == 2 ? rhythmB : rhythmA];
            var contour = Contours[phrase == 2 ? contourB : contourA];
            double beat = phrase * 8;
            for (int i = 0; i < rhythm.Length; i++)
            {
                int degree = contour[i % contour.Length];
                if (phrase == 1 && i > 0) degree += 1; // the answer lifts every note after the first by a step
                if (phrase == 2) degree += 2; // the contrast sits higher
                bool last = i == rhythm.Length - 1;
                if (phrase == 3 && last) degree = 0; // the tune comes home
                double length = rhythm[i];
                if (last && phrase != 3) length = Math.Max(0.5, length - 0.75); // a breath between phrases
                double velocity = 0.75 + 0.2 * Jitter(seed, 13, phrase * 8 + i);
                notes.Add(new ScoreNote(beat, length, key.Midi(degree) + 24, Math.Clamp(velocity, 0.4, 1)));
                beat += rhythm[i];
            }
        }
        return notes;
    }

    private static List<ScoreNote> BuildHarp(MusicKey key, int[] roots, int[] chordOffsets, long seed, int ctx, MusicContext context)
    {
        var dense = DensePatterns[Pick(seed, 20, DensePatterns.Length)];
        var sparse = SparsePatterns[Pick(seed, 21, SparsePatterns.Length)];
        var pattern = context switch
        {
            MusicContext.Mossy => sparse,
            MusicContext.WulverHill => GentlePattern,
            _ => dense,
        };
        var notes = new List<ScoreNote>();
        for (int bar = 0; bar < MusicTuning.Bars; bar++)
            foreach (var (slot, tone) in pattern)
            {
                int degree = roots[bar] + chordOffsets[tone % chordOffsets.Length];
                double velocity = (slot == 0 ? 0.95 : 0.65) + 0.15 * Jitter(seed, 22 + ctx, bar * 8 + slot);
                notes.Add(new ScoreNote(bar * 4 + slot * 0.5, 4, key.Midi(degree) + 12, Math.Clamp(velocity, 0.4, 1)));
            }
        return notes;
    }

    private static List<ScoreNote> BuildShimmer(MusicKey key, int[] roots, int[] chordOffsets, long seed)
    {
        var slots = ShimmerSlots[Pick(seed, 30, ShimmerSlots.Length)];
        var notes = new List<ScoreNote>();
        for (int bar = 0; bar < MusicTuning.Bars; bar++)
            for (int i = 0; i < slots.Length; i++)
            {
                int degree = roots[bar] + chordOffsets[(i + bar) % chordOffsets.Length];
                notes.Add(new ScoreNote(bar * 4 + slots[i] * 0.25, 2, key.Midi(degree) + 48, 0.55 + 0.25 * Jitter(seed, 31, bar * 16 + i)));
            }
        return notes;
    }

    private static List<ScoreNote> BuildPad(MusicKey key, int[] roots, bool pentatonic)
    {
        int[] offsets = pentatonic ? [0, 2, 3] : [0, 2, 4];
        var notes = new List<ScoreNote>();
        for (int bar = 0; bar < MusicTuning.Bars; bar += 2)
            foreach (int o in offsets)
                notes.Add(new ScoreNote(bar * 4, 8, key.Midi(roots[bar] + o) + 12, 0.7));
        return notes;
    }

    private static List<ScoreNote> BuildChoir(MusicKey key, int[] roots, bool pentatonic)
    {
        // Open fifths and an octave: brass and voices sound hollow and large on them.
        int fifth = pentatonic ? 3 : 4, octave = pentatonic ? 5 : 7;
        var notes = new List<ScoreNote>();
        for (int bar = 0; bar < MusicTuning.Bars; bar += 2)
            foreach (int o in new[] { 0, fifth, octave })
                notes.Add(new ScoreNote(bar * 4, 8, key.Midi(roots[bar] + o) + 12, 0.8));
        return notes;
    }
}
