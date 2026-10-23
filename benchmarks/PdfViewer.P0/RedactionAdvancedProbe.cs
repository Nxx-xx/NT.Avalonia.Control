using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace PdfViewer.P0;

/// <summary>
/// Research evidence for narrowly specified edit primitives. None of these
/// fixture operations are exposed as a general redaction API.
/// </summary>
internal static class RedactionAdvancedProbe
{
    private const string Secret = "P0_RANGE_SECRET_931A";
    private const string FormSecret = "P0_SHARED_SECRET_74CF";
    private const string HiddenSecret = "P0_HIDDEN_ANNOT_FORM_48D2";
    private const string Left = "KEEP_LEFT";
    private const string Right = "KEEP_RIGHT";

    internal static object Run(string directory)
    {
        Directory.CreateDirectory(directory);
        object[] cases =
        [
            RewriteText(directory, false),
            RewriteText(directory, true),
            RemoveSharedInstance(directory),
            RemovePath(directory),
            HiddenAnnotationFormLeak(directory),
            Refuse(directory, "ligature-character-refusal", LigatureFixture(),
                "A ToUnicode entry maps one source charcode to three Unicode characters (ffi). " +
                "Removing a single f is a glyph rewrite, not a substring replacement. " +
                "This extraction-only fixture does not provide an embedded font or prove its visual appearance."),
            Refuse(directory, "partial-path-refusal", PathFixture(),
                "The selected region intersects part of one painted path. Splitting arbitrary curves, " +
                "strokes, clipping, winding rules and transparency has not been implemented or validated.")
        ];
        return new
        {
            Id = "POC-04-advanced", Status = "restricted-native-research-not-production-redaction",
            SaveMode = "full rewrite, FPDF_NO_INCREMENTAL=2", Cases = cases,
            HiddenDataPolicy = new
            {
                Status = "must-be-audited-by-structural-writer", RequiresFreshRewrite = true,
                RequiredSurfaces = new[] { "Info", "XMP Metadata", "Page.Thumb", "Outlines", "EmbeddedFiles",
                    "StructTreeRoot and ActualText", "annotation Contents and AP", "AcroForm values/defaults/AP",
                    "unused page/Form resources", "unreachable xref objects", "old incremental revisions" },
                Decision = "Native page-object edits alone do not authorize a clean/safe redaction result. " +
                    "The independent audit must decode every current xref stream, parse content operands, " +
                    "inspect retained pages, and reject unknown or undecodable surfaces."
            },
            Limitations = new[]
            {
                "All target object identities and ranges are authored fixture knowledge; no general selection engine is implemented.",
                "ASCII and standard Helvetica are the only positive partial-text scope in this probe.",
                "Position preservation is measured separately from text disappearance. SetText success alone is insufficient.",
                "Shared Form instance removal is page-scoped: retained pages must still contain the shared content.",
                "A global secret-marker absence rule would wrongly fail the valid shared-instance result and cannot prove page scope.",
                "No claim is made for Type3, vertical/RTL/complex scripts, arbitrary font encodings, glyph clusters, partial images/paths, signatures or encryption."
            }
        };
    }

