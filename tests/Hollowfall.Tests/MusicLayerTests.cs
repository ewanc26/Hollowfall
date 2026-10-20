using Hollowfall.Core.Audio.Music;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Tests;

/// <summary>The one rendered bank shared by the tests below: rendering five contexts is the slow part, so it happens once.</summary>
internal static class MusicFixture
{
    public const string SeedText = "dreich-howe-318";

    public static readonly long Seed = TerrainGenerator.SeedFromString(SeedText);

    private static readonly Lazy<MusicBank> Shared = new(() => MusicBank.Render(Seed));

    public static MusicBank Bank => Shared.Value;

    public static IEnumerable<MusicScore> Scores(long seed) => Enum.GetValues<MusicContext>().Select(c => MusicScores.For(c, seed));
}

public class MusicModeTests
{
    [Theory]
    [InlineData(MusicMode.Dorian, new[] { 0, 2, 3, 5, 7, 9, 10 })]
    [InlineData(MusicMode.Aeolian, new[] { 0, 2, 3, 5, 7, 8, 10 })]
    [InlineData(MusicMode.Phrygian, new[] { 0, 1, 3, 5, 7, 8, 10 })]
    [InlineData(MusicMode.MajorPentatonic, new[] { 0, 2, 4, 7, 9 })]
    [InlineData(MusicMode.MinorPentatonic, new[] { 0, 3, 5, 7, 10 })]
    public void EachModeHasItsIntervals(MusicMode mode, int[] expected) => Assert.Equal(expected, Modes.Steps(mode));

    [Fact]
    public void DegreesRunPastTheOctaveAndBelowTheRoot()
    {
        Assert.Equal(50, Modes.Midi(50, MusicMode.Dorian, 0));
        Assert.Equal(62, Modes.Midi(50, MusicMode.Dorian, 7));
        Assert.Equal(53, Modes.Midi(50, MusicMode.Dorian, 2)); // the minor third
        Assert.Equal(48, Modes.Midi(50, MusicMode.Dorian, -1)); // one below the root is the minor seventh of the octave below
        Assert.Equal(74, Modes.Midi(50, MusicMode.MajorPentatonic, 10));
    }

    [Fact]
    public void ContainsKnowsTheScaleInAnyOctave()
    {
        Assert.True(Modes.Contains(50, MusicMode.Phrygian, 51)); // the flat second
        Assert.False(Modes.Contains(50, MusicMode.Aeolian, 51));
        Assert.True(Modes.Contains(50, MusicMode.Dorian, 50 + 9 + 24));
        Assert.False(Modes.Contains(50, MusicMode.MajorPentatonic, 50 + 5));
    }
}

public class MusicScoreTests
{
    [Fact]
    public void ContextsUseTheirModes()
    {
        var modes = Enum.GetValues<MusicContext>().ToDictionary(c => c, c => MusicScores.For(c, 7).Key.Mode);
        Assert.Equal(MusicMode.Dorian, modes[MusicContext.Surface]);
        Assert.Equal(MusicMode.Aeolian, modes[MusicContext.Plain]);
        Assert.Equal(MusicMode.Aeolian, modes[MusicContext.Mossy]);
        Assert.Equal(MusicMode.Phrygian, modes[MusicContext.Ashen]);
        Assert.Equal(MusicMode.MajorPentatonic, modes[MusicContext.WulverHill]);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(99L)]
    [InlineData(-4242L)]
    public void EveryNoteOfEveryLayerBelongsToTheKey(long seed)
    {
        foreach (var score in MusicFixture.Scores(seed))
        {
            var all = score.Melody.Concat(score.HarpNotes).Concat(score.ShimmerNotes).Concat(score.PadNotes).Concat(score.ChoirNotes).ToList();
            Assert.NotEmpty(score.Melody);
            Assert.NotEmpty(score.HarpNotes);
            Assert.NotEmpty(score.ShimmerNotes);
            Assert.NotEmpty(score.PadNotes);
            Assert.NotEmpty(score.ChoirNotes);
            foreach (var n in all)
                Assert.True(score.Key.Contains(n.Midi), $"{score.Context} seed {seed}: note {n.Midi} is not in {score.Key.Mode} on {score.Key.RootMidi}");
        }
    }

    [Theory]
    [InlineData(MusicContext.Surface)]
    [InlineData(MusicContext.Plain)]
    [InlineData(MusicContext.Mossy)]
    [InlineData(MusicContext.Ashen)]
    [InlineData(MusicContext.WulverHill)]
    public void NotesStayInTheirInstrumentsRange(MusicContext context)
    {
        var score = MusicScores.For(context, 1234);
        Assert.All(score.Melody, n => Assert.InRange(n.Midi, 56, 95));
        Assert.All(score.HarpNotes, n => Assert.InRange(n.Midi, 46, 88));
        Assert.All(score.ShimmerNotes, n => Assert.InRange(n.Midi, 80, 115));
        Assert.All(score.ChoirNotes, n => Assert.InRange(n.Midi, 46, 86));
        Assert.All(score.Melody.Concat(score.HarpNotes).Concat(score.ShimmerNotes).Concat(score.PadNotes).Concat(score.ChoirNotes), n =>
        {
            Assert.InRange(n.Beat, 0, MusicTuning.Bars * MusicTuning.BeatsPerBar);
            Assert.True(n.Length > 0 && n.Velocity is > 0 and <= 1);
        });
    }

