using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Audio;

/// <summary>What the player is standing on, as far as the ears are concerned.</summary>
public enum Surface
{
    Stone,
    Brick,
    Dirt,
    Grass,
    Sand,
    Gravel,
    Snow,
    Wood,
    Water,
}

/// <summary>Which block is under the player's feet, and which sound that makes. Pure, so the choice is tested and the host only plays.</summary>
public static class Footsteps
{
    /// <summary>The surface a block makes underfoot.</summary>
    public static Surface SurfaceOf(BlockType b) => b switch
    {
        BlockType.Dirt => Surface.Dirt,
        BlockType.Grass or BlockType.Leaves or BlockType.TallGrass or BlockType.Flower => Surface.Grass,
        BlockType.Sand => Surface.Sand,
        BlockType.Gravel => Surface.Gravel,
        BlockType.Snow => Surface.Snow,
        BlockType.Log or BlockType.Cactus => Surface.Wood,
        BlockType.Water => Surface.Water,
        BlockType.BrickFloor or BlockType.StoneBrick or BlockType.Stairs or BlockType.Pillar or BlockType.Gate => Surface.Brick,
        _ => Surface.Stone,
    };

    /// <summary>The surface for a player whose feet are in <paramref name="atFeet"/> and who stands on <paramref name="below"/>. Wading in water always splashes.</summary>
    public static Surface Underfoot(BlockType below, BlockType atFeet) =>
        atFeet == BlockType.Water ? Surface.Water : SurfaceOf(below);

    public static SoundId SoundFor(Surface s) => s switch
    {
        Surface.Brick => SoundId.StepBrick,
        Surface.Dirt => SoundId.StepDirt,
        Surface.Grass => SoundId.StepGrass,
        Surface.Sand => SoundId.StepSand,
        Surface.Gravel => SoundId.StepGravel,
        Surface.Snow => SoundId.StepSnow,
        Surface.Wood => SoundId.StepWood,
        Surface.Water => SoundId.StepWater,
        _ => SoundId.StepStone,
    };

    /// <summary>Every sound a footstep can make.</summary>
    public static IReadOnlyList<SoundId> StepSounds { get; } = [.. Enum.GetValues<Surface>().Select(SoundFor)];

    public static bool IsStepSound(SoundId id) => StepSounds.Contains(id);
}

/// <summary>What the stride timer decided this tick.</summary>
public enum StepKind
{
    None,
    Step,
    Land,
}

/// <summary>
/// The rhythm of walking: one footfall for every <see cref="Length"/> blocks covered on the ground, so running steps faster than
/// walking and standing still is silent. Landing from a fall makes one heavier sound. Volume and pitch for each sound come from the
/// speed and a hash of the step count, so they vary a little and never use a random generator.
/// </summary>
public sealed class Stride
{
    /// <summary>Blocks of ground covered between footfalls.</summary>
    public const double Length = 1.9;

    /// <summary>Below this horizontal speed the player is shuffling, not stepping.</summary>
    public const double MinSpeed = 0.6;

    /// <summary>A fall slower than this (blocks per second) lands silently.</summary>
    public const double MinLandSpeed = 5.0;

    private double _distance = Length * 0.6;
    private double _peakFall;
    private bool _airborne;

    /// <summary>How many sounds have been asked for.</summary>
    public int Count { get; private set; }

    /// <summary>Loudness of the sound just asked for, 0 to 1.</summary>
    public double Volume { get; private set; }

    /// <summary>Pitch scale of the sound just asked for, near 1.</summary>
    public double Pitch { get; private set; } = 1;

    /// <summary>Advances the timer by one tick and says whether a sound is due. <paramref name="active"/> is false while flying, dead or otherwise not walking.</summary>
    public StepKind Update(double dt, double vx, double vz, double vy, bool onGround, bool active)
    {
        if (!active)
        {
            _airborne = false;
            _peakFall = 0;
            return StepKind.None;
        }

        if (!onGround)
        {
            _airborne = true;
            _peakFall = Math.Max(_peakFall, -vy);
            return StepKind.None;
        }

        if (_airborne)
        {
            _airborne = false;
            double fall = _peakFall;
            _peakFall = 0;
            _distance = 0;
            if (fall >= MinLandSpeed)
            {
                Emit(LandVolume(fall), 0.8);
                return StepKind.Land;
            }
        }

        double speed = Math.Sqrt(vx * vx + vz * vz);
        if (speed < MinSpeed)
        {
            // Standing still: the next move starts with a step soon, not after a whole stride.
            _distance = Math.Max(_distance, Length * 0.6);
            return StepKind.None;
        }

        _distance += speed * dt;
        if (_distance < Length) return StepKind.None;
        _distance -= Length; // at most one footfall per tick, however long the tick
        if (_distance > Length) _distance = 0;
        Emit(StepVolume(speed), 1);
        return StepKind.Step;
    }

    private void Emit(double volume, double pitchBase)
    {
        Count++;
        Volume = volume;
        Pitch = pitchBase * Jitter(Count);
    }

    /// <summary>Louder the faster the player moves, but never silent.</summary>
    public static double StepVolume(double speed) => Math.Clamp(0.45 + 0.4 * speed / 4.3, 0.45, 1.0);

    /// <summary>Louder the harder the fall.</summary>
    public static double LandVolume(double fallSpeed) => Math.Clamp(0.5 + fallSpeed / 24.0, 0.5, 1.0);

    /// <summary>A pitch scale between 0.92 and 1.08 for the n-th sound, so repeated footfalls do not sound like one sample.</summary>
    public static double Jitter(int count) => 0.92 + 0.16 * SeedHash.ToUnit(SeedHash.Hash(0x57E9, count, 0, 11));
}
