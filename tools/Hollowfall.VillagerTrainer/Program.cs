using System.Globalization;
using System.Text;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Relations;

// Usage: dotnet run --project tools/Hollowfall.VillagerTrainer [repository root]
// Fits the villager network to the crofters' rules and rewrites src/Hollowfall.Core/Relations/VillagerWeights.cs.
// Training is deterministic: the same rules always give the same weights, and the rounded weights are what the game ships.
string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
string output = Path.Combine(root, "src", "Hollowfall.Core", "Relations", "VillagerWeights.cs");
if (!File.Exists(Path.Combine(root, "Hollowfall.sln")) || !File.Exists(output))
{
    Console.Error.WriteLine($"{root} is not the repository root, or VillagerWeights.cs is missing.");
    return 1;
}

const int Inputs = VillagerNet.Inputs, Hidden = VillagerNet.Hidden, Outcomes = VillagerNet.Outcomes;
var rng = new SeededRandom(2026, 0, 0, 77);
var samples = Enumerable.Range(0, 48000).Select(_ => Sample(rng)).ToList();
var (train, test) = (samples.Take(36000).ToList(), samples.Skip(36000).ToList());

var w1 = Init(Hidden * Inputs, Inputs, Hidden, rng);
var b1 = new double[Hidden];
var w2 = Init(Outcomes * Hidden, Hidden, Outcomes, rng);
var b2 = new double[Outcomes];

const double LearningRate = 0.05;
const int Epochs = 420, Batch = 32;
for (int epoch = 0; epoch < Epochs; epoch++)
{
    double rate = LearningRate * (epoch < Epochs / 2 ? 1 : 0.3);
    var order = train.OrderBy(_ => rng.NextDouble()).ToList();
    for (int start = 0; start < order.Count; start += Batch)
    {
        var gw1 = new double[w1.Length];
        var gb1 = new double[Hidden];
        var gw2 = new double[w2.Length];
        var gb2 = new double[Outcomes];
        foreach (var (x, label) in order.Skip(start).Take(Batch))
        {
            var h = new double[Hidden];
            for (int j = 0; j < Hidden; j++)
            {
                double sum = b1[j];
                for (int i = 0; i < Inputs; i++) sum += w1[j * Inputs + i] * x[i];
                h[j] = Math.Tanh(sum);
            }
            var p = Softmax(Logits(h, w2, b2));
            var dz = new double[Outcomes];
            for (int o = 0; o < Outcomes; o++) dz[o] = p[o] - (o == label ? 1 : 0);
            for (int o = 0; o < Outcomes; o++)
            {
                gb2[o] += dz[o];
                for (int j = 0; j < Hidden; j++) gw2[o * Hidden + j] += dz[o] * h[j];
            }
            for (int j = 0; j < Hidden; j++)
            {
                double dh = 0;
                for (int o = 0; o < Outcomes; o++) dh += w2[o * Hidden + j] * dz[o];
                double da = dh * (1 - h[j] * h[j]);
                gb1[j] += da;
                for (int i = 0; i < Inputs; i++) gw1[j * Inputs + i] += da * x[i];
            }
        }
        double scale = rate / Batch;
        for (int k = 0; k < w1.Length; k++) w1[k] -= scale * gw1[k];
        for (int k = 0; k < w2.Length; k++) w2[k] -= scale * gw2[k];
        for (int k = 0; k < Hidden; k++) b1[k] -= scale * gb1[k];
        for (int k = 0; k < Outcomes; k++) b2[k] -= scale * gb2[k];
    }
}

int correct = 0;
var confusion = new int[Outcomes, Outcomes];
foreach (var (x, label) in test)
{
    var p = Softmax(Logits(Hiddens(x, w1, b1), w2, b2));
    int guess = Array.IndexOf(p, p.Max());
    confusion[label, guess]++;
    if (guess == label) correct++;
}
double accuracy = correct / (double)test.Count;
Console.WriteLine($"held-out accuracy {accuracy:P1} over {test.Count} situations");
for (int o = 0; o < Outcomes; o++)
    Console.WriteLine($"  {VillagerNet.Choices[o],-7} {string.Join(" ", Enumerable.Range(0, Outcomes).Select(g => confusion[o, g].ToString(CultureInfo.InvariantCulture).PadLeft(5)))}");
if (accuracy < 0.93)
{
    Console.Error.WriteLine("the network does not learn the crofters' rules well enough; weights not written");
    return 1;
}

File.WriteAllText(output, Source(w1, b1, w2, b2));
Console.WriteLine($"wrote {output}");
return 0;

