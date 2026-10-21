namespace Hollowfall.Core.Localization;

/// <summary>The language the interface is shown in.</summary>
public enum Language
{
    /// <summary>English only. The default, and the fallback for any string that has no Gaelic.</summary>
    English,

    /// <summary>Scottish Gaelic (Gàidhlig). Strings with no reviewed Gaelic stay in English.</summary>
    Gaelic,

    /// <summary>Gàidhlig with the English beside it (in brackets) where it fits, or beneath it where it does not.</summary>
    Bilingual,
}

/// <summary>Names and parsing for <see cref="Language"/>. Each language is named in itself, so a player can find theirs.</summary>
public static class Languages
{
    /// <summary>The name of a language in that language (or, for Bilingual, in Gàidhlig): "English", "Gàidhlig", "Gàidhlig agus Beurla".</summary>
    public static string NativeName(Language language) => language switch
    {
        Language.Gaelic => "Gàidhlig",
        Language.Bilingual => "Gàidhlig agus Beurla",
        _ => "English",
    };

    /// <summary>A value from outside (a saved file) made safe: anything undefined is English.</summary>
    public static Language Clamp(Language language) => Enum.IsDefined(language) ? language : Language.English;

    public static Language Parse(string? text) =>
        Enum.TryParse<Language>(text, ignoreCase: true, out var l) && Enum.IsDefined(l) ? l : Language.English;

    /// <summary>Whether Gàidhlig is shown at all in this language.</summary>
    public static bool ShowsGaelic(Language language) => language is Language.Gaelic or Language.Bilingual;
}
