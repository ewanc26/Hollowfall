using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Progression;

/// <summary>The two stats a point can go into. Their effects are applied by the simulation from a later step.</summary>
public enum Stat
{
    /// <summary>Raises the most health you can have.</summary>
    Vigour,

    /// <summary>Raises the damage your attacks deal.</summary>
    Might,
}

/// <summary>
/// Your character across runs: experience from the Hollow you defeat, a level for each stretch of it, and a stat point for
/// each level after the first. Pure and deterministic, so it can be tested without the engine and never changes a run that
/// is already underway. Creatures of the Folk and the vermin give no experience: they are not the enemy.
/// </summary>
public sealed class Progression
{
    public const int MaxLevel = 20;
    public const int MaxExperience = 10_000_000;

    /// <summary>The most coin you can carry. Coin is earned from the Hollow and spent at the pedlars.</summary>
    public const int MaxCoin = 9_999;

    /// <summary>Experience to reach level 2 is <see cref="LevelStep"/>; each later level needs more than the one before.</summary>
    public const int LevelStep = 30;

    /// <summary>How much each stat adds for each point put into it.</summary>
    public const double VigourPerPoint = 10;
    public const double MightPerPoint = 0.05;

    private int _vigour;
    private int _might;

    /// <summary>What you carry, and the charm you wear (see <see cref="Inventory"/>).</summary>
    public Inventory Items { get; } = new();

    /// <summary>
    /// A friendly Brownie's boon: it leaves its cream. Returns true if the cream fitted in the bag; with a full bag it does not.
    /// </summary>
    public bool ReceiveBoon() => Items.Add(ItemKind.BrownieCream) == 1;

    /// <summary>The quests you have taken, and how far along they are.</summary>
    public QuestLog Quests { get; } = new();

    /// <summary>
    /// Counts a defeated enemy against your quests and pays out each quest it finishes: experience and any item, into the bag.
    /// Returns the quests finished by this kill.
    /// </summary>
    public IReadOnlyList<QuestDef> RecordKill(EnemyKilled killed)
    {
        AwardCoin(killed.Kind);
        var finished = Quests.Record(killed);
        foreach (var def in finished)
        {
            Gain(def.Experience);
            GainCoin(def.Coin);
            if (def.Item is { } item) Items.Add(item, def.ItemCount);
        }
        return finished;
    }

    public int Experience { get; private set; }

    /// <summary>Coin carried between runs, spent at the pedlars.</summary>
    public int Coin { get; private set; }

    public int Level => LevelFor(Experience);

    /// <summary>The stat points the character has earned in total (one for each level after the first).</summary>
    public int PointsEarned => Level - 1;

    public int Vigour => _vigour;
    public int Might => _might;

    /// <summary>Points you have earned and not yet spent.</summary>
    public int PointsAvailable => PointsEarned - _vigour - _might;

    /// <summary>The most health you can have: the base plus what Vigour adds.</summary>
    public double MaxHealthBonus => _vigour * VigourPerPoint;

    /// <summary>The multiplier on the damage you deal: 1 with no Might.</summary>
    public double DamageScale => 1 + _might * MightPerPoint;

    /// <summary>The stats as a run reads them when it starts (and records them in its replay).</summary>
    public RunStats Stats => new(_vigour, _might, Items.Count(Items.Ready),
        Items.Count(Items.Ready) > 0 ? Items.Ready : null, Items.Charm, Items.Relic);

    /// <summary>The experience a defeated enemy is worth.</summary>
    public static int ExperienceFor(EnemyKind kind) => kind switch
    {
        EnemyKind.Husk => 10,
        EnemyKind.Spitter => 12,
        EnemyKind.CuSith => 14,
        EnemyKind.Villager => 8,
        EnemyKind.Pedlar => 0,
        EnemyKind.Warden => 60,
        _ => 0,
    };

    /// <summary>The coin a defeated enemy drops. Only the Hollow pays; the Folk and vermin give none.</summary>
    public static int CoinFor(EnemyKind kind) => kind switch
    {
        EnemyKind.Husk => 3,
        EnemyKind.Spitter => 4,
        EnemyKind.CuSith => 5,
        EnemyKind.Warden => 30,
        _ => 0,
    };

    /// <summary>Adds coin and returns how much was gained (never negative, never past the cap).</summary>
    public int GainCoin(int amount)
    {
        if (amount <= 0 || Coin >= MaxCoin) return 0;
        int before = Coin;
        Coin = (int)Math.Min(MaxCoin, (long)Coin + amount);
        return Coin - before;
    }

    /// <summary>Pays coin, or refuses if there is not enough. Never goes below zero.</summary>
    public bool SpendCoin(int amount)
    {
        if (amount < 0 || Coin < amount) return false;
        Coin -= amount;
        return true;
    }

    /// <summary>Coin for a defeated enemy, added to what you carry. Returns how much was gained.</summary>
    public int AwardCoin(EnemyKind kind) => GainCoin(CoinFor(kind));

    /// <summary>Adds experience for a defeated enemy and returns how much was gained (never negative, never past the cap).</summary>
    public int Award(EnemyKind kind) => Gain(ExperienceFor(kind));

    /// <summary>Adds experience and returns how much was gained. Non-positive amounts and anything past the cap are ignored.</summary>
    public int Gain(int amount)
    {
        if (amount <= 0 || Experience >= MaxExperience) return 0;
        int before = Experience;
        Experience = (int)Math.Min(MaxExperience, (long)Experience + amount);
        return Experience - before;
    }

    /// <summary>Puts one earned point into a stat. Returns false if there is no point to spend.</summary>
    public bool Spend(Stat stat)
    {
        if (PointsAvailable <= 0) return false;
        if (stat == Stat.Vigour) _vigour++;
        else _might++;
        return true;
    }

    /// <summary>The total experience needed to reach <paramref name="level"/> (zero for level 1).</summary>
    public static int ExperienceForLevel(int level)
    {
        int n = Math.Clamp(level, 1, MaxLevel) - 1;
        return LevelStep * n * n;
    }

    /// <summary>The level for a total experience, from 1 up to <see cref="MaxLevel"/>.</summary>
    public static int LevelFor(int experience)
    {
        int level = 1;
        while (level < MaxLevel && experience >= ExperienceForLevel(level + 1)) level++;
        return level;
    }

    /// <summary>Restores saved values, clamped. Stat points beyond what has been earned are dropped, Might first.</summary>
    public void Restore(int experience, int vigour, int might, int coin = 0)
    {
        Experience = Math.Clamp(experience, 0, MaxExperience);
        Coin = Math.Clamp(coin, 0, MaxCoin);
        _vigour = 0;
        _might = 0;
        int earned = PointsEarned;
        _vigour = Math.Clamp(vigour, 0, earned);
        _might = Math.Clamp(might, 0, earned - _vigour);
    }
}
