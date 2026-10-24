using Hollowfall.Core.Combat;
using Hollowfall.Core.Replay;
using Xunit;

namespace Hollowfall.Tests;

public class DailyBoardTests
{
    private static readonly ReplayFrame[] Frames = [new(102400, 20480, -51200, 0, 1659, 0, 1000, 0, 32767, 0, 0)];

    private static RunReplay Run(string seed, int floor, OverallGrade grade, int points, double seconds, int simVersion = RunReplay.SimVersion) =>
        new(seed, 0, 0, floor, 0, new FloorRecord(grade, points, seconds), Frames, simVersion);

    private const string Day = "daily-2026-10-21";

    [Fact]
    public void OnlyDailySeedsAreOnTheBoard()
    {
        var replays = new[] { Run("amber-hollow-914", 0, OverallGrade.S, 99, 10) };
        Assert.Empty(DailyBoard.Build(replays, _ => true));
    }

    [Fact]
    public void TheBestRunOfAFloorWins()
    {
        var replays = new[]
        {
            Run(Day, 0, OverallGrade.B, 40, 60),
            Run(Day, 0, OverallGrade.A, 10, 90),
            Run(Day, 0, OverallGrade.A, 25, 120),
            Run(Day, 0, OverallGrade.A, 25, 80),
        };
        var entry = Assert.Single(DailyBoard.Build(replays, _ => true));
        Assert.Equal(OverallGrade.A, entry.Result.Grade);
        Assert.Equal(25, entry.Result.Points);
        Assert.Equal(80, entry.Result.Seconds);
    }

    [Fact]
    public void ARunThatDoesNotVerifyNeverAppears()
    {
        var best = Run(Day, 0, OverallGrade.S, 99, 30);
        var second = Run(Day, 0, OverallGrade.C, 5, 200);
        var entry = Assert.Single(DailyBoard.Build([best, second], r => r != best));
        Assert.Equal(OverallGrade.C, entry.Result.Grade);
    }

    [Fact]
    public void ARunFromAnotherSimVersionIsNotVerifiable()
    {
        var old = Run(Day, 0, OverallGrade.S, 99, 30, simVersion: RunReplay.SimVersion - 1);
        var current = Run(Day, 0, OverallGrade.D, 1, 300);
        var entry = Assert.Single(DailyBoard.Build([old, current], _ => true));
        Assert.Equal(OverallGrade.D, entry.Result.Grade);
    }

    [Fact]
    public void EachFloorKeepsItsOwnBest()
    {
        var replays = new[] { Run(Day, 0, OverallGrade.B, 1, 1), Run(Day, 1, OverallGrade.S, 2, 2), Run("daily-2026-10-22", 0, OverallGrade.A, 3, 3) };
        var board = DailyBoard.Build(replays, _ => true);
        Assert.Equal(3, board.Count);
        Assert.Equal(Day, board[0].Seed);
        Assert.Equal(0, board[0].Floor);
        Assert.Equal(1, board[1].Floor);
        Assert.Equal("daily-2026-10-22", board[2].Seed);
    }

    [Fact]
    public void VerificationRunsOnlyOnCandidatesAndStopsAtTheFirstThatPasses()
    {
        var best = Run(Day, 0, OverallGrade.S, 99, 30);
        var mid = Run(Day, 0, OverallGrade.A, 10, 30);
        var low = Run(Day, 0, OverallGrade.C, 1, 30);
        var checkedRuns = new List<RunReplay>();
        DailyBoard.Build([low, best, mid], r => { checkedRuns.Add(r); return r == mid; });
        Assert.Equal([best, mid], checkedRuns);
    }
}
