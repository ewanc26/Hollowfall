using Hollowfall.AssetGen;

// Usage: dotnet run --project tools/Hollowfall.AssetGen [repository root]
// Writes every generated SVG. Run it after changing GlyphSet; a test fails if the committed files are out of date.
string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
if (!File.Exists(Path.Combine(root, "Hollowfall.sln")))
{
    Console.Error.WriteLine($"{root} is not the repository root (no Hollowfall.sln). Pass the root as the first argument.");
    return 1;
}

int written = 0;
foreach (var (path, content) in GlyphSet.All())
{
    string full = Path.Combine(root, path);
    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    if (File.Exists(full) && File.ReadAllText(full) == content) continue;
    File.WriteAllText(full, content);
    written++;
}
Console.WriteLine($"{GlyphSet.All().Count} files, {written} written");
return 0;
