namespace Hollowfall.Core.Ui;

/// <summary>
/// How big the interface should be. The UI is laid out for a 1280 by 720 window; this turns the real window size into a scale
/// so the HUD, menus and story panels stay the same share of the screen on a small laptop, a 4K monitor or a retina display,
/// and the player's own multiplier is applied on top. It is a pure function so it can be tested without an engine.
/// </summary>
public static class UiScaling
{
    public const double BaseWidth = 1280;
    public const double BaseHeight = 720;

    /// <summary>The smallest and largest scale the automatic sizing gives, so a tiny or enormous window stays usable.</summary>
    public const double MinAuto = 0.75;
    public const double MaxAuto = 3.0;

    /// <summary>Scales change in steps this big, so dragging the window edge does not make text shimmer.</summary>
    public const double Step = 0.05;

    public static readonly (double Min, double Max, double Default) UserRange = (0.6, 1.6, 1.0);

    /// <summary>The scale for a window of the given size in pixels. The smaller of the two ratios wins, so nothing is cut off.</summary>
    public static double Auto(double windowWidth, double windowHeight)
    {
        if (!(windowWidth > 0) || !(windowHeight > 0) || double.IsInfinity(windowWidth) || double.IsInfinity(windowHeight)) return 1.0;
        double ratio = Math.Min(windowWidth / BaseWidth, windowHeight / BaseHeight);
        return Math.Clamp(ratio, MinAuto, MaxAuto);
    }

    /// <summary>
    /// The scale to apply: the automatic one (or 1.0 when automatic sizing is off) times the player's multiplier, kept to
    /// whole steps and to a range the interface still works in.
    /// </summary>
    public static double Factor(double windowWidth, double windowHeight, double userScale, bool automatic = true)
    {
        double user = double.IsNaN(userScale) || double.IsInfinity(userScale) ? UserRange.Default : Math.Clamp(userScale, UserRange.Min, UserRange.Max);
        double scale = (automatic ? Auto(windowWidth, windowHeight) : 1.0) * user;
        double snapped = Math.Round(scale / Step) * Step;
        return Math.Clamp(Math.Round(snapped, 2), 0.5, 4.0);
    }
}
