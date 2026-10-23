using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Stable ordering of comparison categories, preserving each original source identity.</summary>
public enum ChartSeriesSortMode { Source, XAscending, ValueAscending, ValueDescending }

internal interface IChartComparisonSeries
{
    Orientation Orientation { get; }
    double Baseline { get; }
    double? BarWidth { get; }
    double WidthRatio { get; }
    ChartSeriesSortMode SortMode { get; }
}

/// <summary>Shared category, direction, baseline and width options for comparison series.</summary>
/// <remarks>CategoryPath continues to identify categories in either orientation. Horizontal display maps values onto the X axis.</remarks>
public abstract class ComparisonSeries : ChartSeries, IChartComparisonSeries
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<ComparisonSeries, Orientation>(nameof(Orientation), Orientation.Vertical, validate: Enum.IsDefined);
    public static readonly StyledProperty<double> BaselineProperty =
        AvaloniaProperty.Register<ComparisonSeries, double>(nameof(Baseline), validate: double.IsFinite);
    public static readonly StyledProperty<double?> BarWidthProperty =
        AvaloniaProperty.Register<ComparisonSeries, double?>(nameof(BarWidth), validate: value => value is null || double.IsFinite(value.Value) && value.Value >= .5);
    public static readonly StyledProperty<double> WidthRatioProperty =
        AvaloniaProperty.Register<ComparisonSeries, double>(nameof(WidthRatio), .86, validate: value => double.IsFinite(value) && value > 0 && value <= 1);
    public static readonly StyledProperty<ChartSeriesSortMode> SortModeProperty =
        AvaloniaProperty.Register<ComparisonSeries, ChartSeriesSortMode>(nameof(SortMode), validate: Enum.IsDefined);
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>Finite value-axis baseline. Zero on a logarithmic axis uses its lowest valid axis value.</summary>
    public double Baseline { get => GetValue(BaselineProperty); set => SetValue(BaselineProperty, value); }
    /// <summary>Optional explicit width in device-independent pixels. Null uses the category slot and WidthRatio.</summary>
    public double? BarWidth { get => GetValue(BarWidthProperty); set => SetValue(BarWidthProperty, value); }
    /// <summary>Fraction of the grouped category slot occupied by a bar, between zero exclusive and one inclusive.</summary>
    public double WidthRatio { get => GetValue(WidthRatioProperty); set => SetValue(WidthRatioProperty, value); }
    public ChartSeriesSortMode SortMode { get => GetValue(SortModeProperty); set => SetValue(SortModeProperty, value); }
}

public sealed partial class BarSeries : IChartComparisonSeries
{
    public static readonly StyledProperty<Orientation> OrientationProperty = ComparisonSeries.OrientationProperty.AddOwner<BarSeries>();
    public static readonly StyledProperty<double> BaselineProperty = ComparisonSeries.BaselineProperty.AddOwner<BarSeries>();
    public static readonly StyledProperty<double?> BarWidthProperty = ComparisonSeries.BarWidthProperty.AddOwner<BarSeries>();
    public static readonly StyledProperty<double> WidthRatioProperty = ComparisonSeries.WidthRatioProperty.AddOwner<BarSeries>();
    public static readonly StyledProperty<ChartSeriesSortMode> SortModeProperty = ComparisonSeries.SortModeProperty.AddOwner<BarSeries>();
    public static readonly StyledProperty<ChartStackMode> StackModeProperty = LineSeries.StackModeProperty.AddOwner<BarSeries>();
    public static readonly StyledProperty<string?> StackGroupProperty = LineSeries.StackGroupProperty.AddOwner<BarSeries>();
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public double Baseline { get => GetValue(BaselineProperty); set => SetValue(BaselineProperty, value); }
    public double? BarWidth { get => GetValue(BarWidthProperty); set => SetValue(BarWidthProperty, value); }
    public double WidthRatio { get => GetValue(WidthRatioProperty); set => SetValue(WidthRatioProperty, value); }
    public ChartSeriesSortMode SortMode { get => GetValue(SortModeProperty); set => SetValue(SortModeProperty, value); }
    public ChartStackMode StackMode { get => GetValue(StackModeProperty); set => SetValue(StackModeProperty, value); }
    /// <summary>Stack name within the same resolved axes, orientation, baseline and stacking mode.</summary>
    public string? StackGroup { get => GetValue(StackGroupProperty); set => SetValue(StackGroupProperty, value); }
    internal bool UsesLegacyBarRenderer => Orientation == Orientation.Vertical && Baseline == 0 && BarWidth is null &&
        WidthRatio == .86 && SortMode == ChartSeriesSortMode.Source && StackMode == ChartStackMode.None;
}

/// <summary>A rectangular interval between LowPath and HighPath, without requiring ValuePath.</summary>
public sealed class RangeBarSeries : ComparisonSeries
{
    static RangeBarSeries() => ShowMarkersProperty.OverrideDefaultValue<RangeBarSeries>(false);
    public override IChartSeriesRenderer Renderer => ChartComparisonSeriesRenderer.Range;
}

