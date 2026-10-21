using Hollowfall.Core.Combat;
using Xunit;

namespace Hollowfall.Tests;

public class RoomMutatorTests
{
    private static IEnumerable<(int Floor, int Room)> SampleRooms()
    {
        for (int floor = 0; floor < 4; floor++)
            for (int room = 0; room < 250; room++)
                yield return (floor, room);
    }

    [Fact]
    public void TheSameRoomAlwaysGetsTheSameRule()
    {
        foreach (var (floor, room) in SampleRooms().Take(200))
            Assert.Equal(RoomMutators.For(7, floor, room, EnemyKind.Husk), RoomMutators.For(7, floor, room, EnemyKind.Husk));
    }

    [Fact]
    public void BossesAndCreaturesAreNeverChanged()
    {
        var creatures = new[] { EnemyKind.Warden, EnemyKind.Brownie, EnemyKind.Selkie, EnemyKind.Kelpie, EnemyKind.BeanNighe, EnemyKind.Hare };
        foreach (var kind in creatures)
            foreach (var (floor, room) in SampleRooms())
                Assert.Equal(RoomMutator.None, RoomMutators.For(7, floor, room, kind));
    }

    [Fact]
    public void AboutOneRoomInThreeGetsARule()
    {
        int changed = SampleRooms().Count(r => RoomMutators.For(7, r.Floor, r.Room, EnemyKind.Husk) != RoomMutator.None);
        double share = changed / 1000.0;
        Assert.InRange(share, 0.25, 0.42);
    }

    [Fact]
    public void EveryRuleAppearsAndADifferentSeedGivesDifferentRooms()
    {
        var rules = SampleRooms().Select(r => RoomMutators.For(7, r.Floor, r.Room, EnemyKind.Spitter)).ToHashSet();
        Assert.Contains(RoomMutator.Frenzy, rules);
        Assert.Contains(RoomMutator.Hardened, rules);
        Assert.Contains(RoomMutator.Weakened, rules);
        Assert.Contains(RoomMutator.None, rules);

        var other = SampleRooms().Select(r => RoomMutators.For(8, r.Floor, r.Room, EnemyKind.Spitter)).ToList();
        Assert.NotEqual(SampleRooms().Select(r => RoomMutators.For(7, r.Floor, r.Room, EnemyKind.Spitter)).ToList(), other);
    }

    [Fact]
    public void NoRuleLeavesTheSpecAlone()
    {
        var spec = EnemySpec.Of(EnemyKind.Husk);
        Assert.Equal(spec, RoomMutators.Apply(spec, RoomMutator.None));
    }

    [Fact]
    public void FrenzyQuickensAndHitsHarder()
    {
        var spec = EnemySpec.Of(EnemyKind.Spitter);
        var fast = RoomMutators.Apply(spec, RoomMutator.Frenzy);
        Assert.Equal(spec.Speed * RoomMutators.FrenzySpeed, fast.Speed, 9);
        Assert.Equal(spec.ProjectileDamage * RoomMutators.FrenzyDamage, fast.ProjectileDamage, 9);
        Assert.Equal(spec.Health, fast.Health);
        Assert.Equal(spec.Kind, fast.Kind);
    }

    [Fact]
    public void HardenedAndWeakenedChangeOnlyHealth()
    {
        var spec = EnemySpec.Of(EnemyKind.Husk);
        Assert.Equal(spec.Health * RoomMutators.HardenedHealth, RoomMutators.Apply(spec, RoomMutator.Hardened).Health, 9);
        Assert.Equal(spec.Health * RoomMutators.WeakenedHealth, RoomMutators.Apply(spec, RoomMutator.Weakened).Health, 9);
        Assert.Equal(spec.Speed, RoomMutators.Apply(spec, RoomMutator.Hardened).Speed);
    }
}
