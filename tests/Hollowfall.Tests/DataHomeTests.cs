using Hollowfall.Core.Storage;

namespace Hollowfall.Tests;

public class DataHomeTests
{
    private static string Temp() => Path.Combine(Path.GetTempPath(), $"hollowfall-home-{Guid.NewGuid():N}");

    [Fact]
    public void TheDefaultFolderIsDotEwanc26HollowfallInTheHomeFolder()
    {
        var home = DataHome.ForCurrentUser(_ => null, "/home/someone");
        Assert.Equal(Path.Combine("/home/someone", ".ewanc26", "hollowfall"), home.Root);
        Assert.Equal(Path.Combine(home.Root, "settings.json"), home.SettingsPath);
        Assert.Equal(Path.Combine(home.Root, "records.json"), home.RecordsPath);
        Assert.Equal(Path.Combine(home.Root, "story.json"), home.StoryPath);
        Assert.Equal(Path.Combine(home.Root, "relations.json"), home.RelationsPath);
    }

    [Fact]
    public void TheWholeFolderCanBeMoved()
    {
        var home = DataHome.ForCurrentUser(v => v == "HOLLOWFALL_HOME" ? "/elsewhere" : null, "/home/someone");
        Assert.Equal("/elsewhere", home.Root);
        Assert.Equal(Path.Combine("/elsewhere", "records.json"), home.RecordsPath);
    }

    [Fact]
    public void AFileCanBeOverriddenOnItsOwn()
    {
        var home = DataHome.ForCurrentUser(v => v == "HOLLOWFALL_SETTINGS" ? "/tmp/mine.json" : null, "/h");
        Assert.Equal("/tmp/mine.json", home.SettingsPath);
        Assert.Equal(Path.Combine("/h", ".ewanc26", "hollowfall", "records.json"), home.RecordsPath);
    }

    [Fact]
    public void AnEmptyOverrideIsIgnored()
    {
        var home = DataHome.ForCurrentUser(v => v == "HOLLOWFALL_HOME" || v == "HOLLOWFALL_SETTINGS" ? "" : null, "/h");
        Assert.Equal(Path.Combine("/h", ".ewanc26", "hollowfall"), home.Root);
        Assert.Equal(Path.Combine(home.Root, "settings.json"), home.SettingsPath);
    }

    [Fact]
    public void ALegacyFolderIsCopiedOnceAndNeverOverwritesNewerFilesOrDeletesTheOld()
    {
        string legacy = Temp(), root = Temp();
        try
        {
            Directory.CreateDirectory(Path.Combine(legacy, "replays"));
            File.WriteAllText(Path.Combine(legacy, "settings.json"), "old-settings");
            File.WriteAllText(Path.Combine(legacy, "records.json"), "old-records");
            File.WriteAllText(Path.Combine(legacy, "replays", "a.bin"), "replay");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "records.json"), "new-records"); // already there: must be kept

            var home = new DataHome(root, _ => null);
            Assert.Equal(2, home.AdoptLegacy(legacy)); // settings and the one replay; records already existed
            Assert.Equal("old-settings", File.ReadAllText(Path.Combine(root, "settings.json")));
            Assert.Equal("new-records", File.ReadAllText(Path.Combine(root, "records.json")));
            Assert.Equal("replay", File.ReadAllText(Path.Combine(root, "replays", "a.bin")));
            Assert.True(File.Exists(Path.Combine(legacy, "settings.json")), "the old copy stays");

            Assert.Equal(0, home.AdoptLegacy(legacy)); // a second run changes nothing
        }
        finally
        {
            foreach (string d in new[] { legacy, root }) try { Directory.Delete(d, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void AdoptingFromAMissingOrIdenticalFolderIsHarmless()
    {
        string root = Temp();
        var home = new DataHome(root, _ => null);
        Assert.Equal(0, home.AdoptLegacy(Path.Combine(root, "nope")));
        Directory.CreateDirectory(root);
        try { Assert.Equal(0, home.AdoptLegacy(root)); }
        finally { Directory.Delete(root, true); }
    }
}
