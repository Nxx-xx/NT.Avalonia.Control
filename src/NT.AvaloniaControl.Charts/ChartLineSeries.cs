using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Shape of the path between adjacent, valid source samples.</summary>
public enum ChartLineInterpolation { Linear, Smooth, StepBefore, StepAfter, StepCenter }

/// <summary>How line and area contributions accumulate at each actual X coordinate and duplicate occurrence.</summary>
/// <remarks>Continuous shared boundaries require matching interpolation, source-order X and valid segment topology.</remarks>
public enum ChartStackMode { None, Stacked, Percent }

internal interface IChartLineSeries
{
    ChartLineInterpolation Interpolation { get; }
    ChartStackMode StackMode { get; }
    string? StackGroup { get; }
}

public sealed partial class LineSeries : IChartLineSeries
{
    public static readonly StyledProperty<ChartLineInterpolation> InterpolationProperty =
        AvaloniaProperty.Register<LineSeries, ChartLineInterpolation>(nameof(Interpolation), validate: Enum.IsDefined);
    public static readonly StyledProperty<ChartStackMode> StackModeProperty =
        AvaloniaProperty.Register<LineSeries, ChartStackMode>(nameof(StackMode), validate: Enum.IsDefined);
    public static readonly StyledProperty<string?> StackGroupProperty =
        AvaloniaProperty.Register<LineSeries, string?>(nameof(StackGroup));
    public ChartLineInterpolation Interpolation { get => GetValue(InterpolationProperty); set => SetValue(InterpolationProperty, value); }
    public ChartStackMode StackMode { get => GetValue(StackModeProperty); set => SetValue(StackModeProperty, value); }
    /// <summary>Stack name within the same resolved axes and stacking mode. Null and empty name the default stack.</summary>
    public string? StackGroup { get => GetValue(StackGroupProperty); set => SetValue(StackGroupProperty, value); }
}

/// <summary>A filled line whose lower boundary is Baseline, or its preceding stack contribution.</summary>
public sealed class AreaSeries : ChartSeries, IChartLineSeries
{
    public static readonly StyledProperty<ChartLineInterpolation> InterpolationProperty = LineSeries.InterpolationProperty.AddOwner<AreaSeries>();
    public static readonly StyledProperty<ChartStackMode> StackModeProperty = LineSeries.StackModeProperty.AddOwner<AreaSeries>();
    public static readonly StyledProperty<string?> StackGroupProperty = LineSeries.StackGroupProperty.AddOwner<AreaSeries>();
    public static readonly StyledProperty<double> BaselineProperty =
        AvaloniaProperty.Register<AreaSeries, double>(nameof(Baseline), validate: double.IsFinite);
    public ChartLineInterpolation Interpolation { get => GetValue(InterpolationProperty); set => SetValue(InterpolationProperty, value); }
    public ChartStackMode StackMode { get => GetValue(StackModeProperty); set => SetValue(StackModeProperty, value); }
    /// <summary>Stack name within the same resolved axes and stacking mode. Null and empty name the default stack.</summary>
    public string? StackGroup { get => GetValue(StackGroupProperty); set => SetValue(StackGroupProperty, value); }
    public double Baseline { get => GetValue(BaselineProperty); set => SetValue(BaselineProperty, value); }
    public override IChartSeriesRenderer Renderer => ChartLineSeriesRenderer.Area;
}

/// <summary>A band between the finite values mapped by HighPath and LowPath.</summary>
public sealed class RangeAreaSeries : ChartSeries, IChartLineSeries, IChartRangeSeries
{
    public static readonly StyledProperty<ChartLineInterpolation> InterpolationProperty = LineSeries.InterpolationProperty.AddOwner<RangeAreaSeries>();
    public ChartLineInterpolation Interpolation { get => GetValue(InterpolationProperty); set => SetValue(InterpolationProperty, value); }
    ChartStackMode IChartLineSeries.StackMode => ChartStackMode.None;
    string? IChartLineSeries.StackGroup => null;
    public override IChartSeriesRenderer Renderer => ChartLineSeriesRenderer.RangeArea;
}
