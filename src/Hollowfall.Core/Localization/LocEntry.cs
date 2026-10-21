namespace Hollowfall.Core.Localization;

/// <summary>
/// How far a Gàidhlig string has been checked. Nothing here has been reviewed by a native speaker yet:
/// <see cref="Verified"/> means a source says exactly this, <see cref="Likely"/> means it is put together from words and rules a source
/// gives, and <see cref="NeedsReview"/> means a speaker must decide before it is shown (it is held back by default).
/// </summary>
public enum ReviewStatus
{
    Verified,
    Likely,
    NeedsReview,
}

/// <summary>
/// One interface string: its key, the English text, the Gàidhlig (null when there is none we can stand behind, so the English is
/// shown), how sure we are, and where the Gàidhlig came from (ids in <see cref="LocSources"/>, separated by spaces). Placeholders are
/// <c>{0}</c>, <c>{1}</c>… and must match in both languages.
/// </summary>
public sealed record LocEntry(string Key, string English, string? Gaelic, ReviewStatus Status, string Source, string Note = "")
{
    /// <summary>Whether there is Gàidhlig for this key at all (reviewed or not).</summary>
    public bool HasGaelic => Gaelic != null;
}

/// <summary>The sources Gàidhlig is checked against. Every entry with Gàidhlig names one or more of these ids.</summary>
public static class LocSources
{
    public static readonly IReadOnlyDictionary<string, (string Name, string Url)> All = new Dictionary<string, (string, string)>
    {
        ["WK"] = ("Wiktionary, Scottish Gaelic entries (spelling, gender, meanings, mutations)", "https://en.wiktionary.org/wiki/Category:Scottish_Gaelic_lemmas"),
        ["FF"] = ("Firefox in Gàidhlig (Mozilla l10n, gd): software wording such as Fàg and Roghainnean", "https://github.com/mozilla-l10n/firefox-l10n/tree/main/gd"),
        ["LG"] = ("LearnGaelic, Little by Little, unit 7: the weather ('Tha i fuar.')", "https://learngaelic.net/littlebylittle/unit07/index.jsp"),
        ["GW"] = ("Scottish Gaelic Grammar Wiki (after Fisher 2004): the article and lenition after feminine nouns", "https://gaelicgrammar.org/~gaelic/mediawiki/index.php/Article"),
        ["GDW"] = ("Vicipeid na Gàidhlig, the Gaelic Wikipedia (Bean-nighe, Gruagach, Each-uisge)", "https://gd.wikipedia.org"),
        ["TAD"] = ("Tobar an Dualchais: recording titled 'Each-uisge ann an Loch Fada'", "https://www.tobarandualchais.co.uk/track/5824"),
        ["DRU"] = ("Drupal Colossal Menu translation, gd: 'Menu' is 'Clàr-taice'", "https://ftp.drupal.org/files/translations/all/colossal_menu/colossal_menu-3.0.0.gd.po"),
    };
}
