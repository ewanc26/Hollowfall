using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Input;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

/// <summary>Sever (docs/features.md 2.1): closing a portal on an enemy cuts it in half.</summary>
public class SeverRulesTests
{
    /// <summary>A stone wall at x = 10 with open air to its west, so a portal fired east lands on its west face.</summary>
    private static BlockType Wall(int x, int y, int z) => x >= 10 && y is >= 0 and < 8 ? BlockType.Stone : BlockType.Air;

    private static Portal EastWallPortal()
    {
        var system = new PortalSystem();
        var result = system.Place(PortalColor.Teal, new Vec3(2.5, 4.5, 0.5), new Vec3(1, 0, 0), new Vec3(1, 0, 0), Wall);
        Assert.True(result.Placed, result.Failure.ToString());
        return result.Portal!;
    }

    private static Box Body(double x, double z, double y = 3.0, double half = 0.3, double height = 1.8) =>
        Box.AroundFeet(new Vec3(x, y, z), half, height);

    [Fact]
    public void TheCutIsTheOneByTwoFacePlusPointThreeInFront()
    {
        var portal = EastWallPortal();
        var zone = SeverRules.Zone(portal);
        var c = portal.Frame.Center;
        Assert.Equal(c.X - 0.3, zone.Min.X, 6); // the normal points west, so "in front" is smaller x
        Assert.Equal(c.X, zone.Max.X, 6);
        Assert.Equal(1.0, zone.Max.Z - zone.Min.Z, 6);
        Assert.Equal(2.0, zone.Max.Y - zone.Min.Y, 6);
        Assert.Equal(SeverRules.ReachInFront, 0.3);
    }

    [Fact]
    public void AnEnemyInsideTheOpeningOverlapsIt()
    {
        var portal = EastWallPortal();
        var c = portal.Frame.Center;
        Assert.True(SeverRules.Overlaps(portal, Body(c.X - 0.2, c.Z, c.Y - 1)));
    }

    [Fact]
    public void AnEnemyJustReachedByTheFrontMarginOverlapsAndOneBeyondItDoesNot()
    {
        var portal = EastWallPortal();
        var c = portal.Frame.Center;
        // Its box edge at 0.25 from the face is inside the 0.3 margin; at 0.35 it is outside.
        Assert.True(SeverRules.Overlaps(portal, Body(c.X - 0.25 - 0.3, c.Z, c.Y - 1)));
        Assert.False(SeverRules.Overlaps(portal, Body(c.X - 0.35 - 0.3, c.Z, c.Y - 1)));
    }

    [Fact]
    public void EnemiesBesideAboveOrBelowTheOpeningAreNotCut()
    {
        var portal = EastWallPortal();
        var c = portal.Frame.Center;
        Assert.False(SeverRules.Overlaps(portal, Body(c.X - 0.2, c.Z + 1.0, c.Y - 1))); // box edge at 0.7 from centre, opening ends at 0.5
        Assert.False(SeverRules.Overlaps(portal, Body(c.X - 0.2, c.Z - 1.0, c.Y - 1)));
        Assert.False(SeverRules.Overlaps(portal, Body(c.X - 0.2, c.Z, c.Y + 1.01))); // feet above the top edge
        Assert.False(SeverRules.Overlaps(portal, Body(c.X - 0.2, c.Z, c.Y - 1 - 1.81))); // head below the bottom edge
    }

    [Fact]
    public void TouchingTheEdgeIsNotAnOverlap()
    {
        var portal = EastWallPortal();
        var c = portal.Frame.Center;
        Assert.False(SeverRules.Overlaps(portal, Body(c.X - 0.3 - 0.3, c.Z, c.Y - 1)));
    }

    [Fact]
    public void LightEnemiesAreKilledAndAwardTheBonus()
    {
        foreach (var kind in new[] { EnemyKind.Husk, EnemyKind.Spitter })
        {
            var spec = EnemySpec.Of(kind);
            Assert.False(SeverRules.IsHeavy(spec));
            var o = SeverRules.Decide(spec, spec.Health, alreadySevered: false);
            Assert.True(o.Killed);
            Assert.False(o.Staggered);
            Assert.True(o.AwardsStyle);
            Assert.Equal(spec.Health, o.Damage, 6);
        }
    }

    [Fact]
    public void WardensAreImmune()
    {
        var spec = EnemySpec.Of(EnemyKind.Warden);
        Assert.True(SeverRules.Immune(EnemyKind.Warden));
        var o = SeverRules.Decide(spec, spec.Health, alreadySevered: false);
        Assert.True(o.Immune);
        Assert.Equal(0, o.Damage);
        Assert.False(o.Killed);
        Assert.False(o.AwardsStyle);
    }

