namespace Hollowfall.Core.Combat;

public enum StyleRank
{
    None,
    Rowdy,
    Fierce,
    Brutal,
    Savage,
    Merciless,
    Relentless,
    Apocalyptic,
    Legendary,
}

/// <summary>Things that earn style points, with their base values.</summary>
public enum StyleBonus
{
    Kill,
    DoubleKill,
    TripleKill,
    MultiKill,
    Headshot,
    LimbHit,
    AirShot,
    GroundSlam,
    AirSlam,
    Parry,
    Chargeback,
    Arsenal,
    Quickdraw,
    Exploded,
    FinishedOff,
    Meteor,
    Sever,
    Hollowfall,
    Freefall,
}

/// <summary>How worn a weapon's novelty is: stale weapons earn less style, which pushes you to mix them.</summary>
public enum Freshness
{
    Fresh,
    Used,
    Stale,
    Dull,
}

public readonly record struct StyleAward(StyleBonus Bonus, double Points, double Time);

/// <summary>
/// A decaying score that rewards aggressive, varied play. Points fill ranks; each rank drains faster when
/// you stop; being hit drops you a rank. Deterministic and driven only by <see cref="Tick"/>.
/// </summary>
public sealed class StyleMeter
{
    // Cumulative points at which each rank begins (Rowdy..Legendary).
    private static readonly double[] RankStart = [150, 400, 750, 1200, 1800, 2600, 3600, 5000];

    // Passive loss per second while in each rank (None..Legendary).
    private static readonly double[] Decay = [8, 10, 14, 19, 26, 36, 50, 70, 100];

    public const double MaxPoints = 6500;
    public const double MovementMultiplierMax = 3.0;
    private const double MovementFalloffSeconds = 0.2;
    private const double ChainWindowSeconds = 0.6;
    private const int RecentAwardsKept = 8;

    private static readonly Dictionary<StyleBonus, double> BasePoints = new()
    {
        [StyleBonus.Kill] = 30,
        [StyleBonus.DoubleKill] = 25,
        [StyleBonus.TripleKill] = 50,
        [StyleBonus.MultiKill] = 100,
        [StyleBonus.Headshot] = 80,
        [StyleBonus.LimbHit] = 60,
        [StyleBonus.AirShot] = 50,
        [StyleBonus.GroundSlam] = 60,
        [StyleBonus.AirSlam] = 160,
        [StyleBonus.Parry] = 100,
        [StyleBonus.Chargeback] = 400,
        [StyleBonus.Arsenal] = 50,
        [StyleBonus.Quickdraw] = 50,
        [StyleBonus.Exploded] = 45,
        [StyleBonus.FinishedOff] = 50,
        [StyleBonus.Meteor] = 180,
        [StyleBonus.Sever] = 250,
        [StyleBonus.Hollowfall] = 150,
        [StyleBonus.Freefall] = 120,
    };

    private readonly List<double> _killTimes = [];
    private readonly Dictionary<string, double> _freshness = [];
    private readonly List<StyleAward> _recent = [];
    private double _clock;
    private double _movementBoostUntil;
    private string? _lastKillWeapon;

    public double Points { get; private set; }

    /// <summary>Everything earned this floor, ignoring decay. Used for the end-of-floor style rank.</summary>
    public double TotalScore { get; private set; }

    public IReadOnlyList<StyleAward> RecentAwards => _recent;

    public StyleRank Rank
    {
        get
        {
            int r = 0;
            while (r < RankStart.Length && Points >= RankStart[r]) r++;
            return (StyleRank)r;
        }
    }

    /// <summary>Progress through the current rank, 0 to 1 (1 at the top rank).</summary>
    public double RankProgress
    {
        get
        {
            int r = (int)Rank;
            if (r >= RankStart.Length) return 1;
            double lo = r == 0 ? 0 : RankStart[r - 1];
            return (Points - lo) / (RankStart[r] - lo);
        }
    }

