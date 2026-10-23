using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.LogicalTree;
using Avalonia.Metadata;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdig;

namespace NT.AvaloniaControl.Markdown;

internal readonly record struct MarkdownReadingAnchor(long Id, double Offset, MarkdownReadingLayoutAnchor? Layout);

/// <summary>A read-only native Markdown reader with a document-wide logical selection.</summary>
[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer), IsRequired = true)]
[TemplatePart("PART_DocumentPresenter", typeof(MarkdownDocumentPresenter), IsRequired = true)]
public partial class Markdown : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<Markdown, string?>(nameof(Text));
    public static readonly StyledProperty<bool> SelectionEnabledProperty = AvaloniaProperty.Register<Markdown, bool>(nameof(SelectionEnabled), true);
    public static readonly StyledProperty<bool> AutoScrollToEndProperty = AvaloniaProperty.Register<Markdown, bool>(nameof(AutoScrollToEnd));
    public static readonly StyledProperty<IBrush> SelectionBrushProperty = AvaloniaProperty.Register<Markdown, IBrush>(nameof(SelectionBrush), new SolidColorBrush(0x663399ff));
    public static readonly StyledProperty<IBrush> CaretBrushProperty = AvaloniaProperty.Register<Markdown, IBrush>(nameof(CaretBrush), Brushes.DodgerBlue);
    public static readonly StyledProperty<IMarkdownImageLoader?> ImageLoaderProperty = AvaloniaProperty.Register<Markdown, IMarkdownImageLoader?>(nameof(ImageLoader), inherits: true);
    public static readonly StyledProperty<IMarkdownCodeHighlighter?> CodeHighlighterProperty = AvaloniaProperty.Register<Markdown, IMarkdownCodeHighlighter?>(nameof(CodeHighlighter), inherits: true);
    public static readonly StyledProperty<Uri?> BaseUriProperty = AvaloniaProperty.Register<Markdown, Uri?>(nameof(BaseUri), inherits: true, validate: uri => uri is null || uri.IsAbsoluteUri);
    public static readonly StyledProperty<Func<MarkdownElementDescriptor, MarkdownServiceOverrides?>?> ElementServicesProperty = AvaloniaProperty.Register<Markdown, Func<MarkdownElementDescriptor, MarkdownServiceOverrides?>?>(nameof(ElementServices));
    public static readonly StyledProperty<MarkdownRendererRegistry> NodeRenderersProperty = AvaloniaProperty.Register<Markdown, MarkdownRendererRegistry>(nameof(NodeRenderers), MarkdownRendererRegistry.Empty, validate: value => value is not null);
    public static readonly DirectProperty<Markdown, MarkdownDocumentSnapshot> DocumentProperty = AvaloniaProperty.RegisterDirect<Markdown, MarkdownDocumentSnapshot>(nameof(Document), control => control.Document);
    public static readonly DirectProperty<Markdown, string> SelectedTextProperty = AvaloniaProperty.RegisterDirect<Markdown, string>(nameof(SelectedText), control => control.SelectedText);
    public static readonly DirectProperty<Markdown, bool> CanCopyProperty = AvaloniaProperty.RegisterDirect<Markdown, bool>(nameof(CanCopy), control => control.CanCopy);

    private MarkdownDocumentSnapshot _document = MarkdownDocumentSnapshot.Empty;
    private MarkdownSelection _selection;
    private string _selectedText = string.Empty;
    private bool _canCopy;
    private bool _syncText;
    private string? _syncTextValue;
    private bool _attached;
    private ScrollViewer? _scroll;
    private MarkdownDocumentPresenter? _presenter;
    private IPointer? _pointer;
    private Point _dragPosition;
    private DispatcherTimer? _dragTimer;
    private bool _following = true;
    private bool _restoring;
    private long _readingRevision;
    private readonly HashSet<string> _visitedLinks = [];
    private MarkdownSpan? _hoverLink;
    private int _linkFocusPosition = -1;
    private MarkdownParser _parser = new();
    private int _activeResourceRequests;
    private readonly MarkdownResourceScheduler _resourceRequests = new();
    private readonly List<MarkdownDiagnostic> _renderingDiagnostics = [];
    private readonly HashSet<(long, int, string)> _renderingDiagnosticKeys = [];

    static Markdown() { FocusableProperty.OverrideDefaultValue<Markdown>(true); }
    public Markdown() { InitializeUpdates(); Styles.CollectionChanged += LocalStylesChanged; }

    private void LocalStylesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs change)
    {
        if (_presenter is null) return;
        var anchor = CaptureReadingAnchor();
        var styleHost = (Avalonia.Styling.IStyleHost)_presenter;
        if (change.OldItems is { } removed) styleHost.StylesRemoved(removed.Cast<Avalonia.Styling.IStyle>().ToArray());
        if (change.NewItems is { } added) styleHost.StylesAdded(added.Cast<Avalonia.Styling.IStyle>().ToArray());
        if (change.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move) styleHost.StylesAdded(Styles.ToArray());
        ApplyStyling();
        // Inline style invalidation does not schedule a Visual measure by itself.
        // Reapply through the public styling API to the realized logical nodes.
        _presenter.ApplyStyling();
        foreach (var element in _presenter.GetLogicalDescendants().OfType<StyledElement>()) element.ApplyStyling();
        RestoreReadingAnchor(anchor);
    }

    [Content]
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool SelectionEnabled { get => GetValue(SelectionEnabledProperty); set => SetValue(SelectionEnabledProperty, value); }
    public bool AutoScrollToEnd { get => GetValue(AutoScrollToEndProperty); set => SetValue(AutoScrollToEndProperty, value); }
    public IBrush SelectionBrush { get => GetValue(SelectionBrushProperty); set => SetValue(SelectionBrushProperty, value); }
    public IBrush CaretBrush { get => GetValue(CaretBrushProperty); set => SetValue(CaretBrushProperty, value); }
    public IMarkdownImageLoader? ImageLoader { get => GetValue(ImageLoaderProperty); set => SetValue(ImageLoaderProperty, value); }
    public IMarkdownCodeHighlighter? CodeHighlighter { get => GetValue(CodeHighlighterProperty); set => SetValue(CodeHighlighterProperty, value); }
    public Uri? BaseUri { get => GetValue(BaseUriProperty); set => SetValue(BaseUriProperty, value); }
    public Func<MarkdownElementDescriptor, MarkdownServiceOverrides?>? ElementServices { get => GetValue(ElementServicesProperty); set => SetValue(ElementServicesProperty, value); }
    public MarkdownRendererRegistry NodeRenderers { get => GetValue(NodeRenderersProperty); set => SetValue(NodeRenderersProperty, value); }
    public IReadOnlyList<MarkdownDiagnostic> RenderingDiagnostics => _renderingDiagnostics.ToArray();
    public int ActiveParseCount => _updates.ActiveParseCount;
    public int PendingRequestCount => _updates.PendingRequestCount;
    public int ActiveResourceRequestCount => Volatile.Read(ref _activeResourceRequests);
    public int ExecutingResourceRequestCount => _resourceRequests.ActiveCount;
    public int PendingResourceRequestCount => _resourceRequests.QueuedCount;
    public MarkdownDocumentSnapshot Document => _document;
    public MarkdownSelection Selection => _selection;
    public string SelectedText => _selectedText;
    public bool CanCopy => _canCopy;
    public MarkdownDocumentPresenter? DocumentPresenter => _presenter;
    public bool HasPresentationEnvironment => _attached && _scroll is not null && _presenter is not null;
    public event EventHandler? SelectionChanged;
    public event EventHandler? Updated;
    public event EventHandler<MarkdownCopyEventArgs>? CopyingToClipboard;
    public event EventHandler<MarkdownLinkEventArgs>? LinkClicked;
    public event EventHandler<MarkdownErrorEventArgs>? Error;
    public event EventHandler<MarkdownElementPreparedEventArgs>? ElementPrepared;
    public event EventHandler<MarkdownUpdateMetrics>? UpdateMeasured;

    // PRIVATE EXPERIMENT: only native default realizations may change preparation order.
    internal bool CanRefineDefaultNativeRange => GetType() == typeof(Markdown) &&
        ElementPrepared is null && ElementServices is null && ImageLoader is null &&
        NodeRenderers.Blocks.IsEmpty && NodeRenderers.Inlines.IsEmpty;

    public void ConfigurePipeline(Action<MarkdownPipelineBuilder>? configure)
    {
        Dispatcher.UIThread.VerifyAccess();
        _parser = new MarkdownParser(configure);
        ReconfigureUpdates();
    }

    public void ConfigureParser(MarkdownParserOptions options)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentNullException.ThrowIfNull(options);
        _parser = MarkdownParser.FromOptions(options);
        ReconfigureUpdates();
    }

    internal void PrepareElement(StyledElement element, MarkdownBlock block, MarkdownInline? inline = null)
    {
        var descriptor = new MarkdownElementDescriptor(block, inline);
        try
        {
            if (ElementServices?.Invoke(descriptor) is { } overrides)
            {
                if (element is MarkdownImage image && overrides.OverrideImageLoader) image.ImageLoader = overrides.ImageLoader;
                if (element is MarkdownCodeBlock code && overrides.OverrideCodeHighlighter) code.CodeHighlighter = overrides.CodeHighlighter;
            }
        }
        catch (Exception error) { ReportError("ElementServices", error); }
        ElementPrepared?.Invoke(this, new(element, descriptor));
    }
    internal void ResourceRequestStarted() => Interlocked.Increment(ref _activeResourceRequests);
    internal void ResourceRequestFinished() => Interlocked.Decrement(ref _activeResourceRequests);
    internal Task<T> RunResourceAsync<T>(Func<CancellationToken, ValueTask<T>> action, CancellationToken token) => _resourceRequests.RunAsync(action, token);
    internal void ReportRenderingDiagnostic(string key, long nodeId, int sourceStart, int sourceLength, string message, Exception? error = null)
    {
        if (_renderingDiagnosticKeys.Add((nodeId, sourceStart, key))) _renderingDiagnostics.Add(new("MDRENDER", message, sourceStart, sourceLength));
        if (error is not null) ReportError("Renderer", error);
    }

    public void SetSelection(int anchor, int active)
    {
        Dispatcher.UIThread.VerifyAccess();
        var next = SelectionEnabled ? new MarkdownSelection(anchor, active).Normalize(Document.Text) : default;
        if (next == _selection && _selectedText == next.GetText(Document)) return;
        _selection = next;
        SetAndRaise(SelectedTextProperty, ref _selectedText, next.GetText(Document));
        SetAndRaise(CanCopyProperty, ref _canCopy, !_selection.IsEmpty);
        _presenter?.InvalidateSelection();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    public void SelectAll() => SetSelection(0, Document.Text.Length);
    public void ClearSelection() => SetSelection(0, 0);
    public void Copy() => _ = CopyAsync();
    public Task<MarkdownCopyResult> CopyAsync() => CopyTextAsync(SelectedText);
    internal async Task<MarkdownCopyResult> CopyTextAsync(string text)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (text.Length == 0) return MarkdownCopyResult.EmptySelection;
        var args = new MarkdownCopyEventArgs(text);
        CopyingToClipboard?.Invoke(this, args);
        if (args.Cancel) return MarkdownCopyResult.Cancelled;
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return MarkdownCopyResult.ClipboardUnavailable;
        try { await clipboard.SetTextAsync(text); return MarkdownCopyResult.Copied; }
        catch (Exception error) { ReportError("Clipboard", error); return MarkdownCopyResult.Failed; }
    }

    public void ScrollToEnd()
    {
        Dispatcher.UIThread.VerifyAccess();
        _readingRevision++;
        _following = true;
        _scroll?.ScrollToEnd();
    }

    /// <summary>Cancels a drag when a hosting page is deactivated.</summary>
    public void CancelInteraction()
    {
        _pointer?.Capture(null);
        _pointer = null;
        _dragTimer?.Stop();
    }

    internal Uri? ResolveUri(string url) => MarkdownUriResolver.Resolve(url, BaseUri);
    internal bool IsLinkVisited(string? url) => url is not null && _visitedLinks.Contains(ResolveUri(url)?.AbsoluteUri ?? url);
    internal void ReportError(string category, Exception error) => Error?.Invoke(this, new(category, error));
    internal MarkdownReadingAnchor? CaptureReadingAnchor()
    {
        if (_scroll is null) return null;
        if (AutoScrollToEnd) _following = _scroll.Extent.Height - _scroll.Viewport.Height - _scroll.Offset.Y <= 24;
        return _presenter?.CaptureAnchor(_scroll.Offset.Y) is { } block ? new(block.Id, block.Offset, CaptureReadingLayoutAnchor()) : null;
    }
    internal void RestoreReadingAnchor(MarkdownReadingAnchor? anchor)
    {
        if (anchor?.Layout is { } layout) { ResetCoarseReadingAnchor(); RestoreReadingLayoutAnchor(layout); return; }
        RestoreCoarseReadingAnchor(anchor);
    }

    private double ApplyDocument(MarkdownDocumentSnapshot next, bool replace, string source, Func<bool> stillCurrent, out bool followingLayoutPending)
    {
        Dispatcher.UIThread.VerifyAccess();
        followingLayoutPending = false;
        var anchor = CaptureReadingAnchor();
        var following = CaptureFollowingDocumentCommit();
        var selection = replace ? default : MarkdownDocumentMatcher.MigrateSelection(Document, next, _selection);
        var focusedLink = _linkFocusPosition >= 0 ? LinkAt(Document, _linkFocusPosition) : null;
        var nextLinkPosition = focusedLink is not null && !replace ? MarkdownDocumentMatcher.MigrateSelection(Document, next, new(_linkFocusPosition, _linkFocusPosition)).Active : -1;
        var nextLink = nextLinkPosition >= 0 ? LinkAt(next, nextLinkPosition) : null;
        ResetLinkPresentation();
        if (focusedLink is not null && nextLink is not null && nextLink.TextStart == nextLinkPosition &&
            focusedLink.SemanticKind == nextLink.SemanticKind && focusedLink.Text == nextLink.Text &&
            focusedLink.Metadata.GetValueOrDefault("url") == nextLink.Metadata.GetValueOrDefault("url") &&
            focusedLink.Metadata.GetValueOrDefault("label") == nextLink.Metadata.GetValueOrDefault("label")) _linkFocusPosition = nextLinkPosition;
        _syncTextValue = source;
        _syncText = true;
        try { SetCurrentValue(TextProperty, source); }
        finally { _syncText = false; }
        if (!stillCurrent()) return 0;
        SetAndRaise(DocumentProperty, ref _document, next);
        _renderingDiagnostics.Clear(); _renderingDiagnosticKeys.Clear();
        SetSelection(selection.Anchor, selection.Active);
        _presenter?.SetDocument(next, preserveHeights: !replace);
        var layoutWatch = System.Diagnostics.Stopwatch.StartNew();
        var followingResult = MarkdownFollowingDocumentCommitResult.NotFollowing;
        if (HasPresentationEnvironment && IsEffectivelyVisible)
        {
            followingResult = TryApplyFollowingDocumentCommit(following, stillCurrent);
            if (followingResult == MarkdownFollowingDocumentCommitResult.NotFollowing) UpdateLayout();
            else if (followingResult != MarkdownFollowingDocumentCommitResult.Pending && stillCurrent() &&
                (!IsMeasureValid || !IsArrangeValid || _presenter is { IsMeasureValid: false } or { IsArrangeValid: false }))
                // The following scope has ended. If a reentrant user navigation
                // invalidated the range, realize that actual viewport without a
                // second follow or a replay of the previous reading anchor.
                UpdateLayout();
        }
        layoutWatch.Stop();
        followingLayoutPending = followingResult == MarkdownFollowingDocumentCommitResult.Pending ||
            (followingResult != MarkdownFollowingDocumentCommitResult.NotFollowing &&
                (!IsMeasureValid || !IsArrangeValid || _presenter is { IsMeasureValid: false } or { IsArrangeValid: false }));
        if (followingResult == MarkdownFollowingDocumentCommitResult.NotFollowing) RestoreReadingAnchor(anchor);
        if (stillCurrent()) Updated?.Invoke(this, EventArgs.Empty);
        return layoutWatch.Elapsed.TotalMilliseconds;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        CancelInteraction();
        ResetLinkPresentation();
        ResetReadingLayoutTracking();
        if (_scroll is not null) { _scroll.ScrollChanged -= ScrollChanged; _scroll.PropertyChanged -= ScrollOffsetChanged; }
        if (_presenter is not null) { _presenter.ResetPresentation(); _presenter.Owner = null; }
        base.OnApplyTemplate(e);
        _scroll = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _presenter = e.NameScope.Find<MarkdownDocumentPresenter>("PART_DocumentPresenter");
        if (_presenter is not null) { _presenter.Owner = this; _presenter.SetDocument(Document); }
        if (_scroll is not null) { _scroll.ScrollChanged += ScrollChanged; _scroll.PropertyChanged += ScrollOffsetChanged; }
        if (_scroll is null || _presenter is null) ReportError("Template", new InvalidOperationException("Markdown requires PART_ScrollViewer and PART_DocumentPresenter."));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        if (_presenter is not null) _presenter.Owner = this;
        if (_scroll is not null) { _scroll.ScrollChanged -= ScrollChanged; _scroll.ScrollChanged += ScrollChanged; _scroll.PropertyChanged -= ScrollOffsetChanged; _scroll.PropertyChanged += ScrollOffsetChanged; }
        InitializeReadingLayoutTracking();
        ResumeUpdates();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        ClearReadingLayoutTracking();
        if (_scroll is not null) { _scroll.ScrollChanged -= ScrollChanged; _scroll.PropertyChanged -= ScrollOffsetChanged; }
        CancelInteraction();
        ResetLinkPresentation();
        SuspendUpdates();
        _presenter?.ResetPresentation();
        base.OnDetachedFromVisualTree(e);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty && (!_syncText || change.NewValue as string != _syncTextValue)) RequestParse(true);
        else if (change.Property == SelectionEnabledProperty && !SelectionEnabled) { CancelInteraction(); ClearSelection(); }
        else if (change.Property == NodeRenderersProperty) ReconfigureUpdates();
        else if (change.Property == ElementServicesProperty) { var anchor = CaptureReadingAnchor(); _presenter?.ResetPresentation(); RestoreReadingAnchor(anchor); }
        else if (change.Property == BaseUriProperty)
        {
            foreach (var span in LinkSpans())
                if (IsLinkVisited(span.Url)) span.Classes.Add("visited"); else span.Classes.Remove("visited");
        }
        else if (change.Property == IsVisibleProperty && !IsVisible) CancelInteraction();
        else if (change.Property == SelectionBrushProperty || change.Property == CaretBrushProperty) _presenter?.InvalidateSelection();
    }
    private void ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroll is null) return;
        if (!_restoring && Math.Abs(e.OffsetDelta.Y) > .01) _following = _scroll.Extent.Height - _scroll.Viewport.Height - _scroll.Offset.Y <= 24;
        _presenter?.SetViewport(_scroll.Offset.Y, _scroll.Viewport.Height);
        ReadingLayoutScrolled();
    }
    private void ScrollOffsetChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ScrollViewer.OffsetProperty && !_restoring && !_readingLayoutRestoring &&
            !IsFollowingDocumentOffsetAdjustment(change) &&
            _scroll is { IsMeasureValid: true, IsArrangeValid: true } && change.GetOldValue<Vector>().Y != change.GetNewValue<Vector>().Y)
        { _readingRevision++; ResetReadingLayoutTracking(); }
        if (change.Property == ScrollViewer.OffsetProperty && _scroll is not null)
            _presenter?.SetViewport(_scroll.Offset.Y, _scroll.Viewport.Height);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Source is ScrollBar || e.Source is Visual scrollSource && scrollSource.GetVisualAncestors().Any(parent => parent is ScrollBar)) { _readingRevision++; ResetReadingLayoutTracking(); }
        if (_presenter is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Source is Button or ScrollBar || e.Source is Visual source && source.GetVisualAncestors().Any(parent => parent is Button or ScrollBar)) return;
        if (!SelectionEnabled) { ActivateLink(_presenter.Hit(e.GetPosition(_presenter))); e.Handled = true; return; }
        _readingRevision++;
        Focus();
        var position = _presenter.Hit(e.GetPosition(_presenter));
        if (e.ClickCount == 2)
        {
            var start = position; var end = position;
            while (start > 0 && !char.IsWhiteSpace(Document.Text[start - 1])) start = MarkdownTextBoundary.Previous(Document.Text, start);
            while (end < Document.Text.Length && !char.IsWhiteSpace(Document.Text[end])) end = MarkdownTextBoundary.Next(Document.Text, end);
            SetSelection(start, end);
        }
        else SetSelection(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? _selection.Anchor : position, position);
        _pointer = e.Pointer;
        e.Pointer.Capture(this);
        _dragPosition = e.GetPosition(this);
        StartDragTimer();
        e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pointer is null && _presenter is not null)
        {
            var hit = _presenter.Hit(e.GetPosition(_presenter));
            var hovered = LinkSpans().FirstOrDefault(span => hit >= span.TextStart && hit < span.TextStart + span.TextLength);
            if (!ReferenceEquals(hovered, _hoverLink))
            {
                _hoverLink?.Classes.Remove("pointerover");
                _hoverLink = hovered;
                _hoverLink?.Classes.Add("pointerover");
                Cursor = hovered is null ? Cursor.Default : new Cursor(StandardCursorType.Hand);
            }
        }
        if (_pointer != e.Pointer || _presenter is null) return;
        _dragPosition = e.GetPosition(this);
        SetSelection(_selection.Anchor, _presenter.Hit(e.GetPosition(_presenter)));
        e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pointer != e.Pointer) return;
        var position = _selection.Active;
        var activate = _selection.IsEmpty;
        CancelInteraction();
        if (activate) ActivateLink(position);
        e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { base.OnPointerCaptureLost(e); _pointer = null; _dragTimer?.Stop(); }
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverLink?.Classes.Remove("pointerover");
        _hoverLink = null;
        Cursor = Cursor.Default;
    }
    private void DragScroll()
    {
        if (_pointer is null || _scroll is null || _presenter is null) return;
        var delta = _dragPosition.Y < 0 ? -20 : _dragPosition.Y > Bounds.Height ? 20 : 0;
        if (delta == 0) return;
        _scroll.Offset = new Vector(_scroll.Offset.X, Math.Max(0, _scroll.Offset.Y + delta));
        UpdateLayout();
        if (this.TranslatePoint(_dragPosition, _presenter) is { } point) SetSelection(_selection.Anchor, _presenter.Hit(point));
    }
    private void StartDragTimer()
    {
        // A timer posts subsequent ticks using the context captured at Start. A drag
        // can outlive its input callback, so its internal ticks need no caller scope.
        if (ExecutionContext.IsFlowSuppressed()) Start();
        else { using (ExecutionContext.SuppressFlow()) Start(); }

        void Start()
        {
            _dragTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Input, (_, _) => DragScroll());
            _dragTimer.Start();
        }
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        _readingRevision++;
        base.OnPointerWheelChanged(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.Key is Key.Enter or Key.Space && (e.Source is Button || e.Source is Visual source && source.GetVisualAncestors().Any(parent => parent is Button))) return;
        base.OnKeyDown(e);
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (command && e.Key == Key.A) { SelectAll(); e.Handled = true; return; }
        if (command && e.Key == Key.C) { Copy(); e.Handled = true; return; }
        if (e.Key == Key.Escape) { ClearSelection(); e.Handled = true; return; }
        if (e.Key == Key.Enter) { ActivateLink(_linkFocusPosition >= 0 ? _linkFocusPosition : _selection.Active); e.Handled = true; return; }
        if (e.Key == Key.Tab)
        {
            _readingRevision++;
            var links = Document.Descendants().SelectMany(block => Flatten(block.Inlines)).Where(inline => inline.SemanticKind is MarkdownInlineKind.Link or MarkdownInlineKind.FootnoteReference).OrderBy(inline => inline.TextStart).ToArray();
            var position = _linkFocusPosition >= 0 ? _linkFocusPosition : _selection.Active;
            var next = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? links.LastOrDefault(link => _linkFocusPosition >= 0 ? link.TextStart < position : link.TextStart <= position) : links.FirstOrDefault(link => _linkFocusPosition >= 0 ? link.TextStart > position : link.TextStart >= position);
            if (next is not null) { _linkFocusPosition = next.TextStart; SetSelection(next.TextStart, next.TextStart); BringPositionIntoView(next.TextStart); e.Handled = true; }
            return;
        }
        if (!SelectionEnabled) return;
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
        { _readingRevision++; ResetReadingLayoutTracking(); }
        var active = _selection.Active;
        var navigationDocument = Document;
        var navigationPresenter = _presenter;
        var navigationGeneration = _updates.DocumentGeneration;
        var navigationRevision = _readingRevision;
        switch (e.Key)
        {
            case Key.Left: active = !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !_selection.IsEmpty ? _selection.Start : MarkdownTextBoundary.Previous(Document.Text, active); break;
            case Key.Right: active = !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !_selection.IsEmpty ? _selection.End : MarkdownTextBoundary.Next(Document.Text, active); break;
            case Key.Home: active = command ? 0 : Document.Text.LastIndexOf('\n', Math.Max(0, active - 1), Math.Min(active, Document.Text.Length)) + 1; break;
            case Key.End: var end = Document.Text.IndexOf('\n', active); active = command || end < 0 ? Document.Text.Length : end; break;
            case Key.PageUp: case Key.PageDown:
                if (_presenter?.Position(active) is { } pagePosition && _scroll is { } scroll)
                {
                    var direction = e.Key == Key.PageUp ? -1 : 1;
                    var distance = scroll.Viewport.Height;
                    var target = new Point(pagePosition.X, Math.Max(0, pagePosition.Y + direction * distance));
                    var revision = _readingRevision;
                    var restoring = _restoring;
                    _restoring = true;
                    try { scroll.Offset = new Vector(scroll.Offset.X, Math.Max(0, scroll.Offset.Y + direction * distance)); }
                    finally { _restoring = restoring; }
                    // Hit tests only native helpers that currently exist. Realize the
                    // destination page before asking its shaped text for the new caret.
                    var trackingRestore = _readingLayoutRestoring;
                    _readingLayoutRestoring = true;
                    try
                    {
                        _presenter.SetViewport(scroll.Offset.Y, distance);
                        UpdateLayout();
                        if (!ReferenceEquals(navigationDocument, Document) || navigationGeneration != _updates.DocumentGeneration ||
                            !ReferenceEquals(navigationPresenter, _presenter) || !ReferenceEquals(scroll, _scroll) || revision != _readingRevision)
                        { e.Handled = true; return; }
                        active = _presenter.Hit(target);
                    }
                    finally { _readingLayoutRestoring = trackingRestore; }
                }
                break;
            case Key.Up: case Key.Down:
                if (_presenter?.Position(active) is { } p)
                {
                    var direction = e.Key == Key.Up ? -1 : 1;
                    active = _presenter.Hit(new Point(p.X, p.Y + direction * FontSize * 1.5));
                }
                break;
            default: return;
        }
        _linkFocusPosition = -1;
        SetSelection(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? _selection.Anchor : active, active);
        if (ReferenceEquals(navigationDocument, Document) && navigationGeneration == _updates.DocumentGeneration &&
            ReferenceEquals(navigationPresenter, _presenter) && navigationRevision == _readingRevision) BringPositionIntoView(active);
        e.Handled = true;
    }
    private void ActivateLink(int position)
    {
        var inline = LinkAt(Document, position);
        if (inline is null) return;
        if (inline.SemanticKind == MarkdownInlineKind.FootnoteReference)
        {
            var footnote = Document.Descendants().FirstOrDefault(block => block.SemanticKind == MarkdownBlockKind.Footnote && block.Metadata.GetValueOrDefault("label") == inline.Metadata.GetValueOrDefault("label"));
            if (footnote is not null) { SetSelection(footnote.TextStart, footnote.TextStart); BringPositionIntoView(footnote.TextStart); }
        }
        else
        {
            var url = inline.Metadata.GetValueOrDefault("url", "");
            var destination = ResolveUri(url)?.AbsoluteUri ?? url;
            var document = Document;
            var generation = _updates.DocumentGeneration;
            var attached = _attached;
            var args = new MarkdownLinkEventArgs(destination, inline.Metadata.GetValueOrDefault("title"));
            LinkClicked?.Invoke(this, args);
            // The host always owns external navigation. Its handled flag suppresses
            // only our default visited state, and cannot clear an earlier visit.
            // A replacement invalidates its generation before the next snapshot
            // commits; detach/reconfigure also invalidate an in-flight activation.
            if (args.Handled || generation != _updates.DocumentGeneration ||
                !ReferenceEquals(document, Document) || attached != _attached) return;
            _visitedLinks.Add(destination);
            foreach (var span in LinkSpans())
                if (IsLinkVisited(span.Url)) span.Classes.Add("visited"); else span.Classes.Remove("visited");
        }
    }
    private static MarkdownInline? LinkAt(MarkdownDocumentSnapshot document, int position) => document.Descendants().SelectMany(block => Flatten(block.Inlines)).FirstOrDefault(inline => inline.SemanticKind is MarkdownInlineKind.Link or MarkdownInlineKind.FootnoteReference && position >= inline.TextStart && position < inline.TextStart + inline.TextLength);
    private void ResetLinkPresentation()
    {
        _linkFocusPosition = -1;
        _hoverLink?.Classes.Remove("pointerover"); _hoverLink = null;
        Cursor = Cursor.Default;
    }
    private IEnumerable<MarkdownSpan> LinkSpans() => this.GetVisualDescendants().OfType<MarkdownTextPresenter>().SelectMany(text => text.GetLogicalDescendants().OfType<MarkdownSpan>()).Where(span => span.Classes.Contains("link"));
    private void BringPositionIntoView(int position)
    {
        if (_scroll is null || _presenter is null) return;
        if (ReadingPositionBounds(position) is { } bounds)
        {
            if (bounds.Top < _scroll.Offset.Y) _scroll.Offset = new Vector(_scroll.Offset.X, Math.Max(0, bounds.Top));
            else if (bounds.Bottom > _scroll.Offset.Y + _scroll.Viewport.Height) _scroll.Offset = new Vector(_scroll.Offset.X, Math.Max(0, bounds.Bottom - _scroll.Viewport.Height));
        }
        else if (_presenter.Position(position) is { } point)
        {
            if (point.Y < _scroll.Offset.Y) _scroll.Offset = new Vector(_scroll.Offset.X, Math.Max(0, point.Y));
            else if (point.Y > _scroll.Offset.Y + _scroll.Viewport.Height) _scroll.Offset = new Vector(_scroll.Offset.X, Math.Max(0, point.Y - _scroll.Viewport.Height + FontSize * 2));
        }
        else
        {
            var block = Document.Blocks.FirstOrDefault(block => position >= block.TextStart && position <= block.TextStart + block.TextLength);
            if (block is not null && _presenter.AnchorOffset(block.Id, 0) is { } offset) _scroll.Offset = new Vector(_scroll.Offset.X, offset);
        }
    }
    private static IEnumerable<MarkdownInline> Flatten(IEnumerable<MarkdownInline> inlines) { foreach (var inline in inlines) { yield return inline; foreach (var child in Flatten(inline.Children)) yield return child; } }
    protected override AutomationPeer OnCreateAutomationPeer() => new MarkdownAutomationPeer(this);
}
