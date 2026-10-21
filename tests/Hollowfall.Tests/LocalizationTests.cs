using System.Text;
using System.Text.RegularExpressions;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Input;
using Hollowfall.Core.Localization;
using Hollowfall.Core.Story;
using Hollowfall.Core.Story.Dialogue;
using Hollowfall.Core.Terrain;
using Xunit.Abstractions;

namespace Hollowfall.Tests;

internal static class RepoFiles
{
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Hollowfall.Core"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    public static IEnumerable<string> SourceFiles(params string[] folders) =>
        folders.SelectMany(f => Directory.EnumerateFiles(Path.Combine(Root(), f), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
}

public class LocTableTests(ITestOutputHelper output)
{
    // Scottish Gaelic takes the grave accent only; the acute belongs to Irish (Gaeilge), which must never be mixed in.
    private const string Acute = "áéíóúÁÉÍÓÚ";
    private const string Grave = "àèìòùÀÈÌÒÙ";

    private static bool GaelicChar(char c) => c is >= ' ' and <= '~' || Grave.Contains(c) || c is '\n' or '…';

    [Fact]
    public void EveryKeyIsUniqueAndNeverEmpty()
    {
        var seen = new HashSet<string>();
        foreach (var e in LocTable.All)
        {
            Assert.True(seen.Add(e.Key), $"duplicate key {e.Key}");
            Assert.Matches(@"^[a-z0-9]+(\.[A-Za-z0-9\-]+)+$", e.Key);
            Assert.False(string.IsNullOrWhiteSpace(e.English), $"{e.Key} has no English");
            if (e.Gaelic != null) Assert.False(string.IsNullOrWhiteSpace(e.Gaelic), $"{e.Key} has empty Gàidhlig");
        }
    }

    [Fact]
    public void PlaceholdersMatchInBothLanguages()
    {
        foreach (var e in LocTable.All.Where(e => e.HasGaelic))
            Assert.True(LocTable.Placeholders(e.English).SetEquals(LocTable.Placeholders(e.Gaelic!)), $"{e.Key}: placeholders differ");
    }

    [Fact]
    public void AnyGaelicHasASourceAndEverySourceIdIsKnown()
    {
        foreach (var e in LocTable.All)
        {
            if (!e.HasGaelic)
            {
                Assert.Equal("", e.Source);
                continue;
            }
            Assert.False(string.IsNullOrWhiteSpace(e.Source), $"{e.Key} has Gàidhlig but cites no source");
            foreach (var id in e.Source.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                Assert.True(LocSources.All.ContainsKey(id), $"{e.Key} cites unknown source {id}");
            Assert.False(string.IsNullOrWhiteSpace(e.Note), $"{e.Key} needs a note saying how it was reached");
        }
    }

    [Fact]
    public void GaelicUsesGraveAccentsOnlyAndNeverIrishAcutes()
    {
        var texts = LocTable.All.Where(e => e.HasGaelic).Select(e => e.Gaelic!)
            .Concat(GaelicNames.Nouns.Select(n => n.Gaelic))
            .Concat(GaelicNames.Adjectives.SelectMany(a => new[] { a.Base, a.Lenited }))
            .Concat(Enum.GetValues<Language>().Select(Languages.NativeName));
        foreach (var t in texts)
            foreach (char c in t)
            {
                Assert.False(Acute.Contains(c), $"'{t}' has an acute accent ({c}); Scottish Gaelic uses the grave");
                Assert.True(GaelicChar(c), $"'{t}' has an unexpected character {c} (U+{(int)c:X4})");
            }
    }

    [Fact]
    public void TheLanguageNamesAreWrittenInThemselves()
    {
        Assert.Equal("English", Languages.NativeName(Language.English));
        Assert.Equal("Gàidhlig", Languages.NativeName(Language.Gaelic));
        Assert.Equal("Gàidhlig agus Beurla", Languages.NativeName(Language.Bilingual));
    }

    [Fact]
    public void ReviewStatusCountsArePrintedAndAddUp()
    {
        output.WriteLine("Area                      Verified  Likely  NeedsReview  Untranslated");
        foreach (var (area, entries) in LocTable.Areas)
        {
            var c = LocTable.Counts(entries);
            output.WriteLine($"{area,-24} {c.Verified,8} {c.Likely,7} {c.NeedsReview,12} {c.Untranslated,13}");
        }
        var total = LocTable.Counts();
        output.WriteLine($"{"All",-24} {total.Verified,8} {total.Likely,7} {total.NeedsReview,12} {total.Untranslated,13}");
        Assert.Equal(LocTable.All.Count, total.Verified + total.Likely + total.NeedsReview + total.Untranslated);
        Assert.True(total.Verified > 0 && total.Likely > 0 && total.NeedsReview > 0 && total.Untranslated > 0);

        int storyLinesInEnglish = StoryBook.AllText().Count();
        output.WriteLine($"Story text still English only, outside the tables: {storyLinesInEnglish} strings in StoryBook (epigraphs, notes, opening, boss, floor-complete and death lines)");
    }

    [Fact]
    public void EveryActionGroupRankAndCardHasAnEntryAndTheEnglishIsWhatTheGameShowedBefore()
    {
        foreach (var a in Enum.GetValues<GameAction>())
            Assert.Equal(ActionInfo.Label(a), LocTable.Find(InterfaceStrings.ActionKey(a))!.English);
        foreach (var g in Enum.GetValues<ActionGroup>())
            Assert.Equal(g.ToString().ToUpperInvariant(), LocTable.Find(InterfaceStrings.GroupKey(g))!.English);
        foreach (var r in Enum.GetValues<StyleRank>().Where(r => r != StyleRank.None))
            Assert.Equal(r.ToString().ToUpperInvariant(), LocTable.Find(InterfaceStrings.RankKey(r))!.English);
        foreach (var card in StoryBook.Cards)
            Assert.Equal(card.Name, LocTable.Find(StoryStrings.CardKey(card.Name))!.English);
    }

    [Fact]
    public void EveryKeyUsedInCodeHasATableEntry()
    {
        var call = new Regex(@"\.(?:Tr|English|GaelicOf)\(\s*""([^""]+)""");
        var used = new HashSet<string>();
        foreach (var file in RepoFiles.SourceFiles(Path.Combine("src", "Hollowfall.Core"), Path.Combine("game", "scripts")))
            foreach (Match m in call.Matches(File.ReadAllText(file)))
                used.Add(m.Groups[1].Value);

        foreach (var key in used) Assert.True(LocTable.Has(key), $"the code asks for '{key}', which has no table entry");
    }

    [Fact]
    public void GaelicLinesOfTheWulverAreGraveOnlyAndHaveEnglishBeneath()
    {
        foreach (var e in WulverGreetings.All)
        {
            string line = WulverGreetings.Line(e);
            Assert.Contains("\n(", line);
            Assert.EndsWith(")", line);
            Assert.DoesNotContain(line, c => Acute.Contains(c));
        }
    }
}

public class LocTests
{
    [Fact]
    public void EnglishShowsTheEnglishAndFillsPlaceholders()
    {
        var loc = new Loc();
        Assert.Equal("Play", loc.Tr("menu.play"));
        Assert.Equal("Floor 2 / 3", loc.Tr("hud.floor", 2, 3));
        Assert.Equal("Keys 4", loc.Tr("hud.keys", 4));
    }

    [Fact]
    public void GaelicShowsGaelicWhereThereIsSomeAndEnglishWhereThereIsNot()
    {
        var loc = new Loc(Language.Gaelic);
        Assert.Equal("Cluich", loc.Tr("menu.play"));
        Assert.Equal("Iuchraichean 4", loc.Tr("hud.keys", 4));
        Assert.Equal("Controls", loc.Tr("menu.controls"));
        Assert.Empty(loc.MissingKeys); // a key with no Gàidhlig is not a missing key
    }

    [Fact]
    public void AStringMarkedNeedsReviewIsHeldBackUnlessAskedFor()
    {
        var loc = new Loc(Language.Gaelic);
        Assert.Equal("Floor 2 / 3", loc.Tr("hud.floor", 2, 3));
        loc.ShowNeedsReview = true;
        Assert.Equal("Làr 2 / 3", loc.Tr("hud.floor", 2, 3));
    }

    [Fact]
    public void BilingualPutsShortTextInBracketsAndLongTextOnTwoLines()
    {
        var loc = new Loc(Language.Bilingual);
        Assert.Equal("Cluich (Play)", loc.Tr("menu.play"));
        Assert.Equal("Controls", loc.Tr("menu.controls"));
        string longer = loc.Tr("prompt.read", "F", "Day Two");
        Assert.Equal("F  leugh: Day Two\nF  read: Day Two", longer);
    }

    [Fact]
    public void AMissingKeyReturnsTheKeyAndIsRecordedOnce()
    {
        var loc = new Loc(Language.Gaelic);
        Assert.Equal("no.such.key", loc.Tr("no.such.key"));
        loc.Tr("no.such.key");
        Assert.Equal(["no.such.key"], loc.MissingKeys);
        Assert.Equal("no.such.key", loc.English("no.such.key"));
    }

    [Fact]
    public void ChangingTheLanguageRaisesChangedOnceAndAnUndefinedValueIsEnglish()
    {
        var loc = new Loc();
        int raised = 0;
        loc.Changed += () => raised++;
        loc.Language = Language.Gaelic;
        loc.Language = Language.Gaelic;
        Assert.Equal(1, raised);
        loc.Language = (Language)99;
        Assert.Equal(Language.English, loc.Language);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void AWrongNumberOfArgumentsShowsTheTextAndDoesNotThrow() => Assert.Equal("Floor {0} / {1}", new Loc().Tr("hud.floor", "only one"));

    [Fact]
    public void TextNeverDependsOnACount()
    {
        // Labels take the number beside them, so there is no singular or plural form of anything.
        foreach (var e in LocTable.All)
        {
            Assert.DoesNotMatch(@"\b(1|one) (key|note|enemy|chunk)s?\b", e.English);
            Assert.False(e.English.Contains("(s)"), e.Key);
        }
    }

    [Fact]
    public void FloorCardNamesFollowTheLanguageAndFallBackToEnglish()
    {
        Assert.Equal("The Throat", new Loc().CardName("The Throat"));
        Assert.Equal("An Sgòrnan", new Loc(Language.Gaelic).CardName("The Throat"));
        Assert.Equal("An Sgòrnan (The Throat)", new Loc(Language.Bilingual).CardName("The Throat"));
        Assert.Equal("An Doras Deireannach", new Loc(Language.Gaelic).CardName("The Last Door"));
        Assert.Equal("The Ossuary", new Loc(Language.Gaelic).CardName("The Ossuary"));
        Assert.Equal("Not A Card", new Loc(Language.Gaelic).CardName("Not A Card"));
    }
}

public class LanguageSettingTests
{
    [Fact]
    public void TheDefaultIsEnglish() => Assert.Equal(Language.English, new GameSettings().Language);

    [Theory]
    [InlineData(Language.English)]
    [InlineData(Language.Gaelic)]
    [InlineData(Language.Bilingual)]
    public void ItRoundTripsThroughJson(Language language)
    {
        var s = new GameSettings { Language = language };
        Assert.Equal(language, GameSettings.FromJson(s.ToJson()).Language);
    }

    [Fact]
    public void ItRoundTripsThroughAFileAtATemporaryPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hollowfall-lang-{Guid.NewGuid():N}.json");
        try
        {
            Assert.True(new GameSettings { Language = Language.Bilingual }.TrySave(path));
            Assert.Equal(Language.Bilingual, GameSettings.Load(path).Language);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("{\"language\":\"Klingon\"}")]
    [InlineData("{\"language\":7}")]
    [InlineData("{\"language\":null}")]
    [InlineData("{\"language\":\"99\"}")]
    [InlineData("not json")]
    public void BadValuesFallBackToEnglish(string json) => Assert.Equal(Language.English, GameSettings.FromJson(json).Language);

    [Fact]
    public void ASetterClampsAndResetGoesBackToEnglish()
    {
        var s = new GameSettings { Language = (Language)42 };
        Assert.Equal(Language.English, s.Language);
        s.Language = Language.Gaelic;
        s.ResetToDefaults();
        Assert.Equal(Language.English, s.Language);
    }

    [Fact]
    public void ChoosingTheLanguageChangesWhatTheLocAnswers()
    {
        var settings = new GameSettings();
        var loc = new Loc(settings.Language);
        Assert.Equal("Settings", loc.Tr("menu.settings"));
        settings.Language = Language.Gaelic;
        loc.Language = settings.Language;
        Assert.Equal("Roghainnean", loc.Tr("menu.settings"));
    }
}

public class GaelicNameTests
{
    // An independent statement of the rule, written from the sources rather than from the generator: these nouns are feminine
    // (so the adjective is lenited); these are masculine (so it is not). Loch and tobar are never joined to an adjective.
    private static readonly HashSet<string> Feminine = ["Uamh", "Beinn", "Clach"];
    private static readonly HashSet<string> Masculine = ["Cnoc", "Gleann", "Càrn", "Bothan", "Dùn", "Eas", "Sgùrr", "Allt", "Coire"];
    private static readonly HashSet<string> Unsettled = ["Loch", "Tobar"];

    private static readonly Dictionary<string, string> Lenited = new()
    {
        ["dubh"] = "dhubh",
        ["geal"] = "gheal",
        ["glas"] = "ghlas",
        ["ruadh"] = "ruadh",
        ["fada"] = "fhada",
        ["mòr"] = "mhòr",
        ["beag"] = "bheag",
    };

    [Fact]
    public void EveryNounIsClassifiedAndEveryAdjectiveHasItsSourcedLenitedForm()
    {
        Assert.Equal(Feminine.Count + Masculine.Count + Unsettled.Count, GaelicNames.Nouns.Count);
        foreach (var n in GaelicNames.Nouns)
            Assert.Equal(
                Feminine.Contains(n.Gaelic) ? NounGender.Feminine : Masculine.Contains(n.Gaelic) ? NounGender.Masculine : NounGender.Unsure,
                n.Gender);
        Assert.Equal(Lenited.Count, GaelicNames.Adjectives.Count);
        foreach (var a in GaelicNames.Adjectives) Assert.Equal(Lenited[a.Base], a.Lenited);
    }

    [Fact]
    public void EveryNounAdjectivePairAgrees()
    {
        int pairs = 0;
        foreach (var noun in GaelicNames.Nouns.Where(n => n.Gender != NounGender.Unsure))
            foreach (var adj in GaelicNames.Adjectives.Where(a => !GaelicNames.Blocked.Contains((noun.Gaelic, a.Base))))
            {
                string name = GaelicNames.Join(noun, adj);
                string expectedAdjective = Feminine.Contains(noun.Gaelic) ? Lenited[adj.Base] : adj.Base;
                Assert.Equal($"{noun.Gaelic} {expectedAdjective}", name);
                pairs++;
            }
        Assert.Equal(12 * 7 - 1, pairs);
        Assert.Throws<ArgumentException>(() => GaelicNames.Join(GaelicNames.Nouns.First(n => n.Gaelic == "Loch"), GaelicNames.Adjectives[0]));
    }

    [Fact]
    public void GeneratedNamesAreDeterministicOrderedAndAgreeAcrossManySeeds()
    {
        var seen = new HashSet<string>();
        for (long seed = -50; seed < 2000; seed++)
        {
            string name = GaelicNames.For(seed);
            Assert.Equal(name, GaelicNames.For(seed));
            seen.Add(name);

            var words = name.Split(' ');
            Assert.InRange(words.Length, 1, 2);
            Assert.True(Feminine.Contains(words[0]) || Masculine.Contains(words[0]) || Unsettled.Contains(words[0]), name); // the noun comes first
            if (words.Length == 1) continue;
            Assert.False(Unsettled.Contains(words[0]), name);
            Assert.NotEqual(("Beinn", "dubh"), (words[0], words[1]));
            bool fem = Feminine.Contains(words[0]);
            Assert.Contains(Lenited.Keys, b => (fem ? Lenited[b] : b) == words[1]);
        }
        Assert.True(seen.Count > 40, "names should be varied");
    }

    [Fact]
    public void TheGaelicNameDoesNotTouchTheSeedWordsOrTheEnglishDungeonName()
    {
        for (ulong n = 0; n < 300; n++) Assert.Matches(@"^[a-z]+-[a-z]+-\d{3}$", SeedWords.From(n));
        Assert.StartsWith("The ", StoryPlan.TitleFor(1));
        Assert.Equal(StoryPlan.TitleFor(7), StoryPlan.TitleFor(7));
    }

    [Fact]
    public void DisplayFollowsTheLanguage()
    {
        long seed = 12;
        Assert.Null(GaelicNames.Display(seed, Language.English));
        Assert.Equal(GaelicNames.For(seed), GaelicNames.Display(seed, Language.Gaelic));
        Assert.Equal($"{GaelicNames.For(seed)} ({GaelicNames.Gloss(seed)})", GaelicNames.Display(seed, Language.Bilingual));
        Assert.Equal(GaelicNames.For(seed), new Loc(Language.Gaelic).DungeonName(seed));
        Assert.Null(new Loc().DungeonName(seed));
    }

    [Fact]
    public void TheGlossIsInEnglishWordOrder()
    {
        for (long seed = 0; seed < 200; seed++)
        {
            var (noun, adj) = GaelicNames.PartsFor(seed);
            Assert.Equal(adj == null ? Cap(noun.English) : $"{Cap(adj.English)} {Cap(noun.English)}", GaelicNames.Gloss(seed));
        }
        static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];
    }
}

public class WulverGaelicTests
{
    private static DialogueContext Ctx(Language language, int visits = 0) => new() { Seed = 5, SeedText = "voxel", VisitCount = visits, FloorCount = 3, Language = language };

    private static string FirstText(Language language, int visits = 0) => new DialogueRunner(WulverDialogue.Graph, Ctx(language, visits)).Text;

    [Fact]
    public void InEnglishNoGaelicIsSpoken()
    {
        foreach (var e in WulverGreetings.All)
            Assert.DoesNotContain(e.Gaelic!, FirstText(Language.English));
        Assert.DoesNotContain("Halò", FirstText(Language.English));
    }

    [Theory]
    [InlineData(Language.Gaelic)]
    [InlineData(Language.Bilingual)]
    public void InGaelicAndBilingualHeGreetsInGaelicWithEnglishBeneath(Language language)
    {
        string text = FirstText(language);
        Assert.StartsWith("Halò. Fàilte.\n(Hello. Welcome.)", text);
        Assert.Contains("Ah. ", text);
    }

    [Fact]
    public void HisGaelicLinesAreFiveAndAppearAcrossTheTree()
    {
        var all = WulverDialogue.Graph.Nodes.SelectMany(n => n.Lines).Select(l => l.Text).ToList();
        foreach (var e in WulverGreetings.All)
            Assert.Contains(all, t => t.Contains(e.Gaelic!));
        Assert.Equal(5, WulverGreetings.All.Count);
    }

    [Fact]
    public void ApproachingGreetsInGaelicOnlyInThoseLanguages()
    {
        var spot = new WulverSpot(0, 0, 9, 0.5, 10, 0.5, 0, 0, 0, 50);
        var english = new WulverEncounter(1, "voxel", spot);
        Assert.Equal(WulverDialogue.GreetingFirst, english.Approach(1, 10, 1));
        var gaelic = new WulverEncounter(1, "voxel", spot) { Language = Language.Gaelic };
        string g = gaelic.Approach(1, 10, 1)!;
        Assert.StartsWith("Halò. Fàilte.", g);
        Assert.EndsWith(WulverDialogue.GreetingFirst, g);
    }

    [Fact]
    public void TheTreeStillValidatesAndTheNameStaysInHisFile()
    {
        Assert.Empty(DialogueValidator.Validate(WulverDialogue.Graph));
        foreach (var e in WulverGreetings.All) Assert.DoesNotMatch(@"E[oò]ghann|Ewan", e.English + e.Gaelic);
    }
}

/// <summary>Keeps docs/gaidhlig.md's tables identical to the data. Run with HOLLOWFALL_WRITE_DOCS=1 to rewrite them.</summary>
public class GaelicDocsTests
{
    private const string Begin = "<!-- BEGIN GENERATED TABLES -->";
    private const string End = "<!-- END GENERATED TABLES -->";

    private static string Cell(string? text) => string.IsNullOrEmpty(text) ? "" : text.Replace("|", "\\|").Replace("\n", "\\n");

    public static string Render()
    {
        var sb = new StringBuilder();
        foreach (var (area, entries) in LocTable.Areas)
        {
            var c = LocTable.Counts(entries);
            sb.AppendLine($"### {area}");
            sb.AppendLine();
            sb.AppendLine($"Verified {c.Verified}, Likely {c.Likely}, NeedsReview {c.NeedsReview}, no Gàidhlig yet {c.Untranslated}.");
            sb.AppendLine();
            sb.AppendLine("| Key | English | Gàidhlig | Status | Source | Note |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (var e in entries)
                sb.AppendLine($"| `{e.Key}` | {Cell(e.English)} | {Cell(e.Gaelic) switch { "" => "(English shown)", var g => g }} | {(e.HasGaelic ? e.Status.ToString() : "Untranslated")} | {e.Source} | {Cell(e.Note)} |");
            sb.AppendLine();
        }

        sb.AppendLine("### Place-name elements (Gaelic dungeon names)");
        sb.AppendLine();
        sb.AppendLine("| Gàidhlig | English | Gender (Wiktionary) | Use |");
        sb.AppendLine("| --- | --- | --- | --- |");
        foreach (var n in GaelicNames.Nouns)
            sb.AppendLine($"| {n.Gaelic} | {n.English} | {n.Gender} | {(n.Gender == NounGender.Unsure ? "alone only" : "noun, adjective after it")} |");
        sb.AppendLine();
        sb.AppendLine("| Adjective | Lenited (after a feminine noun) | English |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var a in GaelicNames.Adjectives) sb.AppendLine($"| {a.Base} | {a.Lenited} | {a.English} |");
        sb.AppendLine();
        sb.AppendLine("Pairs never made: " + string.Join(", ", GaelicNames.Blocked.Select(b => $"{b.Noun} + {b.Adjective}")) + ".");
        return sb.ToString().TrimEnd() + "\n";
    }

    [Fact]
    public void TheDocsTablesMatchTheData()
    {
        string path = Path.Combine(RepoFiles.Root(), "docs", "gaidhlig.md");
        string doc = File.ReadAllText(path);
        int a = doc.IndexOf(Begin, StringComparison.Ordinal), b = doc.IndexOf(End, StringComparison.Ordinal);
        Assert.True(a >= 0 && b > a, "docs/gaidhlig.md needs the generated-table markers");
        string expected = doc[..(a + Begin.Length)] + "\n\n" + Render() + "\n" + doc[b..];

        if (Environment.GetEnvironmentVariable("HOLLOWFALL_WRITE_DOCS") == "1")
        {
            File.WriteAllText(path, expected);
            return;
        }
        Assert.True(doc == expected, "docs/gaidhlig.md is out of date: run  HOLLOWFALL_WRITE_DOCS=1 dotnet test --filter GaelicDocsTests  and commit it");
    }
}
