using System.Buffers.Binary;

namespace NT.AvaloniaControl.PdfViewer.Editing;

/// <summary>Validates the restricted static sfnt TrueType font contract and Unicode coverage.</summary>
internal sealed class PdfTrueTypeFont
{
    private readonly byte[] _bytes;
    private readonly int _glyphCount;
    private readonly List<(int Offset, int Length, ushort Format)> _maps = [];

    internal PdfTrueTypeFont(byte[] bytes)
    {
        _bytes = bytes;
        if (bytes.Length is < 12 or > 64 * 1024 * 1024 || U32(0) != 0x00010000)
            throw Invalid("A static sfnt TrueType font up to 64 MiB is required; collections and CFF fonts are not supported.");
        var count = U16(4);
        if (count is 0 or > 4096 || 12L + count * 16L > bytes.Length)
            throw Invalid("Invalid TrueType table directory.");
        var tables = new Dictionary<uint, (int Offset, int Length)>();
        for (var index = 0; index < count; index++)
        {
            var entry = 12 + index * 16;
            var offset = U32(entry + 8);
            var length = U32(entry + 12);
            if ((ulong)offset + length > (ulong)bytes.Length || offset > int.MaxValue || length > int.MaxValue ||
                !tables.TryAdd(U32(entry), ((int)offset, (int)length)))
                throw Invalid("Invalid or duplicate TrueType table.");
        }
        if (!tables.ContainsKey(Tag("glyf")) || !tables.ContainsKey(Tag("loca")) || tables.ContainsKey(Tag("fvar")))
            throw Invalid("A static glyf/loca TrueType font is required; variable fonts are not supported.");
        if (!tables.TryGetValue(Tag("maxp"), out var maxp) || maxp.Length < 6 || (_glyphCount = U16(maxp.Offset + 4)) == 0)
            throw Invalid("Invalid TrueType glyph count.");
        if (!tables.TryGetValue(Tag("OS/2"), out var os2) || os2.Length < 10)
            throw Invalid("The font must declare its embedding permissions in OS/2.");
        var permissions = U16(os2.Offset + 8);
        if ((permissions & 0x0202) != 0)
            throw Invalid("The font prohibits outline embedding. Supply a font licensed for embedding in editable PDFs.");
        // fsType 4 permits preview/print embedding only; this is an editable annotation.
        if ((permissions & 0x000C) == 0x0004)
            throw Invalid("The font permits preview/print embedding only, not editable embedding.");
        if (!tables.TryGetValue(Tag("cmap"), out var cmap) || cmap.Length < 4)
            throw Invalid("The font has no Unicode cmap.");
        var records = U16(cmap.Offset + 2);
        if (records is 0 or > 1024 || 4L + records * 8L > cmap.Length)
            throw Invalid("Invalid cmap directory.");
        for (var index = 0; index < records; index++)
        {
            var record = cmap.Offset + 4 + index * 8;
            var platform = U16(record);
            var encoding = U16(record + 2);
            if (platform != 0 && !(platform == 3 && encoding is 1 or 10)) continue;
            var relative = U32(record + 4);
            if (relative > (uint)cmap.Length - 2) throw Invalid("Invalid Unicode cmap offset.");
            var offset = checked(cmap.Offset + (int)relative);
            var format = U16(offset);
            if (format is not (4 or 12)) continue;
            var length = format == 4 ? U16(offset + 2) : U32(offset + 4);
            if (length > int.MaxValue || (ulong)relative + length > (ulong)cmap.Length)
                throw Invalid("Invalid Unicode cmap length.");
            ValidateMap(offset, (int)length, format);
            _maps.Add((offset, (int)length, format));
        }
        if (_maps.Count == 0) throw Invalid("A Unicode cmap format 4 or 12 is required.");
        _maps.Sort((a, b) => b.Format.CompareTo(a.Format));
    }

    internal bool Contains(int unicode) => GetGlyphIndex(unicode) != 0;

