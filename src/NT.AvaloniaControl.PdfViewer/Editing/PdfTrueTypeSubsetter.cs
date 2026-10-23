using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace NT.AvaloniaControl.PdfViewer.Editing;

internal sealed record PdfTrueTypeSubsetResult(byte[] FontBytes, bool WasSubset,
    int OriginalGlyphCount, int RetainedGlyphCount, string Reason);

/// <summary>
/// A bounded static TrueType subsetter for the horizontal, unshaped FreeText contract.
/// Retains .notdef and the composite closure, compacts glyph IDs, and rebuilds every
/// glyph-indexed table that is emitted. It does not call an installed font or process.
/// </summary>
internal static class PdfTrueTypeSubsetter
{
    internal static PdfTrueTypeSubsetResult Create(byte[] font, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        if (font.Length > 32 * 1024 * 1024)
            throw Invalid("TrueType fonts must not exceed 32 MiB.");
        // Reuse the permissions and supported Unicode-map checks used by the writer.
        _ = new PdfTrueTypeFont(font);
        return new Builder(font, text, cancellationToken).Build();
    }

    private sealed class Builder
    {
        private readonly byte[] _font;
        private readonly string _text;
        private readonly CancellationToken _cancellationToken;
        private readonly Dictionary<uint, Table> _tables = [];
        private readonly List<Map> _maps = [];
        private readonly SortedDictionary<int, int> _characters = [];
        private readonly Dictionary<int, Glyph> _glyphs = [];
        private readonly byte[] _states;
        private readonly int[] _locations;
        private readonly int _glyphCount;
        private readonly int _metricCount;
        private readonly Table _glyf;
        private readonly Table _hmtx;
        private readonly ushort _permissions;
        private int _componentCount;

        internal Builder(byte[] font, string text, CancellationToken cancellationToken)
        {
            _font = font;
            _text = text;
            _cancellationToken = cancellationToken;
            var tableCount = U16(4);
            if (tableCount > 256) throw Invalid("The TrueType subsetter supports at most 256 tables.");
            var directoryEnd = 12 + tableCount * 16;
            for (var index = 0; index < tableCount; index++)
            {
                var entry = 12 + index * 16;
                var offset = checked((int)U32(entry + 8));
                var length = checked((int)U32(entry + 12));
                if (length > 0 && (offset < directoryEnd || (offset & 3) != 0))
                    throw Invalid("A TrueType table overlaps the directory or is not aligned.");
                _tables.Add(U32(entry), new(offset, length));
            }
            var physical = _tables.Values.Where(x => x.Length > 0).OrderBy(x => x.Offset).ToArray();
            for (var index = 1; index < physical.Length; index++)
                if ((long)physical[index - 1].Offset + physical[index - 1].Length > physical[index].Offset)
                    throw Invalid("Overlapping TrueType tables are not supported.");

            var head = Required("head", 54);
            var hhea = Required("hhea", 36);
            var maxp = Required("maxp", 32);
            var os2 = Required("OS/2", 10);
            _permissions = U16(os2.Offset + 8);
            if (U32(head.Offset + 12) != 0x5F0F3CF5 || U16(head.Offset + 18) is < 16 or > 16384 ||
                U16(head.Offset + 52) != 0 || U32(maxp.Offset) != 0x00010000 || U32(hhea.Offset) != 0x00010000)
                throw Invalid("Unsupported or invalid TrueType outline headers.");
            var locationFormat = I16(head.Offset + 50);
            if (locationFormat is not (0 or 1)) throw Invalid("Invalid TrueType loca format.");
            _glyphCount = U16(maxp.Offset + 4);
            _metricCount = U16(hhea.Offset + 34);
            if (_metricCount is 0 || _metricCount > _glyphCount)
                throw Invalid("Invalid horizontal metric count.");
            _glyf = Required("glyf", 0);
            var loca = Required("loca", checked((_glyphCount + 1) * (locationFormat == 0 ? 2 : 4)));
            _hmtx = Required("hmtx", checked(_metricCount * 4 + (_glyphCount - _metricCount) * 2));
            Required("name", 6);
            Required("post", 32);
            _locations = new int[_glyphCount + 1];
            for (var index = 0; index <= _glyphCount; index++)
            {
                var location = locationFormat == 0 ? U16(loca.Offset + index * 2) * 2L : U32(loca.Offset + index * 4);
                if (location > _glyf.Length || index > 0 && location < _locations[index - 1])
                    throw Invalid("Invalid or unordered TrueType glyph locations.");
                _locations[index] = (int)location;
            }
            _states = new byte[_glyphCount];
            ReadMaps();
        }

