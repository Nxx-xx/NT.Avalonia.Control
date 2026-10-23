using System.Buffers;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using NT.AvaloniaControl.PdfViewer.Interop;

namespace NT.AvaloniaControl.PdfViewer.Editing;

/// <summary>
/// The native, whole-object stage of the bounded redaction pipeline. This stage
/// does not clean hidden information or certify resource removal. Every call must
/// execute while PdfiumRuntime's process-wide native gate is held.
/// </summary>
internal static class PdfContentRedactor
{
    private const int MaximumRegions = 1024;
    private const int MaximumObjects = 200_000;
    private const int MaximumDocumentBytes = 256 * 1024 * 1024;
    // PDFium exposes single-precision bounds. Permit only rounding noise, not a
    // user-visible sliver of a partially covered object.
    private const double BoundaryTolerance = 0.001;

    internal static byte[] Apply(byte[] source, IReadOnlyList<PdfRedactionRegion> regions,
        CancellationToken token)
    {
        ValidateArguments(source, regions);
        using var document = new NativeDocument(source, token);
        var pageRegions = GroupRegions(regions, document.PageCount);
        var pages = new List<PageSelection>(pageRegions.Count);
        try
        {
            var remainingBudget = MaximumObjects;
            var hits = new int[regions.Count];
            // Load and inspect every affected page before the first mutation.
            foreach (var (pageIndex, requests) in pageRegions)
            {
                token.ThrowIfCancellationRequested();
                var page = LoadPage(document.Handle, pageIndex);
                var selection = new PageSelection(pageIndex, page);
                pages.Add(selection);
                ValidatePageGeometry(page);
                var objectCount = CountObjects(page, ref remainingBudget);
                for (var index = 0; index < objectCount; ++index)
                {
                    token.ThrowIfCancellationRequested();
                    var item = ReadObject(page, index);
                    ValidateObjectGeometry(item.Handle, item.Type);
                    var intersects = false;
                    var covered = false;
                    foreach (var request in requests)
                    {
                        if (!Intersects(item.Bounds, request.Region.Bounds)) continue;
                        intersects = true;
                        if (!Contains(request.Region.Bounds, item.Bounds)) continue;
                        covered = true;
                        ++hits[request.Index];
                    }
                    if (intersects && !covered)
                        throw new NotSupportedException("A redaction region partially intersects a page object. This pipeline removes only fully covered text, image, path or Form objects.");
                    if (covered) selection.Removed.Add(item.Handle);
                    else selection.Retained.Add(new ObjectSnapshot(item.Type, item.Bounds));
                }
            }
            if (hits.Any(count => count == 0))
                throw new InvalidOperationException("Each redaction region must fully cover at least one supported page object; an empty selection is not reported as successful deletion.");

            token.ThrowIfCancellationRequested();
            foreach (var selection in pages)
            {
                foreach (var item in selection.Removed)
                {
                    token.ThrowIfCancellationRequested();
                    Require(Native.FPDFPage_RemoveObject(selection.Page, item) != 0, "remove a selected page object");
                    // Successful removal transfers ownership from the page.
                    Native.FPDFPageObj_Destroy(item);
                }
                Require(Native.FPDFPage_GenerateContent(selection.Page) != 0, "regenerate edited page content");
            }
            var candidate = Save(document.Handle, token);
            ValidateNativePreservation(candidate, pages, token);
            return candidate;
        }
        finally
        {
            foreach (var selection in pages) PdfiumNative.FPDF_ClosePage(selection.Page);
        }
    }

    /// <summary>Reopens the final structural output and verifies all target regions are empty.</summary>
    internal static void Audit(byte[] candidate, IReadOnlyList<PdfRedactionRegion> regions,
        CancellationToken token)
    {
        ValidateArguments(candidate, regions);
        using var document = new NativeDocument(candidate, token);
        var pageRegions = GroupRegions(regions, document.PageCount);
        var remainingBudget = MaximumObjects;
        foreach (var (pageIndex, requests) in pageRegions)
        {
            token.ThrowIfCancellationRequested();
            var page = LoadPage(document.Handle, pageIndex);
            try
            {
                ValidatePageGeometry(page);
                var count = CountObjects(page, ref remainingBudget);
                for (var index = 0; index < count; ++index)
                {
                    token.ThrowIfCancellationRequested();
                    var item = ReadObject(page, index);
                    ValidateObjectGeometry(item.Handle, item.Type);
                    if (requests.Any(request => Intersects(item.Bounds, request.Region.Bounds)))
                        throw new InvalidDataException("The final redaction output still contains a page object intersecting a target region.");
                }
            }
            finally { PdfiumNative.FPDF_ClosePage(page); }
        }
        token.ThrowIfCancellationRequested();
    }

