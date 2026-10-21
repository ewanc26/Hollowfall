using Hollowfall.Core.Combat;
using Hollowfall.Core.Story.Dialogue;

namespace Hollowfall.Core.Story;

/// <summary>
/// The cat, for one run of one seed: when he wakes and notices you, how near you must stand to talk, and the conversation, which is the tree in
/// <see cref="CatDialogue"/> run by a <see cref="DialogueRunner"/>. He wakes and sits up when you come within <see cref="NoticeRange"/>. His
/// gift, a little warmth by the fire (hard damage washed away and a small heal, nothing more), is given once per run, by the node where you
/// sit beside him. What he remembers (<see cref="Flags"/>, <see cref="Visits"/>) is saved with the story progress. He has no hit box: he is
/// not an enemy, and nothing in the combat simulation knows he exists. Pure logic with no engine types.
/// </summary>
public sealed class CatEncounter
{
    /// <summary>How far, in blocks along the ground, you may stand and still speak to him.</summary>
    public const double Reach = 2.5;

    public const double ReachHeight = 2.5;

    /// <summary>How near you come before he wakes, sits up and looks at you.</summary>
    public const double NoticeRange = 8.0;

    /// <summary>How far you must go before he settles to sleep again.</summary>
    public const double LeaveRange = 11.0;

    /// <summary>The most flags he can remember.</summary>
    public const int MaxFlags = 16;

    /// <summary>How much health the warmth restores. A little, by design.</summary>
    public const double WarmthHeal = 20;

    private HashSet<string> _flags;
    private bool _sighted;

    public CatSpot Spot { get; }
    public int Visits { get; private set; }
    public bool GiftTaken { get; private set; }

    /// <summary>The conversation in progress, or null.</summary>
    public DialogueRunner? Conversation { get; private set; }

    public IReadOnlyCollection<string> Flags => _flags;

    private readonly long _seed;
    private readonly string _seedText;

    public CatEncounter(long seed, string seedText, CatSpot spot, IEnumerable<string>? flags = null, int visits = 0)
    {
        _seed = seed;
        _seedText = seedText;
        Spot = spot;
        _flags = [.. (flags ?? []).Where(WulverEncounter.ValidFlag).Take(MaxFlags)];
        Visits = Math.Clamp(visits, 0, 10_000);
    }

    private double Distance(double x, double z)
    {
        double dx = x - Spot.X, dz = z - Spot.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    public bool IsNear(double x, double y, double z) => Math.Abs(y - Spot.Y) <= ReachHeight && Distance(x, z) <= Reach;

    /// <summary>Whether he is awake: you are within <see cref="NoticeRange"/> on his floor.</summary>
    public bool Notices(double x, double y, double z) => Math.Abs(y - Spot.Y) <= 4 && Distance(x, z) <= NoticeRange;

    /// <summary>
    /// Call each step with where the player stands. Returns the caption once, the first time he comes into view, on a seed whose cat has
    /// never been spoken to; after that, nothing.
    /// </summary>
    public string? Sighting(double x, double y, double z)
    {
        if (_sighted || Visits > 0 || !Notices(x, y, z)) return null;
        _sighted = true;
        return CatDialogue.FirstSight;
    }

    /// <summary>Starts a conversation, applying anything its first node gives.</summary>
    public DialogueRunner Begin(PlayerVitals vitals)
    {
        var context = new DialogueContext
        {
            Seed = _seed,
            SeedText = _seedText,
            VisitCount = Visits,
            HealthFraction = Math.Clamp(vitals.Health / PlayerVitals.MaxHealth, 0, 1),
            GiftGiven = GiftTaken,
            Flags = new HashSet<string>(_flags),
        };
        var runner = new DialogueRunner(CatDialogue.Graph, context);
        Conversation = runner;
        Sync(runner, vitals);
        return runner;
    }

    /// <summary>Takes a choice in the conversation, hands over any warmth it gives and remembers what changed. False for a bad index.</summary>
    public bool Choose(int index, PlayerVitals vitals)
    {
        if (Conversation is not { Finished: false } runner || !runner.Advance(index)) return false;
        Sync(runner, vitals);
        return true;
    }

    /// <summary>Ends the conversation (the player closed the panel or walked off). What was chosen so far is kept.</summary>
    public void End()
    {
        Conversation?.Abandon();
        Conversation = null;
    }

    private void Sync(DialogueRunner runner, PlayerVitals vitals)
    {
        foreach (var gift in runner.TakeGifts())
        {
            if (gift != GiftKind.Warmth) continue;
            GiftTaken = true;
            if (vitals.Dead) continue;
            vitals.Cleanse();
            vitals.Heal(WarmthHeal);
        }
        _flags = [.. runner.Context.Flags.Where(WulverEncounter.ValidFlag).Take(MaxFlags)];
        Visits = runner.Context.VisitCount;
        if (runner.Finished) Conversation = null;
    }
}
