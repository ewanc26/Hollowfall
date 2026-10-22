using Hollowfall.Core.Combat;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Portals;

public enum PlaceFailure
{
    None,
    NothingInRange,
    NotPortalable,
    NoRoom,
    Overlaps,
    Disabled,
}

public readonly record struct PlaceResult(bool Placed, Portal? Portal, PlaceFailure Failure);

/// <summary>Where a body ends up after going through a portal.</summary>
public readonly record struct Crossing(Portal Entered, Portal Exited, Vec3 Position, Vec3 Velocity, Vec3 Forward);

/// <summary>
/// A linked pair of portals (teal and violet). Placing one replaces the previous of its colour. Anything that
/// crosses the face of one comes out of the other, keeping its speed. The Warden can open a pair of his own on the
/// arena walls, which switches the player's pair off while it stands. Pure and engine-free.
/// </summary>
public sealed class PortalSystem
{
    public const double MaxPlaceDistance = 80;

    /// <summary>Slowest a body may leave a portal, so stepping in gently never leaves it stuck inside.</summary>
    public const double MinExitSpeed = 3.0;

    /// <summary>How far in front of a gate a launched shot starts, so that it crosses the face on its first step.</summary>
    public const double LaunchGap = 0.4;

    private readonly List<(Portal A, Portal B)> _fixed = [];
    private (Portal A, Portal B)? _warden;

    public Portal? Teal { get; private set; }
    public Portal? Violet { get; private set; }

    /// <summary>Both portals exist, so they work.</summary>
    public bool Linked => Teal != null && Violet != null;

    /// <summary>The Warden's own pair is open, and the player's placed pair is disabled until it closes.</summary>
    public bool WardenLive => _warden != null;

    public (Portal A, Portal B)? Warden => _warden;

    /// <summary>Every open face a body or ray can pass through: the placed pair once linked, then the Warden's, then the fixed rift openings.</summary>
    public IEnumerable<Portal> Openings
    {
        get
        {
            if (Linked && !WardenLive)
            {
                yield return Teal!;
                yield return Violet!;
            }
            if (_warden is { } w)
            {
                yield return w.A;
                yield return w.B;
            }
            foreach (var (a, b) in _fixed)
            {
                yield return a;
                yield return b;
            }
        }
    }

    /// <summary>The fixed rift openings of the current floor.</summary>
    public IEnumerable<Portal> Rifts => _fixed.SelectMany(pair => new[] { pair.A, pair.B });

    public Portal? Get(PortalColor color) => color == PortalColor.Teal ? Teal : Violet;

    public Portal? Other(Portal p)
    {
        if (p.Color != PortalColor.Rift) return p.Color == PortalColor.Teal ? Violet : Teal;
        foreach (var (a, b) in _fixed)
        {
            if (a == p) return b;
            if (b == p) return a;
        }
        if (_warden is { } w)
        {
            if (w.A == p) return w.B;
            if (w.B == p) return w.A;
        }
        return null;
    }

    /// <summary>Replaces the rift openings for the floor the body is on. They survive <see cref="Clear"/>.</summary>
    public void SetFixed(IEnumerable<(Portal A, Portal B)> pairs)
    {
        _fixed.Clear();
        _fixed.AddRange(pairs);
    }

    public void Clear()
    {
        Teal = null;
        Violet = null;
        _warden = null;
    }

    public void Remove(PortalColor color)
    {
        if (color == PortalColor.Teal) Teal = null; else Violet = null;
    }

    /// <summary>Exchanges where the player's two portals are. Each keeps its colour, and the openings stay where the walls are.</summary>
    public void SwapPlaced()
    {
        if (Teal is not { } t || Violet is not { } v) return;
        Teal = new Portal(PortalColor.Teal, v.Frame, v.HoleCells.ToArray(), v.NormalCell);
        Violet = new Portal(PortalColor.Violet, t.Frame, t.HoleCells.ToArray(), t.NormalCell);
    }

    /// <summary>Takes the Warden's pair down. The player's pair works again.</summary>
    public void CloseWarden() => _warden = null;

    // ---- placement --------------------------------------------------------------------------

    /// <summary>
    /// Fires a portal along a ray. The 1 x 2 opening is fitted to the struck surface: both blocks behind it must
    /// be portalable and both cells in front of it open. On a floor or ceiling its long side points the way
    /// <paramref name="facing"/> points, so you can step in the direction you are looking.
    /// </summary>
    public PlaceResult Place(PortalColor color, Vec3 origin, Vec3 direction, Vec3 facing, Func<int, int, int, BlockType> blockAt, bool platesOnly = false)
    {
        var result = Fit(color, origin, direction, facing, blockAt, b => BlockInfo.Portalable(b, platesOnly), p => Other(p) is { } partner && p.Overlaps(partner));
        if (result.Placed)
        {
            if (color == PortalColor.Teal) Teal = result.Portal; else Violet = result.Portal;
        }
        return result;
    }

