using Hollowfall.Core.Portals;

namespace Hollowfall.Core.Combat;

/// <summary>What closing a portal does to one enemy standing in its opening. Pure data, decided by <see cref="SeverRules.Decide"/>.</summary>
public readonly record struct SeverOutcome(bool Immune, double Damage, bool Killed, bool Staggered, bool AwardsStyle)
{
    public static readonly SeverOutcome None = new(true, 0, false, false, false);
}

/// <summary>
/// Sever: closing a portal on someone cuts them in half. When a portal is replaced or removed, any enemy whose body
/// overlaps the old opening (the 1 x 2 face plus a little way in front of it) takes <see cref="Damage"/>. Those it
/// kills are severed: they drop no blood (no healing) and earn the Sever style bonus. Heavy enemies, whose health
/// is more than the cut does, are staggered instead of killed. Wardens are immune, and the player is never hurt.
/// </summary>
public static class SeverRules
{
    public const double Damage = 60;

    /// <summary>How far in front of the opening the cut reaches, in blocks.</summary>
    public const double ReachInFront = 0.3;

    public const double StaggerSeconds = 1.6;

    /// <summary>Bosses cannot be cut.</summary>
    public static bool Immune(EnemyKind kind) => kind == EnemyKind.Warden;

    /// <summary>Heavy enemies would survive the cut at full health: they are staggered, never killed.</summary>
    public static bool IsHeavy(EnemySpec spec) => spec.Health > Damage;

    /// <summary>The space the cut covers: the portal's 1 x 2 face, extended <see cref="ReachInFront"/> out of the wall.</summary>
    public static Box Zone(Portal portal)
    {
        var f = portal.Frame;
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (double a in new[] { -Portal.Width / 2, Portal.Width / 2 })
            foreach (double b in new[] { -Portal.Height / 2, Portal.Height / 2 })
                foreach (double c in new[] { 0, ReachInFront })
                {
                    var p = f.Center + f.Right * a + f.Up * b + f.Normal * c;
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                    minZ = Math.Min(minZ, p.Z); maxZ = Math.Max(maxZ, p.Z);
                }
        return new Box(new Vec3(minX, minY, minZ), new Vec3(maxX, maxY, maxZ));
    }

    /// <summary>Whether a body's box overlaps the cut (touching a face does not count).</summary>
    public static bool Overlaps(Portal portal, Box body)
    {
        var z = Zone(portal);
        return body.Min.X < z.Max.X && body.Max.X > z.Min.X &&
               body.Min.Y < z.Max.Y && body.Max.Y > z.Min.Y &&
               body.Min.Z < z.Max.Z && body.Max.Z > z.Min.Z;
    }

    /// <summary>
    /// What the cut does to an enemy that overlaps it. Light enemies die if the damage is enough; heavy ones are left on
    /// at least 1 health and staggered. The Sever bonus is earned the first time an enemy is cut, not on later cuts.
    /// </summary>
    public static SeverOutcome Decide(EnemySpec spec, double health, bool alreadySevered)
    {
        if (Immune(spec.Kind) || health <= 0) return SeverOutcome.None;
        if (IsHeavy(spec))
        {
            double taken = Math.Min(Damage, Math.Max(0, health - 1));
            return new(false, taken, false, true, !alreadySevered);
        }
        return new(false, Math.Min(Damage, health), health <= Damage, false, !alreadySevered);
    }

    /// <summary>Applies the cut of a closed portal to every enemy it touches. Called by <see cref="CombatSim"/>.</summary>
    internal static void Apply(Portal closed, IReadOnlyList<Enemy> enemies, StyleMeter style, Action<CombatEvent> emit, Action<Enemy> onKill)
    {
        foreach (var e in enemies.ToList())
        {
            if (e.Dead || !Overlaps(closed, e.Bounds)) continue;
            var outcome = Decide(e.Spec, e.Health, e.Severed);
            if (outcome.Immune) continue;

            e.Health -= outcome.Damage;
            double points = 0;
            if (outcome.AwardsStyle)
            {
                e.Severed = true;
                points = style.Award(StyleBonus.Sever);
            }
            if (outcome.Staggered)
            {
                e.State = EnemyState.Stunned;
                e.Timer = StaggerSeconds;
            }
            emit(new EnemySevered(e.Id, e.Kind, closed.Color, outcome.Damage, outcome.Killed, e.Feet + new Vec3(0, e.Spec.Height / 2, 0), points));
            if (outcome.Killed)
            {
                e.Health = 0;
                e.State = EnemyState.Dead;
                onKill(e);
                emit(new EnemyKilled(e.Id, e.Kind));
            }
        }
    }
}
