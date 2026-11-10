namespace Hollowfall.Core.Portals;

/// <summary>The non-colour cue for the two placed portals: the teal frame carries a chevron, the violet frame a diamond.</summary>
public enum PortalMark
{
    Chevron,
    Diamond,
}

/// <summary>
/// The portal pair's non-colour identity. Nothing in the interface may depend on colour alone, so the teal and violet
/// frames each carry a shaped notch in the world and its name in the HUD; which mark belongs to which portal is decided
/// here, once, in Core, and the host only draws the result.
/// </summary>
public static class PortalMarks
{
    /// <summary>How big a notch is, in blocks, when drawn on a portal frame.</summary>
    public const double NotchBlocks = 0.35;

    /// <summary>The teal pair is chevrons; the violet pair is diamonds.</summary>
    public static PortalMark Mark(PortalColor color) => color switch
    {
        PortalColor.Teal => PortalMark.Chevron,
        PortalColor.Violet => PortalMark.Diamond,
        _ => throw new ArgumentOutOfRangeException(nameof(color), "the fixed rift openings are not the placed pair and carry no mark"),
    };

    /// <summary>The HUD word for a mark, e.g. <c>portal.mark.chevron</c>.</summary>
    public static string MarkKey(PortalMark mark) => "portal.mark." + (mark == PortalMark.Chevron ? "chevron" : "diamond");

    /// <summary>
    /// The notch for a mark, as triangles in the frame's local plane: x along <see cref="Frame.Right"/>, y along
    /// <see cref="Frame.Up"/>, centred on the origin and half a block across, so the host scales it by
    /// <see cref="NotchBlocks"/> and sits it on the glow band above the opening. A chevron is a bent band; a diamond is
    /// a filled rhombus: two silhouettes that never read the same at any distance or tint.
    /// </summary>
    public static IReadOnlyList<((double X, double Y) A, (double X, double Y) B, (double X, double Y) C)> Notch(PortalMark mark)
    {
        return mark switch
        {
            PortalMark.Chevron =>
            [
                ((-0.5, -0.4), (0, 0.3), (0, 0.06)),
                ((-0.5, -0.4), (0, 0.06), (-0.32, -0.4)),
                ((0, 0.3), (0.5, -0.4), (0.32, -0.4)),
                ((0, 0.3), (0.32, -0.4), (0, 0.06)),
            ],
            PortalMark.Diamond =>
            [
                ((0, 0.5), (-0.42, 0), (0, -0.1)),
                ((0, 0.5), (0, -0.1), (0.42, 0)),
                ((0, -0.5), (0.42, 0), (0, 0.1)),
                ((0, -0.5), (0, 0.1), (-0.42, 0)),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(mark)),
        };
    }
}
