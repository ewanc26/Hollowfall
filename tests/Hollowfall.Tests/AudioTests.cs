using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Input;
using Hollowfall.Core.Portals;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Tests;

public class SynthTests
{
    public static TheoryData<SoundId> Sounds => [.. Enum.GetValues<SoundId>()];

    [Theory]
    [MemberData(nameof(Sounds))]
    public void EverySoundIsAudibleBoundedAndFinite(SoundId id)
    {
        var s = Synth.Render(id);
        Assert.NotEmpty(s);
        Assert.All(s, v => Assert.True(float.IsFinite(v) && Math.Abs(v) <= 1f));
        double rms = Math.Sqrt(s.Sum(v => (double)v * v) / s.Length);
        Assert.True(rms > 0.01, $"{id} is too quiet (rms {rms:F4})");
        Assert.True(s.Length <= Synth.SampleRate * 4.5, $"{id} is too long");
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void RenderingIsDeterministic(SoundId id) => Assert.Equal(Synth.Render(id), Synth.Render(id));

    [Theory]
    [MemberData(nameof(Sounds))]
    public void OneShotsStartAndEndAtSilenceSoTheyNeverClick(SoundId id)
    {
        if (Synth.Loops(id)) return;
        var s = Synth.Render(id);
        Assert.True(Math.Abs(s[0]) < 0.02f, $"{id} starts with a click");
        Assert.True(Math.Abs(s[^1]) < 0.02f, $"{id} ends with a click");
    }

    [Fact]
    public void TheAmbienceLoopsSeamlessly()
    {
        var s = Synth.Render(SoundId.Ambience);
        Assert.Equal((int)(Synth.AmbienceSeconds * Synth.SampleRate), s.Length);
        // The sample after the last is the first, so the step across the join must be as small as any other step.
        double join = Math.Abs(s[0] - s[^1]);
        double biggest = 0;
        for (int i = 1; i < s.Length; i++) biggest = Math.Max(biggest, Math.Abs(s[i] - s[i - 1]));
        Assert.True(join <= biggest * 1.05, $"the loop join steps {join:F4} against a normal maximum of {biggest:F4}");
    }

    [Fact]
    public void OnlyTheAmbienceLoops() =>
        Assert.Equal([SoundId.Ambience], Enum.GetValues<SoundId>().Where(Synth.Loops));

    [Fact]
    public void SoundsAreDistinctFromOneAnother()
    {
        var all = Enum.GetValues<SoundId>().ToDictionary(id => id, Synth.Render);
        foreach (var a in all)
            foreach (var b in all)
                if (a.Key < b.Key)
                    Assert.False(a.Value.Length == b.Value.Length && a.Value.SequenceEqual(b.Value), $"{a.Key} and {b.Key} are the same sound");
    }

    [Fact]
    public void TheShotgunIsLongerAndLouderThanTheRevolver()
    {
        var revolver = Synth.Render(SoundId.RevolverShot);
        var shotgun = Synth.Render(SoundId.ShotgunShot);
        Assert.True(shotgun.Length > revolver.Length);
        double Energy(float[] s) => s.Sum(v => (double)v * v);
        Assert.True(Energy(shotgun) > Energy(revolver));
    }

    [Fact]
    public void GlidesCoverTheirPitchRange()
    {
        // A downward sweep (the kill) has more zero crossings early than late.
        var kill = Synth.Render(SoundId.Kill);
        int Crossings(int from, int to)
        {
            int c = 0;
            for (int i = from + 1; i < to; i++) if (kill[i - 1] < 0 != kill[i] < 0) c++;
            return c;
        }
        int third = kill.Length / 3;
        Assert.True(Crossings(0, third) > Crossings(2 * third, kill.Length));
    }
}

public class SoundMapTests
{
    private static readonly Vec3 V = new(0, 0, 0);

    [Fact]
    public void CombatEventsMapToTheRightSounds()
    {
        Assert.Equal(SoundId.RevolverShot, SoundMap.For(new ShotFired("revolver", V, V)));
        Assert.Equal(SoundId.ShotgunShot, SoundMap.For(new ShotFired("shotgun", V, V)));
        Assert.Equal(SoundId.PortalTeal, SoundMap.For(new PortalPlaced(PortalColor.Teal, true, V, V, V, default)));
        Assert.Equal(SoundId.PortalViolet, SoundMap.For(new PortalPlaced(PortalColor.Violet, true, V, V, V, default)));
        Assert.Equal(SoundId.PortalFail, SoundMap.For(new PortalPlaced(PortalColor.Teal, false, V, V, V, default)));
        Assert.Equal(SoundId.Headshot, SoundMap.For(new EnemyHit(1, HitZone.Head, 10, false, V)));
        Assert.Equal(SoundId.Hit, SoundMap.For(new EnemyHit(1, HitZone.Torso, 10, false, V)));
        Assert.Null(SoundMap.For(new EnemyHit(1, HitZone.Head, 10, true, V))); // the kill sound covers it
        Assert.Equal(SoundId.Kill, SoundMap.For(new EnemyKilled(1, EnemyKind.Husk)));
        Assert.Equal(SoundId.Explosion, SoundMap.For(new ProjectileGone(1, true, V)));
        Assert.Null(SoundMap.For(new ProjectileGone(1, false, V)));
        Assert.Equal(SoundId.KeyPickup, SoundMap.For(new KeyCollected(0)));
        Assert.Equal(SoundId.ChestOpen, SoundMap.For(new ChestOpened(0, 30, false)));
        Assert.Equal(SoundId.ChestLocked, SoundMap.For(new ChestLocked(0)));
        Assert.Equal(SoundId.TrapStrike, SoundMap.For(new TrapSprung(0, 22)));
    }

