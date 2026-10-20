namespace Hollowfall.Core.Relations;

/// <summary>The named bands of a standing, from the worst to the best.</summary>
public enum ReputationTier
{
    Hated,
    Hostile,
    Wary,
    Neutral,
    Friendly,
    Trusted,
    Revered,
}

/// <summary>Where the tiers begin on the -100 to 100 scale, and the margin that stops a standing flickering across a boundary.</summary>
public static class ReputationTiers
{
    public const double Min = -100;
    public const double Max = 100;

    /// <summary>A standing must pass a boundary by this much to rise into a tier, and fall this far below it to leave one.</summary>
    public const double Hysteresis = 4;

    /// <summary>The lowest standing of a tier. Hated reaches down to <see cref="Min"/>.</summary>
    public static double LowerBound(ReputationTier tier) => tier switch
    {
        ReputationTier.Hated => Min,
        ReputationTier.Hostile => -75,
        ReputationTier.Wary => -40,
        ReputationTier.Neutral => -10,
        ReputationTier.Friendly => 15,
        ReputationTier.Trusted => 50,
        _ => 80,
    };

    /// <summary>The tier a value falls in with no history (used at the start of a run and when loading).</summary>
    public static ReputationTier Plain(double value)
    {
        var tier = ReputationTier.Hated;
        while (tier < ReputationTier.Revered && value >= LowerBound(tier + 1)) tier++;
        return tier;
    }

    /// <summary>The tier after a change in value: it moves only when the value clears a boundary by <see cref="Hysteresis"/>.</summary>
    public static ReputationTier Resolve(double value, ReputationTier current)
    {
        var tier = current;
        while (tier < ReputationTier.Revered && value >= LowerBound(tier + 1) + Hysteresis) tier++;
        while (tier > ReputationTier.Hated && value < LowerBound(tier) - Hysteresis) tier--;
        return tier;
    }

    /// <summary>How the tier reads in a sentence: "The Folk now regard you as Wary".</summary>
    public static string Label(ReputationTier tier) => tier.ToString();

    public static Stance ToStance(ReputationTier tier) => tier switch
    {
        ReputationTier.Hated => Stance.Enemy,
        ReputationTier.Hostile => Stance.Hostile,
        ReputationTier.Wary => Stance.Wary,
        ReputationTier.Neutral => Stance.Neutral,
        ReputationTier.Friendly or ReputationTier.Trusted => Stance.Friendly,
        _ => Stance.Ally,
    };
}

/// <summary>One faction's standing moved.</summary>
public readonly record struct ReputationChange(Faction Faction, double OldValue, double NewValue, ReputationTier OldTier, ReputationTier NewTier)
{
    public double Delta => NewValue - OldValue;
    public bool TierChanged => OldTier != NewTier;
}

/// <summary>A line of the recent-changes list on the Standing screen.</summary>
public readonly record struct ReputationNote(Faction Faction, double Delta, string Reason, double Time);

/// <summary>
/// The player's standing with every faction, -100 to 100, in named tiers with hysteresis. Acting on a faction moves several
/// standings at once through the ripple table, standings drift back toward neutral over time (except for the hated), and the
/// whole thing is serialisable. Reputation belongs to a run: <see cref="Reset"/> puts every faction back to where it began.
/// </summary>
public sealed class Reputation
{
    public const int MaxNotes = 8;

    /// <summary>Points per second a standing drifts toward zero. The hated and the Hollow (whose stance is fixed) do not forgive.</summary>
    public const double DecayPerSecond = 0.02;

    private readonly double[] _values = new double[Enum.GetValues<Faction>().Length];
    private readonly ReputationTier[] _tiers = new ReputationTier[Enum.GetValues<Faction>().Length];
    private readonly List<ReputationNote> _notes = [];

    public Reputation() => Reset();

    /// <summary>Most recent first.</summary>
    public IReadOnlyList<ReputationNote> RecentChanges => _notes;

    public double Value(Faction f) => _values[(int)f];

    public ReputationTier Tier(Faction f) => _tiers[(int)f];

    /// <summary>Puts every standing back to its starting value and forgets the recent changes.</summary>
    public void Reset()
    {
        foreach (var info in FactionGraph.All)
        {
            _values[(int)info.Faction] = info.StartStanding;
            _tiers[(int)info.Faction] = ReputationTiers.Plain(info.StartStanding);
        }
        _notes.Clear();
    }