    private static object RewriteText(string directory, bool positions)
    {
        string name = positions ? "partial-text-preserve-positions" : "partial-text-naive-settext";
        string source = $"{Left} {Secret} {Right}";
        byte[] input = TextFixture(source);
        string inputPath = Write(directory, name + ".input.pdf", input);
        string outputPath = Path.Combine(directory, name + ".native-candidate.pdf");
        using Doc document = new(input);
        IntPtr page = N.FPDF_LoadPage(document.Handle, 0);
        bool setText;
        bool setPositions = false;
        bool generated;
        double[] originalRight;
        double[] originalLeft;
        try
        {
            IntPtr textObject = N.FPDFPage_GetObject(page, 0);
            if (N.FPDFPageObj_GetType(textObject) != 1) throw new InvalidOperationException("Expected one text object.");
            string objectText = ObjectText(page, textObject);
            if (objectText != source) throw new InvalidOperationException("The authored range must match exact source text.");
            int removeAt = source.IndexOf(Secret, StringComparison.Ordinal);
            int rightIndex = source.IndexOf(Right, StringComparison.Ordinal);
            string replacement = source.Remove(removeAt, Secret.Length);
            double[][] origins = Origins(page, source.Length);
            originalLeft = origins[0];
            originalRight = origins[rightIndex];
            setText = N.FPDFText_SetText(textObject, replacement) != 0;
            if (positions)
            {
                // The authored object has identity text transform plus translation,
                // no glyph clusters, no horizontal scale/rotation and no char spacing.
                // FPDFText_SetPositions receives original object-space advances.
                List<float> retainedPositions = [];
                for (int i = 1; i < source.Length; i++)
                    if (i < removeAt || i >= removeAt + Secret.Length)
                        retainedPositions.Add((float)(origins[i][0] - origins[0][0]));
                setPositions = N.FPDFText_SetPositions(textObject, retainedPositions.ToArray(), (nuint)retainedPositions.Count) != 0;
            }
            generated = N.FPDFPage_GenerateContent(page) != 0;
            Save(document.Handle, outputPath);
        }
        finally { N.FPDF_ClosePage(page); }
        byte[] saved = File.ReadAllBytes(outputPath);
        using Doc reopened = new(saved);
        IntPtr reopenedPage = N.FPDF_LoadPage(reopened.Handle, 0);
        string after;
        double[] savedRight;
        double[] savedLeft;
        try
        {
            after = ReadText(reopenedPage);
            savedLeft = FindOrigin(reopenedPage, Left);
            savedRight = FindOrigin(reopenedPage, Right);
        }
        finally { N.FPDF_ClosePage(reopenedPage); }
        double rightShift = savedRight[0] - originalRight[0];
        bool preserved = Distance(originalLeft, savedLeft) < 0.01 && Distance(originalRight, savedRight) < 0.01;
        return new
        {
            Name = name, Input = inputPath, Output = outputPath, Secret,
            InputSha256 = Hash(input), OutputSha256 = Hash(saved), BeforeText = source, AfterText = after,
            OriginalLeftOrigin = originalLeft, SavedLeftOrigin = savedLeft,
            OriginalRightOrigin = originalRight, SavedRightOrigin = savedRight, RightShiftPoints = rightShift,
            NativeSetTextReturnedSuccess = setText, NativeSetPositionsReturnedSuccess = positions ? (bool?)setPositions : null,
            NativeGenerateReturnedSuccess = generated, AdjacentGeometryPreserved = preserved,
            Decision = positions ? "bounded-ascii-candidate-independent-audit-required" : "unsafe-naive-rewrite-changes-adjacent-geometry",
            Checks = new[]
            {
                Check("native-settext-success", setText), Check("native-generate-success", generated),
                Check("saved-text-excludes-selected-characters", !after.Contains(Secret, StringComparison.Ordinal)),
                Check("left-and-right-text-retained", after.Contains(Left, StringComparison.Ordinal) && after.Contains(Right, StringComparison.Ordinal)),
                Check(positions ? "setter-accepted-retained-positions" : "negative-test-proves-adjacent-movement", positions ? setPositions : Math.Abs(rightShift) > 1),
                Check(positions ? "adjacent-origins-preserved" : "negative-test-rejects-geometry-change", positions ? preserved : !preserved)
            },
            SecurityStatus = "not-approved-until-all-object-audit-and-visual-inspection"
        };
    }

    private static object RemoveSharedInstance(string directory)
    {
        const string name = "shared-form-single-instance";
        byte[] input = SharedFixture();
        string inputPath = Write(directory, name + ".input.pdf", input);
        string outputPath = Path.Combine(directory, name + ".native-candidate.pdf");
        using Doc document = new(input);
        IntPtr page = N.FPDF_LoadPage(document.Handle, 0);
        bool removed = false;
        bool generated;
        try
        {
            for (int i = N.FPDFPage_CountObjects(page) - 1; i >= 0; i--)
            {
                IntPtr target = N.FPDFPage_GetObject(page, i);
                if (N.FPDFPageObj_GetType(target) != 5) continue;
                removed = N.FPDFPage_RemoveObject(page, target) != 0;
                if (removed) N.FPDFPageObj_Destroy(target);
            }
            generated = N.FPDFPage_GenerateContent(page) != 0;
            Save(document.Handle, outputPath);
        }
        finally { N.FPDF_ClosePage(page); }
        byte[] saved = File.ReadAllBytes(outputPath);
        string[] before = [TextOf(input, 0), TextOf(input, 1)];
        string[] after = [TextOf(saved, 0), TextOf(saved, 1)];
        return new
        {
            Name = name, Input = inputPath, Output = outputPath, Secret = FormSecret,
            InputSha256 = Hash(input), OutputSha256 = Hash(saved), BeforePageText = before, AfterPageText = after,
            Scope = "remove one entire top-level Form invocation on page 0; the same Form resource must remain intact on page 1",
            Checks = new[]
            {
                Check("native-remove-success", removed), Check("native-generate-success", generated),
                Check("both-input-pages-contain-shared-text", before.All(text => text.Contains(FormSecret, StringComparison.Ordinal))),
                Check("page-zero-excludes-shared-text", !after[0].Contains(FormSecret, StringComparison.Ordinal)),
                Check("page-zero-retains-control", after[0].Contains("KEEP_PAGE_ZERO", StringComparison.Ordinal)),
                Check("page-one-fully-unchanged", after[1] == before[1]),
                Check("page-one-must-still-contain-shared-text", after[1].Contains(FormSecret, StringComparison.Ordinal))
            },
            SecurityStatus = "page-scoped-removal-candidate-document-wide-secret-intentionally-retained"
        };
    }

