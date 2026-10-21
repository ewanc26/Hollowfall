namespace Hollowfall.Core.Relations;

/// <summary>What a villager senses this tick: the player and the Hollow around it, its personality, its memory and what it was doing.</summary>
public readonly record struct VillagerSense(Perception P, Traits T, double Anger, double Fear, Intent Previous);

/// <summary>
/// A villager's brain: a small two-layer network (13 inputs, 8 tanh units, 4 outcomes) that picks Idle, Flee, Warn or Attack. The
/// weights are committed in <c>VillagerWeights.cs</c>, written by <c>tools/Hollowfall.VillagerTrainer</c>, which fits the network to
/// labelled examples of how a crofter should act. Inference is a pure function of the sense, so the same sense always gives the
/// same intent.
/// </summary>
public static class VillagerNet
{
    public const int Inputs = 13, Hidden = 8, Outcomes = 4;

    public static readonly Intent[] Choices = [Intent.Idle, Intent.Flee, Intent.Warn, Intent.Attack];

    /// <summary>The network's inputs, each scaled to roughly 0 to 1 (approach speed to -1 to 1).</summary>
    public static double[] Encode(VillagerSense s)
    {
        var p = s.P;
        return
        [
            Math.Clamp(p.Distance / Mind.NoticeRange, 0, 1),
            Math.Clamp(p.ApproachSpeed / Brain.FastApproach, -1, 1),
            p.Sees ? 1 : 0,
            Math.Clamp(p.PlayerFromHome / Mind.NoticeRange, 0, 1),
            s.Anger,
            s.Fear,
            s.T.Bravery,
            s.T.Grudge,
            s.T.Curiosity,
            Math.Clamp(1 - p.HostileDistance / Brain.AssistRange, 0, 1),
            s.Previous == Intent.Flee ? 1 : 0,
            s.Previous == Intent.Warn ? 1 : 0,
            s.Previous == Intent.Attack ? 1 : 0,
        ];
    }

    public static Intent Decide(VillagerSense s)
    {
        var scores = Forward(Encode(s));
        int best = 0;
        for (int i = 1; i < scores.Length; i++)
            if (scores[i] > scores[best]) best = i;
        return Choices[best];
    }

    /// <summary>The softmax over the four outcomes, in the order of <see cref="Choices"/>.</summary>
    public static double[] Forward(double[] x)
    {
        var hidden = new double[Hidden];
        for (int h = 0; h < Hidden; h++)
        {
            double sum = VillagerWeights.HiddenBias[h];
            for (int i = 0; i < Inputs; i++) sum += VillagerWeights.Hidden[h * Inputs + i] * x[i];
            hidden[h] = Math.Tanh(sum);
        }

        var logits = new double[Outcomes];
        double max = double.NegativeInfinity;
        for (int o = 0; o < Outcomes; o++)
        {
            double sum = VillagerWeights.OutputBias[o];
            for (int h = 0; h < Hidden; h++) sum += VillagerWeights.Output[o * Hidden + h] * hidden[h];
            logits[o] = sum;
            max = Math.Max(max, sum);
        }

        double total = 0;
        for (int o = 0; o < Outcomes; o++)
        {
            logits[o] = Math.Exp(logits[o] - max);
            total += logits[o];
        }
        for (int o = 0; o < Outcomes; o++) logits[o] /= total;
        return logits;
    }
}
