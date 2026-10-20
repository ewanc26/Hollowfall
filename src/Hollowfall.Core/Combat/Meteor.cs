using Hollowfall.Core.Physics;

namespace Hollowfall.Core.Combat;

/// <summary>
/// Every tuned number for the Meteor (momentum kill) lives here, so balance changes touch one file.
/// Leaving a portal at speed turns the player into a weapon for a short window; see docs/features.md section 2.2.
/// </summary>
public static class MeteorTuning
{
    /// <summary>Slowest speed (blocks per second) that arms a Meteor, and that keeps the window open. Hitting a wall drops you below it.</summary>
    public const double MinSpeed = 18;

    /// <summary>How long after leaving a portal the window stays open, in seconds.</summary>
    public const double WindowSeconds = 0.6;

    /// <summary>How close (blocks, to the enemy's hit box) the player must be to hit.</summary>
    public const double ImpactReach = 1.2;

    /// <summary>Damage is speed times this, capped at <see cref="MaxDamage"/>.</summary>
    public const double DamagePerSpeed = 2.5;

    public const double MaxDamage = 120;

    /// <summary>Speed above this earns no extra damage.</summary>
    public const double MaxCreditedSpeed = 60;

    /// <summary>Fraction of speed kept after an impact (a 30% loss).</summary>
    public const double SpeedKept = 0.7;

    /// <summary>A fall longer than this (blocks, before the impact) doubles the style award.</summary>
    public const double LongFallBlocks = 20;

    public const double LongFallMultiplier = 2.0;

    /// <summary>Minimum time between any two Meteors.</summary>
    public const double CooldownSeconds = 0.4;

    /// <summary>A given enemy cannot be Meteored again for this long, so cycling a portal pair cannot farm one target.</summary>
    public const double PerEnemyLockoutSeconds = 2.0;
}

/// <summary>The pure rules of the Meteor: no state, so they are easy to test and tune.</summary>
public static class MeteorRules
{
    public static bool Qualifies(double speed) => speed >= MeteorTuning.MinSpeed;

    public static double Damage(double speed) =>
        Math.Min(MeteorTuning.MaxDamage, Math.Min(speed, MeteorTuning.MaxCreditedSpeed) * MeteorTuning.DamagePerSpeed);

    public static double StyleMultiplier(double fallDistance) =>
        fallDistance > MeteorTuning.LongFallBlocks ? MeteorTuning.LongFallMultiplier : 1.0;
}

/// <summary>One Meteor the tracker wants applied.</summary>
internal readonly record struct MeteorHit(Enemy Enemy, double Speed, double Damage, bool LongFall);

/// <summary>
/// The Meteor window. Opens when the host reports a portal crossing at speed, closes after
/// <see cref="MeteorTuning.WindowSeconds"/> or as soon as speed falls below the minimum (a wall), and limits
/// hits with a global cooldown and a per-enemy lockout. Driven only by <see cref="Update"/>.
/// </summary>
internal sealed class MeteorTracker
{
    private readonly Dictionary<int, double> _lockedUntil = [];
    private double _window;
    private double _cooldown;
    private double _fall;
    private double? _lastY;

    public bool WindowOpen => _window > 0;

    public double FallDistance => _fall;

    public void Reset()
    {
        _window = _cooldown = _fall = 0;
        _lastY = null;
        _lockedUntil.Clear();
    }

    public List<MeteorHit> Update(double dt, double now, PlayerView view, IEnumerable<Enemy> enemies)
    {
        var hits = new List<MeteorHit>();
        _cooldown = Math.Max(0, _cooldown - dt);
        _window = Math.Max(0, _window - dt);

        // Fall distance: blocks dropped while airborne. The jump through a portal is not a fall.
        if (!view.Airborne) _fall = 0;
        else if (_lastY is { } y && !view.Crossed && view.Feet.Y < y) _fall += y - view.Feet.Y;
        _lastY = view.Feet.Y;

        double speed = view.Velocity.Length;
        if (view.Crossed && MeteorRules.Qualifies(speed)) _window = MeteorTuning.WindowSeconds;
        if (_window <= 0) return hits;
        if (!MeteorRules.Qualifies(speed)) { _window = 0; return hits; }
        if (_cooldown > 0) return hits;

        var centre = view.Feet + new Vec3(0, PlayerBody.Height / 2, 0);
        Enemy? best = null;
        double bestDist = MeteorTuning.ImpactReach;
        foreach (var e in enemies)
        {
            if (e.Dead || _lockedUntil.GetValueOrDefault(e.Id) > now) continue;
            double d = DistanceToBox(centre, e.Bounds);
            if (d <= bestDist) { bestDist = d; best = e; }
        }
        if (best == null) return hits;

        hits.Add(new MeteorHit(best, speed, MeteorRules.Damage(speed), _fall > MeteorTuning.LongFallBlocks));
        _lockedUntil[best.Id] = now + MeteorTuning.PerEnemyLockoutSeconds;
        _cooldown = MeteorTuning.CooldownSeconds;
        _fall = 0; // the fall bonus is spent
        return hits;
    }

    private static double DistanceToBox(Vec3 p, Box b)
    {
        double dx = Math.Max(Math.Max(b.Min.X - p.X, 0), p.X - b.Max.X);
        double dy = Math.Max(Math.Max(b.Min.Y - p.Y, 0), p.Y - b.Max.Y);
        double dz = Math.Max(Math.Max(b.Min.Z - p.Z, 0), p.Z - b.Max.Z);
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
