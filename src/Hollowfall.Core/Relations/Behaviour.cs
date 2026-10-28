using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Relations;

/// <summary>What a creature is trying to do this moment. The sim turns it into movement and attacks.</summary>
public enum Intent
{
    /// <summary>Wander near home; ignore the player.</summary>
    Idle,
    Flee,

    /// <summary>Squared up and growling: the last chance to back off before it attacks.</summary>
    Warn,
    Attack,

    /// <summary>Keep company with the player at a distance.</summary>
    Follow,

    /// <summary>Go and fight an enemy of the player.</summary>
    Assist,
}

/// <summary>The little coloured pip above a creature: how the player should read it.</summary>
public enum Cue
{
    None,
    Friendly,
    Wary,
    Hostile,
    Allied,
}

public readonly record struct DispositionInput(SpeciesInfo Species, Stance Stance, Traits Traits, bool Provoked, bool Scared);

/// <summary>
/// Turns a faction's regard, a personality and a memory into a disposition. A pure function: the same inputs always give the
/// same answer, and it is tested across the whole matrix of stance, temperament, personality and memory.
/// </summary>
public static class Dispositions
{
    /// <summary>Gratitude at or above this warms a creature's stance by one step.</summary>
    public const double GratefulAt = 0.5;

    /// <summary>A coward flees instead of fighting even when angry.</summary>
    public const double CowardBelow = 0.35;

    /// <summary>The stance a creature takes toward the player: its faction's, or the one the player's tier earns, warmed by gratitude.</summary>
    public static Stance EffectiveStance(SpeciesInfo species, ReputationTier tier, double gratitude)
    {
        var info = FactionGraph.Info(species.Faction);
        if (info.Fixed) return info.TowardPlayer;
        var stance = ReputationTiers.ToStance(tier);
        if (gratitude >= GratefulAt && stance < Stance.Ally) stance++;
        return stance;
    }

    public static Disposition Decide(DispositionInput i)
    {
        var sp = i.Species;
        if (sp.AlwaysHostile) return Disposition.Aggressive;

        // Memory comes first: a creature that has been hurt or frightened acts on it whatever it thinks of the player's standing.
        if (i.Provoked)
        {
            if (sp.Pacifist) return Disposition.Skittish;
            return i.Scared && i.Traits.Bravery < CowardBelow ? Disposition.Skittish : Disposition.Aggressive;
        }
        if (i.Scared && sp.Natural != Disposition.Aggressive && !(sp.Natural == Disposition.Territorial && i.Traits.Bravery >= 0.5))
            return Disposition.Skittish;

        var natural = sp.Natural;
        bool brave = i.Traits.Bravery >= 0.5 && !sp.Pacifist;
        switch (i.Stance)
        {
            case Stance.Ally:
                if (sp.CanAlly) return Disposition.Allied;
                return sp.CanFollow ? Disposition.Curious : Soften(natural);

            case Stance.Friendly:
                if (natural is Disposition.Passive or Disposition.Skittish or Disposition.Curious)
                    return sp.CanFollow ? Disposition.Curious : Disposition.Passive; // a friend you can walk up to
                return Disposition.Neutral;

            case Stance.Neutral:
                return natural;

            case Stance.Wary:
                return natural switch
                {
                    Disposition.Passive or Disposition.Curious => Disposition.Skittish,
                    Disposition.Neutral => Disposition.Territorial,
                    _ => natural,
                };

            case Stance.Hostile:
                return natural switch
                {
                    Disposition.Passive or Disposition.Curious => Disposition.Skittish,
                    Disposition.Skittish => brave ? Disposition.Territorial : Disposition.Skittish,
                    Disposition.Neutral => Disposition.Territorial,
                    _ => natural,
                };

            default: // Enemy
                return natural switch
                {
                    Disposition.Passive or Disposition.Curious => Disposition.Skittish,
                    Disposition.Skittish => brave ? Disposition.Aggressive : Disposition.Skittish,
                    Disposition.Neutral or Disposition.Territorial => sp.Pacifist ? Disposition.Skittish : Disposition.Aggressive,
                    _ => natural,
                };
        }
    }

    private static Disposition Soften(Disposition d) => d is Disposition.Aggressive or Disposition.Territorial ? Disposition.Neutral : d;
}

/// <summary>What a creature can sense this moment.</summary>
/// <param name="Distance">To the player.</param>
/// <param name="ApproachSpeed">How fast the player is closing on it, blocks per second (negative when moving away).</param>
/// <param name="Sees">Whether it has line of sight to the player.</param>
/// <param name="PlayerFromHome">How far the player is from the creature's home spot.</param>
/// <param name="HostileDistance">To the nearest enemy of the player it could help against, or infinity.</param>
public readonly record struct Perception(double Distance, double ApproachSpeed, bool Sees, double PlayerFromHome, double HostileDistance = double.PositiveInfinity);

