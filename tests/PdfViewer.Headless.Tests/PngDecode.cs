using System.Buffers.Binary;
using System.IO.Compression;

namespace PdfViewer.Headless.Tests;

/// <summary>
/// Independent PNG reader used to validate exported page images. Checks the chunk layout and
/// every chunk CRC with a bit-by-bit CRC-32 (no shared table with the package encoder), inflates
/// IDAT through <see cref="ZLibStream"/> (which verifies the zlib header and the Adler-32
/// trailer) and unfilters scanlines into RGBA. Deliberately shares no code with PngEncoder.
/// </summary>
internal static class PngDecode
{
    internal sealed record PngImage(int Width, int Height, byte[] Rgba);

    internal static PngImage Decode(byte[] png)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(signature))
            throw new InvalidDataException("Not a PNG (bad signature).");

        var offset = 8;
        int width = 0, height = 0;
        byte bitDepth = 0, colorType = 0;
        using var idat = new MemoryStream();
        var sawHeader = false;
        var sawEnd = false;
        while (offset < png.Length)
        {
            if (sawEnd) throw new InvalidDataException("Chunks after IEND.");
            if (offset + 12 > png.Length) throw new InvalidDataException("Truncated chunk header.");
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            if (length < 0 || offset + 12L + length > png.Length) throw new InvalidDataException("Truncated chunk data.");
            var type = png.AsSpan(offset + 4, 4);
            var data = png.AsSpan(offset + 8, length);
            var stored = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 8 + length, 4));
            var actual = Crc32(type, data);
            if (stored != actual)
                throw new InvalidDataException($"Chunk CRC mismatch for {System.Text.Encoding.ASCII.GetString(type)}.");
            if (!sawHeader && type.SequenceEqual("IHDR"u8))
            {
                sawHeader = true;
                width = BinaryPrimitives.ReadInt32BigEndian(data[..4]);
                height = BinaryPrimitives.ReadInt32BigEndian(data[4..8]);
                bitDepth = data[8];
                colorType = data[9];
                if (width <= 0 || height <= 0) throw new InvalidDataException("Non-positive IHDR size.");
                if (bitDepth != 8 || colorType != 6) throw new InvalidDataException("Only 8-bit RGBA is expected.");
                if (data[10] != 0 || data[11] != 0 || data[12] != 0)
                    throw new InvalidDataException("Unexpected compression / filter / interlace method.");
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (!sawHeader) throw new InvalidDataException("IDAT before IHDR.");
                idat.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                if (length != 0) throw new InvalidDataException("IEND must be empty.");
                sawEnd = true;
            }
            offset += 12 + length;
        }
        if (!sawHeader || !sawEnd) throw new InvalidDataException("Missing IHDR or IEND.");

        // ZLibStream validates the zlib header and the big-endian Adler-32 trailer.
        var scanlineCount = checked(height * (1 + width * 4));
        var scanlines = new byte[scanlineCount];
        using (var compressed = new MemoryStream(idat.ToArray()))
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
        {
            var read = 0;
            while (read < scanlines.Length)
            {
                var step = zlib.Read(scanlines, read, scanlines.Length - read);
                if (step == 0) throw new InvalidDataException("Truncated image data.");
                read += step;
            }
            if (zlib.ReadByte() != -1) throw new InvalidDataException("Trailing image data.");
        }
        return new PngImage(width, height, Unfilter(scanlines, width, height));
    }

    private static byte[] Unfilter(byte[] scanlines, int width, int height)
    {
        const int bpp = 4;
        var stride = width * bpp;
        var output = new byte[stride * height];
        var prior = new byte[stride];
        for (var row = 0; row < height; row++)
        {
            var filter = scanlines[row * (stride + 1)];
            var input = scanlines.AsSpan(row * (stride + 1) + 1, stride);
            var line = output.AsSpan(row * stride, stride);
            for (var i = 0; i < stride; i++)
            {
                var a = i >= bpp ? line[i - bpp] : (byte)0;
                var b = prior[i];
                var c = i >= bpp ? prior[i - bpp] : (byte)0;
                line[i] = filter switch
                {
                    0 => input[i],
                    1 => unchecked((byte)(input[i] + a)),
                    2 => unchecked((byte)(input[i] + b)),
                    3 => unchecked((byte)(input[i] + ((a + b) >> 1))),
                    4 => unchecked((byte)(input[i] + Paeth(a, b, c))),
                    _ => throw new InvalidDataException($"Unknown filter {filter}."),
                };
            }
            line.CopyTo(prior);
        }
        return output;
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    /// <summary>Bit-by-bit reflected CRC-32 over type+data; independent of the encoder's table.</summary>
    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xffffffffu;
        foreach (var value in type) crc = Step(crc, value);
        foreach (var value in data) crc = Step(crc, value);
        return crc ^ 0xffffffff;

        static uint Step(uint crc, byte value)
        {
            crc ^= value;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
            return crc;
        }
    }
}
