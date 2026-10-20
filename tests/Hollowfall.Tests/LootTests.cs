using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Models;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class LootTests
{
    private const double Dt = 1.0 / 60;
    private const long Seed = 7;

    private static Spawn Chest(int x, int z, int tier, int floor = 0) => new(SpawnKind.Loot, x, 40, z, floor, 1, tier);

    private static IEnumerable<Spawn> Grid(int tier, int count = 4000, int floor = 0) =>
        Enumerable.Range(0, count).Select(i => Chest(i % 80 * 3 + 5, i / 80 * 3 + 5, tier, floor));

    /// <summary>A chest of the given tier whose seeded loot is <paramref name="kind"/>.</summary>
    private static Spawn ChestHolding(LootKind kind, int tier)
    {
        for (int x = 0; x < 5000; x++)
        {
            var s = Chest(x, 10, tier);
            if (LootTable.Roll(Seed, s).Kind == kind) return s;
        }
        throw new InvalidOperationException($"no {kind} chest at tier {tier}");
    }

    /// <summary>Real vitals, style and interactables wired the way the sim does.</summary>
    private sealed class Rig
    {
        public PlayerVitals Vitals = new();
        public StyleMeter Style = new();
        public List<CombatEvent> Events = [];
        public Interactables Items;

        public Rig(params Spawn[] spawns) =>
            Items = new Interactables(spawns, Seed, Vitals.Damage, Vitals.Heal, Events.Add, Vitals.Cleanse, m => Style.Award(StyleBonus.Parry, extraMultiplier: m));

        public void Open(Spawn s, double seconds = Dt)
        {
            var feet = new Vec3(s.X + 1.5, s.Y, s.Z + 0.5);
            var view = new PlayerView(feet, feet + new Vec3(0, 1.6, 0), new Vec3(0, 0, -1), false, false);
            Items.Tick(Dt, view, true);
            for (double t = Dt; t < seconds; t += Dt) Items.Tick(Dt, view, false);
        }

        public ChestOpened Opened => Assert.Single(Events.OfType<ChestOpened>());
    }

    // ---- the table ----------------------------------------------------------------------------

    [Fact]
    public void LootIsAPureFunctionOfTheSeedAndTheChest()
    {
        var chests = Grid(1, 300).ToList();
        var forward = chests.Select(c => LootTable.Roll(Seed, c)).ToList();
        var backward = Enumerable.Reverse(chests).Select(c => LootTable.Roll(Seed, c)).Reverse().ToList();
        Assert.Equal(forward, backward);
        Assert.Equal(forward, chests.Select(c => LootTable.Roll(Seed, c)).ToList());
        Assert.NotEqual(forward, chests.Select(c => LootTable.Roll(Seed + 1, c)).ToList());
        Assert.True(forward.Select(l => l.Kind).Distinct().Count() > 3, "chests differ from one another");
    }

    [Fact]
    public void AChestHoldsTheSameLootWhetherOrNotItWasOpened()
    {
        var s = Chest(10, 10, 1);
        var rig = new Rig(s);
        var before = rig.Items.Chests[0].Loot;
        rig.Open(s);
        Assert.Equal(before, rig.Items.Chests[0].Loot);
        Assert.Equal(before, rig.Opened.Loot);
        Assert.Equal(before, LootTable.Roll(Seed, s));
    }

    [Theory]
    [InlineData(1, 0, LootRarity.Common)]
    [InlineData(2, 0, LootRarity.Rare)]
    [InlineData(3, 0, LootRarity.Epic)]
    [InlineData(2, 1, LootRarity.Common)]
    [InlineData(3, 1, LootRarity.Rare)]
    [InlineData(4, 1, LootRarity.Epic)]
    public void RarityFollowsTheChestsTierForItsFloor(int tier, int floor, LootRarity expected)
    {
        var s = Chest(1, 1, tier, floor);
        Assert.Equal(expected, LootTable.RarityOf(s));
        Assert.Equal(expected, LootTable.Roll(Seed, s).Rarity);
        Assert.Equal(expected != LootRarity.Common, new ChestState(0, s).Locked);
    }

    [Fact]
    public void LockedChestsNeverGiveBackTheKeyTheyCost()
    {
        foreach (int tier in new[] { 2, 3 })
            Assert.DoesNotContain(Grid(tier).Select(c => LootTable.Roll(Seed, c)), l => l.Kind == LootKind.Key);
    }

    [Theory]
    [InlineData(LootRarity.Common, 1)]
    [InlineData(LootRarity.Rare, 2)]
    [InlineData(LootRarity.Epic, 3)]
    public void ManyChestsFollowTheTableWeights(LootRarity rarity, int tier)
    {
        const int count = 6000;
        var tally = Grid(tier, count).GroupBy(c => LootTable.Roll(Seed, c).Kind).ToDictionary(g => g.Key, g => g.Count());
        double total = LootTable.AllKinds.Sum(k => LootTable.Weight(rarity, k));
        foreach (var kind in LootTable.AllKinds)
        {
            double expected = LootTable.Weight(rarity, kind) / total;
            double seen = tally.GetValueOrDefault(kind) / (double)count;
            Assert.InRange(seen, expected - 0.03, expected + 0.03);
            if (expected == 0) Assert.Equal(0, tally.GetValueOrDefault(kind));
            else Assert.True(tally.ContainsKey(kind), $"{kind} never rolled at {rarity}");
        }
    }

    [Fact]
    public void BetterChestsGiveStrongerAndLongerEffects()
    {
        foreach (var kind in LootTable.AllKinds.Where(k => k != LootKind.Key && k != LootKind.Cleanse))
        {
            var common = LootTable.Make(kind, LootRarity.Common);
            var rare = LootTable.Make(kind, LootRarity.Rare);
            var epic = LootTable.Make(kind, LootRarity.Epic);
            Assert.True(common.Amount < rare.Amount && rare.Amount < epic.Amount, $"{kind} amount grows with rarity");
            Assert.True(common.Seconds <= rare.Seconds && rare.Seconds <= epic.Seconds, $"{kind} lasts longer with rarity");
        }
        Assert.Equal(Interactables.ChestHeal, LootTable.Make(LootKind.Health, LootRarity.Common).Amount);
        Assert.Equal(Interactables.LockedChestHeal, LootTable.Make(LootKind.Health, LootRarity.Rare).Amount);
    }

    [Fact]
    public void BetterChestsRollPerksAndStyleMoreOften()
    {
        double Share(int tier) => Grid(tier, 4000).Count(c => LootTable.Roll(Seed, c).Kind is LootKind.Bloodlust or LootKind.WideParry or LootKind.StyleBoost) / 4000.0;
        Assert.True(Share(1) < Share(2) && Share(2) < Share(3));
    }

    [Fact]
    public void EveryKindHasANameAndABannerLine()
    {
        Assert.Equal(LootTable.AllKinds.Count, LootTable.AllKinds.Select(LootTable.Name).Distinct().Count());
        foreach (var kind in LootTable.AllKinds)
        {
            var text = LootTable.Announce(LootTable.Make(kind, LootRarity.Rare), 12);
            Assert.Contains(LootTable.Name(kind), text);
        }
        Assert.Contains("+30 health", LootTable.Announce(LootTable.Make(LootKind.Health, LootRarity.Common), 30));
    }

    // ---- every outcome applies ----------------------------------------------------------------

    [Fact]
    public void HealthLootHealsByItsAmountUpToTheCeiling()
    {
        var s = ChestHolding(LootKind.Health, 1);
        var rig = new Rig(s);
        rig.Vitals.Damage(60);
        double before = rig.Vitals.Health;
        rig.Open(s);
        var e = rig.Opened;
        Assert.Equal(LootKind.Health, e.Loot!.Value.Kind);
        Assert.Equal(e.Healed, rig.Vitals.Health - before, 6);
        Assert.Equal(Math.Min(30, rig.Vitals.Ceiling - before), e.Healed, 6);

        var locked = ChestHolding(LootKind.Health, 2);
        var rig2 = new Rig(locked, Key(30));
        rig2.Vitals.Damage(90);
        double health = rig2.Vitals.Health;
        rig2.Open(Key(30));
        rig2.Open(locked);
        Assert.True(rig2.Vitals.Health - health > 0);
        Assert.Equal(LootRarity.Rare, rig2.Opened.Loot!.Value.Rarity);
    }

    private static Spawn Key(int x) => new(SpawnKind.Key, x, 40, 10, 0, 1, 1);

    [Fact]
    public void CleanseLootClearsHardDamage()
    {
        var s = ChestHolding(LootKind.Cleanse, 1);
        var rig = new Rig(s);
        rig.Vitals.Damage(60);
        Assert.True(rig.Vitals.HardDamage > 0);
        double hard = rig.Vitals.HardDamage;
        rig.Open(s);
        Assert.Equal(0, rig.Vitals.HardDamage);
        Assert.Equal(100, rig.Vitals.Ceiling);
        Assert.Equal(hard, rig.Opened.Applied, 6);
        Assert.Equal(0, rig.Opened.Healed);
    }

    [Fact]
    public void StyleLootAddsStylePointsScaledByRarity()
    {
        double Gained(int tier)
        {
            var s = ChestHolding(LootKind.StyleBoost, tier);
            var rig = tier > 1 ? new Rig(s, Key(30)) : new Rig(s);
            if (tier > 1) rig.Open(Key(30));
            rig.Open(s);
            var e = rig.Events.OfType<ChestOpened>().Single();
            Assert.Equal(rig.Style.Points, e.Applied, 6);
            return rig.Style.Points;
        }
        double common = Gained(1), rare = Gained(2), epic = Gained(3);
        Assert.Equal(StyleMeter.BaseValue(StyleBonus.Parry) * 2, common, 3);
        Assert.True(common < rare && rare < epic);
    }

    [Fact]
    public void KeyLootGivesAnExtraKeyThatOpensALockedChest()
    {
        var s = ChestHolding(LootKind.Key, 1);
        var locked = Chest(60, 10, 2);
        var rig = new Rig(s, locked);
        rig.Open(s);
        Assert.Equal(1, rig.Items.KeysHeld);
        rig.Open(locked);
        Assert.Equal(0, rig.Items.KeysHeld);
        Assert.Equal(2, rig.Events.OfType<ChestOpened>().Count());
    }

    [Fact]
    public void BloodlustMultipliesBloodFromHitsThenWearsOff()
    {
        var s = ChestHolding(LootKind.Bloodlust, 1);
        var rig = new Rig(s);
        Assert.Equal(1, rig.Items.Perks.HealScale);
        rig.Open(s);
        var perk = rig.Opened.Loot!.Value;
        Assert.Equal(perk.Amount, rig.Items.Perks.HealScale);
        Assert.Equal(perk.Seconds, rig.Items.Perks.BloodlustLeft, 1);

        rig.Vitals.Damage(80);
        rig.Vitals.HitHealScale = rig.Items.Perks.HealScale;
        double scaled = rig.Vitals.HealFromHit(HitZone.Torso, 1);
        Assert.Equal(10 * perk.Amount, scaled, 6);

        rig.Open(s, perk.Seconds + 1); // already opened: just lets time pass
        Assert.Equal(1, rig.Items.Perks.HealScale);
        rig.Vitals.HitHealScale = rig.Items.Perks.HealScale;
        Assert.Equal(10, rig.Vitals.HealFromHit(HitZone.Torso, 1), 6);
    }

    [Fact]
    public void WideParryAddsToTheParryWindowThenWearsOff()
    {
        var s = ChestHolding(LootKind.WideParry, 1);
        var rig = new Rig(s);
        rig.Open(s);
        var perk = rig.Opened.Loot!.Value;
        Assert.Equal(perk.Amount, rig.Items.Perks.ParryWindowBonus);
        rig.Open(s, perk.Seconds + 1);
        Assert.Equal(0, rig.Items.Perks.ParryWindowBonus);
    }

    [Fact]
    public void RetakingAPerkRefreshesItToTheStrongerOfTheTwo()
    {
        var perks = new Perks();
        perks.Grant(LootTable.Make(LootKind.Bloodlust, LootRarity.Epic));
        perks.Tick(10);
        perks.Grant(LootTable.Make(LootKind.Bloodlust, LootRarity.Common));
        Assert.Equal(3, perks.HealScale);
        Assert.Equal(35, perks.BloodlustLeft, 6);
        perks.Clear();
        Assert.Equal(1, perks.HealScale);
        Assert.Equal(0, perks.ParryWindowBonus);
    }

    // ---- the hooks themselves -----------------------------------------------------------------

    [Fact]
    public void CleanseRemovesHardDamageAndReportsHowMuch()
    {
        var v = new PlayerVitals();
        Assert.Equal(0, v.Cleanse());
        v.Damage(50);
        double hard = v.HardDamage;
        Assert.Equal(hard, v.Cleanse());
        Assert.Equal(0, v.HardDamage);
        Assert.Equal(100, v.Ceiling);
        Assert.Equal(50, v.Heal(100)); // the whole gap can be healed now
    }

    [Fact]
    public void HitHealScaleIsClampedAndOnlyAffectsBloodFromHits()
    {
        var v = new PlayerVitals();
        v.Damage(90);
        v.HitHealScale = 99;
        Assert.Equal(PlayerVitals.MaxHitHealScale, v.HitHealScale);
        v.HitHealScale = 0.1;
        Assert.Equal(1, v.HitHealScale);
        v.HitHealScale = double.NaN;
        Assert.Equal(1, v.HitHealScale);
        v.HitHealScale = 2;
        Assert.Equal(20, v.HealFromHit(HitZone.Torso, 1));
        Assert.Equal(30, v.Heal(30));
        Assert.Equal(0, v.HealFromHit(HitZone.Head, PlayerVitals.BloodRange + 1));
    }

    // ---- through the simulation ---------------------------------------------------------------

    private static IEnumerable<(VoxelWorld World, DungeonSite Site, Spawn Chest)> RealChests()
    {
        for (long seed = 1; seed < 40; seed++)
        {
            var world = new VoxelWorld(seed);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is { } site)
                        foreach (var s in site.Spawns.Where(s => s.Kind == SpawnKind.Loot && s.Tier < s.Floor + 2))
                            yield return (world, site, s);
        }
    }

    private static (CombatSim Sim, PlayerView View) Near(VoxelWorld world, DungeonSite site, Spawn chest)
    {
        var sim = new CombatSim(world, site);
        var feet = new Vec3(chest.X + 1.5, chest.Y, chest.Z + 0.5);
        return (sim, new PlayerView(feet, feet + new Vec3(0, 1.6, 0), new Vec3(0, 0, -1), false, false));
    }

    [Fact]
    public void ARealChestGivesTheLootTheTableRolledForIt()
    {
        var (world, site, chest) = RealChests().First();
        var (sim, view) = Near(world, site, chest);
        sim.Tick(Dt, view, new PlayerActions(false, false, false, Interact: true));
        var opened = sim.DrainEvents().OfType<ChestOpened>().Single();
        Assert.Equal(LootTable.Roll(world.Seed, chest), opened.Loot);
    }

    [Fact]
    public void ACleanseChestInTheSimulationClearsHardDamage()
    {
        var (world, site, chest) = RealChests().First(c => LootTable.Roll(c.World.Seed, c.Chest).Kind == LootKind.Cleanse);
        var (sim, view) = Near(world, site, chest);
        sim.Vitals.Damage(40);
        Assert.True(sim.Vitals.HardDamage > 0);
        sim.Tick(Dt, view, new PlayerActions(false, false, false, Interact: true));
        Assert.Equal(0, sim.Vitals.HardDamage);
    }

    [Fact]
    public void AStyleChestInTheSimulationAddsStyle()
    {
        var (world, site, chest) = RealChests().First(c => LootTable.Roll(c.World.Seed, c.Chest).Kind == LootKind.StyleBoost);
        var (sim, view) = Near(world, site, chest);
        sim.Tick(Dt, view, new PlayerActions(false, false, false, Interact: true));
        Assert.True(sim.Style.Points > 100);
        Assert.True(sim.Style.TotalScore > 100);
    }

    [Fact]
    public void PerksReachTheSimulationThroughTheirHooks()
    {
        var (world, site, chest) = RealChests().First();
        var sim = new CombatSim(world, site);
        var view = new PlayerView(new Vec3(chest.X, chest.Y, chest.Z), new Vec3(chest.X, chest.Y + 1.6, chest.Z), new Vec3(0, 0, -1), false, false);

        sim.Items.Perks.Grant(LootTable.Make(LootKind.WideParry, LootRarity.Epic));
        sim.Items.Perks.Grant(LootTable.Make(LootKind.Bloodlust, LootRarity.Rare));
        sim.Tick(Dt, view, new PlayerActions(false, false, true));
        Assert.Equal(CombatSim.ParryWindowSeconds + 0.28 - Dt, sim.ParryWindowLeft, 6);
        Assert.Equal(2, sim.Vitals.HitHealScale);

        var plain = new CombatSim(world, site);
        plain.Tick(Dt, view, new PlayerActions(false, false, true));
        Assert.Equal(CombatSim.ParryWindowSeconds - Dt, plain.ParryWindowLeft, 6);
        Assert.Equal(1, plain.Vitals.HitHealScale);
    }

    // ---- the pickup models --------------------------------------------------------------------

    [Fact]
    public void EveryKindOfLootHasAPickupModelThatGlowsAndIsSmall()
    {
        foreach (var kind in LootTable.AllKinds)
        {
            var model = ModelCatalog.ForLoot(kind);
            Assert.True(model.Parts.Sum(p => p.Glow.TriangleCount) > 0 || kind == LootKind.Key, $"{kind} glows");
            Assert.InRange(model.Width, 0.08, 0.5);
            Assert.InRange(model.Height, 0.1, 0.6);
        }
        var pickups = LootTable.AllKinds.Where(k => k != LootKind.Key).Select(ModelCatalog.ForLoot).ToList();
        Assert.Equal(pickups.Count, pickups.Select(m => m.Id).Distinct().Count());
        Assert.All(pickups, m => Assert.StartsWith("loot_", m.Id));
        Assert.Same(ModelCatalog.Key, ModelCatalog.ForLoot(LootKind.Key));
    }

    [Fact]
    public void PickupModelsAreSmallEnoughToFloatOutOfAChest()
    {
        foreach (var kind in LootTable.AllKinds.Where(k => k != LootKind.Key))
        {
            var m = ModelCatalog.ForLoot(kind);
            Assert.True(m.TriangleCount <= 120, $"{m.Id} has {m.TriangleCount} triangles");
            Assert.True(m.Min.Y >= -0.01, $"{m.Id} sits on the ground");
        }
    }
}
