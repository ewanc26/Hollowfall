using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Core.Combat;

/// <summary>One chest in the dungeon. A locked one is in a treasure or boss room and takes a key.</summary>
public sealed class ChestState(int index, Spawn spawn)
{
    public int Index { get; } = index;
    public Spawn Spawn { get; } = spawn;
    public bool Opened { get; internal set; }
    public bool Locked => Spawn.Tier >= Spawn.Floor + 2;

    /// <summary>What is inside, rolled from the seed. It is the same whether or not the chest has been opened.</summary>
    public Loot Loot { get; internal init; }
}

/// <summary>
/// The dungeon's furniture that does something: keys are picked up by walking over them, chests open on request (locked
/// ones spend a key and give more), and spike traps cycle through a warning and a strike. Fixed-step and free of engine
/// types; the sim owns one and the host reads its state to draw it.
/// </summary>
public sealed class Interactables
{
    public const double PickupReach = 1.3;
    public const double ChestReach = 2.4;
    public const double TrapPeriod = 3.2;
    public const double TrapWarning = 0.8;
    public const double TrapStrike = 0.6;
    public const double TrapDamage = 22;
    public const double TrapHalfSize = 0.55;
    public const double ChestHeal = 30;
    public const double LockedChestHeal = 60;

    private readonly List<ChestState> _chests = [];
    private readonly List<(int Index, Spawn Spawn)> _keys = [];
    private readonly List<(int Index, Spawn Spawn, double Phase)> _traps = [];
    private readonly HashSet<int> _collected = [];
    private readonly HashSet<int> _struck = [];
    private readonly Func<double, double> _hurt;
    private readonly Func<double, double> _heal;
    private readonly Action<CombatEvent> _emit;
    private readonly Func<double>? _cleanse;
    private readonly Func<double, double>? _boostStyle;
    private double _time;

    /// <summary>Timed perks from loot; the sim reads them through small hooks.</summary>
    public Perks Perks { get; } = new();

    public IReadOnlyList<ChestState> Chests => _chests;
    public int KeysHeld { get; private set; }

    public Interactables(IReadOnlyList<Spawn> spawns, long seed, Func<double, double> hurt, Func<double, double> heal, Action<CombatEvent> emit,
        Func<double>? cleanse = null, Func<double, double>? boostStyle = null)
    {
        _cleanse = cleanse;
        _boostStyle = boostStyle;
        _hurt = hurt;
        _heal = heal;
        _emit = emit;
        for (int i = 0; i < spawns.Count; i++)
        {
            var s = spawns[i];
            switch (s.Kind)
            {
                case SpawnKind.Loot: _chests.Add(new ChestState(i, s) { Loot = LootTable.Roll(seed, s) }); break;
                case SpawnKind.Key: _keys.Add((i, s)); break;
                case SpawnKind.Trap:
                    // Each trap has its own beat, so a room of them never strikes in unison.
                    _traps.Add((i, s, SeedHash.ToUnit(SeedHash.Hash(seed, s.X, s.Z, 910)) * TrapPeriod));
                    break;
            }
        }
    }

    public bool KeyTaken(int spawnIndex) => _collected.Contains(spawnIndex);

    /// <summary>How far a trap's spikes are out, 0 (hidden) to 1 (fully out). It shivers a little during the warning.</summary>
    public double SpikeExtension(int spawnIndex)
    {
        foreach (var (index, _, phase) in _traps)
            if (index == spawnIndex) return Extension(Cycle(phase));
        return 0;
    }

    /// <summary>True while a trap's spikes are warning of a strike (for a sound and a tremble).</summary>
    public bool Warning(int spawnIndex)
    {
        foreach (var (index, _, phase) in _traps)
            if (index == spawnIndex) return Cycle(phase) is >= TrapPeriod - TrapWarning - TrapStrike and < TrapPeriod - TrapStrike;
        return false;
    }

    private double Cycle(double phase) => (_time + phase) % TrapPeriod;

    private static double Extension(double t)
    {
        double warnStart = TrapPeriod - TrapWarning - TrapStrike;
        double strikeStart = TrapPeriod - TrapStrike;
        if (t < warnStart) return 0;
        if (t < strikeStart) return 0.12 + 0.05 * Math.Sin(t * 40); // a tremble
        double since = t - strikeStart;
        return since < 0.08 ? 0.12 + since / 0.08 * 0.88 : since > TrapStrike - 0.15 ? Math.Max(0, (TrapStrike - since) / 0.15) : 1;
    }

    public void Tick(double dt, PlayerView view, bool interact)
    {
        _time += dt;
        Perks.Tick(dt);
        var feet = view.Feet;

        foreach (var (index, s) in _keys)
        {
            if (_collected.Contains(index) || Math.Abs(feet.Y - s.Y) > 2.2) continue;
            if (Dist(feet, s) > PickupReach) continue;
            _collected.Add(index);
            KeysHeld++;
            _emit(new KeyCollected(index));
        }

        if (interact)
        {
            ChestState? nearest = null;
            double best = ChestReach;
            foreach (var c in _chests)
            {
                if (c.Opened || Math.Abs(feet.Y - c.Spawn.Y) > 2.2) continue;
                double d = Dist(feet, c.Spawn);
                if (d < best) { best = d; nearest = c; }
            }
            if (nearest != null) Open(nearest);
        }

        foreach (var (index, s, phase) in _traps)
        {
            double t = Cycle(phase);
            bool striking = t >= TrapPeriod - TrapStrike + 0.05;
            if (!striking)
            {
                _struck.Remove(index);
                continue;
            }
            if (_struck.Contains(index)) continue;
            if (Math.Abs(feet.X - (s.X + 0.5)) < TrapHalfSize + 0.3 && Math.Abs(feet.Z - (s.Z + 0.5)) < TrapHalfSize + 0.3 && feet.Y < s.Y + 0.6 && feet.Y > s.Y - 1.5)
            {
                // Only a hit that did damage counts as the strike; a player spared by invulnerability is hit once it ends.
                double taken = _hurt(TrapDamage);
                if (taken <= 0) continue;
                _struck.Add(index);
                _emit(new TrapSprung(index, taken));
            }
        }
    }

    private void Open(ChestState chest)
    {
        if (chest.Locked)
        {
            if (KeysHeld <= 0)
            {
                _emit(new ChestLocked(chest.Index));
                return;
            }
            KeysHeld--;
        }
        chest.Opened = true;
        var loot = chest.Loot;
        double healed = 0, applied;
        switch (loot.Kind)
        {
            case LootKind.Health: healed = applied = _heal(loot.Amount); break;
            case LootKind.Cleanse: applied = _cleanse?.Invoke() ?? 0; break;
            case LootKind.StyleBoost: applied = _boostStyle?.Invoke(loot.Amount) ?? 0; break;
            case LootKind.Key: KeysHeld += (int)loot.Amount; applied = loot.Amount; break;
            default: Perks.Grant(loot); applied = loot.Seconds; break;
        }
        _emit(new ChestOpened(chest.Index, healed, chest.Locked, loot, applied));
    }

    private static double Dist(Vec3 feet, Spawn s)
    {
        double dx = feet.X - (s.X + 0.5), dz = feet.Z - (s.Z + 0.5);
        return Math.Sqrt(dx * dx + dz * dz);
    }
}