    [Fact]
    public void HeavyEnemiesTakeTheCutButAreStaggeredInsteadOfKilled()
    {
        var heavy = new EnemySpec(EnemyKind.Husk, 200, 3, 1.5, 20, 0.4, 1, 0, 0, 0, 0);
        Assert.True(SeverRules.IsHeavy(heavy));
        var o = SeverRules.Decide(heavy, 200, alreadySevered: false);
        Assert.False(o.Killed);
        Assert.True(o.Staggered);
        Assert.Equal(SeverRules.Damage, o.Damage);
        Assert.True(o.AwardsStyle);

        // Even a heavy enemy that is nearly dead is left standing, on one health.
        var weak = SeverRules.Decide(heavy, 40, alreadySevered: false);
        Assert.False(weak.Killed);
        Assert.Equal(39, weak.Damage, 6);
    }

    [Fact]
    public void TheBonusIsEarnedOncePerEnemy()
    {
        var heavy = new EnemySpec(EnemyKind.Husk, 200, 3, 1.5, 20, 0.4, 1, 0, 0, 0, 0);
        Assert.True(SeverRules.Decide(heavy, 200, alreadySevered: false).AwardsStyle);
        var again = SeverRules.Decide(heavy, 140, alreadySevered: true);
        Assert.False(again.AwardsStyle);
        Assert.True(again.Staggered); // it still takes the cut
    }

    [Fact]
    public void TheBonusIsWorth250Points()
    {
        Assert.Equal(250, StyleMeter.BaseValue(StyleBonus.Sever));
        Assert.Equal(60, SeverRules.Damage);
    }
}

public class SeverSimTests
{
    private const double Dt = 1.0 / 60;

    private sealed class Arena
    {
        public required VoxelWorld World { get; init; }
        public required CombatSim Sim { get; init; }
        public required double X { get; init; }
        public required double Y { get; init; }
        public required double Z { get; init; }

        public PlayerView View(double dx = 0, double dz = 0) =>
            new(new Vec3(X + dx, Y, Z + dz), new Vec3(X + dx, Y + 1.62, Z + dz), new Vec3(1, 0, 0), false, false);
    }

