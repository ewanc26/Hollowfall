using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
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
    private Control _crossCluster = null!;
    private MiniMap _map = null!;
    private Label _floorStairs = null!, _floorKills = null!, _floorTime = null!, _floorKeys = null!;
    private PanelContainer _floorPanel = null!;
    private ShaderMaterial _vignette = null!;
    private readonly List<Control> _avoid = [];
    private readonly List<Rect2> _openings = [];
    private bool _debug;
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

        // Damage and parry flashes are a vignette: strongest at the screen edges and fully clear in the middle, so they
        // never tint what is in view, including the picture inside a portal.
        _vignette = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = """
                    shader_type canvas_item;
                    uniform vec4 tint : source_color = vec4(1.0, 0.0, 0.0, 0.0);
                    void fragment() {
                        vec2 d = (UV - vec2(0.5)) * vec2(1.0, 0.8);
                        float edge = smoothstep(0.28, 0.78, length(d));
                        COLOR = vec4(tint.rgb, tint.a * edge);
                    }
                    """,
            },
        };
        _flash = new ColorRect { Color = Colors.White, Material = _vignette, MouseFilter = Control.MouseFilterEnum.Ignore };
        _flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_flash);

        _info = Text(root, new Vector2(12, 8), 16);
        _info.Visible = false;

        // Top-right: where you are in the dungeon and how the floor is going.
        _floorPanel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _floorPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 14);
        _floorPanel.GrowHorizontal = Control.GrowDirection.Begin;
        _floorPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.04f, 0.06f, 0.62f),
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginLeft = 12,
            ContentMarginRight = 14,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        });
        var floorRows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        floorRows.AddThemeConstantOverride("separation", 3);
        _floorPanel.AddChild(floorRows);
        _floorStairs = FloorRow(floorRows, GameIcons.Stairs);
        _floorKills = FloorRow(floorRows, GameIcons.Kills);
        _floorTime = FloorRow(floorRows, GameIcons.Time);
        _floorKeys = FloorRow(floorRows, GameIcons.Key);
        root.AddChild(_floorPanel);

        // The map hangs under the floor panel in the same corner.
        _map = new MiniMap { Visible = false };
        _map.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 14);
        _map.GrowHorizontal = Control.GrowDirection.Begin;
        _map.OffsetTop += 168;
        _map.OffsetBottom += 168;
        root.AddChild(_map);

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
        _avoid.Add(stack);
        _avoid.Add(_floorPanel);
        _avoid.Add(_map);
        _avoid.Add(_info);

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
        _crossCluster = cross;
        _avoid.Add(cross);
        _hitMarker = Text(crossCentre, Vector2.Zero, 34);
        _hitMarker.Text = "×";
        _hitMarker.SelfModulate = new Color(1, 1, 1, 0);
        _avoid.Add(_hitMarker);

        var bannerTop = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bannerTop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide, Control.LayoutPresetMode.Minsize, 0);
        bannerTop.OffsetTop = 130;
        root.AddChild(bannerTop);
        _banner = Text(bannerTop, Vector2.Zero, 40);
        _banner.HorizontalAlignment = HorizontalAlignment.Center;
        _banner.AddThemeFontOverride("font", UiFonts.Display);
        _avoid.Add(_banner);
        _avoid.Add(_awards);

        var resultCentre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        resultCentre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(resultCentre);
        _result = new PanelContainer { CustomMinimumSize = new Vector2(420, 0), Visible = false };
        _result.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.03f, 0.03f, 0.05f, 0.92f), BorderColor = new Color(1f, 0.8f, 0.3f), BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2, ContentMarginBottom = 14, ContentMarginTop = 14, ContentMarginLeft = 20, ContentMarginRight = 20 });
        _resultText = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _resultText.AddThemeFontSizeOverride("font_size", 24);
        _result.AddChild(_resultText);
        resultCentre.AddChild(_result);
        _avoid.Add(_result);
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

    private static Label FloorRow(Control parent, string icon)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(Glyphs.Icon(icon, 22));
        var label = Text(row, Vector2.Zero, 22);
        label.AddThemeFontOverride("font", UiFonts.DisplayMedium);
        parent.AddChild(row);
        return label;
    }

    /// <summary>The debug overlay (position, speed, frame rate) is off unless asked for, to keep the screen clear.</summary>
    public void SetDebug(bool on)
    {
        _debug = on;
        _info.Visible = on;
    }

    public bool DebugOn => _debug;

    /// <summary>How visible the crosshair is (it fades while it would sit on a portal opening). For tests.</summary>
    public float CrosshairAlpha => _crossCluster.Modulate.A;

    /// <summary>Shows the dungeon progress panel, or hides it on the surface.</summary>
    public void SetFloorInfo(int floor, int floors, int kills, int enemies, double elapsed, double par, int keys = 0)
    {
        _floorPanel.Visible = floor >= 0;
        if (floor < 0) return;
        _floorStairs.Text = $"Floor {floor + 1} / {floors}";
        _floorKills.Text = $"{kills} / {enemies}";
        _floorTime.Text = $"{Clock(elapsed)}  /  {Clock(par)}";
        _floorKeys.Text = keys == 1 ? "1 key" : $"{keys} keys";
        _floorKeys.GetParent<Control>().Visible = keys > 0;
        _floorTime.AddThemeColorOverride("font_color", elapsed <= par ? Colors.White : new Color(1f, 0.55f, 0.45f));
    }

    /// <summary>Shows what the player has seen of the floor, or hides the map on the surface.</summary>
    public void SetMap(FloorMap? map, int floor, Vector2 cell, float yaw)
    {
        _map.Visible = map != null && floor >= 0;
        if (_map.Visible) _map.Show(map!, floor, cell, yaw);
    }

    private static string Clock(double seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:D2}";

    /// <summary>
    /// Tells the HUD where portal openings are on screen. Anything that would be drawn over one fades out, so the view
    /// through a portal is never covered by the HUD.
    /// </summary>
    public void SetOpenings(IReadOnlyList<Rect2> screenRects)
    {
        _openings.Clear();
        _openings.AddRange(screenRects.Select(r => r.Grow(10)));
    }

    private void FadeOverOpenings(double delta)
    {
        foreach (var control in _avoid)
        {
            bool covers = control.Visible && _openings.Any(o => o.Intersects(control.GetGlobalRect()));
            float target = covers ? 0f : 1f;
            float a = Mathf.MoveToward(control.Modulate.A, target, (float)(delta * 10));
            control.Modulate = new Color(1, 1, 1, a);
        }
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
            _vignette.SetShaderParameter("tint", new Color(_flashColour, (float)(_flashColour.A * Math.Max(0, _flashTime / _flashTotal))));
        }
        if (_hitTime > 0)
        {
            _hitTime -= delta;
            _hitMarker.SelfModulate = new Color(1, 1, 1, _hitTime > 0 ? 1f : 0f);
        }
        FadeOverOpenings(delta);
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
