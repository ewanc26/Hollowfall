using Hollowfall.Core.Physics;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Topology;

/// <summary>
/// A box of blocks whose two X faces are glued together. Walking out of the +X face comes in at the −X face and carries on,
/// so the space has no edge along X. When <see cref="Mirrored"/> is set the seam also flips Z, which makes the space
/// non-orientable (a Klein-bottle style gluing): a walker who goes round once comes back mirrored sideways. The walls at the
/// ±Z sides and the floor are solid, and the top is open. Everything here is a pure function of the box and the body.
/// </summary>
public sealed class GluedBox
{
    private readonly Func<int, int, int, BlockType> _interior;

    public GluedBox(int width, int depth, Func<int, int, int, BlockType> interior, bool mirrored)
    {
        Width = width;
        Depth = depth;
        Mirrored = mirrored;
        _interior = interior;
    }

    public int Width { get; }
    public int Depth { get; }
    public bool Mirrored { get; }

    /// <summary>The block in a cell of the unwrapped space: cells past the glued faces are read from the far side.</summary>
    public BlockType BlockAt(int x, int y, int z)
    {
        if (y < 0 || z < 0 || z >= Depth) return BlockType.Stone;
        int seams = (int)Math.Floor((double)x / Width);
        int cx = x - seams * Width;
        bool flipped = Mirrored && (seams & 1) != 0;
        return _interior(cx, y, flipped ? Depth - 1 - z : z);
    }

    /// <summary>
    /// Steps the body through the space and, if its centre has passed a glued face, moves it to the matching point on the
    /// far side with its velocity carried over. Returns true when it crossed a seam.
    /// </summary>
    public bool Step(PlayerBody body, double dt, double moveX, double moveZ, double speed = PlayerBody.WalkSpeed)
    {
        body.Step(dt, moveX, moveZ, false, BlockAt, speed);
        if (body.X >= 0 && body.X < Width) return false;

        double x = body.X - Math.Floor(body.X / Width) * Width;
        double z = Mirrored ? Depth - body.Z : body.Z;
        double vz = Mirrored ? -body.VelocityZ : body.VelocityZ;
        body.Teleport(x, body.Y, z, body.VelocityX, body.VelocityY, vz);
        return true;
    }
}
