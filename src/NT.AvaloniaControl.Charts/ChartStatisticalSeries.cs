using Avalonia;

namespace NT.AvaloniaControl.Charts;

public enum ChartErrorMode { Both, Plus, Minus }

/// <summary>Category/value statistics sharing the Cartesian axis, orientation and source identity contracts.</summary>
/// <remarks>Box and error series reserve at least half a category slot at automatic CategoryAxis bounds,
/// including OnTicks, so their bodies and caps fit at the first and last category. Explicit axis bounds
/// continue to control clipping. Existing Gap/BetweenTicks padding is not added twice.</remarks>
public abstract class StatisticalSeries : ComparisonSeries
{
    internal abstract IEnumerable<string?> ExtraPaths { get; }
}

/// <summary>A Tukey box and whiskers, optionally showing original outliers and the arithmetic mean.</summary>
/// <remarks>Each source item is one group. Set SamplesPath to its numeric observations, SummaryPath to a
/// ChartBoxPlotSummary, or supply summaries directly. Hits identify the original group; nested outlier
/// indices remain available through ChartDataPoint.BoxPlot.Outliers. Mutating a nested sample collection
/// requires a group property notification or Refresh().</remarks>
public sealed class BoxPlotSeries : StatisticalSeries
{
    public static readonly StyledProperty<string?> SamplesPathProperty = AvaloniaProperty.Register<BoxPlotSeries, string?>(nameof(SamplesPath));
    public static readonly StyledProperty<string?> SummaryPathProperty = AvaloniaProperty.Register<BoxPlotSeries, string?>(nameof(SummaryPath));
    public static readonly StyledProperty<double> WhiskerMultiplierProperty = AvaloniaProperty.Register<BoxPlotSeries, double>(nameof(WhiskerMultiplier), 1.5, validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<bool> ShowOutliersProperty = AvaloniaProperty.Register<BoxPlotSeries, bool>(nameof(ShowOutliers), true);
    public static readonly StyledProperty<bool> ShowMeanProperty = AvaloniaProperty.Register<BoxPlotSeries, bool>(nameof(ShowMean));
    static BoxPlotSeries() => ShowMarkersProperty.OverrideDefaultValue<BoxPlotSeries>(false);
    public string? SamplesPath { get => GetValue(SamplesPathProperty); set => SetValue(SamplesPathProperty, value); }
    public string? SummaryPath { get => GetValue(SummaryPathProperty); set => SetValue(SummaryPathProperty, value); }
    public double WhiskerMultiplier { get => GetValue(WhiskerMultiplierProperty); set => SetValue(WhiskerMultiplierProperty, value); }
    public bool ShowOutliers { get => GetValue(ShowOutliersProperty); set => SetValue(ShowOutliersProperty, value); }
    public bool ShowMean { get => GetValue(ShowMeanProperty); set => SetValue(ShowMeanProperty, value); }
    internal override IEnumerable<string?> ExtraPaths => [SamplesPath, SummaryPath];
    public override IChartSeriesRenderer Renderer => ChartStatisticalSeriesRenderer.Box;
}

/// <summary>Symmetric or asymmetric absolute error distances around ValuePath, with horizontal/vertical orientation.</summary>
/// <remarks>ErrorPath supplies both distances; LowerErrorPath and UpperErrorPath override their respective
/// side. IntervalPath, or direct ChartErrorInterval items, instead supplies a validated absolute interval.
/// Missing/negative distances become a gap and report DataError; no interval is silently reordered.</remarks>
public sealed class ErrorBarSeries : StatisticalSeries
{
    public static readonly StyledProperty<string?> ErrorPathProperty = AvaloniaProperty.Register<ErrorBarSeries, string?>(nameof(ErrorPath));
    public static readonly StyledProperty<string?> LowerErrorPathProperty = AvaloniaProperty.Register<ErrorBarSeries, string?>(nameof(LowErrorPath));
    public static readonly StyledProperty<string?> UpperErrorPathProperty = AvaloniaProperty.Register<ErrorBarSeries, string?>(nameof(HighErrorPath));
    public static readonly StyledProperty<string?> IntervalPathProperty = AvaloniaProperty.Register<ErrorBarSeries, string?>(nameof(IntervalPath));
    public static readonly StyledProperty<double> CapSizeProperty = AvaloniaProperty.Register<ErrorBarSeries, double>(nameof(CapWidth), 12, validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<string?> LowErrorPathProperty = LowerErrorPathProperty;
    public static readonly StyledProperty<string?> HighErrorPathProperty = UpperErrorPathProperty;
    public static readonly StyledProperty<double> CapWidthProperty = CapSizeProperty;
    public static readonly StyledProperty<ChartErrorMode> ErrorModeProperty = AvaloniaProperty.Register<ErrorBarSeries, ChartErrorMode>(nameof(ErrorMode), validate: Enum.IsDefined);
    static ErrorBarSeries() => ShowMarkersProperty.OverrideDefaultValue<ErrorBarSeries>(false);
    public string? ErrorPath { get => GetValue(ErrorPathProperty); set => SetValue(ErrorPathProperty, value); }
    public string? LowerErrorPath { get => GetValue(LowerErrorPathProperty); set => SetValue(LowerErrorPathProperty, value); }
    public string? UpperErrorPath { get => GetValue(UpperErrorPathProperty); set => SetValue(UpperErrorPathProperty, value); }
    public string? IntervalPath { get => GetValue(IntervalPathProperty); set => SetValue(IntervalPathProperty, value); }
    /// <summary>Alias for LowerErrorPath; both names address the same property.</summary>
    public string? LowErrorPath { get => LowerErrorPath; set => LowerErrorPath = value; }
    /// <summary>Alias for UpperErrorPath; both names address the same property.</summary>
    public string? HighErrorPath { get => UpperErrorPath; set => UpperErrorPath = value; }
    public ChartErrorMode ErrorMode { get => GetValue(ErrorModeProperty); set => SetValue(ErrorModeProperty, value); }
    /// <summary>Total cap length in DIPs, independent of the interval's numeric width.</summary>
    public double CapSize { get => GetValue(CapSizeProperty); set => SetValue(CapSizeProperty, value); }
    /// <summary>Alias for CapSize; both names address the same DIP length property.</summary>
    public double CapWidth { get => CapSize; set => CapSize = value; }
    internal override IEnumerable<string?> ExtraPaths => [ErrorPath, LowerErrorPath, UpperErrorPath, IntervalPath];
    public override IChartSeriesRenderer Renderer => ChartStatisticalSeriesRenderer.Error;
}

/// <summary>Histogram bars use explicit numeric bin boundaries, including unequal-width bins.</summary>
/// <remarks>Bind ChartStatistics.Histogram(...).Bins directly, or map LowerBoundPath, UpperBoundPath and
/// ValuePath. Count/probability/density normalization is performed by ChartStatistics, never inferred by
/// the renderer. Each hit identifies its original bin item, not an invented raw observation.</remarks>
public sealed class HistogramSeries : StatisticalSeries
{
    public static readonly StyledProperty<string?> LowerBoundPathProperty = AvaloniaProperty.Register<HistogramSeries, string?>(nameof(LowerBoundPath));
    public static readonly StyledProperty<string?> UpperBoundPathProperty = AvaloniaProperty.Register<HistogramSeries, string?>(nameof(UpperBoundPath));
    public static readonly StyledProperty<double> BinGapRatioProperty = AvaloniaProperty.Register<HistogramSeries, double>(nameof(BinGapRatio), .03, validate: value => double.IsFinite(value) && value >= 0 && value < 1);
    static HistogramSeries() => ShowMarkersProperty.OverrideDefaultValue<HistogramSeries>(false);
    public string? LowerBoundPath { get => GetValue(LowerBoundPathProperty); set => SetValue(LowerBoundPathProperty, value); }
    public string? UpperBoundPath { get => GetValue(UpperBoundPathProperty); set => SetValue(UpperBoundPathProperty, value); }
    public double BinGapRatio { get => GetValue(BinGapRatioProperty); set => SetValue(BinGapRatioProperty, value); }
    internal override IEnumerable<string?> ExtraPaths => [LowerBoundPath, UpperBoundPath];
    public override IChartSeriesRenderer Renderer => ChartStatisticalSeriesRenderer.Histogram;
}

/// <summary>A nonnegative density curve, optionally filled to zero, accepting ChartPoint samples or mapped rows.</summary>
/// <remarks>Bind ChartStatistics.KernelDensity(...).Points. Hit testing returns the nearest original
/// density sample on a visible segment; it does not invent an observation from the estimated curve.</remarks>
public sealed class DensitySeries : ChartSeries
{
    public static readonly StyledProperty<bool> FillUnderCurveProperty = AvaloniaProperty.Register<DensitySeries, bool>(nameof(FillUnderCurve), true);
    static DensitySeries() => ShowMarkersProperty.OverrideDefaultValue<DensitySeries>(false);
    public bool FillUnderCurve { get => GetValue(FillUnderCurveProperty); set => SetValue(FillUnderCurveProperty, value); }
    public override IChartSeriesRenderer Renderer => ChartStatisticalSeriesRenderer.Density;
}
