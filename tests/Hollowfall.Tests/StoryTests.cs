using Hollowfall.Core.Combat;
using Hollowfall.Core.Story;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Tests;

public class StoryBookTests
{
    private static int Sentences(string text)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++)
            if (text[i] is '.' or '!' or '?' && (i + 1 == text.Length || text[i + 1] is ' ' or '\''))
                count++;
        return count;
    }

    [Fact]
    public void EveryFragmentHasTextWithinBounds()
    {
        foreach (var text in StoryBook.AllText())
        {
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.Equal(text.Trim(), text);
            Assert.InRange(text.Length, 2, StoryBook.MaxNoteLength);
        }
    }

    [Fact]
    public void TextIsPlainAsciiSoAnyFontCanDrawIt()
    {
        foreach (var text in StoryBook.AllText())
            Assert.All(text, c => Assert.InRange(c, ' ', '~'));
    }

    [Fact]
    public void NotesAreShortAndOriginalLengthWithTwoToFourSentences()
    {
        foreach (var n in StoryBook.Notes)
        {
            Assert.InRange(n.Text.Length, 60, StoryBook.MaxNoteLength);
            Assert.InRange(Sentences(n.Text), 2, 4);
            Assert.InRange(n.Title.Length, 3, 40);
        }
    }

    [Fact]
    public void ThereIsAGoodAmountOfAuthoredText()
    {
        Assert.InRange(StoryBook.Notes.Count, 20, 40);
        Assert.InRange(StoryBook.AllText().Count(), 50, 140);
    }

    [Fact]
    public void IdsAreUniqueAndTitlesAreNotRepeated()
    {
        Assert.Equal(StoryBook.Notes.Count, StoryBook.Notes.Select(n => n.Id).Distinct().Count());
        Assert.Equal(StoryBook.Notes.Count, StoryBook.Notes.Select(n => n.Title).Distinct().Count());
        Assert.Equal(StoryBook.Notes.Count, StoryBook.Notes.Select(n => n.Text).Distinct().Count());
        Assert.Equal(StoryBook.Cards.Count, StoryBook.Cards.Select(c => c.Id).Distinct().Count());
        Assert.Equal(StoryBook.Cards.Count, StoryBook.Cards.Select(c => c.Name).Distinct().Count());
    }

    [Fact]
    public void NoteThreadsReadInOrderWithNoGaps()
    {
        foreach (var thread in StoryBook.Notes.GroupBy(n => (n.Band, n.Thread)))
        {
            var orders = thread.Select(n => n.Order).OrderBy(o => o).ToList();
            Assert.Equal(Enumerable.Range(1, orders.Count), orders);
            Assert.InRange(orders.Count, 1, StoryPlan.MaxNotesPerFloor);
        }
    }

    [Fact]
    public void EveryBandHasNotesAndEnoughCardsToTellFloorsApart()
    {
        foreach (var band in Enum.GetValues<DepthBand>())
        {
            Assert.Contains(StoryBook.Notes, n => n.Band == band);
            Assert.True(StoryBook.Cards.Count(c => c.Band == band) >= 2, $"{band} needs at least two title cards");
            Assert.True(StoryBook.Notes.Where(n => n.Band == band).Select(n => n.Thread).Distinct().Count() >= 2, $"{band} needs at least two threads");
        }
    }

    [Fact]
    public void EveryGradeHasAFloorCompleteLine() =>
        Assert.All(Enum.GetValues<OverallGrade>(), g => Assert.False(string.IsNullOrWhiteSpace(StoryBook.FloorComplete[g])));

    [Fact]
    public void OnlyKnownPlaceholdersAreUsedAndNamesDoNotCollide()
    {
        var known = new[] { "{hollow}", "{Hollow}", "{warden}" };
        foreach (var text in StoryBook.AllText())
        {
            string rest = known.Aggregate(text, (t, k) => t.Replace(k, ""));
            Assert.DoesNotContain('{', rest);
            Assert.DoesNotContain('}', rest);
        }
        foreach (var fixedName in new[] { "Tamsin", "Odile", "Marrec", "Ansel", "Haldane" })
            Assert.DoesNotContain(fixedName, StoryBook.WardenNames);
    }

    [Theory]
    [InlineData(0, 1, DepthBand.Last)]
    [InlineData(0, 2, DepthBand.Upper)]
    [InlineData(1, 2, DepthBand.Last)]
    [InlineData(1, 3, DepthBand.Middle)]
    [InlineData(2, 3, DepthBand.Last)]
    [InlineData(1, 4, DepthBand.Middle)]
    [InlineData(2, 4, DepthBand.Lower)]
    [InlineData(3, 4, DepthBand.Last)]
    [InlineData(3, 6, DepthBand.Middle)]
    public void BandsFollowRelativeDepth(int floor, int count, DepthBand expected) =>
        Assert.Equal(expected, StoryBook.BandOf(floor, count));
}

