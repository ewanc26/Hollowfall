using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Effects;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class FootstepTests
{
    [Fact]
    public void EveryBlockHasASurfaceAndEverySurfaceItsOwnSound()
    {
        foreach (var b in Enum.GetValues<BlockType>()) Assert.True(Enum.IsDefined(Footsteps.SurfaceOf(b)));
        var sounds = Enum.GetValues<Surface>().Select(Footsteps.SoundFor).ToList();
        Assert.Equal(sounds.Count, sounds.Distinct().Count());
        Assert.All(sounds, id => Assert.True(Footsteps.IsStepSound(id)));
        Assert.Equal(sounds.Count, Footsteps.StepSounds.Count);
    }

    [Theory]
    [InlineData(BlockType.Stone, Surface.Stone)]
    [InlineData(BlockType.BrickFloor, Surface.Brick)]
    [InlineData(BlockType.Grass, Surface.Grass)]
    [InlineData(BlockType.Dirt, Surface.Dirt)]
    [InlineData(BlockType.Sand, Surface.Sand)]
    [InlineData(BlockType.Gravel, Surface.Gravel)]
    [InlineData(BlockType.Snow, Surface.Snow)]
    [InlineData(BlockType.Log, Surface.Wood)]
    public void BlocksMakeTheExpectedSurface(BlockType block, Surface surface) => Assert.Equal(surface, Footsteps.SurfaceOf(block));

    [Fact]
    public void WadingAlwaysSplashesWhateverIsBelow()
    {
        Assert.Equal(Surface.Water, Footsteps.Underfoot(BlockType.Sand, BlockType.Water));
        Assert.Equal(Surface.Sand, Footsteps.Underfoot(BlockType.Sand, BlockType.Air));
        Assert.Equal(Surface.Dirt, Footsteps.Underfoot(BlockType.Dirt, BlockType.TallGrass)); // plants at the feet do not change what is underfoot
    }

    private static int CountSteps(Stride stride, double seconds, double speed, double dt = 1 / 60.0)
    {
        int n = 0;
        for (double t = 0; t < seconds; t += dt)
            if (stride.Update(dt, speed, 0, 0, onGround: true, active: true) == StepKind.Step) n++;
        return n;
    }

    [Fact]
    public void StandingStillIsSilent()
    {
        var stride = new Stride();
        for (int i = 0; i < 600; i++) Assert.Equal(StepKind.None, stride.Update(1 / 60.0, 0, 0, 0, true, true));
    }

    [Fact]
    public void ARunStepsFasterThanAWalkAndDistanceDecidesTheCount()
    {
        int walk = CountSteps(new Stride(), 10, 4.3);
        int run = CountSteps(new Stride(), 10, 8.6);
        Assert.InRange(walk, 21, 24); // 43 blocks / 1.9, plus the early first step
        Assert.True(run > walk * 1.8);
    }

    [Fact]
    public void LongTicksStillGiveAtMostOneStep()
    {
        var stride = new Stride();
        Assert.Equal(StepKind.Step, stride.Update(5.0, 4.3, 0, 0, true, true));
        Assert.Equal(1, stride.Count);
    }

    [Fact]
    public void TheAirAndFlightAreSilentAndWalkingStartsWithAStepSoon()
    {
        var stride = new Stride();
        for (int i = 0; i < 300; i++) Assert.Equal(StepKind.None, stride.Update(1 / 60.0, 4.3, 0, 2, onGround: false, active: true));
        var flying = new Stride();
        for (int i = 0; i < 300; i++) Assert.Equal(StepKind.None, flying.Update(1 / 60.0, 14, 0, 0, onGround: true, active: false));

        var fresh = new Stride();
        int ticks = 0;
        while (fresh.Update(1 / 60.0, 4.3, 0, 0, true, true) != StepKind.Step) ticks++;
        Assert.True(ticks < 40, $"first step took {ticks} ticks");
    }

    [Fact]
    public void LandingMakesOneSoundOnlyForARealFall()
    {
        var hard = new Stride();
        hard.Update(1 / 60.0, 0, 0, -9, false, true);
        Assert.Equal(StepKind.Land, hard.Update(1 / 60.0, 0, 0, 0, true, true));
        Assert.Equal(StepKind.None, hard.Update(1 / 60.0, 0, 0, 0, true, true));
        Assert.True(hard.Volume > 0.8);

        var hop = new Stride();
        hop.Update(1 / 60.0, 0, 0, -1.5, false, true);
        Assert.Equal(StepKind.None, hop.Update(1 / 60.0, 0, 0, 0, true, true));
    }

    [Fact]
    public void VolumeAndPitchAreBoundedDeterministicAndFollowSpeed()
    {
        Assert.InRange(Stride.StepVolume(0), 0.4, 1.0);
        Assert.True(Stride.StepVolume(8) > Stride.StepVolume(3));
        Assert.Equal(1.0, Stride.StepVolume(100));
        Assert.True(Stride.LandVolume(20) > Stride.LandVolume(6));
        var pitches = Enumerable.Range(0, 200).Select(Stride.Jitter).ToList();
        Assert.All(pitches, p => Assert.InRange(p, 0.92, 1.08));
        Assert.True(pitches.Distinct().Count() > 150);
        Assert.Equal(Stride.Jitter(17), Stride.Jitter(17));
    }

    [Fact]
    public void StepSoundsAreShortAndQuieterThanGunfire()
    {
        foreach (var id in Footsteps.StepSounds)
        {
            Assert.True(Synth.Render(id).Length <= Synth.SampleRate * 0.4, $"{id} is too long for a footstep");
            Assert.True(SoundMap.Gain(id) < SoundMap.Gain(SoundId.RevolverShot));
        }
    }
}

