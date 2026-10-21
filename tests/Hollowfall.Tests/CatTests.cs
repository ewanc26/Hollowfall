using System.Text.RegularExpressions;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Models;
using Hollowfall.Core.Story;
using Hollowfall.Core.Story.Dialogue;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

internal static class RepoRoot
{
    public static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Hollowfall.Core"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}

public class CatPlacerTests
{
    private static readonly long[] Seeds = [.. Enumerable.Range(1, 30).Select(i => (long)i), TerrainGenerator.SeedFromString("voxel"), TerrainGenerator.SeedFromString("ewan")];

    public static TheoryData<long> SeedData => [.. Seeds];

    private static DungeonSite SiteOf(long seed) => WulverPlacer.NearestSite(new TerrainGenerator(seed))!;

    [Theory]
    [MemberData(nameof(SeedData))]
    public void TheSameSeedGivesTheSameSpot(long seed)
    {
        var site = SiteOf(seed);
        Assert.Equal(CatPlacer.Place(site, seed), CatPlacer.Place(site, seed));
    }

    [Theory]
    [MemberData(nameof(SeedData))]
    public void HeRestsBesideAPitNeverOnIt(long seed)
    {
        var site = SiteOf(seed);
        var spot = CatPlacer.Place(site, seed);
        Assert.NotNull(spot);
        var s = spot.Value;
        double d = Math.Sqrt(Math.Pow(s.X - s.Pit.X, 2) + Math.Pow(s.Z - s.Pit.Z, 2));
        Assert.InRange(d, 1.0, 1.5);
        Assert.Equal(1, Math.Abs(s.Dx) + Math.Abs(s.Dz));

        // The seat lies in the pit's free ring: floor cells of the same room, one step from the pit's cell.
        var floor = site.Floors[s.Pit.Floor];
        int cx = s.Pit.CellX + s.Dx, cz = s.Pit.CellZ + s.Dz;
        Assert.Equal(CellKind.Floor, floor.CellAt(cx, cz));
        Assert.Equal(s.Pit.RoomId, floor.RoomIdAt(cx, cz));
        Assert.Equal((int)Math.Floor(s.X), site.OriginX + cx);
        Assert.Equal((int)Math.Floor(s.Z), site.OriginZ + cz);
        Assert.Contains(s.Pit, FirePitPlacer.Place(site, seed));
    }

    [Theory]
    [MemberData(nameof(SeedData))]
    public void HeLooksAtTheFire(long seed)
    {
        var s = CatPlacer.Place(SiteOf(seed), seed)!.Value;
        double fx = -Math.Sin(s.Yaw), fz = -Math.Cos(s.Yaw);
        double dx = s.Pit.X - s.X, dz = s.Pit.Z - s.Z, len = Math.Sqrt(dx * dx + dz * dz);
        Assert.Equal(1.0, fx * dx / len + fz * dz / len, 6);
    }

    [Theory]
    [MemberData(nameof(SeedData))]
    public void HePrefersASafeRoomThenTheDeepestPitOnFloorOneThenFloorZero(long seed)
    {
        var site = SiteOf(seed);
        var pits = FirePitPlacer.Place(site, seed);
        var s = CatPlacer.Place(site, seed)!.Value;
        bool Safe(FirePitSpot p) => site.Floors[p.Floor].Rooms[p.RoomId].Role == RoomRole.Safe;
        if (pits.Any(p => Safe(p) && p.Floor <= 1)) Assert.True(Safe(s.Pit) && s.Pit.Floor <= 1);
        else if (pits.Any(Safe)) Assert.True(Safe(s.Pit));
        else Assert.Equal(pits.Where(p => p.Floor <= 1).Max(p => p.Floor), s.Pit.Floor);
    }

    [Fact]
    public void ThereIsExactlyOneCatPerWorldAndDifferentSeedsDiffer()
    {
        var spots = Seeds.Select(seed => CatPlacer.Place(new VoxelWorld(seed))!.Value).ToList();
        Assert.Equal(Seeds.Length, spots.Count);
        int distinct = spots.Select(s => (Math.Round(s.X), Math.Round(s.Z))).Distinct().Count();
        Assert.True(distinct >= Seeds.Length - 1, $"only {distinct} distinct spots in {Seeds.Length} seeds");
        Assert.Equal(CatPlacer.Place(new VoxelWorld(Seeds[0])), CatPlacer.Place(new VoxelWorld(Seeds[0])));
    }

