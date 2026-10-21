using Godot;
using Hollowfall.Core.Assets;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Models;

namespace Hollowfall.Game;

/// <summary>A built model: its root, one node per named part (rotate these to pose it), its glowing materials, and its clips.</summary>
public sealed class ModelRig
{
    public Node3D Root { get; }
    public LowPolyModel Model { get; }
    public IReadOnlyDictionary<string, Node3D> Parts { get; }

    /// <summary>Plays the clips baked into the model; null for a model with none.</summary>
    public AnimationPlayer? Animator { get; }
    private readonly Dictionary<string, StandardMaterial3D> _glow;

    internal ModelRig(Node3D root, LowPolyModel model, Dictionary<string, Node3D> parts, Dictionary<string, StandardMaterial3D> glow, AnimationPlayer? animator)
    {
        Root = root;
        Model = model;
        Parts = parts;
        _glow = glow;
        Animator = animator;
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

    public static ModelRig Build(LowPolyModel model, bool castShadows = true)
    {
        var root = Instantiate(model);
        root.Name = model.Id;
        var parts = new Dictionary<string, Node3D>();
        var glow = new Dictionary<string, StandardMaterial3D>();
        foreach (var part in model.Parts)
        {
            var node = (Node3D)root.FindChild("part_" + part.Name, true, false)!;
            parts[part.Name] = node;
            if (node.GetNodeOrNull<MeshInstance3D>("mesh_" + part.Name) is not { } mesh) continue;

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

        var animator = root.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        if (animator != null)
            foreach (var name in animator.GetAnimationList())
                animator.GetAnimation(name).LoopMode = Motions.Loops(Enum.Parse<Motion>(name, ignoreCase: true))
                    ? Animation.LoopModeEnum.Linear
                    : Animation.LoopModeEnum.None;
        return new ModelRig(root, model, parts, glow, animator);
    }

    /// <summary>The committed scene when the file exists; otherwise the same bytes Core writes for it, imported the same way.</summary>
    private static Node3D Instantiate(LowPolyModel model)
    {
        string path = "res://" + StaticAssets.ModelPath(model.Id);
        if (ResourceLoader.Exists(path) && GD.Load<PackedScene>(path).Instantiate() is Node3D imported) return imported;

        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromBuffer(StaticAssets.ModelBytes(model), "", state) != Error.Ok)
            throw new InvalidOperationException($"Could not import the {model.Id} model");
        return (Node3D)document.GenerateScene(state);
    }

    public static Vector3 ToVector(Hollowfall.Core.Combat.Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);
}
