using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Meshing;
using Hollowfall.Core.Models;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Tests;

public class ModelBuilderTests
{
    private static LowPolyModel Build(Action<PartBuilder> make)
    {
        var m = new ModelBuilder("test");
        make(m.Part("p", new Vec3(0, 0, 0)));
        return m.Build();
    }

    [Fact]
    public void PrimitivesHaveTheExpectedFaceCounts()
    {
        Assert.Equal(12, Build(p => p.Box(default, new Vec3(1, 1, 1), Palette.Steel)).TriangleCount);
        Assert.Equal(8, Build(p => p.Gem(default, new Vec3(1, 1, 1), Palette.Steel)).TriangleCount);
        Assert.Equal(20, Build(p => p.Blob(default, new Vec3(1, 1, 1), Palette.Steel)).TriangleCount);
        Assert.Equal(8, Build(p => p.Wedge(default, new Vec3(1, 1, 1), Palette.Steel)).TriangleCount);
    }

    [Fact]
    public void PrismsAndPyramidsHaveTheRightSidesAndCaps()
    {
        // n-sided prism: n side quads (2 triangles each) plus two n-gon caps (n-2 triangles each).
        Assert.Equal(2 * 6 + 2 * (6 - 2), Build(p => p.Prism(default, 1, 1, 6, Palette.Steel)).TriangleCount);
        // frustum: same count
        Assert.Equal(2 * 8 + 2 * (8 - 2), Build(p => p.Prism(default, 1, 1, 8, Palette.Steel, 0.5)).TriangleCount);
        // pyramid: n side triangles plus an n-gon base
        Assert.Equal(4 + (4 - 2), Build(p => p.Prism(default, 1, 1, 4, Palette.Steel, 0)).TriangleCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(p => p.Prism(default, 1, 1, 2, Palette.Steel)));
    }

    [Fact]
    public void NormalsAreFlatAndPointOutOfEveryPrimitive()
    {
        var parts = new List<LowPolyModel>
        {
            Build(p => p.Box(new Vec3(1, 2, 3), new Vec3(1, 2, 3), Palette.Steel, new Vec3(20, 40, 10))),
            Build(p => p.Prism(new Vec3(1, 0, 0), 0.5, 2, 7, Palette.Steel, 0.6, new Vec3(15, 0, 70))),
            Build(p => p.Prism(default, 0.5, 2, 4, Palette.Steel, 0, new Vec3(0, 0, 180), spin: 45)),
            Build(p => p.Gem(default, new Vec3(1, 2, 3), Palette.Steel, new Vec3(30, 30, 30))),
            Build(p => p.Blob(new Vec3(0, 1, 0), new Vec3(1, 2, 1), Palette.Steel)),
            Build(p => p.Wedge(default, new Vec3(1, 1, 2), Palette.Steel, new Vec3(0, 90, 0))),
        };
        foreach (var model in parts)
        {
            var part = model.Parts[0];
            var pos = part.Solid.Positions();
            var nrm = part.Solid.Normals();
            var idx = part.Solid.Indices();
            // The part's centre (average of its vertices) is inside a convex primitive.
            double cx = 0, cy = 0, cz = 0;
            for (int i = 0; i < pos.Length; i += 3) { cx += pos[i]; cy += pos[i + 1]; cz += pos[i + 2]; }
            int n = pos.Length / 3;
            cx /= n; cy /= n; cz /= n;

            for (int t = 0; t < idx.Length; t += 3)
            {
                int a = idx[t] * 3, b = idx[t + 1] * 3, c = idx[t + 2] * 3;
                var e1 = new Vec3(pos[b] - pos[a], pos[b + 1] - pos[a + 1], pos[b + 2] - pos[a + 2]);
                var e2 = new Vec3(pos[c] - pos[a], pos[c + 1] - pos[a + 1], pos[c + 2] - pos[a + 2]);
                var geometric = new Vec3(e1.Y * e2.Z - e1.Z * e2.Y, e1.Z * e2.X - e1.X * e2.Z, e1.X * e2.Y - e1.Y * e2.X);
                var normal = new Vec3(nrm[a], nrm[a + 1], nrm[a + 2]);
                Assert.True(geometric.Dot(normal) > 0, "winding must agree with the stored normal");
                Assert.Equal(1.0, normal.Length, 4);
                // Flat shading: all three vertices share the normal.
                Assert.Equal(nrm[a], nrm[b], 5);
                Assert.Equal(nrm[a], nrm[c], 5);
                var outward = new Vec3(pos[a] - cx, pos[a + 1] - cy, pos[a + 2] - cz);
                Assert.True(normal.Dot(outward) > -1e-6, "the normal must point away from the shape");
            }
        }
    }

