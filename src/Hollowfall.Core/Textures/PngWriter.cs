using System.IO.Compression;

namespace Hollowfall.Core.Textures;

/// <summary>A minimal PNG encoder (8-bit RGBA, no filtering) so previews can be written without an imaging library.</summary>
public static class PngWriter
{
    public static byte[] Encode(byte[] rgba, int width, int height)
    {
        if (rgba.Length != width * height * 4) throw new ArgumentException("pixel data does not match the size", nameof(rgba));

        var raw = new byte[(width * 4 + 1) * height];
        for (int y = 0; y < height; y++)
        {
            raw[y * (width * 4 + 1)] = 0; // filter: none
            Buffer.BlockCopy(rgba, y * width * 4, raw, y * (width * 4 + 1) + 1, width * 4);
        }

        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        WriteInt(header, 0, width);
        WriteInt(header, 4, height);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        Chunk(output, "IHDR", header);

        using (var compressed = new MemoryStream())
        {
            using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);
            Chunk(output, "IDAT", compressed.ToArray());
        }
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    /// <summary>Scales a picture up by a whole factor with no smoothing, for looking at small pixel art.</summary>
    public static (byte[] Rgba, int Width, int Height) Upscale(byte[] rgba, int width, int height, int factor)
    {
        int w = width * factor, h = height * factor;
        var result = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                Buffer.BlockCopy(rgba, ((y / factor) * width + x / factor) * 4, result, (y * w + x) * 4, 4);
        return (result, w, h);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        output.Write(length);
        var body = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) body[i] = (byte)type[i];
        Buffer.BlockCopy(data, 0, body, 4, data.Length);
        output.Write(body);
        var crc = new byte[4];
        WriteInt(crc, 0, unchecked((int)Crc(body)));
        output.Write(crc);
    }

    private static uint Crc(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }
}
