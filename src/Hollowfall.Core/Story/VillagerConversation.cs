using Hollowfall.Core.Combat;
using Hollowfall.Core.Relations;
using Hollowfall.Core.Story.Dialogue;

namespace Hollowfall.Core.Story;

/// <summary>
/// Talking to the villagers of this world. Any villager standing beside you will talk, and they share one memory per seed: what
/// you have asked about and how often you have spoken to the glen. The conversation is the tree in <see cref="VillagerDialogue"/>,
/// run by a <see cref="DialogueRunner"/>, and the Villagers' standing decides how they receive you. Pure logic with no engine types.
/// </summary>
public sealed class VillagerConversation
{
    /// <summary>How far, in blocks along the ground, you may stand and still speak to one.</summary>
    public const double Reach = 2.5;

    public const double ReachHeight = 2.5;

    /// <summary>The most flags the glen remembers.</summary>
    public const int MaxFlags = 16;

    private HashSet<string> _flags;
    private readonly long _seed;
    private readonly string _seedText;

    public int Visits { get; private set; }

    /// <summary>The conversation in progress, or null.</summary>
    public DialogueRunner? Conversation { get; private set; }

    public IReadOnlyCollection<string> Flags => _flags;

    public VillagerConversation(long seed, string seedText, IEnumerable<string>? flags = null, int visits = 0)
    {
        _seed = seed;
        _seedText = seedText;
        _flags = [.. (flags ?? []).Where(WulverEncounter.ValidFlag).Distinct().Take(MaxFlags)];
        Visits = Math.Clamp(visits, 0, 10_000);
    }

    /// <summary>Whether a living villager on the surface is near enough to talk to.</summary>
    public static bool InReach(IEnumerable<Enemy> enemies, Vec3 feet)
    {
        foreach (var e in enemies)
        {
            if (e.Kind != EnemyKind.Villager || e.Dead || e.Floor != CombatSim.SurfaceFloor) continue;
            double dx = e.Feet.X - feet.X, dz = e.Feet.Z - feet.Z;
            if (Math.Abs(e.Feet.Y - feet.Y) <= ReachHeight && dx * dx + dz * dz <= Reach * Reach) return true;
        }
        return false;
    }

    /// <summary>Starts a conversation with the villagers at the given standing.</summary>
    public DialogueRunner Begin(ReputationTier tier)
    {
        var context = new DialogueContext
        {
            Seed = _seed,
            SeedText = _seedText,
            VisitCount = Visits,
            VillagerTier = tier,
            Flags = new HashSet<string>(_flags),
        };
        var runner = new DialogueRunner(VillagerDialogue.Graph, context);
        Conversation = runner;
        Sync(runner);
        return runner;
    }

    /// <summary>Takes a choice in the conversation and remembers what changed. False for a bad index.</summary>
    public bool Choose(int index)
    {
        if (Conversation is not { Finished: false } runner || !runner.Advance(index)) return false;
        Sync(runner);
        return true;
    }

    /// <summary>Ends the conversation by any route; what was chosen so far is kept.</summary>
    public void End()
    {
        Conversation?.Abandon();
        Conversation = null;
    }

    private void Sync(DialogueRunner runner)
    {
        _flags = [.. runner.Context.Flags.Where(WulverEncounter.ValidFlag).Take(MaxFlags)];
        Visits = runner.Context.VisitCount;
        if (runner.Finished) Conversation = null;
    }
}
