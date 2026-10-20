using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Physics;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class MeteorTests
{
    private const double Dt = 1.0 / 60;

    private sealed class Rig
    {
        public required CombatSim Sim { get; init; }
        public required Vec3 Feet { get; init; }

        public PlayerView View(Vec3 velocity, bool crossed = false, bool airborne = false, Vec3? feet = null)
        {
            var f = feet ?? Feet;
            return new PlayerView(f, f + new Vec3(0, 1.62, 0), new Vec3(1, 0, 0), airborne, false, velocity, crossed);
        }

        /// <summary>A husk <paramref name="dx"/> blocks east of the player, frozen in a long windup so it neither moves nor attacks.</summary>
        public Enemy Husk(double dx, double health = 1000, double dz = 0)
        {
            var e = Sim.SpawnEnemy(EnemyKind.Husk, new Vec3(Feet.X + dx, Feet.Y, Feet.Z + dz));
            e.SetHealth(health);
            e.ForceWindup(1000);
            return e;
        }

        public List<CombatEvent> Run(int ticks, PlayerView view)
        {
            var events = new List<CombatEvent>();
            for (int i = 0; i < ticks; i++)
            {
                Sim.Tick(Dt, view, default);
                events.AddRange(Sim.DrainEvents());
            }
            return events;
        }

        /// <summary>One tick that reports a portal crossing at <paramref name="speed"/> blocks per second, heading east.</summary>
        public List<CombatEvent> Cross(double speed, bool airborne = false) => Run(1, View(new Vec3(speed, 0, 0), crossed: true, airborne: airborne));
    }

    private static Rig MakeRig()
    {
        for (long s = 1; s < 40; s++)
        {
            var world = new VoxelWorld(s);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is { } site)
                        return new Rig
                        {
                            Sim = new CombatSim(world, site),
                            Feet = new Vec3(site.OriginX + 10.5, DungeonSite.FloorY(0) + 1, site.OriginZ + 10.5),
                        };
        }
        throw new InvalidOperationException("no dungeon found");
    }

    private static List<MeteorImpact> Impacts(IEnumerable<CombatEvent> events) => [.. events.OfType<MeteorImpact>()];

    // ---- thresholds -----------------------------------------------------------------------------

    [Fact]
    public void BelowTheSpeedThresholdNothingHappens()
    {
        var rig = MakeRig();
        var enemy = rig.Husk(1.0);
        var events = rig.Cross(MeteorTuning.MinSpeed - 0.01);
        Assert.Empty(Impacts(events));
        Assert.Equal(1000, enemy.Health);
    }

    [Fact]
    public void AtTheSpeedThresholdTheEnemyIsHit()
    {
        var rig = MakeRig();
        var enemy = rig.Husk(1.0);
        var events = rig.Cross(MeteorTuning.MinSpeed);
        var hit = Assert.Single(Impacts(events));
        Assert.Equal(enemy.Id, hit.Id);
        Assert.True(enemy.Health < 1000);
    }

    [Fact]
    public void OnlyAPortalLaunchArmsTheWindow()
    {
        var rig = MakeRig();
        var enemy = rig.Husk(1.0);
        var events = rig.Run(30, rig.View(new Vec3(40, 0, 0), airborne: true));
        Assert.Empty(Impacts(events));
        Assert.Equal(1000, enemy.Health);
    }

    [Fact]
    public void TheEnemyMustBeWithinReach()
    {
        var near = MakeRig();
        near.Husk(1.45);
        Assert.Single(Impacts(near.Cross(30)));

        var far = MakeRig();
        far.Husk(1.6);
        Assert.Empty(Impacts(far.Cross(30)));
    }

    // ---- the window -----------------------------------------------------------------------------

    [Fact]
    public void TheWindowStaysOpenForPointSixSecondsThenExpires()
    {
        var rig = MakeRig();
        rig.Cross(30); // out of the portal, nothing to hit yet
        rig.Run(30, rig.View(new Vec3(30, 0, 0))); // 0.5 s later...
        var late = rig.Husk(1.0);
        Assert.Single(Impacts(rig.Run(1, rig.View(new Vec3(30, 0, 0))))); // ...still open

        var rig2 = MakeRig();
        rig2.Cross(30);
        rig2.Run((int)Math.Ceiling((MeteorTuning.WindowSeconds + 0.05) / Dt), rig2.View(new Vec3(30, 0, 0)));
        var enemy = rig2.Husk(1.0);
        Assert.Empty(Impacts(rig2.Run(10, rig2.View(new Vec3(30, 0, 0)))));
        Assert.Equal(1000, enemy.Health);
        Assert.NotNull(late);
    }

    [Fact]
    public void HittingAWallEndsTheWindow()
    {
        var rig = MakeRig();
        rig.Cross(30);
        rig.Run(5, rig.View(new Vec3(3, 0, 0))); // a wall killed the speed
        var enemy = rig.Husk(1.0);
        Assert.Empty(Impacts(rig.Run(10, rig.View(new Vec3(30, 0, 0))))); // speed back, but the window is gone
        Assert.Equal(1000, enemy.Health);
    }

    // ---- damage, style, safety ------------------------------------------------------------------

    [Theory]
    [InlineData(18, 45)]
    [InlineData(20, 50)]
    [InlineData(30, 75)]
    [InlineData(48, 120)]
    [InlineData(55, 120)]
    [InlineData(500, 120)]
    public void DamageIsSpeedTimesTwoPointFiveCappedAtOneTwenty(double speed, double expected)
    {
        var rig = MakeRig();
        rig.Husk(1.0);
        var events = rig.Cross(speed);
        Assert.Equal(expected, Assert.Single(Impacts(events)).Damage, 6);
        Assert.Equal(expected, events.OfType<EnemyHit>().Single().Damage, 6);
        Assert.Equal(expected, MeteorRules.Damage(speed), 6);
    }

    [Fact]
    public void TheImpactCostsThirtyPercentOfSpeedAndTheHostIsToldHowMuchToKeep()
    {
        var rig = MakeRig();
        rig.Husk(1.0);
        Assert.Equal(0.7, Assert.Single(Impacts(rig.Cross(30))).SpeedKept, 6);
    }

    [Fact]
    public void TheImpactNeverHurtsThePlayer()
    {
        var rig = MakeRig();
        rig.Husk(1.0);
        var events = rig.Cross(60);
        Assert.Single(Impacts(events));
        Assert.DoesNotContain(events, e => e is PlayerHurt or PlayerDied);
        Assert.Equal(PlayerVitals.MaxHealth, rig.Sim.Vitals.Health);
        Assert.Equal(0, rig.Sim.Vitals.HardDamage);
    }

    [Fact]
    public void TheImpactDoesNotKnockTheStyleRankDown()
    {
        var rig = MakeRig();
        rig.Sim.Style.Award(StyleBonus.Chargeback);
        var before = rig.Sim.Style.Rank;
        rig.Husk(1.0);
        rig.Cross(30);
        Assert.True(rig.Sim.Style.Rank >= before);
    }

    [Fact]
    public void AMeteorAwardsOneHundredEightyStylePoints()
    {
        Assert.Equal(180, StyleMeter.BaseValue(StyleBonus.Meteor));
        var rig = MakeRig();
        rig.Husk(1.0);
        var hit = Assert.Single(Impacts(rig.Cross(30)));
        Assert.Equal(180, hit.Points, 6);
        Assert.False(hit.LongFall);
    }

    [Fact]
    public void AFallOfMoreThanTwentyBlocksDoublesTheStyle()
    {
        var rig = MakeRig();
        // Fall 25 blocks, one block per tick of position, then come out of a portal.
        for (int i = 0; i < 25; i++)
            rig.Run(1, rig.View(new Vec3(0, -30, 0), airborne: true, feet: rig.Feet + new Vec3(0, 40 - i, 0)));
        rig.Husk(1.0);
        var hit = Assert.Single(Impacts(rig.Cross(30, airborne: true)));
        Assert.True(hit.LongFall);
        Assert.Equal(180 * 2 * 3, hit.Points, 6); // airborne movement multiplier (x3) applies to every award
    }

    [Fact]
    public void AShortFallDoesNotDoubleTheStyle()
    {
        var rig = MakeRig();
        for (int i = 0; i < 15; i++)
            rig.Run(1, rig.View(new Vec3(0, -30, 0), airborne: true, feet: rig.Feet + new Vec3(0, 40 - i, 0)));
        rig.Husk(1.0);
        var hit = Assert.Single(Impacts(rig.Cross(30, airborne: true)));
        Assert.False(hit.LongFall);
        Assert.Equal(180 * 3, hit.Points, 6);
    }

    [Fact]
    public void AMeteorCanKillAndCountsAsAKill()
    {
        var rig = MakeRig();
        var enemy = rig.Husk(1.0, health: 20);
        var events = rig.Cross(30);
        Assert.True(enemy.Dead);
        Assert.Contains(events, e => e is EnemyKilled k && k.Id == enemy.Id);
        Assert.True(rig.Sim.Style.TotalScore > 180);
    }

    // ---- cooldown and lockout ------------------------------------------------------------------

    [Fact]
    public void OnlyOneEnemyIsHitPerCooldown()
    {
        var rig = MakeRig();
        var a = rig.Husk(1.0);
        var b = rig.Husk(1.0, dz: 0.5);
        var first = Impacts(rig.Cross(30));
        Assert.Single(first);

        // Still inside the cooldown: the second enemy is spared.
        var during = Impacts(rig.Run((int)(MeteorTuning.CooldownSeconds / Dt) - 3, rig.View(new Vec3(30, 0, 0))));
        Assert.Empty(during);

        // After it, the other enemy is hit (the window is still open: 0.6 s > 0.4 s).
        var after = Impacts(rig.Run(10, rig.View(new Vec3(30, 0, 0))));
        var second = Assert.Single(after);
        Assert.NotEqual(first[0].Id, second.Id);
        Assert.NotNull(a);
        Assert.NotNull(b);
    }

    [Fact]
    public void TheSameEnemyCannotBeHitAgainUntilItsLockoutEnds()
    {
        var rig = MakeRig();
        rig.Husk(1.0);
        Assert.Single(Impacts(rig.Cross(30)));

        // A fresh crossing one second later: window open, cooldown over, but the enemy is locked out.
        rig.Run(60, rig.View(new Vec3(0, 0, 0)));
        Assert.Empty(Impacts(rig.Cross(30)));

        // After the lockout it can be hit again.
        rig.Run((int)(MeteorTuning.PerEnemyLockoutSeconds / Dt), rig.View(new Vec3(0, 0, 0)));
        Assert.Single(Impacts(rig.Cross(30)));
    }

    // ---- portals cannot be farmed ---------------------------------------------------------------

    private static Func<int, int, int, BlockType> Room() => (x, y, z) =>
        y <= 9 || y >= 20 || x >= 10 || x <= -6 || z >= 20 || z <= -20 ? BlockType.Stone : BlockType.Air;

    [Fact]
    public void CyclingAFloorAndCeilingPortalPairNeverBuildsUpSpeed()
    {
        var world = Room();
        var portals = new PortalSystem();
        portals.Place(PortalColor.Teal, new Vec3(0.5, 18, 0.5), new Vec3(0, -1, 0), new Vec3(1, 0, 0), world);
        portals.Place(PortalColor.Violet, new Vec3(0.5, 12, 0.5), new Vec3(0, 1, 0), new Vec3(1, 0, 0), world);
        Assert.True(portals.Linked);

        var body = new PlayerBody(0.5, 17, 0.5);
        double firstHalfMax = 0, secondHalfMax = 0;
        int crossings = 0;
        const int Ticks = 60 * 20;
        for (int i = 0; i < Ticks; i++)
        {
            double entering = body.Speed;
            if (PortalWalker.Step(body, portals, Dt, 0, 0, false, world, new Vec3(0, -1, 0)) is not null) crossings++;
            if (i < Ticks / 2) firstHalfMax = Math.Max(firstHalfMax, Math.Max(entering, body.Speed));
            else secondHalfMax = Math.Max(secondHalfMax, Math.Max(entering, body.Speed));
        }
        Assert.True(crossings > 10, $"expected a loop, got {crossings} crossings");
        Assert.True(secondHalfMax <= firstHalfMax + 0.5, $"speed grew: {firstHalfMax} -> {secondHalfMax}");
        Assert.True(secondHalfMax <= PlayerBody.TerminalSpeed, $"speed {secondHalfMax} passed terminal");
    }

    [Fact]
    public void CyclingAPortalPairBesideOneEnemyHitsItOncePerLockoutAtMost()
    {
        var rig = MakeRig();
        var world = Room();
        var portals = new PortalSystem();
        portals.Place(PortalColor.Teal, new Vec3(0.5, 18, 0.5), new Vec3(0, -1, 0), new Vec3(1, 0, 0), world);
        portals.Place(PortalColor.Violet, new Vec3(0.5, 12, 0.5), new Vec3(0, 1, 0), new Vec3(1, 0, 0), world);

        // The enemy stands in the fall line, so the looping player brushes it on every pass.
        var enemy = rig.Sim.SpawnEnemy(EnemyKind.Warden, rig.Feet);
        enemy.SetHealth(1_000_000);
        enemy.ForceWindup(1000);
        var spot = new Vec3(0.5, 10, 0.5);
        var origin = rig.Feet;

        var body = new PlayerBody(0.5, 17, 0.5);
        var events = new List<CombatEvent>();
        const double Seconds = 20;
        int crossings = 0;
        for (int i = 0; i < Seconds / Dt; i++)
        {
            bool crossed = PortalWalker.Step(body, portals, Dt, 0, 0, false, world, new Vec3(0, -1, 0)) is not null;
            if (crossed) crossings++;
            // Re-map the body (which lives in the test room) into the dungeon around the enemy, so distances match.
            var feet = origin + (new Vec3(body.X, body.Y, body.Z) - spot);
            var view = new PlayerView(feet, feet + new Vec3(0, 1.62, 0), new Vec3(1, 0, 0), !body.OnGround, false, PortalWalker.Velocity(body), crossed);
            rig.Sim.Tick(Dt, view, default);
            events.AddRange(rig.Sim.DrainEvents());
        }

        var impacts = Impacts(events);
        Assert.True(crossings > 10, $"expected a loop, got {crossings} crossings");
        Assert.NotEmpty(impacts); // the loop does hit...
        Assert.All(impacts, i => Assert.True(i.Damage <= MeteorTuning.MaxDamage));
        double maxHits = Math.Floor(Seconds / MeteorTuning.PerEnemyLockoutSeconds) + 1;
        Assert.True(impacts.Count <= maxHits, $"{impacts.Count} hits in {Seconds}s, at most {maxHits} allowed"); // ...but not every pass
        Assert.True(impacts.Count < crossings);
    }

    // ---- wiring ---------------------------------------------------------------------------------

    [Fact]
    public void TheImpactHasItsOwnSound()
    {
        var e = new MeteorImpact(1, 30, 75, 180, false, 0.7);
        Assert.Equal(SoundId.Meteor, SoundMap.For(e));
        Assert.True(SoundMap.Gain(SoundId.Meteor) > 0.9);
    }

    [Fact]
    public void RespawnClearsTheWindow()
    {
        var rig = MakeRig();
        rig.Cross(30);
        rig.Sim.Respawn();
        var enemy = rig.Husk(1.0);
        Assert.Empty(Impacts(rig.Run(5, rig.View(new Vec3(30, 0, 0)))));
        Assert.Equal(1000, enemy.Health);
    }
}
