using Hollowfall.Core.Combat;
using Hollowfall.Core.Physics;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

/// <summary>Two portals on the floor must be a loop you can leave, never a pit you are stuck in.</summary>
public class PortalEscapeTests
{
    private const double Dt = 1.0 / 60;

    private static BlockType Room(int x, int y, int z) =>
        y <= 9 || y >= 30 || x >= 30 || x <= -30 || z >= 30 || z <= -30 ? BlockType.Stone : BlockType.Air;

    private static (PortalSystem Portals, Portal A, Portal B) FloorPair(double gap = 5)
    {
        var portals = new PortalSystem();
        var a = portals.Place(PortalColor.Teal, new Vec3(0.5, 14, 0.5), new Vec3(0, -1, 0), new Vec3(1, 0, 0), Room).Portal!;
        var b = portals.Place(PortalColor.Violet, new Vec3(0.5 + gap, 14, 0.5), new Vec3(0, -1, 0), new Vec3(1, 0, 0), Room).Portal!;
        Assert.True(portals.Linked);
        return (portals, a, b);
    }

    [Fact]
    public void ABodyNeverEmergesWithItsFeetInsideTheExitOpening()
    {
        var (portals, a, b) = FloorPair();
        var body = new PlayerBody(a.Frame.Center.X, 13, a.Frame.Center.Z);
        int crossings = 0;
        for (int i = 0; i < 60 * 15; i++)
        {
            var crossing = PortalWalker.Step(body, portals, Dt, 0, 0, false, Room, new Vec3(0, -1, 0));
            if (crossing is { } c)
            {
                crossings++;
                double planeY = c.Exited.Frame.Center.Y;
                Assert.True(body.Y >= planeY - 1e-9, $"crossing {crossings}: feet at {body.Y:F2} are below the exit plane {planeY:F2}");
            }
        }
        Assert.True(crossings > 5, "the loop should still run when nobody steers");
    }

    [Fact]
    public void SteeringLeavesTheLoopAndLandsOnTheFloor()
    {
        var (portals, a, b) = FloorPair();
        var body = new PlayerBody(a.Frame.Center.X, 13, a.Frame.Center.Z);
        double escapedAt = -1;
        for (int i = 0; i < 60 * 8; i++)
        {
            PortalWalker.Step(body, portals, Dt, -1, 0, false, Room, new Vec3(0, -1, 0)); // away from the other portal
            bool outsideBoth = Math.Abs(body.X - a.Frame.Center.X) > 2.5 && Math.Abs(body.X - b.Frame.Center.X) > 2.5;
            if (escapedAt < 0 && outsideBoth && body.OnGround) escapedAt = i * Dt;
        }
        Assert.True(escapedAt >= 0, $"the player is still in the loop after 8 s at ({body.X:F1}, {body.Y:F1}) crossing between the portals");
        Assert.True(body.OnGround && body.Y >= 9.99, $"the player should be standing on the floor, not in a pit (y {body.Y:F2})");
    }

    [Fact]
    public void SteeringAlsoWorksFromAFloorToACeilingPair()
    {
        var portals = new PortalSystem();
        portals.Place(PortalColor.Teal, new Vec3(0.5, 14, 0.5), new Vec3(0, -1, 0), new Vec3(1, 0, 0), Room);
        portals.Place(PortalColor.Violet, new Vec3(0.5, 14, 0.5), new Vec3(0, 1, 0), new Vec3(1, 0, 0), Room);
        Assert.True(portals.Linked);
        var body = new PlayerBody(0.5, 20, 0.5);
        for (int i = 0; i < 60 * 8; i++) PortalWalker.Step(body, portals, Dt, 1, 0, false, Room, new Vec3(0, -1, 0));
        Assert.True(Math.Abs(body.X - 1.0) > 3, $"still under the portals at x {body.X:F1}");
    }

    [Fact]
    public void AWallPairLetsYouWalkThroughAndKeepGoing()
    {
        var portals = new PortalSystem();
        var west = portals.Place(PortalColor.Teal, new Vec3(0.5, 11.5, 0.5), new Vec3(-1, 0, 0), new Vec3(0, 1, 0), Room).Portal!;
        var east = portals.Place(PortalColor.Violet, new Vec3(0.5, 11.5, 0.5), new Vec3(1, 0, 0), new Vec3(0, 1, 0), Room).Portal!;
        Assert.True(portals.Linked);
        var body = new PlayerBody(-27, 10, 0.5);
        int crossings = 0;
        for (int i = 0; i < 60 * 4; i++)
            if (PortalWalker.Step(body, portals, Dt, -1, 0, false, Room, new Vec3(-1, 0, 0)) is { } c)
            {
                crossings++;
                Assert.True((PortalWalker.Centre(body) - c.Exited.Frame.Center).Dot(c.Exited.Frame.Normal) >= PlayerBody.HalfWidth - 1e-9, "the body must be fully out of the wall it exits");
            }
        Assert.True(crossings >= 1);
        Assert.True(body.X is > 5 and < 29, $"after going through the west portal the player should come out of the east one and walk on, at x {body.X:F1}");
    }
}
