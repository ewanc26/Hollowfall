namespace VoxelGame.Core.Terrain;

/// <summary>
/// Seeded improved-Perlin gradient noise (2D and 3D). The permutation table is a
/// Fisher-Yates shuffle driven by <see cref="SeedHash"/>, not System.Random,
/// so output is stable across .NET versions and platforms.
/// Output range is roughly [-1, 1].
/// </summary>
public sealed class PerlinNoise
{
    private readonly byte[] _perm = new byte[512];

    public PerlinNoise(long seed)
    {
        var p = new byte[256];
        for (int i = 0; i < 256; i++) p[i] = (byte)i;

        ulong state = (ulong)seed;
        for (int i = 255; i > 0; i--)
        {
            state = SeedHash.Mix(state);
            int j = (int)(state % (ulong)(i + 1));
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static double Lerp(double a, double b, double t) => a + t * (b - a);

    private static double Grad2(int h, double x, double y)
    {
        // 8 evenly spaced unit-ish directions
        return (h & 7) switch
        {
            0 => x + y,
            1 => x - y,
            2 => -x + y,
            3 => -x - y,
            4 => x,
            5 => -x,
            6 => y,
            _ => -y,
        };
    }

    private static double Grad3(int h, double x, double y, double z)
    {
        h &= 15;
        double u = h < 8 ? x : y;
        double v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    public double Sample(double x, double y)
    {
        int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
        double xf = x - xi, yf = y - yi;
        xi &= 255; yi &= 255;
        double u = Fade(xf), v = Fade(yf);

        int aa = _perm[_perm[xi] + yi], ab = _perm[_perm[xi] + yi + 1];
        int ba = _perm[_perm[xi + 1] + yi], bb = _perm[_perm[xi + 1] + yi + 1];

        double x1 = Lerp(Grad2(aa, xf, yf), Grad2(ba, xf - 1, yf), u);
        double x2 = Lerp(Grad2(ab, xf, yf - 1), Grad2(bb, xf - 1, yf - 1), u);
        return Lerp(x1, x2, v) * 0.7071067811865476; // normalise toward [-1,1]
    }

    public double Sample(double x, double y, double z)
    {
        int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y), zi = (int)Math.Floor(z);
        double xf = x - xi, yf = y - yi, zf = z - zi;
        xi &= 255; yi &= 255; zi &= 255;
        double u = Fade(xf), v = Fade(yf), w = Fade(zf);

        int a = _perm[xi] + yi, aa = _perm[a] + zi, ab = _perm[a + 1] + zi;
        int b = _perm[xi + 1] + yi, ba = _perm[b] + zi, bb = _perm[b + 1] + zi;

        return Lerp(
            Lerp(Lerp(Grad3(_perm[aa], xf, yf, zf), Grad3(_perm[ba], xf - 1, yf, zf), u),
                 Lerp(Grad3(_perm[ab], xf, yf - 1, zf), Grad3(_perm[bb], xf - 1, yf - 1, zf), u), v),
            Lerp(Lerp(Grad3(_perm[aa + 1], xf, yf, zf - 1), Grad3(_perm[ba + 1], xf - 1, yf, zf - 1), u),
                 Lerp(Grad3(_perm[ab + 1], xf, yf - 1, zf - 1), Grad3(_perm[bb + 1], xf - 1, yf - 1, zf - 1), u), v),
            w);
    }

    /// <summary>Fractal Brownian motion, normalised to roughly [-1, 1].</summary>
    public double Fbm(double x, double y, int octaves, double lacunarity = 2.0, double gain = 0.5)
    {
        double sum = 0, amp = 1, freq = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += Sample(x * freq, y * freq) * amp;
            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }
        return sum / norm;
    }

    public double Fbm(double x, double y, double z, int octaves, double lacunarity = 2.0, double gain = 0.5)
    {
        double sum = 0, amp = 1, freq = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += Sample(x * freq, y * freq, z * freq) * amp;
            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }
        return sum / norm;
    }
}
