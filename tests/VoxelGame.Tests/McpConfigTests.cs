using System.Text.Json.Nodes;

namespace VoxelGame.Tests;

public class McpConfigTests
{
    private static JsonObject Servers() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(LanguagePolicy.FindRepoRoot(), ".mcp.json")))!["mcpServers"]!.AsObject();

    [Fact]
    public void GodotMcpIsRegisteredAndPinnedToAnExactVersion()
    {
        var godot = Servers()["godot"]!;
        Assert.Equal("npx", godot["command"]!.GetValue<string>());
        var package = godot["args"]!.AsArray().Select(a => a!.GetValue<string>()).Single(a => a.StartsWith("@coding-solo/godot-mcp"));
        Assert.Matches(@"^@coding-solo/godot-mcp@\d+\.\d+\.\d+$", package); // never @latest: it executes code on this machine
    }

    [Fact]
    public void GodotPathHasADefaultButCanBeOverridden()
    {
        string path = Servers()["godot"]!["env"]!["GODOT_PATH"]!.GetValue<string>();
        Assert.StartsWith("${GODOT_PATH:-", path);
    }

    [Fact]
    public void NoCredentialsAreCommittedInTheMcpConfig()
    {
        string text = File.ReadAllText(Path.Combine(LanguagePolicy.FindRepoRoot(), ".mcp.json"));
        Assert.DoesNotMatch(@"(?i)(token|secret|password|api[_-]?key)""\s*:", text);
    }
}
