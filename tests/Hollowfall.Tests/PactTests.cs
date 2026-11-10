using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Portals;
using Hollowfall.Core.Replay;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

/// <summary>
/// Pacts (docs/features.md 4.1): offer drawing from the seed, the strength ladder by style rank, burdens that always apply,
/// persistence to later floors and across respawns, and the pact becoming part of the recorded run.
/// </summary>
public class PactTests
{
    private const double Dt = 1.0 / 60;

    // ---- offers -------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(987654321)]
    public void OfferIsDeterministicDistinctAndAtMostThree(long seed)
    {
        for (int floor = 0; floor < 6; floor++)
        {
            var first = Pacts.Offer(seed, floor, []);
            var again = Pacts.Offer(seed, floor, []);
            Assert.Equal(first, again);
            Assert.Equal(first.Distinct(), first);
            Assert.InRange(first.Count, 1, Pacts.OfferSize);
            Assert.Equal(Math.Min(Pacts.OfferSize, Enum.GetValues<PactKind>().Length), first.Count);
        }
    }

    [Fact]
    public void OfferFollowsTheSeedAndFloorAndNeverRepeatsATakenPact()
    {
        var a = CombatSimTests.MakeArena();
        Assert.Equal(Pacts.Offer(a.World.Seed, 0, []), a.Sim.PactOffer(0)); // the sim draws from its world's seed
        Assert.Equal(3, a.Sim.PactOffer(0).Count);

        var taken = new List<PactKind>();
        var rolls = new List<IReadOnlyList<PactKind>>();
        while (a.Sim.PactOffer(rolls.Count).Count > 0)
        {
            var offer = a.Sim.PactOffer(rolls.Count);
            Assert.DoesNotContain(offer, p => taken.Contains(p));
            a.Sim.TakePact(offer[0]);
            taken.Add(offer[0]);
            rolls.Add(offer);
            Assert.Equal(taken, a.Sim.PactsTaken);
        }

        // Every pact is taken exactly once (stacking limit: no pact twice in a run), and the offer is never empty while any remain.
        Assert.Equal(Enum.GetValues<PactKind>().OrderBy(p => (int)p), taken.OrderBy(p => (int)p));
        Assert.All(rolls, r => Assert.InRange(r.Count, 1, Pacts.OfferSize));
        Assert.Empty(a.Sim.PactOffer(rolls.Count));
    }

    [Fact]
    public void TakingAPactRaisesOneEventAndWholeFloorsOfOffersStayDeterministic()
    {
        var a = CombatSimTests.MakeArena();
        var offer = a.Sim.PactOffer(0);
        a.Sim.PactOffer(0); // asking twice changes nothing

        a.Sim.TakePact(offer[0]);
        var taken = a.Sim.DrainEvents().OfType<PactTaken>().ToList();
        Assert.Equal([offer[0]], a.Sim.PactsTaken);
        Assert.Single(taken);

        a.Sim.TakePact(offer[0]); // a second take is refused: no double stacking
        Assert.Empty(a.Sim.DrainEvents().OfType<PactTaken>());
        Assert.Single(a.Sim.PactsTaken);
        Assert.DoesNotContain(offer[0], a.Sim.PactOffer(1));
    }

    // ---- the strength ladder ------------------------------------------------------------

    [Fact]
    public void BoonStrengthIsHalfBelowSavageFullFromSavageAndDoubleAtLegendary()
    {
        Assert.Equal(0.5, Pacts.Strength(StyleRank.None));
        Assert.Equal(0.5, Pacts.Strength(StyleRank.Rowdy));
        Assert.Equal(0.5, Pacts.Strength(StyleRank.Fierce));
        Assert.Equal(0.5, Pacts.Strength(StyleRank.Brutal));
        Assert.Equal(1.0, Pacts.Strength(StyleRank.Savage));
        Assert.Equal(1.0, Pacts.Strength(StyleRank.Merciless));
        Assert.Equal(1.0, Pacts.Strength(StyleRank.Relentless));
        Assert.Equal(1.0, Pacts.Strength(StyleRank.Apocalyptic));
        Assert.Equal(2.0, Pacts.Strength(StyleRank.Legendary));
    }

