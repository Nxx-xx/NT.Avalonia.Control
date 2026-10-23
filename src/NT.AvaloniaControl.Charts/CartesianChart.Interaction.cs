using System.Windows.Input;
using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<bool> IsTooltipEnabledProperty =
        AvaloniaProperty.Register<CartesianChart, bool>(nameof(IsTooltipEnabled), true);
    public static readonly StyledProperty<bool> IsZoomEnabledProperty =
        AvaloniaProperty.Register<CartesianChart, bool>(nameof(IsZoomEnabled));
    public static readonly StyledProperty<bool> IsPanEnabledProperty =
        AvaloniaProperty.Register<CartesianChart, bool>(nameof(IsPanEnabled));
    public static readonly StyledProperty<ChartInteractionAxes> InteractionAxesProperty =
        AvaloniaProperty.Register<CartesianChart, ChartInteractionAxes>(nameof(InteractionAxes), ChartInteractionAxes.Both);
    public static readonly StyledProperty<ChartViewport?> ViewportProperty =
        AvaloniaProperty.Register<CartesianChart, ChartViewport?>(nameof(Viewport),
            defaultBindingMode: BindingMode.TwoWay, validate: value => value is null || value.Value.IsValid);
    public static readonly DirectProperty<CartesianChart, ChartViewport?> VisibleRangeProperty =
        AvaloniaProperty.RegisterDirect<CartesianChart, ChartViewport?>(nameof(VisibleRange), chart => chart.VisibleRange);
    public static readonly DirectProperty<CartesianChart, Rect> PlotAreaProperty =
        AvaloniaProperty.RegisterDirect<CartesianChart, Rect>(nameof(PlotArea), chart => chart.PlotArea);
    public static readonly DirectProperty<CartesianChart, ChartPointHit?> HoveredPointProperty =
        AvaloniaProperty.RegisterDirect<CartesianChart, ChartPointHit?>(nameof(HoveredPoint), chart => chart.HoveredPoint);
    public static readonly StyledProperty<ChartPointHit?> SelectedPointProperty =
        AvaloniaProperty.Register<CartesianChart, ChartPointHit?>(nameof(SelectedPoint), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<ICommand?> PointInvokedCommandProperty =
        AvaloniaProperty.Register<CartesianChart, ICommand?>(nameof(PointInvokedCommand));
    public static readonly StyledProperty<IBrush?> TooltipBackgroundProperty =
        AvaloniaProperty.Register<CartesianChart, IBrush?>(nameof(TooltipBackground), Brushes.White);
    public static readonly StyledProperty<IBrush?> TooltipForegroundProperty =
        AvaloniaProperty.Register<CartesianChart, IBrush?>(nameof(TooltipForeground), Brushes.Black);
    public static readonly StyledProperty<IBrush?> TooltipBorderBrushProperty =
        AvaloniaProperty.Register<CartesianChart, IBrush?>(nameof(TooltipBorderBrush), Brushes.LightGray);

    // Pure helpers and property metadata may be used before an Avalonia platform is
    // initialized. Allocate the native cursor only when an actual drag starts.
    private static Cursor? _panCursor;
    private static Cursor PanCursor => _panCursor ??= new(StandardCursorType.SizeAll);
    private ChartPointHit? _hoveredPoint;
    private Point _tooltipAnchor;
    private IPointer? _panPointer;
    private Point _panOrigin;
    private ChartScene? _panScene;
    private Cursor? _previousCursor;
    private bool _applyingPan;
    private ChartViewport? _visibleRange;
    private Rect _plotArea;

    public bool IsTooltipEnabled { get => GetValue(IsTooltipEnabledProperty); set => SetValue(IsTooltipEnabledProperty, value); }
    public bool IsZoomEnabled { get => GetValue(IsZoomEnabledProperty); set => SetValue(IsZoomEnabledProperty, value); }
    public bool IsPanEnabled { get => GetValue(IsPanEnabledProperty); set => SetValue(IsPanEnabledProperty, value); }
    public ChartInteractionAxes InteractionAxes { get => GetValue(InteractionAxesProperty); set => SetValue(InteractionAxesProperty, value); }
    /// <summary>Requested data viewport, constrained to the full axis range. Null enables automatic ranges.</summary>
    public ChartViewport? Viewport { get => GetValue(ViewportProperty); set => SetValue(ViewportProperty, value); }
    /// <summary>Effective data range after layout and extent constraints.</summary>
    public ChartViewport? VisibleRange => GetScene()?.Range;
    /// <summary>The chart-local plot rectangle, excluding labels, titles and legends.</summary>
    public Rect PlotArea => GetScene()?.Plot ?? default;
    public ChartPointHit? HoveredPoint => _hoveredPoint;
    /// <summary>Last selected point; synchronized with SelectedPoints and preserved by stable identity.</summary>
    public ChartPointHit? SelectedPoint { get => GetValue(SelectedPointProperty); set => SetValue(SelectedPointProperty, value); }
    public ICommand? PointInvokedCommand { get => GetValue(PointInvokedCommandProperty); set => SetValue(PointInvokedCommandProperty, value); }
    public IBrush? TooltipBackground { get => GetValue(TooltipBackgroundProperty); set => SetValue(TooltipBackgroundProperty, value); }
    public IBrush? TooltipForeground { get => GetValue(TooltipForegroundProperty); set => SetValue(TooltipForegroundProperty, value); }
    public IBrush? TooltipBorderBrush { get => GetValue(TooltipBorderBrushProperty); set => SetValue(TooltipBorderBrushProperty, value); }

    /// <summary>Raised when the requested viewport changes; axis configurations are left intact.</summary>
    public event EventHandler? ViewportChanged;

    public override void ResetViewport() => ResetViewportCore(pauseFollowing: true);

    private void ResetViewportCore(bool pauseFollowing)
    {
        VerifyAccess();
        if (pauseFollowing) PauseFollowingForNavigation();
        InvalidateInputTransform();
        StopRectangle();
        StopPan();
        ClearHover();
        var previousReset = _resettingViewportForDataPolicy;
        var previousViewportWrite = _writingFollowViewport;
        _resettingViewportForDataPolicy = true;
        _writingFollowViewport = true;
        try
        {
            foreach (var axis in Axes.Append(XAxis).Append(YAxis).Distinct())
            { axis.SetCurrentValue(ChartAxis.ViewMinimumProperty, null); axis.SetCurrentValue(ChartAxis.ViewMaximumProperty, null); }
            SetCurrentValue(ViewportProperty, null);
        }
        finally
        {
            _resettingViewportForDataPolicy = previousReset;
            _writingFollowViewport = previousViewportWrite;
        }
        RequestRender();
    }

    private void OnInteractionPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ViewportProperty)
        {
            StopRectangle();
            if (!_applyingPan) StopPan();
            RequestRender();
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }
        if ((change.Property == IsPanEnabledProperty && !IsPanEnabled) ||
            change.Property == InteractionAxesProperty || !IsEffectivelyEnabled)
        { StopRectangle(); StopPan(); }
        if (change.Property == TooltipBackgroundProperty || change.Property == TooltipForegroundProperty ||
            change.Property == TooltipBorderBrushProperty || change.Property == IsTooltipEnabledProperty)
            RequestRender();
    }

    /// <summary>Finds the nearest visible data point or grouped bar at a chart-local position.</summary>
    public ChartPointHit? HitTestPoint(Point position, double tolerance = 12)
    {
        VerifyAccess();
        var scene = GetScene();
        if (scene is null || !scene.Plot.Contains(position) || !double.IsFinite(tolerance) || tolerance < 0) return null;
        var bestDistance = double.PositiveInfinity;
        ChartPointHit? best = null;
        foreach (var snapshot in scene.Snapshots)
        {
            var rawPosition = snapshot.Context!.Projection.Unproject(position);
            if (snapshot.XAxis.IsInScaleBreak(rawPosition.X) || snapshot.YAxis.IsInScaleBreak(rawPosition.Y)) continue;
            var hit = snapshot.Series.Renderer.HitTest(snapshot.Context!, position, tolerance);
            if (hit is null || !double.IsFinite(hit.Position.X) || !double.IsFinite(hit.Position.Y) ||
                !double.IsFinite(hit.DistanceSquared) || hit.DistanceSquared < 0 || hit.DistanceSquared > bestDistance) continue;
            if (!snapshot.Data.TryGetPoint(hit.SourceIndex, out var item) || !IsValid(item.Point)) continue;
            bestDistance = hit.DistanceSquared;
            best = ChartPointHit.FromData(snapshot.Series, item, hit.Position);
        }
        return best;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (double.IsFinite(e.Delta.X) && double.IsFinite(e.Delta.Y) && e.Delta != default &&
            (IsTouchInputActive || _panPointer is not null || _selectionPointer is not null))
        {
            var expectedRevision = _inputTransformRevision + 1;
            CancelInput();
            if (_inputTransformRevision != expectedRevision) return;
        }
        if (HandleLegendPresentationWheel(e)) return;
        HandleChartWheel(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (HandleTouchPressed(e)) return;
        InvalidateInputTransform();
        if (HandleLegendPresentationPressed(e)) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var scene = GetScene();
        var position = e.GetPosition(this);
        if (scene is not null && LegendItemAt(scene, position) is { } legend && (IsLegendInteractive || IsLegendFloating))
        {
            if (IsLegendInteractive) ToggleSeriesVisibility(legend.Series);
            e.Handled = true; return;
        }
        if (scene is not null && IsLegendFloating && LegendPanelContains(scene, position))
        { e.Handled = true; return; }
        if (scene is null || !scene.HasPoints || !scene.Plot.Contains(position)) return;
        if (IsKeyboardNavigationEnabled)
        {
            var revision = _inputTransformRevision;
            Focus();
            if (!_attached || revision != _inputTransformRevision) return;
            scene = GetScene();
            position = e.GetPosition(this);
            if (scene is null || !scene.HasPoints || !scene.Plot.Contains(position)) return;
        }
        var pressRevision = _inputTransformRevision;
        if (BeginRectangle(e, position)) return;
        var selected = HitTestPoint(position);
        var seriesSelectionRevision = _seriesSelectionRevision;
        SelectFromPointer(selected, e.KeyModifiers);
        if (SeriesSelectionMode != ChartSelectionMode.None && seriesSelectionRevision == _seriesSelectionRevision && ReferenceEquals(_scene, scene))
            SelectSeriesFromPointer(selected?.Series ?? HitTestSegment(position)?.Series, e.KeyModifiers);
        var command = PointInvokedCommand;
        var commandRevision = _inputTransformRevision;
        if (selected is not null && command?.CanExecute(selected) == true && _attached &&
            ReferenceEquals(command, PointInvokedCommand) && commandRevision == _inputTransformRevision && ReferenceEquals(_scene, scene))
            command.Execute(selected);
        // Commands and two-way bindings can replace data or detach the chart synchronously.
        if (!_attached || pressRevision != _inputTransformRevision || !ReferenceEquals(_scene, scene))
        {
            if (ReferenceEquals(e.Pointer.Captured, this)) e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }
        if (InteractionAxes == ChartInteractionAxes.None) return;
        if (e.ClickCount >= 2 && (IsZoomEnabled || IsPanEnabled))
        {
            ResetViewport();
            e.Handled = true;
            return;
        }
        if (!IsPanEnabled) return;
        StopPan();
        ClearHover();
        _panOrigin = position;
        _panScene = scene;
        _panPointer = e.Pointer;
        _previousCursor = Cursor;
        SetCurrentValue(CursorProperty, PanCursor);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (HandleTouchMoved(e)) return;
        var position = e.GetPosition(this);
        if (_selectionPointer == e.Pointer)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { StopRectangle(); return; }
            _selectionEnd = position; RequestRender(); e.Handled = true; return;
        }
        if (_panPointer == e.Pointer && _panScene is { } initial)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { StopPan(); return; }
            _applyingPan = true;
            try { TryApplyInputTransform(initial, _panOrigin, position, 1, InteractionAxes); }
            finally { _applyingPan = false; }
            e.Handled = true;
            return;
        }
        if ((UsesLegendPresentation || IsLegendFloating) && GetScene() is { } scene && LegendPanelContains(scene, position))
        { ClearHover(); return; }
        UpdateReadings(position);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (HandleTouchReleased(e)) return;
        if (_selectionPointer == e.Pointer)
        {
            _selectionEnd = e.GetPosition(this);
            var scene = GetScene();
            var rectangle = scene is null ? default : SelectionRectangle(scene);
            var additive = _additiveRectangle;
            StopRectangle();
            if (scene is not null) SelectRectangle(rectangle, additive);
            e.Handled = true; return;
        }
        if (_panPointer != e.Pointer) return;
        StopPan();
        e.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearHover();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        HandleTouchCaptureLost(e);
        StopRectangle();
        StopPan();
        ClearHover();
    }

    private void StopPan()
    {
        var pointer = _panPointer;
        _panPointer = null;
        _panScene = null;
        if (pointer is null) return;
        SetCurrentValue(CursorProperty, _previousCursor);
        _previousCursor = null;
        if (ReferenceEquals(pointer.Captured, this)) pointer.Capture(null);
    }

    private void ClearHover()
    {
        ClearReadings();
    }

    private static double AscendingFraction(ChartScale scale, double value) => scale.IsInversed ? 1 - scale.Normalize(value) : scale.Normalize(value);
    private static double AscendingValue(ChartScale scale, double fraction) => scale.Denormalize(scale.IsInversed ? 1 - fraction : fraction);
    private static ChartViewport NormalizeViewport(ChartViewport range, ChartScale x, ChartScale y) => new(
        AscendingFraction(x, range.XMinimum), AscendingFraction(x, range.XMaximum), AscendingFraction(y, range.YMinimum), AscendingFraction(y, range.YMaximum));
    private static ChartViewport DenormalizeViewport(ChartViewport range, ChartScale x, ChartScale y) => new(
        AscendingValue(x, range.XMinimum), AscendingValue(x, range.XMaximum), AscendingValue(y, range.YMinimum), AscendingValue(y, range.YMaximum));

    private ChartViewport ConstrainViewport(ChartViewport requested, ChartScale xScale, ChartScale yScale)
    {
        var extent = new ChartViewport(xScale.Minimum, xScale.Maximum, yScale.Minimum, yScale.Maximum);
        var safe = new ChartViewport(XAxis.IsValidValue(requested.XMinimum) ? requested.XMinimum : extent.XMinimum,
            XAxis.IsValidValue(requested.XMaximum) ? requested.XMaximum : extent.XMaximum,
            YAxis.IsValidValue(requested.YMinimum) ? requested.YMinimum : extent.YMinimum,
            YAxis.IsValidValue(requested.YMaximum) ? requested.YMaximum : extent.YMaximum);
        var x = ConstrainAxisView(XAxis, xScale, safe.XMinimum, safe.XMaximum);
        var y = ConstrainAxisView(YAxis, yScale, safe.YMinimum, safe.YMaximum);
        var constrained = new ChartViewport(x.Minimum, x.Maximum, y.Minimum, y.Maximum);
        return constrained.IsValid ? constrained : extent;
    }

    private static (double MinimumSpan, double MaximumSpan) ZoomLimits(ChartAxis axis) =>
        (1 / Math.Max(axis.MinimumZoom, axis.MaximumZoom), 1 / axis.MinimumZoom);

    private static (double Minimum, double Maximum) ConstrainAxisView(ChartAxis axis, ChartScale full, double minimum, double maximum,
        double anchor = .5)
    {
        var lower = axis.IsValidValue(minimum) ? AscendingFraction(full, minimum) : 0;
        var upper = axis.IsValidValue(maximum) ? AscendingFraction(full, maximum) : 1;
        var limits = ZoomLimits(axis);
        // An already valid window needs no transform round trip. Keeping its exact endpoints
        // prevents repeated layout or an interaction on the other axis from drifting this axis.
        if (axis.IsValidValue(minimum) && axis.IsValidValue(maximum) && minimum < maximum &&
            double.IsFinite(lower) && double.IsFinite(upper) && lower >= 0 && upper <= 1 &&
            upper - lower >= limits.MinimumSpan && upper - lower <= limits.MaximumSpan)
            return (minimum, maximum);
        if (!double.IsFinite(lower) || !double.IsFinite(upper) || lower >= upper) { lower = 0; upper = 1; }
        lower = Math.Clamp(lower, 0, 1); upper = Math.Clamp(upper, 0, 1);
        if (lower >= upper) { lower = 0; upper = 1; }
        var span = Math.Clamp(upper - lower, limits.MinimumSpan, limits.MaximumSpan);
        var center = lower + (upper - lower) * Math.Clamp(anchor, 0, 1);
        lower = Math.Clamp(center - span * anchor, 0, 1 - span);
        var first = AscendingValue(full, lower); var last = AscendingValue(full, lower + span);
        if (first >= last) { first = Math.Max(full.Minimum, Math.BitDecrement(first)); last = Math.Min(full.Maximum, Math.BitIncrement(last)); }
        return first < last ? (first, last) : (full.Minimum, full.Maximum);
    }

    private void ReconcileSelection()
    {
        ReconcileSelectionList();
        ReconcileSeriesSelection();
    }

    private void UpdateSelectionFromScene(ChartScene scene)
    {
        ReconcileSelectionList(scene);
    }

    private void RenderTooltip(DrawingContext context, ChartScene scene)
    {
        if (_foregroundTooltipPresentation is not null && !_drawingForegroundTooltip) return;
        if (IsTrackballActive || !IsTooltipEnabled || _hoveredPoint is not { } hit || _panPointer is not null ||
            _selectionPointer is not null || SuppressDrawnTooltip) return;
        var snapshot = scene.Snapshots.FirstOrDefault(item => ReferenceEquals(item.Series, hit.Series));
        if (snapshot is null) return;
        var lines = TooltipFallbackLines(scene);
        if (lines.Length == 0) return;
        var texts = lines.Select((line, index) => InteractionText(line, index == 0)).ToArray();
        var width = Math.Min(Math.Max(90, texts.Max(text => text.Width) + 24), Math.Max(1, Bounds.Width - 16));
        var height = texts.Sum(text => text.Height + 4) + 16;
        var x = _tooltipAnchor.X + 14;
        var y = _tooltipAnchor.Y + 16;
        if (x + width > Bounds.Width - 8) x = _tooltipAnchor.X - width - 14;
        if (y + height > Bounds.Height - 8) y = _tooltipAnchor.Y - height - 12;
        x = Math.Clamp(x, 8, Math.Max(8, Bounds.Width - width - 8));
        y = Math.Clamp(y, 8, Math.Max(8, Bounds.Height - height - 8));
        context.DrawRectangle(TooltipBackground, new Pen(TooltipBorderBrush, 1), new Rect(x, y, width, height), 6, 6);
        using var clip = context.PushClip(new Rect(x + 10, y + 8, Math.Max(0, width - 20), Math.Max(0, height - 16)));
        foreach (var text in texts)
        {
            DrawInteractionText(context, text, new Point(x + 12, y + 9));
            y += text.Height + 4;
        }
    }

}
