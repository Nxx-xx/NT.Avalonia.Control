using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace PdfViewer.P0;

/// <summary>
/// P0 research only. This does not expose a production redaction API. Native edit
/// success and text disappearance are intentionally reported separately from the
/// independent all-object and decoded-stream audit performed by the runner.
/// </summary>
internal static class RedactionProbe
{
    private const string TextSecret = "P0_TEXT_SECRET_7B912F";
    private const string ImageSecret = "P0_IMAGE_SECRET_58AC3D";
    private const string FormSecret = "P0_FORM_SECRET_D2671A";
    private const string HiddenSecret = "P0_HIDDEN_SECRET_8C47E2";
    private const string OldSecret = "P0_OLD_REVISION_SECRET_19DF40";
    private const string PartialSecret = "P0_CHAR_SECRET_493E2B";

    internal static object Run(string runDirectory)
    {
        Directory.CreateDirectory(runDirectory);
        List<object> cases = [];

        cases.Add(RunRemoval(runDirectory, "whole-text", CreateSimple(TextSecret, false),
            TextSecret, 1, "KEEP_VISIBLE_TEXT"));
        cases.Add(RunRemoval(runDirectory, "whole-image", CreateSimple("KEEP_VISIBLE_TEXT", true),
            ImageSecret, 3, "KEEP_VISIBLE_TEXT"));
        cases.Add(RunRemoval(runDirectory, "whole-form", CreateWholeForm(),
            FormSecret, 5, "KEEP_VISIBLE_TEXT"));
        cases.Add(RunHidden(runDirectory));
        cases.Add(RunNestedForm(runDirectory));
        cases.Add(RunRefusal(runDirectory, "partial-character",
            CreateSimple($"KEEP_LEFT {PartialSecret} KEEP_RIGHT", false), PartialSecret,
            "A character range within one text object is not a whole-object deletion. " +
            "Reject until glyph rewriting, advances, shaping, and preservation of adjacent text are validated."));
        cases.Add(RunRefusal(runDirectory, "partial-image",
            CreateSimple("KEEP_VISIBLE_TEXT", true), ImageSecret,
            "Reject a crop within an image until pixel sanitization and replacement of every underlying resource are verified."));

        return new
        {
            Id = "POC-04", Status = "partial-native-primitive-verified-security-audit-required",
            NativeLibrary = "bblanchon.PDFium 155.0.8044", SaveFlags = 2,
            SaveMode = "FPDF_NO_INCREMENTAL; never append a redaction as an incremental revision",
            Cases = cases,
            Limitations = new[]
            {
                "No production redaction feature is implemented or enabled by this probe.",
                "Deleting a native page object does not itself prove all recoverable bytes are gone.",
                "Native candidate files are research evidence, not documents certified safe to disclose.",
                "Metadata, outlines, attachments, structure, thumbnails, unused resources, shared Form XObjects, and previous revisions require an independent decoded-object audit.",
                "Partial text, partial image, arbitrary vector paths, clipping, transparency, and signed/encrypted redaction need separate implementations and acceptance cases.",
                "The product must reject unsupported scopes before mutating the document and must fail closed if sanitization cannot be proven."
            }
        };
    }

