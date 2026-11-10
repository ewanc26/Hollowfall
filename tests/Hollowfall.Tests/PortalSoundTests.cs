using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Localization;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

/// <summary>
/// The two halves of docs/features.md 2.6: sounds that know about portals (a source is heard from the closer of the direct
/// path and the path through a pair, the same reflection test the enemy shortcut uses) and the pair's non-colour cue
/// (chevrons on teal, diamonds on violet; nothing in the interface may depend on colour alone).
/// </summary>
public class PortalSoundTests
{
    /// <summary>A stone box room: floor top at y=10, walls at x=-6 and x=10, ceiling at y=20, open between; extra blocks optional.</summary>
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

    /// <summary>A linked pair facing each other across the room: teal on the east wall, violet on the west.</summary>
    private static PortalSystem Pair(Func<int, int, int, BlockType>? world = null)
    {
        world ??= Room();
        var s = new PortalSystem();
        s.Place(PortalColor.Teal, new Vec3(0.5, 11.6, 0.5), East, East, world);
        s.Place(PortalColor.Violet, new Vec3(0.5, 11.6, 0.5), new Vec3(-1, 0, 0), East, world);
        return s;
    }

    // ---- portal-aware sound -----------------------------------------------------------------

    [Fact]
    public void WithoutAPairASoundIsHeardWhereItIs()
    {
        var world = Room();
        var portals = new PortalSystem();
        var listener = new Vec3(2, 11.6, 0.5);
        var source = new Vec3(7, 11.6, 0.5);
        var hearing = PortalAcoustics.WhereHeard(listener, source, portals, world);
        Assert.Equal(source, hearing.Position);
        Assert.False(hearing.ThroughPortal);
    }

    [Fact]
    public void ALinkedPairCarriesASoundFromTheFarRoom()
    {
        var world = Room();
        var portals = Pair(world);
        Assert.True(portals.Linked);

        // The listener stands in front of the teal gate; the sound happens beside the far (violet) gate.
        var listener = portals.Teal!.Frame.Center + portals.Teal.Frame.Normal * 1;
        var source = portals.Violet!.Frame.Center + portals.Violet.Frame.Normal * 1;
        var mirrored = PortalTransform.Point(portals.Violet, portals.Teal, source);

        var hearing = PortalAcoustics.WhereHeard(listener, source, portals, world);
        Assert.True(hearing.ThroughPortal, "the sound should come through the gate the listener can see");
        Assert.Equal(mirrored.X, hearing.Position.X, 6);
        Assert.Equal(mirrored.Y, hearing.Position.Y, 6);
        Assert.Equal(mirrored.Z, hearing.Position.Z, 6);
        Assert.True(hearing.Position.DistanceTo(listener) + PortalAcoustics.CrossingCost < listener.DistanceTo(source));
    }

    [Fact]
    public void ADirectSoundCloserThanTheGateStaysDirect()
    {
        var world = Room();
        var portals = Pair(world);
        var listener = new Vec3(2, 11.6, 0.5);
        var source = new Vec3(4, 11.6, 0.5);
        var hearing = PortalAcoustics.WhereHeard(listener, source, portals, world);
        Assert.False(hearing.ThroughPortal);
        Assert.Equal(source, hearing.Position);
    }

    [Fact]
    public void AGateTheListenerCannotSeeCarriesNoSound()
    {
        var world = Room();
        var portals = Pair(world);
        var source = portals.Violet!.Frame.Center + portals.Violet.Frame.Normal * 1; // beside the far gate

        // The listener stands behind the east wall, a stone's throw from the gate: the mirrored path would be far
        // shorter, but no gate face is in sight from inside the wall, so the sound is heard the direct way.
        var hidden = portals.Teal!.Frame.Center - portals.Teal.Frame.Normal * 1.5;
        var hearing = PortalAcoustics.WhereHeard(hidden, source, portals, world);
        Assert.False(hearing.ThroughPortal, "no line of sight to a gate's face means no sound through it");
        Assert.Equal(source, hearing.Position);

        // The same listener one step the other side of the wall, with the gate in plain sight, hears it through.
        var seen = PortalAcoustics.WhereHeard(portals.Teal.Frame.Center + portals.Teal.Frame.Normal * 1.5, source, portals, world);
        Assert.True(seen.ThroughPortal);
    }

    [Fact]
    public void ThroughPortalVoicingLosesNothingButSlightlyMuffles()
    {
        Assert.InRange(PortalAcoustics.ThroughPortalVolume, 0.1, 1.0);
        Assert.InRange(PortalAcoustics.ThroughPortalPitch, 0.1, 1.0);
    }

    [Fact]
    public void TheVeilIsAShortOneShotTheSynthOwns()
    {
        Assert.False(Synth.Loops(SoundId.PortalVeil));
        var s = Synth.Render(SoundId.PortalVeil);
        Assert.True(s.Length > 0.1 * Synth.SampleRate, "the veil should be a clear short chime");
        Assert.Contains(SoundId.PortalVeil, Enum.GetValues<SoundId>());
        Assert.Equal(SoundId.PortalVeil, Enum.GetValues<SoundId>()[^1]); // appended at the end, per convention
    }

    // ---- the non-colour cue -----------------------------------------------------------------

    [Fact]
    public void TealIsChevronsAndVioletIsDiamonds()
    {
        Assert.Equal(PortalMark.Chevron, PortalMarks.Mark(PortalColor.Teal));
        Assert.Equal(PortalMark.Diamond, PortalMarks.Mark(PortalColor.Violet));
        Assert.Throws<ArgumentOutOfRangeException>(() => PortalMarks.Mark(PortalColor.Rift));
    }

    [Fact]
    public void TheTwoMarksAreDifferentShapesInTheSameRegion()
    {
        var chevron = PortalMarks.Notch(PortalMark.Chevron);
        var diamond = PortalMarks.Notch(PortalMark.Diamond);

        foreach (var triangles in new[] { chevron, diamond })
        {
            Assert.NotEmpty(triangles);
            foreach (var ((ax, ay), (bx, by), (cx, cy)) in triangles)
            {
                // Inside the half-a-block notch square, and never a degenerate sliver.
                Assert.All(new[] { ax, ay, bx, by, cx, cy }, v => Assert.True(Math.Abs(v) <= 0.5, "a notch must sit in its half-block square"));
                double area = Math.Abs((bx - ax) * (cy - ay) - (cx - ax) * (by - ay)) / 2;
                Assert.True(area > 0.01, "a mark must be drawn, not degenerate");
            }
        }

        static string Flat(IReadOnlyList<((double X, double Y) A, (double X, double Y) B, (double X, double Y) C)> triangles) =>
            string.Join(" ", triangles.Select(p => $"{p.A.X:F2},{p.A.Y:F2} {p.B.X:F2},{p.B.Y:F2} {p.C.X:F2},{p.C.Y:F2}"));
        Assert.NotEqual(Flat(chevron), Flat(diamond));
    }

    [Fact]
    public void TheMarksHaveHudWordsHeldBackUntilASpeakerChecksThem()
    {
        foreach (var (mark, word) in new[] { (PortalMark.Chevron, "chevron"), (PortalMark.Diamond, "diamond") })
        {
            var entry = LocTable.Find(PortalMarks.MarkKey(mark));
            Assert.NotNull(entry);
            Assert.Equal(word, entry!.English);
            Assert.False(entry.HasGaelic, "the mark words stay English until a speaker blesses a rendering");
            Assert.False(entry.HasIrish, "the mark words stay English in the Irish table too");
        }
    }
}
