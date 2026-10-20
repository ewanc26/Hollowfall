using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Core.Story;

/// <summary>A lore note with its placeholders filled in and its place in the dungeon decided.</summary>
public sealed record StoryNote(string Id, int Floor, int Order, string Title, string Text);

/// <summary>The title card shown when a floor is first entered: "I. The Antechamber" and a line beneath it.</summary>
public sealed record FloorCard(int Floor, string Numeral, string Name, string Epigraph)
{
    public string Title => $"{Numeral}. {Name}";
}

public sealed record EndingCard(string Title, IReadOnlyList<string> Paragraphs);

/// <summary>
/// The story of one dungeon, chosen from the authored <see cref="StoryBook"/> by a seed and a floor count. It is a pure
/// function of those two numbers, built with <see cref="SeededRandom"/> only, so every run of a seed tells the same story
/// and a different seed tells a different one. Each note is used at most once, each floor gets a distinct title, and the
/// notes on a floor come from one voice and read in order. It never throws: for any floor count it gives every floor a card
/// and simply runs short of notes when the book runs out.
/// </summary>
public sealed class StoryPlan
{
    private static readonly string[] Roman =
        ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX"];

    private static readonly string[] Ordinals =
        ["First", "Second", "Third", "Fourth", "Fifth", "Sixth", "Seventh", "Eighth", "Ninth", "Tenth"];

    public const int MaxFloors = 20;
    public const int MaxNotesPerFloor = 3;

    private readonly List<FloorCard> _cards = [];
    private readonly List<List<StoryNote>> _runs = [];
    private readonly Dictionary<string, StoryNote> _byId = [];

    public long Seed { get; }
    public int FloorCount { get; }

    /// <summary>The dungeon's name without its article ("Pale Vault").</summary>
    public string Name { get; }

    /// <summary>The dungeon's name as a title ("The Pale Vault").</summary>
    public string Title => "The " + Name;

    /// <summary>The last Warden, the boss.</summary>
    public string WardenName { get; }

    public IReadOnlyList<FloorCard> Cards => _cards;
    public IReadOnlyList<string> Intro { get; }
    public string BossIntro { get; }
    public EndingCard Ending { get; }

    private StoryPlan(long seed, int floorCount)
    {
        Seed = seed;
        FloorCount = floorCount;
        Name = NameOf(seed);
        WardenName = WardenOf(seed);

        Intro =
        [
            Fill(StoryBook.IntroOpeners[Pick(seed, 0, 0, 805, StoryBook.IntroOpeners.Length)]),
            .. StoryBook.IntroBody.Select(Fill),
        ];
        BossIntro = Fill(StoryBook.BossIntros[Pick(seed, 0, 0, 806, StoryBook.BossIntros.Length)]);
        Ending = new EndingCard(StoryBook.EndingTitle, [.. StoryBook.EndingParagraphs.Select(Fill)]);

        var usedCards = new HashSet<string>();
        var usedNotes = new HashSet<string>();
        for (int floor = 0; floor < floorCount; floor++)
        {
            var band = StoryBook.BandOf(floor, floorCount);
            _cards.Add(ChooseCard(floor, band, usedCards));
            var run = ChooseRun(floor, band, usedNotes);
            _runs.Add(run);
            foreach (var n in run) _byId[n.Id] = n;
        }
    }

    /// <summary>The story for a seed and a dungeon of this many floors (clamped to 1 to <see cref="MaxFloors"/>).</summary>
    public static StoryPlan For(long seed, int floorCount) => new(seed, Math.Clamp(floorCount, 1, MaxFloors));

    /// <summary>The name a seed gives its dungeon, with its article: "The Pale Vault". Needs no floor count.</summary>
    public static string TitleFor(long seed) => "The " + NameOf(seed);

    private static string NameOf(long seed)
    {
        ulong h = SeedHash.Hash(seed, 0, 0, 801);
        string adjective = StoryBook.HollowAdjectives[(int)(h % (ulong)StoryBook.HollowAdjectives.Length)];
        h = SeedHash.Mix(h);
        return $"{adjective} {StoryBook.HollowNouns[(int)(h % (ulong)StoryBook.HollowNouns.Length)]}";
    }

