using System.Buffers.Binary;

namespace NT.AvaloniaControl.Markdown;

/// <summary>
/// Reads unscaled dimensions only from a complete standard PNG IHDR or an 8-bit
/// JPEG SOF0/SOF1/SOF2 header. This does not validate or decode the image payload.
/// Other formats, truncated headers and uncertain frame layouts use native decode.
/// </summary>
internal static class MarkdownImageDimensions
{
    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];

    internal static bool TryReadPixelCount(ReadOnlySpan<byte> encoded, out long pixels)
    {
        pixels = 0;
        if (encoded.StartsWith(PngSignature)) return TryReadPng(encoded, out pixels);
        if (encoded.Length < 2 || encoded[0] != 0xff || encoded[1] != 0xd8) return false;
        return TryReadJpeg(encoded, out pixels);
    }

    private static bool TryReadPng(ReadOnlySpan<byte> encoded, out long pixels)
    {
        pixels = 0;
        // Signature + length + IHDR type/payload + CRC. Do not trust a partial IHDR.
        if (encoded.Length < 33 || BinaryPrimitives.ReadUInt32BigEndian(encoded[8..]) != 13 ||
            !encoded.Slice(12, 4).SequenceEqual("IHDR"u8)) return false;
        var width = BinaryPrimitives.ReadUInt32BigEndian(encoded[16..]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(encoded[20..]);
        if (width is 0 or > int.MaxValue || height is 0 or > int.MaxValue ||
            encoded[26] != 0 || encoded[27] != 0 || encoded[28] > 1 ||
            !ValidPngDepth(encoded[24], encoded[25]) ||
            Crc32(encoded.Slice(12, 17)) != BinaryPrimitives.ReadUInt32BigEndian(encoded[29..])) return false;
        pixels = (long)width * height;
        return true;
    }

    private static bool ValidPngDepth(byte depth, byte color) => color switch
    {
        0 => depth is 1 or 2 or 4 or 8 or 16,
        2 or 4 or 6 => depth is 8 or 16,
        3 => depth is 1 or 2 or 4 or 8,
        _ => false
    };

    private static bool TryReadJpeg(ReadOnlySpan<byte> encoded, out long pixels)
    {
        pixels = 0;
        var offset = 2;
        while (offset < encoded.Length)
        {
            if (encoded[offset++] != 0xff) return false;
            while (offset < encoded.Length && encoded[offset] == 0xff) offset++;
            if (offset == encoded.Length) return false;
            var marker = encoded[offset++];
            // Stop before entropy data; do not search compressed data or a later image for a frame.
            if (marker is 0 or 0xd8 or 0xd9 or 0xda or 0xde or 0xdf) return false;
            if (marker is 0x01 or >= 0xd0 and <= 0xd7) continue;
            if (encoded.Length - offset < 2) return false;
            var length = BinaryPrimitives.ReadUInt16BigEndian(encoded[offset..]);
            if (length < 2 || length > encoded.Length - offset) return false;
            if (marker is 0xc0 or 0xc1 or 0xc2)
            {
                if (length < 8) return false;
                var frame = encoded.Slice(offset + 2, length - 2);
                var height = BinaryPrimitives.ReadUInt16BigEndian(frame[1..]);
                var width = BinaryPrimitives.ReadUInt16BigEndian(frame[3..]);
                var components = frame[5];
                // Zero height requires a later DNL; other precisions/frame processes
                // remain the native decoder's responsibility rather than being guessed.
                if (frame[0] != 8 || width == 0 || height == 0 || components is not (1 or 3 or 4) ||
                    length != 8 + 3 * components) return false;
                pixels = (long)width * height;
                return true;
            }
            // Lossless, differential and hierarchical frames need different interpretation.
            if (marker is >= 0xc0 and <= 0xcf && marker is not (0xc4 or 0xc8 or 0xcc)) return false;
            offset += length;
        }
        return false;
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }
        return ~crc;
    }
}
