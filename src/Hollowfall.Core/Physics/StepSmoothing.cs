namespace Hollowfall.Core.Physics;

/// <summary>
/// Smooths the eye over a step-up. Physics lifts the body the whole way at once; the view keeps its old height and glides up
/// to the new one, so walking up a hill does not jerk the camera.
/// </summary>
public static class StepSmoothing
{
    /// <summary>The slowest the eye catches up at the tail, in blocks per second, so it always arrives.</summary>
    public const double Speed = 2.5;

    /// <summary>The share of the remaining gap closed per second, so a big step starts fast and settles gently.</summary>
    public const double Rate = 9.0;

    /// <summary>Moves a negative offset toward zero. Never overshoots, never changes sign, and reaches zero in finite time.</summary>
    public static double Settle(double offset, double dt)
    {
        if (offset >= 0 || double.IsNaN(offset)) return 0;
        double eased = offset * Math.Exp(-Rate * dt);
        double linear = offset + Speed * dt;
        double next = Math.Max(eased, linear); // the larger of the two moves: gentle at first, never stalling near zero
        return next > -0.001 ? 0 : next;
    }
}
