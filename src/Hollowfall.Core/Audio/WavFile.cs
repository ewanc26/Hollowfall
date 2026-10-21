using System.Text;

namespace Hollowfall.Core.Audio;

/// <summary>Mono 16-bit PCM WAV, the format the game's sound effects are stored in.</summary>
public static class WavFile
{
    public static byte[] Encode(float[] samples, int sampleRate)
    {
        const int channels = 1, bits = 16;
        int dataBytes = samples.Length * 2;
        var bytes = new byte[44 + dataBytes];
        using var w = new BinaryWriter(new MemoryStream(bytes));
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataBytes);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);
        w.Write((short)channels);
        w.Write(sampleRate);
        w.Write(sampleRate * channels * bits / 8);
        w.Write((short)(channels * bits / 8));
        w.Write((short)bits);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataBytes);
        foreach (var s in samples)
            w.Write((short)Math.Clamp((int)Math.Round(s * short.MaxValue), short.MinValue, short.MaxValue));
        return bytes;
    }
}
