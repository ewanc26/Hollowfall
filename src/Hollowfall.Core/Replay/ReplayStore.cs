using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Replay;

/// <summary>
/// The best run of each seed and floor, one small file each in a <c>replays</c> folder beside <c>records.json</c>. Only a run that
/// beats what is stored replaces it. Loading is defensive: a missing, oversized or damaged file is simply no ghost, and nothing
/// here throws on bad data or a failing disk.
/// </summary>
public sealed class ReplayStore(string directory)
{
    /// <summary>At most this many files are kept; the oldest are deleted first.</summary>
    public const int MaxEntries = 64;

    public string Folder { get; } = directory;

    /// <summary>The store that sits beside a records file, so <c>HOLLOWFALL_RECORDS</c> moves both.</summary>
    public static ReplayStore Beside(string recordsPath) =>
        new(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(recordsPath)) ?? ".", "replays"));

    public string PathFor(string seed, int floor)
    {
        ulong h = 1469598103934665603UL;
        foreach (char c in seed)
        {
            h ^= c;
            h *= 1099511628211UL;
        }
        return Path.Combine(Folder, $"{h:x16}-{floor}.hfr");
    }

    /// <summary>The stored best for a seed and floor, or null if there is none or the file cannot be trusted.</summary>
    public RunReplay? Load(string seed, int floor)
    {
        if (string.IsNullOrEmpty(seed) || floor is < 0 or >= RunReplay.MaxFloors) return null;
        try
        {
            string path = PathFor(seed, floor);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is < 1 or > ReplayCodec.MaxFileBytes) return null;
            if (!ReplayCodec.TryDecode(File.ReadAllBytes(path), out var replay) || replay is null) return null;
            return replay.Seed == seed && replay.Floor == floor ? replay : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Saves the run if nothing better is stored for its seed and floor. Returns whether it was written.</summary>
    public bool TrySaveIfBest(RunReplay replay)
    {
        if (replay.Frames.Count == 0 || replay.Frames.Count > RunReplay.MaxTicks) return false;
        if (Load(replay.Seed, replay.Floor) is { } existing && !replay.Result.Beats(existing.Result)) return false;

        try
        {
            var bytes = ReplayCodec.Encode(replay);
            if (bytes.Length > ReplayCodec.MaxFileBytes) return false;

            System.IO.Directory.CreateDirectory(Folder);
            string path = PathFor(replay.Seed, replay.Floor);
            string temp = path + ".tmp";
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, path, overwrite: true);
            Prune();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException)
        {
            return false;
        }
    }

    private void Prune()
    {
        try
        {
            var files = new DirectoryInfo(Folder).GetFiles("*.hfr").OrderBy(f => f.LastWriteTimeUtc).ThenBy(f => f.Name, StringComparer.Ordinal).ToList();
            for (int i = 0; i < files.Count - MaxEntries; i++) files[i].Delete();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be tidied is not worth failing a save over.
        }
    }
}
