using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace PdfViewer.P0;

/// <summary>Isolated P0 prototype. No production authoring API is added by this probe.</summary>
internal static class AnnotationFontProbe
{
    private const string InitialText = "中文批注 字体验证 ABC 123";
    private const string EditedText = "中文批注 编辑后 ABC 123";

    internal static object Run(string runDirectory)
    {
        Directory.CreateDirectory(runDirectory);
        var fontPath = Environment.GetEnvironmentVariable("PDFVIEWER_P0_FONT")
            ?? Path.Combine(runDirectory, "P0AnnotationCjk.ttf");
        if (!File.Exists(fontPath))
            throw new FileNotFoundException("Prepare the licensed P0 CJK font using prepare_annotation_font.py, or set PDFVIEWER_P0_FONT.", fontPath);
        var fontBytes = File.ReadAllBytes(fontPath);
        var checks = new List<object>();
        var outputs = new List<object>();
        var document = N.FPDF_CreateNewDocument();
        Require(document != IntPtr.Zero, "Create document");
        try
        {
            var page = N.FPDFPage_New(document, 0, 600, 260);
            Require(page != IntPtr.Zero, "Create page");
            var font = N.FPDFText_LoadFont(document, fontBytes, (uint)fontBytes.Length, 2, 1);
            Require(font != IntPtr.Zero, "Load embedded CJK TrueType CID font");
            try
            {
                var embedded = N.FPDFFont_GetIsEmbedded(font);
                Require(embedded == 1, "Font must be embedded; fallback is not acceptable");
                checks.Add(new { id = "cjk-font-embedded", passed = true, embedded, fontBytes = fontBytes.Length });
                var stamp = N.FPDFPage_CreateAnnot(page, 13);
                Require(stamp != IntPtr.Zero, "Create stamp annotation");
                try
                {
                    var rect = new RectF(28, 206, 570, 130);
                    Require(N.FPDFAnnot_SetRect(stamp, ref rect) != 0, "Set stamp rectangle");
                    Require(N.FPDFAnnot_SetFlags(stamp, 4) != 0, "Set annotation Print flag");
                    SetString(stamp, "Contents", InitialText);
                    SetString(stamp, "T", "PDF Viewer P0");
                    var text = NewText(document, font, InitialText, 40, 160);
                    if (N.FPDFAnnot_AppendObject(stamp, text) == 0)
                    {
                        N.FPDFPageObj_Destroy(text);
                        throw new InvalidOperationException("PDFium refused embedded CJK text in Stamp AP");
                    }
                    var initial = Path.Combine(runDirectory, "native-stamp-cjk.pdf");
                    Save(document, initial);
                    Render(page, Path.Combine(runDirectory, "native-stamp-cjk.ppm"));
                    outputs.Add(Output(initial));
                    checks.Add(new { id = "stamp-append-text", passed = true, objectCount = N.FPDFAnnot_GetObjectCount(stamp), contents = ReadString(stamp, "Contents") });
                }
                finally { N.FPDFPage_CloseAnnot(stamp); }
            }
            finally
            {
                N.FPDFFont_Close(font);
                N.FPDF_ClosePage(page);
            }
        }
        finally { N.FPDF_CloseDocument(document); }

        // Reopen serialized bytes before editing: tests resource ownership across saves.
        var sourceBytes = File.ReadAllBytes(Path.Combine(runDirectory, "native-stamp-cjk.pdf"));
        var pinned = GCHandle.Alloc(sourceBytes, GCHandleType.Pinned);
        document = N.FPDF_LoadMemDocument64(pinned.AddrOfPinnedObject(), (nuint)sourceBytes.Length, null);
        Require(document != IntPtr.Zero, "Reopen stamp PDF");
        try
        {
            var page = N.FPDF_LoadPage(document, 0);
            Require(page != IntPtr.Zero, "Reopen page");
            try
            {
                Require(N.FPDFPage_GetAnnotCount(page) == 1, "Stamp annotation persisted");
                var stamp = N.FPDFPage_GetAnnot(page, 0);
                Require(stamp != IntPtr.Zero, "Read stamp");
                try
                {
                    Require(ReadString(stamp, "Contents") == InitialText, "Unicode canonical Contents survived save");
                    var text = N.FPDFAnnot_GetObject(stamp, 0);
                    Require(text != IntPtr.Zero, "Reopen Stamp AP text object");
                    Require(N.FPDFText_SetText(text, Wide(EditedText)) != 0, "Edit Stamp AP text");
                    Require(N.FPDFAnnot_UpdateObject(stamp, text) != 0, "Regenerate Stamp appearance after edit");
                    SetString(stamp, "Contents", EditedText);
                    var edited = Path.Combine(runDirectory, "native-stamp-cjk-edited.pdf");
                    Save(document, edited);
                    Render(page, Path.Combine(runDirectory, "native-stamp-cjk-edited.ppm"));
                    outputs.Add(Output(edited));
                    checks.Add(new { id = "stamp-reopen-edit-save", passed = true, contents = ReadString(stamp, "Contents"), objectCount = N.FPDFAnnot_GetObjectCount(stamp) });
                }
                finally { N.FPDFPage_CloseAnnot(stamp); }
            }
            finally { N.FPDF_ClosePage(page); }
        }
        finally { N.FPDF_CloseDocument(document); pinned.Free(); }

