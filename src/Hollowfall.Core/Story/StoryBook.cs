using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Story;

/// <summary>
/// How deep a floor is, relative to its dungeon. A dungeon has two to four floors, so the story is told by position and
/// not by absolute floor number: the first floor is always the Upper band and the last (the boss) is always Last.
/// </summary>
public enum DepthBand
{
    Upper,
    Middle,
    Lower,
    Last,
}

/// <summary>One authored lore note. <see cref="Thread"/> groups the notes of one voice; they are read in <see cref="Order"/>.</summary>
public sealed record NoteDef(string Id, DepthBand Band, string Thread, int Order, string Title, string Text);

/// <summary>One authored floor title card. Several are written for each band and a seed picks among them.</summary>
public sealed record CardDef(string Id, DepthBand Band, string Name, string Epigraph);

/// <summary>
/// Every word of the story, in one engine-free place. Text may hold three placeholders, filled in per seed by
/// <see cref="StoryPlan"/>: <c>{hollow}</c> (the dungeon's name, "the Pale Vault"), <c>{Hollow}</c> (the same with a capital
/// T) and <c>{warden}</c> (the name of the last Warden, the boss). The text is plain ASCII on purpose, so any font draws it.
/// See docs/story.md for what it all means.
/// </summary>
public static class StoryBook
{
    public const int MaxNoteLength = 300;
    public const int MaxLineLength = 200;

    // Names are picked by seed. The Wardens named in the notes (Tamsin, Odile, Marrec, Ansel, Haldane) must not appear in WardenNames.
    public static readonly string[] HollowAdjectives =
    [
        "Ashen", "Bleak", "Cinder", "Drowned", "Gilded", "Hushed", "Iron", "Marrow",
        "Pale", "Rust", "Silent", "Sunken", "Thorn", "Umber", "Weeping", "Wicker",
    ];

    public static readonly string[] HollowNouns =
    [
        "Barrow", "Cellar", "Cistern", "Crypt", "Delve", "Fold", "Gullet", "Maw",
        "Reliquary", "Shaft", "Sink", "Throat", "Undercroft", "Vault", "Warren", "Well",
    ];

    public static readonly string[] WardenNames =
    [
        "Ysolde", "Corvane", "Maren", "Edric", "Sabine", "Orsolo", "Thessaly", "Garrick",
    ];

    // ---- the opening ---------------------------------------------------------------------------

    /// <summary>The first line of the opening crawl; the seed picks one. The rest of the crawl is <see cref="IntroBody"/>.</summary>
    public static readonly string[] IntroOpeners =
    [
        "Beneath the hill lies a Hollow. The old maps call it {hollow}, and it is falling in on itself.",
        "{Hollow} is the name on the old maps. Under it the Hollow has begun to fall inward, and nobody above has noticed.",
        "Every night the Hollow under the hill settles deeper. The old maps call it {hollow}. Tonight it began to fall.",
    ];

    public static readonly string[] IntroBody =
    [
        "Seven Wardens went down to hold it shut. Their red cord still runs from the door into the dark.",
        "Six of them are only names now. The seventh, {warden}, took the fold-craft and went below, and the stone has not stopped coming down since.",
        "You are a Delver. You follow the cord. Heal on what you break, and do not stop.",
    ];

    // ---- floor title cards ---------------------------------------------------------------------

    public static readonly IReadOnlyList<CardDef> Cards =
    [
        new("card-up-1", DepthBand.Upper, "The Antechamber", "Where the cord begins."),
        new("card-up-2", DepthBand.Upper, "The Sealed Gallery", "Seven names above the door. Six have been scratched out."),
        new("card-up-3", DepthBand.Upper, "The Threshold Stair", "The first step down is the last one anyone remembers."),

        new("card-mid-1", DepthBand.Middle, "The Drowned Archive", "Everything here was written down. Nothing here was kept."),
        new("card-mid-2", DepthBand.Middle, "The Ossuary", "The stone leans in to listen."),
        new("card-mid-3", DepthBand.Middle, "The Long Fold", "A door that opens onto itself is still a door."),
        new("card-mid-4", DepthBand.Middle, "The Gallery of Stitches", "Every seam is a promise somebody made."),

        new("card-low-1", DepthBand.Lower, "The Throat", "Dust runs downhill. So do you."),
        new("card-low-2", DepthBand.Lower, "The Unspooling", "Here the cord grows thin."),
        new("card-low-3", DepthBand.Lower, "The Swallow", "The Hollow never needed to bite."),

        new("card-last-1", DepthBand.Last, "The Last Door", "Someone is holding it shut from the inside."),
        new("card-last-2", DepthBand.Last, "The Warden's Rest", "Nobody rests here. Someone has been standing a very long time."),
        new("card-last-3", DepthBand.Last, "The Bottom of the Hollow", "Falling stops. Everything else continues."),
    ];

