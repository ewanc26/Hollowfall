using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Models;

/// <summary>
/// The enemies' motions, authored once here and baked into each enemy's committed file. The game plays the clips and
/// only chooses which one from the simulation, so no animation is computed in the engine.
/// </summary>
public static class EnemyMotions
{
    private const int LoopFrames = 72, StrideFrames = 64, WindupFrames = 16, StrikeFrames = 4;

    public static IReadOnlyList<MotionClip> For(LowPolyModel model)
    {
        var kind = Enum.GetValues<EnemyKind>().Cast<EnemyKind?>().FirstOrDefault(k => ModelCatalog.ForEnemy(k!.Value).Id == model.Id);
        if (kind is not { } enemy) return [];
        var spec = EnemySpec.Of(enemy);
        return Available(enemy).Select(motion => Motions.Bake(
            model,
            motion,
            DurationOf(motion, spec.WindupSeconds),
            FramesOf(motion),
            t => Pose(enemy, Beat.At(motion, t, spec.WindupSeconds)))).ToList();
    }

    private static Motion[] Available(EnemyKind kind) => kind switch
    {
        EnemyKind.Husk or EnemyKind.Warden => [Motion.Idle, Motion.Walk, Motion.Windup, Motion.Strike, Motion.Stunned],
        EnemyKind.Spitter => [Motion.Idle, Motion.Windup, Motion.Strike, Motion.Stunned],
        EnemyKind.Brownie or EnemyKind.Selkie => [Motion.Idle, Motion.Walk, Motion.Flee, Motion.Warn, Motion.Windup, Motion.Strike, Motion.Stunned],
        EnemyKind.Kelpie => [Motion.Idle, Motion.Walk, Motion.Warn, Motion.Windup, Motion.Strike, Motion.Stunned],
        EnemyKind.BeanNighe => [Motion.Idle, Motion.Flee, Motion.Stunned],
        EnemyKind.Hare => [Motion.Idle, Motion.Walk, Motion.Stunned],
        EnemyKind.CuSith => [Motion.Idle, Motion.Walk, Motion.Windup, Motion.Strike, Motion.Stunned],
        EnemyKind.Villager => [Motion.Idle, Motion.Walk, Motion.Flee, Motion.Warn, Motion.Windup, Motion.Strike, Motion.Stunned],
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static double DurationOf(Motion motion, double windupSeconds) => motion switch
    {
        Motion.Windup => windupSeconds,
        Motion.Strike => Motions.StrikeSeconds,
        Motion.Walk or Motion.Flee => Motions.StrideSeconds,
        _ => Motions.LoopSeconds,
    };

    private static int FramesOf(Motion motion) => motion switch
    {
        Motion.Windup => WindupFrames,
        Motion.Strike => StrikeFrames,
        Motion.Walk or Motion.Flee => StrideFrames,
        _ => LoopFrames,
    };

    private static Pose Pose(EnemyKind kind, Beat b) => kind switch
    {
        EnemyKind.Husk => Husk(b),
        EnemyKind.Spitter => Spitter(b),
        EnemyKind.Brownie => Biped(b, swingDegrees: 30, armFactor: 1.0, bob: 0.04),
        EnemyKind.Selkie => Biped(b, swingDegrees: 28, armFactor: 0.7, bob: 0.03),
        EnemyKind.Kelpie => Kelpie(b),
        EnemyKind.BeanNighe => Nighe(b),
        EnemyKind.Hare => Hare(b),
        EnemyKind.CuSith => Hound(b),
        EnemyKind.Villager => Biped(b, swingDegrees: 26, armFactor: 0.8, bob: 0.03),
        _ => Warden(b),
    };

    /// <summary>
    /// The state a clip is sampled at. Walk and flee read the stride; a windup reads its ease-in; a strike is the blow's
    /// decay; the rest run on the clip's own time, so looping clips use whole cycles of <see cref="Wave"/>.
    /// </summary>
    private readonly record struct Beat(double Gait, double Phase, double Time, double Raise, double Strike, bool Stunned, bool Flee, bool Warn)
    {
        public static Beat At(Motion motion, double t, double windupSeconds)
        {
            bool stride = Motions.FollowsStride(motion);
            return new Beat(
                Gait: stride ? 1 : 0,
                Phase: stride ? 2 * Math.PI * t / Motions.StrideSeconds : 0,
                Time: stride ? 0 : t,
                Raise: motion == Motion.Windup ? Ease(t / windupSeconds) : 0,
                Strike: motion == Motion.Strike ? Math.Max(0, 1 - t / Motions.StrikeSeconds) : 0,
                Stunned: motion == Motion.Stunned,
                Flee: motion == Motion.Flee,
                Warn: motion == Motion.Warn);
        }
    }

    private static double Ease(double t) => 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 3);

    /// <summary>A sinusoid that completes <paramref name="cycles"/> whole cycles in a looping clip.</summary>
    private static double Wave(double cycles, double t, double shift = 0) => Math.Sin(2 * Math.PI * cycles * t / Motions.LoopSeconds + shift);

