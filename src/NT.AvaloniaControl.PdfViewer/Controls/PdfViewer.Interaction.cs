using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace NT.AvaloniaControl.PdfViewer;

public partial class PdfViewer
{
    private CancellationTokenSource? _searchCancellation;
    private long _searchVersion;
    private long _navigationVersion;
    internal Task PendingContentFit { get; private set; } = Task.CompletedTask;
    private int? _pendingNavigationPage;
    private int? _navigationPage;
    private Vector? _navigationOffset;
    private PdfSearchResult? _pendingSearchTarget;
    private LinkTarget? _pendingLinkTarget;
    private Point? _zoomAnchorOverride;
    private ViewAnchor? _pendingViewportAnchor;

    private readonly record struct ViewAnchor(int Page, Point Position, Point ScreenPosition);

    /// <summary>How a followed axis moves its anchor point when a link destination is applied.</summary>
    private enum AxisPin
    {
        /// <summary>The axis keeps its current scroll offset.</summary>
        Keep,
        /// <summary>The anchor lands near the viewport's top-left, like a search target.</summary>
        Near,
        /// <summary>The anchor is pinned exactly at the viewport edge.</summary>
        Edge,
        /// <summary>The viewport centers on the anchor.</summary>
        Center,
    }

    /// <summary>
    /// A pending link destination placement. X / Y select how each axis moves; each axis reads its
    /// anchor from XAnchor's displayed X and YAnchor's displayed Y (both page-display points, mapped
    /// through the current viewer rotation). The two anchors differ only for content-box fits that
    /// pin one box edge while centering the other axis.
    /// </summary>
    private readonly record struct LinkTarget(Point XAnchor, Point YAnchor, AxisPin X, AxisPin Y)
    {
        internal Vector? RetainedOffset { get; init; }
        internal static LinkTarget Both(Point position, AxisPin x, AxisPin y) => new(position, position, x, y);
    }

    private ViewAnchor? CaptureViewportAnchor()
    {
        if (_scroll is null || _presenter is null || !HasDocument) return null;
        var screen = new Point(_scroll.Viewport.Width / 2, _scroll.Viewport.Height / 2);
        return CaptureAnchorAt(screen + _scroll.Offset, screen);
    }

    private ViewAnchor? CaptureAnchorAt(Point contentPoint, Point screen)
    {
        if (_scroll is null || _presenter is null || !HasDocument) return null;
        var anchor = _presenter.CaptureAnchor(contentPoint);
        return anchor is { } position ? new ViewAnchor(position.Page, position.Position, screen) : null;
    }

    private void RestoreViewportAnchor(ViewAnchor anchor)
    {
        SetCurrentPage(anchor.Page + 1);
        _navigationPage = _pendingNavigationPage = anchor.Page;
        _pendingSearchTarget = null;
        _pendingViewportAnchor = anchor;
        TryApplyNavigation();
        RefreshViewportWithoutPageChange();
    }

    /// <summary>
    /// Captures the reading position: the content point at the viewport's top-left corner plus the
    /// zoom mode, zoom level and rotation that defined the view. When that point lands in a layout
    /// gap above a page, the position is attributed to the first page at or below the top edge
    /// (anchored near its top) so the page matches the one being read. Hosts persist the returned
    /// values and rebuild them through the <see cref="PdfReadingPosition"/> constructor. Requires a
    /// loaded document; returns null while empty or loading.
    /// </summary>
    public PdfReadingPosition? CaptureReadingPosition()
    {
        VerifyAccess();
        if (_disposed || !HasDocument || PageCount == 0) return null;
        if (_scroll is not null && _presenter is not null)
        {
            var topLeft = new Point(_scroll.Offset.X, _scroll.Offset.Y);
            if (_presenter.CaptureAnchor(topLeft) is { } anchor)
            {
                var page = anchor.Page;
                var position = anchor.Position;
                var bounds = _presenter.GetPageBounds(page);
                // The viewport top-left often lands in a layout gap above a page; the standard
                // anchor then falls back to the previous row, which may be entirely off-screen.
                // A reading position belongs to the page being read: the first page at or below
                // the top edge, anchored at its top.
                if (topLeft.Y < bounds.Top || topLeft.Y >= bounds.Bottom)
                {
                    if (_presenter.CaptureAnchor(new Point(topLeft.X, topLeft.Y + PdfPageLayout.Gap)) is { } below)
                        (page, position) = (below.Page, below.Position);
                }
                return new PdfReadingPosition(page, position, ZoomLevel, ZoomMode, Rotation);
            }
        }
        // Before the first layout pass there is no anchor geometry yet; fall back to the page top.
        return new PdfReadingPosition(Math.Clamp(CurrentPage - 1, 0, PageCount - 1), new Point(0, 0), ZoomLevel, ZoomMode, Rotation);
    }

