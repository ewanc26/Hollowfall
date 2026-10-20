using Godot;
using VoxelGame.Core.Terrain;

namespace VoxelGame.Game;

/// <summary>
/// Entry scene. Game logic lives in VoxelGame.Core; Godot scripts stay thin and only
/// connect the engine to it. Rendering arrives once Core can mesh chunks.
/// </summary>
public partial class Main : Node3D
{
    [Export] public string SeedText { get; set; } = "voxel";

    public override void _Ready()
    {
        var generator = new TerrainGenerator(TerrainGenerator.SeedFromString(SeedText));
        var chunk = generator.GenerateChunk(0, 0);
        GD.Print($"VoxelGame: seed \"{SeedText}\", chunk (0,0) top at y={chunk.TopY(8, 8)}");
    }
}
