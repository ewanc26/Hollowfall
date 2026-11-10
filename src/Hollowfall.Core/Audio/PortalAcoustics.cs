using Hollowfall.Core.Combat;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Audio;

/// <summary>Where a sound is heard from, and whether it reached the listener through a portal.</summary>
public readonly record struct Hearing(Vec3 Position, bool ThroughPortal);

/// <summary>
/// Portal-aware sound, host-side only: a source is heard from the closer of the direct path and the path through a portal
/// pair, using the same reflection test the enemy shortcut logic uses (the mirrored point behind the gate's face, with line
/// of sight to the face), so a room behind a gate can be heard before it is seen. The hearing is mapped here, once, in Core;
/// the host plays the sound at the position this returns.
/// </summary>
public static class PortalAcoustics
{
    /// <summary>The cost of passing through a gate, in blocks, so a tie is heard the direct way (the enemy shortcut margin).</summary>
    public const double CrossingCost = 1.5;

    /// <summary>A sound heard through a gate is a little quieter and duller: the gate keeps some of it.</summary>
    public const double ThroughPortalVolume = 0.7;
    public const double ThroughPortalPitch = 0.94;

    public static Hearing WhereHeard(Vec3 listener, Vec3 source, PortalSystem portals, Func<int, int, int, BlockType> blockAt)
    {
        double direct = listener.DistanceTo(source);
        Vec3 best = source;
        double bestDistance = direct;
        bool throughPortal = false;
        foreach (var entrance in portals.Openings.ToArray())
        {
            if (portals.Other(entrance) is not { } exit) continue;
            var face = entrance.Frame.Center + entrance.Frame.Normal * 0.5;
            if (!RayCast.HasLineOfSight(blockAt, listener, face)) continue;
            // The source mirrored through the pair, into the listener's side of the gate they can see.
            var mirrored = PortalTransform.Point(exit, entrance, source);
            double via = listener.DistanceTo(mirrored) + CrossingCost;
            if (via < bestDistance)
            {
                best = mirrored;
                bestDistance = via;
                throughPortal = true;
            }
        }
        return new Hearing(best, throughPortal);
    }
}
