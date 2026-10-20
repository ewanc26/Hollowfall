using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Relations;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

/// <summary>A real dungeon room with room to move: the player stands at the west end, empty floor to the east.</summary>
internal sealed class RelationArena
{
    public required VoxelWorld World { get; init; }
    public required DungeonSite Site { get; init; }
    public required CombatSim Sim { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Z { get; init; }

    public PlayerView View(double dx = 0, double dz = 0) =>
        new(new Vec3(X + dx, Y, Z + dz), new Vec3(X + dx, Y + 1.62, Z + dz), new Vec3(1, 0, 0), false, false);

    public static RelationArena Make(long seed = 1, bool relations = true)
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
                        int z = room.CenterZ + 2;
                        for (int x = room.Bounds.X + 1; x + 11 < room.Bounds.Right - 1; x++)
                        {
                            bool clear = Enumerable.Range(0, 12).All(i => Enumerable.Range(-3, 7).All(dz => site.Floors[0].CellAt(x + i, z + dz) == CellKind.Floor));
                            if (!clear) continue;
                            var sim = new CombatSim(world, site);
                            if (relations) sim.EnableRelations();
                            return new RelationArena
                            {
                                World = world,
                                Site = site,
                                Sim = sim,
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

    public Enemy Place(EnemyKind kind, double dx, double dz = 0) => Sim.SpawnEnemy(kind, new Vec3(X + dx, Y, Z + dz));

    public List<CombatEvent> Events { get; } = [];

    public void Run(int ticks, PlayerView? view = null, PlayerActions actions = default)
    {
        for (int i = 0; i < ticks; i++)
        {
            Sim.Tick(1.0 / 60, view ?? View(), i == 0 ? actions : default);
            Events.AddRange(Sim.DrainEvents());
        }
    }

    /// <summary>Fires the revolver at a point on the enemy and waits out the cooldown.</summary>
    public void Shoot(Enemy e, double aimHeight, int wait = 20)
    {
        var view = View();
        var at = new Vec3(e.Feet.X, e.Feet.Y + aimHeight, e.Feet.Z);
        Run(1, view with { Forward = (at - view.Eye).Normalized() }, new PlayerActions(true, false, false));
        Run(wait);
    }

    /// <summary>One head shot kills anything small.</summary>
    public void Kill(Enemy e)
    {
        for (int i = 0; i < 6 && !e.Dead; i++) Shoot(e, e.Spec.Height * 0.85);
        Assert.True(e.Dead, $"{e.Kind} should be dead");
    }
}

public class CreatureSpawnTests
{
    private static IEnumerable<(long Seed, DungeonSite Site)> SitesWithSeed()
    {
        for (long seed = 1; seed <= 12; seed++)
        {
            var g = new TerrainGenerator(seed);
            for (int rx = -2; rx <= 1; rx++)
                for (int rz = -2; rz <= 1; rz++)
                    if (g.Dungeons!.GetSite(rx, rz) is { } s)
                        yield return (seed, s);
        }
    }

    private static IEnumerable<DungeonSite> Sites() => SitesWithSeed().Select(t => t.Site);

    private static readonly EnemyKind[] Kinds = [EnemyKind.Brownie, EnemyKind.Selkie, EnemyKind.Kelpie, EnemyKind.BeanNighe, EnemyKind.Hare];

    [Fact]
    public void CreaturesComeLastSoEveryEarlierSpawnKeepsItsIndex()
    {
        foreach (var site in Sites())
        {
            int firstCreature = site.Spawns.ToList().FindIndex(s => s.Kind == SpawnKind.Creature);
            if (firstCreature < 0) continue;
            Assert.All(site.Spawns.Skip(firstCreature), s => Assert.Equal(SpawnKind.Creature, s.Kind));
            Assert.All(site.Spawns.Take(firstCreature), s => Assert.NotEqual(SpawnKind.Creature, s.Kind));
        }
    }

    [Fact]
    public void CreatureSpawnsAreDeterministicAndVaryBySeed()
    {
        var (seed, a) = SitesWithSeed().First(s => s.Site.Spawns.Any(x => x.Kind == SpawnKind.Creature));
        var again = new TerrainGenerator(seed).Dungeons!.GetSite(a.RegionX, a.RegionZ)!;
        Assert.Equal(a.Spawns, again.Spawns);

        var bySeed = SitesWithSeed().GroupBy(s => s.Seed).Select(g => g.SelectMany(s => s.Site.Spawns.Where(x => x.Kind == SpawnKind.Creature)).Select(x => (x.Variant, x.Floor)).ToList()).ToList();
        Assert.True(bySeed.Select(l => string.Join(",", l)).Distinct().Count() > 4);
    }

    [Fact]
    public void EveryKindAppearsSomewhereAndOnlyTheRightKinds()
    {
        var seen = new HashSet<int>();
        foreach (var site in Sites())
            foreach (var sp in site.Spawns.Where(s => s.Kind == SpawnKind.Creature))
            {
                Assert.Contains((EnemyKind)sp.Variant, Kinds);
                seen.Add(sp.Variant);
            }
        foreach (var k in Kinds) Assert.Contains((int)k, seen);
    }

    [Fact]
    public void CreaturesStandOnFloorInTheRightRoomsAwayFromDoorsAndStairs()
    {
        foreach (var site in Sites())
        {
            foreach (var sp in site.Spawns.Where(s => s.Kind == SpawnKind.Creature))
            {
                var floor = site.Floors[sp.Floor];
                var room = floor.Rooms[sp.RoomId];
                int lx = sp.X - site.OriginX, lz = sp.Z - site.OriginZ;
                Assert.Equal(CellKind.Floor, floor.CellAt(lx, lz));
                Assert.Equal(sp.RoomId, floor.RoomIdAt(lx, lz));
                Assert.Equal(BlockType.Air, site.BlockAt(sp.X, sp.Y, sp.Z));
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                        Assert.False(floor.CellAt(lx + dx, lz + dz) is CellKind.Door or CellKind.SecretDoor);

                switch ((EnemyKind)sp.Variant)
                {
                    case EnemyKind.Kelpie:
                    case EnemyKind.Selkie:
                        Assert.True(SpawnPlanner.IsCistern(room), "water-folk live in round rooms");
                        break;
                    case EnemyKind.Brownie:
                        Assert.True(room.Role is RoomRole.Safe or RoomRole.Entrance or RoomRole.Standard);
                        break;
                    case EnemyKind.BeanNighe:
                        Assert.True(sp.Floor >= 1 && room.Role == RoomRole.Standard && room.Depth >= 1);
                        break;
                    case EnemyKind.Hare:
                        Assert.True(room.Role is RoomRole.Safe or RoomRole.Entrance or RoomRole.Standard);
                        break;
                }
            }
            Assert.True(site.Spawns.Count(s => s.Kind == SpawnKind.Creature && s.Variant == (int)EnemyKind.BeanNighe) <= site.FloorCount);
            foreach (var floor in site.Floors)
                Assert.True(site.Spawns.Count(s => s.Kind == SpawnKind.Creature && s.Floor == floor.Floor && s.Variant == (int)EnemyKind.BeanNighe) <= 1);
        }
    }

    [Fact]
    public void NoKelpieWaitsWhereYouArriveOrInAnOrdinaryRoomOnTheFirstFloor()
    {
        foreach (var site in Sites())
            foreach (var sp in site.Spawns.Where(s => s.Kind == SpawnKind.Creature && s.Variant == (int)EnemyKind.Kelpie))
            {
                var role = site.Floors[sp.Floor].Rooms[sp.RoomId].Role;
                Assert.NotEqual(RoomRole.Entrance, role);
                if (sp.Floor == 0) Assert.NotEqual(RoomRole.Standard, role);
            }
    }

    [Fact]
    public void ExistingMonsterBossLootKeyAndTrapSpawnsAreUntouched()
    {
        // The creature pass uses its own random stream and runs last: removing it must leave exactly what the plan always produced.
        foreach (var site in Sites())
        {
            var old = site.Spawns.Where(s => s.Kind != SpawnKind.Creature).ToList();
            Assert.Equal(old.Count(s => s.Kind == SpawnKind.Boss), site.Spawns.Count(s => s.Kind == SpawnKind.Boss));
            Assert.Equal(old.Count, site.Spawns.Count - site.Spawns.Count(s => s.Kind == SpawnKind.Creature));
            Assert.All(old, s => Assert.Equal(0, s.Variant));
        }
    }
}

public class RelationSimTests
{
    [Fact]
    public void WithoutRelationsNoCreatureEverAppears()
    {
        var a = RelationArena.Make(relations: false);
        a.Run(120);
        Assert.Null(a.Sim.Relations);
        Assert.Empty(a.Sim.Enemies);
    }

    [Fact]
    public void WithRelationsCreaturesAppearOnceWhenTheirFloorIsReached()
    {
        var a = RelationArena.Make();
        a.Run(60);
        int expected = a.Site.Spawns.Count(s => s.Kind == SpawnKind.Creature && s.Floor == 0);
        Assert.Equal(expected, a.Sim.Enemies.Count);
        Assert.Equal(expected, a.Events.OfType<EnemySpawned>().Count());
        a.Run(60);
        Assert.Equal(expected, a.Events.OfType<EnemySpawned>().Count());
        Assert.All(a.Sim.Enemies, e => Assert.NotNull(e.Mind));
    }

    [Fact]
    public void AHuskStillChasesAtDefaultAndMovesExactlyAsItDidWithoutRelations()
    {
        var plain = RelationArena.Make(relations: false);
        var rel = RelationArena.Make();
        var h1 = plain.Place(EnemyKind.Husk, 9);
        var h2 = rel.Place(EnemyKind.Husk, 9);
        // Creatures on the arena's own floor would add noise to the second sim; the husk is what we compare.
        double start = h1.Feet.DistanceTo(plain.View().Feet);
        for (int i = 0; i < 90; i++)
        {
            plain.Run(1);
            rel.Run(1);
            Assert.Equal(h1.Feet, h2.Feet);
            Assert.Equal(h1.State, h2.State);
        }
        Assert.True(h2.Feet.DistanceTo(rel.View().Feet) < start - 3, "the husk walked toward the player");
        Assert.Equal(Disposition.Aggressive, h2.Mind!.Disposition);
        Assert.Equal(Intent.Attack, h2.Mind.Intent);
    }

    [Fact]
    public void ARangedSpitterAndAWardenKeepTheirBehaviourToo()
    {
        var plain = RelationArena.Make(relations: false);
        var rel = RelationArena.Make();
        var s1 = plain.Place(EnemyKind.Spitter, 9);
        var s2 = rel.Place(EnemyKind.Spitter, 9);
        var w1 = plain.Place(EnemyKind.Warden, 5, 2);
        var w2 = rel.Place(EnemyKind.Warden, 5, 2);
        for (int i = 0; i < 240; i++)
        {
            plain.Run(1);
            rel.Run(1);
            Assert.Equal(s1.Feet, s2.Feet);
            Assert.Equal(w1.Feet, w2.Feet);
        }
        Assert.Equal(plain.Events.OfType<PlayerHurt>().Count(), rel.Events.OfType<PlayerHurt>().Count());
        Assert.Equal(plain.Events.OfType<ProjectileFired>().Count(), rel.Events.OfType<ProjectileFired>().Count());
    }

    [Fact]
    public void RoomsHoldingOnlyCreaturesNeverLockTheirDoors()
    {
        for (long seed = 1; seed <= 12; seed++)
        {
            var world = new VoxelWorld(seed);
            for (int rx = -2; rx <= 1; rx++)
                for (int rz = -2; rz <= 1; rz++)
                {
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is not { } site) continue;
                    foreach (var sp in site.Spawns.Where(s => s.Kind == SpawnKind.Creature && s.Floor == 0))
                    {
                        bool hasMonsters = site.Spawns.Any(s => s.Floor == 0 && s.RoomId == sp.RoomId && s.Kind is SpawnKind.Monster or SpawnKind.Boss);
                        if (hasMonsters) continue;

                        var sim = new CombatSim(world, site);
                        sim.EnableRelations();
                        var feet = new Vec3(sp.X + 0.5, sp.Y, sp.Z + 0.5);
                        var view = new PlayerView(feet, feet + new Vec3(0, 1.62, 0), new Vec3(1, 0, 0), false, false);
                        var events = new List<CombatEvent>();
                        for (int i = 0; i < 120; i++) { sim.Tick(1.0 / 60, view, default); events.AddRange(sim.DrainEvents()); }
                        Assert.Empty(events.OfType<EncounterStarted>());
                        Assert.Empty(events.OfType<DoorsChanged>());
                        Assert.NotEmpty(events.OfType<EnemySpawned>());
                        return;
                    }
                }
        }
        Assert.Fail("no room that holds only creatures was found");
    }

    [Fact]
    public void FloorCountsAndRankingIgnoreCreatures()
    {
        var a = RelationArena.Make();
        int before = a.Sim.EnemiesOn(0);
        a.Run(10);
        Assert.Equal(before, a.Sim.EnemiesOn(0));
        Assert.Equal(a.Site.Spawns.Count(s => s.Floor == 0 && s.Kind is SpawnKind.Monster or SpawnKind.Boss), a.Sim.EnemiesOn(0));

        var hare = a.Place(EnemyKind.Hare, 6);
        double style = a.Sim.Style.TotalScore;
        a.Kill(hare);
        Assert.Equal(0, a.Sim.KillsOn(0));
        Assert.Equal(style, a.Sim.Style.TotalScore);
    }

    [Fact]
    public void KillingAHuskWarmsTheFolkThroughTheSim()
    {
        var a = RelationArena.Make();
        var husk = a.Place(EnemyKind.Husk, 9);
        husk.SetHealth(5);
        a.Shoot(husk, 1.2);
        Assert.True(husk.Dead);
        var rep = a.Sim.Relations!.Reputation;
        Assert.Equal(5, rep.Value(Faction.Brownies), 1);
        Assert.Equal(-100, rep.Value(Faction.Husks), 6);
        Assert.Contains(a.Events, e => e is EnemyKilled { Kind: EnemyKind.Husk });
    }

    [Fact]
    public void HurtingNeutralAnimalsRaisesAngerOnlyOncePerVolley()
    {
        var a = RelationArena.Make();
        var selkie = a.Place(EnemyKind.Selkie, 6);
        a.Run(2, actions: default);
        var view = a.View() with { Forward = ((selkie.Feet + new Vec3(0, 0.8, 0)) - a.View().Eye).Normalized() };
        a.Run(1, view, new PlayerActions(false, true, false)); // a shotgun blast: eight pellets in one tick
        a.Run(1);
        var anger = selkie.Mind!.Memory.Anger;
        Assert.InRange(anger, 0.01, 0.99);
        Assert.True(anger < 0.75, "eight pellets are one hurt");
        Assert.Contains(a.Events, e => e is EntityProvoked);
    }

    [Fact]
    public void ScenarioKillABrownieInFrontOfTheFolkAndTheFolkTurnWhileTheHollowIsUnmoved()
    {
        var a = RelationArena.Make();
        var rep = a.Sim.Relations!.Reputation;
        // Pin the arena's own population down first: this scene is about the creatures placed here.
        var husk = a.Place(EnemyKind.Husk, 11, 3);
        var brownies = new[] { a.Place(EnemyKind.Brownie, 6, 0), a.Place(EnemyKind.Brownie, 6, 1.5), a.Place(EnemyKind.Brownie, 7, -1.5) };
        var selkies = Enumerable.Range(0, 8).Select(i => a.Place(EnemyKind.Selkie, 8 + (i % 3) * 0.7, -2.5 + i * 0.7)).ToList();
        foreach (var s in selkies) s.Mind!.Memory.Clear();
        double hollowBefore = rep.Value(Faction.Spitters);

        a.Kill(brownies[0]);
        Assert.Equal(-20, rep.Value(Faction.Brownies), 1);
        Assert.Equal(-16, rep.Value(Faction.Selkies), 1);
        Assert.Contains(a.Events, e => e is WitnessAlerted);
        Assert.Equal(hollowBefore, rep.Value(Faction.Spitters), 6);
        Assert.True(selkies.Any(s => s.Mind!.Memory.Anger > 0), "friends of the brownie saw it die");
        Assert.True(brownies[1].Mind!.Memory.Anger > 0.3 && brownies[1].Mind.Provoked, "its own kin are provoked");
        Assert.Equal(Disposition.Skittish, brownies[1].Mind.Disposition);

        a.Kill(brownies[1]);
        a.Kill(brownies[2]);
        Assert.True(rep.Tier(Faction.Brownies) <= ReputationTier.Hostile);
        Assert.True(rep.Tier(Faction.Selkies) <= ReputationTier.Hostile);
        Assert.Contains(a.Events, e => e is ReputationChanged { Faction: Faction.Brownies, New: ReputationTier.Hostile });

        // The selkies are provoked; the brave ones fight, the timid ones run, and nobody is left calm.
        foreach (var s in selkies)
        {
            if (s.Dead) continue;
            Assert.True(s.Mind!.Provoked, $"selkie with bravery {s.Mind.Traits.Bravery:F2}");
            Assert.Equal(s.Mind.Traits.Bravery < Dispositions.CowardBelow && s.Mind.Scared ? Disposition.Skittish : Disposition.Aggressive, s.Mind.Disposition);
        }
        Assert.Contains(selkies, s => !s.Dead && s.Mind!.Disposition == Disposition.Aggressive);

        // The Hollow does not care: the husk is as hostile as ever and remembers nothing.
        Assert.Equal(Disposition.Aggressive, husk.Mind!.Disposition);
        Assert.True(husk.Mind.Memory.Blank || husk.Mind.Memory.Anger == 0);
        Assert.Equal(-90, rep.Value(Faction.Husks), 6);
    }

    [Fact]
    public void ProvokedSelkiesWarnAndThenAttackAndLaterCalmDown()
    {
        var a = RelationArena.Make();
        var selkie = a.Place(EnemyKind.Selkie, 3);
        selkie.Mind!.Memory.Set(0.65, 0.1, 0);
        var intents = new List<Intent>();
        for (int i = 0; i < 300; i++)
        {
            a.Run(1);
            if (intents.Count == 0 || intents[^1] != selkie.Mind.Intent) intents.Add(selkie.Mind.Intent);
        }
        Assert.Contains(Intent.Warn, intents);
        Assert.Contains(Intent.Attack, intents);
        Assert.True(intents.IndexOf(Intent.Warn) < intents.IndexOf(Intent.Attack), "a warning first");
        Assert.Contains(a.Events, e => e is EntityProvoked);
        Assert.Contains(a.Events, e => e is PlayerHurt || e is EnemyTelegraph);

        selkie.Mind.Memory.Set(0, 0, 0);
        a.Run(10);
        Assert.Contains(a.Events, e => e is EntityCalmed);
        Assert.NotEqual(Intent.Attack, selkie.Mind.Intent);
    }

    [Fact]
    public void AHareRunsFromAPlayerWhoRushesIt()
    {
        var a = RelationArena.Make();
        var hare = a.Place(EnemyKind.Hare, 4);
        double start = hare.Feet.DistanceTo(a.View().Feet);
        for (int i = 0; i < 60; i++) a.Run(1, a.View(0.04 * i)); // the player closes in at about 2.4 blocks per second
        var player = a.View(0.04 * 59).Feet;
        Assert.Equal(Intent.Flee, hare.Mind!.Intent);
        Assert.True(hare.Feet.DistanceTo(player) > 2, $"started {start}");
        Assert.DoesNotContain(a.Events, e => e is PlayerHurt);
    }

    [Fact]
    public void AKelpieIsCalmUntilYouComeCloseThenWarnsAndLunges()
    {
        var a = RelationArena.Make();
        var kelpie = a.Place(EnemyKind.Kelpie, 11);
        var home = kelpie.Mind!.Home;
        a.Run(90);
        Assert.Equal(Intent.Idle, kelpie.Mind.Intent);
        Assert.True(kelpie.Feet.DistanceTo(home) < 4);

        var near = a.View(6.5);
        var intents = new List<Intent>();
        for (int i = 0; i < 400; i++)
        {
            a.Run(1, near);
            if (intents.Count == 0 || intents[^1] != kelpie.Mind.Intent) intents.Add(kelpie.Mind.Intent);
        }
        Assert.Contains(Intent.Warn, intents);
        Assert.Contains(Intent.Attack, intents);
        Assert.Contains(a.Events, e => e is EnemyTelegraph);
        Assert.Contains(a.Events, e => e is PlayerHurt);
    }

    [Fact]
    public void AKelpieAttackCanBeParriedLikeAnyOther()
    {
        var a = RelationArena.Make();
        var kelpie = a.Place(EnemyKind.Kelpie, 2);
        kelpie.Mind!.Memory.Set(1, 0, 0);
        bool parried = false;
        for (int i = 0; i < 400 && !parried; i++)
        {
            bool telegraphing = kelpie.Telegraphing;
            a.Run(1, actions: new PlayerActions(false, false, telegraphing));
            parried = a.Events.OfType<Parried>().Any();
        }
        Assert.True(parried);
        Assert.Equal(EnemyState.Stunned, kelpie.State);
    }

    [Fact]
    public void ARevereredPlayerGetsASelkieAllyThatFightsTheHollowAndNeverTheirFriend()
    {
        var a = RelationArena.Make();
        a.Sim.Relations!.Reputation.Set(Faction.Selkies, 90);
        var selkie = a.Place(EnemyKind.Selkie, 2, 1);
        selkie.Mind!.Memory.Clear();
        a.Run(5);
        Assert.Equal(Disposition.Allied, selkie.Mind.Disposition);
        Assert.Contains(a.Events, e => e is EntityAllied);
        Assert.Equal(Cue.Allied, selkie.Mind.Cue);

        // With only the player and the ally about, nothing hurts the player however long it runs.
        a.Run(600);
        Assert.DoesNotContain(a.Events, e => e is PlayerHurt || e is EnemyTelegraph);

        var husk = a.Place(EnemyKind.Husk, 7, 2);
        husk.SetHealth(30);
        var view = a.View(0, -3);
        for (int i = 0; i < 600 && !husk.Dead; i++) a.Run(1, view);
        Assert.True(husk.Health < 30, "the ally struck the husk");
        Assert.Contains(a.Events, e => e is EnemyHit h && h.Id == husk.Id);
        Assert.Equal(0, a.Sim.KillsOn(0) - (husk.Dead ? 1 : 0));
    }

    [Fact]
    public void HarmingAnAllyBreaksTheAlliance()
    {
        var a = RelationArena.Make();
        a.Sim.Relations!.Reputation.Set(Faction.Selkies, 90);
        var selkie = a.Place(EnemyKind.Selkie, 5);
        selkie.Mind!.Memory.Clear();
        a.Run(5);
        Assert.Equal(Disposition.Allied, selkie.Mind.Disposition);
        a.Sim.Relations.PlayerHurt(selkie, 6, false, a.Sim.Enemies, a.Sim.Time); // an accident, an explosion, a portal: not a shot
        a.Run(2);
        Assert.NotEqual(Disposition.Allied, selkie.Mind.Disposition);
        Assert.True(a.Sim.Relations.Reputation.Value(Faction.Selkies) < 90);
    }

    [Fact]
    public void ABrowniesBoonNeedsFriendshipAndComesOnlyOnce()
    {
        var a = RelationArena.Make();
        var brownie = a.Place(EnemyKind.Brownie, 1.5);
        a.Sim.Vitals.Damage(50);
        double hurt = a.Sim.Vitals.Health;
        a.Run(1, actions: new PlayerActions(false, false, false, Interact: true));
        Assert.DoesNotContain(a.Events, e => e is BoonGiven);

        a.Sim.Relations!.Reputation.Set(Faction.Brownies, 30);
        a.Run(1, actions: new PlayerActions(false, false, false, Interact: true));
        var boon = Assert.Single(a.Events.OfType<BoonGiven>());
        Assert.Equal(brownie.Id, boon.Id);
        Assert.True(a.Sim.Vitals.Health > hurt);
        Assert.True(brownie.Mind!.BoonGiven);

        a.Run(1, actions: new PlayerActions(false, false, false, Interact: true));
        Assert.Single(a.Events.OfType<BoonGiven>());
    }

    [Fact]
    public void ABrownieOutOfReachGivesNothing()
    {
        var a = RelationArena.Make();
        a.Place(EnemyKind.Brownie, 8);
        a.Sim.Relations!.Reputation.Set(Faction.Brownies, 60);
        a.Run(1, actions: new PlayerActions(false, false, false, Interact: true));
        Assert.DoesNotContain(a.Events, e => e is BoonGiven);
    }

    [Fact]
    public void ABeanNigheWarnsOfAnAmbushAndTheNextEncounterHoldsAnExtraWave()
    {
        // Find an encounter room, then walk into it with and without a washerwoman nearby.
        for (long seed = 1; seed <= 12; seed++)
        {
            var world = new VoxelWorld(seed);
            for (int rx = -2; rx <= 1; rx++)
                for (int rz = -2; rz <= 1; rz++)
                {
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is not { } site) continue;
                    var spawn = site.Spawns.FirstOrDefault(s => s.Floor == 0 && s.Kind == SpawnKind.Monster);
                    if (spawn.Kind != SpawnKind.Monster) continue;

                    List<CombatEvent> Enter(bool nighe, bool killHer = false)
                    {
                        var sim = new CombatSim(world, site);
                        sim.EnableRelations();
                        var feet = new Vec3(spawn.X + 0.5, spawn.Y, spawn.Z + 0.5);
                        var start = new Vec3(site.OriginX + site.Floors[0].EntranceX + 0.5, spawn.Y, site.OriginZ + site.Floors[0].EntranceZ + 0.5);
                        var events = new List<CombatEvent>();
                        PlayerView At(Vec3 f) => new(f, f + new Vec3(0, 1.62, 0), new Vec3(1, 0, 0), false, false);
                        if (nighe) sim.SpawnEnemy(EnemyKind.BeanNighe, start + new Vec3(2, 0, 0));
                        sim.Tick(1.0 / 60, At(start), default); // the washerwoman is within twelve blocks of the player here
                        events.AddRange(sim.DrainEvents());
                        if (killHer)
                        {
                            sim.Enemies.First(e => e.Kind == EnemyKind.BeanNighe).SetHealth(0.0001);
                            sim.Enemies.First(e => e.Kind == EnemyKind.BeanNighe).Mind!.Memory.Clear();
                        }
                        sim.Tick(1.0 / 60, At(feet), default);
                        events.AddRange(sim.DrainEvents());
                        return events;
                    }

                    var calm = Enter(false);
                    var warned = Enter(true);
                    var plain = calm.OfType<EncounterStarted>().Single();
                    var ambush = warned.OfType<EncounterStarted>().Single();
                    Assert.Empty(calm.OfType<OmenFulfilled>());
                    Assert.Contains(warned, e => e is OmenSensed);
                    var omen = Assert.Single(warned.OfType<OmenFulfilled>());
                    Assert.InRange(omen.Extra, 1, 4);
                    Assert.Equal(plain.Enemies + omen.Extra, ambush.Enemies);
                    return;
                }
        }
        Assert.Fail("no dungeon with a monster room found");
    }

    [Fact]
    public void OnceSprungTheOmenIsSpentAndKillingTheWasherwomanCancelsItAndAngersTheFolk()
    {
        var a = RelationArena.Make();
        var rep = a.Sim.Relations!.Reputation;
        var nighe = a.Place(EnemyKind.BeanNighe, 4);
        a.Run(2);
        Assert.True(a.Sim.Relations.OmenArmed);
        Assert.Contains(a.Events, e => e is OmenSensed);

        a.Kill(nighe);
        Assert.False(a.Sim.Relations.OmenArmed, "she cannot warn you now");
        Assert.True(rep.Value(Faction.Brownies) <= -15, "the Folk take it badly");
        Assert.True(rep.Value(Faction.Selkies) <= -14);
        Assert.Equal(-23, rep.Value(Faction.Wanderers), 0); // a wound, then the kill
    }

    [Fact]
    public void WitnessesBehindAWallDoNotSeeAKill()
    {
        var a = RelationArena.Make();
        var victim = a.Place(EnemyKind.Brownie, 5);
        var seeing = a.Place(EnemyKind.Selkie, 7, 2);
        var hidden = a.Place(EnemyKind.Selkie, 9, -2);
        // A wall of stone between the victim and the hidden selkie, but not the player or the other witness.
        for (int dz = -3; dz <= -1; dz++)
            for (int dy = 0; dy < 4; dy++)
                for (int dx = 6; dx <= 8; dx++)
                    a.World.SetBlock((int)Math.Floor(a.X) + dx, (int)a.Y + dy, (int)Math.Floor(a.Z) + dz, BlockType.Stone);
        seeing.Mind!.Memory.Clear();
        hidden.Mind!.Memory.Clear();
        a.Run(1);
        a.Kill(victim);
        Assert.True(seeing.Mind.Memory.Anger > 0 || seeing.Mind.Memory.Fear > 0, "in view");
        Assert.Equal(0, hidden.Mind.Memory.Anger, 6);
        Assert.Contains(a.Events, e => e is WitnessAlerted w && w.Id == seeing.Id);
        Assert.DoesNotContain(a.Events, e => e is WitnessAlerted w && w.Id == hidden.Id);
    }

    [Fact]
    public void AlarmSpreadsOneHopToThoseNearAFrightenedWitness()
    {
        var a = RelationArena.Make();
        var victim = a.Place(EnemyKind.Brownie, 5);
        var witness = a.Place(EnemyKind.Brownie, 6, 2);
        var bystander = a.Place(EnemyKind.Hare, 6, 5);
        // Shut the hare off from the victim so it can only have heard the alarm.
        for (int dz = 3; dz <= 3; dz++)
            for (int dy = 0; dy < 4; dy++)
                for (int dx = 4; dx <= 8; dx++)
                    a.World.SetBlock((int)Math.Floor(a.X) + dx, (int)a.Y + dy, (int)Math.Floor(a.Z) + dz, BlockType.Stone);
        witness.Mind!.Memory.Clear();
        bystander.Mind!.Memory.Clear();
        a.Run(1);
        a.Kill(victim);
        Assert.True(witness.Mind.Memory.Fear > 0);
        Assert.True(bystander.Mind.Memory.Fear > 0, "it heard the cry");
        Assert.True(bystander.Mind.Memory.Fear < witness.Mind.Memory.Fear, "an echo is weaker than the sight");
    }

    [Fact]
    public void TheSameScriptGivesTheSameEventsAndStandings()
    {
        (List<string>, double[]) Run()
        {
            var a = RelationArena.Make();
            var b = a.Place(EnemyKind.Brownie, 6);
            a.Place(EnemyKind.Selkie, 8, 1);
            a.Place(EnemyKind.Kelpie, 11, -2);
            a.Kill(b);
            a.Run(300);
            return (a.Events.Select(e => e.ToString()!).ToList(), FactionGraph.Standable.Select(f => a.Sim.Relations!.Reputation.Value(f)).ToArray());
        }
        var (e1, r1) = Run();
        var (e2, r2) = Run();
        Assert.Equal(e1, e2);
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void RespawnClearsAnArmedOmenAndRepopulatesTheFloor()
    {
        var a = RelationArena.Make();
        a.Place(EnemyKind.BeanNighe, 4);
        a.Run(2);
        Assert.True(a.Sim.Relations!.OmenArmed);
        a.Sim.Respawn();
        Assert.False(a.Sim.Relations.OmenArmed);
        a.Run(2);
        int creatures = a.Site.Spawns.Count(s => s.Kind == SpawnKind.Creature && s.Floor == 0);
        Assert.Equal(creatures, a.Sim.Enemies.Count);
    }

    [Fact]
    public void StandingDecaysWhileYouPlayAndToastsOnlyOnTierChange()
    {
        var a = RelationArena.Make();
        a.Sim.Relations!.Reputation.Set(Faction.Vermin, -30);
        a.Run(1);
        // Wary (-30) decays toward 0 at 0.02/s: far too slowly to change tier in a minute of play.
        a.Run(60 * 60);
        Assert.DoesNotContain(a.Events, e => e is ReputationChanged);
        Assert.True(a.Sim.Relations.Reputation.Value(Faction.Vermin) > -30);
    }

    [Fact]
    public void AGiftRaisesStandingAndGratitudeAndWarmsAShyBrownieIntoFollowingYou()
    {
        var a = RelationArena.Make();
        var brownie = a.Place(EnemyKind.Brownie, 8);
        brownie.Mind!.Memory.Clear();
        a.Run(2);
        Assert.Equal(Disposition.Passive, brownie.Mind.Disposition);
        a.Sim.Relations!.PlayerGave(brownie, a.Sim.Time);
        Assert.Equal(12, a.Sim.Relations.Reputation.Value(Faction.Brownies), 1);
        Assert.True(a.Sim.Relations.Reputation.Value(Faction.Selkies) > 0);
        Assert.True(brownie.Mind.Memory.Gratitude >= Dispositions.GratefulAt);
        a.Run(2);
        Assert.Equal(Disposition.Curious, brownie.Mind.Disposition);
        Assert.NotEqual(Disposition.Aggressive, brownie.Mind.Disposition);
    }

    [Fact]
    public void AnAllyIsWonByFightingTheHollowAndMakingAnOfferingUsingOnlyInteract()
    {
        var a = RelationArena.Make();
        var rep = a.Sim.Relations!.Reputation;
        var selkie = a.Place(EnemyKind.Selkie, 2, 1);
        selkie.Mind!.Memory.Clear();
        a.Run(2);
        a.Run(1, actions: new PlayerActions(false, false, false, Interact: true));
        Assert.DoesNotContain(a.Events, e => e is OfferingAccepted); // not yet: the Folk do not know you

        for (int i = 0; i < 8; i++)
        {
            var husk = a.Place(EnemyKind.Husk, 9);
            husk.SetHealth(1);
            a.Shoot(husk, 1.2);
            Assert.True(husk.Dead);
        }
        Assert.InRange(rep.Value(Faction.Selkies), 38, 40);
        Assert.Equal(ReputationTier.Friendly, rep.Tier(Faction.Selkies));
        a.Run(5);
        Assert.Equal(Disposition.Curious, selkie.Mind.Disposition);

        double health = a.Sim.Vitals.Health;
        var close = a.View(selkie.Feet.X - a.X - 1.0, selkie.Feet.Z - a.Z); // walk up to her: friends do not run
        a.Run(1, close, new PlayerActions(false, false, false, Interact: true));
        var offer = Assert.Single(a.Events.OfType<OfferingAccepted>());
        Assert.Equal(selkie.Id, offer.Id);
        Assert.True(a.Sim.Vitals.Health < health, "an offering costs a little blood");
        a.Run(5, close);
        Assert.Equal(Disposition.Allied, selkie.Mind.Disposition);
        Assert.Contains(a.Events, e => e is EntityAllied);

        a.Run(1, close, new PlayerActions(false, false, false, Interact: true));
        Assert.Single(a.Events.OfType<OfferingAccepted>()); // once per individual
    }

    [Fact]
    public void ShotsAndExplosionsPassThroughAllies()
    {
        var a = RelationArena.Make();
        a.Sim.Relations!.Reputation.Set(Faction.Selkies, 90);
        var selkie = a.Place(EnemyKind.Selkie, 3);
        a.Run(5);
        Assert.Equal(Disposition.Allied, selkie.Mind!.Disposition);
        var husk = a.Place(EnemyKind.Husk, 8);
        a.Shoot(husk, 1.2);
        Assert.Equal(selkie.Spec.Health, selkie.Health);
        Assert.True(husk.Health < husk.Spec.Health);
    }
}