        internal PdfTrueTypeSubsetResult Build()
        {
            ReadCharacters();
            // NoSubsetting is a binding embedding restriction, even when every other
            // table is supported. The caller receives an owned unmodified full font.
            if ((_permissions & 0x0100) != 0)
                return new((byte[])_font.Clone(), false, _glyphCount, _glyphCount, "NoSubsetting");

            foreach (var tag in new[] { "fvar", "gvar", "cvar", "HVAR", "VVAR", "MVAR", "COLR", "CPAL",
                         "CBDT", "CBLC", "EBDT", "EBLC", "EBSC", "sbix", "SVG ", "Silf", "Glat", "Gloc" })
                if (_tables.ContainsKey(Tag(tag)))
                    throw new NotSupportedException($"Automatic subsetting does not support the {tag} table; supply a supported static outline font or explicitly embed the full font.");

            Visit(0, 0);
            foreach (var glyph in _characters.Values) Visit(glyph, 0);
            var originals = _glyphs.Keys.Order().ToArray();
            var remapping = new Dictionary<int, ushort>(originals.Length);
            for (var index = 0; index < originals.Length; index++) remapping.Add(originals[index], checked((ushort)index));

            var output = new SortedDictionary<uint, byte[]>();
            // The remaining source tables either carry global information or bytecode,
            // never a glyph-indexed array. Layout, bitmap, signature and unknown tables
            // are intentionally omitted under the unshaped horizontal-only contract.
            foreach (var tag in new[] { "head", "hhea", "maxp", "OS/2", "cvt ", "fpgm", "prep", "gasp" })
                if (_tables.TryGetValue(Tag(tag), out var table)) output.Add(Tag(tag), Slice(table));
            var head = output[Tag("head")];
            Put32(head, 8, 0);
            Put16(head, 50, 1); // Always use the unambiguous long loca format.

            using var glyf = new MemoryStream();
            var locations = new byte[(originals.Length + 1) * 4];
            var metrics = new byte[originals.Length * 4];
            var xMin = short.MaxValue;
            var yMin = short.MaxValue;
            var xMax = short.MinValue;
            var yMax = short.MinValue;
            var advanceMax = 0;
            var lsbMin = int.MaxValue;
            var rsbMin = int.MaxValue;
            var extentMax = int.MinValue;
            long totalAdvance = 0;
            var maxPoints = 0;
            var maxContours = 0;
            var maxCompositePoints = 0;
            var maxCompositeContours = 0;
            var maxComponentElements = 0;
            var maxComponentDepth = 0;
            var maxInstructions = 0;
            for (var index = 0; index < originals.Length; index++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var original = originals[index];
                var glyph = _glyphs[original];
                Put32(locations, index * 4, checked((uint)glyf.Length));
                var data = _font.AsSpan(_glyf.Offset + _locations[original], _locations[original + 1] - _locations[original]).ToArray();
                foreach (var component in glyph.Components)
                    Put16(data, component.Position, remapping[component.GlyphId]);
                glyf.Write(data);
                while ((glyf.Length & 3) != 0) glyf.WriteByte(0);
                var (advance, bearing) = Metric(original);
                Put16(metrics, index * 4, advance);
                Put16(metrics, index * 4 + 2, unchecked((ushort)bearing));
                advanceMax = Math.Max(advanceMax, advance);
                lsbMin = Math.Min(lsbMin, bearing);
                var width = data.Length == 0 ? 0 : I16(data, 6) - I16(data, 2);
                rsbMin = Math.Min(rsbMin, advance - bearing - width);
                extentMax = Math.Max(extentMax, bearing + width);
                totalAdvance += advance;
                if (data.Length != 0)
                {
                    xMin = Math.Min(xMin, I16(data, 2)); yMin = Math.Min(yMin, I16(data, 4));
                    xMax = Math.Max(xMax, I16(data, 6)); yMax = Math.Max(yMax, I16(data, 8));
                }
                if (glyph.Components.Count == 0)
                {
                    maxPoints = Math.Max(maxPoints, glyph.Points);
                    maxContours = Math.Max(maxContours, glyph.Contours);
                }
                else
                {
                    maxCompositePoints = Math.Max(maxCompositePoints, glyph.Points);
                    maxCompositeContours = Math.Max(maxCompositeContours, glyph.Contours);
                    maxComponentElements = Math.Max(maxComponentElements, glyph.Components.Count);
                    maxComponentDepth = Math.Max(maxComponentDepth, glyph.Depth);
                }
                maxInstructions = Math.Max(maxInstructions, glyph.Instructions);
            }
            Put32(locations, originals.Length * 4, checked((uint)glyf.Length));
            if (xMin == short.MaxValue) xMin = yMin = xMax = yMax = 0;
            Put16(head, 36, unchecked((ushort)xMin)); Put16(head, 38, unchecked((ushort)yMin));
            Put16(head, 40, unchecked((ushort)xMax)); Put16(head, 42, unchecked((ushort)yMax));
            var hhea = output[Tag("hhea")];
            Put16(hhea, 10, checked((ushort)advanceMax));
            Put16(hhea, 12, SignedMetric(lsbMin)); Put16(hhea, 14, SignedMetric(rsbMin));
            Put16(hhea, 16, SignedMetric(extentMax)); Put16(hhea, 34, checked((ushort)originals.Length));
            var maxp = output[Tag("maxp")];
            Put16(maxp, 4, checked((ushort)originals.Length));
            Put16(maxp, 6, checked((ushort)maxPoints)); Put16(maxp, 8, checked((ushort)maxContours));
            Put16(maxp, 10, checked((ushort)maxCompositePoints)); Put16(maxp, 12, checked((ushort)maxCompositeContours));
            Put16(maxp, 26, checked((ushort)maxInstructions)); Put16(maxp, 28, checked((ushort)maxComponentElements));
            Put16(maxp, 30, checked((ushort)maxComponentDepth));
            var os2 = output[Tag("OS/2")];
            Put16(os2, 2, SignedMetric((int)(totalAdvance / originals.Length)));
            if (os2.Length >= 68)
            {
                Put16(os2, 64, (ushort)Math.Min(0xFFFF, _characters.Keys.Min()));
                Put16(os2, 66, (ushort)Math.Min(0xFFFF, _characters.Keys.Max()));
            }
            output.Add(Tag("glyf"), glyf.ToArray());
            output.Add(Tag("loca"), locations);
            output.Add(Tag("hmtx"), metrics);
            output.Add(Tag("cmap"), BuildCmap(remapping));
            output.Add(Tag("name"), BuildName(SubsetPrefix(originals)));
            var post = Slice(_tables[Tag("post")]).AsSpan(0, 32).ToArray();
            Put32(post, 0, 0x00030000); // No stale glyph-name indices.
            output.Add(Tag("post"), post);
            var bytes = Assemble(output);
            _ = new PdfTrueTypeFont(bytes);
            _cancellationToken.ThrowIfCancellationRequested();
            return new(bytes, true, _glyphCount, originals.Length, "AutomaticSubset");
        }