        // Deliberately test FreeText separately. Stamp success cannot establish FreeText support.
        document = N.FPDF_CreateNewDocument();
        Require(document != IntPtr.Zero, "Create FreeText probe document");
        try
        {
            var page = N.FPDFPage_New(document, 0, 600, 260);
            Require(page != IntPtr.Zero, "Create FreeText probe page");
            try
            {
                var supported = N.FPDFAnnot_IsSupportedSubtype(3);
                var freeText = N.FPDFPage_CreateAnnot(page, 3);
                var createSuccess = freeText != IntPtr.Zero;
                var appendSuccess = false;
                var setApSuccess = false;
                if (createSuccess)
                {
                    try
                    {
                        var rect = new RectF(28, 206, 570, 130);
                        Require(N.FPDFAnnot_SetRect(freeText, ref rect) != 0, "Set FreeText rectangle");
                        SetString(freeText, "Contents", InitialText);
                        var font = N.FPDFText_LoadFont(document, fontBytes, (uint)fontBytes.Length, 2, 1);
                        Require(font != IntPtr.Zero, "Load FreeText probe CJK font");
                        try
                        {
                            var text = NewText(document, font, InitialText, 40, 160);
                            appendSuccess = N.FPDFAnnot_AppendObject(freeText, text) != 0;
                            if (!appendSuccess) N.FPDFPageObj_Destroy(text);
                            // This API accepts AP operators but exposes no Resources/Font setter.
                            setApSuccess = N.FPDFAnnot_SetAP(freeText, 0, Wide("BT /P0CJK 18 Tf 1 0 0 1 40 160 Tm <4E2D6587> Tj ET")) != 0;
                        }
                        finally { N.FPDFFont_Close(font); }
                    }
                    finally { N.FPDFPage_CloseAnnot(freeText); }
                }
                var negative = Path.Combine(runDirectory, "native-freetext-negative-control.pdf");
                Save(document, negative);
                outputs.Add(Output(negative));
                checks.Add(new { id = "freetext-public-api-gap", passed = !appendSuccess, supported, createSuccess, appendSuccess, setApSuccess,
                    interpretation = "An expected negative control. Creating FreeText/setting AP operators does not supply an embedded font resource. Use independent AP/resource inspection." });
            }
            finally { N.FPDF_ClosePage(page); }
        }
        finally { N.FPDF_CloseDocument(document); }

        return new
        {
            id = "POC-02", status = "partial", capturedUtc = DateTimeOffset.UtcNow,
            font = new { path = Path.GetFullPath(fontPath), sha256 = Convert.ToHexString(SHA256.HashData(fontBytes)), bytes = fontBytes.Length, type = "TrueType CID; converted subset fixture", embedding = "embedded; no system fallback" },
            checks, outputs,
            conclusions = new[] {
                "PDFium public APIs can create, embed CJK font in, persist and edit Stamp annotation APs.",
                "Public FreeText AppendObject support is independently tested, not inferred from Stamp.",
                "complete_annotation_font_bridge.py is a separate writer-bridge experiment; only its independent result establishes FreeText/AP resource serialization." },
            limitations = new[] {
                "Stamp is not FreeText; unsupported FreeText requires a writer/resource bridge before product implementation.",
                "Font preparation and writer bridge use Python only in this prototype; product dependencies and font licensing/subsetting must be decided explicitly.",
                "Native Poppler-independent rendering, AP/font structure inspection and final bridge reopen are separate verification steps.",
                "Linux/Kylin, RTL/shaping, font fallback, annotation text layout and Avalonia editing UI are not validated by this prototype." }
        };
    }

