namespace Hollowfall.Core.Localization;

public enum Language
{
    /// <summary>English only. The default, and the fallback for any string that has no Gàidhlig or Gaeilge.</summary>
    English,

    /// <summary>Scottish Gaelic (Gàidhlig). Strings with no reviewed Gaelic stay in English.</summary>
    Gaelic,

    /// <summary>Gàidhlig with the English beside it (in brackets) where it fits, or beneath it where it does not.</summary>
    Bilingual,

    /// <summary>Irish (Gaeilge). Strings with no reviewed Irish stay in English.</summary>
    Irish,

    /// <summary>Gaeilge with the English beside it (in brackets) where it fits, or beneath it where it does not.</summary>
    IrishBilingual,
}

public static class Languages
{
    /// <summary>The name of a language in that language (or, for the bilingual modes, in Gàidhlig or Gaeilge).</summary>
    public static string NativeName(Language language) => language switch
    {
        Language.Gaelic => "Gàidhlig",
        Language.Bilingual => "Gàidhlig agus Beurla",
        Language.Irish => "Gaeilge",
        Language.IrishBilingual => "Gaeilge agus Béarla",
        _ => "English",
    };

    /// <summary>A value from outside (a saved file) made safe: anything undefined is English.</summary>
    public static Language Clamp(Language language) => Enum.IsDefined(language) ? language : Language.English;

    public static Language Parse(string? text) =>
        Enum.TryParse<Language>(text, ignoreCase: true, out var l) && Enum.IsDefined(l) ? l : Language.English;

    /// <summary>Whether Gàidhlig is shown at all in this language.</summary>
    public static bool ShowsGaelic(Language language) => language is Language.Gaelic or Language.Bilingual;

    /// <summary>Whether Gaeilge is shown at all in this language.</summary>
    public static bool ShowsIrish(Language language) => language is Language.Irish or Language.IrishBilingual;
}