    [Fact]
    public void HollowHeartsBloodMultiplierFollowsTheRankEveryStep()
    {
        var a = CombatSimTests.MakeArena();
        a.Sim.TakePact(PactKind.HollowHeart);
        Assert.Equal(1.0, a.Sim.Vitals.HitHealScale); // nothing until the sim steps
        Run(a.Sim, a.View(), 1);
        Assert.Equal(1.25, a.Sim.Vitals.HitHealScale, 6); // below Savage: half strength

        Rank(a.Sim, 1600); // Savage
        Run(a.Sim, a.View(), 1);
        Assert.Equal(1.5, a.Sim.Vitals.HitHealScale, 6); // full strength

        Rank(a.Sim, 5400); // Legendary
        Run(a.Sim, a.View(), 1);
        Assert.Equal(2.0, a.Sim.Vitals.HitHealScale, 6); // doubled
    }

    /// <summary>Awards Chargebacks until the meter sits where the rank is full strength; decay knocks a point or two off per tick, so this stays above the thresholds.</summary>
    private static void Rank(CombatSim sim, double points)
    {
        sim.Style.Reset();
        sim.Style.AwardScale = 1; // build the rank on raw chargebacks; the next step applies the pact's own scale again
        while (sim.Style.Points < points) sim.Style.Award(StyleBonus.Chargeback);
        Assert.Equal(points < 1800 ? StyleRank.Savage : StyleRank.Legendary, sim.Style.Rank);
    }

    [Fact]
    public void ShowmansStyleMultiplierFollowsTheRankAndItsBurdenAlwaysApplies()
    {
        var a = CombatSimTests.MakeArena();
        a.Sim.TakePact(PactKind.Showman);
        Assert.Equal(Pacts.ShowmanStaleness, a.Sim.Style.StalenessScale); // the burden is flat, not rank-scaled
        Run(a.Sim, a.View(), 1);
        Assert.Equal(1.125, a.Sim.Style.AwardScale, 6); // 25% at half strength

        // Worn 40% faster: twelve shots turn a pact-bound weapon Stale while a free one is still only Used.
        var plain = new StyleMeter();
        for (int i = 0; i < 12; i++) plain.NotifyWeaponUsed("gun");
        for (int i = 0; i < 12; i++) a.Sim.Style.NotifyWeaponUsed("gun");
        Assert.Equal(Freshness.Used, plain.FreshnessOf("gun"));
        Assert.Equal(Freshness.Stale, a.Sim.Style.FreshnessOf("gun"));

        Rank(a.Sim, 1600);
        Run(a.Sim, a.View(), 1);
        Assert.Equal(1.25, a.Sim.Style.AwardScale, 6);
        Rank(a.Sim, 5400);
        Run(a.Sim, a.View(), 1);
        Assert.Equal(1.5, a.Sim.Style.AwardScale, 6);
    }

    // ---- burdens ------------------------------------------------------------------------

