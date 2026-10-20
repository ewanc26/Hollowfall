using Hollowfall.Core.Combat;
using Hollowfall.Core.Physics;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Portals;

/// <summary>Moves a <see cref="PlayerBody"/> through a world that has portals in it.</summary>
public static class PortalWalker
{
    /// <summary>Height of the body's centre above its feet, used to decide when a body goes through a portal.</summary>
    public const double CentreHeight = PlayerBody.Height / 2;

    public static Vec3 Centre(PlayerBody body) => new(body.X, body.Y + CentreHeight, body.Z);

    public static Vec3 Velocity(PlayerBody body) => new(body.VelocityX, body.VelocityY, body.VelocityZ);

    /// <summary>
    /// Steps the body with portal openings walkable, then, if it went through a portal, puts it out of the other
    /// one with its velocity turned. Returns the crossing (with the turned facing) when one happened.
    /// </summary>
    public static Crossing? Step(PlayerBody body, PortalSystem portals, double dt, double moveX, double moveZ, bool jump,
        Func<int, int, int, BlockType> blockAt, Vec3 forward)
    {
        var before = Centre(body);

        // Use the velocity the body had going in: if it lands on the bottom of the opening in the same step,
        // the physics zeroes its speed, but the speed it dived in with is what must come out the other side.
        var v = Velocity(body);
        body.Step(dt, moveX, moveZ, jump, portals.WithHoles(blockAt));

        if (portals.TryCross(before, Centre(body), v, forward) is not { } crossing) return null;

        body.Teleport(crossing.Position.X, crossing.Position.Y - CentreHeight, crossing.Position.Z,
            crossing.Velocity.X, crossing.Velocity.Y, crossing.Velocity.Z);
        return crossing;
    }
}
