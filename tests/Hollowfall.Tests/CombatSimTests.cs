using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class RayCastTests
{
    private static BlockType Wall(int x, int y, int z) => x == 10 && y is >= 0 and < 5 ? BlockType.Stone : BlockType.Air;

    [Fact]
    public void FindsTheFirstSolidBlockAlongTheRay()
    {
        var d = RayCast.FirstSolid(Wall, new Vec3(2.5, 2.5, 0.5), new Vec3(1, 0, 0), 50);
        Assert.Equal(7.5, d!.Value, 6);
    }

    [Fact]
    public void RespectsTheMaximumDistanceAndDirection()
    {
        Assert.Null(RayCast.FirstSolid(Wall, new Vec3(2.5, 2.5, 0.5), new Vec3(1, 0, 0), 5));
        Assert.Null(RayCast.FirstSolid(Wall, new Vec3(2.5, 2.5, 0.5), new Vec3(-1, 0, 0), 50));
        Assert.Null(RayCast.FirstSolid(Wall, new Vec3(2.5, 8.5, 0.5), new Vec3(1, 0, 0), 50)); // passes over the wall
    }

    [Fact]
    public void DiagonalAndSteepRaysHitWhereTheyShould()
    {
        var d = RayCast.FirstSolid(Wall, new Vec3(0.5, 4.5, 0.5), new Vec3(1, -0.2, 0.1), 50);
        Assert.NotNull(d);
        var hit = new Vec3(0.5, 4.5, 0.5) + new Vec3(1, -0.2, 0.1).Normalized() * d!.Value;
        Assert.Equal(10.0, hit.X, 3);
    }

    [Fact]
    public void StartingInsideASolidBlockHitsAtOnce()
    {
        Assert.Equal(0, RayCast.FirstSolid(Wall, new Vec3(10.5, 1, 0), new Vec3(0, 0, 1), 10));
    }

    [Fact]
    public void BoxIntersectionUsesTheNearFace()
    {
        var box = new Box(new Vec3(5, 0, -1), new Vec3(6, 2, 1));
        Assert.Equal(4.0, RayCast.HitBox(new Vec3(1, 1, 0), new Vec3(1, 0, 0), box, 20)!.Value, 6);
        Assert.Null(RayCast.HitBox(new Vec3(1, 1, 0), new Vec3(1, 0, 0), box, 3));
        Assert.Null(RayCast.HitBox(new Vec3(1, 5, 0), new Vec3(1, 0, 0), box, 20));
        Assert.Null(RayCast.HitBox(new Vec3(1, 1, 0), new Vec3(-1, 0, 0), box, 20));
        Assert.Equal(0.0, RayCast.HitBox(new Vec3(5.5, 1, 0), new Vec3(1, 0, 0), box, 20)!.Value, 6);
    }

    [Fact]
    public void LineOfSightIsBlockedByWalls()
    {
        Assert.False(RayCast.HasLineOfSight(Wall, new Vec3(2, 2, 0.5), new Vec3(15, 2, 0.5)));
        Assert.True(RayCast.HasLineOfSight(Wall, new Vec3(2, 2, 0.5), new Vec3(8, 2, 0.5)));
        Assert.True(RayCast.HasLineOfSight(Wall, new Vec3(2, 7, 0.5), new Vec3(15, 7, 0.5)));
    }
}

public class CombatSimTests
{
    private const double Dt = 1.0 / 60;

    private sealed class Arena
    {
        public required VoxelWorld World { get; init; }
        public required DungeonSite Site { get; init; }
        public required CombatSim Sim { get; init; }
        public required double X { get; init; }
        public required double Y { get; init; }
        public required double Z { get; init; }

        /// <summary>The player stands here, looking toward +X.</summary>
        public PlayerView View(double dx = 0, double dz = 0, bool airborne = false, double eyeHeight = 1.62) =>
            new(new Vec3(X + dx, Y, Z + dz), new Vec3(X + dx, Y + eyeHeight, Z + dz), new Vec3(1, 0, 0), airborne, false);
    }