    [Fact]
    public void BloodswornsBurdenRaisesHardDamageAndItsBoonHealsParriesMoreAndRefundsTheCooldown()
    {
        var a = CombatSimTests.MakeArena();
        a.Sim.Vitals.Damage(40);
        Assert.Equal(40 * PlayerVitals.HardDamageShare, a.Sim.Vitals.HardDamage, 6); // 35% without the pact

        a.Sim.TakePact(PactKind.Bloodsworn);
        Assert.Equal(Pacts.BloodswornHardDamageFraction, a.Sim.Vitals.HardDamageFraction);
        a.Sim.Vitals.Damage(40);
        Assert.Equal(40 * PlayerVitals.HardDamageShare + 40 * Pacts.BloodswornHardDamageFraction, a.Sim.Vitals.HardDamage, 6);

        // The boon: a parry heals the signature refill (to the ceiling) plus 20 at half strength (the rank is below Savage).
        var husk = Place(a, EnemyKind.Husk, 3);
        a.Sim.Vitals.Damage(10); // leave room to heal; the ceiling stays counting rows from the two burdens above
        bool parried = false;
        for (int i = 0; i < 60 * 4 && !parried; i++)
        {
            bool press = husk.Telegraphing;
            a.Sim.Tick(Dt, a.View(), new PlayerActions(false, false, press));
            parried = a.Sim.DrainEvents().OfType<Parried>().Any(p => p.Kind == ParryKind.Melee);
        }
        Assert.True(parried);
        Assert.Equal(Math.Min(a.Sim.Vitals.Ceiling + Pacts.BloodswornParryHeal * 0.5, a.Sim.Vitals.Max), a.Sim.Vitals.Health, 6);

        // The spent window's cooldown is refunded, so a second press works at once (without the pact it costs half a second).
        var second = Place(a, EnemyKind.Husk, 2);
        second.ForceWindup(1);
        var t = 0;
        while (!second.Telegraphing && t++ < 60 * 4) a.Sim.Tick(Dt, a.View(), new PlayerActions(false, false, true));
        a.Sim.Tick(Dt, a.View(), new PlayerActions(false, false, true));
        Assert.Contains(a.Sim.DrainEvents(), e => e is Parried);
    }

    [Fact]
    public void HollowHeartsBurdenLowersMaximumHealthAndClampsWhatIsAboveIt()
    {
        var a = CombatSimTests.MakeArena();
        Assert.Equal(100, a.Sim.Vitals.Max, 6);
        a.Sim.TakePact(PactKind.HollowHeart);
        Assert.Equal(100 - Pacts.HollowHeartHealthLoss, a.Sim.Vitals.Max, 6);
        Assert.Equal(a.Sim.Vitals.Max, a.Sim.Vitals.Health, 6);

        // Taken while hurt: the lowered maximum caps the hard damage below, but the current health does not change.
        var b = CombatSimTests.MakeArena();
        b.Sim.Vitals.Damage(45);
        b.Sim.Vitals.Cleanse();
        double hurt = b.Sim.Vitals.Health;
        b.Sim.TakePact(PactKind.HollowHeart);
        Assert.Equal(100 - Pacts.HollowHeartHealthLoss, b.Sim.Vitals.Max, 6);
        Assert.Equal(hurt, b.Sim.Vitals.Health, 6);

        // A limb's blood heals 1.5x at full strength: 20 becomes 30, and neither heal is short of room under the ceiling.
        b.Sim.Vitals.Damage(40);
        Rank(b.Sim, 1600);
        Run(b.Sim, b.View(), 1);
        Assert.True(b.Sim.Vitals.Ceiling - b.Sim.Vitals.Health > 20 * 1.5, "room to see the multiplier");
        Assert.Equal(20 * 1.5, b.Sim.Vitals.HealFromHit(HitZone.Limb, 2), 6);
        var plain = CombatSimTests.MakeArena();
        plain.Sim.Vitals.Damage(40);
        Assert.Equal(20, plain.Sim.Vitals.HealFromHit(HitZone.Limb, 2), 6); // without the pact
    }

