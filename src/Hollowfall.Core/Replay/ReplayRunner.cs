using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Replay;

/// <summary>What replaying a run produced.</summary>
/// <param name="Hash">A fingerprint of the final simulation state.</param>
/// <param name="Ticks">How many steps were played.</param>
/// <param name="Completed">The last <c>FloorCompleted</c> the replay raised on its final step, if any.</param>
public readonly record struct ReplayOutcome(ulong Hash, int Ticks, FloorCompleted? Completed);

/// <summary>Plays a recorded run back through <see cref="CombatSim"/> with no engine, and fingerprints the state it ends in.</summary>
public static class ReplayRunner
{
    /// <summary>
    /// Plays every step of <paramref name="replay"/> on <paramref name="sim"/>, which must be fresh (built from the replay's seed
    /// and site and not yet ticked). <paramref name="onTick"/> sees the simulation after each step.
    /// </summary>
    public static ReplayOutcome Play(RunReplay replay, CombatSim sim, DungeonSite site, Action<int, CombatSim>? onTick = null)
    {
        FloorCompleted? completed = null;
        for (int i = 0; i < replay.Frames.Count; i++)
        {
            var f = replay.Frames[i];
            for (int k = 0; k < f.Respawns; k++) sim.Respawn();
            sim.Tick(RunReplay.Dt, f.ToView(), f.ToActions());

            completed = null;
            foreach (var e in sim.DrainEvents()) if (e is FloorCompleted fc) completed = fc;
            sim.TakeDirtyChunks();
            onTick?.Invoke(i, sim);
        }
        return new ReplayOutcome(StateHash(sim, site.FloorCount, replay.Frames.Count), replay.Frames.Count, completed);
    }

    /// <summary>
    /// Re-simulates a replay on a fresh sim and checks it really completed its floor on its last step with the grade it claims.
    /// This is what makes a stored run a verified one. False if the world or site does not match, or the rules have changed.
    /// </summary>
    public static bool Verify(RunReplay replay, VoxelWorld world, out ReplayOutcome outcome)
    {
        outcome = default;
        if (!replay.IsVerifiable || world.Seed != Terrain.TerrainGenerator.SeedFromString(replay.Seed)) return false;
        if (world.Generator.Dungeons?.GetSite(replay.RegionX, replay.RegionZ) is not { } site || replay.Floor >= site.FloorCount) return false;

        // The game plays with the relationship system on, so the re-simulation does too (creatures use ids and can be shot).
        var sim = new CombatSim(world, site);
        sim.EnableRelations();
        outcome = Play(replay, sim, site);
        return outcome.Completed is { } c && c.Floor == replay.Floor && c.Result.Overall == replay.Result.Grade && c.Result.Points == replay.Result.Points &&
               Math.Abs(c.Seconds - replay.Result.Seconds) < 0.0015 && replay.EndTick == outcome.Ticks - 1;
    }

    // ---- state fingerprint ------------------------------------------------------------------

    /// <summary>
    /// FNV-1a over the parts of the simulation that make a run what it was: time (as a tick count), vitals, style, floor progress,
    /// keys, portals, every enemy and projectile. Numbers are rounded to 1e-4 first so the value is the same on every machine.
    /// </summary>
    public static ulong StateHash(CombatSim sim, int floorCount, int ticks)
    {
        var h = new Fnv();
        h.Add(ticks);
        h.Add(sim.CurrentFloor);
        h.Add(sim.Dead);
        h.Add(sim.Vitals.Health);
        h.Add(sim.Vitals.HardDamage);
        h.Add(sim.Style.Points);
        h.Add(sim.Style.TotalScore);
        h.Add((int)sim.Style.Rank);
        h.Add(sim.Items.KeysHeld);
        h.Add(sim.Portals.Teal != null);
        h.Add(sim.Portals.Violet != null);
        for (int f = 0; f < floorCount; f++)
        {
            h.Add(sim.KillsOn(f));
            h.Add(sim.FloorCompleted(f));
            h.Add(sim.ElapsedOn(f));
        }
        h.Add(sim.Enemies.Count);
        foreach (var e in sim.Enemies)
        {
            h.Add(e.Id);
            h.Add((int)e.Kind);
            h.Add((int)e.State);
            h.Add(e.Health);
            h.Add(e.Feet);
        }
        h.Add(sim.Projectiles.Count);
        foreach (var p in sim.Projectiles)
        {
            h.Add(p.Id);
            h.Add(p.Position);
            h.Add(p.Velocity);
            h.Add(p.Damage);
            h.Add(p.Reflected);
        }
        return h.Value;
    }

    private struct Fnv
    {
        public ulong Value = 1469598103934665603UL;

        public Fnv() { }

        public void Add(long v)
        {
            for (int i = 0; i < 8; i++)
            {
                Value ^= (byte)(v >> (8 * i));
                Value *= 1099511628211UL;
            }
        }

        public void Add(int v) => Add((long)v);

        public void Add(bool v) => Add(v ? 1L : 0L);

        public void Add(double v) => Add(double.IsFinite(v) ? (long)Math.Round(v * 1e4) : long.MinValue);

        public void Add(Vec3 v)
        {
            Add(v.X);
            Add(v.Y);
            Add(v.Z);
        }
    }
}
