using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hollowfall.Core.Progression;

/// <summary>
/// Your character saved as <c>progression.json</c> in the data folder. Read defensively: a damaged or hand-edited file gives a
/// fresh character rather than stopping the game, and every value is clamped on the way in. Saving is atomic. Version 1 files
/// (no bag) still load, with an empty bag.
/// </summary>
public static class ProgressionStore
{
    public const int MaxFileBytes = 4 * 1024;
    public const int Version = 2;

    public static string ToJson(Progression progression)
    {
        var items = new JsonObject();
        foreach (var (kind, count) in progression.Items.Contents()) items[kind.ToString()] = count;
        var quests = new JsonObject();
        foreach (var (quest, progress, done) in progression.Quests.Entries())
            quests[quest.ToString()] = new JsonObject { ["progress"] = progress, ["done"] = done };
        return new JsonObject
        {
            ["version"] = Version,
            ["experience"] = progression.Experience,
            ["vigour"] = progression.Vigour,
            ["might"] = progression.Might,
            ["items"] = items,
            ["charm"] = progression.Items.Charm is { } worn ? worn.ToString() : null,
            ["quests"] = quests,
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static Progression FromJson(string? json)
    {
        var progression = new Progression();
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxFileBytes) return progression;
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowDuplicateProperties = false, MaxDepth = 4 }) as JsonObject;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or FormatException) { return progression; }
        if (root is null) return progression;

        progression.Restore(Number(root, "experience"), Number(root, "vigour"), Number(root, "might"));
        progression.Items.Restore(ReadItems(root), ReadCharm(root));
        ReadQuests(root, progression.Quests);
        return progression;
    }

    public static Progression Load(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > MaxFileBytes) return new Progression();
            return FromJson(File.ReadAllText(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new Progression(); }
    }

    public static bool TrySave(Progression progression, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, ToJson(progression));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>A whole number from the file, or zero if it is missing or not a finite number.</summary>
    private static int Number(JsonObject root, string name)
    {
        if (root[name] is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d))
            return (int)Math.Clamp(d, int.MinValue, int.MaxValue);
        return 0;
    }

    /// <summary>The bag from the file: item names with counts. Names that are not known items are dropped.</summary>
    private static List<(ItemKind Kind, int Count)> ReadItems(JsonObject root)
    {
        var list = new List<(ItemKind, int)>();
        if (root["items"] is not JsonObject items) return list;
        foreach (var (name, node) in items)
        {
            if (!Enum.TryParse<ItemKind>(name, out var kind) || !Enum.IsDefined(kind)) continue;
            if (node is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d))
                list.Add((kind, (int)Math.Clamp(d, 0, int.MaxValue)));
        }
        return list;
    }

    /// <summary>The quests from the file, each clamped. Names that are not known quests are dropped.</summary>
    private static void ReadQuests(JsonObject root, QuestLog quests)
    {
        if (root["quests"] is not JsonObject entries) return;
        foreach (var (name, node) in entries)
        {
            if (!Enum.TryParse<QuestId>(name, out var quest) || !Enum.IsDefined(quest)) continue;
            if (node is not JsonObject entry) continue;
            bool done = entry["done"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
            quests.Restore(quest, Number(entry, "progress"), done);
        }
    }

    /// <summary>The worn charm from the file, or null if there is none or the name is not known.</summary>
    private static ItemKind? ReadCharm(JsonObject root) =>
        root["charm"] is JsonValue v && v.TryGetValue<string>(out var name) && Enum.TryParse<ItemKind>(name, out var kind) && Enum.IsDefined(kind)
            ? kind
            : null;
}