    /// <summary>Sets a standing directly (clamped), for loading, scripted scenes and tests. Returns the change, if any.</summary>
    public ReputationChange? Set(Faction f, double value)
    {
        if (f == Faction.Delver || !double.IsFinite(value)) return null;
        return Move(f, Math.Clamp(value, ReputationTiers.Min, ReputationTiers.Max), forceTier: true);
    }

    /// <summary>
    /// Records that the player did <paramref name="act"/> to a member of <paramref name="target"/>. Every faction in the ripple
    /// table moves by the act's size times its weight (scaled by <paramref name="scale"/>). Second-hand gains stop at
    /// <see cref="FactionGraph.RippleCeiling"/>, and merely hurting someone never makes anyone like you more.
    /// </summary>
    public List<ReputationChange> Apply(RelationAct act, Faction target, double scale = 1, string? reason = null, double time = 0)
    {
        var changes = new List<ReputationChange>();
        if (target == Faction.Delver || !double.IsFinite(scale)) return changes;
        double size = FactionGraph.Size(act) * scale;
        foreach (var ripple in FactionGraph.Ripples(act, target))
        {
            double delta = size * ripple.Weight;
            if (delta == 0) continue;
            bool direct = ripple.Affected == target;
            if (delta > 0 && !direct && act != RelationAct.Help)
            {
                // Approval of a kill is second-hand; a mere wound earns none.
                if (act == RelationAct.Harm) continue;
            }
            double old = _values[(int)ripple.Affected];
            double next = old + delta;
            if (delta > 0 && !direct) next = Math.Min(next, Math.Max(old, FactionGraph.RippleCeiling));
            next = Math.Clamp(next, ReputationTiers.Min, ReputationTiers.Max);
            if (Move(ripple.Affected, next) is { } change)
            {
                changes.Add(change);
                AddNote(new ReputationNote(ripple.Affected, change.Delta, reason ?? act.ToString(), time));
            }
        }
        return changes;
    }

    /// <summary>Lets standings drift toward neutral. Returns the tier changes, if any.</summary>
    public List<ReputationChange> Tick(double dt)
    {
        var changes = new List<ReputationChange>();
        if (dt <= 0) return changes;
        foreach (var info in FactionGraph.All)
        {
            var f = info.Faction;
            if (f == Faction.Delver || info.Fixed || _tiers[(int)f] == ReputationTier.Hated) continue;
            double v = _values[(int)f];
            if (v == 0) continue;
            double step = DecayPerSecond * dt;
            double next = v > 0 ? Math.Max(0, v - step) : Math.Min(0, v + step);
            if (Move(f, next) is { } change && change.TierChanged) changes.Add(change);
        }
        return changes;
    }

    private ReputationChange? Move(Faction f, double next, bool forceTier = false)
    {
        int i = (int)f;
        double old = _values[i];
        var oldTier = _tiers[i];
        _values[i] = next;
        _tiers[i] = forceTier ? ReputationTiers.Plain(next) : ReputationTiers.Resolve(next, oldTier);
        return next == old ? null : new ReputationChange(f, old, next, oldTier, _tiers[i]);
    }

    private void AddNote(ReputationNote note)
    {
        _notes.Insert(0, note);
        if (_notes.Count > MaxNotes) _notes.RemoveRange(MaxNotes, _notes.Count - MaxNotes);
    }

    /// <summary>The standings as plain data, for saving.</summary>
    public Dictionary<Faction, double> Snapshot() => FactionGraph.Standable.ToDictionary(f => f, f => _values[(int)f]);

    /// <summary>Loads standings, clamping them and ignoring unknown or non-finite entries. Returns how many were applied.</summary>
    public int Restore(IReadOnlyDictionary<Faction, double> values)
    {
        int n = 0;
        foreach (var (f, v) in values)
        {
            if (f == Faction.Delver || !Enum.IsDefined(f) || !double.IsFinite(v)) continue;
            double c = Math.Clamp(v, ReputationTiers.Min, ReputationTiers.Max);
            _values[(int)f] = c;
            _tiers[(int)f] = ReputationTiers.Plain(c);
            n++;
        }
        return n;
    }
}
