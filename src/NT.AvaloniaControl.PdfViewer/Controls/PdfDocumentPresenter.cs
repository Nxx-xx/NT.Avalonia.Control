using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace NT.AvaloniaControl.PdfViewer;

/// <summary>Draws a continuous document while decoding and retaining only a bounded set of pages.</summary>
public sealed class PdfDocumentPresenter : Control
{
    /// <summary>
    /// Entry-count safety valve independent of the byte budgets: tiny pages can stay far
    /// under their byte budget while their per-entry bitmap-handle overhead accumulates.
    /// </summary>
    private const int MaxCachedPages = 2048;
    private const int MaxCachedTextPages = 1024;
    private const double LinkClickMovementLimit = 4;
    /// <summary>Preview-tier linear scale against the settled target: half the edge, a quarter of the pixels.</summary>
    private const double PreviewScale = .5;
    private static readonly TimeSpan SettleDelayDefault = TimeSpan.FromMilliseconds(150);
    private static readonly Cursor s_linkCursor = new(StandardCursorType.Hand);
    private static readonly Cursor s_defaultCursor = new(StandardCursorType.Arrow);
    /// <summary>Stable empty page list so a document-less rebuild keeps its build signature.</summary>
    private static readonly PdfPageInfo[] s_noPages = [];
    private readonly PdfPageLayout _layout = new();
    private readonly Dictionary<int, CachedPage> _cache = [];
    private readonly Dictionary<int, CachedText> _text = [];
    private readonly Dictionary<int, PdfLink[]> _links = [];
    private readonly HashSet<int> _linksPending = [];
    private readonly DispatcherTimer _selectionTimer;
    private readonly DispatcherTimer _settleTimer;
    private PdfDocument? _document;
    private CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _renderCancellation;
    private CancellationTokenSource? _selectionCancellation;
    private CancellationTokenSource? _selectionTextBuild;
    private Task _renderTask = Task.CompletedTask;
    private Task _selectionTextTask = Task.CompletedTask;
    private bool _selectionTextDirty;
    private bool _selectionTextRunning;
    private long _selectionTextVersion;
    private IReadOnlyList<PdfSearchResult>? _searchSource;
    private Dictionary<int, (int Index, PdfSearchResult Result)[]> _searchIndex = [];
    private string _renderKey = "";
    private Rect _viewport = new(0, 0, 800, 600);
    private long _stamp;
    private long _textStamp;
    private long _selectionVersion;
    private TextPosition? _anchor;
    private TextPosition? _focus;
    private bool _dragging;
    private PdfLink? _pendingLinkClick;
    private Point _linkPressPoint;
    private IPointer? _capturedPointer;
    private Point _lastPointerViewport;
    private bool _pinching;
    private double _pinchBaseZoom;
    private bool _interacting;
    private TaskCompletionSource _settled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TimeSpan _settleDelay = SettleDelayDefault;
    private double _renderScaling = 1;

    public PdfDocumentPresenter()
    {
        Focusable = true;
        ClipToBounds = true;
        _selectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _selectionTimer.Tick += SelectionTimerTick;
        _settleTimer = new DispatcherTimer { Interval = SettleDelayDefault };
        _settleTimer.Tick += SettleTimerTick;
        var copy = new MenuItem { Header = PdfViewerStrings.CopySelectionMenuText };
        copy.PointerEntered += (_, _) => copy.IsEnabled = Owner?.CanSelectText == true;
        copy.Click += async (_, _) => { if (Owner is { } owner) await owner.CopySelectionToClipboardAsync(); };
        ContextMenu = new ContextMenu { ItemsSource = new[] { copy } };
        ContextMenu.Opening += (_, _) => copy.IsEnabled = Owner?.CanSelectText == true;
        // Touch pinch: the recognizer turns two contacts into cumulative Pinch events
        // (Scale is relative to the gesture start) plus PinchEnded on release or cancel.
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(PinchEvent, OnPinch);
        AddHandler(PinchEndedEvent, OnPinchEnded);
        AddHandler(PointerTouchPadGestureMagnifyEvent, OnTouchPadMagnify);
    }

    private void OnPinch(object? sender, PinchEventArgs args)
    {
        if (Owner is not { } owner) return;
        if (!_pinching)
        {
            // A new gesture scales relative to the zoom that was active when it began.
            _pinching = true;
            _pinchBaseZoom = owner.ZoomLevel;
            // A two-contact pinch supersedes any in-progress drag selection.
            AbortDragSelection();
        }
        // Scale is cumulative from the gesture start; keep the pinch origin fixed.
        if (!double.IsFinite(args.Scale) || args.Scale <= 0) return;
        owner.ZoomAt(_pinchBaseZoom * args.Scale / owner.ZoomLevel, args.ScaleOrigin);
        args.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs args)
    {
        // End and cancel share this path: the zoom already applied stays, only the state resets.
        _pinching = false;
        args.Handled = true;
    }

