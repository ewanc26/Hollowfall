using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Progression;

/// <summary>
/// The character's stats as one run sees them. They are read once, when the run starts, and recorded in its replay, so a run
/// can be replayed exactly. The default is a character with nothing spent, which is how every run looked before progression.
/// </summary>
public readonly record struct RunStats(int Vigour = 0, int Might = 0, int Creams = 0, ItemKind? Charm = null)
{
    /// <summary>The most points that can go into one stat: one for each level after the first.</summary>
    public const int MaxPoints = Progression.MaxLevel - 1;

    /// <summary>The most health this run allows: the base plus what Vigour adds.</summary>
    public double MaxHealth => PlayerVitals.MaxHealth + ClampedVigour * Progression.VigourPerPoint;

    /// <summary>The multiplier on the damage your attacks deal in this run.</summary>
    public double DamageScale => 1 + ClampedMight * Progression.MightPerPoint;

    /// <summary>The share of each blow the worn charm turns aside: none unless it is the thorn charm.</summary>
    public double WardFraction => Charm == ItemKind.ThornCharm ? Inventory.ThornWard : 0;

    /// <summary>Stats read from a file or a replay are clamped to what can be earned.</summary>
    public int ClampedVigour => Math.Clamp(Vigour, 0, MaxPoints);
    public int ClampedMight => Math.Clamp(Might, 0, MaxPoints);
    public int ClampedCreams => Math.Clamp(Creams, 0, Inventory.MaxStack * Inventory.Slots);
}