    [Fact]
    public void NoDungeonMeansNoCat() => Assert.Null(CatPlacer.Place(null, 1));
}

public class CatModelTests
{
    private static LowPolyModel M => ModelCatalog.Cat;

    [Fact]
    public void HasTheRiggedPartsWithTheirParents()
    {
        foreach (var name in new[] { "hearth", "body", "head", "eyes", "ear_l", "ear_r", "tail_1", "tail_2", "leg_fl", "leg_fr", "leg_bl", "leg_br" }) Assert.True(M.HasPart(name), name);
        Assert.Null(M.Part("body").Parent);
        Assert.Null(M.Part("hearth").Parent);
        Assert.Equal("body", M.Part("head").Parent);
        Assert.Equal("head", M.Part("ear_l").Parent);
        Assert.Equal("head", M.Part("ear_r").Parent);
        Assert.Equal("head", M.Part("eyes").Parent);
        Assert.Equal("body", M.Part("tail_1").Parent);
        Assert.Equal("tail_1", M.Part("tail_2").Parent);
        foreach (var leg in new[] { "leg_fl", "leg_fr", "leg_bl", "leg_br" }) Assert.Equal("body", M.Part(leg).Parent);
    }

    [Fact]
    public void TheJointsSitWhereTheyShouldAndMirror()
    {
        Assert.Equal(M.Part("ear_l").Pivot.X, -M.Part("ear_r").Pivot.X, 6);
        Assert.Equal(M.Part("leg_fl").Pivot.X, -M.Part("leg_fr").Pivot.X, 6);
        Assert.Equal(M.Part("leg_bl").Pivot.X, -M.Part("leg_br").Pivot.X, 6);
        Assert.True(M.Part("head").Pivot.Z < M.Part("body").Pivot.Z, "the head is at the front (-Z)");
        Assert.True(M.Part("tail_1").Pivot.Z > M.Part("body").Pivot.Z, "the tail is at the back");
        Assert.True(M.Part("tail_2").Pivot.Z > M.Part("tail_1").Pivot.Z, "the second tail segment hangs off the first");
        Assert.True(M.Part("ear_l").Pivot.Y > M.Part("head").Pivot.Y, "the ears rise from the head");
        foreach (var leg in new[] { "leg_fl", "leg_fr" })
        {
            var p = M.Part(leg).Solid.Positions();
            double min = double.MaxValue, max = double.MinValue;
            for (int i = 1; i < p.Length; i += 3) { min = Math.Min(min, p[i]); max = Math.Max(max, p[i]); }
            Assert.True(min < -0.2 && max <= 0.01, $"{leg} hangs from its shoulder");
        }
    }

    [Fact]
    public void WearsAShortSilveryBlueGreyCoatWithVividGreenEyes()
    {
        var body = M.Part("body").Solid;
        var c = body.Colors();
        double r = 0, g = 0, b = 0;
        int n = c.Length / 4;
        for (int i = 0; i < c.Length; i += 4) { r += c[i]; g += c[i + 1]; b += c[i + 2]; }
        r /= n; g /= n; b /= n;
        Assert.True(b > r && b >= g * 0.99, $"blue-grey coat, was {r:F2} {g:F2} {b:F2}");
        Assert.InRange((r + g + b) / 3, 0.35, 0.65);
        Assert.InRange(Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)), 0.02, 0.2);