    [Fact]
    public void GravediggersBurdenDecaysStyleFasterAndItsBoonScalesTheSlam()
    {
        var a = CombatSimTests.MakeArena();
        a.Sim.TakePact(PactKind.Gravedigger);
        Assert.Equal(Pacts.GravediggerStyleDecay, a.Sim.Style.DecayScale);

        Rank(a.Sim, 5400); // Legendary: radius 3.5 + 1.5 x 2 = 6.5
        var hit = Place(a, EnemyKind.Husk, 6.2);
        a.Sim.Slam(new Vec3(a.X, a.Y, a.Z), 0);
        Assert.True(a.Sim.DrainEvents().Any(e => e is EnemyHit h && h.Id == hit.Id), "the wider Legendary slam reaches 6.2 blocks");

        Rank(a.Sim, 1600); // Savage: radius 5.0
        var missed = Place(a, EnemyKind.Husk, 5.4);
        a.Sim.Slam(new Vec3(a.X, a.Y, a.Z), 0);
        Assert.DoesNotContain(a.Sim.DrainEvents(), e => e is EnemyHit h && h.Id == missed.Id);

        var caught = Place(a, EnemyKind.Husk, 4.8);
        a.Sim.Slam(new Vec3(a.X, a.Y, a.Z), 0);
        Assert.Contains(a.Sim.DrainEvents(), e => e is EnemyHit h && h.Id == caught.Id);

        var plain = CombatSimTests.MakeArena(); // without the pact even 3.8 is out of reach
        var far = Place(plain, EnemyKind.Husk, 3.8);
        plain.Sim.Slam(new Vec3(plain.X, plain.Y, plain.Z), 0);
        Assert.DoesNotContain(plain.Sim.DrainEvents(), e => e is EnemyHit h && h.Id == far.Id);
    }

    [Fact]
    public void GravediggerBreaksFloorsInOneSlam()
    {
        const int x = 500, y = 40, z = 500;
        var world = new VoxelWorld(7);
        world.SetBlock(x, y, z, BlockType.BrickFloor);
        var breach = new SlamBreach { BreakInOneHit = true };

        Assert.Equal(FloorSlam.Broken, breach.Land(world, x, y, z, 0)); // the first slam breaks it
        Assert.Equal(BlockType.Air, world.GetBlock(x, y, z));
    }

    [Fact]
    public void TakenPactsPersistToLaterFloorsAndThroughARespawn()
    {
        var a = CombatSimTests.MakeArena();
        var first = a.Sim.PactOffer(0)[0];
        var second = first == PactKind.HollowHeart ? PactKind.Gravedigger : PactKind.HollowHeart;
        a.Sim.TakePact(first);
        a.Sim.TakePact(second);

        // The later floor's offer keeps excluding what is taken, and the burdens stay applied.
        a.Sim.DrainEvents();
        Assert.DoesNotContain(first, a.Sim.PactOffer(1));
        Assert.DoesNotContain(second, a.Sim.PactOffer(1));
        Assert.Equal(100 - Pacts.HollowHeartHealthLoss, a.Sim.Vitals.Max, 6);

        a.Sim.Respawn();
        Assert.Equal([first, second], a.Sim.PactsTaken);
        Assert.Equal(100 - Pacts.HollowHeartHealthLoss, a.Sim.Vitals.Max, 6); // Reset restores health to the lowered maximum: the pact survives
        Assert.DoesNotContain(first, a.Sim.PactOffer(1));
        Assert.DoesNotContain(second, a.Sim.PactOffer(1));
    }

    [Fact]
    public void ABurdenNeverLowersMaximumHealthBelowTheFloor()
    {
        var v = new PlayerVitals(Pacts.MinMaxHealth);
        v.ReduceMax(Pacts.HollowHeartHealthLoss);
        Assert.Equal(Pacts.MinMaxHealth, v.Max, 6);
    }

    // ---- the run and the replay ----------------------------------------------------------