public class StoryPlanTests
{
    private static string Signature(StoryPlan p) =>
        string.Join("|", p.Title, p.WardenName, string.Join(",", p.Cards.Select(c => c.Title)),
            string.Join(",", p.AllNotes.Select(n => n.Id)), p.Intro[0], p.BossIntro);

    [Fact]
    public void TheSameSeedTellsTheSameStory()
    {
        foreach (long seed in new long[] { 0, 1, -7, 123456789, long.MaxValue, long.MinValue })
            for (int floors = 1; floors <= 6; floors++)
            {
                var a = StoryPlan.For(seed, floors);
                var b = StoryPlan.For(seed, floors);
                Assert.Equal(Signature(a), Signature(b));
                Assert.Equal(a.Cards, b.Cards);
                Assert.Equal(a.AllNotes.ToList(), b.AllNotes.ToList());
                Assert.Equal(a.Intro, b.Intro);
                Assert.Equal(a.Ending, b.Ending, (x, y) => x.Title == y.Title && x.Paragraphs.SequenceEqual(y.Paragraphs));
            }
    }

    [Fact]
    public void DifferentSeedsTellDifferentStories()
    {
        var names = new HashSet<string>();
        var stories = new HashSet<string>();
        var wardens = new HashSet<string>();
        for (long seed = 1; seed <= 300; seed++)
        {
            var p = StoryPlan.For(seed, 3);
            names.Add(p.Title);
            wardens.Add(p.WardenName);
            stories.Add(Signature(p));
        }
        Assert.True(names.Count > 100, $"only {names.Count} dungeon names in 300 seeds");
        Assert.Equal(StoryBook.WardenNames.Length, wardens.Count);
        Assert.True(stories.Count > 250, $"only {stories.Count} distinct stories in 300 seeds");
        Assert.NotEqual(Signature(StoryPlan.For(1, 3)), Signature(StoryPlan.For(2, 3)));
    }

    [Fact]
    public void AnySeedNamesItsDungeonWithoutNeedingTheFloorCount()
    {
        for (long seed = -20; seed <= 20; seed++)
        {
            string title = StoryPlan.TitleFor(seed);
            Assert.StartsWith("The ", title);
            Assert.Equal(title, StoryPlan.For(seed, 2).Title);
            Assert.Equal(title, StoryPlan.For(seed, 4).Title);
        }
    }

    [Fact]
    public void ANamedSeedKeepsItsNameFromTheSeedText()
    {
        long seed = TerrainGenerator.SeedFromString("amber-hollow-914");
        Assert.Equal(StoryPlan.TitleFor(seed), StoryPlan.TitleFor(TerrainGenerator.SeedFromString("amber-hollow-914")));
    }

