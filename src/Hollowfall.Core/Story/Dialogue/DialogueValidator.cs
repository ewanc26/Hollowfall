namespace Hollowfall.Core.Story.Dialogue;

/// <summary>Checks a <see cref="DialogueGraph"/> for mistakes an author can make: dangling ids, nodes nobody can reach, dead ends, empty or huge text.</summary>
public static class DialogueValidator
{
    public const int MaxLineLength = 460;
    public const int MaxChoiceLength = 60;
    public const int MaxChoices = 5;

    /// <summary>Accented letters his Gaelic and Scots use, on top of plain ASCII.</summary>
    public const string ApprovedAccents = "òàèìùóáéíúÒÀ";

    public static bool ApprovedChar(char c) => c is >= ' ' and <= '~' or '\n' || ApprovedAccents.Contains(c);

    /// <summary>A node whose only way on is to end the talk: a place to say goodbye.</summary>
    public static bool IsTerminal(DialogueNode node) => node.Choices is [{ Next: null, When: null }];

    /// <summary>Whether the player can always get out from this node: an unconditional choice that ends the talk, or goes straight to a goodbye.</summary>
    public static bool HasLeave(DialogueGraph graph, DialogueNode node) =>
        node.Choices.Any(c => c.When == null && (c.Next == null || graph.Find(c.Next) is { } n && IsTerminal(n)));

    public static IReadOnlyList<string> Validate(DialogueGraph graph)
    {
        var problems = new List<string>();

        var ids = new HashSet<string>();
        foreach (var n in graph.Nodes)
        {
            if (string.IsNullOrWhiteSpace(n.Id)) problems.Add("a node has an empty id");
            else if (!ids.Add(n.Id)) problems.Add($"duplicate node id {n.Id}");
        }

        foreach (var e in graph.Entries)
            if (graph.Find(e.NodeId) == null) problems.Add($"entry points at missing node {e.NodeId}");
        if (graph.Entries.Count == 0) problems.Add("no entries");

        foreach (var n in graph.Nodes)
        {
            if (n.Guard != null && graph.Find(n.GuardFallback) == null) problems.Add($"{n.Id}: a guarded node needs a real fallback");
            if (n.Lines.Count == 0) problems.Add($"{n.Id}: no lines");
            foreach (var line in n.Lines)
            {
                foreach (var text in new[] { line.Text }.Concat(line.Alternatives ?? []))
                {
                    if (string.IsNullOrWhiteSpace(text)) problems.Add($"{n.Id}: an empty line");
                    else if (text.Length > MaxLineLength) problems.Add($"{n.Id}: a line of {text.Length} characters is too long");
                    else if (text.Any(c => !ApprovedChar(c))) problems.Add($"{n.Id}: a line has an unapproved character");
                }
            }
            if (n.Choices.Count is < 1 or > MaxChoices) problems.Add($"{n.Id}: {n.Choices.Count} choices");
            if (!HasLeave(graph, n)) problems.Add($"{n.Id}: no way to leave");
            foreach (var c in n.Choices)
            {
                if (string.IsNullOrWhiteSpace(c.Text) || c.Text.Length > MaxChoiceLength) problems.Add($"{n.Id}: a choice has bad text");
                else if (c.Text.Any(ch => !ApprovedChar(ch))) problems.Add($"{n.Id}: a choice has an unapproved character");
                if (c.Next != null && graph.Find(c.Next) == null) problems.Add($"{n.Id}: choice goes to missing node {c.Next}");
            }
        }

        // Reachable from some entry (ignoring conditions, following choices and guard fallbacks).
        var reach = new HashSet<string>();
        var queue = new Queue<string>(graph.Entries.Select(e => e.NodeId));
        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            if (!reach.Add(id) || graph.Find(id) is not { } node) continue;
            foreach (var c in node.Choices) if (c.Next != null) queue.Enqueue(c.Next);
            if (node.GuardFallback != null) queue.Enqueue(node.GuardFallback);
        }
        foreach (var n in graph.Nodes)
            if (!reach.Contains(n.Id)) problems.Add($"{n.Id}: unreachable");

        // Every node can reach an end.
        var canEnd = new HashSet<string>(graph.Nodes.Where(n => n.Choices.Any(c => c.Next == null)).Select(n => n.Id));
        for (bool changed = true; changed;)
        {
            changed = false;
            foreach (var n in graph.Nodes)
                if (!canEnd.Contains(n.Id) && n.Choices.Any(c => c.Next != null && canEnd.Contains(c.Next)))
                {
                    canEnd.Add(n.Id);
                    changed = true;
                }
        }
        foreach (var n in graph.Nodes)
            if (!canEnd.Contains(n.Id)) problems.Add($"{n.Id}: cannot reach an end");

        return problems;
    }
}
