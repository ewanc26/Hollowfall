using System.Buffers.Binary;
using System.Text.Json.Nodes;
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
            var glb = GlbWriter.Encode(model);
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
    public void GlowIsUnlitAndOnlyWhereAPartHasGlow()
    {
        foreach (var model in ModelCatalog.All)
        {
            var json = ReadJson(GlbWriter.Encode(model));
            bool anyGlow = model.Parts.Any(p => !p.Glow.IsEmpty);
            Assert.Equal(anyGlow, json["extensionsUsed"] is not null);
            Assert.Equal(anyGlow, json["materials"] is not null);
        }
    }

    [Fact]
    public void EncodingIsDeterministic()
    {
        foreach (var model in ModelCatalog.All)
            Assert.True(GlbWriter.Encode(model).AsSpan().SequenceEqual(GlbWriter.Encode(model)), model.Id);
    }

    [Fact]
    public void ModelIdsAreUniqueAndSafeAsFileNames()
    {
        var ids = ModelCatalog.All.Select(m => m.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var id in ids) Assert.Matches("^[a-z][a-z0-9_]*$", id);
    }
}
