using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class RiftPlateTests
{
    [Theory]
    [InlineData(BlockType.Stone)]
    [InlineData(BlockType.StoneBrick)]
    [InlineData(BlockType.BrickFloor)]
    [InlineData(BlockType.RiftPlate)]
    public void MostSurfacesAcceptAPortalOnTheShallowFloors(BlockType b) =>
        Assert.True(BlockInfo.Portalable(b));

    [Theory]
    [InlineData(BlockType.Obsidian)]
    [InlineData(BlockType.Bedrock)]
    [InlineData(BlockType.Air)]
    public void ObsidianAndTheRestRefuseAPortal(BlockType b) =>
        Assert.False(BlockInfo.Portalable(b));

    [Theory]
    [InlineData(BlockType.Stone)]
    [InlineData(BlockType.StoneBrick)]
    [InlineData(BlockType.BrickFloor)]
    [InlineData(BlockType.Obsidian)]
    public void DeepFloorsTakeAPortalOnlyOnAPlate(BlockType b) =>
        Assert.False(BlockInfo.Portalable(b, platesOnly: true));

    [Fact]
    public void ADeepPlateTakesAPortal() =>
        Assert.True(BlockInfo.Portalable(BlockType.RiftPlate, platesOnly: true));

    [Fact]
    public void PlatesAndObsidianAreNotBrokenByAMortar()
    {
        Assert.False(BlockInfo.Breakable(BlockType.RiftPlate));
        Assert.False(BlockInfo.Breakable(BlockType.Obsidian));
    }
}
