using Hollowfall.Core.Audio;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.Textures;

namespace Hollowfall.Tests;

public class BedTests
{
    [Theory]
    [MemberData(nameof(BedIds))]
    public void EveryBedJoinsWithoutAClick(SoundId id)
    {
        var s = Synth.Render(id);
        double join = Math.Abs(s[0] - s[^1]);
        double biggest = 0;
        for (int i = 1; i < s.Length; i++) biggest = Math.Max(biggest, Math.Abs(s[i] - s[i - 1]));
        Assert.True(join <= biggest * 1.05, $"{id}: the loop join steps {join:F4} against a normal maximum of {biggest:F4}");
    }

    [Theory]
    [MemberData(nameof(BedIds))]
    public void EveryBedIsTheLengthOfItsLoop(SoundId id) =>
        Assert.Equal((int)(Beds.Seconds * Synth.SampleRate), Synth.Render(id).Length);

    [Theory]
    [MemberData(nameof(BedIds))]
    public void EveryBedSitsAtAQuietBedLevel(SoundId id)
    {
        var s = Synth.Render(id);
        double peak = s.Max(v => Math.Abs(v));
        double rms = Math.Sqrt(s.Sum(v => (double)v * v) / s.Length);
        double dbfs = 20 * Math.Log10(rms);
        Assert.True(peak < 0.9, $"{id} peaks at {peak:F3}");
        Assert.InRange(dbfs, -36, -12);
    }

    [Fact]
    public void EachSurfaceBiomeHasItsOwnBed()
    {
        var beds = Enum.GetValues<Biome>().Select(b => Beds.For(b, null)).ToList();
        Assert.Equal(Enum.GetValues<Biome>().Length, beds.Distinct().Count());
    }

    [Fact]
    public void EachDungeonThemeHasItsOwnBed()
    {
        var beds = Enum.GetValues<DungeonTheme>().Select(t => Beds.For(Biome.Plains, t)).ToList();
        Assert.Equal(Enum.GetValues<DungeonTheme>().Length, beds.Distinct().Count());
    }

    [Fact]
    public void UndergroundTakesTheDungeonBedOverTheSurface() =>
        Assert.Equal(Beds.For(Biome.Desert, DungeonTheme.Mossy), Beds.For(Biome.Forest, DungeonTheme.Mossy));

    public static IEnumerable<object[]> BedIds() => Beds.All.Select(id => new object[] { id });
}