    private static void ValidateNativePreservation(byte[] candidate, IReadOnlyList<PageSelection> before,
        CancellationToken token)
    {
        using var document = new NativeDocument(candidate, token);
        var remainingBudget = MaximumObjects;
        foreach (var selection in before)
        {
            token.ThrowIfCancellationRequested();
            var page = LoadPage(document.Handle, selection.PageIndex);
            try
            {
                var count = CountObjects(page, ref remainingBudget);
                if (count != selection.Retained.Count)
                    throw new InvalidDataException("Native content rewriting changed the number of retained page objects.");
                for (var index = 0; index < count; ++index)
                {
                    token.ThrowIfCancellationRequested();
                    var item = ReadObject(page, index);
                    var expected = selection.Retained[index];
                    if (item.Type != expected.Type || !item.Bounds.Near(expected.Bounds))
                        throw new InvalidDataException("Native content rewriting changed retained page-object geometry or ordering.");
                }
            }
            finally { PdfiumNative.FPDF_ClosePage(page); }
        }
    }

    private static void ValidateArguments(byte[] source, IReadOnlyList<PdfRedactionRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(regions);
        if (source.Length is 0 or > MaximumDocumentBytes)
            throw new ArgumentOutOfRangeException(nameof(source));
        if (regions.Count is 0 or > MaximumRegions)
            throw new ArgumentOutOfRangeException(nameof(regions), $"Use between 1 and {MaximumRegions} redaction regions.");
        foreach (var region in regions)
        {
            ArgumentNullException.ThrowIfNull(region);
            region.Bounds.Validate();
        }
    }

    private static SortedDictionary<int, List<RegionRequest>> GroupRegions(
        IReadOnlyList<PdfRedactionRegion> regions, int pageCount)
    {
        var result = new SortedDictionary<int, List<RegionRequest>>();
        for (var index = 0; index < regions.Count; ++index)
        {
            var region = regions[index];
            if (region.PageIndex < 0 || region.PageIndex >= pageCount)
                throw new ArgumentOutOfRangeException(nameof(regions), "A redaction page index is outside the document.");
            if (!result.TryGetValue(region.PageIndex, out var requests))
                result.Add(region.PageIndex, requests = []);
            requests.Add(new RegionRequest(index, region));
        }
        return result;
    }

    private static IntPtr LoadPage(IntPtr document, int index)
    {
        var page = PdfiumNative.FPDF_LoadPage(document, index);
        Require(page != IntPtr.Zero, "load a redaction page");
        return page;
    }

    private static int CountObjects(IntPtr page, ref int remainingBudget)
    {
        var count = Native.FPDFPage_CountObjects(page);
        if (count < 0 || count > remainingBudget)
            throw new NotSupportedException($"The affected pages exceed the bounded redaction limit of {MaximumObjects} native objects or have an unreadable object count.");
        remainingBudget -= count;
        return count;
    }

    private static void ValidatePageGeometry(IntPtr page)
    {
        if (Native.FPDFPage_GetRotation(page) != 0)
            throw new NotSupportedException("The bounded redaction pipeline currently requires an unrotated page.");
        if (Native.FPDFPage_HasTransparency(page) != 0)
            throw new NotSupportedException("The bounded redaction pipeline currently refuses transparency on affected pages.");
    }

    private static NativeObject ReadObject(IntPtr page, int index)
    {
        var item = Native.FPDFPage_GetObject(page, index);
        Require(item != IntPtr.Zero, "read a page object");
        var type = Native.FPDFPageObj_GetType(item);
        if (type is not (1 or 2 or 3 or 5))
            throw new NotSupportedException("An affected page contains an unsupported object type. Only text, images, paths and whole Form invocations are accepted.");
        Require(Native.FPDFPageObj_GetBounds(item, out var left, out var bottom, out var right, out var top) != 0,
            "measure a page object");
        if (!float.IsFinite(left) || !float.IsFinite(bottom) || !float.IsFinite(right) || !float.IsFinite(top) ||
            right < left || top < bottom)
            throw new NotSupportedException("A page object has invalid bounds and cannot be safely classified for redaction.");
        return new NativeObject(item, type, new Bounds(left, bottom, right, top));
    }

    private static void ValidateObjectGeometry(IntPtr item, int type)
    {
        var clip = Native.FPDFPageObj_GetClipPath(item);
        Require(clip != IntPtr.Zero, "inspect a page object's clip state");
        var clipPaths = Native.FPDFClipPath_CountPaths(clip);
        // For a valid object PDFium returns the address of its CPDF_ClipPath even
        // when it has no underlying reference. CountPaths returns -1 for that
        // ordinary empty state (fpdfsdk/fpdf_transformpage.cpp), not only for an
        // invalid caller handle. The handle above came from this loaded page.
        if (clipPaths < -1) throw new InvalidDataException("PDFium returned an unreadable clip-path count.");
        if (clipPaths > 0)
            throw new NotSupportedException("Custom clipping on an affected page requires unsupported geometric analysis.");
        if (type == 1 && Native.FPDFTextObj_GetTextRenderMode(item) is < 0 or >= 4)
            throw new NotSupportedException("Text clipping or an unreadable text rendering mode is not supported by bounded redaction.");
    }

