using System.Globalization;
using System.Text;

namespace Hollowfall.AssetGen;

/// <summary>
/// Draws every vector asset the game uses: controller, mouse and keyboard prompts, HUD icons, style-rank emblems, the app
/// icon and the README banner. Output is deterministic, so a test can check the committed files are up to date. Everything
/// is original line art made of paths (no text elements), because the engine's SVG importer does not draw text and
/// because that keeps the set free of anyone else's licence.
/// </summary>
public static class GlyphSet
{
    // Palette. Dark UI chrome, light ink, and the colours the game already uses for portals and rank.
    public const string Ink = "#e8e8f0";
    public const string Dim = "#c9ccd9";
    public const string Panel = "#2c2f3b";
    public const string Deep = "#15161c";
    public const string Amber = "#ffb830";
    public const string Orange = "#ff8c1a";
    public const string Teal = "#19d3c5";
    public const string Violet = "#a24bff";
    public const string Red = "#e5484d";
    public const string Green = "#3ec46d";
    public const string Yellow = "#f5c542";

    /// <summary>Every file as (path relative to the repository root, contents).</summary>
    public static IReadOnlyList<(string Path, string Content)> All()
    {
        var files = new List<(string, string)>();
        foreach (var (id, svg) in Glyphs()) files.Add(($"game/assets/glyphs/{id}.svg", svg));
        files.Add(("game/icon.svg", AppIcon()));
        files.Add(("game/assets/logo.svg", Logo()));
        files.Add(("docs/banner.svg", Banner(1280, 320)));
        files.Add(("docs/social-preview.svg", Banner(1280, 640)));
        return files;
    }

    /// <summary>All glyph ids, so code and tests can refer to them.</summary>
    public static IReadOnlyList<string> GlyphIds() => Glyphs().Select(g => g.Id).ToList();

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Svg(int w, int h, string body) =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {w} {h}\" width=\"{w}\" height=\"{h}\">\n{body.Trim()}\n</svg>\n";

    private static string Stroke(string d, string colour, double width, string extra = "") =>
        $"<path d=\"{d}\" fill=\"none\" stroke=\"{colour}\" stroke-width=\"{F(width)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"{extra}/>";

    // ---- letters (drawn as strokes) ---------------------------------------------------------------

    private const string LetterL1 = "M19 26 L19 40 L28 40";
    private const string LetterR1 = "M19 40 L19 26 L25 26 Q29 26 29 30 Q29 34 25 34 L19 34 M24 34 L29 40";
    private const string LetterB2 = "M36 26 L36 40 M36 26 L42 26 Q46 26 46 29.5 Q46 33 42 33 L36 33 M36 33 L43 33 Q47 33 47 36.5 Q47 40 43 40 L36 40";
    private const string LetterT2 = "M35 26 L47 26 M41 26 L41 40";
    private const string LetterZ1 = "M19 26 H29 L19 40 H29";
    private const string Digit1 = "M37 30 L41 26 V40";
    private const string Digit2 = "M36 29 Q36 26 41 26 Q46 26 46 30 Q46 33 36 40 H46";

