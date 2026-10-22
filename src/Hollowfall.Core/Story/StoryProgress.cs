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
        public readonly List<string> WulverFlags = [];
        public int WulverVisits;
        public readonly List<string> CatFlags = [];
        public int CatVisits;
        public readonly List<string> VillagerFlags = [];
        public int VillagerVisits;
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

    /// <summary>What the Wulver remembers of this seed: short flag ids (at most <see cref="WulverEncounter.MaxFlags"/>) and how often he has been visited.</summary>
    public IReadOnlyList<string> WulverFlags(string seed) => Find(seed)?.WulverFlags ?? (IReadOnlyList<string>)[];

    public int WulverVisits(string seed) => Find(seed)?.WulverVisits ?? 0;

    /// <summary>Replaces what the Wulver remembers. Invalid or surplus flags are dropped.</summary>
    public void SetWulver(string seed, IEnumerable<string> flags, int visits)
    {
        if (Touch(seed) is not { } s) return;
        s.WulverFlags.Clear();
        foreach (var f in flags)
            if (WulverEncounter.ValidFlag(f) && !s.WulverFlags.Contains(f) && s.WulverFlags.Count < WulverEncounter.MaxFlags) s.WulverFlags.Add(f);
        s.WulverVisits = Math.Clamp(visits, 0, 10_000);
    }

    /// <summary>What the cat remembers of this seed: short flag ids (at most <see cref="CatEncounter.MaxFlags"/>) and how often he has been visited.</summary>
    public IReadOnlyList<string> CatFlags(string seed) => Find(seed)?.CatFlags ?? (IReadOnlyList<string>)[];

    public int CatVisits(string seed) => Find(seed)?.CatVisits ?? 0;

    /// <summary>Replaces what the cat remembers. Invalid or surplus flags are dropped.</summary>
    public void SetCat(string seed, IEnumerable<string> flags, int visits)
    {
        if (Touch(seed) is not { } s) return;
        s.CatFlags.Clear();
        foreach (var f in flags)
            if (WulverEncounter.ValidFlag(f) && !s.CatFlags.Contains(f) && s.CatFlags.Count < CatEncounter.MaxFlags) s.CatFlags.Add(f);
        s.CatVisits = Math.Clamp(visits, 0, 10_000);
    }

    /// <summary>What the villagers remember of this seed: short flag ids (at most <see cref="VillagerConversation.MaxFlags"/>) and how often they have been spoken to.</summary>
    public IReadOnlyList<string> VillagerFlags(string seed) => Find(seed)?.VillagerFlags ?? (IReadOnlyList<string>)[];

    public int VillagerVisits(string seed) => Find(seed)?.VillagerVisits ?? 0;

    /// <summary>Replaces what the villagers remember. Invalid or surplus flags are dropped.</summary>
    public void SetVillagers(string seed, IEnumerable<string> flags, int visits)
    {
        if (Touch(seed) is not { } s) return;
        s.VillagerFlags.Clear();
        foreach (var f in flags)
            if (WulverEncounter.ValidFlag(f) && !s.VillagerFlags.Contains(f) && s.VillagerFlags.Count < VillagerConversation.MaxFlags) s.VillagerFlags.Add(f);
        s.VillagerVisits = Math.Clamp(visits, 0, 10_000);
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
                ["wulver"] = new JsonObject
                {
                    ["visits"] = s.WulverVisits,
                    ["flags"] = new JsonArray([.. s.WulverFlags.Select(id => (JsonNode?)JsonValue.Create(id))]),
                },
                ["cat"] = new JsonObject
                {
                    ["visits"] = s.CatVisits,
                    ["flags"] = new JsonArray([.. s.CatFlags.Select(id => (JsonNode?)JsonValue.Create(id))]),
                },
                ["villagers"] = new JsonObject
                {
                    ["visits"] = s.VillagerVisits,
                    ["flags"] = new JsonArray([.. s.VillagerFlags.Select(id => (JsonNode?)JsonValue.Create(id))]),
                },
            };
        }
        return new JsonObject { ["version"] = 1, ["seeds"] = seeds }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static StoryProgress FromJson(string? json)
    {
        var progress = new StoryProgress();
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxFileBytes) return progress;
        JsonObject? root;
        try
        {
            // A repeated property name would otherwise throw later, when the tree is first read, so refuse it here.
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowDuplicateProperties = false }) as JsonObject;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or FormatException) { return progress; }
        if (root?["seeds"] is not JsonObject seeds) return progress;

        foreach (var (seed, node) in seeds)
        {
            if (node is not JsonObject entry || !ValidSeed(seed)) continue;
            if (progress.Touch(seed) is not { } s) continue;
            s.Intro = entry["intro"] is JsonValue iv && iv.TryGetValue<bool>(out var intro) && intro;
            s.Ending = entry["ending"] is JsonValue ev && ev.TryGetValue<bool>(out var ending) && ending;
            if (entry["wulver"] is JsonObject w)
            {
                if (w["visits"] is JsonValue vv && vv.TryGetValue<int>(out var visits)) s.WulverVisits = Math.Clamp(visits, 0, 10_000);
                if (w["flags"] is JsonArray fl)
                    foreach (var item in fl)
                        if (item is JsonValue v && v.TryGetValue<string>(out var flag) && WulverEncounter.ValidFlag(flag) && !s.WulverFlags.Contains(flag) && s.WulverFlags.Count < WulverEncounter.MaxFlags)
                            s.WulverFlags.Add(flag);
            }
            if (entry["cat"] is JsonObject c)
            {
                if (c["visits"] is JsonValue cv && cv.TryGetValue<int>(out var catVisits)) s.CatVisits = Math.Clamp(catVisits, 0, 10_000);
                if (c["flags"] is JsonArray cf)
                    foreach (var item in cf)
                        if (item is JsonValue v && v.TryGetValue<string>(out var flag) && WulverEncounter.ValidFlag(flag) && !s.CatFlags.Contains(flag) && s.CatFlags.Count < CatEncounter.MaxFlags)
                            s.CatFlags.Add(flag);
            }
            if (entry["villagers"] is JsonObject vg)
            {
                if (vg["visits"] is JsonValue gv && gv.TryGetValue<int>(out var villagerVisits)) s.VillagerVisits = Math.Clamp(villagerVisits, 0, 10_000);
                if (vg["flags"] is JsonArray vf)
                    foreach (var item in vf)
                        if (item is JsonValue v && v.TryGetValue<string>(out var flag) && WulverEncounter.ValidFlag(flag) && !s.VillagerFlags.Contains(flag) && s.VillagerFlags.Count < VillagerConversation.MaxFlags)
                            s.VillagerFlags.Add(flag);
            }
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