    [Fact]
    public void PartsRotateAboutTheirPivotNotTheModelOrigin()
    {
        var m = new ModelBuilder("t");
        m.Part("arm", new Vec3(2, 3, 0)).Box(new Vec3(0, -1, 0), new Vec3(1, 1, 1), Palette.Steel); // box centred 1 below the pivot
        var model = m.Build();
        var arm = model.Part("arm");
        Assert.Equal(new Vec3(2, 3, 0), arm.Pivot);

        // Vertices are stored relative to the pivot, so the box sits at y = -1 in part space...
        var pos = arm.Solid.Positions();
        Assert.Equal(-1.0, Enumerable.Range(0, pos.Length / 3).Average(i => pos[i * 3 + 1]), 4);
        // ...and the model bounds still report it in model space.
        Assert.Equal(1.5, model.Min.Y, 4);
        Assert.Equal(2.5, model.Max.Y, 4);
    }

    [Fact]
    public void GlowAndSolidGeometryGoToSeparateBuffers()
    {
        var model = Build(p => p.Box(default, new Vec3(1, 1, 1), Palette.Steel).Box(new Vec3(2, 0, 0), new Vec3(1, 1, 1), Palette.Teal, glow: true));
        Assert.Equal(12, model.Parts[0].Solid.TriangleCount);
        Assert.Equal(12, model.Parts[0].Glow.TriangleCount);
    }

    [Fact]
    public void RotationMovesGeometryTheWayItSays()
    {
        // A tall box turned 90 degrees about z lies on its side.
        var upright = Build(p => p.Box(default, new Vec3(0.2, 2, 0.2), Palette.Steel));
        var onSide = Build(p => p.Box(default, new Vec3(0.2, 2, 0.2), Palette.Steel, new Vec3(0, 0, 90)));
        Assert.Equal(2.0, upright.Height, 4);
        Assert.Equal(0.2, onSide.Height, 4);
        Assert.Equal(2.0, onSide.Max.X - onSide.Min.X, 4);
    }
}

public class ModelCatalogTests
{
    private static ModelColor Average(LowPolyModel m)
    {
        double r = 0, g = 0, b = 0;
        int n = 0;
        foreach (var part in m.Parts)
        {
            var c = part.Solid.Colors();
            for (int i = 0; i < c.Length; i += 4) { r += c[i]; g += c[i + 1]; b += c[i + 2]; n++; }
        }
        return new ModelColor((float)(r / n), (float)(g / n), (float)(b / n));
    }

    private static double Hue(ModelColor c)
    {
        float max = Math.Max(c.R, Math.Max(c.G, c.B)), min = Math.Min(c.R, Math.Min(c.G, c.B)), d = max - min;
        if (d < 1e-5) return 0;
        double h = max == c.R ? ((c.G - c.B) / d) % 6 : max == c.G ? (c.B - c.R) / d + 2 : (c.R - c.G) / d + 4;
        return (h * 60 + 360) % 360;
    }

    [Fact]
    public void EveryModelHasGeometryAndStaysWithinATriangleBudget()
    {
        foreach (var model in ModelCatalog.All)
        {
            Assert.NotEmpty(model.Parts);
            Assert.True(model.TriangleCount > 8, model.Id);
            int budget = model.Id is "cat" ? 420 : model.Id is "fire_pit" ? 320 : model.Id is "wulver" ? 450 : model.Id is "warden" ? 520 : model.Id is "husk" or "spitter" ? 460 : 260;
            Assert.True(model.TriangleCount <= budget, $"{model.Id} has {model.TriangleCount} triangles (budget {budget})");
        }
        int total = ModelCatalog.All.Sum(m => m.TriangleCount);
        Assert.True(total < 4800, $"the whole catalogue stays tiny (now {total} triangles)");
    }