    private static object Output(string path) => new { path = Path.GetFullPath(path), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), bytes = new FileInfo(path).Length };
    private static byte[] Wide(string value) => Encoding.Unicode.GetBytes(value + '\0');
    private static void Require(bool success, string operation) { if (!success) throw new InvalidOperationException(operation); }
    private static void SetString(IntPtr annotation, string key, string value) => Require(N.FPDFAnnot_SetStringValue(annotation, key, Wide(value)) != 0, "Set " + key);
    private static string ReadString(IntPtr annotation, string key)
    {
        nuint size = N.FPDFAnnot_GetStringValue(annotation, key, null, new CULong(0u)).Value;
        var buffer = new byte[checked((int)size)];
        Require(size >= 2 && N.FPDFAnnot_GetStringValue(annotation, key, buffer, new CULong(size)).Value == size, "Read " + key);
        return Encoding.Unicode.GetString(buffer, 0, checked((int)size - 2));
    }
    private static IntPtr NewText(IntPtr document, IntPtr font, string text, double x, double y)
    {
        var result = N.FPDFPageObj_CreateTextObj(document, font, 18);
        Require(result != IntPtr.Zero, "Create text object");
        Require(N.FPDFText_SetText(result, Wide(text)) != 0, "Set CJK text");
        N.FPDFPageObj_Transform(result, 1, 0, 0, 1, x, y);
        return result;
    }
    private static void Save(IntPtr document, string path)
    {
        using var stream = new MemoryStream();
        Exception? exception = null;
        WriteBlock callback = (_, data, size) =>
        {
            try { var block = new byte[checked((int)size.Value)]; Marshal.Copy(data, block, 0, block.Length); stream.Write(block); return 1; }
            catch (Exception error) { exception = error; return 0; }
        };
        var writer = new FileWrite { Version = 1, WriteBlock = Marshal.GetFunctionPointerForDelegate(callback) };
        var saved = N.FPDF_SaveAsCopy(document, ref writer, new CULong(2u));
        GC.KeepAlive(callback);
        if (exception is not null) throw new IOException("PDF save callback failed", exception);
        Require(saved != 0, "Full save");
        File.WriteAllBytes(path, stream.ToArray());
    }
    private static void Render(IntPtr page, string path)
    {
        const int width = 1200, height = 520, stride = width * 4;
        var pixels = new byte[stride * height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        var bitmap = N.FPDFBitmap_CreateEx(width, height, 4, handle.AddrOfPinnedObject(), stride);
        Require(bitmap != IntPtr.Zero, "Create render bitmap");
        try
        {
            Require(N.FPDFBitmap_FillRect(bitmap, 0, 0, width, height, new CULong(0xffffffffu)) != 0, "Fill render bitmap");
            N.FPDF_RenderPageBitmap(bitmap, page, 0, 0, width, height, 0, 1);
            using var stream = File.Create(path);
            stream.Write(Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));
            for (var offset = 0; offset < pixels.Length; offset += 4)
            { stream.WriteByte(pixels[offset + 2]); stream.WriteByte(pixels[offset + 1]); stream.WriteByte(pixels[offset]); }
        }
        finally { N.FPDFBitmap_Destroy(bitmap); handle.Free(); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct RectF(float left, float top, float right, float bottom)
    { public readonly float Left = left; public readonly float Top = top; public readonly float Right = right; public readonly float Bottom = bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct FileWrite { public int Version; public IntPtr WriteBlock; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int WriteBlock(IntPtr writer, IntPtr data, CULong size);
    private static class N
    {
        private const string Library = "pdfium";
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_CreateNewDocument();
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPage_New(IntPtr document, int index, double width, double height);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_LoadMemDocument64(IntPtr data, nuint size, [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_LoadPage(IntPtr document, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_ClosePage(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_CloseDocument(IntPtr document);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFText_LoadFont(IntPtr document, byte[] data, uint size, int type, int cid);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFFont_Close(IntPtr font);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFFont_GetIsEmbedded(IntPtr font);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPageObj_CreateTextObj(IntPtr document, IntPtr font, float size);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFText_SetText(IntPtr text, byte[] value);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPageObj_Transform(IntPtr obj, double a, double b, double c, double d, double e, double f);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPageObj_Destroy(IntPtr obj);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_IsSupportedSubtype(int subtype);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPage_CreateAnnot(IntPtr page, int subtype);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_GetAnnotCount(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPage_GetAnnot(IntPtr page, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPage_CloseAnnot(IntPtr annotation);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_SetRect(IntPtr annotation, ref RectF rect);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_SetFlags(IntPtr annotation, int flags);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_SetStringValue(IntPtr annotation, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, byte[] value);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern CULong FPDFAnnot_GetStringValue(IntPtr annotation, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, byte[]? buffer, CULong size);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_AppendObject(IntPtr annotation, IntPtr obj);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_GetObjectCount(IntPtr annotation);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFAnnot_GetObject(IntPtr annotation, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_UpdateObject(IntPtr annotation, IntPtr obj);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_SetAP(IntPtr annotation, int mode, byte[] value);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDF_SaveAsCopy(IntPtr document, ref FileWrite writer, CULong flags);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr pixels, int stride);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, CULong color);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int x, int y, int width, int height, int rotate, int flags);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFBitmap_Destroy(IntPtr bitmap);
    }
}