    /// <summary>
    /// Opens the Warden's pair on the first two walls the aims strike that take a portal. Returns false, and leaves
    /// any pair already open, if fewer than two fit.
    /// </summary>
    public bool OpenWarden(IEnumerable<(Vec3 Origin, Vec3 Direction)> aims, Func<BlockType, bool> portable, Func<int, int, int, BlockType> blockAt)
    {
        var found = new List<Portal>();
        foreach (var (origin, direction) in aims)
        {
            if (found.Count == 2) break;
            var result = Fit(PortalColor.Rift, origin, direction, direction, blockAt, portable,
                p => found.Any(f => f.Overlaps(p)) || Rifts.Any(f => f.Overlaps(p)));
            if (result.Placed) found.Add(result.Portal!);
        }
        if (found.Count < 2) return false;
        _warden = (found[0], found[1]);
        return true;
    }

    private static PlaceResult Fit(PortalColor color, Vec3 origin, Vec3 direction, Vec3 facing, Func<int, int, int, BlockType> blockAt,
        Func<BlockType, bool> portable, Func<Portal, bool> clashes)
    {
        if (RayCast.FirstSolidFace(blockAt, origin, direction, MaxPlaceDistance) is not { } hit)
            return new(false, null, PlaceFailure.NothingInRange);
        if (!portable(blockAt(hit.X, hit.Y, hit.Z)))
            return new(false, null, PlaceFailure.NotPortalable);

        var d = direction.Normalized();
        var point = origin + d * hit.Distance;
        var normal = new Vec3(hit.NormalX, hit.NormalY, hit.NormalZ);
        bool vertical = hit.NormalY != 0;

        // Which way the portal's long side runs.
        (int X, int Y, int Z) along;
        if (!vertical) along = (0, 1, 0);
        else
        {
            var f = facing.Normalized();
            along = Math.Abs(f.X) >= Math.Abs(f.Z) ? (Math.Sign(f.X) == 0 ? 1 : Math.Sign(f.X), 0, 0) : (0, 0, Math.Sign(f.Z));
            if (hit.NormalY < 0) along = (-along.X, 0, -along.Z); // seen from below, keep "up" pointing where you look
        }

        // Candidate placements, nearest to the aim point first: the struck block together with the one beyond or before it.
        double offset = along.X * (point.X - (hit.X + 0.5)) + along.Y * (point.Y - (hit.Y + 0.5)) + along.Z * (point.Z - (hit.Z + 0.5));
        var shifts = offset >= 0 ? new[] { 0, -1, 1 } : new[] { -1, 0, 1 };

        var failure = PlaceFailure.NoRoom;
        foreach (int shift in shifts)
        {
            var c0 = (X: hit.X + along.X * shift, Y: hit.Y + along.Y * shift, Z: hit.Z + along.Z * shift);
            var c1 = (X: c0.X + along.X, Y: c0.Y + along.Y, Z: c0.Z + along.Z);
            if (!Fits(c0, c1, hit, blockAt, portable)) continue;

            var portal = Build(color, c0, c1, (hit.NormalX, hit.NormalY, hit.NormalZ), along, normal);
            if (clashes(portal))
            {
                failure = PlaceFailure.Overlaps;
                continue;
            }
            return new(true, portal, PlaceFailure.None);
        }
        return new(false, null, failure);
    }

    private static bool Fits((int X, int Y, int Z) c0, (int X, int Y, int Z) c1, FaceHit hit, Func<int, int, int, BlockType> blockAt, Func<BlockType, bool> portable)
    {
        foreach (var c in new[] { c0, c1 })
        {
            if (!portable(blockAt(c.X, c.Y, c.Z))) return false;
            if (BlockInfo.IsSolid(blockAt(c.X + hit.NormalX, c.Y + hit.NormalY, c.Z + hit.NormalZ))) return false;
        }
        return true;
    }

    /// <summary>A fixed opening on a wall: its two blocks are at <paramref name="wx"/>, <paramref name="wy"/> and one above, facing <paramref name="normal"/>.</summary>
    public static Portal Fixed(int wx, int wy, int wz, (int X, int Y, int Z) normal) =>
        Build(PortalColor.Rift, (wx, wy, wz), (wx, wy + 1, wz), normal, (0, 1, 0), new Vec3(normal.X, normal.Y, normal.Z));

    private static Portal Build(PortalColor color, (int X, int Y, int Z) c0, (int X, int Y, int Z) c1, (int X, int Y, int Z) n,
        (int X, int Y, int Z) along, Vec3 normal)
    {
        var up = new Vec3(along.X, along.Y, along.Z);
        // Right = Up x Normal keeps (Right, Up, Normal) right-handed.
        var right = new Vec3(up.Y * normal.Z - up.Z * normal.Y, up.Z * normal.X - up.X * normal.Z, up.X * normal.Y - up.Y * normal.X);

        // The surface plane sits half a block in front of the cells' centres.
        var centre = new Vec3((c0.X + c1.X) / 2.0 + 0.5, (c0.Y + c1.Y) / 2.0 + 0.5, (c0.Z + c1.Z) / 2.0 + 0.5) + normal * 0.5;
        return new Portal(color, new Frame(centre, right, up, normal), [c0, c1], n);
    }

