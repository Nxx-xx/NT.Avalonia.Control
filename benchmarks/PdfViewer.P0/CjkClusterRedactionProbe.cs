using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using NT.AvaloniaControl.PdfViewer.Editing;
using SharpDocument = PdfSharp.Pdf.PdfDocument;

namespace PdfViewer.P0;

/// <summary>A restricted, glyph-positioned Identity-H experiment. This is not the production redactor.</summary>
internal static class CjkClusterRedactionProbe
{
    private const double Size = 26, Baseline = 160;
    private sealed record Unit(ushort Cid, string Unicode, string GlyphUnicode, double X, double Y);
    internal sealed record Box(double Left, double Bottom, double Right, double Top)
    {
        public double Width => Right - Left;
        public double Height => Top - Bottom;
        internal bool Hits(Box b) => Left < b.Right && Right > b.Left && Bottom < b.Top && Top > b.Bottom;
        internal bool Contains(Box b) => Left <= b.Left && Right >= b.Right && Bottom <= b.Bottom && Top >= b.Top;
        internal static Box Union(IEnumerable<Box> boxes) => new(boxes.Min(x => x.Left), boxes.Min(x => x.Bottom), boxes.Max(x => x.Right), boxes.Max(x => x.Top));
        internal Box Center() => new(Left + Width * .4, Bottom + Height * .4, Left + Width * .6, Bottom + Height * .6);
    }
    private sealed record Atom(ushort Cid, string Unicode, double X, double Y, Box Bounds);
    private sealed record Cluster(string Unicode, ushort[] Cids, Atom[] Atoms, Box Bounds);
    private sealed record Definition(string Id, string Font, string[] Text, string[] Glyphs, int[] TargetIndices, string Purpose);

