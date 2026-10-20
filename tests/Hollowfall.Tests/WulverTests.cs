using System.Text.RegularExpressions;
using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Models;
using Hollowfall.Core.Story;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class WulverPlacerTests
{
    public static TheoryData<long> Seeds => [1, 2, 3, 5, 7, 11, 42, 99, 12345, 777];

    private static readonly Lazy<long> Voxel = new(() => TerrainGenerator.SeedFromString("voxel"));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheSameSeedGivesTheSameSpot(long seed)
    {
        var a = WulverPlacer.Place(new VoxelWorld(seed));
        var b = WulverPlacer.Place(new VoxelWorld(seed));
        Assert.Equal(a, b);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void HeStandsInTheBandFromTheEntranceOnSolidDryLevelGroundWithHeadroom(long seed)
    {
        var world = new VoxelWorld(seed);
        var spot = WulverPlacer.Place(world);
        Assert.NotNull(spot);
        var s = spot.Value;

        Assert.InRange(s.Distance, WulverPlacer.MinDistance, WulverPlacer.MaxDistance);
        var site = WulverPlacer.NearestSite(world.Generator);
        Assert.Equal(site?.EntranceX ?? 0, s.AnchorX);
        Assert.Equal(site?.EntranceZ ?? 0, s.AnchorZ);
        Assert.Equal(s.Distance, Math.Sqrt(Math.Pow(s.BlockX - s.AnchorX, 2) + Math.Pow(s.BlockZ - s.AnchorZ, 2)), 6);

        Assert.True(s.GroundY > TerrainGenerator.SeaLevel + 2, "above the water line");
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var ground = world.GetBlock(s.BlockX + dx, s.GroundY, s.BlockZ + dz);
                Assert.True(BlockInfo.IsSolid(ground), $"solid ground at {dx},{dz}, was {ground}");
                Assert.NotEqual(BlockType.Snow, ground);
                Assert.NotEqual(BlockType.Water, ground);
                for (int h = 1; h <= WulverPlacer.Headroom; h++)
                {
                    var above = world.GetBlock(s.BlockX + dx, s.GroundY + h, s.BlockZ + dz);
                    Assert.True(above == BlockType.Air || BlockInfo.IsPlant(above), $"headroom at {dx},{dz},{h}, was {above}");
                }
            }
        Assert.Equal(s.GroundY + 1, s.Y);
    }

    [Fact]
    public void DifferentSeedsDiffer()
    {
        var spots = new long[] { 1, 2, 3, 5, 7, 11, 42, 99, 12345, 777 }.Select(seed => WulverPlacer.Place(new VoxelWorld(seed))!.Value).Select(s => (s.BlockX, s.BlockZ)).Distinct().Count();
        Assert.True(spots >= 8, $"only {spots} distinct spots in 10 seeds");
    }

    [Fact]
    public void HeFacesTheEntrance()
    {
        var s = WulverPlacer.Place(new VoxelWorld(Voxel.Value))!.Value;
        // A model facing -Z turned by yaw faces (-sin, -cos).
        double fx = -Math.Sin(s.Yaw), fz = -Math.Cos(s.Yaw);
        double dx = s.AnchorX - s.BlockX, dz = s.AnchorZ - s.BlockZ, len = Math.Sqrt(dx * dx + dz * dz);
        Assert.Equal(1.0, fx * dx / len + fz * dz / len, 6);
    }

    [Fact]
    public void HeIsNeverInWaterAcrossManySeeds()
    {
        for (long seed = 100; seed < 130; seed++)
        {
            var world = new VoxelWorld(seed);
            if (WulverPlacer.Place(world) is not { } s) continue;
            Assert.NotEqual(BlockType.Water, world.GetBlock(s.BlockX, s.GroundY, s.BlockZ));
            Assert.NotEqual(BlockType.Water, world.GetBlock(s.BlockX, s.GroundY + 1, s.BlockZ));
        }
    }
}

public class WulverModelTests
{
    private static LowPolyModel M => ModelCatalog.Wulver;

    [Fact]
    public void HasTheRiggedPartsForIdleAnimation()
    {
        foreach (var name in new[] { "body", "head", "arm_l", "arm_r", "branch", "fish", "rock" }) Assert.True(M.HasPart(name), name);
        Assert.Equal("body", M.Part("head").Parent);
        Assert.Equal("body", M.Part("arm_l").Parent);
        Assert.Equal("body", M.Part("arm_r").Parent);
        Assert.Equal("arm_r", M.Part("branch").Parent);
    }

    [Fact]
    public void ArmsPivotAtTheShouldersAndMirror()
    {
        Assert.Equal(M.Part("arm_l").Pivot.Y, M.Part("arm_r").Pivot.Y, 6);
        Assert.Equal(M.Part("arm_l").Pivot.X, -M.Part("arm_r").Pivot.X, 6);
        foreach (var name in new[] { "arm_l", "arm_r" })
        {
            var p = M.Part(name).Solid.Positions();
            double min = double.MaxValue, max = double.MinValue;
            for (int i = 1; i < p.Length; i += 3) { min = Math.Min(min, p[i]); max = Math.Max(max, p[i]); }
            Assert.True(min < -0.4 && max < 0.2, $"{name} hangs from its shoulder");
        }
    }

    [Fact]
    public void IsTallWithGlowingEyesAndRedArils()
    {
        Assert.InRange(M.Height, 2.1, 2.9);
        Assert.False(M.Part("head").Glow.IsEmpty, "amber eyes");
        Assert.False(M.Part("branch").Glow.IsEmpty, "red arils");
        Assert.True(M.Max.Y > M.Part("head").Pivot.Y + 0.3, "ears and branch rise above the shoulders");
    }

    [Fact]
    public void StaysWithinItsOwnBudget() => Assert.InRange(M.TriangleCount, 200, 450);
}

public class NameIsHisAloneTests
{
    private static readonly Regex Forbidden = new(@"E[oò]ghann|E\\u00f2ghann|Ewan", RegexOptions.IgnoreCase);

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Hollowfall.Core"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    [Fact]
    public void NoAuthoredStringInCoreOrTheGameShellUsesHisNameExceptHisOwnFile()
    {
        string root = Root();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "Hollowfall.Core"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "game", "scripts"), "*.cs"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        foreach (var file in files)
        {
            if (Path.GetFileName(file) == "WulverDialogue.cs") continue;
            // The one other mention is the data folder's own path, ~/.ewanc26 (in DataHome and its comments); nothing else may match.
            string text = File.ReadAllText(file).Replace(".ewanc26", "");
            var m = Forbidden.Match(text);
            Assert.False(m.Success, $"{Path.GetFileName(file)} uses his name ({m.Value}); it belongs only in WulverDialogue");
        }
    }

    [Fact]
    public void NoGeneratedNameOrLineMatchesItAcrossManySeeds()
    {
        for (long seed = 1; seed <= 300; seed++)
        {
            var plan = StoryPlan.For(seed, 3);
            var text = new List<string> { plan.WardenName, plan.Title, plan.BossIntro, plan.Ending.Title };
            text.AddRange(plan.Intro);
            text.AddRange(plan.Ending.Paragraphs);
            foreach (var t in text) Assert.DoesNotMatch(Forbidden, t);
        }
        foreach (var model in ModelCatalog.All) Assert.DoesNotMatch(Forbidden, model.Id);
        foreach (var kind in Enum.GetNames<EnemyKind>()) Assert.DoesNotMatch(Forbidden, kind);
    }
}