    private static Arena MakeArena()
    {
        for (long s = 1; s < 21; s++)
        {
            var world = new VoxelWorld(s);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                {
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is not { } site) continue;
                    foreach (var room in site.Floors[0].Rooms.Where(r => r.Shape == RoomShape.Rect && r.Bounds.W >= 16 && r.Bounds.H >= 14))
                    {
                        int z = room.CenterZ + 2;
                        for (int x = room.Bounds.X + 1; x + 11 < room.Bounds.Right - 1; x++)
                        {
                            bool clear = Enumerable.Range(0, 12).All(i => Enumerable.Range(-1, 3).All(dz => site.Floors[0].CellAt(x + i, z + dz) == CellKind.Floor));
                            if (!clear) continue;
                            return new Arena
                            {
                                World = world,
                                Sim = new CombatSim(world, site),
                                X = site.OriginX + x + 0.5,
                                Y = DungeonSite.FloorY(0) + 1,
                                Z = site.OriginZ + z + 0.5,
                            };
                        }
                    }
                }
        }
        throw new InvalidOperationException("no suitable arena found");
    }

    private static readonly Vec3 East = new(1, 0, 0);
    private static readonly Vec3 West = new(-1, 0, 0);

    private static void Tick(Arena a, PlayerActions actions, Vec3? facing = null) =>
        a.Sim.Tick(Dt, a.View() with { Forward = facing ?? East }, actions);

    private static void LinkPair(Arena a)
    {
        Tick(a, new PlayerActions(false, false, false, PortalTeal: true), East);
        Tick(a, new PlayerActions(false, false, false, PortalViolet: true), West);
        Assert.True(a.Sim.Portals.Linked);
        a.Sim.DrainEvents();
    }

    /// <summary>Stands an enemy of a kind in front of a portal, <paramref name="outFront"/> blocks out from its face and <paramref name="across"/> along it.</summary>
    private static Enemy StandAt(Arena a, Portal p, EnemyKind kind, double outFront, double across = 0)
    {
        var at = p.Frame.Center + p.Frame.Normal * outFront + p.Frame.Right * across;
        return a.Sim.SpawnEnemy(kind, new Vec3(at.X, a.Y, at.Z));
    }

    private static (Portal Rift, Portal Partner) RiftPairOn(Arena a)
    {
        var rift = PortalSystem.Fixed(10, (int)a.Y - 1, (int)a.Z, (1, 0, 0));
        var partner = PortalSystem.Fixed(30, (int)a.Y - 1, (int)a.Z, (-1, 0, 0));
        a.Sim.Portals.SetFixed([(rift, partner)]);
        return (rift, partner);
    }

    [Fact]
    public void ClosingPortalsCutsWhoeverStandsInARiftOpeningAndKeepsIt()
    {
        var a = MakeArena();
        LinkPair(a);
        var (rift, partner) = RiftPairOn(a);
        var c = rift.Frame.Center;
        var husk = a.Sim.SpawnEnemy(EnemyKind.Husk, new Vec3(c.X + 0.2, c.Y - 1, c.Z));
        a.Sim.DrainEvents();

        Assert.True(a.Sim.ClosePortals());
        var events = a.Sim.DrainEvents();

        var cut = Assert.Single(events.OfType<EnemySevered>());
        Assert.Equal(husk.Id, cut.Id);
        Assert.Equal(PortalColor.Rift, cut.Portal);
        Assert.True(husk.Dead);
        Assert.Same(partner, a.Sim.Portals.Other(rift));
        Assert.Equal(2, a.Sim.Portals.Rifts.Count());
    }

    [Fact]
    public void CloseWithNoPlacedPortalDoesNotCutRiftOpenings()
    {
        var a = MakeArena();
        var (rift, _) = RiftPairOn(a);
        var c = rift.Frame.Center;
        var husk = a.Sim.SpawnEnemy(EnemyKind.Husk, new Vec3(c.X + 0.2, c.Y - 1, c.Z));
        a.Sim.DrainEvents();

        Assert.False(a.Sim.ClosePortals());
        Assert.Empty(a.Sim.DrainEvents().OfType<EnemySevered>());
        Assert.Equal(husk.Spec.Health, husk.Health);
    }

    private static void ReplaceTeal(Arena a) => Tick(a, new PlayerActions(false, false, false, PortalTeal: true), East);

    [Fact]
    public void ReplacingAPortalKillsAHuskStandingInItsOpening()
    {
        var a = MakeArena();
        LinkPair(a);
        var husk = StandAt(a, a.Sim.Portals.Teal!, EnemyKind.Husk, 0.2);
        a.Sim.DrainEvents();
        double before = a.Sim.Style.TotalScore;

        ReplaceTeal(a);
        var events = a.Sim.DrainEvents();

        var cut = Assert.Single(events.OfType<EnemySevered>());
        Assert.Equal(husk.Id, cut.Id);
        Assert.True(cut.Killed);
        Assert.True(husk.Dead);
        Assert.Contains(events, e => e is EnemyKilled k && k.Id == husk.Id);
        Assert.Contains(events, e => e is PortalClosed { Color: PortalColor.Teal });
        Assert.Equal(250, a.Sim.Style.TotalScore - before, 6);
        Assert.Equal(StyleBonus.Sever, a.Sim.Style.RecentAwards[^1].Bonus);
        Assert.Equal(1, a.Sim.KillsOn(0));
    }

    [Fact]
    public void AnEnemyOutsideTheOpeningIsUnharmed()
    {
        var a = MakeArena();
        LinkPair(a);
        var far = StandAt(a, a.Sim.Portals.Teal!, EnemyKind.Husk, 2.5);
        var beside = StandAt(a, a.Sim.Portals.Teal!, EnemyKind.Spitter, 0.2, across: 2.0);
        a.Sim.DrainEvents();

        ReplaceTeal(a);
        var events = a.Sim.DrainEvents();

        Assert.Empty(events.OfType<EnemySevered>());
        Assert.Equal(far.Spec.Health, far.Health);
        Assert.Equal(beside.Spec.Health, beside.Health);
        Assert.Contains(events, e => e is PortalClosed); // the portal still closed, it just caught nobody
    }

    [Fact]
    public void WardensAreImmuneToSeverInTheSim()
    {
        var a = MakeArena();
        LinkPair(a);
        var warden = StandAt(a, a.Sim.Portals.Teal!, EnemyKind.Warden, 0.2);
        a.Sim.DrainEvents();
        double before = a.Sim.Style.TotalScore;

        ReplaceTeal(a);
        var events = a.Sim.DrainEvents();

        Assert.Empty(events.OfType<EnemySevered>());
        Assert.Equal(warden.Spec.Health, warden.Health);
        Assert.False(warden.Dead);
        Assert.Equal(before, a.Sim.Style.TotalScore);
    }

    [Fact]
    public void OnlyReplacingOrRemovingCutsNotTheFirstPlacement()
    {
        var a = MakeArena();
        // There is no teal portal yet. Fire one at the east wall with an enemy standing right where it will land.
        Tick(a, default, East);
        var wallDistance = RayCast.FirstSolid(a.World.GetBlock, a.View().Eye, East, 80)!.Value;
        var husk = a.Sim.SpawnEnemy(EnemyKind.Husk, new Vec3(a.X + wallDistance - 0.4, a.Y, a.Z));
        a.Sim.DrainEvents();

        Tick(a, new PlayerActions(false, false, false, PortalTeal: true), East);
        var events = a.Sim.DrainEvents();

        Assert.True(a.Sim.Portals.Teal != null);
        Assert.Empty(events.OfType<EnemySevered>());
        Assert.Empty(events.OfType<PortalClosed>());
        Assert.Equal(husk.Spec.Health, husk.Health);
    }

    [Fact]
    public void AFailedPlacementDoesNotCloseTheExistingPortal()
    {
        var a = MakeArena();
        LinkPair(a);
        var husk = StandAt(a, a.Sim.Portals.Teal!, EnemyKind.Husk, 0.2);
        var teal = a.Sim.Portals.Teal;
        a.Sim.DrainEvents();

        // Firing teal at the very spot violet already holds is refused (the two would overlap).
        Tick(a, new PlayerActions(false, false, false, PortalTeal: true), West);
        var events = a.Sim.DrainEvents();

        var placed = Assert.Single(events.OfType<PortalPlaced>());
        Assert.False(placed.Success);
        Assert.Equal(PlaceFailure.Overlaps, placed.Failure);
        Assert.Same(teal, a.Sim.Portals.Teal);
        Assert.Empty(events.OfType<EnemySevered>());
        Assert.Empty(events.OfType<PortalClosed>());
        Assert.False(husk.Dead);
    }

    [Fact]
    public void RemovingAPortalOnPurposeCutsAndClearsIt()
    {
        var a = MakeArena();
        LinkPair(a);
        var husk = StandAt(a, a.Sim.Portals.Violet!, EnemyKind.Husk, 0.2);
        a.Sim.DrainEvents();

        Assert.True(a.Sim.ClosePortal(PortalColor.Violet));
        var events = a.Sim.DrainEvents();

        Assert.Null(a.Sim.Portals.Violet);
        Assert.NotNull(a.Sim.Portals.Teal);
        Assert.True(husk.Dead);
        Assert.Equal(PortalColor.Violet, Assert.Single(events.OfType<EnemySevered>()).Portal);
        Assert.False(a.Sim.ClosePortal(PortalColor.Violet)); // nothing left to close
    }

    [Fact]
    public void TheClosePortalsActionClosesBothAndSeversEachSide()
    {
        var a = MakeArena();
        LinkPair(a);
        var one = StandAt(a, a.Sim.Portals.Teal!, EnemyKind.Husk, 0.2);
        var two = StandAt(a, a.Sim.Portals.Violet!, EnemyKind.Spitter, 0.2);
        a.Sim.DrainEvents();

        Tick(a, new PlayerActions(false, false, false, ClosePortals: true));
        var events = a.Sim.DrainEvents();

        Assert.False(a.Sim.Portals.Linked);
        Assert.Null(a.Sim.Portals.Teal);
        Assert.Null(a.Sim.Portals.Violet);
        Assert.True(one.Dead);
        Assert.True(two.Dead);
        Assert.Equal(2, events.OfType<EnemySevered>().Count());
        Assert.Equal(2, events.OfType<PortalClosed>().Count());
    }

    [Fact]
    public void EachEnemyAwardsTheBonusExactlyOnce()
    {
        var a = MakeArena();
        LinkPair(a);
        StandAt(a, a.Sim.Portals.Teal!, EnemyKind.Husk, 0.2, across: -0.1);
        StandAt(a, a.Sim.Portals.Teal!, EnemyKind.Spitter, 0.2, across: 0.1);
        a.Sim.DrainEvents();
        double before = a.Sim.Style.TotalScore;

        ReplaceTeal(a);
        var events = a.Sim.DrainEvents();
        Assert.Equal(2, events.OfType<EnemySevered>().Count());
        Assert.Equal(2, a.Sim.Style.RecentAwards.Count(x => x.Bonus == StyleBonus.Sever));
        Assert.Equal(500, a.Sim.Style.TotalScore - before, 6);

        // The dead are not cut again by the next closure.
        ReplaceTeal(a);
        Assert.Empty(a.Sim.DrainEvents().OfType<EnemySevered>());
        Assert.Equal(500, a.Sim.Style.TotalScore - before, 6);
    }

    [Fact]
    public void PlacingAPortalNeverHurtsThePlayer()
    {
        var a = MakeArena();
        LinkPair(a);
        var teal = a.Sim.Portals.Teal!;
        var inside = teal.Frame.Center + teal.Frame.Normal * 0.15;
        var view = new PlayerView(new Vec3(inside.X, a.Y, inside.Z), new Vec3(inside.X, a.Y + 1.62, inside.Z), East, false, false);
        double health = a.Sim.Vitals.Health;

        a.Sim.Tick(Dt, view, new PlayerActions(false, false, false, PortalTeal: true));
        a.Sim.Tick(Dt, view, new PlayerActions(false, false, false, ClosePortals: true));
        var events = a.Sim.DrainEvents();

        Assert.Equal(health, a.Sim.Vitals.Health);
        Assert.Empty(events.OfType<PlayerHurt>());
        Assert.False(a.Sim.Dead);
    }

    [Fact]
    public void SeveredEnemiesDropNoBlood()
    {
        var a = MakeArena();
        LinkPair(a);
        a.Sim.Vitals.Damage(50);
        double health = a.Sim.Vitals.Health;
        Assert.True(a.Sim.Vitals.Ceiling > health); // there is room to heal, so blood would show
        StandAt(a, a.Sim.Portals.Teal!, EnemyKind.Husk, 0.2);
        a.Sim.DrainEvents();

        ReplaceTeal(a);
        var events = a.Sim.DrainEvents();

        Assert.Single(events.OfType<EnemySevered>());
        Assert.Equal(health, a.Sim.Vitals.Health);
        Assert.Empty(events.OfType<EnemyHit>()); // no shot landed, so nothing heals
        Assert.Empty(events.OfType<Parried>());
    }

    [Fact]
    public void RespawningClearsPortalsWithoutCuttingAnyone()
    {
        var a = MakeArena();
        LinkPair(a);
        StandAt(a, a.Sim.Portals.Teal!, EnemyKind.Husk, 0.2);
        a.Sim.DrainEvents();

        a.Sim.Respawn();

        Assert.False(a.Sim.Portals.Linked);
        Assert.Empty(a.Sim.DrainEvents().OfType<EnemySevered>());
        Assert.Equal(0, a.Sim.KillsOn(0));
    }

    [Fact]
    public void ASeveredEnemyCanFinishAnEncounterByCountingAsAKill()
    {
        var a = MakeArena();
        LinkPair(a);
        var husk = StandAt(a, a.Sim.Portals.Teal!, EnemyKind.Husk, 0.2);
        ReplaceTeal(a);
        Assert.True(husk.Dead);
        a.Sim.Tick(Dt, a.View(), default);
        Assert.DoesNotContain(husk, a.Sim.Enemies); // swept away on the next tick like any kill
    }

    [Fact]
    public void SeverIsDeterministic()
    {
        static List<string> Play()
        {
            var a = MakeArena();
            LinkPair(a);
            StandAt(a, a.Sim.Portals.Teal!, EnemyKind.Husk, 0.2);
            ReplaceTeal(a);
            return a.Sim.DrainEvents().Select(e => e.ToString()!).ToList();
        }
        Assert.Equal(Play(), Play());
    }
}

