using Hollowfall.Core.Physics;
using Hollowfall.Core.Topology;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class GluedBoxTests
{
    private const double Dt = 1.0 / 60;
    private const int Width = 8;
    private const int Depth = 6;

    private static BlockType Empty(int x, int y, int z) => BlockType.Air;

    /// <summary>Walks in +X until the body has crossed the seam <paramref name="crossings"/> times.</summary>
    private static void WalkAcross(GluedBox box, PlayerBody body, int crossings)
    {
        int seen = 0;
        for (int tick = 0; tick < 4000 && seen < crossings; tick++)
            if (box.Step(body, Dt, 1, 0)) seen++;
        Assert.Equal(crossings, seen);
    }

    [Fact]
    public void WalkingRoundAFlatBoxReturnsToTheSameSpot()
    {
        var box = new GluedBox(Width, Depth, Empty, mirrored: false);
        var body = new PlayerBody(1, 0, 2);

        WalkAcross(box, body, 1);

        Assert.True(Math.Abs(body.Z - 2) < 1e-6, $"z {body.Z}");
        Assert.InRange(body.X, 0, Width);
    }

    [Fact]
    public void WalkingRoundTheMirroredSeamComesBackSidewaysFlipped()
    {
        var box = new GluedBox(Width, Depth, Empty, mirrored: true);
        var body = new PlayerBody(1, 0, 2);

        WalkAcross(box, body, 1);
        Assert.True(Math.Abs(body.Z - (Depth - 2)) < 1e-6, $"after one lap z {body.Z}");
        Assert.True(body.VelocityX > 0, "still walking forward after the seam");

        WalkAcross(box, body, 1);
        Assert.True(Math.Abs(body.Z - 2) < 1e-6, $"after two laps z {body.Z}");
    }

    [Fact]
    public void MomentumCarriedThroughTheMirroredSeamKeepsItsDirectionFlipped()
    {
        var box = new GluedBox(Width, Depth, Empty, mirrored: true);
        var body = new PlayerBody(0, 0, 2);
        body.Teleport(Width - 0.2, 0, 2, 10, 0, 3);

        bool crossed = false;
        for (int tick = 0; tick < 60 && !crossed; tick++)
            crossed = box.Step(body, Dt, 0, 0);

        Assert.True(crossed);
        Assert.True(body.VelocityX > 0);
        Assert.True(body.VelocityZ < 0, $"sideways speed {body.VelocityZ} should now point back");
        Assert.True(Math.Abs(body.Z - (Depth - 2)) < 0.3, $"z {body.Z}");
    }

    [Fact]
    public void CellsPastAMirroredSeamAreReadFromTheFarSideFlipped()
    {
        BlockType Pillar(int x, int y, int z) => x == 0 && y == 0 && z == 3 ? BlockType.Stone : BlockType.Air;
        var box = new GluedBox(Width, Depth, Pillar, mirrored: true);

        Assert.Equal(BlockType.Stone, box.BlockAt(Width, 0, 2));
        Assert.Equal(BlockType.Air, box.BlockAt(Width, 0, 3));
        Assert.Equal(BlockType.Stone, box.BlockAt(-Width, 0, 2));
        Assert.Equal(BlockType.Stone, box.BlockAt(0, -1, 0));
        Assert.Equal(BlockType.Stone, box.BlockAt(1, 0, Depth));
    }
}