// One situation, its features and the label the crofters' rules give it.
(double[] X, int Label) Sample(SeededRandom r)
{
    double Uniform(double lo, double hi) => lo + (hi - lo) * r.NextDouble();
    double distance = Uniform(0, 16), approach = Uniform(-4, 6), fromHome = Uniform(0, 20);
    bool sees = r.NextDouble() < 0.7;
    double anger = r.NextDouble() < 0.4 ? 0 : r.NextDouble();
    double fear = r.NextDouble();
    var traits = (Bravery: r.NextDouble(), Grudge: r.NextDouble(), Curiosity: r.NextDouble());
    double hostile = r.NextDouble() < 0.3 ? double.PositiveInfinity : Uniform(0, 20);
    var previous = VillagerNet.Choices[r.Range(0, Outcomes - 1)];

    var perception = new Perception(distance, approach, sees, fromHome, hostile);
    var sense = new VillagerSense(perception, new Traits(traits.Bravery, traits.Grudge, traits.Curiosity), anger, fear, previous);
    return (VillagerNet.Encode(sense), Array.IndexOf(VillagerNet.Choices, Rules(sense)));
}

// The crofters' rules: a provoked villager stands its ground or fights when brave, flees when timid or frightened, and
// keeps to its door otherwise. Calm villagers back off from the Hollow and from a close, fast-approaching player.
Intent Rules(VillagerSense s)
{
    var p = s.P;
    bool provoked = s.Anger >= Mind.WarnAnger;
    bool scared = s.Fear >= Mind.ScaredAt;
    if (provoked)
    {
        if (scared || s.T.Bravery < 0.35) return Intent.Flee;
        if (p.Distance < 8) return s.T.Bravery >= 0.5 ? Intent.Attack : Intent.Warn;
        return Intent.Idle;
    }
    if (scared) return Intent.Flee;
    if (p.HostileDistance < 6) return Intent.Flee;
    if (p.Sees && p.Distance < 4) return Intent.Flee;
    if (p.Sees && p.Distance < VillagerNet.ClosingRange && p.ApproachSpeed > Brain.FastApproach) return Intent.Flee;
    if (s.Previous == Intent.Flee && p.Distance < 9) return Intent.Flee;
    return Intent.Idle;
}

static double[] Init(int count, int fanIn, int fanOut, SeededRandom r)
{
    double limit = Math.Sqrt(6.0 / (fanIn + fanOut));
    return Enumerable.Range(0, count).Select(_ => (r.NextDouble() * 2 - 1) * limit).ToArray();
}

static double[] Hiddens(double[] x, double[] w1, double[] b1)
{
    var h = new double[Hidden];
    for (int j = 0; j < Hidden; j++)
    {
        double sum = b1[j];
        for (int i = 0; i < Inputs; i++) sum += w1[j * Inputs + i] * x[i];
        h[j] = Math.Tanh(sum);
    }
    return h;
}

static double[] Logits(double[] h, double[] w2, double[] b2)
{
    var z = new double[Outcomes];
    for (int o = 0; o < Outcomes; o++)
    {
        double sum = b2[o];
        for (int j = 0; j < Hidden; j++) sum += w2[o * Hidden + j] * h[j];
        z[o] = sum;
    }
    return z;
}

static double[] Softmax(double[] z)
{
    double max = z.Max();
    var e = z.Select(v => Math.Exp(v - max)).ToArray();
    double total = e.Sum();
    return e.Select(v => v / total).ToArray();
}

static string Source(double[] w1, double[] b1, double[] w2, double[] b2)
{
    var sb = new StringBuilder();
    sb.AppendLine("// Generated by tools/Hollowfall.VillagerTrainer. Do not edit by hand; rerun the trainer after changing the rules.");
    sb.AppendLine("namespace Hollowfall.Core.Relations;");
    sb.AppendLine();
    sb.AppendLine("public static class VillagerWeights");
    sb.AppendLine("{");
    WriteArray(sb, "Hidden", w1, Inputs);
    WriteArray(sb, "HiddenBias", b1, Hidden);
    WriteArray(sb, "Output", w2, Hidden);
    WriteArray(sb, "OutputBias", b2, Outcomes);
    sb.AppendLine("}");
    return sb.ToString();
}

static void WriteArray(StringBuilder sb, string name, double[] values, int perRow)
{
    sb.AppendLine($"    public static readonly double[] {name} =");
    sb.AppendLine("    [");
    for (int i = 0; i < values.Length; i += perRow)
    {
        var row = values.Skip(i).Take(perRow).Select(v => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture));
        sb.AppendLine($"        {string.Join(", ", row)},");
    }
    sb.AppendLine("    ];");
    sb.AppendLine();
}