    private static object RunRemoval(string directory, string name, byte[] input, string secret, int removeType, string preserved)
    {
        string inputPath = Write(directory, name + ".input.pdf", input);
        string outputPath = Path.Combine(directory, name + ".native-candidate.pdf");
        using Document document = new(input);
        IntPtr page = N.FPDF_LoadPage(document.Handle, 0);
        if (page == IntPtr.Zero) throw new InvalidOperationException("Cannot load redaction fixture page.");
        string before;
        int beforeObjects;
        int afterObjects;
        bool removed = false;
        bool generated;
        try
        {
            before = ReadText(page);
            beforeObjects = N.FPDFPage_CountObjects(page);
            IntPtr target = IntPtr.Zero;
            // The authored simple fixture has one secret text object and one
            // retained object, or one image/Form plus retained text. Do not select an
            // arbitrary object in an untrusted production document this way.
            for (int i = 0; i < beforeObjects; ++i)
            {
                IntPtr item = N.FPDFPage_GetObject(page, i);
                if (N.FPDFPageObj_GetType(item) != removeType) continue;
                if (removeType is 3 or 5 || GetObjectText(page, item).Contains(secret, StringComparison.Ordinal))
                {
                    target = item;
                    break;
                }
            }
            if (target != IntPtr.Zero)
            {
                removed = N.FPDFPage_RemoveObject(page, target) != 0;
                if (removed) N.FPDFPageObj_Destroy(target);
            }
            generated = N.FPDFPage_GenerateContent(page) != 0;
            afterObjects = N.FPDFPage_CountObjects(page);
            Save(document.Handle, outputPath);
        }
        finally { N.FPDF_ClosePage(page); }
        string after = TextOf(File.ReadAllBytes(outputPath), 0);
        byte[] saved = File.ReadAllBytes(outputPath);
        return new
        {
            Name = name, Input = inputPath, Output = outputPath, Secret = secret,
            Scope = "whole top-level page object", BeforeText = before, AfterText = after,
            SelectionMethod = removeType == 5
                ? "Entire exclusive top-level Form XObject selected by authored fixture object identity; no region hit-testing algorithm is implemented."
                : "Authored fixture object identity; no region hit-testing algorithm is implemented.",
            BeforeObjects = beforeObjects, AfterObjects = afterObjects,
            Checks = new[]
            {
                Check("native-remove-returned-success", removed),
                Check("generate-content-returned-success", generated),
                Check("object-count-decreased-by-one", afterObjects == beforeObjects - 1),
                Check("reopened-native-text-excludes-secret", !after.Contains(secret, StringComparison.Ordinal)),
                Check("reopened-native-text-retains-control", after.Contains(preserved, StringComparison.Ordinal)),
                Check("raw-bytes-exclude-ascii-secret-not-a-complete-security-check", !Contains(saved, secret))
            },
            InputSha256 = Hash(input), OutputSha256 = Hash(saved),
            SecurityStatus = "pending-independent-decoded-stream-and-all-object-audit"
        };
    }

    private static object RunHidden(string directory)
    {
        // The active page contains TextSecret, a previous revision contains
        // OldSecret, and non-page surfaces contain HiddenSecret.
        byte[] input = CreateHiddenRevision();
        string inputPath = Write(directory, "hidden-revision.input.pdf", input);
        string outputPath = Path.Combine(directory, "hidden-revision.native-candidate.pdf");
        using Document document = new(input);
        IntPtr page = N.FPDF_LoadPage(document.Handle, 0);
        bool removed = false;
        bool generated;
        try
        {
            for (int i = N.FPDFPage_CountObjects(page) - 1; i >= 0; --i)
            {
                IntPtr item = N.FPDFPage_GetObject(page, i);
                if (N.FPDFPageObj_GetType(item) != 1 ||
                    !GetObjectText(page, item).Contains(TextSecret, StringComparison.Ordinal)) continue;
                removed = N.FPDFPage_RemoveObject(page, item) != 0;
                if (removed) N.FPDFPageObj_Destroy(item);
            }
            generated = N.FPDFPage_GenerateContent(page) != 0;
            Save(document.Handle, outputPath);
        }
        finally { N.FPDF_ClosePage(page); }
        byte[] output = File.ReadAllBytes(outputPath);
        string after = TextOf(output, 0);
        return new
        {
            Name = "hidden-and-previous-revision", Input = inputPath, Output = outputPath,
            Secrets = new { ActiveText = TextSecret, HiddenSurfaces = HiddenSecret, PreviousRevision = OldSecret },
            HiddenSurfaces = new[] { "Info.Subject", "Catalog.Metadata XMP", "Page.Thumb", "Outlines.Title", "EmbeddedFiles stream", "StructTreeRoot.ActualText" },
            Checks = new[]
            {
                Check("native-remove-returned-success", removed),
                Check("generate-content-returned-success", generated),
                Check("reopened-native-text-excludes-active-secret", !after.Contains(TextSecret, StringComparison.Ordinal)),
                Check("reopened-native-text-retains-control", after.Contains("KEEP_VISIBLE_TEXT", StringComparison.Ordinal)),
                Check("input-has-previous-revision-marker", Contains(input, OldSecret)),
                Check("raw-rewrite-excludes-previous-revision-marker-not-decompression-proof", !Contains(output, OldSecret))
            },
            RawHiddenMarkerRetained = Contains(output, HiddenSecret), AfterText = after,
            InputSha256 = Hash(input), OutputSha256 = Hash(output),
            SecurityStatus = "not-approved-hidden-sanitization-is-not-implemented",
            Decision = "Reject hidden-data redaction through this native-only path. The candidate is retained only for independent inspection."
        };
    }

