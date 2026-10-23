using System.Buffers.Binary;
using System.IO.Compression;

namespace NT.AvaloniaControl.PdfViewer;

/// <summary>
/// Minimal PNG encoder for the viewer's opaque BGRA page renders. Writes 8-bit RGBA with a
/// filter byte of 0 per scanline and one zlib-compressed IDAT, so page image export needs no
/// platform imaging service and behaves identically in headless and native runs.
/// </summary>
internal static class PngEncoder
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    internal static byte[] Encode(PdfRenderedPage page)
    {
        var width = page.Width;
        var height = page.Height;
        var scanlines = GC.AllocateUninitializedArray<byte>(checked(height * (1 + width * 4)));
        var offset = 0;
        for (var row = 0; row < height; row++)
        {
            scanlines[offset++] = 0; // filter: none
            var source = row * page.Stride;
            for (var column = 0; column < width; column++)
            {
                var pixel = source + column * 4;
                scanlines[offset++] = page.Pixels[pixel + 2]; // R
                scanlines[offset++] = page.Pixels[pixel + 1]; // G
                scanlines[offset++] = page.Pixels[pixel];     // B
                scanlines[offset++] = 255;                    // opaque
            }
        }

        using var compressed = new MemoryStream();
        // zlib wrapper: 0x78 0x9C header, raw deflate body, big-endian Adler-32 trailer.
        compressed.WriteByte(0x78);
        compressed.WriteByte(0x9C);
        using (var deflate = new DeflateStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            deflate.Write(scanlines);
        Span<byte> trailer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(trailer, Adler32(scanlines));
        compressed.Write(trailer);

        var output = new MemoryStream();
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        output.Write(signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8; // bit depth
        header[9] = 6; // color type: RGBA
        // header[10..13] stays 0: compression method, filter method, interlace.
        WriteChunk(output, "IHDR"u8, header);
        WriteChunk(output, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);
        Span<byte> crc = stackalloc byte[4];
        // PNG chunk CRC is one CRC-32 over type+data: seed with the running value after the
        // type (no finalize in between), then finalize once at the end.
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(data, Crc32(type, 0xffffffff)) ^ 0xffffffff);
        output.Write(crc);
    }

    private static uint Crc32(ReadOnlySpan<byte> data, uint seed)
    {
        var crc = seed;
        foreach (var value in data)
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        return crc;
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        const uint Mod = 65521;
        uint a = 1, b = 0;
        var index = 0;
        while (index < data.Length)
        {
            // The running sums stay below the modulus when stepped in blocks of 5552 bytes.
            var end = Math.Min(index + 5552, data.Length);
            for (; index < end; index++)
            {
                a += data[index];
                b += a;
            }
            a %= Mod;
            b %= Mod;
        }
        return (b << 16) | a;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
