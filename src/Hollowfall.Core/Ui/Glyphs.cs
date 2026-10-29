using Hollowfall.Core.Combat;
using Hollowfall.Core.Input;
using Hollowfall.Core.Progression;

namespace Hollowfall.Core.Ui;

/// <summary>
/// Picks the image to draw for a binding or a game concept. Ids are file names (without extension) of the SVGs in
/// <c>game/assets/glyphs</c>, which <c>tools/Hollowfall.AssetGen</c> generates. Keyboard keys have no image: they are
/// drawn as a keycap with the key's name, so any key works without needing a file for it.
/// </summary>
public static class BindingGlyphs
{
    /// <summary>The glyph id for a gamepad or mouse input, or null for keyboard keys and anything without one.</summary>
    public static string? IdOf(Binding? binding, ControllerStyle style = ControllerStyle.Xbox)
    {
        if (binding is not { } b) return null;
        switch (b.Device)
        {
            case InputDevice.Mouse:
                return b.Code switch
                {
                    "Left" => "mouse_left",
                    "Right" => "mouse_right",
                    "Middle" => "mouse_middle",
                    "WheelUp" => "mouse_wheel_up",
                    "WheelDown" => "mouse_wheel_down",
                    _ => null,
                };

            case InputDevice.GamepadButton:
                return StyledButton(b.Code, style) ?? b.Code switch
                {
                    "A" => "pad_a",
                    "B" => "pad_b",
                    "X" => "pad_x",
                    "Y" => "pad_y",
                    "LeftShoulder" => "pad_lb",
                    "RightShoulder" => "pad_rb",
                    "Start" => "pad_start",
                    "Back" => "pad_back",
                    "LeftStick" => "pad_l3",
                    "RightStick" => "pad_r3",
                    "DpadUp" => "pad_dpad_up",
                    "DpadDown" => "pad_dpad_down",
                    "DpadLeft" => "pad_dpad_left",
                    "DpadRight" => "pad_dpad_right",
                    _ => null,
                };

            case InputDevice.GamepadAxis:
                if (b.Code == "TriggerLeft") return style switch { ControllerStyle.PlayStation => "ps_l2", ControllerStyle.Nintendo => "ns_zl", _ => "pad_lt" };
                if (b.Code == "TriggerRight") return style switch { ControllerStyle.PlayStation => "ps_r2", ControllerStyle.Nintendo => "ns_zr", _ => "pad_rt" };
                string? side = b.Code.StartsWith("Left") ? "l" : b.Code.StartsWith("Right") ? "r" : null;
                if (side == null) return null;
                bool horizontal = b.Code.EndsWith("X");
                bool vertical = b.Code.EndsWith("Y");
                if (!horizontal && !vertical) return null;
                string dir = horizontal ? (b.AxisSign < 0 ? "left" : "right") : (b.AxisSign < 0 ? "up" : "down");
                return $"pad_{side}stick_{dir}";

            default:
                return null;
        }
    }

    /// <summary>
    /// Face buttons, shoulders and menu buttons that look different on other controllers. Codes are the engine's positional
    /// names, so on a Nintendo pad the bottom button ("A") shows as B and the right one ("B") shows as A.
    /// </summary>
    private static string? StyledButton(string code, ControllerStyle style) => style switch
    {
        ControllerStyle.PlayStation => code switch
        {
            "A" => "ps_cross",
            "B" => "ps_circle",
            "X" => "ps_square",
            "Y" => "ps_triangle",
            "LeftShoulder" => "ps_l1",
            "RightShoulder" => "ps_r1",
            "Start" => "ps_options",
            "Back" => "ps_share",
            _ => null,
        },
        ControllerStyle.Nintendo => code switch
        {
            "A" => "ns_b",
            "B" => "ns_a",
            "X" => "ns_y",
            "Y" => "ns_x",
            "LeftShoulder" => "ns_l",
            "RightShoulder" => "ns_r",
            "Start" => "ns_plus",
            "Back" => "ns_minus",
            _ => null,
        },
        _ => null,
    };

    /// <summary>Every glyph id <see cref="IdOf"/> can return, so tests can check each has a file.</summary>
    public static IReadOnlyList<string> AllIds { get; } = BuildAll();

    /// <summary>The four face buttons by position (bottom, right, left, top), for any controller style.</summary>
    public static readonly string[] FaceButtonCodes = ["A", "B", "X", "Y"];

    private static List<string> BuildAll()
    {
        var ids = new HashSet<string>();
        foreach (var code in new[] { "Left", "Right", "Middle", "WheelUp", "WheelDown" }) ids.Add(IdOf(Binding.Mouse(code))!);
        foreach (var style in Enum.GetValues<ControllerStyle>())
        {
            foreach (var code in new[] { "A", "B", "X", "Y", "LeftShoulder", "RightShoulder", "Start", "Back", "LeftStick", "RightStick", "DpadUp", "DpadDown", "DpadLeft", "DpadRight" })
                ids.Add(IdOf(Binding.Button(code), style)!);
            foreach (var code in new[] { "TriggerLeft", "TriggerRight" }) ids.Add(IdOf(Binding.Axis(code, 1), style)!);
        }
        foreach (var code in new[] { "LeftX", "LeftY", "RightX", "RightY" })
            foreach (var sign in new[] { -1, 1 })
                ids.Add(IdOf(Binding.Axis(code, sign))!);
        return [.. ids.Order()];
    }
}

/// <summary>Ids of the game icons (HUD and menus). Each is a file in <c>game/assets/glyphs</c>.</summary>
public static class GameIcons
{
    public const string PortalTeal = "icon_portal_teal";
    public const string PortalViolet = "icon_portal_violet";
    public const string Revolver = "icon_revolver";
    public const string Shotgun = "icon_shotgun";
    public const string Parry = "icon_parry";
    public const string Health = "icon_health";
    public const string Blood = "icon_blood";
    public const string Style = "icon_style";
    public const string Skull = "icon_skull";
    public const string Time = "icon_time";
    public const string Kills = "icon_kills";
    public const string Stairs = "icon_stairs";
    public const string Key = "icon_key";
    public const string Cream = "icon_cream";
    public const string Charm = "icon_charm";
    public const string Coin = "icon_coin";
    public const string Relic = "icon_relic";

    public static IReadOnlyList<string> All { get; } =
        [PortalTeal, PortalViolet, Revolver, Shotgun, Parry, Health, Blood, Style, Skull, Time, Kills, Stairs, Key, Cream, Charm, Coin, Relic];

    /// <summary>The glyph that stands for an item in the HUD and the bag.</summary>
    public static string ForItem(ItemKind kind) => kind switch
    {
        ItemKind.BrownieCream => Cream,
        ItemKind.ThornCharm => Charm,
        ItemKind.CleansingDrop => Health,
        ItemKind.VialOfBlood => Blood,
        ItemKind.StyleShard => Style,
        ItemKind.BloodlustDraught => Blood,
        ItemKind.OpenGuardTonic => Parry,
        ItemKind.WardenSeal or ItemKind.HareFoot => Relic,
        _ => Cream,
    };

    /// <summary>The chevron emblem for a style rank (one chevron for the lowest, eight for the highest), or null for no rank.</summary>
    public static string? RankIcon(StyleRank rank) => rank == StyleRank.None ? null : $"rank_{(int)rank}";

    public static IReadOnlyList<string> RankIcons { get; } = Enumerable.Range(1, 8).Select(i => $"rank_{i}").ToList();
}