/// <summary>A running-value waterfall. Ordinary rows are signed deltas; TotalPath true rows are explicit totals and reset the running value.</summary>
public sealed class WaterfallSeries : ComparisonSeries
{
    public static readonly StyledProperty<string?> TotalPathProperty = AvaloniaProperty.Register<WaterfallSeries, string?>(nameof(TotalPath));
    public static readonly StyledProperty<IBrush?> PositiveFillProperty = AvaloniaProperty.Register<WaterfallSeries, IBrush?>(nameof(PositiveFill));
    public static readonly StyledProperty<IBrush?> NegativeFillProperty = AvaloniaProperty.Register<WaterfallSeries, IBrush?>(nameof(NegativeFill));
    public static readonly StyledProperty<IBrush?> TotalFillProperty = AvaloniaProperty.Register<WaterfallSeries, IBrush?>(nameof(TotalFill));
    public static readonly StyledProperty<bool> ShowConnectorsProperty = AvaloniaProperty.Register<WaterfallSeries, bool>(nameof(ShowConnectors), true);
    public static readonly StyledProperty<IBrush?> ConnectorStrokeProperty = AvaloniaProperty.Register<WaterfallSeries, IBrush?>(nameof(ConnectorStroke));
    public static readonly StyledProperty<double> ConnectorThicknessProperty = AvaloniaProperty.Register<WaterfallSeries, double>(nameof(ConnectorThickness), 1,
        validate: value => double.IsFinite(value) && value >= 0);
    static WaterfallSeries() => ShowMarkersProperty.OverrideDefaultValue<WaterfallSeries>(false);
    public string? TotalPath { get => GetValue(TotalPathProperty); set => SetValue(TotalPathProperty, value); }
    public IBrush? PositiveFill { get => GetValue(PositiveFillProperty); set => SetValue(PositiveFillProperty, value); }
    public IBrush? NegativeFill { get => GetValue(NegativeFillProperty); set => SetValue(NegativeFillProperty, value); }
    public IBrush? TotalFill { get => GetValue(TotalFillProperty); set => SetValue(TotalFillProperty, value); }
    public bool ShowConnectors { get => GetValue(ShowConnectorsProperty); set => SetValue(ShowConnectorsProperty, value); }
    public IBrush? ConnectorStroke { get => GetValue(ConnectorStrokeProperty); set => SetValue(ConnectorStrokeProperty, value); }
    public double ConnectorThickness { get => GetValue(ConnectorThicknessProperty); set => SetValue(ConnectorThicknessProperty, value); }
    public override IChartSeriesRenderer Renderer => ChartComparisonSeriesRenderer.Waterfall;
}

/// <summary>One marker per category with optional stable value sorting.</summary>
public sealed class DotSeries : ComparisonSeries
{
    public override IChartSeriesRenderer Renderer => ChartComparisonSeriesRenderer.Dot;
}

/// <summary>A marker and a line from the configured baseline to its value.</summary>
public sealed class LollipopSeries : ComparisonSeries
{
    public override IChartSeriesRenderer Renderer => ChartComparisonSeriesRenderer.Lollipop;
}

/// <summary>A comparison between LowPath and HighPath rendered as a connector and two endpoint markers.</summary>
public sealed class VarianceSeries : ComparisonSeries
{
    public static readonly StyledProperty<IBrush?> PositiveFillProperty = WaterfallSeries.PositiveFillProperty.AddOwner<VarianceSeries>();
    public static readonly StyledProperty<IBrush?> NegativeFillProperty = WaterfallSeries.NegativeFillProperty.AddOwner<VarianceSeries>();
    public IBrush? PositiveFill { get => GetValue(PositiveFillProperty); set => SetValue(PositiveFillProperty, value); }
    public IBrush? NegativeFill { get => GetValue(NegativeFillProperty); set => SetValue(NegativeFillProperty, value); }
    public override IChartSeriesRenderer Renderer => ChartComparisonSeriesRenderer.Variance;
}

/// <summary>Descending, nonnegative category bars with an optional cumulative percentage line.</summary>
public sealed class ParetoSeries : ComparisonSeries
{
    public static readonly StyledProperty<string?> CumulativeAxisKeyProperty = AvaloniaProperty.Register<ParetoSeries, string?>(nameof(CumulativeAxisKey));
    public static readonly StyledProperty<bool> ShowCumulativeLineProperty = AvaloniaProperty.Register<ParetoSeries, bool>(nameof(ShowCumulativeLine), true);
    public static readonly StyledProperty<IBrush?> CumulativeStrokeProperty = AvaloniaProperty.Register<ParetoSeries, IBrush?>(nameof(CumulativeStroke));
    public static readonly StyledProperty<double> CumulativeStrokeThicknessProperty = AvaloniaProperty.Register<ParetoSeries, double>(nameof(CumulativeStrokeThickness), 2,
        validate: value => double.IsFinite(value) && value >= 0);
    static ParetoSeries()
    {
        ShowMarkersProperty.OverrideDefaultValue<ParetoSeries>(false);
        SortModeProperty.OverrideDefaultValue<ParetoSeries>(ChartSeriesSortMode.ValueDescending);
    }
    /// <summary>Optional unique linear numeric value axis for cumulative percentages; null creates a private zero-to-one-hundred far axis.</summary>
    public string? CumulativeAxisKey { get => GetValue(CumulativeAxisKeyProperty); set => SetValue(CumulativeAxisKeyProperty, value); }
    public bool ShowCumulativeLine { get => GetValue(ShowCumulativeLineProperty); set => SetValue(ShowCumulativeLineProperty, value); }
    public IBrush? CumulativeStroke { get => GetValue(CumulativeStrokeProperty); set => SetValue(CumulativeStrokeProperty, value); }
    public double CumulativeStrokeThickness { get => GetValue(CumulativeStrokeThicknessProperty); set => SetValue(CumulativeStrokeThicknessProperty, value); }
    public override IChartSeriesRenderer Renderer => ChartComparisonSeriesRenderer.Pareto;
}
