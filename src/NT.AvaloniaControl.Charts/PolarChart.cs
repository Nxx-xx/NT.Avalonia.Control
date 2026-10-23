using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public enum PolarGridShape { Circle, Polygon }

/// <summary>Numeric angle/radius and shared-category radial projections, with independent source identity.</summary>
public partial class PolarChart : ChartBase
{
    public static readonly StyledProperty<IEnumerable<PolarSeries>?> SeriesSourceProperty = AvaloniaProperty.Register<PolarChart, IEnumerable<PolarSeries>?>(nameof(SeriesSource));
    public static readonly StyledProperty<ChartAxis> AngularAxisProperty = AvaloniaProperty.Register<PolarChart, ChartAxis>(nameof(AngularAxis), coerce: (_, a) => a ?? new NumericAxis());
    public static readonly StyledProperty<ChartAxis> RadialAxisProperty = AvaloniaProperty.Register<PolarChart, ChartAxis>(nameof(RadialAxis), coerce: (_, a) => a ?? new NumericAxis());
    public static readonly StyledProperty<IReadOnlyList<string>?> AxisLabelsProperty = AvaloniaProperty.Register<PolarChart, IReadOnlyList<string>?>(nameof(AxisLabels));
    public static readonly StyledProperty<ChartAngleUnit> AngleUnitProperty = AvaloniaProperty.Register<PolarChart, ChartAngleUnit>(nameof(AngleUnit), validate: Enum.IsDefined);
    public static readonly StyledProperty<double> StartAngleProperty = AvaloniaProperty.Register<PolarChart, double>(nameof(StartAngle), -90, validate: double.IsFinite);
    public static readonly StyledProperty<double> SweepAngleProperty = AvaloniaProperty.Register<PolarChart, double>(nameof(SweepAngle), 360, validate: v => double.IsFinite(v) && v > 0 && v <= 360);
    public static readonly StyledProperty<bool> ClockwiseProperty = AvaloniaProperty.Register<PolarChart, bool>(nameof(Clockwise), true);
    public static readonly StyledProperty<bool> WrapAnglesProperty = AvaloniaProperty.Register<PolarChart, bool>(nameof(WrapAngles), true);
    public static readonly StyledProperty<PolarGridShape> GridShapeProperty = AvaloniaProperty.Register<PolarChart, PolarGridShape>(nameof(GridShape), validate: Enum.IsDefined);
    public static readonly StyledProperty<int> GridLevelsProperty = AvaloniaProperty.Register<PolarChart, int>(nameof(GridLevels), 5, validate: v => v > 0 && v <= 100);
    public static readonly StyledProperty<bool> ShowGridLinesProperty = AvaloniaProperty.Register<PolarChart, bool>(nameof(ShowGridLines), true);
    public static readonly StyledProperty<bool> ShowAxisLabelsProperty = AvaloniaProperty.Register<PolarChart, bool>(nameof(ShowAxisLabels), true);
    public static readonly StyledProperty<bool> ShowLegendProperty = AvaloniaProperty.Register<PolarChart, bool>(nameof(ShowLegend), true);
    public static readonly StyledProperty<bool> ShowDataLabelsProperty = AvaloniaProperty.Register<PolarChart, bool>(nameof(ShowDataLabels));
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<PolarChart, string?>(nameof(Title));
    public static readonly StyledProperty<string> EmptyTextProperty = AvaloniaProperty.Register<PolarChart, string>(nameof(EmptyText), "暂无有效数据");
    public static readonly StyledProperty<IBrush?> AxisBrushProperty = AvaloniaProperty.Register<PolarChart, IBrush?>(nameof(AxisBrush), Brushes.SlateGray);
    public static readonly StyledProperty<IBrush?> GridLineBrushProperty = AvaloniaProperty.Register<PolarChart, IBrush?>(nameof(GridLineBrush), Brushes.LightGray);
    public static readonly StyledProperty<double> GridLineStrokeThicknessProperty = AvaloniaProperty.Register<PolarChart, double>(nameof(GridLineStrokeThickness), 1, validate: v => double.IsFinite(v) && v >= 0 && v <= 32);
    public static readonly StyledProperty<IEnumerable<IBrush>?> ColorPaletteProperty = AvaloniaProperty.Register<PolarChart, IEnumerable<IBrush>?>(nameof(ColorPalette));
    public static readonly StyledProperty<bool> IsTooltipEnabledProperty = AvaloniaProperty.Register<PolarChart, bool>(nameof(IsTooltipEnabled), true);
    public static readonly StyledProperty<Func<ChartPointHit, string>?> TooltipFormatterProperty = AvaloniaProperty.Register<PolarChart, Func<ChartPointHit, string>?>(nameof(TooltipFormatter));
    public static readonly StyledProperty<Func<ChartPointHit, string>?> LabelFormatterProperty = AvaloniaProperty.Register<PolarChart, Func<ChartPointHit, string>?>(nameof(LabelFormatter));
    public static readonly DirectProperty<PolarChart, Rect> PlotAreaProperty = AvaloniaProperty.RegisterDirect<PolarChart, Rect>(nameof(PlotArea), c => c.PlotArea);
    private static readonly IBrush[] DefaultPalette = [Brush.Parse("#4385F4"), Brush.Parse("#19A798"), Brush.Parse("#F2A33A"), Brush.Parse("#9B72E8"), Brush.Parse("#E36D8F"), Brush.Parse("#44B4C4")];
    private readonly HashSet<PolarSeries> _observed = [];
    private readonly HashSet<ChartAxis> _observedAxes = [];
    private INotifyCollectionChanged? _sourceNotifications, _labelNotifications;
    private bool _attached, _dirty = true, _building, _rendering, _renderRequestQueued, _selectionUpdateQueued;
    private PolarScene? _scene, _renderScene;
    private Rect _plotArea;
    private Size _layoutSize;
    private ChartPointHit? _hovered, _keyboardPoint;
    public PolarChart()
    {
        SetCurrentValue(AngularAxisProperty, new NumericAxis());
        SetCurrentValue(RadialAxisProperty, new NumericAxis());
        ClipToBounds = true; Focusable = true;
        Series.CollectionChanged += CollectionChanged;
    }
    public ObservableCollection<PolarSeries> Series { get; } = [];
    public IEnumerable<PolarSeries>? SeriesSource { get => GetValue(SeriesSourceProperty); set => SetValue(SeriesSourceProperty, value); }
    /// <summary>Linear numeric angular domain in AngleUnit; an unset maximum is one full turn.</summary>
    public ChartAxis AngularAxis { get => GetValue(AngularAxisProperty); set => SetValue(AngularAxisProperty, value); }
    /// <summary>Shared nonnegative linear radial domain. A RadarSeries.MaxValue can explicitly override its maximum.</summary>
    public ChartAxis RadialAxis { get => GetValue(RadialAxisProperty); set => SetValue(RadialAxisProperty, value); }
    public IReadOnlyList<string>? AxisLabels { get => GetValue(AxisLabelsProperty); set => SetValue(AxisLabelsProperty, value); }
    public ChartAngleUnit AngleUnit { get => GetValue(AngleUnitProperty); set => SetValue(AngleUnitProperty, value); }
    public double StartAngle { get => GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    public double SweepAngle { get => GetValue(SweepAngleProperty); set => SetValue(SweepAngleProperty, value); }
    public bool Clockwise { get => GetValue(ClockwiseProperty); set => SetValue(ClockwiseProperty, value); }
    public bool WrapAngles { get => GetValue(WrapAnglesProperty); set => SetValue(WrapAnglesProperty, value); }
    public PolarGridShape GridShape { get => GetValue(GridShapeProperty); set => SetValue(GridShapeProperty, value); }
    public int GridLevels { get => GetValue(GridLevelsProperty); set => SetValue(GridLevelsProperty, value); }
    public bool ShowGridLines { get => GetValue(ShowGridLinesProperty); set => SetValue(ShowGridLinesProperty, value); }
    public bool ShowAxisLabels { get => GetValue(ShowAxisLabelsProperty); set => SetValue(ShowAxisLabelsProperty, value); }
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    public bool ShowDataLabels { get => GetValue(ShowDataLabelsProperty); set => SetValue(ShowDataLabelsProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    public IBrush? AxisBrush { get => GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
    public IBrush? GridLineBrush { get => GetValue(GridLineBrushProperty); set => SetValue(GridLineBrushProperty, value); }
    public double GridLineStrokeThickness { get => GetValue(GridLineStrokeThicknessProperty); set => SetValue(GridLineStrokeThicknessProperty, value); }
    public IEnumerable<IBrush>? ColorPalette { get => GetValue(ColorPaletteProperty); set => SetValue(ColorPaletteProperty, value); }
    public bool IsTooltipEnabled { get => GetValue(IsTooltipEnabledProperty); set => SetValue(IsTooltipEnabledProperty, value); }
    public Func<ChartPointHit, string>? TooltipFormatter { get => GetValue(TooltipFormatterProperty); set => SetValue(TooltipFormatterProperty, value); }
    public Func<ChartPointHit, string>? LabelFormatter { get => GetValue(LabelFormatterProperty); set => SetValue(LabelFormatterProperty, value); }
    public Rect PlotArea => _plotArea;
    public Point PolarCenter => GetScene().Center;
    public double PolarRadius => GetScene().Radius;
    public IReadOnlyList<PolarPointInfo> GetPoints() => GetScene().Points;
    public IReadOnlyList<PolarSectorInfo> GetSectors() => GetScene().Sectors;
    public override void ResetViewport() { VerifyAccess(); AngularAxis.Minimum = AngularAxis.Maximum = null; RadialAxis.Minimum = RadialAxis.Maximum = null; }
    private IEnumerable<PolarSeries> ActiveSeries => SeriesSource ?? Series;
    protected override Size MeasureOverride(Size availableSize) => new(double.IsFinite(availableSize.Width) ? Math.Min(520, availableSize.Width) : 520, double.IsFinite(availableSize.Height) ? Math.Min(360, availableSize.Height) : 360);
    protected override Size ArrangeOverride(Size finalSize)
    { if (_layoutSize != finalSize) { _layoutSize = finalSize; _dirty = true; } var result = base.ArrangeOverride(finalSize); _ = GetScene(); return result; }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); _attached = true; SynchronizeSubscriptions(); InvalidatePolar(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { _attached = false; SynchronizeSubscriptions(); _hovered = _keyboardPoint = null; base.OnDetachedFromVisualTree(e); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlotAreaProperty) return;
        if (SelectionPropertyChanged(change)) return;
        if (change.Property == SeriesSourceProperty || change.Property == AngularAxisProperty || change.Property == RadialAxisProperty || change.Property == AxisLabelsProperty) SynchronizeSubscriptions();
        if (change.Property == BoundsProperty && Bounds.Size == _layoutSize)
        { if (_pendingSelection is not null && !_rendering && Bounds.Width > 0 && Bounds.Height > 0) { _dirty = true; _ = GetScene(); } RequestPolarRender(); return; }
        InvalidatePolar();
    }
    private void InvalidatePolar() { _dirty = true; RequestPolarRender(); if (!_rendering) InvalidateArrange(); }
    private void RequestPolarRender()
    {
        if (!_rendering) { InvalidateVisual(); return; }
        if (_renderRequestQueued) return;
        _renderRequestQueued = true;
        Dispatcher.UIThread.Post(() => { _renderRequestQueued = false; InvalidateVisual(); InvalidateArrange(); });
    }
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { VerifyAccess(); SynchronizeSubscriptions(); InvalidatePolar(); }
    private void SeriesChanged(object? sender, EventArgs e) { VerifyAccess(); InvalidatePolar(); }
    private void AxisChanged(object? sender, ChartAxisChangedEventArgs e) { VerifyAccess(); InvalidatePolar(); }
    private void LabelsChanged(object? sender, NotifyCollectionChangedEventArgs e) { VerifyAccess(); InvalidatePolar(); }
    private void SynchronizeSubscriptions()
    {
        var desired = _attached ? ActiveSeries.Distinct().ToHashSet() : [];
        foreach (var series in _observed.ToArray()) if (!desired.Contains(series))
        { series.Changed -= SeriesChanged; _observed.Remove(series); if (ReferenceEquals(((ILogical)series).LogicalParent, this)) LogicalChildren.Remove(series); }
        foreach (var series in desired) if (_observed.Add(series))
        { if (((ILogical)series).LogicalParent is null) LogicalChildren.Add(series); series.Changed += SeriesChanged; }
        var axes = _attached ? new[] { AngularAxis, RadialAxis }.Where(a => a is not null).ToHashSet() : [];
        foreach (var axis in _observedAxes.ToArray()) if (!axes.Contains(axis))
        { axis.Changed -= AxisChanged; _observedAxes.Remove(axis); if (ReferenceEquals(((ILogical)axis).LogicalParent, this)) LogicalChildren.Remove(axis); }
        foreach (var axis in axes) if (_observedAxes.Add(axis))
        { if (((ILogical)axis).LogicalParent is null) LogicalChildren.Add(axis); axis.Changed += AxisChanged; }
        var source = _attached ? SeriesSource as INotifyCollectionChanged : null;
        if (!ReferenceEquals(source, _sourceNotifications)) { if (_sourceNotifications is not null) _sourceNotifications.CollectionChanged -= CollectionChanged; _sourceNotifications = source; if (source is not null) source.CollectionChanged += CollectionChanged; }
        var labels = _attached ? AxisLabels as INotifyCollectionChanged : null;
        if (!ReferenceEquals(labels, _labelNotifications)) { if (_labelNotifications is not null) _labelNotifications.CollectionChanged -= LabelsChanged; _labelNotifications = labels; if (labels is not null) labels.CollectionChanged += LabelsChanged; }
    }
    private PolarScene GetScene()
    {
        VerifyAccess();
        if (_renderScene is { } rendered) return rendered;
        if (_building) return _scene ?? PolarChartGeometry.Build([], default, 0);
        if (!_attached && !_rendering) _dirty = true;
        if (!_dirty && _scene is { } cached) return cached;
        _building = true;
        try
        {
            _dirty = false;
            ValidateAxis(AngularAxis); ValidateAxis(RadialAxis);
            var size = _layoutSize.Width > 0 && _layoutSize.Height > 0 ? _layoutSize : Bounds.Size;
            var reserve = ShowAxisLabels ? 48 : 0;
            var left = Padding.Left + 8 + reserve; var top = Padding.Top + 8 + reserve + (string.IsNullOrEmpty(Title) ? 0 : FontSize * 1.6 + 8);
            var right = size.Width - Padding.Right - 8 - reserve;
            var bottom = size.Height - Padding.Bottom - 8 - reserve - (ShowLegend ? FontSize * 2 + 10 : 0);
            SetAndRaise(PlotAreaProperty, ref _plotArea, new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)));
            var sources = ActiveSeries.Where(s => s.IsVisible).Distinct().Select(s => (Series: s, Data: s.CreateSnapshot())).ToArray();
            var scene = PolarChartGeometry.Build(sources, _plotArea.Center, Math.Min(_plotArea.Width, _plotArea.Height) / 2,
                AngularAxis.Minimum ?? 0, AngularAxis.Maximum ?? (AngleUnit == ChartAngleUnit.Degrees ? 360 : Math.PI * 2),
                RadialAxis.Minimum ?? 0, RadialAxis.Maximum, StartAngle, SweepAngle, Clockwise, AngleUnit, WrapAngles, AngularAxis.IsInversed, RadialAxis.IsInversed);
            var palette = ColorPalette?.Where(b => b is not null).ToArray(); if (palette is null || palette.Length == 0) palette = DefaultPalette;
            var groups = scene.Series.Select((s, i) => s with
            {
                Points = s.Points.Select(p => p with { Fill = ResolveFill(p.Data.ColorValue) ?? s.Series.Fill ?? s.Series.Stroke ?? palette[i % palette.Length] }).ToArray(),
                Sectors = s.Sectors.Select(p => p with { Fill = ResolveFill(p.Data.ColorValue) ?? s.Series.Fill ?? palette[p.Data.SourceIndex % palette.Length] }).ToArray()
            }).ToArray();
            _scene = new PolarScene(scene.Center, scene.Radius, scene.RadiusMinimum, scene.RadiusMaximum, scene.Categories, groups);
            ReconcileSelection(CurrentHits(_scene));
            _hovered = _hovered is null ? null : CurrentHits(_scene).LastOrDefault(p => SameIdentity(p, _hovered));
            _keyboardPoint = _keyboardPoint is null ? null : CurrentHits(_scene).LastOrDefault(p => SameIdentity(p, _keyboardPoint));
            return _scene;
        }
        catch { _dirty = true; throw; }
        finally { _building = false; }
    }
    private static void ValidateAxis(ChartAxis axis)
    {
        if (axis.GetType() != typeof(ChartAxis) && axis is not NumericAxis) throw new NotSupportedException("Polar axes currently support linear numeric domains only.");
        if (axis.GetMergedScaleBreaks().Count > 0) throw new NotSupportedException("Polar scale breaks are not supported.");
    }
    private static IBrush? ResolveFill(object? value) => value switch { IBrush b => b, Color c => new SolidColorBrush(c), string s when Color.TryParse(s, out var c) => new SolidColorBrush(c), _ => null };
    private static ChartPointHit[] CurrentHits(PolarScene scene) => scene.Series.SelectMany(s =>
        s.Series is PolarAreaSeries or NightingaleRoseSeries or RadialBarSeries
            ? s.Sectors.Where(p => p.IsVisible).Select(p => p.Hit)
            : s.Points.Where(p => p.IsVisible).Select(p => p.Hit)).ToArray();
    public ChartPointHit? HitTest(Point position, double tolerance = 8)
    {
        VerifyAccess();
        if (!double.IsFinite(tolerance) || tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        var scene = GetScene(); if (!new Rect(Bounds.Size).Contains(position) || Math.Sqrt(DistanceSquared(position, scene.Center)) > scene.Radius) return null;
        foreach (var group in scene.Series.Reverse())
        {
            var sector = group.Sectors.LastOrDefault(s => s.IsVisible && PolarChartGeometry.Contains(s, position)); if (sector is not null) return sector.Hit;
            if (group.Series is PolarAreaSeries or NightingaleRoseSeries or RadialBarSeries) continue;
            var point = group.Points.Where(p => p.IsVisible).OrderBy(p => DistanceSquared(p.Position, position)).FirstOrDefault();
            if (point is not null && Math.Sqrt(DistanceSquared(point.Position, position)) <= tolerance + (group.Series.ShowMarkers ? group.Series.MarkerSize / 2 : 0)) return point.Hit;
            if (group.Series is PolarScatterSeries || group.Series.StrokeThickness <= 0) continue;
            foreach (var path in group.Paths.Reverse())
            {
                var points = path.Points;
                for (var i = 1; i < points.Count + (path.IsClosed ? 1 : 0); i++)
                {
                    var a = points[(i - 1) % points.Count]; var b = points[i % points.Count];
                    if (SegmentDistanceSquared(position, a.Position, b.Position) > Math.Pow(tolerance + group.Series.StrokeThickness / 2, 2)) continue;
                    return DistanceSquared(position, a.Position) <= DistanceSquared(position, b.Position) ? a.Hit : b.Hit;
                }
            }
        }
        return null;
    }
    private static double SegmentDistanceSquared(Point p, Point a, Point b)
        => PolarChartGeometry.SegmentDistanceSquared(p, a, b);
    private static double DistanceSquared(Point a, Point b) => Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2);
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); _hovered = IsTooltipEnabled ? HitTest(e.GetPosition(this)) : null; RequestPolarRender(); }
    protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); _hovered = null; RequestPolarRender(); }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    { base.OnPointerPressed(e); if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && HitTest(e.GetPosition(this)) is { } hit && SelectPointCore(hit, true, ChartSelectionChangeReason.Pointer)) { Focus(); e.Handled = true; } }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); var visible = CurrentHits(GetScene()); if (visible.Length == 0) return;
        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End)
        {
            var i = Array.FindIndex(visible, p => SameIdentity(p, _keyboardPoint));
            i = e.Key switch { Key.Home => 0, Key.End => visible.Length - 1, Key.Left => i < 0 ? visible.Length - 1 : (i - 1 + visible.Length) % visible.Length, _ => (i + 1) % visible.Length };
            _hovered = _keyboardPoint = visible[i]; e.Handled = true; RequestPolarRender();
        }
        else if (e.Key is Key.Space or Key.Enter && _keyboardPoint is { } point) { SelectPointCore(point, true, ChartSelectionChangeReason.Keyboard); e.Handled = true; }
        else if (e.Key == Key.Escape) { ClearSelection(); _hovered = _keyboardPoint = null; e.Handled = true; }
    }
    private FormattedText Text(string text, double? size = null) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight), size ?? FontSize, Foreground ?? Brushes.Black);
}

/// <summary>A polar host whose grid defaults to category-aligned polygons.</summary>
public sealed class RadarChart : PolarChart
{
    static RadarChart() => GridShapeProperty.OverrideDefaultValue<RadarChart>(PolarGridShape.Polygon);
    protected override Type StyleKeyOverride => typeof(PolarChart);
}
