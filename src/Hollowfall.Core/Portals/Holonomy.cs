using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Portals;

/// <summary>
/// A loop of portal hops: go in through one opening, come out of its partner, walk on to the next. Each hop is a rigid
/// motion, and walking between hops keeps a body's velocity, so a velocity carried round the loop comes back turned by the
/// product of the hops. If that product is not the identity, the loop is non-Euclidean: the space closes up with a twist,
/// and a traveller who goes round it leaves in a direction they did not enter with, though every walk is locally flat.
/// </summary>
public static class Holonomy
{
    public readonly record struct Hop(Frame In, Frame Out);

    public static Vec3 Vector(IReadOnlyList<Hop> loop, Vec3 v)
    {
        foreach (var hop in loop) v = PortalTransform.Vector(hop.In, hop.Out, v);
        return v;
    }
}
