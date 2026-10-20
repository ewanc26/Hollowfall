namespace VoxelGame.Tests;

public class LanguagePolicyTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("vg-policy").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Write(string rel, string text)
    {
        string path = Path.Combine(_dir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private const string GoodProject = "config/features=PackedStringArray(\"4.7\", \"C#\")\n[dotnet]\nproject/assembly_name=\"VoxelGame.Game\"\n";

    [Fact]
    public void TheRepositoryIsCSharpOnly()
    {
        Assert.Empty(LanguagePolicy.Check(LanguagePolicy.FindRepoRoot()));
    }

    [Fact]
    public void CleanGodotProjectPasses()
    {
        Write("game/project.godot", GoodProject + "[autoload]\nBus=\"*res://scripts/Bus.cs\"\n");
        Write("game/scenes/Main.tscn", "[ext_resource type=\"Script\" path=\"res://scripts/Main.cs\" id=\"1\"]\n");
        Assert.Empty(LanguagePolicy.Check(_dir));
    }

    [Fact]
    public void GDScriptFilesAreRejected()
    {
        Write("game/project.godot", GoodProject);
        Write("game/scripts/player.gd", "extends Node\n");
        var v = Assert.Single(LanguagePolicy.Check(_dir));
        Assert.Contains("player.gd", v);
    }

    [Fact]
    public void GDExtensionIsRejected()
    {
        Write("game/native.gdextension", "[configuration]\n");
        Assert.Contains(LanguagePolicy.Check(_dir), v => v.Contains(".gdextension"));
    }

    [Fact]
    public void ScenesCannotAttachNonCSharpScripts()
    {
        Write("game/project.godot", GoodProject);
        Write("game/scenes/A.tscn", "[ext_resource type=\"Script\" uid=\"uid://abc\" path=\"res://scripts/a.gd\" id=\"1\"]\n");
        Write("game/scenes/B.tres", "[ext_resource type=\"Script\" path=\"res://scripts/b.txt\" id=\"1\"]\n");
        Assert.Equal(2, LanguagePolicy.Check(_dir).Count(v => v.Contains("non-C# script")));
    }

    [Fact]
    public void ProjectMustDeclareCSharpAndAnAssembly()
    {
        Write("game/project.godot", "config/features=PackedStringArray(\"4.7\")\n");
        var v = LanguagePolicy.Check(_dir);
        Assert.Contains(v, x => x.Contains("must include \"C#\""));
        Assert.Contains(v, x => x.Contains("assembly_name"));
    }

    [Fact]
    public void AutoloadsMustBeCSharpOrScenes()
    {
        Write("game/project.godot", GoodProject + "[autoload]\nBad=\"*res://scripts/bad.gd\"\nScene=\"*res://scenes/S.tscn\"\n");
        Assert.Contains(LanguagePolicy.Check(_dir), v => v.Contains("autoload Bad"));
        Assert.DoesNotContain(LanguagePolicy.Check(_dir), v => v.Contains("autoload Scene"));
    }

    [Fact]
    public void BuildOutputAndGodotCacheAreIgnored()
    {
        Write(".godot/imported/x.gd", "");
        Write("game/bin/y.gd", "");
        Assert.Empty(LanguagePolicy.Check(_dir));
    }
}
