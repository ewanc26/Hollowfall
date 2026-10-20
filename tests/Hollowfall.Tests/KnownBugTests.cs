using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Input;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

/// <summary>
/// Evidence for bugs found by review. Each test states the behaviour that SHOULD hold and is skipped while the bug stands,
/// so CI stays green and the proof is in the repository. When a bug is fixed, delete its Skip and the test becomes a
/// regression test.
/// </summary>
public class KnownBugTests
{
    private const double Dt = 1.0 / 60;

    private static (VoxelWorld World, DungeonSite Site) FindSite()
    {
        for (long seed = 1; seed < 40; seed++)
        {
            var world = new VoxelWorld(seed);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is { FloorCount: >= 2 } site)
                        return (world, site);
        }
        throw new InvalidOperationException("no dungeon found");
    }

    private static PlayerView Stand(double x, double y, double z) =>
        new(new Vec3(x, y, z), new Vec3(x, y + 1.62, z), new Vec3(0, 0, -1), false, false);

    // ---- JSON loaders ------------------------------------------------------------------------

    [Fact(Skip = "BUG: GameSettings.FromJson throws ArgumentException on a duplicated property name, although it promises never to throw (Bindings/GameSettings.cs: JsonNode.Parse only caught as JsonException)")]
    public void SettingsWithADuplicatedKeyStillLoad()
    {
        var s = GameSettings.FromJson("{\"masterVolume\":0.5,\"masterVolume\":0.2}");
        Assert.InRange(s.MasterVolume, 0, 1);
    }

    [Fact(Skip = "BUG: RunRecords.FromJson throws ArgumentException on a duplicated property name, so a hand-edited records.json stops the game starting (Main._Ready calls RunRecords.Load)")]
    public void RecordsWithADuplicatedSeedStillLoad()
    {
        var r = RunRecords.FromJson("{\"seeds\":{\"a\":{\"0\":{\"grade\":\"S\",\"points\":14,\"seconds\":5}},\"a\":{}}}");
        Assert.NotNull(r);
    }

    // ---- bindings ----------------------------------------------------------------------------

    [Fact(Skip = "BUG: rebinding any action onto a stick axis used by Look reports Swapped but silently unbinds the Look action (BindingMap.Rebind: the displaced action cannot take the old button, so its slot is nulled)")]
    public void RebindingOntoALookAxisDoesNotLeaveLookUnbound()
    {
        var map = BindingMap.CreateDefault();
        var outcome = map.Rebind(GameAction.Jump, Binding.Axis("RightX", -1));
        Assert.Equal(RebindOutcome.Swapped, outcome);
        Assert.NotNull(map.Get(GameAction.LookLeft, Slot.Gamepad));
    }

    // ---- run records -------------------------------------------------------------------------

    [Fact(Skip = "BUG: RunRecords evicts by first insertion, not recency, so improving a record on an old seed does not protect it from being dropped once 100 other seeds exist")]
    public void ImprovingARecordKeepsItsSeedFromBeingEvicted()
    {
        var weak = new FloorResult(Grade.D, Grade.D, Grade.D, 0, 3, OverallGrade.D);
        var strong = new FloorResult(Grade.S, Grade.S, Grade.S, 0, 15, OverallGrade.P);
        var r = new RunRecords();
        r.Submit("keep", 0, weak, 100);
        for (int i = 0; i < RunRecords.MaxSeeds - 1; i++) r.Submit($"other-{i}", 0, weak, 100);
        Assert.True(r.Submit("keep", 0, strong, 50).IsBest); // just played and beaten
        r.Submit("newcomer", 0, weak, 100);
        Assert.NotNull(r.Best("keep", 0));
    }

    // ---- interactables -----------------------------------------------------------------------

    [Fact(Skip = "BUG: a spike trap hurts a player standing directly below it on a lower floor (Interactables.Tick checks feet.Y < s.Y + 0.6 with no lower bound; floors are stacked 8 blocks apart)")]
    public void ATrapDoesNotHurtAPlayerOnTheFloorBelow()
    {
        double damaged = 0;
        var items = new Interactables([new Spawn(SpawnKind.Trap, 10, 40, 10, 0, 1, 1)], 7, d => { damaged += d; return d; }, d => d, _ => { });
        var below = Stand(10.5, 32, 10.5); // one floor down, same x and z
        for (int i = 0; i < 400; i++) items.Tick(Dt, below, false);
        Assert.Equal(0, damaged);
    }

    [Fact(Skip = "BUG: a trap marks itself as having struck even when the player took no damage (parry invulnerability), so standing on fully extended spikes costs nothing (Interactables.Tick adds to _struck before reading what _hurt returned)")]
    public void ATrapStrikesAgainOnceInvulnerabilityEnds()
    {
        double damaged = 0, clock = 0;
        const double Spared = 0.3; // seconds of invulnerability, as after a parry
        double struckAt = -1;
        var items = new Interactables([new Spawn(SpawnKind.Trap, 10, 40, 10, 0, 1, 1)], 7, d =>
        {
            if (struckAt < 0) struckAt = clock;
            if (clock - struckAt < Spared) return 0;
            damaged += d;
            return d;
        }, d => d, _ => { });
        var on = Stand(10.5, 40, 10.5);
        // Only the first strike: it lasts 0.6 s, so stop shortly after it ends.
        for (int i = 0; i < 400 && (struckAt < 0 || clock - struckAt < Interactables.TrapStrike - 0.1); i++) { clock += Dt; items.Tick(Dt, on, false); }
        Assert.True(damaged > 0, "the player stood on the spikes for the whole strike and never took damage");
    }

    [Fact(Skip = "BUG: dungeons plan more locked chests (treasure and boss rooms) than keys (one per floor except the last), so some chests can never be opened (SpawnPlanner.Plan / Interactables.Open)")]
    public void EveryLockedChestCanBeOpened()
    {
        var (_, site) = FindSite();
        int locked = site.Spawns.Count(s => s.Kind == SpawnKind.Loot && s.Tier >= s.Floor + 2);
        int keys = site.Spawns.Count(s => s.Kind == SpawnKind.Key);
        Assert.True(keys >= locked, $"{locked} locked chests but {keys} keys");
    }

    // ---- combat sim --------------------------------------------------------------------------

    [Fact(Skip = "BUG: after a respawn the floor's style baseline is stale, so the floor's style grade goes negative (CombatSim.Respawn resets StyleMeter.TotalScore but FloorProgress.StartStyle is kept)")]
    public void StyleEarnedAfterARespawnStillCountsTowardTheFloorGrade()
    {
        var (world, site) = FindSite();
        var sim = new CombatSim(world, site);

        void Earn() { for (int i = 0; i < 40; i++) sim.Style.Award(StyleBonus.Chargeback); }

        var surface = Stand(site.EntranceX + 0.5, site.SurfaceY + 1, site.EntranceZ + 0.5);
        var floor0 = Stand(site.OriginX + 1.5, DungeonSite.FloorY(0) + 1, site.OriginZ + 1.5);
        var floor1 = Stand(site.OriginX + 1.5, DungeonSite.FloorY(1) + 1, site.OriginZ + 1.5);

        sim.Tick(Dt, surface, default);
        Earn();                       // style gained before the floor starts becomes its baseline
        sim.Tick(Dt, floor0, default);
        sim.Respawn();                // the meter restarts from zero...
        Earn();                       // ...and then plenty is earned
        sim.Tick(Dt, floor1, default);

        var done = sim.DrainEvents().OfType<FloorCompleted>().Single();
        Assert.Equal(Grade.S, done.Result.Style);
    }
}