    private static bool Intersects(Bounds item, PdfEditRectangle region) =>
        item.Right >= region.Left && item.Left <= region.Left + region.Width &&
        item.Top >= region.Bottom && item.Bottom <= region.Bottom + region.Height;

    private static bool Contains(PdfEditRectangle region, Bounds item) =>
        region.Left <= item.Left + BoundaryTolerance && region.Bottom <= item.Bottom + BoundaryTolerance &&
        region.Left + region.Width >= item.Right - BoundaryTolerance &&
        region.Bottom + region.Height >= item.Top - BoundaryTolerance;

    private static byte[] Save(IntPtr document, CancellationToken token)
    {
        using var stream = new MemoryStream();
        ExceptionDispatchInfo? callbackError = null;
        WriteBlock callback = (_, data, size) =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var length = checked((int)size.Value);
                if (stream.Length + length > MaximumDocumentBytes)
                    throw new NotSupportedException("The redaction output exceeds 256 MiB.");
                var buffer = ArrayPool<byte>.Shared.Rent(length);
                try { Marshal.Copy(data, buffer, 0, length); stream.Write(buffer, 0, length); }
                finally { ArrayPool<byte>.Shared.Return(buffer); }
                return 1;
            }
            catch (Exception error)
            {
                callbackError ??= ExceptionDispatchInfo.Capture(error);
                return 0;
            }
        };
        var writer = new FileWrite { Version = 1, WriteBlock = Marshal.GetFunctionPointerForDelegate(callback) };
        var saved = Native.FPDF_SaveAsCopy(document, ref writer, new CULong(2u)); // FPDF_NO_INCREMENTAL.
        GC.KeepAlive(callback);
        // PDFium can return TRUE after the callback returned 0. The captured error
        // takes precedence over the native return value.
        callbackError?.Throw();
        token.ThrowIfCancellationRequested();
        Require(saved != 0, "serialize a full redaction rewrite");
        return stream.ToArray();
    }

    private static void Require(bool condition, string operation)
    {
        if (!condition) throw new InvalidDataException($"PDFium could not {operation}.");
    }

    private sealed class NativeDocument : IDisposable
    {
        private GCHandle _pin;
        internal IntPtr Handle { get; private set; }
        internal int PageCount { get; }

        internal NativeDocument(byte[] data, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                Handle = PdfiumNative.FPDF_LoadMemDocument64(_pin.AddrOfPinnedObject(), (nuint)data.Length, null);
                Require(Handle != IntPtr.Zero, "open the redaction document");
                PageCount = PdfiumNative.FPDF_GetPageCount(Handle);
                if (PageCount is <= 0 or > 10_000)
                    throw new NotSupportedException("The bounded redaction pipeline accepts documents with between 1 and 10000 readable pages.");
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero) { PdfiumNative.FPDF_CloseDocument(Handle); Handle = IntPtr.Zero; }
            if (_pin.IsAllocated) _pin.Free();
        }
    }

    private sealed class PageSelection(int pageIndex, IntPtr page)
    {
        internal int PageIndex { get; } = pageIndex;
        internal IntPtr Page { get; } = page;
        internal List<IntPtr> Removed { get; } = [];
        internal List<ObjectSnapshot> Retained { get; } = [];
    }

    private readonly record struct RegionRequest(int Index, PdfRedactionRegion Region);
    private readonly record struct NativeObject(IntPtr Handle, int Type, Bounds Bounds);
    private readonly record struct ObjectSnapshot(int Type, Bounds Bounds);
    private readonly record struct Bounds(double Left, double Bottom, double Right, double Top)
    {
        internal bool Near(Bounds other) => Math.Abs(Left - other.Left) <= BoundaryTolerance &&
            Math.Abs(Bottom - other.Bottom) <= BoundaryTolerance && Math.Abs(Right - other.Right) <= BoundaryTolerance &&
            Math.Abs(Top - other.Top) <= BoundaryTolerance;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileWrite { public int Version; public IntPtr WriteBlock; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int WriteBlock(IntPtr writer, IntPtr data, CULong size);

    private static class Native
    {
        private const string Library = "pdfium";
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_CountObjects(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPage_GetObject(IntPtr page, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPageObj_GetType(IntPtr item);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPageObj_GetBounds(IntPtr item, out float left, out float bottom, out float right, out float top);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPageObj_GetClipPath(IntPtr item);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFClipPath_CountPaths(IntPtr clip);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFTextObj_GetTextRenderMode(IntPtr item);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_GetRotation(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_HasTransparency(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_RemoveObject(IntPtr page, IntPtr item);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPageObj_Destroy(IntPtr item);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_GenerateContent(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDF_SaveAsCopy(IntPtr document, ref FileWrite writer, CULong flags);
    }
}
