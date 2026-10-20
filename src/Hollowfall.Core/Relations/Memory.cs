namespace Hollowfall.Core.Relations;

/// <summary>
/// What one individual remembers about the player: how angry it is (it was hurt, or saw its kin hurt), how afraid, and how
/// grateful. All three run 0 to 1 and fade with time at a rate set by personality: a grudge-bearer stays angry for minutes,
/// a brave one stops being afraid quickly.
/// </summary>
public sealed class Memory
{
    // ---- tuning ---------------------------------------------------------------------------------

    /// <summary>Anger gained from being hurt (plus a share of the health lost) and fear gained (less for the brave).</summary>
    public const double AngerPerHurt = 0.4;
    public const double FearPerHurt = 0.3;

    /// <summary>What seeing someone else hurt does, at full strength, to a witness who is kin: anger; and fear for anyone close.</summary>
    public const double AngerFromWitness = 0.55;
    public const double FearFromWitness = 0.5;

    /// <summary>A hit that does not kill is this much as bad as a kill, for witnesses.</summary>
    public const double HarmSeverity = 0.4;

    public const double GratitudePerGift = 0.6;

    /// <summary>Seconds for full anger to fade is <c>AngerBaseSeconds + AngerGrudgeSeconds * grudge</c>.</summary>
    public const double AngerBaseSeconds = 15;
    public const double AngerGrudgeSeconds = 225;

    /// <summary>Seconds for full fear to fade is <c>FearBaseSeconds - FearBraverySeconds * bravery</c>.</summary>
    public const double FearBaseSeconds = 25;
    public const double FearBraverySeconds = 15;

    public const double GratitudeSeconds = 600;

    public double Anger { get; private set; }
    public double Fear { get; private set; }
    public double Gratitude { get; private set; }

    public bool Blank => Anger == 0 && Fear == 0 && Gratitude == 0;

    /// <summary>The creature was hurt by the player, for <paramref name="healthFraction"/> of its health.</summary>
    public void Hurt(double healthFraction, Traits traits)
    {
        Anger = Clamp(Anger + AngerPerHurt + 0.5 * Clamp(healthFraction));
        Fear = Clamp(Fear + FearPerHurt * (1 - 0.6 * traits.Bravery) + 0.4 * Clamp(healthFraction));
    }

    /// <summary>
    /// It saw the player hurt (<paramref name="severity"/> 1 for a kill) a creature it regards as <paramref name="toVictim"/>.
    /// Kin and friends of the victim grow angry; anyone who is not the victim's enemy grows afraid, less so if brave.
    /// </summary>
    public void Witnessed(Stance toVictim, double severity, Traits traits)
    {
        double kin = toVictim switch { Stance.Ally => 1, Stance.Friendly => 0.5, _ => 0 };
        double scare = toVictim switch { Stance.Ally => 1, Stance.Friendly => 0.8, Stance.Neutral => 0.5, Stance.Wary => 0.2, _ => 0 };
        Anger = Clamp(Anger + AngerFromWitness * kin * severity);
        Fear = Clamp(Fear + FearFromWitness * scare * severity * (1 - 0.6 * traits.Bravery));
    }

    /// <summary>It heard the alarm from someone nearby who saw it: a weaker echo.</summary>
    public void Alarmed(double strength, bool kin, Traits traits)
    {
        Fear = Clamp(Fear + 0.5 * FearFromWitness * strength * (1 - 0.6 * traits.Bravery));
        if (kin) Anger = Clamp(Anger + 0.5 * AngerFromWitness * strength);
    }

    public void Gift() => Gratitude = Clamp(Gratitude + GratitudePerGift);

    public void Tick(double dt, Traits traits)
    {
        if (dt <= 0) return;
        Anger = Clamp(Anger - dt / (AngerBaseSeconds + AngerGrudgeSeconds * traits.Grudge));
        Fear = Clamp(Fear - dt / (FearBaseSeconds - FearBraverySeconds * traits.Bravery));
        Gratitude = Clamp(Gratitude - dt / GratitudeSeconds);
    }

    /// <summary>Sets the values directly, for scripted scenes and tests.</summary>
    public void Set(double anger, double fear, double gratitude)
    {
        Anger = Clamp(anger);
        Fear = Clamp(fear);
        Gratitude = Clamp(gratitude);
    }

    public void Clear() => Set(0, 0, 0);

    private static double Clamp(double v) => double.IsFinite(v) ? Math.Clamp(v, 0, 1) : 0;
}
