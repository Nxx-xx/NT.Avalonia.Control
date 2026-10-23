using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using NT.AvaloniaControl.PdfViewer.Interop;

namespace NT.AvaloniaControl.PdfViewer;

/// <summary>
/// An owned, read-only PDFium document. Calls run off the UI thread and are serialized
/// across all documents. Dispose starts cancellation and release without blocking;
/// DisposeAsync waits until the native handle and pinned input have actually been released.
/// </summary>
public sealed partial class PdfDocument : IDisposable, IAsyncDisposable
{
    private IntPtr _handle;
    private GCHandle _dataPin;
    private PdfFileInput? _fileInput;
    private readonly PdfDocumentLifetime _lifetime;
    // Per-page content bounds, computed on first use and cached. Touched only inside the
    // serialized scheduler work items, so no additional synchronization is required.
    private Rect?[]? _contentBoundsCache;
    private bool[]? _contentBoundsComputed;
    // Decode destination buffers currently owned by in-flight renders, and their high-water
    // mark since load. Each buffer is the managed array PDFium draws into through the wrapped
    // FPDFBitmap, so it is both the managed decode intermediate and the native render target.
    private long _decodeBufferBytes;
    private long _decodeBufferPeakBytes;
    private readonly long _inputBytes;

    private PdfDocument(IntPtr handle, GCHandle dataPin, PdfFileInput? fileInput, PdfPageInfo[] pages,
        string[] pageLabels, IReadOnlyList<PdfBookmark> bookmarks)
    {
        InitializeContracts(handle);
        _handle = handle;
        _dataPin = dataPin;
        _fileInput = fileInput;
        _inputBytes = fileInput?.Length
            ?? (dataPin.IsAllocated && dataPin.Target is byte[] owned ? owned.Length : 0);
        Pages = Array.AsReadOnly(pages);
        PageLabels = Array.AsReadOnly(pageLabels);
        Bookmarks = bookmarks;
        _lifetime = new PdfDocumentLifetime(ReleaseNativeResources,
            release => PdfiumRuntime.RunAsync(() => { release(); return true; }, CancellationToken.None));
    }

    /// <summary>The immutable page sizes, available without calling native code.</summary>
    public IReadOnlyList<PdfPageInfo> Pages { get; }

    /// <summary>The document's immutable outline tree. External destinations are not followed.</summary>
    public IReadOnlyList<PdfBookmark> Bookmarks { get; }

    /// <summary>
    /// Opens an owned copy of the bytes. The caller may change or release the source array
    /// after this method returns. Passwords are passed to PDFium as UTF-8.
    /// </summary>
    public static Task<PdfDocument> LoadAsync(byte[] bytes, string? password = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes.Length == 0)
            throw new PdfDocumentException(PdfViewerStrings.ErrorEmptyDocument, 3);