    /// <summary>
    /// Restores a reading position: zoom mode / level and rotation first, then the anchor point
    /// back to the viewport's top-left corner. Invalid stored zoom / rotation values are ignored
    /// (the page and anchor still restore); out-of-range pages are clamped. Requires a loaded
    /// document; otherwise a no-op. Display layout and cover rules are view settings and not part
    /// of the position.
    /// </summary>
    public void RestoreReadingPosition(PdfReadingPosition position)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(position);
        if (_disposed || !HasDocument || PageCount == 0 || _scroll is null || _presenter is null) return;
        ++_navigationVersion;
        var index = Math.Clamp(position.PageIndex, 0, PageCount - 1);
        _fitting = true;
        try
        {
            if (position.Rotation % 90 == 0) SetCurrentValue(RotationProperty, position.Rotation);
            if (Enum.IsDefined(position.ZoomMode)) SetCurrentValue(ZoomModeProperty, position.ZoomMode);
            if (position.ZoomMode == PdfZoomMode.Manual && double.IsFinite(position.ZoomLevel) && position.ZoomLevel > 0)
                SetCurrentValue(ZoomLevelProperty, position.ZoomLevel);
        }
        finally { _fitting = false; }
        RecomputeFit();
        _presenter.RebuildLayout();
        RestoreViewportAnchor(new ViewAnchor(index, position.PagePosition, new Point(0, 0)));
    }

    public void GoToPage(int pageNumber)
    {
        VerifyAccess();
        if (_disposed || !HasDocument) return;
        GoToPageCore(pageNumber, ++_navigationVersion);
    }

    private void GoToPageCore(int pageNumber, long navigationVersion)
    {
        if (_disposed || !HasDocument || navigationVersion != _navigationVersion) return;
        pageNumber = Math.Clamp(pageNumber, 1, PageCount);
        SetCurrentPage(pageNumber);
        if (_disposed || !HasDocument || navigationVersion != _navigationVersion) return;
        // Non-continuous modes display only the active page or spread.
        if (PageDisplayMode is PdfPageDisplayMode.SinglePage or PdfPageDisplayMode.TwoPage)
            _presenter?.RebuildLayout();
        RecomputeFit();
        if (_disposed || !HasDocument || navigationVersion != _navigationVersion) return;
        _navigationPage = pageNumber - 1;
        _pendingNavigationPage = pageNumber - 1;
        _pendingSearchTarget = null;
        _pendingViewportAnchor = null;
        _pendingLinkTarget = null;
        TryApplyNavigation();
        UpdateParts();
        RefreshViewportWithoutPageChange();
    }

    private void TryApplyNavigation()
    {
        if (_pendingNavigationPage is not { } index || _scroll is null || _presenter is null ||
            index >= _presenter.PageBounds.Count || !HasDocument) return;
        var expected = _presenter.DocumentExtent;
        // Offset is coerced against ScrollViewer's extent. Wait until layout has
        // measured the new zoom/rotation before applying a requested destination.
        if (Math.Abs(_scroll.Extent.Height - expected.Height) > 1 ||
            Math.Abs(_scroll.Extent.Width - expected.Width) > 1) return;
        var page = _presenter.GetPageBounds(index);
        var x = _scroll.Offset.X;
        var y = Math.Max(0, page.Y - PdfPageLayout.Gap);
        if (_pendingSearchTarget is { } result)
        {
            var bounds = _presenter.GetSearchBounds(result);
            if (bounds.Left < x || bounds.Right > x + _scroll.Viewport.Width) x = Math.Max(0, bounds.X - 40);
            y = Math.Max(0, bounds.Y - 60);
        }
        else if (_pendingViewportAnchor is { } anchor)
        {
            var position = _presenter.GetAnchorPosition(anchor.Page, anchor.Position);
            x = Math.Max(0, position.X - anchor.ScreenPosition.X);
            y = Math.Max(0, position.Y - anchor.ScreenPosition.Y);
        }
        else if (_pendingLinkTarget is { } target)
        {
            // Bring the destination point near the viewport's top-left, like a search target;
            // fit destinations with an edge coordinate pin that axis exactly at the viewport
            // edge, and content-box fits center the viewport on the box.
            var xAnchor = _presenter.GetAnchorPosition(index, target.XAnchor);
            var yAnchor = _presenter.GetAnchorPosition(index, target.YAnchor);
            // GoToPage and zoom/layout may already have moved the viewport. A
            // partially specified XYZ destination keeps the pre-navigation axis.
            if (target.RetainedOffset is { } retained)
            {
                if (target.X == AxisPin.Keep) x = retained.X;
                if (target.Y == AxisPin.Keep) y = retained.Y;
            }
            if (target.X != AxisPin.Keep) x = target.X switch
            {
                AxisPin.Edge => Math.Max(0, xAnchor.X),
                AxisPin.Center => Math.Max(0, xAnchor.X - _scroll.Viewport.Width / 2),
                _ => Math.Max(0, xAnchor.X - 40),
            };
            if (target.Y != AxisPin.Keep) y = target.Y switch
            {
                AxisPin.Edge => Math.Max(0, yAnchor.Y),
                AxisPin.Center => Math.Max(0, yAnchor.Y - _scroll.Viewport.Height / 2),
                _ => Math.Max(0, yAnchor.Y - 60),
            };
        }
        _updatingCurrent = true;
        try
        {
            _scroll.Offset = new Vector(x, y);
            _navigationOffset = _scroll.Offset;
            _pendingNavigationPage = null;
            _pendingSearchTarget = null;
            _pendingViewportAnchor = null;
            _pendingLinkTarget = null;
        }
        finally { _updatingCurrent = false; }
        // ScrollChanged is coalesced onto LayoutUpdated and records the offset after its
        // handlers run, so an offset applied here produces no follow-up event of its own.
        // Refresh immediately or the presenter keeps culling against the pre-navigation
        // viewport and the newly shown pages stay blank and unhit-testable.
        RefreshViewportWithoutPageChange();
    }

    private void RefreshViewportWithoutPageChange()
    {
        _updatingCurrent = true;
        try { RefreshViewport(); }
        finally { _updatingCurrent = false; }
    }

    /// <summary>Advances one page, or one whole spread in the two-page modes.</summary>
    public void NextPage()
    {
        VerifyAccess();
        if (IsTwoPageMode && PageCount > 0)
        {
            var spread = PdfPageLayout.SpreadPages(CurrentPage - 1, PageCount, ShowCoverPage);
            var next = spread[^1] + 1;
            if (next < PageCount) GoToPage(next + 1);
            return;
        }
        GoToPage(CurrentPage + 1);
    }

    /// <summary>Goes back one page, or one whole spread in the two-page modes.</summary>
    public void PreviousPage()
    {
        VerifyAccess();
        if (IsTwoPageMode && PageCount > 0)
        {
            var spread = PdfPageLayout.SpreadPages(CurrentPage - 1, PageCount, ShowCoverPage);
            var previous = spread[0] - 1;
            if (previous >= 0) GoToPage(PdfPageLayout.SpreadPages(previous, PageCount, ShowCoverPage)[0] + 1);
            return;
        }
        GoToPage(CurrentPage - 1);
    }

    private bool IsTwoPageMode =>
        PageDisplayMode is PdfPageDisplayMode.TwoPage or PdfPageDisplayMode.TwoPageContinuous;

    public void ZoomIn() { VerifyAccess(); SetCurrentValue(ZoomLevelProperty, ZoomLevel * ZoomStep); }
    public void ZoomOut() { VerifyAccess(); SetCurrentValue(ZoomLevelProperty, ZoomLevel / ZoomStep); }
    /// <summary>Resets to 100% manual zoom, keeping the current page visible.</summary>
    public void ActualSize()
    {
        VerifyAccess();
        SetCurrentValue(ZoomModeProperty, PdfZoomMode.Manual);
        SetCurrentValue(ZoomLevelProperty, 1d);
    }
    public void FitWidth() { VerifyAccess(); SetCurrentValue(ZoomModeProperty, PdfZoomMode.FitWidth); RecomputeFit(); GoToPage(CurrentPage); }
    public void FitPage() { VerifyAccess(); SetCurrentValue(ZoomModeProperty, PdfZoomMode.FitPage); RecomputeFit(); GoToPage(CurrentPage); }
    /// <summary>Fits the current page's height to the viewport (the tallest page of the spread in two-page modes).</summary>
    public void FitHeight() { VerifyAccess(); SetCurrentValue(ZoomModeProperty, PdfZoomMode.FitHeight); RecomputeFit(); GoToPage(CurrentPage); }
    public void RotateLeft() { VerifyAccess(); SetCurrentValue(RotationProperty, Rotation - 90); }
    public void RotateRight() { VerifyAccess(); SetCurrentValue(RotationProperty, Rotation + 90); }

    /// <summary>Zooms by a relative factor keeping the given content point fixed. Used by touch pinch.</summary>
    internal void ZoomAt(double factor, Point contentPoint)
    {
        VerifyAccess();
        if (!HasDocument || !double.IsFinite(factor) || factor <= 0) return;
        _zoomAnchorOverride = contentPoint;
        try { SetCurrentValue(ZoomLevelProperty, ZoomLevel * factor); }
        finally { _zoomAnchorOverride = null; }
    }

    /// <summary>
    /// Navigates to an internal destination with its fit semantics: /Fit fits the page, /FitH fits
    /// the width and /FitV fits the height, /FitR zooms so the destination rectangle just fits the
    /// viewport, and /XYZ applies its zoom. /FitB, /FitBH and /FitBV fit the page's content box
    /// (the page box when the page has no content), fetched asynchronously before the final
    /// placement. Fit types with an edge coordinate (/FitR's rectangle, /FitH's top, /FitV's left)
    /// pin that edge at the viewport; content-box fits center the remaining axis. /XYZ positions
    /// land near the top-left like a search target, and an /XYZ with only one coordinate moves
    /// only that axis. Requires a loaded document.
    /// </summary>
    public void NavigateToDestination(PdfLinkDestination destination)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(destination);
        if (_disposed || !HasDocument) return;
        if (destination.PageIndex < 0 || destination.PageIndex >= PageCount) return;
        var retainedOffset = _scroll?.Offset;
        LinkTarget? target = null;
        switch (destination.Fit)
        {
            case PdfDestinationFit.Fit:
                FitPage();
                break;
            case PdfDestinationFit.FitB:
            case PdfDestinationFit.FitBH:
            case PdfDestinationFit.FitBV:
                // Content-box fits need the page's content bounds; complete when they arrive.
                GoToPage(destination.PageIndex + 1);
                PendingContentFit = ApplyContentFitAsync(destination, _navigationVersion);
                Observe(PendingContentFit);
                return;
            case PdfDestinationFit.FitH:
                FitWidth();
                if (destination.Top is { } top && double.IsFinite(top))
                    target = LinkTarget.Both(new Point(0, top), AxisPin.Keep, AxisPin.Edge);
                break;
            case PdfDestinationFit.FitV:
                FitHeight();
                if (destination.Left is { } left && double.IsFinite(left))
                    target = LinkTarget.Both(new Point(left, 0), AxisPin.Edge, AxisPin.Keep);
                break;
            case PdfDestinationFit.FitR when destination.Rectangle is { Width: > 0, Height: > 0 } rectangle:
                ZoomToFitBox(rectangle, fitWidth: true, fitHeight: true);
                target = LinkTarget.Both(ViewMinCorner(rectangle), AxisPin.Edge, AxisPin.Edge);
                break;
            case PdfDestinationFit.Xyz when destination.Zoom is { } zoom && zoom > 0:
                SetCurrentValue(ZoomLevelProperty, zoom);
                break;
        }
        GoToPage(destination.PageIndex + 1);
        if (target is null && destination.Position is { } position)
        {
            target = LinkTarget.Both(position, AxisPin.Near, AxisPin.Near);
        }
        else if (target is null)
        {
            // /XYZ with a single coordinate keeps the other axis where it is.
            var hasLeft = destination.Left is { } leftValue && double.IsFinite(leftValue);
            var hasTop = destination.Top is { } topValue && double.IsFinite(topValue);
            if (hasLeft || hasTop || destination.Fit == PdfDestinationFit.Xyz)
                target = LinkTarget.Both(new Point(destination.Left ?? 0, destination.Top ?? 0),
                    hasLeft ? AxisPin.Near : AxisPin.Keep, hasTop ? AxisPin.Near : AxisPin.Keep);
        }
        if (target is { } placement)
        {
            if (destination.Fit == PdfDestinationFit.Xyz)
                placement = placement with { RetainedOffset = retainedOffset };
            ApplyNavigationTarget(destination.PageIndex, placement);
        }
    }

    // /FitB, /FitBH, /FitBV: fit the page's content box (the page box when the page has no
    // content). The box is fetched asynchronously, so navigation completes when it arrives;
    // a replaced document or disposal drops the pending placement.
    private async Task ApplyContentFitAsync(PdfLinkDestination destination, long navigationVersion)
    {
        if (_disposed || _document is not { } document) return;
        Rect? content = null;
        try { content = await document.GetPageContentBoundsAsync(destination.PageIndex); }
        catch (OperationCanceledException) { return; }
        catch (PdfDocumentException) { } // An unreadable box falls back to the page box below.
        if (_disposed || !ReferenceEquals(document, _document) || navigationVersion != _navigationVersion ||
            PageCount == 0 || destination.PageIndex >= PageCount)
            return;
        var page = document.Pages[destination.PageIndex];
        var box = content ?? new Rect(0, 0, page.Width, page.Height);
        var center = box.Center;
        var corner = ViewMinCorner(box);
        LinkTarget target = destination.Fit switch
        {
            PdfDestinationFit.FitBH => LinkTarget.Both(new Point(center.X, corner.Y), AxisPin.Center, AxisPin.Edge),
            PdfDestinationFit.FitBV => LinkTarget.Both(new Point(corner.X, center.Y), AxisPin.Edge, AxisPin.Center),
            _ => LinkTarget.Both(center, AxisPin.Center, AxisPin.Center),
        };
        if (destination.Fit == PdfDestinationFit.FitBH && destination.Top is { } top && double.IsFinite(top))
            target = new LinkTarget(target.XAnchor, new Point(center.X, top), AxisPin.Center, AxisPin.Edge);
        if (destination.Fit == PdfDestinationFit.FitBV && destination.Left is { } left && double.IsFinite(left))
            target = new LinkTarget(new Point(left, center.Y), target.YAnchor, AxisPin.Edge, AxisPin.Center);
        // These are updates within this request. Suppress the ordinary zoom-mode
        // handler's GoToPage so it cannot invalidate the fit as a new request.
        var wasFitting = _fitting;
        _fitting = true;
        try
        {
            SetCurrentValue(ZoomModeProperty, PdfZoomMode.Manual);
            if (navigationVersion != _navigationVersion || !ReferenceEquals(document, _document) || _disposed) return;
            ZoomToFitBox(box,
                fitWidth: destination.Fit != PdfDestinationFit.FitBV,
                fitHeight: destination.Fit != PdfDestinationFit.FitBH);
        }
        finally { _fitting = wasFitting; }
        if (navigationVersion != _navigationVersion || !ReferenceEquals(document, _document) || _disposed) return;
        GoToPageCore(destination.PageIndex + 1, navigationVersion);
        if (navigationVersion != _navigationVersion || !ReferenceEquals(document, _document) || _disposed) return;
        ApplyNavigationTarget(destination.PageIndex, target);
    }

    private void ApplyNavigationTarget(int pageIndex, LinkTarget placement)
    {
        _pendingNavigationPage = pageIndex;
        _pendingSearchTarget = null;
        _pendingViewportAnchor = null;
        _pendingLinkTarget = placement;
        TryApplyNavigation();
        RefreshViewportWithoutPageChange();
    }

    /// <summary>
    /// The rectangle corner that lands at the viewport's top-left under the current viewer
    /// rotation: rotation turns the rectangle on screen, so a non-square rectangle is pinned by
    /// a different page-display corner per quarter turn.
    /// </summary>
    private Point ViewMinCorner(Rect rectangle) => (((Rotation % 360) + 360) % 360 / 90) switch
    {
        1 => new Point(rectangle.X, rectangle.Bottom),
        2 => new Point(rectangle.Right, rectangle.Bottom),
        3 => new Point(rectangle.Right, rectangle.Y),
        _ => rectangle.Position,
    };

    // /FitR and the content-box fits: a specific magnification that contains the rectangle,
    // not a fit mode. Width-only / height-only fits honor just that axis; viewer rotation
    // 90 / 270 swaps the rectangle's on-screen width and height.
    private void ZoomToFitBox(Rect rectangle, bool fitWidth, bool fitHeight)
    {
        if (_scroll is null || rectangle.Width <= 0 || rectangle.Height <= 0) return;
        var size = _scroll.Viewport;
        if (size.Width <= 60 || size.Height <= 60) size = _scroll.Bounds.Size;
        if (size.Width <= 60 || size.Height <= 60) return;
        var swap = ((Rotation % 360) + 360) % 360 / 90 % 2 == 1;
        var width = (swap ? rectangle.Height : rectangle.Width) * 96d / 72;
        var height = (swap ? rectangle.Width : rectangle.Height) * 96d / 72;
        var zoom = double.PositiveInfinity;
        if (fitWidth) zoom = Math.Min(zoom, (size.Width - (PdfPageLayout.Gap * 2 + 4)) / width);
        if (fitHeight) zoom = Math.Min(zoom, (size.Height - PdfPageLayout.Gap * 2) / height);
        if (!double.IsFinite(zoom) || zoom <= 0) return;
        zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        if (Math.Abs(zoom - ZoomLevel) < .0001) return;
        SetCurrentValue(ZoomLevelProperty, zoom);
        _presenter?.RebuildLayout();
    }

    // Called by the presenter after a click on a link annotation. External and unsupported
    // links do nothing by default; hosts act through the LinkActivated event.
    internal void ActivateLink(PdfLink link)
    {
        if (_disposed) return;
        var args = new PdfLinkActivatedEventArgs(link);
        LinkActivated?.Invoke(this, args);
        if (args.Handled) return;
        if (link.Kind == PdfLinkKind.Internal && link.Destination is { PageIndex: >= 0 } destination)
            NavigateToDestination(destination);
    }
    internal void ScrollBy(double dy)
    {
        _navigationPage = _pendingNavigationPage = null;
        _navigationOffset = null;
        if (_scroll is not null) _scroll.Offset = new Vector(_scroll.Offset.X, Math.Max(0, _scroll.Offset.Y + dy));
    }

    /// <summary>
    /// Searches document text (scanned pages need an existing OCR text layer) and publishes
    /// <see cref="SearchResults"/> page by page while the scan continues. Results use zero-based
    /// page indexes; a search that reaches <see cref="SearchResultLimit"/> sets
    /// <see cref="SearchResultsTruncated"/>.
    /// </summary>
    public async Task<IReadOnlyList<PdfSearchResult>> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        _searchTimer.Stop();
        _settingSearch = true;
        try { SetCurrentValue(SearchQueryProperty, query); }
        finally { _settingSearch = false; }
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = CancellationTokenSource.CreateLinkedTokenSource(_documentCancellation.Token, cancellationToken);
        var token = _searchCancellation.Token;
        var document = _document;
        ClearSearchResults();
        var version = ++_searchVersion;
        if (document is null || !Capabilities.CanSearch || string.IsNullOrWhiteSpace(query)) return [];
        SetAndRaise(IsSearchingProperty, ref _isSearching, true);
        PseudoClasses.Set(":searching", true);
        UpdateStatus();
        try
        {
            var collected = new List<PdfSearchResult>();
            var navigated = false;
            await foreach (var page in document.SearchPagesAsync(query, SearchMatchCase, SearchWholeWord, SearchResultLimit, token))
            {
                token.ThrowIfCancellationRequested();
                if (version != _searchVersion || !ReferenceEquals(document, _document)) throw new OperationCanceledException(token);
                if (page.Matches.Count > 0)
                {
                    collected.AddRange(page.Matches);
                    // Publish each page's batch so bindings and status see progressive results.
                    SetAndRaise(SearchResultsProperty, ref _searchResults, collected.ToArray());
                    SetAndRaise(SearchResultCountProperty, ref _searchResultCount, collected.Count);
                    if (!navigated)
                    {
                        navigated = true;
                        NavigateSearchResult(0);
                    }
                }
                SetAndRaise(SearchProgressProperty, ref _searchProgress, (page.PageIndex + 1.0) / document.Pages.Count);
                _presenter?.InvalidateVisual();
                UpdateStatus();
            }
            token.ThrowIfCancellationRequested();
            if (version != _searchVersion || !ReferenceEquals(document, _document)) throw new OperationCanceledException(token);
            var truncated = collected.Count >= SearchResultLimit;
            // A truncated walk stops early, so progress stays at the fraction actually scanned;
            // an exhausted walk already reaches 1 from its last page batch.
            if (!truncated)
                SetAndRaise(SearchProgressProperty, ref _searchProgress, 1);
            SetAndRaise(SearchResultsTruncatedProperty, ref _searchResultsTruncated, truncated);
            _presenter?.InvalidateVisual();
            // Natural completion only: cancel, supersede and empty-query exits raise nothing.
            SearchCompleted?.Invoke(this, new PdfSearchCompletedEventArgs(_searchResults.Count, truncated));
            return _searchResults;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { if (!token.IsCancellationRequested) ReportError(error); throw; }
        finally
        {
            if (version == _searchVersion)
            {
                SetAndRaise(IsSearchingProperty, ref _isSearching, false);
                PseudoClasses.Set(":searching", false);
                UpdateStatus();
            }
        }
    }

    private void ClearSearchResults()
    {
        ++_searchVersion;
        SetAndRaise(SearchResultsProperty, ref _searchResults, Array.Empty<PdfSearchResult>());
        SetAndRaise(SearchResultCountProperty, ref _searchResultCount, 0);
        SetAndRaise(CurrentSearchResultIndexProperty, ref _currentSearchResultIndex, -1);
        SetAndRaise(SearchProgressProperty, ref _searchProgress, 0);
        SetAndRaise(SearchResultsTruncatedProperty, ref _searchResultsTruncated, false);
        SetAndRaise(IsSearchingProperty, ref _isSearching, false);
        PseudoClasses.Set(":searching", false);
        _presenter?.InvalidateVisual();
        UpdateStatus();
    }

    /// <summary>Cancels any running search and clears the query, results, progress and limit flag.</summary>
    public void ClearSearch()
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        _searchTimer.Stop();
        _searchCancellation?.Cancel();
        _settingSearch = true;
        try
        {
            SetCurrentValue(SearchQueryProperty, null);
            if (_searchBox is not null && !string.IsNullOrEmpty(_searchBox.Text)) _searchBox.Text = "";
        }
        finally { _settingSearch = false; }
        ClearSearchResults();
    }

    public void FindNext()
    {
        VerifyAccess();
        if (!Capabilities.CanSearch) return;
        if (SearchResultCount > 0) NavigateSearchResult((CurrentSearchResultIndex + 1) % SearchResultCount);
        else if (!string.IsNullOrWhiteSpace(SearchQuery)) Observe(SearchAsync(SearchQuery));
    }
    public void FindPrevious()
    {
        VerifyAccess();
        if (!Capabilities.CanSearch) return;
        if (SearchResultCount > 0) NavigateSearchResult((CurrentSearchResultIndex - 1 + SearchResultCount) % SearchResultCount);
        else if (!string.IsNullOrWhiteSpace(SearchQuery)) Observe(SearchAsync(SearchQuery));
    }

    private void NavigateSearchResult(int index)
    {
        SetAndRaise(CurrentSearchResultIndexProperty, ref _currentSearchResultIndex, index);
        var result = _searchResults[index];
        GoToPage(result.PageIndex + 1);
        _pendingNavigationPage = result.PageIndex;
        _pendingSearchTarget = result;
        TryApplyNavigation();
        _presenter?.InvalidateVisual();
        UpdateStatus();
    }

    public async Task SelectAllAsync()
    {
        VerifyAccess();
        if (CanSelectText && _presenter is not null) await _presenter.SelectAllAsync();
    }

    /// <summary>Clears the active text selection, if any (the Esc shortcut's public entry).</summary>
    public void ClearSelection()
    {
        VerifyAccess();
        if (_disposed) return;
        _presenter?.ClearSelection();
    }

    public async Task CopySelectionToClipboardAsync()
    {
        VerifyAccess();
        if (!CanSelectText || _presenter is null) return;
        var version = _loadVersion;
        try
        {
            var text = await _presenter.GetSelectedTextAsync();
            if (!CanSelectText || version != _loadVersion || _disposed) return;
            SetSelectedText(text);
            if (!string.IsNullOrEmpty(text) && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(text);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { ReportError(error); }
    }

    /// <summary>
    /// Extracts one page's Unicode characters and bounds (same coordinate system as
    /// <see cref="PdfTextCharacter"/>). Requires the document to allow text extraction.
    /// </summary>
    public Task<PdfTextPage> GetPageTextAsync(int pageIndex, CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        var document = RequireDocumentForExtraction();
        return document.GetTextAsync(pageIndex, cancellationToken);
    }

    /// <summary>
    /// Extracts the whole document's text with a form feed ('\f') between pages. Requires the
    /// document to allow text extraction.
    /// </summary>
    public Task<string> GetDocumentTextAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        var document = RequireDocumentForExtraction();
        return document.GetFullTextAsync(cancellationToken);
    }

    /// <summary>
    /// Renders one page at the given DPI (36..1200) and returns opaque PNG bytes; the page's
    /// built-in rotation and crop are honored. Cancels when the document is replaced meanwhile.
    /// </summary>
    public async Task<byte[]> ExportPageImageAsync(int pageIndex, double dpi = 96, CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        if (dpi is < 36 or > 1200)
            throw new ArgumentOutOfRangeException(nameof(dpi), PdfViewerStrings.ErrorExportDpiRange);
        var document = _document ?? throw new InvalidOperationException(PdfViewerStrings.ErrorNoDocument);
        if (!Capabilities.CanRead)
            throw new UnauthorizedAccessException(PdfViewerStrings.ErrorDocumentUnreadable);
        if (pageIndex >= document.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        var version = _loadVersion;
        var page = document.Pages[pageIndex];
        // 72 PDF points per inch; round to the nearest pixel and always render at least one.
        var pixelWidth = Math.Max(1, (int)Math.Round(page.Width * dpi / 72));
        var pixelHeight = Math.Max(1, (int)Math.Round(page.Height * dpi / 72));
        var rendered = await document.RenderAsync(pageIndex, pixelWidth, pixelHeight, cancellationToken: cancellationToken);
        if (version != _loadVersion || _disposed) throw new OperationCanceledException();
        // Self-contained PNG encoding: Bitmap.Save is a platform imaging service and writes
        // nothing under Avalonia.Headless, while this path behaves identically everywhere.
        return PngEncoder.Encode(rendered);
    }

    private PdfDocument RequireDocumentForExtraction()
    {
        if (_document is not { } document)
            throw new InvalidOperationException(PdfViewerStrings.ErrorNoDocument);
        if (!CanSelectText)
            throw new UnauthorizedAccessException(PdfViewerStrings.ErrorCopyDenied);
        return document;
    }

    // Keyboard contract (in priority order): search-box Enter/F3 walk results wherever focus is;
    // text inputs own every other key; the remaining shortcuts apply to the viewer. AllowShortcuts
    // false or a disabled control suppresses the whole shortcut layer. Documented in
    // docs/pdfviewer-usage.md; tests exercise the same order.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled || !AllowShortcuts) return;
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (command && e.Key == Key.O) { Observe(OpenDocumentAsync()); e.Handled = true; return; }
        if (command && e.Key == Key.F && Capabilities.CanSearch)
        {
            SetCurrentValue(IsToolbarVisibleProperty, true);
            _searchBox?.Focus(); _searchBox?.SelectAll(); e.Handled = true;
            return;
        }
        if (e.Key == Key.F3 || (e.Key == Key.Enter && _searchBox?.IsKeyboardFocusWithin == true))
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) FindPrevious(); else FindNext();
            e.Handled = true;
            return;
        }
        // Text-entry controls keep their own keys (caret movement, in-control copy, activation).
        if (e.Source is TextBox or NumericUpDown or Button or ToggleButton or CheckBox or MenuItem) return;
        if (command && e.Key == Key.S)
        {
            var save = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? SaveAsCommand : SaveCommand;
            if (save.CanExecute(null)) { save.Execute(null); e.Handled = true; }
            return;
        }
        if (command && e.Key is Key.Z or Key.Y)
        {
            var history = e.Key == Key.Y || e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? RedoCommand : UndoCommand;
            if (history.CanExecute(null)) { history.Execute(null); e.Handled = true; }
            return;
        }
        if (command && e.Key == Key.C) { Observe(CopySelectionToClipboardAsync()); e.Handled = true; }
        else if (command && e.Key == Key.A) { Observe(SelectAllAsync()); e.Handled = true; }
        else if (command && e.Key is Key.Add or Key.OemPlus) { ZoomIn(); e.Handled = true; }
        else if (command && e.Key is Key.Subtract or Key.OemMinus) { ZoomOut(); e.Handled = true; }
        else if (command && e.Key is Key.D0 or Key.NumPad0) { ActualSize(); e.Handled = true; }
        else if (command && e.Key is Key.D1 or Key.NumPad1) { FitPage(); e.Handled = true; }
        else if (command && e.Key is Key.D2 or Key.NumPad2) { FitWidth(); e.Handled = true; }
        else if (command && e.Key is Key.D3 or Key.NumPad3) { FitHeight(); e.Handled = true; }
        else if (e.Key == Key.Home) { GoToPage(1); e.Handled = true; }
        else if (e.Key == Key.End) { GoToPage(PageCount); e.Handled = true; }
        else if (e.Key == Key.Left) { PreviousPage(); e.Handled = true; }
        else if (e.Key == Key.Right) { NextPage(); e.Handled = true; }
        else if (e.Key == Key.PageDown || (e.Key == Key.Space && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            if (!IsTwoPageMode) ScrollBy(Math.Max(100, (_scroll?.Viewport.Height ?? 600) - 40));
            else NextPage();
            e.Handled = true;
        }
        else if (e.Key == Key.PageUp || (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            if (!IsTwoPageMode) ScrollBy(-Math.Max(100, (_scroll?.Viewport.Height ?? 600) - 40));
            else PreviousPage();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape) { ClearSelection(); e.Handled = true; }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        // Ctrl/Cmd wheel zoom is part of the shortcut layer; plain wheel scrolling stays native.
        if (IsEnabled && AllowShortcuts &&
            (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            if (e.Delta.Y > 0) ZoomIn(); else if (e.Delta.Y < 0) ZoomOut();
            e.Handled = true;
        }
        base.OnPointerWheelChanged(e);
    }
}