    /// <summary>A real dungeon room with a big open floor: the player at the west end, empty space to the east.</summary>
    private static Arena MakeArena(long seed = 1)
    {
        for (long s = seed; s < seed + 20; s++)
        {
            var world = new VoxelWorld(s);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                {
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is not { } site) continue;
                    foreach (var room in site.Floors[0].Rooms.Where(r => r.Shape == RoomShape.Rect && r.Bounds.W >= 16 && r.Bounds.H >= 14))
                    {
                        // 11 clear cells along +X from a start, at floor level, with no pillar in the way.
                        int z = room.CenterZ + 2;
                        for (int x = room.Bounds.X + 1; x + 11 < room.Bounds.Right - 1; x++)
                        {
                            bool clear = Enumerable.Range(0, 12).All(i => Enumerable.Range(-1, 3).All(dz => site.Floors[0].CellAt(x + i, z + dz) == CellKind.Floor));
                            if (!clear) continue;
                            return new Arena
                            {
                                World = world,
                                Site = site,
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

    private static void Run(CombatSim sim, PlayerView view, int ticks, PlayerActions? actions = null)
    {
        for (int i = 0; i < ticks; i++) sim.Tick(Dt, view, actions ?? default);
    }

    private static T? Last<T>(IEnumerable<CombatEvent> events) where T : CombatEvent => events.OfType<T>().LastOrDefault();

    /// <summary>An enemy placed <paramref name="distance"/> blocks east of the player.</summary>
    private static Enemy Place(Arena a, EnemyKind kind, double distance, double dz = 0) =>
        a.Sim.SpawnEnemy(kind, new Vec3(a.X + distance, a.Y, a.Z + dz));

    private static PlayerView Aim(PlayerView v, Vec3 at)
    {
        var f = (at - v.Eye).Normalized();
        return v with { Forward = f };
    }

    // ---- shooting ---------------------------------------------------------------------------

    [Fact]
    public void RevolverHitsTheBodyHeadAndLegsForDifferentDamage()
    {
        var a = MakeArena();
        var e = Place(a, EnemyKind.Warden, 8);
        var view = a.View();

        double Shot(double aimHeight)
        {
            Run(a.Sim, Aim(view, new Vec3(a.X + 8, a.Y + aimHeight, a.Z)), 1, new PlayerActions(true, false, false));
            Run(a.Sim, view, 30); // wait out the cooldown
            return Last<EnemyHit>(a.Sim.DrainEvents())!.Damage;
        }

        // The warden is 2.3 tall: head above 1.725, legs below 0.7.
        double torso = Shot(1.2), head = Shot(2.1), legs = Shot(0.4);
        Assert.Equal(12, torso, 6);
        Assert.Equal(24, head, 6);
        Assert.Equal(9.6, legs, 6);
        Assert.True(e.Health < e.Spec.Health);
    }

    [Fact]
    public void RevolverHasACooldown()
    {
        var a = MakeArena();
        Place(a, EnemyKind.Warden, 8);
        var view = Aim(a.View(), new Vec3(a.X + 8, a.Y + 1, a.Z));
        Run(a.Sim, view, 5, new PlayerActions(true, false, false)); // held for 5 ticks, 83 ms
        Assert.Single(a.Sim.DrainEvents().OfType<ShotFired>());
        Run(a.Sim, view, 20);
        Run(a.Sim, view, 1, new PlayerActions(true, false, false));
        Assert.Single(a.Sim.DrainEvents().OfType<ShotFired>());
    }

    [Fact]
    public void ShotsStopAtWallsAndMissEmptySpace()
    {
        var a = MakeArena();
        Place(a, EnemyKind.Husk, 6);
        // Aim straight up into the ceiling: no hit, but the shot still happens and ends at the ceiling.
        Run(a.Sim, a.View() with { Forward = new Vec3(0, 1, 0) }, 1, new PlayerActions(true, false, false));
        var ev = a.Sim.DrainEvents();
        Assert.Empty(ev.OfType<EnemyHit>());
        var shot = Assert.Single(ev.OfType<ShotFired>());
        Assert.InRange(shot.End.Y - shot.Origin.Y, 1, 6);
    }

    [Fact]
    public void WallsHideEnemiesFromBullets()
    {
        var a = MakeArena();
        var e = Place(a, EnemyKind.Warden, 8);
        // Put a wall between the player and the enemy.
        for (int y = 0; y < 4; y++)
            for (int dz = -2; dz <= 2; dz++)
                a.World.SetBlock((int)Math.Floor(a.X) + 4, (int)a.Y + y, (int)Math.Floor(a.Z) + dz, BlockType.Stone);
        Run(a.Sim, Aim(a.View(), e.Feet + new Vec3(0, 1, 0)), 1, new PlayerActions(true, false, false));
        Assert.Empty(a.Sim.DrainEvents().OfType<EnemyHit>());
    }

    [Fact]
    public void KillingEarnsStyleAndHeadshotsEarnMore()
    {
        var a = MakeArena();
        var e = Place(a, EnemyKind.Spitter, 8); // 20 hp: one headshot (24); it is 1.5 tall, so its head starts at 1.125
        Run(a.Sim, Aim(a.View(), new Vec3(a.X + 8, a.Y + 1.35, a.Z)), 1, new PlayerActions(true, false, false));
        var ev = a.Sim.DrainEvents();
        Assert.True(Last<EnemyHit>(ev)!.Killed);
        Assert.NotNull(Last<EnemyKilled>(ev));
        Assert.True(e.Dead);
        Assert.True(a.Sim.Style.TotalScore >= StyleMeter.BaseValue(StyleBonus.Headshot) * 1.5 + StyleMeter.BaseValue(StyleBonus.Kill) * 1.5 - 0.001);
        Assert.DoesNotContain(e, a.Sim.Enemies);
    }

    [Fact]
    public void ShotgunPelletsSpreadAndHealUpCloseButNotFarAway()
    {
        double HealOver(double distance)
        {
            var a = MakeArena();
            a.Sim.Vitals.Damage(60);
            a.Sim.Vitals.Tick(20); // let hard damage clear
            double before = a.Sim.Vitals.Health;
            var e = Place(a, EnemyKind.Warden, distance);
            Run(a.Sim, Aim(a.View(), e.Feet + new Vec3(0, 1, 0)), 1, new PlayerActions(false, true, false));
            return a.Sim.Vitals.Health - before;
        }
        Assert.True(HealOver(2.5) > 6, "pellets at close range heal");
        Assert.Equal(0, HealOver(9));
    }

    [Fact]
    public void BloodHealingNeedsClosenessForTheRevolverToo()
    {
        var a = MakeArena();
        Place(a, EnemyKind.Warden, 3);
        a.Sim.Vitals.Damage(50);
        a.Sim.Vitals.Tick(20);
        double h = a.Sim.Vitals.Health;
        Run(a.Sim, Aim(a.View(), new Vec3(a.X + 3, a.Y + 2.1, a.Z)), 1, new PlayerActions(true, false, false));
        Assert.Equal(h + 30, a.Sim.Vitals.Health, 3); // head hit up close (the warden is 2.3 tall)
    }

    // ---- enemies ----------------------------------------------------------------------------

    [Fact]
    public void HusksCloseInTelegraphThenHit()
    {
        var a = MakeArena();
        var husk = Place(a, EnemyKind.Husk, 6);
        var view = a.View();
        Run(a.Sim, view, 60 * 4);
        var ev = a.Sim.DrainEvents();
        Assert.Contains(ev, e => e is EnemyTelegraph t && t.Id == husk.Id);
        Assert.Contains(ev, e => e is PlayerHurt { Damage: 20 });
        Assert.True(a.Sim.Vitals.Health < 100);
    }

    [Fact]
    public void ParryingAHuskWindupHealsStunsDamagesAndEarnsStyle()
    {
        var a = MakeArena();
        var husk = Place(a, EnemyKind.Husk, 3);
        var view = a.View();
        a.Sim.Vitals.Damage(30);

        bool parried = false;
        for (int i = 0; i < 60 * 3 && !parried; i++)
        {
            bool press = husk.Telegraphing;
            a.Sim.Tick(Dt, view, new PlayerActions(false, false, press));
            parried = a.Sim.DrainEvents().Any(e => e is Parried { Kind: ParryKind.Melee });
        }
        Assert.True(parried);
        Assert.Equal(EnemyState.Stunned, husk.State);
        Assert.Equal(a.Sim.Vitals.Ceiling, a.Sim.Vitals.Health, 3);
        Assert.True(husk.Health < husk.Spec.Health);
        Assert.True(a.Sim.Style.TotalScore >= StyleMeter.BaseValue(StyleBonus.Parry));
        Assert.True(a.Sim.Vitals.Invulnerable);
    }

    [Fact]
    public void ParryingTooEarlyWhiffsAndCostsACooldown()
    {
        var a = MakeArena();
        Place(a, EnemyKind.Husk, 20); // far away, not telegraphing
        var view = a.View();
        Run(a.Sim, view, 1, new PlayerActions(false, false, true));
        Run(a.Sim, view, 20); // window passes
        Assert.DoesNotContain(a.Sim.DrainEvents(), e => e is Parried);

        // During the cooldown a new press does nothing, even with a telegraph ready.
        var husk = Place(a, EnemyKind.Husk, 2);
        husk.ForceWindup(1);
        Run(a.Sim, view, 1, new PlayerActions(false, false, true));
        Assert.DoesNotContain(a.Sim.DrainEvents(), e => e is Parried);
    }

    [Fact]
    public void PointBlankShotgunParriesAWindup()
    {
        var a = MakeArena();
        var husk = Place(a, EnemyKind.Husk, 1.5);
        husk.ForceWindup(0.4);
        Run(a.Sim, Aim(a.View(), husk.Feet + new Vec3(0, 1, 0)), 1, new PlayerActions(false, true, false));
        Assert.Contains(a.Sim.DrainEvents(), e => e is Parried { Kind: ParryKind.Shotgun });
    }

    [Fact]
    public void SpittersShootProjectilesThatHurt()
    {
        var a = MakeArena();
        Place(a, EnemyKind.Spitter, 9);
        var view = a.View();
        Run(a.Sim, view, 60 * 6);
        var ev = a.Sim.DrainEvents();
        Assert.Contains(ev, e => e is ProjectileFired);
        Assert.Contains(ev, e => e is PlayerHurt { Damage: 15 });
    }

    [Fact]
    public void ReflectingAProjectileKillsTheShooterWithAChargeback()
    {
        var a = MakeArena();
        var spitter = Place(a, EnemyKind.Spitter, 9);
        var view = a.View();

        bool reflected = false;
        for (int i = 0; i < 60 * 8 && !spitter.Dead; i++)
        {
            bool press = !reflected && a.Sim.Projectiles.Any(p => !p.Reflected && p.Position.DistanceTo(view.Eye) < 2.8);
            var v = reflected || !press ? Aim(view, spitter.Feet + new Vec3(0, 1, 0)) : view;
            a.Sim.Tick(Dt, v, new PlayerActions(false, false, press));
            reflected |= a.Sim.DrainEvents().Any(e => e is ProjectileReflected);
        }
        Assert.True(reflected, "the projectile should have been parried");
        Assert.True(spitter.Dead, "the reflected projectile should kill a 20 hp spitter");
        Assert.True(a.Sim.Style.TotalScore >= StyleMeter.BaseValue(StyleBonus.Chargeback));
    }

    [Fact]
    public void ProjectilesDieOnWalls()
    {
        var a = MakeArena();
        Place(a, EnemyKind.Spitter, 9);
        // Stand behind a wall so the shot cannot reach: the spitter has no line of sight and will not fire.
        for (int y = 0; y < 4; y++)
            for (int dz = -3; dz <= 3; dz++)
                a.World.SetBlock((int)Math.Floor(a.X) + 3, (int)a.Y + y, (int)Math.Floor(a.Z) + dz, BlockType.Stone);
        Run(a.Sim, a.View(), 60 * 4);
        Assert.DoesNotContain(a.Sim.DrainEvents(), e => e is PlayerHurt);
        Assert.Empty(a.Sim.Projectiles);
    }

    [Fact]
    public void BeingHitDropsStyleAndStartsHardDamage()
    {
        var a = MakeArena();
        for (int i = 0; i < 20; i++) a.Sim.Style.Award(StyleBonus.Chargeback);
        var before = a.Sim.Style.Rank;
        var husk = Place(a, EnemyKind.Husk, 1.2);
        Run(a.Sim, a.View(), 60 * 2);
        Assert.True(a.Sim.Vitals.Health < 100);
        Assert.True(a.Sim.Style.Rank < before);
        Assert.True(husk.Health > 0);
    }

    [Fact]
    public void DyingEmitsOnceAndStopsTheSim()
    {
        var a = MakeArena();
        a.Sim.Vitals.Damage(95);
        Place(a, EnemyKind.Husk, 1.2);
        Run(a.Sim, a.View(), 60 * 3);
        Assert.Single(a.Sim.DrainEvents().OfType<PlayerDied>());
        Assert.True(a.Sim.Dead);
        double t = a.Sim.Time;
        Run(a.Sim, a.View(), 10, new PlayerActions(true, true, true));
        Assert.Empty(a.Sim.DrainEvents().OfType<ShotFired>());
        Assert.True(a.Sim.Time > t);
    }

    [Fact]
    public void FightsAreDeterministic()
    {
        List<string> Play()
        {
            var a = MakeArena();
            Place(a, EnemyKind.Husk, 5);
            Place(a, EnemyKind.Spitter, 10, 2);
            var log = new List<string>();
            for (int i = 0; i < 600; i++)
            {
                var target = a.Sim.Enemies.FirstOrDefault(e => !e.Dead);
                var view = target == null ? a.View() : Aim(a.View(), target.Feet + new Vec3(0, 1.4, 0));
                a.Sim.Tick(Dt, view, new PlayerActions(i % 20 == 0, i % 90 == 0, i % 47 == 0));
                log.AddRange(a.Sim.DrainEvents().Select(e => e.ToString()!));
            }
            log.Add($"{a.Sim.Vitals.Health:F4} {a.Sim.Style.Points:F4}");
            return log;
        }
        Assert.Equal(Play(), Play());
    }

    // ---- encounters and floors --------------------------------------------------------------

    private static (VoxelWorld World, DungeonSite Site, CombatSim Sim, Room Room) FindEncounter()
    {
        for (long seed = 1; seed < 15; seed++)
        {
            var world = new VoxelWorld(seed);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is { } site)
                    {
                        var rooms = site.Spawns.Where(s => s.Floor == 0 && s.Kind == SpawnKind.Monster).GroupBy(s => s.RoomId).OrderBy(g => g.Count()).ToList();
                        if (rooms.Count == 0) continue;
                        return (world, site, new CombatSim(world, site), site.Floors[0].Rooms[rooms[0].Key]);
                    }
        }
        throw new InvalidOperationException("no encounter found");
    }

    private static PlayerView InRoom(DungeonSite site, Room room, int floor = 0)
    {
        var f = site.Floors[floor];
        for (int r = 0; r < 6; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                    if (f.CellAt(room.CenterX + dx, room.CenterZ + dz) == CellKind.Floor && f.RoomIdAt(room.CenterX + dx, room.CenterZ + dz) == room.Id)
                    {
                        var (wx, wy, wz) = site.ToWorld(floor, room.CenterX + dx, room.CenterZ + dz);
                        var feet = new Vec3(wx + 0.5, wy, wz + 0.5);
                        return new PlayerView(feet, feet + new Vec3(0, 1.62, 0), new Vec3(1, 0, 0), false, false);
                    }
        throw new InvalidOperationException("no standing spot");
    }

    [Fact]
    public void EnteringARoomSpawnsItsEnemiesAndLocksItsDoors()
    {
        var (world, site, sim, room) = FindEncounter();
        var expected = site.Spawns.Count(s => s.Floor == 0 && s.RoomId == room.Id && s.Kind is SpawnKind.Monster or SpawnKind.Boss);
        Assert.Empty(sim.Enemies);

        Run(sim, InRoom(site, room), 2);
        var ev = sim.DrainEvents();
        var started = Assert.Single(ev.OfType<EncounterStarted>());
        Assert.Equal(expected, started.Enemies);
        Assert.Equal(expected, sim.Enemies.Count);
        Assert.Equal(expected, ev.OfType<EnemySpawned>().Count());
        Assert.Contains(ev, e => e is DoorsChanged { Locked: true });

        var gates = 0;
        for (int z = 0; z < FloorLayout.Size; z++)
            for (int x = 0; x < FloorLayout.Size; x++)
                if (world.GetBlock(site.OriginX + x, DungeonSite.FloorY(0) + 1, site.OriginZ + z) == BlockType.Gate) gates++;
        Assert.True(gates > 0, "doors should now be gates");
        Assert.NotEmpty(sim.TakeDirtyChunks());
        Assert.Empty(sim.TakeDirtyChunks());

        Run(sim, InRoom(site, room), 5);
        Assert.Empty(sim.DrainEvents().OfType<EncounterStarted>()); // only once
    }

    [Fact]
    public void ClearingARoomUnlocksItsDoors()
    {
        var (world, site, sim, room) = FindEncounter();
        var view = InRoom(site, room);
        Run(sim, view, 2);
        sim.DrainEvents();

        // Kill everything the moment it spawns (a scripted shortcut for this test).
        foreach (var e in sim.Enemies.ToList()) e.SetHealth(0.01);
        for (int i = 0; i < 600 && sim.Enemies.Any(e => !e.Dead); i++)
        {
            var target = sim.Enemies.First(e => !e.Dead);
            var aimed = Aim(view, target.Feet + new Vec3(0, 1.0, 0));
            sim.Tick(Dt, aimed, new PlayerActions(i % 20 == 0, false, false));
        }

        var ev = sim.DrainEvents();
        Assert.Contains(ev, e => e is EncounterCleared);
        Assert.Contains(ev, e => e is DoorsChanged { Locked: false });
        for (int z = 0; z < FloorLayout.Size; z++)
            for (int x = 0; x < FloorLayout.Size; x++)
                Assert.NotEqual(BlockType.Gate, world.GetBlock(site.OriginX + x, DungeonSite.FloorY(0) + 1, site.OriginZ + z));
    }

    [Fact]
    public void RespawningReopensDoorsResetsTheFightAndCostsRankPoints()
    {
        var (world, site, sim, room) = FindEncounter();
        Run(sim, InRoom(site, room), 2);
        Assert.NotEmpty(sim.Enemies);

        sim.Respawn();
        Assert.Empty(sim.Enemies);
        Assert.Equal(100, sim.Vitals.Health);
        for (int z = 0; z < FloorLayout.Size; z++)
            for (int x = 0; x < FloorLayout.Size; x++)
                Assert.NotEqual(BlockType.Gate, world.GetBlock(site.OriginX + x, DungeonSite.FloorY(0) + 1, site.OriginZ + z));

        sim.DrainEvents();
        Run(sim, InRoom(site, room), 2);
        Assert.Single(sim.DrainEvents().OfType<EncounterStarted>()); // the room can fight again
    }

    [Fact]
    public void LeavingAFloorGradesIt()
    {
        var (world, site, sim, room) = FindEncounter();
        // Enter floor 0, then drop to floor 1: floor 0 is complete.
        Run(sim, InRoom(site, site.Floors[0].EntranceRoom), 2);
        Assert.Equal(0, sim.CurrentFloor);
        Assert.False(sim.FloorCompleted(0));

        var below = InRoom(site, site.Floors[1].EntranceRoom, floor: 1);
        Run(sim, below, 2);
        var done = Assert.Single(sim.DrainEvents().OfType<FloorCompleted>());
        Assert.Equal(0, done.Floor);
        Assert.True(sim.FloorCompleted(0));
        Assert.Equal(Grade.S, done.Result.Time); // instantly through
        Assert.Equal(Grade.D, done.Result.Style);
        Assert.Equal(OverallGrade.C, done.Result.Overall); // S + D + D = 7 points: speed alone does not make a good run
        Assert.Equal(Grade.D, done.Result.Kills);
        Assert.Equal(1, sim.CurrentFloor);
        Assert.True(sim.TargetsFor(0).ParTimeSeconds > 40);
        Assert.Equal(0, sim.KillsOn(0));
        Assert.True(sim.EnemiesOn(0) > 0);
    }

    // ---- portals ----------------------------------------------------------------------------

    private static void LinkEastAndWest(Arena a, out Vec3 eastWall, out Vec3 westWall)
    {
        var view = a.View();
        var east = new Vec3(1, 0, 0);
        var west = new Vec3(-1, 0, 0);
        eastWall = view.Eye + east * (RayCast.FirstSolid(a.World.GetBlock, view.Eye, east, 80) ?? throw new InvalidOperationException("no east wall"));
        westWall = view.Eye + west * (RayCast.FirstSolid(a.World.GetBlock, view.Eye, west, 80) ?? throw new InvalidOperationException("no west wall"));
        a.Sim.Tick(Dt, view, new PlayerActions(false, false, false, PortalTeal: true));
        a.Sim.Tick(Dt, view with { Forward = west }, new PlayerActions(false, false, false, PortalViolet: true));
    }

    [Fact]
    public void FiringPortalsPlacesAndLinksThemAndReportsIt()
    {
        var a = MakeArena();
        var view = a.View();
        a.Sim.Tick(Dt, view, new PlayerActions(false, false, false, PortalTeal: true));
        var teal = Assert.Single(a.Sim.DrainEvents().OfType<PortalPlaced>());
        Assert.True(teal.Success, teal.Failure.ToString());
        Assert.Equal(PortalColor.Teal, teal.Color);
        Assert.Equal(-1, teal.Normal.X);
        Assert.False(a.Sim.Portals.Linked);

        a.Sim.Tick(Dt, view with { Forward = new Vec3(-1, 0, 0) }, new PlayerActions(false, false, false, PortalViolet: true));
        Assert.True(Assert.Single(a.Sim.DrainEvents().OfType<PortalPlaced>()).Success);
        Assert.True(a.Sim.Portals.Linked);
    }

    [Fact]
    public void ShotsGoThroughPortalsAndHitEnemiesOnTheOtherSide()
    {
        var a = MakeArena();
        LinkEastAndWest(a, out _, out var westWall);
        a.Sim.DrainEvents();

        // The violet portal is on the west wall facing east; stand an enemy a few blocks out from it.
        var enemy = a.Sim.SpawnEnemy(EnemyKind.Warden, new Vec3(westWall.X + 1.6, a.Y, a.Z));
        a.Sim.DrainEvents();

        // Shoot from a few blocks east, so the enemy near the violet portal is not on top of the shooter.
        var view = a.View(dx: 5) with { Forward = new Vec3(1, 0, 0) };
        a.Sim.Tick(Dt, view, new PlayerActions(true, false, false));
        var events = a.Sim.DrainEvents();
        var shot = Assert.Single(events.OfType<ShotFired>());
        Assert.True(shot.Path!.Count >= 2, "the shot should be in two pieces: to the teal portal and out of the violet one");
        Assert.Contains(events, e => e is EnemyHit h && h.Id == enemy.Id);
        Assert.True(enemy.Health < enemy.Spec.Health);
    }

    [Fact]
    public void WithoutALinkedPairShotsJustHitTheWall()
    {
        var a = MakeArena();
        var view = a.View();
        a.Sim.Tick(Dt, view, new PlayerActions(false, false, false, PortalTeal: true));
        a.Sim.DrainEvents();
        a.Sim.Tick(Dt, view, new PlayerActions(true, false, false));
        var shot = Assert.Single(a.Sim.DrainEvents().OfType<ShotFired>());
        Assert.Single(shot.Path!);
    }

    [Fact]
    public void ReflectedProjectilesFlyThroughPortals()
    {
        var a = MakeArena();
        LinkEastAndWest(a, out _, out _);
        a.Sim.DrainEvents();

        var spitter = a.Sim.SpawnEnemy(EnemyKind.Spitter, new Vec3(a.X + 9, a.Y, a.Z + 1));
        var view = a.View();
        bool through = false, reflected = false;
        for (int i = 0; i < 60 * 8 && !through; i++)
        {
            bool press = !reflected && a.Sim.Projectiles.Any(p => !p.Reflected && p.Position.DistanceTo(view.Eye) < 2.8);
            a.Sim.Tick(Dt, view, new PlayerActions(false, false, press));
            foreach (var e in a.Sim.DrainEvents())
            {
                reflected |= e is ProjectileReflected;
                through |= e is PortalTraversed { What: "projectile" };
            }
        }
        Assert.True(reflected, "the shot should have been parried");
        Assert.True(through || spitter.Dead, "a reflected projectile should fly into the teal portal, or kill the spitter first");
    }

    [Fact]
    public void EnemiesWalkThroughPortalsToReachYou()
    {
        var a = MakeArena();
        LinkEastAndWest(a, out var eastWall, out _);
        a.Sim.DrainEvents();

        // The player stands near the violet portal on the west wall; the enemy is right beside the teal one on the east wall.
        var husk = a.Sim.SpawnEnemy(EnemyKind.Husk, new Vec3(eastWall.X - 1.5, a.Y, a.Z));
        double direct = husk.Feet.DistanceTo(new Vec3(a.X, a.Y, a.Z));
        Assert.True(direct > 9, $"the long way round is {direct} blocks");

        bool through = false;
        double closest = double.MaxValue;
        for (int i = 0; i < 60 * 6 && !through; i++)
        {
            a.Sim.Tick(Dt, a.View(), default);
            through |= a.Sim.DrainEvents().Any(e => e is PortalTraversed { What: "enemy" });
            closest = Math.Min(closest, husk.Feet.DistanceTo(new Vec3(a.X, a.Y, a.Z)));
        }
        Assert.True(through, "the husk should take the shortcut through the portal");
        Assert.True(husk.Feet.DistanceTo(new Vec3(a.X, a.Y, a.Z)) < direct - 5, "and come out far closer to the player");
    }

    [Fact]
    public void EnemiesIgnorePortalsThatAreNotAShortcut()
    {
        var a = MakeArena();
        LinkEastAndWest(a, out _, out _);
        a.Sim.DrainEvents();
        var husk = a.Sim.SpawnEnemy(EnemyKind.Husk, new Vec3(a.X + 3, a.Y, a.Z)); // already close
        Run(a.Sim, a.View(), 60);
        Assert.DoesNotContain(a.Sim.DrainEvents(), e => e is PortalTraversed { What: "enemy" });
        Assert.NotNull(husk);
    }

    [Fact]
    public void RespawningRemovesPortals()
    {
        var a = MakeArena();
        LinkEastAndWest(a, out _, out _);
        Assert.True(a.Sim.Portals.Linked);
        a.Sim.Respawn();
        Assert.False(a.Sim.Portals.Linked);
        Assert.Null(a.Sim.Portals.Teal);
    }

    [Fact]
    public void FloorTimersRunWhileYouAreOnAFloorAndFreezeWhenItIsDone()
    {
        var (_, site, sim, _) = FindEncounter();
        Assert.Equal(0, sim.ElapsedOn(0)); // not entered yet

        var inFloor0 = InRoom(site, site.Floors[0].EntranceRoom);
        Run(sim, inFloor0, 60);
        double t = sim.ElapsedOn(0);
        Assert.InRange(t, 0.9, 1.1);
        Run(sim, inFloor0, 60);
        Assert.InRange(sim.ElapsedOn(0), t + 0.9, t + 1.1);

        Run(sim, InRoom(site, site.Floors[1].EntranceRoom, floor: 1), 2); // down to floor 1: floor 0 is complete
        double frozen = sim.ElapsedOn(0);
        Run(sim, InRoom(site, site.Floors[1].EntranceRoom, floor: 1), 120);
        Assert.Equal(frozen, sim.ElapsedOn(0), 6);
        Assert.True(sim.ElapsedOn(1) > 1.5);
    }

    [Fact]
    public void FloorIndexAtMapsHeightsToFloors()
    {
        var (_, site, _, _) = FindEncounter();
        Assert.Equal(0, site.FloorIndexAt(site.OriginX + 10, DungeonSite.FloorY(0) + 1, site.OriginZ + 10));
        Assert.Equal(1, site.FloorIndexAt(site.OriginX + 10, DungeonSite.FloorY(1) + 3, site.OriginZ + 10));
        Assert.Equal(-1, site.FloorIndexAt(site.OriginX + 10, site.SurfaceY + 1, site.OriginZ + 10));
        Assert.Equal(-1, site.FloorIndexAt(site.OriginX - 5, DungeonSite.FloorY(0) + 1, site.OriginZ + 10));
    }
}