    private void OnTouchPadMagnify(object? sender, PointerDeltaEventArgs args)
    {
        if (Owner is not { } owner) return;
        // Precision-touchpad pinch: Delta.X is this event's fractional scale change.
        var factor = 1 + args.Delta.X;
        if (!double.IsFinite(factor) || factor <= 0) return;
        owner.ZoomAt(factor, args.GetPosition(this));
        args.Handled = true;
    }

    /// <summary>Aborts an in-progress drag selection without touching a settled one.</summary>
    private void AbortDragSelection()
    {
        if (!_dragging && _anchor is null) return;
        _dragging = false;
        _selectionTimer.Stop();
        _selectionCancellation?.Cancel();
        CancelSelectionText();
        ++_selectionVersion;
        _anchor = _focus = null;
        _capturedPointer?.Capture(null);
        _capturedPointer = null;
        Owner?.SetSelectedText("");
        InvalidateVisual();
    }

    internal PdfViewer? Owner { get; set; }
    public int RenderedPageCount { get; private set; }
    public int CachedPageCount => _cache.Count;
    public long CachedBytes => _cache.Values.Sum(page => page.Bytes);
    /// <summary>Cached pages holding preview-tier bitmaps awaiting their full-quality upgrade.</summary>
    public int PreviewPageCount => _cache.Values.Count(page => page.Preview);
    /// <summary>Cached pages whose full-quality raster was cut below the on-screen target by
    /// decode caps or the page cache budget.</summary>
    public int DegradedPageCount => _cache.Values.Count(page => page.Degraded);
    internal int TextCacheCount => _text.Count;
    internal long TextCacheBytes => _text.Values.Sum(entry => entry.Bytes);
    private long PageBudget => Owner?.EffectivePageCacheBudget ?? PdfViewer.DefaultPageCacheBudget;
    private long TextBudget => Owner?.EffectiveTextCacheBudget ?? PdfViewer.DefaultTextCacheBudget;
    private int PrerenderRows => Owner?.PrerenderRows ?? PdfViewer.DefaultPrerenderRows;
    private int MaxRenderDimension => Owner?.MaxRenderDimension ?? PdfViewer.DefaultMaxRenderDimension;
    private long MaxRenderPixels => Owner?.MaxRenderPixels ?? PdfViewer.DefaultMaxRenderPixels;
    private double MaxRenderScale => Owner?.MaxRenderScale ?? PdfViewer.DefaultMaxRenderScale;
    internal IReadOnlyList<Rect> PageBounds => _layout.Pages;
    internal Size DocumentExtent => _layout.Extent;

    /// <summary>Debounce before the settled full-quality pass; tests stretch it to observe the
    /// in-burst preview state.</summary>
    internal TimeSpan SettleDelay
    {
        get => _settleDelay;
        set
        {
            _settleDelay = value <= TimeSpan.Zero ? SettleDelayDefault : value;
            _settleTimer.Interval = _settleDelay;
            // Restart a pending debounce so stretching the delay mid-burst is observable
            // and shortening it takes effect immediately.
            if (_interacting)
            {
                _settleTimer.Stop();
                _settleTimer.Start();
            }
        }
    }

    /// <summary>What a cached page currently holds, for quality-contract tests and diagnostics.</summary>
    internal (int Width, int Height, bool Preview, bool Degraded)? GetCachedPageState(int pageIndex) =>
        _cache.TryGetValue(pageIndex, out var page) ? (page.Width, page.Height, page.Preview, page.Degraded) : null;

    /// <summary>Re-issues the render pass after the quality caps changed.</summary>
    internal void RefreshRenderQuality()
    {
        _renderKey = "";
        ScheduleRendering();
        InvalidateVisual();
    }

    // Marks a scroll / zoom burst: pages that already hold any bitmap keep it as a stretched
    // preview, missing pages fill at preview quality, and the settled pass upgrades visible
    // pages to full quality once the burst ends. The first paint after a load goes straight
    // to full quality (nothing to preview yet).
    private void MarkInteracting()
    {
        if (_cache.Count == 0) return;
        _interacting = true;
        if (_settled.Task.IsCompleted)
            _settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _settleTimer.Stop();
        _settleTimer.Start();
    }

    private void SettleTimerTick(object? sender, EventArgs e)
    {
        _settleTimer.Stop();
        _interacting = false;
        _settled.TrySetResult();
        ScheduleRendering();
    }

    private void ResetSettleState()
    {
        _settleTimer.Stop();
        _interacting = false;
        _settled.TrySetResult();
    }

