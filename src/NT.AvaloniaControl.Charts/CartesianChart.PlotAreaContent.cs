using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

/// <summary>A snapshot supplied to a plot-area content template. Projection coordinates are DIPs local to the plot content.</summary>
public sealed class ChartPlotAreaContext
{
    private readonly ChartScale _x;
    private readonly ChartScale _y;
    private readonly IReadOnlyDictionary<string, ChartScale> _xAxes;
    private readonly IReadOnlyDictionary<string, ChartScale> _yAxes;

    internal ChartPlotAreaContext(object content, object? dataContext, Rect plotArea, ChartViewport visibleRange,
        ChartViewport extent, ChartScale x, ChartScale y, IReadOnlyDictionary<string, ChartScale> xAxes,
        IReadOnlyDictionary<string, ChartScale> yAxes)
    {
        Content = content; DataContext = dataContext; PlotArea = plotArea; VisibleRange = visibleRange;
        Extent = extent; _x = x; _y = y; _xAxes = xAxes; _yAxes = yAxes;
        Projection = new PlotContentProjection(new Rect(plotArea.Size), x, y);
    }

    public object Content { get; }
    /// <summary>The chart's inherited application context at the time of this snapshot.</summary>
    public object? DataContext { get; }
    /// <summary>Effective plot bounds in chart-local DIPs. The content itself starts at (0, 0).</summary>
    public Rect PlotArea { get; }
    public ChartViewport VisibleRange { get; }
    public ChartViewport Extent { get; }
    /// <summary>Primary-axis projection into plot-local DIPs, including inverse, logarithmic and break transforms.</summary>
    public IChartProjection Projection { get; }
    public Point Project(double x, double y) => Projection.Project(new ChartPoint(x, y));

    /// <summary>Resolves only uniquely named axes participating in this scene and dimension. Null/empty keys use the primary axis.</summary>
    public bool TryGetProjection(string? xAxisKey, string? yAxisKey, out IChartProjection projection)
    {
        if (TryGetScales(xAxisKey, yAxisKey, out var x, out var y))
        { projection = new PlotContentProjection(new Rect(PlotArea.Size), x, y); return true; }
        projection = null!;
        return false;
    }

    public bool TryGetVisibleRange(string? xAxisKey, string? yAxisKey, out ChartViewport range)
    {
        if (TryGetScales(xAxisKey, yAxisKey, out var x, out var y))
        { range = new(x.Minimum, x.Maximum, y.Minimum, y.Maximum); return true; }
        range = default;
        return false;
    }

    private bool TryGetScales(string? xKey, string? yKey, out ChartScale x, out ChartScale y)
    {
        x = _x; y = _y;
        return (string.IsNullOrWhiteSpace(xKey) || _xAxes.TryGetValue(xKey, out x!)) &&
            (string.IsNullOrWhiteSpace(yKey) || _yAxes.TryGetValue(yKey, out y!));
    }