    private static object RunNestedForm(string directory)
    {
        byte[] input = CreateNestedForm();
        string inputPath = Write(directory, "shared-nested-form.input.pdf", input);
        string outputPath = Path.Combine(directory, "shared-nested-form.native-candidate.pdf");
        using Document document = new(input);
        IntPtr page = N.FPDF_LoadPage(document.Handle, 0);
        bool removed = false;
        bool generated;
        int depth = -1;
        try
        {
            for (int i = 0; i < N.FPDFPage_CountObjects(page); ++i)
            {
                IntPtr item = N.FPDFPage_GetObject(page, i);
                if (N.FPDFPageObj_GetType(item) == 5 && RemoveInForm(page, item, 1, out depth))
                { removed = true; break; }
            }
            generated = N.FPDFPage_GenerateContent(page) != 0;
            Save(document.Handle, outputPath);
        }
        finally { N.FPDF_ClosePage(page); }
        byte[] output = File.ReadAllBytes(outputPath);
        string page0 = TextOf(output, 0);
        string page1 = TextOf(output, 1);
        return new
        {
            Name = "shared-nested-form", Input = inputPath, Output = outputPath, Secret = FormSecret,
            Scope = "attempt to delete a text child inside a nested Form XObject referenced on two pages",
            NativeChildRemovalReturnedSuccess = removed, NativeGenerateReturnedSuccess = generated,
            NestedDepth = depth, ReopenedPage0Text = page0, ReopenedPage1Text = page1,
            Page0SecretPresent = page0.Contains(FormSecret, StringComparison.Ordinal),
            Page1SecretPresent = page1.Contains(FormSecret, StringComparison.Ordinal),
            RawSecretMarkerRetained = Contains(output, FormSecret),
            InputSha256 = Hash(input), OutputSha256 = Hash(output),
            SecurityStatus = "not-approved-shared-form-scope-and-resource-rewrite-require-validation",
            Decision = "Reject shared/nested Form partial redaction in the initial product. Native mutation success must not be interpreted as durable or safely scoped deletion."
        };
    }

    private static bool RemoveInForm(IntPtr page, IntPtr form, int depth, out int removedDepth)
    {
        removedDepth = -1;
        for (uint i = 0; i < N.FPDFFormObj_CountObjects(form); ++i)
        {
            IntPtr child = N.FPDFFormObj_GetObject(form, i);
            int type = N.FPDFPageObj_GetType(child);
            if (type == 1 && GetObjectText(page, child).Contains(FormSecret, StringComparison.Ordinal))
            {
                bool removed = N.FPDFFormObj_RemoveObject(form, child) != 0;
                if (removed) { N.FPDFPageObj_Destroy(child); removedDepth = depth; }
                return removed;
            }
            if (type == 5 && depth < 8 && RemoveInForm(page, child, depth + 1, out removedDepth)) return true;
        }
        return false;
    }

    private static object RunRefusal(string directory, string name, byte[] input, string secret, string reason)
    {
        string path = Write(directory, name + ".input.pdf", input);
        return new
        {
            Name = name, Input = path, Secret = secret, InputSha256 = Hash(input),
            Decision = "rejected-before-mutation", Output = (string?)null,
            Reason = reason, InputUnchangedSha256 = Hash(File.ReadAllBytes(path)),
            SecurityStatus = "explicitly-unsupported-no-redacted-output-produced"
        };
    }

