namespace Hollowfall.Core.Storage;

/// <summary>
/// Where everything the game saves lives: one folder, <c>~/.ewanc26/hollowfall</c>, holding the settings, personal bests,
/// replays, story progress and relationships. A file path can still be overridden by its own environment variable (the smoke
/// test and screenshots use that), and <c>HOLLOWFALL_HOME</c> moves the whole folder. New saved files must get their path from
/// here, never from the engine's user folder.
/// </summary>
public sealed class DataHome
{
    public const string FolderEnvironment = "HOLLOWFALL_HOME";

    public const string Settings = "settings.json";
    public const string Records = "records.json";
    public const string Story = "story.json";
    public const string Relations = "relations.json";
    public const string Progression = "progression.json";
    public const string ReplayFolder = "replays";

    private readonly Func<string, string?> _environment;

    /// <summary>The data folder.</summary>
    public string Root { get; }

    public DataHome(string root, Func<string, string?>? environment = null)
    {
        Root = root;
        _environment = environment ?? System.Environment.GetEnvironmentVariable;
    }

    /// <summary>The folder for this machine and user: HOLLOWFALL_HOME if set, otherwise <c>~/.ewanc26/hollowfall</c>.</summary>
    public static DataHome ForCurrentUser(Func<string, string?>? environment = null, string? userProfile = null)
    {
        environment ??= System.Environment.GetEnvironmentVariable;
        if (environment(FolderEnvironment) is { Length: > 0 } custom) return new DataHome(custom, environment);
        string home = userProfile ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) home = System.Environment.GetEnvironmentVariable("HOME") ?? Path.GetTempPath();
        return new DataHome(Path.Combine(home, ".ewanc26", "hollowfall"), environment);
    }

    /// <summary>The path of a saved file. A per-file environment variable (for example HOLLOWFALL_SETTINGS) wins when set.</summary>
    public string PathOf(string file, string? overrideVariable = null)
    {
        if (overrideVariable != null && _environment(overrideVariable) is { Length: > 0 } custom) return custom;
        return Path.Combine(Root, file);
    }

    public string SettingsPath => PathOf(Settings, "HOLLOWFALL_SETTINGS");
    public string RecordsPath => PathOf(Records, "HOLLOWFALL_RECORDS");
    public string StoryPath => PathOf(Story, "HOLLOWFALL_STORY");
    public string RelationsPath => PathOf(Relations, "HOLLOWFALL_RELATIONS");
    public string ProgressionPath => PathOf(Progression, "HOLLOWFALL_PROGRESSION");

    /// <summary>
    /// Copies the files an earlier version kept in another folder (the engine's user folder) into this one, once: a file is
    /// copied only if this folder does not already have it, and the old copies are left where they were. Returns how many
    /// files were copied. Never throws.
    /// </summary>
    public int AdoptLegacy(string legacyFolder)
    {
        int copied = 0;
        try
        {
            if (!Directory.Exists(legacyFolder) || string.Equals(Path.GetFullPath(legacyFolder), Path.GetFullPath(Root), StringComparison.Ordinal)) return 0;
            Directory.CreateDirectory(Root);
            foreach (string name in new[] { Settings, Records, Story, Relations })
            {
                string from = Path.Combine(legacyFolder, name), to = Path.Combine(Root, name);
                if (!File.Exists(from) || File.Exists(to)) continue;
                File.Copy(from, to);
                copied++;
            }
            string replays = Path.Combine(legacyFolder, ReplayFolder), target = Path.Combine(Root, ReplayFolder);
            if (Directory.Exists(replays) && !Directory.Exists(target))
            {
                Directory.CreateDirectory(target);
                foreach (string file in Directory.GetFiles(replays))
                {
                    File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
                    copied++;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A failed copy costs the player an old save, not the game.
        }
        return copied;
    }
}
