using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Relations;
using Hollowfall.Core.Story;
using Hollowfall.Core.Story.Dialogue;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.Village;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class VillagerDialogueTests
{
    private static DialogueRunner Begin(VillagerConversation conversation, ReputationTier tier) => conversation.Begin(tier);

    private static void Take(VillagerConversation conversation, string choice)
    {
        var runner = conversation.Conversation ?? throw new InvalidOperationException("no conversation in progress");
        int index = runner.Choices.ToList().FindIndex(c => c.Text == choice);
        Assert.True(index >= 0, $"no choice '{choice}' at node {runner.NodeId}");
        Assert.True(conversation.Choose(index));
    }

    [Fact]
    public void TheTreeIsSound() => Assert.Empty(DialogueValidator.Validate(VillagerDialogue.Graph));

    [Fact]
    public void EveryNodeOffersAWayOut()
    {
        foreach (var node in VillagerDialogue.Graph.Nodes)
            Assert.True(DialogueValidator.HasLeave(VillagerDialogue.Graph, node), $"node {node.Id} has no way out");
    }

    [Fact]
    public void AFirstVisitorIsGreetedAtAnyNeutralStanding()
    {
        var conversation = new VillagerConversation(7, "amber-hollow-914");
        Assert.Equal("first", Begin(conversation, ReputationTier.Neutral).NodeId);
        Assert.Equal("first", new VillagerConversation(7, "amber-hollow-914").Begin(ReputationTier.Wary).NodeId);
    }

    [Theory]
    [InlineData(ReputationTier.Hated)]
    [InlineData(ReputationTier.Hostile)]
    public void HostileCroftersKeepTheirDistance(ReputationTier tier)
    {
        var runner = new VillagerConversation(7, "amber-hollow-914").Begin(tier);
        Assert.Equal("hostile", runner.NodeId);
    }

    [Fact]
    public void TrustedCroftersAskYouIn()
    {
        var conversation = new VillagerConversation(7, "amber-hollow-914");
        Assert.Equal("trusted", Begin(conversation, ReputationTier.Trusted).NodeId);
        Assert.Equal("trusted", new VillagerConversation(7, "amber-hollow-914").Begin(ReputationTier.Revered).NodeId);
    }

    [Fact]
    public void TheGlenRemembersWhatWasAskedAndHowOftenItWasSpokenTo()
    {
        var first = new VillagerConversation(7, "amber-hollow-914");
        Begin(first, ReputationTier.Neutral);
        Assert.Equal(1, first.Visits);
        Take(first, "Ask about the Hollow.");
        first.End();
        Assert.Contains(VillagerDialogue.AskedHollow, first.Flags);

        var again = new VillagerConversation(7, "amber-hollow-914", first.Flags, first.Visits);
        var runner = Begin(again, ReputationTier.Neutral);
        Assert.Equal("again", runner.NodeId);
        Assert.Contains("last time", runner.Text);
        Assert.Equal(2, again.Visits);
    }

    [Fact]
    public void EveryPathEndsWhateverTheStanding()
    {
        foreach (var tier in Enum.GetValues<ReputationTier>())
        {
            var conversation = new VillagerConversation(7, "amber-hollow-914");
            Begin(conversation, tier);
            for (int step = 0; step < 40 && conversation.Conversation != null; step++)
                Assert.True(conversation.Choose(conversation.Conversation.Choices.Count - 1));
            Assert.Null(conversation.Conversation);
        }
    }

    [Fact]
    public void ANearbyVillagerCountsAsInReachOnlyWhileAlive()
    {
        var (world, plan) = FindVillage();
        var sim = new CombatSim(world, FindSite(world));
        sim.EnableRelations();
        var door = plan.Residents.First();
        var near = new Vec3(door.X + 0.5, plan.Ground + 1, door.Z + 0.5);
        sim.Tick(1.0 / 60, new PlayerView(near, near + new Vec3(0, 1.6, 0), new Vec3(1, 0, 0), false, false), default);

        Assert.True(VillagerConversation.InReach(sim.Enemies, near));
        Assert.False(VillagerConversation.InReach(sim.Enemies, new Vec3(plan.CenterX + 40.5, plan.Ground + 1, plan.CenterZ + 40.5)));
        Assert.False(VillagerConversation.InReach([], near));
    }

    private static (VoxelWorld World, VillagePlan Plan) FindVillage()
    {
        for (long seed = 1; seed <= 200; seed++)
        {
            var world = new VoxelWorld(seed);
            foreach (var (cx, cz) in Enumerable.Range(-2, 5).SelectMany(x => Enumerable.Range(-2, 5).Select(z => (x, z))))
                if (world.Generator.Villages.PlanFor(cx, cz) is { } plan) return (world, plan);
        }
        throw new InvalidOperationException("no village in the first 200 seeds");
    }

    private static DungeonSite FindSite(VoxelWorld world)
    {
        for (int rx = -3; rx <= 3; rx++)
            for (int rz = -3; rz <= 3; rz++)
                if (world.Generator.Dungeons!.GetSite(rx, rz) is { } s) return s;
        throw new InvalidOperationException("no dungeon site near the origin");
    }
}
