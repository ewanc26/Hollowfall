using Hollowfall.Core.Combat;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Storage;
using Xunit;

namespace Hollowfall.Tests;

public class ProgressionTests
{
    [Fact]
    public void TheCurveStartsAtLevelOneAndRisesEachStep()
    {
        Assert.Equal(0, Progression.ExperienceForLevel(1));
        Assert.Equal(30, Progression.ExperienceForLevel(2));
        Assert.Equal(120, Progression.ExperienceForLevel(3));
        Assert.Equal(1, Progression.LevelFor(0));
        Assert.Equal(1, Progression.LevelFor(29));
        Assert.Equal(2, Progression.LevelFor(30));
        Assert.Equal(2, Progression.LevelFor(119));
        Assert.Equal(3, Progression.LevelFor(120));
    }

    [Fact]
    public void TheLevelStopsAtTheCap()
    {
        Assert.Equal(Progression.MaxLevel, Progression.LevelFor(int.MaxValue));
        Assert.Equal(Progression.MaxLevel, Progression.LevelFor(Progression.ExperienceForLevel(Progression.MaxLevel)));
    }

    [Fact]
    public void EveryLevelIsReachedAtItsOwnThreshold()
    {
        for (int level = 1; level <= Progression.MaxLevel; level++)
            Assert.Equal(level, Progression.LevelFor(Progression.ExperienceForLevel(level)));
    }

    [Fact]
    public void KillsGiveExperienceAndCreaturesDoNot()
    {
        var p = new Progression();
        Assert.Equal(10, p.Award(EnemyKind.Husk));
        Assert.Equal(12, p.Award(EnemyKind.Spitter));
        Assert.Equal(22, p.Experience);
        Assert.Equal(0, p.Award(EnemyKind.Brownie));
        Assert.Equal(0, p.Award(EnemyKind.Selkie));
        Assert.Equal(22, p.Experience);
    }

    [Fact]
    public void OnlyTheHollowDropsCoinAndItStopsAtTheCap()
    {
        var p = new Progression();
        Assert.Equal(3, p.AwardCoin(EnemyKind.Husk));
        Assert.Equal(4, p.AwardCoin(EnemyKind.Spitter));
        Assert.Equal(5, p.AwardCoin(EnemyKind.CuSith));
        Assert.Equal(30, p.AwardCoin(EnemyKind.Warden));
        Assert.Equal(42, p.Coin);
        Assert.Equal(0, p.AwardCoin(EnemyKind.Brownie));
        Assert.Equal(0, p.AwardCoin(EnemyKind.Villager));
        Assert.Equal(0, p.AwardCoin(EnemyKind.Pedlar));
        Assert.Equal(42, p.Coin);

        Assert.Equal(0, p.GainCoin(0));
        Assert.Equal(0, p.GainCoin(-9));
        Assert.Equal(Progression.MaxCoin - 42, p.GainCoin(int.MaxValue));
        Assert.Equal(Progression.MaxCoin, p.Coin);
        Assert.Equal(0, p.GainCoin(1));
    }

    [Fact]
    public void SpendingCoinNeedsEnoughAndNeverGoesBelowZero()
    {
        var p = new Progression();
        p.GainCoin(10);
        Assert.False(p.SpendCoin(11));
        Assert.False(p.SpendCoin(-1));
        Assert.Equal(10, p.Coin);
        Assert.True(p.SpendCoin(10));
        Assert.Equal(0, p.Coin);
    }

    [Fact]
    public void CoinSurvivesASaveAndIsClampedOnTheWayIn()
    {
        var p = new Progression();
        p.GainCoin(123);
        Assert.Equal(123, ProgressionStore.FromJson(ProgressionStore.ToJson(p)).Coin);
        Assert.Equal(Progression.MaxCoin, ProgressionStore.FromJson("{\"coin\":999999}").Coin);
        Assert.Equal(0, ProgressionStore.FromJson("{\"coin\":-4}").Coin);
    }

    [Fact]
    public void GainIgnoresNonPositiveAmountsAndStopsAtTheCap()
    {
        var p = new Progression();
        Assert.Equal(0, p.Gain(0));
        Assert.Equal(0, p.Gain(-50));
        Assert.Equal(Progression.MaxExperience, p.Gain(int.MaxValue));
        Assert.Equal(0, p.Gain(5));
        Assert.Equal(Progression.MaxExperience, p.Experience);
    }

