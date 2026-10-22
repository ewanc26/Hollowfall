using System.Xml.Linq;
using Hollowfall.AssetGen;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Input;
using Hollowfall.Core.Ui;

namespace Hollowfall.Tests;

public class AssetTests
{
    private static string Root => LanguagePolicy.FindRepoRoot();

    private static string Normalise(string s) => s.Replace("\r\n", "\n");

    [Fact]
    public void CommittedVectorAssetsMatchTheGenerator()
    {
        // If this fails, run: dotnet run --project tools/Hollowfall.AssetGen
        foreach (var (path, content) in GlyphSet.All())
        {
            string full = Path.Combine(Root, path);
            Assert.True(File.Exists(full), $"{path} is missing; run the asset generator");
            Assert.Equal(Normalise(content), Normalise(File.ReadAllText(full)));
        }
    }

    [Fact]
    public void NoStrayFilesSitInTheGlyphFolder()
    {
        var expected = GlyphSet.GlyphIds().Select(id => $"{id}.svg").ToHashSet();
        var actual = Directory.EnumerateFiles(Path.Combine(Root, "game", "assets", "glyphs"), "*.svg").Select(Path.GetFileName).ToHashSet();
        Assert.Empty(actual.Except(expected));
        Assert.Empty(expected.Except(actual));
    }

