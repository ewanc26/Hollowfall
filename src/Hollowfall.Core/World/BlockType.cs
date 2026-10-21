namespace Hollowfall.Core.World;

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
    Gate,

    /// <summary>Iron spikes on the floor of a pit (appended: block values never shift).</summary>
    Spikes,

    /// <summary>A brick floor a slam has cracked; a second slam soon after breaks it (appended: block values never shift).</summary>
    CrackedFloor,
}

public static class BlockInfo
{
    public static bool IsSolid(BlockType b) =>
        b != BlockType.Air && b != BlockType.Water && b != BlockType.Door && !IsPlant(b);

    /// <summary>Plants are walk-through decorations that sit on top of the ground.</summary>
    public static bool IsPlant(BlockType b) => b == BlockType.TallGrass || b == BlockType.Flower;

    /// <summary>Hides the faces of neighbouring blocks: a solid, full, non-see-through cube.</summary>
    public static bool Occludes(BlockType b) => IsSolid(b) && b != BlockType.Leaves;

    /// <summary>What a Block Mortar shell can dig out: brick, stone, earth and sand. Never bedrock, plates, gates or door frames.</summary>
    public static bool Breakable(BlockType b) =>
        b is BlockType.Stone or BlockType.StoneBrick or BlockType.BrickFloor or BlockType.CrackedFloor or BlockType.Dirt
            or BlockType.Grass or BlockType.Sand;

    /// <summary>Surfaces a portal can be placed on: plain solid walls, floors and ceilings.</summary>
    public static bool Portalable(BlockType b) =>
        IsSolid(b) && b is not (BlockType.Bedrock or BlockType.Gate or BlockType.SecretDoor or BlockType.Stairs or BlockType.Pillar
            or BlockType.Leaves or BlockType.Cactus or BlockType.Snow or BlockType.Log or BlockType.Spikes);
}
