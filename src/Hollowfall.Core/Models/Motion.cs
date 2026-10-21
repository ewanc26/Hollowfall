using System.Numerics;
using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Models;

/// <summary>A pose a model can be shown in. Each one is a clip in the model's committed file; the game picks one from the simulation.</summary>
public enum Motion { Idle, Walk, Flee, Warn, Windup, Strike, Stunned }

/// <summary>One part's keys in a clip: rotation, translation in the glTF node's space, and a uniform scale.</summary>
public sealed record PartTrack(string Part, Quaternion[] Rotations, Vec3[] Translations, double[] Scales);

/// <summary>A baked clip. Looping clips repeat their first key at the end, so the wrap is seamless.</summary>
public sealed record MotionClip(Motion Motion, double Duration, double[] Times, IReadOnlyList<PartTrack> Tracks);

public static class Motions
{
    /// <summary>Length of a looping idle-style clip, in seconds; its sinusoids complete whole cycles within it.</summary>
    public const double LoopSeconds = 6;

    /// <summary>One stride: the clip the game scrubs through as the creature moves.</summary>
    public const double StrideSeconds = 1;

    /// <summary>How long a blow's arms take to swing down from the raised pose.</summary>
    public const double StrikeSeconds = 0.17;

    public static string Name(Motion motion) => motion.ToString().ToLowerInvariant();

    public static bool Loops(Motion motion) => motion is not (Motion.Windup or Motion.Strike);

    /// <summary>Walk and flee clips are played by scrubbing to the creature's stride, so the feet match its speed.</summary>
    public static bool FollowsStride(Motion motion) => motion is Motion.Walk or Motion.Flee;

    /// <summary>Samples a pose at each time and bakes the keys. Parts the pose names must exist in the model.</summary>
    public static MotionClip Bake(LowPolyModel model, Motion motion, double duration, int frames, Func<double, Pose> sample)
    {
        var times = new double[frames + 1];
        var poses = new Pose[frames + 1];
        for (int i = 0; i <= frames; i++)
        {
            times[i] = duration * i / frames;
            poses[i] = sample(times[i]);
        }

        var names = poses.SelectMany(p => p.Parts).ToHashSet();
        foreach (var name in names)
            if (!model.HasPart(name)) throw new InvalidOperationException($"{model.Id} has no part '{name}' for the {Name(motion)} motion");

        var tracks = new List<PartTrack>();
        foreach (var part in model.Parts.Where(p => names.Contains(p.Name)))
        {
            // Keys are absolute in the node's space: the rest offset from its parent's joint, plus the posed offset.
            var rest = part.Parent == null ? part.Pivot : new Vec3(part.Pivot.X - model.Part(part.Parent).Pivot.X, part.Pivot.Y - model.Part(part.Parent).Pivot.Y, part.Pivot.Z - model.Part(part.Parent).Pivot.Z);
            var rotations = new Quaternion[poses.Length];
            var translations = new Vec3[poses.Length];
            var scales = new double[poses.Length];
            for (int i = 0; i < poses.Length; i++)
            {
                var joint = poses[i].At(part.Name);
                rotations[i] = Euler(joint.Rotation);
                translations[i] = new Vec3(rest.X + joint.Offset.X, rest.Y + joint.Offset.Y, rest.Z + joint.Offset.Z);
                scales[i] = joint.Scale;
            }
            tracks.Add(new PartTrack(part.Name, rotations, translations, scales));
        }
        return new MotionClip(motion, duration, times, tracks);
    }

    /// <summary>Degrees about Y, then X, then Z: the order the engine uses for its own rotations, so authored angles mean the same on both sides.</summary>
    private static Quaternion Euler(Vec3 degrees) =>
        Quaternion.CreateFromAxisAngle(Vector3.UnitY, Radians(degrees.Y)) *
        Quaternion.CreateFromAxisAngle(Vector3.UnitX, Radians(degrees.X)) *
        Quaternion.CreateFromAxisAngle(Vector3.UnitZ, Radians(degrees.Z));

    private static float Radians(double degrees) => (float)(degrees * Math.PI / 180);
}

/// <summary>What a part is doing at one moment: a rotation in degrees, an offset from rest, and a uniform scale.</summary>
public sealed class Pose
{
    private readonly Dictionary<string, PartPose> _parts = [];

    public IEnumerable<string> Parts => _parts.Keys;

    public void Rotate(string part, double x, double y = 0, double z = 0) => Of(part).Rotation = new Vec3(x, y, z);

    public void Move(string part, double x, double y, double z) => Of(part).Offset = new Vec3(x, y, z);

    public void Scale(string part, double scale) => Of(part).Scale = scale;

    internal PartPose At(string part) => _parts.GetValueOrDefault(part) ?? new PartPose();

    private PartPose Of(string part)
    {
        if (!_parts.TryGetValue(part, out var pose)) _parts[part] = pose = new PartPose();
        return pose;
    }
}

internal sealed class PartPose
{
    public Vec3 Rotation { get; set; }
    public Vec3 Offset { get; set; }
    public double Scale { get; set; } = 1;
}
