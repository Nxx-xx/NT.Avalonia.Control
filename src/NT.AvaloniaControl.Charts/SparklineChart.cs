using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public enum SparklineType { Line, Area, Bar, WinLoss }

/// <summary>Data mapping and source identity owned by a compact sparkline.</summary>
public sealed class SparklineSeries : ChartSeries;

/// <summary>A source slot projected into a sparkline. Missing slots remain present but invisible.</summary>
public sealed record SparklinePointInfo(SparklineSeries Series, ChartDataPoint Data, Point Position, Rect BarBounds)
{
    public bool IsVisible { get; init; }
    public bool IsFirst { get; init; }
    public bool IsLast { get; init; }
    public bool IsMinimum { get; init; }
    public bool IsMaximum { get; init; }
    public ChartPointHit Hit => ChartPointHit.FromData(Series, Data, Position);
}

/// <summary>Compact source-order trends without axes. CategoryPath supplies metadata, not horizontal spacing.</summary>
public sealed partial class SparklineChart : ChartBase
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<SparklineChart, IEnumerable?>(nameof(ItemsSource));
    public static readonly StyledProperty<string?> ValuePathProperty = AvaloniaProperty.Register<SparklineChart, string?>(nameof(ValuePath));
    public static readonly StyledProperty<string?> CategoryPathProperty = AvaloniaProperty.Register<SparklineChart, string?>(nameof(CategoryPath));
    public static readonly StyledProperty<string?> IdPathProperty = AvaloniaProperty.Register<SparklineChart, string?>(nameof(IdPath));
    public static readonly StyledProperty<SparklineType> SparklineTypeProperty = AvaloniaProperty.Register<SparklineChart, SparklineType>(nameof(SparklineType), validate: Enum.IsDefined);
    public static readonly StyledProperty<double?> MinimumProperty = AvaloniaProperty.Register<SparklineChart, double?>(nameof(Minimum), validate: v => v is null || double.IsFinite(v.Value));
    public static readonly StyledProperty<double?> MaximumProperty = AvaloniaProperty.Register<SparklineChart, double?>(nameof(Maximum), validate: v => v is null || double.IsFinite(v.Value));
    public static readonly StyledProperty<double> BaselineProperty = AvaloniaProperty.Register<SparklineChart, double>(nameof(Baseline), validate: double.IsFinite);
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<SparklineChart, double>(nameof(StrokeThickness), 2, validate: v => double.IsFinite(v) && v >= 0);
    public static readonly StyledProperty<double> MarkerSizeProperty = AvaloniaProperty.Register<SparklineChart, double>(nameof(MarkerSize), 5, validate: v => double.IsFinite(v) && v >= 0);
    public static readonly StyledProperty<double> BarSpacingProperty = AvaloniaProperty.Register<SparklineChart, double>(nameof(BarSpacing), .2, validate: v => double.IsFinite(v) && v >= 0 && v < 1);
    public static readonly StyledProperty<bool> ShowMarkersProperty = AvaloniaProperty.Register<SparklineChart, bool>(nameof(ShowMarkers));
    public static readonly StyledProperty<bool> ShowMinMaxProperty = AvaloniaProperty.Register<SparklineChart, bool>(nameof(ShowMinMax), true);
    public static readonly StyledProperty<bool> ShowFirstLastProperty = AvaloniaProperty.Register<SparklineChart, bool>(nameof(ShowFirstLast));
    public static readonly StyledProperty<bool> ShowBaselineProperty = AvaloniaProperty.Register<SparklineChart, bool>(nameof(ShowBaseline));
    public static readonly StyledProperty<IBrush?> LineBrushProperty = AvaloniaProperty.Register<SparklineChart, IBrush?>(nameof(LineBrush));
    public static readonly StyledProperty<IBrush?> AreaFillProperty = AvaloniaProperty.Register<SparklineChart, IBrush?>(nameof(AreaFill));
    public static readonly StyledProperty<IBrush?> BarBrushProperty = AvaloniaProperty.Register<SparklineChart, IBrush?>(nameof(BarBrush));
    public static readonly StyledProperty<IBrush?> WinBrushProperty = AvaloniaProperty.Register<SparklineChart, IBrush?>(nameof(WinBrush));
    public static readonly StyledProperty<IBrush?> LossBrushProperty = AvaloniaProperty.Register<SparklineChart, IBrush?>(nameof(LossBrush));
    public static readonly StyledProperty<IBrush?> ZeroBrushProperty = AvaloniaProperty.Register<SparklineChart, IBrush?>(nameof(ZeroBrush));
    public static readonly StyledProperty<IBrush?> MinBrushProperty = AvaloniaProperty.Register<SparklineChart, IBrush?>(nameof(MinBrush));
    public static readonly StyledProperty<IBrush?> MaxBrushProperty = AvaloniaProperty.Register<SparklineChart, IBrush?>(nameof(MaxBrush));
    public static readonly StyledProperty<IBrush?> FirstBrushProperty = AvaloniaProperty.Register<SparklineChart, IBrush?>(nameof(FirstBrush));
    public static readonly StyledProperty<IBrush?> LastBrushProperty = AvaloniaProperty.Register<SparklineChart, IBrush?>(nameof(LastBrush));
    public static readonly StyledProperty<IBrush?> BaselineBrushProperty = AvaloniaProperty.Register<SparklineChart, IBrush?>(nameof(BaselineBrush));
    public static readonly StyledProperty<bool> IsTooltipEnabledProperty = AvaloniaProperty.Register<SparklineChart, bool>(nameof(IsTooltipEnabled), true);
    public static readonly StyledProperty<Func<ChartPointHit, string>?> TooltipFormatterProperty = AvaloniaProperty.Register<SparklineChart, Func<ChartPointHit, string>?>(nameof(TooltipFormatter));
    public static readonly DirectProperty<SparklineChart, Rect> PlotAreaProperty = AvaloniaProperty.RegisterDirect<SparklineChart, Rect>(nameof(PlotArea), c => c.PlotArea);
    public static readonly DirectProperty<SparklineChart, ChartPointHit?> ActivePointProperty = AvaloniaProperty.RegisterDirect<SparklineChart, ChartPointHit?>(nameof(ActivePoint), c => c.ActivePoint);
    private static readonly IBrush DefaultLine = Brush.Parse("#4385F4"), DefaultArea = Brush.Parse("#404385F4"), DefaultWin = Brush.Parse("#16A085"), DefaultLoss = Brush.Parse("#D94D65");
    private bool _attached, _dirty = true, _building, _rendering, _renderQueued;
    private bool _seriesSubscribed, _synchronizingSeries, _seriesSynchronizationPending;
    private long _attachmentRevision;
    private Rect _plotArea;
    private Size _layoutSize;
    private SparklineScene? _scene, _renderScene;
    private ChartPointHit? _activePoint;

    public SparklineChart() { Focusable = true; ClipToBounds = true; }
    public SparklineSeries Series { get; } = new();
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public string? ValuePath { get => GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
    public string? CategoryPath { get => GetValue(CategoryPathProperty); set => SetValue(CategoryPathProperty, value); }
    public string? IdPath { get => GetValue(IdPathProperty); set => SetValue(IdPathProperty, value); }
    public SparklineType SparklineType { get => GetValue(SparklineTypeProperty); set => SetValue(SparklineTypeProperty, value); }
    /// <summary>Optional raw-value limit. WinLoss instead uses the fixed sign domain [-1, 1].</summary>
    public double? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Baseline { get => GetValue(BaselineProperty); set => SetValue(BaselineProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public double MarkerSize { get => GetValue(MarkerSizeProperty); set => SetValue(MarkerSizeProperty, value); }
    public double BarSpacing { get => GetValue(BarSpacingProperty); set => SetValue(BarSpacingProperty, value); }
    public bool ShowMarkers { get => GetValue(ShowMarkersProperty); set => SetValue(ShowMarkersProperty, value); }
    public bool ShowMinMax { get => GetValue(ShowMinMaxProperty); set => SetValue(ShowMinMaxProperty, value); }
    public bool ShowFirstLast { get => GetValue(ShowFirstLastProperty); set => SetValue(ShowFirstLastProperty, value); }
    public bool ShowBaseline { get => GetValue(ShowBaselineProperty); set => SetValue(ShowBaselineProperty, value); }
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public IBrush? AreaFill { get => GetValue(AreaFillProperty); set => SetValue(AreaFillProperty, value); }
    public IBrush? BarBrush { get => GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }
    public IBrush? WinBrush { get => GetValue(WinBrushProperty); set => SetValue(WinBrushProperty, value); }
    public IBrush? LossBrush { get => GetValue(LossBrushProperty); set => SetValue(LossBrushProperty, value); }
    public IBrush? ZeroBrush { get => GetValue(ZeroBrushProperty); set => SetValue(ZeroBrushProperty, value); }
    public IBrush? MinBrush { get => GetValue(MinBrushProperty); set => SetValue(MinBrushProperty, value); }
    public IBrush? MaxBrush { get => GetValue(MaxBrushProperty); set => SetValue(MaxBrushProperty, value); }
    public IBrush? FirstBrush { get => GetValue(FirstBrushProperty); set => SetValue(FirstBrushProperty, value); }
    public IBrush? LastBrush { get => GetValue(LastBrushProperty); set => SetValue(LastBrushProperty, value); }
    public IBrush? BaselineBrush { get => GetValue(BaselineBrushProperty); set => SetValue(BaselineBrushProperty, value); }
    public bool IsTooltipEnabled { get => GetValue(IsTooltipEnabledProperty); set => SetValue(IsTooltipEnabledProperty, value); }
    public Func<ChartPointHit, string>? TooltipFormatter { get => GetValue(TooltipFormatterProperty); set => SetValue(TooltipFormatterProperty, value); }
    public Rect PlotArea { get { _ = GetScene(); return _plotArea; } }
    public double EffectiveMinimum => GetScene().Minimum;
    public double EffectiveMaximum => GetScene().Maximum;
    /// <summary>False for an inverted or explicitly equal domain; geometry remains empty until bindings supply a valid range.</summary>
    public bool IsDomainValid => GetScene().IsDomainValid;
    public double BaselinePosition => GetScene().Baseline;
    public ChartPointHit? ActivePoint { get { _ = GetScene(); return _activePoint; } }
    public IReadOnlyList<SparklinePointInfo> GetPoints() => GetScene().Points;
    public override void ResetViewport() { VerifyAccess(); SetCurrentValue(MinimumProperty, null); SetCurrentValue(MaximumProperty, null); }
    protected override Size MeasureOverride(Size availableSize) => new(double.IsFinite(availableSize.Width) ? Math.Min(160, availableSize.Width) : 160, double.IsFinite(availableSize.Height) ? Math.Min(40, availableSize.Height) : 40);
    protected override Size ArrangeOverride(Size finalSize)
    { if (_layoutSize != finalSize) { _layoutSize = finalSize; _dirty = true; } var result = base.ArrangeOverride(finalSize); _ = GetScene(); return result; }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var revision = ++_attachmentRevision;
        _attached = true; _dirty = true;
        base.OnAttachedToVisualTree(e);
        if (!_attached || revision != _attachmentRevision) return;
        SynchronizeSeriesOwnership();
        if (_attached && revision == _attachmentRevision) InvalidateSparkline();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var revision = ++_attachmentRevision;
        _attached = false; _dirty = true;
        if (!_attached && revision == _attachmentRevision) SetActive(null);
        SynchronizeSeriesOwnership();
        base.OnDetachedFromVisualTree(e);
    }
    private void SynchronizeSeriesOwnership()
    {
        _seriesSynchronizationPending = true;
        if (_synchronizingSeries) return;
        _synchronizingSeries = true;
        try
        {
            while (_seriesSynchronizationPending)
            {
                _seriesSynchronizationPending = false;
                if (_attached)
                {
                    // Observe before inheriting DataContext. Binding callbacks may
                    // detach/reparent us while LogicalChildren.Add is still running;
                    // let that operation finish before reconciling its final parent.
                    if (!_seriesSubscribed) { _seriesSubscribed = true; Series.Changed += SeriesChanged; }
                    if (_seriesSynchronizationPending || !_attached) continue;
                    if (((ILogical)Series).LogicalParent is null)
                    {
                        // StyledElement applies inherited values before publishing its
                        // logical parent. Let bindings settle before putting Series in
                        // the child collection, so a nested host reattach cannot visit
                        // a child whose SetParent operation is only half complete.
                        ((ISetInheritanceParent)Series).SetParent(this);
                        if (_seriesSynchronizationPending || !_attached) continue;
                        LogicalChildren.Add(Series);
                    }
                }
                else
                {
                    if (_seriesSubscribed) { _seriesSubscribed = false; Series.Changed -= SeriesChanged; }
                    if (_seriesSynchronizationPending || _attached) continue;
                    if (ReferenceEquals(((ILogical)Series).LogicalParent, this)) LogicalChildren.Remove(Series);
                    else if (((ILogical)Series).LogicalParent is null) ((ISetInheritanceParent)Series).SetParent(null);
                }
            }
        }
        finally { _synchronizingSeries = false; }
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlotAreaProperty || change.Property == ActivePointProperty) return;
        if (change.Property == ItemsSourceProperty) Series.ItemsSource = ItemsSource;
        else if (change.Property == ValuePathProperty) Series.ValuePath = ValuePath;
        else if (change.Property == CategoryPathProperty) Series.CategoryPath = CategoryPath;
        else if (change.Property == IdPathProperty) Series.IdPath = IdPath;
        InvalidateSparkline();
    }
    private void SeriesChanged(object? sender, EventArgs e) => InvalidateSparkline();
    private void InvalidateSparkline()
    {
        _dirty = true;
        if (!_rendering) { InvalidateVisual(); return; }
        if (_renderQueued) return;
        _renderQueued = true; Dispatcher.UIThread.Post(() => { _renderQueued = false; InvalidateVisual(); });
    }
    private SparklineScene GetScene()
    {
        VerifyAccess(); if (_renderScene is { } captured) return captured;
        if (_building) return _scene ?? new([], 0, 1, 0);
        if (!_attached && !_rendering) _dirty = true;
        if (!_dirty && _scene is { } cached) return cached;
        _building = true;
        try
        {
            _dirty = false;
            var size = _layoutSize.Width > 0 && _layoutSize.Height > 0 ? _layoutSize : Bounds.Size;
            var inset = Math.Max(StrokeThickness / 2, MarkerSize / 2);
            var left = Math.Clamp(Padding.Left + inset, 0, size.Width); var top = Math.Clamp(Padding.Top + inset, 0, size.Height);
            var right = Math.Clamp(size.Width - Padding.Right - inset, left, size.Width); var bottom = Math.Clamp(size.Height - Padding.Bottom - inset, top, size.Height);
            var plot = new Rect(left, top, right - left, bottom - top);
            SetAndRaise(PlotAreaProperty, ref _plotArea, plot);
            var data = Series.CreateSnapshot().Points;
            var finite = data.Where(p => !p.IsEmpty && double.IsFinite(p.Point.Y)).ToArray();
            var minimum = finite.Length > 0 ? finite.Min(p => p.Point.Y) : 0;
            var maximum = finite.Length > 0 ? finite.Max(p => p.Point.Y) : 1;
            var rawMinimum = minimum; var rawMaximum = maximum;
            if (SparklineType is SparklineType.Area or SparklineType.Bar) { minimum = Math.Min(minimum, Baseline); maximum = Math.Max(maximum, Baseline); }
            minimum = Minimum ?? minimum; maximum = Maximum ?? maximum;
            if (SparklineType == SparklineType.WinLoss) { minimum = -1; maximum = 1; }
            var domainValid = minimum < maximum || minimum == maximum && (Minimum is null || Maximum is null);
            if (domainValid && minimum == maximum) ExpandEqualDomain(ref minimum, ref maximum);
            var baseline = domainValid ? plot.Bottom - plot.Height * Normalize(SparklineType == SparklineType.WinLoss ? 0 : Baseline, minimum, maximum) : plot.Center.Y;
            int? first = finite.Length > 0 ? finite[0].SourceIndex : null; int? last = finite.Length > 0 ? finite[^1].SourceIndex : null;
            var bars = SparklineType is SparklineType.Bar or SparklineType.WinLoss;
            var count = data.Count; var result = new SparklinePointInfo[count];
            for (var i = 0; i < count; i++)
            {
                var p = data[i]; var visible = domainValid && !p.IsEmpty && double.IsFinite(p.Point.Y) && plot.Width > 0 && plot.Height > 0;
                var x = bars ? plot.Left + plot.Width * ((i + .5) / count) : count == 1 ? plot.Center.X : plot.Left + plot.Width * (i / (double)(count - 1));
                var value = SparklineType == SparklineType.WinLoss ? p.Point.Y.CompareTo(Baseline) : p.Point.Y;
                var y = visible ? plot.Bottom - plot.Height * Normalize(value, minimum, maximum) : baseline;
                var width = count > 0 ? plot.Width / count * (1 - BarSpacing) : 0;
                var height = Math.Abs(y - baseline);
                // A zero has a visible, bounded 1-DIP band in bar families, never a fabricated nonzero value.
                var zeroTop = Math.Clamp(Math.Floor(baseline), plot.Top, Math.Max(plot.Top, plot.Bottom - Math.Min(1, plot.Height)));
                var rect = bars ? new Rect(x - width / 2, height == 0 ? zeroTop : Math.Min(y, baseline), width, height == 0 ? Math.Min(1, plot.Height) : height) : default;
                result[i] = new(Series, p, new Point(x, y), rect) { IsVisible = visible, IsFirst = visible && i == first, IsLast = visible && i == last, IsMinimum = visible && p.Point.Y == rawMinimum, IsMaximum = visible && p.Point.Y == rawMaximum };
            }
            _scene = new(result, minimum, maximum, baseline, domainValid);
            if (_activePoint is { } active) SetActive(result.FirstOrDefault(p => p.IsVisible && SameIdentity(p.Hit, active))?.Hit);
            return _scene;
        }
        catch { _dirty = true; throw; }
        finally { _building = false; }
    }
    private static void ExpandEqualDomain(ref double minimum, ref double maximum)
    {
        var delta = Math.Max(1, Math.Abs(minimum) * .05);
        var lower = minimum - delta; var upper = maximum + delta;
        minimum = double.IsFinite(lower) ? lower : minimum;
        maximum = double.IsFinite(upper) ? upper : maximum;
    }
    internal static double Normalize(double value, double minimum, double maximum)
    {
        if (value <= minimum) return 0;
        if (value >= maximum) return 1;
        var scale = Math.Max(Math.Abs(value), Math.Max(Math.Abs(minimum), Math.Abs(maximum)));
        if (scale == 0) return .5;
        return Math.Clamp((value / scale - minimum / scale) / (maximum / scale - minimum / scale), 0, 1);
    }
    private bool SameIdentity(ChartPointHit a, ChartPointHit b) => ReferenceEquals(a.Series, b.Series) &&
        (string.IsNullOrWhiteSpace(Series.IdPath) ? ReferenceEquals(a.StableId, b.StableId) : Equals(a.StableId, b.StableId));
    private void SetActive(ChartPointHit? hit) => SetAndRaise(ActivePointProperty, ref _activePoint, hit);
    public ChartPointHit? HitTest(Point position, double tolerance = 6)
    {
        VerifyAccess(); if (!double.IsFinite(tolerance) || tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        var points = GetScene().Points;
        if (!new Rect(Bounds.Size).Contains(position) || !_plotArea.Inflate(tolerance + MarkerSize / 2).Contains(position)) return null;
        if (SparklineType is SparklineType.Bar or SparklineType.WinLoss) return points.LastOrDefault(p => p.IsVisible && p.BarBounds.Contains(position))?.Hit;
        var candidate = points.Where(p => p.IsVisible).OrderBy(p => DistanceSquared(p.Position, position)).FirstOrDefault();
        if (candidate is not null)
        {
            var hasMarker = ShowMarkers || ShowMinMax && (candidate.IsMinimum || candidate.IsMaximum) || ShowFirstLast && (candidate.IsFirst || candidate.IsLast);
            if (DistanceSquared(candidate.Position, position) <= Math.Pow(tolerance + (hasMarker ? MarkerSize / 2 : 0), 2)) return candidate.Hit;
        }
        if (StrokeThickness <= 0) return null;
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1]; var b = points[i]; if (!a.IsVisible || !b.IsVisible) continue;
            if (PolarChartGeometry.SegmentDistanceSquared(position, a.Position, b.Position) <= tolerance * tolerance)
                return DistanceSquared(position, a.Position) <= DistanceSquared(position, b.Position) ? a.Hit : b.Hit;
        }
        return null;
    }
    private static double DistanceSquared(Point a, Point b) => Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2);
    protected override void OnPointerMoved(PointerEventArgs e)
    { base.OnPointerMoved(e); SetActive(HitTest(e.GetPosition(this))); InvalidateVisual(); }
    protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); SetActive(null); InvalidateVisual(); }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    { base.OnPointerPressed(e); if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { SetActive(HitTest(e.GetPosition(this))); Focus(); InvalidateVisual(); } }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) { SetActive(null); e.Handled = true; InvalidateVisual(); return; }
        if (e.Key is not (Key.Left or Key.Right or Key.Home or Key.End)) return;
        var points = GetScene().Points.Where(p => p.IsVisible).ToArray(); if (points.Length == 0) return;
        var index = Array.FindIndex(points, p => _activePoint is { } active && SameIdentity(p.Hit, active));
        index = e.Key switch { Key.Home => 0, Key.End => points.Length - 1, Key.Left => index < 0 ? points.Length - 1 : (index - 1 + points.Length) % points.Length, _ => (index + 1) % points.Length };
        SetActive(points[index].Hit); e.Handled = true; InvalidateVisual();
    }
    private FormattedText Text(string value) => new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight), FontSize, Foreground ?? Brushes.Black);
    private sealed record SparklineScene(IReadOnlyList<SparklinePointInfo> Points, double Minimum, double Maximum, double Baseline, bool IsDomainValid = true);
}
