using Godot;
using Hollowfall.Core.Input;
using Hollowfall.Core.Ui;

namespace Hollowfall.Game;

/// <summary>The game's two fonts: Chakra Petch for titles and Rajdhani for everything else (both SIL OFL, see assets/CREDITS.md).</summary>
public static class UiFonts
{
    private static Font? _display, _displayMedium, _body, _bodyBold;

    /// <summary>Angular, heavy: the title, menu headings and rank names.</summary>
    public static Font Display => _display ??= Load("ChakraPetch/ChakraPetch-Bold.ttf");

    public static Font DisplayMedium => _displayMedium ??= Load("ChakraPetch/ChakraPetch-SemiBold.ttf");

    /// <summary>Condensed and clear at small sizes: menus, hints and the HUD.</summary>
    public static Font Body => _body ??= Load("Rajdhani/Rajdhani-SemiBold.ttf");

    public static Font BodyBold => _bodyBold ??= Load("Rajdhani/Rajdhani-Bold.ttf");

    private static Font Load(string relative)
    {
        // A missing or un-imported file must never break the game: fall back to the engine's default font.
        string path = $"res://assets/fonts/{relative}";
        return ResourceLoader.Exists(path) && ResourceLoader.Load(path) is Font font ? font : ThemeDB.FallbackFont;
    }

    /// <summary>A theme whose default font is Rajdhani, to put on the root of a UI tree.</summary>
    public static Theme BodyTheme(int size = 20) => new() { DefaultFont = Body, DefaultFontSize = size };
}

/// <summary>Loads the vector glyphs and builds the pieces that show a binding: a glyph image, or a keycap for a key.</summary>
public static class Glyphs
{
    private static readonly Dictionary<string, Texture2D?> Cache = [];

    public static Texture2D? Get(string? id)
    {
        if (id == null) return null;
        if (Cache.TryGetValue(id, out var cached)) return cached;
        string path = $"res://assets/glyphs/{id}.svg";
        return Cache[id] = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    public static Texture2D? ForBinding(Binding? b, ControllerStyle style = ControllerStyle.Xbox) => Get(BindingGlyphs.IdOf(b, style));

    /// <summary>A square image of an icon or glyph, or a blank spacer if the file is missing.</summary>
    public static Control Icon(string id, int size, Color? tint = null)
    {
        var rect = new TextureRect
        {
            Texture = Get(id),
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (tint is { } t) rect.Modulate = t;
        return rect;
    }

    /// <summary>What to draw for a binding: its glyph, or for a key a keycap with its name.</summary>
    public static Control Prompt(Binding? binding, int size, ControllerStyle style = ControllerStyle.Xbox)
    {
        if (ForBinding(binding, style) is { } tex)
            return new TextureRect
            {
                Texture = tex,
                CustomMinimumSize = new Vector2(size, size),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
        return KeyCap(BindingNames.Display(binding), size);
    }

    public static Control KeyCap(string text, int height)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.17f, 0.18f, 0.23f),
            BorderColor = new Color(0.79f, 0.8f, 0.85f),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 4,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            ContentMarginLeft = 7,
            ContentMarginRight = 7,
            ContentMarginTop = 1,
            ContentMarginBottom = 1,
        };
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(height, height), MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", style);
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", UiFonts.BodyBold);
        label.AddThemeFontSizeOverride("font_size", Math.Max(10, height * 5 / 8));
        label.AddThemeColorOverride("font_color", new Color(0.93f, 0.93f, 0.97f));
        panel.AddChild(label);
        return panel;
    }
}

/// <summary>A row of "[button] label" prompts, such as the controls hint along the bottom of the HUD.</summary>
public partial class HintBar : HBoxContainer
{
    private string _signature = "";

    public HintBar()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 16);
    }

    /// <summary>Rebuilds the row only when it changed, so calling this every frame is cheap.</summary>
    public void Set(IReadOnlyList<(Binding? Binding, string Label)> hints, ControllerStyle style, int size = 22)
    {
        string signature = style + "#" + string.Join("|", hints.Select(h => $"{h.Binding}:{h.Label}"));
        if (signature == _signature) return;
        _signature = signature;

        foreach (var child in GetChildren()) child.QueueFree();
        foreach (var (binding, label) in hints)
        {
            var pair = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            pair.AddThemeConstantOverride("separation", 5);
            pair.AddChild(Glyphs.Prompt(binding, size, style));
            var text = new Label { Text = label, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            text.AddThemeFontSizeOverride("font_size", size - 4);
            text.AddThemeColorOverride("font_color", new Color(0.93f, 0.93f, 0.97f));
            text.AddThemeColorOverride("font_shadow_color", Colors.Black);
            text.AddThemeConstantOverride("shadow_offset_x", 1);
            text.AddThemeConstantOverride("shadow_offset_y", 1);
            pair.AddChild(text);
            AddChild(pair);
        }
    }
}
