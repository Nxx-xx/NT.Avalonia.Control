using System.Text;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using SharpDocument = PdfSharp.Pdf.PdfDocument;

namespace NT.AvaloniaControl.PdfViewer.Editing;

/// <summary>
/// A deliberately bounded, destructive page-only rewrite. It keeps painting data, not arbitrary
/// PDF dictionaries. Unsupported painting constructs cause the whole candidate to be rejected.
/// Embedded font programs and retained pixel samples are painting data, not a steganography scan.
/// </summary>
internal static class PdfHiddenInformationSanitizer
{
    private const int MaximumDecodedStream = 32 * 1024 * 1024;
    private const int MaximumObjects = 100_000;
    private const int MaximumDepth = 64;
    private static readonly HashSet<string> PageKeys = Set("/Type /Parent /Contents /Resources /MediaBox /CropBox /BleedBox /TrimBox /ArtBox /Rotate /UserUnit /Group");
    private static readonly HashSet<string> StreamKeys = Set("/Length /Filter /DecodeParms");
    private static readonly HashSet<string> BasicOperators = Set("q Q cm w J j M d ri i m l c v y h re S s f F f* B B* b b* n W W* BT ET Tc Tw Tz TL Tr Ts Td TD Tm T* Tj TJ ' \" G g RG rg K k SC sc SCN scn BX EX");
    private static readonly HashSet<string> FontKeys = Set("/Type /Subtype /BaseFont /FirstChar /LastChar /Widths /Encoding /FontDescriptor /ToUnicode /DescendantFonts /CIDSystemInfo /DW /W /DW2 /W2 /CIDToGIDMap");
    private static readonly HashSet<string> DescriptorKeys = Set("/Type /FontName /Flags /FontBBox /ItalicAngle /Ascent /Descent /Leading /CapHeight /XHeight /StemV /StemH /AvgWidth /MaxWidth /MissingWidth /FontFile /FontFile2 /FontFile3");
    private static readonly HashSet<string> ImageKeys = Set("/Type /Subtype /Width /Height /ColorSpace /BitsPerComponent /Intent /ImageMask /Mask /Decode /Interpolate /SMask /Matte /SMaskInData /Length /Filter /DecodeParms");
    private static readonly HashSet<string> FormKeys = Set("/Type /Subtype /FormType /BBox /Matrix /Resources /Group /Length /Filter /DecodeParms");
    private static readonly HashSet<string> StateKeys = Set("/Type /LW /LC /LJ /ML /D /RI /OP /op /OPM /Font /BG /BG2 /UCR /UCR2 /TR /TR2 /HT /FL /SM /SA /BM /SMask /CA /ca /AIS /TK");