/// <summary>
/// The small state machine that turns a disposition into an intent. Escalation is a warning first and an attack after it
/// (immediately if it is already furious), and de-escalation falls out of the memory fading: when the creature is no longer
/// provoked its disposition relaxes and the brain drops back to idle.
/// </summary>
public sealed class Brain
{
    public const double PassiveFleeRadius = 3.5;
    public const double SkittishRadius = 7;
    public const double FastApproach = 2.5;
    public const double FleeRelax = 3.5;
    public const double WarnSlowest = 1.4;
    public const double WarnFastest = 0.5;
    public const double Leash = 4;
    public const double CuriousRange = 14;
    public const double CuriousKeep = 4;
    public const double AlliedKeep = 3.5;
    public const double AssistRange = 10;

    /// <summary>Anger at or above this skips the warning.</summary>
    public const double FuryAnger = 0.7;

    private double _warn;

    public Intent Intent { get; private set; } = Intent.Idle;

    public double WarnLeft => Intent == Intent.Warn ? Math.Max(0, _warn) : 0;

    public Intent Step(double dt, Disposition disposition, SpeciesInfo species, Traits traits, bool provoked, bool scared, bool furious, Perception p)
    {
        var next = Decide(dt, disposition, species, traits, provoked, scared, furious, p);
        if (next is not (Intent.Warn or Intent.Attack)) _warn = 0;
        return Intent = next;
    }

    public Intent StepVillager(Perception p, Traits t, double anger, double fear)
    {
        var next = VillagerNet.Decide(new VillagerSense(p, t, anger, fear, Intent));
        if (next is not (Intent.Warn or Intent.Attack)) _warn = 0;
        return Intent = next;
    }

    public void Reset()
    {
        Intent = Intent.Idle;
        _warn = 0;
    }

    private Intent Decide(double dt, Disposition d, SpeciesInfo sp, Traits t, bool provoked, bool scared, bool furious, Perception p)
    {
        switch (d)
        {
            case Disposition.Passive:
                {
                    double radius = PassiveFleeRadius * (1.3 - t.Bravery);
                    if (scared || provoked) return Intent.Flee;
                    if (p.Sees && p.Distance < radius) return Intent.Flee;
                    return Intent == Intent.Flee && p.Distance < radius + FleeRelax ? Intent.Flee : Intent.Idle;
                }

            case Disposition.Skittish:
                {
                    double radius = SkittishRadius * (1.2 - 0.6 * t.Bravery);
                    if (scared || provoked) return Intent.Flee;
                    if (p.Sees && p.Distance < radius && (p.ApproachSpeed >= FastApproach || p.Distance < radius * 0.5)) return Intent.Flee;
                    return Intent == Intent.Flee && p.Distance < radius + FleeRelax ? Intent.Flee : Intent.Idle;
                }

            case Disposition.Neutral:
                return Intent.Idle;

            case Disposition.Territorial:
                {
                    double radius = sp.TerritoryRadius * (0.8 + 0.4 * t.Bravery);
                    bool engaged = Intent is Intent.Warn or Intent.Attack;
                    if (engaged && p.PlayerFromHome <= radius + Leash && p.Distance < 30) return Engage(dt, t, furious);
                    if (p.Sees && p.PlayerFromHome <= radius) return Engage(dt, t, furious);
                    return Intent.Idle;
                }

            case Disposition.Aggressive:
                if (sp.AlwaysHostile) return Intent.Attack;
                if (provoked) return p.Distance < 30 || Intent == Intent.Attack ? Engage(dt, t, furious) : Intent.Idle;
                return p.Sees && p.Distance <= sp.AggroRange ? Intent.Attack : Intent == Intent.Attack && p.Distance <= sp.AggroRange ? Intent.Attack : Intent.Idle;

            case Disposition.Curious:
                if (!p.Sees || p.Distance > CuriousRange) return Intent.Idle;
                if (p.ApproachSpeed > 4 || p.Distance < 1.8) return Intent.Idle;
                return p.Distance > CuriousKeep ? Intent.Follow : Intent.Idle;

            default: // Allied: never attacks the player
                if (p.HostileDistance <= AssistRange) return Intent.Assist;
                return p.Distance > AlliedKeep ? Intent.Follow : Intent.Idle;
        }
    }

