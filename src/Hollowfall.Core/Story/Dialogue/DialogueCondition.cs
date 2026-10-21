using System.Globalization;
using System.Text;
using Hollowfall.Core.Localization;

namespace Hollowfall.Core.Story.Dialogue;

/// <summary>
/// A test on a <see cref="DialogueContext"/>. Atoms are made with the static factories (<see cref="Flag"/>, <see cref="HealthBelow"/>
/// and so on) and composed with <c>&amp;</c>, <c>|</c> and <c>!</c> (or <see cref="All"/>, <see cref="Any"/> and <see cref="Not"/>).
/// </summary>
public abstract class DialogueCondition
{
    public abstract bool Evaluate(DialogueContext context);

    public static DialogueCondition operator &(DialogueCondition a, DialogueCondition b) => new AndCondition([a, b]);

    public static DialogueCondition operator |(DialogueCondition a, DialogueCondition b) => new OrCondition([a, b]);

    public static DialogueCondition operator !(DialogueCondition a) => new NotCondition(a);

    public static DialogueCondition All(params DialogueCondition[] parts) => new AndCondition(parts);

    public static DialogueCondition Any(params DialogueCondition[] parts) => new OrCondition(parts);

    public static DialogueCondition Not(DialogueCondition part) => new NotCondition(part);

    public static DialogueCondition Always { get; } = new Atom(_ => true);

    public static DialogueCondition Flag(string id) => new Atom(c => c.Has(id));

    public static DialogueCondition VisitsAtLeast(int n) => new Atom(c => c.VisitCount >= n);

    public static DialogueCondition VisitsBelow(int n) => new Atom(c => c.VisitCount < n);

    public static DialogueCondition HealthBelow(double fraction) => new Atom(c => c.HealthFraction < fraction);

    public static DialogueCondition DeathsAtLeast(int n) => new Atom(c => c.Deaths >= n);

    public static DialogueCondition KillsAtLeast(int n) => new Atom(c => c.Kills >= n);

    public static DialogueCondition FloorAtLeast(int floor) => new Atom(c => c.DeepestFloor >= floor);

    public static DialogueCondition NotesAtLeast(int n) => new Atom(c => c.NotesRead >= n);

    public static DialogueCondition CarryingKey { get; } = new Atom(c => c.HasKey);

    public static DialogueCondition GiftGiven { get; } = new Atom(c => c.GiftGiven);

    public static DialogueCondition EndingReached { get; } = new Atom(c => c.EndingReached);

    /// <summary>The interface is in Gàidhlig or Bilingual, so his short Gàidhlig lines are said.</summary>
    public static DialogueCondition GaelicShown { get; } = new Atom(c => Languages.ShowsGaelic(c.Language));

    public static DialogueCondition FinalFloor { get; } = new Atom(c => c.FinalFloorReached);

    /// <summary>The seed text, ignoring case, accents, spaces and hyphens, is one of the given names.</summary>
    public static DialogueCondition SeedIsOneOf(params string[] names)
    {
        var set = names.Select(Normalise).ToHashSet();
        return new Atom(c => set.Contains(Normalise(c.SeedText)));
    }

    /// <summary>Lower case, accents stripped, and only letters and digits kept.</summary>
    public static string Normalise(string text)
    {
        var sb = new StringBuilder();
        foreach (char ch in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private sealed class Atom(Func<DialogueContext, bool> test) : DialogueCondition
    {
        public override bool Evaluate(DialogueContext context) => test(context);
    }

    private sealed class AndCondition(DialogueCondition[] parts) : DialogueCondition
    {
        public override bool Evaluate(DialogueContext context) => parts.All(p => p.Evaluate(context));
    }

    private sealed class OrCondition(DialogueCondition[] parts) : DialogueCondition
    {
        public override bool Evaluate(DialogueContext context) => parts.Any(p => p.Evaluate(context));
    }

    private sealed class NotCondition(DialogueCondition part) : DialogueCondition
    {
        public override bool Evaluate(DialogueContext context) => !part.Evaluate(context);
    }
}
