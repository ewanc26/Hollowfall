using Hollowfall.Core.World;

namespace Hollowfall.Core.Physics;

/// <summary>
/// A walking body: 0.6 wide, 1.8 tall, with gravity, jumping, wall sliding and automatic stepping up
/// one-block ledges (which is what the dungeon staircases need). Collision is against solid voxels only.
/// Position is the centre of the feet. Deterministic and free of engine types, so it can be tested
/// and run on any thread.
/// </summary>
public sealed class PlayerBody
{
    public const double HalfWidth = 0.3;
    public const double Height = 1.8;
    public const double StepHeight = 1.0;
    public const double WalkSpeed = 4.3;
    public const double Gravity = 28.0;
    public const double JumpSpeed = 8.5;
    public const double TerminalSpeed = 50.0;

    private const double Epsilon = 1e-4;
    private const double MaxSubstep = 0.05;

    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double VelocityY { get; private set; }
    public bool OnGround { get; private set; }

    public PlayerBody(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Advances the body by <paramref name="dt"/> seconds. <paramref name="moveX"/> and <paramref name="moveZ"/> are the
    /// desired horizontal direction (any length up to 1; longer vectors are normalised).
    /// </summary>
    public void Step(double dt, double moveX, double moveZ, bool jump, Func<int, int, int, BlockType> blockAt, double speed = WalkSpeed)
    {
        double len = Math.Sqrt(moveX * moveX + moveZ * moveZ);
        if (len > 1) { moveX /= len; moveZ /= len; }

        while (dt > 1e-9)
        {
            double h = Math.Min(dt, MaxSubstep);
            dt -= h;
            Substep(h, moveX, moveZ, jump, blockAt, speed);
            jump = false; // one jump press per call, however many substeps it spans
        }
    }

    private void Substep(double dt, double moveX, double moveZ, bool jump, Func<int, int, int, BlockType> blockAt, double speed)
    {
        if (jump && OnGround)
        {
            VelocityY = JumpSpeed;
            OnGround = false;
        }

        VelocityY = Math.Max(VelocityY - Gravity * dt, -TerminalSpeed);

        bool wasOnGround = OnGround;
        MoveHorizontal(moveX * speed * dt, 0, wasOnGround, blockAt);
        MoveHorizontal(0, moveZ * speed * dt, wasOnGround, blockAt);
        MoveVertical(VelocityY * dt, blockAt);
    }

    private void MoveHorizontal(double dx, double dz, bool canStep, Func<int, int, int, BlockType> blockAt)
    {
        if (dx == 0 && dz == 0) return;
        double nx = X + dx, nz = Z + dz;

        if (!Collides(nx, Y, nz, blockAt))
        {
            X = nx;
            Z = nz;
            return;
        }

        if (canStep)
        {
            // Try the same move one step higher; keep it only if the body fits there and there is floor to land on.
            double raised = Y + StepHeight;
            if (!Collides(nx, raised, nz, blockAt) && !Collides(X, raised, Z, blockAt))
            {
                X = nx;
                Z = nz;
                Y = raised;
                return;
            }
        }

        // Blocked: slide up to the face of whatever is in the way.
        if (dx != 0)
            X = dx > 0 ? Math.Floor(X + HalfWidth + dx) - HalfWidth - Epsilon : Math.Floor(X - HalfWidth + dx) + 1 + HalfWidth + Epsilon;
        else
            Z = dz > 0 ? Math.Floor(Z + HalfWidth + dz) - HalfWidth - Epsilon : Math.Floor(Z - HalfWidth + dz) + 1 + HalfWidth + Epsilon;

        // The snap above can only move us backwards; if it still overlaps (corner cases), undo the move entirely.
        if (Collides(X, Y, Z, blockAt))
        {
            X = nx - dx;
            Z = nz - dz;
        }
    }

    private void MoveVertical(double dy, Func<int, int, int, BlockType> blockAt)
    {
        // Move in slices of at most a quarter block so fast falls cannot skip over thin floors.
        int slices = (int)Math.Ceiling(Math.Abs(dy) / 0.25);
        double slice = dy / Math.Max(slices, 1);
        for (int i = 0; i < slices; i++)
            if (!MoveVerticalOnce(slice, blockAt)) return;
    }

    /// <summary>Returns false if the body hit something and stopped.</summary>
    private bool MoveVerticalOnce(double dy, Func<int, int, int, BlockType> blockAt)
    {
        double ny = Y + dy;
        if (!Collides(X, ny, Z, blockAt))
        {
            Y = ny;
            OnGround = false;
            return true;
        }

        if (dy < 0)
        {
            Y = Math.Floor(Y + dy) + 1; // rest on top of the block below
            OnGround = true;
        }
        else
        {
            Y = Math.Floor(Y + Height + dy) - Height - Epsilon; // bump the head
            OnGround = false;
        }
        VelocityY = 0;
        return false;
    }

    private static bool Collides(double x, double y, double z, Func<int, int, int, BlockType> blockAt)
    {
        int x0 = (int)Math.Floor(x - HalfWidth), x1 = (int)Math.Floor(x + HalfWidth);
        int z0 = (int)Math.Floor(z - HalfWidth), z1 = (int)Math.Floor(z + HalfWidth);
        int y0 = (int)Math.Floor(y), y1 = (int)Math.Floor(y + Height);
        for (int bx = x0; bx <= x1; bx++)
            for (int by = y0; by <= y1; by++)
                for (int bz = z0; bz <= z1; bz++)
                    if (BlockInfo.IsSolid(blockAt(bx, by, bz))) return true;
        return false;
    }
}
