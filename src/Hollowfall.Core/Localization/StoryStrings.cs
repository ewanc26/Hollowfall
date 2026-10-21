using Hollowfall.Core.Story;

namespace Hollowfall.Core.Localization;

/// <summary>
/// The few story lines that have Gàidhlig: two floor-card names and a short line shown with a death. Everything else the story says
/// (epigraphs, notes, the opening, boss, floor-complete and death lines) stays in English in <see cref="StoryBook"/>, which is plain
/// ASCII by design; docs/gaidhlig.md lists what is and is not translated. Also the names of the creatures the game borrows.
/// </summary>
public static class StoryStrings
{
    /// <summary>The key for a floor card's name: "The Throat" is <c>card.the-throat</c>.</summary>
    public static string CardKey(string englishName)
    {
        var sb = new System.Text.StringBuilder("card.");
        bool gap = false;
        foreach (char c in englishName.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                if (gap && sb.Length > 5) sb.Append('-');
                gap = false;
                sb.Append(c);
            }
            else if (c != '\'') gap = true;
        }
        return sb.ToString();
    }

    public static IReadOnlyList<LocEntry> All { get; } = Build();

    private static List<LocEntry> Build()
    {
        var gaelicCards = new Dictionary<string, (string Gaelic, string Note)>
        {
            ["The Throat"] = ("An Sgòrnan", "sgòrnan is a masculine noun, throat (WK); a masculine noun takes 'an' before s+g (GW)"),
            ["The Last Door"] = ("An Doras Deireannach", "doras is a masculine noun, door (WK); deireannach is the adjective last or final (WK); the adjective follows the noun, unlenited after a masculine noun (GW)"),
        };

        var list = new List<LocEntry>();
        foreach (var card in StoryBook.Cards)
        {
            string key = CardKey(card.Name);
            list.Add(gaelicCards.TryGetValue(card.Name, out var g)
                ? new LocEntry(key, card.Name, g.Gaelic, ReviewStatus.Likely, "WK GW", g.Note)
                : new LocEntry(key, card.Name, null, ReviewStatus.NeedsReview, "", "no verified Gàidhlig; English is shown"));
        }

        list.Add(new("story.deathHead", "Try again.", "Feuch a-rithist.", ReviewStatus.Likely, "WK", "feuch, try (the imperative) + a-rithist, again; shown above the English death line, which is not translated"));
        return list;
    }
}

/// <summary>
/// The Gàidhlig names of the creatures docs/mythology.md describes. The game does not show these names on screen yet; they are here so
/// a speaker can review them with the rest and so a later feature can use them.
/// </summary>
public static class CreatureNames
{
    public static IReadOnlyList<LocEntry> All { get; } =
    [
        new("name.eachUisge", "water-horse (each-uisge)", "each-uisge", ReviewStatus.Verified, "GDW TAD", "the Gaelic Wikipedia has a page titled Each-uisge and Tobar an Dualchais titles a recording 'Each-uisge ann an Loch Fada'"),
        new("name.beanNighe", "washerwoman at the ford (bean nighe)", "bean-nighe", ReviewStatus.Verified, "GDW WK", "a feminine noun; the Gaelic Wikipedia page is titled Bean-nighe"),
        new("name.gruagach", "brownie, household or cattle spirit (gruagach)", "gruagach", ReviewStatus.Likely, "GDW WK", "the Gaelic Wikipedia page Gruagach describes a spirit of the cattle that is given milk; Wiktionary's Scottish Gaelic entry glosses only 'maiden', and 'brownie' is given for the Irish word, so the brownie sense is reasoned"),
        new("name.ron", "seal (ròn)", "ròn", ReviewStatus.Verified, "WK", "a masculine noun, seal (the animal); this is NOT a claim about selkies or seal-folk, for which no Gàidhlig was verified"),
        new("name.cailleach", "the winter hag (the Cailleach)", "Cailleach", ReviewStatus.Verified, "WK", "cailleach, a feminine noun: old woman, hag, crone"),
        new("name.brownie", "brownie (a brùnaidh?)", null, ReviewStatus.NeedsReview, "", "the form brùnaidh was not found in any source checked; needs a speaker"),
    ];
}

/// <summary>
/// Short greetings the Wulver says in Gàidhlig (or Bilingual) mode, each with its English beneath. They sit in the Gàidhlig tables so a
/// speaker can review them with the rest; his dialogue file only places them.
/// </summary>
public static class WulverGreetings
{
    public static readonly LocEntry Hello = new("wulver.hello", "Hello.", "Halò.", ReviewStatus.Verified, "WK", "an interjection, hello (borrowed from English)");
    public static readonly LocEntry Welcome = new("wulver.welcome", "Welcome.", "Fàilte.", ReviewStatus.Verified, "WK", "fàilte, welcome");
    public static readonly LocEntry Goodbye = new("wulver.farewell", "Goodbye.", "Mar sin leat.", ReviewStatus.Verified, "WK", "an informal farewell to one person");
    public static readonly LocEntry Thanks = new("wulver.thanks", "Thank you.", "Mòran taing.", ReviewStatus.Verified, "WK FF", "many thanks; Firefox gd also uses 'Mòran taing'");
    public static readonly LocEntry Weather = new("wulver.weather", "It is cold.", "Tha i fuar.", ReviewStatus.Verified, "LG", "LearnGaelic's Little by Little unit 7 prints 'Tha i fuar.' (It's cold.)");

    public static IReadOnlyList<LocEntry> All { get; } = [Hello, Welcome, Goodbye, Thanks, Weather];

    /// <summary>The line as he says it: Gàidhlig, and the English beneath it in brackets.</summary>
    public static string Line(params LocEntry[] entries) =>
        $"{string.Join(' ', entries.Select(e => e.Gaelic))}\n({string.Join(' ', entries.Select(e => e.English))})";
}
