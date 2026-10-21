using Hollowfall.Core.Relations;

namespace Hollowfall.Tests;

public class VillagerNetTests
{
    private static readonly Traits Middling = new(0.5, 0.5, 0.5);
    private static readonly Traits Brave = new(0.8, 0.5, 0.5);
    private static readonly Traits Timid = new(0.2, 0.5, 0.5);

    private static Intent Decide(Perception p, Traits? t = null, double anger = 0, double fear = 0, Intent previous = Intent.Idle) =>
        VillagerNet.Decide(new VillagerSense(p, t ?? Middling, anger, fear, previous));

    [Fact]
    public void TheWeightsMatchTheNetworkShape()
    {
        Assert.Equal(VillagerNet.Inputs * VillagerNet.Hidden, VillagerWeights.Hidden.Length);
        Assert.Equal(VillagerNet.Hidden, VillagerWeights.HiddenBias.Length);
        Assert.Equal(VillagerNet.Hidden * VillagerNet.Outcomes, VillagerWeights.Output.Length);
        Assert.Equal(VillagerNet.Outcomes, VillagerWeights.OutputBias.Length);
        Assert.Equal(VillagerNet.Inputs, VillagerNet.Encode(new VillagerSense(new Perception(1, 0, true, 0), Middling, 0, 0, Intent.Idle)).Length);
    }

    [Fact]
    public void TheOutcomesAreAProbabilityAndTheSameEveryTime()
    {
        var sense = new VillagerSense(new Perception(5, 1, true, 3), Brave, 0.4, 0.1, Intent.Warn);
        var first = VillagerNet.Forward(VillagerNet.Encode(sense));
        Assert.Equal(1.0, first.Sum(), 9);
        Assert.All(first, v => Assert.InRange(v, 0, 1));
        Assert.Equal(first, VillagerNet.Forward(VillagerNet.Encode(sense)));
    }

    [Fact]
    public void ACalmVillagerKeepsToItsDoorWhenThePlayerIsFar()
    {
        Assert.Equal(Intent.Idle, Decide(new Perception(12, 0, true, 2)));
    }

    [Fact]
    public void ACalmVillagerBacksOffWhenThePlayerIsTooClose()
    {
        Assert.Equal(Intent.Flee, Decide(new Perception(2.5, 0.5, true, 4)));
    }

    [Fact]
    public void ACalmVillagerBacksOffFromAFastApproach()
    {
        Assert.Equal(Intent.Flee, Decide(new Perception(6, 3.5, true, 4)));
    }

    [Fact]
    public void ABraveVillagerWhoIsProvokedAndCloseFights()
    {
        Assert.Equal(Intent.Attack, Decide(new Perception(4, 0, true, 3), Brave, anger: 0.6));
    }

    [Fact]
    public void ATimidVillagerWhoIsProvokedRuns()
    {
        Assert.Equal(Intent.Flee, Decide(new Perception(4, 0, true, 3), Timid, anger: 0.6));
    }

    [Fact]
    public void AFrightenedVillagerRunsEvenFromAFarPlayer()
    {
        Assert.Equal(Intent.Flee, Decide(new Perception(10, 0, true, 3), fear: 0.9));
    }

    [Fact]
    public void AVillagerBacksAwayFromTheHollowBesideIt()
    {
        Assert.Equal(Intent.Flee, Decide(new Perception(9, 0, false, 3, HostileDistance: 2)));
    }
}
