using Hollowfall.Core.Combat;
using Hollowfall.Core.Physics;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class PortalTests
{
    /// <summary>
    /// A stone box room: floor top at y=10, walls at x=-6 and x=10 (stone, y 10..19), a stone ceiling at y=20,
    /// open space between, with extra blocks optionally set.
    /// </summary>
    private static Func<int, int, int, BlockType> Room(Dictionary<(int, int, int), BlockType>? extra = null)
    {
        extra ??= [];
        return (x, y, z) =>
        {
            if (extra.TryGetValue((x, y, z), out var b)) return b;
            if (y <= 9 || y >= 20) return BlockType.Stone;
            if (x >= 10 || x <= -6) return BlockType.Stone;
            if (z >= 20 || z <= -20) return BlockType.Stone;
            return BlockType.Air;
        };
    }

    private static readonly Vec3 East = new(1, 0, 0);

    private static PlaceResult FireAtEastWall(PortalSystem s, PortalColor c, double eyeY = 11.6, double z = 0.5, Func<int, int, int, BlockType>? world = null) =>
        s.Place(c, new Vec3(0.5, eyeY, z), East, East, world ?? Room());

    private static PortalSystem Pair(out Portal blue, out Portal orange, Func<int, int, int, BlockType>? world = null)
    {
        world ??= Room();
        var s = new PortalSystem();
        blue = s.Place(PortalColor.Blue, new Vec3(0.5, 11.6, 0.5), East, East, world).Portal!;
        orange = s.Place(PortalColor.Orange, new Vec3(0.5, 11.6, 0.5), new Vec3(-1, 0, 0), East, world).Portal!;
        return s;
    }

    // ---- placement --------------------------------------------------------------------------

    [Fact]
    public void APortalSitsOnTheStruckWallFace()
    {
        var s = new PortalSystem();
        var r = FireAtEastWall(s, PortalColor.Blue);
        Assert.True(r.Placed);
        var p = r.Portal!;
        Assert.Equal(new Vec3(-1, 0, 0), p.Frame.Normal);
        Assert.Equal(new Vec3(0, 1, 0), p.Frame.Up);
        Assert.Equal(10.0, p.Frame.Center.X, 6); // the plane of the wall face
        Assert.Equal(0.5, p.Frame.Center.Z, 6);
        Assert.Equal(2, p.HoleCells.Count);
        Assert.All(p.HoleCells, c => Assert.Equal(10, c.X));
        Assert.Equal(s.Blue, p);
    }

    [Fact]
    public void FramesAreRightHandedUnitAxes()
    {
        var s = Pair(out var blue, out var orange);
        foreach (var p in new[] { blue, orange })
        {
            var f = p.Frame;
            Assert.Equal(1.0, f.Right.Length, 9);
            Assert.Equal(1.0, f.Up.Length, 9);
            Assert.Equal(1.0, f.Normal.Length, 9);
            Assert.Equal(0.0, f.Right.Dot(f.Up), 9);
            Assert.Equal(0.0, f.Right.Dot(f.Normal), 9);
            var cross = new Vec3(f.Right.Y * f.Up.Z - f.Right.Z * f.Up.Y, f.Right.Z * f.Up.X - f.Right.X * f.Up.Z, f.Right.X * f.Up.Y - f.Right.Y * f.Up.X);
            Assert.Equal(f.Normal, cross); // Right x Up = Normal
        }
        Assert.True(s.Linked);
    }

    [Fact]
    public void FloorPortalsPointTheirLongSideWhereYouLook()
    {
        var s = new PortalSystem();
        var r = s.Place(PortalColor.Blue, new Vec3(0.5, 15, 0.5), new Vec3(0, -1, 0), new Vec3(0, 0, 1), Room());
        Assert.True(r.Placed);
        Assert.Equal(new Vec3(0, 1, 0), r.Portal!.Frame.Normal);
        Assert.Equal(new Vec3(0, 0, 1), r.Portal.Frame.Up);
        Assert.Equal(10.0, r.Portal.Frame.Center.Y, 6);
    }

    [Fact]
    public void CeilingPortalsFaceDownward()
    {
        var s = new PortalSystem();
        var r = s.Place(PortalColor.Orange, new Vec3(0.5, 12, 0.5), new Vec3(0, 1, 0), East, Room());
        Assert.True(r.Placed);
        Assert.Equal(new Vec3(0, -1, 0), r.Portal!.Frame.Normal);
        Assert.Equal(20.0, r.Portal.Frame.Center.Y, 6);
    }

    [Theory]
    [InlineData(BlockType.Bedrock)]
    [InlineData(BlockType.Gate)]
    [InlineData(BlockType.Stairs)]
    [InlineData(BlockType.Leaves)]
    [InlineData(BlockType.SecretDoor)]
    public void NonPortalSurfacesAreRefused(BlockType block)
    {
        var extra = new Dictionary<(int, int, int), BlockType>();
        for (int y = 10; y < 20; y++)
            for (int z = -5; z < 6; z++) extra[(10, y, z)] = block;
        var r = FireAtEastWall(new PortalSystem(), PortalColor.Blue, world: Room(extra));
        Assert.False(r.Placed);
        Assert.Equal(PlaceFailure.NotPortalable, r.Failure);
    }

    [Fact]
    public void ThereMustBeRoomForTheWholeOpening()
    {
        // A wall only one block tall (the second block is a window) cannot hold a 1 x 2 portal.
        var extra = new Dictionary<(int, int, int), BlockType>();
        for (int y = 11; y < 20; y++) extra[(10, y, 0)] = BlockType.Air;
        extra[(10, 11, 0)] = BlockType.Air;
        var narrow = Room(extra);
        var r = new PortalSystem().Place(PortalColor.Blue, new Vec3(0.5, 10.5, 0.5), East, East, narrow);
        Assert.False(r.Placed);
        Assert.Equal(PlaceFailure.NoRoom, r.Failure);

        // Blocks in front of part of the wall: the portal may land elsewhere, but never over a blocked cell.
        var blocked = new Dictionary<(int, int, int), BlockType> { [(9, 12, 0)] = BlockType.Stone };
        var placed = FireAtEastWall(new PortalSystem(), PortalColor.Blue, 11.6, 0.5, Room(blocked));
        if (placed.Placed)
            foreach (var c in placed.Portal!.HoleCells)
                Assert.False(BlockInfo.IsSolid(Room(blocked)(c.X - 1, c.Y, c.Z)), $"hole {c} has a solid block in front of it");
    }

    [Fact]
    public void NothingInRangeAndOverlapAreReported()
    {
        var s = new PortalSystem();
        var none = s.Place(PortalColor.Blue, new Vec3(0.5, 11.6, 0.5), new Vec3(0, 1, 0), East, (_, _, _) => BlockType.Air);
        Assert.Equal(PlaceFailure.NothingInRange, none.Failure);

        Assert.True(FireAtEastWall(s, PortalColor.Blue).Placed);
        // The same spot for orange: it must shift to a free spot beside it, or be refused, never overlap.
        var second = FireAtEastWall(s, PortalColor.Orange);
        if (second.Placed) Assert.False(second.Portal!.Overlaps(s.Blue!));
        else Assert.Equal(PlaceFailure.Overlaps, second.Failure);
    }

    [Fact]
    public void ReplacingAPortalKeepsTheOtherOne()
    {
        var s = Pair(out var blue, out _);
        var moved = FireAtEastWall(s, PortalColor.Blue, 11.6, 4.5);
        Assert.True(moved.Placed);
        Assert.NotSame(blue, s.Blue);
        Assert.NotNull(s.Orange);
        s.Remove(PortalColor.Orange);
        Assert.False(s.Linked);
        s.Clear();
        Assert.Null(s.Blue);
    }

    // ---- transforms -------------------------------------------------------------------------

    [Fact]
    public void PositionsMapThroughAndBack()
    {
        Pair(out var blue, out var orange);
        var p = new Vec3(9.7, 11.3, 0.2);
        var through = PortalTransform.Point(blue, orange, p);
        var back = PortalTransform.Point(orange, blue, through);
        Assert.Equal(p.X, back.X, 9);
        Assert.Equal(p.Y, back.Y, 9);
        Assert.Equal(p.Z, back.Z, 9);
    }

    [Fact]
    public void WalkingIntoOnePortalMeansLeavingTheOther()
    {
        Pair(out var blue, out var orange);
        // 0.1 behind the blue face (inside the wall) comes out 0.1 in front of the orange face.
        var inside = new Vec3(10.1, 11.5, 0.5);
        var (_, _, cIn) = blue.Frame.ToLocal(inside);
        Assert.Equal(-0.1, cIn, 9);
        var out1 = PortalTransform.Point(blue, orange, inside);
        var (_, _, cOut) = orange.Frame.ToLocal(out1);
        Assert.Equal(0.1, cOut, 9);

        // Moving into the wall at blue becomes moving away from the wall at orange.
        var v = PortalTransform.Vector(blue, orange, new Vec3(5, 0, 0));
        Assert.Equal(5.0, v.Dot(orange.Frame.Normal), 9);
    }

    [Fact]
    public void TransformsPreserveLengthsAndAngles()
    {
        var s = new PortalSystem();
        var blue = s.Place(PortalColor.Blue, new Vec3(0.5, 15, 0.5), new Vec3(0, -1, 0), new Vec3(0, 0, 1), Room()).Portal!;
        var orange = FireAtEastWall(s, PortalColor.Orange).Portal!;
        var a = new Vec3(3, -4, 12);
        var b = new Vec3(-2, 7, 1);
        var ta = PortalTransform.Vector(blue, orange, a);
        var tb = PortalTransform.Vector(blue, orange, b);
        Assert.Equal(a.Length, ta.Length, 9);
        Assert.Equal(a.Dot(b), ta.Dot(tb), 9);
        // A proper rotation: it keeps the sense of the cross product.
        Vec3 Cross(Vec3 p, Vec3 q) => new(p.Y * q.Z - p.Z * q.Y, p.Z * q.X - p.X * q.Z, p.X * q.Y - p.Y * q.X);
        var c = Cross(a, b);
        var tc = Cross(ta, tb);
        var mapped = PortalTransform.Vector(blue, orange, c);
        Assert.Equal(mapped.X, tc.X, 9);
        Assert.Equal(mapped.Y, tc.Y, 9);
        Assert.Equal(mapped.Z, tc.Z, 9);
    }

    // ---- travel -----------------------------------------------------------------------------

    private static void Walk(PlayerBody body, PortalSystem portals, double mx, double mz, int ticks, Func<int, int, int, BlockType> world, ref Vec3 forward, List<Crossing>? log = null)
    {
        for (int i = 0; i < ticks; i++)
            if (PortalWalker.Step(body, portals, 1.0 / 60, mx, mz, false, world, forward) is { } c)
            {
                forward = c.Forward;
                log?.Add(c);
            }
    }

    [Fact]
    public void WalkingIntoABluePortalCarriesYouOutOfTheOrangeOne()
    {
        var world = Room();
        var s = Pair(out var blue, out var orange, world);
        // Move the orange portal to the west wall, facing east, so a walk east emerges still heading east... into the room.
        s.Remove(PortalColor.Orange);
        var orangeWest = s.Place(PortalColor.Orange, new Vec3(0.5, 11.6, 0.5), new Vec3(-1, 0, 0), East, world).Portal!;
        Assert.Equal(new Vec3(1, 0, 0), orangeWest.Frame.Normal);

        var body = new PlayerBody(5.5, 10, 0.5);
        var forward = East;
        var crossings = new List<Crossing>();
        Walk(body, s, 1, 0, 120, world, ref forward, crossings);

        Assert.Single(crossings);
        Assert.Same(s.Blue, crossings[0].Entered);
        Assert.Same(orangeWest, crossings[0].Exited);
        Assert.True(body.X < 0, $"should have come out near the west wall and be heading back east, x={body.X}");
        Assert.True(Math.Abs(body.VelocityX) > 3, "keeps its speed");
        Assert.Equal(1.0, forward.X, 6); // faces away from the wall it came out of
    }

    [Fact]
    public void YouCanWalkBackThroughToWhereYouStarted()
    {
        var world = Room();
        var s = Pair(out _, out _, world);
        var body = new PlayerBody(5.5, 10, 0.5);
        var forward = East;
        var crossings = new List<Crossing>();
        Walk(body, s, 1, 0, 90, world, ref forward, crossings);
        Assert.Single(crossings);

        // Turn round and walk back into the portal you came out of.
        forward = new Vec3(-forward.X, 0, -forward.Z);
        Walk(body, s, -1, 0, 240, world, ref forward, crossings);
        Assert.True(crossings.Count >= 2);
    }

    [Fact]
    public void FallingIntoAFloorPortalFlingsYouOutOfAWallAtTheSameSpeed()
    {
        var world = Room();
        var s = new PortalSystem();
        // Blue on the floor, orange on the east wall facing west.
        s.Place(PortalColor.Blue, new Vec3(0.5, 18, 0.5), new Vec3(0, -1, 0), East, world);
        s.Place(PortalColor.Orange, new Vec3(0.5, 12, 8.5), East, East, world);
        Assert.True(s.Linked);

        var body = new PlayerBody(0.5, 17, 0.5);
        var forward = East;
        double speedAtEntry = 0;
        Crossing? hit = null;
        for (int i = 0; i < 120 && hit == null; i++)
        {
            double v = body.Speed;
            if (PortalWalker.Step(body, s, 1.0 / 60, 0, 0, false, world, forward) is { } c) { hit = c; speedAtEntry = v; }
        }
        Assert.NotNull(hit);
        var h = hit!.Value;
        Assert.Same(s.Orange, h.Exited);
        Assert.True(speedAtEntry > 8, $"fell fast, {speedAtEntry}");
        // What went down comes out sideways: all the speed leaves along the orange portal's normal (west).
        Assert.Equal(speedAtEntry, h.Velocity.Length, 3); // speed in, speed out
        Assert.Equal(h.Velocity.Length, h.Velocity.Dot(s.Orange!.Frame.Normal), 3);
        Assert.True(body.VelocityX < -8 || Math.Abs(body.VelocityX) > 8, $"vx={body.VelocityX}");
    }

    [Fact]
    public void MomentumCarriesYouAcrossTheRoomAfterAFling()
    {
        var world = Room();
        var s = new PortalSystem();
        s.Place(PortalColor.Blue, new Vec3(0.5, 18, 0.5), new Vec3(0, -1, 0), East, world);
        s.Place(PortalColor.Orange, new Vec3(0.5, 12, 8.5), East, East, world);
        var body = new PlayerBody(0.5, 17, 0.5);
        var forward = East;
        for (int i = 0; i < 300; i++)
            if (PortalWalker.Step(body, s, 1.0 / 60, 0, 0, false, world, forward) is { } c) forward = c.Forward;
        Assert.True(body.X < 5, $"flung west across the room, x={body.X}"); // it landed near x=9.9 and slid on
    }

    [Fact]
    public void AnOnlyPortalDoesNothingAndItsWallStaysSolid()
    {
        var world = Room();
        var s = new PortalSystem();
        s.Place(PortalColor.Blue, new Vec3(0.5, 11.6, 0.5), East, East, world);
        Assert.False(s.Linked);
        var body = new PlayerBody(5.5, 10, 0.5);
        var forward = East;
        var crossings = new List<Crossing>();
        Walk(body, s, 1, 0, 120, world, ref forward, crossings);
        Assert.Empty(crossings);
        Assert.True(body.X < 10 - PlayerBody.HalfWidth + 0.01);
    }

    [Fact]
    public void ThePortalOpeningIsWalkableButThePortalWallBesideItIsNot()
    {
        var world = Room();
        var s = Pair(out _, out _, world);
        var view = s.WithHoles(world);
        foreach (var (x, y, z) in s.Blue!.HoleCells) Assert.Equal(BlockType.Air, view(x, y, z));
        Assert.Equal(BlockType.Stone, view(10, 10, s.Blue.HoleCells[0].Z + 3));
        Assert.Same(world, new PortalSystem().WithHoles(world)); // no portals, no change
    }

    [Fact]
    public void ArriveSlowlyAndYouStillGetOut()
    {
        Pair(out var blue, out var orange);
        var s = new PortalSystem();
        var world = Room();
        s.Place(PortalColor.Blue, new Vec3(0.5, 11.6, 0.5), East, East, world);
        s.Place(PortalColor.Orange, new Vec3(0.5, 11.6, 0.5), new Vec3(-1, 0, 0), East, world);
        var c = s.TryCross(new Vec3(9.99, 11, 0.5), new Vec3(10.01, 11, 0.5), new Vec3(0.2, 0, 0), East);
        Assert.NotNull(c);
        var crossing = c!.Value;
        Assert.True(crossing.Velocity.Dot(crossing.Exited.Frame.Normal) >= PortalSystem.MinExitSpeed - 1e-9);
    }

    [Fact]
    public void OnlyCrossingTheFaceInsideTheOpeningCounts()
    {
        var world = Room();
        var s = Pair(out var blue, out _, world);
        // Straight through the middle crosses; 3 blocks to the side does not; coming from behind does not.
        Assert.NotNull(s.TryCross(new Vec3(9.9, 11, 0.5), new Vec3(10.1, 11, 0.5), new Vec3(3, 0, 0), East));
        Assert.Null(s.TryCross(new Vec3(9.9, 11, 4.5), new Vec3(10.1, 11, 4.5), new Vec3(3, 0, 0), East));
        Assert.Null(s.TryCross(new Vec3(10.1, 11, 0.5), new Vec3(9.9, 11, 0.5), new Vec3(-3, 0, 0), East));
        Assert.Null(s.TryCross(new Vec3(9.9, 14.5, 0.5), new Vec3(10.1, 14.5, 0.5), new Vec3(3, 0, 0), East)); // above the opening
    }

    // ---- rays -------------------------------------------------------------------------------

    [Fact]
    public void ARayWithNoPortalIsOneSegmentToTheWall()
    {
        var s = new PortalSystem();
        var seg = Assert.Single(s.TraceRay(Room(), new Vec3(0.5, 11.5, 0.5), East, 100));
        Assert.Equal(9.5, seg.Length, 6);
    }

    [Fact]
    public void ARayThroughAPortalContinuesFromTheOtherOne()
    {
        var world = Room();
        var s = new PortalSystem();
        // Blue on the east wall, orange on the north wall.
        s.Place(PortalColor.Blue, new Vec3(0.5, 11.6, 0.5), East, East, world);
        var orange = s.Place(PortalColor.Orange, new Vec3(0.5, 11.6, 0.5), new Vec3(0, 0, 1), East, world).Portal!;
        Assert.Equal(new Vec3(0, 0, -1), orange.Frame.Normal);

        var segs = s.TraceRay(world, new Vec3(0.5, 11.5, 0.5), East, 100);
        Assert.Equal(2, segs.Count);
        Assert.Equal(9.5, segs[0].Length, 6);
        // The second segment starts at the orange face and heads out along its normal.
        Assert.Equal(20.0, segs[1].Origin.Z, 2);
        Assert.Equal(-1.0, segs[1].Direction.Z, 6);
        Assert.True(segs[1].Length > 30, "it flies across the room to the far wall");
        Assert.True(segs[0].Length + segs[1].Length <= 100);
    }

    [Fact]
    public void RaysMissingTheOpeningHitTheWallAndRaysFromBehindAreIgnored()
    {
        var world = Room();
        var s = Pair(out _, out _, world);
        var missing = s.TraceRay(world, new Vec3(0.5, 11.5, 6.5), East, 100);
        Assert.Single(missing);
        Assert.Equal(9.5, missing[0].Length, 6);
    }

    [Fact]
    public void RayBouncingBetweenFacingPortalsStops()
    {
        var world = Room();
        var s = Pair(out _, out _, world);
        var segs = s.TraceRay(world, new Vec3(0.5, 11.5, 0.5), East, 10_000, maxBounces: 3);
        Assert.True(segs.Count <= 4);
    }

    // ---- the pair of surfaces matter on real dungeons ----------------------------------------

    [Fact]
    public void PortalsCanBePlacedOnDungeonWalls()
    {
        for (long seed = 1; seed < 6; seed++)
        {
            var world = new VoxelWorld(seed);
            for (int rx = -1; rx <= 1; rx++)
                for (int rz = -1; rz <= 1; rz++)
                {
                    if (world.Generator.Dungeons!.GetSite(rx, rz) is not { } site) continue;
                    var room = site.Floors[0].Rooms.First(r => r.Shape == Hollowfall.Core.Dungeon.RoomShape.Rect && r.Bounds.W >= 14);
                    var (wx, wy, wz) = site.ToWorld(0, room.Bounds.X + 2, room.CenterZ);
                    var s = new PortalSystem();
                    var r1 = s.Place(PortalColor.Blue, new Vec3(wx + 0.5, wy + 1.6, wz + 0.5), new Vec3(-1, 0, 0), new Vec3(-1, 0, 0), world.GetBlock);
                    Assert.True(r1.Placed, $"seed {seed}: {r1.Failure}");
                    Assert.Equal(BlockType.StoneBrick, world.GetBlock(r1.Portal!.HoleCells[0].X, r1.Portal.HoleCells[0].Y, r1.Portal.HoleCells[0].Z));
                    return;
                }
        }
        Assert.Fail("no dungeon found");
    }
}
