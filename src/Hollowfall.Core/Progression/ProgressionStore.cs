using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hollowfall.Core.Progression;

/// <summary>
/// Your character saved as <c>progression.json</c> in the data folder. Read defensively: a damaged or hand-edited file gives a
/// fresh character rather than stopping the game, and every value is clamped on the way in. Saving is atomic.
/// </summary>
public static class ProgressionStore
{
    public const int MaxFileBytes = 4 * 1024;
    public const int Version = 1;

    public static string ToJson(Progression progression) => new JsonObject
    {
        ["version"] = Version,
        ["experience"] = progression.Experience,
        ["vigour"] = progression.Vigour,
        ["might"] = progression.Might,
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

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
}
