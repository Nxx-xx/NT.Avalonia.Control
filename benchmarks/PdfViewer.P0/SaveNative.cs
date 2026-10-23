using System.Runtime.InteropServices;
using System.Text;

namespace PdfViewer.P0;

internal static class SaveNative
{
    private const string Library = "pdfium";
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_InitLibrary();
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_LoadMemDocument64(IntPtr data, nuint size, [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_CloseDocument(IntPtr document);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern uint FPDF_GetLastError();
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDF_GetPageCount(IntPtr document);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_LoadPage(IntPtr document, int index);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_ClosePage(IntPtr page);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFText_LoadPage(IntPtr page);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFText_ClosePage(IntPtr page);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFText_CountChars(IntPtr page);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern uint FPDFText_GetUnicode(IntPtr page, int index);
    [DllImport(Library, EntryPoint = "FPDF_SaveAsCopy", CallingConvention = CallingConvention.Winapi)] private static extern int SaveAsCopy(IntPtr document, ref FileWrite write, CULong flags);
    internal static int FPDF_SaveAsCopy(IntPtr document, ref FileWrite write, uint flags) => SaveAsCopy(document, ref write, new CULong(flags));
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDF_GetSecurityHandlerRevision(IntPtr document);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDF_GetSignatureCount(IntPtr document);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPage_New(IntPtr document, int index, double width, double height);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_GenerateContent(IntPtr page);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPageObj_NewTextObj(IntPtr document, [MarshalAs(UnmanagedType.LPStr)] string font, float size);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFText_SetText(IntPtr text, [MarshalAs(UnmanagedType.LPWStr)] string value);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPageObj_Transform(IntPtr obj, double a, double b, double c, double d, double e, double f);
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPage_InsertObject(IntPtr page, IntPtr obj);

    [StructLayout(LayoutKind.Sequential)] internal struct FileWrite { public int Version; public IntPtr WriteBlock; }
    // FPDF_FILEWRITE callbacks use the C calling convention. C unsigned long is
    // 32-bit on Windows and pointer-sized on the supported 64-bit Unix targets.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int WindowsWrite(IntPtr self, IntPtr data, uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int UnixWrite(IntPtr self, IntPtr data, nuint count);

    internal readonly record struct SaveResult(bool NativeSuccess, Exception? Error)
    {
        internal bool Success => NativeSuccess && Error is null;
    }

    internal static SaveResult Save(IntPtr document, Stream stream, uint flags = 2, long? failAfter = null, CancellationToken token = default)
    {
        Exception? error = null;
        long written = 0;
        int Write(IntPtr data, nuint count)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                if (failAfter.HasValue && written + (long)count > failAfter) throw new IOException("Injected storage failure");
                var bytes = new byte[checked((int)count)];
                Marshal.Copy(data, bytes, 0, bytes.Length);
                stream.Write(bytes);
                written += bytes.Length;
                return 1;
            }
            catch (Exception exception) { error = exception; return 0; }
        }
        Delegate callback = OperatingSystem.IsWindows()
            ? new WindowsWrite((_, data, count) => Write(data, count))
            : new UnixWrite((_, data, count) => Write(data, count));
        var writer = new FileWrite { Version = 1, WriteBlock = Marshal.GetFunctionPointerForDelegate(callback) };
        var success = FPDF_SaveAsCopy(document, ref writer, flags) != 0;
        GC.KeepAlive(callback);
        // This build can return TRUE after WriteBlock returned zero. A host must
        // treat its captured callback error as failure regardless of native BOOL.
        return new(success, error);
    }

    internal static string Text(IntPtr document)
    {
        var result = new StringBuilder();
        for (var index = 0; index < FPDF_GetPageCount(document); index++)
        {
            var page = FPDF_LoadPage(document, index);
            var text = FPDFText_LoadPage(page);
            try
            {
                for (var i = 0; i < FPDFText_CountChars(text); i++)
                {
                    var scalar = FPDFText_GetUnicode(text, i);
                    if (Rune.IsValid((int)scalar)) result.Append(char.ConvertFromUtf32((int)scalar));
                }
            }
            finally { FPDFText_ClosePage(text); FPDF_ClosePage(page); }
        }
        return result.ToString();
    }
}

internal sealed class SaveDocument : IDisposable
{
    private GCHandle _pin;
    internal IntPtr Handle { get; }
    internal SaveDocument(byte[] bytes, string? password = null)
    {
        _pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        Handle = SaveNative.FPDF_LoadMemDocument64(_pin.AddrOfPinnedObject(), (nuint)bytes.Length, password);
        if (Handle == IntPtr.Zero) { _pin.Free(); throw new InvalidDataException($"PDFium load error {SaveNative.FPDF_GetLastError()}"); }
    }
    public void Dispose() { SaveNative.FPDF_CloseDocument(Handle); _pin.Free(); }
}