        var eyes = M.Part("eyes");
        Assert.False(eyes.Glow.IsEmpty, "the eyes glow");
        var e = eyes.Glow.Colors();
        Assert.True(e[1] > e[0] * 2 && e[1] > e[2] * 2, "vivid green");
    }

    [Fact]
    public void SitsOnTheHearthAndStaysSmall()
    {
        Assert.InRange(M.TriangleCount, 250, 420);
        Assert.InRange(M.Width, 0.8, 1.5); // the long tail stretched out behind him; at rest he curls it round
        Assert.InRange(M.Height, 0.5, 0.85);
        Assert.True(M.Min.Y >= -1e-6, "the seat rests on the floor");
        Assert.True(M.Part("body").Pivot.Y > 0.2, "the cat is above the seat");
        Assert.False(M.Part("hearth").Solid.IsEmpty);
    }

    [Fact]
    public void IsNeverAnEnemy()
    {
        Assert.DoesNotContain(Enum.GetNames<EnemyKind>(), n => n.Contains("cat", StringComparison.OrdinalIgnoreCase));
    }
}

public class CatDialogueTests
{
    private static PlayerVitals Vitals() => new();

    private static CatEncounter NewEncounter(IEnumerable<string>? flags = null, int visits = 0)
    {
        var spot = CatPlacer.Place(WulverPlacer.NearestSite(new TerrainGenerator(7)), 7)!.Value;
        return new CatEncounter(7, "seven", spot, flags, visits);
    }

    [Fact]
    public void TheTreeIsSoundAndSmall()
    {
        Assert.Empty(DialogueValidator.Validate(CatDialogue.Graph));
        Assert.InRange(CatDialogue.Graph.Nodes.Count, 8, 12);
    }

    [Fact]
    public void EveryIrishLineHasItsEnglishBeneathAndIsInTheReviewedList()
    {
        string doc = File.ReadAllText(Path.Combine(RepoRoot.Find(), "docs", "gaeilge.md"));
        foreach (var node in CatDialogue.Graph.Nodes)
            foreach (var line in node.Lines)
            {
                if (line.Text.StartsWith('(')) { Assert.EndsWith(")", line.Text); continue; }
                int cut = line.Text.IndexOf("\n(", StringComparison.Ordinal);
                Assert.True(cut > 0 && line.Text.EndsWith(')'), $"{node.Id}: Irish line without English beneath: {line.Text}");
                string irish = line.Text[..cut];
                // Each sentence of the Irish line is in the document's table.
                foreach (var sentence in Regex.Split(irish, @"(?<=[.!?])\s+").Where(s => s.Length > 0))
                    Assert.Contains(sentence.TrimEnd('.', '!', '?'), doc, StringComparison.Ordinal);
            }
    }

    [Fact]
    public void ChoicesAreEnglish() =>
        Assert.All(CatDialogue.Graph.Nodes.SelectMany(n => n.Choices), c => Assert.DoesNotMatch(new Regex("[áéíóú]"), c.Text));

    [Fact]
    public void TheWarmLinePlaysOnlyOnTheThirdVisitAndOnlyOnce()
    {
        var cat = NewEncounter();
        var vitals = Vitals();
        var texts = new List<string>();
        for (int visit = 1; visit <= 5; visit++)
        {
            var runner = cat.Begin(vitals);
            texts.Add(runner.Text);
            cat.End();
        }
        Assert.DoesNotContain("Tine bheag", texts[0]);
        Assert.DoesNotContain("Tine bheag", texts[1]);
        Assert.Contains("Tine bheag", texts[2]);
        Assert.Contains("(A little fire.", texts[2]);
        Assert.DoesNotContain("Tine bheag", texts[3]);
        Assert.DoesNotContain("Tine bheag", texts[4]);
        Assert.Equal(5, cat.Visits);
    }

