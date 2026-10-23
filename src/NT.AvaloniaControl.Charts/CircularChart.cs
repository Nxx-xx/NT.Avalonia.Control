using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

public enum CircularLabelPosition { Outside, Inside, None }

/// <summary>Pie, annular and partial-circle projections. Each series has an independent value total.</summary>
public sealed partial class CircularChart : ChartBase
{
    public static readonly StyledProperty<IEnumerable<CircularSeries>?> SeriesSourceProperty = AvaloniaProperty.Register<CircularChart, IEnumerable<CircularSeries>?>(nameof(SeriesSource));
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<CircularChart, string?>(nameof(Title));
    public static readonly StyledProperty<string> EmptyTextProperty = AvaloniaProperty.Register<CircularChart, string>(nameof(EmptyText), "暂无正值数据");
    public static readonly StyledProperty<bool> ShowLegendProperty = AvaloniaProperty.Register<CircularChart, bool>(nameof(ShowLegend), true);
    public static readonly StyledProperty<bool> ShowDataLabelsProperty = AvaloniaProperty.Register<CircularChart, bool>(nameof(ShowDataLabels), true);
    public static readonly StyledProperty<CircularLabelPosition> LabelPositionProperty = AvaloniaProperty.Register<CircularChart, CircularLabelPosition>(nameof(LabelPosition), validate: Enum.IsDefined);
    public static readonly StyledProperty<Func<CircularSliceInfo, string>?> LabelFormatterProperty = AvaloniaProperty.Register<CircularChart, Func<CircularSliceInfo, string>?>(nameof(LabelFormatter));
    public static readonly StyledProperty<IBrush?> LabelConnectorBrushProperty = AvaloniaProperty.Register<CircularChart, IBrush?>(nameof(LabelConnectorBrush), Brushes.SlateGray);
    public static readonly StyledProperty<double> LabelConnectorThicknessProperty = AvaloniaProperty.Register<CircularChart, double>(nameof(LabelConnectorThickness), 1, validate: v => double.IsFinite(v) && v >= 0 && v <= 32);
    public static readonly StyledProperty<string?> CenterTextProperty = AvaloniaProperty.Register<CircularChart, string?>(nameof(CenterText));
    public static readonly StyledProperty<Control?> CenterContentProperty = AvaloniaProperty.Register<CircularChart, Control?>(nameof(CenterContent));
    public static readonly StyledProperty<IEnumerable<IBrush>?> ColorPaletteProperty = AvaloniaProperty.Register<CircularChart, IEnumerable<IBrush>?>(nameof(ColorPalette));
    public static readonly StyledProperty<bool> IsTooltipEnabledProperty = AvaloniaProperty.Register<CircularChart, bool>(nameof(IsTooltipEnabled), true);
    public static readonly StyledProperty<Func<CircularSliceInfo, string>?> TooltipFormatterProperty = AvaloniaProperty.Register<CircularChart, Func<CircularSliceInfo, string>?>(nameof(TooltipFormatter));
    public static readonly DirectProperty<CircularChart, Rect> PlotAreaProperty = AvaloniaProperty.RegisterDirect<CircularChart, Rect>(nameof(PlotArea), c => c.PlotArea);
    private static readonly IBrush[] DefaultPalette = [Brush.Parse("#4385F4"), Brush.Parse("#19A798"), Brush.Parse("#F2A33A"), Brush.Parse("#9B72E8"), Brush.Parse("#E36D8F"), Brush.Parse("#44B4C4")];
    private readonly HashSet<CircularSeries> _observed = [];
    private readonly Dictionary<CircularSeries, (long Source, long Published)> _versions = [];
    private INotifyCollectionChanged? _sourceNotifications;
    private IReadOnlyList<CircularSliceInfo> _slices = Array.Empty<CircularSliceInfo>();
    private bool _dirty = true, _attached, _building;
    private bool _rendering, _renderRequestQueued, _selectionUpdateQueued;
    private IReadOnlyList<CircularSliceInfo>? _renderScene;
    private Rect _plotArea;
    private Point _circleCenter;
    private double _circleRadius;
    private CircularSliceInfo? _hovered;
    private CircularSliceInfo? _keyboardSlice;
    private Control? _centerChild;
    private CircularCenterHost? _centerHost;
    public Rect CenterContentBounds { get; private set; }
    private Size _layoutSize;
    private ChartAnimationTriggers _pendingTrigger = ChartAnimationTriggers.Data;
    public ObservableCollection<CircularSeries> Series { get; } = [];
    public IEnumerable<CircularSeries>? SeriesSource { get => GetValue(SeriesSourceProperty); set => SetValue(SeriesSourceProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    public bool ShowDataLabels { get => GetValue(ShowDataLabelsProperty); set => SetValue(ShowDataLabelsProperty, value); }
    public CircularLabelPosition LabelPosition { get => GetValue(LabelPositionProperty); set => SetValue(LabelPositionProperty, value); }
    public Func<CircularSliceInfo, string>? LabelFormatter { get => GetValue(LabelFormatterProperty); set => SetValue(LabelFormatterProperty, value); }
    public IBrush? LabelConnectorBrush { get => GetValue(LabelConnectorBrushProperty); set => SetValue(LabelConnectorBrushProperty, value); }
    public double LabelConnectorThickness { get => GetValue(LabelConnectorThicknessProperty); set => SetValue(LabelConnectorThicknessProperty, value); }
    public string? CenterText { get => GetValue(CenterTextProperty); set => SetValue(CenterTextProperty, value); }
    public Control? CenterContent { get => GetValue(CenterContentProperty); set => SetValue(CenterContentProperty, value); }
    public IEnumerable<IBrush>? ColorPalette { get => GetValue(ColorPaletteProperty); set => SetValue(ColorPaletteProperty, value); }
    public bool IsTooltipEnabled { get => GetValue(IsTooltipEnabledProperty); set => SetValue(IsTooltipEnabledProperty, value); }
    public Func<CircularSliceInfo, string>? TooltipFormatter { get => GetValue(TooltipFormatterProperty); set => SetValue(TooltipFormatterProperty, value); }
    public Rect PlotArea => _plotArea;
    private IEnumerable<CircularSeries> ActiveSeries => SeriesSource ?? Series;
    public CircularChart()
    {
        ClipToBounds = true;
        Focusable = true;
        Series.CollectionChanged += CollectionChanged;
    }
    public override void ResetViewport() { VerifyAccess(); CancelAnimations(); }
    protected override Size MeasureOverride(Size availableSize)
    {
        _centerChild?.Measure(new Size(200, 120));
        return new(double.IsFinite(availableSize.Width) ? Math.Min(520, availableSize.Width) : 520,
            double.IsFinite(availableSize.Height) ? Math.Min(320, availableSize.Height) : 320);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_layoutSize != finalSize) { _dirty = true; _pendingTrigger |= ChartAnimationTriggers.Viewport; _layoutSize = finalSize; }
        var result = base.ArrangeOverride(finalSize);
        _ = GetSlices();
        ArrangeCenter();
        return result;
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        SynchronizeSubscriptions();
        _entrancePending = true;
        InvalidateCircular();
        RefreshAnimationAncestors();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        CancelAnimations();
        RefreshAnimationAncestors();
        SynchronizeSubscriptions();
        _hovered = null;
        base.OnDetachedFromVisualTree(e);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlotAreaProperty || change.Property == AnimationProgressProperty || change.Property == AnimationStateProperty) return;
        if (change.Property == BoundsProperty && Bounds.Size == _layoutSize)
        {
            if (_pendingSelection is not null) { _dirty = true; if (!_rendering && Bounds.Width > 0 && Bounds.Height > 0) _ = GetSlices(); }
            RequestCircularRender(); return;
        }
        if (AnimationPropertyChanged(change)) return;
        if (SelectionPropertyChanged(change)) return;
        if (change.Property == CenterContentProperty) ReplaceCenterContent();
        if (change.Property == SeriesSourceProperty) SynchronizeSubscriptions();
        if (change.Property == IsVisibleProperty && !IsVisible || change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled) { CancelAnimations(); _hovered = null; }
        _pendingTrigger |= ChartAnimationTriggers.Viewport;
        InvalidateCircular();
    }
    private void InvalidateCircular()
    {
        _dirty = true;
        RequestCircularRender();
        if (!_rendering) InvalidateArrange();
    }
    private void RequestCircularRender()
    {
        if (_rendering)
        {
            if (!_renderRequestQueued)
            {
                _renderRequestQueued = true;
                Dispatcher.UIThread.Post(() => { _renderRequestQueued = false; InvalidateVisual(); InvalidateArrange(); });
            }
            return;
        }
        InvalidateVisual();
    }
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    { VerifyAccess(); _pendingTrigger |= ChartAnimationTriggers.Data; SynchronizeSubscriptions(); InvalidateCircular(); }
    private void SeriesChanged(object? sender, EventArgs e)
    {
        VerifyAccess();
        if (sender is CircularSeries series)
        {
            var version = (series.SourceVersion, series.PublishedSnapshotRevision);
            _pendingTrigger |= _versions.TryGetValue(series, out var old) && old == version ? ChartAnimationTriggers.Viewport : ChartAnimationTriggers.Data;
            _versions[series] = version;
        }
        InvalidateCircular();
    }
    private void SynchronizeSubscriptions()
    {
        var desired = _attached ? ActiveSeries.Distinct().ToHashSet() : [];
        foreach (var series in _observed.ToArray())
            if (!desired.Contains(series))
            {
                series.Changed -= SeriesChanged; _observed.Remove(series); _versions.Remove(series);
                if (ReferenceEquals(((ILogical)series).LogicalParent, this)) LogicalChildren.Remove(series);
            }
        foreach (var series in desired)
            if (_observed.Add(series))
            {
                if (((ILogical)series).LogicalParent is null) LogicalChildren.Add(series);
                series.Changed += SeriesChanged;
                _versions[series] = (series.SourceVersion, series.PublishedSnapshotRevision);
            }
        var source = _attached ? SeriesSource as INotifyCollectionChanged : null;
        if (!ReferenceEquals(_sourceNotifications, source))
        {
            if (_sourceNotifications is not null) _sourceNotifications.CollectionChanged -= CollectionChanged;
            _sourceNotifications = source;
            if (source is not null) source.CollectionChanged += CollectionChanged;
        }
    }
    /// <summary>Current displayed sectors, including hidden small sectors and transient exiting sectors. Source values are never rewritten.</summary>
    public IReadOnlyList<CircularSliceInfo> GetSlices()
    {
        VerifyAccess();
        if (_renderScene is { } rendering) return rendering;
        if (_building) return _slices;
        if (!_attached && !_rendering) _dirty = true;
        if (!_dirty) return _slices;
        _building = true;
        try
        {
            _dirty = false;
            var previous = _slices;
            var series = ActiveSeries.Where(s => s.IsVisible).Distinct().ToArray();
            ComputeLayout(series);
            var palette = ColorPalette?.Where(b => b is not null).ToArray();
            if (palette is null || palette.Length == 0) palette = DefaultPalette;
            var target = new List<CircularSliceInfo>();
            foreach (var item in series)
            {
                var snapshot = item.CreateSnapshot();
                var data = CircularChartGeometry.Build(item, snapshot, _circleCenter, _circleRadius,
                    p => IsSelected(item, p) ? SelectedExplodeOffset : 0);
                foreach (var slice in data)
                    target.Add(slice with { Fill = ResolveFill(slice.Data.ColorValue) ?? item.Fill ?? palette[Math.Abs((long)slice.Data.SourceIndex % palette.Length)] });
            }
            BeginTransition(previous, target.AsReadOnly());
            ReconcileSelection(_slices);
            _hovered = _hovered is { } hover ? _slices.LastOrDefault(s => !s.IsExiting && SameIdentity(s.Hit, hover.Hit)) : null;
            _keyboardSlice = _keyboardSlice is { } keyboard ? _slices.LastOrDefault(s => !s.IsExiting && SameIdentity(s.Hit, keyboard.Hit)) : null;
            return _slices;
        }
        catch { _dirty = true; throw; }
        finally { _building = false; }
    }
    private static IBrush? ResolveFill(object? value) => value switch
    {
        IBrush brush => brush,
        Color color => new SolidColorBrush(color),
        string text when Color.TryParse(text, out var color) => new SolidColorBrush(color),
        _ => null,
    };
    private void ComputeLayout(CircularSeries[] series)
    {
        var left = Padding.Left + 8; var top = Padding.Top + 8 + (string.IsNullOrEmpty(Title) ? 0 : FontSize * 1.6 + 8);
        var size = _layoutSize.Width > 0 && _layoutSize.Height > 0 ? _layoutSize : Bounds.Size;
        var right = size.Width - Padding.Right - 8;
        var bottom = size.Height - Padding.Bottom - 8 - (ShowLegend ? FontSize * 2 + 10 : 0);
        if (ShowDataLabels && LabelPosition == CircularLabelPosition.Outside)
        { var reserve = Math.Min(110, Math.Max(0, (right - left) * .2)); left += reserve; right -= reserve; top += 12; bottom -= 12; }
        var plot = new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        SetAndRaise(PlotAreaProperty, ref _plotArea, plot);
        // Fit actual angular extents. A top half-ring uses its full allotted height.
        var extent = new Rect(-1, -1, 2, 2);
        if (series.Length > 0)
        {
            var points = new List<Point>();
            foreach (var s in series)
            {
                var start = s.StartAngle % 360;
                var sweep = (s.Clockwise ? 1 : -1) * s.SweepAngle;
                points.Add(default);
                foreach (var angle in new[] { start, start + sweep, 0d, 90d, 180d, 270d })
                    if (angle == start || angle == start + sweep || AngleInSweep(angle, start, sweep))
                        points.Add(new(Math.Cos(angle * Math.PI / 180), Math.Sin(angle * Math.PI / 180)));
            }
            extent = new Rect(new Point(points.Min(p => p.X), points.Min(p => p.Y)), new Point(points.Max(p => p.X), points.Max(p => p.Y)));
        }
        var explode = series.Select(s => s.ExplodeOffset).DefaultIfEmpty(0).Max() + (SelectionMode == ChartSelectionMode.None ? 0 : SelectedExplodeOffset);
        var width = Math.Max(0, plot.Width - explode * 2); var height = Math.Max(0, plot.Height - explode * 2);
        _circleRadius = Math.Max(0, Math.Min(width / Math.Max(.001, extent.Width), height / Math.Max(.001, extent.Height)));
        _circleCenter = plot.Center - new Vector((extent.Left + extent.Right) * _circleRadius / 2, (extent.Top + extent.Bottom) * _circleRadius / 2);
    }
    private static bool AngleInSweep(double angle, double start, double sweep)
    {
        var delta = ((sweep >= 0 ? angle - start : start - angle) % 360 + 360) % 360;
        return delta <= Math.Abs(sweep) + 1e-10;
    }
    private void ReplaceCenterContent()
    {
        var next = CenterContent;
        if (ReferenceEquals(_centerChild, next)) return;
        if (next is not null && (((ILogical)next).LogicalParent is not null || next.GetVisualParent() is not null))
            throw new InvalidOperationException("CenterContent must be an unparented control.");
        if (_centerChild is { } old) { _centerHost?.SetChild(null); LogicalChildren.Remove(old); }
        _centerChild = next;
        if (next is not null)
        {
            if (_centerHost is null) { _centerHost = new(); LogicalChildren.Add(_centerHost); VisualChildren.Add(_centerHost); }
            LogicalChildren.Add(next); _centerHost.SetChild(next);
        }
        else if (_centerHost is { } host) { VisualChildren.Remove(host); LogicalChildren.Remove(host); _centerHost = null; CenterContentBounds = default; }
    }
    private void ArrangeCenter()
    {
        if (_centerChild is not { } child) return;
        var hole = GetSlices().Where(s => s.IsVisible).Select(s => s.InnerRadius).DefaultIfEmpty(0).Min();
        var size = Math.Max(0, hole * Math.Sqrt(2) - 4);
        var box = new Rect(_circleCenter - new Vector(size / 2, size / 2), new Size(size, size)).Intersect(_plotArea);
        CenterContentBounds = box;
        _centerHost?.Measure(box.Size);
        _centerHost?.Arrange(box);
    }
    /// <summary>Topmost visible current-frame sector; donut holes, angular gaps and outgoing sectors cannot be selected.</summary>
    public CircularSliceInfo? HitTest(Point position)
    {
        if (!new Rect(Bounds.Size).Contains(position)) return null;
        return GetSlices().LastOrDefault(s => !s.IsExiting && s.IsVisible && CircularChartGeometry.Contains(s, position));
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    { base.OnPointerMoved(e); _hovered = IsTooltipEnabled ? HitTest(e.GetPosition(this)) : null; RequestCircularRender(); }
    protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); _hovered = null; RequestCircularRender(); }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (HitTest(e.GetPosition(this)) is { } hit && SelectSlice(hit, true, ChartSelectionChangeReason.Pointer)) { Focus(); e.Handled = true; }
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var visible = GetSlices().Where(s => s.IsVisible && !s.IsExiting).ToArray();
        if (visible.Length == 0) return;
        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End)
        {
            var index = Array.FindIndex(visible, s => SameIdentity(s.Hit, _keyboardSlice?.Hit));
            index = e.Key switch { Key.Home => 0, Key.End => visible.Length - 1, Key.Left => index < 0 ? visible.Length - 1 : (index - 1 + visible.Length) % visible.Length, _ => (index + 1) % visible.Length };
            _keyboardSlice = visible[index]; _hovered = visible[index]; e.Handled = true; RequestCircularRender();
        }
        else if (e.Key is Key.Space or Key.Enter && _keyboardSlice is { } current) { SelectSlice(current, true, ChartSelectionChangeReason.Keyboard); e.Handled = true; }
        else if (e.Key == Key.Escape) { ClearSelection(); _hovered = _keyboardSlice = null; e.Handled = true; }
    }
    private FormattedText Text(string text, double? size = null) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyle, FontWeight), size ?? FontSize, Foreground ?? Brushes.Black);
    private sealed class CircularCenterHost : Control
    {
        private Control? _child;
        public CircularCenterHost() { ClipToBounds = true; UseLayoutRounding = false; }
        public void SetChild(Control? child) { if (_child is { } old) VisualChildren.Remove(old); _child = child; if (child is not null) VisualChildren.Add(child); }
        protected override Size MeasureOverride(Size availableSize) { _child?.Measure(availableSize); return default; }
        protected override Size ArrangeOverride(Size finalSize) { _child?.Arrange(new Rect(finalSize)); return finalSize; }
    }
}