    // ---- collision and travel ---------------------------------------------------------------

    /// <summary>The world as a walking body should see it: the blocks behind a portal's opening are empty, so you can step in.</summary>
    public Func<int, int, int, BlockType> WithHoles(Func<int, int, int, BlockType> blockAt)
    {
        var openings = Openings.ToArray();
        if (openings.Length == 0) return blockAt;
        return (x, y, z) => openings.Any(p => p.IsHole(x, y, z)) ? BlockType.Air : blockAt(x, y, z);
    }

    /// <summary>
    /// The ways a shot can leave the Warden's pair towards <paramref name="eye"/>, offset sideways by
    /// <paramref name="spread"/> so a volley fans out. Each is the gate it enters, the point just in front of that gate
    /// the shot starts from, and its direction. Only gates whose far side faces the player are offered.
    /// </summary>
    public IEnumerable<(Portal Entry, Vec3 Start, Vec3 Direction)> WardenLaunches(Vec3 eye, double spread)
    {
        if (_warden is not { } w) yield break;
        foreach (var (entry, exit) in new[] { (w.A, w.B), (w.B, w.A) })
        {
            var target = eye + exit.Frame.Right * (spread * 0.6);
            var towards = (target - exit.Frame.Center).Normalized();
            if (towards.Dot(exit.Frame.Normal) < 0.2) continue;

            var inward = PortalTransform.Vector(exit, entry, towards).Normalized();
            yield return (entry, entry.Frame.Center - inward * LaunchGap, inward);
        }
    }

    /// <summary>
    /// Checks whether a body's centre went through a portal face between two positions. If so, returns where it
    /// comes out, with its velocity and facing turned to match. A body must have been in front of the face and
    /// ended behind it, within the opening.
    /// </summary>
    public Crossing? TryCross(Vec3 previous, Vec3 current, Vec3 velocity, Vec3 forward)
    {
        foreach (var entered in Openings.ToArray())
        {
            var exited = Other(entered)!;
            var (_, _, c0) = entered.Frame.ToLocal(previous);
            var (_, _, c1) = entered.Frame.ToLocal(current);
            if (c0 < 0 || c1 >= 0) continue; // must go from the front to the back

            double t = c0 / (c0 - c1);
            var at = previous + (current - previous) * t;
            if (!entered.Covers(at)) continue;

            var position = PortalTransform.Point(entered, exited, current);
            var v = PortalTransform.Vector(entered, exited, velocity);
            var f = PortalTransform.Vector(entered, exited, forward);

            // Make sure it actually leaves: push the outward speed up to a minimum.
            double outward = v.Dot(exited.Frame.Normal);
            if (outward < MinExitSpeed) v += exited.Frame.Normal * (MinExitSpeed - outward);
            return new Crossing(entered, exited, position, v, f);
        }
        return null;
    }

    /// <summary>
    /// Follows a ray through the world, passing through portals. Returns the straight pieces in order, each with
    /// the length it covers; the last piece ends at a wall or at the maximum range. Used for bullets and lasers.
    /// </summary>
    public List<(Vec3 Origin, Vec3 Direction, double Length)> TraceRay(Func<int, int, int, BlockType> blockAt, Vec3 origin, Vec3 direction, double maxDistance, int maxBounces = 4)
    {
        var segments = new List<(Vec3, Vec3, double)>();
        var o = origin;
        var d = direction.Normalized();
        double remaining = maxDistance;
        var view = WithHoles(blockAt);

        for (int i = 0; i <= maxBounces && remaining > 1e-6; i++)
        {
            double wall = RayCast.FirstSolid(view, o, d, remaining) ?? remaining;
            Portal? hitPortal = null;
            double hitAt = wall;

            foreach (var p in Openings)
            {
                var (_, _, c0) = p.Frame.ToLocal(o);
                double facing = d.Dot(p.Frame.Normal);
                if (c0 <= 0 || facing >= 0) continue;
                double t = c0 / -facing;
                if (t >= hitAt || !p.Covers(o + d * t)) continue;
                hitAt = t;
                hitPortal = p;
            }

            segments.Add((o, d, hitAt));
            if (hitPortal == null) break;

            var exit = Other(hitPortal)!;
            var at = o + d * hitAt;
            remaining -= hitAt;
            o = PortalTransform.Point(hitPortal, exit, at) + exit.Frame.Normal * 1e-3;
            d = PortalTransform.Vector(hitPortal, exit, d).Normalized();
        }
        return segments;
    }
}
