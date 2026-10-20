using Hollowfall.Core.Dungeon;

namespace Hollowfall.Core.Story;

/// <summary>
/// The Wulver's own voice. In Shetland folklore he is a gentle wolf-headed man who fishes from a rock and leaves his catch on the
/// windowsills of the poor; ours stands on a hill with a branch of yew. His name and his words live here and nowhere else: no
/// other character, enemy, warden or place is called by his name, and his manner (gentle, dry, unhurried, a little Scots) is his alone.
/// The first meeting is an introduction; later visits walk through the rest in an order chosen by the seed, with no repeat until
/// every line has been heard.
/// </summary>
public static class WulverVoice
{
    /// <summary>What the panel calls him.</summary>
    public const string Name = "Eòghann";

    public const string Introduction =
        "Ah, you found the hill. Eòghann, they call me, though the old stories say wulver and I have given up arguing. " +
        "Stand easy a moment. The yew gives, and so do I, and neither of us makes much of it.";

    private static readonly string[] Others =
    [
        "Fishing is mostly standing still and being agreeable to the weather. You look like someone who has done neither lately.",
        "This yew was here before the hill had a name. It is unimpressed by swords, and by most people.",
        "Och, it is a long way down, is it not? Mind the stairs on the way back. They are patient, and you are not.",
        "I leave a fish where one is needed and do not wait to be thanked. Fish are poor company, but very honest.",
        "Rain by supper, I would say. Down in the dark you will not need my opinion on weather, but you have it.",
        "Dinna fret about the red berries. They are only bright because nobody told them otherwise. Leave them be and they will leave you.",
        "Dark things down there, aye. But the dark is only a hill you have not climbed yet, with the lamp out.",
        "Slàinte. I never learned a proper blessing, so I mostly say the weather and hope it counts.",
    ];

    /// <summary>Every line he can say, the introduction first.</summary>
    public static IReadOnlyList<string> AllLines { get; } = [Introduction, .. Others];

    /// <summary>The lines after the introduction, in the order this seed hears them.</summary>
    public static IReadOnlyList<string> OrderFor(long seed)
    {
        var order = new List<string>(Others);
        new SeededRandom(seed, 0, 0, 990).Shuffle(order);
        return order;
    }

    /// <summary>The line for a visit: 0 is the introduction; later visits go through <see cref="OrderFor"/> and begin again only once all are used.</summary>
    public static string LineFor(long seed, int visit)
    {
        if (visit <= 0) return Introduction;
        var order = OrderFor(seed);
        return order[(visit - 1) % order.Count];
    }
}
