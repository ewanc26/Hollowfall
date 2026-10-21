using Hollowfall.Core.Localization;
using static Hollowfall.Core.Localization.WulverGreetings;
using static Hollowfall.Core.Story.Dialogue.DialogueCondition;

namespace Hollowfall.Core.Story.Dialogue;

/// <summary>
/// Everything the Wulver says, and the tree it hangs on, in one file. He is a hidden figure and a nod to the game's solo developer:
/// his name is the Gaelic form of theirs, and this file is the ONLY place in the game where either name is written. The developer's
/// own name is used on a single guarded path: the player must pick the choice that claims they made this place, and at least one
/// circumstance must hold (see <see cref="MakerCircumstance"/>). Without both he answers kindly and does not use it. A test walks every path
/// of the tree under every combination to prove it.
/// <para>
/// His voice: gentle, dry, unhurried. Fishing, the yew, weather, the long way down, the odd Gaelic or Scots word. Nobody else talks like this.
/// </para>
/// </summary>
public static class WulverDialogue
{
    /// <summary>What the panel calls him.</summary>
    public const string Name = "Eòghann";

    /// <summary>What he says as you come up the hill, once per approach, before you have spoken. Generic on purpose.</summary>
    public const string GreetingFirst = "Ah. You. Come up, then, and mind the wolf: he is mostly harmless.";

    public const string GreetingReturn = "Ah. You again. The hill must have missed you, or I am flattering it.";

    // ---- flags he remembers ----------------------------------------------------------------------
    public const string AskedWhoIAm = "askedWhoIAm";
    public const string HeardYew = "heardYew";
    public const string TookGift = "tookGift";
    public const string GaveFishBack = "gaveFishBack";
    public const string KeptFish = "keptFish";
    public const string ClaimedMaker = "claimedMaker";
    public const string MakerKey = "makerKey";
    public const string MakerKnown = "makerKnown";

    /// <summary>The seed texts that count as the maker's own (compared ignoring case, accents, spaces and hyphens).</summary>
    public static readonly string[] MakerSeeds = ["ewan", "eoghann", "ewan croft", "ewanc26"];

    /// <summary>
    /// Any one of: the seed is the maker's own; the ending has been reached on this seed and he has been visited at least three times;
    /// or the flag earned by carrying a key from the last floor and showing it to him.
    /// </summary>
    public static readonly DialogueCondition MakerCircumstance = Any(SeedIsOneOf(MakerSeeds), All(EndingReached, VisitsAtLeast(3)), Flag(MakerKey));

    /// <summary>The whole maker path needs the claim AND a circumstance.</summary>
    public static readonly DialogueCondition MakerPath = All(Flag(ClaimedMaker), MakerCircumstance);

    private static readonly DialogueCondition Hurt = HealthBelow(0.4);
    private static readonly DialogueCondition Veteran = Any(KillsAtLeast(25), FloorAtLeast(2));

    // ---- the tree --------------------------------------------------------------------------------

    public static DialogueGraph Graph { get; } = Build();

    private static DialogueLine L(string text, DialogueCondition? when = null, params string[] alternatives) => new(text, when, alternatives.Length > 0 ? alternatives : null);

    /// <summary>A short Gàidhlig line (with the English beneath), said only when the interface is in Gàidhlig or Bilingual.</summary>
    private static DialogueLine G(params LocEntry[] entries) => new(Line(entries), GaelicShown);

    private static DialogueChoice C(string text, string? next, DialogueCondition? when = null, params DialogueEffect[] effects) => new(text, next, when, effects.Length > 0 ? effects : null);

    private static DialogueNode N(string id, DialogueLine[] lines, DialogueChoice[] choices, DialogueEffect[]? enter = null, DialogueCondition? guard = null, string? fallback = null) =>
        new(id, lines, choices, enter, guard, fallback);