    [Fact]
    public void EveryFloorCountWorksAndNoTwoFloorsShareATitle()
    {
        for (long seed = 1; seed <= 60; seed++)
            for (int floors = 1; floors <= StoryPlan.MaxFloors + 5; floors++)
            {
                var p = StoryPlan.For(seed, floors);
                int expected = Math.Min(floors, StoryPlan.MaxFloors);
                Assert.Equal(expected, p.Cards.Count);
                Assert.Equal(expected, p.Cards.Select(c => c.Title).Distinct().Count());
                Assert.Equal(expected, p.Cards.Select(c => c.Name).Distinct().Count());
                Assert.All(p.Cards, c =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(c.Name));
                    Assert.False(string.IsNullOrWhiteSpace(c.Epigraph));
                    Assert.DoesNotContain('{', c.Name + c.Epigraph);
                });
                Assert.Equal("I", p.Cards[0].Numeral);
            }
    }

    [Fact]
    public void EveryNoteIsUsedAtMostOncePerRun()
    {
        for (long seed = 1; seed <= 60; seed++)
            for (int floors = 1; floors <= 8; floors++)
            {
                var ids = StoryPlan.For(seed, floors).AllNotes.Select(n => n.Id).ToList();
                Assert.Equal(ids.Count, ids.Distinct().Count());
            }
    }

    [Fact]
    public void ARunIsOneVoiceInReadingOrderFromTheRightDepth()
    {
        for (long seed = 1; seed <= 80; seed++)
            for (int floors = 2; floors <= 4; floors++)
            {
                var plan = StoryPlan.For(seed, floors);
                for (int f = 0; f < floors; f++)
                {
                    var run = plan.RunFor(f);
                    Assert.InRange(run.Count, 1, StoryPlan.MaxNotesPerFloor);
                    Assert.Equal(run.Select(n => n.Order).OrderBy(o => o), run.Select(n => n.Order));
                    var defs = run.Select(n => StoryBook.Notes.Single(d => d.Id == n.Id)).ToList();
                    Assert.Single(defs.Select(d => d.Thread).Distinct());
                    Assert.All(defs, d => Assert.Equal(StoryBook.BandOf(f, floors), d.Band));
                    Assert.All(run, n => Assert.Equal(f, n.Floor));
                }
            }
    }

    [Fact]
    public void TheFirstFloorIsUpperAndTheLastIsTheBossFloor()
    {
        var plan = StoryPlan.For(5, 4);
        Assert.Equal("I", plan.Cards[0].Numeral);
        Assert.Equal("IV", plan.Cards[3].Numeral);
        Assert.Contains(plan.Cards[3].Name, StoryBook.Cards.Where(c => c.Band == DepthBand.Last).Select(c => c.Name));
        Assert.Contains(plan.Cards[0].Name, StoryBook.Cards.Where(c => c.Band == DepthBand.Upper).Select(c => c.Name));
        Assert.True(plan.IsBossFloor(3));
        Assert.False(plan.IsBossFloor(2));
    }

    [Fact]
    public void NoPlaceholderSurvivesAndTheNamesAreFilledIn()
    {
        for (long seed = 1; seed <= 100; seed++)
        {
            var p = StoryPlan.For(seed, 4);
            var all = p.Intro.Concat(p.Ending.Paragraphs).Append(p.BossIntro)
                .Concat(p.AllNotes.SelectMany(n => new[] { n.Title, n.Text }))
                .Concat(p.Cards.SelectMany(c => new[] { c.Name, c.Epigraph }))
                .Concat(Enum.GetValues<OverallGrade>().Select(p.FloorCompleteLine))
                .Concat(Enumerable.Range(0, 8).Select(d => p.DeathLine(2, d)))
                .ToList();
            Assert.All(all, t =>
            {
                Assert.DoesNotContain('{', t);
                Assert.DoesNotContain('}', t);
                Assert.False(string.IsNullOrWhiteSpace(t));
            });
            Assert.Contains(p.WardenName, p.BossIntro);
            Assert.Contains(p.Name, string.Join(" ", p.Intro));
            Assert.Equal(4, p.Intro.Count);
        }
    }

    [Fact]
    public void AcrossSeedsEveryAuthoredFragmentGetsUsed()
    {
        var notes = new HashSet<string>();
        var cards = new HashSet<string>();
        var openers = new HashSet<string>();
        var bosses = new HashSet<string>();
        var wardenNotes = new HashSet<string>();
        for (long seed = 1; seed <= 600; seed++)
            for (int floors = 2; floors <= 4; floors++)
            {
                var p = StoryPlan.For(seed, floors);
                foreach (var n in p.AllNotes) notes.Add(n.Id);
                foreach (var c in p.Cards) cards.Add(c.Name);
                openers.Add(p.Intro[0]);
                bosses.Add(p.BossIntro);
                wardenNotes.Add(p.WardenName);
            }
        Assert.Equal(StoryBook.Notes.Select(n => n.Id).Order(), notes.Order());
        Assert.Equal(StoryBook.Cards.Select(c => c.Name).Order(), cards.Order());
        // Names are filled in, so the variants are told apart by the words around them.
        foreach (var marker in new[] { "Beneath the hill lies", "is the name on the old maps", "Every night the Hollow" })
            Assert.Contains(openers, o => o.Contains(marker));
        foreach (var marker in new[] { "turns from the open door", "That craft is mine", "The cord ends at" })
            Assert.Contains(bosses, b => b.Contains(marker));
        Assert.Equal(StoryBook.WardenNames.Length, wardenNotes.Count);
    }

    [Fact]
    public void DyingTwiceInARowNeverRepeatsALine()
    {
        var plan = StoryPlan.For(9, 3);
        for (int deaths = 0; deaths < 20; deaths++)
            Assert.NotEqual(plan.DeathLine(1, deaths), plan.DeathLine(1, deaths + 1));
        Assert.Equal(plan.DeathLine(1, 3), plan.DeathLine(1, 3));
        Assert.False(string.IsNullOrEmpty(plan.DeathLine(-1, -5)));
    }

    [Fact]
    public void LookupsAreSafeForAnythingTheSaveFileMightHold()
    {
        var plan = StoryPlan.For(3, 3);
        Assert.Null(plan.Note("nope"));
        Assert.Empty(plan.RunFor(-1));
        Assert.Empty(plan.RunFor(99));
        Assert.Null(plan.CardFor(7));
        Assert.All(plan.AllNotes, n => Assert.Same(n, plan.Note(n.Id)));
    }
}