public class ShardTests
{
    private static BlockType Floor(int x, int y, int z) => y < 10 ? BlockType.Stone : BlockType.Air;

    private static ShardField Burst(EnemyKind kind, long seed, int capacity = 64)
    {
        var field = new ShardField(capacity);
        Gibs.Burst(field, kind, 4.5, 10, 4.5, seed);
        return field;
    }

    [Fact]
    public void BurstsAreDeterministicAndSeedsDiffer()
    {
        Assert.Equal(Burst(EnemyKind.Husk, 5).Items, Burst(EnemyKind.Husk, 5).Items);
        Assert.NotEqual(Burst(EnemyKind.Husk, 5).Items, Burst(EnemyKind.Husk, 6).Items);
    }

    [Fact]
    public void BiggerEnemiesBreakIntoMoreShardsInTheirOwnColours()
    {
        Assert.Equal(Gibs.GibCount(EnemyKind.Husk), Burst(EnemyKind.Husk, 1).AliveCount);
        Assert.True(Gibs.GibCount(EnemyKind.Warden) > Gibs.GibCount(EnemyKind.Husk));
        Assert.Equal(Gibs.GibCount(EnemyKind.Warden), Burst(EnemyKind.Warden, 1).AliveCount);

        foreach (var kind in Enum.GetValues<EnemyKind>())
        {
            var palette = Gibs.Palette(kind);
            Assert.True(palette.Count >= 4);
            var field = Burst(kind, 9);
            foreach (var s in field.Items.Where(s => s.Alive))
            {
                var allowed = palette.Select(h => (((h >> 16) & 0xFF) / 255f, ((h >> 8) & 0xFF) / 255f, (h & 0xFF) / 255f)).ToList();
                Assert.Contains((s.R, s.G, s.B), allowed);
            }
        }
        Assert.NotEqual(Gibs.Palette(EnemyKind.Husk), Gibs.Palette(EnemyKind.Spitter));
    }

    [Fact]
    public void ShardsStartInsideTheBodyAndFlyOutward()
    {
        var spec = EnemySpec.Of(EnemyKind.Husk);
        foreach (var s in Burst(EnemyKind.Husk, 3).Items.Where(s => s.Alive))
        {
            Assert.InRange(s.Y, 10, 10 + spec.Height);
            Assert.InRange(Math.Abs(s.X - 4.5), 0, spec.HalfWidth + 1e-4);
            Assert.True(s.Vy > 3, "every shard should leave the corpse upward");
            Assert.True(s.Size > 0.05f && s.Size < 0.5f);
        }
    }

