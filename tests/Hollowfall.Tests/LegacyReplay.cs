using System.Text;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Replay;

namespace Hollowfall.Tests;

/// <summary>
/// Writes a one-frame run in an older replay format, for the loader's compatibility tests. The frame uses the old control
/// layout (ten flag bits, then the respawn marker at bit 10 and the delta mask at bit 11), with a single changed direction.
/// </summary>
internal static class LegacyReplay
{
    private const uint Control = 64u << 11; // mask bit 6: Dx changed
    private const uint Delta = 65533;       // zigzag of -32767

    public static byte[] OneFrame(int format, string seed, RunStats stats)
    {
        var o = new List<byte> { (byte)'H', (byte)'F', (byte)'R', (byte)'P', (byte)format, (byte)RunReplay.SimVersion };
        if (format >= RunReplay.FormatVersionWithoutItems)
        {
            PutVar(o, (uint)stats.ClampedVigour);
            PutVar(o, (uint)stats.ClampedMight);
        }
        if (format >= RunReplay.FormatVersionWithCream)
        {
            PutVar(o, (uint)stats.ClampedUses);
            PutVar(o, stats.Charm is { } charm ? (uint)charm + 1 : 0);
        }

        var bytes = Encoding.UTF8.GetBytes(seed);
        PutVar(o, (uint)bytes.Length);
        o.AddRange(bytes);
        PutVar(o, 0); // region x
        PutVar(o, 0); // region z
        PutVar(o, 0); // floor
        PutVar(o, 0); // end tick
        o.Add((byte)OverallGrade.C);
        PutVar(o, 2);    // points, zigzag of 1
        PutVar(o, 1000); // milliseconds
        PutVar(o, 1);    // ticks
        PutVar(o, 1);    // runs
        PutVar(o, 1);    // repeat
        PutVar(o, Control);
        PutVar(o, Delta);

        uint sum = ReplayCodec.Fnv32(o);
        o.AddRange(BitConverter.GetBytes(sum));
        return [.. o];
    }

    private static void PutVar(List<byte> o, uint v)
    {
        while (v >= 0x80)
        {
            o.Add((byte)(v | 0x80));
            v >>= 7;
        }
        o.Add((byte)v);
    }
}