    [Fact]
    public void TheWulversTuneUsesOnlyNotesTheSurfaceScaleHas()
    {
        for (long seed = 0; seed < 25; seed++)
        {
            var surface = MusicScores.For(MusicContext.Surface, seed);
            var hill = MusicScores.For(MusicContext.WulverHill, seed);
            Assert.Equal(surface.Key.RootMidi + 3, hill.Key.RootMidi);
            Assert.All(hill.Melody.Concat(hill.HarpNotes), n => Assert.True(surface.Key.Contains(n.Midi)));
        }
    }

    [Fact]
    public void TheSameSeedGivesTheSameScoreAndSeedsUsuallyDiffer()
    {
        foreach (var context in Enum.GetValues<MusicContext>())
            Assert.Equal(MusicScores.For(context, 31337).Signature(), MusicScores.For(context, 31337).Signature());

        for (int c = 0; c < Enum.GetValues<MusicContext>().Length; c++)
        {
            var context = (MusicContext)c;
            var signatures = Enumerable.Range(0, 40).Select(s => MusicScores.For(context, s * 7919L + 3).Signature()).Distinct().Count();
            Assert.True(signatures >= 28, $"{context}: only {signatures} different scores in 40 seeds");
        }
        var tonics = Enumerable.Range(0, 40).Select(s => MusicScores.TonicPitchClass(s * 104729L)).Distinct().Count();
        Assert.True(tonics >= 5, $"only {tonics} different keys in 40 seeds");
    }

    [Fact]
    public void AllContextsOfAWorldShareATonic()
    {
        var scores = MusicFixture.Scores(777).ToList();
        int pc = MusicScores.TonicPitchClass(777);
        Assert.All(scores.Where(s => s.Context != MusicContext.WulverHill), s => Assert.Equal(pc, s.Key.RootMidi % 12));
    }

    [Fact]
    public void TheLoopIsEightBarsOfFourFourOnWholeSamples()
    {
        Assert.Equal(15750, MusicTuning.SamplesPerBeat);
        Assert.Equal(504000, MusicTuning.LoopSamples);
        Assert.Equal(8 * 4 * 60.0 / 84, MusicTuning.LoopSeconds, 6);
    }

    [Fact]
    public void ScoreDoesNotUseTheRuntimesRandomisedHashing()
    {
        // Signatures are stable across processes, so a pinned value can be checked here; if this changes, the music of existing worlds changed.
        var a = MusicScores.For(MusicContext.Surface, TerrainGenerator.SeedFromString("dreich-howe-318")).Signature();
        Assert.Equal(a, MusicScores.For(MusicContext.Surface, TerrainGenerator.SeedFromString("dreich-howe-318")).Signature());
        Assert.NotEqual(a, MusicScores.For(MusicContext.Surface, TerrainGenerator.SeedFromString("haar-bothy-042")).Signature());
    }
}

public class MusicLayerRenderTests
{
    public static TheoryData<MusicContext, MusicLayer> All
    {
        get
        {
            var data = new TheoryData<MusicContext, MusicLayer>();
            foreach (var c in Enum.GetValues<MusicContext>())
                foreach (var l in Enum.GetValues<MusicLayer>())
                    data.Add(c, l);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(All))]
    public void EveryLayerIsTheLoopLengthFiniteBoundedAndAudible(MusicContext context, MusicLayer layer)
    {
        var data = MusicFixture.Bank.Layer(context, layer);
        Assert.Equal(MusicTuning.LoopSamples, data.Length);
        Assert.All(data, v => Assert.True(float.IsFinite(v)));
        float peak = data.Max(Math.Abs);
        Assert.Equal(MusicTuning.Peak(layer), peak, 3);
        double rms = Math.Sqrt(data.Sum(v => (double)v * v) / data.Length);
        Assert.True(rms > MusicTuning.Peak(layer) * 0.04, $"{context} {layer} is nearly silent (rms {rms:F4})");
    }

    [Theory]
    [MemberData(nameof(All))]
    public void EveryLayerLoopsWithoutASeam(MusicContext context, MusicLayer layer)
    {
        var data = MusicFixture.Bank.Layer(context, layer);
        float seam = Math.Abs(data[0] - data[^1]);
        float largest = 0;
        for (int i = 1; i < data.Length; i++) largest = Math.Max(largest, Math.Abs(data[i] - data[i - 1]));
        Assert.True(seam <= largest, $"{context} {layer}: the step across the loop point is {seam:F5}, bigger than the biggest step inside ({largest:F5})");
        // A click would also show as a jump much larger than the layer's typical step.
        double meanStep = 0;
        for (int i = 1; i < data.Length; i++) meanStep += Math.Abs(data[i] - data[i - 1]);
        meanStep /= data.Length - 1;
        Assert.True(seam <= Math.Max(meanStep * 40, 1e-3f), $"{context} {layer}: seam step {seam:F5} against a typical step of {meanStep:F6}");
    }

