using Hollowfall.Core.Dungeon;

namespace Hollowfall.Core.Combat;

/// <summary>
/// The pacts on offer between floors (docs/features.md 4.1): each is a boon and a burden. The boon's strength follows the
/// current style rank — half below Savage, full from Savage, double at Legendary — so a pact is only as strong as the play
/// sustaining it. A pact is taken at most once a run, and offers are drawn deterministically from the world seed and the
/// floor index. The burdens always apply, at full strength, whatever the rank.
/// </summary>
public enum PactKind
{
    /// <summary>Parry heals 20 more and refunds the parry cooldown; hard damage takes half your health's ceiling instead of 35%.</summary>
    Bloodsworn,

    /// <summary>Blood heals 1.5x; maximum health 30 lower.</summary>
    HollowHeart,

    /// <summary>Slam shock radius +1.5 and floors break in one slam; style decays 20% faster.</summary>
    Gravedigger,

    /// <summary>All style points +25%; weapons go stale 40% faster.</summary>
    Showman,
}

/// <summary>The pacts' numbers and the deterministic offer. Kept in one place so playtesting changes numbers, not logic.</summary>
public static class Pacts
{
    /// <summary>How many pacts a completed floor offers.</summary>
    public const int OfferSize = 3;

    // Boons, at full strength; multiplied by <see cref="Strength"/> for the current rank.
    public const double BloodswornParryHeal = 20;
    public const double HollowHeartBloodMultiplier = 1.5;
    public const double GravediggerSlamRadiusBonus = 1.5;
    public const double ShowmanStyleBonus = 0.25;

    // Burdens: always at full strength once the pact is taken.
    public const double BloodswornHardDamageFraction = 0.50;
    public const double HollowHeartHealthLoss = 30;
    public const double GravediggerStyleDecay = 1.2;
    public const double ShowmanStaleness = 1.4;

    /// <summary>The lowest maximum health a burden may leave the player with.</summary>
    public const double MinMaxHealth = 10;

    /// <summary>Boon strength at a style rank: half below Savage, full from Savage, double at Legendary.</summary>
    public static double Strength(StyleRank rank) => rank switch
    {
        >= StyleRank.Legendary => 2.0,
        >= StyleRank.Savage => 1.0,
        _ => 0.5,
    };

    /// <summary>
    /// The pacts offered after a floor: up to <see cref="OfferSize"/> distinct pacts the run has not yet taken, drawn from the world
    /// seed and the completed <paramref name="floor"/>'s index. Never a pact already taken; never more than remain untaken; never
    /// empty while any pact is untaken.
    /// </summary>
    public static IReadOnlyList<PactKind> Offer(long worldSeed, int floor, IReadOnlyCollection<PactKind> taken)
    {
        var untaken = Enum.GetValues<PactKind>().Where(p => !taken.Contains(p)).ToList();
        var rng = new SeededRandom(worldSeed, floor, 0, 613);
        rng.Shuffle(untaken);
        return untaken.Take(Math.Min(OfferSize, untaken.Count)).ToList();
    }
}
