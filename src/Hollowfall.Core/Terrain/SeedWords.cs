namespace Hollowfall.Core.Terrain;

/// <summary>Readable random seeds ("amber-hollow-914") so a world can be named, shared and typed back in.</summary>
public static class SeedWords
{
    public const int MaxLength = 100;
    public const string Default = "voxel";

    private static readonly string[] First =
    [
        "amber", "ashen", "bleak", "cinder", "cold", "crimson", "dusk", "ember", "frost", "gilded", "grim", "hushed",
        "iron", "jade", "lost", "mossy", "pale", "quiet", "rust", "silent", "sunken", "thorn", "umber", "violet",
    ];

    private static readonly string[] Second =
    [
        "abyss", "barrow", "catacomb", "crypt", "deep", "delve", "fall", "gully", "hollow", "keep", "maw", "mine",
        "pit", "reach", "shaft", "spire", "tomb", "vault", "warren", "well", "vein", "vale", "undercroft", "den",
    ];

    /// <summary>A seed made from a number: the same number always gives the same words.</summary>
    public static string From(ulong entropy)
    {
        ulong h = SeedHash.Mix(entropy);
        string a = First[(int)(h % (ulong)First.Length)];
        h = SeedHash.Mix(h);
        string b = Second[(int)(h % (ulong)Second.Length)];
        h = SeedHash.Mix(h);
        return $"{a}-{b}-{h % 1000:D3}";
    }

    /// <summary>Cleans typed text into a usable seed: trimmed, free of control characters, at most <see cref="MaxLength"/> long, never empty.</summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Default;
        var cleaned = new string(text.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length > MaxLength) cleaned = cleaned[..MaxLength].TrimEnd();
        return cleaned.Length == 0 ? Default : cleaned;
    }
}
