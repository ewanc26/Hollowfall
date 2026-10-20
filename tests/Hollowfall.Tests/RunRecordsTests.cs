using Hollowfall.Core.Combat;

namespace Hollowfall.Tests;

public class RunRecordsTests
{
    private static FloorResult Result(OverallGrade grade, int points) => new(Grade.A, Grade.A, Grade.A, 0, points, grade);

    [Fact]
    public void TheFirstResultIsAlwaysABest()
    {
        var r = new RunRecords();
        var (best, previous) = r.Submit("deep", 0, Result(OverallGrade.C, 6), 90);
        Assert.True(best);
        Assert.Null(previous);
        Assert.Equal(new FloorRecord(OverallGrade.C, 6, 90), r.Best("deep", 0));
    }

    [Fact]
    public void OnlyBetterResultsReplaceIt()
    {
        var r = new RunRecords();
        r.Submit("deep", 0, Result(OverallGrade.B, 9), 80);
        Assert.False(r.Submit("deep", 0, Result(OverallGrade.C, 7), 40).IsBest); // worse grade, even though faster
        Assert.False(r.Submit("deep", 0, Result(OverallGrade.B, 8), 40).IsBest); // fewer points
        Assert.False(r.Submit("deep", 0, Result(OverallGrade.B, 9), 80).IsBest); // a tie is not a record
        Assert.True(r.Submit("deep", 0, Result(OverallGrade.B, 9), 70).IsBest);  // same score, faster
        var (better, previous) = r.Submit("deep", 0, Result(OverallGrade.A, 11), 120);
        Assert.True(better);
        Assert.Equal(OverallGrade.B, previous!.Value.Grade);
        Assert.Equal(OverallGrade.A, r.Best("deep", 0)!.Value.Grade);
    }

    [Fact]
    public void SeedsAndFloorsAreSeparate()
    {
        var r = new RunRecords();
        r.Submit("a", 0, Result(OverallGrade.S, 14), 50);
        Assert.Null(r.Best("a", 1));
        Assert.Null(r.Best("b", 0));
        Assert.True(r.Submit("b", 0, Result(OverallGrade.D, 3), 500).IsBest);
    }

    [Fact]
    public void RecordsSurviveTheRoundTripIncludingOddSeeds()
    {
        var r = new RunRecords();
        r.Submit("amber-hollow-914", 0, Result(OverallGrade.P, 15), 61.5);
        r.Submit("amber-hollow-914", 2, Result(OverallGrade.B, 9), 130);
        r.Submit("weird \"seed\" \\ ünï", 1, Result(OverallGrade.S, 14), 99);
        var back = RunRecords.FromJson(r.ToJson());
        Assert.Equal(r.Best("amber-hollow-914", 0), back.Best("amber-hollow-914", 0));
        Assert.Equal(r.Best("amber-hollow-914", 2), back.Best("amber-hollow-914", 2));
        Assert.Equal(r.Best("weird \"seed\" \\ ünï", 1), back.Best("weird \"seed\" \\ ünï", 1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"seeds\": 5}")]
    [InlineData("{\"seeds\": {\"a\": {\"0\": {\"grade\": \"Z\"}, \"x\": {}, \"-3\": {\"grade\": \"S\"}, \"9999\": {\"grade\": \"S\"}}}}")]
    public void DamagedFilesCostOnlyTheBadEntries(string? json)
    {
        var r = RunRecords.FromJson(json);
        Assert.Equal(0, r.SeedCount == 0 ? 0 : r.Best("a", 0) == null ? 0 : 1);
    }

    [Fact]
    public void GoodEntriesSurviveNextToBadOnes()
    {
        var r = RunRecords.FromJson("{\"seeds\": {\"a\": {\"0\": {\"grade\": \"S\", \"points\": 14, \"seconds\": 50}, \"1\": {\"grade\": \"nope\"}}}}");
        Assert.Equal(OverallGrade.S, r.Best("a", 0)!.Value.Grade);
        Assert.Null(r.Best("a", 1));
    }

    [Fact]
    public void OnlyTheMostRecentSeedsAreKept()
    {
        var r = new RunRecords();
        for (int i = 0; i < RunRecords.MaxSeeds + 20; i++) r.Submit($"seed{i}", 0, Result(OverallGrade.C, 5), 10);
        Assert.Equal(RunRecords.MaxSeeds, r.SeedCount);
        Assert.Null(r.Best("seed0", 0));
        Assert.NotNull(r.Best($"seed{RunRecords.MaxSeeds + 19}", 0));
    }

    [Fact]
    public void SavingIsAtomicAndLoadingAMissingFileIsEmpty()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hollowfall-records-{Guid.NewGuid():N}", "records.json");
        try
        {
            Assert.Equal(0, RunRecords.Load(path).SeedCount);
            var r = new RunRecords();
            r.Submit("x", 0, Result(OverallGrade.A, 12), 77);
            Assert.True(r.TrySave(path));
            Assert.False(File.Exists(path + ".tmp"));
            Assert.Equal(OverallGrade.A, RunRecords.Load(path).Best("x", 0)!.Value.Grade);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(path)!, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void BadInputIsRefusedNotStored()
    {
        var r = new RunRecords();
        Assert.False(r.Submit("", 0, Result(OverallGrade.S, 14), 1).IsBest);
        Assert.False(r.Submit("a", -1, Result(OverallGrade.S, 14), 1).IsBest);
        Assert.False(r.Submit("a", RunRecords.MaxFloors, Result(OverallGrade.S, 14), 1).IsBest);
        Assert.Equal(0, r.SeedCount);
        r.Submit("a", 0, Result(OverallGrade.S, 14), double.NaN);
        Assert.Equal(0, r.Best("a", 0)!.Value.Seconds);
    }
}