    private Intent Engage(double dt, Traits t, bool furious)
    {
        if (Intent == Intent.Attack || furious) return Intent.Attack;
        if (Intent != Intent.Warn) _warn = WarnSlowest + (WarnFastest - WarnSlowest) * t.Bravery;
        _warn -= dt;
        return _warn <= 0 ? Intent.Attack : Intent.Warn;
    }
}

[Flags]
public enum MindEvents
{
    None = 0,
    Provoked = 1,
    Calmed = 2,
    Allied = 4,
}

/// <summary>
/// One creature's whole inner life: its species and faction, its personality, its memory, and the brain that decides what to
/// do. The sim calls <see cref="Update"/> once per tick with what the creature can sense and the player's standing with its
/// faction, and reads <see cref="Intent"/> back.
/// </summary>
public sealed class Mind
{
    public const double WarnAnger = 0.3;
    public const double CalmAnger = 0.1;
    public const double ScaredAt = 0.5;
    public const double SteadyAt = 0.2;

    /// <summary>How far away the player must be, with a clear view, for a creature's disposition to become known (shown as a pip).</summary>
    public const double NoticeRange = 14;

    private readonly Brain _brain = new();

    public Mind(SpeciesInfo species, Traits traits, Vec3 home)
    {
        Species = species;
        Traits = traits;
        Home = home;
        Disposition = species.Natural;
    }

    public SpeciesInfo Species { get; }
    public Faction Faction => Species.Faction;
    public Traits Traits { get; }
    public Memory Memory { get; } = new();
    public Vec3 Home { get; }
    public Disposition Disposition { get; private set; }
    public Intent Intent => Species.AlwaysHostile ? Intent.Attack : _brain.Intent;
    public double WarnLeft => _brain.WarnLeft;
    public bool Provoked { get; private set; }
    public bool Scared { get; private set; }
    public bool Known { get; private set; }
    public bool BoonGiven { get; internal set; }

    /// <summary>Whether the player has already made this creature an offering.</summary>
    public bool Offered { get; internal set; }

    // Bookkeeping the sim keeps for the creature: the player's distance last tick (for approach speed) and an ally's strike timer.
    internal double LastDistance { get; set; } = double.NaN;
    internal double StrikeCooldown { get; set; }

    /// <summary>When it was last hurt by the player, in sim seconds (so a volley of pellets counts once).</summary>
    public double LastHurtAt { get; internal set; } = double.NegativeInfinity;

    /// <summary>The pip colour. None until the player has noticed it, and always none for the Hollow's own (they are plainly hostile).</summary>
    public Cue Cue
    {
        get
        {
            if (!Known || Species.AlwaysHostile) return Cue.None;
            if (Disposition == Disposition.Allied) return Cue.Allied;
            if (Disposition == Disposition.Aggressive || Intent is Intent.Warn or Intent.Attack) return Cue.Hostile;
            if (Disposition is Disposition.Passive or Disposition.Curious) return Cue.Friendly;
            return Cue.Wary;
        }
    }

    public MindEvents Update(double dt, ReputationTier tier, Perception p)
    {
        var events = MindEvents.None;
        if (Species.AlwaysHostile)
        {
            Disposition = Disposition.Aggressive;
            _brain.Step(dt, Disposition, Species, Traits, false, false, false, p);
            return events;
        }

        Memory.Tick(dt, Traits);
        if (!Provoked && Memory.Anger >= WarnAnger) { Provoked = true; events |= MindEvents.Provoked; }
        else if (Provoked && Memory.Anger <= CalmAnger) { Provoked = false; events |= MindEvents.Calmed; _brain.Reset(); }
        if (!Scared && Memory.Fear >= ScaredAt) Scared = true;
        else if (Scared && Memory.Fear <= SteadyAt) Scared = false;
        if (p.Sees && p.Distance <= NoticeRange) Known = true;

        var before = Disposition;
        var stance = Dispositions.EffectiveStance(Species, tier, Memory.Gratitude);
        Disposition = Dispositions.Decide(new DispositionInput(Species, stance, Traits, Provoked, Scared));
        // A trader keeps his pitch: he may refuse to serve a hated Delver, but he does not run from a customer. Once hurt or
        // frightened he is a pacifist like any other, and the ordinary fear rules take over.
        if (Species.Kind == EnemyKind.Pedlar && !Provoked && !Scared) Disposition = Disposition.Neutral;
        if (Disposition == Disposition.Allied && before != Disposition.Allied) events |= MindEvents.Allied;

        if (Species.Kind == EnemyKind.Villager) _brain.StepVillager(p, Traits, Memory.Anger, Memory.Fear);
        else _brain.Step(dt, Disposition, Species, Traits, Provoked, Scared, Memory.Anger >= Brain.FuryAnger, p);
        return events;
    }
}