    private sealed class PlotContentProjection(Rect plot, ChartScale x, ChartScale y) : IChartProjection
    {
        public Rect PlotArea => plot;
        public double MapX(double value) => x.IsValidValue(value) ? x.Map(value, plot.Left, plot.Right) : double.NaN;
        public double MapY(double value) => y.IsValidValue(value) ? y.Map(value, plot.Bottom, plot.Top) : double.NaN;
        public double YOrigin => y.Map(y.IsValidValue(0) ? 0 : y.Minimum, plot.Bottom, plot.Top);
        public Point Project(ChartPoint point) => new(Limit(MapX(point.X)), Limit(MapY(point.Y)));
        public ChartPoint Unproject(Point point) => new(x.Denormalize((point.X - plot.Left) / plot.Width),
            y.Denormalize((plot.Bottom - point.Y) / plot.Height));
        private static double Limit(double value) => double.IsFinite(value) ? Math.Clamp(value, -1_000_000, 1_000_000) : value;
    }
}

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<object?> PlotAreaContentProperty =
        AvaloniaProperty.Register<CartesianChart, object?>(nameof(PlotAreaContent));
    public static readonly StyledProperty<IDataTemplate?> PlotAreaContentTemplateProperty =
        AvaloniaProperty.Register<CartesianChart, IDataTemplate?>(nameof(PlotAreaContentTemplate));

    /// <summary>Passive content arranged and clipped within PlotArea, above drawn chart elements and below control-based labels/readings.
    /// A Control without a template inherits the chart DataContext. Other objects require PlotAreaContentTemplate.</summary>
    public object? PlotAreaContent { get => GetValue(PlotAreaContentProperty); set => SetValue(PlotAreaContentProperty, value); }
    /// <summary>Builds a control with ChartPlotAreaContext as its inherited DataContext. The control is reused across view/layout changes.
    /// Null content or a null Build result produces no overlay. Application template errors propagate after cleanup.</summary>
    public IDataTemplate? PlotAreaContentTemplate { get => GetValue(PlotAreaContentTemplateProperty); set => SetValue(PlotAreaContentTemplateProperty, value); }

    private PlotAreaPresentationHost? _plotAreaContentHost;
    private Border? _plotAreaContentBorder;
    private Control? _plotAreaContentChild;
    private object? _presentedPlotAreaContent;
    private IDataTemplate? _presentedPlotAreaTemplate;
    private ChartScene? _plotAreaContentScene;
    private object? _failedPlotAreaContent;
    private IDataTemplate? _failedPlotAreaTemplate;
    private bool _plotAreaContentFailed;
    private bool _synchronizingPlotAreaContent;
    private bool _clearingPlotAreaContent;
    private bool _queuedPlotAreaContentLayout;
    private long _plotAreaContentRevision;
    private readonly List<Visual> _plotAreaContentAncestors = [];

    private bool HasPlotAreaContentPresentation => PlotAreaContent is Control ||
        PlotAreaContent is not null && PlotAreaContentTemplate is not null;

    private bool CanPresentPlotAreaContent() => !_clearingPlotAreaContent && (_attached || _exportInProgress) &&
        IsVisible && IsEffectivelyVisible && PlotAreaContent is not null;

    private bool PlotAreaContentPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == PlotAreaContentProperty || change.Property == PlotAreaContentTemplateProperty)
        {
            _plotAreaContentRevision++;
            _plotAreaContentFailed = false; _failedPlotAreaContent = null; _failedPlotAreaTemplate = null;
            ReleasePlotAreaContentVisual();
            // Floating legend and drawn-tooltip layers must move above this
            // control layer, or return to the drawing path when it is removed.
            InvalidateScene();
            InvalidateMeasure(); InvalidateArrange(); RequestRender();
            return true;
        }
        if (change.Property == DataContextProperty)
        {
            InvalidatePlotAreaContent();
            return false;
        }
        if (change.Property == IsVisibleProperty && !IsVisible) ClearPlotAreaContent();
        return false;
    }

    // Scene invalidation only marks layout dirty. Never invoke application templates during Render or scene publication.
    private void InvalidatePlotAreaContent()
    {
        _plotAreaContentRevision++;
        _plotAreaContentScene = null;
        if (_plotAreaContentHost is { } host) host.IsVisible = false;
        if (_plotAreaContentHost is not null || PlotAreaContent is not null)
        {
            InvalidateMeasure(); InvalidateArrange();
            QueuePlotAreaContentLayout();
        }
    }

    private void QueuePlotAreaContentLayout()
    {
        if (_queuedPlotAreaContentLayout || !CanPresentPlotAreaContent()) return;
        _queuedPlotAreaContentLayout = true;
        Dispatcher.UIThread.Post(() =>
        {
            _queuedPlotAreaContentLayout = false;
            if (!CanPresentPlotAreaContent()) return;
            InvalidateMeasure(); InvalidateArrange(); RequestRender();
        }, DispatcherPriority.Loaded);
    }

    private void MeasurePlotAreaContent(Size size)
    {
        if (!CanPresentPlotAreaContent()) { ClearPlotAreaContent(); return; }
        if (_synchronizingPlotAreaContent || _clearingPlotAreaContent || _rendering) return;
        var content = PlotAreaContent!;
        var template = PlotAreaContentTemplate;
        if (template is null && content is not Control) { ReleasePlotAreaContentVisual(); return; }
        if (_plotAreaContentFailed && ReferenceEquals(content, _failedPlotAreaContent) && ReferenceEquals(template, _failedPlotAreaTemplate)) return;
        var revision = _plotAreaContentRevision;
        bool Current() => revision == _plotAreaContentRevision && CanPresentPlotAreaContent() &&
            ReferenceEquals(content, PlotAreaContent) && ReferenceEquals(template, PlotAreaContentTemplate);
        _synchronizingPlotAreaContent = true;
        Border? localBorder = null;
        Control? localChild = null;
        try
        {
            var scene = GetScene();
            if (!Current()) return;
            if (scene is null || scene.Plot.Width <= 0 || scene.Plot.Height <= 0) { ReleasePlotAreaContentVisual(); return; }
            var samePresentation = ReferenceEquals(content, _presentedPlotAreaContent) && ReferenceEquals(template, _presentedPlotAreaTemplate);
            var context = template is null ? null : CreatePlotAreaContext(content, scene);
            if (!Current()) return;
            if (!samePresentation)
            {
                localChild = template is null ? (Control)content : template.Build(context);
                if (!Current()) return;
                if (localChild is null)
                {
                    ReleasePlotAreaContentVisual();
                    if (Current()) { _presentedPlotAreaContent = content; _presentedPlotAreaTemplate = template; _plotAreaContentScene = scene; }
                    return;
                }
                if (((ILogical)localChild).LogicalParent is not null || localChild.GetVisualParent() is not null)
                    throw new InvalidOperationException("Plot area content must be an unparented control.");
                var host = new PlotAreaPresentationHost
                {
                    Name = "PART_ChartPlotAreaContentOverlay", IsHitTestVisible = false, Focusable = false,
                    ClipToBounds = true, UseLayoutRounding = false, ZIndex = 10,
                };
                KeyboardNavigation.SetTabNavigation(host, KeyboardNavigationMode.None);
                localBorder = new Border { ClipToBounds = true, UseLayoutRounding = false, IsHitTestVisible = false, Focusable = false };
                host.Children.Add(localBorder);
                _plotAreaContentHost = host; _plotAreaContentBorder = localBorder; _plotAreaContentChild = localChild;
                _presentedPlotAreaContent = content; _presentedPlotAreaTemplate = template;
                localBorder.Child = localChild;
                if (!Current()) { DiscardObsoletePlotAreaContent(localBorder, localChild); return; }
            }
            localBorder = _plotAreaContentBorder;
            localChild = _plotAreaContentChild;
            if (localBorder is null || localChild is null || _plotAreaContentHost is not { } existingHost) return;
            if (template is not null && !ReferenceEquals(scene, _plotAreaContentScene)) localBorder.DataContext = context;
            if (!Current()) { DiscardObsoletePlotAreaContent(localBorder, localChild); return; }
            EnsurePlotAreaContentMembership();
            if (!Current()) { DiscardObsoletePlotAreaContent(localBorder, localChild); return; }
            existingHost.SetPlot(scene.Plot);
            existingHost.IsVisible = true;
            _plotAreaContentScene = scene;
            existingHost.Measure(size);
            if (!Current()) DiscardObsoletePlotAreaContent(localBorder, localChild);
        }
        catch
        {
            if (revision == _plotAreaContentRevision)
            {
                ReleasePlotAreaContentVisual();
                _plotAreaContentFailed = true; _failedPlotAreaContent = content; _failedPlotAreaTemplate = template;
            }
            throw;
        }
        finally
        {
            _synchronizingPlotAreaContent = false;
            if (revision != _plotAreaContentRevision) QueuePlotAreaContentLayout();
        }
    }

    private ChartPlotAreaContext CreatePlotAreaContext(object content, ChartScene scene)
    {
        Dictionary<string, ChartScale> Named(ChartAxis primary, ChartAxis[] additional)
        {
            var named = new Dictionary<string, ChartScale>(StringComparer.Ordinal);
            foreach (var axis in additional.Append(primary).Distinct())
                if (!string.IsNullOrWhiteSpace(axis.Key) && ReferenceEquals(AnnotationAxis(axis.Key, primary, additional), axis) &&
                    scene.Scales.TryGetValue(axis, out var scale)) named[axis.Key] = scale;
            return named;
        }
        return new(content, DataContext, scene.Plot, scene.Range, scene.Extent, scene.XScale, scene.YScale,
            Named(XAxis, scene.ExtraXAxes), Named(YAxis, scene.ExtraYAxes));
    }

    private void ArrangePlotAreaContent(Size size)
    {
        MeasurePlotAreaContent(size);
        if (_plotAreaContentHost is not { IsVisible: true } host) return;
        EnsurePlotAreaContentMembership();
        if (ReferenceEquals(host, _plotAreaContentHost)) host.Arrange(new Rect(size));
    }

    private void EnsurePlotAreaContentMembership()
    {
        if (_plotAreaContentHost is not { } host) return;
        if (((ILogical)host).LogicalParent is null) LogicalChildren.Add(host);
        if (!ReferenceEquals(host, _plotAreaContentHost)) return;
        if (host.GetVisualParent() is null) VisualChildren.Add(host);
    }

    private static void DiscardPlotAreaContent(Border border, Control child)
    {
        try { if (ReferenceEquals(border.Child, child)) border.Child = null; }
        finally { border.ClearValue(DataContextProperty); }
    }

    private void DiscardObsoletePlotAreaContent(Border border, Control child)
    {
        // A callback can invalidate the view without replacing the source/template.
        // Release the owned entry as well as its child so the next layout can rebuild
        // rather than reusing an empty stale container. Never touch a newer entry.
        if (ReferenceEquals(border, _plotAreaContentBorder)) ReleasePlotAreaContentVisual();
        else DiscardPlotAreaContent(border, child);
    }

    private void ReleasePlotAreaContentVisual()
    {
        var host = _plotAreaContentHost; var border = _plotAreaContentBorder;
        _plotAreaContentHost = null; _plotAreaContentBorder = null; _plotAreaContentChild = null;
        _presentedPlotAreaContent = null; _presentedPlotAreaTemplate = null; _plotAreaContentScene = null;
        try { if (border is not null) border.Child = null; }
        finally
        {
            try { border?.ClearValue(DataContextProperty); }
            finally
            {
                if (host is not null)
                {
                    try { if (ReferenceEquals(host.GetVisualParent(), this)) VisualChildren.Remove(host); }
                    finally
                    {
                        try { if (ReferenceEquals(((ILogical)host).LogicalParent, this)) LogicalChildren.Remove(host); }
                        finally { host.Children.Clear(); }
                    }
                }
            }
        }
    }

    private void ClearPlotAreaContent()
    {
        if (_clearingPlotAreaContent) return;
        _clearingPlotAreaContent = true;
        try
        {
            _plotAreaContentRevision++;
            _plotAreaContentFailed = false; _failedPlotAreaContent = null; _failedPlotAreaTemplate = null;
            ReleasePlotAreaContentVisual();
        }
        finally { _clearingPlotAreaContent = false; }
    }

    private void PlotAreaContentAttached()
    {
        RefreshPlotAreaContentAncestors();
        InvalidatePlotAreaContent();
    }

    private void PlotAreaContentDetached()
    {
        RefreshPlotAreaContentAncestors();
        ClearPlotAreaContent();
    }

    private void RefreshPlotAreaContentAncestors()
    {
        foreach (var ancestor in _plotAreaContentAncestors) ancestor.PropertyChanged -= PlotAreaContentAncestorChanged;
        _plotAreaContentAncestors.Clear();
        if (!_attached) return;
        foreach (var ancestor in this.GetVisualAncestors())
        { _plotAreaContentAncestors.Add(ancestor); ancestor.PropertyChanged += PlotAreaContentAncestorChanged; }
    }

    private void PlotAreaContentAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != IsVisibleProperty) return;
        if (CanPresentPlotAreaContent()) InvalidatePlotAreaContent();
        else ClearPlotAreaContent();
    }

    private void RenderPlotAreaContentForExport(DrawingContext drawing)
    {
        if (_plotAreaContentHost is not { IsVisible: true } host || host.Bounds.Width <= 0 || host.Bounds.Height <= 0) return;
        var brush = new VisualBrush(host) { Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
        drawing.DrawRectangle(brush, null, host.Bounds);
    }

    private sealed class PlotAreaPresentationHost : Panel
    {
        private Rect _plot;
        public void SetPlot(Rect plot) { if (_plot == plot) return; _plot = plot; InvalidateMeasure(); InvalidateArrange(); }
        protected override Size MeasureOverride(Size availableSize)
        {
            if (Children.Count > 0) Children[0].Measure(_plot.Size);
            return default;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Children.Count > 0) Children[0].Arrange(_plot);
            return finalSize;
        }
    }
}
