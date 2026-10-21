using Hollowfall.Core.Localization;

namespace Hollowfall.Tests;

public class PoFilesTests
{
    [Fact]
    public void EveryKeyIsAMessageContextWithItsEnglish()
    {
        string po = PoFiles.Export(LocTable.All, "gd");

        foreach (var e in LocTable.All)
            Assert.Contains($"msgctxt \"{e.Key}\"", po);
    }

    [Fact]
    public void ImportGivesBackEveryGaelicTranslation()
    {
        var back = PoFiles.Import(PoFiles.Export(LocTable.All, "gd"));

        foreach (var e in LocTable.All.Where(e => e.HasGaelic))
            Assert.Equal(e.Gaelic, back[e.Key]);
        foreach (var e in LocTable.All.Where(e => !e.HasGaelic))
            Assert.False(back.ContainsKey(e.Key));
    }

    [Fact]
    public void NeedsReviewEntriesAreFlaggedFuzzy()
    {
        var entries = new[] { new LocEntry("t.review", "Hello", "Halò", ReviewStatus.NeedsReview, "WK") };
        string po = PoFiles.Export(entries, "gd");

        Assert.Contains("#, fuzzy", po);
    }

    [Fact]
    public void TranslationsKeepTheirPlaceholders()
    {
        foreach (var e in LocTable.All.Where(e => e.HasGaelic))
            Assert.Equal(LocTable.Placeholders(e.English), LocTable.Placeholders(e.Gaelic!));
    }

    [Fact]
    public void AMalformedFileIsRefused()
    {
        Assert.Throws<FormatException>(() => PoFiles.Import("msgid \"unterminated"));
    }
}
