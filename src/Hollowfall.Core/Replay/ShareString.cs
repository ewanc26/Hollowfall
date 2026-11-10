namespace Hollowfall.Core.Replay;

/// <summary>
/// A run as text: a <c>hollowfall:</c> prefix followed by the run file's bytes in base64url, so a seed and a whole replay
/// fit in a message. Decoding goes back through <see cref="ReplayCodec"/>'s checksum and range checks, so a damaged or
/// hand-edited string is simply no run, and nothing here throws.
/// </summary>
public static class ShareString
{
    public const string Prefix = "hollowfall:";

    /// <summary>The longest string we will read: the base64url of a largest run file, plus the prefix.</summary>
    public static readonly int MaxLength = Prefix.Length + (ReplayCodec.MaxFileBytes + 2) / 3 * 4;

    /// <summary>Writes a run as text. Always uses the plain ASCII alphabet, so it survives any chat or clipboard.</summary>
    public static string Encode(RunReplay replay)
    {
        var bytes = ReplayCodec.Encode(replay);
        return Prefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Reads a run back from shared text. False — never an exception — for anything that is not one.</summary>
    public static bool TryDecode(string? text, out RunReplay? replay)
    {
        replay = null;
        if (text == null || !text.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        string body = text[Prefix.Length..];
        if (body.Length == 0 || body.Length > MaxLength - Prefix.Length || body.Length % 4 == 1) return false;
        if (body.Any(c => c is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9') and not ('-' or '_' or '='))) return false;
        if (body.Count(c => c == '=') > 2 || (body.Contains('=') && !body.EndsWith("=", StringComparison.Ordinal))) return false;
        try
        {
            // Trimmed base64 is missing its padding; FromBase64String wants it back.
            string base64 = body.Replace('-', '+').Replace('_', '/');
            base64 += new string('=', (4 - base64.Length % 4) % 4);
            return ReplayCodec.TryDecode(Convert.FromBase64String(base64), out replay);
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            return false;
        }
    }
}