public class StoryProgressTests
{
    private const string Seed = "amber-hollow-914";
    private static readonly string First = StoryBook.Notes[0].Id;
    private static readonly string Second = StoryBook.Notes[1].Id;

    [Fact]
    public void ReadingANoteIsRememberedOnceAndPerSeed()
    {
        var p = new StoryProgress();
        Assert.False(p.IsRead(Seed, First));
        Assert.True(p.MarkRead(Seed, First));
        Assert.False(p.MarkRead(Seed, First));
        Assert.True(p.IsRead(Seed, First));
        Assert.False(p.IsRead("other", First));
        Assert.Equal([First], p.ReadIds(Seed));
        Assert.Empty(p.ReadIds("other"));
    }

    [Fact]
    public void UnknownNotesAndBadSeedsAreRefused()
    {
        var p = new StoryProgress();
        Assert.False(p.MarkRead(Seed, "not-a-note"));
        Assert.False(p.MarkRead("", First));
        Assert.False(p.MarkRead(new string('x', SeedWords.MaxLength + 1), First));
        Assert.Equal(0, p.SeedCount);
    }

    [Fact]
    public void TheIntroAndEndingAreRemembered()
    {
        var p = new StoryProgress();
        Assert.False(p.IntroSeen(Seed));
        p.MarkIntroSeen(Seed);
        Assert.True(p.IntroSeen(Seed));
        Assert.False(p.IntroSeen("other"));
        Assert.False(p.EndingSeen(Seed));
        p.MarkEndingSeen(Seed);
        Assert.True(p.EndingSeen(Seed));
    }

