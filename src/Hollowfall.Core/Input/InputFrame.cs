namespace Hollowfall.Core.Input;

/// <summary>What the player is asking for this frame, after the engine has turned devices into numbers.</summary>
public readonly record struct InputFrame(
    double MoveX,
    double MoveZ,
    double LookYawDelta,
    double LookPitchDelta,
    bool Jump,
    bool FireHeld,
    bool ShotgunHeld,
    bool ParryPressed,
    bool PortalTealPressed,
    bool PortalVioletPressed);

/// <summary>Maths for turning raw device values into movement and turning.</summary>
public static class InputMath
{
    /// <summary>
    /// Radial dead zone: inside it the stick reads zero, outside it the remaining range is stretched back to 0..1, so
    /// there is no jump in speed as you leave the dead zone. The result never exceeds length 1.
    /// </summary>
    public static (double X, double Y) Stick(double x, double y, double deadzone)
    {
        double len = Math.Sqrt(x * x + y * y);
        if (len <= deadzone || len < 1e-9) return (0, 0);
        double scaled = Math.Min(1.0, (len - deadzone) / (1.0 - deadzone));
        return (x / len * scaled, y / len * scaled);
    }

    /// <summary>A gentle response curve on a 0..1 magnitude so small stick movements aim finely.</summary>
    public static double Curve(double magnitude) => Math.Pow(Math.Clamp(magnitude, 0, 1), 1.6);

    /// <summary>Turn for this frame from a stick: (yaw, pitch) in radians. Up on the stick looks up unless inverted.</summary>
    public static (double Yaw, double Pitch) StickLook(double x, double y, double deadzone, double speedRadiansPerSecond, double dt, bool invertY)
    {
        var (sx, sy) = Stick(x, y, deadzone);
        double mag = Math.Sqrt(sx * sx + sy * sy);
        if (mag < 1e-9) return (0, 0);
        double k = Curve(mag) / mag * speedRadiansPerSecond * dt;
        // Stick right turns right (yaw decreases); stick up (negative y) looks up (pitch increases).
        return (-sx * k, -sy * k * (invertY ? -1 : 1));
    }

    /// <summary>Turn for this frame from mouse movement in pixels.</summary>
    public static (double Yaw, double Pitch) MouseLook(double dx, double dy, double sensitivity, bool invertY) =>
        (-dx * sensitivity, -dy * sensitivity * (invertY ? -1 : 1));

    /// <summary>
    /// Converts a movement request in the player's own frame (strafe, forward) into a world-space direction for a
    /// view turned by <paramref name="yaw"/>. Yaw 0 looks down -Z. The result has length at most 1.
    /// </summary>
    public static (double X, double Z) WorldMove(double strafe, double forward, double yaw)
    {
        double len = Math.Sqrt(strafe * strafe + forward * forward);
        if (len > 1) { strafe /= len; forward /= len; }
        double sin = Math.Sin(yaw), cos = Math.Cos(yaw);
        return (strafe * cos - forward * sin, -strafe * sin - forward * cos);
    }

    /// <summary>Keeps the view from flipping over the top or bottom.</summary>
    public static double ClampPitch(double pitch) => Math.Clamp(pitch, -1.5, 1.5);
}
