using System.Runtime.InteropServices;

namespace NT.AvaloniaControl.PdfViewer.Interop;

internal static partial class PdfiumNative
{
    // Matched against the headers shipped in bblanchon.PDFium 155.0.8044.
    // CULong follows the C ABI: 32-bit on Windows and 64-bit on Linux x64/ARM64.
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern CULong FPDF_GetDocPermissions(IntPtr document);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern CULong FPDF_GetDocUserPermissions(IntPtr document);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDF_GetSecurityHandlerRevision(IntPtr document);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDF_GetFileVersion(IntPtr document, out int version);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDF_GetSignatureCount(IntPtr document);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern CULong FPDF_GetMetaText(IntPtr document,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string tag, [Out] byte[]? buffer, CULong bufferLength);
}
