using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Replay;

/// <summary>One line of the daily board: the best verified run of one floor of one day's dungeon.</summary>
public sealed record DailyEntry(string Seed, int Floor, FloorRecord Result);

/// <summary>
/// The daily board. Each day's dungeon has a seed of the form <c>daily-YYYY-MM-DD</c>; for each of its floors the board keeps the best
/// run that <b>verifies</b>: a replay recorded under the current rules whose re-simulation reaches the same result. A run that does
/// not verify never appears, however good it looks. Runs are ranked by grade, then points, then the less time taken.
/// Pure: the caller supplies the verification, so this does not need a world.
/// </summary>
public static class DailyBoard
{
    /// <summary>Whether a seed names a daily dungeon.</summary>
    public static bool IsDaily(string seed) => seed.StartsWith(Hollowfall.Core.Terrain.DailySeed.Prefix, StringComparison.Ordinal);

    /// <summary>Whether one result beats another: higher grade, then more points, then less time.</summary>
    public static bool Better(FloorRecord a, FloorRecord b) =>
        a.Grade != b.Grade ? a.Grade > b.Grade
        : a.Points != b.Points ? a.Points > b.Points
        : a.Seconds < b.Seconds;

    /// <summary>
    /// The board for these replays. <paramref name="verify"/> re-simulates a replay and says whether it reaches its recorded result; it
    /// is only called on the runs that could be the best, best first, and stops at the first that verifies for each floor.
    /// </summary>
    public static IReadOnlyList<DailyEntry> Build(IEnumerable<RunReplay> replays, Func<RunReplay, bool> verify)
    {
        var entries = new List<DailyEntry>();
        foreach (var group in replays.Where(r => IsDaily(r.Seed)).GroupBy(r => (r.Seed, r.Floor)).OrderBy(g => g.Key.Seed, StringComparer.Ordinal).ThenBy(g => g.Key.Floor))
        {
            foreach (var replay in group.OrderByDescending(r => r.Result, Comparer<FloorRecord>.Create((a, b) => Better(a, b) ? 1 : Better(b, a) ? -1 : 0)))
            {
                if (!replay.IsVerifiable || !verify(replay)) continue;
                entries.Add(new DailyEntry(replay.Seed, replay.Floor, replay.Result));
                break;
            }
        }
        return entries;
    }
}
