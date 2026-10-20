using System.Collections.Concurrent;
using Godot;
using Hollowfall.Core.Meshing;
using Hollowfall.Core.World;

namespace Hollowfall.Game;

/// <summary>
/// Keeps the chunks around a focus point meshed and on screen. Meshing runs on worker threads
/// (Core's mesher is pure); only creating Godot objects happens here on the main thread.
/// </summary>
public partial class WorldView : Node3D
{
    private const int MaxMeshesPerFrame = 4;
    private const int MaxJobs = 4;

    private readonly Dictionary<(int, int), MeshInstance3D?> _shown = [];
    private readonly HashSet<(int, int)> _pending = [];
    private readonly ConcurrentQueue<((int X, int Z) Key, ChunkMesh Mesh)> _finished = new();
    private int _jobs;

    public VoxelWorld World { get; private set; } = null!;
    public Vector3 Focus { get; set; }

    /// <summary>Chunks within this many chunks of the focus are shown.</summary>
    public int ViewRadius { get; set; } = 6;

    public int ShownChunks => _shown.Count;
    public int PendingChunks => _pending.Count;

    public void Initialize(VoxelWorld world) => World = world;

    public override void _Process(double delta)
    {
        if (World == null) return;

        int fx = (int)Math.Floor(Focus.X) >> 4, fz = (int)Math.Floor(Focus.Z) >> 4;
        QueueMissingChunks(fx, fz);
        Receive();
        Unload(fx, fz);
    }

    private void QueueMissingChunks(int fx, int fz)
    {
        // Nearest first, so the ground under the player appears before the horizon.
        var wanted = new List<(int X, int Z, int D)>();
        for (int dx = -ViewRadius; dx <= ViewRadius; dx++)
            for (int dz = -ViewRadius; dz <= ViewRadius; dz++)
            {
                int d = dx * dx + dz * dz;
                if (d > ViewRadius * ViewRadius) continue;
                var key = (fx + dx, fz + dz);
                if (!_shown.ContainsKey(key) && !_pending.Contains(key)) wanted.Add((key.Item1, key.Item2, d));
            }

        foreach (var (x, z, _) in wanted.OrderBy(w => w.D))
        {
            if (Volatile.Read(ref _jobs) >= MaxJobs) break;
            var key = (x, z);
            _pending.Add(key);
            Interlocked.Increment(ref _jobs);
            Task.Run(() =>
            {
                try { _finished.Enqueue((key, ChunkMesher.Build(key.x, key.z, World.GetBlock))); }
                finally { Interlocked.Decrement(ref _jobs); }
            });
        }
    }

    private void Receive()
    {
        for (int i = 0; i < MaxMeshesPerFrame && _finished.TryDequeue(out var done); i++)
        {
            _pending.Remove(done.Key);
            MeshInstance3D? instance = null;
            if (ChunkMeshBuilder.ToArrayMesh(done.Mesh) is { } mesh)
            {
                instance = new MeshInstance3D
                {
                    Mesh = mesh,
                    Position = new Vector3(done.Key.X * Chunk.Size, 0, done.Key.Z * Chunk.Size),
                    Name = $"Chunk_{done.Key.X}_{done.Key.Z}",
                };
                AddChild(instance);
            }
            _shown[done.Key] = instance;
        }
    }

    private void Unload(int fx, int fz)
    {
        int limit = (ViewRadius + 2) * (ViewRadius + 2);
        foreach (var key in _shown.Keys.ToList())
        {
            int dx = key.Item1 - fx, dz = key.Item2 - fz;
            if (dx * dx + dz * dz <= limit) continue;
            _shown[key]?.QueueFree();
            _shown.Remove(key);
            World.Unload(key.Item1, key.Item2);
        }
    }
}
