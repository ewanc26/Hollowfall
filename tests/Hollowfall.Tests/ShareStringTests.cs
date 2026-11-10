using Hollowfall.Core.Combat;
using Hollowfall.Core.Replay;

namespace Hollowfall.Tests;

public class ShareStringTests
{
    private static RunReplay Tiny(string seed, OverallGrade grade, int points, double seconds)
    {
        var frames = Enumerable.Range(0, 120).Select(i =>
            ReplayFrame.From(new PlayerView(new Vec3(i * 0.05, 10, 5), new Vec3(i * 0.05, 11.62, 5), new Vec3(1, 0, 0), false, false), default)).ToList();
        // Press the teal portal's button a few times, so portal marks have something to read.
        for (int i = 20; i < 24; i++) frames[i] = frames[i] with { Flags = frames[i].Flags | ReplayFrame.TealBit };
        return new RunReplay(seed, 3, 4, 2, 119, new FloorRecord(grade, points, seconds), frames);
    }

    [Fact]
    public void AShareStringRoundTripsThroughCodecAndBack()
    {
        var replay = Tiny("amber-hollow-12", OverallGrade.B, 420, 61.5);
        string shared = ShareString.Encode(replay);
        Assert.StartsWith("hollowfall:", shared);
        Assert.True(shared.Length <= ShareString.MaxLength);
        Assert.True(ShareString.TryDecode(shared, out var back));
        Assert.Equal((replay.Seed, replay.Floor, replay.Result.Points, replay.Result.Seconds, replay.TickCount),
            (back!.Seed, back.Floor, back.Result.Points, back.Result.Seconds, back.TickCount));
        // A whole run file is identical: the string is the file.
        Assert.Equal(ReplayCodec.Encode(replay), ReplayCodec.Encode(back));
    }

    [Fact]
    public void TheStringIsTheEncodedFile()
    {
        var replay = Tiny("sìol-daily", OverallGrade.C, 10, 30);
        string base64 = Convert.ToBase64String(ReplayCodec.Encode(replay)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Equal("hollowfall:" + base64, ShareString.Encode(replay));
    }

    [Fact]
    public void GhostTrackSeesTheRecordedPortalPresses()
    {
        var track = new GhostTrack(Tiny("seed", OverallGrade.C, 10, 2.0)); // 2 seconds: the whole floor is the 120 frames
        Assert.False(track.TealAt(10));
        Assert.True(track.TealAt(21));
        Assert.True(track.TealAt(23));
        Assert.False(track.TealAt(30));
        var eye = track.EyeAt(21);
        Assert.True(Math.Abs(eye.X - 21 * 0.05) < 1e-3 && Math.Abs(eye.Y - 11.62) < 1e-3 && Math.Abs(eye.Z - 5) < 1e-3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hollowfall:")]
    [InlineData("hollowfall:!not-base64")]
    [InlineData("hollowfall:AAAAA")] // a length no valid base64 has
    [InlineData("hollowfall:AAA=AAA")] // padding in the middle
    [InlineData("hollowfall:AA A=")] // whitespace
    [InlineData("acaistory:AAAA")]
    public void AnythingElseIsNotAShareString(string? text)
    {
        Assert.False(ShareString.TryDecode(text, out var replay));
        Assert.Null(replay);
    }

    [Fact]
    public void ADamagedBodyIsRejected()
    {
        string shared = ShareString.Encode(Tiny("good-seed", OverallGrade.D, 5, 61.5));
        char[] body = shared.ToCharArray();
        body[^1] = body[^1] == 'A' ? 'B' : 'A';
        Assert.False(ShareString.TryDecode(new string(body), out _));
    }
}
