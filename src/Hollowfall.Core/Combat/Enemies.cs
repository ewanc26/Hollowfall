using Hollowfall.Core.Physics;

namespace Hollowfall.Core.Combat;

public enum EnemyKind
{
    Husk,
    Spitter,
    Warden,

    // ---- the Folk, wanderers and vermin (phase 1 of the relationship system; append new kinds below) ----
    Brownie,
    Selkie,
    Kelpie,
    BeanNighe,
    Hare,
}

public enum EnemyState
{
    Idle,
    Chasing,
    Windup,
    Recover,
    Stunned,
    Dead,
}

/// <summary>Tuning data for one enemy type. Windup is the telegraph: while it runs the attack can be parried.</summary>
public sealed record EnemySpec(
    EnemyKind Kind,
    double Health,
    double Speed,
    double MeleeRange,
    double MeleeDamage,
    double WindupSeconds,
    double CooldownSeconds,
    double ProjectileDamage,
    double PreferredMin,
    double PreferredMax,
    int Volley,
    double HalfWidth = 0.3,
    double Height = 1.8)
{
    public bool IsRanged => ProjectileDamage > 0;

    public static EnemySpec Of(EnemyKind kind) => kind switch
    {
        EnemyKind.Husk => new(EnemyKind.Husk, 30, 3.4, 1.6, 20, 0.45, 1.1, 0, 0, 0, 0),
        EnemyKind.Spitter => new(EnemyKind.Spitter, 20, 2.2, 0, 0, 0.6, 2.4, 15, 6, 12, 1, 0.38, 1.5),
        EnemyKind.Brownie => new(EnemyKind.Brownie, 14, 2.6, 0, 0, 0.5, 1.5, 0, 0, 0, 0, 0.28, 0.95),
        EnemyKind.Selkie => new(EnemyKind.Selkie, 40, 3.4, 1.6, 14, 0.5, 1.4, 0, 0, 0, 0, 0.35, 1.65),
        EnemyKind.Kelpie => new(EnemyKind.Kelpie, 90, 4.2, 2.2, 28, 0.55, 1.8, 0, 0, 0, 0, 0.5, 1.9),
        EnemyKind.BeanNighe => new(EnemyKind.BeanNighe, 30, 1.6, 0, 0, 0.5, 1.5, 0, 0, 0, 0, 0.3, 1.55),
        EnemyKind.Hare => new(EnemyKind.Hare, 6, 5.0, 0, 0, 0.5, 1.5, 0, 0, 0, 0, 0.2, 0.55),
        _ => new(EnemyKind.Warden, 260, 2.8, 2.2, 35, 0.7, 2.0, 18, 5, 11, 3, 0.5, 2.3),
    };
}

public sealed class Enemy
{
    /// <summary>The head is the top quarter of an enemy and the legs the bottom third, whatever its size.</summary>
    public const double HeadFraction = 0.75;

    public const double LegFraction = 0.305;

    internal Enemy(int id, EnemySpec spec, Vec3 feet, int floor, int roomId)
    {
        Id = id;
        Spec = spec;
        Floor = floor;
        RoomId = roomId;
        Health = spec.Health;
        Body = new PlayerBody(feet.X, feet.Y, feet.Z);
        State = EnemyState.Chasing;
    }

    public int Id { get; }
    public EnemySpec Spec { get; }
    public EnemyKind Kind => Spec.Kind;
    public int Floor { get; }
    public int RoomId { get; }
    public double Health { get; internal set; }
    public EnemyState State { get; internal set; }
    internal double Timer { get; set; }

    /// <summary>
    /// What this one wants and remembers (faction, temperament, personality, memory). Null when the relationship system is off,
    /// and then the enemy chases as it always has.
    /// </summary>
    public Relations.Mind? Mind { get; internal set; }

    /// <summary>Whether a closing portal has already cut this enemy (the Sever bonus is earned once).</summary>
    public bool Severed { get; internal set; }
    internal PlayerBody Body { get; }

    public Vec3 Feet => new(Body.X, Body.Y, Body.Z);
    /// <summary>The hit box: each kind has its own size, matching its model.</summary>
    public Box Bounds => Box.AroundFeet(Feet, Spec.HalfWidth, Spec.Height);
    public bool Dead => State == EnemyState.Dead;

    /// <summary>True while the enemy is winding up an attack: the cue to parry (shown yellow in the UI).</summary>
    public bool Telegraphing => State == EnemyState.Windup;

    /// <summary>Puts the enemy into an attack windup (for scripted fights and tests).</summary>
    public void ForceWindup(double seconds)
    {
        State = EnemyState.Windup;
        Timer = seconds;
    }

    /// <summary>Sets remaining health directly (for scripted fights and tests).</summary>
    public void SetHealth(double health) => Health = health;

    public HitZone ZoneAt(double hitY)
    {
        double rel = hitY - Body.Y;
        return rel >= Spec.Height * HeadFraction ? HitZone.Head : rel < Spec.Height * LegFraction ? HitZone.Limb : HitZone.Torso;
    }
}

public sealed class Projectile
{
    public const double Radius = 0.25;
    public const double LifetimeSeconds = 6;

    internal Projectile(int id, Vec3 position, Vec3 velocity, double damage)
    {
        Id = id;
        Position = position;
        Velocity = velocity;
        Damage = damage;
    }

    public int Id { get; }
    public Vec3 Position { get; internal set; }
    public Vec3 Velocity { get; internal set; }
    public double Damage { get; internal set; }
    public bool Reflected { get; internal set; }
    internal double Age { get; set; }
}

/// <summary>A player weapon. Pellets above one are spread randomly inside a cone.</summary>
public sealed record WeaponSpec(string Name, double Cooldown, int Pellets, double SpreadRadians, double Damage, double HeadMultiplier, double LimbMultiplier, double Range, double PelletHeal)
{
    public static readonly WeaponSpec Revolver = new("revolver", 0.28, 1, 0, 12, 2.0, 0.8, 60, 0);
    public static readonly WeaponSpec Shotgun = new("shotgun", 0.85, 8, 0.09, 5, 1.5, 0.8, 14, 3);
}