    [Fact]
    public void TwinGateGivesTheThirdSlotInPlayAndChargesPlacementsBelowBrutal()
    {
        var a = CombatSimTests.MakeArena();
        a.Sim.TakePact(PactKind.TwinGate);
        Assert.True(a.Sim.Portals.TwinGate);

        var east = a.View();
        var west = a.View() with { Forward = new Vec3(-1, 0, 0) };
        // Aimed down and ahead, so the opening lands on the floor well clear of either wall portal.
        var floor = a.View(dx: 4) with { Forward = new Vec3(4, -3, 0).Normalized() };

        a.Sim.Tick(Dt, east, new PlayerActions(false, false, false, PortalTeal: true));
        Assert.True(Assert.Single(a.Sim.DrainEvents().OfType<PortalPlaced>()).Success, "the east wall takes teal");
        Assert.Equal(100 - Pacts.TwinGatePlacementHealthCost, a.Sim.Vitals.Health, 6);

        a.Sim.Tick(Dt, west, new PlayerActions(false, false, false, PortalViolet: true));
        Assert.True(Assert.Single(a.Sim.DrainEvents().OfType<PortalPlaced>()).Success, "the west wall takes violet");
        Assert.Equal(100 - 2 * Pacts.TwinGatePlacementHealthCost, a.Sim.Vitals.Health, 6);
        Assert.Equal(2, a.Sim.Portals.Placed.Count);

        // The boon: the third placement takes the ring's third slot instead of replacing its colour.
        a.Sim.Tick(Dt, floor, new PlayerActions(false, false, false, PortalTeal: true));
        Assert.True(Assert.Single(a.Sim.DrainEvents().OfType<PortalPlaced>()).Success, "a third slot opens without closing the older teal");
        Assert.Equal(3, a.Sim.Portals.Placed.Count);
        Assert.Equal(100 - 3 * Pacts.TwinGatePlacementHealthCost, a.Sim.Vitals.Health, 6);

        // From Brutal up the dungeon stops charging: at Savage the same placement costs nothing.
        Rank(a.Sim, 1600);
        a.Sim.Tick(Dt, west, new PlayerActions(false, false, false, PortalViolet: true));
        a.Sim.DrainEvents();
        Assert.Equal(100 - 3 * Pacts.TwinGatePlacementHealthCost, a.Sim.Vitals.Health, 6);
    }

    [Fact]
    public void TwinGateRotatesTheOldestSlotOutThroughAClose()
    {
        var a = CombatSimTests.MakeArena();
        a.Sim.TakePact(PactKind.TwinGate);
        a.Sim.DrainEvents();

        var east = a.View();
        var west = a.View() with { Forward = new Vec3(-1, 0, 0) };
        var floor = a.View(dx: 4) with { Forward = new Vec3(4, -3, 0).Normalized() };

        var aims = new[] { (east, true), (west, false), (floor, true), (floor, false) };
        for (int i = 0; i < 4; i++)
        {
            a.Sim.Tick(Dt, aims[i].Item1, new PlayerActions(false, false, false, PortalTeal: aims[i].Item2, PortalViolet: !aims[i].Item2));
            var events = a.Sim.DrainEvents();
            Assert.True(events.OfType<PortalPlaced>().Last().Success, $"placement {i} succeeded");
            if (i == 3)
            {
                Assert.Contains(events, e => e is PortalClosed c && c.Color == PortalColor.Teal); // the pushed-off oldest is closed
                break;
            }
            Assert.Empty(events.OfType<PortalClosed>());
        }
        Assert.Equal(PortalSystem.TwinGateSlots, a.Sim.Portals.Placed.Count);
    }


    [Fact]
    public void PactPicksRoundTripThroughTheFileFormat()
    {
        var frames = new[] { ReplayFrame.From(new PlayerView(new Vec3(1, 2, 3), new Vec3(1, 3.62, 3), new Vec3(1, 0, 0), true, false), default), ReplayFrame.From(new PlayerView(new Vec3(1, 2, 3), new Vec3(1, 3.62, 3), new Vec3(0, 1, 0), false, true), default) };
        var picks = new PactKind?[] { null, PactKind.Bloodsworn, PactKind.Showman, null, PactKind.HollowHeart, null, PactKind.Gravedigger };
        var replay = new RunReplay("pacts", 1, 2, 3, 1, new FloorRecord(OverallGrade.A, 100, 12.5), [.. frames]) { PactPicks = picks };

        Assert.True(ReplayCodec.TryDecode(ReplayCodec.Encode(replay), out var decoded));
        Assert.Equal(picks, decoded!.PactPicks);
        Assert.Equal(RunReplay.SimVersion, decoded.SimVersionRecorded);
    }

