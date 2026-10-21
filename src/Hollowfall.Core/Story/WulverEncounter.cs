using Hollowfall.Core.Combat;
using Hollowfall.Core.Localization;
using Hollowfall.Core.Story.Dialogue;

namespace Hollowfall.Core.Story;

/// <summary>Where the player stands in the run, as far as the Wulver is concerned. The host fills it in from the game.</summary>
public readonly record struct WulverFacts(int Deaths, int Kills, int DeepestFloor, int FloorCount, int NotesRead, bool HasKey, bool EndingReached);

/// <summary>
/// The Wulver, for one run of one seed: when he notices you, how near you must stand to talk, and the conversation itself, which is the
/// tree in <see cref="WulverDialogue"/> run by a <see cref="DialogueRunner"/>. He notices you at <see cref="NoticeRange"/> (he looks at
/// you, and greets you once); the greeting fires again only after you have gone beyond <see cref="LeaveRange"/> and come back. His gift is
/// handed over once per run, by whichever node gives it. What he remembers (<see cref="Flags"/>, <see cref="Visits"/>) is meant to be
/// saved with the story progress. Pure logic with no engine types.
/// </summary>
public sealed class WulverEncounter
{
    /// <summary>How far, in blocks along the ground, you may stand and still speak to him.</summary>
    public const double Reach = 3.0;

    public const double ReachHeight = 3.0;

    /// <summary>How near you come before he looks at you and greets you.</summary>
    public const double NoticeRange = 7.0;

    /// <summary>How far you must go before the next approach counts as a new visit.</summary>
    public const double LeaveRange = 10.0;

    /// <summary>The most flags he can remember, and the longest one.</summary>
    public const int MaxFlags = 32, MaxFlagLength = 24;

    private readonly long _seed;
    private readonly string _seedText;
    private HashSet<string> _flags;
    private bool _inRange;

    public WulverSpot Spot { get; }

    /// <summary>The interface language (set by the host). In Gàidhlig and Bilingual he greets in Gàidhlig first, with the English beneath.</summary>
    public Language Language { get; set; } = Language.English;
    public int Visits { get; private set; }
    public bool GiftTaken { get; private set; }

    /// <summary>The conversation in progress, or null.</summary>
    public DialogueRunner? Conversation { get; private set; }

    public IReadOnlyCollection<string> Flags => _flags;

    public WulverEncounter(long seed, string seedText, WulverSpot spot, IEnumerable<string>? flags = null, int visits = 0)
    {
        _seed = seed;
        _seedText = seedText;
        Spot = spot;
        _flags = [.. (flags ?? []).Where(ValidFlag).Take(MaxFlags)];
        Visits = Math.Clamp(visits, 0, 10_000);
    }

    /// <summary>A flag is a short id of letters, digits and underscores.</summary>
    public static bool ValidFlag(string? flag) => !string.IsNullOrEmpty(flag) && flag.Length <= MaxFlagLength && flag.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    public bool IsNear(double x, double y, double z)
    {
        if (Math.Abs(y - Spot.Y) > ReachHeight) return false;
        return Distance(x, z) <= Reach;
    }

    private double Distance(double x, double z)
    {
        double dx = x - Spot.X, dz = z - Spot.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Whether he can see you (for turning his head): within <see cref="NoticeRange"/> and not far above or below.</summary>
    public bool Notices(double x, double y, double z) => Math.Abs(y - Spot.Y) <= 12 && Distance(x, z) <= NoticeRange;

    /// <summary>
    /// Call each step with where the player stands. Returns the greeting once as you come within range, and nothing more until you have
    /// left (beyond <see cref="LeaveRange"/>) and come back.
    /// </summary>
    public string? Approach(double x, double y, double z)
    {
        double d = Distance(x, z);
        if (_inRange)
        {
            if (d > LeaveRange || Math.Abs(y - Spot.Y) > 20) _inRange = false;
            return null;
        }
        if (!Notices(x, y, z)) return null;
        _inRange = true;
        string english = Visits == 0 ? WulverDialogue.GreetingFirst : WulverDialogue.GreetingReturn;
        if (!Languages.ShowsGaelic(Language)) return english;
        return (Visits == 0 ? WulverGreetings.Line(WulverGreetings.Hello, WulverGreetings.Welcome) : WulverGreetings.Line(WulverGreetings.Welcome)) + "\n\n" + english;
    }

    /// <summary>Starts a conversation, applying anything its first node gives.</summary>
    public DialogueRunner Begin(WulverFacts facts, PlayerVitals vitals, Perks perks)
    {
        var context = new DialogueContext
        {
            Seed = _seed,
            SeedText = _seedText,
            VisitCount = Visits,
            HealthFraction = Math.Clamp(vitals.Health / vitals.Max, 0, 1),
            Deaths = facts.Deaths,
            Kills = facts.Kills,
            DeepestFloor = facts.DeepestFloor,
            FloorCount = facts.FloorCount,
            NotesRead = facts.NotesRead,
            HasKey = facts.HasKey,
            GiftGiven = GiftTaken,
            EndingReached = facts.EndingReached,
            Language = Language,
            Flags = new HashSet<string>(_flags),
        };
        var runner = new DialogueRunner(WulverDialogue.Graph, context);
        Conversation = runner;
        Sync(runner, vitals, perks);
        return runner;
    }

    /// <summary>Takes a choice in the conversation, hands over any gift it gives and remembers what changed. False for a bad index.</summary>
    public bool Choose(int index, PlayerVitals vitals, Perks perks)
    {
        if (Conversation is not { Finished: false } runner || !runner.Advance(index)) return false;
        Sync(runner, vitals, perks);
        return true;
    }

    /// <summary>Ends the conversation (the player closed the panel or walked off). What was chosen so far is kept.</summary>
    public void End()
    {
        Conversation?.Abandon();
        Conversation = null;
    }

    private void Sync(DialogueRunner runner, PlayerVitals vitals, Perks perks)
    {
        foreach (var gift in runner.TakeGifts())
        {
            GiftTaken = true;
            if (vitals.Dead) continue;
            vitals.Cleanse();
            vitals.Heal(vitals.Max);
            perks.Grant(gift == GiftKind.Hurt ? HurtGift : Gift);
        }
        _flags = [.. runner.Context.Flags.Where(ValidFlag).Take(MaxFlags)];
        Visits = runner.Context.VisitCount;
        if (runner.Finished) Conversation = null;
    }

    /// <summary>The perk of the ordinary gift: a wide parry for a good while.</summary>
    public static readonly Loot Gift = new(LootKind.WideParry, LootRarity.Rare, 0.18, 40);

    /// <summary>The perk for someone badly hurt: a wider parry, for longer.</summary>
    public static readonly Loot HurtGift = new(LootKind.WideParry, LootRarity.Epic, 0.28, 60);
}