    private static Pose Husk(Beat b)
    {
        var pose = new Pose();
        double swing = Math.Sin(b.Phase) * 38 * b.Gait;
        double idle = Wave(2, b.Time) * 2;
        pose.Rotate("leg_l", swing);
        pose.Rotate("leg_r", -swing);

        double armSwing = -swing * 0.7;
        double up = -150 * b.Raise + 60 * b.Strike;
        pose.Rotate("arm_l", b.Stunned ? 15 : armSwing * (1 - b.Raise) + up, 0, 4 + idle);
        pose.Rotate("arm_r", b.Stunned ? 15 : -armSwing * (1 - b.Raise) + up, 0, -4 - idle);

        double lean = (b.Stunned ? 14 : -6 * b.Gait) + 10 * b.Raise - 22 * b.Strike;
        pose.Rotate("torso", lean, Math.Sin(b.Phase) * 5 * b.Gait, b.Stunned ? Wave(8, b.Time) * 6 : 0);
        pose.Move("torso", 0, Math.Abs(Math.Sin(b.Phase)) * 0.05 * b.Gait, 0);
        pose.Rotate("head", b.Stunned ? 40 : 6 * b.Gait - 10 * b.Raise, 0, b.Stunned ? Wave(7, b.Time) * 8 : 0);
        return pose;
    }

    private static Pose Biped(Beat b, double swingDegrees, double armFactor, double bob)
    {
        var pose = new Pose();
        double swing = Math.Sin(b.Phase) * swingDegrees * b.Gait;
        pose.Rotate("leg_l", swing);
        pose.Rotate("leg_r", -swing);

        double up = -150 * b.Raise + 60 * b.Strike;
        double fright = b.Flee ? -120 : 0;
        double square = b.Warn ? -35 : 0;
        pose.Rotate("arm_l", b.Stunned ? 15 : -swing * armFactor * (1 - b.Raise) + up + fright + square, 0, 6);
        pose.Rotate("arm_r", b.Stunned ? 15 : swing * armFactor * (1 - b.Raise) + up + fright + square, 0, -6);

        double breathe = Wave(2, b.Time) * 1.5;
        double lean = b.Stunned ? 20 : (b.Flee ? 14 : 0) + (b.Warn ? -6 : 0);
        pose.Rotate("torso", lean + 8 * b.Raise - 18 * b.Strike + breathe, Math.Sin(b.Phase) * 4 * b.Gait, b.Stunned ? Wave(8, b.Time) * 6 : 0);
        pose.Move("torso", 0, Math.Abs(Math.Sin(b.Phase)) * bob * b.Gait, 0);
        pose.Rotate("head", b.Stunned ? 40 : (b.Flee ? -12 : 0) + Wave(1, b.Time) * 3);
        return pose;
    }

    private static Pose Kelpie(Beat b)
    {
        var pose = new Pose();
        double swing = Math.Sin(b.Phase) * 34 * b.Gait;
        // Gallop: diagonal pairs move together.
        pose.Rotate("leg_fl", swing);
        pose.Rotate("leg_br", swing);
        pose.Rotate("leg_fr", -swing);
        pose.Rotate("leg_bl", -swing);
        if (b.Raise > 0 || b.Strike > 0)
        {
            // It rears onto its hind legs for the blow, forelegs pawing, then crashes down.
            pose.Rotate("leg_fl", -70 * b.Raise + 40 * b.Strike);
            pose.Rotate("leg_fr", -70 * b.Raise + 40 * b.Strike);
        }

        pose.Rotate("body", b.Stunned ? 12 : -28 * b.Raise + 18 * b.Strike);
        pose.Move("body", 0, Math.Abs(Math.Sin(b.Phase)) * 0.05 * b.Gait, 0);
        pose.Rotate("neck", b.Stunned ? 25 : Wave(2, b.Time) * 3 + 6 * b.Gait);
        double head = b.Stunned ? 30 : 4 * b.Raise + Wave(1, b.Time) * 3;
        if (b.Warn) head = -12 + Wave(7, b.Time) * 5;
        pose.Rotate("head", head);
        pose.Rotate("tail", Wave(2, b.Time) * 10 + 12 * b.Gait, 0, Wave(1, b.Time, Math.PI / 2) * 8);
        return pose;
    }

    private static Pose Nighe(Beat b)
    {
        // She wrings out a cloth as she stands: forearms working, the cloth twisting, a slow sway.
        var pose = new Pose();
        double wring = b.Flee ? Math.Sin(8 * b.Phase) : Wave(3, b.Time);
        pose.Rotate("arm_l", b.Stunned ? 20 : -30 + wring * 22, 0, 4);
        pose.Rotate("arm_r", b.Stunned ? 20 : -30 - wring * 22, 0, -4);
        pose.Rotate("torso", (b.Stunned ? 18 : 6) + Wave(1, b.Time) * 2, Wave(1, b.Time) * 4, b.Stunned ? Wave(8, b.Time) * 5 : Wave(1, b.Time) * 2);
        pose.Move("torso", 0, Math.Abs(Math.Sin(b.Phase)) * 0.03 * b.Gait, 0);
        pose.Rotate("head", b.Stunned ? 35 : 8 + Wave(1, b.Time) * 4);
        pose.Rotate("cloth", 0, wring * 30, wring * 12);
        pose.Move("cloth", 0, wring * 0.02, 0);
        return pose;
    }

