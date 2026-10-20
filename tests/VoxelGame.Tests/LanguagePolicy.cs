using System.Text.RegularExpressions;

namespace VoxelGame.Tests;

/// <summary>
/// Enforces the project's language rule: all game code is C#. Godot is only the shell, so no
/// GDScript and no GDExtension. Scenes and resources are data and are fine, but every script they
/// attach has to be a .cs file.
/// </summary>
public static partial class LanguagePolicy
{
    private static readonly string[] SkippedDirectories = [".git", ".godot", "bin", "obj", "node_modules"];
    private static readonly string[] ForbiddenExtensions = [".gd", ".gdextension"];

    public static IReadOnlyList<string> Check(string root)
    {
        var violations = new List<string>();
        foreach (var file in Files(root))
        {
            string rel = Path.GetRelativePath(root, file);
            string ext = Path.GetExtension(file).ToLowerInvariant();

            if (ForbiddenExtensions.Contains(ext))
                violations.Add($"{rel}: {ext} files are not allowed; write it in C#");

            if (ext is ".tscn" or ".tres")
                foreach (Match m in ScriptResource().Matches(File.ReadAllText(file)))
                    if (!m.Groups["path"].Value.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        violations.Add($"{rel}: attaches non-C# script {m.Groups["path"].Value}");

            if (Path.GetFileName(file) == "project.godot")
                violations.AddRange(CheckProjectFile(rel, File.ReadAllText(file)));
        }
        return violations;
    }

    private static IEnumerable<string> CheckProjectFile(string rel, string text)
    {
        if (!Regex.IsMatch(text, @"config/features=.*""C#"""))
            yield return $"{rel}: config/features must include \"C#\"";
        if (!Regex.IsMatch(text, @"project/assembly_name="))
            yield return $"{rel}: [dotnet] project/assembly_name is missing";
        foreach (Match m in Autoload().Matches(text))
        {
            string path = m.Groups["path"].Value;
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                yield return $"{rel}: autoload {m.Groups["name"].Value} points at {path}, which is not C#";
        }
    }

    private static IEnumerable<string> Files(string dir)
    {
        foreach (var file in Directory.EnumerateFiles(dir))
            yield return file;
        foreach (var sub in Directory.EnumerateDirectories(dir))
            if (!SkippedDirectories.Contains(Path.GetFileName(sub)))
                foreach (var file in Files(sub))
                    yield return file;
    }

    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "VoxelGame.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("VoxelGame.sln not found above the test binaries");
    }

    [GeneratedRegex(@"type=""Script""\s+(?:uid=""[^""]*""\s+)?path=""(?<path>[^""]+)""")]
    private static partial Regex ScriptResource();

    [GeneratedRegex(@"^(?<name>\w+)=""\*?(?<path>[^""]+)""", RegexOptions.Multiline)]
    private static partial Regex Autoload();
}
