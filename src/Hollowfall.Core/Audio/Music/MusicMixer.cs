namespace Hollowfall.Core.Audio.Music;

/// <summary>
/// Turns the game's state into how loud each layer of the music should be. <see cref="Targets"/> is the pure rule (a state in, a
/// gain for every layer out); the instance adds the time: smoothing with a slow release and a faster attack, a hold so combat does
/// not flicker, the dialogue duck, the pause filter, the death fade and the crossfade between contexts. No engine types and no
/// randomness: the same states at the same times always give the same gains.
/// </summary>
public sealed class MusicMixer
{
    private static readonly int LayerCount = Enum.GetValues<MusicLayer>().Length;
    private static readonly int ContextCount = Enum.GetValues<MusicContext>().Length;

    private readonly double[] _layer = new double[LayerCount];
    private readonly double[] _context = new double[ContextCount];
    private double _combatHold, _duck, _pause, _start;
    private bool _first = true;

    /// <summary>Whether the music is in its combat mood. It starts at once and ends only after <see cref="MusicTuning.CombatHoldSeconds"/> of calm.</summary>
    public bool Combat { get; private set; }

    /// <summary>Whether the heavy drum is in. It comes in above <see cref="MusicTuning.HeavyOn"/> and goes below <see cref="MusicTuning.HeavyOff"/>.</summary>
    public bool Heavy { get; private set; }

    /// <summary>How hard the fight is, 0 to 1, from the last state.</summary>
    public double Intensity { get; private set; }

    // ---- the pure rule -----------------------------------------------------------------------

    /// <summary>How hard a fight is, 0 to 1: a base, the enemies close, the style rank and a locked-door encounter. The boss is always 1.</summary>
    public static double IntensityOf(MusicState s)
    {
        if (s.BossActive) return 1;
        double enemies = Math.Min(Math.Max(s.EnemiesNear, 0), MusicTuning.MaxEnemiesCounted) / (double)MusicTuning.MaxEnemiesCounted;
        double rank = Math.Clamp(s.StyleRank, 0, 8) / 8.0;
        double v = MusicTuning.IntensityBase + MusicTuning.IntensityEnemies * enemies + MusicTuning.IntensityStyle * rank + (s.EncounterActive ? MusicTuning.IntensityEncounter : 0);
        return Math.Clamp(v, 0, 1);
    }

    /// <summary>The state would put the music in combat (before the hold that keeps it from flickering).</summary>
    public static bool WantsCombat(MusicState s) => s.EncounterActive || s.EnemiesNear > 0 || s.BossActive;

    /// <summary>
    /// The gain wanted for every layer, indexed by <see cref="MusicLayer"/>, each 0 to <see cref="MusicTuning.MasterHeadroom"/>. The
    /// sum of the gains weighted by the layers' peaks (the drone at its largest boost) is never more than 1, so the loudest possible
    /// mix cannot clip. Pause and dialogue are not here: they are applied by the instance, smoothly.
    /// </summary>
    public static double[] Targets(MusicState s, bool combat, bool heavy)
    {
        var t = new double[LayerCount];
        if (s.Dead) return t;

        var context = MusicContexts.Of(s);
        double intensity = IntensityOf(s);
        bool boss = s.BossActive;
        bool calm = !combat && !boss;

        t[(int)MusicLayer.Drone] = boss ? MusicTuning.DroneBoss : s.Underground ? MusicTuning.DroneUnderground : MusicTuning.DroneSurface;

        double harp = !calm ? (boss ? 0 : MusicTuning.HarpInCombat) : context switch
        {
            MusicContext.Mossy or MusicContext.Ashen => MusicTuning.HarpSparse,
            MusicContext.WulverHill => MusicTuning.HarpGentle,
            _ => MusicTuning.HarpCalm,
        };
        double whistle = !calm ? 0 : context switch
        {
            MusicContext.Surface => MusicTuning.WhistleSurface,
            MusicContext.WulverHill => MusicTuning.WhistleWulver,
            MusicContext.Plain => MusicTuning.WhistlePlain,
            MusicContext.Mossy => MusicTuning.WhistleMossy,
            _ => 0,
        };
        if (s.NearHearth)
        {
            harp = Math.Min(harp, MusicTuning.HarpNearHearth);
            whistle = Math.Min(whistle, MusicTuning.WhistleNearHearth);
        }
        t[(int)MusicLayer.Harp] = harp;
        t[(int)MusicLayer.Whistle] = whistle;

        t[(int)MusicLayer.Pulse] = boss ? MusicTuning.BossPulse
            : combat ? MusicTuning.PulseBase + MusicTuning.PulseSpan * intensity
            : context switch { MusicContext.Plain => MusicTuning.PulsePlain, MusicContext.Mossy => MusicTuning.PulseMossy, MusicContext.Ashen => MusicTuning.PulseAshen, _ => 0 };

        t[(int)MusicLayer.DrumLight] = boss ? MusicTuning.BossDrum : combat ? MusicTuning.DrumLightBase + MusicTuning.DrumLightSpan * intensity : 0;
        t[(int)MusicLayer.DrumHeavy] = boss ? MusicTuning.BossDrum : combat && heavy ? MusicTuning.DrumHeavyBase + MusicTuning.DrumHeavySpan * intensity : 0;

        double shimmer = 0;
        if (s.PassiveEntityNearby) shimmer = Math.Max(shimmer, MusicTuning.ShimmerPassive);
        if (s.PortalLinked) shimmer = Math.Max(shimmer, MusicTuning.ShimmerPortal);
        if (combat && !boss && intensity >= MusicTuning.ShimmerTensionIntensity) shimmer = Math.Max(shimmer, MusicTuning.ShimmerTension);
        t[(int)MusicLayer.Shimmer] = shimmer;

        t[(int)MusicLayer.Choir] = boss ? MusicTuning.BossChoir : 0;

        double health = Math.Clamp(double.IsNaN(s.HealthFraction) ? 1 : s.HealthFraction, 0, 1);
        t[(int)MusicLayer.Heartbeat] = health >= MusicTuning.HeartbeatStart ? 0
            : MusicTuning.HeartbeatMax * Math.Clamp((MusicTuning.HeartbeatStart - health) / (MusicTuning.HeartbeatStart - MusicTuning.HeartbeatFull), 0, 1);

        t[(int)MusicLayer.Hearth] = s.NearHearth && calm ? MusicTuning.HearthLevel : 0;

        double worst = 0;
        for (int i = 0; i < LayerCount; i++)
        {
            t[i] *= MusicTuning.MasterHeadroom;
            worst += t[i] * MusicTuning.Peak((MusicLayer)i) * ((MusicLayer)i == MusicLayer.Drone ? MusicTuning.DroneBoost : 1);
        }
        if (worst > 1)
            for (int i = 0; i < LayerCount; i++) t[i] /= worst;
        return t;
    }

