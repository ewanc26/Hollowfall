using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Relations;

namespace Hollowfall.Core.Progression;

/// <summary>What happened when the player tried to buy something.</summary>
public enum Purchase
{
    Bought,

    /// <summary>The merchant will not serve you at all.</summary>
    Refused,

    /// <summary>You do not have enough coin.</summary>
    NoCoin,

    /// <summary>There is no room in the bag.</summary>
    BagFull,
}

/// <summary>
/// The pedlars sell the bag's items. A price depends on how the merchant's people regard you: friends pay less, the wary and
/// hostile pay more, and the hated are not served at all. Pure: the price is a function of the item and your standing, and a
/// purchase only moves coin from <see cref="Progression"/> into the bag when it can complete.
/// </summary>
public static class Merchant
{
    /// <summary>What a pedlar sells. Appended to, never reordered.</summary>
    public static readonly IReadOnlyList<ItemKind> Wares = [ItemKind.BrownieCream, ItemKind.ThornCharm];

    /// <summary>How far, in blocks along the ground, you may stand and still trade.</summary>
    public const double Reach = 3.0;

    public const double ReachHeight = 2.5;

    /// <summary>Where the pedlar waits on the dungeon's surface plaza, opposite the player's arrival.</summary>
    public static Vec3 PedlarSpot(DungeonSite site) =>
        new(site.EntranceX - 2 + 0.5, site.SurfaceY + 1, site.EntranceZ + 2 + 0.5);

    /// <summary>Whether a pedlar will open the pack for someone at this standing.</summary>
    public static bool WillServe(ReputationTier tier) => tier != ReputationTier.Hated;

    /// <summary>What an item costs at the base standing (neutral), in coin.</summary>
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
        if (!WillServe(tier)) return null;
        return (int)Math.Round(BasePrice(kind) * Multiplier(tier), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Buys one item if the pedlar serves you, you can pay and the bag has room. Coin is only spent once the item is in the bag.
    /// </summary>
    public static Purchase Buy(Progression character, ItemKind kind, ReputationTier tier)
    {
        if (!Wares.Contains(kind)) return Purchase.Refused;
        if (PriceFor(kind, tier) is not { } price) return Purchase.Refused;
        if (character.Coin < price) return Purchase.NoCoin;
        if (character.Items.Add(kind) == 0) return Purchase.BagFull;
        if (!character.SpendCoin(price)) throw new InvalidOperationException("coin was checked before the item was added");
        return Purchase.Bought;
    }
}
