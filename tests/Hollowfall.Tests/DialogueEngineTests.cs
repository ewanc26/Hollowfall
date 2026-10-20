using Hollowfall.Core.Story;
using Hollowfall.Core.Story.Dialogue;
using static Hollowfall.Core.Story.Dialogue.DialogueCondition;

namespace Hollowfall.Tests;

public class DialogueEngineTests
{
    private static DialogueContext Ctx(params string[] flags) => new() { Flags = new HashSet<string>(flags) };

    private static DialogueGraph Tiny(params DialogueNode[] extra)
    {
        var nodes = new List<DialogueNode>
        {
            new("a", [new DialogueLine("Hello.")], [new DialogueChoice("On", "b", null, [DialogueEffect.Set("seen"), DialogueEffect.CountVisit()]), new DialogueChoice("Leave", null)]),
            new("b", [new DialogueLine("Seen.", Flag("seen")), new DialogueLine("Unseen.", Not(Flag("seen")))], [new DialogueChoice("Bye", null)]),
            new("g", [new DialogueLine("Guarded.")], [new DialogueChoice("Bye", null)], null, Flag("key"), "b"),
        };
        nodes.AddRange(extra);
        return new DialogueGraph(nodes, [new DialogueEntry(Flag("g"), "g"), new DialogueEntry(Always, "a")]);
    }

    // ---- conditions ----

    [Fact]
    public void AtomsAndCombinatorsEvaluate()
    {
        var c = new DialogueContext { VisitCount = 2, HealthFraction = 0.3, Deaths = 4, Kills = 10, DeepestFloor = 1, FloorCount = 2, NotesRead = 3, HasKey = true, GiftGiven = true, EndingReached = true, Flags = new HashSet<string> { "x" } };
        Assert.True(Flag("x").Evaluate(c));
        Assert.False(Flag("y").Evaluate(c));
        Assert.True(VisitsAtLeast(2).Evaluate(c));
        Assert.False(VisitsBelow(2).Evaluate(c));
        Assert.True(HealthBelow(0.35).Evaluate(c));
        Assert.True(DeathsAtLeast(4).Evaluate(c));
        Assert.False(KillsAtLeast(11).Evaluate(c));
        Assert.True(FloorAtLeast(1).Evaluate(c));
        Assert.True(NotesAtLeast(3).Evaluate(c));
        Assert.True(CarryingKey.Evaluate(c));
        Assert.True(GiftGiven.Evaluate(c));
        Assert.True(EndingReached.Evaluate(c));
        Assert.True(FinalFloor.Evaluate(c));
        Assert.True((Flag("x") & CarryingKey).Evaluate(c));
        Assert.False((Flag("x") & Flag("y")).Evaluate(c));
        Assert.True((Flag("y") | Flag("x")).Evaluate(c));
        Assert.True((!Flag("y")).Evaluate(c));
        Assert.True(All(Flag("x"), Not(Flag("y")), Any(Flag("z"), CarryingKey)).Evaluate(c));
        Assert.True(Always.Evaluate(c));
    }

    [Theory]
    [InlineData("ewan", true)]
    [InlineData("EWAN", true)]
    [InlineData("Ewan Croft", true)]
    [InlineData("ewan-croft", true)]
    [InlineData("Eòghann", true)]
    [InlineData("EOGHANN", true)]
    [InlineData("ÈÒGHANN", true)]
    [InlineData("ewanc26", true)]
    [InlineData("Ewan C 26", true)]
    [InlineData("voxel", false)]
    [InlineData("ewans", false)]
    [InlineData("", false)]
    public void SeedNamesMatchIgnoringCaseAccentsSpacesAndHyphens(string seed, bool expected)
    {
        var maker = SeedIsOneOf(WulverDialogue.MakerSeeds);
        Assert.Equal(expected, maker.Evaluate(new DialogueContext { SeedText = seed }));
    }

    // ---- runner ----

    [Fact]
    public void TheRunnerStartsAtTheFirstEntryThatFitsAndShowsLinesAndChoices()
    {
        var r = new DialogueRunner(Tiny(), Ctx());
        Assert.Equal("a", r.NodeId);
        Assert.Equal("Hello.", r.Text);
        Assert.Equal(["On", "Leave"], r.Choices.Select(c => c.Text));
        Assert.Equal("g", new DialogueRunner(Tiny(), Ctx("g", "key")).NodeId);
    }

    [Fact]
    public void ChoicesApplyEffectsAndMoveOnAndLinesFollowConditions()
    {
        var r = new DialogueRunner(Tiny(), Ctx());
        Assert.True(r.Advance(0));
        Assert.Equal("b", r.NodeId);
        Assert.Equal("Seen.", r.Text);
        Assert.Contains("seen", r.Context.Flags);
        Assert.Equal(1, r.Context.VisitCount);
        Assert.True(r.Advance(0));
        Assert.True(r.Finished);
    }