    [Fact]
    public void SittingGivesAGentleWarmthOncePerRun()
    {
        var cat = NewEncounter();
        var vitals = Vitals();
        vitals.Damage(60);
        double before = vitals.Health;
        Assert.True(vitals.HardDamage > 0);

        var runner = cat.Begin(vitals);
        Assert.Equal("meet", runner.NodeId);
        Assert.Equal(before, vitals.Health);
        Assert.True(cat.Choose(0, vitals)); // blink back
        Assert.StartsWith("Sit down", runner.Choices[0].Text);
        Assert.True(cat.Choose(0, vitals)); // sit
        Assert.Equal("sit", runner.NodeId);
        Assert.True(cat.GiftTaken);
        Assert.Equal(0, vitals.HardDamage);
        Assert.Equal(before + CatEncounter.WarmthHeal, vitals.Health, 6);
        Assert.True(vitals.Health < PlayerVitals.MaxHealth, "a little, never a full heal");

        // A second time: nothing more.
        vitals.Damage(30);
        double hurt = vitals.Health;
        cat.End();
        var again = cat.Begin(vitals);
        Assert.Equal("back", again.NodeId);
        Assert.DoesNotContain(again.Choices, c => c.Text.StartsWith("Sit down"));
        Assert.Equal(hurt, vitals.Health);
        Assert.Contains(CatDialogue.Sat, cat.Flags);
    }

    [Fact]
    public void WarmthIsOncePerRunNotOncePerSeedSoAFreshLaunchCanSitAgain()
    {
        // The file remembers that you sat before (a flag and three visits), but this run has not had the warmth yet.
        var cat = NewEncounter([CatDialogue.Sat, CatDialogue.Petted], 1);
        var vitals = Vitals();
        vitals.Damage(60);
        double before = vitals.Health;

        var runner = cat.Begin(vitals);
        Assert.Equal("back", runner.NodeId);
        int sit = runner.Choices.ToList().FindIndex(c => c.Text.StartsWith("Sit down"));
        Assert.True(sit >= 0, "Sit is offered on a new run even though a flag says you sat last time");
        Assert.True(cat.Choose(sit, vitals));
        Assert.True(cat.GiftTaken);
        Assert.Equal(0, vitals.HardDamage);
        Assert.Equal(before + CatEncounter.WarmthHeal, vitals.Health, 6);

        // Within the same run it is not offered again, and nothing more is given.
        vitals.Damage(20);
        double hurt = vitals.Health;
        cat.End();
        var again = cat.Begin(vitals);
        Assert.DoesNotContain(again.Choices, c => c.Text.StartsWith("Sit down"));
        Assert.Equal(hurt, vitals.Health);
    }

    [Fact]
    public void ReturnVisitsRememberWhatYouDid()
    {
        var cat = NewEncounter();
        var vitals = Vitals();
        var first = cat.Begin(vitals);
        Assert.Contains("Dia duit", first.Text);
        cat.Choose(0, vitals); // blink
        cat.Choose(1, vitals); // stroke
        Assert.Contains(CatDialogue.Petted, cat.Flags);
        Assert.Contains(CatDialogue.Blinked, cat.Flags);
        cat.End();
        var back = cat.Begin(vitals);
        Assert.Equal("back", back.NodeId);
        Assert.Contains("remembers your hand", back.Text);
    }

    [Fact]
    public void EveryPathCanBeLeftAndNeverGivesMoreThanOneGift()
    {
        // Walk every path to depth 7 from each entry and make sure the player can always say goodbye and at most one gift comes out.
        void Walk(DialogueRunner runner, int depth, int gifts)
        {
            gifts += runner.TakeGifts().Count;
            Assert.True(gifts <= 1);
            if (runner.Finished || depth == 0) return;
            Assert.Contains(runner.Choices, c => c.Next == null || CatDialogue.Graph.Find(c.Next) is { Choices: [{ Next: null }] });
            for (int i = 0; i < runner.Choices.Count; i++)
            {
                var fork = runner.Fork();
                fork.Advance(i);
                Walk(fork, depth - 1, gifts);
            }
        }
        foreach (int visits in new[] { 0, 1, 2, 3, 6 })
            Walk(new DialogueRunner(CatDialogue.Graph, new DialogueContext { VisitCount = visits, Seed = 3 }), 7, 0);
    }

    [Fact]
    public void ReachAndNoticeRangesAreAboutRight()
    {
        var cat = NewEncounter();
        var s = cat.Spot;
        Assert.True(cat.IsNear(s.X + 2.0, s.Y, s.Z));
        Assert.False(cat.IsNear(s.X + 3.2, s.Y, s.Z));
        Assert.True(cat.Notices(s.X + 7.5, s.Y, s.Z));
        Assert.False(cat.Notices(s.X + 9, s.Y, s.Z));
        Assert.Null(cat.Sighting(s.X + 20, s.Y, s.Z));
        Assert.Equal(CatDialogue.FirstSight, cat.Sighting(s.X + 6, s.Y, s.Z));
        Assert.Null(cat.Sighting(s.X + 5, s.Y, s.Z));
    }