        private void ReadCharacters()
        {
            if (_text.Length is 0 or > 4096) throw new ArgumentException("Expected 1 to 4096 UTF-16 code units.", "text");
            var remaining = _text.AsSpan();
            while (!remaining.IsEmpty)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (Rune.DecodeFromUtf16(remaining, out var rune, out var consumed) != OperationStatus.Done)
                    throw new ArgumentException("Annotation text contains invalid UTF-16.", "text");
                remaining = remaining[consumed..];
                if (_characters.ContainsKey(rune.Value)) continue;
                var glyph = 0;
                foreach (var map in _maps)
                {
                    var candidate = Lookup(map, rune.Value);
                    if (candidate == 0) continue;
                    if (candidate >= _glyphCount) throw Invalid("Unicode cmap references an invalid glyph.");
                    if (glyph != 0 && candidate != glyph)
                        throw new NotSupportedException($"Unicode cmap records disagree for U+{rune.Value:X4}; automatic subsetting refuses ambiguous font selection.");
                    glyph = candidate;
                }
                if (glyph == 0) throw Invalid($"The supplied font has no glyph for U+{rune.Value:X4}.");
                _characters.Add(rune.Value, glyph);
            }
        }

        private void ReadMaps()
        {
            var cmap = Required("cmap", 4);
            var count = U16(cmap.Offset + 2);
            for (var index = 0; index < count; index++)
            {
                var entry = cmap.Offset + 4 + index * 8;
                var platform = U16(entry);
                var encoding = U16(entry + 2);
                if (platform != 0 && !(platform == 3 && encoding is 1 or 10)) continue;
                var relative = checked((int)U32(entry + 4));
                var offset = checked(cmap.Offset + relative);
                var format = U16(offset);
                if (format is 4 or 12)
                    _maps.Add(new(offset, format, format == 4 ? U16(offset + 2) : checked((int)U32(offset + 4))));
            }
        }