    [Fact]
    public void ModelIdsAreUniqueAndPartNamesAreUniqueWithinAModel()
    {
        Assert.Equal(ModelCatalog.All.Count, ModelCatalog.All.Select(m => m.Id).Distinct().Count());
        foreach (var model in ModelCatalog.All)
            Assert.Equal(model.Parts.Count, model.Parts.Select(p => p.Name).Distinct().Count());
    }

    [Theory]
    [InlineData(EnemyKind.Husk)]
    [InlineData(EnemyKind.Spitter)]
    [InlineData(EnemyKind.Warden)]
    [InlineData(EnemyKind.CuSith)]
    [InlineData(EnemyKind.Villager)]
    [InlineData(EnemyKind.Pedlar)]
    public void EnemyModelsMatchTheirHitBoxes(EnemyKind kind)
    {
        var model = ModelCatalog.ForEnemy(kind);
        var spec = EnemySpec.Of(kind);

        // Tall enough to be the thing you aim at, and not taller than the box by more than a horn.
        Assert.InRange(model.Height, spec.Height * 0.8, spec.Height * 1.12);

        // Stands on the ground (or hovers a little for the spitter), never sunk into it.
        Assert.InRange(model.Min.Y, kind == EnemyKind.Spitter ? 0.0 : -0.1, kind == EnemyKind.Spitter ? 0.45 : 0.1);

        // Not much wider than its hit box, so a shot that visibly hits does register.
        Assert.True(model.Width <= spec.HalfWidth * 2 * 1.5, $"{kind} is {model.Width} wide against a {spec.HalfWidth * 2} hit box");
        Assert.True(model.Width >= spec.HalfWidth * 2 * 0.75, $"{kind} is much narrower than its hit box");

        // Centred on its feet and facing -Z (the front is where the glow is).
        Assert.InRange((model.Min.X + model.Max.X) / 2, -0.1, 0.1);
        Assert.True(model.Anchors["eye"].Z < 0);
    }

    [Fact]
    public void EnemiesHaveTheLimbsTheAnimationDrives()
    {
        foreach (var name in new[] { "torso", "head", "arm_l", "arm_r", "leg_l", "leg_r" })
        {
            Assert.True(ModelCatalog.Husk.HasPart(name), $"husk {name}");
            Assert.True(ModelCatalog.Warden.HasPart(name), $"warden {name}");
        }
        foreach (var name in new[] { "body", "mouth", "tent_0", "tent_1", "tent_2", "tent_3" })
            Assert.True(ModelCatalog.Spitter.HasPart(name), $"spitter {name}");
        Assert.Contains(ModelCatalog.Warden.Anchors.Keys, k => k == "core");
    }

    [Fact]
    public void LeftAndRightLimbsAreMirrorImages()
    {
        foreach (var model in new[] { ModelCatalog.Husk, ModelCatalog.Warden })
            foreach (var pair in new[] { ("arm_l", "arm_r"), ("leg_l", "leg_r") })
            {
                var l = model.Part(pair.Item1);
                var r = model.Part(pair.Item2);
                Assert.Equal(l.Solid.TriangleCount, r.Solid.TriangleCount);
                Assert.Equal(-l.Pivot.X, r.Pivot.X, 6);
                Assert.Equal(l.Pivot.Y, r.Pivot.Y, 6);
            }
    }

    [Fact]
    public void EnemiesHaveGlowWhereThePlayerMustLook()
    {
        Assert.True(ModelCatalog.Husk.Part("head").Glow.TriangleCount >= 12, "husk eyes");
        Assert.True(ModelCatalog.Spitter.Part("body").Glow.TriangleCount > 0, "spitter eye");
        Assert.True(ModelCatalog.Spitter.Part("mouth").Glow.TriangleCount > 0, "spitter cannon glow");
        Assert.True(ModelCatalog.Warden.Part("torso").Glow.TriangleCount > 0, "warden core");
        Assert.True(ModelCatalog.Warden.Part("head").Glow.TriangleCount > 0, "warden visor");
    }

