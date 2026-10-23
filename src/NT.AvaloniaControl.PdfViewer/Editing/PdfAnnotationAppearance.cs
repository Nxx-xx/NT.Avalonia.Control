using System.Buffers;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;

namespace NT.AvaloniaControl.PdfViewer.Editing;

/// <summary>
/// Builds an isolated embedded-font Stamp appearance. The structure writer clones its
/// AP resource graph into a FreeText annotation; no Stamp is added to the user's PDF.
/// All calls must execute inside PdfiumRuntime's process-wide native gate.
/// </summary>
internal static class PdfAnnotationAppearance
{
    internal static byte[] Create(string text, byte[] trueTypeFont, double width, double height,
        float fontSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(trueTypeFont);
        ValidateDimension(width, nameof(width));
        ValidateDimension(height, nameof(height));
        if (!float.IsFinite(fontSize) || fontSize <= 0 || fontSize > 14400)
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        ValidateText(text, trueTypeFont, cancellationToken);
        var cidFont = PdfFreeTextCidFont.Create(text, trueTypeFont, cancellationToken);

        var document = Native.FPDF_CreateNewDocument();
        Require(document != IntPtr.Zero, "Create annotation appearance document");
        try
        {
            var page = Native.FPDFPage_New(document, 0, width, height);
            Require(page != IntPtr.Zero, "Create annotation appearance page");
            try
            {
                var font = Native.FPDFText_LoadCidType2Font(document, trueTypeFont, checked((uint)trueTypeFont.Length),
                    cidFont.ToUnicode, cidFont.CidToGid, checked((uint)cidFont.CidToGid.Length));
                Require(font != IntPtr.Zero, "Load annotation TrueType CID font");
                try
                {
                    Require(Native.FPDFFont_GetIsEmbedded(font) == 1, "Embed annotation font");
                    var annotation = Native.FPDFPage_CreateAnnot(page, 13); // Stamp supports native object AP generation.
                    Require(annotation != IntPtr.Zero, "Create annotation appearance");
                    try
                    {
                        var rectangle = new RectF(0, (float)height, (float)width, 0);
                        Require(Native.FPDFAnnot_SetRect(annotation, ref rectangle) != 0, "Set annotation appearance bounds");
                        Require(Native.FPDFAnnot_SetFlags(annotation, 4) != 0, "Set annotation Print flag");
                        Require(Native.FPDFAnnot_SetStringValue(annotation, "Contents", Wide(text)) != 0, "Set canonical annotation text");
                        var textObject = Native.FPDFPageObj_CreateTextObj(document, font, fontSize);
                        Require(textObject != IntPtr.Zero, "Create annotation text object");
                        try
                        {
                            Require(Native.FPDFText_SetCharcodes(textObject, cidFont.Charcodes, checked((nuint)cidFont.Charcodes.Length)) != 0,
                                "Set Unicode-scalar annotation character codes");
                            Require(Native.FPDFPageObj_SetFillColor(textObject, 0, 0, 0, 255) != 0, "Set annotation text color");
                            Require(Native.FPDFPageObj_GetBounds(textObject, out var left, out var bottom, out var right, out var top) != 0,
                                "Measure annotation text");
                            if (!float.IsFinite(left) || !float.IsFinite(bottom) || !float.IsFinite(right) || !float.IsFinite(top) ||
                                right <= left || top <= bottom)
                                throw new InvalidOperationException("The supplied font produced invalid annotation text bounds.");
                            // Single-line layout is intentionally explicit. Do not silently clip or shrink text.
                            var padding = Math.Min(2d, Math.Min(width, height) / 10d);
                            if (right - left > width - 2 * padding || top - bottom > height - 2 * padding)
                                throw new ArgumentException("Annotation text does not fit the rectangle at the requested font size; wrapping is not supported.", nameof(text));
                            Native.FPDFPageObj_Transform(textObject, 1, 0, 0, 1, padding - left, height - padding - top);
                            cancellationToken.ThrowIfCancellationRequested();
                            Require(Native.FPDFAnnot_AppendObject(annotation, textObject) != 0, "Generate embedded-font annotation appearance");
                            textObject = IntPtr.Zero; // Ownership transferred only on success.
                        }
                        finally
                        {
                            if (textObject != IntPtr.Zero) Native.FPDFPageObj_Destroy(textObject);
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        return Save(document, cancellationToken);
                    }
                    finally { Native.FPDFPage_CloseAnnot(annotation); }
                }
                finally { Native.FPDFFont_Close(font); }
            }
            finally { Native.FPDF_ClosePage(page); }
        }
        finally { Native.FPDF_CloseDocument(document); }
    }

    // Called before CPU subsetting, outside the native gate, and again on the prepared font.
    internal static void ValidateText(string text, byte[] trueTypeFont, CancellationToken token)
    {
        if (text.Length is 0 or > 4096 || string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Annotation text must contain visible text and at most 4096 UTF-16 code units.", nameof(text));
        token.ThrowIfCancellationRequested();
        var fontInfo = new PdfTrueTypeFont(trueTypeFont);
        var remaining = text.AsSpan();
        while (!remaining.IsEmpty)
        {
            token.ThrowIfCancellationRequested();
            if (Rune.DecodeFromUtf16(remaining, out var rune, out var consumed) != OperationStatus.Done)
                throw new ArgumentException("Annotation text contains invalid UTF-16.", nameof(text));
            ValidateRune(rune);
            if (!fontInfo.Contains(rune.Value))
                throw new ArgumentException($"The supplied font has no glyph for U+{rune.Value:X4}; font fallback is not used.", nameof(trueTypeFont));
            remaining = remaining[consumed..];
        }
    }

    private static void ValidateDimension(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0 || value > 14400)
            throw new ArgumentOutOfRangeException(name, "Annotation dimensions must be finite and in (0, 14400] PDF points.");
    }

    private static void ValidateRune(Rune rune)
    {
        // This renderer supplies glyphs directly to PDFium, without a shaping engine.
        // Admit the scripts covered by its current horizontal, single-line contract.
        var value = rune.Value;
        var simpleScript = value is >= 0x20 and <= 0x024F or >= 0x0370 and <= 0x052F or
            >= 0x2000 and <= 0x2BFF or >= 0x2E80 and <= 0xA4CF or >= 0xAC00 and <= 0xD7AF or
            >= 0xF900 and <= 0xFAFF or >= 0xFE10 and <= 0xFE6F or >= 0xFF01 and <= 0xFFEF or
            >= 0x20000 and <= 0x323AF;
        var category = Rune.GetUnicodeCategory(rune);
        if (!simpleScript || category is UnicodeCategory.Control or UnicodeCategory.Format or
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or
            UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.OtherNotAssigned)
            throw new NotSupportedException($"U+{value:X4} requires unsupported layout or shaping. FreeText currently supports one horizontal line of Latin, Greek, Cyrillic, CJK or precomposed Hangul text.");
    }

    private static byte[] Save(IntPtr document, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        ExceptionDispatchInfo? callbackError = null;
        WriteBlock callback = (_, data, size) =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var length = checked((int)size.Value);
                var buffer = ArrayPool<byte>.Shared.Rent(length);
                try { Marshal.Copy(data, buffer, 0, length); stream.Write(buffer, 0, length); }
                finally { ArrayPool<byte>.Shared.Return(buffer); }
                return 1;
            }
            catch (Exception error)
            {
                callbackError ??= ExceptionDispatchInfo.Capture(error);
                return 0; // A managed exception must never cross a native callback boundary.
            }
        };
        var writer = new FileWrite { Version = 1, WriteBlock = Marshal.GetFunctionPointerForDelegate(callback) };
        // Full save. Experimental FPDF_SUBSET_NEW_FONTS is deliberately not enabled:
        // coverage of annotation AP fonts requires separate cross-reader verification.
        var saved = Native.FPDF_SaveAsCopy(document, ref writer, new CULong(2u));
        GC.KeepAlive(callback);
        callbackError?.Throw();
        cancellationToken.ThrowIfCancellationRequested();
        Require(saved != 0, "Serialize annotation appearance resources");
        return stream.ToArray();
    }

    private static byte[] Wide(string value) => Encoding.Unicode.GetBytes(value + '\0');
    private static void Require(bool condition, string operation)
    {
        if (!condition) throw new InvalidOperationException($"PDFium could not {operation.ToLowerInvariant()}.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct RectF(float left, float top, float right, float bottom)
    {
        public readonly float Left = left;
        public readonly float Top = top;
        public readonly float Right = right;
        public readonly float Bottom = bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileWrite { public int Version; public IntPtr WriteBlock; }

    // FPDF_FILEWRITE.WriteBlock is cdecl, even where exported FPDF_* APIs use stdcall.
    // CULong represents C unsigned long correctly on both Windows and Linux x64/ARM64.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int WriteBlock(IntPtr writer, IntPtr data, CULong size);

    private static class Native
    {
        private const string Library = "pdfium";
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_CreateNewDocument();
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPage_New(IntPtr document, int index, double width, double height);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_ClosePage(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_CloseDocument(IntPtr document);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFText_LoadCidType2Font(IntPtr document, byte[] data, uint size,
            [MarshalAs(UnmanagedType.LPStr)] string toUnicode, byte[] cidToGid, uint cidToGidBytes);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFFont_GetIsEmbedded(IntPtr font);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFFont_Close(IntPtr font);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPageObj_CreateTextObj(IntPtr document, IntPtr font, float size);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFText_SetCharcodes(IntPtr text, uint[] charcodes, nuint count);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPageObj_SetFillColor(IntPtr obj, uint r, uint g, uint b, uint a);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPageObj_GetBounds(IntPtr obj, out float left, out float bottom, out float right, out float top);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPageObj_Transform(IntPtr obj, double a, double b, double c, double d, double e, double f);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPageObj_Destroy(IntPtr obj);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPage_CreateAnnot(IntPtr page, int subtype);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPage_CloseAnnot(IntPtr annotation);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_SetRect(IntPtr annotation, ref RectF rect);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_SetFlags(IntPtr annotation, int flags);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_SetStringValue(IntPtr annotation, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, byte[] value);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_AppendObject(IntPtr annotation, IntPtr obj);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDF_SaveAsCopy(IntPtr document, ref FileWrite writer, CULong flags);
    }
}
