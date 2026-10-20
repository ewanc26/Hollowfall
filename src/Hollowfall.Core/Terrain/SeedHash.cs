namespace Hollowfall.Core.Terrain;

/// <summary>
/// Stateless, platform-independent integer hashing (SplitMix64 finalizer).
/// All randomness in terrain generation flows through here so a seed always
/// yields the same world, regardless of generation order or thread.
/// </summary>
public static class SeedHash
{
    public static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public static ulong Hash(long seed, int x, int z, int salt = 0)
    {
        ulong h = Mix((ulong)seed ^ ((ulong)(uint)salt << 32));
        h = Mix(h ^ (ulong)(uint)x);
        h = Mix(h ^ ((ulong)(uint)z << 21) ^ 0xA5A5A5A5UL);
        return h;
    }

    /// <summary>Uniform double in [0,1) derived from a hash.</summary>
    public static double ToUnit(ulong h) => (h >> 11) * (1.0 / (1UL << 53));

    /// <summary>Derives an independent sub-seed for a named noise layer.</summary>
    public static long Derive(long seed, int salt) => (long)Mix((ulong)seed ^ Mix((ulong)salt));
}