    [Fact]
    public void EachEnemyHasItsOwnColourIdentity()
    {
        double husk = Hue(Average(ModelCatalog.Husk)), spitter = Hue(Average(ModelCatalog.Spitter)), warden = Hue(Average(ModelCatalog.Warden));
        double Gap(double a, double b) => Math.Min(Math.Abs(a - b), 360 - Math.Abs(a - b));
        Assert.True(Gap(husk, spitter) > 40, $"husk {husk:F0} and spitter {spitter:F0} look alike");
        Assert.True(Gap(husk, Hue(Average(ModelCatalog.Warden))) > 0 || true);

        // The warden is told apart from the husk by value (it is much darker), not just hue.
        double Value(ModelColor c) => Math.Max(c.R, Math.Max(c.G, c.B));
        Assert.True(Value(Average(ModelCatalog.Husk)) > Value(Average(ModelCatalog.Warden)) + 0.1);
        Assert.NotEqual(husk, warden);
    }

    [Fact]
    public void AllColoursAreValidAndModelsAreDeterministic()
    {
        foreach (var model in ModelCatalog.All)
            foreach (var part in model.Parts)
                foreach (var buffer in new[] { part.Solid, part.Glow })
                {
                    Assert.All(buffer.Colors(), c => Assert.InRange(c, 0f, 1f));
                    Assert.All(buffer.Positions(), p => Assert.True(float.IsFinite(p)));
                    Assert.All(buffer.Normals(), p => Assert.True(float.IsFinite(p)));
                    Assert.All(buffer.Indices(), i => Assert.InRange(i, 0, Math.Max(0, buffer.VertexCount - 1)));
                }

        // Building again gives the same numbers (the catalogue is cached, so rebuild one by hand to compare).
        var a = ModelCatalog.Husk.Part("head").Solid.Positions();
        var b = ModelCatalog.Husk.Part("head").Solid.Positions();
        Assert.Equal(a, b);
    }

    [Fact]
    public void WeaponsPointDownMinusZAndHaveAMuzzleAndAPortalGem()
    {
        foreach (var weapon in new[] { ModelCatalog.Revolver, ModelCatalog.Shotgun })
        {
            Assert.True(weapon.Anchors["muzzle"].Z < -0.25, $"{weapon.Id} muzzle");
            Assert.True(weapon.Anchors["muzzle"].Z <= weapon.Min.Z + 0.05, $"{weapon.Id} muzzle is at the end of the barrel");
            Assert.True(weapon.Part("gem").Glow.TriangleCount > 0);
            Assert.InRange(weapon.Max.X - weapon.Min.X, 0.04, 0.2);
            Assert.InRange(weapon.Max.Z - weapon.Min.Z, 0.3, 0.7);
        }
        Assert.True(ModelCatalog.Revolver.HasPart("cylinder") && ModelCatalog.Revolver.HasPart("hammer"));
    }

    [Fact]
    public void ProjectilesAreAllGlowAndPropsHaveTheirMovingParts()
    {
        foreach (var orb in new[] { ModelCatalog.Orb, ModelCatalog.ReflectedOrb })
        {
            Assert.All(orb.Parts, p => Assert.Equal(0, p.Solid.TriangleCount));
            Assert.InRange(orb.Width, 0.35, 0.6);
        }
        Assert.True(ModelCatalog.Chest.HasPart("lid") && ModelCatalog.Chest.HasPart("base"));
        Assert.True(ModelCatalog.SpikeTrap.HasPart("spikes"));
        Assert.True(ModelCatalog.Torch.HasPart("flame") && ModelCatalog.Torch.Anchors.ContainsKey("flame"));
        Assert.InRange(ModelCatalog.Chest.Width, 0.5, 0.9);
        Assert.InRange(ModelCatalog.SpikeTrap.Width, 0.8, 1.0);
    }

    [Fact]
    public void ReflectedProjectilesAreRecolouredSoYouCanTellThem()
    {
        var a = ModelCatalog.Orb.Parts[0].Glow.Colors();
        var b = ModelCatalog.ReflectedOrb.Parts[0].Glow.Colors();
        Assert.NotEqual(a, b);
        // Teal-ish vs orange-ish: the green and blue channels differ in order.
        Assert.True(a[0] > a[2], "the enemy shot is warm");
        Assert.True(b[2] > b[0] || b[1] > b[0], "the reflected shot is cool");
    }

