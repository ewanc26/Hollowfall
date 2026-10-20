using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hollowfall.Core.Relations;

/// <summary>
/// The standings a player left each seed with, saved as <c>relations.json</c> in the data folder. A run always starts from
/// the factions' starting standings (reputation is per run for now); the store remembers where each seed ended so the Standing
/// screen can show it and a later option can carry it over. Read defensively: a damaged file loses its bad entries and never
/// stops the game. Sizes are bounded, duplicate keys are refused rather than thrown on, and saving is atomic.
/// </summary>
public sealed class RelationsStore
{
    public const int MaxSeeds = 50;
    public const int MaxSeedLength = 80;
    public const int MaxFileBytes = 256 * 1024;

    private readonly List<string> _order = [];
    private readonly Dictionary<string, Dictionary<Faction, double>> _seeds = [];

    public int SeedCount => _seeds.Count;

    /// <summary>The standings saved for a seed, or null.</summary>
    public IReadOnlyDictionary<Faction, double>? Standings(string seed) => _seeds.GetValueOrDefault(seed);

    /// <summary>Remembers where a run of <paramref name="seed"/> ended. The newest seeds are kept when there are too many.</summary>
    public bool Record(string seed, Reputation reputation)
    {
        if (string.IsNullOrEmpty(seed) || seed.Length > MaxSeedLength) return false;
        _order.Remove(seed);
        _order.Add(seed);
        _seeds[seed] = reputation.Snapshot();
        while (_order.Count > MaxSeeds)
        {
            _seeds.Remove(_order[0]);
            _order.RemoveAt(0);
        }
        return true;
    }

    /// <summary>A reputation holding the saved standings of a seed (or fresh starting ones if none were saved).</summary>
    public Reputation Carry(string seed)
    {
        var rep = new Reputation();
        if (Standings(seed) is { } saved) rep.Restore(saved);
        return rep;
    }

    public string ToJson()
    {
        var seeds = new JsonObject();
        foreach (var seed in _order)
        {
            var o = new JsonObject();
            foreach (var (f, v) in _seeds[seed].OrderBy(kv => kv.Key)) o[f.ToString()] = Math.Round(v, 3);
            seeds[seed] = o;
        }
        return new JsonObject { ["version"] = 1, ["seeds"] = seeds }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static RelationsStore FromJson(string? json)
    {
        var store = new RelationsStore();
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxFileBytes) return store;
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowDuplicateProperties = false, MaxDepth = 8 }) as JsonObject;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or FormatException) { return store; }
        if (root?["seeds"] is not JsonObject seeds) return store;

        foreach (var (seed, node) in seeds)
        {
            if (store._order.Count >= MaxSeeds) break;
            if (node is not JsonObject standings || string.IsNullOrEmpty(seed) || seed.Length > MaxSeedLength) continue;
            var values = new Dictionary<Faction, double>();
            foreach (var (name, value) in standings)
            {
                if (!Enum.TryParse<Faction>(name, out var f) || !Enum.IsDefined(f) || f == Faction.Delver) continue;
                if (value is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d))
                    values[f] = Math.Clamp(d, ReputationTiers.Min, ReputationTiers.Max);
            }
            if (values.Count == 0) continue;
            store._order.Add(seed);
            store._seeds[seed] = values;
        }
        return store;
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

    public static RelationsStore Load(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > MaxFileBytes) return new RelationsStore();
            return FromJson(File.ReadAllText(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new RelationsStore(); }
    }
}
