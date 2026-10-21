using Karambolo.PO;

namespace Hollowfall.Core.Localization;

/// <summary>
/// Writes the string tables as a gettext PO catalogue for translators (Poedit and similar tools), and reads one back. The key is the
/// message context and the English is the message id, so a PO file lists each interface string once with its status and sources.
/// Entries that still need a speaker are flagged fuzzy, which those tools show as needing attention.
/// </summary>
public static class PoFiles
{
    public static string Export(IEnumerable<LocEntry> entries, string language)
    {
        var catalog = new POCatalog { Language = language, Encoding = "UTF-8" };
        foreach (var e in entries)
        {
            var comments = new List<POComment> { new POTranslatorComment { Text = Describe(e) } };
            if (e.Status == ReviewStatus.NeedsReview && e.HasGaelic)
                comments.Add(new POFlagsComment { Flags = new HashSet<string> { "fuzzy" } });
            catalog.Add(new POSingularEntry(new POKey(e.English, null, e.Key))
            {
                Translation = e.Gaelic ?? "",
                Comments = comments.ToArray(),
            });
        }

        var text = new StringWriter();
        // The text is UTF-8 (Gàidhlig accents), which the generator's single-byte check would refuse; the caller writes it as UTF-8.
        new POGenerator(new POGeneratorSettings { IgnoreEncoding = true }).Generate(text, catalog);
        return text.ToString();
    }

    /// <summary>The translations in a PO catalogue, by key. Blank translations are left out.</summary>
    public static IReadOnlyDictionary<string, string> Import(string po)
    {
        var result = new POParser().Parse(po);
        if (!result.Success) throw new FormatException("the PO file could not be read");

        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in result.Catalog)
            if (entry is POSingularEntry { Key.ContextId: { } key, Translation: { Length: > 0 } translation })
                found[key] = translation;
        return found;
    }

    private static string Describe(LocEntry e)
    {
        string sources = e.Source.Length > 0 ? e.Source : "none";
        string note = e.Note.Length > 0 ? $" {e.Note}" : "";
        return $"Key {e.Key}. Status {e.Status}. Sources {sources}.{note}";
    }
}
