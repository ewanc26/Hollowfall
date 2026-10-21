using Hollowfall.Core.Relations;

namespace Hollowfall.Core.Progression;

/// <summary>
/// The merchants of the dungeon sell the bag's items. A price depends on how the merchant's people regard you: friends pay less,
/// and the hated are not served at all. Pure: the price is a function of the item and your standing.
/// </summary>
public static class Merchant
{
    /// <summary>What an item costs at the base standing (neutral), in coin. Coin is not held yet; this is the price list.</summary>
    public static int BasePrice(ItemKind kind) => kind switch
    {
        ItemKind.BrownieCream => 10,
        ItemKind.ThornCharm => 40,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>How much of the base price each standing pays: friends get a discount, the wary a markup.</summary>
    public static double Multiplier(ReputationTier tier) => tier switch
    {
        ReputationTier.Revered => 0.7,
        ReputationTier.Trusted => 0.8,
        ReputationTier.Friendly => 0.9,
        ReputationTier.Neutral => 1.0,
        ReputationTier.Wary => 1.25,
        ReputationTier.Hostile => 1.5,
        _ => 0,
    };

    /// <summary>The price of an item for someone at this standing, or null if the merchant will not serve them (the hated).</summary>
    public static int? PriceFor(ItemKind kind, ReputationTier tier)
    {
        if (tier == ReputationTier.Hated) return null;
        return (int)Math.Round(BasePrice(kind) * Multiplier(tier), MidpointRounding.AwayFromZero);
    }
}