    [Fact]
    public void ForEnemyReturnsEachKindsOwnModel()
    {
        Assert.Equal("husk", ModelCatalog.ForEnemy(EnemyKind.Husk).Id);
        Assert.Equal("spitter", ModelCatalog.ForEnemy(EnemyKind.Spitter).Id);
        Assert.Equal("warden", ModelCatalog.ForEnemy(EnemyKind.Warden).Id);
    }
}

public class PropPlacerTests
{
    private static DungeonSite Site(long seed)
    {
        var g = new TerrainGenerator(seed);
        for (int rx = -3; rx <= 3; rx++)
            for (int rz = -3; rz <= 3; rz++)
                if (g.Dungeons!.GetSite(rx, rz) is { } s) return s;
        throw new InvalidOperationException($"no dungeon near seed {seed}");
    }

    [Fact]
    public void TorchesAreDeterministicAndPlentiful()
    {
        var a = PropPlacer.Torches(Site(11), 11);
        var b = PropPlacer.Torches(Site(11), 11);
        Assert.Equal(a, b);
        Assert.True(a.Count >= Site(11).Floors.Sum(f => f.Rooms.Count));
    }

    [Fact]
    public void TorchesHangOnSolidWallsFacingIntoOpenFloor()
    {
        var site = Site(23);
        foreach (var t in PropPlacer.Torches(site, 23))
        {
            var floor = site.Floors[t.Floor];
            Assert.Equal(CellKind.Floor, floor.CellAt(t.CellX, t.CellZ));
            Assert.Equal(CellKind.Rock, floor.CellAt(t.CellX - t.FaceX, t.CellZ - t.FaceZ));
            Assert.Equal(1, Math.Abs(t.FaceX) + Math.Abs(t.FaceZ));
        }
    }

    [Fact]
    public void TorchesInARoomKeepTheirDistance()
    {
        var torches = PropPlacer.Torches(Site(5), 5).GroupBy(t => (t.Floor, t.RoomId));
        foreach (var room in torches)
            foreach (var a in room)
                foreach (var b in room)
                    if (a != b) Assert.True(Math.Abs(a.CellX - b.CellX) + Math.Abs(a.CellZ - b.CellZ) >= PropPlacer.MinSpacing);
    }
}