    private static byte[] CreateSimple(string text, bool image)
    {
        string content = $"BT /F1 14 Tf 30 245 Td ({Escape(text)}) Tj ET\n";
        if (text == TextSecret) content += "BT /F1 14 Tf 30 210 Td (KEEP_VISIBLE_TEXT) Tj ET\n";
        if (image) content += "q 210 0 0 45 30 120 cm /Im1 Do Q\n";
        List<string> objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 4 0 R >> " +
                (image ? "/XObject << /Im1 6 0 R >> " : "") + ">> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream(content)
        ];
        if (image) objects.Add(Image(ImageSecret));
        return Pdf(objects);
    }

    private static byte[] CreateHiddenRevision()
    {
        string oldText = $"BT /F1 14 Tf 30 245 Td ({OldSecret}) Tj ET\nBT /F1 14 Tf 30 210 Td (KEEP_VISIBLE_TEXT) Tj ET\n";
        List<string> objects =
        [
            "<< /Type /Catalog /Pages 2 0 R /Metadata 7 0 R /Outlines 9 0 R /Names << /EmbeddedFiles << /Names [(evidence.txt) 11 0 R] >> >> /StructTreeRoot 13 0 R /MarkInfo << /Marked true >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R /Thumb 8 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream(oldText),
            $"<< /Producer (P0 generated fixture) /Subject ({HiddenSecret}) >>",
            Stream($"<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><secret>{HiddenSecret}</secret></x:xmpmeta>\n", "/Type /Metadata /Subtype /XML"),
            Image(HiddenSecret),
            "<< /Type /Outlines /First 10 0 R /Last 10 0 R /Count 1 >>",
            $"<< /Title ({HiddenSecret}) /Parent 9 0 R /Dest [3 0 R /Fit] >>",
            "<< /Type /Filespec /F (evidence.txt) /EF << /F 12 0 R >> >>",
            Stream(HiddenSecret + "\n", "/Type /EmbeddedFile /Subtype /text#2Fplain"),
            $"<< /Type /StructTreeRoot /K [14 0 R] >>",
            $"<< /Type /StructElem /S /Span /P 13 0 R /Pg 3 0 R /ActualText ({HiddenSecret}) >>"
        ];
        byte[] original = Pdf(objects, " /Info 6 0 R");
        long previousXref = ParseStartXref(original);
        using MemoryStream result = new();
        result.Write(original);
        long offset = result.Position;
        WriteAscii(result, "5 0 obj\n" + Stream($"BT /F1 14 Tf 30 245 Td ({TextSecret}) Tj ET\nBT /F1 14 Tf 30 210 Td (KEEP_VISIBLE_TEXT) Tj ET\n") + "\nendobj\n");
        long xref = result.Position;
        WriteAscii(result, $"xref\n5 1\n{offset:D10} 00000 n \ntrailer\n<< /Size 15 /Root 1 0 R /Info 6 0 R /Prev {previousXref} >>\nstartxref\n{xref}\n%%EOF\n");
        return result.ToArray();
    }

    private static byte[] CreateNestedForm()
    {
        List<string> objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 5 0 R >> /XObject << /Top 8 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 5 0 R >> /XObject << /Top 9 0 R >> >> /Contents 10 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream("BT /F1 14 Tf 30 270 Td (KEEP_PAGE_ZERO) Tj ET\n/Top Do\n"),
            Stream($"BT /F1 14 Tf 30 220 Td ({FormSecret}) Tj ET\nBT /F1 14 Tf 30 190 Td (KEEP_NESTED_TEXT) Tj ET\n",
                "/Type /XObject /Subtype /Form /FormType 1 /BBox [0 0 300 300] /Resources << /Font << /F1 5 0 R >> >>"),
            Stream("/Inner Do\n", "/Type /XObject /Subtype /Form /FormType 1 /BBox [0 0 300 300] /Resources << /XObject << /Inner 7 0 R >> >>"),
            Stream("/Inner Do\n", "/Type /XObject /Subtype /Form /FormType 1 /BBox [0 0 300 300] /Resources << /XObject << /Inner 7 0 R >> >>"),
            Stream("BT /F1 14 Tf 30 270 Td (KEEP_PAGE_ONE) Tj ET\n/Top Do\n")
        ];
        return Pdf(objects);
    }

    private static byte[] CreateWholeForm()
    {
        // Both forms belong exclusively to this page. The entire outer Form
        // BBox [20 80 280 250] is selected; retained page text lies above it.
        // This is distinct from trying to mutate a text child inside a shared
        // Form, which is the intentionally unsafe partial-form counterexample.
        List<string> objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 4 0 R >> /XObject << /Target 6 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream("BT /F1 14 Tf 30 270 Td (KEEP_VISIBLE_TEXT) Tj ET\n/Target Do\n"),
            Stream("/Inner Do\n", "/Type /XObject /Subtype /Form /FormType 1 /BBox [20 80 280 250] /Resources << /XObject << /Inner 7 0 R >> >>"),
            Stream($"BT /F1 14 Tf 30 220 Td ({FormSecret}) Tj ET\nBT /F1 10 Tf 30 190 Td (FORM_REGION_SELECTED_FOR_REMOVAL) Tj ET\n",
                "/Type /XObject /Subtype /Form /FormType 1 /BBox [20 80 280 250] /Resources << /Font << /F1 4 0 R >> >>")
        ];
        return Pdf(objects);
    }

    private static string Image(string marker)
    {
        // Valid 10x1 RGB image. The uncompressed pixel payload deliberately
        // contains a marker so an unused image resource cannot pass unnoticed.
        string pixels = marker.PadRight(30, '_')[..30];
        return Stream(pixels, "/Type /XObject /Subtype /Image /Width 10 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8");
    }

    private static byte[] Pdf(List<string> objects, string trailer = "")
    {
        using MemoryStream stream = new();
        WriteAscii(stream, "%PDF-1.7\n% P0 independently authored fixture\n");
        List<long> offsets = [0];
        for (int i = 0; i < objects.Count; ++i)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        long xref = stream.Position;
        WriteAscii(stream, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) WriteAscii(stream, $"{offset:D10} 00000 n \n");
        WriteAscii(stream, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R{trailer} >>\nstartxref\n{xref}\n%%EOF\n");
        return stream.ToArray();
    }

    private static long ParseStartXref(byte[] pdf)
    {
        string text = Encoding.ASCII.GetString(pdf);
        return long.Parse(text[(text.LastIndexOf("startxref", StringComparison.Ordinal) + 9)..]
            .Trim().Split('\n')[0], CultureInfo.InvariantCulture);
    }

    private static string Stream(string content, string entries = "") =>
        $"<< /Length {Encoding.ASCII.GetByteCount(content)} {entries} >>\nstream\n{content}\nendstream";
    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
    private static void WriteAscii(Stream stream, string value) => stream.Write(Encoding.ASCII.GetBytes(value));
    private static string Write(string directory, string name, byte[] bytes)
    { string path = Path.Combine(directory, name); File.WriteAllBytes(path, bytes); return path; }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static bool Contains(byte[] data, string marker) => Encoding.Latin1.GetString(data).Contains(marker, StringComparison.Ordinal);
    private static object Check(string name, bool passed) => new { Name = name, Passed = passed };

    private static string TextOf(byte[] bytes, int index)
    {
        using Document document = new(bytes);
        IntPtr page = N.FPDF_LoadPage(document.Handle, index);
        if (page == IntPtr.Zero) throw new InvalidOperationException("Cannot reopen redaction candidate.");
        try { return ReadText(page); }
        finally { N.FPDF_ClosePage(page); }
    }

    private static string ReadText(IntPtr page)
    {
        IntPtr text = N.FPDFText_LoadPage(page);
        if (text == IntPtr.Zero) return "";
        try
        {
            StringBuilder result = new();
            int count = N.FPDFText_CountChars(text);
            for (int i = 0; i < count; ++i)
            {
                uint code = N.FPDFText_GetUnicode(text, i);
                if (code is > 0 and <= 0x10FFFF && code is not (>= 0xD800 and <= 0xDFFF))
                    result.Append(char.ConvertFromUtf32((int)code));
            }
            return result.ToString();
        }
        finally { N.FPDFText_ClosePage(text); }
    }

    private static string GetObjectText(IntPtr page, IntPtr item)
    {
        IntPtr text = N.FPDFText_LoadPage(page);
        if (text == IntPtr.Zero) return "";
        try
        {
            nuint size = OperatingSystem.IsWindows()
                ? N.FPDFTextObj_GetTextWindows(item, text, IntPtr.Zero, 0)
                : N.FPDFTextObj_GetTextUnix(item, text, IntPtr.Zero, 0);
            if (size == 0 || size > 1_048_576) return "";
            IntPtr buffer = Marshal.AllocHGlobal(checked((int)size));
            try
            {
                if (OperatingSystem.IsWindows()) N.FPDFTextObj_GetTextWindows(item, text, buffer, (uint)size);
                else N.FPDFTextObj_GetTextUnix(item, text, buffer, size);
                return Marshal.PtrToStringUni(buffer) ?? "";
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        finally { N.FPDFText_ClosePage(text); }
    }

    private static void Save(IntPtr document, string path)
    {
        using MemoryStream stream = new();
        Exception? callbackError = null;
        int WriteBlock(IntPtr _, IntPtr data, nuint size)
        {
            try
            {
                byte[] buffer = new byte[checked((int)size)];
                Marshal.Copy(data, buffer, 0, buffer.Length);
                stream.Write(buffer);
                return 1;
            }
            catch (Exception error) { callbackError = error; return 0; }
        }
        N.WriteBlockWindows windows = (self, data, size) => WriteBlock(self, data, size);
        N.WriteBlockUnix unix = WriteBlock;
        Delegate callback = OperatingSystem.IsWindows() ? windows : unix;
        N.FileWrite writer = new() { Version = 1, WriteBlock = Marshal.GetFunctionPointerForDelegate(callback) };
        int success = N.FPDF_SaveAsCopy(document, ref writer, 2);
        GC.KeepAlive(callback);
        if (success == 0 || callbackError != null) throw new IOException("Native full rewrite failed.", callbackError);
        File.WriteAllBytes(path, stream.ToArray());
    }

    private sealed class Document : IDisposable
    {
        private GCHandle _pin;
        internal IntPtr Handle { get; }
        internal Document(byte[] bytes)
        {
            _pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Handle = N.FPDF_LoadMemDocument64(_pin.AddrOfPinnedObject(), (nuint)bytes.Length, IntPtr.Zero);
            if (Handle == IntPtr.Zero) { _pin.Free(); throw new InvalidOperationException("Native fixture load failed."); }
        }
        public void Dispose() { N.FPDF_CloseDocument(Handle); _pin.Free(); }
    }

    private static class N
    {
        private const string Library = "pdfium";
        [StructLayout(LayoutKind.Sequential)] internal struct FileWrite { internal int Version; internal IntPtr WriteBlock; }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int WriteBlockWindows(IntPtr self, IntPtr data, uint size);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int WriteBlockUnix(IntPtr self, IntPtr data, nuint size);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_LoadMemDocument64(IntPtr bytes, nuint size, IntPtr password);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_CloseDocument(IntPtr document);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_LoadPage(IntPtr document, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_ClosePage(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFText_LoadPage(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFText_ClosePage(IntPtr text);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFText_CountChars(IntPtr text);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern uint FPDFText_GetUnicode(IntPtr text, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_CountObjects(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPage_GetObject(IntPtr page, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPageObj_GetType(IntPtr item);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_RemoveObject(IntPtr page, IntPtr item);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPageObj_Destroy(IntPtr item);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_GenerateContent(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFFormObj_CountObjects(IntPtr form);
        internal static IntPtr FPDFFormObj_GetObject(IntPtr form, uint index) =>
            FPDFFormObj_GetObjectNative(form, new CULong(index));
        [DllImport(Library, EntryPoint = "FPDFFormObj_GetObject", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr FPDFFormObj_GetObjectNative(IntPtr form, CULong index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFFormObj_RemoveObject(IntPtr form, IntPtr item);
        [DllImport(Library, EntryPoint = "FPDFTextObj_GetText", CallingConvention = CallingConvention.Winapi)] internal static extern uint FPDFTextObj_GetTextWindows(IntPtr item, IntPtr text, IntPtr buffer, uint size);
        [DllImport(Library, EntryPoint = "FPDFTextObj_GetText", CallingConvention = CallingConvention.Winapi)] internal static extern nuint FPDFTextObj_GetTextUnix(IntPtr item, IntPtr text, IntPtr buffer, nuint size);
        // FPDF_DWORD is C unsigned long: 32 bits on Windows, 64 bits on
        // Linux x64/ARM64. CULong preserves that ABI rather than assuming uint.
        internal static int FPDF_SaveAsCopy(IntPtr document, ref FileWrite writer, uint flags) =>
            FPDF_SaveAsCopyNative(document, ref writer, new CULong(flags));
        [DllImport(Library, EntryPoint = "FPDF_SaveAsCopy", CallingConvention = CallingConvention.Winapi)]
        private static extern int FPDF_SaveAsCopyNative(IntPtr document, ref FileWrite writer, CULong flags);
    }
}