    internal static async Task<object> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        // Initialize the production singleton once before raw, strictly serial native research calls.
        using (await NT.AvaloniaControl.PdfViewer.PdfDocument.LoadAsync(PdfViewer.Tests.PdfFixture.SinglePage("BOOTSTRAP"))) { }
        var source = Path.GetFullPath(Path.Combine(directory, "..", "font-subset", "sources"));
        var checks = new List<object>(); var cases = new List<object>(); var refusals = new List<object>();
        void Check(string id, bool passed, object detail) => checks.Add(new { Id = id, Passed = passed, Detail = detail });
        foreach (var definition in Definitions())
        {
            var fontPath = Path.Combine(source, definition.Font);
            var font = File.ReadAllBytes(fontPath);
            var units = definition.Text.Select((text, index) => new Unit((ushort)(index + 1), text, definition.Glyphs[index], 40 + index * 32, Baseline)).ToArray();
            var input = Fixture(font, units);
            var inputName = definition.Id + "-input.pdf"; var outputName = definition.Id + "-redacted.pdf";
            File.WriteAllBytes(Path.Combine(directory, inputName), input);
            var before = Read(input);
            var regions = definition.TargetIndices.Select(index => before.Atoms[index].Bounds.Center()).ToArray();
            var transformed = Rewrite(input, regions, expand: true, out var removed, out var retained);
            byte[] output;
            await using (var session = await PdfEditSession.OpenAsync(transformed))
            {
                await session.SanitizeAsync(PdfHiddenInformationPolicy.RemoveAllNonPageData);
                output = await session.ExportAsync();
                Check(definition.Id + ":public-cleanup-commit", session.Revision == 1 && session.IsDirty, "A fresh page-only rewrite drops old content streams, unused objects, annotations and source document information.");
            }
            File.WriteAllBytes(Path.Combine(directory, outputName), output);
            var after = Read(output);
            var expectedBefore = string.Concat(units.Select(x => x.Unicode));
            var expectedAfter = string.Concat(retained.SelectMany(x => x.Atoms).Select(x => x.Unicode));
            Check(definition.Id + ":actual-source-decoded", string.Concat(before.Atoms.Select(x => x.Unicode)) == expectedBefore,
                "The algorithm reads actual source ToUnicode and text operators; the expected fixture text is checked only after the transformation.");
            Check(definition.Id + ":actual-output-decoded", string.Concat(after.Atoms.Select(x => x.Unicode)) == expectedAfter && removed.Length > 0,
                new { Expected = expectedAfter, Actual = string.Concat(after.Atoms.Select(x => x.Unicode)) });
            Check(definition.Id + ":input-one-native-object", NativeObjectCount(input) == 1 && before.Atoms.Length > 1 && removed.SelectMany(x => x.Atoms).Count() < before.Atoms.Length,
                "A packed original Tj is one native text pageobject; a strict internal CID range is removed while neighboring glyphs survive.");
            Check(definition.Id + ":neighbor-origins-preserved", after.Atoms.All(atom => before.Atoms.Any(old => old.Cid == atom.Cid && Math.Abs(old.X-atom.X)<.00001 && old.Y == atom.Y)),
                "Each retained CID keeps the source absolute text matrix; no SetText, reflow, clipping or overlay is used.");
            Check(definition.Id + ":neighbor-native-bounds-preserved", after.Atoms.All(atom => before.Atoms.Any(old => old.Cid == atom.Cid && Near(old.Bounds, atom.Bounds))),
                "Native text object bounds survive the rewrite within 0.002 PDF points.");
            Check(definition.Id + ":cluster-expansion", removed.Any(cluster => !regions.Any(region => region.Contains(cluster.Bounds))),
                "A small geometric hit expands to the complete source CID / Unicode grapheme cluster.");
            Check(definition.Id + ":unused-mappings-pruned", after.Map.Keys.Order().SequenceEqual(after.Atoms.Select(x => x.Cid).Distinct().Order()),
                "The rewritten ToUnicode contains only CIDs used by retained painting operators.");
            var rejected = false;
            try { Rewrite(input, regions, expand: false, out _, out _); }
            catch (NotSupportedException) { rejected = true; }
            Check(definition.Id + ":strict-partial-cluster-refusal", rejected && Hash(input) == Hash(File.ReadAllBytes(Path.Combine(directory, inputName))),
                "Strict policy refuses a partial glyph/cluster hit without writing a candidate or mutating source bytes.");
            cases.Add(new { definition.Id, definition.Purpose, FontFile = definition.Font, FontSourceSHA256 = Hash(font), InputFile = inputName, OutputFile = outputName,
                ExpectedBefore = expectedBefore, ExpectedAfter = expectedAfter, Glyphs = units.Select(x => new { x.Cid, x.Unicode, x.GlyphUnicode }), Regions = regions,
                OriginalAtoms = before.Atoms, RemovedClusters = removed, RetainedClusters = retained,
                InputNativeObjectCount = NativeObjectCount(input), OutputNativeObjectCount = NativeObjectCount(output),
                ClusterPolicy = "expand-geometric-hit-to-complete-Unicode-grapheme-and-source-CID", InputSHA256 = Hash(input), OutputSHA256 = Hash(output) });
            if (definition.Id == "simplified")
            {
                foreach (var mode in new[] { "rotated-page", "unsupported-TJ", "missing-ToUnicode", "invalid-surrogate-ToUnicode" })
                {
                    var unsupported = Mutate(input, mode); var file = mode + "-refused-input.pdf";
                    File.WriteAllBytes(Path.Combine(directory, file), unsupported);
                    var refused = false; string? reason = null;
                    try { Rewrite(unsupported, regions, true, out _, out _); }
                    catch (NotSupportedException exception) { refused = true; reason = exception.Message; }
                    Check(mode + ":refused-before-output", refused, reason ?? "Unexpectedly accepted.");
                    refusals.Add(new { Id = mode, InputFile = file, InputSHA256 = Hash(unsupported), Refused = refused, Reason = reason, OutputFile = (string?)null });
                }
            }
        }
        return new { Status = "bounded-cjk-grapheme-cid-redaction-feasibility", Checks = checks, Cases = cases, Refusals = refusals,
            FunctionalAssemblySHA256 = Hash(File.ReadAllBytes(typeof(PdfEditSession).Assembly.Location)),
            Outputs = Directory.GetFiles(directory, "*.pdf").Order().Select(path => new { File = Path.GetFileName(path), SHA256 = Hash(File.ReadAllBytes(path)), Bytes = new FileInfo(path).Length }),
            Limitations = new[] {
                "Benchmark-only restricted positive grammar: one page, horizontal Identity-H Type0 CIDFontType2, two-byte CIDs in Tj, identity text matrix, uniform size, no shaping, no TJ, clipping, marked content, forms or transforms. Every positive source uses a single packed Tj / native text pageobject, then splits retained glyphs into absolute matrices.",
                "Unicode grapheme membership is read from actual ToUnicode. A source CID mapping to multiple scalars is indivisible even when it spans multiple graphemes; overlapping glyph clusters are merged transitively.",
                "Kana combining uses separately positioned base/mark glyphs; Hangul decomposition, ideographic variation and multi-ideograph mappings are explicitly authored synthetic ToUnicode fixtures, not proof of OpenType shaping or real IVS glyph selection.",
                "The complete licensed embedded TrueType program remains painting data; its general cmap and glyph outlines are not scrubbed. Unused document ToUnicode entries and removed CIDToGID entries are scrubbed.",
                "Raw native fixture/geometry calls run strictly serially after singleton initialization; this is not a production queue, UI integration or cross-platform result.",
                "Independent semantic/xref/font checks and Poppler target plus retained-pixel comparisons are required. Production partial text redaction remains unsupported until the parser and transaction integration are implemented." } };
    }

    private static Definition[] Definitions() =>
    [
        Def("simplified", "保留中文删除再见", [4,5], "Simplified Han, two individual partial glyph hits."),
        Def("traditional", "保留繁體刪除再見", [4,5], "Traditional Han, two individual partial glyph hits."),
        new("kana-combining", "LXGWWenKai-Regular.ttf", ["残","す","カ","\u3099","保","存"], ["残","す","カ","\u3099","保","存"], [2], "Two source CIDs compose one Unicode kana grapheme; a base-only hit removes both."),
        new("hangul-jamo", "GowunDodum-Regular.ttf", ["한","\u1100\u1161","글"], ["한","가","글"], [1], "One source CID maps to decomposed Hangul Jamo, rendered with precomposed 가 glyph; synthetic shaping-free mapping."),
        Def("supplementary-extb", "保\U00020003\U0002000B留", [1], "Supplementary CJK Extension B scalar remains an indivisible UTF-16 surrogate pair."),
        new("variation-sequence", "LXGWWenKai-Regular.ttf", ["保","漢\U000E0100","留"], ["保","漢","留"], [1], "One source CID maps to Han plus supplementary variation selector; synthetic semantic mapping, not IVS font support."),
        new("single-glyph-multi-scalar", "LXGWWenKai-Regular.ttf", ["保","秘密","留"], ["保","密","留"], [1], "One source CID maps to two Han scalars/graphemes; no geometric subdivision can safely remove only one.")
    ];
    private static Definition Def(string id, string text, int[] target, string purpose)
    { var items = text.EnumerateRunes().Select(x => x.ToString()).ToArray(); return new(id, "LXGWWenKai-Regular.ttf", items, items, target, purpose); }

    private sealed record Parsed(Atom[] Atoms, Dictionary<ushort, string> Map);
    private static Parsed Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, false); using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        if (pdf.PageCount != 1 || pdf.Pages[0].Rotate != 0) throw new NotSupportedException("Only a single unrotated page is accepted.");
        var page = pdf.Pages[0]; var fonts = page.Elements.GetDictionary("/Resources")?.Elements.GetDictionary("/Font") ?? throw new NotSupportedException("No fonts.");
        if (page.Elements.ContainsKey("/UserUnit") && page.Elements.GetReal("/UserUnit") != 1) throw new NotSupportedException("Nondefault page units are unsupported.");
        if (fonts.Elements.Count != 1) throw new NotSupportedException("Exactly one CID font is required.");
        var key = fonts.Elements.KeyNames.Single().Value; var font = fonts.Elements.GetDictionary(key)!;
        if (font.Elements.GetName("/Subtype") != "/Type0" || font.Elements.GetName("/Encoding") != "/Identity-H") throw new NotSupportedException("Only Identity-H Type0 is accepted.");
        var cmap = font.Elements.GetDictionary("/ToUnicode")?.Stream?.UnfilteredValue ?? throw new NotSupportedException("A complete ToUnicode is mandatory.");
        var map = ParseMap(Encoding.ASCII.GetString(cmap));
        var operations = ContentReader.ReadContent(page.Contents.CreateSingleContent().Stream.UnfilteredValue).Cast<CObject>().Select(item => item as COperator ?? throw new NotSupportedException("Only operators are accepted.")).ToArray();
        var descendant = (PdfDictionary)Dereference(font.Elements.GetArray("/DescendantFonts")!.Elements[0]);
        var positions = new List<(ushort Cid, double X, double Y)>(); var p = 0; var blocks = 0;
        COperator Take(string name, int count)
        {
            if (p >= operations.Length || operations[p].Name != name || operations[p].Operands.Count != count) throw new NotSupportedException("Unsupported text operator/state: expected " + name);
            return operations[p++];
        }
        Take("q",0);
        while (p < operations.Length && operations[p].Name == "BT")
        {
            Take("BT",0); var tf = Take("Tf",2); var tm = Take("Tm",6); var tj = Take("Tj",1); Take("ET",0); blocks++;
            if (tf.Operands[0] is not CName fontName || fontName.Name != key || CNumber(tf.Operands[1]) != Size || tj.Operands[0] is not CString codes)
                throw new NotSupportedException("Only the canonical fixed size CID font and byte strings are accepted.");
            if (CNumber(tm.Operands[0])!=1 || CNumber(tm.Operands[1])!=0 || CNumber(tm.Operands[2])!=0 || CNumber(tm.Operands[3])!=1) throw new NotSupportedException("Only identity text matrix is accepted.");
            var x = CNumber(tm.Operands[4]); var y = CNumber(tm.Operands[5]); var cidBytes = Encoding.Latin1.GetBytes(codes.Value);
            if (cidBytes.Length == 0 || cidBytes.Length %2 !=0) throw new NotSupportedException("Only complete two-byte CIDs are accepted.");
            for (var offset = 0; offset < cidBytes.Length; offset+=2)
            {
                var cid = BinaryPrimitives.ReadUInt16BigEndian(cidBytes.AsSpan(offset,2));
                positions.Add((cid,x,y)); x += Width(descendant, cid) * Size / 1000;
            }
        }
        Take("Q",0);
        if (p != operations.Length || blocks == 0 || NativeObjectCount(bytes) != blocks) throw new NotSupportedException("Each complete Tj block must correspond to one native text object and no other state.");
        var bounds = NativeBounds(bytes, positions);
        var atoms = positions.Select((item, index) => {
            if (!map.TryGetValue(item.Cid, out var unicode)) throw new NotSupportedException("A used CID has no complete Unicode mapping.");
            return new Atom(item.Cid, unicode, item.X, item.Y, bounds[index]);
        }).ToArray();
        if (atoms.Any(atom => atom.Y != Baseline) || atoms.Select(x => x.Cid).Distinct().Count() != atoms.Length) throw new NotSupportedException("Only unique CIDs on the canonical horizontal baseline are accepted.");
        return new(atoms, map);
    }

    private static byte[] Rewrite(byte[] input, Box[] regions, bool expand, out Cluster[] removed, out Cluster[] retained)
    {
        var parsed = Read(input); var clusters = Group(parsed.Atoms);
        removed = clusters.Where(cluster => cluster.Atoms.Any(atom => regions.Any(region => region.Hits(atom.Bounds)))).ToArray();
        retained = clusters.Except(removed).ToArray();
        if (removed.Length == 0 || retained.Length == 0) throw new NotSupportedException("The bounded experiment requires both a target and retained control glyphs.");
        if (!expand && removed.Any(cluster => !regions.Any(region => region.Contains(cluster.Bounds)))) throw new NotSupportedException("The region intersects only part of an indivisible glyph/Unicode cluster.");
        var keep = retained.SelectMany(x => x.Atoms).ToArray(); var used = keep.Select(x => x.Cid).ToHashSet();
        using var stream = new MemoryStream(input, false); using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
        var page = pdf.Pages[0]; var fonts = page.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/Font")!;
        var key = fonts.Elements.KeyNames.Single().Value; var font = fonts.Elements.GetDictionary(key)!;
        SetRaw(font.Elements.GetDictionary("/ToUnicode")!, Encoding.ASCII.GetBytes(CMap(parsed.Map.Where(pair => used.Contains(pair.Key)).ToDictionary())));
        var descendant = (PdfDictionary)Dereference(font.Elements.GetArray("/DescendantFonts")!.Elements[0]);
        var cidMap = descendant.Elements.GetDictionary("/CIDToGIDMap")!; var glyphBytes = cidMap.Stream.UnfilteredValue;
        for (var cid = 0; cid < glyphBytes.Length / 2; cid++) if (!used.Contains((ushort)cid)) glyphBytes.AsSpan(cid * 2, 2).Clear();
        SetRaw(cidMap, glyphBytes);
        SetContent(page, keep.Select(x => (x.Cid, x.X, x.Y)), key);
        using var output = new MemoryStream(); pdf.Save(output, false); return output.ToArray();
    }

    private static Cluster[] Group(Atom[] atoms)
    {
        var text = string.Concat(atoms.Select(x => x.Unicode)); var starts = StringInfo.ParseCombiningCharacters(text);
        var atomRanges = new List<(int Start, int End)>(); var offset = 0;
        foreach (var atom in atoms) { atomRanges.Add((offset, offset + atom.Unicode.Length)); offset += atom.Unicode.Length; }
        // A CID may cover multiple graphemes. Union every grapheme interval overlapping the same CID.
        var parent = Enumerable.Range(0, atoms.Length).ToArray();
        int Root(int n) { while (parent[n] != n) { parent[n] = parent[parent[n]]; n = parent[n]; } return n; }
        for (var i = 0; i < starts.Length; i++)
        {
            var end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
            var matching = Enumerable.Range(0, atoms.Length).Where(index => atomRanges[index].Start < end && atomRanges[index].End > starts[i]).ToArray();
            foreach (var index in matching.Skip(1)) parent[Root(index)] = Root(matching[0]);
        }
        return atoms.Select((atom, index) => (atom, index)).GroupBy(x => Root(x.index)).Select(group => {
            var members = group.Select(x => x.atom).ToArray();
            return new Cluster(string.Concat(members.Select(x => x.Unicode)), members.Select(x => x.Cid).ToArray(), members, Box.Union(members.Select(x => x.Bounds)));
        }).ToArray();
    }

    private static Dictionary<ushort, string> ParseMap(string cmap)
    {
        if (!cmap.Contains("begincodespacerange", StringComparison.Ordinal) || cmap.Contains("beginbfrange", StringComparison.Ordinal)) throw new NotSupportedException("Only complete explicit bfchar ToUnicode is accepted.");
        var map = new Dictionary<ushort, string>(); var utf16 = new UnicodeEncoding(true, false, true);
        foreach (Match section in Regex.Matches(cmap, @"\d+\s+beginbfchar(?<body>.*?)endbfchar", RegexOptions.Singleline))
            foreach (Match pair in Regex.Matches(section.Groups["body"].Value, @"<(?<cid>[0-9a-fA-F]{4})>\s*<(?<unicode>[0-9a-fA-F]+)>"))
            {
                string unicode;
                try { unicode = utf16.GetString(Convert.FromHexString(pair.Groups["unicode"].Value)); }
                catch (Exception exception) when (exception is DecoderFallbackException or FormatException) { throw new NotSupportedException("Invalid UTF-16 Unicode mapping.", exception); }
                if (unicode.Length == 0 || unicode.EnumerateRunes().Any(rune => Rune.IsControl(rune))) throw new NotSupportedException("Empty/control mappings are not accepted.");
                if (!map.TryAdd(ushort.Parse(pair.Groups["cid"].Value, NumberStyles.HexNumber), unicode)) throw new NotSupportedException("Ambiguous duplicate CID mapping.");
            }
        return map;
    }

    private static byte[] Fixture(byte[] bytes, Unit[] units)
    {
        var fontType = typeof(PdfEmbeddedTrueTypeFont).Assembly.GetType("NT.AvaloniaControl.PdfViewer.Editing.PdfTrueTypeFont")!;
        var info = Activator.CreateInstance(fontType, BindingFlags.Instance | BindingFlags.NonPublic, null, [bytes], null)!;
        var lookup = fontType.GetMethod("GetGlyphIndex", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cidToGid = new byte[(units.Length + 1) * 2];
        foreach (var unit in units)
        {
            var scalar = unit.GlyphUnicode.EnumerateRunes().Single().Value;
            var gid = (int)lookup.Invoke(info, [scalar])!;
            if (gid == 0) throw new InvalidDataException("Missing fixture glyph U+" + scalar.ToString("X"));
            BinaryPrimitives.WriteUInt16BigEndian(cidToGid.AsSpan(unit.Cid * 2, 2), checked((ushort)gid));
        }
        var document = N.FPDF_CreateNewDocument(); byte[] scratch;
        try
        {
            var page = SaveNative.FPDFPage_New(document, 0, 600, 260);
            try
            {
                var font = N.FPDFText_LoadCidType2Font(document, bytes, (uint)bytes.Length, CMap(units.ToDictionary(x => x.Cid, x => x.Unicode)), cidToGid, (uint)cidToGid.Length);
                if (font == IntPtr.Zero) throw new InvalidDataException("Cannot load fixture CID font.");
                try
                {
                    var text = N.FPDFPageObj_CreateTextObj(document, font, (float)Size);
                    if (N.FPDFText_SetCharcodes(text, units.Select(x => (uint)x.Cid).ToArray(), (nuint)units.Length) == 0) throw new InvalidDataException("Cannot set CID fixture.");
                    SaveNative.FPDFPage_InsertObject(page, text);
                    if (SaveNative.FPDFPage_GenerateContent(page) == 0) throw new InvalidDataException("Cannot generate scratch fixture.");
                    using var output = new MemoryStream(); if (!SaveNative.Save(document, output).Success) throw new IOException("Cannot save scratch fixture."); scratch = output.ToArray();
                }
                finally { N.FPDFFont_Close(font); }
            }
            finally { SaveNative.FPDF_ClosePage(page); }
        }
        finally { SaveNative.FPDF_CloseDocument(document); }
        using var input = new MemoryStream(scratch, false); using var target = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        var targetPage = target.Pages[0]; var fonts = targetPage.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/Font")!;
        var key = fonts.Elements.KeyNames.Single().Value;
        var packed = "q\nBT " + key + " 26 Tf 1 0 0 1 40 160 Tm <" + string.Concat(units.Select(x => x.Cid.ToString("X4"))) + "> Tj ET\nQ\n";
        targetPage.Contents.Elements.Clear(); var packedStream = new PdfDictionary(target); packedStream.CreateStream(Encoding.ASCII.GetBytes(packed)); target.Internals.AddObject(packedStream); targetPage.Elements["/Contents"] = packedStream.Reference!;
        target.Info.Title = "CJK source hidden copy " + string.Concat(units.Select(x => x.Unicode));
        using var final = new MemoryStream(); target.Save(final, false); return final.ToArray();
    }

    private static void SetContent(PdfPage page, IEnumerable<(ushort Cid, double X, double Y)> atoms, string resource)
    {
        var builder = new StringBuilder("q\n");
        foreach (var atom in atoms) builder.Append(FormattableString.Invariant($"BT {resource} 26 Tf 1 0 0 1 {atom.X:0.######} {atom.Y:0.######} Tm <{atom.Cid:X4}> Tj ET\n"));
        builder.Append("Q\n"); page.Contents.Elements.Clear(); var content = new PdfDictionary(page.Owner); content.CreateStream(Encoding.ASCII.GetBytes(builder.ToString())); page.Owner.Internals.AddObject(content); page.Elements["/Contents"] = content.Reference!;
    }
    private static string CMap(Dictionary<ushort, string> map)
    {
        var b = new StringBuilder("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n/CMapName /NTCjkCluster def\n/CMapType 2 def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
        foreach (var group in map.OrderBy(x => x.Key).Chunk(100))
        { b.Append(group.Length).Append(" beginbfchar\n"); foreach (var pair in group) b.Append('<').Append(pair.Key.ToString("X4")).Append("> <").Append(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(pair.Value))).Append(">\n"); b.Append("endbfchar\n"); }
        return b.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n").ToString();
    }
    private static byte[] Mutate(byte[] input, string mode)
    {
        using var stream = new MemoryStream(input, false); using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
        if (mode == "rotated-page") pdf.Pages[0].Rotate = 90;
        if (mode == "unsupported-TJ")
        {
            var content = pdf.Pages[0].Contents.CreateSingleContent();
            var changed = Regex.Replace(Encoding.ASCII.GetString(content.Stream.UnfilteredValue), @"<([0-9A-F]+)> Tj", "[<$1>] TJ");
            if (!changed.Contains(" TJ",StringComparison.Ordinal)) throw new InvalidDataException("TJ refusal fixture was not changed.");
            var replacement = new PdfDictionary(pdf); replacement.CreateStream(Encoding.ASCII.GetBytes(changed)); pdf.Internals.AddObject(replacement); pdf.Pages[0].Elements["/Contents"] = replacement.Reference!;
        }
        if (mode == "missing-ToUnicode")
        { var fonts = pdf.Pages[0].Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/Font")!; fonts.Elements.GetDictionary(fonts.Elements.KeyNames.Single().Value)!.Elements.Remove("/ToUnicode"); }
        if (mode == "invalid-surrogate-ToUnicode")
        {
            var fonts = pdf.Pages[0].Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/Font")!;
            var map = fonts.Elements.GetDictionary(fonts.Elements.KeyNames.Single().Value)!.Elements.GetDictionary("/ToUnicode")!;
            var content = Encoding.ASCII.GetString(map.Stream.UnfilteredValue);
            var changed = Regex.Replace(content, @"<0001>\s*<[0-9A-Fa-f]+>", "<0001> <D800>");
            if (changed == content) throw new InvalidDataException("The surrogate refusal fixture was not changed.");
            SetRaw(map,Encoding.ASCII.GetBytes(changed));
        }
        using var output = new MemoryStream(); pdf.Save(output, false); return output.ToArray();
    }
    private static double Width(PdfDictionary descendant, ushort cid)
    {
        var widths = descendant.Elements.GetArray("/W");
        if (widths is not null)
            for (var i = 0; i < widths.Elements.Count;)
            {
                var start = ((PdfInteger)widths.Elements[i++]).Value;
                if (Dereference(widths.Elements[i]) is PdfArray list)
                {
                    i++; if (cid >= start && cid < start + list.Elements.Count) return Number(list.Elements[cid - start]);
                }
                else { var end = ((PdfInteger)widths.Elements[i++]).Value; var width = Number(widths.Elements[i++]); if (cid >= start && cid <= end) return width; }
            }
        return descendant.Elements.ContainsKey("/DW") ? descendant.Elements.GetReal("/DW") : 1000;
    }
    private static double Number(PdfItem item) => item is PdfInteger integer ? integer.Value : ((PdfReal)item).Value;
    private static double CNumber(CObject item) => item is CInteger integer ? integer.Value : item is CReal real ? real.Value : throw new NotSupportedException("Numeric text matrix required.");
    private static int NativeObjectCount(byte[] bytes)
    {
        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try { var doc = SaveNative.FPDF_LoadMemDocument64(pin.AddrOfPinnedObject(), (nuint)bytes.Length, null);
            try { var page = SaveNative.FPDF_LoadPage(doc,0); try { return N.FPDFPage_CountObjects(page); } finally { SaveNative.FPDF_ClosePage(page); } }
            finally { SaveNative.FPDF_CloseDocument(doc); } }
        finally { pin.Free(); }
    }
    private static Box[] NativeBounds(byte[] bytes, List<(ushort Cid, double X, double Y)> positions)
    {
        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var doc = SaveNative.FPDF_LoadMemDocument64(pin.AddrOfPinnedObject(), (nuint)bytes.Length, null); if (doc == IntPtr.Zero) throw new InvalidDataException("Cannot reopen fixture.");
            try
            {
                var page = SaveNative.FPDF_LoadPage(doc, 0);
                try
                {
                    var first = N.FPDFPage_GetObject(page, 0);
                    if (N.FPDFPageObj_GetType(first) != 1) throw new NotSupportedException("Only text objects are accepted.");
                    var font = N.FPDFTextObj_GetFont(first);
                    return positions.Select(item => {
                        var obj = N.FPDFPageObj_CreateTextObj(doc, font, (float)Size);
                        try {
                            if (N.FPDFText_SetCharcodes(obj, [(uint)item.Cid],1)==0) throw new NotSupportedException("Cannot measure actual font CID.");
                            SaveNative.FPDFPageObj_Transform(obj,1,0,0,1,item.X,item.Y);
                            if (N.FPDFPageObj_GetBounds(obj, out var l,out var b,out var r,out var t)==0) throw new NotSupportedException("No glyph geometry.");
                            return new Box(l,b,r,t);
                        } finally { N.FPDFPageObj_Destroy(obj); }
                    }).ToArray();
                }
                finally { SaveNative.FPDF_ClosePage(page); }
            }
            finally { SaveNative.FPDF_CloseDocument(doc); }
        }
        finally { pin.Free(); }
    }
    private static PdfItem Dereference(PdfItem item) => item is PdfReference reference ? reference.Value : item;
    private static void SetRaw(PdfDictionary dictionary, byte[] bytes) { dictionary.Elements.Remove("/Filter"); dictionary.Elements.Remove("/DecodeParms"); dictionary.Stream.Value = bytes; }
    private static bool Near(Box a, Box b) => Math.Abs(a.Left-b.Left)<.002 && Math.Abs(a.Bottom-b.Bottom)<.002 && Math.Abs(a.Right-b.Right)<.002 && Math.Abs(a.Top-b.Top)<.002;
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static class N
    {
        private const string Library = "pdfium";
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_CreateNewDocument();
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFText_LoadCidType2Font(IntPtr document, byte[] font, uint size, [MarshalAs(UnmanagedType.LPStr)] string toUnicode, byte[] cidToGid, uint mapLength);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFFont_Close(IntPtr font);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPageObj_CreateTextObj(IntPtr document, IntPtr font, float size);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFText_SetCharcodes(IntPtr text, uint[] codes, nuint count);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_CountObjects(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPage_GetObject(IntPtr page, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPageObj_GetType(IntPtr obj);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFTextObj_GetFont(IntPtr obj);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPageObj_Destroy(IntPtr obj);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPageObj_GetBounds(IntPtr obj, out float left, out float bottom, out float right, out float top);
    }
}
