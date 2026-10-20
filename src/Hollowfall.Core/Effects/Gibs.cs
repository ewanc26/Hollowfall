using Hollowfall.Core.Combat;
using Hollowfall.Core.Terrain;

namespace Hollowfall.Core.Effects;

/// <summary>
/// Decides what a kill and a hit throw into the air: how many shards, where they start, how fast they fly and what colour they
/// are. Everything comes from a seed (the host passes the enemy id and a counter), never from a random generator, so the same
/// fight always bursts the same way and the numbers are tested. The look is chunky coloured shards, never blood.
/// </summary>
public static class Gibs
{
    /// <summary>Shards from a kill, by enemy size. Bigger enemies break into more, bigger pieces.</summary>
    public static int GibCount(EnemyKind kind) => kind switch
    {
        EnemyKind.Husk => 12,
        EnemyKind.Spitter => 10,
        _ => 22,
    };

    public const int SparksPerHit = 6;
    public const int SparksPerHeadshot = 9;
    public const int SparksPerKillingBlow = 8;

    /// <summary>The colours an enemy breaks into: its body colours first, then trim and glow. Hex RGB, matching the model palette.</summary>
    public static IReadOnlyList<uint> Palette(EnemyKind kind) => kind switch
    {
        EnemyKind.Husk => [0x8a3b2a, 0x8a3b2a, 0x4a1f18, 0xd9cfb0, 0xffb830],
        EnemyKind.Spitter => [0x6a3a9a, 0x6a3a9a, 0x35205a, 0xd9cfb0, 0xa24bff],
        _ => [0x4a4f5a, 0x34303c, 0x8189a0, 0x9a1626, 0xc9a227],
    };

    /// <summary>Breaks an enemy standing at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) into shards.</summary>
    public static void Burst(ShardField field, EnemyKind kind, double x, double y, double z, long seed)
    {
        var spec = EnemySpec.Of(kind);
        var palette = Palette(kind);
        float scale = (float)(spec.Height / 1.8);
        var rng = new Stream(seed, 1);
        int count = GibCount(kind);
        for (int i = 0; i < count; i++)
        {
            double a = rng.Next() * Math.PI * 2;
            double outward = 1.8 + rng.Next() * 3.6;
            var (r, g, b) = Unpack(palette[(int)(rng.Next() * palette.Count) % palette.Count]);
            field.Emit(new Shard
            {
                X = (float)(x + Math.Cos(a) * spec.HalfWidth * rng.Next()),
                Y = (float)(y + spec.Height * (0.12 + 0.8 * rng.Next())),
                Z = (float)(z + Math.Sin(a) * spec.HalfWidth * rng.Next()),
                Vx = (float)(Math.Cos(a) * outward),
                Vy = (float)(3.5 + rng.Next() * 5.0),
                Vz = (float)(Math.Sin(a) * outward),
                Rx = (float)(rng.Next() * 6.28),
                Ry = (float)(rng.Next() * 6.28),
                Rz = (float)(rng.Next() * 6.28),
                Wx = (float)((rng.Next() - 0.5) * 20),
                Wy = (float)((rng.Next() - 0.5) * 20),
                Wz = (float)((rng.Next() - 0.5) * 20),
                Size = (float)((0.13 + rng.Next() * 0.15) * scale),
                Life = (float)(1.8 + rng.Next() * 1.2),
                R = r,
                G = g,
                B = b,
                Kind = ShardKind.Gib,
            });
        }
    }

    /// <summary>A shower of bright flecks where a shot landed.</summary>
    public static void Sparks(ShardField field, double x, double y, double z, HitZone zone, bool killed, long seed)
    {
        int count = killed ? SparksPerKillingBlow : zone == HitZone.Head ? SparksPerHeadshot : SparksPerHit;
        var rng = new Stream(seed, 2);
        for (int i = 0; i < count; i++)
        {
            // A random direction on a sphere, pushed upward a little.
            double u = rng.Next() * 2 - 1, a = rng.Next() * Math.PI * 2, ring = Math.Sqrt(1 - u * u);
            double speed = 2.0 + rng.Next() * 3.5;
            // Head shots are gold, kills hot white, everything else orange.
            var (r, g, b) = zone == HitZone.Head ? (1f, 0.82f, 0.3f) : killed ? (1f, 0.95f, 0.8f) : (1f, 0.55f + (float)rng.Next() * 0.2f, 0.18f);
            field.Emit(new Shard
            {
                X = (float)x,
                Y = (float)y,
                Z = (float)z,
                Vx = (float)(ring * Math.Cos(a) * speed),
                Vy = (float)(u * speed + 1.2),
                Vz = (float)(ring * Math.Sin(a) * speed),
                Rx = (float)(rng.Next() * 6.28),
                Ry = (float)(rng.Next() * 6.28),
                Rz = (float)(rng.Next() * 6.28),
                Wx = (float)((rng.Next() - 0.5) * 30),
                Wy = (float)((rng.Next() - 0.5) * 30),
                Wz = (float)((rng.Next() - 0.5) * 30),
                Size = (float)(0.08 + rng.Next() * 0.07),
                Life = (float)(0.3 + rng.Next() * 0.25),
                R = r,
                G = g,
                B = b,
                Kind = ShardKind.Spark,
            });
        }
    }

    private static (float R, float G, float B) Unpack(uint hex) => (((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);

    /// <summary>A stateless-looking stream of numbers in [0,1): each call hashes the seed and a counter.</summary>
    private struct Stream(long seed, int salt)
    {
        private int _n;

        public double Next() => SeedHash.ToUnit(SeedHash.Hash(seed, _n++, 0, salt));
    }
}
