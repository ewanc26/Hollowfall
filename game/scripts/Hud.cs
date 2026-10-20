using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Portals;

namespace Hollowfall.Game;

/// <summary>Health with hard damage, the style meter, recent bonuses, portal status and floor results.</summary>
public partial class Hud : CanvasLayer
{
    private const float BarWidth = 320;

    private ColorRect _healthFill = null!, _hardFill = null!, _styleFill = null!;
    private Label _info = null!, _style = null!, _awards = null!, _banner = null!, _crosshair = null!;
    private double _bannerTime;

    public override void _Ready()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _info = Text(root, new Vector2(12, 8), 16);

        // Health: red fill, with the hard-damage ceiling shown in orange at the right end.
        Bar(root, new Vector2(12, 650), new Vector2(BarWidth, 18), new Color(0.12f, 0.05f, 0.05f, 0.85f));
        _hardFill = Bar(root, new Vector2(12, 650), new Vector2(0, 18), new Color(0.95f, 0.6f, 0.1f, 0.9f));
        _healthFill = Bar(root, new Vector2(12, 650), new Vector2(BarWidth, 18), new Color(0.85f, 0.12f, 0.12f, 0.95f));

        _style = Text(root, new Vector2(12, 560), 22);
        Bar(root, new Vector2(12, 592), new Vector2(BarWidth, 8), new Color(0.1f, 0.1f, 0.1f, 0.8f));
        _styleFill = Bar(root, new Vector2(12, 592), new Vector2(0, 8), new Color(1f, 0.85f, 0.2f, 0.95f));
        _awards = Text(root, new Vector2(12, 440), 15);

        _crosshair = Text(root, new Vector2(572, 330), 28);
        _crosshair.Text = "+";

        _banner = Text(root, new Vector2(340, 190), 40);
        _banner.HorizontalAlignment = HorizontalAlignment.Center;
        _banner.CustomMinimumSize = new Vector2(600, 0);
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

    public void Update(double delta, CombatSim sim, PortalSystem portals, string info)
    {
        _info.Text = info;

        var v = sim.Vitals;
        _healthFill.Size = new Vector2((float)(BarWidth * v.Health / PlayerVitals.MaxHealth), 18);
        float ceiling = (float)(BarWidth * v.Ceiling / PlayerVitals.MaxHealth);
        _hardFill.Position = new Vector2(12 + ceiling, 650);
        _hardFill.Size = new Vector2(BarWidth - ceiling, 18);

        var style = sim.Style;
        _style.Text = style.Rank == StyleRank.None ? "" : style.Rank.ToString().ToUpperInvariant();
        _styleFill.Size = new Vector2((float)(BarWidth * (style.Rank == StyleRank.None ? style.Points / 150.0 : style.RankProgress)), 8);
        _awards.Text = string.Join("\n", style.RecentAwards.TakeLast(5).Reverse().Select(a => $"+{a.Points:F0} {a.Bonus}"));

        // Crosshair colour shows which portals exist: blue and orange halves light up as they are placed.
        _crosshair.Text = (portals.Blue != null ? "◖" : "·") + "+" + (portals.Orange != null ? "◗" : "·");
        _crosshair.AddThemeColorOverride("font_color", portals.Linked ? new Color(0.9f, 1f, 0.9f) : Colors.White);

        if (_bannerTime > 0)
        {
            _bannerTime -= delta;
            if (_bannerTime <= 0) _banner.Text = "";
        }
    }
}