        private int Lookup(Map map, int code)
        {
            if (map.Format == 12)
            {
                var low = 0;
                var high = checked((int)U32(map.Offset + 12)) - 1;
                while (low <= high)
                {
                    var middle = low + (high - low) / 2;
                    var entry = map.Offset + 16 + middle * 12;
                    var start = U32(entry);
                    var end = U32(entry + 4);
                    if (code < start) high = middle - 1;
                    else if (code > end) low = middle + 1;
                    else return checked((int)(U32(entry + 8) + code - start));
                }
                return 0;
            }
            if (code > 0xFFFF) return 0;
            var count = U16(map.Offset + 6) / 2;
            for (var index = 0; index < count; index++)
            {
                var end = U16(map.Offset + 14 + index * 2);
                if (code > end) continue;
                var start = U16(map.Offset + 16 + count * 2 + index * 2);
                if (code < start) return 0;
                var delta = U16(map.Offset + 16 + count * 4 + index * 2);
                var position = map.Offset + 16 + count * 6 + index * 2;
                var range = U16(position);
                if (range == 0) return (code + delta) & 0xFFFF;
                var glyph = U16(position + range + (code - start) * 2);
                return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
            }
            return 0;
        }

        private Glyph Visit(int glyphId, int depth)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (depth > 64) throw Invalid("Composite glyph nesting exceeds the supported depth of 64.");
            if (_states[glyphId] == 1) throw Invalid("A composite glyph contains a cycle.");
            if (_states[glyphId] == 2)
            {
                var cached = _glyphs[glyphId];
                if (depth + cached.Depth > 64) throw Invalid("Composite glyph nesting exceeds the supported depth of 64.");
                return cached;
            }
            _states[glyphId] = 1;
            var data = _font.AsSpan(_glyf.Offset + _locations[glyphId], _locations[glyphId + 1] - _locations[glyphId]);
            var result = new Glyph();
            _glyphs.Add(glyphId, result);
            if (!data.IsEmpty)
            {
                Need(data, 0, 10);
                var contours = I16(data, 0);
                if (contours >= 0) ReadSimple(data, contours, result);
                else ReadComposite(data, result, depth);
            }
            _states[glyphId] = 2;
            return result;
        }

        private static void ReadSimple(ReadOnlySpan<byte> data, int contours, Glyph glyph)
        {
            var position = 10;
            Need(data, position, contours * 2);
            var previous = -1;
            for (var index = 0; index < contours; index++)
            {
                var endpoint = U16(data, position + index * 2);
                if (endpoint <= previous) throw Invalid("Unordered simple-glyph contour endpoints.");
                previous = endpoint;
            }
            glyph.Points = previous + 1;
            glyph.Contours = contours;
            position += contours * 2;
            if (contours == 0 && position == data.Length) return;
            Need(data, position, 2);
            glyph.Instructions = U16(data, position);
            position += 2;
            Need(data, position, glyph.Instructions);
            position += glyph.Instructions;
            var points = 0;
            var coordinates = 0;
            while (points < glyph.Points)
            {
                Need(data, position, 1);
                var flags = data[position++];
                if ((flags & 0x80) != 0) throw Invalid("A simple glyph uses a reserved flag.");
                var repeat = 1;
                if ((flags & 8) != 0)
                {
                    Need(data, position, 1);
                    repeat += data[position++];
                }
                if (repeat > glyph.Points - points) throw Invalid("A glyph flag repeat exceeds its point count.");
                coordinates = checked(coordinates + repeat * (((flags & 2) != 0 ? 1 : (flags & 16) != 0 ? 0 : 2) +
                    ((flags & 4) != 0 ? 1 : (flags & 32) != 0 ? 0 : 2)));
                points += repeat;
            }
            Need(data, position, coordinates);
        }

        private void ReadComposite(ReadOnlySpan<byte> data, Glyph glyph, int depth)
        {
            var position = 10;
            ushort flags;
            var hasInstructions = false;
            do
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (glyph.Components.Count >= 4096) throw Invalid("A composite glyph has too many components.");
                if (++_componentCount > 1_000_000) throw Invalid("The glyph closure exceeds the composite-component processing budget.");
                Need(data, position, 4);
                flags = U16(data, position);
                var componentId = U16(data, position + 2);
                if ((flags & 0xE010) != 0 || (flags & 0x1800) == 0x1800 || componentId >= _glyphCount)
                    throw Invalid("Invalid composite glyph flags or component ID.");
                var transforms = ((flags & 8) != 0 ? 1 : 0) + ((flags & 0x40) != 0 ? 1 : 0) + ((flags & 0x80) != 0 ? 1 : 0);
                if (transforms > 1) throw Invalid("Conflicting composite glyph transformations.");
                glyph.Components.Add(new(position + 2, componentId));
                position += 4 + ((flags & 1) != 0 ? 4 : 2) + ((flags & 8) != 0 ? 2 : (flags & 0x40) != 0 ? 4 : (flags & 0x80) != 0 ? 8 : 0);
                Need(data, 0, position);
                var component = Visit(componentId, depth + 1);
                glyph.Points = checked(glyph.Points + component.Points);
                glyph.Contours = checked(glyph.Contours + component.Contours);
                if (glyph.Points > ushort.MaxValue || glyph.Contours > ushort.MaxValue)
                    throw Invalid("A composite glyph exceeds TrueType point or contour limits.");
                glyph.Depth = Math.Max(glyph.Depth, component.Depth + 1);
                hasInstructions |= (flags & 0x100) != 0;
            } while ((flags & 0x20) != 0);
            if (hasInstructions)
            {
                Need(data, position, 2);
                glyph.Instructions = U16(data, position);
                Need(data, position + 2, glyph.Instructions);
            }
        }

        private byte[] BuildCmap(Dictionary<int, ushort> remapping)
        {
            var mappings = _characters.Select(pair => (Code: pair.Key, Glyph: (int)remapping[pair.Value])).ToArray();
            var bmp = mappings.Where(pair => pair.Code < 0xFFFF).ToArray();
            var segments = bmp.Length + 1;
            var format4 = new byte[16 + segments * 8];
            Put16(format4, 0, 4); Put16(format4, 2, checked((ushort)format4.Length));
            Put16(format4, 6, checked((ushort)(segments * 2)));
            var power = 1;
            var logarithm = 0;
            while (power * 2 <= segments) { power *= 2; logarithm++; }
            Put16(format4, 8, checked((ushort)(power * 2))); Put16(format4, 10, checked((ushort)logarithm));
            Put16(format4, 12, checked((ushort)(segments * 2 - power * 2)));
            for (var index = 0; index < segments; index++)
            {
                var code = index < bmp.Length ? bmp[index].Code : 0xFFFF;
                var glyph = index < bmp.Length ? bmp[index].Glyph : 0;
                Put16(format4, 14 + index * 2, checked((ushort)code));
                Put16(format4, 16 + segments * 2 + index * 2, checked((ushort)code));
                Put16(format4, 16 + segments * 4 + index * 2, unchecked((ushort)(glyph - code)));
            }
            var groups = new List<(int Start, int End, int Glyph)>();
            foreach (var pair in mappings)
            {
                if (groups.Count > 0)
                {
                    var last = groups[^1];
                    if (pair.Code == last.End + 1 && pair.Glyph == last.Glyph + pair.Code - last.Start)
                    {
                        groups[^1] = (last.Start, pair.Code, last.Glyph);
                        continue;
                    }
                }
                groups.Add((pair.Code, pair.Code, pair.Glyph));
            }
            var format12 = new byte[16 + groups.Count * 12];
            Put16(format12, 0, 12); Put32(format12, 4, checked((uint)format12.Length));
            Put32(format12, 12, checked((uint)groups.Count));
            for (var index = 0; index < groups.Count; index++)
            {
                Put32(format12, 16 + index * 12, checked((uint)groups[index].Start));
                Put32(format12, 20 + index * 12, checked((uint)groups[index].End));
                Put32(format12, 24 + index * 12, checked((uint)groups[index].Glyph));
            }
            var output = new byte[28 + format4.Length + format12.Length];
            Put16(output, 2, 3);
            Put16(output, 4, 0); Put16(output, 6, 4); Put32(output, 8, checked((uint)(28 + format4.Length)));
            Put16(output, 12, 3); Put16(output, 14, 1); Put32(output, 16, 28);
            Put16(output, 20, 3); Put16(output, 22, 10); Put32(output, 24, checked((uint)(28 + format4.Length)));
            format4.CopyTo(output, 28); format12.CopyTo(output, 28 + format4.Length);
            return output;
        }

        private string SubsetPrefix(int[] originals)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(_font);
            Span<byte> id = stackalloc byte[2];
            foreach (var glyph in originals)
            {
                BinaryPrimitives.WriteUInt16BigEndian(id, checked((ushort)glyph));
                hash.AppendData(id);
            }
            Span<byte> mapping = stackalloc byte[6];
            foreach (var (unicode, glyph) in _characters)
            {
                BinaryPrimitives.WriteUInt32BigEndian(mapping, checked((uint)unicode));
                BinaryPrimitives.WriteUInt16BigEndian(mapping[4..], checked((ushort)glyph));
                hash.AppendData(mapping);
            }
            var digest = hash.GetHashAndReset();
            return string.Create(7, digest, static (span, bytes) =>
            {
                for (var index = 0; index < 6; index++) span[index] = (char)('A' + bytes[index] % 26);
                span[6] = '+';
            });
        }

        private byte[] BuildName(string prefix)
        {
            var table = _tables[Tag("name")];
            var source = _font.AsSpan(table.Offset, table.Length);
            var format = U16(source, 0);
            var count = U16(source, 2);
            var storageOffset = U16(source, 4);
            if (format > 1) throw Invalid("Unsupported TrueType name-table format.");
            var directoryLength = 6 + count * 12;
            Need(source, 0, directoryLength);
            var languages = 0;
            if (format == 1)
            {
                Need(source, directoryLength, 2);
                languages = U16(source, directoryLength);
                directoryLength = checked(directoryLength + 2 + languages * 4);
            }
            Need(source, 0, directoryLength);
            if (storageOffset < directoryLength || storageOffset > source.Length || directoryLength > ushort.MaxValue)
                throw Invalid("Invalid TrueType name string-storage offset.");
            var header = source[..directoryLength].ToArray();
            Put16(header, 4, checked((ushort)directoryLength));
            using var storage = new MemoryStream();
            var narrowPrefix = Encoding.ASCII.GetBytes(prefix);
            var widePrefix = Encoding.BigEndianUnicode.GetBytes(prefix);
            for (var index = 0; index < count; index++)
            {
                var entry = 6 + index * 12;
                var platform = U16(source, entry);
                var nameId = U16(source, entry + 6);
                var length = U16(source, entry + 8);
                var offset = U16(source, entry + 10);
                Need(source, storageOffset + offset, length);
                var name = source.Slice(storageOffset + offset, length);
                var append = nameId is 1 or 3 or 4 or 6 or 16 or 18 or 21 or 25;
                var extra = append ? platform is 0 or 3 ? widePrefix : platform == 1 ? narrowPrefix : [] : [];
                if (extra.Length != 0)
                {
                    var unit = platform is 0 or 3 ? 2 : 1;
                    if (unit == 2 && (name.Length & 1) != 0) throw Invalid("Invalid Unicode font-name string.");
                    var hasPrefix = name.Length >= 7 * unit;
                    for (var character = 0; hasPrefix && character < 7; character++)
                    {
                        var value = unit == 2 ? U16(name, character * 2) : name[character];
                        hasPrefix = character == 6 ? value == '+' : value is >= 'A' and <= 'Z';
                    }
                    if (hasPrefix) name = name[(7 * unit)..];
                    if (nameId == 6)
                    {
                        // PostScript names are at most 63 printable ASCII characters.
                        // The deterministic prefix reserves seven of those characters.
                        if (name.IsEmpty) throw Invalid("An empty PostScript font name cannot be subset.");
                        for (var character = 0; character < name.Length / unit; character++)
                        {
                            var value = unit == 2 ? U16(name, character * 2) : name[character];
                            if (value is < 33 or > 126 or '[' or ']' or '(' or ')' or '{' or '}' or '<' or '>' or '/' or '%')
                                throw Invalid("A PostScript font name contains unsupported characters.");
                        }
                        name = name[..Math.Min(name.Length, 56 * unit)];
                    }
                }
                length = checked((ushort)name.Length);
                if (storage.Length > ushort.MaxValue || length + extra.Length > ushort.MaxValue)
                    throw Invalid("Subset name strings exceed TrueType name-table limits.");
                Put16(header, entry + 8, checked((ushort)(length + extra.Length)));
                Put16(header, entry + 10, checked((ushort)storage.Length));
                storage.Write(extra); storage.Write(name);
            }
            for (var index = 0; index < languages; index++)
            {
                var entry = 6 + count * 12 + 2 + index * 4;
                var length = U16(source, entry);
                var offset = U16(source, entry + 2);
                Need(source, storageOffset + offset, length);
                if (storage.Length > ushort.MaxValue) throw Invalid("Subset language strings exceed TrueType name-table limits.");
                Put16(header, entry + 2, checked((ushort)storage.Length));
                storage.Write(source.Slice(storageOffset + offset, length));
            }
            var output = new byte[checked(header.Length + (int)storage.Length)];
            header.CopyTo(output, 0); storage.ToArray().CopyTo(output, header.Length);
            return output;
        }

        private byte[] Assemble(SortedDictionary<uint, byte[]> tables)
        {
            var directoryLength = 12 + tables.Count * 16;
            var length = directoryLength;
            foreach (var data in tables.Values) length = checked(length + ((data.Length + 3) & ~3));
            if (length > 32 * 1024 * 1024) throw Invalid("Subset font exceeds 32 MiB.");
            var output = new byte[length];
            Put32(output, 0, 0x00010000); Put16(output, 4, checked((ushort)tables.Count));
            var power = 1;
            var logarithm = 0;
            while (power * 2 <= tables.Count) { power *= 2; logarithm++; }
            Put16(output, 6, checked((ushort)(power * 16))); Put16(output, 8, checked((ushort)logarithm));
            Put16(output, 10, checked((ushort)(tables.Count * 16 - power * 16)));
            var index = 0;
            var offset = directoryLength;
            var headOffset = 0;
            foreach (var (tag, data) in tables)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var entry = 12 + index++ * 16;
                Put32(output, entry, tag); Put32(output, entry + 4, Checksum(data));
                Put32(output, entry + 8, checked((uint)offset)); Put32(output, entry + 12, checked((uint)data.Length));
                data.CopyTo(output, offset);
                if (tag == Tag("head")) headOffset = offset;
                offset += (data.Length + 3) & ~3;
            }
            Put32(output, headOffset + 8, unchecked(0xB1B0AFBAu - Checksum(output)));
            return output;
        }

        private (ushort Advance, short Bearing) Metric(int glyph)
        {
            var advance = U16(_hmtx.Offset + Math.Min(glyph, _metricCount - 1) * 4);
            var bearingPosition = glyph < _metricCount ? _hmtx.Offset + glyph * 4 + 2 : _hmtx.Offset + _metricCount * 4 + (glyph - _metricCount) * 2;
            return (advance, I16(bearingPosition));
        }

        private Table Required(string tag, int minimum)
        {
            if (!_tables.TryGetValue(Tag(tag), out var table) || table.Length < minimum)
                throw Invalid($"Missing or truncated {tag} TrueType table.");
            return table;
        }

        private byte[] Slice(Table table) => _font.AsSpan(table.Offset, table.Length).ToArray();
        private ushort U16(int offset) => PdfTrueTypeSubsetter.U16(_font, offset);
        private short I16(int offset) => PdfTrueTypeSubsetter.I16(_font, offset);
        private static ushort U16(ReadOnlySpan<byte> bytes, int offset) => PdfTrueTypeSubsetter.U16(bytes, offset);
        private static short I16(ReadOnlySpan<byte> bytes, int offset) => PdfTrueTypeSubsetter.I16(bytes, offset);
        private uint U32(int offset)
        {
            Need(_font, offset, 4);
            return BinaryPrimitives.ReadUInt32BigEndian(_font.AsSpan(offset, 4));
        }
    }

    private sealed class Glyph
    {
        internal readonly List<Component> Components = [];
        internal int Points;
        internal int Contours;
        internal int Depth;
        internal int Instructions;
    }

    private readonly record struct Component(int Position, int GlyphId);
    private readonly record struct Table(int Offset, int Length);
    private readonly record struct Map(int Offset, int Format, int Length);

    private static ushort SignedMetric(int metric)
    {
        if (metric is < short.MinValue or > short.MaxValue) throw Invalid("A subset metric exceeds the signed TrueType metric range.");
        return unchecked((ushort)(short)metric);
    }

    private static uint Checksum(ReadOnlySpan<byte> bytes)
    {
        uint sum = 0;
        var position = 0;
        for (; position + 4 <= bytes.Length; position += 4)
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(position, 4)));
        uint tail = 0;
        for (var index = 0; position + index < bytes.Length; index++) tail |= (uint)bytes[position + index] << (24 - index * 8);
        return unchecked(sum + tail);
    }

    private static void Need(ReadOnlySpan<byte> data, int offset, int length)
    {
        if (offset < 0 || length < 0 || (long)offset + length > data.Length)
            throw Invalid("Truncated TrueType glyph or table data.");
    }

    private static ushort U16(ReadOnlySpan<byte> bytes, int offset)
    {
        Need(bytes, offset, 2);
        return BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
    }

    private static short I16(ReadOnlySpan<byte> bytes, int offset) => unchecked((short)U16(bytes, offset));
    private static void Put16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset, 2), value);
    private static void Put32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset, 4), value);
    private static uint Tag(string text) => (uint)text[0] << 24 | (uint)text[1] << 16 | (uint)text[2] << 8 | text[3];
    private static ArgumentException Invalid(string message) => new(message, "trueTypeFont");
}
