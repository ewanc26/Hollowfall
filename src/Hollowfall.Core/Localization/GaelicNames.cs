using Hollowfall.Core.Dungeon;

namespace Hollowfall.Core.Localization;

public enum NounGender
{
    Masculine,
    Feminine,

    /// <summary>The sources disagree or it varies by dialect, so no adjective is ever joined to it.</summary>
    Unsure,
}

/// <summary>A place-name noun with the gender a source gives and what it means.</summary>
public sealed record PlaceNoun(string Gaelic, NounGender Gender, string English);

/// <summary>An adjective, its lenited form as a source gives it, and what it means.</summary>
public sealed record PlaceAdjective(string Base, string Lenited, string English);

/// <summary>
/// The Gàidhlig name of a dungeon, shown under the title in Gàidhlig and Bilingual modes. It is a SEPARATE flavour name chosen from the
/// seed (the English dungeon name and the seed words such as <c>amber-hollow-914</c> are unchanged), made the way real place names are:
/// a noun, then an adjective after it (<c>Cnoc Dubh</c>, black hill), or a noun alone (<c>Dùn</c>). No article is used, which keeps out
/// every article rule. After a feminine noun the adjective is lenited (<c>Beinn Mhòr</c>, <c>Clach Ghlas</c>); the lenited forms are
/// stored as data from the sources rather than computed, and a noun whose gender is not settled (loch, tobar) is only ever used alone.
/// The one pair left out is beinn + dubh, where whether the d lenites after the final n was not confirmed in a source.
/// Every noun and adjective, and the rule, are cited in docs/gaidhlig.md.
/// </summary>
public static class GaelicNames
{
    public static readonly IReadOnlyList<PlaceNoun> Nouns =
    [
        new("Uamh", NounGender.Feminine, "cave"),
        new("Cnoc", NounGender.Masculine, "hill"),
        new("Gleann", NounGender.Masculine, "glen"),
        new("Beinn", NounGender.Feminine, "mountain"),
        new("Loch", NounGender.Unsure, "loch"),
        new("Càrn", NounGender.Masculine, "cairn"),
        new("Bothan", NounGender.Masculine, "bothy"),
        new("Clach", NounGender.Feminine, "stone"),
        new("Dùn", NounGender.Masculine, "fort"),
        new("Tobar", NounGender.Unsure, "well"),
        new("Eas", NounGender.Masculine, "waterfall"),
        new("Sgùrr", NounGender.Masculine, "peak"),
        new("Allt", NounGender.Masculine, "burn"),
        new("Coire", NounGender.Masculine, "corrie"),
    ];

    public static readonly IReadOnlyList<PlaceAdjective> Adjectives =
    [
        new("dubh", "dhubh", "black"),
        new("geal", "gheal", "white"),
        new("glas", "ghlas", "grey"),
        new("ruadh", "ruadh", "red"),
        new("fada", "fhada", "long"),
        new("mòr", "mhòr", "big"),
        new("beag", "bheag", "small"),
    ];

    /// <summary>Pairs that are never made (noun, adjective), where the mutation could not be confirmed.</summary>
    public static readonly IReadOnlyList<(string Noun, string Adjective)> Blocked = [("Beinn", "dubh")];

    private const int Salt = 829;

    /// <summary>What the name is made of: the noun, and the adjective if there is one.</summary>
    public static (PlaceNoun Noun, PlaceAdjective? Adjective) PartsFor(long seed)
    {
        var rng = new SeededRandom(seed, 0, 0, Salt);
        var noun = Nouns[rng.Next(Nouns.Count)];
        bool alone = rng.Next(5) == 0;
        var options = Adjectives.Where(a => !Blocked.Contains((noun.Gaelic, a.Base))).ToList();
        var adjective = options[rng.Next(options.Count)];
        if (alone || noun.Gender == NounGender.Unsure) return (noun, null);
        return (noun, adjective);
    }

    /// <summary>The Gàidhlig name for a seed: "Cnoc Dubh", "Beinn Mhòr", "Dùn".</summary>
    public static string For(long seed)
    {
        var (noun, adjective) = PartsFor(seed);
        return adjective == null ? noun.Gaelic : Join(noun, adjective);
    }

    /// <summary>A noun with an adjective after it, lenited if the noun is feminine. Never called for a noun of unsure gender.</summary>
    public static string Join(PlaceNoun noun, PlaceAdjective adjective)
    {
        if (noun.Gender == NounGender.Unsure) throw new ArgumentException("a noun of unsure gender takes no adjective", nameof(noun));
        return noun.Gaelic + " " + (noun.Gender == NounGender.Feminine ? adjective.Lenited : adjective.Base);
    }

    /// <summary>The English meaning of the same name: "black hill" is "Black Hill".</summary>
    public static string Gloss(long seed)
    {
        var (noun, adjective) = PartsFor(seed);
        string text = adjective == null ? noun.English : adjective.English + " " + noun.English;
        return string.Join(' ', text.Split(' ').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    /// <summary>What to show under the title for a language: nothing in English, the name in Gàidhlig, or the name with its meaning.</summary>
    public static string? Display(long seed, Language language) => language switch
    {
        Language.Gaelic => For(seed),
        Language.Bilingual => $"{For(seed)} ({Gloss(seed)})",
        _ => null,
    };
}
