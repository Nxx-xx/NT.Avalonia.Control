using Avalonia;

namespace NT.AvaloniaControl.PdfViewer;

/// <summary>A page's display size in PDF points (72 points per inch), including its built-in rotation.</summary>
public sealed record PdfPageInfo(int Index, double Width, double Height);

/// <summary>An opaque, top-down BGRA32 image. The returned array belongs to the caller.</summary>
public sealed record PdfRenderedPage(int Width, int Height, int Stride, byte[] Pixels);

/// <summary>Extracted page text and its original PDFium character indices.</summary>
public sealed record PdfTextPage(IReadOnlyList<PdfTextCharacter> Characters, string Text);

/// <summary>
/// A PDFium character. Bounds use the page's displayed top-left origin in PDF points,
/// including its built-in rotation and crop, before a viewer's additional rotation.
/// An index is a PDFium character index, which need not equal a .NET UTF-16 string offset.
/// </summary>
public sealed record PdfTextCharacter(int Index, string Text, Rect Bounds);

/// <summary>A text match. StartIndex and Length use PDFium character indices.</summary>
public sealed record PdfSearchResult(int PageIndex, int StartIndex, int Length, IReadOnlyList<Rect> Bounds, string Text);

/// <summary>
/// One page's search matches, yielded by <c>PdfDocument.SearchPagesAsync</c> as soon as that
/// page has been scanned. Pages without matches still appear so callers can report progress.
/// </summary>
public sealed record PdfSearchPageResult(int PageIndex, IReadOnlyList<PdfSearchResult> Matches);

/// <summary>
/// A document outline entry. PageIndex is -1 for entries without an internal page destination.
/// Destination carries the full resolved target (page, position and zoom) when one exists.
/// </summary>
public sealed record PdfBookmark(string Title, int PageIndex, IReadOnlyList<PdfBookmark> Children)
{
    /// <summary>The complete internal destination; null for external or unresolved targets.</summary>
    public PdfLinkDestination? Destination { get; init; }
}

/// <summary>
/// A viewer's cache accounting snapshot. Byte fields describe distinct, non-overlapping
/// pools so hosts can sum them without double counting:
/// <list type="bullet">
/// <item><description><see cref="PageBitmapBytes"/> / <see cref="ThumbnailBitmapBytes"/> are the
/// retained final BGRA32 bitmaps (decode outputs kept for drawing).</description></item>
/// <item><description><see cref="TextBytes"/> is an estimate of the retained text pages
/// (character records and their strings), not an exact heap measurement.</description></item>
/// <item><description><see cref="DecodeBufferBytes"/> counts the managed pixel arrays of
/// in-flight renders. PDFium draws into these same arrays through the wrapped FPDFBitmap
/// target, so they are also the native render-target memory; the count falls back to zero
/// when no render is running even though the caller may still hold a just-returned page.</description></item>
/// <item><description><see cref="InputBytes"/> is the document data PDFium reads (a pinned
/// byte array, a staged temp file or the open file's snapshotted length) and
/// <see cref="InputBytesServed"/> counts blocks actually fetched on random-access loads.</description></item>
/// </list>
/// PDFium's internal heap (parsed objects, font and path caches) is not measurable through
/// the interop surface and is deliberately not claimed here.
/// <see cref="BudgetedBytes"/> is the three budgeted pools (page + thumbnail + text);
/// <see cref="TrackedBytes"/> adds input and in-flight decode buffers on top.
/// </summary>
public sealed record PdfViewerCacheStatistics(
    int PageBitmapCount,
    long PageBitmapBytes,
    int ThumbnailCount,
    long ThumbnailBitmapBytes,
    int TextPageCount,
    long TextBytes,
    long DecodeBufferBytes,
    long DecodeBufferPeakBytes,
    long InputBytes,
    long InputBytesServed,
    long BudgetedBytes,
    long TrackedBytes);

/// <summary>A document could not be loaded or processed by PDFium.</summary>
public sealed class PdfDocumentException : IOException
{
    public PdfDocumentException(string message, uint errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>The PDFium FPDF_ERR_* code; 4 indicates a required or incorrect password.</summary>
    public uint ErrorCode { get; }

    public PdfErrorKind Kind => ErrorCode switch
    {
        2 => PdfErrorKind.FileAccess,
        3 => PdfErrorKind.InvalidDocument,
        4 => PdfErrorKind.PasswordRequiredOrIncorrect,
        5 => PdfErrorKind.UnsupportedSecurity,
        6 => PdfErrorKind.PageContent,
        _ => PdfErrorKind.Unknown
    };
}
