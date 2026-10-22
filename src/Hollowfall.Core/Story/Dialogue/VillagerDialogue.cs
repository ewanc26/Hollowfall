using Hollowfall.Core.Relations;
using static Hollowfall.Core.Story.Dialogue.DialogueCondition;

namespace Hollowfall.Core.Story.Dialogue;

/// <summary>
/// What the crofters of the glen say to you, in one file. They keep to their doors, fear what climbs up from the Hollow and
/// remember whether you have been decent to them. How they receive you depends on the Villagers' standing (a tier from the
/// relationship system): the hostile keep their distance, the trusted ask you in. Their speech is plain English, as the
/// story text is; nothing here is Gàidhlig until a speaker has reviewed it.
/// </summary>
public static class VillagerDialogue
{
    /// <summary>What the panel calls them.</summary>
    public const string Name = "Crofter";

    public const string AskedHollow = "askedHollow";
    public const string AskedVillage = "askedVillage";
    public const string AskedWulver = "askedWulver";

    public static DialogueGraph Graph { get; } = Build();

    private static DialogueLine L(string text, DialogueCondition? when = null) => new(text, when);

    private static DialogueChoice C(string text, string? next, DialogueCondition? when = null, params DialogueEffect[] effects) => new(text, next, when, effects.Length > 0 ? effects : null);

    private static DialogueNode N(string id, DialogueLine[] lines, DialogueChoice[] choices, DialogueEffect[]? enter = null) => new(id, lines, choices, enter);

    private static DialogueGraph Build()
    {
        DialogueEffect visit = DialogueEffect.CountVisit();
        DialogueChoice leave = C("Say goodbye.", "bye");

        var nodes = new List<DialogueNode>
        {
            N("first",
                [
                    L("(She looks up from the doorstep and wipes her hands on her apron.)"),
                    L("Ye'll be the one that came up out of the Hollow, then. Well. Ye're standing on the road, so ye'll do for now."),
                ],
                [
                    C("Ask about the Hollow.", "hollow", null, DialogueEffect.Set(AskedHollow)),
                    C("Ask about the village.", "village", null, DialogueEffect.Set(AskedVillage)),
                    leave,
                ],
                [visit]),
            N("again",
                [
                    L("Back again. The kettle's on, if ye're stopping."),
                    L("Ye asked about the Hollow last time. It has not gone anywhere.", Flag(AskedHollow)),
                ],
                [
                    C("Ask about the Hollow.", "hollow", null, DialogueEffect.Set(AskedHollow)),
                    C("Ask about the village.", "village", null, DialogueEffect.Set(AskedVillage)),
                    leave,
                ],
                [visit]),
            N("trusted",
                [
                    L("(She sets down her bucket and comes to the gate.)"),
                    L("Ye've been good to this glen, friend. Come in out of the wet."),
                ],
                [
                    C("Ask about the Hollow.", "hollow", null, DialogueEffect.Set(AskedHollow)),
                    C("Ask about the wulver.", "wulver", null, DialogueEffect.Set(AskedWulver)),
                    leave,
                ],
                [visit]),
            N("hostile",
                [
                    L("(She does not come down off the step.)"),
                    L("Keep your distance. The last one that came up this road came up out of the hole."),
                ],
                [C("Go", null)],
                [visit]),
            N("hollow",
                [
                    L("Nobody comes back from the Hollow the same, and nobody here goes down to find out."),
                    L("The bell in the chapel rings when something climbs out after dark. Hear it, and ye bar the door and say nothing."),
                ],
                [
                    C("What climbs out?", "climbs"),
                    leave,
                ]),
            N("climbs",
                [
                    L("Husks, mostly. Things that were folk once, or the shape of them. The wulver's hill keeps the worst of them off the road, so they say."),
                ],
                [
                    C("Ask about the wulver.", "wulver", null, DialogueEffect.Set(AskedWulver)),
                    leave,
                ]),
            N("village",
                [
                    L("The well is older than the chapel, and the yew on the hill is older than the well."),
                    L("Every house has its door to the road, and every door has someone at it. That is how we have kept the glen."),
                ],
                [
                    C("Ask about the wulver.", "wulver", null, DialogueEffect.Set(AskedWulver)),
                    C("Ask about the Hollow.", "hollow", null, DialogueEffect.Set(AskedHollow)),
                    leave,
                ]),
            N("wulver",
                [
                    L("The wulver? He fishes off his rock and leaves fish on our sills in the hard winters. Nobody has ever seen him take a penny for it."),
                    L("Some say he holds the hill against the Hollow. Some say he only likes the fish."),
                ],
                [
                    C("Ask about the Hollow.", "hollow", null, DialogueEffect.Set(AskedHollow)),
                    leave,
                ]),
            N("bye", [L("Mind yourself down there.")], [C("Go", null)]),
        };

        var entries = new List<DialogueEntry>
        {
            new(Not(VillagerTierAtLeast(ReputationTier.Wary)), "hostile"),
            new(VillagerTierAtLeast(ReputationTier.Trusted), "trusted"),
            new(VisitsBelow(1), "first"),
            new(Always, "again"),
        };
        return new DialogueGraph(nodes, entries);
    }
}