    [Fact]
    public void EveryGameplaySoundIsUsedByAnEvent()
    {
        var events = new CombatEvent[]
        {
            new ShotFired("revolver", V, V), new ShotFired("shotgun", V, V),
            new PortalPlaced(PortalColor.Teal, true, V, V, V, default), new PortalPlaced(PortalColor.Violet, true, V, V, V, default),
            new PortalPlaced(PortalColor.Teal, false, V, V, V, default), new PortalTraversed("player", PortalColor.Teal),
            new EnemyTelegraph(1), new EnemyHit(1, HitZone.Head, 1, false, V), new EnemyHit(1, HitZone.Torso, 1, false, V),
            new EnemyKilled(1, EnemyKind.Husk), new PlayerHurt(1, 1), new PlayerDied(), new Parried(ParryKind.Melee, 0),
            new ProjectileFired(1, V), new ProjectileGone(1, true, V), new EncounterStarted(0, 0, 1), new EncounterCleared(0, 0),
            new KeyCollected(0), new ChestOpened(0, 1, false), new ChestLocked(0), new TrapSprung(0, 1),
            new MeteorImpact(1, 30, 75, 180, false, 0.7),
            new EnemySevered(1, EnemyKind.Husk, PortalColor.Teal, 30, true, V, 250),
        };
        var used = events.Select(SoundMap.For).Where(s => s != null).Select(s => s!.Value).ToHashSet();
        // Floor completion needs a result object; UI and ambience sounds are played by the host directly.
        var exempt = new[] { SoundId.FloorComplete, SoundId.UiMove, SoundId.UiSelect, SoundId.Ambience }.Concat(Footsteps.StepSounds);
        foreach (var id in Enum.GetValues<SoundId>().Except(exempt)) Assert.Contains(id, used);
    }

    [Fact]
    public void GainsAreSensible()
    {
        foreach (var id in Enum.GetValues<SoundId>()) Assert.InRange(SoundMap.Gain(id), 0.1, 1.0);
    }
}

public class VolumeSettingsTests
{
    [Fact]
    public void VolumesClampAndRoundTrip()
    {
        var s = new GameSettings { MasterVolume = 2, EffectsVolume = -1, AmbienceVolume = 0.3 };
        Assert.Equal(1, s.MasterVolume);
        Assert.Equal(0, s.EffectsVolume);
        var back = GameSettings.FromJson(s.ToJson());
        Assert.Equal(1, back.MasterVolume);
        Assert.Equal(0, back.EffectsVolume);
        Assert.Equal(0.3, back.AmbienceVolume, 6);
    }

    [Fact]
    public void MissingVolumesGetDefaults()
    {
        var s = GameSettings.FromJson("{}");
        Assert.Equal(GameSettings.VolumeRange.Default, s.MasterVolume);
        Assert.Equal(1.0, s.EffectsVolume);
        Assert.Equal(GameSettings.AmbienceRange.Default, s.AmbienceVolume);
    }
}

public class SeedWordsTests
{
    [Fact]
    public void WordsAreStableReadableAndVaried()
    {
        Assert.Equal(SeedWords.From(5), SeedWords.From(5));
        var seeds = Enumerable.Range(0, 200).Select(i => SeedWords.From((ulong)i)).ToHashSet();
        Assert.True(seeds.Count > 150);
        Assert.All(seeds, s => Assert.Matches("^[a-z]+-[a-z]+-\\d{3}$", s));
    }

    [Fact]
    public void CleaningMakesAUsableSeed()
    {
        Assert.Equal("voxel", SeedWords.Clean(null));
        Assert.Equal("voxel", SeedWords.Clean("   "));
        Assert.Equal("abc", SeedWords.Clean("  a\tb\nc "), ignoreCase: false);
        Assert.Equal(SeedWords.MaxLength, SeedWords.Clean(new string('x', 500)).Length);
        Assert.Equal("amber-hollow-914", SeedWords.Clean("amber-hollow-914"));
    }

    [Fact]
    public void ASeedRoundTripsThroughSettingsAndDrivesTheWorld()
    {
        var s = new GameSettings { Seed = "  Deep Delve 7 " };
        Assert.Equal("Deep Delve 7", s.Seed);
        var back = GameSettings.FromJson(s.ToJson());
        Assert.Equal("Deep Delve 7", back.Seed);
        Assert.Equal(Hollowfall.Core.Terrain.TerrainGenerator.SeedFromString(back.Seed), Hollowfall.Core.Terrain.TerrainGenerator.SeedFromString(s.Seed));
        Assert.Equal("voxel", GameSettings.FromJson("{\"seed\": 12}").Seed);
    }
}