    [Fact]
    public void APactTakenBetweenFloorsIsRecordedAndReplayed()
    {
        var (world, site) = ThreeFloors();
        var sim = new CombatSim(world, site);
        var rec = new ReplayRecorder();
        var at0 = At(site, 0);
        var at1 = At(site, 1);
        var at2 = At(site, 2);
        var picks = new List<PactKind?>();
        RunReplay? replay = null;
        int ticks = 0;

        for (int t = 0; t < 720 && replay == null; t++)
        {
            ticks = t + 1;
            var feet = t < 180 ? at0 : t < 400 ? at1 : at2;
            var eye = feet + new Vec3(0, 1.62, 0);
            var view = new PlayerView(feet, eye, new Vec3(0, 0, 1), false, false);
            (view, _) = rec.Capture(view, default);
            sim.Tick(Dt, view, default);
            foreach (var e in sim.DrainEvents())
            {
                if (e is not FloorCompleted c) continue;
                var pact = sim.PactOffer(c.Floor).FirstOrDefault();
                if (pact is { } kind)
                {
                    sim.TakePact(kind); // the panel's take, applied between steps
                    rec.NotePact(c.Floor, kind);
                    picks.Add(kind);
                }
                else
                {
                    rec.NotePact(c.Floor, null);
                    picks.Add(null);
                }
                if (c.Floor == 1) replay = rec.Finish("pact-run", site.RegionX, site.RegionZ, c.Floor, new FloorRecord(c.Result.Overall, c.Result.Points, c.Seconds), sim.Stats);
            }
        }

        Assert.NotNull(replay);
        Assert.Equal(1, replay!.Floor);
        Assert.Equal(picks, replay.PactPicks);

        // A fresh sim that plays the recorded run must finish in exactly the same state: mid-run pacts included.
        var (world2, site2) = ThreeFloors();
        var replayed = new CombatSim(world2, site2);
        var outcome = ReplayRunner.Play(replay, replayed, site2);
        Assert.Equal(ReplayRunner.StateHash(sim, site.FloorCount, ticks), outcome.Hash);
        Assert.Equal(picks.OfType<PactKind>(), replayed.PactsTaken);
        Assert.Equal(1.0, replayed.Vitals.Max / sim.Vitals.Max, 6); // the burden bit in the replay too
        Assert.True(ReplayCodec.TryDecode(ReplayCodec.Encode(replay), out var decoded));
        Assert.Equal(picks, decoded!.PactPicks);
    }

    private static (VoxelWorld World, DungeonSite Site) ThreeFloors()
    {
        foreach (var s in new long[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            var world = new VoxelWorld(s);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is { FloorCount: >= 3 } site)
                        return (world, site);
        }
        throw new InvalidOperationException("no three-floor dungeon found");
    }

    private static Vec3 At(DungeonSite site, int floor)
    {
        var (x, y, z) = site.ToWorld(floor, site.Floors[floor].EntranceX, site.Floors[floor].EntranceZ);
        return new Vec3(x + 0.5, y, z + 0.5);
    }

    private static Enemy Place(CombatSimTests.Arena a, EnemyKind kind, double distance, double dz = 0) =>
        a.Sim.SpawnEnemy(kind, new Vec3(a.X + distance, a.Y, a.Z + dz));

    private static void Run(CombatSim sim, PlayerView view, int ticks)
    {
        for (int i = 0; i < ticks; i++) sim.Tick(Dt, view, default);
    }
}
