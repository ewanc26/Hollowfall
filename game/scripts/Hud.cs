using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Input;
using Hollowfall.Core.Ui;
using Hollowfall.Core.Portals;

namespace Hollowfall.Game;

/// <summary>
/// Health with hard damage, the style meter and recent bonuses, the crosshair with portal status and hit markers, a damage
/// and parry flash, controls hints that follow the device you are using, and the end-of-floor result panel.
/// </summary>
public partial class Hud : CanvasLayer
{
    private const float BarWidth = 320;

    private ColorRect _healthFill = null!, _hardFill = null!, _styleFill = null!, _styleBack = null!, _flash = null!;
    private Label _info = null!, _style = null!, _awards = null!, _banner = null!, _hitMarker = null!, _health = null!;
    private HintBar _hint = null!;
    private Control _tealMark = null!, _violetMark = null!;
    private TextureRect _rankIcon = null!;
    private PanelContainer _result = null!;
    private Label _resultText = null!;
    private double _bannerTime, _flashTime, _flashTotal, _hitTime, _resultTime;
    private Color _flashColour;

    public override void _Ready()
    {
        Layer = 5;
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiFonts.BodyTheme() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _flash = new ColorRect { Color = new Color(1, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        _flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_flash);

        _info = Text(root, new Vector2(12, 8), 16);

        // Bottom-left stack: bonuses, style meter, health bar and the controls hint. Everything is anchored, so it
        // stays put whatever the window size.
        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 4);
        stack.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.Minsize, 12);
        stack.GrowVertical = Control.GrowDirection.Begin;
        root.AddChild(stack);

        _awards = Text(stack, Vector2.Zero, 15);
        var styleRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        styleRow.AddThemeConstantOverride("separation", 8);
        stack.AddChild(styleRow);
        _rankIcon = new TextureRect { CustomMinimumSize = new Vector2(30, 30), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
        styleRow.AddChild(_rankIcon);
        _style = Text(styleRow, Vector2.Zero, 26);
        _style.AddThemeFontOverride("font", UiFonts.Display);
        var styleBar = BarGroup(stack, 8);
        _styleBack = styleBar.Back;
        _styleFill = styleBar.Fill;

        var healthRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        healthRow.AddThemeConstantOverride("separation", 10);
        stack.AddChild(healthRow);
        var healthBar = BarGroup(healthRow, 18);
        _healthFill = healthBar.Fill;
        _hardFill = healthBar.Extra;
        healthRow.AddChild(Glyphs.Icon(GameIcons.Health, 24));
        _health = Text(healthRow, Vector2.Zero, 24);
        _health.AddThemeFontOverride("font", UiFonts.DisplayMedium);

        _hint = new HintBar();
        stack.AddChild(_hint);

        // Centre: crosshair, hit marker and the banner.
        var crossCentre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        crossCentre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(crossCentre);
        var cross = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        cross.AddThemeConstantOverride("separation", 4);
        _tealMark = Glyphs.Icon(GameIcons.PortalTeal, 22);
        _violetMark = Glyphs.Icon(GameIcons.PortalViolet, 22);
        var plus = Text(cross, Vector2.Zero, 30);
        plus.Text = "+";
        cross.AddChild(_tealMark);
        cross.MoveChild(_tealMark, 0);
        cross.AddChild(_violetMark);
        crossCentre.AddChild(cross);
        _hitMarker = Text(crossCentre, Vector2.Zero, 34);
        _hitMarker.Text = "×";
        _hitMarker.Modulate = new Color(1, 1, 1, 0);

        var bannerTop = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bannerTop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide, Control.LayoutPresetMode.Minsize, 0);
        bannerTop.OffsetTop = 130;
        root.AddChild(bannerTop);
        _banner = Text(bannerTop, Vector2.Zero, 40);
        _banner.HorizontalAlignment = HorizontalAlignment.Center;

