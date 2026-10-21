using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Replay;
using Hollowfall.Core.World;
using Xunit;

namespace Hollowfall.Tests;

public class RunStatsTests
{
    private static (VoxelWorld World, DungeonSite Site) AnySite()
    {
        var world = new VoxelWorld(1);
        for (int rx = -2; rx <= 1; rx++)
            for (int rz = -2; rz <= 1; rz++)
                if (world.Generator.Dungeons!.GetSite(rx, rz) is { } site) return (world, site);
        throw new InvalidOperationException("the seed has no dungeon site");
    }

    [Fact]
    public void TheDefaultIsTheCharacterBeforeProgression()
    {
        var stats = new RunStats();
        Assert.Equal(PlayerVitals.MaxHealth, stats.MaxHealth);
        Assert.Equal(1, stats.DamageScale);
        Assert.Equal(new Progression().Stats, stats);
    }

    [Fact]
    public void StatsAreClampedToWhatCanBeEarned()
    {
        var stats = new RunStats(Vigour: 99, Might: -4);
        Assert.Equal(RunStats.MaxPoints, stats.ClampedVigour);
        Assert.Equal(0, stats.ClampedMight);
        Assert.Equal(PlayerVitals.MaxHealth + RunStats.MaxPoints * Progression.VigourPerPoint, stats.MaxHealth);
    }

    [Fact]
    public void VigourRaisesTheMaximumHealthOfTheRun()
    {
        var (world, site) = AnySite();
        var sim = new CombatSim(world, site, new RunStats(Vigour: 2));
        Assert.Equal(PlayerVitals.MaxHealth + 2 * Progression.VigourPerPoint, sim.Vitals.Max);
        Assert.Equal(sim.Vitals.Max, sim.Vitals.Health);
        Assert.Equal(new RunStats(Vigour: 2), sim.Stats);
    }

    [Fact]
    public void APlainRunKeepsTheBaseMaximum()
    {
        var (world, site) = AnySite();
        var sim = new CombatSim(world, site);
        Assert.Equal(PlayerVitals.MaxHealth, sim.Vitals.Max);
        Assert.Equal(PlayerVitals.MaxHealth, sim.Vitals.Health);
    }

    [Fact]
    public void TheStatsSurviveTheReplayFile()
    {
        var frames = new List<ReplayFrame> { new(102400, 20480, -51200, 0, 1659, 0, 1000, 0, 32767, 0, 0) };
        var replay = new RunReplay("stats", 0, 0, 0, 0, new FloorRecord(OverallGrade.C, 1, 1), frames) { Stats = new RunStats(Vigour: 3, Might: 5) };
        Assert.True(ReplayCodec.TryDecode(ReplayCodec.Encode(replay), out var back));
        Assert.Equal(new RunStats(Vigour: 3, Might: 5), back!.Stats);
    }

    [Fact]
    public void AReplayFromBeforeStatsLoadsAsAPlainCharacter()
    {
        var frames = new List<ReplayFrame> { new(102400, 20480, -51200, 0, 1659, 0, 1000, 0, 32767, 0, 0) };
        var replay = new RunReplay("old", 0, 0, 0, 0, new FloorRecord(OverallGrade.C, 1, 1), frames);
        byte[] v3 = ReplayCodec.Encode(replay);

        // Rebuild the same run in format 2: drop the two stat bytes after the sim version, set the version and re-checksum.
        var payload = new List<byte>(v3[..^4]);
        payload[4] = RunReplay.FormatVersionWithoutStats;
        payload.RemoveRange(6, 2);
        uint sum = ReplayCodec.Fnv32(payload);
        payload.AddRange(BitConverter.GetBytes(sum));

        Assert.True(ReplayCodec.TryDecode(payload.ToArray(), out var old));
        Assert.Equal(new RunStats(), old!.Stats);
        Assert.Equal("old", old.Seed);
    }
}
