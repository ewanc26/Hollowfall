using Godot;
using Hollowfall.Core.Assets;
using Hollowfall.Core.Textures;
using Hollowfall.Core.Meshing;

namespace Hollowfall.Game;

/// <summary>Converts Core's engine-free chunk meshes into Godot meshes. Must run on the main thread.</summary>
public static class ChunkMeshBuilder
{
    private static StandardMaterial3D? _opaque, _cutout;

    private static ImageTexture? _atlas;

    /// <summary>The block texture atlas, drawn by Core and uploaded once.</summary>
    public static ImageTexture Atlas => _atlas ??= ImageTexture.CreateFromImage(
        GD.Load<Texture2D>("res://" + StaticAssets.AtlasPath)?.GetImage()
        ?? Image.CreateFromData(BlockTextures.Width, BlockTextures.Height, false, Image.Format.Rgba8, BlockTextures.Rgba));

    private static StandardMaterial3D Textured(Action<StandardMaterial3D>? tweak = null)
    {
        var m = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = Atlas,
            // Tiles are patterns of light and shade a little below white; the gain brings the average back to the palette colour.
            AlbedoColor = new Color(BlockTextures.Gain, BlockTextures.Gain, BlockTextures.Gain),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Roughness = 1f,
        };
        tweak?.Invoke(m);
        return m;
    }

    public static StandardMaterial3D Opaque => _opaque ??= Textured();

    public static StandardMaterial3D Cutout => _cutout ??= Textured(m =>
    {
        m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        m.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
        m.AlphaScissorThreshold = 0.5f;
    });

    /// <summary>Animated inside the water tile by a code-built shader; see <see cref="EffectsWater"/>.</summary>
    public static Material Water => EffectsWater.Material;

    /// <summary>Returns null for an empty chunk. Each non-empty buffer becomes one surface with its own material.</summary>
    public static ArrayMesh? ToArrayMesh(ChunkMesh chunk)
    {
        if (chunk.IsEmpty) return null;
        var mesh = new ArrayMesh();
        AddSurface(mesh, chunk.Opaque, Opaque);
        AddSurface(mesh, chunk.Cutout, Cutout);
        AddSurface(mesh, chunk.Water, Water);
        return mesh;
    }

    /// <summary>Adds one buffer as a surface with its own material. Core winds counter-clockwise; Godot wants clockwise, so triangles are reversed.</summary>
    public static void AddSurface(ArrayMesh mesh, MeshBuffer buffer, Material material)
    {
        if (buffer.IsEmpty) return;

        var p = buffer.Positions();
        var n = buffer.Normals();
        var c = buffer.Colors();
        var idx = buffer.Indices();

        var vertices = new Vector3[buffer.VertexCount];
        var normals = new Vector3[buffer.VertexCount];
        var colors = new Color[buffer.VertexCount];
        var uvs = new Vector2[buffer.VertexCount];
        var uv = buffer.Uvs();
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new Vector3(p[i * 3], p[i * 3 + 1], p[i * 3 + 2]);
            normals[i] = new Vector3(n[i * 3], n[i * 3 + 1], n[i * 3 + 2]);
            colors[i] = new Color(c[i * 4], c[i * 4 + 1], c[i * 4 + 2], c[i * 4 + 3]);
            uvs[i] = new Vector2(uv[i * 2], uv[i * 2 + 1]);
        }

        // Core winds counter-clockwise seen from outside; Godot expects clockwise.
        for (int t = 0; t < idx.Length; t += 3)
            (idx[t + 1], idx[t + 2]) = (idx[t + 2], idx[t + 1]);

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = idx;

        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, material);
    }
}
