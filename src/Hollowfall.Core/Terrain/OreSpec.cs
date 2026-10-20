using Hollowfall.Core.World;

namespace Hollowfall.Core.Terrain;

/// <summary>
/// Describes one ore. Veins are blobs where a 3D noise field exceeds
/// <see cref="Threshold"/>; a higher threshold means rarer, smaller veins.
/// </summary>
public sealed record OreSpec(BlockType Block, int MinY, int MaxY, double Scale, double Threshold, int Salt)
{
    public static readonly IReadOnlyList<OreSpec> Defaults =
    [
        new(BlockType.CoalOre, 5, 100, 1.0 / 5.0, 0.57, 20),
        new(BlockType.IronOre, 5, 64, 1.0 / 4.5, 0.61, 21),
        new(BlockType.GoldOre, 3, 28, 1.0 / 4.0, 0.68, 22),
    ];
}