    // ---- the smoother ------------------------------------------------------------------------

    /// <summary>The smoothed gain of a layer, before the duck, the pause and the start fade.</summary>
    public double Smoothed(MusicLayer layer) => _layer[(int)layer];

    /// <summary>How much of a context's loops is wanted, 0 to 1. They add up to at most 1.</summary>
    public double ContextWeight(MusicContext context) => _context[(int)context];

    /// <summary>The dialogue duck now: 1 is none, <see cref="MusicTuning.DialogueLevel"/> is full.</summary>
    public double DuckFactor => 1 + (MusicTuning.DialogueLevel - 1) * _duck;

    /// <summary>How far the pause filter has closed, 0 to 1.</summary>
    public double PauseAmount => _pause;

    /// <summary>The level of the whole mix: ramps up from silence at the start.</summary>
    public double StartLevel => _start;

    /// <summary>The gain of one layer: its smoothed level, the duck, the pause filter (which takes the highs first) and the start ramp. 0 to 1.</summary>
    public double LayerGain(MusicLayer layer)
    {
        double keep = 1 + (MusicTuning.PauseKeep(layer) * MusicTuning.PauseLevel - 1) * _pause;
        return Math.Clamp(_layer[(int)layer] * DuckFactor * keep * _start, 0, 1);
    }

    /// <summary>The gain of a layer in a context's loop: what the engine sets on that stream.</summary>
    public double Gain(MusicContext context, MusicLayer layer) => LayerGain(layer) * _context[(int)context];

    /// <summary>Moves the mix toward what the state asks for. <paramref name="seconds"/> is the frame's time, whether or not the game is paused.</summary>
    public void Update(MusicState state, double seconds)
    {
        double dt = Math.Clamp(double.IsNaN(seconds) ? 0 : seconds, 0, MusicTuning.MaxStepSeconds);

        // ---- combat with a hold, and the heavy drum with hysteresis
        if (state.Dead) { Combat = false; Heavy = false; _combatHold = 0; }
        else if (WantsCombat(state)) { Combat = true; _combatHold = MusicTuning.CombatHoldSeconds; }
        else if (Combat)
        {
            _combatHold -= dt;
            if (_combatHold <= 0) Combat = false;
        }
        Intensity = IntensityOf(state);
        if (!Combat) Heavy = false;
        else if (state.BossActive || Intensity >= MusicTuning.HeavyOn) Heavy = true;
        else if (Intensity <= MusicTuning.HeavyOff) Heavy = false;

        // ---- the contexts cross over, the first state taking effect at once
        var wanted = (int)MusicContexts.Of(state);
        if (_first)
        {
            _context[wanted] = 1;
            _first = false;
        }
        else
        {
            double step = dt / MusicTuning.ContextCrossfadeSeconds;
            for (int i = 0; i < ContextCount; i++)
                _context[i] = i == wanted ? Math.Min(1, _context[i] + step) : Math.Max(0, _context[i] - step);
            double sum = _context.Sum();
            if (sum > 1) for (int i = 0; i < ContextCount; i++) _context[i] /= sum;
        }

        // ---- every layer slides toward its target: in at its attack rate, out at its release rate, all together on death
        var targets = Targets(state, Combat, Heavy);
        for (int i = 0; i < LayerCount; i++)
        {
            var layer = (MusicLayer)i;
            double target = targets[i];
            double rate = target > _layer[i] ? 1 / MusicTuning.AttackSeconds(layer)
                : state.Dead ? 1 / MusicTuning.DeathFadeSeconds
                : 1 / MusicTuning.ReleaseSeconds(layer);
            _layer[i] = Approach(_layer[i], target, rate * dt);
        }

        _duck = Approach(_duck, state.DialogueOpen ? 1 : 0, dt / (state.DialogueOpen ? MusicTuning.DuckInSeconds : MusicTuning.DuckOutSeconds));
        _pause = Approach(_pause, state.Paused ? 1 : 0, dt / (state.Paused ? MusicTuning.DuckInSeconds : MusicTuning.DuckOutSeconds));
        _start = Approach(_start, 1, dt / MusicTuning.StartFadeSeconds);
    }

    private static double Approach(double current, double target, double step) =>
        current < target ? Math.Min(target, current + step) : Math.Max(target, current - step);
}