    // ---- lore notes ----------------------------------------------------------------------------

    public static readonly IReadOnlyList<NoteDef> Notes =
    [
        // Upper: the expedition's own words, practical and still hopeful.
        new("up-ledger-1", DepthBand.Upper, "ledger", 1, "Muster Roll",
            "Seven Wardens went in under the hill, and seven cords were paid out behind us. Mine is the red one. If you have followed it this far, someone is still keeping the door."),
        new("up-ledger-2", DepthBand.Upper, "ledger", 2, "Day Two",
            "The stairs are older than the order that sealed them. We found the first folded door standing open and chose not to ask why. Tamsin says the walls are listening, but Tamsin says that about every wall."),
        new("up-ledger-3", DepthBand.Upper, "ledger", 3, "Fresh Blood",
            "Odile cut her hand on a lintel and the cut closed while she watched. The Hollow gives back what you spill in it, she says. Nobody laughed. We began to count what we owed."),

        new("up-warning-1", DepthBand.Upper, "warning", 1, "Scratched Warning",
            "DO NOT STAND STILL. The floor remembers weight. Keep moving, keep the cord taut, and when something bleeds near you, be near enough to take it back."),
        new("up-warning-2", DepthBand.Upper, "warning", 2, "Odile's Rule",
            "Strike close. Never wait for mercy. Never rest in a room you did not clear. The Hollow heals the ones who press forward and buries the ones who stop."),
        new("up-warning-3", DepthBand.Upper, "warning", 3, "A Torn Page",
            "Whoever lit these torches, thank you. Whoever put out the others, I understand. Some rooms should keep their dark."),

        // Middle: how the sealing worked, and what the place does to people.
        new("mid-gates-1", DepthBand.Middle, "gates", 1, "On the Folded Doors",
            "The craft is simple to say. Two doors, one threshold: step through the first and stand in the second. The old Wardens sealed the Hollow by folding every way out back onto itself, because a sealed thing does not fall. It only waits."),
        new("mid-gates-2", DepthBand.Middle, "gates", 2, "Marrec's Warning",
            "Marrec says every door we fold is a stitch, and this place has been torn too many times to hold another. He is right. I am writing it down so that when he is proved right it will be on record."),
        new("mid-gates-3", DepthBand.Middle, "gates", 3, "A Poor Joke",
            "Ansel stepped through a gate and saw his own back walking away. He laughed for an hour. The next day he did not laugh, and the day after that he would not step through anything."),

        new("mid-bones-1", DepthBand.Middle, "bones", 1, "The Count",
            "Forty-one skulls on the shelf and not a mark on any of them. The Hollow does not kill, Brother Haldane says. It lets you fall until you stop being the sort of thing that gets up."),
        new("mid-bones-2", DepthBand.Middle, "bones", 2, "Haldane's Sermon",
            "The stone leans inward and we lean with it. Pray if you like. The Hollow is no god. It is a debt, and debts do not hear prayers, they collect."),
        new("mid-bones-3", DepthBand.Middle, "bones", 3, "Unsent Letter",
            "To whoever has my cord: I am not coming back by the stair. Tell my sister the mill is hers. Tell her I was not afraid until the very end, which is the longest part."),

        // Lower: the collapse itself, and the first doubt about one of the seven.
        new("low-crack-1", DepthBand.Lower, "crack", 1, "Survey, Last Page",
            "The measurements will not agree. The shaft is deeper than the hill is tall. Either the Hollow has grown, or we have been walking a very long way down into somewhere that was never under the hill at all."),
        new("low-crack-2", DepthBand.Lower, "crack", 2, "The Tilt",
            "Every floor leans a little further than the one above. Dust runs toward the middle. By the lowest door the whole place is a throat, and it is swallowing."),
        new("low-crack-3", DepthBand.Lower, "crack", 3, "Tamsin's Hand",
            "Tamsin wrote in her own blood because the ink had frozen: 'Not a collapse. An unfolding. Someone took a stitch out.' Under it is a name, and the name is {warden}."),

        new("low-warden-1", DepthBand.Lower, "warden", 1, "The Youngest",
            "{warden} was the youngest of the seven and the best with the craft. They never once asked why the old Wardens feared the lowest door. We should have asked for them."),
        new("low-warden-2", DepthBand.Lower, "warden", 2, "The Empty Case",
            "The fold-work case is empty. Every paired gate, every needle, every line of the craft is gone, and the case was left latched. Whoever took it had the key and a reason, and I know one of us who had both."),
        new("low-warden-3", DepthBand.Lower, "warden", 3, "Last Entry",
            "I followed {warden} to the final stair and watched them unthread the cord from their own wrist. 'Someone has to hold it shut from the inside,' they said. I let them go. I have been sorry since."),

        // Last: found beside the boss, and left deliberately unresolved.
        new("last-verdict-1", DepthBand.Last, "verdict", 1, "Cut Into the Lintel",
            "The last door is open from the inside. Nothing forced it. It was invited."),
        new("last-verdict-2", DepthBand.Last, "verdict", 2, "In {warden}'s Hand",
            "I have the craft. I have the stair. I have everything they wanted and nothing I came for. The Hollow does not want to fall, it wants to be finished, and I am finishing it slowly so that nothing else has to."),
        new("last-verdict-3", DepthBand.Last, "verdict", 3, "To the One With the Cord",
            "If you have come this far, the cord is yours and the choice with it. Put me down or let me hold. I have forgotten which of us is the more tired."),

        new("last-door-1", DepthBand.Last, "door", 1, "A Scratch by the Frame",
            "Count the gates you leave open behind you. Each is a stitch you did not take out, and each is a way for the dark to follow. Mind that you cut only what you must."),
        new("last-door-2", DepthBand.Last, "door", 2, "Epitaph",
            "Seven went in. One cord came back to the surface, and the surface forgot to ask whose hand was on the end of it."),
    ];