    internal int GetGlyphIndex(int unicode)
    {
        var found = 0;
        foreach (var map in _maps)
        {
            if (map.Format == 4 && unicode > 0xFFFF) continue;
            var glyph = map.Format == 12 ? Lookup12(map.Offset, unicode) : Lookup4(map.Offset, map.Length, unicode);
            if (glyph == 0) continue;
            if (glyph >= _glyphCount) throw Invalid("A Unicode cmap refers to an invalid glyph.");
            if (found != 0 && found != glyph)
                throw Invalid($"Unicode cmap records disagree for U+{unicode:X4}; ambiguous native font selection is not supported.");
            found = glyph;
        }
        return found;
    }

    private void ValidateMap(int offset, int length, ushort format)
    {
        if (format == 12)
        {
            if (length < 16) throw Invalid("Invalid cmap format 12 header.");
            var groups = U32(offset + 12);
            if (16UL + groups * 12UL > (ulong)length) throw Invalid("Invalid cmap format 12 groups.");
            uint previousEnd = 0;
            for (var index = 0; index < groups; index++)
            {
                var group = checked(offset + 16 + (int)index * 12);
                var start = U32(group);
                var end = U32(group + 4);
                if (start > end || end > 0x10FFFF || index > 0 && start <= previousEnd ||
                    (ulong)U32(group + 8) + end - start >= (ulong)_glyphCount)
                    throw Invalid("Invalid cmap format 12 glyph range.");
                previousEnd = end;
            }
            return;
        }
        if (length < 16) throw Invalid("Invalid cmap format 4 header.");
        var twiceSegments = U16(offset + 6);
        var segments = twiceSegments / 2;
        if (twiceSegments % 2 != 0 || segments == 0 || 16L + segments * 8L > length)
            throw Invalid("Invalid cmap format 4 segments.");
        var previous = -1;
        for (var index = 0; index < segments; index++)
        {
            var end = U16(offset + 14 + index * 2);
            var start = U16(offset + 16 + segments * 2 + index * 2);
            if (start > end || start <= previous) throw Invalid("Unordered cmap format 4 segments.");
            previous = end;
            var rangePosition = offset + 16 + segments * 6 + index * 2;
            var range = U16(rangePosition);
            if (range != 0 && ((range & 1) != 0 || (long)rangePosition + range + (end - start) * 2 + 2 > (long)offset + length))
                throw Invalid("Invalid cmap format 4 glyph array.");
        }
    }

    private int Lookup12(int offset, int unicode)
    {
        var low = 0;
        var high = checked((int)U32(offset + 12)) - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var group = offset + 16 + middle * 12;
            var start = U32(group);
            var end = U32(group + 4);
            if (unicode < start) high = middle - 1;
            else if (unicode > end) low = middle + 1;
            else return checked((int)(U32(group + 8) + unicode - start));
        }
        return 0;
    }

    private int Lookup4(int offset, int length, int unicode)
    {
        if (unicode > 0xFFFF) return 0;
        var segments = U16(offset + 6) / 2;
        for (var index = 0; index < segments; index++)
        {
            var end = U16(offset + 14 + index * 2);
            if (unicode > end) continue;
            var start = U16(offset + 16 + segments * 2 + index * 2);
            if (unicode < start) return 0;
            var delta = U16(offset + 16 + segments * 4 + index * 2);
            var rangePosition = offset + 16 + segments * 6 + index * 2;
            var range = U16(rangePosition);
            if (range == 0) return (unicode + delta) & 0xFFFF;
            var glyphPosition = rangePosition + range + (unicode - start) * 2;
            if (glyphPosition + 2 > offset + length) throw Invalid("Invalid cmap glyph address.");
            var glyph = U16(glyphPosition);
            return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
        }
        return 0;
    }

    private ushort U16(int offset)
    {
        if (offset < 0 || offset > _bytes.Length - 2) throw Invalid("Truncated TrueType table.");
        return BinaryPrimitives.ReadUInt16BigEndian(_bytes.AsSpan(offset, 2));
    }

    private uint U32(int offset)
    {
        if (offset < 0 || offset > _bytes.Length - 4) throw Invalid("Truncated TrueType table.");
        return BinaryPrimitives.ReadUInt32BigEndian(_bytes.AsSpan(offset, 4));
    }

    private static uint Tag(string text) => (uint)text[0] << 24 | (uint)text[1] << 16 | (uint)text[2] << 8 | text[3];
    private static ArgumentException Invalid(string message) => new(message, "trueTypeFont");
}
