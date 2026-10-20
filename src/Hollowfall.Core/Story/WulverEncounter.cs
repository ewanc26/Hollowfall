using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Story;

/// <summary>What the Wulver said and gave on one press of Interact.</summary>
public readonly record struct WulverSpeech(string Speaker, string Text, bool GiftGiven, double Healed, double Cleansed, Loot? Perk);

/// <summary>
/// One meeting with the Wulver, for one run of one seed: how near you must stand, which line he says, and his gift. The gift is given
/// once: a full heal, the hard damage washed away, and a timed Open guard perk (the same perk a chest can hold). Later presses only
/// bring the next line. Pure logic with no engine types; the host shows the words and plays the sound.
/// </summary>
public sealed class WulverEncounter(long seed, WulverSpot spot)
{
    /// <summary>How far, in blocks along the ground, you may stand and still speak to him.</summary>
    public const double Reach = 3.0;

    public const double ReachHeight = 3.0;

    /// <summary>The perk he gives: a wide parry for a good while.</summary>
    public static readonly Loot Gift = new(LootKind.WideParry, LootRarity.Rare, 0.18, 40);

    public WulverSpot Spot { get; } = spot;
    public int Visits { get; private set; }
    public bool GiftTaken { get; private set; }

    public bool IsNear(double x, double y, double z)
    {
        if (Math.Abs(y - Spot.Y) > ReachHeight) return false;
        double dx = x - Spot.X, dz = z - Spot.Z;
        return dx * dx + dz * dz <= Reach * Reach;
    }

    /// <summary>He speaks. On the first press he also gives his gift to <paramref name="vitals"/> and <paramref name="perks"/>.</summary>
    public WulverSpeech Speak(PlayerVitals vitals, Perks perks)
    {
        string text = WulverVoice.LineFor(seed, Visits++);
        if (GiftTaken || vitals.Dead) return new WulverSpeech(WulverVoice.Name, text, false, 0, 0, null);

        GiftTaken = true;
        double cleansed = vitals.Cleanse();
        double healed = vitals.Heal(PlayerVitals.MaxHealth);
        perks.Grant(Gift);
        return new WulverSpeech(WulverVoice.Name, text, true, healed, cleansed, Gift);
    }
}
