namespace Hollowfall.Core.Meshing;

/// <summary>
/// Triangle soup for one material. Positions are chunk-local (x and z in 0..16, y in 0..128), right-handed
/// with y up, and triangles wind counter-clockwise seen from outside. Engines that use the opposite
/// winding (Godot) must reverse each triangle.
/// </summary>
public sealed class MeshBuffer
{
    private readonly List<float> _positions = [];
    private readonly List<float> _normals = [];
    private readonly List<float> _colors = [];
    private readonly List<float> _uvs = [];
    private readonly List<int> _indices = [];

    public int VertexCount => _positions.Count / 3;
    public int TriangleCount => _indices.Count / 3;
    public bool IsEmpty => _indices.Count == 0;

    public float[] Positions() => [.. _positions];

    public float[] Normals() => [.. _normals];

    /// <summary>RGBA, four floats per vertex.</summary>
    public float[] Colors() => [.. _colors];

    /// <summary>Texture coordinates, two floats per vertex (zero for geometry that is not textured, such as models).</summary>
    public float[] Uvs() => [.. _uvs];

    public int[] Indices() => [.. _indices];

    internal int AddVertex(float x, float y, float z, float nx, float ny, float nz, float r, float g, float b, float a = 1f, float u = 0f, float v = 0f)
    {
        int index = VertexCount;
        _positions.Add(x); _positions.Add(y); _positions.Add(z);
        _normals.Add(nx); _normals.Add(ny); _normals.Add(nz);
        _colors.Add(r); _colors.Add(g); _colors.Add(b); _colors.Add(a);
        _uvs.Add(u); _uvs.Add(v);
        return index;
    }

    internal void AddTriangle(int a, int b, int c)
    {
        _indices.Add(a); _indices.Add(b); _indices.Add(c);
    }
}

/// <summary>A chunk's geometry split by how it must be drawn.</summary>
public sealed class ChunkMesh
{
    /// <summary>Solid cubes, including leaves. Single-sided.</summary>
    public MeshBuffer Opaque { get; } = new();

    /// <summary>Crossed-quad plants. Needs a double-sided material.</summary>
    public MeshBuffer Cutout { get; } = new();

    /// <summary>Water surfaces. Needs alpha blending.</summary>
    public MeshBuffer Water { get; } = new();

    public bool IsEmpty => Opaque.IsEmpty && Cutout.IsEmpty && Water.IsEmpty;
}
