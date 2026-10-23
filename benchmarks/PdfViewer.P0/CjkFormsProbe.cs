using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using NT.AvaloniaControl.PdfViewer.Editing;
using SharpDocument = PdfSharp.Pdf.PdfDocument;

namespace PdfViewer.P0;

/// <summary>
/// Native canonical form-value commit plus bounded managed embedded-font appearance experiment.
/// Native CJK appearance fallback is preserved as an explicit rejected counterexample; every final
/// output is bridged after commit and must pass independent actual-font and appearance checks.
/// </summary>
internal static class CjkFormsProbe
{
    public static object Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var corpus = Path.GetFullPath(Path.Combine(directory, "..", "font-subset", "sources"));
        var sourceManifestPath = Path.Combine(corpus, "source-manifest.json");
        using var sourceManifest = JsonDocument.Parse(File.ReadAllBytes(sourceManifestPath));
        var checks = new List<object>();
        var cases = new List<object>();
        void Check(string id, bool passed, object detail) => checks.Add(new { Id = id, Passed = passed, Detail = detail });
        foreach (var entry in new[] { (Id: "lxgw", File: "LXGWWenKai-Regular.ttf"), (Id: "zen", File: "ZenKakuGothicNew-Regular.ttf"), (Id: "gowun", File: "GowunDodum-Regular.ttf") })
        {
            var fontPath = Path.Combine(corpus, entry.File);
            var fontBytes = File.ReadAllBytes(fontPath);
            var expectedHash = sourceManifest.RootElement.GetProperty("Files").EnumerateArray().Single(value => value.GetProperty("File").GetString() == entry.File).GetProperty("SHA256").GetString();
            if (Convert.ToHexString(SHA256.HashData(fontBytes)) != expectedHash) throw new InvalidDataException("CJK font source does not match the frozen provenance manifest: " + entry.File);
            var options = entry.Id == "gowun" ? new[] { "한글", "글꼴", "ABC" } : new[] { "中文", "日本", "ABC" };
            var firstText = entry.Id == "gowun" ? "한글입력ABC" : "中文日本ABC";
            var firstMultiline = entry.Id == "gowun" ? "첫째 줄\r둘째 줄" : "中文第一行\r日本第二行";
            var secondText = entry.Id switch { "gowun" => "새문자검증ABC", "zen" => "互換漢字ABC", _ => "兼容汉字ABC" };
            var secondMultiline = entry.Id == "gowun" ? "다시 한글\r글꼴 확인" : "第二次中文\r日本兼容";
            var input = CreateFixture(fontBytes, options, firstText + firstMultiline + secondText + secondMultiline + string.Concat(options), out var mapped);
            var inputName = entry.Id + "-input.pdf";
            var savedName = entry.Id + "-saved.pdf";
            var editedName = entry.Id + "-reedited.pdf";
            File.WriteAllBytes(Path.Combine(directory, inputName), input);
            var stages = new List<object>();
            var stageBytes = input;
            foreach (var stage in new[] { (Id: "saved", File: savedName, Text: firstText, Multiline: firstMultiline, List: options[1], Index: 1),
                         (Id: "reedited", File: editedName, Text: secondText, Multiline: secondMultiline, List: "ABC", Index: 2) })
            {
                using var host = new Host(stageBytes);
                host.Click(60, 315);
                var inputResult = host.Type(stage.Text);
                Native.FORM_ForceToKillFocus(host.Handle);
                host.Click(60, 245);
                var multilineResult = host.Type(stage.Multiline);
                Native.FORM_ForceToKillFocus(host.Handle);
                host.Click(60, 140);
                Native.FORM_SelectAllText(host.Handle, host.Page);
                var listResult = Native.FORM_SetIndexSelected(host.Handle, host.Page, stage.Index, 1);
                Native.FORM_ForceToKillFocus(host.Handle);
                var states = host.Fields();
                Check(entry.Id + ":" + stage.Id + ":characters-accepted", inputResult == stage.Text.Length && multilineResult == stage.Multiline.Length,
                    new { AcceptedText = inputResult, AcceptedMultiline = multilineResult });
                Check(entry.Id + ":" + stage.Id + ":canonical-values", states.Single(x => x.Name == "CjkText").Value == stage.Text &&
                    Normalize(states.Single(x => x.Name == "CjkMultiline").Value) == Normalize(stage.Multiline) &&
                    states.Single(x => x.Name == "CjkList").Value == stage.List,
                    new { Fields = states, NativeListResult = listResult });
                var nativeName = entry.Id + "-native-" + stage.Id + ".pdf";
                var nativePath = Path.Combine(directory, nativeName);
                host.Save(nativePath);
                var nativeBytes = File.ReadAllBytes(nativePath);
                var nativeFonts = ReadNativeAppearanceFonts(nativeBytes);
                Check(entry.Id + ":" + stage.Id + ":native-fallback-counterexample", nativeFonts.Any(font => !font.Embedded), nativeFonts);
                stageBytes = RepairAppearances(nativeBytes);
                var outputPath = Path.Combine(directory, stage.File);
                File.WriteAllBytes(outputPath, stageBytes);
                using var reopened = new Host(stageBytes);
                var persisted = reopened.Fields();
                Check(entry.Id + ":" + stage.Id + ":reopen-values", persisted.Select(x => Normalize(x.Value)).SequenceEqual(states.Select(x => Normalize(x.Value))), persisted);
                Check(entry.Id + ":" + stage.Id + ":appearances", persisted.Length == 3 && persisted.All(x => x.HasAppearance), persisted);
                stages.Add(new { Id = stage.Id, File = stage.File, NativeFile = nativeName, NativeFonts = nativeFonts,
                    Expected = new[] { new { Name = "CjkText", Value = stage.Text }, new { Name = "CjkMultiline", Value = stage.Multiline }, new { Name = "CjkList", Value = stage.List } }, Fields = persisted });
            }
            cases.Add(new { entry.Id, FontFile = entry.File, FontPath = fontPath, SourceSHA256 = Hash(fontPath), SourceBytes = fontBytes.Length,
                MappedBmpScalars = mapped, Options = options, InputFile = inputName, Stages = stages });
        }
        return new { Status = "native-canonical-managed-appearance-acroform-experiment", NativeAutomaticAppearanceStatus = "rejected-unembedded-fallback",
            FontSourceManifestSHA256 = Hash(sourceManifestPath), Checks = checks, Cases = cases,
            Outputs = Directory.EnumerateFiles(directory, "*.pdf").Order().Select(path => new { File = Path.GetFileName(path), SHA256 = Hash(path), Bytes = new FileInfo(path).Length }).ToArray(),
            Limitations = new[] { "This is a headless native form-host feasibility probe, not a production form editor or an Avalonia IME implementation.",
                "Full static TrueType font with BMP identity CID coverage; supplementary Unicode, shaping, vertical writing, CFF and TTC are not covered.",
                "Native form input commits correct canonical values but regenerates CJK appearance with unembedded system-font fallback. A bounded managed appearance bridge is required after every commit; native automatic font compatibility is rejected.",
                "The managed bridge supports this fixture's fixed size, left aligned single line, explicit newline text and fully visible single-choice list only. Auto wrapping, scrolling, rotation, text fitting and arbitrary existing form layouts are not implemented.",
                "A value-only annotation subset is insufficient for arbitrary later input; the full licensed font is retained in AcroForm default resources.",
                "Independent pypdf canonical field, default-resource and appearance checks plus Poppler rendering are required." } };
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed record AppearanceFont(string Field, string Resource, string BaseFont, bool Embedded);
    private static AppearanceFont[] ReadNativeAppearanceFonts(byte[] input)
    {
        using var stream = new MemoryStream(input, false);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        var result = new List<AppearanceFont>();
        foreach (var item in document.Internals.Catalog.Elements.GetDictionary("/AcroForm")!.Elements.GetArray("/Fields")!.Elements)
        {
            var field = (PdfDictionary)Dereference(item);
            var ap = field.Elements.GetDictionary("/AP")!.Elements.GetDictionary("/N")!;
            var fonts = ap.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/Font")!;
            foreach (var name in Regex.Matches(Encoding.Latin1.GetString(ap.Stream.UnfilteredValue), @"/([^\s]+)\s+[0-9.]+\s+Tf").Select(match => "/" + match.Groups[1].Value).Distinct())
            {
                var font = fonts.Elements.GetDictionary(name)!;
                var descendant = (PdfDictionary)Dereference(font.Elements.GetArray("/DescendantFonts")!.Elements[0]);
                var descriptor = descendant.Elements.GetDictionary("/FontDescriptor");
                result.Add(new AppearanceFont(field.Elements.GetString("/T"), name, font.Elements.GetName("/BaseFont"), descriptor?.Elements.GetDictionary("/FontFile2")?.Stream is not null));
            }
        }
        return result.ToArray();
    }