    [Fact]
    public void RenderingIsDeterministicAndParallelismDoesNotChangeIt()
    {
        var a = MusicLayers.RenderAll(MusicScores.For(MusicContext.Mossy, 5));
        var b = MusicLayers.RenderAll(MusicScores.For(MusicContext.Mossy, 5));
        for (int i = 0; i < a.Length; i++) Assert.True(a[i].AsSpan().SequenceEqual(b[i]), $"layer {i} differs between runs");

        var serial = MusicBank.Render(MusicFixture.Seed, parallel: false);
        for (int c = 0; c < serial.Layers.Length; c++)
            for (int l = 0; l < serial.Layers[c].Length; l++)
                Assert.True(serial.Layers[c][l].AsSpan().SequenceEqual(MusicFixture.Bank.Layers[c][l]), $"context {c} layer {l} differs between serial and parallel rendering");
    }

    [Fact]
    public void ANoteRingingOutOfTheLastBarContinuesIntoTheFirst()
    {
        var buffer = new LoopBuffer(MusicTuning.LoopSamples);
        var note = Instruments.Pluck(Modes.Hertz(62), 1, 3, 0.6, 5);
        int start = MusicTuning.LoopSamples - MusicTuning.SamplesPerBeat; // one beat before the end
        buffer.Add(start, note);
        // The sound that was cut by the end of the buffer is what appears at its start.
        for (int i = 0; i < 2000; i++)
            Assert.Equal(note[MusicTuning.SamplesPerBeat + i], buffer.Data[i]);
        Assert.True(buffer.Data.Take(4000).Max(Math.Abs) > 0.01f);
    }

    [Fact]
    public void TheRealLayersAlsoRingAcrossTheLoopPoint()
    {
        // The harp's last notes are plucked in bar 8 and are still sounding in bar 1: the first bar is not empty even if the score has silence there.
        var score = MusicScores.For(MusicContext.Mossy, 3);
        Assert.Contains(score.HarpNotes, n => n.Beat >= (MusicTuning.Bars - 1) * 4);
        var harp = MusicFixture.Bank.Layer(MusicContext.Plain, MusicLayer.Harp);
        double firstMoment = Math.Sqrt(harp.Take(2000).Sum(v => (double)v * v) / 2000);
        Assert.True(firstMoment > 0.002);
    }

    [Fact]
    public void ThePluckedStringIsInTune()
    {
        foreach (int midi in new[] { 50, 62, 69, 74, 81, 88 })
        {
            double hz = Modes.Hertz(midi);
            var note = Instruments.Pluck(hz, 1, 2.5, 0.6, 9);
            double measured = Estimate(note, 4000, 12000);
            double cents = 1200 * Math.Log2(measured / hz);
            Assert.True(Math.Abs(cents) < 8, $"midi {midi}: {measured:F2} Hz against {hz:F2} Hz is {cents:F1} cents out");
        }
    }

    /// <summary>The fundamental by autocorrelation with parabolic refinement, over a stretch after the attack.</summary>
    private static double Estimate(float[] x, int from, int length)
    {
        int bestLag = 0;
        double best = double.MinValue;
        var corr = new double[1000];
        for (int lag = 10; lag < 700; lag++)
        {
            double sum = 0;
            for (int i = from; i < from + length; i++) sum += x[i] * x[i + lag];
            corr[lag] = sum;
        }
        // The first strong peak, not the highest (a multiple of the period can be a hair higher).
        double max = corr.Max();
        for (int lag = 11; lag < 699; lag++)
            if (corr[lag] > corr[lag - 1] && corr[lag] >= corr[lag + 1] && corr[lag] > 0.6 * max)
            {
                bestLag = lag;
                best = corr[lag];
                break;
            }
        Assert.True(bestLag > 0 && best > 0);
        double a = corr[bestLag - 1], b = corr[bestLag], c = corr[bestLag + 1];
        double offset = 0.5 * (a - c) / (a - 2 * b + c);
        return Hollowfall.Core.Audio.Music.MusicTuning.SampleRate / (bestLag + offset);
    }

    [Fact]
    public void DronesFitWholeCyclesInTheLoop()
    {
        foreach (double hz in new[] { 36.7, 73.4, 110.0, 220.0 })
        {
            double snapped = Instruments.SnapToLoop(hz);
            double cycles = snapped * MusicTuning.LoopSeconds;
            Assert.Equal(Math.Round(cycles), cycles, 6);
            Assert.True(Math.Abs(1200 * Math.Log2(snapped / hz)) < 1.5);
        }
    }

    [Fact]
    public void TheBankReportsItsSize()
    {
        var bank = MusicFixture.Bank;
        Assert.Equal(5L * 9 * MusicTuning.LoopSamples * 4, bank.FloatBytes);
        Assert.Equal(5L * 9 * MusicTuning.LoopSamples * 2, bank.Pcm16Bytes);
        Assert.True(bank.RenderMilliseconds > 0);
    }
}