    [Fact]
    public void FlagsAreBoundedAndSavedWithTheStoryProgress()
    {
        var cat = NewEncounter(Enumerable.Range(0, 40).Select(i => $"f{i}").Append("bad flag!"));
        Assert.Equal(CatEncounter.MaxFlags, cat.Flags.Count);

        var progress = new StoryProgress();
        progress.SetCat("seven", [CatDialogue.Petted, CatDialogue.Sat, "bad flag!"], 3);
        var back = StoryProgress.FromJson(progress.ToJson());
        Assert.Equal(3, back.CatVisits("seven"));
        Assert.Equal([CatDialogue.Petted, CatDialogue.Sat], back.CatFlags("seven"));
        Assert.Equal(0, back.CatVisits("other"));
        Assert.Equal(0, StoryProgress.FromJson("{\"seeds\":{\"x\":{\"cat\":{\"visits\":\"lots\",\"flags\":[1,2]}}}}").CatVisits("x"));
    }

    [Fact]
    public void TheFileIsOnlyWrittenToTheGivenPath()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hollowfall-cat-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(dir, "story.json");
            var progress = new StoryProgress();
            progress.SetCat("seven", [CatDialogue.Sat], 1);
            Assert.True(progress.TrySave(path));
            Assert.Equal([CatDialogue.Sat], StoryProgress.Load(path).CatFlags("seven"));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}

public class CatNameIsHisAloneTests
{
    private static readonly Regex Forbidden = new(@"\b(Aodh[aá]n|Aidan|Aiden)\b", RegexOptions.IgnoreCase);

    private static IEnumerable<string> CodeFiles(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "src", "Hollowfall.Core"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "game", "scripts"), "*.cs"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void NoCodeOutsideTheCatsOwnFileWritesHisName()
    {
        string root = RepoRoot.Find();
        foreach (var file in CodeFiles(root))
        {
            if (Path.GetFileName(file) == "CatDialogue.cs") continue;
            var m = Forbidden.Match(File.ReadAllText(file));
            Assert.False(m.Success, $"{Path.GetFileName(file)} uses the cat's name ({m.Value}); it belongs only in CatDialogue.cs");
        }
    }

    [Fact]
    public void TheNameIsKeptExactlyWithItsAcuteAccent() => Assert.Equal("Aodhán", CatDialogue.Name);

    [Fact]
    public void OnlyTheGaeilgeAndStoryDocsMayMentionHim()
    {
        string root = RepoRoot.Find();
        var files = Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md").Append(Path.Combine(root, "README.md")).Append(Path.Combine(root, "CHANGELOG.md")).Append(Path.Combine(root, "AGENTS.md"));
        foreach (var file in files)
        {
            if (Path.GetFileName(file) is "gaeilge.md" or "story.md") continue;
            var m = Forbidden.Match(File.ReadAllText(file));
            Assert.False(m.Success, $"{Path.GetFileName(file)} names the cat ({m.Value}); only docs/gaeilge.md and docs/story.md may");
        }
    }

    [Fact]
    public void NoModelOrGeneratedNameMatches()
    {
        foreach (var model in ModelCatalog.All) Assert.DoesNotMatch(Forbidden, model.Id);
        foreach (var kind in Enum.GetNames<EnemyKind>()) Assert.DoesNotMatch(Forbidden, kind);
        for (long seed = 1; seed <= 300; seed++)
        {
            var plan = StoryPlan.For(seed, 3);
            foreach (var t in new[] { plan.WardenName, plan.Title, plan.BossIntro, plan.Ending.Title }.Concat(plan.Intro).Concat(plan.Ending.Paragraphs))
                Assert.DoesNotMatch(Forbidden, t);
        }
    }
}
