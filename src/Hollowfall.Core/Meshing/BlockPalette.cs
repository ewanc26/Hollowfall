using Hollowfall.Core.World;

namespace Hollowfall.Core.Meshing;

/// <summary>The base colour of each block and face. It tints the block's texture, which supplies the pattern.</summary>
public static class BlockPalette
{
    public readonly record struct Rgb(float R, float G, float B);

    public static Rgb Color(BlockType b, int face) => b switch
    {
        BlockType.Bedrock => new(0.16f, 0.16f, 0.17f),
        BlockType.Stone => new(0.50f, 0.50f, 0.52f),
        BlockType.Dirt => new(0.45f, 0.32f, 0.22f),
        BlockType.Grass => face == FaceTop ? new(0.36f, 0.64f, 0.25f) : face == FaceBottom ? new(0.45f, 0.32f, 0.22f) : new(0.42f, 0.45f, 0.24f),
        BlockType.Sand => new(0.87f, 0.82f, 0.58f),
        BlockType.Sandstone => new(0.80f, 0.74f, 0.50f),
        BlockType.Snow => new(0.95f, 0.97f, 0.98f),
        BlockType.Water => new(0.20f, 0.40f, 0.78f),
        BlockType.Gravel => new(0.55f, 0.53f, 0.51f),
        BlockType.Log => face is FaceTop or FaceBottom ? new(0.62f, 0.50f, 0.32f) : new(0.38f, 0.28f, 0.17f),
        BlockType.Leaves => new(0.16f, 0.44f, 0.16f),
        BlockType.Cactus => new(0.24f, 0.52f, 0.28f),
        BlockType.CoalOre or BlockType.IronOre or BlockType.GoldOre => new(0.50f, 0.50f, 0.52f), // stone; the flecks are in the texture
        BlockType.TallGrass => new(0.42f, 0.70f, 0.28f),
        BlockType.Flower => new(0.90f, 0.35f, 0.48f),
        BlockType.StoneBrick or BlockType.SecretDoor => new(0.43f, 0.42f, 0.45f),
        BlockType.BrickFloor => new(0.34f, 0.32f, 0.36f),
        BlockType.Pillar => new(0.62f, 0.60f, 0.58f),
        BlockType.Stairs => new(0.55f, 0.50f, 0.46f),
        BlockType.Gate => new(0.62f, 0.18f, 0.16f),
        _ => new(1f, 0f, 1f), // magenta marks a block with no colour yet
    };

    public const int FaceTop = 2;
    public const int FaceBottom = 3;
}