public class SeverPlumbingTests
{
    [Fact]
    public void ClosePortalsIsARebindableActionWithBothDefaults()
    {
        var m = BindingMap.CreateDefault();
        Assert.False(string.IsNullOrWhiteSpace(ActionInfo.Label(GameAction.ClosePortals)));
        Assert.Equal(ActionGroup.Portals, ActionInfo.GroupOf(GameAction.ClosePortals));
        Assert.NotNull(m.Get(GameAction.ClosePortals, Slot.KeyboardMouse));
        Assert.NotNull(m.Get(GameAction.ClosePortals, Slot.Gamepad));
        Assert.NotEqual(BindingMap.PauseKey, m.Get(GameAction.ClosePortals, Slot.KeyboardMouse));
        Assert.False(ActionInfo.Required(GameAction.ClosePortals));
    }

    [Fact]
    public void SeverHasItsOwnSound()
    {
        var e = new EnemySevered(1, EnemyKind.Husk, PortalColor.Teal, 30, true, new Vec3(0, 0, 0), 250);
        Assert.Equal(SoundId.Sever, SoundMap.For(e));
        Assert.NotEmpty(Synth.Render(SoundId.Sever));
    }

    [Fact]
    public void TheReadmeListsTheControl()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "README.md"))) dir = Path.GetDirectoryName(dir)!;
        Assert.NotNull(dir);
        string readme = File.ReadAllText(Path.Combine(dir, "README.md"));
        Assert.Contains("Close portals", readme);
    }
}