    private static Pose Hare(Beat b)
    {
        // Hops: the whole body leaps and the hind legs kick out behind; at rest the nose twitches and the ears turn.
        var pose = new Pose();
        double hop = Math.Abs(Math.Sin(b.Phase));
        double twitch = Wave(8, b.Time);
        pose.Move("body", 0, hop * 0.22 * b.Gait, 0);
        pose.Rotate("body", b.Stunned ? 70 : -22 * b.Gait * hop + Wave(2, b.Time) * 1.5, 0, b.Stunned ? 80 : 0);
        pose.Rotate("leg_l", b.Stunned ? 0 : 40 * hop * b.Gait);
        pose.Rotate("leg_r", b.Stunned ? 0 : 40 * hop * b.Gait);
        pose.Rotate("head", b.Stunned ? 20 : 10 * b.Gait * hop + twitch * 2);
        bool alert = b.Flee || b.Gait > 0.3;
        pose.Rotate("ear_l", alert ? -30 : 8 + Wave(2, b.Time) * 8, 0, 10);
        pose.Rotate("ear_r", alert ? -30 : 8 + Wave(1, b.Time, 1) * 8, 0, -10);
        return pose;
    }

    private static Pose Hound(Beat b)
    {
        // A gallop: diagonal pairs swing together and the body rocks; it crouches low for the lunge and snaps forward on the blow.
        var pose = new Pose();
        double swing = Math.Sin(b.Phase) * 40 * b.Gait;
        pose.Rotate("leg_fl", swing);
        pose.Rotate("leg_br", swing);
        pose.Rotate("leg_fr", -swing);
        pose.Rotate("leg_bl", -swing);
        pose.Move("body", 0, Math.Abs(Math.Sin(b.Phase)) * 0.08 * b.Gait - 0.08 * b.Raise, 0);
        pose.Rotate("body", (b.Stunned ? 20 : 10 * b.Raise - 14 * b.Strike + Math.Sin(b.Phase) * 4 * b.Gait), 0, b.Stunned ? Wave(8, b.Time) * 6 : 0);
        pose.Rotate("head", b.Stunned ? 30 : -8 * b.Raise + 10 * b.Strike + Wave(1, b.Time) * 3);
        pose.Rotate("tail", Wave(2, b.Time) * 12 + 14 * b.Gait, 0, Wave(1, b.Time, Math.PI / 2) * 8);
        return pose;
    }

    private static Pose Warden(Beat b)
    {
        // Both fists go over the head on a windup and come down hard; the core flares.
        var pose = new Pose();
        double swing = Math.Sin(b.Phase) * 24 * b.Gait;
        pose.Rotate("leg_l", swing);
        pose.Rotate("leg_r", -swing);

        double up = -165 * b.Raise + 80 * b.Strike;
        pose.Rotate("arm_l", b.Stunned ? 20 : -swing * 0.5 * (1 - b.Raise) + up, 0, 6);
        pose.Rotate("arm_r", b.Stunned ? 20 : swing * 0.5 * (1 - b.Raise) + up, 0, -6);
        pose.Rotate("torso", (b.Stunned ? 12 : 0) + 8 * b.Raise - 18 * b.Strike, Math.Sin(b.Phase) * 3 * b.Gait);
        pose.Move("torso", 0, Math.Abs(Math.Sin(b.Phase)) * 0.04 * b.Gait, 0);
        pose.Scale("torso", 1 + 0.03 * Wave(3, b.Time));
        pose.Rotate("head", b.Stunned ? 35 : -6 * b.Raise + 8 * b.Strike);
        return pose;
    }

    private static Pose Spitter(Beat b)
    {
        // It floats: bob, and drift side to side a little. The mouth swells and the tentacles lift on a windup.
        var pose = new Pose();
        double bob = Wave(2, b.Time) * 0.09;
        double swell = 1 + 0.2 * b.Raise;
        pose.Move("body", 0, bob, 0);
        pose.Scale("body", swell * (1 - 0.08 * b.Strike));
        pose.Rotate("body", b.Stunned ? Wave(9, b.Time) * 12 : 0, 0, b.Stunned ? Wave(7, b.Time) * 18 : Wave(1, b.Time) * 3);
        pose.Move("mouth", 0, bob, -0.3 * (swell - 1));
        pose.Scale("mouth", 1 + 0.45 * b.Raise);
        for (int i = 0; i < 4; i++)
        {
            string tent = "tent_" + i;
            pose.Move(tent, 0, bob, 0);
            pose.Rotate(tent, Wave(3, b.Time, i * 1.7) * 14 + 25 * b.Raise, 0, Wave(2, b.Time, i + Math.PI / 2) * 14);
        }
        return pose;
    }
}
