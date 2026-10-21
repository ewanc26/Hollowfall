using Hollowfall.Core.Terrain;
using Xunit;

namespace Hollowfall.Tests;

public class DailySeedTests
{
    [Fact]
    public void TheSeedNamesTheDayInAFixedFormat()
    {
        Assert.Equal("daily-2026-10-21", DailySeed.For(new DateOnly(2026, 10, 21)));
        Assert.Equal("daily-2026-01-05", DailySeed.For(new DateOnly(2026, 1, 5)));
    }

    [Fact]
    public void EveryoneOnTheSameDayGetsTheSameSeedAndNeighbouringDaysDiffer()
    {
        var day = new DateOnly(2026, 10, 21);
        Assert.Equal(DailySeed.For(day), DailySeed.For(day));
        Assert.NotEqual(DailySeed.For(day), DailySeed.For(day.AddDays(1)));
        Assert.NotEqual(DailySeed.For(day), DailySeed.For(day.AddYears(1)));
    }

    [Fact]
    public void TheSeedSurvivesCleaningUnchanged()
    {
        var day = new DateOnly(2026, 10, 21);
        Assert.Equal(DailySeed.For(day), SeedWords.Clean(DailySeed.For(day)));
    }

    [Fact]
    public void TodayIsTheUtcDay()
    {
        Assert.Equal("daily-2026-10-21", DailySeed.Today(new DateTime(2026, 10, 21, 23, 59, 0, DateTimeKind.Utc)));
        Assert.Equal("daily-2026-10-22", DailySeed.Today(new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void TheDailyDungeonIsTheSameDungeonEveryTime()
    {
        long a = TerrainGenerator.SeedFromString(DailySeed.For(new DateOnly(2026, 10, 21)));
        long b = TerrainGenerator.SeedFromString(DailySeed.Today(new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(a, b);
    }
}
