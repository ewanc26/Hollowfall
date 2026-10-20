using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hollowfall.Core.Combat;

/// <summary>The best a player has done on one floor of one seed.</summary>
public readonly record struct FloorRecord(OverallGrade Grade, int Points, double Seconds)
{
    /// <summary>Better grade wins, then more points, then a faster time.</summary>
    public bool Beats(FloorRecord other) =>
        Grade != other.Grade ? Grade > other.Grade :
        Points != other.Points ? Points > other.Points :
        Seconds < other.Seconds;
}

/// <summary>
/// Personal bests per seed and floor, so a dungeon can be revisited and beaten. Saved as JSON beside the settings and read
/// defensively: a damaged file loses its bad entries and never stops the game.
/// </summary>
public sealed class RunRecords
{
    public const int MaxSeeds = 100;
    public const int MaxFloors = 64;

    private readonly List<string> _order = [];
    private readonly Dictionary<string, Dictionary<int, FloorRecord>> _bests = [];

    public int SeedCount => _bests.Count;

    public FloorRecord? Best(string seed, int floor) =>
        _bests.TryGetValue(seed, out var floors) && floors.TryGetValue(floor, out var r) ? r : null;

    /// <summary>Records a result if it is the best yet. Returns the previous best (or null if there was none) and whether this beat it.</summary>
    public (bool IsBest, FloorRecord? Previous) Submit(string seed, int floor, FloorResult result, double seconds)
    {
        if (floor is < 0 or >= MaxFloors || string.IsNullOrEmpty(seed)) return (false, null);
        var mine = new FloorRecord(result.Overall, result.Points, double.IsFinite(seconds) ? Math.Max(0, seconds) : 0);
        var previous = Best(seed, floor);
        if (previous is { } p && !mine.Beats(p)) return (false, previous);

        if (!_bests.TryGetValue(seed, out var floors))
        {
            floors = [];
            _bests[seed] = floors;
            _order.Add(seed);
            while (_order.Count > MaxSeeds)
            {
                _bests.Remove(_order[0]);
                _order.RemoveAt(0);
            }
        }
        floors[floor] = mine;
        return (true, previous);
    }

    // ---- saving ---------------------------------------------------------------------------

    public string ToJson()
    {
        var seeds = new JsonObject();
        foreach (var seed in _order)
        {
            var floors = new JsonObject();
            foreach (var (floor, r) in _bests[seed].OrderBy(kv => kv.Key))
                floors[floor.ToString()] = new JsonObject { ["grade"] = r.Grade.ToString(), ["points"] = r.Points, ["seconds"] = r.Seconds };
            seeds[seed] = floors;
        }
        return new JsonObject { ["version"] = 1, ["seeds"] = seeds }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static RunRecords FromJson(string? json)
    {
        var records = new RunRecords();
        if (string.IsNullOrWhiteSpace(json)) return records;
        JsonObject? root;
        try { root = JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { return records; }
        if (root?["seeds"] is not JsonObject seeds) return records;

        foreach (var (seed, node) in seeds)
        {
            if (node is not JsonObject floors || string.IsNullOrEmpty(seed)) continue;
            foreach (var (key, entry) in floors)
            {
                if (!int.TryParse(key, out int floor) || floor is < 0 or >= MaxFloors || entry is not JsonObject e) continue;
                if (!Enum.TryParse<OverallGrade>(e["grade"] is JsonValue g && g.TryGetValue<string>(out var gs) ? gs : null, out var grade) || !Enum.IsDefined(grade)) continue;
                int points = e["points"] is JsonValue pv && pv.TryGetValue<int>(out var pi) ? pi : 0;
                double seconds = e["seconds"] is JsonValue sv && sv.TryGetValue<double>(out var sd) && double.IsFinite(sd) ? Math.Max(0, sd) : 0;
                records.Submit(seed, floor, new FloorResult(Grade.D, Grade.D, Grade.D, 0, points, grade), seconds);
            }
        }
        return records;
    }

    public bool TrySave(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, ToJson());
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static RunRecords Load(string path)
    {
        try { return File.Exists(path) ? FromJson(File.ReadAllText(path)) : new RunRecords(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new RunRecords(); }
    }
}
