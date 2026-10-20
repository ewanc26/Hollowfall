using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Relations;

/// <summary>
/// The relationship system for one run: the player's reputation, and the rules that turn what the player does (hurting,
/// killing, giving) into standing, into creatures' memories and into news that spreads. <see cref="CombatSim"/> owns an
/// optional one; with none, nothing here runs and the enemies behave as they always did.
/// </summary>
public sealed class RelationSystem
{
    /// <summary>Witnesses within this many blocks, with a clear line of sight to the victim, see what happened.</summary>
    public const double WitnessRadius = 14;

    /// <summary>A witness that takes alarm tells those within this many blocks of it, line of sight or not.</summary>
    public const double AlarmRadius = 8;

    /// <summary>Seconds between hurts that count: a shotgun blast is one hurt, not eight.</summary>
    public const double HurtDebounce = 0.2;

    /// <summary>A Bean Nighe within this many blocks of the player warns of an ambush.</summary>
    public const double OmenRadius = 12;

    private readonly Func<Vec3, Vec3, bool> _lineOfSight;
    private readonly Action<CombatEvent> _emit;

    public RelationSystem(long seed, Func<Vec3, Vec3, bool> lineOfSight, Action<CombatEvent> emit, Reputation? start = null)
    {
        Seed = seed;
        _lineOfSight = lineOfSight;
        _emit = emit;
        Reputation = start ?? new Reputation();
    }

    public long Seed { get; }
    public Reputation Reputation { get; }

    /// <summary>True once a Bean Nighe has warned of an ambush and the next encounter has not yet sprung it.</summary>
    public bool OmenArmed { get; internal set; }

    /// <summary>A mind for a creature of a kind standing at a place: its personality comes from the seed and the place.</summary>
    public Mind CreateMind(EnemyKind kind, Vec3 feet)
    {
        var species = SpeciesInfo.Of(kind);
        var traits = Traits.For(Seed, species, (int)Math.Floor(feet.X), (int)Math.Floor(feet.Y), (int)Math.Floor(feet.Z));
        return new Mind(species, traits, feet);
    }

    /// <summary>Advances reputation decay; announces tier changes.</summary>
    public void Tick(double dt)
    {
        foreach (var change in Reputation.Tick(dt)) Announce(change);
    }

    /// <summary>The player hurt <paramref name="victim"/> (or killed it). Updates standing, the victim's memory and every witness.</summary>
    public void PlayerHurt(Enemy victim, double damage, bool killed, IReadOnlyList<Enemy> everyone, double time)
    {
        if (victim.Mind is not { } mind) return;
        bool counts = killed || time - mind.LastHurtAt >= HurtDebounce;
        if (!counts) return;
        mind.LastHurtAt = time;

        string reason = (killed ? "Killed a " : "Hurt a ") + mind.Species.Name;
        foreach (var change in Reputation.Apply(killed ? RelationAct.Kill : RelationAct.Harm, mind.Faction, 1, reason, time)) Announce(change);

        double severity = killed ? 1 : Memory.HarmSeverity;
        if (!killed) mind.Memory.Hurt(Math.Clamp(damage / victim.Spec.Health, 0, 1), mind.Traits);
        SpreadNews(victim, severity, everyone);
    }

    /// <summary>The player gave <paramref name="receiver"/> something it values (food, a healing): standing and gratitude rise.</summary>
    public void PlayerGave(Enemy receiver, double time)
    {
        if (receiver.Mind is not { } mind) return;
        mind.Memory.Gift();
        foreach (var change in Reputation.Apply(RelationAct.Help, mind.Faction, 1, "Gave a gift to a " + mind.Species.Name, time)) Announce(change);
    }

    private void SpreadNews(Enemy victim, double severity, IReadOnlyList<Enemy> everyone)
    {
        if (victim.Mind is not { } vm) return;
        var from = victim.Feet + new Vec3(0, victim.Spec.Height * 0.6, 0);
        var alerted = new List<Enemy>();
        foreach (var w in everyone)
        {
            if (w == victim || w.Dead || w.Mind is not { } wm || wm.Species.AlwaysHostile) continue;
            var eye = w.Feet + new Vec3(0, w.Spec.Height * 0.8, 0);
            if (eye.DistanceTo(from) > WitnessRadius || !_lineOfSight(eye, from)) continue;
            var toVictim = wm.Faction == vm.Faction ? Stance.Ally : FactionGraph.Between(wm.Faction, vm.Faction);
            double angerBefore = wm.Memory.Anger, fearBefore = wm.Memory.Fear;
            wm.Memory.Witnessed(toVictim, severity, wm.Traits);
            if (wm.Memory.Anger > angerBefore || wm.Memory.Fear > fearBefore)
            {
                alerted.Add(w);
                _emit(new WitnessAlerted(w.Id, victim.Id, wm.Faction));
            }
        }

        // Those who took real alarm cry out; whoever is near them hears, though they saw nothing.
        foreach (var w in alerted.Where(a => a.Mind!.Memory.Fear >= Mind.ScaredAt || a.Mind.Memory.Anger >= Mind.WarnAnger))
            foreach (var x in everyone)
            {
                if (x == victim || x == w || x.Dead || alerted.Contains(x) || x.Mind is not { } xm || xm.Species.AlwaysHostile) continue;
                if (x.Feet.DistanceTo(w.Feet) > AlarmRadius) continue;
                var toVictim = xm.Faction == vm.Faction ? Stance.Ally : FactionGraph.Between(xm.Faction, vm.Faction);
                xm.Memory.Alarmed(severity, toVictim >= Stance.Friendly, xm.Traits);
                _emit(new WitnessAlerted(x.Id, victim.Id, xm.Faction));
            }
    }

    private void Announce(ReputationChange change)
    {
        if (change.TierChanged) _emit(new ReputationChanged(change.Faction, change.OldTier, change.NewTier));
    }
}