    private static object RemovePath(string directory)
    {
        const string name = "whole-vector-path";
        byte[] input = PathFixture();
        string inputPath = Write(directory, name + ".input.pdf", input);
        string outputPath = Path.Combine(directory, name + ".native-candidate.pdf");
        using Doc document = new(input);
        IntPtr page = N.FPDF_LoadPage(document.Handle, 0);
        bool removed = false;
        bool generated;
        int beforeCount;
        int afterCount;
        try
        {
            beforeCount = N.FPDFPage_CountObjects(page);
            for (int i = 0; i < beforeCount; i++)
            {
                IntPtr target = N.FPDFPage_GetObject(page, i);
                if (N.FPDFPageObj_GetType(target) != 2) continue;
                if (N.FPDFPageObj_GetBounds(target, out float left, out float bottom, out float right, out float top) == 0)
                    throw new InvalidOperationException("Path bounds unavailable.");
                if (Math.Abs(left - 20) > 0.01 || Math.Abs(bottom - 80) > 0.01 || Math.Abs(right - 120) > 0.01 || Math.Abs(top - 120) > 0.01) continue;
                removed = N.FPDFPage_RemoveObject(page, target) != 0;
                if (removed) N.FPDFPageObj_Destroy(target);
                break;
            }
            generated = N.FPDFPage_GenerateContent(page) != 0;
            afterCount = N.FPDFPage_CountObjects(page);
            Save(document.Handle, outputPath);
        }
        finally { N.FPDF_ClosePage(page); }
        byte[] saved = File.ReadAllBytes(outputPath);
        return new
        {
            Name = name, Input = inputPath, Output = outputPath,
            InputSha256 = Hash(input), OutputSha256 = Hash(saved),
            TargetBounds = new[] { 20, 80, 120, 120 }, RetainedBounds = new[] { 180, 80, 260, 120 },
            TargetColor = "red", RetainedColor = "blue", BeforeObjects = beforeCount, AfterObjects = afterCount,
            Checks = new[]
            {
                Check("native-remove-success", removed), Check("native-generate-success", generated),
                Check("exactly-one-object-removed", afterCount == beforeCount - 1),
                Check("control-text-retained", TextOf(saved, 0).Contains("KEEP_VISIBLE_TEXT", StringComparison.Ordinal))
            },
            SecurityStatus = "whole-path-candidate-independent-operator-and-render-audit-required"
        };
    }

    private static object HiddenAnnotationFormLeak(string directory)
    {
        const string name = "hidden-annotation-form-native-leak";
        byte[] input = Pdf([
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 8 0 R /OpenAction 10 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R /Annots [6 0 R 7 0 R] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream($"BT /F1 10 Tf 20 245 Td ({Secret}) Tj ET\nBT /F1 14 Tf 30 270 Td (KEEP_VISIBLE_TEXT) Tj ET\n"),
            $"<< /Type /Annot /Subtype /Text /Rect [200 200 220 220] /Contents ({HiddenSecret}) /AP << /N 9 0 R >> >>",
            $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (hidden-field) /V ({HiddenSecret}) /DV ({HiddenSecret}) /Rect [30 80 240 110] /AP << /N 9 0 R >> /F 4 /DA (/F1 10 Tf 0 g) /P 3 0 R >>",
            "<< /Fields [7 0 R] /DR << /Font << /F1 4 0 R >> >> /DA (/F1 10 Tf 0 g) /NeedAppearances false >>",
            Stream($"BT /F1 10 Tf 0 12 Td ({HiddenSecret}) Tj ET\n", "/Type /XObject /Subtype /Form /BBox [0 0 210 30] /Resources << /Font << /F1 4 0 R >> >>"),
            $"<< /S /JavaScript /JS (app.alert\\({HiddenSecret}\\)) >>"
        ]);
        string inputPath = Write(directory, name + ".input.pdf", input);
        string outputPath = Path.Combine(directory, name + ".native-candidate.pdf");
        using Doc document = new(input);
        IntPtr page = N.FPDF_LoadPage(document.Handle, 0);
        bool removed = false;
        bool generated;
        try
        {
            for (int i = N.FPDFPage_CountObjects(page) - 1; i >= 0; --i)
            {
                IntPtr target = N.FPDFPage_GetObject(page, i);
                if (N.FPDFPageObj_GetType(target) != 1 || !ObjectText(page, target).Contains(Secret, StringComparison.Ordinal)) continue;
                removed = N.FPDFPage_RemoveObject(page, target) != 0;
                if (removed) N.FPDFPageObj_Destroy(target);
            }
            generated = N.FPDFPage_GenerateContent(page) != 0;
            Save(document.Handle, outputPath);
        }
        finally { N.FPDF_ClosePage(page); }
        byte[] saved = File.ReadAllBytes(outputPath);
        string after = TextOf(saved, 0);
        return new
        {
            Name = name, Input = inputPath, Output = outputPath, Secret, HiddenSecret,
            InputSha256 = Hash(input), OutputSha256 = Hash(saved),
            HiddenSurfaces = new[] { "annotation Contents", "annotation AP", "AcroForm V", "AcroForm DV", "Widget AP", "Catalog.OpenAction JavaScript" },
            Checks = new[] { Check("native-remove-success", removed), Check("native-generate-success", generated),
                Check("active-page-text-secret-removed", !after.Contains(Secret, StringComparison.Ordinal)),
                Check("control-text-retained", after.Contains("KEEP_VISIBLE_TEXT", StringComparison.Ordinal)) },
            SecurityStatus = "unsafe-native-candidate-hidden-annotation-form-action-data-requires-structural-sanitization"
        };
    }