    // ---- grades, boss, death, ending -----------------------------------------------------------

    /// <summary>A line for the end of a floor, one for each overall grade (D to P).</summary>
    public static readonly IReadOnlyDictionary<OverallGrade, string> FloorComplete = new Dictionary<OverallGrade, string>
    {
        [OverallGrade.D] = "You are through, but only just. The cord is still in your hand.",
        [OverallGrade.C] = "The stair takes you. Another piece of the floor lets go behind you.",
        [OverallGrade.B] = "Steady work. The stone leans a little less as you leave.",
        [OverallGrade.A] = "Clean. The Hollow notices.",
        [OverallGrade.S] = "Fast and merciless. The Hollow remembers fast things.",
        [OverallGrade.P] = "Not a drop wasted. Even the dust holds its breath.",
    };

    public static readonly string[] BossIntros =
    [
        "{warden}, last of the Wardens, turns from the open door. 'You followed the cord. Of course you did.'",
        "'That craft is mine,' says {warden}. 'I would know my own stitching anywhere.' The dark behind them is moving.",
        "The cord ends at {warden}'s wrist. They look at you, then at the door, then at you again, as if choosing which to keep.",
    ];

    public static readonly string[] DeathLines =
    [
        "The cord goes slack. Then it pulls you back.",
        "You fall, and the Hollow, being patient, lets you.",
        "Nobody counts the ones who stop. You begin again.",
        "Blood returns to the stone. So do you, for a moment.",
        "The stair remembers your weight. Try again.",
        "Seven went in. You are not the first to be sent back.",
    ];

    public const string EndingTitle = "THE CORD HOLDS";

    public static readonly string[] EndingParagraphs =
    [
        "{warden} falls, and the Hollow stops falling. For a moment nothing in the dark moves.",
        "The fold-craft is in your hand, and it is warm, like something that has been waiting to be held. The last door stands open. Beyond it, the cord goes on.",
        "Seven went in. One cord came back. Whether you have sealed {hollow} or only opened it wider, the stone does not say.",
    ];

    // ---- helpers -------------------------------------------------------------------------------

    /// <summary>Which band a floor belongs to. The first is Upper and the last is always Last; those between are Middle, then Lower, then Middle again.</summary>
    public static DepthBand BandOf(int floor, int floorCount)
    {
        if (floorCount <= 1 || floor >= floorCount - 1) return DepthBand.Last;
        if (floor <= 0) return DepthBand.Upper;
        return (floor - 1) % 2 == 0 ? DepthBand.Middle : DepthBand.Lower;
    }

    /// <summary>Every authored string, for tests that check bounds, uniqueness and emptiness.</summary>
    public static IEnumerable<string> AllText()
    {
        foreach (var s in IntroOpeners) yield return s;
        foreach (var s in IntroBody) yield return s;
        foreach (var c in Cards)
        {
            yield return c.Name;
            yield return c.Epigraph;
        }
        foreach (var n in Notes)
        {
            yield return n.Title;
            yield return n.Text;
        }
        foreach (var s in FloorComplete.Values) yield return s;
        foreach (var s in BossIntros) yield return s;
        foreach (var s in DeathLines) yield return s;
        yield return EndingTitle;
        foreach (var s in EndingParagraphs) yield return s;
    }
}
