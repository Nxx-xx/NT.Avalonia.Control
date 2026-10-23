using System.Runtime.InteropServices;
using NT.AvaloniaControl.PdfViewer.Interop;

namespace NT.AvaloniaControl.PdfViewer.Editing;

internal static class PdfWriteValidation
{
    internal static Task<int> ValidateAsync(byte[] data, CancellationToken token) => PdfiumRuntime.RunAsync(() =>
    {
        var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        var handle = IntPtr.Zero;
        try
        {
            token.ThrowIfCancellationRequested();
            handle = PdfiumNative.FPDF_LoadMemDocument64(pin.AddrOfPinnedObject(), (nuint)data.Length, null);
            if (handle == IntPtr.Zero)
            {
                var error = PdfiumNative.FPDF_GetLastError();
                if (error == 4) throw new NotSupportedException("The structural editing session does not support encrypted PDFs.");
                throw new PdfDocumentException("The PDF cannot be opened for structural editing.", error);
            }
            if (Native.FPDF_GetSecurityHandlerRevision(handle) >= 0)
                throw new NotSupportedException("The structural editing session does not preserve encryption and refuses encrypted PDFs.");
            var signatures = Native.FPDF_GetSignatureCount(handle);
            if (signatures != 0)
                throw new NotSupportedException("Structural editing rewrites the PDF and refuses signed PDFs or unreadable signature state.");
            var pages = PdfiumNative.FPDF_GetPageCount(handle);
            if (pages <= 0) throw new InvalidDataException("The PDF has no readable pages.");
            token.ThrowIfCancellationRequested();
            return pages;
        }
        finally
        {
            if (handle != IntPtr.Zero) PdfiumNative.FPDF_CloseDocument(handle);
            pin.Free();
        }
    }, token);

    private static class Native
    {
        [DllImport("pdfium", CallingConvention = CallingConvention.Winapi)] internal static extern int FPDF_GetSecurityHandlerRevision(IntPtr document);
        [DllImport("pdfium", CallingConvention = CallingConvention.Winapi)] internal static extern int FPDF_GetSignatureCount(IntPtr document);
    }
}