    internal static byte[] Rewrite(byte[] source, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length is 0 or > PdfStructureWriter.MaximumDocumentBytes)
            throw new ArgumentException("The PDF must contain 1 byte to 256 MiB.", nameof(source));
        token.ThrowIfCancellationRequested();
        using var input = new MemoryStream(source, writable: false);
        using var original = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        using var output = new SharpDocument();
        var context = new Cleaner(original, audit: false, token);
        for (var index = 0; index < original.PageCount; index++)
        {
            token.ThrowIfCancellationRequested();
            var page = original.Pages[index];
            context.Page(page);
            var imported = output.AddPage(page);
            // PDFsharp's page importer copies its known page fields. Assert the same strict boundary
            // on the imported page too, independently of that implementation detail.
            KeepOnly(imported, PageKeys, audit: false);
        }
        if (output.PageCount == 0) throw new InvalidDataException("A sanitized PDF must contain at least one page.");
        foreach (var key in output.Info.Elements.KeyNames.ToArray()) output.Info.Elements.Remove(key.Value);
        output.Info.Creator = "";
        output.Info.CreationDate = DateTime.UtcNow;
        output.Info.ModificationDate = DateTime.UtcNow;
        token.ThrowIfCancellationRequested();
        using var bytes = new MemoryStream();
        output.Save(bytes, closeStream: false);
        if (bytes.Length > PdfStructureWriter.MaximumDocumentBytes)
            throw new InvalidDataException("The sanitized PDF exceeds the 256 MiB limit.");
        var result = bytes.ToArray();
        Audit(result, token);
        return result;
    }

    /// <summary>Reopens a candidate and checks the page-only graph and used-resource boundary.</summary>
    internal static void Audit(byte[] candidate, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var stream = new MemoryStream(candidate, writable: false);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        KeepOnly(document.Internals.Catalog, Set("/Type /Pages /Version /Metadata"), audit: true);
        // PDFsharp adds a fresh producer and dates. No source Info dictionary is imported.
        KeepOnly(document.Info, Set("/Creator /Producer /CreationDate /ModDate"), audit: true);
        if (document.Info.Creator is not ("" or "PDFsharp 6.2.4 (www.pdfsharp.com)") || document.Info.Producer != "PDFsharp 6.2.4")
            throw new InvalidDataException("The sanitized document has unexpected document information.");
        AuditGeneratedMetadata(document);
        var cleaner = new Cleaner(document, audit: true, token);
        for (var index = 0; index < document.PageCount; index++) cleaner.Page(document.Pages[index]);
        if (document.PageCount == 0) throw new InvalidDataException("The sanitized PDF contains no pages.");
    }

    private sealed class Cleaner(SharpDocument document, bool audit, CancellationToken token)
    {
        private readonly HashSet<PdfDictionary> _visited = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<PdfDictionary> _active = new(ReferenceEqualityComparer.Instance);
        private long _decodedBytes;
        private int _objects;

        internal void Page(PdfPage page)
        {
            token.ThrowIfCancellationRequested();
            if (!audit)
                foreach (var inherited in new[] { "/Resources", "/MediaBox", "/CropBox", "/Rotate" })
                    if (page.Elements[inherited] is null && Inherited(page, inherited) is { } value)
                        page.Elements[inherited] = value;
            var userUnit = page.Elements.GetReal("/UserUnit");
            if (page.Elements["/UserUnit"] is not null && userUnit != 1)
                throw Unsupported("Non-default page UserUnit is not supported by the sanitizer.");
            KeepOnly(page, PageKeys, audit);
            PlainFields(page, "/Contents /Resources /Group", "/Type", "/Rotate /UserUnit", "/MediaBox /CropBox /BleedBox /TrimBox /ArtBox", "/Parent");
            Group(page.Elements.GetDictionary("/Group"));
            var resources = page.Elements.GetDictionary("/Resources");
            var content = ReadContents(page.Elements["/Contents"]);
            if (audit) _ = Content(content, resources, depth: 0);
            else
            {
                var pruned = ResourceCopy(resources, content, depth: 0, out var rewritten);
                page.Elements["/Resources"] = pruned;
                var stream = new PdfDictionary(document);
                stream.CreateStream(rewritten);
                document.Internals.AddObject(stream);
                page.Elements["/Contents"] = stream.Reference!;
            }
        }

        private byte[] ReadContents(PdfItem? item)
        {
            item = Dereference(item);
            if (item is null || item is PdfNull) return [];
            if (item is PdfDictionary dictionary && dictionary.Stream is not null)
            {
                if (audit) KeepOnly(dictionary, StreamKeys, audit: true);
                return Decode(dictionary);
            }
            if (item is not PdfArray array) throw Unsupported("Malformed page content.");
            using var combined = new MemoryStream();
            foreach (var element in array.Elements)
            {
                if (Dereference(element) is not PdfDictionary part || part.Stream is null)
                    throw Unsupported("Malformed content stream array.");
                if (audit) KeepOnly(part, StreamKeys, audit: true);
                var bytes = Decode(part);
                combined.Write(bytes);
                combined.WriteByte(10);
                if (combined.Length > MaximumDecodedStream) throw Unsupported("The combined page content exceeds 32 MiB.");
            }
            return combined.ToArray();
        }

        private byte[] Content(byte[] data, PdfDictionary? resources, int depth)
        {
            CheckDepth(depth);
            CSequence sequence;
            try { sequence = ContentReader.ReadContent(data); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { throw new NotSupportedException("The PDF content cannot be parsed by the bounded sanitizer.", ex); }
            var retained = new CSequence();
            var used = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var markedDepth = 0;
            foreach (var item in sequence)
            {
                token.ThrowIfCancellationRequested();
                if (item is CComment)
                {
                    if (audit) throw new InvalidDataException("A sanitized content stream contains comments.");
                    continue;
                }
                if (item is not COperator operation) throw Unsupported("Unrecognized content token.");
                PdfSanitizationContentValidation.Validate(operation);
                switch (operation.Name)
                {
                    case "Tf": Use("/Font", Name(operation, 0, 2)); break;
                    case "Do": Use("/XObject", Name(operation, 0, 1)); break;
                    case "gs": Use("/ExtGState", Name(operation, 0, 1)); break;
                    case "CS": case "cs":
                        var color = Name(operation, 0, 1);
                        if (!IsDeviceColor(color)) Use("/ColorSpace", color);
                        break;
                    case "SCN": case "scn":
                        if (operation.Operands.Any(operand => operand is CName)) throw Unsupported("Pattern painting is not supported by the sanitizer.");
                        break;
                    case "BMC":
                        if (Name(operation, 0, 1) == "/OC") throw Unsupported("Optional content is not supported by the sanitizer.");
                        if (audit) throw new InvalidDataException("Marked-content metadata remains in the candidate.");
                        markedDepth++;
                        continue;
                    case "EMC":
                        if (operation.Operands.Count != 0 || --markedDepth < 0) throw Unsupported("Unbalanced marked content.");
                        if (audit) throw new InvalidDataException("Marked-content metadata remains in the candidate.");
                        continue;
                    case "MP":
                        _ = Name(operation, 0, 1);
                        if (audit) throw new InvalidDataException("Marked-content metadata remains in the candidate.");
                        continue;
                    case "BDC": case "DP":
                        throw Unsupported("Marked-content properties and optional content require a separate sanitizer implementation.");
                    default:
                        if (!BasicOperators.Contains(operation.Name)) throw Unsupported($"Unsupported painting operator '{operation.Name}'.");
                        break;
                }
                retained.Add(operation);
            }
            if (markedDepth != 0) throw Unsupported("Unbalanced marked content.");
            Resources(resources, used, depth + 1);
            return retained.ToContent();

            void Use(string kind, string name)
            {
                if (!used.TryGetValue(kind, out var names)) used.Add(kind, names = new(StringComparer.Ordinal));
                names.Add(name);
            }
        }

        private void Resources(PdfDictionary? resources, Dictionary<string, HashSet<string>> used, int depth)
        {
            if (resources is null)
            {
                if (used.Count != 0) throw Unsupported("Content refers to missing resources.");
                return;
            }
            CheckDepth(depth);
            if (resources.Elements.GetDictionary("/ColorSpace") is { } colorSpaces
                && new[] { "/DefaultGray", "/DefaultRGB", "/DefaultCMYK" }.Any(name => colorSpaces.Elements[name] is not null))
                throw Unsupported("Default device-color overrides are not supported by the sanitizer.");
            // Resource containers can be shared. Never mutate one in place until every caller's used
            // names is known: the first cleaner call only processes them; each page/Form gets its own
            // pruned container through ResourceCopy below.
            foreach (var kind in used)
            {
                var entries = resources.Elements.GetDictionary(kind.Key)
                    ?? throw Unsupported($"Missing resource category '{kind.Key}'.");
                foreach (var name in kind.Value)
                {
                    var value = Dereference(entries.Elements[name]) ?? throw Unsupported($"Undefined resource '{name}'.");
                    switch (kind.Key)
                    {
                        case "/Font": Font(RequireDictionary(value), depth + 1); break;
                        case "/XObject": XObject(RequireDictionary(value), depth + 1); break;
                        case "/ExtGState": State(RequireDictionary(value), depth + 1); break;
                        case "/ColorSpace": ColorSpace(value, depth + 1); break;
                        default: throw Unsupported("Unsupported resource category.");
                    }
                }
            }
            var allowed = new HashSet<string>(used.Keys, StringComparer.Ordinal);
            if (audit)
            {
                KeepOnly(resources, allowed, audit: true);
                foreach (var kind in used) KeepOnly(resources.Elements.GetDictionary(kind.Key)!, kind.Value, audit: true);
            }
        }

        private PdfDictionary ResourceCopy(PdfDictionary? resources, byte[] content, int depth, out byte[] rewritten)
        {
            // Work with private container dictionaries to preserve page-local resource selection.
            var copy = new PdfDictionary(document);
            if (resources is not null)
                foreach (var category in resources.Elements)
                {
                    if (Dereference(category.Value) is not PdfDictionary entries) continue;
                    var privateEntries = new PdfDictionary(document);
                    foreach (var entry in entries.Elements) privateEntries.Elements[entry.Key] = entry.Value;
                    copy.Elements[category.Key] = privateEntries;
                }
            rewritten = Content(content, copy, depth);
            // Derive retained resources from the canonical content after validation. This second
            // small parse has no side effects and avoids sharing caller-specific name sets.
            var used = UsedResources(rewritten);
            foreach (var key in copy.Elements.KeyNames.ToArray())
            {
                if (!used.TryGetValue(key.Value, out var names)) { copy.Elements.Remove(key.Value); continue; }
                KeepOnly(copy.Elements.GetDictionary(key.Value)!, names, audit: false);
            }
            return copy;
        }

        private void XObject(PdfDictionary value, int depth)
        {
            Enter(value, depth, out var first);
            if (!first) return;
            try
            {
                if (value.Stream is null) throw Unsupported("An XObject must be a stream.");
                switch (value.Elements.GetName("/Subtype"))
                {
                    case "/Image": Image(value, depth + 1); break;
                    case "/Form":
                        KeepOnly(value, FormKeys, audit);
                        PlainFields(value, "/Resources /Group", "/Type /Subtype", "/FormType /Length", "/BBox /Matrix", "/Filter /DecodeParms");
                        Group(value.Elements.GetDictionary("/Group"));
                        var content = Decode(value);
                        var resources = value.Elements.GetDictionary("/Resources");
                        if (resources is null && UsedResources(content).Count != 0)
                            throw Unsupported("A resource-using Form must declare its own Resources.");
                        if (audit) _ = Content(content, resources, depth + 1);
                        else
                        {
                            var pruned = ResourceCopy(resources, content, depth + 1, out var rewritten);
                            value.Elements["/Resources"] = pruned;
                            SetDecoded(value, rewritten);
                        }
                        break;
                    default: throw Unsupported("Only Image and Form XObjects are supported.");
                }
            }
            finally { _active.Remove(value); }
        }

        private void Image(PdfDictionary image, int depth)
        {
            KeepOnly(image, ImageKeys, audit);
            PlainFields(image, "/ColorSpace /Mask /SMask", "/Type /Subtype /Intent", "/Width /Height /BitsPerComponent /SMaskInData /Length", "/Decode /Matte", "/ImageMask /Interpolate /Filter /DecodeParms");
            foreach (var key in new[] { "/ImageMask", "/Interpolate" })
                if (image.Elements[key] is { } flag && Dereference(flag) is not PdfBoolean) throw Unsupported("Malformed image boolean.");
            if (image.Elements["/Mask"] is { } maskArray && Dereference(maskArray) is PdfArray array) NumericArray(array);
            var filters = Filters(image);
            if (filters.Any(name => name is not ("/FlateDecode" or "/Fl" or "/ASCIIHexDecode" or "/AHx" or "/ASCII85Decode" or "/A85" or "/RunLengthDecode" or "/RL" or "/LZWDecode" or "/LZW")))
                throw Unsupported("JPEG, JPEG2000, JBIG2 and other opaque image containers are not supported by the strict sanitizer.");
            var width = RequiredInteger(image, "/Width", 1, 32_768);
            var height = RequiredInteger(image, "/Height", 1, 32_768);
            var maskImage = Dereference(image.Elements["/ImageMask"]) is PdfBoolean { Value: true };
            var components = maskImage ? 1 : image.Elements["/ColorSpace"] is { } color
                ? ColorSpace(Dereference(color)!, depth + 1)
                : throw Unsupported("An ordinary image must declare its color space.");
            var bits = maskImage ? 1 : RequiredInteger(image, "/BitsPerComponent", 1, 16);
            if (bits is not (1 or 2 or 4 or 8 or 16)) throw Unsupported("Unsupported image sample bit depth.");
            if (maskImage && image.Elements["/BitsPerComponent"] is not null && RequiredInteger(image, "/BitsPerComponent", 1, 16) != 1)
                throw Unsupported("An image mask must use one-bit samples.");
            foreach (var key in new[] { "/Decode", "/Matte" })
                if (image.Elements.GetArray(key) is { } samples && (samples.Elements.Count != components * (key == "/Decode" ? 2 : 1)
                    || samples.Elements.Any(item => !IsNumber(Dereference(item)))))
                    throw Unsupported("Malformed image sample adjustment array.");
            if (image.Elements.GetArray("/Mask") is { } colorMask && (colorMask.Elements.Count != components * 2
                || colorMask.Elements.Any(item => !IsNumber(Dereference(item))))) throw Unsupported("Malformed image color-key mask.");
            if (image.Elements["/Intent"] is not null && image.Elements.GetName("/Intent") is not
                ("/AbsoluteColorimetric" or "/RelativeColorimetric" or "/Saturation" or "/Perceptual")) throw Unsupported("Unknown image rendering intent.");
            foreach (var key in new[] { "/SMask", "/Mask" })
                if (Dereference(image.Elements[key]) is PdfDictionary mask) XObject(mask, depth + 1);
            if (image.Elements.GetInteger("/SMaskInData") != 0) throw Unsupported("Embedded image soft masks require an unsupported image container.");
            var pixels = Decode(image);
            var rowBits = checked((long)width * components * bits);
            var rowBytes = checked((int)((rowBits + 7) / 8));
            var expectedLength = checked((long)rowBytes * height);
            if (expectedLength > MaximumDecodedStream || pixels.LongLength < expectedLength)
                throw Unsupported("The image sample data is shorter than its declared dimensions or exceeds the stream budget.");
            if (audit && pixels.LongLength != expectedLength) throw new InvalidDataException("Unused trailing image data remains in the sanitized PDF.");
            var canonical = pixels.AsSpan(0, (int)expectedLength).ToArray();
            var paddingBits = (int)((8 - rowBits % 8) % 8);
            if (paddingBits > 0)
                for (var row = 0; row < height; row++)
                {
                    var index = (row + 1) * rowBytes - 1;
                    var masked = (byte)(canonical[index] & (0xFF << paddingBits));
                    if (audit && canonical[index] != masked) throw new InvalidDataException("Unused image row padding contains nonzero bits.");
                    canonical[index] = masked;
                }
            if (!audit) SetDecoded(image, canonical);
        }

        private void Font(PdfDictionary font, int depth)
        {
            Enter(font, depth, out var first);
            if (!first) return;
            try
            {
                var type = font.Elements.GetName("/Subtype");
                if (type is not ("/Type1" or "/MMType1" or "/TrueType" or "/Type0" or "/CIDFontType0" or "/CIDFontType2"))
                    throw Unsupported("Type3 and unknown font types are not supported by the sanitizer.");
                KeepOnly(font, FontKeys, audit);
                PlainFields(font, "/Encoding /FontDescriptor /ToUnicode /DescendantFonts /CIDSystemInfo /CIDToGIDMap", "/Type /Subtype /BaseFont", "/FirstChar /LastChar /DW", "/Widths /W /DW2 /W2", "");
                if (font.Elements["/CIDToGIDMap"] is { } gid && Dereference(gid) is not (PdfName or PdfDictionary)) throw Unsupported("Malformed CID-to-glyph map.");
                if (font.Elements.GetDictionary("/FontDescriptor") is { } descriptor)
                {
                    KeepOnly(descriptor, DescriptorKeys, audit);
                    PlainFields(descriptor, "/FontFile /FontFile2 /FontFile3", "/Type /FontName", "/Flags /ItalicAngle /Ascent /Descent /Leading /CapHeight /XHeight /StemV /StemH /AvgWidth /MaxWidth /MissingWidth", "/FontBBox", "");
                    foreach (var key in new[] { "/FontFile", "/FontFile2", "/FontFile3" })
                        if (descriptor.Elements.GetDictionary(key) is { } program) OpaqueFontStream(program);
                }
                if (font.Elements.GetDictionary("/Encoding") is { } encoding)
                {
                    if (encoding.Stream is null)
                    {
                        KeepOnly(encoding, Set("/Type /BaseEncoding /Differences"), audit);
                        PlainFields(encoding, "/Differences", "/Type /BaseEncoding", "", "", "");
                        if (encoding.Elements.GetArray("/Differences") is { } differences)
                            foreach (var difference in differences.Elements)
                                if (Dereference(difference) is not (PdfInteger or PdfName)) throw Unsupported("Malformed font encoding differences.");
                    }
                    else CMap(encoding);
                }
                if (font.Elements.GetDictionary("/ToUnicode") is { } toUnicode) CMap(toUnicode);
                if (font.Elements.GetDictionary("/CIDToGIDMap") is { } map) OpaqueFontStream(map);
                if (font.Elements.GetDictionary("/CIDSystemInfo") is { } info)
                {
                    KeepOnly(info, Set("/Registry /Ordering /Supplement"), audit);
                    foreach (var key in new[] { "/Registry", "/Ordering" })
                        if (info.Elements[key] is { } text && Dereference(text) is not PdfString) throw Unsupported("Malformed CID system information.");
                    if (info.Elements["/Supplement"] is { } supplement && Dereference(supplement) is not PdfInteger) throw Unsupported("Malformed CID supplement.");
                }
                if (font.Elements.GetArray("/DescendantFonts") is { } descendants)
                    foreach (var child in descendants.Elements) Font(RequireDictionary(Dereference(child)), depth + 1);
            }
            finally { _active.Remove(font); }
        }

        private void OpaqueFontStream(PdfDictionary stream)
        {
            if (stream.Stream is null) throw Unsupported("An embedded font resource is not a stream.");
            KeepOnly(stream, Set("/Length /Length1 /Length2 /Length3 /Subtype /Filter /DecodeParms"), audit);
            PlainFields(stream, "", "/Subtype", "/Length /Length1 /Length2 /Length3", "", "/Filter /DecodeParms");
            if (!audit) SetDecoded(stream, Decode(stream));
        }

        private void CMap(PdfDictionary map)
        {
            if (map.Stream is null) throw Unsupported("A CMap resource is not a stream.");
            if (map.Elements["/UseCMap"] is not null) throw Unsupported("External or inherited CMaps are not supported by the sanitizer.");
            KeepOnly(map, StreamKeys, audit);
            // CMap programs contain names and mappings required for extraction. Comments are not
            // needed by the interpreter and may carry discarded annotation/content information.
            var bytes = Decode(map);
            var text = Encoding.Latin1.GetString(bytes);
            var clean = RemoveCMapComments(text);
            if (audit && !string.Equals(text, clean, StringComparison.Ordinal)) throw new InvalidDataException("CMap comments remain in the sanitized document.");
            if (!audit) SetDecoded(map, Encoding.Latin1.GetBytes(clean));
        }

        private void State(PdfDictionary state, int depth)
        {
            Enter(state, depth, out var first);
            if (!first) return;
            try
            {
                KeepOnly(state, StateKeys, audit);
                PlainFields(state, "/Font /BG /BG2 /UCR /UCR2 /TR /TR2 /HT /BM /SMask", "/Type /RI", "/LW /LC /LJ /ML /OPM /FL /SM /CA /ca", "/D", "/OP /op /SA /AIS /TK");
                foreach (var key in new[] { "/OP", "/op", "/SA", "/AIS", "/TK" })
                    if (state.Elements[key] is { } flag && Dereference(flag) is not PdfBoolean) throw Unsupported("Malformed graphics-state boolean.");
                if (state.Elements["/BM"] is { } blend)
                {
                    if (Dereference(blend) is PdfArray blends)
                    {
                        foreach (var mode in blends.Elements) if (Dereference(mode) is not PdfName) throw Unsupported("Malformed blend mode.");
                    }
                    else if (Dereference(blend) is not PdfName) throw Unsupported("Malformed blend mode.");
                }
                foreach (var key in new[] { "/BG", "/BG2", "/UCR", "/UCR2", "/TR", "/TR2", "/HT" })
                    if (state.Elements[key] is { } value && Dereference(value) is not PdfName { Value: "/Identity" or "/Default" })
                        throw Unsupported("Custom transfer functions and halftones are not supported.");
                if (state.Elements["/SMask"] is { } mask && Dereference(mask) is not PdfName { Value: "/None" })
                    throw Unsupported("Graphics-state soft masks are not supported by the sanitizer.");
                if (state.Elements.GetArray("/Font") is { } fonts)
                {
                    if (fonts.Elements.Count != 2) throw Unsupported("Malformed graphics-state font.");
                    if (!IsNumber(Dereference(fonts.Elements[1]))) throw Unsupported("Malformed graphics-state font size.");
                    Font(RequireDictionary(Dereference(fonts.Elements[0])), depth + 1);
                }
            }
            finally { _active.Remove(state); }
        }

        private int ColorSpace(PdfItem color, int depth)
        {
            CheckDepth(depth);
            color = Dereference(color)!;
            if (color is PdfName name && IsDeviceColor(name.Value)) return name.Value switch { "/DeviceRGB" => 3, "/DeviceCMYK" => 4, _ => 1 };
            if (color is PdfArray array && array.Elements.Count == 4
                && Dereference(array.Elements[0]) is PdfName { Value: "/Indexed" or "/I" })
            {
                if (Dereference(array.Elements[1]) is not PdfName baseName || !IsDeviceColor(baseName.Value))
                    throw Unsupported("An indexed color space must use a device base color space.");
                var channels = ColorSpace(baseName, depth + 1);
                if (Dereference(array.Elements[2]) is not PdfInteger { Value: >= 0 and <= 255 } high)
                    throw Unsupported("Malformed indexed color palette limit.");
                var expected = (high.Value + 1) * channels;
                if (Dereference(array.Elements[3]) is PdfDictionary palette)
                {
                    KeepOnly(palette, StreamKeys, audit);
                    var samples = Decode(palette);
                    if (samples.Length < expected) throw Unsupported("The indexed color palette is too short.");
                    if (audit && samples.Length != expected) throw new InvalidDataException("Unused palette stream data remains.");
                    if (!audit) SetDecoded(palette, samples.AsSpan(0, expected).ToArray());
                }
                else if (Dereference(array.Elements[3]) is PdfString paletteString)
                {
                    if (paletteString.Value.Any(character => character > 255) || paletteString.Value.Length < expected)
                        throw Unsupported("Malformed indexed color palette string.");
                    if (audit && paletteString.Value.Length != expected) throw new InvalidDataException("Unused palette string data remains.");
                    if (!audit) array.Elements[3] = new PdfString(paletteString.Value[..expected], PdfStringEncoding.RawEncoding);
                }
                else throw Unsupported("Malformed indexed color palette.");
                return 1;
            }
            throw Unsupported("Only device and indexed color spaces are supported by the strict sanitizer.");
        }

        private void Group(PdfDictionary? group)
        {
            if (group is null) return;
            KeepOnly(group, Set("/Type /S /CS /I /K"), audit);
            PlainFields(group, "/CS", "/Type /S", "", "", "/I /K");
            foreach (var key in new[] { "/I", "/K" })
                if (group.Elements[key] is { } flag && Dereference(flag) is not PdfBoolean) throw Unsupported("Malformed transparency group boolean.");
            if (group.Elements.GetName("/S") != "/Transparency") throw Unsupported("Unknown page or Form group.");
            if (group.Elements["/CS"] is { } color) ColorSpace(color, 1);
        }

        private byte[] Decode(PdfDictionary dictionary)
        {
            token.ThrowIfCancellationRequested();
            if (dictionary.Stream is null) throw Unsupported("Expected a stream.");
            if (audit && dictionary.Elements["/DecodeParms"] is not null)
                throw new InvalidDataException("A sanitized stream contains decoder parameter dictionaries.");
            if (!audit && Dereference(dictionary.Elements["/DecodeParms"]) is { } parameters and not PdfNull)
            {
                if (parameters is not PdfDictionary decoder || decoder.Elements["/Predictor"] is { } predictor
                    && (Dereference(predictor) is not PdfInteger number || number.Value != 1))
                    throw Unsupported("Stream predictor transforms and compound decoder parameters are not supported.");
                foreach (var key in new[] { "/Columns", "/Colors", "/BitsPerComponent", "/EarlyChange" })
                    if (decoder.Elements[key] is { } value && Dereference(value) is not PdfInteger)
                        throw Unsupported("Malformed stream decoder parameters.");
            }
            if (Filters(dictionary).Any(name => name is not ("/FlateDecode" or "/Fl" or "/ASCIIHexDecode" or "/AHx" or "/ASCII85Decode" or "/A85" or "/RunLengthDecode" or "/RL" or "/LZWDecode" or "/LZW")))
                throw Unsupported("The stream uses an unsupported filter.");
            var bytes = dictionary.Stream.UnfilteredValue;
            _decodedBytes += bytes.Length;
            if (bytes.Length > MaximumDecodedStream || _decodedBytes > PdfStructureWriter.MaximumDocumentBytes)
                throw Unsupported("The sanitizer decoded-stream memory budget was exceeded.");
            return bytes;
        }

        private void Enter(PdfDictionary value, int depth, out bool first)
        {
            CheckDepth(depth);
            if (_active.Contains(value)) throw Unsupported("Cyclic rendering resources are not supported.");
            first = _visited.Add(value);
            if (!first) return;
            if (++_objects > MaximumObjects) throw Unsupported("The sanitizer object budget was exceeded.");
            _active.Add(value);
        }

        private static void CheckDepth(int depth)
        {
            if (depth > MaximumDepth) throw Unsupported("The sanitizer resource depth limit was exceeded.");
        }
    }

    private static PdfItem? Inherited(PdfDictionary page, string key)
    {
        var seen = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        PdfDictionary? ancestor = page;
        while (ancestor is not null)
        {
            if (!seen.Add(ancestor) || seen.Count > MaximumDepth) throw Unsupported("Cyclic or excessively deep page inheritance.");
            if (ancestor.Elements[key] is { } value) return value;
            ancestor = ancestor.Elements.GetDictionary("/Parent");
        }
        return null;
    }

    private static void PlainFields(PdfDictionary dictionary, string special, string names, string numbers, string arrays, string exempt)
    {
        var nameKeys = Set(names);
        var numberKeys = Set(numbers);
        var arrayKeys = Set(arrays);
        var specialKeys = Set(special);
        var exemptKeys = Set(exempt);
        foreach (var entry in dictionary.Elements)
        {
            var value = Dereference(entry.Value);
            if (exemptKeys.Contains(entry.Key)) continue;
            if (nameKeys.Contains(entry.Key))
            {
                if (value is not PdfName) throw Unsupported($"Malformed name '{entry.Key}'.");
            }
            else if (numberKeys.Contains(entry.Key))
            {
                if (!IsNumber(value)) throw Unsupported($"Malformed number '{entry.Key}'.");
            }
            else if (arrayKeys.Contains(entry.Key))
            {
                if (value is PdfArray array) NumericArray(array);
                else if (value is PdfRectangle rectangle && new[] { rectangle.X1, rectangle.Y1, rectangle.X2, rectangle.Y2 }.All(double.IsFinite)) { }
                else throw Unsupported($"Malformed numeric array '{entry.Key}'.");
            }
            else if (specialKeys.Contains(entry.Key))
            {
                var valid = entry.Key switch
                {
                    "/Contents" => value is PdfDictionary or PdfArray or PdfNull,
                    "/Resources" or "/Group" or "/FontDescriptor" or "/ToUnicode" or "/CIDSystemInfo" or "/FontFile" or "/FontFile2" or "/FontFile3" => value is PdfDictionary,
                    "/Encoding" or "/CIDToGIDMap" or "/SMask" => value is PdfName or PdfDictionary,
                    "/DescendantFonts" or "/Differences" or "/Font" => value is PdfArray,
                    "/ColorSpace" or "/CS" or "/BM" => value is PdfName or PdfArray,
                    "/Mask" => value is PdfDictionary or PdfArray,
                    "/BG" or "/BG2" or "/UCR" or "/UCR2" or "/TR" or "/TR2" or "/HT" => value is PdfName,
                    _ => false
                };
                if (!valid) throw Unsupported($"Malformed rendering value '{entry.Key}'.");
            }
            else throw Unsupported($"Unclassified rendering value '{entry.Key}'.");
        }
    }

    private static bool IsNumber(PdfItem? value) => value is PdfInteger || value is PdfReal real && double.IsFinite(real.Value);

    private static int RequiredInteger(PdfDictionary dictionary, string key, int minimum, int maximum)
    {
        if (Dereference(dictionary.Elements[key]) is not PdfInteger integer || integer.Value < minimum || integer.Value > maximum)
            throw Unsupported($"Rendering value '{key}' must be an integer between {minimum} and {maximum}.");
        return integer.Value;
    }

    private static void NumericArray(PdfArray array, int depth = 0)
    {
        if (depth > MaximumDepth) throw Unsupported("A rendering numeric array is too deeply nested.");
        foreach (var item in array.Elements)
        {
            var value = Dereference(item);
            if (value is PdfArray nested) NumericArray(nested, depth + 1);
            else if (!IsNumber(value)) throw Unsupported("A rendering numeric array contains an unexpected object.");
        }
    }

    private static string RemoveCMapComments(string text)
    {
        var output = new StringBuilder(text.Length);
        var literalDepth = 0;
        var escaped = false;
        var comment = false;
        foreach (var character in text)
        {
            if (comment)
            {
                if (character is not ('\r' or '\n')) continue;
                comment = false;
                output.Append(character);
                continue;
            }
            if (literalDepth > 0)
            {
                output.Append(character);
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == '(') literalDepth++;
                else if (character == ')') literalDepth--;
                continue;
            }
            if (character == '%') { comment = true; continue; }
            output.Append(character);
            if (character == '(') literalDepth = 1;
        }
        if (literalDepth != 0 || escaped) throw Unsupported("Unterminated CMap literal string.");
        return output.ToString();
    }

    private static void AuditGeneratedMetadata(SharpDocument document)
    {
        var metadata = document.Internals.Catalog.Elements.GetDictionary("/Metadata")
            ?? throw new InvalidDataException("The freshly generated technical metadata is missing.");
        KeepOnly(metadata, Set("/Type /Subtype /Length /Filter"), audit: true);
        if (metadata.Elements.GetName("/Type") != "/Metadata" || metadata.Elements.GetName("/Subtype") != "/XML" || metadata.Stream is null)
            throw new InvalidDataException("Unexpected technical metadata stream.");
        var bytes = metadata.Stream.UnfilteredValue;
        if (bytes.Length > 64 * 1024) throw new InvalidDataException("Technical metadata exceeds its bounded size.");
        using var input = new MemoryStream(bytes, writable: false);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        XNamespace x = "adobe:ns:meta/";
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace pdf = "http://ns.adobe.com/pdf/1.3/";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace xmp = "http://ns.adobe.com/xap/1.0/";
        XNamespace mm = "http://ns.adobe.com/xap/1.0/mm/";
        var allowed = new HashSet<XName> { x + "xmpmeta", rdf + "RDF", rdf + "Description", rdf + "Alt", rdf + "Seq", rdf + "li", pdf + "Producer", pdf + "Keywords", dc + "title", dc + "creator", dc + "description", xmp + "CreatorTool", xmp + "CreateDate", xmp + "ModifyDate", mm + "DocumentID", mm + "InstanceID" };
        if (xml.Root?.Name != x + "xmpmeta") throw new InvalidDataException("Unexpected metadata root.");
        foreach (var node in xml.DescendantNodes())
        {
            switch (node)
            {
                case XElement element:
                    if (!allowed.Contains(element.Name)) throw new InvalidDataException("Unexpected technical metadata element.");
                    foreach (var attribute in element.Attributes())
                    {
                        if (attribute.IsNamespaceDeclaration) continue;
                        if (attribute.Name == x + "xmptk" && attribute.Value == "3.1-701") continue;
                        if (attribute.Name == rdf + "about" && attribute.Value.Length == 0) continue;
                        if (attribute.Name == XNamespace.Xml + "lang" && attribute.Value == "x-default") continue;
                        throw new InvalidDataException("Unexpected technical metadata attribute.");
                    }
                    if (element.HasElements) break;
                    var name = element.Name;
                    var value = element.Value;
                    if (name == pdf + "Producer")
                    {
                        if (value != "PDFsharp 6.2.4") throw new InvalidDataException("Unexpected metadata producer.");
                    }
                    else if (name == xmp + "CreatorTool")
                    {
                        if (value is not ("" or "PDFsharp 6.2.4 (www.pdfsharp.com)")) throw new InvalidDataException("Unexpected metadata creator.");
                    }
                    else if (name == xmp + "CreateDate" || name == xmp + "ModifyDate")
                    {
                        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new InvalidDataException("Unexpected metadata date.");
                    }
                    else if (name == mm + "DocumentID" || name == mm + "InstanceID")
                    {
                        if (!value.StartsWith("uuid:", StringComparison.Ordinal) || !Guid.TryParseExact(value[5..], "D", out _)) throw new InvalidDataException("Unexpected metadata identifier.");
                    }
                    else if (value.Trim().Length != 0) throw new InvalidDataException("Source-like metadata remains in the candidate.");
                    break;
                case XText text:
                    if (text.Parent?.HasElements == true && text.Value.Trim().Length != 0) throw new InvalidDataException("Unexpected metadata text.");
                    break;
                case XProcessingInstruction instruction:
                    if (instruction.Target != "xpacket" || instruction.Data is not ("begin=\"\uFEFF\" id=\"W5M0MpCehiHzreSzNTczkc9d\"" or "end=\"w\"")) throw new InvalidDataException("Unexpected metadata processing instruction.");
                    break;
                default: throw new InvalidDataException("Unexpected technical metadata node.");
            }
        }
        foreach (var name in new[] { pdf + "Producer", xmp + "CreatorTool", xmp + "CreateDate", xmp + "ModifyDate", mm + "DocumentID", mm + "InstanceID" })
            if (xml.Descendants(name).Count() != 1) throw new InvalidDataException("Duplicate or missing technical metadata field.");
    }

    private static Dictionary<string, HashSet<string>> UsedResources(byte[] data)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var item in ContentReader.ReadContent(data))
        {
            if (item is not COperator operation) continue;
            string? kind = operation.Name switch { "Tf" => "/Font", "Do" => "/XObject", "gs" => "/ExtGState", "CS" or "cs" => "/ColorSpace", _ => null };
            if (kind is null || operation.Operands.Count == 0 || operation.Operands[0] is not CName name) continue;
            var normalized = Normalize(name.Name);
            if (kind == "/ColorSpace" && IsDeviceColor(normalized)) continue;
            if (!result.TryGetValue(kind, out var names)) result.Add(kind, names = new(StringComparer.Ordinal));
            names.Add(normalized);
        }
        return result;
    }

    private static void SetDecoded(PdfDictionary stream, byte[] bytes)
    {
        stream.Stream.Value = bytes;
        stream.Elements.Remove("/Filter");
        stream.Elements.Remove("/DecodeParms");
        stream.Elements.SetInteger("/Length", bytes.Length);
    }

    private static IReadOnlyList<string> Filters(PdfDictionary dictionary)
    {
        var filter = Dereference(dictionary.Elements["/Filter"]);
        return filter switch
        {
            null or PdfNull => [],
            PdfName name => [name.Value],
            PdfArray array => array.Elements.Select(item => Dereference(item) is PdfName name ? name.Value : throw Unsupported("Malformed stream filter.")).ToArray(),
            _ => throw Unsupported("Malformed stream filter.")
        };
    }

    private static string Name(COperator operation, int position, int count)
    {
        if (operation.Operands.Count != count || operation.Operands[position] is not CName name)
            throw Unsupported($"Malformed '{operation.Name}' operator.");
        return Normalize(name.Name);
    }

    private static bool IsDeviceColor(string name) => name is "/DeviceGray" or "/DeviceRGB" or "/DeviceCMYK";
    private static string Normalize(string name) => name.StartsWith('/') ? name : "/" + name;
    private static PdfItem? Dereference(PdfItem? item) => item is PdfReference reference ? reference.Value : item;
    private static PdfDictionary RequireDictionary(PdfItem? item) => item as PdfDictionary ?? throw Unsupported("Expected a rendering resource dictionary.");
    private static HashSet<string> Set(string names) => new(names.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
    private static NotSupportedException Unsupported(string message) => new(message);

    private static void KeepOnly(PdfDictionary dictionary, HashSet<string> keys, bool audit)
    {
        foreach (var key in dictionary.Elements.KeyNames.ToArray())
        {
            if (keys.Contains(key.Value)) continue;
            if (audit) throw new InvalidDataException($"A sanitized dictionary contains unexpected key '{key.Value}'.");
            dictionary.Elements.Remove(key.Value);
        }
    }
}
