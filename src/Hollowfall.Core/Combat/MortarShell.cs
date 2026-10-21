using Hollowfall.Core.World;

namespace Hollowfall.Core.Combat;

/// <summary>A slow arcing shell from the Block Mortar. It detonates on the first wall or enemy it reaches, or when its lifetime runs out.</summary>
public sealed class MortarShell
{
    public const double Speed = 22;
    public const double Gravity = 12;
    public const double LifetimeSeconds = 4;

    internal MortarShell(int id, Vec3 position, Vec3 velocity)
    {
        Id = id;
        Position = position;
        Velocity = velocity;
    }

    public int Id { get; }
    public Vec3 Position { get; internal set; }
    public Vec3 Velocity { get; internal set; }
    internal double Age { get; set; }
}

/// <summary>What a Block Mortar detonation does to the world: a sphere of digging, and falling-off damage at its edge.</summary>
public static class MortarBlast
{
    public const double Radius = 2.5;
    public const double CenterDamage = 40;
    public const double EdgeDamage = 10;

    /// <summary>Digs every 10 cm cube of breakable material whose centre is inside the sphere. Returns how many cubes were removed.</summary>
    public static int Carve(VoxelWorld world, Vec3 center)
    {
        const double cube = 1.0 / BlockHole.Side;
        int reach = (int)Math.Ceiling(Radius);
        int cx = (int)Math.Floor(center.X), cy = (int)Math.Floor(center.Y), cz = (int)Math.Floor(center.Z);
        int removed = 0;
        for (int x = cx - reach; x <= cx + reach; x++)
            for (int y = cy - reach; y <= cy + reach; y++)
                for (int z = cz - reach; z <= cz + reach; z++)
                {
                    if (!BlockInfo.Breakable(world.GetBlock(x, y, z))) continue;
                    for (int sz = 0; sz < BlockHole.Side; sz++)
                        for (int sy = 0; sy < BlockHole.Side; sy++)
                            for (int sx = 0; sx < BlockHole.Side; sx++)
                            {
                                var sub = new Vec3(x + (sx + 0.5) * cube, y + (sy + 0.5) * cube, z + (sz + 0.5) * cube);
                                if ((sub - center).Length > Radius) continue;
                                if (world.DigCube(x, y, z, sx, sy, sz)) removed++;
                            }
                }
        return removed;
    }

    /// <summary>Damage to an enemy whose body centre is <paramref name="distance"/> blocks from the blast, falling linearly to the edge.</summary>
    public static double DamageAt(double distance) =>
        EdgeDamage + (CenterDamage - EdgeDamage) * (1 - Math.Clamp(distance / Radius, 0, 1));
}