    private static string WardenOf(long seed) =>
        StoryBook.WardenNames[Pick(seed, 0, 0, 802, StoryBook.WardenNames.Length)];

    private static int Pick(long seed, int a, int b, int salt, int count) => new SeededRandom(seed, a, b, salt).Next(count);

    private string Fill(string text) =>
        text.Replace("{hollow}", "the " + Name).Replace("{Hollow}", Title).Replace("{warden}", WardenName);

    // ---- floors ----------------------------------------------------------------------------------

    private FloorCard ChooseCard(int floor, DepthBand band, HashSet<string> used)
    {
        string numeral = Roman[floor];
        var options = StoryBook.Cards.Where(c => c.Band == band && !used.Contains(c.Id)).ToList();
        if (options.Count == 0)
        {
            // More floors than the book has cards for: a plain, still unique, card.
            string ordinal = floor < Ordinals.Length ? Ordinals[floor] : $"No. {floor + 1}";
            return new FloorCard(floor, numeral, $"The {ordinal} Descent", "The Hollow goes on.");
        }
        var card = options[new SeededRandom(Seed, floor, 0, 810).Next(options.Count)];
        used.Add(card.Id);
        return new FloorCard(floor, numeral, Fill(card.Name), Fill(card.Epigraph));
    }

    private List<StoryNote> ChooseRun(int floor, DepthBand band, HashSet<string> used)
    {
        var threads = StoryBook.Notes
            .Where(n => n.Band == band && !used.Contains(n.Id))
            .Select(n => n.Thread)
            .Distinct()
            .ToList();
        if (threads.Count == 0) return [];

        string thread = threads[new SeededRandom(Seed, floor, 0, 811).Next(threads.Count)];
        var run = new List<StoryNote>();
        foreach (var def in StoryBook.Notes.Where(n => n.Band == band && n.Thread == thread && !used.Contains(n.Id)).OrderBy(n => n.Order).Take(MaxNotesPerFloor))
        {
            used.Add(def.Id);
            run.Add(new StoryNote(def.Id, floor, def.Order, Fill(def.Title), Fill(def.Text)));
        }
        return run;
    }

    // ---- reading it ------------------------------------------------------------------------------

    /// <summary>The floor's notes in reading order: a coherent run of up to three. Empty if the book ran out or the floor is out of range.</summary>
    public IReadOnlyList<StoryNote> RunFor(int floor) => floor >= 0 && floor < _runs.Count ? _runs[floor] : [];

    /// <summary>Every note this dungeon can hold, in floor and reading order.</summary>
    public IEnumerable<StoryNote> AllNotes => _runs.SelectMany(r => r);

    public StoryNote? Note(string id) => _byId.GetValueOrDefault(id);

    public FloorCard? CardFor(int floor) => floor >= 0 && floor < _cards.Count ? _cards[floor] : null;

    /// <summary>The line shown when a floor is finished, for its overall grade.</summary>
    public string FloorCompleteLine(OverallGrade grade) =>
        Fill(StoryBook.FloorComplete.TryGetValue(grade, out var line) ? line : StoryBook.FloorComplete[OverallGrade.D]);

    /// <summary>
    /// The line shown on dying. It starts at a seed-chosen place in the set and moves on with each death (<paramref name="deaths"/>
    /// counts from 0), so dying twice in a row never repeats a line.
    /// </summary>
    public string DeathLine(int floor, int deaths)
    {
        int n = StoryBook.DeathLines.Length;
        int start = new SeededRandom(Seed, Math.Max(floor, 0), 0, 813).Next(n);
        return Fill(StoryBook.DeathLines[((start + Math.Max(deaths, 0)) % n)]);
    }

    /// <summary>Whether a floor is the boss floor, where the boss intro and the ending belong.</summary>
    public bool IsBossFloor(int floor) => floor == FloorCount - 1;
}