    // Bounded feasibility bridge. The caller has already committed field /V through the native host.
    // It deliberately reads canonical values instead of copying a requested value into appearance only.
    private static byte[] RepairAppearances(byte[] input)
    {
        using var source = new MemoryStream(input, false);
        using var document = PdfReader.Open(source, PdfDocumentOpenMode.Modify);
        var form = document.Internals.Catalog.Elements.GetDictionary("/AcroForm")!;
        var fonts = new PdfDictionary(document);
        fonts.Elements["/Cjk"] = form.Elements.GetDictionary("/DR")!.Elements.GetDictionary("/Font")!.Elements["/Cjk"];
        var resources = new PdfDictionary(document); resources.Elements["/Font"] = fonts;
        foreach (var item in form.Elements.GetArray("/Fields")!.Elements)
        {
            var field = (PdfDictionary)Dereference(item);
            var rectangle = field.Elements.GetArray("/Rect")!;
            var coordinates = rectangle.Elements.Select(value => double.Parse(value.ToString()!, CultureInfo.InvariantCulture)).ToArray();
            var width = coordinates[2] - coordinates[0]; var height = coordinates[3] - coordinates[1];
            var appearance = new PdfDictionary(document); document.Internals.AddObject(appearance);
            appearance.Elements.SetName("/Type", "/XObject"); appearance.Elements.SetName("/Subtype", "/Form");
            appearance.Elements["/BBox"] = new PdfArray(document, new PdfInteger(0), new PdfInteger(0), new PdfReal(width), new PdfReal(height));
            appearance.Elements["/Resources"] = resources;
            var content = new StringBuilder(FormattableString.Invariant($"q 1 g 0 0 {width} {height} re f 0 G 1 w 0.5 0.5 {width - 1} {height - 1} re S\n1 1 {width - 2} {height - 2} re W n\n"));
            static string Hex(string text) => Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text));
            if (field.Elements.GetName("/FT") == "/Ch")
            {
                var selected = field.Elements.GetString("/V"); var options = field.Elements.GetArray("/Opt")!;
                for (var index = 0; index < options.Elements.Count; index++)
                {
                    var text = ((PdfString)Dereference(options.Elements[index])).Value;
                    var y = height - 20 - index * 22;
                    if (text == selected) content.Append(FormattableString.Invariant($"q 0 0.2 0.443137 rg 1 {y - 4} {width - 2} 22 re f Q\n"));
                    content.Append(FormattableString.Invariant($"BT /Cjk 16 Tf {(text == selected ? "1" : "0")} g 1 0 0 1 4 {y} Tm <{Hex(text)}> Tj ET\n"));
                }
            }
            else
            {
                var lines = Normalize(field.Elements.GetString("/V")).Split('\n');
                for (var index = 0; index < lines.Length; index++)
                    content.Append(FormattableString.Invariant($"BT /Cjk 16 Tf 0 g 1 0 0 1 4 {height - 20 - index * 22} Tm <{Hex(lines[index])}> Tj ET\n"));
            }
            content.Append("Q\n"); appearance.CreateStream(Encoding.ASCII.GetBytes(content.ToString()));
            var ap = new PdfDictionary(document); ap.Elements["/N"] = appearance.Reference!; field.Elements["/AP"] = ap;
            field.Elements.SetString("/DA", "/Cjk 16 Tf 0 g");
        }
        form.Elements.SetBoolean("/NeedAppearances", false);
        using var output = new MemoryStream(); document.Save(output, false); return output.ToArray();
    }

    private static PdfItem Dereference(PdfItem item) => item is PdfReference reference ? reference.Value : item;

    private static byte[] CreateFixture(byte[] bytes, string[] optionsText, string requiredText, out int mapped)
    {
        var fontType = typeof(PdfEditSession).Assembly.GetType("NT.AvaloniaControl.PdfViewer.Editing.PdfTrueTypeFont")!;
        var fontInfo = Activator.CreateInstance(fontType, BindingFlags.Instance | BindingFlags.NonPublic, null, [bytes], null)!;
        var lookup = fontType.GetMethod("GetGlyphIndex", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var character in requiredText.Distinct().Where(character => character != '\r'))
            if ((int)lookup.Invoke(fontInfo, [(int)character])! == 0) throw new ArgumentException($"The chosen full font has no glyph for U+{(int)character:X4}; fallback is forbidden.");
        var cidToGid = new byte[65536 * 2];
        var mappings = new List<int>();
        for (var code = 0x20; code <= 0xFFFF; code++)
        {
            if (code is >= 0xD800 and <= 0xDFFF) continue;
            var glyph = (int)lookup.Invoke(fontInfo, [code])!;
            if (glyph == 0) continue;
            BinaryPrimitives.WriteUInt16BigEndian(cidToGid.AsSpan(code * 2, 2), checked((ushort)glyph));
            mappings.Add(code);
        }
        mapped = mappings.Count;
        var cmap = new StringBuilder("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n/CMapName /NTCjkFormUnicode def\n/CMapType 2 def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
        foreach (var group in mappings.Chunk(100))
        {
            cmap.Append(group.Length).Append(" beginbfchar\n");
            foreach (var code in group) cmap.Append('<').Append(code.ToString("X4", CultureInfo.InvariantCulture)).Append("> <").Append(code.ToString("X4", CultureInfo.InvariantCulture)).Append(">\n");
            cmap.Append("endbfchar\n");
        }
        cmap.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
        var document = Native.FPDF_CreateNewDocument();
        byte[] scratch;
        try
        {
            var page = Native.FPDFPage_New(document, 0, 400, 400);
            try
            {
                var font = Native.FPDFText_LoadCidType2Font(document, bytes, checked((uint)bytes.Length), cmap.ToString(), cidToGid, (uint)cidToGid.Length);
                if (font == IntPtr.Zero) throw new InvalidDataException("Cannot load complete BMP CID form font.");
                try
                {
                    var text = Native.FPDFPageObj_CreateTextObj(document, font, 14);
                    var codes = "KEEP CJK FORM BASELINE".Select(c => (uint)c).ToArray();
                    if (Native.FPDFText_SetCharcodes(text, codes, (nuint)codes.Length) == 0) throw new InvalidDataException("Cannot set form fixture baseline text.");
                    Native.FPDFPageObj_Transform(text, 1, 0, 0, 1, 40, 365);
                    Native.FPDFPage_InsertObject(page, text);
                    if (Native.FPDFPage_GenerateContent(page) == 0) throw new InvalidDataException("Cannot generate form fixture content.");
                    using var stream = new MemoryStream();
                    if (!SaveNative.Save(document, stream).Success) throw new InvalidDataException("Cannot save full font scratch.");
                    scratch = stream.ToArray();
                }
                finally { Native.FPDFFont_Close(font); }
            }
            finally { SaveNative.FPDF_ClosePage(page); }
        }
        finally { SaveNative.FPDF_CloseDocument(document); }
        using var input = new MemoryStream(scratch, false);
        using var target = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        var targetPage = target.Pages[0];
        var fonts = targetPage.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/Font")!;
        var fontResource = fonts.Elements[fonts.Elements.KeyNames.Single()];
        var formFonts = new PdfDictionary(target);
        formFonts.Elements["/Cjk"] = fontResource;
        var resources = new PdfDictionary(target);
        resources.Elements["/Font"] = formFonts;
        var form = new PdfDictionary(target);
        var fields = new PdfArray(target);
        form.Elements["/Fields"] = fields;
        form.Elements["/DR"] = resources;
        form.Elements.SetString("/DA", "/Cjk 16 Tf 0 g");
        form.Elements.SetBoolean("/NeedAppearances", false);
        target.Internals.Catalog.Elements["/AcroForm"] = form;
        var annotations = new PdfArray(target);
        targetPage.Elements["/Annots"] = annotations;
        AddField(target, fields, annotations, targetPage, resources, "CjkText", "/Tx", 0, [40, 300, 360, 330]);
        AddField(target, fields, annotations, targetPage, resources, "CjkMultiline", "/Tx", 4096, [40, 180, 360, 270]);
        var list = AddField(target, fields, annotations, targetPage, resources, "CjkList", "/Ch", 0, [40, 60, 250, 155]);
        var options = new PdfArray(target);
        foreach (var option in optionsText) options.Elements.Add(new PdfString(option, PdfStringEncoding.Unicode));
        list.Elements["/Opt"] = options;
        list.Elements["/V"] = new PdfString(optionsText[0], PdfStringEncoding.Unicode);
        list.Elements["/I"] = new PdfArray(target, new PdfInteger(0));
        var initialList = new StringBuilder("q 1 g 0 0 210 95 re f 0 G 1 w 0.5 0.5 209 94 re S\nBT /Cjk 16 Tf 0 g\n");
        for (var index = 0; index < optionsText.Length; index++)
            initialList.Append("1 0 0 1 4 ").Append(75 - index * 22).Append(" Tm <")
                .Append(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(optionsText[index]))).Append("> Tj\n");
        initialList.Append("ET Q\n");
        list.Elements.GetDictionary("/AP")!.Elements.GetDictionary("/N")!.Stream.Value = Encoding.ASCII.GetBytes(initialList.ToString());
        using var output = new MemoryStream();
        target.Save(output, false);
        return output.ToArray();
    }

    private static PdfDictionary AddField(SharpDocument document, PdfArray fields, PdfArray annotations, PdfPage page,
        PdfDictionary resources, string name, string type, int flags, double[] rectangle)
    {
        var field = new PdfDictionary(document);
        document.Internals.AddObject(field);
        field.Elements.SetName("/Type", "/Annot"); field.Elements.SetName("/Subtype", "/Widget");
        field.Elements.SetName("/FT", type); field.Elements.SetString("/T", name);
        field.Elements.SetString("/V", ""); field.Elements.SetInteger("/F", 4); field.Elements.SetInteger("/Ff", flags);
        field.Elements["/Rect"] = new PdfArray(document, rectangle.Select(value => (PdfItem)new PdfReal(value)).ToArray());
        field.Elements["/P"] = page.Reference!; field.Elements.SetString("/DA", "/Cjk 16 Tf 0 g");
        var border = new PdfDictionary(document); border.Elements.SetInteger("/W", 1); border.Elements.SetName("/S", "/S"); field.Elements["/BS"] = border;
        var mk = new PdfDictionary(document); mk.Elements["/BC"] = new PdfArray(document, new PdfInteger(0)); mk.Elements["/BG"] = new PdfArray(document, new PdfInteger(1)); field.Elements["/MK"] = mk;
        var ap = new PdfDictionary(document); var normal = new PdfDictionary(document); document.Internals.AddObject(normal);
        normal.Elements.SetName("/Type", "/XObject"); normal.Elements.SetName("/Subtype", "/Form");
        normal.Elements["/BBox"] = new PdfArray(document, new PdfInteger(0), new PdfInteger(0), new PdfReal(rectangle[2] - rectangle[0]), new PdfReal(rectangle[3] - rectangle[1]));
        normal.Elements["/Resources"] = resources;
        normal.CreateStream(Encoding.ASCII.GetBytes(FormattableString.Invariant($"q 1 g 0 0 {rectangle[2] - rectangle[0]} {rectangle[3] - rectangle[1]} re f 0 G 1 w 0.5 0.5 {rectangle[2] - rectangle[0] - 1} {rectangle[3] - rectangle[1] - 1} re S Q\n")));
        ap.Elements["/N"] = normal.Reference!; field.Elements["/AP"] = ap;
        fields.Elements.Add(field.Reference!); annotations.Elements.Add(field.Reference!);
        return field;
    }

    private sealed record State(string Name, int Type, string Value, int[] SelectedIndices, bool HasAppearance);
    private sealed class Host : IDisposable
    {
        private static readonly Type HostType = typeof(FormsProbe).GetNestedType("FormHost", BindingFlags.NonPublic)!;
        private readonly object _host;
        internal IntPtr Handle => (IntPtr)HostType.GetProperty("Handle")!.GetValue(_host)!;
        internal IntPtr Page => (IntPtr)HostType.GetProperty("Page")!.GetValue(_host)!;
        internal Host(byte[] bytes) => _host = Activator.CreateInstance(HostType, BindingFlags.Instance | BindingFlags.Public, null, [bytes], null)!;
        internal void Click(double x, double y) => HostType.GetMethod("FocusAndClick")!.Invoke(_host, ["cjk", x, y]);
        internal int Type(string value)
        {
            Native.FORM_SelectAllText(Handle, Page);
            Native.FORM_ReplaceSelection(Handle, Page, Encoding.Unicode.GetBytes("\0"));
            var accepted = 0;
            foreach (var character in value)
            {
                if (character == '\r') Native.FORM_OnKeyDown(Handle, Page, 0x0D, 0);
                if (Native.FORM_OnChar(Handle, Page, character, 0) != 0) accepted++;
            }
            return accepted;
        }
        internal State[] Fields() => ((Array)HostType.GetMethod("ReadFields")!.Invoke(_host, null)!).Cast<object>().Select(value =>
        {
            var t = value.GetType(); object Read(string key) => t.GetProperty(key)!.GetValue(value)!;
            return new State((string)Read("Name"), (int)Read("Type"), (string)Read("Value"), (int[])Read("SelectedIndices"), (bool)Read("HasAppearance"));
        }).ToArray();
        internal void Save(string path) => HostType.GetMethod("Save")!.Invoke(_host, [path]);
        public void Dispose() => ((IDisposable)_host).Dispose();
    }

    private static class Native
    {
        private const string Library = "pdfium";
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_CreateNewDocument();
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPage_New(IntPtr doc, int index, double width, double height);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFText_LoadCidType2Font(IntPtr document, byte[] data, uint size, [MarshalAs(UnmanagedType.LPStr)] string toUnicode, byte[] cidToGid, uint cidToGidBytes);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFFont_Close(IntPtr font);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPageObj_CreateTextObj(IntPtr doc, IntPtr font, float size);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFText_SetCharcodes(IntPtr text, uint[] codes, nuint count);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPageObj_Transform(IntPtr obj, double a, double b, double c, double d, double e, double f);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPage_InsertObject(IntPtr page, IntPtr obj);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_GenerateContent(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FORM_OnChar(IntPtr form, IntPtr page, int character, int modifier);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FORM_OnKeyDown(IntPtr form, IntPtr page, int key, int modifier);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FORM_ForceToKillFocus(IntPtr form);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FORM_SelectAllText(IntPtr form, IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FORM_ReplaceSelection(IntPtr form, IntPtr page, byte[] text);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FORM_SetIndexSelected(IntPtr form, IntPtr page, int index, int selected);
    }
}
