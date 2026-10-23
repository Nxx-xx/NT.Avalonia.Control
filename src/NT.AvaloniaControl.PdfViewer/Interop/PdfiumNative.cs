using System.Runtime.InteropServices;

namespace NT.AvaloniaControl.PdfViewer.Interop;

// Minimal bindings to PDFium's public C API. Every call, including close/finalizer calls,
// must run inside PdfiumRuntime's process-wide gate: PDFium is not thread safe.
internal static partial class PdfiumNative
{
    private const string Library = "pdfium";

    [StructLayout(LayoutKind.Sequential)]
    internal struct PageSize
    {
        public float Width;
        public float Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RectF
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;
    }

    // PDFACTION_* from fpdf_doc.h.
    internal const uint ActionUnsupported = 0;
    internal const uint ActionGoto = 1;
    internal const uint ActionRemoteGoto = 2;
    internal const uint ActionUri = 3;
    internal const uint ActionLaunch = 4;
    internal const uint ActionEmbeddedGoto = 5;

    // PDFDEST_VIEW_* from fpdf_doc.h.
    internal const uint DestViewUnknown = 0;
    internal const uint DestViewXyz = 1;
    internal const uint DestViewFit = 2;
    internal const uint DestViewFitH = 3;
    internal const uint DestViewFitV = 4;
    internal const uint DestViewFitR = 5;
    internal const uint DestViewFitB = 6;
    internal const uint DestViewFitBH = 7;
    internal const uint DestViewFitBV = 8;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int NeedToPauseCallback(IntPtr pause);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Pause
    {
        public int Version;
        public IntPtr NeedToPauseNow;
        public IntPtr User;
    }

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern void FPDF_InitLibrary();

    // FPDF_FILEACCESS from fpdfview.h. Every field is a C `unsigned long` / pointer, so the
    // integer fields use CULong (32-bit on Windows, 64-bit on Linux x64). PDFium keeps the
    // pointer passed to FPDF_LoadCustomDocument for the document's lifetime: the block, the
    // GetBlock delegate and its param must all outlive FPDF_CloseDocument.
    [StructLayout(LayoutKind.Sequential)]
    internal struct FpdfFileAccess
    {
        public CULong FileLen;
        public IntPtr GetBlock;
        public IntPtr Param;
    }

    // `int m_GetBlock(void* param, unsigned long position, unsigned char* pBuf, unsigned long buf_size)`;
    // a plain C function pointer, so Cdecl. Non-zero return means success.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int GetBlockCallback(IntPtr param, CULong position, IntPtr buffer, CULong size);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDF_LoadCustomDocument(IntPtr fileAccess,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDF_LoadMemDocument64(IntPtr data, nuint length, [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern uint FPDF_GetLastError();

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDF_GetPageCount(IntPtr document);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDF_GetPageSizeByIndexF(IntPtr document, int pageIndex, out PageSize size);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern void FPDF_ClosePage(IntPtr page);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFPage_CountObjects(IntPtr page);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDFPage_GetObject(IntPtr page, int index);

    // Left / bottom / right / top are PDF user-space floats (fpdf_edit.h), unlike RectF.
    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFPageObj_GetBounds(IntPtr pageObject, out float left, out float bottom, out float right, out float top);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern void FPDF_CloseDocument(IntPtr document);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr buffer, int stride);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern void FPDFBitmap_Destroy(IntPtr bitmap);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDF_RenderPageBitmap_Start(IntPtr bitmap, IntPtr page, int startX, int startY, int width, int height, int rotation, int flags, ref Pause pause);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDF_RenderPage_Continue(IntPtr page, ref Pause pause);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern void FPDF_RenderPage_Close(IntPtr page);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDF_PageToDevice(IntPtr page, int startX, int startY, int width, int height, int rotation, double pageX, double pageY, out int deviceX, out int deviceY);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDFText_LoadPage(IntPtr page);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern void FPDFText_ClosePage(IntPtr textPage);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFText_CountChars(IntPtr textPage);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern uint FPDFText_GetUnicode(IntPtr textPage, int index);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFText_GetCharBox(IntPtr textPage, int index, out double left, out double right, out double bottom, out double top);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDFText_FindStart(IntPtr textPage, IntPtr query, nuint flags, int startIndex);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFText_FindNext(IntPtr search);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFText_GetSchResultIndex(IntPtr search);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFText_GetSchCount(IntPtr search);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern void FPDFText_FindClose(IntPtr search);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFText_CountRects(IntPtr textPage, int startIndex, int count);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFText_GetRect(IntPtr textPage, int index, out double left, out double top, out double right, out double bottom);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDFBookmark_GetFirstChild(IntPtr document, IntPtr bookmark);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDFBookmark_GetNextSibling(IntPtr document, IntPtr bookmark);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern uint FPDFBookmark_GetTitle(IntPtr bookmark, [Out] byte[]? buffer, nuint bufferLength);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDFBookmark_GetDest(IntPtr document, IntPtr bookmark);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDFBookmark_GetAction(IntPtr bookmark);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern uint FPDFAction_GetType(IntPtr action);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDFAction_GetDest(IntPtr document, IntPtr action);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFDest_GetDestPageIndex(IntPtr document, IntPtr destination);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern uint FPDFDest_GetView(IntPtr destination, out uint numParams, [Out] float[]? parameters);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFDest_GetLocationInPage(IntPtr destination, out int hasX, out int hasY, out int hasZoom,
        out float x, out float y, out float zoom);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFLink_Enumerate(IntPtr page, ref int startPos, out IntPtr linkAnnot);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern int FPDFLink_GetAnnotRect(IntPtr linkAnnot, out RectF rect);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDFLink_GetDest(IntPtr document, IntPtr linkAnnot);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr FPDFLink_GetAction(IntPtr linkAnnot);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern uint FPDFAction_GetURIPath(IntPtr document, IntPtr action, [Out] byte[]? buffer, uint bufferLength);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern uint FPDFAction_GetFilePath(IntPtr action, [Out] byte[]? buffer, uint bufferLength);

    [DllImport(Library, CallingConvention = CallingConvention.Winapi)]
    internal static extern uint FPDF_GetPageLabel(IntPtr document, int pageIndex, [Out] byte[]? buffer, uint bufferLength);
}