    /// <summary>Sheds retained pages and text down to the low-water mark of their effective budgets.</summary>
    internal void TrimCaches()
    {
        TrimPageCache(null, -1);
        TrimTextCache(-1);
        ScheduleRendering();
    }

    internal void SetDocument(PdfDocument? document)
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        _lifetime = new CancellationTokenSource();
        _renderCancellation?.Cancel();
        ResetSettleState();
        ClearSelection();
        _pinching = false;
        foreach (var page in _cache.Values) page.Bitmap.Dispose();
        _cache.Clear();
        _text.Clear();
        _links.Clear();
        _linksPending.Clear();
        _document = document;
        RenderedPageCount = 0;
        _renderKey = "";
        RebuildLayout();
    }

    internal void Pause()
    {
        _renderCancellation?.Cancel();
        _renderKey = "";
        ResetSettleState();
        _selectionTimer.Stop();
        _selectionCancellation?.Cancel();
        CancelSelectionText();
        ++_selectionVersion;
        _dragging = false;
        _pinching = false;
        _pendingLinkClick = null;
        _capturedPointer?.Capture(null);
        _capturedPointer = null;
    }

    internal void Refresh(Rect viewport)
    {
        var widthChanged = Math.Abs(_viewport.Width - viewport.Width) > .5;
        var moved = widthChanged || Math.Abs(_viewport.Left - viewport.Left) > .5 ||
            Math.Abs(_viewport.Top - viewport.Top) > .5 || Math.Abs(_viewport.Height - viewport.Height) > .5;
        _viewport = viewport;
        if (widthChanged) RebuildLayout();
        else
        {
            // Only an actual scroll counts as a burst; re-assertions of the same viewport
            // (the navigation offset refresh path) leave the settled quality alone.
            if (moved) MarkInteracting();
            RefreshScaling();
            ScheduleRendering();
            InvalidateVisual();
        }
    }

    internal void RebuildLayout()
    {
        var rotation = Owner?.Rotation ?? 0;
        if (_layout.Rotation != rotation)
        {
            foreach (var page in _cache.Values) page.Bitmap.Dispose();
            _cache.Clear();
        }
        var layoutStarted = PdfPerformanceSession.Now;
        var built = _layout.Build(_document?.Pages ?? s_noPages, Owner?.ZoomLevel ?? 1, rotation, _viewport.Width,
            Owner?.PageDisplayMode ?? PdfPageDisplayMode.Continuous, Math.Max(0, (Owner?.CurrentPage ?? 1) - 1),
            Owner?.ShowCoverPage ?? true);
        if (PdfPerformanceSession.Current is { } layoutPerf)
            layoutPerf.Record(layoutPerf.NextCorrelationId("layout"), PdfPerformanceStage.LayoutBuild,
                layoutStarted, PdfPerformanceSession.Now);
        if (built != PdfPageLayout.BuildResult.Unchanged)
        {
            if (built == PdfPageLayout.BuildResult.Rebuilt)
            {
                // Zoom / fit / rotation / mode changes with pages on screen are a burst: keep the
                // cached bitmaps as stretched previews until the geometry settles. A pure recenter
                // keeps the raster targets and fills at the normal quality.
                MarkInteracting();
                _renderKey = "";
            }
            InvalidateMeasure();
        }
        // Even unchanged geometry can hide different rows under a moved viewport.
        RefreshScaling();
        ScheduleRendering();
        InvalidateVisual();
    }

    // A cross-screen DPI change alters the target raster of every page; the requested pixel
    // sizes carry it into the render key, so re-scheduling here is what invalidates the cache.
    private bool RefreshScaling()
    {
        var scaling = Math.Clamp(TopLevel.GetTopLevel(this)?.RenderScaling ?? 1, 1, MaxRenderScale);
        if (Math.Abs(scaling - _renderScaling) <= .001) return false;
        _renderScaling = scaling;
        _renderKey = "";
        return true;
    }

    protected override Size MeasureOverride(Size availableSize) => _layout.Extent;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        // A cross-screen DPI change re-renders the tree before any scroll event; pick the new
        // scaling up here so the cached rasters are invalidated and re-decoded at the new scale.
        var scalingChanged = RefreshScaling();
        if (_document is null || _layout.Pages.Count == 0) return;
        if (scalingChanged) ScheduleRendering();
        var (firstRow, lastRow) = _layout.RowsBetween(_viewport.Top, _viewport.Bottom);
        for (var row = firstRow; row <= lastRow; row++)
        {
            foreach (var i in _layout.Rows[row])
            {
                var bounds = _layout.Pages[i];
                context.DrawRectangle(new SolidColorBrush(Color.FromArgb(45, 0, 0, 0)), null, bounds.Translate(new Vector(2, 3)));
                context.DrawRectangle(Brushes.White, null, bounds);
                if (_cache.TryGetValue(i, out var cached))
                    context.DrawImage(cached.Bitmap, new Rect(cached.Bitmap.Size), bounds);
                else
                {
                    var loading = new FormattedText($"{i + 1}", System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, Typeface.Default, 18, Brushes.Gray);
                    context.DrawText(loading, bounds.Center - new Vector(loading.Width / 2, loading.Height / 2));
                }
                if (Owner is { } owner)
                {
                    using var clip = context.PushClip(bounds);
                    EnsureSearchIndex(owner);
                    if (_searchIndex.TryGetValue(i, out var hits))
                    {
                        foreach (var (resultIndex, result) in hits)
                        {
                            var brush = resultIndex == owner.CurrentSearchResultIndex
                                ? new SolidColorBrush(Color.FromArgb(155, 255, 155, 30))
                                : new SolidColorBrush(Color.FromArgb(100, 255, 225, 30));
                            foreach (var rect in result.Bounds)
                                context.DrawRectangle(brush, null, _layout.ToDisplay(i, rect, _document.Pages[i]));
                        }
                    }
                    if (SelectionRange is { } range && _text.TryGetValue(i, out var text))
                    {
                        var brush = new SolidColorBrush(Color.FromArgb(100, 45, 125, 245));
                        // Interior pages of a multi-page selection are entirely selected; skip
                        // the per-character range probe for them.
                        var wholePage = i > range.Start.Page && i < range.End.Page;
                        foreach (var character in text.Text.Characters)
                            if (wholePage || InRange(i, character.Index, range))
                                context.DrawRectangle(brush, null, _layout.ToDisplay(i, character.Bounds, _document.Pages[i]));
                    }
                }
            }
        }
    }

    // The search result list is republished as a new array per batch; rebuild the per-page
    // buckets only when the published snapshot actually changed.
    private void EnsureSearchIndex(PdfViewer owner)
    {
        var source = owner.SearchResults;
        if (ReferenceEquals(_searchSource, source)) return;
        _searchSource = source;
        _searchIndex = PdfSearchPageIndex.Build(source);
    }

    private void ScheduleRendering()
    {
        if (_document is null || Owner?.IsPresentationActive != true || _layout.Pages.Count == 0) return;
        var (firstRow, lastRow) = _layout.RowsBetween(_viewport.Top, _viewport.Bottom);
        var visible = new HashSet<int>(Enumerable.Range(firstRow, Math.Max(0, lastRow - firstRow + 1))
            .SelectMany(row => _layout.Rows[row]));
        // Pre-render: decode whole layout rows beyond the viewport on each side before the
        // reader scrolls them in. A row is one page in single / continuous layouts and two
        // pages in the two-page layouts.
        var prerender = Math.Clamp(PrerenderRows, 0, 8);
        firstRow = Math.Max(0, firstRow - prerender);
        lastRow = Math.Min(_layout.Rows.Count - 1, lastRow + prerender);
        var candidates = Enumerable.Range(firstRow, Math.Max(0, lastRow - firstRow + 1))
            .SelectMany(row => _layout.Rows[row]).Distinct().ToArray();
        PrefetchLinks(candidates);
        RefreshScaling();
        var dpi = _renderScaling;
        var requests = new List<RenderRequest>(candidates.Length);
        foreach (var index in candidates)
        {
            // During a burst any cached bitmap continues to serve as the zoom preview; only
            // missing pages fill, at preview quality, so the pass stays cheap and quick.
            if (_interacting && _cache.TryGetValue(index, out var held))
            {
                held.Stamp = ++_stamp;
                continue;
            }
            var bounds = _layout.Pages[index];
            // Settled visible pages rasterize at full quality; pre-render rows and burst fills
            // use the preview tier and upgrade once they are visible and the burst ends.
            var preview = _interacting || !visible.Contains(index);
            var w = Math.Max(1, bounds.Width * dpi);
            var h = Math.Max(1, bounds.Height * dpi);
            if (preview) { w *= PreviewScale; h *= PreviewScale; }
            var pagePixelBudget = Math.Min((double)MaxRenderPixels, PageBudget * .95 / 4d / Math.Max(1, candidates.Length));
            var factor = Math.Min(1, Math.Min(MaxRenderDimension / Math.Max(w, h), Math.Sqrt(pagePixelBudget / (w * h))));
            // A full-quality page below its on-screen target is a recorded degradation, never a
            // silent win: caps and budget cut the raster, and DegradedPageCount says so.
            var degraded = !preview && factor < 1;
            var rw = Math.Max(1, (int)Math.Ceiling(w * factor));
            var rh = Math.Max(1, (int)Math.Ceiling(h * factor));
            // Ceil rounding can push the raster a hair over the caps; floor-shrink back so the
            // dimension and pixel budgets stay hard limits.
            rw = Math.Min(rw, (int)MaxRenderDimension);
            rh = Math.Min(rh, (int)MaxRenderDimension);
            if ((double)rw * rh > pagePixelBudget)
            {
                var fix = Math.Sqrt(pagePixelBudget / ((double)rw * rh));
                rw = Math.Max(1, (int)Math.Floor(rw * fix));
                rh = Math.Max(1, (int)Math.Floor(rh * fix));
            }
            requests.Add(new RenderRequest(index, rw, rh, preview, degraded));
        }
        var key = $"{_layout.Rotation}:" + string.Join(';', requests.Select(r => $"{r.Index}/{r.Width}/{r.Height}"));
        if (key == _renderKey) return;
        _renderKey = key;
        _renderCancellation?.Cancel();
        _renderCancellation?.Dispose();
        _renderCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _renderTask = RenderPagesAsync(_document, requests.ToArray(), _layout.Rotation, _renderCancellation.Token);
    }

    private async Task RenderPagesAsync(PdfDocument document, RenderRequest[] requests, int rotation, CancellationToken token)
    {
        try
        {
            foreach (var request in requests)
            {
                token.ThrowIfCancellationRequested();
                if (_cache.TryGetValue(request.Index, out var existing) && existing.Width == request.Width && existing.Height == request.Height)
                {
                    existing.Stamp = ++_stamp;
                    continue;
                }
                // PERF01/PERF17: one correlation id per page fill groups its decode, upload and
                // submit spans. Decode complete, UI submit and compositor render stay distinct —
                // the compositor step is only ever recorded by a present harness outside the product.
                var correlationId = PdfPerformanceSession.Current is { } pagePerf ? pagePerf.NextCorrelationId("page") : "";
                var decoded = await document.RenderForDisplayAsync(request.Index, request.Width, request.Height, rotation, token, correlationId);
                token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(document, _document)) return;
                if (PdfPerformanceSession.Current is { } decodePerf)
                    decodePerf.Mark(PdfPerformanceSession.FirstPageDecodedMark, correlationId);
                var uploadStarted = PdfPerformanceSession.Now;
                var bitmap = CreateBitmap(decoded);
                var uploadFinished = PdfPerformanceSession.Now;
                if (_cache.Remove(request.Index, out var previous)) previous.Bitmap.Dispose();
                _cache[request.Index] = new CachedPage(bitmap, request.Width, request.Height, ++_stamp, request.Preview, request.Degraded);
                RenderedPageCount++;
                TrimPageCache(requests, request.Index);
                InvalidateVisual();
                if (correlationId.Length != 0 && PdfPerformanceSession.Current is { } submitPerf)
                {
                    submitPerf.Record(correlationId, PdfPerformanceStage.BitmapUpload, uploadStarted, uploadFinished);
                    submitPerf.Record(correlationId, PdfPerformanceStage.UiSubmit, uploadFinished, PdfPerformanceSession.Now);
                    submitPerf.Mark(PdfPerformanceSession.FirstPageSubmittedMark, correlationId);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) when (token.IsCancellationRequested) { }
        catch (Exception error) { if (!token.IsCancellationRequested) Owner?.ReportError(error); }
    }

    // Retention hysteresis: pages stay cached after leaving the wanted set and are only shed
    // down to the low-water mark once retained bytes cross the effective budget (or the entry
    // safety cap bites). The pages a pass is still about to draw are never evicted mid-pass.
    private void TrimPageCache(RenderRequest[]? requests, int insertIndex)
    {
        if (CachedBytes <= PageBudget && _cache.Count <= MaxCachedPages) return;
        var lowWater = (long)(PageBudget * PdfCacheGovernor.TrimWaterRatio);
        while (CachedBytes > lowWater || _cache.Count > MaxCachedPages)
        {
            var victim = _cache.Where(pair => pair.Key != insertIndex &&
                    (requests is null || requests.All(request => request.Index != pair.Key)))
                .OrderBy(pair => pair.Value.Stamp).FirstOrDefault();
            if (victim.Value is null) break;
            victim.Value.Bitmap.Dispose();
            _cache.Remove(victim.Key);
        }
    }

    // Same hysteresis for extracted text pages; the selection anchor and focus pages are kept
    // so a long drag never loses the text it is measuring against.
    private void TrimTextCache(int insertPage)
    {
        if (TextCacheBytes <= TextBudget && _text.Count <= MaxCachedTextPages) return;
        var lowWater = (long)(TextBudget * PdfCacheGovernor.TrimWaterRatio);
        while (TextCacheBytes > lowWater || _text.Count > MaxCachedTextPages)
        {
            var victim = _text.Where(pair => pair.Key != insertPage &&
                    pair.Key != _anchor?.Page && pair.Key != _focus?.Page)
                .OrderBy(pair => pair.Value.Stamp).FirstOrDefault();
            if (victim.Value is null) break;
            _text.Remove(victim.Key);
        }
    }

    // Link extraction is asynchronous; prefetch visible pages so hover and clicks can hit-test
    // synchronously against the cache. A click on a not-yet-loaded page simply starts a selection.
    private void PrefetchLinks(int[] pages)
    {
        var document = _document;
        if (document is null) return;
        foreach (var index in pages)
        {
            if (index < 0 || index >= _layout.Pages.Count || _links.ContainsKey(index) || !_linksPending.Add(index)) continue;
            _ = LoadLinksAsync(document, index);
        }
    }

    private async Task LoadLinksAsync(PdfDocument document, int index)
    {
        try
        {
            var links = await document.GetLinksAsync(index, _lifetime.Token);
            if (!ReferenceEquals(document, _document)) return;
            _links[index] = links.ToArray();
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error) { if (ReferenceEquals(document, _document) && !_lifetime.IsCancellationRequested) Owner?.ReportError(error); }
        finally { if (ReferenceEquals(document, _document)) _linksPending.Remove(index); }
    }

    /// <summary>The link annotation whose display rectangle contains the point, or null.</summary>
    internal PdfLink? LinkAt(Point point)
    {
        var index = _layout.PageIndexAt(point);
        if (index < 0 || _document is not { } document || !_links.TryGetValue(index, out var links)) return null;
        foreach (var link in links)
            if (_layout.ToDisplay(index, link.Bounds, document.Pages[index]).Contains(point))
                return link;
        return null;
    }

    internal static WriteableBitmap CreateBitmap(PdfRenderedPage page)
    {
        var bitmap = new WriteableBitmap(new PixelSize(page.Width, page.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        try
        {
            using var target = bitmap.Lock();
            for (var row = 0; row < page.Height; row++)
                Marshal.Copy(page.Pixels, row * page.Stride, target.Address + row * target.RowBytes, page.Width * 4);
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    internal async Task WaitForIdleAsync(CancellationToken token)
    {
        Task render;
        Task selection;
        do
        {
            // A still-running scroll / zoom burst ends with a full-quality upgrade pass;
            // waiting only for the current pass would report preview rasters as final.
            while (_interacting) await _settled.Task.WaitAsync(token);
            render = _renderTask;
            selection = _selectionTextTask;
            await render.WaitAsync(token);
            await selection.WaitAsync(token);
        }
        while (_interacting || !ReferenceEquals(render, _renderTask) || !ReferenceEquals(selection, _selectionTextTask));
    }

    internal int PageAt(double y) => _layout.PageAt(y);
    internal Rect GetPageBounds(int pageIndex) => _layout.Pages[pageIndex];
    internal (int Page, Point Position)? CaptureAnchor(Point point)
    {
        if (_document is null) return null;
        var index = _layout.PageIndexAt(point);
        if (index < 0) index = _layout.PageAt(point.Y);
        if (index < 0) return null;
        var page = _document.Pages[index];
        var position = _layout.ToPage(index, point, page);
        return (index, new Point(Math.Clamp(position.X, 0, page.Width), Math.Clamp(position.Y, 0, page.Height)));
    }
    internal Point GetAnchorPosition(int page, Point position) =>
        _layout.ToDisplay(page, new Rect(position, default(Size)), _document!.Pages[page]).Position;
    internal Rect GetSearchBounds(PdfSearchResult result) => result.Bounds.Count == 0 || _document is null
        ? GetPageBounds(result.PageIndex) : _layout.ToDisplay(result.PageIndex, result.Bounds[0], _document.Pages[result.PageIndex]);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // While a pinch is in progress, extra contacts start neither selection nor links.
        if (_pinching) return;
        if (_document is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        if (LinkAt(point) is { } link)
        {
            // Capture the press so a small movement still counts as activation while a
            // drag is ignored; link activation works even when text selection is disabled.
            Focus();
            e.Pointer.Capture(this);
            _capturedPointer = e.Pointer;
            _pendingLinkClick = link;
            _linkPressPoint = point;
            e.Handled = true;
            return;
        }
        _pendingLinkClick = null;
        if (Owner?.CanSelectText != true) return;
        var page = _layout.PageIndexAt(point);
        if (page < 0 || !_layout.Pages[page].Contains(point)) return;
        Focus();
        e.Pointer.Capture(this);
        _capturedPointer = e.Pointer;
        _dragging = true;
        _lastPointerViewport = point - _viewport.Position;
        var version = ++_selectionVersion;
        CancelSelectionText();
        _anchor = _focus = null;
        Owner.SetSelectedText("");
        Owner.SetSelectionRangeState(false);
        InvalidateVisual();
        _selectionTimer.Start();
        _ = BeginSelectionAsync(point, version);
        e.Handled = true;
    }

    private async Task BeginSelectionAsync(Point point, long version)
    {
        try
        {
            var position = await HitAsync(point, _lifetime.Token);
            if (version != _selectionVersion) return;
            _anchor = position;
            await UpdateSelectionAsync(_lastPointerViewport + _viewport.Position, version);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { Owner?.ReportError(error); }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pendingLinkClick is not null) return;
        if (!_dragging)
        {
            var point = e.GetPosition(this);
            var cursor = LinkAt(point) is null ? s_defaultCursor : s_linkCursor;
            if (!ReferenceEquals(Cursor, cursor)) Cursor = cursor;
            return;
        }
        _lastPointerViewport = e.GetPosition(this) - _viewport.Position;
        if (_anchor is not null) _ = UpdateSelectionAsync(e.GetPosition(this), _selectionVersion);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pendingLinkClick is { } pending)
        {
            _pendingLinkClick = null;
            e.Pointer.Capture(null);
            _capturedPointer = null;
            if (new Vector(e.GetPosition(this).X - _linkPressPoint.X, e.GetPosition(this).Y - _linkPressPoint.Y).Length <= LinkClickMovementLimit && Owner is { } owner)
                owner.ActivateLink(pending);
            e.Handled = true;
            return;
        }
        if (!_dragging) return;
        _lastPointerViewport = e.GetPosition(this) - _viewport.Position;
        if (_anchor is not null) _ = UpdateSelectionAsync(e.GetPosition(this), _selectionVersion);
        _dragging = false;
        _selectionTimer.Stop();
        e.Pointer.Capture(null);
        _capturedPointer = null;
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        // Selection / link capture only: pinch end and cancel arrive as PinchEndedEvent.
        _dragging = false;
        _pendingLinkClick = null;
        _selectionTimer.Stop();
        _capturedPointer = null;
        base.OnPointerCaptureLost(e);
    }

    private void SelectionTimerTick(object? sender, EventArgs e)
    {
        if (!_dragging || Owner is null) return;
        var dy = _lastPointerViewport.Y < 20 ? -28 : _lastPointerViewport.Y > _viewport.Height - 20 ? 28 : 0;
        if (dy == 0) return;
        Owner.ScrollBy(dy);
        if (_anchor is not null) _ = UpdateSelectionAsync(_lastPointerViewport + _viewport.Position, _selectionVersion);
    }

    private async Task UpdateSelectionAsync(Point point, long version)
    {
        _selectionCancellation?.Cancel();
        _selectionCancellation?.Dispose();
        _selectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _selectionCancellation.Token;
        try
        {
            var next = await HitAsync(point, token);
            if (version != _selectionVersion || next is null || token.IsCancellationRequested) return;
            // The drag maintains only the interval; the selected text is a coalesced preview
            // rebuilt at most one at a time and completed for real at copy time.
            _focus = next;
            Owner?.SetSelectionRangeState(SelectionRange is not null);
            InvalidateVisual();
            QueueSelectionText(version);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!token.IsCancellationRequested) Owner?.ReportError(error); }
    }

    // Every move only marks the interval dirty and cancels the in-flight preview build; a
    // single runner drains the marks, so a drag never stacks one full extract per event.
    private void QueueSelectionText(long version)
    {
        _selectionTextDirty = true;
        _selectionTextVersion = version;
        _selectionTextBuild?.Cancel();
        if (_selectionTextRunning) return;
        _selectionTextTask = RunSelectionTextAsync();
    }

    /// <summary>Stops any pending or in-flight preview rebuild; the next move queues a new one.</summary>
    private void CancelSelectionText()
    {
        _selectionTextDirty = false;
        _selectionTextBuild?.Cancel();
    }

    private async Task RunSelectionTextAsync()
    {
        if (_selectionTextRunning) return;
        _selectionTextRunning = true;
        try
        {
            while (_selectionTextDirty)
            {
                _selectionTextDirty = false;
                var version = _selectionTextVersion;
                _selectionTextBuild?.Cancel();
                _selectionTextBuild?.Dispose();
                _selectionTextBuild = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                var token = _selectionTextBuild.Token;
                try
                {
                    var selected = await GetSelectedTextAsync(token);
                    // A newer gesture (press / clear / pinch) owns the text now; never land late.
                    if (version == _selectionVersion && !token.IsCancellationRequested)
                        Owner?.SetSelectedText(selected);
                }
                catch (OperationCanceledException) { }
            }
        }
        finally
        {
            _selectionTextRunning = false;
            // A move marked the interval dirty while the running flag still held; pick it up.
            if (_selectionTextDirty) _selectionTextTask = RunSelectionTextAsync();
        }
    }

    private async Task<TextPosition?> HitAsync(Point point, CancellationToken token)
    {
        var document = _document;
        var pageIndex = _layout.PageIndexAt(point);
        if (document is null || pageIndex < 0) return null;
        var pagePoint = _layout.ToPage(pageIndex, point, document.Pages[pageIndex]);
        var text = await GetTextAsync(pageIndex, token);
        token.ThrowIfCancellationRequested();
        var character = text.HitIndex.FindNearest(pagePoint);
        return character < 0 ? null : new TextPosition(pageIndex, character);
    }

    private async Task<CachedText> GetTextAsync(int page, CancellationToken token)
    {
        if (_text.TryGetValue(page, out var cached))
        {
            cached.Stamp = ++_textStamp;
            return cached;
        }
        var document = _document ?? throw new OperationCanceledException(token);
        var text = await document.GetTextAsync(page, token);
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(document, _document)) throw new OperationCanceledException(token);
        var entry = new CachedText(text, PdfCacheGovernor.EstimateTextBytes(text), ++_textStamp);
        _text[page] = entry;
        TrimTextCache(page);
        return entry;
    }

    private (TextPosition Start, TextPosition End)? SelectionRange => _anchor is { } a && _focus is { } b
        ? a.CompareTo(b) <= 0 ? (a, b) : (b, a) : null;

    private static bool InRange(int page, int index, (TextPosition Start, TextPosition End) range) =>
        page >= range.Start.Page && page <= range.End.Page &&
        (page != range.Start.Page || index >= range.Start.Character) &&
        (page != range.End.Page || index <= range.End.Character);

    internal async Task<string> GetSelectedTextAsync(CancellationToken cancellationToken = default)
    {
        if (Owner?.CanSelectText != true || SelectionRange is not { } range) return "";
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var builder = new StringBuilder();
        for (var page = range.Start.Page; page <= range.End.Page; page++)
        {
            var text = await GetTextAsync(page, linked.Token);
            if (page > range.Start.Page) builder.AppendLine();
            foreach (var character in text.Text.Characters)
                if (InRange(page, character.Index, range)) builder.Append(character.Text);
        }
        return builder.ToString();
    }

    internal async Task SelectAllAsync()
    {
        if (_document is null || Owner?.CanSelectText != true) return;
        var page = Owner.CurrentPage - 1;
        var version = ++_selectionVersion;
        CancelSelectionText();
        try
        {
            var entry = await GetTextAsync(page, _lifetime.Token);
            if (version != _selectionVersion || entry.Text.Characters.Count == 0) return;
            _anchor = new TextPosition(page, entry.Text.Characters[0].Index);
            _focus = new TextPosition(page, entry.Text.Characters[^1].Index);
            Owner.SetSelectedText(entry.Text.Text);
            Owner.SetSelectionRangeState(true);
            InvalidateVisual();
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { Owner?.ReportError(error); }
    }

    internal void ClearSelection()
    {
        ++_selectionVersion;
        _selectionCancellation?.Cancel();
        CancelSelectionText();
        _selectionTimer.Stop();
        _dragging = false;
        _capturedPointer?.Capture(null);
        _capturedPointer = null;
        _anchor = _focus = null;
        Owner?.SetSelectedText("");
        Owner?.SetSelectionRangeState(false);
        InvalidateVisual();
    }

    private readonly record struct RenderRequest(int Index, int Width, int Height, bool Preview, bool Degraded);

    private sealed class CachedPage(WriteableBitmap bitmap, int width, int height, long stamp, bool preview, bool degraded)
    {
        internal WriteableBitmap Bitmap { get; } = bitmap;
        internal int Width { get; } = width;
        internal int Height { get; } = height;
        internal long Bytes => (long)Width * Height * 4;
        internal long Stamp { get; set; } = stamp;
        /// <summary>True when the bitmap was decoded at preview tier and still awaits its upgrade.</summary>
        internal bool Preview { get; } = preview;
        /// <summary>True when the full-quality raster was cut below the on-screen target.</summary>
        internal bool Degraded { get; } = degraded;
    }

    private sealed class CachedText
    {
        internal CachedText(PdfTextPage text, long bytes, long stamp)
        {
            Text = text;
            Bytes = bytes;
            Stamp = stamp;
            HitIndex = new PdfCharacterHitIndex(text.Characters);
        }

        internal PdfTextPage Text { get; }
        internal long Bytes { get; }
        internal long Stamp { get; set; }
        /// <summary>Nearest-character index over the page text; built once per cached page.</summary>
        internal PdfCharacterHitIndex HitIndex { get; }
    }

    private readonly record struct TextPosition(int Page, int Character) : IComparable<TextPosition>
    {
        public int CompareTo(TextPosition other) => Page != other.Page ? Page.CompareTo(other.Page) : Character.CompareTo(other.Character);
    }
}
