namespace VoxelGame.Core.Combat;

/// <summary>Letter grades worth D=1 to S=5 points each.</summary>
public enum Grade
{
    D = 1,
    C,
    B,
    A,
    S,
}

public enum OverallGrade
{
    D,
    C,
    B,
    A,
    S,
    P,
}

/// <summary>What a floor asked of the player, derived from its layout so generated floors are fairly scored.</summary>
public readonly record struct FloorTargets(double ParTimeSeconds, int Enemies)
{
    public const double BaseSeconds = 40;
    public const double SecondsPerEnemy = 1.2;
    public const double SecondsPerBlockOfPath = 0.25;

    /// <summary>Par time grows with the number of enemies and the distance from entrance to exit.</summary>
    public static FloorTargets For(int enemies, int pathLengthBlocks) =>
        new(BaseSeconds + SecondsPerEnemy * enemies + SecondsPerBlockOfPath * pathLengthBlocks, enemies);

    public Grade GradeTime(double seconds) =>
        seconds <= ParTimeSeconds ? Grade.S :
        seconds <= ParTimeSeconds * 1.3 ? Grade.A :
        seconds <= ParTimeSeconds * 2.0 ? Grade.B :
        seconds <= ParTimeSeconds * 3.0 ? Grade.C : Grade.D;

    public Grade GradeKills(int kills)
    {
        if (Enemies == 0) return Grade.S;
        double share = kills / (double)Enemies;
        return share >= 1.0 ? Grade.S : share >= 0.9 ? Grade.A : share >= 0.75 ? Grade.B : share >= 0.5 ? Grade.C : Grade.D;
    }

    public Grade GradeStyle(double points)
    {
        if (Enemies == 0) return Grade.S;
        double perEnemy = points / Enemies;
        return perEnemy >= 150 ? Grade.S : perEnemy >= 110 ? Grade.A : perEnemy >= 70 ? Grade.B : perEnemy >= 30 ? Grade.C : Grade.D;
    }
}

public readonly record struct FloorResult(Grade Time, Grade Kills, Grade Style, int Restarts, int Points, OverallGrade Overall);

public static class FloorRanking
{
    /// <summary>
    /// Sums the three grades, subtracts a point per restart, and maps the total: 14 or more is S, 11-13 A,
    /// 8-10 B, 5-7 C, anything less D. P needs S in all three with no restarts.
    /// </summary>
    public static FloorResult Score(FloorTargets targets, double seconds, int kills, double styleScore, int restarts)
    {
        var time = targets.GradeTime(seconds);
        var killGrade = targets.GradeKills(kills);
        var style = targets.GradeStyle(styleScore);

        int points = (int)time + (int)killGrade + (int)style - restarts;
        bool perfect = time == Grade.S && killGrade == Grade.S && style == Grade.S && restarts == 0;
        var overall = perfect ? OverallGrade.P
            : points >= 14 ? OverallGrade.S
            : points >= 11 ? OverallGrade.A
            : points >= 8 ? OverallGrade.B
            : points >= 5 ? OverallGrade.C
            : OverallGrade.D;
        return new FloorResult(time, killGrade, style, restarts, points, overall);
    }
}
