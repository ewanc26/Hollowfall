using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Replay;

/// <summary>
/// One fixed step of recorded input in its canonical, quantised form. The recorder hands these values back to the live game,
/// so what the simulation saw while you played is exactly what a replay feeds it. Positions are in 1/1024 of a block and
/// the look direction is three 16-bit components, which keeps a frame to a few bytes without any rounding drift.
/// </summary>
public readonly record struct ReplayFrame(int Fx, int Fy, int Fz, int Ex, int Ey, int Ez, int Dx, int Dy, int Dz, int Flags, int Respawns, int Speed = 0)
{
    public const int PositionScale = 1024;
    public const int DirectionScale = 32767;

    /// <summary>Largest stored position magnitude (about 131 072 blocks): far beyond any dungeon, small enough that deltas fit 32 bits.</summary>
    public const int MaxPosition = 1 << 27;

    /// <summary>Largest eye offset from the feet (1024 blocks).</summary>
    public const int MaxEyeOffset = 1 << 20;

    public const int MaxRespawns = 16;

    public const int AirborneBit = 1 << 0;
    public const int SlidingBit = 1 << 1;
    public const int RevolverBit = 1 << 2;
    public const int ShotgunBit = 1 << 3;
    public const int ParryBit = 1 << 4;
    public const int TealBit = 1 << 5;
    public const int VioletBit = 1 << 6;
    public const int InteractBit = 1 << 7;
    public const int ClosePortalsBit = 1 << 8;
    public const int CrossedBit = 1 << 9;
    public const int AllFlags = 0x3FF;

    /// <summary>Speed is stored in 1/64 of a block per second. The Meteor reads only the speed and whether the player just crossed a portal, so that is all the frame keeps of the velocity.</summary>
    public const int SpeedScale = 64;

    public const int MaxSpeed = 65535;

    /// <summary>Quantises a view and its buttons. Non-finite numbers become zero and out-of-range ones are clamped.</summary>
    public static ReplayFrame From(PlayerView view, PlayerActions actions, int respawns = 0)
    {
        int fx = Pos(view.Feet.X), fy = Pos(view.Feet.Y), fz = Pos(view.Feet.Z);
        int ex = Offset(view.Eye.X - view.Feet.X), ey = Offset(view.Eye.Y - view.Feet.Y), ez = Offset(view.Eye.Z - view.Feet.Z);

        var f = view.Forward;
        double len = f.Length;
        int dx = 0, dy = 0, dz = -DirectionScale;
        if (double.IsFinite(len) && len > 1e-9)
        {
            dx = Dir(f.X / len);
            dy = Dir(f.Y / len);
            dz = Dir(f.Z / len);
            if (dx == 0 && dy == 0 && dz == 0) dz = -DirectionScale;
        }

        int flags = 0;
        if (view.Airborne) flags |= AirborneBit;
        if (view.Sliding) flags |= SlidingBit;
        if (actions.FireRevolver) flags |= RevolverBit;
        if (actions.FireShotgun) flags |= ShotgunBit;
        if (actions.Parry) flags |= ParryBit;
        if (actions.PortalTeal) flags |= TealBit;
        if (actions.PortalViolet) flags |= VioletBit;
        if (actions.Interact) flags |= InteractBit;
        if (actions.ClosePortals) flags |= ClosePortalsBit;
        if (view.Crossed) flags |= CrossedBit;
        double speed = view.Velocity.Length;
        int quantisedSpeed = double.IsFinite(speed) ? (int)Math.Clamp(Math.Round(speed * SpeedScale), 0, MaxSpeed) : 0;
        return new ReplayFrame(fx, fy, fz, ex, ey, ez, dx, dy, dz, flags, Math.Clamp(respawns, 0, MaxRespawns), quantisedSpeed);
    }

    private static int Pos(double v) => double.IsFinite(v) ? (int)Math.Clamp(Math.Round(v * PositionScale), -MaxPosition, MaxPosition) : 0;

    private static int Offset(double v) => double.IsFinite(v) ? (int)Math.Clamp(Math.Round(v * PositionScale), -MaxEyeOffset, MaxEyeOffset) : 0;

    private static int Dir(double v) => (int)Math.Clamp(Math.Round(v * DirectionScale), -DirectionScale, DirectionScale);

    /// <summary>Whether every field is inside the range the format allows (used when decoding untrusted data).</summary>
    public bool InRange =>
        Math.Abs((long)Fx) <= MaxPosition && Math.Abs((long)Fy) <= MaxPosition && Math.Abs((long)Fz) <= MaxPosition &&
        Math.Abs((long)Ex) <= MaxEyeOffset && Math.Abs((long)Ey) <= MaxEyeOffset && Math.Abs((long)Ez) <= MaxEyeOffset &&
        Math.Abs((long)Dx) <= DirectionScale && Math.Abs((long)Dy) <= DirectionScale && Math.Abs((long)Dz) <= DirectionScale &&
        (Dx != 0 || Dy != 0 || Dz != 0) &&
        (Flags & ~AllFlags) == 0 && Respawns is >= 0 and <= MaxRespawns && Speed is >= 0 and <= MaxSpeed;

    public Vec3 Feet => new((double)Fx / PositionScale, (double)Fy / PositionScale, (double)Fz / PositionScale);

    /// <summary>The view the simulation sees. Only division, addition and a square root, so it is exact on every machine.</summary>
    public PlayerView ToView()
    {
        var feet = Feet;
        var eye = new Vec3(feet.X + (double)Ex / PositionScale, feet.Y + (double)Ey / PositionScale, feet.Z + (double)Ez / PositionScale);
        var forward = new Vec3((double)Dx / DirectionScale, (double)Dy / DirectionScale, (double)Dz / DirectionScale).Normalized();
        return new PlayerView(feet, eye, forward, (Flags & AirborneBit) != 0, (Flags & SlidingBit) != 0, new Vec3((double)Speed / SpeedScale, 0, 0), (Flags & CrossedBit) != 0);
    }

    public PlayerActions ToActions() => new(
        (Flags & RevolverBit) != 0, (Flags & ShotgunBit) != 0, (Flags & ParryBit) != 0,
        (Flags & TealBit) != 0, (Flags & VioletBit) != 0, (Flags & InteractBit) != 0, (Flags & ClosePortalsBit) != 0);
}