    private static IEnumerable<(string Id, string Svg)> Glyphs()
    {
        // Face buttons.
        yield return ("pad_a", FaceButton(Green, "M20 46 L32 18 L44 46 M25 36 L39 36"));
        yield return ("pad_b", FaceButton(Red, "M25 18 L25 46 M25 18 L36 18 Q44 18 44 25 Q44 32 36 32 L25 32 M25 32 L37 32 Q46 32 46 39 Q46 46 37 46 L25 46"));
        yield return ("pad_x", FaceButton("#3b8cff", "M22 20 L42 44 M42 20 L22 44"));
        yield return ("pad_y", FaceButton(Yellow, "M21 19 L32 33 L43 19 M32 33 L32 46"));

        // Shoulder buttons and triggers.
        yield return ("pad_lb", Bumper(LetterL1 + " " + LetterB2));
        yield return ("pad_rb", Bumper(LetterR1 + " " + LetterB2));
        yield return ("pad_lt", Trigger(LetterL1 + " " + LetterT2));
        yield return ("pad_rt", Trigger(LetterR1 + " " + LetterT2));

        // Sticks: pushed in each direction, plus the clicks.
        foreach (var (side, letter) in new[] { ("l", "M29 28 L29 37 L36 37"), ("r", "M29 37 L29 28 L33 28 Q36 28 36 30.5 Q36 33 33 33 L29 33 M32 33 L36 37") })
        {
            yield return ($"pad_{side}stick", Stick(null, letter));
            foreach (var dir in new[] { "up", "down", "left", "right" })
                yield return ($"pad_{side}stick_{dir}", Stick(dir, letter));
            yield return ($"pad_{side}3", StickClick(letter));
        }

        // D-pad.
        yield return ("pad_dpad", Dpad(null));
        foreach (var dir in new[] { "up", "down", "left", "right" }) yield return ($"pad_dpad_{dir}", Dpad(dir));

        // Menu buttons.
        yield return ("pad_start", Svg(64, 64, $"""
            <rect x="6" y="18" width="52" height="28" rx="14" fill="{Panel}" stroke="{Dim}" stroke-width="3"/>
            {Stroke("M23 27 H41 M23 32 H41 M23 37 H41", Ink, 3.5)}
            """));
        yield return ("pad_back", Svg(64, 64, $"""
            <rect x="6" y="18" width="52" height="28" rx="14" fill="{Panel}" stroke="{Dim}" stroke-width="3"/>
            <rect x="21" y="25" width="14" height="11" rx="2" fill="none" stroke="{Ink}" stroke-width="3"/>
            <rect x="29" y="29" width="14" height="11" rx="2" fill="{Panel}" stroke="{Ink}" stroke-width="3"/>
            """));

        // PlayStation layout: face buttons are shapes, shoulders are L1/R1/L2/R2.
        yield return ("ps_cross", PsButton("#6aa7ff", "M22 22 L42 42 M42 22 L22 42"));
        yield return ("ps_circle", PsButton("#ff6b6b", "M32 20 A12 12 0 1 1 31.9 20 Z"));
        yield return ("ps_square", PsButton("#ff8bd0", "M21 21 H43 V43 H21 Z"));
        yield return ("ps_triangle", PsButton("#5fe3a1", "M32 19 L44.5 42 H19.5 Z"));
        yield return ("ps_l1", Bumper(LetterL1 + " " + Digit1));
        yield return ("ps_r1", Bumper(LetterR1 + " " + Digit1));
        yield return ("ps_l2", Trigger(LetterL1 + " " + Digit2));
        yield return ("ps_r2", Trigger(LetterR1 + " " + Digit2));
        yield return ("ps_options", Svg(64, 64, $"""
            <rect x="12" y="20" width="40" height="24" rx="12" fill="{Panel}" stroke="{Dim}" stroke-width="3"/>
            {Stroke("M25 28 H39 M25 32 H39 M25 36 H39", Ink, 3)}
            """));
        yield return ("ps_share", Svg(64, 64, $"""
            <rect x="12" y="20" width="40" height="24" rx="12" fill="{Panel}" stroke="{Dim}" stroke-width="3"/>
            {Stroke("M26 36 V31 H38 V36 M32 34 V25 M28.5 28.5 L32 25 L35.5 28.5", Ink, 3)}
            """));

        // Nintendo layout: the bottom button is labelled B and the right one A, so these are positioned by label, not place.
        yield return ("ns_a", NsButton("M20 46 L32 18 L44 46 M25 36 L39 36"));
        yield return ("ns_b", NsButton("M25 18 L25 46 M25 18 L36 18 Q44 18 44 25 Q44 32 36 32 L25 32 M25 32 L37 32 Q46 32 46 39 Q46 46 37 46 L25 46"));
        yield return ("ns_x", NsButton("M22 20 L42 44 M42 20 L22 44"));
        yield return ("ns_y", NsButton("M21 19 L32 33 L43 19 M32 33 L32 46"));
        yield return ("ns_l", Bumper("M27 26 L27 40 L38 40"));
        yield return ("ns_r", Bumper("M27 40 L27 26 L34 26 Q38 26 38 30 Q38 34 34 34 L27 34 M33 34 L38 40"));
        yield return ("ns_zl", Trigger(LetterZ1 + " M36 26 L36 40 L45 40"));
        yield return ("ns_zr", Trigger(LetterZ1 + " M36 40 L36 26 L42 26 Q46 26 46 30 Q46 34 42 34 L36 34 M41 34 L46 40"));
        yield return ("ns_plus", Svg(64, 64, $"""
            <circle cx="32" cy="32" r="22" fill="{Panel}" stroke="{Dim}" stroke-width="3"/>
            {Stroke("M32 21 V43 M21 32 H43", Ink, 4.5)}
            """));
        yield return ("ns_minus", Svg(64, 64, $"""
            <circle cx="32" cy="32" r="22" fill="{Panel}" stroke="{Dim}" stroke-width="3"/>
            {Stroke("M21 32 H43", Ink, 4.5)}
            """));

        // Mouse.
        yield return ("mouse", Mouse(null));
        foreach (var part in new[] { "left", "right", "middle", "wheel_up", "wheel_down" }) yield return ($"mouse_{part}", Mouse(part));

        // HUD icons.
        yield return ("icon_portal_teal", PortalIcon(Teal));
        yield return ("icon_portal_violet", PortalIcon(Violet));
        yield return ("icon_revolver", Svg(64, 64, $"""<path d="M4 20 H46 V27 H49 Q55 27 55 33 V38 H43 L40 32 H27 L24 38 L19 56 H8 L13 36 L10 28 H4 Z" fill="{Ink}"/><circle cx="36" cy="26" r="3" fill="{Deep}"/>"""));
        yield return ("icon_shotgun", Svg(64, 64, $"""<path d="M2 24 H46 L52 20 H62 V30 H52 L46 28 H28 L23 34 H8 L12 28 H2 Z" fill="{Ink}"/><path d="M12 34 L6 46 H14 L20 36 Z" fill="{Dim}"/><rect x="30" y="31" width="14" height="4" rx="2" fill="{Dim}"/>"""));
        yield return ("icon_parry", Svg(64, 64, $"""
            <circle cx="32" cy="32" r="14" fill="none" stroke="{Amber}" stroke-width="5"/>
            {Stroke("M32 4 V13 M32 51 V60 M4 32 H13 M51 32 H60 M12 12 L18 18 M46 46 L52 52 M52 12 L46 18 M12 52 L18 46", Amber, 4)}
            """));
        yield return ("icon_health", Svg(64, 64, $"""<path d="M32 56 C9 40 5 28 5 21 C5 12 12 7 19 7 C25 7 29 10 32 15 C35 10 39 7 45 7 C52 7 59 12 59 21 C59 28 55 40 32 56 Z" fill="{Red}"/><path d="M14 18 Q16 12 22 12" fill="none" stroke="#ffffff" stroke-opacity=".5" stroke-width="3" stroke-linecap="round"/>"""));
        yield return ("icon_blood", Svg(64, 64, $"""<path d="M32 5 C32 5 12 28 12 40 C12 52 21 59 32 59 C43 59 52 52 52 40 C52 28 32 5 32 5 Z" fill="{Red}"/><path d="M22 42 Q22 50 28 52" fill="none" stroke="#ffffff" stroke-opacity=".5" stroke-width="3" stroke-linecap="round"/>"""));
        yield return ("icon_style", Svg(64, 64, $"""<path d="M32 4 L38 26 L60 32 L38 38 L32 60 L26 38 L4 32 L26 26 Z" fill="{Amber}" stroke="#a86a00" stroke-width="2" stroke-linejoin="round"/>"""));
        yield return ("icon_skull", Svg(64, 64, $"""
            <circle cx="32" cy="27" r="20" fill="{Ink}"/>
            <rect x="21" y="40" width="22" height="16" rx="4" fill="{Ink}"/>
            <circle cx="23.5" cy="27" r="5.5" fill="{Deep}"/><circle cx="40.5" cy="27" r="5.5" fill="{Deep}"/>
            <path d="M32 33 L28.5 40 H35.5 Z" fill="{Deep}"/>
            {Stroke("M27 46 V54 M32 46 V54 M37 46 V54", Deep, 2)}
            """));
        yield return ("icon_time", Svg(64, 64, $"""
            <circle cx="32" cy="34" r="22" fill="none" stroke="{Ink}" stroke-width="5"/>
            {Stroke("M32 34 V20 M32 34 L42 40 M26 6 H38", Ink, 4.5)}
            """));
        yield return ("icon_kills", Svg(64, 64, $"""
            <circle cx="32" cy="32" r="17" fill="none" stroke="{Red}" stroke-width="4.5"/>
            {Stroke("M32 4 V17 M32 47 V60 M4 32 H17 M47 32 H60", Red, 4.5)}
            <circle cx="32" cy="32" r="3" fill="{Red}"/>
            """));
        yield return ("icon_stairs", Svg(64, 64, $"""<path d="M6 56 V44 H18 V32 H30 V20 H42 V8 H58 V56 Z" fill="{Dim}"/>"""));
        yield return ("icon_cream", Svg(64, 64, $"""
            <rect x="10" y="10" width="44" height="12" rx="3" fill="#8a5a2b"/>
            <path d="M12 22 H52 V50 Q52 58 44 58 H20 Q12 58 12 50 Z" fill="#fff3d6" stroke="#8a5a2b" stroke-width="3" stroke-linejoin="round"/>
            <path d="M12 26 Q19 20 26 26 T40 26 T52 26 V32 H12 Z" fill="#fffaf0"/>
            <rect x="12" y="40" width="40" height="9" fill="{Red}"/>
            """));
        yield return ("icon_charm", Svg(64, 64, $"""
            <path d="M32 22 C22 22 19 10 32 5 C45 10 42 22 32 22" fill="none" stroke="{Red}" stroke-width="3"/>
            <circle cx="32" cy="40" r="16" fill="#6b4a2f" stroke="#3d2817" stroke-width="2"/>
            <circle cx="25" cy="36" r="3.5" fill="{Red}"/>
            <circle cx="38" cy="35" r="3.5" fill="{Red}"/>
            <circle cx="32" cy="46" r="3.5" fill="{Red}"/>
            <path d="M46 30 L56 24 L48 36 Z" fill="{Ink}"/>
            """));
        yield return ("icon_key", Svg(64, 64, $"""
            <circle cx="20" cy="22" r="13" fill="none" stroke="{Amber}" stroke-width="6"/>
            {Stroke("M30 31 L56 57 M46 47 L40 53 M52 53 L46 59", Amber, 6)}
            """));
        yield return ("icon_coin", Svg(64, 64, $"""
            <ellipse cx="25" cy="40" rx="17" ry="11" fill="#a77f18" stroke="#5c4309" stroke-width="3"/>
            <ellipse cx="38" cy="28" rx="17" ry="11" fill="{Amber}" stroke="#5c4309" stroke-width="3"/>
            {Stroke("M38 21 V34", "#5c4309", 3)}
            """));
        yield return ("icon_relic", Svg(64, 64, $"""
            <path d="M32 4 L56 26 L32 60 L8 26 Z" fill="#4a3521" stroke="#2a1c10" stroke-width="3" stroke-linejoin="round"/>
            <path d="M32 14 L46 27 L32 48 L18 27 Z" fill="{Amber}"/>
            <path d="M18 27 H46 M32 14 V48" stroke="#5c4309" stroke-width="2"/>
            <circle cx="32" cy="27" r="4" fill="{Ink}"/>
            """));

        // Style ranks: n stacked chevrons, hotter as they climb.
        string[] rankColours = [Yellow, Yellow, "#ffd04a", Amber, Orange, "#ff6a1a", Red, "#ff2fb4"];
        for (int n = 1; n <= 8; n++)
        {
            var body = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                double y = 57 - i * 6;
                body.AppendLine(Stroke($"M10 {F(y)} L32 {F(y - 11)} L54 {F(y)}", rankColours[n - 1], 4));
            }
            yield return ($"rank_{n}", Svg(64, 64, body.ToString()));
        }
    }

    // ---- gamepad ----------------------------------------------------------------------------------

    private static string FaceButton(string colour, string letter) => Svg(64, 64, $"""
        <circle cx="32" cy="32" r="27" fill="{colour}"/>
        <circle cx="32" cy="32" r="27" fill="none" stroke="#000000" stroke-opacity=".28" stroke-width="3"/>
        {Stroke(letter, Deep, 5)}
        """);

    private static string PsButton(string colour, string symbol) => Svg(64, 64, $"""
        <circle cx="32" cy="32" r="27" fill="#23252e" stroke="{Dim}" stroke-width="3"/>
        {Stroke(symbol, colour, 4.5)}
        """);

    private static string NsButton(string letter) => Svg(64, 64, $"""
        <circle cx="32" cy="32" r="27" fill="#d8dae4" stroke="#8a8d9c" stroke-width="3"/>
        {Stroke(letter, Deep, 5)}
        """);

    private static string Bumper(string letters) => Svg(64, 64, $"""
        <rect x="5" y="20" width="54" height="24" rx="11" fill="{Panel}" stroke="{Dim}" stroke-width="3"/>
        {Stroke(letters, Ink, 3.5)}
        """);

    private static string Trigger(string letters) => Svg(64, 64, $"""
        <path d="M10 48 L10 26 Q10 12 24 12 L40 12 Q54 12 54 26 L54 48 Q54 54 48 54 L16 54 Q10 54 10 48 Z" fill="{Panel}" stroke="{Dim}" stroke-width="3"/>
        {Stroke(letters, Ink, 3.5)}
        """);

    private static string Stick(string? direction, string letter)
    {
        string arrow = direction switch
        {
            "up" => "M32 3 L41 14 L23 14 Z",
            "down" => "M32 61 L41 50 L23 50 Z",
            "left" => "M3 32 L14 23 L14 41 Z",
            "right" => "M61 32 L50 23 L50 41 Z",
            _ => "",
        };
        return Svg(64, 64, $"""
            <circle cx="32" cy="32" r="22" fill="#23252e" stroke="{Dim}" stroke-width="3"/>
            <circle cx="32" cy="32" r="12" fill="#444857" stroke="{Dim}" stroke-width="2.5"/>
            {Stroke(letter, Ink, 2.5)}
            {(arrow.Length > 0 ? $"<path d=\"{arrow}\" fill=\"{Amber}\" stroke=\"#a86a00\" stroke-width=\"1.5\" stroke-linejoin=\"round\"/>" : "")}
            """);
    }

    private static string StickClick(string letter) => Svg(64, 64, $"""
        <circle cx="32" cy="32" r="26" fill="#23252e" stroke="{Amber}" stroke-width="4"/>
        <circle cx="32" cy="32" r="13" fill="#444857" stroke="{Amber}" stroke-width="2.5"/>
        {Stroke(letter, Ink, 2.5)}
        """);

    private static string Dpad(string? direction)
    {
        string arm = direction switch
        {
            "up" => "M24 6 H40 V24 H24 Z",
            "down" => "M24 40 H40 V58 H24 Z",
            "left" => "M6 24 H24 V40 H6 Z",
            "right" => "M40 24 H58 V40 H40 Z",
            _ => "",
        };
        return Svg(64, 64, $"""
            <path d="M24 6 H40 V24 H58 V40 H40 V58 H24 V40 H6 V24 H24 Z" fill="{Panel}" stroke="{Dim}" stroke-width="3" stroke-linejoin="round"/>
            {(arm.Length > 0 ? $"<path d=\"{arm}\" fill=\"{Amber}\" stroke=\"#a86a00\" stroke-width=\"1.5\" stroke-linejoin=\"round\"/>" : "")}
            """);
    }

    // ---- mouse ------------------------------------------------------------------------------------

    private static string Mouse(string? part)
    {
        string highlight = part switch
        {
            "left" => $"<path d=\"M18 26 Q18 8 32 8 V26 Z\" fill=\"{Amber}\"/>",
            "right" => $"<path d=\"M46 26 Q46 8 32 8 V26 Z\" fill=\"{Amber}\"/>",
            "middle" => $"<rect x=\"28.5\" y=\"12\" width=\"7\" height=\"13\" rx=\"3.5\" fill=\"{Amber}\"/>",
            "wheel_up" => $"<rect x=\"28.5\" y=\"12\" width=\"7\" height=\"13\" rx=\"3.5\" fill=\"{Amber}\"/>{Stroke("M26 36 L32 30 L38 36", Amber, 3.5)}",
            "wheel_down" => $"<rect x=\"28.5\" y=\"12\" width=\"7\" height=\"13\" rx=\"3.5\" fill=\"{Amber}\"/>{Stroke("M26 30 L32 36 L38 30", Amber, 3.5)}",
            _ => "",
        };
        return Svg(64, 64, $"""
            <path d="M18 26 Q18 8 32 8 Q46 8 46 26 L46 38 Q46 56 32 56 Q18 56 18 38 Z" fill="{Panel}" stroke="{Dim}" stroke-width="3"/>
            {highlight}
            {Stroke("M18 26 H46 M32 8 V26", Dim, 2.5)}
            """);
    }

    // ---- icons ------------------------------------------------------------------------------------

    /// <summary>
    /// A portal drawn as a chamfered gate: a double-edged frame with cut corners and a slanted shimmer inside. Deliberately
    /// angular rather than a glowing oval, so the game's portals have their own look.
    /// </summary>
    private static string PortalIcon(string colour) => Svg(64, 64, Gate(32, 32, 44, 58, colour, 6));

    /// <summary>The gate shape centred at (cx, cy), with its stroke and shimmer sized from its own width.</summary>
    private static string Gate(double cx, double cy, double w, double h, string colour, double stroke)
    {
        double x0 = cx - w / 2, x1 = cx + w / 2, y0 = cy - h / 2, y1 = cy + h / 2, c = Math.Min(w, h) * 0.2;
        string Outline(double inset, double cut) =>
            $"M{F(x0 + cut + inset)} {F(y0 + inset)} H{F(x1 - cut - inset)} L{F(x1 - inset)} {F(y0 + cut + inset)} V{F(y1 - cut - inset)} L{F(x1 - cut - inset)} {F(y1 - inset)} H{F(x0 + cut + inset)} L{F(x0 + inset)} {F(y1 - cut - inset)} V{F(y0 + cut + inset)} Z";
        double inner = stroke * 1.35;
        return $"""<path d="{Outline(stroke / 2, c)}" fill="{colour}" fill-opacity=".2"/>""" +
               $"""<path d="{Outline(stroke / 2, c)}" fill="none" stroke="{colour}" stroke-width="{F(stroke)}" stroke-linejoin="miter"/>""" +
               $"""<path d="{Outline(inner + stroke / 2, c * 0.55)}" fill="none" stroke="{colour}" stroke-opacity=".55" stroke-width="{F(Math.Max(1, stroke / 4))}" stroke-linejoin="miter"/>""" +
               Stroke($"M{F(cx - w * 0.16)} {F(cy + h * 0.2)} L{F(cx + w * 0.16)} {F(cy - h * 0.2)} M{F(cx - w * 0.02)} {F(cy + h * 0.28)} L{F(cx + w * 0.3)} {F(cy - h * 0.12)}", "#ffffff", Math.Max(1.5, w * 0.075), " stroke-opacity=\".5\"");
    }

    // ---- app icon, banner -------------------------------------------------------------------------

    private static string AppIcon() => Svg(128, 128, $"""
        <defs><linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1d1f2b"/><stop offset="1" stop-color="#0d0e13"/></linearGradient></defs>
        <rect width="128" height="128" rx="26" fill="url(#bg)"/>
        {Gate(44, 40, 38, 52, Teal, 7)}
        {Gate(84, 88, 38, 52, Violet, 7)}
        {Stroke("M44 72 C44 86 56 90 64 94", Amber, 6, " stroke-dasharray=\"2 11\"")}
        <path d="M72 86 L63 100 L79 98 Z" fill="{Amber}"/>
        """);

    /// <summary>The wordmark on a transparent background with a bar under it, for the title screen.</summary>
    private static string Logo()
    {
        var (shapes, letters) = Wordmark();
        const double cell = 40, gap = 26, scale = 1.0;
        double total = (letters.Length * cell + (letters.Length - 1) * gap) * scale;
        double padX = 14, width = total + padX * 2, height = 56 + 40;
        var body = new StringBuilder();
        for (int i = 0; i < letters.Length; i++)
        {
            double x = padX + i * (cell + gap) * scale;
            body.AppendLine($"<g transform=\"translate({F(x)} 8)\">{Stroke(shapes[letters[i]], Ink, 9, " stroke-linecap=\"square\"").Replace(" stroke-linecap=\"round\"", "")}</g>");
        }
        body.AppendLine($"<rect x=\"{F(padX)}\" y=\"{F(56 + 22)}\" width=\"{F(total)}\" height=\"5\" fill=\"{Amber}\"/>");
        return Svg((int)Math.Ceiling(width), (int)height, body.ToString());
    }

    private static (Dictionary<string, string> Shapes, string[] Letters) Wordmark()
    {
        var shapes = new Dictionary<string, string>
        {
            ["H"] = "M0 0 V56 M40 0 V56 M0 28 H40",
            ["O"] = "M8 0 H32 Q40 0 40 8 V48 Q40 56 32 56 H8 Q0 56 0 48 V8 Q0 0 8 0 Z",
            ["L"] = "M0 0 V56 H40",
            ["W"] = "M0 0 L10 56 L20 22 L30 56 L40 0",
            ["F"] = "M0 56 V0 H40 M0 26 H30",
            ["A"] = "M0 56 L20 0 L40 56 M8 36 H32",
        };
        return (shapes, ["H", "O", "L", "L", "O", "W", "F", "A", "L", "L"]);
    }

    /// <summary>
    /// HOLLOWFALL in blocky geometric letters, between two portals. Each letter is a stroked path in a 40 x 56 cell.
    /// </summary>
    private static string Banner(int width, int height)
    {
        var (shapes, letters) = Wordmark();

        const double cell = 40, gap = 26;
        double scale = height >= 500 ? 2.1 : 1.45;
        double total = (letters.Length * cell + (letters.Length - 1) * gap) * scale;
        double x0 = (width - total) / 2, y0 = height / 2.0 - 28 * scale;

        var body = new StringBuilder();
        body.AppendLine($"<defs><linearGradient id=\"bg\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#1d1f2b\"/><stop offset=\"1\" stop-color=\"#0b0c11\"/></linearGradient></defs>");
        body.AppendLine($"<rect width=\"{width}\" height=\"{height}\" fill=\"url(#bg)\"/>");
        body.AppendLine(Gate(width * 0.075, height * 0.5, height * 0.2, height * 0.4, Teal, height * 0.03));
        body.AppendLine(Gate(width * 0.925, height * 0.5, height * 0.2, height * 0.4, Violet, height * 0.03));
        for (int i = 0; i < letters.Length; i++)
        {
            double x = x0 + i * (cell + gap) * scale;
            body.AppendLine($"<g transform=\"translate({F(x)} {F(y0)}) scale({F(scale)})\">{Stroke(shapes[letters[i]], Ink, 9, " stroke-linecap=\"square\"").Replace(" stroke-linecap=\"round\"", "")}</g>");
        }
        double barY = y0 + 56 * scale + 34 * scale;
        body.AppendLine($"<rect x=\"{F(x0)}\" y=\"{F(barY)}\" width=\"{F(total)}\" height=\"{F(5 * scale)}\" fill=\"{Amber}\"/>");
        return Svg(width, height, body.ToString());
    }
}