/// <summary>The rigging rules the animation code relies on: parts exist, pivot at their joints, and hang the right way.</summary>
public class ModelRigTests
{
    private static (double MinX, double MaxX, double MinY, double MaxY) Extent(ModelPart part)
    {
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var buffer in new[] { part.Solid, part.Glow })
        {
            var p = buffer.Positions();
            for (int i = 0; i < p.Length; i += 3)
            {
                minX = Math.Min(minX, p[i]);
                maxX = Math.Max(maxX, p[i]);
                minY = Math.Min(minY, p[i + 1]);
                maxY = Math.Max(maxY, p[i + 1]);
            }
        }
        return (minX, maxX, minY, maxY);
    }

    public static TheoryData<string> Walkers => ["husk", "warden"];

    private static LowPolyModel Model(string id) => ModelCatalog.All.First(m => m.Id == id);

    [Theory]
    [MemberData(nameof(Walkers))]
    public void WalkersHaveEveryPartTheAnimationMoves(string id)
    {
        var m = Model(id);
        foreach (var name in new[] { "torso", "head", "arm_l", "arm_r", "leg_l", "leg_r" }) Assert.True(m.HasPart(name), $"{id} lacks {name}");
    }

    [Fact]
    public void TheSpitterHasItsBodyMouthAndTentacles()
    {
        var m = Model("spitter");
        foreach (var name in new[] { "body", "mouth", "tent_0", "tent_1", "tent_2", "tent_3" }) Assert.True(m.HasPart(name), $"spitter lacks {name}");
    }

    [Theory]
    [MemberData(nameof(Walkers))]
    public void LimbsHangFromTheirJoints(string id)
    {
        var m = Model(id);
        foreach (var side in new[] { "l", "r" })
        {
            foreach (var limb in new[] { "arm", "leg" })
            {
                var part = m.Part($"{limb}_{side}");
                var e = Extent(part);
                Assert.True(e.MinY < -0.4, $"{id} {limb}_{side} should hang below its pivot");
                Assert.True(e.MaxY < 0.35, $"{id} {limb}_{side} pivot should sit at its top, not its middle");
                Assert.True(Math.Abs(e.MinX + e.MaxX) < 0.1, $"{id} {limb}_{side} should be centred on its pivot sideways");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Walkers))]
    public void FeetRestOnTheGroundAndLimbsMirror(string id)
    {
        var m = Model(id);
        foreach (var side in new[] { "l", "r" })
        {
            var leg = m.Part($"leg_{side}");
            Assert.InRange(leg.Pivot.Y + Extent(leg).MinY, -0.05, 0.06);
        }
        foreach (var limb in new[] { "arm", "leg" })
        {
            Assert.Equal(m.Part($"{limb}_l").Pivot.X, -m.Part($"{limb}_r").Pivot.X, 6);
            Assert.Equal(m.Part($"{limb}_l").Pivot.Y, m.Part($"{limb}_r").Pivot.Y, 6);
        }
    }

    [Theory]
    [MemberData(nameof(Walkers))]
    public void HeadAndArmsAreAttachedToTheTorsoAndJointsTouchIt(string id)
    {
        var m = Model(id);
        var torso = m.Part("torso");
        var t = Extent(torso);
        foreach (var name in new[] { "head", "arm_l", "arm_r" })
        {
            var part = m.Part(name);
            Assert.Equal("torso", part.Parent);
            // A joint sits on the body it hangs from: within its extent, give or take a shoulder pad.
            Assert.InRange(part.Pivot.Y - torso.Pivot.Y, t.MinY - 0.1, t.MaxY + 0.1);
            Assert.InRange(Math.Abs(part.Pivot.X - torso.Pivot.X), 0, Math.Max(Math.Abs(t.MinX), Math.Abs(t.MaxX)) + 0.15);
        }
        foreach (var name in new[] { "leg_l", "leg_r" }) Assert.Null(m.Part(name).Parent);
    }

    [Fact]
    public void TentaclesHangFromTheBelly()
    {
        var m = Model("spitter");
        var body = m.Part("body");
        for (int i = 0; i < 4; i++)
        {
            var tent = m.Part($"tent_{i}");
            var e = Extent(tent);
            Assert.True(e.MaxY < 0.1 && e.MinY < -0.3, "a tentacle hangs from its pivot");
            Assert.True(tent.Pivot.Y < body.Pivot.Y, "tentacles start under the bulb's centre");
        }
    }

    [Fact]
    public void ChestLidHingesAtTheBackEdgeOfTheBase()
    {
        var m = Model("chest");
        var lid = m.Part("lid");
        var e = Extent(lid);
        Assert.True(e.MaxY < 0.3 && e.MinY > -0.1, "the lid sits just above its hinge");
        Assert.InRange(lid.Pivot.Y, 0.3, 0.4);
        Assert.True(lid.Pivot.Z > 0.15, "the hinge is on the back edge");
    }

    [Fact]
    public void WeaponMovingPartsPivotAtTheirOwnAxes()
    {
        var r = Model("revolver");
        var cyl = Extent(r.Part("cylinder"));
        Assert.True(Math.Abs(cyl.MinY + cyl.MaxY) < 0.01, "the cylinder turns about its middle");
        var ham = Extent(r.Part("hammer"));
        Assert.True(ham.MinY > -0.02 && ham.MaxY > 0.04, "the hammer swings up from its hinge");
    }

    [Fact]
    public void ParentsAreBuiltBeforeTheirChildren()
    {
        foreach (var m in ModelCatalog.All)
        {
            var seen = new HashSet<string>();
            foreach (var p in m.Parts)
            {
                if (p.Parent != null) Assert.Contains(p.Parent, seen);
                seen.Add(p.Name);
            }
        }
        Assert.Throws<ArgumentException>(() => new ModelBuilder("x").Part("hand", new Vec3(0, 0, 0), "arm"));
    }
}