    [Fact]
    public void ShardsLandOnTheFloorRestAndNeverEnterSolidBlocks()
    {
        var field = Burst(EnemyKind.Warden, 2, 64);
        for (int i = 0; i < 90; i++)
        {
            field.Step(1 / 60f, Floor);
            foreach (var s in field.Items.Where(s => s.Alive))
                Assert.True(s.Y >= 10 - 1e-3f, $"a shard fell through the floor to {s.Y}");
        }
        var lying = field.Items.Where(s => s.Alive && s.Resting).ToList();
        Assert.NotEmpty(lying);
        Assert.All(lying, s => Assert.Equal(10f + s.Size / 2, s.Y, 3));
    }

    [Fact]
    public void ShardsStopAtWalls()
    {
        // A wall at x >= 6: shards thrown at it from x = 4.5 must stay on this side.
        BlockType Walled(int x, int y, int z) => y < 10 || x >= 6 ? BlockType.Stone : BlockType.Air;
        var field = Burst(EnemyKind.Warden, 4);
        for (int i = 0; i < 240; i++) field.Step(1 / 60f, Walled);
        foreach (var s in field.Items.Where(s => s.Alive)) Assert.True(s.X < 6f, $"a shard went through the wall to {s.X}");
    }

    [Fact]
    public void EverythingFadesAndTheFieldEmptiesWithinItsLongestLife()
    {
        var field = Burst(EnemyKind.Warden, 8);
        Gibs.Sparks(field, 4.5, 11, 4.5, HitZone.Head, killed: false, 8);
        for (int i = 0; i < 60 * 4; i++) field.Step(1 / 60f, Floor);
        Assert.Equal(0, field.AliveCount);
    }

    [Fact]
    public void ScaleDwindlesAtTheEndOfALife()
    {
        var gib = new Shard { Alive = true, Kind = ShardKind.Gib, Life = 2, Age = 1 };
        Assert.Equal(1f, gib.Scale);
        gib.Age = 1.9f;
        Assert.InRange(gib.Scale, 0.01f, 0.5f);
        var spark = new Shard { Alive = true, Kind = ShardKind.Spark, Life = 0.4f, Age = 0.2f };
        Assert.Equal(0.5f, spark.Scale, 3);
        Assert.Equal(0f, new Shard().Scale);
    }

    [Fact]
    public void ThePoolRecyclesTheOldestAndNeverGrows()
    {
        var field = new ShardField(10);
        for (int i = 0; i < 5; i++) Gibs.Burst(field, EnemyKind.Warden, 0, 10, 0, i);
        Assert.Equal(10, field.AliveCount);
        Assert.Equal(10, field.Items.Length);
    }

    [Fact]
    public void SparksAreBrightShortAndTheirColourFollowsTheHit()
    {
        var body = new ShardField(32);
        Gibs.Sparks(body, 1, 12, 1, HitZone.Torso, false, 1);
        var head = new ShardField(32);
        Gibs.Sparks(head, 1, 12, 1, HitZone.Head, false, 1);
        var kill = new ShardField(32);
        Gibs.Sparks(kill, 1, 12, 1, HitZone.Torso, true, 1);

        Assert.Equal(Gibs.SparksPerHit, body.AliveCount);
        Assert.Equal(Gibs.SparksPerHeadshot, head.AliveCount);
        Assert.Equal(Gibs.SparksPerKillingBlow, kill.AliveCount);
        foreach (var s in body.Items.Concat(head.Items).Concat(kill.Items).Where(s => s.Alive))
        {
            Assert.Equal(ShardKind.Spark, s.Kind);
            Assert.InRange(s.Life, 0.3f, 0.6f);
            Assert.True(s.R >= 0.99f && Math.Max(s.G, s.B) > 0.1f);
            Assert.Equal(1f, s.X);
        }
        Assert.NotEqual((body.Items[0].R, body.Items[0].G, body.Items[0].B), (head.Items[0].R, head.Items[0].G, head.Items[0].B));
    }
}
