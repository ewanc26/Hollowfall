namespace Hollowfall.Core.Audio.Music;

/// <summary>The scales the score is written in. Each is a list of semitone offsets from the root.</summary>
public enum MusicMode
{
    Dorian,
    Aeolian,
    Phrygian,
    MajorPentatonic,
    MinorPentatonic,
}

/// <summary>Scale arithmetic: turns a root, a mode and a scale degree (which may be negative or run past an octave) into a MIDI note.</summary>
public static class Modes
{
    private static readonly int[] DorianSteps = [0, 2, 3, 5, 7, 9, 10];
    private static readonly int[] AeolianSteps = [0, 2, 3, 5, 7, 8, 10];
    private static readonly int[] PhrygianSteps = [0, 1, 3, 5, 7, 8, 10];
    private static readonly int[] MajorPentatonicSteps = [0, 2, 4, 7, 9];
    private static readonly int[] MinorPentatonicSteps = [0, 3, 5, 7, 10];

    public static IReadOnlyList<int> Steps(MusicMode mode) => mode switch
    {
        MusicMode.Dorian => DorianSteps,
        MusicMode.Aeolian => AeolianSteps,
        MusicMode.Phrygian => PhrygianSteps,
        MusicMode.MajorPentatonic => MajorPentatonicSteps,
        MusicMode.MinorPentatonic => MinorPentatonicSteps,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    /// <summary>The MIDI note of a scale degree: 0 is the root, 1 the next note of the scale, and the scale repeats every octave in both directions.</summary>
    public static int Midi(int root, MusicMode mode, int degree)
    {
        var steps = Steps(mode);
        int octave = (int)Math.Floor((double)degree / steps.Count);
        int index = degree - octave * steps.Count;
        return root + 12 * octave + steps[index];
    }

    /// <summary>Whether a MIDI note is a note of the scale (in any octave).</summary>
    public static bool Contains(int root, MusicMode mode, int midi)
    {
        int pc = (((midi - root) % 12) + 12) % 12;
        return Steps(mode).Contains(pc);
    }

    public static double Hertz(double midi) => 440.0 * Math.Pow(2.0, (midi - 69.0) / 12.0);
}

/// <summary>A tonic (a MIDI note, in the low octaves where the drone sits) and the mode built on it.</summary>
public readonly record struct MusicKey(int RootMidi, MusicMode Mode)
{
    public int Midi(int degree) => Modes.Midi(RootMidi, Mode, degree);

    public bool Contains(int midi) => Modes.Contains(RootMidi, Mode, midi);
}

/// <summary>Where the player is, as far as the music cares: each context has its own key, mode and character.</summary>
public enum MusicContext
{
    /// <summary>The open world: a Dorian folk mood.</summary>
    Surface,

    /// <summary>Dungeon floor 0, plain brick: cold Aeolian.</summary>
    Plain,

    /// <summary>Dungeon floor 1, mossy: darker, with a slower and sparser harp.</summary>
    Mossy,

    /// <summary>Dungeon floor 2 and deeper, ashen: Phrygian with a heavier drum.</summary>
    Ashen,

    /// <summary>The Wulver's hill: a warm major-pentatonic whistle tune over a gentle harp.</summary>
    WulverHill,
}

/// <summary>
/// The layers of the music. Every layer is rendered once as a loop of exactly the same length, so any set of them
/// can be mixed in sync, and the mixer decides how loud each one is. Append new layers at the end.
/// </summary>
public enum MusicLayer
{
    /// <summary>The bed: a drone on the root and fifth with slow pad chords over it.</summary>
    Drone,

    /// <summary>A plucked clarsach: sparse arpeggios for calm exploration.</summary>
    Harp,

    /// <summary>A breathy whistle: the surface and Wulver melody.</summary>
    Whistle,

    /// <summary>A soft low pulse for tension.</summary>
    Pulse,

    /// <summary>A frame drum with a light groove, for combat.</summary>
    DrumLight,

    /// <summary>A lower, heavier drum pattern on top of the light one, as combat intensifies.</summary>
    DrumHeavy,

    /// <summary>A high shimmering arpeggio for stealth, omens and the faint whimsy of linked portals.</summary>
    Shimmer,

    /// <summary>A low brass- and choir-like swell for the boss.</summary>
    Choir,

    /// <summary>A low heartbeat that grows as health falls.</summary>
    Heartbeat,

    /// <summary>The fireside mood: a low hum and a soft, slow harp, for hearths and fire pits.</summary>
    Hearth,
}

/// <summary>Short one-shots played over the music.</summary>
public enum Stinger
{
    FloorComplete,
    BossIntro,
    Death,
    Ending,
    WulverGreeting,
}

/// <summary>
/// Everything the music listens to, as a plain record the host fills in each frame. Nothing here is an engine type.
/// </summary>
public sealed record MusicState
{
    /// <summary>Inside the dungeon (as opposed to on the surface).</summary>
    public bool Underground { get; init; }

