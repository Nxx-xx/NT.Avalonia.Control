using System.Buffers.Binary;
using System.IO.Compression;
using NT.AvaloniaControl.PdfViewer;

namespace NT.AvaloniaControl.Demo.Pages.PdfViewer;

/// <summary>
/// Independent PNG reader for the demo smoke run: verifies every chunk CRC with a bit-by-bit
/// CRC-32, inflates IDAT through ZLibStream (zlib header + Adler-32 trailer) and unfilters into
/// RGBA, so the smoke check validates the exported bytes rather than only the file header.
/// </summary>
internal static class PdfSmokePng
{
    internal sealed record PngImage(int Width, int Height, byte[] Rgba);

    /// <summary>Decodes the PNG or throws <see cref="InvalidDataException"/> on any structural or checksum error.</summary>
    internal static PngImage Decode(byte[] png)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(signature))
            throw new InvalidDataException("Not a PNG (bad signature).");
        var offset = 8;
        int width = 0, height = 0;
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
            if (stored != Crc32(type, data))
                throw new InvalidDataException("Chunk CRC mismatch.");
            if (!sawHeader && type.SequenceEqual("IHDR"u8))
            {
                sawHeader = true;
                width = BinaryPrimitives.ReadInt32BigEndian(data[..4]);
                height = BinaryPrimitives.ReadInt32BigEndian(data[4..8]);
                if (width <= 0 || height <= 0 || data[8] != 8 || data[9] != 6)
                    throw new InvalidDataException("Unexpected IHDR.");
            }
            else if (type.SequenceEqual("IDAT"u8)) idat.Write(data);
            else if (type.SequenceEqual("IEND"u8)) sawEnd = true;
            offset += 12 + length;
        }
        if (!sawHeader || !sawEnd) throw new InvalidDataException("Missing IHDR or IEND.");
        var scanlines = new byte[height * (1 + width * 4)];
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
        var rgba = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            if (scanlines[row * (width * 4 + 1)] != 0) throw new InvalidDataException("Unexpected scanline filter.");
            Array.Copy(scanlines, row * (width * 4 + 1) + 1, rgba, row * width * 4, width * 4);
        }
        return new PngImage(width, height, rgba);
    }

    /// <summary>True when the decoded RGBA pixels match a reference render's BGRA pixels exactly.</summary>
    internal static bool Matches(PngImage image, PdfRenderedPage rendered)
    {
        if (image.Width != rendered.Width || image.Height != rendered.Height) return false;
        for (var row = 0; row < rendered.Height; row++)
        {
            for (var column = 0; column < rendered.Width; column++)
            {
                var source = row * rendered.Stride + column * 4;
                var target = (row * rendered.Width + column) * 4;
                if (image.Rgba[target] != rendered.Pixels[source + 2]
                    || image.Rgba[target + 1] != rendered.Pixels[source + 1]
                    || image.Rgba[target + 2] != rendered.Pixels[source]
                    || image.Rgba[target + 3] != 255)
                    return false;
            }
        }
        return true;
    }

    /// <summary>Bit-by-bit reflected CRC-32 over type+data; independent of the package encoder.</summary>
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
