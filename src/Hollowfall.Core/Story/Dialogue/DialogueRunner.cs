using Hollowfall.Core.Terrain;

namespace Hollowfall.Core.Story.Dialogue;

/// <summary>
/// Walks a <see cref="DialogueGraph"/>. It starts at the entry node that fits the context, exposes the lines and the choices that
/// are open, and moves on when a choice is taken. It never throws: a bad choice index is ignored, and a missing node ends the talk.
/// Effects change the <see cref="Context"/> (flags, visit count, gift given) and the host reads it back to save; gifts are queued for
/// the host to hand over.
/// </summary>
public sealed class DialogueRunner
{
    private const int MaxRedirects = 8;

    private readonly DialogueGraph _graph;
    private readonly List<GiftKind> _gifts = [];
    private HashSet<string> _flags;

    public DialogueContext Context { get; private set; }
    public bool Finished { get; private set; }

    /// <summary>The id of the node being shown (empty once finished with nothing to show).</summary>
    public string NodeId { get; private set; } = "";

    /// <summary>What is being said now: the open lines, a paragraph each.</summary>
    public string Text { get; private set; } = "";

    /// <summary>The choices open now, in the order they are shown. Never empty while the talk is on.</summary>
    public IReadOnlyList<DialogueChoice> Choices { get; private set; } = [];

    public DialogueRunner(DialogueGraph graph, DialogueContext context)
    {
        _graph = graph;
        Context = context;
        _flags = [.. context.Flags];
        Enter(graph.EntryFor(context));
    }

    private DialogueRunner(DialogueRunner other)
    {
        _graph = other._graph;
        _gifts = [.. other._gifts];
        _flags = [.. other._flags];
        Context = other.Context;
        Finished = other.Finished;
        NodeId = other.NodeId;
        Text = other.Text;
        Choices = other.Choices;
    }

    /// <summary>An independent copy at the same point, to try a choice without losing the place (for tools and tests).</summary>
    public DialogueRunner Fork() => new(this);

    /// <summary>The gifts given since the last call.</summary>
    public IReadOnlyList<GiftKind> TakeGifts()
    {
        var taken = _gifts.ToArray();
        _gifts.Clear();
        return taken;
    }

    /// <summary>Takes the choice at this index (in <see cref="Choices"/>). Returns false, and does nothing, if there is no such choice.</summary>
    public bool Advance(int index)
    {
        if (Finished || index < 0 || index >= Choices.Count) return false;
        var choice = Choices[index];
        Apply(choice.Effects);
        if (Finished) return true;
        if (choice.Next == null)
        {
            Finish();
            return true;
        }
        Enter(choice.Next);
        return true;
    }

    /// <summary>Ends the talk now (the player walked away or closed the panel).</summary>
    public void Abandon() => Finish();

    private void Finish()
    {
        Finished = true;
        Choices = [];
    }

    private void Enter(string? id)
    {
        var node = _graph.Find(id);
        for (int hops = 0; node?.Guard != null && !node.Guard.Evaluate(Context) && hops < MaxRedirects; hops++)
            node = _graph.Find(node.GuardFallback);
        if (node == null || node.Guard != null && !node.Guard.Evaluate(Context))
        {
            Finish();
            return;
        }

        NodeId = node.Id;
        Apply(node.OnEnter);
        if (Finished) return;

        var lines = new List<string>();
        for (int i = 0; i < node.Lines.Count; i++)
        {
            var line = node.Lines[i];
            if (line.When != null && !line.When.Evaluate(Context)) continue;
            lines.Add(Pick(line, node.Id, i));
        }
        Text = string.Join("\n\n", lines);
        Choices = [.. node.Choices.Where(c => c.When == null || c.When.Evaluate(Context))];
        if (Choices.Count == 0) Choices = [new DialogueChoice("Leave", null)];
    }

    private string Pick(DialogueLine line, string nodeId, int index)
    {
        if (line.Alternatives is not { Count: > 0 } alts) return line.Text;
        ulong h = SeedHash.Hash(Context.Seed, StableHash(nodeId), index, 960);
        int pick = (int)(h % (ulong)(alts.Count + 1));
        return pick == 0 ? line.Text : alts[pick - 1];
    }

    private static int StableHash(string s)
    {
        uint h = 2166136261;
        foreach (char c in s) h = (h ^ c) * 16777619;
        return (int)h;
    }

    private void Apply(IReadOnlyList<DialogueEffect>? effects)
    {
        if (effects == null) return;
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case SetFlag f: _flags.Add(f.Id); break;
                case ClearFlag f: _flags.Remove(f.Id); break;
                case CountVisitEffect: Context = Context with { VisitCount = Context.VisitCount + 1 }; break;
                case GiveGift g when !Context.GiftGiven:
                    _gifts.Add(g.Kind);
                    Context = Context with { GiftGiven = true };
                    break;
                case EndEffect: Finish(); break;
            }
            Context = Context with { Flags = new HashSet<string>(_flags) };
        }
    }
}
