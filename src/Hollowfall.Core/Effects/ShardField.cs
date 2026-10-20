using Hollowfall.Core.World;

namespace Hollowfall.Core.Effects;

public enum ShardKind : byte
{
    /// <summary>A chunk of a broken enemy: tumbles, bounces and lies on the floor for a while.</summary>
    Gib,

    /// <summary>A bright fleck from a hit: short-lived and light.</summary>
    Spark,
}

/// <summary>One flying piece. A plain struct in a fixed array, so effects never allocate while the game runs.</summary>
public struct Shard
{
    public float X, Y, Z;
    public float Vx, Vy, Vz;

    /// <summary>Orientation as Euler angles (radians) and the spin that turns it.</summary>
    public float Rx, Ry, Rz;

    public float Wx, Wy, Wz;

    /// <summary>Edge length in blocks at full scale.</summary>
    public float Size;

    public float Age, Life;

    /// <summary>Colour, 0 to 1 per channel.</summary>
    public float R, G, B;

    public ShardKind Kind;
    public bool Alive;
    public bool Resting;

    /// <summary>How big it is drawn now, 0 to 1: sparks dwindle over their life, gibs shrink away at the end.</summary>
    public readonly float Scale
    {
        get
        {
            if (!Alive) return 0;
            float left = Life - Age;
            return Kind == ShardKind.Spark ? Math.Clamp(left / Life, 0f, 1f) : Math.Clamp(left / ShardField.GibFade, 0f, 1f);
        }
    }
}

/// <summary>
/// A fixed pool of flying shards and their physics: gravity, spin, and bouncing off solid blocks until they come to rest. New
/// shards take the slot of the oldest, so a long fight never grows the pool. The host reads <see cref="Items"/> each frame and
/// draws the live ones.
/// </summary>
public sealed class ShardField
{
    /// <summary>Seconds a lying gib spends shrinking away at the end of its life.</summary>
    public const float GibFade = 0.4f;

    public const float GibGravity = 22f;
    public const float SparkGravity = 7f;

    private int _next;

    public ShardField(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Items = new Shard[capacity];
    }

    public Shard[] Items { get; }

    public int Capacity => Items.Length;

    /// <summary>How many shards are live.</summary>
    public int AliveCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Items.Length; i++) if (Items[i].Alive) n++;
            return n;
        }
    }

    /// <summary>Adds a shard, replacing the oldest slot if the pool is full.</summary>
    public void Emit(Shard shard)
    {
        shard.Alive = true;
        shard.Age = 0;
        Items[_next] = shard;
        _next = (_next + 1) % Items.Length;
    }

    /// <summary>Advances every live shard by <paramref name="dt"/> seconds.</summary>
    public void Step(float dt, Func<int, int, int, BlockType> blockAt)
    {
        dt = Math.Min(dt, 0.05f); // a slow frame must not throw shards through walls
        for (int i = 0; i < Items.Length; i++)
        {
            ref var s = ref Items[i];
            if (!s.Alive) continue;
            s.Age += dt;
            if (s.Age >= s.Life)
            {
                s.Alive = false;
                continue;
            }
            if (s.Resting) continue;

            s.Vy -= (s.Kind == ShardKind.Spark ? SparkGravity : GibGravity) * dt;
            s.Rx += s.Wx * dt;
            s.Ry += s.Wy * dt;
            s.Rz += s.Wz * dt;

            float half = s.Size * 0.5f;

            // One axis at a time, so a shard slides along a wall and lands on a floor instead of stopping dead.
            float nx = s.X + s.Vx * dt;
            if (Solid(blockAt, nx + Math.Sign(s.Vx) * half, s.Y, s.Z)) { s.Vx *= -0.35f; s.Vz *= 0.8f; }
            else s.X = nx;

            float nz = s.Z + s.Vz * dt;
            if (Solid(blockAt, s.X, s.Y, nz + Math.Sign(s.Vz) * half)) { s.Vz *= -0.35f; s.Vx *= 0.8f; }
            else s.Z = nz;

            float ny = s.Y + s.Vy * dt;
            if (s.Vy < 0 && Solid(blockAt, s.X, ny - half, s.Z))
            {
                s.Y = MathF.Floor(ny - half) + 1f + half;
                if (s.Kind == ShardKind.Gib && MathF.Abs(s.Vy) > 2.2f)
                {
                    s.Vy = -s.Vy * 0.38f;
                    s.Vx *= 0.62f;
                    s.Vz *= 0.62f;
                    s.Wx *= 0.6f; s.Wy *= 0.6f; s.Wz *= 0.6f;
                }
                else
                {
                    s.Vx = s.Vy = s.Vz = 0;
                    s.Wx = s.Wy = s.Wz = 0;
                    s.Resting = true;
                }
            }
            else if (s.Vy > 0 && Solid(blockAt, s.X, ny + half, s.Z))
            {
                s.Vy = -s.Vy * 0.3f;
            }
            else s.Y = ny;
        }
    }

    private static bool Solid(Func<int, int, int, BlockType> blockAt, float x, float y, float z) =>
        BlockInfo.IsSolid(blockAt((int)MathF.Floor(x), (int)MathF.Floor(y), (int)MathF.Floor(z)));
}