    [Fact]
    public void EachLevelAfterTheFirstEarnsAPoint()
    {
        var p = new Progression();
        Assert.Equal(0, p.PointsAvailable);
        p.Gain(Progression.ExperienceForLevel(3));
        Assert.Equal(2, p.PointsEarned);
        Assert.Equal(2, p.PointsAvailable);
    }

    [Fact]
    public void SpendingNeedsAPointAndIsCountedPerStat()
    {
        var p = new Progression();
        Assert.False(p.Spend(Stat.Vigour));
        p.Gain(Progression.ExperienceForLevel(3));
        Assert.True(p.Spend(Stat.Vigour));
        Assert.True(p.Spend(Stat.Might));
        Assert.False(p.Spend(Stat.Might));
        Assert.Equal(1, p.Vigour);
        Assert.Equal(1, p.Might);
        Assert.Equal(0, p.PointsAvailable);
    }

    [Fact]
    public void TheStatsGiveTheirEffects()
    {
        var p = new Progression();
        p.Gain(Progression.ExperienceForLevel(4));
        p.Spend(Stat.Vigour);
        p.Spend(Stat.Vigour);
        p.Spend(Stat.Might);
        Assert.Equal(2 * Progression.VigourPerPoint, p.MaxHealthBonus);
        Assert.Equal(1 + Progression.MightPerPoint, p.DamageScale, 9);
        Assert.Equal(1, new Progression().DamageScale);
    }

    [Fact]
    public void RestoreClampsAndDropsPointsThatWereNeverEarned()
    {
        var p = new Progression();
        p.Restore(Progression.ExperienceForLevel(3), vigour: 9, might: 9);
        Assert.Equal(2, p.PointsEarned);
        Assert.Equal(2, p.Vigour);
        Assert.Equal(0, p.Might);

        p.Restore(-400, vigour: -3, might: 1);
        Assert.Equal(0, p.Experience);
        Assert.Equal(0, p.Vigour);
        Assert.Equal(0, p.Might);
    }

    [Fact]
    public void ASavedCharacterComesBackAsItWasLeft()
    {
        var p = new Progression();
        p.Gain(500);
        p.Spend(Stat.Vigour);
        var back = ProgressionStore.FromJson(ProgressionStore.ToJson(p));
        Assert.Equal(p.Experience, back.Experience);
        Assert.Equal(p.Level, back.Level);
        Assert.Equal(p.Vigour, back.Vigour);
        Assert.Equal(p.Might, back.Might);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"experience\":\"lots\",\"vigour\":true}")]
    public void DamagedFilesGiveAFreshCharacter(string? json)
    {
        var p = ProgressionStore.FromJson(json);
        Assert.Equal(0, p.Experience);
        Assert.Equal(1, p.Level);
    }

    [Fact]
    public void AFileTooLargeToBeTrustedIsIgnored()
    {
        string json = "{\"experience\":500," + new string(' ', ProgressionStore.MaxFileBytes) + "\"vigour\":0}";
        Assert.Equal(0, ProgressionStore.FromJson(json).Experience);
    }

    [Fact]
    public void TheFileSavesAndLoadsAndAMissingOneIsAFreshCharacter()
    {
        string folder = Path.Combine(Path.GetTempPath(), "hollowfall-progression-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(folder, "progression.json");
            Assert.Equal(0, ProgressionStore.Load(path).Experience);

            var p = new Progression();
            p.Gain(130);
            Assert.True(ProgressionStore.TrySave(p, path));
            Assert.Equal(130, ProgressionStore.Load(path).Experience);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TheProgressionPathComesFromTheDataFolderAndHonoursItsEnvironmentVariable()
    {
        Assert.Equal(Path.Combine("/data/hollowfall", "progression.json"), new DataHome("/data/hollowfall", _ => null).ProgressionPath);
        Assert.Equal("/tmp/p.json", new DataHome("/data/hollowfall", n => n == "HOLLOWFALL_PROGRESSION" ? "/tmp/p.json" : null).ProgressionPath);
    }
}
