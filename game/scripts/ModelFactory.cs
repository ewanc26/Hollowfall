using Godot;
using Hollowfall.Core.Assets;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Models;

namespace Hollowfall.Game;

/// <summary>A built model: its root, one node per named part (rotate these to pose it), and its glowing materials.</summary>
public sealed class ModelRig
{
    public Node3D Root { get; }
    public LowPolyModel Model { get; }
    public IReadOnlyDictionary<string, Node3D> Parts { get; }

    /// <summary>Whether the geometry came from the committed model file rather than being built in code.</summary>
    public bool FromFile { get; }
    private readonly Dictionary<string, StandardMaterial3D> _glow;

    internal ModelRig(Node3D root, LowPolyModel model, Dictionary<string, Node3D> parts, Dictionary<string, StandardMaterial3D> glow, bool fromFile)
    {
        Root = root;
        Model = model;
        Parts = parts;
        _glow = glow;
        FromFile = fromFile;
    }

    public Node3D? Part(string name) => Parts.GetValueOrDefault(name);

    /// <summary>Multiplies the colour of every glowing surface, to brighten, dim or recolour a model (telegraphs, stun).</summary>
    public void SetGlow(Color tint)
    {
        foreach (var material in _glow.Values) material.AlbedoColor = tint;
    }

    /// <summary>Tints the glow of one part only (a weapon's portal gem, say).</summary>
    public void SetGlow(string part, Color tint)
    {
        if (_glow.TryGetValue(part, out var material)) material.AlbedoColor = tint;
    }
}

/// <summary>Turns Core's engine-free <see cref="LowPolyModel"/> into Godot nodes: flat-shaded lit surfaces plus unlit glow.</summary>
public static class ModelFactory
{
    private static StandardMaterial3D? _solid;

    private static StandardMaterial3D Solid => _solid ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true,
        Roughness = 0.85f,
        Metallic = 0.05f,
    };

    private static StandardMaterial3D NewGlow() => new()
    {
        VertexColorUseAsAlbedo = true,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = Colors.White,
    };

    /// <summary>Loads the model's committed file, or builds it in code when the file is missing or does not match the model.</summary>
    public static ModelRig Build(LowPolyModel model, bool castShadows = true) =>
        FromFile(model, castShadows) ?? FromCode(model, castShadows);

    private static ModelRig? FromFile(LowPolyModel model, bool castShadows)
    {
        if (GD.Load<PackedScene>("res://" + StaticAssets.ModelPath(model.Id)) is not { } scene) return null;
        if (scene.Instantiate() is not Node3D root) return null;

        var parts = new Dictionary<string, Node3D>();
        var glow = new Dictionary<string, StandardMaterial3D>();
        foreach (var part in model.Parts)
        {
            var node = root.FindChild("part_" + part.Name, true, false) as Node3D;
            var mesh = node?.GetNodeOrNull<MeshInstance3D>("mesh_" + part.Name);
            int surfaces = (part.Solid.IsEmpty ? 0 : 1) + (part.Glow.IsEmpty ? 0 : 1);
            if (node == null || (surfaces > 0 && (mesh?.Mesh?.GetSurfaceCount() ?? -1) != surfaces))
            {
                root.Free();
                return null;
            }
            parts[part.Name] = node;
            if (mesh == null) continue;

            mesh.CastShadow = castShadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
            int surface = 0;
            if (!part.Solid.IsEmpty) mesh.SetSurfaceOverrideMaterial(surface++, Solid);
            if (!part.Glow.IsEmpty)
            {
                var material = NewGlow();
                glow[part.Name] = material;
                mesh.SetSurfaceOverrideMaterial(surface, material);
            }
        }
        root.Name = model.Id;
        return new ModelRig(root, model, parts, glow, fromFile: true);
    }

    private static ModelRig FromCode(LowPolyModel model, bool castShadows)
    {
        var root = new Node3D { Name = model.Id };
        var parts = new Dictionary<string, Node3D>();
        var glow = new Dictionary<string, StandardMaterial3D>();

        foreach (var part in model.Parts)
        {
            // A child sits relative to its parent's joint, so it follows when the parent moves or leans.
            var origin = part.Parent == null ? new Vec3(0, 0, 0) : model.Part(part.Parent).Pivot;
            var node = new Node3D { Name = "part_" + part.Name, Position = new Vector3((float)(part.Pivot.X - origin.X), (float)(part.Pivot.Y - origin.Y), (float)(part.Pivot.Z - origin.Z)) };
            var mesh = new ArrayMesh();
            if (!part.Solid.IsEmpty) ChunkMeshBuilder.AddSurface(mesh, part.Solid, Solid);
            if (!part.Glow.IsEmpty)
            {
                var material = NewGlow();
                glow[part.Name] = material;
                ChunkMeshBuilder.AddSurface(mesh, part.Glow, material);
            }

            node.AddChild(new MeshInstance3D
            {
                Name = "mesh_" + part.Name,
                Mesh = mesh,
                CastShadow = castShadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            });
            (part.Parent == null ? root : parts[part.Parent]).AddChild(node);
            parts[part.Name] = node;
        }
        return new ModelRig(root, model, parts, glow, fromFile: false);
    }

    public static Vector3 ToVector(Hollowfall.Core.Combat.Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);
}
