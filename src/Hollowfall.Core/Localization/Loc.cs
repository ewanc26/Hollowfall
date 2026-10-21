using System.Globalization;
using System.Text.RegularExpressions;

namespace Hollowfall.Core.Localization;

/// <summary>Every string table in one place, with lookups and the counts the docs and the tests report.</summary>
public static class LocTable
{
    /// <summary>The areas, in the order docs/gaidhlig.md lists them.</summary>
    public static readonly IReadOnlyList<(string Area, IReadOnlyList<LocEntry> Entries)> Areas =
    [
        ("Interface", InterfaceStrings.All),
        ("Story", StoryStrings.All),
        ("Creature names", CreatureNames.All),
        ("Wulver greetings", WulverGreetings.All),
    ];

    public static IReadOnlyList<LocEntry> All { get; } = [.. Areas.SelectMany(a => a.Entries)];

    private static readonly Dictionary<string, LocEntry> ByKey = Index();

    private static Dictionary<string, LocEntry> Index()
    {
        var map = new Dictionary<string, LocEntry>(StringComparer.Ordinal);
        foreach (var e in All)
            if (!map.TryAdd(e.Key, e)) throw new InvalidOperationException($"duplicate localisation key {e.Key}");
        return map;
    }

    public static LocEntry? Find(string key) => ByKey.GetValueOrDefault(key);

    public static bool Has(string key) => ByKey.ContainsKey(key);

    /// <summary>How many entries are in each review state; Untranslated is the entries with no Gàidhlig at all.</summary>
    public static (int Verified, int Likely, int NeedsReview, int Untranslated) Counts(IEnumerable<LocEntry>? entries = null)
    {
        int v = 0, l = 0, n = 0, u = 0;
        foreach (var e in entries ?? All)
        {
            if (!e.HasGaelic) u++;
            else if (e.Status == ReviewStatus.Verified) v++;
            else if (e.Status == ReviewStatus.Likely) l++;
            else n++;
        }
        return (v, l, n, u);
    }

    /// <summary>The placeholder numbers a string uses, e.g. {0} and {1}.</summary>
    public static IReadOnlySet<int> Placeholders(string text) =>
        Regex.Matches(text, @"\{(\d+)\}").Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToHashSet();
}

/// <summary>
/// Looks up interface text for a language. It holds only its own language (no global state, so tests and screens can each have one),
/// and tells listeners when the language changes so screens can rebuild at once.
/// <list type="bullet">
/// <item>English shows the English text. Gàidhlig shows the Gàidhlig where there is some that is fit to show, else the English.</item>
/// <item>Bilingual shows both: "Gàidhlig (English)" for short single-line text, and the English on a second line for longer text.</item>
/// <item>A key with no entry at all returns the key itself and is recorded in <see cref="MissingKeys"/>.</item>
/// <item>A Gàidhlig string marked NeedsReview is held back (English is shown) unless <see cref="ShowNeedsReview"/> is set.</item>
/// <item>Text never depends on a count: numbers sit beside labels ("Keys 2"), so no plural or mutation is needed.</item>
/// </list>
/// </summary>
public sealed class Loc
{
    /// <summary>Text longer than this, or with a line break, puts the English on its own line in Bilingual mode.</summary>
    public const int InlineLimit = 30;

    private readonly HashSet<string> _missing = [];
    private Language _language;

    public Loc(Language language = Language.English) => _language = Languages.Clamp(language);

    /// <summary>Whether strings still marked NeedsReview are shown. Off for players; a reviewer may turn it on.</summary>
    public bool ShowNeedsReview { get; set; }

    public Language Language
    {
        get => _language;
        set
        {
            value = Languages.Clamp(value);
            if (value == _language) return;
            _language = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Raised after the language changes.</summary>
    public event Action? Changed;

    /// <summary>Keys that were asked for and have no entry, so the game can show the key and a test can list them.</summary>
    public IReadOnlyCollection<string> MissingKeys => _missing;

    /// <summary>The text for a key in the current language, with {0}, {1}… filled from <paramref name="args"/>.</summary>
    public string Tr(string key, params object[] args)
    {
        if (LocTable.Find(key) is not { } entry)
        {
            _missing.Add(key);
            return key;
        }
        string english = Fill(entry.English, args);
        string? gaelic = Shown(entry) is { } g ? Fill(g, args) : null;
        return Compose(english, gaelic);
    }

    /// <summary>The English text of a key, whatever the language.</summary>
    public string English(string key, params object[] args)
    {
        if (LocTable.Find(key) is { } entry) return Fill(entry.English, args);
        _missing.Add(key);
        return key;
    }

    /// <summary>The Gàidhlig for a key if there is some fit to show, whatever the language; otherwise null.</summary>
    public string? GaelicOf(string key, params object[] args) =>
        LocTable.Find(key) is { } entry && Shown(entry) is { } g ? Fill(g, args) : null;

    /// <summary>The name of a floor card in the current language ("III. An Sgòrnan" is built from this).</summary>
    public string CardName(string englishName) => LocTable.Has(StoryStrings.CardKey(englishName)) ? Tr(StoryStrings.CardKey(englishName)) : englishName;

    /// <summary>
    /// What to show under the title for a dungeon: the Gàidhlig name (with its English meaning in Bilingual), or null in English.
    /// </summary>
    public string? DungeonName(long seed) => GaelicNames.Display(seed, _language);

    private string? Shown(LocEntry entry)
    {
        if (!Languages.ShowsGaelic(_language) || entry.Gaelic == null) return null;
        return entry.Status == ReviewStatus.NeedsReview && !ShowNeedsReview ? null : entry.Gaelic;
    }

    private string Compose(string english, string? gaelic)
    {
        if (gaelic == null || _language == Language.English) return english;
        if (_language == Language.Gaelic || gaelic == english) return gaelic;
        bool inline = !gaelic.Contains('\n') && !english.Contains('\n') && gaelic.Length + english.Length <= InlineLimit;
        return inline ? $"{gaelic} ({english})" : $"{gaelic}\n{english}";
    }

    private static string Fill(string text, object[] args)
    {
        if (args.Length == 0) return text;
        try { return string.Format(CultureInfo.InvariantCulture, text, args); }
        catch (FormatException) { return text; }
    }
}
