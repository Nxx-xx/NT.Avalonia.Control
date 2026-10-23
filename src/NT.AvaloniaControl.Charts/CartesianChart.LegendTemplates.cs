using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Runtime.ExceptionServices;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<IDataTemplate?> LegendItemTemplateProperty =
        AvaloniaProperty.Register<CartesianChart, IDataTemplate?>(nameof(LegendItemTemplate));
    public static readonly StyledProperty<bool> IsLegendScrollableProperty =
        AvaloniaProperty.Register<CartesianChart, bool>(nameof(IsLegendScrollable));
    public static readonly StyledProperty<double?> LegendMaxWidthProperty =
        AvaloniaProperty.Register<CartesianChart, double?>(nameof(LegendMaxWidth),
            validate: value => value is null || double.IsFinite(value.Value) && value.Value > 0);
    public static readonly StyledProperty<double?> LegendMaxHeightProperty =
        AvaloniaProperty.Register<CartesianChart, double?>(nameof(LegendMaxHeight),
            validate: value => value is null || double.IsFinite(value.Value) && value.Value > 0);

    /// <summary>
    /// Passive legend content with a ChartLegendItem DataContext. Takes precedence
    /// over LegendRenderer. A null Build result hides the item; errors propagate.
    /// Returned controls must not already have a logical or visual parent.
    /// </summary>
    public IDataTemplate? LegendItemTemplate
    { get => GetValue(LegendItemTemplateProperty); set => SetValue(LegendItemTemplateProperty, value); }
    /// <summary>Enable vertical scrolling inside the bounded legend panel.</summary>
    public bool IsLegendScrollable
    { get => GetValue(IsLegendScrollableProperty); set => SetValue(IsLegendScrollableProperty, value); }
    /// <summary>Optional legend width limit in DIPs, also constrained by the available dock or plot.</summary>
    public double? LegendMaxWidth
    { get => GetValue(LegendMaxWidthProperty); set => SetValue(LegendMaxWidthProperty, value); }
    /// <summary>Optional legend height limit in DIPs, also constrained by the available dock or plot.</summary>
    public double? LegendMaxHeight
    { get => GetValue(LegendMaxHeightProperty); set => SetValue(LegendMaxHeightProperty, value); }

    // None of these controls or collections is created on the default drawing path.
    private LegendPresentationHost? _legendPresentationHost;
    private Border? _legendPresentationBorder;
    private ScrollViewer? _legendPresentationScroll;
    private LegendItemsPanel? _legendPresentationPanel;
    private Dictionary<ChartSeries, LegendPresentationEntry>? _legendPresentationEntries;
    private IReadOnlyList<ChartLegendItem> _visiblePresentedLegendItems = Array.Empty<ChartLegendItem>();
    private Rect _legendPresentationViewport;
    private Size _legendPresentationSize;
    private bool _explicitLegendPresentation;
    private bool _usesLegendPresentation;
    private bool _legendPresentationDirty = true;
    private bool _synchronizingLegendPresentation;
    private bool _clearingLegendPresentation;
    private bool _arrangingLegendPresentation;
    private bool _failedLegendPresentation;
    private bool _queuedLegendPresentationLayout;
    private IDataTemplate? _failedLegendTemplate;
    private long _legendPresentationRevision;

    private bool UsesLegendPresentation => _usesLegendPresentation;
    private bool IsSynchronizingLegendPresentation => _synchronizingLegendPresentation;
    private bool LegendPresentationNeedsLayout => _legendPresentationDirty || _scene is null;

    private bool LegendPresentationPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != LegendItemTemplateProperty && change.Property != IsLegendScrollableProperty &&
            change.Property != LegendMaxWidthProperty && change.Property != LegendMaxHeightProperty) return false;
        _explicitLegendPresentation = LegendItemTemplate is not null || IsLegendScrollable || LegendMaxWidth is not null || LegendMaxHeight is not null;
        _failedLegendPresentation = false;
        _failedLegendTemplate = null;
        InvalidateScene();
        InvalidateMeasure();
        RequestRender();
        return true;
    }

    private void InvalidateLegendPresentation()
    {
        var previouslyPresented = UsesLegendPresentation;
        // A parent Render always precedes its visual children. Move the existing
        // drawn floating legend into this same presentation host when real label
        // controls are present, so it keeps its established foreground position.
        _usesLegendPresentation = _explicitLegendPresentation || IsLegendFloating && (HasLabelTemplates() || HasPlotAreaContentPresentation);
        if (!previouslyPresented && !UsesLegendPresentation && _legendPresentationHost is null) return;
        _legendPresentationRevision++;
        _legendPresentationDirty = true;
        _visiblePresentedLegendItems = Array.Empty<ChartLegendItem>();
        if (!UsesLegendPresentation || !ShowLegend || !IsVisible) ReleaseLegendPresentationVisual();
        if (!_synchronizingLegendPresentation) InvalidateMeasure();
    }

    private void EnsureLegendPresentationForScene()
    {
        if (!UsesLegendPresentation || _synchronizingLegendPresentation || _clearingLegendPresentation) return;
        if (_rendering) { QueueLegendPresentationLayout(); return; }
        if (_failedLegendPresentation && ReferenceEquals(_failedLegendTemplate, LegendItemTemplate)) return;
        if (!_legendPresentationDirty) return;
        var size = _legendPresentationSize;
        if (Bounds.Width > 0 && Bounds.Height > 0) size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) size = new Size(520, 300);
        SynchronizeLegendPresentation(size);
    }

    private void QueueLegendPresentationLayout()
    {
        if (_queuedLegendPresentationLayout || !_attached || !IsVisible || !UsesLegendPresentation || !LegendPresentationNeedsLayout) return;
        _queuedLegendPresentationLayout = true;
        var revision = _legendPresentationRevision;
        Dispatcher.UIThread.Post(() =>
        {
            _queuedLegendPresentationLayout = false;
            if (!_attached || !IsVisible || !UsesLegendPresentation || !LegendPresentationNeedsLayout) return;
            // Never revive the presentation from an earlier attachment/configuration.
            // A later revision may request its own layout, using only current state.
            if (revision != _legendPresentationRevision || _rendering)
            { QueueLegendPresentationLayout(); return; }
            InvalidateMeasure();
            InvalidateArrange();
            RequestRender();
        }, DispatcherPriority.Loaded);
    }

    private void SynchronizeLegendPresentation(Size size)
    {
        if (_synchronizingLegendPresentation || _clearingLegendPresentation) return;
        if (!UsesLegendPresentation || !ShowLegend || !IsVisible)
        { ReleaseLegendPresentationVisual(); _legendPresentationDirty = false; return; }
        _synchronizingLegendPresentation = true;
        var revision = _legendPresentationRevision;
        var template = LegendItemTemplate;
        bool Current() => revision == _legendPresentationRevision && UsesLegendPresentation && ShowLegend && IsVisible &&
            ReferenceEquals(template, LegendItemTemplate) && !_clearingLegendPresentation;
        try
        {
            EnsureLegendPresentationHost();
            var panel = _legendPresentationPanel!;
            var entries = _legendPresentationEntries!;
            var retained = new HashSet<ChartSeries>();
            var ordered = new List<Border>();
            var seriesIndex = 0;
            foreach (var series in ActiveSeries.ToArray())
            {
                seriesIndex++;
                if (!series.ShowInLegend) continue;
                retained.Add(series);
                var context = new ChartLegendItem(series, series.Title ?? $"系列 {seriesIndex}",
                    GetSeriesBrush(series), series.Renderer.LegendSymbol, default);
                if (entries.TryGetValue(series, out var entry) && !ReferenceEquals(entry.Template, template))
                { ReleaseLegendPresentationEntry(series, entry); entry = null; }
                if (entry is null)
                {
                    var child = template is null ? new DrawnLegendItem(this) : template.Build(context);
                    if (!Current()) return;
                    if (child is not null && (((ILogical)child).LogicalParent is not null || child.GetVisualParent() is not null))
                        throw new InvalidOperationException("A legend template must return an unparented control.");
                    var border = child is null ? null : new Border
                    { IsHitTestVisible = false, Focusable = false, ClipToBounds = true };
                    entry = new LegendPresentationEntry(template, border);
                    entries.Add(series, entry);
                    if (border is not null)
                    {
                        // Complete Child before setting inherited context or attaching;
                        // application callbacks may invalidate the entire presentation.
                        border.Child = child;
                        if (!Current()) { DiscardLegendBorder(border); return; }
                        KeyboardNavigation.SetTabNavigation(border, KeyboardNavigationMode.None);
                    }
                }
                entry.Context = context;
                if (entry.Border is not { } container) continue;
                container.DataContext = context;
                if (!Current()) return;
                ApplyLegendTextStyle(container);
                ordered.Add(container);
            }
            foreach (var pair in entries.ToArray())
                if (!retained.Contains(pair.Key)) ReleaseLegendPresentationEntry(pair.Key, pair.Value);
            if (!Current()) return;
            // Preserve controls and their resource inheritance across data revisions.
            if (!panel.Children.SequenceEqual(ordered))
            {
                panel.Children.Clear();
                if (!Current()) return;
                foreach (var container in ordered)
                {
                    panel.Children.Add(container);
                    if (!Current()) return;
                }
            }
            UpdateLegendPresentationAppearance();
            EnsureLegendPresentationMembership();
            if (!Current()) return;
            var width = Math.Max(1, size.Width - Math.Max(8, Padding.Left) - Math.Max(8, Padding.Right));
            if (LegendPosition is ChartLegendPosition.Left or ChartLegendPosition.Right) width = Math.Min(width * .35, 220);
            MeasurePresentedLegend(width);
            if (Current()) _legendPresentationDirty = false;
        }
        catch
        {
            var failed = Current();
            try { ReleaseLegendPresentationVisual(); }
            finally
            {
                if (failed)
                {
                    // Do not rerun a failing application Build on every measure or
                    // paint. Replacing the template/configuration explicitly retries.
                    _failedLegendPresentation = true;
                    _failedLegendTemplate = template;
                    _legendPresentationDirty = false;
                }
            }
            throw;
        }
        finally
        {
            _synchronizingLegendPresentation = false;
            if (revision != _legendPresentationRevision)
            {
                ReleaseLegendPresentationVisual();
                _legendPresentationDirty = true;
                InvalidateMeasure();
            }
        }
    }

    private void EnsureLegendPresentationHost()
    {
        if (_legendPresentationHost is not null) return;
        var panel = new LegendItemsPanel { Focusable = false, IsHitTestVisible = false };
        KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.None);
        var scroll = new ScrollViewer
        {
            Name = "PART_ChartLegendScrollViewer", Content = panel, Focusable = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        KeyboardNavigation.SetTabNavigation(scroll, KeyboardNavigationMode.None);
        var border = new Border { Child = scroll, ClipToBounds = true, Focusable = false };
        var host = new LegendPresentationHost
        { Name = "PART_ChartLegend", ClipToBounds = true, Focusable = false, ZIndex = 25 };
        KeyboardNavigation.SetTabNavigation(host, KeyboardNavigationMode.None);
        host.Children.Add(border);
        _legendPresentationHost = host;
        _legendPresentationBorder = border;
        _legendPresentationScroll = scroll;
        _legendPresentationPanel = panel;
        _legendPresentationEntries = [];
        scroll.ScrollChanged += OnLegendPresentationScrollChanged;
    }

    private void ApplyLegendTextStyle(Control container)
    {
        TextElement.SetForeground(container, Foreground ?? Brushes.Black);
        TextElement.SetFontFamily(container, FontFamily);
        TextElement.SetFontSize(container, FontSize);
        TextElement.SetFontStyle(container, FontStyle);
        TextElement.SetFontWeight(container, FontWeight);
    }

    private void UpdateLegendPresentationAppearance()
    {
        if (_legendPresentationScroll is not { } scroll || _legendPresentationBorder is not { } border) return;
        scroll.VerticalScrollBarVisibility = IsLegendScrollable ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        border.Background = IsLegendFloating ? FloatingLegendBackground ?? Background ?? Brushes.Transparent : Brushes.Transparent;
        ApplyLegendTextStyle(border);
    }

    private Size MeasurePresentedLegend(double maxWidth)
    {
        if (_legendPresentationPanel is not { } panel) return default;
        maxWidth = Math.Max(0, Math.Min(maxWidth, LegendMaxWidth ?? maxWidth));
        if (maxWidth <= 0) return default;
        panel.SetLayout(maxWidth, LegendPosition is ChartLegendPosition.Left or ChartLegendPosition.Right || LegendOrientation == Orientation.Vertical);
        panel.Measure(new Size(maxWidth, double.PositiveInfinity));
        return panel.DesiredSize;
    }

    private IReadOnlyList<ChartLegendItem> LayoutPresentedLegend(ref double left, ref double right, ref double top, ref double bottom)
    {
        var side = LegendPosition is ChartLegendPosition.Left or ChartLegendPosition.Right;
        var availableWidth = Math.Max(0, right - left);
        var natural = MeasurePresentedLegend(side ? Math.Min(availableWidth * .35, 220) : availableWidth);
        var width = Math.Min(availableWidth, natural.Width);
        var height = Math.Min(natural.Height, Math.Min(LegendMaxHeight ?? double.PositiveInfinity,
            Math.Max(0, bottom - top - (side ? 0 : 120))));
        if (width <= 0 || height <= 0) { SetPresentedLegendViewport(default); return Array.Empty<ChartLegendItem>(); }
        var x = LegendPosition == ChartLegendPosition.Right ? right - width : left;
        var y = LegendPosition == ChartLegendPosition.Bottom ? bottom - height : top;
        if (side) y += VerticalLegendOffset(bottom - top - height);
        else x += HorizontalLegendOffset(availableWidth - width);
        SetPresentedLegendViewport(new Rect(x, y, width, height));
        switch (LegendPosition)
        {
            case ChartLegendPosition.Top: top += height + 5; break;
            case ChartLegendPosition.Bottom: bottom -= height + 5; break;
            case ChartLegendPosition.Left: left += width + 8; break;
            case ChartLegendPosition.Right: right -= width + 8; break;
        }
        return VisiblePresentedLegendItems();
    }

    private IReadOnlyList<ChartLegendItem> LayoutFloatingPresentedLegend(Rect plot)
    {
        var margin = FloatingLegendMargin;
        var left = Math.Min(margin.Left, plot.Width); var top = Math.Min(margin.Top, plot.Height);
        var right = Math.Min(margin.Right, plot.Width - left); var bottom = Math.Min(margin.Bottom, plot.Height - top);
        var available = new Rect(plot.Left + left, plot.Top + top, plot.Width - left - right, plot.Height - top - bottom);
        var side = LegendPosition is ChartLegendPosition.Left or ChartLegendPosition.Right;
        var natural = MeasurePresentedLegend(side ? Math.Min(available.Width * .35, 220) : available.Width);
        var width = Math.Min(available.Width, natural.Width);
        var height = Math.Min(available.Height, Math.Min(natural.Height, LegendMaxHeight ?? double.PositiveInfinity));
        if (width <= 0 || height <= 0) { SetPresentedLegendViewport(default); return Array.Empty<ChartLegendItem>(); }
        var x = LegendPosition == ChartLegendPosition.Right ? available.Right - width : available.Left;
        var y = LegendPosition == ChartLegendPosition.Bottom ? available.Bottom - height : available.Top;
        if (side) y += VerticalLegendOffset(available.Height - height);
        else x += HorizontalLegendOffset(available.Width - width);
        SetPresentedLegendViewport(new Rect(x, y, width, height));
        return VisiblePresentedLegendItems();
    }

    private void SetPresentedLegendViewport(Rect viewport)
    {
        if (_legendPresentationViewport == viewport) return;
        _legendPresentationViewport = viewport;
        _legendPresentationHost?.SetViewport(viewport);
    }

    private IReadOnlyList<ChartLegendItem> VisiblePresentedLegendItems()
    {
        RefreshPresentedLegendBounds();
        return _visiblePresentedLegendItems;
    }

    private void RefreshPresentedLegendBounds()
    {
        if (_synchronizingLegendPresentation || _clearingLegendPresentation || _legendPresentationPanel is not { } panel ||
            _legendPresentationScroll is not { } scroll || _legendPresentationEntries is null ||
            _legendPresentationViewport.Width <= 0 || _legendPresentationViewport.Height <= 0)
        { _visiblePresentedLegendItems = Array.Empty<ChartLegendItem>(); return; }
        var clip = _legendPresentationViewport;
        if (scroll.Viewport.Width > 0 && scroll.Viewport.Height > 0)
            clip = clip.Intersect(new Rect(clip.TopLeft, new Size(Math.Min(clip.Width, scroll.Viewport.Width), Math.Min(clip.Height, scroll.Viewport.Height))));
        var transform = panel.Bounds.Width > 0 ? panel.TransformToVisual(this) : null;
        var result = new List<ChartLegendItem>();
        foreach (var child in panel.Children)
        {
            if (child.DataContext is not ChartLegendItem context || !panel.Slots.TryGetValue(child, out var local)) continue;
            var bounds = transform is { } matrix ? local.TransformToAABB(matrix) : local.Translate(
                new Vector(clip.X - scroll.Offset.X, clip.Y - scroll.Offset.Y));
            bounds = bounds.Intersect(clip);
            if (bounds.Width <= 0 || bounds.Height <= 0) continue;
            result.Add(context with { Bounds = bounds });
        }
        _visiblePresentedLegendItems = result.ToArray();
    }

    private void PublishPresentedLegendContexts()
    {
        if (_legendPresentationEntries is not { } entries || _synchronizingLegendPresentation || _clearingLegendPresentation) return;
        _synchronizingLegendPresentation = true;
        var revision = _legendPresentationRevision;
        try
        {
            foreach (var entry in entries.Values.ToArray())
            {
                if (entry.Border is not { } border || entry.Context is not { } context) continue;
                var visible = _visiblePresentedLegendItems.FirstOrDefault(item => ReferenceEquals(item.Series, context.Series));
                var updated = context with { Bounds = visible?.Bounds ?? default };
                if (!Equals(border.DataContext, updated)) border.DataContext = updated;
                if (revision != _legendPresentationRevision) return;
            }
        }
        finally
        {
            _synchronizingLegendPresentation = false;
            if (revision != _legendPresentationRevision)
            {
                ReleaseLegendPresentationVisual();
                _legendPresentationDirty = true;
                InvalidateMeasure();
            }
        }
    }

    private void OnLegendPresentationScrollChanged(object? sender, ScrollChangedEventArgs args)
    {
        if (!ReferenceEquals(sender, _legendPresentationScroll) || _clearingLegendPresentation || _arrangingLegendPresentation) return;
        RefreshPresentedLegendBounds();
        PublishPresentedLegendContexts();
        ClearHover();
        RequestRender();
    }

    private bool HandleLegendPresentationWheel(PointerWheelEventArgs args)
    {
        if (!UsesLegendPresentation) return false;
        var scene = GetScene();
        if (scene is null || !LegendPanelContains(scene, args.GetPosition(this))) return false;
        if (!args.Handled && IsLegendScrollable && _legendPresentationScroll is { } scroll && args.Delta.Y != 0)
            scroll.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, Math.Max(0, scroll.Offset.Y - args.Delta.Y * 48)));
        args.Handled = true;
        ClearHover();
        return true;
    }

    private bool HandleLegendPresentationPressed(PointerPressedEventArgs args)
    {
        if (!UsesLegendPresentation) return false;
        var scene = GetScene();
        var position = args.GetPosition(this);
        if (scene is null || !LegendPanelContains(scene, position)) return false;
        var scrollbar = args.Source is ScrollBar || args.Source is Visual visual && visual.GetVisualAncestors().Any(parent => parent is ScrollBar);
        if (!scrollbar && !args.Handled && args.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            IsLegendInteractive && LegendItemAt(scene, position) is { } item) ToggleSeriesVisibility(item.Series);
        args.Handled = true;
        ClearHover();
        return true;
    }

    private void MeasureLegendPresentation(Size size)
    {
        if (!UsesLegendPresentation) return;
        if (_legendPresentationSize != size)
        { _legendPresentationSize = size; _legendPresentationDirty = true; }
        EnsureLegendPresentationForScene();
    }

    private void ArrangeLegendPresentation(Size size)
    {
        if (!UsesLegendPresentation || _arrangingLegendPresentation) return;
        MeasureLegendPresentation(size);
        if (GetScene() is null || _legendPresentationHost is not { } host) return;
        _arrangingLegendPresentation = true;
        try
        {
            EnsureLegendPresentationMembership();
            UpdateLegendPresentationAppearance();
            host.Measure(size);
            host.Arrange(new Rect(size));
            RefreshPresentedLegendBounds();
            PublishPresentedLegendContexts();
        }
        finally { _arrangingLegendPresentation = false; }
    }

    private void LegendPresentationAttached() { if (UsesLegendPresentation) { _legendPresentationDirty = true; InvalidateMeasure(); } }
    private void LegendPresentationDetached()
    {
        _legendPresentationRevision++; ReleaseLegendPresentationVisual(); _legendPresentationDirty = true;
        _failedLegendPresentation = false; _failedLegendTemplate = null;
    }
    private void LegendPresentationTemplateApplied() => EnsureLegendPresentationMembership();

    private void EnsureLegendPresentationMembership()
    {
        if (_legendPresentationHost is not { } host) return;
        var logical = ((ILogical)host).LogicalParent;
        var visual = host.GetVisualParent();
        if (logical is not null && !ReferenceEquals(logical, this) || visual is not null && !ReferenceEquals(visual, this))
            throw new InvalidOperationException("The legend presentation already has another parent.");
        if (logical is null) LogicalChildren.Add(host);
        if (!ReferenceEquals(host, _legendPresentationHost)) return;
        if (visual is null) VisualChildren.Add(host);
    }

    private static void DiscardLegendBorder(Border border)
    { try { border.Child = null; } finally { border.DataContext = null; } }

    private void ReleaseLegendPresentationEntry(ChartSeries series, LegendPresentationEntry entry)
    {
        _legendPresentationEntries?.Remove(series);
        if (entry.Border is not { } border) return;
        if (border.GetVisualParent() is Panel parent) parent.Children.Remove(border);
        DiscardLegendBorder(border);
    }

    private void ReleaseLegendPresentationVisual()
    {
        if (_clearingLegendPresentation) return;
        _clearingLegendPresentation = true;
        var host = _legendPresentationHost;
        var entries = _legendPresentationEntries;
        var scroll = _legendPresentationScroll;
        _legendPresentationHost = null; _legendPresentationBorder = null;
        _legendPresentationPanel = null; _legendPresentationScroll = null; _legendPresentationEntries = null;
        _legendPresentationViewport = default;
        _visiblePresentedLegendItems = Array.Empty<ChartLegendItem>();
        ExceptionDispatchInfo? failure = null;
        void Clean(Action action)
        {
            try { action(); }
            catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        }
        try
        {
            if (scroll is not null) scroll.ScrollChanged -= OnLegendPresentationScrollChanged;
            if (entries is not null)
                foreach (var entry in entries.Values.ToArray())
                    if (entry.Border is { } border) Clean(() => DiscardLegendBorder(border));
            if (scroll is not null) Clean(() => scroll.Content = null);
            if (host is not null)
            {
                Clean(() => { if (ReferenceEquals(host.GetVisualParent(), this)) VisualChildren.Remove(host); });
                Clean(() => { if (ReferenceEquals(((ILogical)host).LogicalParent, this)) LogicalChildren.Remove(host); });
                Clean(host.Children.Clear);
            }
        }
        finally { _clearingLegendPresentation = false; }
        failure?.Throw();
    }

    private void PrepareLegendPresentationForExport(Size size)
    { MeasureLegendPresentation(size); ArrangeLegendPresentation(size); }
    private void RenderLegendPresentationForExport(DrawingContext context)
    {
        if (_legendPresentationHost is not { } host || host.Bounds.Width <= 0 || host.Bounds.Height <= 0) return;
        context.DrawRectangle(new VisualBrush(host) { Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, host.Bounds);
    }

    private sealed class LegendPresentationEntry(IDataTemplate? template, Border? border)
    {
        public IDataTemplate? Template { get; } = template;
        public Border? Border { get; } = border;
        public ChartLegendItem? Context { get; set; }
    }

    private sealed class LegendPresentationHost : Panel
    {
        private Rect _viewport;
        public void SetViewport(Rect viewport)
        { if (_viewport != viewport) { _viewport = viewport; InvalidateMeasure(); InvalidateArrange(); } }
        protected override Size MeasureOverride(Size availableSize)
        { if (Children.Count != 0) Children[0].Measure(_viewport.Size); return default; }
        protected override Size ArrangeOverride(Size finalSize)
        { if (Children.Count != 0) Children[0].Arrange(_viewport); return finalSize; }
    }

    private sealed class LegendItemsPanel : Panel
    {
        private double _width;
        private bool _vertical;
        public Dictionary<Control, Rect> Slots { get; } = [];
        public void SetLayout(double width, bool vertical)
        {
            if (_width == width && _vertical == vertical) return;
            _width = width; _vertical = vertical; InvalidateMeasure();
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            Slots.Clear();
            var maxWidth = Math.Max(0, double.IsFinite(availableSize.Width) ? Math.Min(_width, availableSize.Width) : _width);
            var x = 0d; var y = 0d; var rowHeight = 0d; var usedWidth = 0d;
            foreach (var child in Children)
            {
                child.Measure(new Size(maxWidth, double.PositiveInfinity));
                var width = Math.Min(maxWidth, child.DesiredSize.Width);
                var height = child.DesiredSize.Height;
                if (x > 0 && (_vertical || x + width > maxWidth)) { y += rowHeight; x = 0; rowHeight = 0; }
                Slots[child] = new Rect(x, y, width, height);
                usedWidth = Math.Max(usedWidth, x + width); x += width; rowHeight = Math.Max(rowHeight, height);
            }
            return new Size(usedWidth, y + rowHeight);
        }
        protected override Size ArrangeOverride(Size finalSize)
        { foreach (var pair in Slots) pair.Key.Arrange(pair.Value); return finalSize; }
    }

    private sealed class DrawnLegendItem(CartesianChart owner) : Control
    {
        protected override Size MeasureOverride(Size availableSize) => DataContext is ChartLegendItem item
            ? new Size(Math.Min(availableSize.Width, owner.MakeText(item.Text).Width + 40), owner.FontSize + 12) : default;
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (DataContext is not ChartLegendItem item) return;
            var local = item with { Bounds = new Rect(Bounds.Size) };
            using var clip = context.PushClip(local.Bounds);
            using var opacity = context.PushOpacity(item.IsVisible ? 1 : .4);
            if (owner.LegendRenderer is { } renderer)
            {
                // The callback retains its original chart-local coordinate contract
                // even though this presenter is inside a scrolled visual subtree.
                if (this.TransformToVisual(owner) is { } transform && transform.TryInvert(out var inverse))
                {
                    using var chartCoordinates = context.PushTransform(inverse);
                    renderer(context, item with { Bounds = local.Bounds.TransformToAABB(transform).Intersect(owner._legendPresentationViewport) });
                }
                return;
            }
            if (item.Symbol == ChartLegendSymbol.Circle) context.DrawEllipse(item.Brush, null, new Point(6, 7), 4, 4);
            else context.DrawRectangle(item.Brush, null, new Rect(0, 5, 12, item.Symbol == ChartLegendSymbol.Rectangle ? 9 : 4), 2, 2);
            owner.DrawLabel(context, item.Text, new Point(20, 0), Math.Max(0, Bounds.Width - 22));
            if (!item.IsVisible) context.DrawLine(new Pen(owner.Foreground ?? Brushes.Gray, 1), new Point(20, 7), new Point(Bounds.Width - 3, 7));
        }
    }
}