    private static DialogueNode Farewell(string id, params DialogueLine[] lines) => N(id, lines, [C("Go", null)]);

    private static DialogueNode Maker(string id, string line, params DialogueChoice[] choices) =>
        N(id, [L(line)], choices, guard: MakerPath, fallback: "claim_plain");

    private static DialogueGraph Build()
    {
        DialogueEffect visit = DialogueEffect.CountVisit();
        var nodes = new List<DialogueNode>
        {
            // ---- the first meeting ----
            N("first_intro",
                [
                    G(Hello, Welcome),
                    L("Ah. You found the hill. Stand easy; I do not bite, whatever the ears suggest.", null, "Ah. There you are. Stand easy; I do not bite, whatever the ears suggest."),
                    L("I am Eòghann. The old stories call me the Wulver, and I have given up arguing with them. Mostly I fish, and I keep an eye on the yew."),
                ],
                [
                    C("Who are you, really?", "first_who", null, DialogueEffect.Set(AskedWhoIAm)),
                    C("What is this place?", "first_place"),
                    C("I should be going.", "farewell_first"),
                ],
                [visit]),
            N("first_who",
                [
                    L("A wulver, if you believe Shetland. A gentle sort of wolf who fishes from a rock and leaves the catch on the windowsills of folk who have little."),
                    L("I never asked for the work. It found me, the way work does, and I have been doing it politely ever since."),
                ],
                [
                    C("I built this hill.", "mk_1", Not(Flag(ClaimedMaker)), DialogueEffect.Set(ClaimedMaker)),
                    C("Tell me about the yew.", "yew_intro"),
                    C("I should be going.", "farewell_first"),
                ]),
            N("first_place",
                [
                    L("A hill. It has been a hill a good while and is not ashamed of it. Underneath is the long way down, and I will tell you straight that it is unkind to anyone who goes without a plan."),
                    L("Up here there is weather, a yew and a rock. The rest is below."),
                ],
                [
                    C("Tell me about the long way down.", "way_down"),
                    C("You mentioned a gift?", "gift_offer", Not(GiftGiven)),
                    C("I should be going.", "farewell_first"),
                ]),
            N("gift_offer",
                [
                    L("Aye, a wee gift. The yew gives and I pass it on. It is only mending, nothing grand: your hurts washed clean, and a steadier hand when something swings at you."),
                    L("You look like something the tide left behind. Let me do right by you.", Hurt),
                ],
                [
                    C("Take it gladly.", "gift_take_hurt", Hurt),
                    C("Take it.", "gift_take_plain", Not(Hurt)),
                    C("Not just now.", "farewell_ret"),
                ]),
            N("gift_take_plain",
                [L("There. Hands out. The yew's blessing is mostly patience, but the mending is real: your hurts are washed, and your guard will stay open a while longer than you could manage alone.")],
                [C("Thank you.", "fish_ritual"), C("Leave.", "farewell_ret")],
                [DialogueEffect.Give(GiftKind.Plain), DialogueEffect.Set(TookGift)]),
            N("gift_take_hurt",
                [
                    L("Och, sit, sit. That is a lot of hurt to carry up a hill. I will do this properly: all of it washed away, and the yew's longer patience on top."),
                    L("Do not tell the fish I went soft."),
                ],
                [C("Thank you.", "fish_ritual"), C("Leave.", "farewell_ret")],
                [DialogueEffect.Give(GiftKind.Hurt), DialogueEffect.Set(TookGift)]),
            N("fish_ritual",
                [L("Now, the fish. There is one at my feet that has done its duty. By custom it is yours to carry down, or to leave where somebody needs it. I do not mind which. I only ask that you do it on purpose.")],
                [
                    C("Leave it on a windowsill for someone.", "fish_leave", null, DialogueEffect.Set(GaveFishBack)),
                    C("Keep it. I am hungry.", "fish_keep", null, DialogueEffect.Set(KeptFish)),
                    C("Leave.", "farewell_ret"),
                ]),
            N("fish_leave",
                [G(Thanks), L("Good. That is the whole craft, you know: someone is cold, and you were passing. I shall say nothing of it, and neither will the fish.")],
                [C("Ask about the yew.", "yew_intro"), C("Leave.", "farewell_ret")]),
            N("fish_keep",
                [L("Aye, fair enough. A hungry person is as good a windowsill as any. Eat it warm, and be honest with yourself about the next one.")],
                [C("Ask about the yew.", "yew_intro"), C("Leave.", "farewell_ret")]),
            Farewell("farewell_first",
                G(Goodbye),
                L("Off you go, then. Come back when you have more to tell me, or less breath. Mind the weather.", null, "Go well, then. Come back when you have more to tell me. Mind the weather.")),

            // ---- the yew ----
            N("yew_intro",
                [
                    L("The yew. Aye. It is older than it looks, and it looks old. Yews do not die so much as lose interest; the one at Fortingall has watched a couple of thousand winters and is still being rude to the weather.", Not(Flag(HeardYew))),
                    L("Back to the yew, are we? It does not mind being asked twice. Patient by trade.", Flag(HeardYew)),
                ],
                [
                    C("Tell me the old story.", "yew_1", Not(Flag(HeardYew))),
                    C("Tell me it again.", "yew_1", Flag(HeardYew)),
                    C("Leave it be.", "farewell_ret"),
                ]),
            N("yew_1",
                [
                    L("They say folk planted yews beside their dead, and beside their churches, and sat beneath them to settle quarrels. Dark needles, flat as combed hair. Poisonous, all of it, bar the red cup around the seed."),
                    L("I think they did it because a yew does not hurry. It lets a quarrel go cold in the shade."),
                ],
                [
                    C("And the red berries?", "yew_arils"),
                    C("Why do you carry a branch?", "yew_2"),
                    C("Leave it be.", "farewell_ret"),
                ],
                [DialogueEffect.Set(HeardYew)]),
            N("yew_2",
                [L("A branch is a promise that I will come back to the tree. It is also good for pointing at weather. See? Rain, over there, by supper.")],
                [C("And the red berries?", "yew_arils"), C("Ask about the long way down.", "way_down"), C("Leave it be.", "farewell_ret")]),
            N("yew_arils",
                [L("Bright, are they not? Red is how a thing says look at me, I am not what I seem. The birds eat the sweet part and leave the seed. You might do the same with most advice, mine included.")],
                [C("Ask about the long way down.", "way_down"), C("Leave it be.", "farewell_ret")]),

            // ---- the long way down: hints that are true ----
            N("way_down",
                [
                    L("The long way down is mostly stairs and patience. I can give you three small truths about it: the doors, the folds, and the guard.", Not(Veteran)),
                    L("You know the way down as well as I do by now. Ask, and I shall be brief.", Veteran),
                ],
                [
                    C("Locked chests and keys.", "hint_chests"),
                    C("Sever and Meteor.", "hint_sever"),
                    C("Parrying.", "hint_parry"),
                    C("Leave.", "farewell_ret"),
                ]),
            N("hint_chests",
                [
                    L("A locked chest wants a key, and the keys are hidden on the same floor, in the plainer rooms: one for each locked chest there. The locked chests sit in the treasure rooms and with the Warden, and they hold the better things.", Not(Veteran)),
                    L("Keys hide on the floor, one per locked chest. Treasure rooms and the Warden hold the best.", Veteran),
                ],
                [C("Anything else?", "way_down"), C("Leave.", "farewell_ret")]),
            N("hint_sever",
                [
                    L("Two doors, one threshold. Sever is when you close the pair on something standing in the gap, and it is cut clean. Meteor is when you fall, or are flung, fast through a door and land on someone: speed turns into harm. Both reward nerve.", Not(Veteran)),
                    L("Sever: close the pair on one in the gap. Meteor: land on them fast. You know.", Veteran),
                ],
                [C("Anything else?", "way_down"), C("Leave.", "farewell_ret")]),
            N("hint_parry",
                [
                    L("When one of them flashes yellow, that is the telling. Parry as it lands and the world forgives you: you are made whole up to your ceiling, and for a breath nothing can hurt you.", Not(Veteran)),
                    L("Wait for the yellow, parry, and you are mended to your ceiling.", Veteran),
                ],
                [
                    C("What about the one at the bottom?", "hint_warden", FloorAtLeast(1)),
                    C("Anything else?", "way_down"),
                    C("Leave.", "farewell_ret"),
                ]),
            N("hint_warden",
                [L("The Warden at the bottom hits hard and slow. Read it, parry on the yellow, and do not stand where you have been standing. It is not clever, only patient, and you can be patienter.")],
                [C("Anything else?", "way_down"), C("Leave.", "farewell_ret")]),

            // ---- return visits ----
            N("hub",
                [
                    G(Weather),
                    L("Sit, then. What is on your mind? The hill keeps no timetable."),
                    L("You asked me once who I was. I am still working out the answer, mind.", Flag(AskedWhoIAm)),
                    L("You have the look of a regular. I shall have to start charging in fish.", VisitsAtLeast(4)),
                ],
                [
                    C("The yew.", "yew_intro"),
                    C("The long way down.", "way_down"),
                    C("I made this place.", "mk_1", Not(Flag(ClaimedMaker)), DialogueEffect.Set(ClaimedMaker)),
                    C("Leave.", "farewell_ret"),
                ]),
            Return("ret_welcome",
                G(Welcome),
                L("Ah, back up the hill. The wolf is still here, as promised.", null, "There you are again. The rock kept your place."),
                L("I hear a windowsill was warmer for you. Good.", Flag(GaveFishBack)),
                L("The yew has not changed its mind since you left.", Flag(HeardYew)),
                L("You are becoming a fixture, you know. I approve.", VisitsAtLeast(4))),
            Return("ret_hurt", L("You are in a state. Who has been at you down there? Sit, before you fall over, and tell the wolf nothing you do not want to.")),
            Return("ret_dead", L("How many times is it now? I stopped counting at three, out of politeness. You keep coming back up, though, and that counts for more than the falling.")),
            Return("ret_key", L("That is a key in your hand. Still warm from somebody's pocket, I would say. Do not leave it to rust on a hill, however good the company.")),
            Return("ret_ending", L("You went right to the bottom and came back, did you? Folk who have seen the lowest door walk differently. Quieter. Sit, sit.")),
            Return("ret_deep", L("You have the cold stone smell on you. Deep, then. Good; the air up here is free.")),
            Return("ret_notes", L("You have been reading the old scraps, by the way you look at the sky. Folk write things down when the dark has been talking too long. It is kind of you to listen.")),
            Farewell("farewell_ret",
                G(Goodbye),
                L("Slàinte. The hill will be here.", null, "Mind yourself on the stairs. They are patient, and you are not."),
                L("And well done with the windowsill.", Flag(GaveFishBack)),
                L("Eat that fish while it is warm.", Flag(KeptFish)),
                L("Drink something warm before you go back down.", HealthBelow(0.35))),

            // ---- claims ----
            N("claim_plain",
                [L("Ha. A good many folk say that, and I let them. It is a kind thing to believe, that someone made all this on purpose. Perhaps someone did.")],
                [C("Ask about the yew.", "yew_intro"), C("Ask about the long way down.", "way_down"), C("Leave.", "farewell_ret")]),
            N("key_recognise",
                [L("That key. From the very bottom, and still in your hand. You told me once that you made this place, and I did not push you on it. But a key like that is not carried by accident. Is there something you would like to say to me?")],
                [C("Show him the key.", "mk_1", null, DialogueEffect.Set(MakerKey)), C("Not yet.", "farewell_ret")],
                [visit]),

            // ---- the maker's path: the only place the developer's name is spoken ----
            Maker("mk_1", "Ewan.\n\nI wondered, when I saw you come up the path. Forgive an old wolf: I have waited a long while to say it to your face.", C("Go on.", "mk_2"), C("Leave.", "mk_farewell")),
            Maker("mk_2", "We wear the same name, you and I, Ewan. You wear it in the newer tongue and I in the older: Eòghann, for what you carry as Ewan. I like to think that is why you gave me the rock.", C("Go on.", "mk_3"), C("Leave.", "mk_farewell")),
            Maker("mk_3", "One person built every stair and stone of this place, alone. I have counted the stairs, Ewan, and I stopped at a number I will not say aloud, for fear of tiring you.", C("Go on.", "mk_4"), C("Leave.", "mk_farewell")),
            Maker("mk_4", "The quiet hill, the yew, the fish. You did not have to give me those, and nobody was asking. That is the kindest sort of making: the part nobody asked for.", C("Go on.", "mk_5"), C("Leave.", "mk_farewell")),
            Maker("mk_5", "Rest your eyes a little, Ewan. Go on. Look up at the sky: it was here before any of us, it is in no hurry, and it needs no mending.", C("Go on.", "mk_6"), C("Leave.", "mk_farewell")),
            Maker("mk_6", "There is no more craft in this than you have already put there. Go and eat something, and drink some water, and say hello to whoever loves you. The hill will hold.", C("Thank him.", "mk_farewell")),
            N("mk_farewell", [L("Go well, Ewan. Slàinte, from the old name to the new.")], [C("Go", null)], guard: MakerPath, fallback: "claim_plain"),
            N("mk_return", [L("Ewan. Up the hill again. The fish are well, and so am I.")], [C("How is the hill?", "mk_return_2"), C("Leave.", "mk_farewell")], [visit], MakerPath, "claim_plain"),
            Maker("mk_return_2", "Quiet, Ewan. That is the best news a hill can give. Mind the water and mind the sky. You made a gentle place; remember to be gentle in it.", C("Thank him.", "mk_farewell")),
        };

        // Once the claim is honoured he remembers: later visits greet the maker by name.
        int first = nodes.FindIndex(n => n.Id == "mk_1");
        nodes[first] = nodes[first] with { OnEnter = [DialogueEffect.Set(MakerKnown)] };

        var entries = new List<DialogueEntry>
        {
            new(All(Flag(MakerKnown), MakerPath), "mk_return"),
            new(All(Flag(ClaimedMaker), CarryingKey, FinalFloor, Not(Flag(MakerKey)), Not(Flag(MakerKnown)), VisitsAtLeast(1)), "key_recognise"),
            new(All(Flag(ClaimedMaker), MakerCircumstance, Not(Flag(MakerKnown)), VisitsAtLeast(1)), "mk_1"),
            new(VisitsBelow(1), "first_intro"),
            new(HealthBelow(0.35), "ret_hurt"),
            new(DeathsAtLeast(3), "ret_dead"),
            new(CarryingKey, "ret_key"),
            new(EndingReached, "ret_ending"),
            new(FloorAtLeast(2), "ret_deep"),
            new(NotesAtLeast(3), "ret_notes"),
            new(Always, "ret_welcome"),
        };
        return new DialogueGraph(nodes, entries);
    }

    /// <summary>The opening beat of a return visit: his remark, then sit, ask about the gift if still unclaimed, or leave.</summary>
    private static DialogueNode Return(string id, params DialogueLine[] lines) => new(id, lines,
    [
        C("Sit a while.", "hub"),
        C("About that gift.", "gift_offer", Not(GiftGiven)),
        C("Leave.", "farewell_ret"),
    ], [DialogueEffect.CountVisit()]);
}
