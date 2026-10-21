using System.Numerics;
using System.Buffers.Binary;
using System.Text.Json.Nodes;
using Hollowfall.Core.Assets;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Models;

namespace Hollowfall.Tests;

public class GlbWriterTests
{
    private static JsonNode ReadJson(byte[] glb)
    {
        int length = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        return JsonNode.Parse(glb.AsSpan(20, length))!;
    }

    [Fact]
    public void EveryModelIsAWellFormedBinaryGltfWithItsGeometry()
    {
        foreach (var model in ModelCatalog.All)
        {
            var glb = StaticAssets.ModelBytes(model);
            Assert.Equal(0x46546C67u, BinaryPrimitives.ReadUInt32LittleEndian(glb));
            Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(4)));
            Assert.Equal((uint)glb.Length, BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(8)));

            var json = ReadJson(glb);
            var names = json["nodes"]!.AsArray().Select(n => n!["name"]!.GetValue<string>()).ToHashSet();
            foreach (var part in model.Parts) Assert.Contains("part_" + part.Name, names);

            int vertices = 0, indices = 0;
            foreach (var mesh in json["meshes"]!.AsArray())
                foreach (var primitive in mesh!["primitives"]!.AsArray())
                {
                    vertices += primitive!["attributes"]!["POSITION"] is { } p ? json["accessors"]![p.GetValue<int>()]!["count"]!.GetValue<int>() : 0;
                    indices += json["accessors"]![primitive["indices"]!.GetValue<int>()]!["count"]!.GetValue<int>();
                }
            Assert.Equal(model.Parts.Sum(p => p.Solid.VertexCount + p.Glow.VertexCount), vertices);
            Assert.Equal(model.TriangleCount * 3, indices);
        }
    }

    [Fact]
    public void EnemyClipsTargetRealPartNodesWithMatchingKeyCounts()
    {
        foreach (var model in ModelCatalog.All)
        {
            var json = ReadJson(StaticAssets.ModelBytes(model));
            var nodes = json["nodes"]!.AsArray().Select(n => n!["name"]!.GetValue<string>()).ToList();
            var accessors = json["accessors"]!.AsArray();
            var animations = json["animations"]?.AsArray() ?? [];
            var expected = EnemyMotions.For(model);
            Assert.Equal(expected.Count, animations.Count);

            foreach (var animation in animations)
            {
                foreach (var channel in animation!["channels"]!.AsArray())
                {
                    var target = channel!["target"]!["node"]!.GetValue<int>();
                    Assert.StartsWith("part_", nodes[target]);
                }
                foreach (var sampler in animation["samplers"]!.AsArray())
                {
                    int input = sampler!["input"]!.GetValue<int>(), output = sampler["output"]!.GetValue<int>();
                    Assert.Equal(accessors[input]!["count"]!.GetValue<int>(), accessors[output]!["count"]!.GetValue<int>());
                }
            }
        }
    }

    [Fact]
    public void TheBeanNigheWringsWithBothArmsAndHerCloth()
    {
        var clip = EnemyMotions.For(ModelCatalog.ForEnemy(EnemyKind.BeanNighe)).Single(c => c.Motion == Motion.Idle);
        var left = clip.Tracks.Single(t => t.Part == "arm_l").Rotations;
        var right = clip.Tracks.Single(t => t.Part == "arm_r").Rotations;
        var cloth = clip.Tracks.Single(t => t.Part == "cloth").Rotations;
        Assert.True(left.Zip(right, AngleDegrees).Max() > 5, "the arms move apart");
        Assert.True(cloth.Max(q => AngleDegrees(q, Quaternion.Identity)) > 5, "the cloth twists away from rest");
    }

    private static double AngleDegrees(Quaternion a, Quaternion b)
    {
        double dot = Math.Min(1, Math.Abs(a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W));
        return 2 * Math.Acos(dot) * 180 / Math.PI;
    }

    [Fact]
    public void LoopingClipsCloseOnTheirFirstKey()
    {
        foreach (var model in ModelCatalog.All)
            foreach (var clip in EnemyMotions.For(model))
            {
                if (!Motions.Loops(clip.Motion)) continue;
                foreach (var track in clip.Tracks)
                {
                    Assert.True(Quaternion.Dot(track.Rotations[0], track.Rotations[^1]) > 1 - 1e-6, $"{model.Id} {clip.Motion} {track.Part}");
                    Assert.True(Math.Abs(track.Translations[0].X - track.Translations[^1].X) < 1e-6 &&
                                Math.Abs(track.Translations[0].Y - track.Translations[^1].Y) < 1e-6 &&
                                Math.Abs(track.Translations[0].Z - track.Translations[^1].Z) < 1e-6, $"{model.Id} {clip.Motion} {track.Part}");
                }
            }
    }

    [Fact]
    public void GlowIsUnlitAndOnlyWhereAPartHasGlow()
    {
        foreach (var model in ModelCatalog.All)
        {
            var json = ReadJson(StaticAssets.ModelBytes(model));
            bool anyGlow = model.Parts.Any(p => !p.Glow.IsEmpty);
            Assert.Equal(anyGlow, json["extensionsUsed"] is not null);
            Assert.Equal(anyGlow, json["materials"] is not null);
        }
    }

    [Fact]
    public void EncodingIsDeterministic()
    {
        foreach (var model in ModelCatalog.All)
            Assert.True(StaticAssets.ModelBytes(model).AsSpan().SequenceEqual(StaticAssets.ModelBytes(model)), model.Id);
    }

    [Fact]
    public void ModelIdsAreUniqueAndSafeAsFileNames()
    {
        var ids = ModelCatalog.All.Select(m => m.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var id in ids) Assert.Matches("^[a-z][a-z0-9_]*$", id);
    }
}
