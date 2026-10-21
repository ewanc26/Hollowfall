using static Hollowfall.Core.Story.Dialogue.DialogueCondition;

namespace Hollowfall.Core.Story.Dialogue;

/// <summary>
/// Everything the cat says, in one file. He is a Russian Blue who sleeps beside a fire pit, a small gift, and the ONLY entity in the
/// game with this name: it is written here and nowhere else in code. He speaks Irish (Gaeilge, not Scottish Gaelic) in short, fixed
/// phrases, each with its English beneath it. Every Irish string is listed with its source and status in docs/gaeilge.md and needs a
/// fluent speaker's review before release. Sources are the Foras na Gaeilge / Ó Dónaill dictionary on teanglann.ie, Wiktionary and
/// learner pages; nothing here is a free sentence that was not checked.
/// <para>
/// His voice: gentle, sleepy and dry. Stage directions are in English, in brackets; the cat's words are Irish, then English in brackets.
/// </para>
/// </summary>
public static class CatDialogue
{
    /// <summary>What the panel calls him (Irish form, with the acute accent on the a).</summary>
    public const string Name = "Aodhán";

    /// <summary>The single caption shown the first time the cat comes into view. Nothing more.</summary>
    public const string FirstSight = "A grey cat rests beside the fire.";

    // ---- flags he remembers (short ids, saved with the story progress) ----
    public const string Blinked = "blinked";
    public const string Petted = "petted";
    public const string Sat = "sat";
    public const string Thanked = "thanked";
    public const string HeardSmall = "heardSmall";

    /// <summary>The conversation that has been started twice before plays the warm line once.</summary>
    public const int VisitsForWarmLine = 2;

    public static DialogueGraph Graph { get; } = Build();

    private static DialogueLine L(string text, DialogueCondition? when = null) => new(text, when);

    private static DialogueChoice C(string text, string? next, DialogueCondition? when = null, params DialogueEffect[] effects) => new(text, next, when, effects.Length > 0 ? effects : null);

    private static DialogueNode N(string id, DialogueLine[] lines, DialogueChoice[] choices, DialogueEffect[]? enter = null) => new(id, lines, choices, enter);

    private static DialogueGraph Build()
    {
        DialogueEffect visit = DialogueEffect.CountVisit();
        var hubChoices = new[]
        {
            C("Sit down by the fire.", "sit", Not(Flag(Sat))),
            C("Stroke him.", "pet"),
            C("Thank him.", "thanks", Not(Flag(Thanked))),
            C("Say goodbye.", "bye"),
        };

        var nodes = new List<DialogueNode>
        {
            N("meet",
                [
                    L("(One green eye opens. Closes. Opens again, with some reluctance, and finds you.)"),
                    L("Dia duit.\n(Hello.)"),
                ],
                [
                    C("Blink back, slowly.", "blink", null, DialogueEffect.Set(Blinked)),
                    C("Answer him: Dia is Muire duit.", "reply"),
                    C("Let him sleep.", "bye"),
                ],
                [visit]),
            N("blink",
                [
                    L("(He blinks back, slowly. Among cats this is a fairly serious declaration of trust.)"),
                    L("Fáilte.\n(Welcome.)"),
                ],
                hubChoices),
            N("reply",
                [
                    L("(The ears turn toward you, pleased. The tail takes one dignified turn.)"),
                    L("Fáilte, a chara.\n(Welcome, friend.)"),
                ],
                hubChoices),
            N("fire",
                [
                    L("(The fire pops. He pretends not to notice.)"),
                    L("Tá sé fuar.\n(It is cold.)"),
                    L("Teas na tine.\n(The warmth of the fire.)"),
                ],
                hubChoices),
            N("pet",
                [
                    L("(He leans into your hand and begins a purr you can feel in your wrist.)"),
                    L("Maith thú.\n(Good for you.)"),
                ],
                [
                    C("Sit beside him.", "sit", Not(Flag(Sat))),
                    C("Look at the fire.", "fire"),
                    C("Say goodbye.", "bye"),
                ],
                [DialogueEffect.Set(Petted)]),
            N("sit",
                [
                    L("Suigh láimh liom.\n(Sit beside me.)"),
                    L("(You do. He shuffles over until he is mostly on your boot. The stone is warm, the fire is warm, and something in you that has been clenched all day lets go a little.)"),
                    L("Tá sé teolaí.\n(It is warm and cosy.)"),
                ],
                [
                    C("Thank him.", "thanks", Not(Flag(Thanked))),
                    C("Stroke him.", "pet"),
                    C("Say goodbye.", "bye_night"),
                ],
                [DialogueEffect.Set(Sat), DialogueEffect.Give(GiftKind.Warmth)]),
            N("thanks",
                [
                    L("(A slow blink. A sleepy twitch of one ear.)"),
                    L("Tá fáilte romhat.\n(You are welcome.)"),
                ],
                [
                    C("Sit down by the fire.", "sit", Not(Flag(Sat))),
                    C("Stroke him.", "pet"),
                    C("Say goodbye.", "bye"),
                ],
                [DialogueEffect.Set(Thanked)]),
            N("back",
                [
                    L("(The cat looks up before you reach the fire, and his ears go forward.)"),
                    L("A chara!\n(Friend!)"),
                    L("(He remembers your hand, and puts his head under it without being asked.)", Flag(Petted)),
                    L("Suigh láimh liom.\n(Sit beside me.)", Flag(Sat)),
                    L("Fáilte romhat.\n(Welcome.)", Not(Flag(Sat))),
                ],
                [
                    C("Sit down by the fire.", "sit", Not(Flag(Sat))),
                    C("Stroke him.", "pet"),
                    C("Look at the fire.", "fire"),
                    C("Say goodbye.", "bye"),
                ],
                [visit]),
            N("small",
                [
                    L("(He looks at you for a long moment. Then he blinks, very slowly, and does not look away.)"),
                    L("Tine bheag. Teas na tine. Suigh láimh liom, a chara.\n(A little fire. The warmth of the fire. Sit beside me, friend.)"),
                ],
                [
                    C("Sit beside him.", "sit", Not(Flag(Sat))),
                    C("Stroke him.", "pet"),
                    C("Say goodbye.", "bye_night"),
                ],
                [visit, DialogueEffect.Set(HeardSmall)]),
            N("bye", [L("(He is already closing his eyes.)"), L("Slán leat.\n(Goodbye.)")], [C("Go", null)]),
            N("bye_night", [L("(He is already closing his eyes.)"), L("Oíche mhaith agat. Codladh sámh duit.\n(Good night. Sleep well.)")], [C("Go", null)]),
        };

        var entries = new List<DialogueEntry>
        {
            new(All(VisitsAtLeast(VisitsForWarmLine), Not(Flag(HeardSmall))), "small"),
            new(VisitsBelow(1), "meet"),
            new(Always, "back"),
        };
        return new DialogueGraph(nodes, entries);
    }
}