    /// <summary>How quickly hard damage recovers: higher style speeds it up.</summary>
    public double HardDamageSpeed => Rank switch
    {
        >= StyleRank.Legendary => 3.0,
        >= StyleRank.Apocalyptic => 2.5,
        >= StyleRank.Merciless => 2.0,
        _ => 1.0,
    };

    public static double BaseValue(StyleBonus bonus) => BasePoints[bonus];

    /// <summary>The current movement multiplier (1 to 3): high while sliding or in the air, gone a moment after.</summary>
    public double MovementMultiplier => _clock < _movementBoostUntil ? MovementMultiplierMax : 1.0;

    public static double FreshnessMultiplier(Freshness f) => f switch
    {
        Freshness.Fresh => 1.5,
        Freshness.Used => 1.0,
        Freshness.Stale => 0.5,
        _ => 0.0,
    };

    public Freshness FreshnessOf(string weapon)
    {
        double v = _freshness.GetValueOrDefault(weapon, 1.0);
        return v >= 0.75 ? Freshness.Fresh : v >= 0.5 ? Freshness.Used : v >= 0.25 ? Freshness.Stale : Freshness.Dull;
    }

    /// <summary>Call every frame the player is sliding or airborne. The multiplier lingers briefly after.</summary>
    public void NotifyMoving(bool slidingOrAirborne)
    {
        if (slidingOrAirborne) _movementBoostUntil = _clock + MovementFalloffSeconds;
    }

    /// <summary>Records a shot or swing with a weapon variant: it goes stale, the others recover.</summary>
    public void NotifyWeaponUsed(string weapon)
    {
        foreach (var other in _freshness.Keys.ToList())
            if (other != weapon) _freshness[other] = Math.Min(1.0, _freshness[other] + 0.02);
        _freshness[weapon] = Math.Max(0.0, _freshness.GetValueOrDefault(weapon, 1.0) - 0.04);
    }

    /// <summary>Awards a bonus. Returns the points granted after multipliers.</summary>
    public double Award(StyleBonus bonus, string? weapon = null, double extraMultiplier = 1.0)
    {
        double mult = MovementMultiplier * extraMultiplier;
        if (weapon != null) mult *= FreshnessMultiplier(FreshnessOf(weapon));

        double points = BasePoints[bonus] * mult;
        if (points <= 0) return 0;

        Points = Math.Min(MaxPoints, Points + points);
        TotalScore += points;
        _recent.Add(new StyleAward(bonus, points, _clock));
        if (_recent.Count > RecentAwardsKept) _recent.RemoveAt(0);
        return points;
    }

    /// <summary>
    /// Registers a kill and the chain and variety bonuses it earns. Kills within 0.6 s of each other chain
    /// into double, triple and multi kills; killing with a different weapon than the last earns Arsenal.
    /// </summary>
    public double RegisterKill(string weapon, bool airborne = false)
    {
        double total = Award(StyleBonus.Kill, weapon);
        _killTimes.RemoveAll(t => _clock - t > ChainWindowSeconds);
        _killTimes.Add(_clock);

        total += _killTimes.Count switch
        {
            2 => Award(StyleBonus.DoubleKill),
            3 => Award(StyleBonus.TripleKill),
            >= 4 => Award(StyleBonus.MultiKill),
            _ => 0,
        };

        if (_lastKillWeapon != null && _lastKillWeapon != weapon) total += Award(StyleBonus.Arsenal);
        _lastKillWeapon = weapon;
        if (airborne) total += Award(StyleBonus.AirShot);
        return total;
    }

    /// <summary>Taking damage knocks you down a rank, to the start of the one below.</summary>
    public void OnDamaged()
    {
        int r = (int)Rank;
        if (r == 0) return;
        Points = r == 1 ? 0 : RankStart[r - 2];
    }

    public void Tick(double dt)
    {
        _clock += dt;
        Points = Math.Max(0, Points - Decay[(int)Rank] * dt);
    }

    public void Reset()
    {
        Points = 0;
        TotalScore = 0;
        _clock = 0;
        _movementBoostUntil = 0;
        _lastKillWeapon = null;
        _killTimes.Clear();
        _freshness.Clear();
        _recent.Clear();
    }
}