    private static object Refuse(string directory, string name, byte[] input, string reason)
    {
        string path = Write(directory, name + ".input.pdf", input);
        string output = Path.Combine(directory, name + ".native-candidate.pdf");
        // Remove only this probe's known stale output, so rerunning cannot leave
        // an old candidate looking like an output of the refusal.
        File.Delete(output);
        return new { Name = name, Input = path, Output = (string?)null, InputSha256 = Hash(input),
            InputUnchangedSha256 = Hash(File.ReadAllBytes(path)), Decision = "rejected-before-mutation", Reason = reason,
            SecurityStatus = "unsupported-no-redacted-output" };
    }

    private static byte[] TextFixture(string source) => Pdf([
        "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 600 300] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        Stream($"BT /F1 14 Tf 30 245 Td ({source}) Tj ET\n")]);

    private static byte[] SharedFixture() => Pdf([
        "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 5 0 R >> /XObject << /Shared 8 0 R >> >> /Contents 6 0 R >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 5 0 R >> /XObject << /Shared 8 0 R >> >> /Contents 7 0 R >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        Stream("BT /F1 14 Tf 30 270 Td (KEEP_PAGE_ZERO) Tj ET\n/Shared Do\n"),
        Stream("BT /F1 14 Tf 30 270 Td (KEEP_PAGE_ONE) Tj ET\n/Shared Do\n"),
        Stream($"BT /F1 14 Tf 30 220 Td ({FormSecret}) Tj ET\nBT /F1 14 Tf 30 190 Td (KEEP_SHARED_TEXT) Tj ET\n",
            "/Type /XObject /Subtype /Form /FormType 1 /BBox [0 0 300 300] /Resources << /Font << /F1 5 0 R >> >>")]);

    private static byte[] PathFixture() => Pdf([
        "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        Stream("BT /F1 14 Tf 30 270 Td (KEEP_VISIBLE_TEXT) Tj ET\nq 1 0 0 rg 20 80 100 40 re f Q\nq 0 0 1 rg 180 80 80 40 re f Q\n")]);

    private static byte[] LigatureFixture() => Pdf([
        "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 4 0 R >> >> /Contents 6 0 R >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding /ToUnicode 5 0 R >>",
        Stream("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n/CMapName /LigatureMap def\n/CMapType 2 def\n1 begincodespacerange\n<00> <FF>\nendcodespacerange\n1 beginbfchar\n<41> <006600660069>\nendbfchar\nendcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n"),
        Stream("BT /F1 14 Tf 30 245 Td <41> Tj ET\n")]);

    private static byte[] Pdf(string[] objects)
    {
        using MemoryStream output = new();
        void Append(string value) => output.Write(Encoding.ASCII.GetBytes(value));
        Append("%PDF-1.7\n% P0 advanced independently authored fixture\n");
        List<long> offsets = [0];
        for (int index = 0; index < objects.Length; index++)
        { offsets.Add(output.Position); Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n"); }
        long xref = output.Position;
        Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) Append(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static string Stream(string content, string entries = "") =>
        $"<< /Length {Encoding.ASCII.GetByteCount(content)} {entries} >>\nstream\n{content}\nendstream";
    private static string Hash(byte[] input) => Convert.ToHexString(SHA256.HashData(input));
    private static string Write(string directory, string name, byte[] input)
    { string path = Path.Combine(directory, name); File.WriteAllBytes(path, input); return path; }
    private static object Check(string name, bool passed) => new { Name = name, Passed = passed };
    private static double Distance(double[] a, double[] b) => Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2));

    private static void Save(IntPtr handle, string path)
    {
        using MemoryStream output = new();
        SaveNative.SaveResult result = SaveNative.Save(handle, output, 2);
        if (!result.Success) throw new IOException("Native full rewrite failed.", result.Error);
        File.WriteAllBytes(path, output.ToArray());
    }

    private static string TextOf(byte[] bytes, int pageIndex)
    {
        using Doc document = new(bytes);
        IntPtr page = N.FPDF_LoadPage(document.Handle, pageIndex);
        if (page == IntPtr.Zero) throw new InvalidDataException("Page failed to load.");
        try { return ReadText(page); }
        finally { N.FPDF_ClosePage(page); }
    }

    private static string ReadText(IntPtr page)
    {
        IntPtr text = N.FPDFText_LoadPage(page);
        try
        {
            StringBuilder result = new();
            for (int i = 0; i < N.FPDFText_CountChars(text); i++)
            {
                uint code = N.FPDFText_GetUnicode(text, i);
                if (Rune.IsValid((int)code)) result.Append(char.ConvertFromUtf32((int)code));
            }
            return result.ToString();
        }
        finally { N.FPDFText_ClosePage(text); }
    }

    private static string ObjectText(IntPtr page, IntPtr item)
    {
        IntPtr text = N.FPDFText_LoadPage(page);
        try
        {
            CULong length = N.FPDFTextObj_GetText(item, text, IntPtr.Zero, new CULong(0));
            int size = checked((int)length.Value);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try { N.FPDFTextObj_GetText(item, text, buffer, length); return Marshal.PtrToStringUni(buffer) ?? ""; }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        finally { N.FPDFText_ClosePage(text); }
    }

    private static double[][] Origins(IntPtr page, int count)
    {
        IntPtr text = N.FPDFText_LoadPage(page);
        try
        {
            double[][] result = new double[count][];
            for (int index = 0; index < count; index++)
            {
                if (N.FPDFText_GetCharOrigin(text, index, out double x, out double y) == 0) throw new InvalidDataException("Character origin unavailable.");
                result[index] = [x, y];
            }
            return result;
        }
        finally { N.FPDFText_ClosePage(text); }
    }

    private static double[] FindOrigin(IntPtr page, string needle)
    {
        string text = ReadText(page);
        int index = text.IndexOf(needle, StringComparison.Ordinal);
        if (index < 0) throw new InvalidDataException("Retained text cannot be located.");
        return Origins(page, index + 1)[index];
    }

    private sealed class Doc : IDisposable
    {
        private GCHandle _pin;
        internal IntPtr Handle { get; }
        internal Doc(byte[] bytes)
        {
            _pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Handle = N.FPDF_LoadMemDocument64(_pin.AddrOfPinnedObject(), (nuint)bytes.Length, IntPtr.Zero);
            if (Handle == IntPtr.Zero) { _pin.Free(); throw new InvalidDataException("Advanced fixture load failed."); }
        }
        public void Dispose() { N.FPDF_CloseDocument(Handle); _pin.Free(); }
    }

    private static class N
    {
        private const string Library = "pdfium";
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_LoadMemDocument64(IntPtr bytes, nuint size, IntPtr password);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_CloseDocument(IntPtr document);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_LoadPage(IntPtr document, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_ClosePage(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_CountObjects(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPage_GetObject(IntPtr page, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPageObj_GetType(IntPtr item);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPageObj_GetBounds(IntPtr item, out float left, out float bottom, out float right, out float top);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_RemoveObject(IntPtr page, IntPtr item);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPageObj_Destroy(IntPtr item);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_GenerateContent(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFText_SetText(IntPtr text, [MarshalAs(UnmanagedType.LPWStr)] string value);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFText_SetPositions(IntPtr text, float[] positions, nuint count);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFText_LoadPage(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFText_ClosePage(IntPtr text);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFText_CountChars(IntPtr text);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern uint FPDFText_GetUnicode(IntPtr text, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFText_GetCharOrigin(IntPtr text, int index, out double x, out double y);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern CULong FPDFTextObj_GetText(IntPtr item, IntPtr text, IntPtr buffer, CULong size);
    }
}
