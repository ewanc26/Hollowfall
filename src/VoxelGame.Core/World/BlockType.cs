namespace VoxelGame.Core.World;

public enum BlockType : byte
{
    Air = 0,
    Bedrock,
    Stone,
    Dirt,
    Grass,
    Sand,
    Sandstone,
    Snow,
    Water,
    Gravel,
    Log,
    Leaves,
    Cactus,
}

public static class BlockInfo
{
    public static bool IsSolid(BlockType b) => b != BlockType.Air && b != BlockType.Water;
}