    [Fact]
    public void ProgressSurvivesTheRoundTripIncludingOddSeeds()
    {
        var p = new StoryProgress();
        p.MarkRead(Seed, First);
        p.MarkRead(Seed, Second);
        p.MarkIntroSeen(Seed);
        p.MarkRead("weird \"seed\" \\ ünï", First);
        p.MarkEndingSeen("weird \"seed\" \\ ünï");
        var back = StoryProgress.FromJson(p.ToJson());
        Assert.Equal([First, Second], back.ReadIds(Seed));
        Assert.True(back.IntroSeen(Seed));
        Assert.False(back.EndingSeen(Seed));
        Assert.True(back.IsRead("weird \"seed\" \\ ünï", First));
        Assert.True(back.EndingSeen("weird \"seed\" \\ ünï"));
        Assert.Equal(2, back.SeedCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"seeds\": 5}")]
    [InlineData("{\"seeds\": []}")]
    [InlineData("{\"seeds\": {\"a\": 7, \"b\": {\"notes\": 3}, \"c\": {\"notes\": [1, null, {}, [], \"zzz\"]}}}")]
    public void DamagedFilesLoseOnlyTheBadParts(string? json)
    {
        var p = StoryProgress.FromJson(json);
        Assert.Empty(p.ReadIds("a"));
        Assert.Empty(p.ReadIds("c"));
        Assert.False(p.IntroSeen("b"));
    }

    [Fact]
    public void GoodEntriesSurviveNextToBadOnes()
    {
        string json = "{\"seeds\": {\"s\": {\"intro\": true, \"ending\": \"yes\", \"notes\": [\"" + First + "\", \"ghost\", 4, \"" + First + "\", \"" + Second + "\"]}, \"\": {\"intro\": true}}}";
        var p = StoryProgress.FromJson(json);
        Assert.Equal([First, Second], p.ReadIds("s"));
        Assert.True(p.IntroSeen("s"));
        Assert.False(p.EndingSeen("s"));
        Assert.Equal(1, p.SeedCount);
    }

    [Fact]
    public void SizesAreBounded()
    {
        var p = new StoryProgress();
        for (int i = 0; i < StoryProgress.MaxSeeds + 25; i++) p.MarkIntroSeen($"seed-{i}");
        Assert.Equal(StoryProgress.MaxSeeds, p.SeedCount);
        Assert.False(p.IntroSeen("seed-0"));
        Assert.True(p.IntroSeen($"seed-{StoryProgress.MaxSeeds + 24}"));

        // A file with far more notes than the cap, and an oversized file, are cut down or refused.
        var many = string.Join(",", Enumerable.Repeat($"\"{First}\"", 500));
        Assert.Single(StoryProgress.FromJson("{\"seeds\": {\"s\": {\"notes\": [" + many + "]}}}").ReadIds("s"));
        string huge = "{\"seeds\": {\"s\": {\"intro\": true}}, \"pad\": \"" + new string('x', StoryProgress.MaxFileBytes) + "\"}";
        Assert.Equal(0, StoryProgress.FromJson(huge).SeedCount);
        Assert.Equal(0, StoryProgress.FromJson("{\"seeds\": {\"" + new string('s', SeedWords.MaxLength + 1) + "\": {\"intro\": true}}}").SeedCount);
    }

    [Fact]
    public void ANoteListNeverGrowsPastTheCap()
    {
        var p = new StoryProgress();
        foreach (var n in StoryBook.Notes) p.MarkRead(Seed, n.Id);
        Assert.True(p.ReadIds(Seed).Count <= StoryProgress.MaxNotesPerSeed);
        Assert.Equal(StoryBook.Notes.Count, p.ReadIds(Seed).Count);
    }

    [Fact]
    public void SavesAtomicallyAndLoadsBackFromDisk()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hollowfall-story-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(dir, "nested", "story.json");
            var p = new StoryProgress();
            p.MarkRead(Seed, First);
            Assert.True(p.TrySave(path));
            Assert.False(File.Exists(path + ".tmp"));
            Assert.Equal([First], StoryProgress.Load(path).ReadIds(Seed));

            File.WriteAllText(path, "{{{ garbage");
            Assert.Equal(0, StoryProgress.Load(path).SeedCount);
            Assert.Equal(0, StoryProgress.Load(Path.Combine(dir, "missing.json")).SeedCount);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AnUnwritablePathFailsQuietly()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hollowfall-story-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // The target is a directory, so the move cannot succeed.
            string blocked = Path.Combine(dir, "story.json");
            Directory.CreateDirectory(blocked);
            Assert.False(new StoryProgress().TrySave(blocked));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