    [Fact]
    public void EverySvgIsWellFormedSafeAndTextFree()
    {
        foreach (var (path, content) in GlyphSet.All())
        {
            var doc = XDocument.Parse(content);
            Assert.Equal("svg", doc.Root!.Name.LocalName);
            Assert.NotNull(doc.Root.Attribute("viewBox"));
            // The engine's importer draws no text, and an asset should never pull anything in from elsewhere or run code.
            Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName is "text" or "script" or "image" or "foreignObject" or "use");
            Assert.DoesNotContain("http://", content.Replace("http://www.w3.org/2000/svg", ""));
            Assert.DoesNotContain("href", content);
        }
    }

    [Fact]
    public void GlyphIdsAreUniqueAndFollowTheNamingScheme()
    {
        var ids = GlyphSet.GlyphIds();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.Matches("^(pad|ps|ns|mouse|icon|rank)_[a-z0-9_]+$|^mouse$", id));
    }

    [Fact]
    public void EveryInputGlyphIdHasAFile()
    {
        var generated = GlyphSet.GlyphIds().ToHashSet();
        Assert.NotEmpty(BindingGlyphs.AllIds);
        Assert.All(BindingGlyphs.AllIds, id => Assert.Contains(id, generated));
        Assert.All(GameIcons.All, id => Assert.Contains(id, generated));
        Assert.Equal(8, GameIcons.RankIcons.Count);
        Assert.All(GameIcons.RankIcons, id => Assert.Contains(id, generated));
    }

    [Fact]
    public void EveryDefaultGamepadAndMouseBindingHasAGlyphAndKeysUseKeycaps()
    {
        var map = BindingMap.CreateDefault();
        foreach (var a in Enum.GetValues<GameAction>())
        {
            if (map.Get(a, Slot.Gamepad) is { } pad) Assert.True(BindingGlyphs.IdOf(pad) != null, $"{a}: no glyph for {pad}");
            if (map.Get(a, Slot.KeyboardMouse) is { } km)
            {
                if (km.Device == InputDevice.Mouse) Assert.NotNull(BindingGlyphs.IdOf(km));
                else Assert.Null(BindingGlyphs.IdOf(km));
            }
        }
        Assert.Null(BindingGlyphs.IdOf(null));
        Assert.Null(BindingGlyphs.IdOf(Binding.Mouse("Xbutton1")));
        Assert.Null(BindingGlyphs.IdOf(Binding.Button("Guide")));
    }

    [Theory]
    [InlineData("Button", "A", 0, "pad_a")]
    [InlineData("Button", "LeftShoulder", 0, "pad_lb")]
    [InlineData("Button", "DpadLeft", 0, "pad_dpad_left")]
    [InlineData("Axis", "TriggerRight", 1, "pad_rt")]
    [InlineData("Axis", "LeftX", -1, "pad_lstick_left")]
    [InlineData("Axis", "LeftY", -1, "pad_lstick_up")]
    [InlineData("Axis", "RightY", 1, "pad_rstick_down")]
    [InlineData("Mouse", "Left", 0, "mouse_left")]
    [InlineData("Mouse", "WheelUp", 0, "mouse_wheel_up")]
    public void BindingsMapToTheRightGlyph(string kind, string code, int sign, string expected)
    {
        Binding b = kind switch
        {
            "Button" => Binding.Button(code),
            "Axis" => Binding.Axis(code, sign),
            _ => Binding.Mouse(code),
        };
        Assert.Equal(expected, BindingGlyphs.IdOf(b));
    }

    [Fact]
    public void RankIconsFollowTheStyleRanks()
    {
        Assert.Null(GameIcons.RankIcon(StyleRank.None));
        Assert.Equal("rank_1", GameIcons.RankIcon(StyleRank.Rowdy));
        Assert.Equal("rank_8", GameIcons.RankIcon(StyleRank.Legendary));
        foreach (var r in Enum.GetValues<StyleRank>().Where(r => r != StyleRank.None))
            Assert.Contains(GameIcons.RankIcon(r)!, GameIcons.RankIcons);
    }

    // ---- fonts ------------------------------------------------------------------------------

    [Theory]
    [InlineData("ChakraPetch", "ChakraPetch-Bold.ttf")]
    [InlineData("ChakraPetch", "ChakraPetch-SemiBold.ttf")]
    [InlineData("Rajdhani", "Rajdhani-Medium.ttf")]
    [InlineData("Rajdhani", "Rajdhani-SemiBold.ttf")]
    [InlineData("Rajdhani", "Rajdhani-Bold.ttf")]
    public void FontFilesAreRealTrueTypeFiles(string family, string file)
    {
        byte[] head = File.ReadAllBytes(Path.Combine(Root, "game", "assets", "fonts", family, file)).Take(4).ToArray();
        Assert.True(head.SequenceEqual(new byte[] { 0, 1, 0, 0 }) || head.SequenceEqual("OTTO"u8.ToArray()) || head.SequenceEqual("true"u8.ToArray()), $"{file} is not a font");
    }

    [Theory]
    [InlineData("ChakraPetch", "Chakra Petch")]
    [InlineData("Rajdhani", "Indian Type Foundry")]
    public void EveryFontFamilyShipsItsOpenFontLicence(string family, string holder)
    {
        string licence = File.ReadAllText(Path.Combine(Root, "game", "assets", "fonts", family, "OFL.txt"));
        Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", licence);
        Assert.Contains(holder, licence);
    }

    [Fact]
    public void CreditsNameEveryFontAndExplainTheGlyphs()
    {
        string credits = File.ReadAllText(Path.Combine(Root, "game", "assets", "CREDITS.md"));
        Assert.Contains("Chakra Petch", credits);
        Assert.Contains("Rajdhani", credits);
        Assert.Contains("SIL Open Font License", credits);
        Assert.Contains("AssetGen", credits);
    }

    [Fact]
    public void ProjectIconIsSetAndExists()
    {
        string project = File.ReadAllText(Path.Combine(Root, "game", "project.godot"));
        Assert.Contains("config/icon=\"res://icon.svg\"", project);
        Assert.True(File.Exists(Path.Combine(Root, "game", "icon.svg")));
    }

    // ---- controller styles ------------------------------------------------------------------

    [Fact]
    public void EveryFaceButtonHasADistinctGlyphInEveryStyle()
    {
        var all = new List<string>();
        foreach (var style in Enum.GetValues<ControllerStyle>())
        {
            var ids = BindingGlyphs.FaceButtonCodes.Select(c => BindingGlyphs.IdOf(Binding.Button(c), style)).ToList();
            Assert.All(ids, id => Assert.NotNull(id));
            Assert.Equal(4, ids.Distinct().Count());
            all.AddRange(ids!);
        }
        Assert.Equal(12, all.Distinct().Count());
        Assert.All(all, id => Assert.Contains(id, GlyphSet.GlyphIds()));
    }

    [Theory]
    [InlineData("A", ControllerStyle.Xbox, "pad_a")]
    [InlineData("A", ControllerStyle.PlayStation, "ps_cross")]
    [InlineData("B", ControllerStyle.PlayStation, "ps_circle")]
    [InlineData("X", ControllerStyle.PlayStation, "ps_square")]
    [InlineData("Y", ControllerStyle.PlayStation, "ps_triangle")]
    [InlineData("A", ControllerStyle.Nintendo, "ns_b")] // the bottom button is labelled B on a Nintendo pad
    [InlineData("B", ControllerStyle.Nintendo, "ns_a")]
    [InlineData("X", ControllerStyle.Nintendo, "ns_y")]
    [InlineData("Y", ControllerStyle.Nintendo, "ns_x")]
    [InlineData("Start", ControllerStyle.PlayStation, "ps_options")]
    [InlineData("Back", ControllerStyle.Nintendo, "ns_minus")]
    [InlineData("LeftShoulder", ControllerStyle.PlayStation, "ps_l1")]
    [InlineData("RightShoulder", ControllerStyle.Nintendo, "ns_r")]
    public void ButtonsAreLabelledTheWayEachControllerLabelsThem(string code, ControllerStyle style, string expected)
    {
        Assert.Equal(expected, BindingGlyphs.IdOf(Binding.Button(code), style));
    }

    [Theory]
    [InlineData(ControllerStyle.PlayStation, "ps_l2", "ps_r2")]
    [InlineData(ControllerStyle.Nintendo, "ns_zl", "ns_zr")]
    [InlineData(ControllerStyle.Xbox, "pad_lt", "pad_rt")]
    public void TriggersFollowTheStyle(ControllerStyle style, string left, string right)
    {
        Assert.Equal(left, BindingGlyphs.IdOf(Binding.Axis("TriggerLeft", 1), style));
        Assert.Equal(right, BindingGlyphs.IdOf(Binding.Axis("TriggerRight", 1), style));
    }

    [Fact]
    public void SticksAndDpadLookTheSameOnEveryController()
    {
        foreach (var style in Enum.GetValues<ControllerStyle>())
        {
            Assert.Equal("pad_lstick_left", BindingGlyphs.IdOf(Binding.Axis("LeftX", -1), style));
            Assert.Equal("pad_dpad_up", BindingGlyphs.IdOf(Binding.Button("DpadUp"), style));
            Assert.Equal("pad_r3", BindingGlyphs.IdOf(Binding.Button("RightStick"), style));
        }
    }

    [Theory]
    [InlineData("Xbox Series X Controller", ControllerStyle.Xbox)]
    [InlineData("PS5 Controller", ControllerStyle.PlayStation)]
    [InlineData("DualSense Wireless Controller", ControllerStyle.PlayStation)]
    [InlineData("Sony Interactive Entertainment Wireless Controller", ControllerStyle.PlayStation)]
    [InlineData("Nintendo Switch Pro Controller", ControllerStyle.Nintendo)]
    [InlineData("Joy-Con (L)", ControllerStyle.Nintendo)]
    [InlineData("Some Generic USB Gamepad", ControllerStyle.Xbox)]
    [InlineData("", ControllerStyle.Xbox)]
    [InlineData(null, ControllerStyle.Xbox)]
    public void ControllerNamesAreRecognised(string? name, ControllerStyle expected)
    {
        Assert.Equal(expected, ControllerStyles.Detect(name));
    }

    [Fact]
    public void PromptStyleCanBeForcedOrFollowTheController()
    {
        Assert.Equal(ControllerStyle.PlayStation, ControllerStyles.Resolve(PromptStyle.Auto, "DualSense"));
        Assert.Equal(ControllerStyle.Xbox, ControllerStyles.Resolve(PromptStyle.Auto, null));
        Assert.Equal(ControllerStyle.Nintendo, ControllerStyles.Resolve(PromptStyle.Nintendo, "DualSense"));
        Assert.Equal(ControllerStyle.Xbox, ControllerStyles.Resolve(PromptStyle.Xbox, "DualSense"));
    }

    [Fact]
    public void PromptStyleSurvivesSavingAndBadValuesFallBackToAuto()
    {
        var s = new GameSettings { ControllerPrompts = PromptStyle.PlayStation };
        Assert.Equal(PromptStyle.PlayStation, GameSettings.FromJson(s.ToJson()).ControllerPrompts);
        Assert.Equal(PromptStyle.Auto, GameSettings.FromJson("{\"controllerPrompts\":\"Atari\"}").ControllerPrompts);
        Assert.Equal(PromptStyle.Auto, GameSettings.FromJson("{\"controllerPrompts\":7}").ControllerPrompts);
        s.ResetToDefaults();
        Assert.Equal(PromptStyle.Auto, s.ControllerPrompts);
    }

    [Fact]
    public void PortalsHaveTheirOwnIdentity()
    {
        // The portals are teal and violet gates, not blue and orange ovals.
        Assert.Contains("icon_portal_teal", GameIcons.All);
        Assert.Contains("icon_portal_violet", GameIcons.All);
        string teal = File.ReadAllText(Path.Combine(Root, "game", "assets", "glyphs", "icon_portal_teal.svg"));
        Assert.DoesNotContain("<ellipse", teal);
        Assert.Contains(GlyphSet.Teal, teal);
        Assert.Contains(GlyphSet.Violet, File.ReadAllText(Path.Combine(Root, "game", "assets", "glyphs", "icon_portal_violet.svg")));
    }

    [Theory]
    [InlineData("A", ControllerStyle.Xbox, "A")]
    [InlineData("A", ControllerStyle.PlayStation, "Cross")]
    [InlineData("Y", ControllerStyle.PlayStation, "Triangle")]
    [InlineData("A", ControllerStyle.Nintendo, "B")]
    [InlineData("Start", ControllerStyle.PlayStation, "Options")]
    [InlineData("RightShoulder", ControllerStyle.PlayStation, "R1")]
    [InlineData("RightShoulder", ControllerStyle.Xbox, "RB")]
    public void ButtonNamesFollowTheControllerStyle(string code, ControllerStyle style, string expected)
    {
        Assert.Equal(expected, BindingNames.Display(Binding.Button(code), style));
    }

    [Fact]
    public void TriggerNamesFollowTheControllerStyle()
    {
        Assert.Equal("L2", BindingNames.Display(Binding.Axis("TriggerLeft", 1), ControllerStyle.PlayStation));
        Assert.Equal("ZR", BindingNames.Display(Binding.Axis("TriggerRight", 1), ControllerStyle.Nintendo));
        Assert.Equal("RT", BindingNames.Display(Binding.Axis("TriggerRight", 1)));
    }

    [Fact]
    public void CommittedStaticAssetsMatchTheGenerator()
    {
        foreach (var (path, bytes) in Hollowfall.Core.Assets.StaticAssets.All())
        {
            string full = Path.Combine(Root, path);
            Assert.True(File.Exists(full), $"{path} is missing; run the asset generator");
            if (path.EndsWith(".png"))
                Assert.True(DecodePixels(File.ReadAllBytes(full)).AsSpan().SequenceEqual(DecodePixels(bytes)), $"{path} is out of date; run the asset generator");
            else
                Assert.True(File.ReadAllBytes(full).AsSpan().SequenceEqual(bytes), $"{path} is out of date; run the asset generator");
        }
    }

    /// <summary>The pixels of an 8-bit RGBA, unfiltered PNG. zlib builds differ in compressed bytes, not in pixels.</summary>
    private static byte[] DecodePixels(byte[] png)
    {
        using var data = new MemoryStream();
        int at = 8;
        while (at < png.Length)
        {
            int length = (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
            string type = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            if (type == "IDAT") data.Write(png, at + 8, length);
            at += 12 + length;
        }
        data.Position = 0;
        using var inflated = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(data, System.IO.Compression.CompressionMode.Decompress)) z.CopyTo(inflated);
        return inflated.ToArray();
    }

    [Fact]
    public void NoStrayFilesSitInTheAudioFolder()
    {
        var expected = Hollowfall.Core.Assets.StaticAssets.All()
            .Select(f => Path.GetFileName(f.Path))
            .Where(n => n.EndsWith(".wav"))
            .ToHashSet();
        var actual = Directory.EnumerateFiles(Path.Combine(Root, "game", "assets", "audio"), "*.wav").Select(Path.GetFileName).ToHashSet();
        Assert.Empty(actual.Except(expected));
        Assert.Empty(expected.Except(actual));
    }

    [Fact]
    public void NoStrayFilesSitInTheModelFolder()
    {
        var expected = Hollowfall.Core.Assets.StaticAssets.All()
            .Select(f => Path.GetFileName(f.Path))
            .Where(n => n.EndsWith(".glb"))
            .ToHashSet();
        var actual = Directory.EnumerateFiles(Path.Combine(Root, "game", "assets", "models"), "*.glb").Select(Path.GetFileName).ToHashSet();
        Assert.Empty(actual.Except(expected));
        Assert.Empty(expected.Except(actual));
    }
}