    /// <summary>The dungeon floor the player is on, 0 for the first; ignored on the surface.</summary>
    public int Floor { get; init; }

    /// <summary>A locked-door encounter is under way.</summary>
    public bool EncounterActive { get; init; }

    /// <summary>Living enemies near the player.</summary>
    public int EnemiesNear { get; init; }

    /// <summary>The style rank, 0 (none) to 8 (legendary).</summary>
    public int StyleRank { get; init; }

    /// <summary>Health as a fraction, 0 to 1.</summary>
    public double HealthFraction { get; init; } = 1;

    /// <summary>The boss fight is on.</summary>
    public bool BossActive { get; init; }

    public bool Dead { get; init; }

    /// <summary>The player is on the Wulver's hill, near enough to hear his tune.</summary>
    public bool NearWulver { get; init; }

    /// <summary>A conversation is open: the music ducks.</summary>
    public bool DialogueOpen { get; init; }

    /// <summary>A menu is open: the music ducks and loses its high layers.</summary>
    public bool Paused { get; init; }

    /// <summary>A peaceful creature is close (for the future relations system). Brings in the shimmer.</summary>
    public bool PassiveEntityNearby { get; init; }

    /// <summary>Both portals are open and linked. A faint shimmer.</summary>
    public bool PortalLinked { get; init; }

    /// <summary>The player is by a hearth or fire pit: the music turns to a soft harp and a low hum. Nothing sets this yet; the fire-pit feature will.</summary>
    public bool NearHearth { get; init; }
}

public static class MusicContexts
{
    /// <summary>The musical context for a state: the Wulver's hill beats the open world, and a floor's theme decides a dungeon's.</summary>
    public static MusicContext Of(MusicState s)
    {
        if (!s.Underground) return s.NearWulver ? MusicContext.WulverHill : MusicContext.Surface;
        return s.Floor switch
        {
            <= 0 => MusicContext.Plain,
            1 => MusicContext.Mossy,
            _ => MusicContext.Ashen,
        };
    }
}

/// <summary>
/// Every tuning number of the music in one place: tempo, levels, smoothing times and thresholds.
/// docs/music.md lists the same values; change them here.
/// </summary>
public static class MusicTuning
{
    public const int SampleRate = Synth.SampleRate;
    public const int Bpm = 84;
    public const int Bars = 8;
    public const int BeatsPerBar = 4;

    /// <summary>Samples in one beat: 22050 * 60 / 84 is a whole number, so beats and bars land exactly on samples.</summary>
    public const int SamplesPerBeat = SampleRate * 60 / Bpm;

    public const int LoopSamples = SamplesPerBeat * BeatsPerBar * Bars;
    public const double LoopSeconds = (double)LoopSamples / SampleRate;

    /// <summary>The loudest sample of each layer when rendered. The sum of these (the drone counted at <see cref="DroneBoost"/>) times <see cref="MasterHeadroom"/> stays under 1, so the full mix cannot clip.</summary>
    public static double Peak(MusicLayer layer) => layer switch
    {
        MusicLayer.Drone => 0.12,
        MusicLayer.Harp => 0.14,
        MusicLayer.Whistle => 0.12,
        MusicLayer.Pulse => 0.12,
        MusicLayer.DrumLight => 0.12,
        MusicLayer.DrumHeavy => 0.14,
        MusicLayer.Shimmer => 0.06,
        MusicLayer.Choir => 0.16,
        MusicLayer.Heartbeat => 0.12,
        MusicLayer.Hearth => 0.10,
        _ => 0.1,
    };

    /// <summary>The Ambience slider scales the drone layer by up to this much (the slider's full travel is 0 to this).</summary>
    public const double DroneBoost = 2.0;

    /// <summary>Every gain the mixer produces is at most this, leaving room for the layers to add up.</summary>
    public const double MasterHeadroom = 0.8;

    // ---- smoothing: seconds to travel the whole 0..1 range -------------------------------------

    public static double AttackSeconds(MusicLayer layer) => layer switch
    {
        MusicLayer.Drone => 3.0,
        MusicLayer.Harp => 2.5,
        MusicLayer.Whistle => 2.5,
        MusicLayer.Pulse => 1.5,
        MusicLayer.DrumLight => 0.6,
        MusicLayer.DrumHeavy => 0.8,
        MusicLayer.Shimmer => 1.5,
        MusicLayer.Choir => 2.0,
        MusicLayer.Heartbeat => 1.0,
        MusicLayer.Hearth => 2.5,
        _ => 2.0,
    };

