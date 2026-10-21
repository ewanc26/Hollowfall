using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Progression;

/// <summary>The quests you can take. Appended to, never reordered.</summary>
public enum QuestId
{
    /// <summary>A washerwoman's errand: clear the Husks that roam the lower halls.</summary>
    ClearTheHusks,

    /// <summary>The Folk's thanks for clearing the Spitters that spoil their hearths.</summary>
    ClearTheSpitters,
}

/// <summary>What a quest asks for, and what it pays.</summary>
public sealed record QuestDef(QuestId Id, string Name, EnemyKind Target, int Count, int Experience, ItemKind? Item, int ItemCount);

/// <summary>
/// Quests are pure data plus a log that counts progress from defeated enemies. A quest is finished once, when its count is reached,
/// and then pays its reward. Nothing here is saved or shown yet; the log is the part the game will hold.
/// </summary>
public sealed class QuestLog
{
    public static readonly IReadOnlyDictionary<QuestId, QuestDef> Catalogue = new Dictionary<QuestId, QuestDef>
    {
        [QuestId.ClearTheHusks] = new(QuestId.ClearTheHusks, "Clear the Husks", EnemyKind.Husk, 5, 40, ItemKind.BrownieCream, 1),
        [QuestId.ClearTheSpitters] = new(QuestId.ClearTheSpitters, "Clear the Spitters", EnemyKind.Spitter, 3, 60, ItemKind.ThornCharm, 1),
    };

    private readonly Dictionary<QuestId, int> _progress = [];
    private readonly HashSet<QuestId> _done = [];

    /// <summary>Quests taken and not yet finished.</summary>
    public IEnumerable<QuestId> Active => _progress.Keys.Where(q => !_done.Contains(q));

    /// <summary>How far along a quest is, out of its count.</summary>
    public int Progress(QuestId quest) => _progress.GetValueOrDefault(quest);

    public bool IsDone(QuestId quest) => _done.Contains(quest);

    /// <summary>Takes a quest. Returns false if it is already taken or already finished.</summary>
    public bool Take(QuestId quest)
    {
        if (_done.Contains(quest) || _progress.ContainsKey(quest)) return false;
        _progress[quest] = 0;
        return true;
    }

    /// <summary>
    /// Counts a defeated enemy against every quest taken. Returns the quests this kill finished, each with its reward, and the bag
    /// and experience are the caller's to grant.
    /// </summary>
    public IReadOnlyList<QuestDef> Record(EnemyKilled killed)
    {
        var finished = new List<QuestDef>();
        foreach (var quest in Active.ToList())
        {
            var def = Catalogue[quest];
            if (def.Target != killed.Kind) continue;
            _progress[quest] = Math.Min(def.Count, _progress[quest] + 1);
            if (_progress[quest] < def.Count) continue;
            _done.Add(quest);
            finished.Add(def);
        }
        return finished;
    }
}
