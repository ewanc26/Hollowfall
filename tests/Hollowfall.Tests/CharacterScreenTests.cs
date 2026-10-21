using Hollowfall.Core.Localization;
using Hollowfall.Core.Progression;
using Xunit;

namespace Hollowfall.Tests;

/// <summary>The Character screen reads its words from the string table; these keys must exist and fill in properly.</summary>
public class CharacterScreenTests
{
    private static readonly string[] Keys =
    [
        "pause.character", "character.title", "character.none", "character.level", "character.nextLevel", "character.maxLevel",
        "character.points", "character.vigour", "character.vigourHint", "character.might", "character.mightHint", "character.nextRun",
    ];

    [Fact]
    public void EveryKeyTheScreenUsesIsInTheTable()
    {
        Assert.DoesNotContain(Keys, key => !LocTable.Has(key));
    }

    [Fact]
    public void TheLinesFillInInEnglish()
    {
        var loc = new Loc(Language.English);
        Assert.Equal("Level 3", loc.Tr("character.level", 3));
        Assert.Equal("Points to spend: 2", loc.Tr("character.points", 2));
        Assert.Equal("Vigour 1", loc.Tr("character.vigour", 1));
        Assert.Equal("12 of 120 experience to the next level", loc.Tr("character.nextLevel", 12, Progression.ExperienceForLevel(3)));
    }
}