    public static double ReleaseSeconds(MusicLayer layer) => layer switch
    {
        MusicLayer.Drone => 6.0,
        MusicLayer.Harp => 3.5,
        MusicLayer.Whistle => 3.5,
        MusicLayer.Pulse => 4.0,
        MusicLayer.DrumLight => 4.5,
        MusicLayer.DrumHeavy => 5.0,
        MusicLayer.Shimmer => 3.5,
        MusicLayer.Choir => 4.0,
        MusicLayer.Heartbeat => 2.5,
        MusicLayer.Hearth => 3.5,
        _ => 4.0,
    };

    /// <summary>When the player dies every layer falls over this many seconds.</summary>
    public const double DeathFadeSeconds = 3.0;

    /// <summary>Seconds to ramp the whole mix up from silence when the music first starts.</summary>
    public const double StartFadeSeconds = 4.0;

    /// <summary>Seconds to cross from one context's music to another's.</summary>
    public const double ContextCrossfadeSeconds = 5.0;

    /// <summary>Seconds for the dialogue duck and the pause filter to take hold, and to let go.</summary>
    public const double DuckInSeconds = 0.4;

    public const double DuckOutSeconds = 1.2;

    /// <summary>The level of the music while a conversation is open.</summary>
    public const double DialogueLevel = 0.4;

    /// <summary>How much of each layer is left while a menu is open: highs go first, which is what a low-pass would do.</summary>
    public static double PauseKeep(MusicLayer layer) => layer switch
    {
        MusicLayer.Drone => 0.7,
        MusicLayer.Pulse => 0.6,
        MusicLayer.Heartbeat => 0.5,
        MusicLayer.Hearth => 0.4,
        MusicLayer.Choir => 0.5,
        MusicLayer.DrumLight or MusicLayer.DrumHeavy => 0.3,
        MusicLayer.Harp or MusicLayer.Whistle => 0.25,
        MusicLayer.Shimmer => 0.15,
        _ => 0.5,
    };

    /// <summary>The whole mix is scaled by this while paused.</summary>
    public const double PauseLevel = 0.6;

    // ---- combat detection ------------------------------------------------------------------

    /// <summary>Combat starts the moment an enemy is near or an encounter begins, and only ends after this long with neither, so counts flickering do not flicker the music.</summary>
    public const double CombatHoldSeconds = 5.0;

    /// <summary>Intensity must rise past <see cref="HeavyOn"/> before the heavy drum comes in, and fall below <see cref="HeavyOff"/> before it goes.</summary>
    public const double HeavyOn = 0.55, HeavyOff = 0.35;

    /// <summary>Health below this brings the heartbeat in; it is full at <see cref="HeartbeatFull"/>.</summary>
    public const double HeartbeatStart = 0.4, HeartbeatFull = 0.1;

    /// <summary>How near the Wulver's hill the player must be to hear his tune, in blocks.</summary>
    public const double WulverHearRange = 30;

    /// <summary>How far an enemy can be and still count as near, in blocks.</summary>
    public const double EnemyRange = 24;

    public const int MaxEnemiesCounted = 6;

    // ---- how loud each layer is wanted (before the master headroom), see MusicMixer.Targets ---------

    /// <summary>The longest a frame may count for when smoothing, so a hitch cannot make the music jump.</summary>
    public const double MaxStepSeconds = 0.1;

    public const double DroneUnderground = 0.6, DroneSurface = 0.4, DroneBoss = 0.7;

    public const double HarpCalm = 0.5, HarpSparse = 0.4, HarpGentle = 0.45, HarpInCombat = 0.12, HarpNearHearth = 0.3;

    public const double WhistleSurface = 0.55, WhistleWulver = 0.75, WhistlePlain = 0.25, WhistleMossy = 0.3, WhistleNearHearth = 0.3;

    public const double PulsePlain = 0.12, PulseMossy = 0.15, PulseAshen = 0.22;

    /// <summary>The drum and pulse levels in combat are a base plus a share of the intensity (0 to 1).</summary>
    public const double PulseBase = 0.35, PulseSpan = 0.35, DrumLightBase = 0.45, DrumLightSpan = 0.45, DrumHeavyBase = 0.5, DrumHeavySpan = 0.4;

    public const double BossDrum = 0.9, BossPulse = 0.7, BossChoir = 0.85;

    public const double ShimmerPassive = 0.35, ShimmerPortal = 0.22, ShimmerTension = 0.18, ShimmerTensionIntensity = 0.75;

    public const double HeartbeatMax = 0.85, HearthLevel = 0.8;

    // ---- intensity: how hard the fight is, 0 to 1 -----------------------------------------------

    public const double IntensityBase = 0.15, IntensityEnemies = 0.4, IntensityStyle = 0.4, IntensityEncounter = 0.1;
}
