using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Replay;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class ReplayTests
{
    private const double Dt = 1.0 / 60;

    /// <summary>A seed whose dungeon has two floors and a monster room on the first.</summary>
    private sealed record Scenario(string SeedName, int RegionX, int RegionZ)
    {
        public long Seed => TerrainGenerator.SeedFromString(SeedName);

        /// <summary>Every sim gets a world of its own: the sim edits blocks (doors), so a shared one would race.</summary>
        public (VoxelWorld World, DungeonSite Site) Build()
        {
            var world = new VoxelWorld(Seed);
            return (world, world.Generator.Dungeons!.GetSite(RegionX, RegionZ)!);
        }
    }

    private static readonly Lazy<Scenario> Found = new(() =>
    {
        for (long s = 1; s < 60; s++)
        {
            var world = new VoxelWorld(s);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is { FloorCount: >= 2 } site && site.Spawns.Any(sp => sp.Floor == 0 && sp.Kind == SpawnKind.Monster))
                        return new Scenario(s.ToString(), rx, rz);
        }
        throw new InvalidOperationException("no suitable dungeon found");
    });

    private const int BotTicks = 720;
    private const int RespawnTick = 500;
    private const int DescendTick = 600;

    private sealed record BotRun(ReplayRecorder Recorder, ulong LiveHash, List<CombatEvent> Events, RunReplay? Replay);

    /// <summary>
    /// A deterministic bot: it stands in the first monster room, aims at the nearest enemy and fires, parries and uses portals on a
    /// rhythm, dies-and-respawns once, then walks down to floor 1. It reads the sim, but only to choose what to feed it.
    /// </summary>
    private static BotRun RunBot(Scenario sc, bool record = true)
    {
        var (world, site) = sc.Build();
        var sim = new CombatSim(world, site);
        var rec = new ReplayRecorder();
        var events = new List<CombatEvent>();
        var room = site.Spawns.First(sp => sp.Floor == 0 && sp.Kind == SpawnKind.Monster);
        var floor1 = site.Floors[1];
        var down = site.ToWorld(1, floor1.EntranceX, floor1.EntranceZ);
        var fixedBase = new Vec3(room.X + 0.5, room.Y, room.Z + 0.5);
        RunReplay? replay = null;
        int ticks = 0;

        for (int t = 0; t < BotTicks && replay == null; t++)
        {
            ticks = t + 1;
            if (t == RespawnTick || sim.Dead)
            {
                sim.Respawn();
                rec.NoteRespawn();
            }

            var feet = t >= DescendTick ? new Vec3(down.X + 0.5, down.Y, down.Z + 0.5) : fixedBase + new Vec3(0.6 * Math.Sin(t / 25.0), 0, 0.6 * Math.Cos(t / 31.0));
            var eye = feet + new Vec3(0, 1.62, 0);
            var target = sim.Enemies.Where(e => !e.Dead).OrderBy(e => e.Feet.DistanceTo(feet)).FirstOrDefault();
            var forward = target != null ? (target.Feet + new Vec3(0, 1.2, 0) - eye).Normalized() : new Vec3(Math.Cos(t / 40.0), -0.1, Math.Sin(t / 40.0)).Normalized();
            var view = new PlayerView(feet, eye, forward, t % 90 > 70, t % 120 > 100);
            var actions = new PlayerActions(target != null && t % 17 == 0, target != null && t % 50 == 0, t % 45 == 0, t == 20, t == 40, t % 90 == 0);

            (view, actions) = rec.Capture(view, actions);
            sim.Tick(Dt, view, actions);
            foreach (var e in sim.DrainEvents())
            {
                events.Add(e);
                // The floor is done on this very tick, so the run so far is the replay.
                if (e is FloorCompleted c) replay = rec.Finish(sc.SeedName, sc.RegionX, sc.RegionZ, c.Floor, new FloorRecord(c.Result.Overall, c.Result.Points, c.Seconds));
            }
        }

        return new BotRun(rec, ReplayRunner.StateHash(sim, site.FloorCount, ticks), events, replay);
    }

    private static ulong ReplayHash(Scenario sc, RunReplay replay)
    {
        var (world, site) = sc.Build();
        return ReplayRunner.Play(replay, new CombatSim(world, site), site).Hash;
    }

    private static RunReplay Roundtrip(RunReplay r)
    {
        Assert.True(ReplayCodec.TryDecode(ReplayCodec.Encode(r), out var decoded));
        return decoded!;
    }

    // ---- the bot actually does something ----------------------------------------------------

    [Fact]
    public void TheBotTriggersACombatEncounterAndCompletesAFloor()
    {
        var run = RunBot(Found.Value);
        Assert.Contains(run.Events, e => e is EncounterStarted);
        Assert.Contains(run.Events, e => e is ShotFired);
        Assert.Contains(run.Events, e => e is FloorCompleted { Floor: 0 });
        Assert.NotNull(run.Replay);
        Assert.Equal(DescendTick + 1, run.Replay!.TickCount);
        Assert.Equal(DescendTick, run.Replay.EndTick);
    }

    // ---- recording and the format -----------------------------------------------------------

    [Fact]
    public void QuantisedFramesAreStableAndSurviveTheirOwnView()
    {
        var view = new PlayerView(new Vec3(12.3456789, -40.1, 7.77), new Vec3(12.3456789, -38.48, 7.77), new Vec3(0.3, -0.5, 0.8), true, false);
        var frame = ReplayFrame.From(view, new PlayerActions(true, false, true, Interact: true));
        var again = ReplayFrame.From(frame.ToView(), frame.ToActions());
        Assert.Equal(frame, again);
        Assert.True(frame.InRange);
        Assert.Equal(12.3456789, frame.Feet.X, 3);
        Assert.Equal(1.0, frame.ToView().Forward.Length, 9);
        Assert.True(frame.ToActions().FireRevolver && frame.ToActions().Parry && frame.ToActions().Interact && !frame.ToActions().FireShotgun);
    }

    [Fact]
    public void NonFiniteAndHugeValuesBecomeSafeFrames()
    {
        var bad = new PlayerView(new Vec3(double.NaN, double.PositiveInfinity, 1e300), new Vec3(double.NaN, 0, 0), new Vec3(0, 0, 0), false, false);
        var frame = ReplayFrame.From(bad, default);
        Assert.True(frame.InRange);
        var rec = new ReplayRecorder();
        var (view, _) = rec.Capture(bad, default);
        Assert.True(double.IsFinite(view.Feet.X) && double.IsFinite(view.Forward.Z) && Math.Abs(view.Forward.Length - 1) < 1e-9);
    }

    [Fact]
    public void AReplayRoundTripsThroughTheFileFormat()
    {
        var run = RunBot(Found.Value);
        var decoded = Roundtrip(run.Replay!);
        Assert.Equal(run.Replay!.Frames, decoded.Frames);
        Assert.Equal(run.Replay.Seed, decoded.Seed);
        Assert.Equal((run.Replay.RegionX, run.Replay.RegionZ, run.Replay.Floor, run.Replay.EndTick), (decoded.RegionX, decoded.RegionZ, decoded.Floor, decoded.EndTick));
        Assert.Equal(run.Replay.Result.Grade, decoded.Result.Grade);
        Assert.Equal(run.Replay.Result.Points, decoded.Result.Points);
        Assert.Equal(run.Replay.Result.Seconds, decoded.Result.Seconds, 3);
        Assert.Equal(RunReplay.SimVersion, decoded.SimVersionRecorded);
        Assert.Contains(decoded.Frames, f => f.Respawns >= 1);
    }

    [Fact]
    public void EncodedBytesAreFixedForAScriptedStream()
    {
        // Integers only, no simulation: this pins the file format itself, on every machine.
        var frames = new List<ReplayFrame>();
        for (int t = 0; t < 300; t++)
        {
            int flags = (t % 40 > 30 ? ReplayFrame.AirborneBit : 0) | (t % 17 == 0 ? ReplayFrame.RevolverBit : 0) | (t % 45 == 0 ? ReplayFrame.ParryBit : 0);
            frames.Add(new ReplayFrame(102400 + t * 71, 20480, -51200 + t * t % 3001, 0, 1659, 0, t * 211 % 30011 - 15000, t % 7 * 100, 32767 - t * 97 % 5000, flags, t == 150 ? 1 : 0));
        }
        var bytes = ReplayCodec.Encode(new RunReplay("golden", 3, -2, 1, 299, new FloorRecord(OverallGrade.B, 9, 4.5), frames));
        Assert.Equal(ExpectedGoldenLength, bytes.Length);
        Assert.Equal(ExpectedGoldenChecksum, ReplayCodec.Fnv32(bytes));
    }

    private const int ExpectedGoldenLength = 4212;
    private const uint ExpectedGoldenChecksum = 2997968840; // changed on purpose: the sim version byte went up to 11 for the floor collapse

    [Fact]
    public void FileSizesStaySane()
    {
        // A minute of sprinting, turning and shooting every tick is the worst case a player can produce.
        var frames = new List<ReplayFrame>();
        for (int t = 0; t < 3600; t++)
        {
            var feet = new Vec3(200 + 9 * Math.Sin(t / 90.0) * t / 400.0, 30, 300 + t * 0.09);
            var view = new PlayerView(feet, feet + new Vec3(0, 1.62, 0), new Vec3(Math.Cos(t / 7.0), Math.Sin(t / 11.0) * 0.3, Math.Sin(t / 7.0)), t % 50 > 40, false);
            frames.Add(ReplayFrame.From(view, new PlayerActions(t % 17 == 0, false, false)));
        }
        int size = ReplayCodec.Encode(new RunReplay("size", 0, 0, 0, 3599, new FloorRecord(OverallGrade.S, 14, 60), frames)).Length;
        Assert.InRange(size, 1, 60 * 1024); // far under a megabyte even over twenty minutes
        Assert.True(size * (RunReplay.MaxTicks / 3600) < ReplayCodec.MaxFileBytes);

        var idle = Enumerable.Repeat(frames[0], 3600).ToList();
        Assert.True(ReplayCodec.Encode(new RunReplay("idle", 0, 0, 0, 3599, new FloorRecord(OverallGrade.D, 0, 60), idle)).Length < 200); // standing still costs nothing
    }

    [Fact]
    public void ARunPastTheTickCapStopsRecordingAndIsNotSaved()
    {
        var rec = new ReplayRecorder();
        var view = new PlayerView(new Vec3(1, 2, 3), new Vec3(1, 3.6, 3), new Vec3(1, 0, 0), false, false);
        for (int i = 0; i < RunReplay.MaxTicks + 5; i++) rec.Capture(view, default);
        Assert.True(rec.Truncated);
        Assert.Equal(RunReplay.MaxTicks, rec.TickCount);
        Assert.Null(rec.Finish("seed", 0, 0, 0, new FloorRecord(OverallGrade.A, 10, 30)));
    }

    // ---- determinism ------------------------------------------------------------------------

    [Fact]
    public void TheSameSeedAndInputsGiveTheSameFinalState()
    {
        var first = RunBot(Found.Value);
        var second = RunBot(Found.Value);
        Assert.Equal(first.LiveHash, second.LiveHash);
        Assert.Equal(first.Replay!.Frames, second.Replay!.Frames);
        Assert.Equal(ReplayCodec.Encode(first.Replay), ReplayCodec.Encode(second.Replay));
    }

    [Fact]
    public void ReplayingTheDecodedFileReproducesTheLiveRun()
    {
        // Recording rounds the inputs, and the live game is fed the rounded values, so a replay is the run and not a copy of it.
        var sc = Found.Value;
        var live = RunBot(sc);
        var replay = Roundtrip(live.Replay!);
        Assert.Equal(live.LiveHash, ReplayHash(sc, replay));
        Assert.Equal(ReplayHash(sc, replay), ReplayHash(sc, replay));
    }

    [Fact]
    public void ReplayingInParallelThreadsGivesTheSameState()
    {
        var sc = Found.Value;
        var replay = Roundtrip(RunBot(sc).Replay!);
        ulong expected = ReplayHash(sc, replay);

        const int threads = 6;
        var hashes = new ulong[threads];
        using var barrier = new Barrier(threads);
        var workers = Enumerable.Range(0, threads).Select(i => new Thread(() =>
        {
            barrier.SignalAndWait(); // start together so the simulations really overlap
            hashes[i] = ReplayHash(sc, replay);
        })).ToList();
        workers.ForEach(w => w.Start());
        workers.ForEach(w => w.Join());
        Assert.All(hashes, h => Assert.Equal(expected, h));
    }

    [Fact]
    public void ARespawnIsPartOfTheRun()
    {
        var sc = Found.Value;
        var replay = Roundtrip(RunBot(sc).Replay!);
        var without = new RunReplay(replay.Seed, replay.RegionX, replay.RegionZ, replay.Floor, replay.EndTick, replay.Result, replay.Frames.Select(f => f with { Respawns = 0 }).ToList());
        Assert.NotEqual(ReplayHash(sc, replay), ReplayHash(sc, without));
    }

    [Fact]
    public void ChangedInputsChangeTheFinalState()
    {
        var sc = Found.Value;
        var replay = Roundtrip(RunBot(sc).Replay!);
        var silent = new RunReplay(replay.Seed, replay.RegionX, replay.RegionZ, replay.Floor, replay.EndTick, replay.Result, replay.Frames.Select(f => f with { Flags = f.Flags & (ReplayFrame.AirborneBit | ReplayFrame.SlidingBit) }).ToList());
        Assert.NotEqual(ReplayHash(sc, replay), ReplayHash(sc, silent));
    }

    [Fact]
    public void AStoredRunVerifiesAndAFakedOneDoesNot()
    {
        var sc = Found.Value;
        var replay = Roundtrip(RunBot(sc).Replay!);
        var (world, _) = sc.Build();
        Assert.True(ReplayRunner.Verify(replay, world, out var outcome));
        Assert.Equal(replay.TickCount, outcome.Ticks);

        // Claim a better grade than the inputs earn.
        var better = (OverallGrade)Math.Min((int)OverallGrade.S, (int)replay.Result.Grade + 1);
        var inflated = new RunReplay(replay.Seed, replay.RegionX, replay.RegionZ, replay.Floor, replay.EndTick, replay.Result with { Grade = better, Points = replay.Result.Points + 3 }, replay.Frames);
        Assert.False(ReplayRunner.Verify(inflated, sc.Build().World, out _));

        // A different seed's world is refused.
        Assert.False(ReplayRunner.Verify(replay, new VoxelWorld(Found.Value.Seed + 1), out _));

        // Rules from another version cannot be verified.
        var old = new RunReplay(replay.Seed, replay.RegionX, replay.RegionZ, replay.Floor, replay.EndTick, replay.Result, replay.Frames, RunReplay.SimVersion + 1);
        Assert.False(old.IsVerifiable);
        Assert.False(ReplayRunner.Verify(old, sc.Build().World, out _));
    }

    // ---- loading damaged data ---------------------------------------------------------------

    private static byte[] Sealed(List<byte> body)
    {
        uint sum = ReplayCodec.Fnv32(body);
        for (int i = 0; i < 4; i++) body.Add((byte)(sum >> (8 * i)));
        return [.. body];
    }

    [Fact]
    public void DamagedFilesNeverThrow()
    {
        var good = ReplayCodec.Encode(Roundtrip(RunBot(Found.Value).Replay!));
        Assert.True(ReplayCodec.TryDecode(good, out _));

        // Every truncation, with and without a repaired checksum.
        for (int len = 0; len < good.Length; len += Math.Max(1, len < 80 ? 1 : 7))
        {
            Assert.False(ReplayCodec.TryDecode(good.AsSpan(0, len), out _));
            if (len > 8) ReplayCodec.TryDecode(Sealed([.. good.Take(len)]), out _);
        }

        // Flipped bits: the checksum rejects them, and with the checksum repaired the parser still holds.
        var rng = new Random(5);
        for (int i = 0; i < 400; i++)
        {
            var bad = (byte[])good.Clone();
            bad[rng.Next(bad.Length)] ^= (byte)(1 << rng.Next(8));
            Assert.False(ReplayCodec.TryDecode(bad, out _));
            var body = bad.Take(bad.Length - 4).ToList();
            ReplayCodec.TryDecode(Sealed(body), out var decoded);
            if (decoded != null) Assert.All(decoded.Frames, f => Assert.True(f.InRange));
        }

        // Pure noise and empty input.
        for (int i = 0; i < 100; i++)
        {
            var noise = new byte[rng.Next(0, 300)];
            rng.NextBytes(noise);
            Assert.False(ReplayCodec.TryDecode(noise, out _));
            ReplayCodec.TryDecode(Sealed([.. noise]), out _);
        }
        Assert.False(ReplayCodec.TryDecode(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void HugeCountsAndOversizedFilesAreRejectedBeforeAllocating()
    {
        // A valid header that claims four billion steps and runs.
        var header = new List<byte> { (byte)'H', (byte)'F', (byte)'R', (byte)'P', RunReplay.FormatVersion, RunReplay.SimVersion, 1, (byte)'x', 0, 0, 0, 0, 0, 0, 0 };
        header.AddRange([0xFF, 0xFF, 0xFF, 0xFF, 0x0F]); // ticks
        header.AddRange([0xFF, 0xFF, 0xFF, 0xFF, 0x0F]); // runs
        Assert.False(ReplayCodec.TryDecode(Sealed(header), out _));

        // A varint that never ends.
        var endless = new List<byte> { (byte)'H', (byte)'F', (byte)'R', (byte)'P', RunReplay.FormatVersion, RunReplay.SimVersion };
        endless.AddRange(Enumerable.Repeat((byte)0xFF, 64));
        Assert.False(ReplayCodec.TryDecode(Sealed(endless), out _));

        Assert.False(ReplayCodec.TryDecode(new byte[ReplayCodec.MaxFileBytes + 1], out _));
    }

    // ---- the store --------------------------------------------------------------------------

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "hollowfall-replays-" + Guid.NewGuid().ToString("N"));

    private static RunReplay Tiny(string seed, int floor, OverallGrade grade, int points, double seconds, int moveX = 0)
    {
        var frames = Enumerable.Range(0, 120).Select(i =>
            ReplayFrame.From(new PlayerView(new Vec3(i * 0.05 + moveX, 10, 5), new Vec3(i * 0.05 + moveX, 11.62, 5), new Vec3(1, 0, 0), false, false), default)).ToList();
        return new RunReplay(seed, 1, 2, floor, 119, new FloorRecord(grade, points, seconds), frames);
    }

    [Fact]
    public void TheStoreKeepsOnlyTheBestRunPerSeedAndFloor()
    {
        var dir = TempDir();
        try
        {
            var store = new ReplayStore(dir);
            Assert.Null(store.Load("deep", 0));
            Assert.True(store.TrySaveIfBest(Tiny("deep", 0, OverallGrade.C, 6, 90)));
            Assert.False(store.TrySaveIfBest(Tiny("deep", 0, OverallGrade.D, 3, 40))); // worse grade, though faster
            Assert.False(store.TrySaveIfBest(Tiny("deep", 0, OverallGrade.C, 6, 90))); // a tie is not a record
            Assert.True(store.TrySaveIfBest(Tiny("deep", 0, OverallGrade.B, 9, 120)));
            Assert.True(store.TrySaveIfBest(Tiny("deep", 1, OverallGrade.D, 1, 200))); // another floor is its own slot
            Assert.True(store.TrySaveIfBest(Tiny("other", 0, OverallGrade.D, 1, 200))); // and so is another seed

            Assert.Equal(OverallGrade.B, store.Load("deep", 0)!.Result.Grade);
            Assert.Equal(OverallGrade.D, store.Load("deep", 1)!.Result.Grade);
            Assert.Equal(3, Directory.GetFiles(dir, "*.hfr").Length);
            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void TheStoreSitsBesideTheRecordsFile()
    {
        var store = ReplayStore.Beside(Path.Combine(TempDir(), "records.json"));
        Assert.Equal("replays", Path.GetFileName(store.Folder));
    }

    [Fact]
    public void LoadAllReturnsTheStoredFloorsOfOneSeedInOrder()
    {
        var dir = TempDir();
        try
        {
            var store = new ReplayStore(dir);
            Assert.True(store.TrySaveIfBest(Tiny("deep", 2, OverallGrade.B, 9, 120)));
            Assert.True(store.TrySaveIfBest(Tiny("deep", 0, OverallGrade.C, 6, 90)));
            Assert.True(store.TrySaveIfBest(Tiny("other", 1, OverallGrade.D, 1, 200)));

            Assert.Equal([0, 2], store.LoadAll("deep").Select(r => r.Floor));
            Assert.Empty(store.LoadAll("unseen"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void TheStoreIgnoresDamagedWrongAndOversizedFiles()
    {
        var dir = TempDir();
        try
        {
            var store = new ReplayStore(dir);
            Assert.True(store.TrySaveIfBest(Tiny("deep", 0, OverallGrade.C, 6, 90)));
            string path = store.PathFor("deep", 0);

            File.WriteAllBytes(path, [1, 2, 3, 4, 5]);
            Assert.Null(store.Load("deep", 0));

            File.WriteAllBytes(path, new byte[ReplayCodec.MaxFileBytes + 10]);
            Assert.Null(store.Load("deep", 0));

            // A genuine file filed under the wrong seed is not trusted.
            File.WriteAllBytes(path, ReplayCodec.Encode(Tiny("elsewhere", 0, OverallGrade.S, 14, 10)));
            Assert.Null(store.Load("deep", 0));

            // A damaged slot can be replaced by a fresh run.
            File.WriteAllBytes(path, [9, 9, 9]);
            Assert.True(store.TrySaveIfBest(Tiny("deep", 0, OverallGrade.D, 1, 99)));
            Assert.NotNull(store.Load("deep", 0));

            Assert.Null(store.Load("", 0));
            Assert.Null(store.Load("deep", -1));
            Assert.Null(store.Load("deep", 9999));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void AnUnwritableFolderFailsQuietly()
    {
        string file = Path.GetTempFileName();
        try
        {
            var store = new ReplayStore(Path.Combine(file, "inside")); // a file where the folder should be
            Assert.False(store.TrySaveIfBest(Tiny("deep", 0, OverallGrade.C, 6, 90)));
            Assert.Null(store.Load("deep", 0));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void TheStoreKeepsAtMostAFixedNumberOfFiles()
    {
        var dir = TempDir();
        try
        {
            var store = new ReplayStore(dir);
            for (int i = 0; i < ReplayStore.MaxEntries + 8; i++) Assert.True(store.TrySaveIfBest(Tiny("seed" + i, 0, OverallGrade.D, 1, 50, i)));
            Assert.Equal(ReplayStore.MaxEntries, Directory.GetFiles(dir, "*.hfr").Length);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ---- ghosts -----------------------------------------------------------------------------

    [Fact]
    public void AGhostFollowsTheRecordedFeetOverTheFloor()
    {
        var run = RunBot(Found.Value);
        var replay = run.Replay!;
        var ghost = new GhostTrack(replay);

        Assert.Equal(0, ghost.Floor);
        Assert.Equal(replay.EndTick - replay.StartTick + 1, ghost.Count);
        Assert.Equal(replay.Frames[replay.StartTick].Feet, ghost.FeetAt(0));
        Assert.Equal(replay.Frames[replay.EndTick].Feet, ghost.FeetAt(ghost.Count - 1));
        Assert.Equal(0, ghost.IndexAt(0));
        Assert.Equal(60, ghost.IndexAt(1.0));
        Assert.Null(ghost.IndexAt(ghost.Seconds + 5)); // it has finished: hide it
        Assert.Null(ghost.IndexAt(double.NaN));
        Assert.Null(ghost.IndexAt(-1));
    }

    [Fact]
    public void AGhostFacesWhereTheRunnerLooked()
    {
        var view = new PlayerView(new Vec3(0, 0, 0), new Vec3(0, 1.6, 0), new Vec3(1, 0, 0), false, false); // east
        var frames = Enumerable.Repeat(ReplayFrame.From(view, default), 10).ToList();
        var ghost = new GhostTrack(new RunReplay("x", 0, 0, 0, 9, new FloorRecord(OverallGrade.D, 0, 0.1), frames));
        Assert.Equal(-Math.PI / 2, ghost.YawAt(0), 3); // yaw 0 faces -Z, so east is a quarter turn clockwise
    }

    [Fact]
    public void AGhostStartsWhenTheFloorStartedNotWhenTheRunDid()
    {
        var run = RunBot(Found.Value);
        var replay = run.Replay!;
        Assert.Equal(replay.EndTick - (int)Math.Round(replay.Result.Seconds / Dt), replay.StartTick);
        Assert.InRange(replay.StartTick, 0, 2); // the bot starts on floor 0 at once
    }

    [Fact]
    public void MeteorAndSeverInputsSurviveQuantisingAndTheFile()
    {
        var view = new PlayerView(new Vec3(10, 20, 30), new Vec3(10, 21.6, 30), new Vec3(0, 0, -1), true, false, new Vec3(0, -25, 0), true);
        var actions = new PlayerActions(false, false, false, ClosePortals: true);
        var frame = ReplayFrame.From(view, actions);
        var seen = frame.ToView();
        Assert.True(seen.Crossed);
        Assert.Equal(25, seen.Velocity.Length, 1);
        Assert.True(frame.ToActions().ClosePortals);

        var plain = ReplayFrame.From(view with { Crossed = false, Velocity = default }, default);
        var replay = new RunReplay("meteor", 0, 0, 0, 2, new FloorRecord(OverallGrade.C, 5, 1), [plain, frame, plain]);
        Assert.True(ReplayCodec.TryDecode(ReplayCodec.Encode(replay), out var back));
        Assert.Equal(frame, back!.Frames[1]);
        Assert.False(back.Frames[0].ToView().Crossed);
    }
}
