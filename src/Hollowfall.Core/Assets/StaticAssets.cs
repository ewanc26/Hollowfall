using Hollowfall.Core.Audio;
using Hollowfall.Core.Textures;

namespace Hollowfall.Core.Assets;

/// <summary>
/// The generated files committed to the game's assets folder. They are written by tools/Hollowfall.AssetGen and the game loads them
/// as ordinary imported assets; code generates the same content only when a file is missing. A test fails when a committed file
/// no longer matches what this produces.
/// </summary>
public static class StaticAssets
{
    /// <summary>Folder paths under game/, as the game's resource paths use them.</summary>
    public const string AtlasPath = "assets/textures/block_atlas.png";
    public const string AudioFolder = "assets/audio";

    public static string SoundFile(SoundId id) => $"{AudioFolder}/{Snake(id.ToString())}.wav";

    public static IReadOnlyList<(string Path, byte[] Bytes)> All()
    {
        var files = new List<(string, byte[])>
        {
            ("game/" + AtlasPath, PngWriter.Encode(BlockTextures.Rgba, BlockTextures.Width, BlockTextures.Height)),
        };
        foreach (var id in Enum.GetValues<SoundId>())
            files.Add(("game/" + SoundFile(id), WavFile.Encode(Synth.Render(id), Synth.SampleRate)));
        return files;
    }

    private static string Snake(string pascal)
    {
        var chars = new List<char>();
        foreach (char c in pascal)
        {
            if (char.IsUpper(c) && chars.Count > 0) chars.Add('_');
            chars.Add(char.ToLowerInvariant(c));
        }
        return new string(chars.ToArray());
    }
}
