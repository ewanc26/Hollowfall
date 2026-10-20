using Hollowfall.Core.Terrain;

namespace Hollowfall.Core.Dungeon;

/// <summary>
/// Small deterministic random stream built on <see cref="SeedHash"/>. Cross-platform
/// stable (integer maths only), unlike System.Random.
/// </summary>
public sealed class SeededRandom
{
    private ulong _state;

    public SeededRandom(long seed, int a = 0, int b = 0, int salt = 0)
    {
        _state = SeedHash.Hash(seed, a, b, salt);
    }

    public ulong NextU64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        return SeedHash.Mix(_state);
    }

    /// <summary>Uniform integer in [0, maxExclusive).</summary>
    public int Next(int maxExclusive) => (int)(NextU64() % (ulong)maxExclusive);

    /// <summary>Uniform integer in [minInclusive, maxInclusive].</summary>
    public int Range(int minInclusive, int maxInclusive) => minInclusive + Next(maxInclusive - minInclusive + 1);

    public double NextDouble() => SeedHash.ToUnit(NextU64());

    public bool Chance(double p) => NextDouble() < p;

    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
