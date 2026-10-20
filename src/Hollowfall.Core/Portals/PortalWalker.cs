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
        Func<int, int, int, BlockType> blockAt, Vec3 forward, double speed = PlayerBody.WalkSpeed)
    {
        var before = Centre(body);

        // Use the velocity the body had going in: if it lands on the bottom of the opening in the same step,
        // the physics zeroes its speed, but the speed it dived in with is what must come out the other side.
        var v = Velocity(body);
        body.Step(dt, moveX, moveZ, jump, portals.WithHoles(blockAt), speed);

        if (portals.TryCross(before, Centre(body), v, forward) is not { } crossing) return null;

        // Come out clear of the opening. The centre is carried across at the depth it had, so a slow body would otherwise
        // emerge with its feet still inside the one-block pit behind the exit, unable to walk sideways and falling straight back
        // in. Lift (or push) it until the whole body is in front of the exit's face.
        var clear = crossing with { Position = ClearOfExit(crossing) };
        body.Teleport(clear.Position.X, clear.Position.Y - CentreHeight, clear.Position.Z,
            clear.Velocity.X, clear.Velocity.Y, clear.Velocity.Z);
        return clear;
    }

    /// <summary>
    /// Moves a body centre out along the exit's normal until the body no longer overlaps the exit's plane: half its height
    /// for floors and ceilings, half its width for walls.
    /// </summary>
    public static Vec3 ClearOfExit(Crossing crossing)
    {
        var frame = crossing.Exited.Frame;
        var n = frame.Normal;
        double vertical = Math.Abs(n.Y);
        double need = vertical * CentreHeight + (1 - vertical) * PlayerBody.HalfWidth;
        double along = (crossing.Position - frame.Center).Dot(n);
        return along >= need ? crossing.Position : crossing.Position + n * (need - along);
    }
}
