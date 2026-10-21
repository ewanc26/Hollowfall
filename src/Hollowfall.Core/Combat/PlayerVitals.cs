namespace Hollowfall.Core.Combat;

public enum HitZone
{
    Torso,
    Limb,
    Head,
}

/// <summary>
/// Health that comes from aggression. There is no regeneration and no pickups: hitting enemies at close
/// range heals you, and being hit adds "hard damage", a temporary ceiling on how much you can heal, which
/// drains away after a short delay. A successful parry refills you to that ceiling.
/// </summary>
public sealed class PlayerVitals
{
    public const double MaxHealth = 100;
    public const double HardDamageShare = 0.35;
    public const double HardDamageDrainPerSecond = 15;
    public const double HardDamageBaseDelay = 1.0;
    public const double HardDamageDelayPerPoint = 0.05;

    /// <summary>Hits from farther than this heal nothing: you have to get close.</summary>
    public const double BloodRange = 4.0;

    public const double ParryInvulnerability = 0.35;

    /// <summary>The most <see cref="HitHealScale"/> can reach.</summary>
    public const double MaxHitHealScale = 4;

    private double _hardDamageDelay;
    private double _invulnerableFor;
    private double _hitHealScale = 1;

    /// <summary>A character with the base maximum health.</summary>
    public PlayerVitals() : this(MaxHealth) { }

    /// <summary>A character with <paramref name="max"/> health (Vigour raises it for a run; see RunStats in the Progression namespace).</summary>
    public PlayerVitals(double max)
    {
        Max = max;
        Health = max;
    }

    /// <summary>The most health this character can have in this run.</summary>
    public double Max { get; }

    public double Health { get; private set; }
    public double HardDamage { get; private set; }

    /// <summary>The most health you can have right now: the maximum less hard damage.</summary>
    public double Ceiling => Max - HardDamage;

    /// <summary>Multiplier on blood from hits (the Bloodlust perk). 1 normally; clamped to 1..4.</summary>
    public double HitHealScale
    {
        get => _hitHealScale;
        set => _hitHealScale = Math.Clamp(double.IsFinite(value) ? value : 1, 1, MaxHitHealScale);
    }

    public bool Dead => Health <= 0;
    public bool Invulnerable => _invulnerableFor > 0;

    /// <summary>Applies damage and returns the amount actually taken (zero while invulnerable or dead).</summary>
    public double Damage(double amount)
    {
        if (amount <= 0 || Dead || Invulnerable) return 0;
        double taken = Math.Min(amount, Health);
        Health -= taken;

        HardDamage = Math.Min(Max - 1, HardDamage + taken * HardDamageShare);
        _hardDamageDelay = HardDamageBaseDelay + taken * HardDamageDelayPerPoint;
        return taken;
    }

    /// <summary>Heals up to the current ceiling; returns the amount healed.</summary>
    public double Heal(double amount)
    {
        if (amount <= 0 || Dead) return 0;
        double room = Math.Max(0, Ceiling - Health);
        double healed = Math.Min(amount, room);
        Health += healed;
        return healed;
    }

    /// <summary>Blood from a hit: close range only, more for the head and limbs.</summary>
    public double HealFromHit(HitZone zone, double distance, double perPellet = 0)
    {
        if (distance > BloodRange) return 0;
        double amount = perPellet > 0 ? perPellet : zone switch
        {
            HitZone.Head => 30,
            HitZone.Limb => 20,
            _ => 10,
        };
        return Heal(amount * _hitHealScale);
    }

    /// <summary>Washes away all hard damage, returning the ceiling to full. Returns how much was removed.</summary>
    public double Cleanse()
    {
        double removed = HardDamage;
        HardDamage = 0;
        _hardDamageDelay = 0;
        return removed;
    }

    /// <summary>A finishing blow at point-blank range gibs the enemy and soaks you in blood.</summary>
    public double HealFromGib(double distance) => distance <= BloodRange ? Heal(120) : 0;

    /// <summary>Full heal to the ceiling, plus a moment of invulnerability.</summary>
    public double Parry()
    {
        double healed = Heal(Ceiling - Health);
        _invulnerableFor = ParryInvulnerability;
        return healed;
    }

    /// <summary>Advances timers. <paramref name="drainSpeed"/> is the style-based speed-up of hard damage recovery.</summary>
    public void Tick(double dt, double drainSpeed = 1.0)
    {
        _invulnerableFor = Math.Max(0, _invulnerableFor - dt);
        if (HardDamage <= 0) return;

        double scaled = dt * drainSpeed;
        if (_hardDamageDelay > 0)
        {
            double used = Math.Min(_hardDamageDelay, scaled);
            _hardDamageDelay -= used;
            scaled -= used;
        }
        if (scaled > 0) HardDamage = Math.Max(0, HardDamage - HardDamageDrainPerSecond * scaled);
    }

    public void Reset()
    {
        Health = Max;
        HardDamage = 0;
        _hardDamageDelay = 0;
        _invulnerableFor = 0;
    }
}
