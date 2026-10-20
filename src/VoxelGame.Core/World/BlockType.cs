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
    CoalOre,
    IronOre,
    GoldOre,
    TallGrass,
    Flower,
    StoneBrick,
    BrickFloor,
    Door,
    SecretDoor,
    Pillar,
    Stairs,
}

public static class BlockInfo
{
    public static bool IsSolid(BlockType b) =>
        b != BlockType.Air && b != BlockType.Water && b != BlockType.Door && !IsPlant(b);

    /// <summary>Plants are walk-through decorations that sit on top of the ground.</summary>
    public static bool IsPlant(BlockType b) => b == BlockType.TallGrass || b == BlockType.Flower;
}