    [Fact]
    public void BadIndexesAreIgnoredAndNeverThrow()
    {
        var r = new DialogueRunner(Tiny(), Ctx());
        foreach (int bad in new[] { -1, 2, 99, int.MinValue, int.MaxValue }) Assert.False(r.Advance(bad));
        Assert.False(r.Finished);
        Assert.Equal("a", r.NodeId);
        r.Advance(1);
        Assert.True(r.Finished);
        Assert.False(r.Advance(0));
        r.Abandon();
        Assert.Empty(r.Choices);
    }

    [Fact]
    public void AGuardRedirectsToItsFallbackAndAMissingNodeEndsTheTalk()
    {
        var graph = Tiny(new DialogueNode("to_g", [new DialogueLine("x")], [new DialogueChoice("Go", "g"), new DialogueChoice("Leave", null)]));
        var r = new DialogueRunner(new DialogueGraph(graph.Nodes, [new DialogueEntry(Always, "to_g")]), Ctx());
        r.Advance(0);
        Assert.Equal("b", r.NodeId);

        var held = new DialogueRunner(new DialogueGraph(graph.Nodes, [new DialogueEntry(Always, "to_g")]), Ctx("key"));
        held.Advance(0);
        Assert.Equal("g", held.NodeId);

        Assert.True(new DialogueRunner(new DialogueGraph(graph.Nodes, [new DialogueEntry(Always, "nowhere")]), Ctx()).Finished);
    }

    [Fact]
    public void AGiftIsReportedOnceAndNotAgainOnceGiven()
    {
        var node = new DialogueNode("gift", [new DialogueLine("Here.")], [new DialogueChoice("Again", "gift", null, [DialogueEffect.Give(GiftKind.Plain)]), new DialogueChoice("Leave", null)], [DialogueEffect.Give(GiftKind.Hurt)]);
        var r = new DialogueRunner(new DialogueGraph([node], [new DialogueEntry(Always, "gift")]), Ctx());
        Assert.Equal([GiftKind.Hurt], r.TakeGifts());
        Assert.Empty(r.TakeGifts());
        r.Advance(0);
        Assert.Empty(r.TakeGifts());
        Assert.True(r.Context.GiftGiven);
    }

    [Fact]
    public void FlagsCanBeClearedAndAnEndEffectEndsTheTalk()
    {
        var node = new DialogueNode("n", [new DialogueLine("x")], [new DialogueChoice("Forget", "n", null, [DialogueEffect.Clear("f")]), new DialogueChoice("Done", null, null, [DialogueEffect.End()])]);
        var r = new DialogueRunner(new DialogueGraph([node], [new DialogueEntry(Always, "n")]), Ctx("f"));
        r.Advance(0);
        Assert.DoesNotContain("f", r.Context.Flags);
        r.Advance(1);
        Assert.True(r.Finished);
    }

    [Fact]
    public void SeededVariantsAreDeterministicAndVaryAcrossSeeds()
    {
        var node = new DialogueNode("v", [new DialogueLine("one", null, ["two", "three"])], [new DialogueChoice("Leave", null)]);
        var graph = new DialogueGraph([node], [new DialogueEntry(Always, "v")]);
        string Say(long seed) => new DialogueRunner(graph, new DialogueContext { Seed = seed }).Text;
        Assert.Equal(Say(5), Say(5));
        Assert.Equal(3, Enumerable.Range(1, 60).Select(s => Say(s)).Distinct().Count());
    }

    [Fact]
    public void AForkIsIndependent()
    {
        var r = new DialogueRunner(Tiny(), Ctx());
        var f = r.Fork();
        f.Advance(0);
        Assert.Equal("a", r.NodeId);
        Assert.Equal("b", f.NodeId);
    }

    // ---- validator ----

    [Fact]
    public void TheValidatorFindsTheMistakesAnAuthorCanMake()
    {
        var bad = new DialogueGraph(
        [
            new DialogueNode("a", [new DialogueLine("")], [new DialogueChoice("x", "missing")]),
            new DialogueNode("a", [new DialogueLine(new string('x', 600))], [new DialogueChoice("x", "a")]),
            new DialogueNode("island", [new DialogueLine("lonely ☃")], []),
            new DialogueNode("trap", [new DialogueLine("in")], [new DialogueChoice("on", "trap2")]),
            new DialogueNode("trap2", [new DialogueLine("in")], [new DialogueChoice("on", "trap")]),
        ],
        [new DialogueEntry(Always, "a"), new DialogueEntry(Always, "gone"), new DialogueEntry(Always, "trap")]);
        var problems = string.Join("\n", DialogueValidator.Validate(bad));
        foreach (var expected in new[] { "duplicate node id a", "missing node missing", "missing node gone", "an empty line", "too long", "unapproved character", "0 choices", "unreachable", "cannot reach an end", "no way to leave" })
            Assert.Contains(expected, problems);
    }
}
