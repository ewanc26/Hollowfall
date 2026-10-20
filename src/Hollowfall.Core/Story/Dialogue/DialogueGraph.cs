namespace Hollowfall.Core.Story.Dialogue;

/// <summary>The gifts a conversation can hand over. The host applies them (heal, cleanse, perk); the runner only reports them.</summary>
public enum GiftKind
{
    /// <summary>A full heal, hard damage washed away, and an Open guard perk.</summary>
    Plain,

    /// <summary>The same for someone badly hurt, but gentler and longer-lasting.</summary>
    Hurt,
}

/// <summary>Something that changes when a node is entered or a choice is taken.</summary>
public abstract record DialogueEffect
{
    public static DialogueEffect Set(string flag) => new SetFlag(flag);

    public static DialogueEffect Clear(string flag) => new ClearFlag(flag);

    public static DialogueEffect Give(GiftKind kind) => new GiveGift(kind);

    public static DialogueEffect CountVisit() => new CountVisitEffect();

    public static DialogueEffect End() => new EndEffect();
}

public sealed record SetFlag(string Id) : DialogueEffect;

public sealed record ClearFlag(string Id) : DialogueEffect;

public sealed record GiveGift(GiftKind Kind) : DialogueEffect;

public sealed record CountVisitEffect : DialogueEffect;

public sealed record EndEffect : DialogueEffect;

/// <summary>
/// A paragraph the speaker says. It is said when <see cref="When"/> holds (or always); if it has <see cref="Alternatives"/> the seed
/// picks one of the text and the alternatives, the same one every time for the same seed and node.
/// </summary>
public sealed record DialogueLine(string Text, DialogueCondition? When = null, IReadOnlyList<string>? Alternatives = null);

/// <summary>What the player can say. Taken, it applies <see cref="Effects"/> and goes to <see cref="Next"/>, or ends the talk when that is null.</summary>
public sealed record DialogueChoice(string Text, string? Next, DialogueCondition? When = null, IReadOnlyList<DialogueEffect>? Effects = null);

/// <summary>
/// One beat of a conversation. <see cref="Guard"/> keeps a node from being entered unless it holds (the runner goes to
/// <see cref="GuardFallback"/> instead), so a path can be made to depend on what the player has done or is.
/// </summary>
public sealed record DialogueNode(
    string Id,
    IReadOnlyList<DialogueLine> Lines,
    IReadOnlyList<DialogueChoice> Choices,
    IReadOnlyList<DialogueEffect>? OnEnter = null,
    DialogueCondition? Guard = null,
    string? GuardFallback = null);

/// <summary>Where a conversation begins: the first entry whose condition holds wins.</summary>
public sealed record DialogueEntry(DialogueCondition When, string NodeId);

/// <summary>A set of nodes and the ordered entry points into them.</summary>
public sealed class DialogueGraph
{
    private readonly Dictionary<string, DialogueNode> _byId;

    public IReadOnlyList<DialogueNode> Nodes { get; }
    public IReadOnlyList<DialogueEntry> Entries { get; }

    public DialogueGraph(IReadOnlyList<DialogueNode> nodes, IReadOnlyList<DialogueEntry> entries)
    {
        Nodes = nodes;
        Entries = entries;
        _byId = [];
        foreach (var n in nodes) _byId.TryAdd(n.Id, n);
    }

    public DialogueNode? Find(string? id) => id != null && _byId.TryGetValue(id, out var n) ? n : null;

    /// <summary>The node a conversation starts at for this context.</summary>
    public string? EntryFor(DialogueContext context)
    {
        foreach (var e in Entries)
            if (e.When.Evaluate(context)) return e.NodeId;
        return null;
    }
}