        var resultCentre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        resultCentre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(resultCentre);
        _result = new PanelContainer { CustomMinimumSize = new Vector2(420, 0), Visible = false };
        _result.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.03f, 0.03f, 0.05f, 0.92f), BorderColor = new Color(1f, 0.8f, 0.3f), BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2, ContentMarginBottom = 14, ContentMarginTop = 14, ContentMarginLeft = 20, ContentMarginRight = 20 });
        _resultText = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _resultText.AddThemeFontSizeOverride("font_size", 24);
        _result.AddChild(_resultText);
        resultCentre.AddChild(_result);
    }

    /// <summary>A bar made of a dark back, an optional extra fill (hard damage) and the main fill, all sized by the HUD.</summary>
    private static (Control Group, ColorRect Back, ColorRect Extra, ColorRect Fill) BarGroup(Control parent, float height)
    {
        var group = new Control { CustomMinimumSize = new Vector2(BarWidth, height), MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(group);
        var back = Bar(group, Vector2.Zero, new Vector2(BarWidth, height), new Color(0.12f, 0.08f, 0.08f, 0.85f));
        var extra = Bar(group, Vector2.Zero, new Vector2(0, height), new Color(0.95f, 0.6f, 0.1f, 0.9f));
        var fill = Bar(group, Vector2.Zero, new Vector2(BarWidth, height), new Color(0.85f, 0.12f, 0.12f, 0.95f));
        return (group, back, extra, fill);
    }

    private static Label Text(Control parent, Vector2 pos, int size)
    {
        var l = new Label { Position = pos };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", Colors.White);
        l.AddThemeColorOverride("font_shadow_color", Colors.Black);
        l.AddThemeConstantOverride("shadow_offset_x", 1);
        l.AddThemeConstantOverride("shadow_offset_y", 1);
        parent.AddChild(l);
        return l;
    }

    private static ColorRect Bar(Control parent, Vector2 pos, Vector2 size, Color colour)
    {
        var r = new ColorRect { Position = pos, Size = size, Color = colour, MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(r);
        return r;
    }

    public void ShowBanner(string text, double seconds)
    {
        _banner.Text = text;
        _bannerTime = seconds;
    }

    private void Flash(Color colour, double seconds)
    {
        _flashColour = colour;
        _flashTime = _flashTotal = seconds;
    }

    /// <summary>Reacts to what happened in combat: flashes, hit markers and the floor result panel.</summary>
    public void OnEvent(CombatEvent e)
    {
        switch (e)
        {
            case PlayerHurt hurt:
                Flash(new Color(0.9f, 0.05f, 0.05f, Math.Clamp((float)(0.25 + hurt.Damage / 60), 0.25f, 0.6f)), 0.35);
                break;
            case Parried:
                Flash(new Color(0.5f, 0.85f, 1f, 0.45f), 0.25);
                break;
            case EnemyHit hit:
                _hitTime = 0.12;
                _hitMarker.AddThemeColorOverride("font_color", hit.Killed ? new Color(1f, 0.25f, 0.2f) : hit.Zone == HitZone.Head ? new Color(1f, 0.9f, 0.2f) : Colors.White);
                break;
            case FloorCompleted done:
                _resultText.Text =
                    $"FLOOR {done.Floor + 1} COMPLETE\n\nRANK  {done.Result.Overall}\n" +
                    $"Time {done.Result.Time}    Kills {done.Result.Kills}    Style {done.Result.Style}" +
                    (done.Result.Restarts > 0 ? $"\nRestarts {done.Result.Restarts}  (−{done.Result.Restarts} points)" : "");
                _result.Visible = true;
                _resultTime = 7;
                break;
        }
    }

    public void Update(double delta, CombatSim sim, PortalSystem portals, string info, IReadOnlyList<(Binding? Binding, string Label)> hints, ControllerStyle prompts)
    {
        _info.Text = info;
        _hint.Set(hints, prompts);

        var v = sim.Vitals;
        _healthFill.Size = new Vector2((float)(BarWidth * v.Health / PlayerVitals.MaxHealth), 18);
        float ceiling = (float)(BarWidth * v.Ceiling / PlayerVitals.MaxHealth);
        _hardFill.Position = new Vector2(ceiling, 0);
        _hardFill.Size = new Vector2(BarWidth - ceiling, 18);
        _health.Text = $"{Math.Ceiling(v.Health):F0}";

        var style = sim.Style;
        _style.Text = style.Rank == StyleRank.None ? "" : style.Rank.ToString().ToUpperInvariant();
        _style.AddThemeColorOverride("font_color", RankColour(style.Rank));
        _styleFill.Color = RankColour(style.Rank);
        _styleFill.Size = new Vector2((float)(BarWidth * (style.Rank == StyleRank.None ? style.Points / 150.0 : style.RankProgress)), 8);
        _awards.Text = string.Join("\n", style.RecentAwards.TakeLast(5).Reverse().Select(a => $"+{a.Points:F0} {a.Bonus}"));

        // The crosshair shows which portals exist: each half lights up as its portal is placed.
        _tealMark.Modulate = new Color(1, 1, 1, portals.Teal != null ? 1f : 0.22f);
        _violetMark.Modulate = new Color(1, 1, 1, portals.Violet != null ? 1f : 0.22f);
        _rankIcon.Texture = Glyphs.Get(GameIcons.RankIcon(style.Rank));

        if (_flashTime > 0)
        {
            _flashTime -= delta;
            _flash.Color = new Color(_flashColour, (float)(_flashColour.A * Math.Max(0, _flashTime / _flashTotal)));
        }
        if (_hitTime > 0)
        {
            _hitTime -= delta;
            _hitMarker.Modulate = new Color(1, 1, 1, _hitTime > 0 ? 1f : 0f);
        }
        if (_bannerTime > 0)
        {
            _bannerTime -= delta;
            if (_bannerTime <= 0) _banner.Text = "";
        }
        if (_resultTime > 0)
        {
            _resultTime -= delta;
            if (_resultTime <= 0) _result.Visible = false;
        }
    }

    private static Color RankColour(StyleRank r) => r switch
    {
        >= StyleRank.Legendary => new Color(1f, 0.2f, 0.9f),
        >= StyleRank.Relentless => new Color(1f, 0.3f, 0.2f),
        >= StyleRank.Savage => new Color(1f, 0.55f, 0.1f),
        >= StyleRank.Brutal => new Color(1f, 0.8f, 0.2f),
        _ => new Color(1f, 0.9f, 0.5f),
    };
}
