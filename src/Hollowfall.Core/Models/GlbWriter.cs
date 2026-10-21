using System.Buffers.Binary;
using System.Text.Json.Nodes;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Meshing;

namespace Hollowfall.Core.Models;

/// <summary>
/// Writes a model as a binary glTF (.glb) file. Each part is a node placed at its pivot relative to its parent, with a child
/// node "mesh_<part>" holding the solid and glowing surfaces; glow is marked KHR_materials_unlit. Positions are already relative to
/// each pivot, and glTF shares Core's right-handed, y-up, counter-clockwise convention, so nothing is flipped.
/// </summary>
public static class GlbWriter
{
    private const uint Magic = 0x46546C67, Version = 2, JsonChunk = 0x4E4F534A, BinaryChunk = 0x004E4942;
    private const int Float = 5126, UnsignedInt = 5125;

    public static byte[] Encode(LowPolyModel model)
    {
        var state = new State(model);
        int root = state.AddNode(new JsonObject { ["name"] = model.Id });
        var children = new JsonArray();
        foreach (var part in model.Parts.Where(p => p.Parent == null)) children.Add(state.Visit(part));
        if (children.Count > 0) state.Nodes[root]["children"] = children;

        var json = new JsonObject
        {
            ["asset"] = new JsonObject { ["version"] = "2.0", ["generator"] = "Hollowfall" },
            ["scene"] = 0,
            ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = new JsonArray(root) }),
            ["nodes"] = new JsonArray(state.Nodes.Cast<JsonNode>().ToArray()),
            ["meshes"] = new JsonArray(state.Meshes.Cast<JsonNode>().ToArray()),
            ["accessors"] = new JsonArray(state.Accessors.Cast<JsonNode>().ToArray()),
            ["bufferViews"] = new JsonArray(state.Views.Cast<JsonNode>().ToArray()),
            ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = state.Bin.Count }),
        };
        if (state.Glowing)
        {
            json["materials"] = new JsonArray(new JsonObject
            {
                ["name"] = "glow",
                ["pbrMetallicRoughness"] = new JsonObject { ["baseColorFactor"] = new JsonArray(1, 1, 1, 1) },
                ["extensions"] = new JsonObject { ["KHR_materials_unlit"] = new JsonObject() },
            });
            json["extensionsUsed"] = new JsonArray("KHR_materials_unlit");
        }

        byte[] jsonBytes = Pad(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString()), 0x20);
        byte[] binBytes = Pad(state.Bin.ToArray(), 0);

        var output = new byte[12 + 8 + jsonBytes.Length + 8 + binBytes.Length];
        var span = output.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span[0..], Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], Version);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)output.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)jsonBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], JsonChunk);
        jsonBytes.CopyTo(output, 20);
        int binHeader = 20 + jsonBytes.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(span[binHeader..], (uint)binBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(binHeader + 4)..], BinaryChunk);
        binBytes.CopyTo(output, binHeader + 8);
        return output;
    }

    private static byte[] Pad(byte[] data, byte fill)
    {
        int padded = (data.Length + 3) & ~3;
        if (padded == data.Length) return data;
        var result = new byte[padded];
        data.CopyTo(result, 0);
        Array.Fill(result, fill, data.Length, padded - data.Length);
        return result;
    }

    private sealed class State
    {
        private readonly LowPolyModel _model;

        public State(LowPolyModel model) => _model = model;

        public List<JsonObject> Nodes { get; } = [];
        public List<JsonObject> Meshes { get; } = [];
        public List<JsonObject> Accessors { get; } = [];
        public List<JsonObject> Views { get; } = [];
        public List<byte> Bin { get; } = [];
        public bool Glowing { get; private set; }

        public int AddNode(JsonObject node)
        {
            Nodes.Add(node);
            return Nodes.Count - 1;
        }

        public int Visit(ModelPart part)
        {
            var origin = part.Parent == null ? new Vec3(0, 0, 0) : _model.Part(part.Parent).Pivot;
            var offset = part.Pivot - origin;
            var node = new JsonObject
            {
                ["name"] = "part_" + part.Name,
                ["translation"] = new JsonArray(offset.X, offset.Y, offset.Z),
            };
            int index = AddNode(node);

            var children = new JsonArray();
            if (!part.Solid.IsEmpty || !part.Glow.IsEmpty)
                children.Add(AddNode(new JsonObject { ["name"] = "mesh_" + part.Name, ["mesh"] = AddMesh(part) }));
            foreach (var child in _model.Parts.Where(p => p.Parent == part.Name)) children.Add(Visit(child));
            if (children.Count > 0) node["children"] = children;
            return index;
        }

        private int AddMesh(ModelPart part)
        {
            var primitives = new JsonArray();
            if (!part.Solid.IsEmpty) primitives.Add(Primitive(part.Solid, glow: false));
            if (!part.Glow.IsEmpty)
            {
                Glowing = true;
                primitives.Add(Primitive(part.Glow, glow: true));
            }
            Meshes.Add(new JsonObject { ["name"] = part.Name, ["primitives"] = primitives });
            return Meshes.Count - 1;
        }

        private JsonObject Primitive(MeshBuffer buffer, bool glow)
        {
            var positions = buffer.Positions();
            var min = new[] { float.MaxValue, float.MaxValue, float.MaxValue };
            var max = new[] { float.MinValue, float.MinValue, float.MinValue };
            for (int i = 0; i < positions.Length; i++)
            {
                min[i % 3] = Math.Min(min[i % 3], positions[i]);
                max[i % 3] = Math.Max(max[i % 3], positions[i]);
            }

            var attributes = new JsonObject
            {
                ["POSITION"] = Accessor(Floats(positions), Float, buffer.VertexCount, "VEC3", min, max),
                ["NORMAL"] = Accessor(Floats(buffer.Normals()), Float, buffer.VertexCount, "VEC3"),
                ["COLOR_0"] = Accessor(Floats(buffer.Colors()), Float, buffer.VertexCount, "VEC4"),
            };
            var primitive = new JsonObject
            {
                ["attributes"] = attributes,
                ["indices"] = Accessor(Unsigned(buffer.Indices()), UnsignedInt, buffer.TriangleCount * 3, "SCALAR"),
            };
            if (glow) primitive["material"] = 0;
            return primitive;
        }

        private int Accessor(byte[] data, int componentType, int count, string type, float[]? min = null, float[]? max = null)
        {
            while (Bin.Count % 4 != 0) Bin.Add(0);
            Views.Add(new JsonObject { ["buffer"] = 0, ["byteOffset"] = Bin.Count, ["byteLength"] = data.Length });
            Bin.AddRange(data);

            var accessor = new JsonObject
            {
                ["bufferView"] = Views.Count - 1,
                ["componentType"] = componentType,
                ["count"] = count,
                ["type"] = type,
            };
            if (min != null && max != null)
            {
                accessor["min"] = new JsonArray(min.Select(v => (JsonNode)(double)v).ToArray());
                accessor["max"] = new JsonArray(max.Select(v => (JsonNode)(double)v).ToArray());
            }
            Accessors.Add(accessor);
            return Accessors.Count - 1;
        }

        private static byte[] Floats(float[] values)
        {
            var bytes = new byte[values.Length * 4];
            for (int i = 0; i < values.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), values[i]);
            return bytes;
        }

        private static byte[] Unsigned(int[] values)
        {
            var bytes = new byte[values.Length * 4];
            for (int i = 0; i < values.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), (uint)values[i]);
            return bytes;
        }
    }
}