        var ownedBytes = (byte[])bytes.Clone();
        return LoadOwnedAsync(ownedBytes, password, cancellationToken);
    }

    /// <summary>
    /// Opens a local file with random access: PDFium reads blocks on demand and no managed copy
    /// of the whole file is made. The file is opened for shared read/write/delete with its
    /// length snapshotted at open; later external edits are not observed (truncation surfaces as
    /// read errors rather than mixed content). The file handle stays open until disposal.
    /// Passwords are passed to PDFium as UTF-8.
    /// </summary>
    public static Task<PdfDocument> LoadFromFileAsync(string path, string? password = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        // Open outside the native gate; only PDFium calls are serialized.
        var input = PdfFileInput.OpenFile(Path.GetFullPath(path));
        return LoadInputAsync(input, password, cancellationToken);
    }

    /// <summary>
    /// Reads from the stream's current position into an independent copy. A seekable stream with
    /// a known length is read into a single exact-size buffer; a non-seekable or oversized
    /// stream is staged to an owned temp file and loaded with random access, so the input never
    /// has to fit twice in managed memory. Unless leaveOpen is true, the stream is disposed even
    /// when loading fails or is canceled.
    /// </summary>
    public static async Task<PdfDocument> LoadAsync(Stream stream, string? password = null, bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stream.CanSeek)
            {
                var remaining = stream.Length - stream.Position;
                if (remaining == 0)
                {
                    // Touch the stream once so stream-side failures, including read-time
                    // cancellation, surface before the empty-document error.
                    if (await stream.ReadAsync(Memory<byte>.Empty, cancellationToken).ConfigureAwait(false) <= 0)
                        throw new PdfDocumentException(PdfViewerStrings.ErrorEmptyDocument, 3);
                    // A broken Length claim falls through to the staging path below.
                }
                else if (remaining <= int.MaxValue)
                {
                    // One exact-size buffer instead of a growing MemoryStream plus ToArray copy.
                    var owned = GC.AllocateUninitializedArray<byte>((int)remaining);
                    try
                    {
                        await stream.ReadExactlyAsync(owned, cancellationToken).ConfigureAwait(false);
                    }
                    catch (EndOfStreamException)
                    {
                        // Length claimed more than the stream delivers; never load a partial mix.
                        throw new PdfDocumentException(PdfViewerStrings.ErrorLoadInvalid, 3);
                    }
                    return await LoadOwnedAsync(owned, password, cancellationToken).ConfigureAwait(false);
                }
            }

            // Non-addressable input: stage to an owned temp file and load with random access.
            // An empty stream surfaces as the same empty-document error as the memory path.
            var input = await PdfFileInput.StageStreamAsync(stream, cancellationToken).ConfigureAwait(false);
            return await LoadInputAsync(input, password, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!leaveOpen)
                await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static Task<PdfDocument> LoadOwnedAsync(byte[] bytes, string? password, CancellationToken cancellationToken)
    {
        return PdfiumRuntime.RunAsync(() =>
        {
            var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var handle = IntPtr.Zero;
            try
            {
                handle = PdfiumNative.FPDF_LoadMemDocument64(pin.AddrOfPinnedObject(), (nuint)bytes.Length, password);
                if (handle == IntPtr.Zero)
                    throw CreateLoadException();
                return FinishLoad(handle, pin, null, cancellationToken);
            }
            catch
            {
                try
                {
                    if (handle != IntPtr.Zero)
                        PdfiumNative.FPDF_CloseDocument(handle);
                }
                finally
                {
                    pin.Free();
                }
                throw;
            }
        }, cancellationToken);
    }

    private static async Task<PdfDocument> LoadInputAsync(PdfFileInput input, string? password,
        CancellationToken cancellationToken)
    {
        try
        {
            return await PdfiumRuntime.RunAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var handle = PdfiumNative.FPDF_LoadCustomDocument(input.NativeAccess, password);
                if (handle == IntPtr.Zero)
                    throw CreateLoadException();
                try
                {
                    return FinishLoad(handle, default, input, cancellationToken);
                }
                catch
                {
                    PdfiumNative.FPDF_CloseDocument(handle);
                    throw;
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Idempotent: covers a canceled queue item, a failed load and the finished transfer.
            input.Dispose();
            throw;
        }
    }

    // Runs inside the native gate with an open handle; transfers ownership to the document.
    private static PdfDocument FinishLoad(IntPtr handle, GCHandle dataPin, PdfFileInput? fileInput,
        CancellationToken cancellationToken)
    {
        var count = PdfiumNative.FPDF_GetPageCount(handle);
        if (count <= 0)
            throw new PdfDocumentException(PdfViewerStrings.ErrorNoReadablePages, 6);

        var pages = new PdfPageInfo[count];
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PdfiumNative.FPDF_GetPageSizeByIndexF(handle, index, out var size) == 0 ||
                !float.IsFinite(size.Width) || !float.IsFinite(size.Height) || size.Width <= 0 || size.Height <= 0)
                throw new PdfDocumentException(string.Format(PdfViewerStrings.ErrorPageSizeUnreadableFormat, index + 1), 6);
            pages[index] = new PdfPageInfo(index, size.Width, size.Height);
        }

        var pageLabels = ReadPageLabels(handle, count, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var bookmarks = ReadBookmarks(handle, pages, IntPtr.Zero, new HashSet<IntPtr>(), 0, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // A gap during load means PDFium parsed zero-filled tail bytes; refuse to hand out a
        // document whose page tree may mix snapshot and replacement bytes.
        if (fileInput is { ReadGaps: > 0 })
            throw new PdfDocumentException(PdfViewerStrings.ErrorFileChanged, 2);
        return new PdfDocument(handle, dataPin, fileInput, pages, pageLabels, bookmarks);
    }

    /// <summary>
    /// Renders a page to opaque BGRA32 pixels. Rotation is an additional clockwise rotation
    /// of 0, 90, 180, or 270 degrees. Cancellation is observed between progressive rendering steps.
    /// </summary>
    public Task<PdfRenderedPage> RenderAsync(int pageIndex, int pixelWidth, int pixelHeight, int rotation = 0,
        CancellationToken cancellationToken = default) =>
        RenderCoreAsync(pageIndex, pixelWidth, pixelHeight, rotation, cancellationToken, PdfiumWorkPriority.Foreground);

    /// <summary>
    /// Renders one rectangular region of a page rasterized at
    /// <paramref name="rasterWidth"/>×<paramref name="rasterHeight"/> pixels into an opaque BGRA32
    /// bitmap of <paramref name="regionWidth"/>×<paramref name="regionHeight"/> pixels, without
    /// materializing the full raster. This is the tile primitive for display tiling: regions of the
    /// same raster map 1:1 to the corresponding crop of a full-page render, stitch seamlessly at
    /// shared edges and stay fully opaque. Region coordinates are measured in the rotated display
    /// raster (the same space <see cref="RenderAsync"/> produces). Cancellation is observed between
    /// progressive rendering steps; a region render is one render pass.
    /// </summary>
    public Task<PdfRenderedPage> RenderPageRegionAsync(int pageIndex, int rasterWidth, int rasterHeight,
        int regionX, int regionY, int regionWidth, int regionHeight, int rotation = 0,
        CancellationToken cancellationToken = default)
    {
        if (regionX < 0 || regionY < 0 || regionWidth <= 0 || regionHeight <= 0 ||
            regionX + regionWidth > rasterWidth || regionY + regionHeight > rasterHeight)
            throw new ArgumentOutOfRangeException(nameof(regionX), PdfViewerStrings.ErrorRenderRegionRange);
        return RenderCoreAsync(pageIndex, rasterWidth, rasterHeight, rotation, cancellationToken,
            PdfiumWorkPriority.Foreground, (regionX, regionY, regionWidth, regionHeight));
    }

    internal Task<PdfRenderedPage> RenderThumbnailAsync(int pageIndex, int pixelWidth, int pixelHeight,
        int rotation = 0, CancellationToken cancellationToken = default) =>
        RenderCoreAsync(pageIndex, pixelWidth, pixelHeight, rotation, cancellationToken, PdfiumWorkPriority.Thumbnail);

    private Task<PdfRenderedPage> RenderCoreAsync(int pageIndex, int pixelWidth, int pixelHeight, int rotation,
        CancellationToken cancellationToken, PdfiumWorkPriority priority,
        (int X, int Y, int Width, int Height)? region = null)
    {
        ValidatePage(pageIndex);
        if (pixelWidth is <= 0 or > 16384)
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), PdfViewerStrings.ErrorRenderWidthRange);
        if (pixelHeight is <= 0 or > 16384)
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), PdfViewerStrings.ErrorRenderHeightRange);
        if ((long)pixelWidth * pixelHeight > 64L * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), PdfViewerStrings.ErrorRenderMegapixels);
        if (rotation is not (0 or 90 or 180 or 270))
            throw new ArgumentOutOfRangeException(nameof(rotation), PdfViewerStrings.ErrorRotationRange);
        var outputWidth = region?.Width ?? pixelWidth;
        var outputHeight = region?.Height ?? pixelHeight;

        return RunDocumentAsync(token =>
        {
            var page = LoadPage(pageIndex);
            var bitmap = IntPtr.Zero;
            var pixelPin = default(GCHandle);
            PdfiumNative.NeedToPauseCallback? pauseCallback = null;
            var renderStarted = false;
            var decodeAccountedBytes = 0L;
            try
            {
                token.ThrowIfCancellationRequested();
                var stride = checked(outputWidth * 4);
                var pixels = GC.AllocateUninitializedArray<byte>(checked(stride * outputHeight));
                decodeAccountedBytes = pixels.LongLength;
                AddDecodeBuffer(decodeAccountedBytes);
                pixelPin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                bitmap = PdfiumNative.FPDFBitmap_CreateEx(outputWidth, outputHeight, 4, pixelPin.AddrOfPinnedObject(), stride);
                if (bitmap == IntPtr.Zero)
                    throw new PdfDocumentException(string.Format(PdfViewerStrings.ErrorPageBitmapCreateFormat, pageIndex + 1), 1);
                PdfiumNative.FPDFBitmap_FillRect(bitmap, 0, 0, outputWidth, outputHeight, 0xffffffff);

                // The callback never throws across the native boundary. PDFium pauses at
                // its own safe points, then the managed loop observes cancellation.
                pauseCallback = _ => token.IsCancellationRequested ? 1 : 0;
                var pause = new PdfiumNative.Pause
                {
                    Version = 1,
                    NeedToPauseNow = Marshal.GetFunctionPointerForDelegate(pauseCallback)
                };
                renderStarted = true;
                // A region render places the full-page raster at (-X, -Y) inside the smaller
                // tile bitmap; PDFium clips drawing to the bitmap, so the tile holds exactly
                // the requested crop. (0, 0) with the bitmap sized to the raster is the
                // full-page path.
                var status = PdfiumNative.FPDF_RenderPageBitmap_Start(bitmap, page,
                    -(region?.X ?? 0), -(region?.Y ?? 0), pixelWidth, pixelHeight,
                    rotation / 90, 0x01 | 0x02 | 0x200, ref pause);
                while (status == 1) // FPDF_RENDER_TOBECONTINUED
                {
                    token.ThrowIfCancellationRequested();
                    status = PdfiumNative.FPDF_RenderPage_Continue(page, ref pause);
                }
                token.ThrowIfCancellationRequested();
                if (status != 2) // FPDF_RENDER_DONE
                    throw new PdfDocumentException(string.Format(PdfViewerStrings.ErrorPageRenderFormat, pageIndex + 1), 6);
                return new PdfRenderedPage(outputWidth, outputHeight, stride, pixels);
            }
            finally
            {
                // The caller receives the pixel array with the rendered page; accounting ends
                // at this handoff even though the array lives until the caller drops it.
                if (decodeAccountedBytes > 0) RemoveDecodeBuffer(decodeAccountedBytes);
                if (renderStarted)
                    PdfiumNative.FPDF_RenderPage_Close(page);
                GC.KeepAlive(pauseCallback);
                if (bitmap != IntPtr.Zero)
                    PdfiumNative.FPDFBitmap_Destroy(bitmap);
                if (pixelPin.IsAllocated)
                    pixelPin.Free();
                PdfiumNative.FPDF_ClosePage(page);
            }
        }, cancellationToken, priority);
    }

    /// <summary>
    /// Extracts Unicode characters and their bounds. Image-only pages return empty text;
    /// this method does not perform OCR. The PDF must permit copying or extracting text.
    /// </summary>
    public Task<PdfTextPage> GetTextAsync(int pageIndex, CancellationToken cancellationToken = default)
    {
        ValidatePage(pageIndex);
        if (!Permissions.CanCopy)
            throw new UnauthorizedAccessException(PdfViewerStrings.ErrorCopyDenied);
        return RunDocumentAsync(token =>
        {
            var page = LoadPage(pageIndex);
            var textPage = IntPtr.Zero;
            try
            {
                textPage = LoadTextPage(page, pageIndex);
                var count = PdfiumNative.FPDFText_CountChars(textPage);
                if (count < 0)
                    throw new PdfDocumentException(string.Format(PdfViewerStrings.ErrorPageTextUnreadableFormat, pageIndex + 1), 6);

                var characters = new PdfTextCharacter[count];
                var text = new StringBuilder(count);
                var coordinates = new PageCoordinates(page, Pages[pageIndex]);
                for (var index = 0; index < count; index++)
                {
                    if ((index & 63) == 0)
                        token.ThrowIfCancellationRequested();
                    var character = GetCharacterText(textPage, index);
                    var bounds = default(Rect);
                    if (PdfiumNative.FPDFText_GetCharBox(textPage, index, out var left, out var right, out var bottom, out var top) != 0)
                        bounds = coordinates.GetBounds(left, top, right, bottom);
                    characters[index] = new PdfTextCharacter(index, character, bounds);
                    text.Append(character);
                }
                token.ThrowIfCancellationRequested();
                return new PdfTextPage(Array.AsReadOnly(characters), text.ToString());
            }
            finally
            {
                if (textPage != IntPtr.Zero)
                    PdfiumNative.FPDFText_ClosePage(textPage);
                PdfiumNative.FPDF_ClosePage(page);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// The union of the page's content-object bounds in displayed top-left origin PDF points
    /// (the same space as PdfTextCharacter.Bounds), or null when the page holds no drawable
    /// content. This is the content box the /FitB, /FitBH and /FitBV destinations fit; callers
    /// fall back to the page box when the result is null. Bounds are computed on demand and
    /// cached per page.
    /// </summary>
    public Task<Rect?> GetPageContentBoundsAsync(int pageIndex, CancellationToken cancellationToken = default)
    {
        ValidatePage(pageIndex);
        return RunDocumentAsync(token => GetPageContentBounds(pageIndex, token), cancellationToken);
    }

    private Rect? GetPageContentBounds(int pageIndex, CancellationToken token)
    {
        _contentBoundsCache ??= new Rect?[Pages.Count];
        _contentBoundsComputed ??= new bool[Pages.Count];
        if (_contentBoundsComputed[pageIndex]) return _contentBoundsCache[pageIndex];
        var page = LoadPage(pageIndex);
        try
        {
            var count = PdfiumNative.FPDFPage_CountObjects(page);
            double left = double.PositiveInfinity, bottom = double.PositiveInfinity;
            double right = double.NegativeInfinity, top = double.NegativeInfinity;
            for (var index = 0; index < count; index++)
            {
                if ((index & 63) == 0) token.ThrowIfCancellationRequested();
                var item = PdfiumNative.FPDFPage_GetObject(page, index);
                if (item == IntPtr.Zero) continue;
                if (PdfiumNative.FPDFPageObj_GetBounds(item, out var itemLeft, out var itemBottom, out var itemRight, out var itemTop) == 0)
                    continue;
                if (!float.IsFinite(itemLeft) || !float.IsFinite(itemBottom) || !float.IsFinite(itemRight) || !float.IsFinite(itemTop))
                    continue;
                // A degenerate box contributes nothing: clips and empty paths are not content.
                if (itemLeft == itemRight || itemBottom == itemTop) continue;
                left = Math.Min(left, itemLeft);
                bottom = Math.Min(bottom, itemBottom);
                right = Math.Max(right, itemRight);
                top = Math.Max(top, itemTop);
            }
            Rect? bounds = null;
            if (!double.IsInfinity(left))
            {
                var mapped = new PageCoordinates(page, Pages[pageIndex]).GetBounds(left, top, right, bottom);
                if (mapped.Width > 0 && mapped.Height > 0) bounds = mapped;
            }
            _contentBoundsCache[pageIndex] = bounds;
            _contentBoundsComputed[pageIndex] = true;
            return bounds;
        }
        finally { PdfiumNative.FPDF_ClosePage(page); }
    }

    /// <summary>
    /// Searches every page using PDFium's Unicode search. Results are ordered by page
    /// and character position, with at most maxResults matches.
    /// </summary>
    public async Task<IReadOnlyList<PdfSearchResult>> SearchAsync(string query, bool matchCase = false, bool wholeWord = false,
        int maxResults = 10000, CancellationToken cancellationToken = default)
    {
        var results = new List<PdfSearchResult>();
        await foreach (var page in SearchPagesAsync(query, matchCase, wholeWord, maxResults, cancellationToken).ConfigureAwait(false))
            results.AddRange(page.Matches);
        return results.AsReadOnly();
    }

    /// <summary>
    /// Searches every page and yields each page's matches as soon as that page has been
    /// scanned, so callers can publish progressive results and report scan progress. Pages
    /// are yielded in order even when they hold no matches; the walk stops once maxResults
    /// matches have been collected in total.
    /// </summary>
    public async IAsyncEnumerable<PdfSearchPageResult> SearchPagesAsync(string query, bool matchCase = false, bool wholeWord = false,
        int maxResults = 10000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        EnsureUsable();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);
        cancellationToken.ThrowIfCancellationRequested();
        if (query.Contains('\0'))
            throw new ArgumentException(PdfViewerStrings.ErrorSearchQueryNul, nameof(query));
        if (query.Length == 0)
            yield break;

        var queryBytes = Encoding.Unicode.GetBytes(query + '\0');
        nuint flags = (nuint)((matchCase ? 1 : 0) | (wholeWord ? 2 : 0));
        var total = 0;
        for (var pageIndex = 0; pageIndex < Pages.Count && total < maxResults; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = pageIndex;
            var remaining = maxResults - total;
            // Return to the global queue between pages so rendering and other documents
            // can make progress during a long document-wide search.
            var pageResults = await RunDocumentAsync(
                token => SearchPage(index, queryBytes, flags, remaining, token), cancellationToken,
                PdfiumWorkPriority.Search).ConfigureAwait(false);
            total += pageResults.Count;
            yield return new PdfSearchPageResult(pageIndex, pageResults.AsReadOnly());
            if (total >= maxResults)
                yield break;
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Extracts the whole document's text, pages separated by a form feed ('\f'). Image-only
    /// pages contribute empty text; this method does not perform OCR. The PDF must permit
    /// copying or extracting its text.
    /// </summary>
    public async Task<string> GetFullTextAsync(CancellationToken cancellationToken = default)
    {
        EnsureUsable();
        if (!Permissions.CanCopy)
            throw new UnauthorizedAccessException(PdfViewerStrings.ErrorCopyDenied);
        var builder = new StringBuilder();
        for (var pageIndex = 0; pageIndex < Pages.Count; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pageIndex > 0) builder.Append('\f');
            var page = await GetTextAsync(pageIndex, cancellationToken).ConfigureAwait(false);
            builder.Append(page.Text);
        }
        return builder.ToString();
    }

    private List<PdfSearchResult> SearchPage(int pageIndex, byte[] queryBytes, nuint flags, int limit, CancellationToken cancellationToken)
    {
        var results = new List<PdfSearchResult>();
        var page = LoadPage(pageIndex);
        var textPage = IntPtr.Zero;
        var search = IntPtr.Zero;
        var queryPin = default(GCHandle);
        try
        {
            textPage = LoadTextPage(page, pageIndex);
            queryPin = GCHandle.Alloc(queryBytes, GCHandleType.Pinned);
            search = PdfiumNative.FPDFText_FindStart(textPage, queryPin.AddrOfPinnedObject(), flags, 0);
            if (search == IntPtr.Zero)
                throw new PdfDocumentException(string.Format(PdfViewerStrings.ErrorPageSearchStartFormat, pageIndex + 1), 6);
            var coordinates = new PageCoordinates(page, Pages[pageIndex]);
            while (results.Count < limit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (PdfiumNative.FPDFText_FindNext(search) == 0)
                    break;
                var start = PdfiumNative.FPDFText_GetSchResultIndex(search);
                var length = PdfiumNative.FPDFText_GetSchCount(search);
                if (start < 0 || length <= 0)
                    break;

                var rectCount = PdfiumNative.FPDFText_CountRects(textPage, start, length);
                var bounds = new List<Rect>(Math.Max(0, rectCount));
                for (var rectIndex = 0; rectIndex < rectCount; rectIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (PdfiumNative.FPDFText_GetRect(textPage, rectIndex, out var left, out var top, out var right, out var bottom) != 0)
                    {
                        var rect = coordinates.GetBounds(left, top, right, bottom);
                        if (rect.Width > 0 && rect.Height > 0)
                            bounds.Add(rect);
                    }
                }
                var text = new StringBuilder(length);
                for (var character = start; character < start + length; character++)
                {
                    if ((character & 63) == 0)
                        cancellationToken.ThrowIfCancellationRequested();
                    text.Append(GetCharacterText(textPage, character));
                }
                results.Add(new PdfSearchResult(pageIndex, start, length, bounds.AsReadOnly(), text.ToString()));
            }
            return results;
        }
        finally
        {
            if (search != IntPtr.Zero)
                PdfiumNative.FPDFText_FindClose(search);
            if (queryPin.IsAllocated)
                queryPin.Free();
            if (textPage != IntPtr.Zero)
                PdfiumNative.FPDFText_ClosePage(textPage);
            PdfiumNative.FPDF_ClosePage(page);
        }
    }

    private static string GetCharacterText(IntPtr textPage, int index)
    {
        var unicode = PdfiumNative.FPDFText_GetUnicode(textPage, index);
        if (unicode == 0)
            return string.Empty;
        if (unicode <= char.MaxValue)
            return ((char)unicode).ToString();
        return unicode <= 0x10ffff ? char.ConvertFromUtf32((int)unicode) : "\ufffd";
    }

    private static IReadOnlyList<PdfBookmark> ReadBookmarks(IntPtr document, IReadOnlyList<PdfPageInfo> pages,
        IntPtr parent, HashSet<IntPtr> visited, int depth, CancellationToken cancellationToken)
    {
        // A malformed outline can contain cycles. Bound traversal depth and total nodes
        // as well as tracking handles so it cannot recurse or allocate indefinitely.
        if (depth >= 128 || visited.Count >= 100000)
            return Array.Empty<PdfBookmark>();
        var result = new List<PdfBookmark>();
        var bookmark = PdfiumNative.FPDFBookmark_GetFirstChild(document, parent);
        while (bookmark != IntPtr.Zero && visited.Count < 100000 && visited.Add(bookmark))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var titleLength = PdfiumNative.FPDFBookmark_GetTitle(bookmark, null, 0);
            var title = string.Empty;
            if (titleLength is > 2 and <= 1024 * 1024)
            {
                var titleBytes = new byte[titleLength];
                PdfiumNative.FPDFBookmark_GetTitle(bookmark, titleBytes, (nuint)titleBytes.Length);
                title = Encoding.Unicode.GetString(titleBytes, 0, titleBytes.Length / 2 * 2).TrimEnd('\0');
            }

            var destination = PdfiumNative.FPDFBookmark_GetDest(document, bookmark);
            if (destination == IntPtr.Zero)
            {
                var action = PdfiumNative.FPDFBookmark_GetAction(bookmark);
                // Follow only in-document navigation. Never launch external files or URLs.
                if (action != IntPtr.Zero && PdfiumNative.FPDFAction_GetType(action) == PdfiumNative.ActionGoto)
                    destination = PdfiumNative.FPDFAction_GetDest(document, action);
            }
            PdfLinkDestination? resolved = null;
            var pageIndex = -1;
            if (destination != IntPtr.Zero)
            {
                resolved = ResolveDestinationCore(document, pages, destination, cancellationToken);
                pageIndex = resolved.PageIndex;
            }
            var children = ReadBookmarks(document, pages, bookmark, visited, depth + 1, cancellationToken);
            result.Add(new PdfBookmark(title, pageIndex, children) { Destination = resolved });
            bookmark = PdfiumNative.FPDFBookmark_GetNextSibling(document, bookmark);
        }
        return result.AsReadOnly();
    }

    private IntPtr LoadPage(int pageIndex)
    {
        var page = PdfiumNative.FPDF_LoadPage(_handle, pageIndex);
        if (page == IntPtr.Zero)
            throw new PdfDocumentException(string.Format(PdfViewerStrings.ErrorPageLoadFormat, pageIndex + 1), 6);
        return page;
    }

    private static IntPtr LoadTextPage(IntPtr page, int pageIndex)
    {
        var textPage = PdfiumNative.FPDFText_LoadPage(page);
        if (textPage == IntPtr.Zero)
            throw new PdfDocumentException(string.Format(PdfViewerStrings.ErrorPageTextLoadFormat, pageIndex + 1), 6);
        return textPage;
    }

    private static PdfDocumentException CreateLoadException()
    {
        var code = PdfiumNative.FPDF_GetLastError();
        return new PdfDocumentException(code switch
        {
            2 => PdfViewerStrings.ErrorLoadUnreadable,
            3 => PdfViewerStrings.ErrorLoadInvalid,
            4 => PdfViewerStrings.ErrorLoadPassword,
            5 => PdfViewerStrings.ErrorLoadSecurity,
            6 => PdfViewerStrings.ErrorLoadPageData,
            _ => PdfViewerStrings.ErrorLoadUnknown
        }, code);
    }

    private void ValidatePage(int pageIndex)
    {
        EnsureUsable();
        if ((uint)pageIndex >= (uint)Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
    }

    private void EnsureUsable()
    {
        ObjectDisposedException.ThrowIf(_lifetime.IsDisposeRequested, this);
    }

    private async Task<T> RunDocumentAsync<T>(Func<CancellationToken, T> operation, CancellationToken cancellationToken,
        PdfiumWorkPriority priority = PdfiumWorkPriority.Foreground)
    {
        // Once the external file diverged from the snapshot, PDFium's parsed objects may already
        // mix versions, so every content operation fails cleanly until the document is reloaded.
        EnsureInputIntact();
        using var lease = _lifetime.Acquire();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.DisposeToken);
        try
        {
            var result = await PdfiumRuntime.RunAsync(() => operation(linkedCancellation.Token), linkedCancellation.Token, priority)
                .ConfigureAwait(false);
            EnsureInputIntact();
            return result;
        }
        catch (PdfDocumentException)
        {
            // Prefer the change error over the downstream page error a zero-filled read caused.
            EnsureInputIntact();
            throw;
        }
    }

    private void EnsureInputIntact()
    {
        if (_fileInput is { ReadGaps: > 0 })
            throw new PdfDocumentException(PdfViewerStrings.ErrorFileChanged, 2);
    }

    private void AddDecodeBuffer(long bytes)
    {
        var current = Interlocked.Add(ref _decodeBufferBytes, bytes);
        long peak;
        while (current > (peak = Interlocked.Read(ref _decodeBufferPeakBytes)))
            if (Interlocked.CompareExchange(ref _decodeBufferPeakBytes, current, peak) == peak) break;
    }

    private void RemoveDecodeBuffer(long bytes) => Interlocked.Add(ref _decodeBufferBytes, -bytes);

    internal Task WhenDisposed => _lifetime.Completion;

    /// <summary>Test hook: the owned staged temp path for non-addressable stream loads.</summary>
    internal string? StagedTempPath => _fileInput?.StagedPath;

    /// <summary>Logical input size PDFium reads: pinned bytes, staged temp file or snapshotted file length.</summary>
    internal long InputBytes => _inputBytes;

    /// <summary>Bytes of decode destination buffers inside renders that are still running.</summary>
    internal long DecodeBufferBytes => Interlocked.Read(ref _decodeBufferBytes);

    /// <summary>High-water mark of concurrent decode destination buffers since the load.</summary>
    internal long DecodeBufferPeakBytes => Interlocked.Read(ref _decodeBufferPeakBytes);

    /// <summary>Test hook: total bytes served to PDFium through random access.</summary>
    internal long InputBytesServed => _fileInput?.BytesServed ?? 0;

    /// <summary>Test hook: zero-filled block reads caused by external file changes.</summary>
    internal long InputReadGaps => _fileInput?.ReadGaps ?? 0;

    /// <summary>Rejects new work immediately and starts asynchronous native release.</summary>
    public void Dispose()
    {
        _lifetime.RequestDispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Stops admitting work and waits until native handles and pinned bytes are released.</summary>
    public ValueTask DisposeAsync()
    {
        _lifetime.RequestDispose();
        GC.SuppressFinalize(this);
        return new ValueTask(_lifetime.Completion);
    }

    private void ReleaseNativeResources()
    {
        var handle = _handle;
        _handle = IntPtr.Zero;
        try
        {
            // The input must outlive the native handle: PDFium can still read blocks while
            // closing the document. It is released only after FPDF_CloseDocument returns.
            if (handle != IntPtr.Zero)
                PdfiumNative.FPDF_CloseDocument(handle);
        }
        finally
        {
            if (_dataPin.IsAllocated)
                _dataPin.Free();
            _fileInput?.Dispose();
            _fileInput = null;
        }
    }

    ~PdfDocument()
    {
        try
        {
            _lifetime?.RequestDispose();
        }
        catch
        {
            // A finalizer must not terminate the host process during native teardown.
        }
    }

    private readonly struct PageCoordinates
    {
        private readonly IntPtr _page;
        private readonly PdfPageInfo _info;
        private readonly int _deviceWidth;
        private readonly int _deviceHeight;

        internal PageCoordinates(IntPtr page, PdfPageInfo info)
        {
            _page = page;
            _info = info;
            // PageToDevice accounts for PDFium's crop and intrinsic rotation. Use a
            // large virtual device, then scale down, to avoid rounding at 72dpi.
            _deviceWidth = (int)Math.Clamp(Math.Ceiling(info.Width * 1024), 1, 2000000);
            _deviceHeight = (int)Math.Clamp(Math.Ceiling(info.Height * 1024), 1, 2000000);
        }

        internal Rect GetBounds(double left, double top, double right, double bottom)
        {
            if (!double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(right) || !double.IsFinite(bottom))
                return default;
            if (!TryMap(left, top, out var a) || !TryMap(right, top, out var b) ||
                !TryMap(right, bottom, out var c) || !TryMap(left, bottom, out var d))
                return default;
            var x1 = Math.Clamp(Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)), 0, _info.Width);
            var y1 = Math.Clamp(Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)), 0, _info.Height);
            var x2 = Math.Clamp(Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)), 0, _info.Width);
            var y2 = Math.Clamp(Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)), 0, _info.Height);
            return new Rect(x1, y1, x2 - x1, y2 - y1);
        }

        private bool TryMap(double x, double y, out Point result)
        {
            var success = PdfiumNative.FPDF_PageToDevice(_page, 0, 0, _deviceWidth, _deviceHeight, 0, x, y,
                out var deviceX, out var deviceY) != 0;
            result = new Point(deviceX * _info.Width / _deviceWidth, deviceY * _info.Height / _deviceHeight);
            return success;
        }
    }
}
