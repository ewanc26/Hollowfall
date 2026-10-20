using System.Text.Json;
using System.Text.Json.Nodes;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Core.Story;

/// <summary>
/// What the player has read and seen, per seed: which lore notes are in the journal, whether the opening crawl has been
/// shown, and whether the ending has been reached. Saved as JSON beside the records (<c>story.json</c>) and read
/// defensively like them: sizes are bounded, unknown note ids are dropped, and a damaged file loses its bad entries and
/// never stops the game.
/// </summary>
public sealed class StoryProgress
{
    public const int MaxSeeds = 100;
    public const int MaxNotesPerSeed = 64;
    public const int MaxFileBytes = 256 * 1024;

    private static readonly HashSet<string> KnownIds = [.. StoryBook.Notes.Select(n => n.Id)];

    private sealed class SeedStory
    {
        public bool Intro, Ending;
        public readonly List<string> Notes = [];
    }

    private readonly List<string> _order = [];
    private readonly Dictionary<string, SeedStory> _seeds = [];

    public int SeedCount => _seeds.Count;

    private static bool ValidSeed(string? seed) => !string.IsNullOrEmpty(seed) && seed.Length <= SeedWords.MaxLength;

    private SeedStory? Find(string seed) => ValidSeed(seed) && _seeds.TryGetValue(seed, out var s) ? s : null;

    /// <summary>Finds or makes a seed's entry, evicting the least recently touched seed when there are too many.</summary>
    private SeedStory? Touch(string seed)
    {
        if (!ValidSeed(seed)) return null;
        if (_seeds.TryGetValue(seed, out var existing))
        {
            _order.Remove(seed);
            _order.Add(seed);
            return existing;
        }
        var created = new SeedStory();
        _seeds[seed] = created;
        _order.Add(seed);
        while (_order.Count > MaxSeeds)
        {
            _seeds.Remove(_order[0]);
            _order.RemoveAt(0);
        }
        return created;
    }

    public bool IntroSeen(string seed) => Find(seed)?.Intro ?? false;

    public void MarkIntroSeen(string seed)
    {
        if (Touch(seed) is { } s) s.Intro = true;
    }

    public bool EndingSeen(string seed) => Find(seed)?.Ending ?? false;

    public void MarkEndingSeen(string seed)
    {
        if (Touch(seed) is { } s) s.Ending = true;
    }

    public bool IsRead(string seed, string noteId) => Find(seed)?.Notes.Contains(noteId) ?? false;

    /// <summary>The ids of the notes read on a seed, in the order they were first read.</summary>
    public IReadOnlyList<string> ReadIds(string seed) => Find(seed)?.Notes ?? (IReadOnlyList<string>)[];

    /// <summary>Records that a note was read. Returns true only the first time. Unknown ids are refused.</summary>
    public bool MarkRead(string seed, string noteId)
    {
        if (!KnownIds.Contains(noteId) || IsRead(seed, noteId)) return false;
        if (Touch(seed) is not { } s || s.Notes.Count >= MaxNotesPerSeed) return false;
        s.Notes.Add(noteId);
        return true;
    }

    // ---- saving ----------------------------------------------------------------------------------

    public string ToJson()
    {
        var seeds = new JsonObject();
        foreach (var seed in _order)
        {
            var s = _seeds[seed];
            seeds[seed] = new JsonObject
            {
                ["intro"] = s.Intro,
                ["ending"] = s.Ending,
                ["notes"] = new JsonArray([.. s.Notes.Select(id => (JsonNode?)JsonValue.Create(id))]),
            };
        }
        return new JsonObject { ["version"] = 1, ["seeds"] = seeds }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static StoryProgress FromJson(string? json)
    {
        var progress = new StoryProgress();
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxFileBytes) return progress;
        JsonObject? root;
        try { root = JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { return progress; }
        if (root?["seeds"] is not JsonObject seeds) return progress;

        foreach (var (seed, node) in seeds)
        {
            if (node is not JsonObject entry || !ValidSeed(seed)) continue;
            if (progress.Touch(seed) is not { } s) continue;
            s.Intro = entry["intro"] is JsonValue iv && iv.TryGetValue<bool>(out var intro) && intro;
            s.Ending = entry["ending"] is JsonValue ev && ev.TryGetValue<bool>(out var ending) && ending;
            if (entry["notes"] is not JsonArray notes) continue;
            foreach (var item in notes)
            {
                if (s.Notes.Count >= MaxNotesPerSeed) break;
                if (item is JsonValue v && v.TryGetValue<string>(out var id) && KnownIds.Contains(id) && !s.Notes.Contains(id)) s.Notes.Add(id);
            }
        }
        return progress;
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

    public static StoryProgress Load(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > MaxFileBytes) return new StoryProgress();
            return FromJson(File.ReadAllText(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new StoryProgress(); }
    }
}
