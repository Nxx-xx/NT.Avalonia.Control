using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>How a circular series treats finite negative values. Zero and nonfinite values never create a sector.</summary>
public enum ChartCircularNegativeValueMode { Ignore, Absolute, Throw }

/// <summary>Whether a sector below MinimumSliceAngle remains visible. Hidden sectors retain their original share.</summary>
public enum ChartCircularSmallSliceMode { Keep, Hide }

/// <summary>Observable category/value mapping and geometry options for a circular chart.</summary>
/// <remarks>ValuePath supplies the contribution and CategoryPath supplies its label. Values are normalized independently
/// for each series without modifying the source snapshot. Radius and InnerRadius are ratios; other distances are DIPs.</remarks>
public abstract class CircularSeries : ChartSeries
{
    public static readonly StyledProperty<ChartCircularNegativeValueMode> NegativeValueModeProperty =
        AvaloniaProperty.Register<CircularSeries, ChartCircularNegativeValueMode>(nameof(NegativeValueMode), validate: Enum.IsDefined);
    public static readonly StyledProperty<string?> ColorPathProperty =
        AvaloniaProperty.Register<CircularSeries, string?>(nameof(ColorPath));
    public static readonly StyledProperty<double> StartAngleProperty =
        AvaloniaProperty.Register<CircularSeries, double>(nameof(StartAngle), -90, validate: double.IsFinite);
    public static readonly StyledProperty<double> SweepAngleProperty =
        AvaloniaProperty.Register<CircularSeries, double>(nameof(SweepAngle), 360,
            validate: value => double.IsFinite(value) && value >= 0 && value <= 360);
    public static readonly StyledProperty<bool> ClockwiseProperty =
        AvaloniaProperty.Register<CircularSeries, bool>(nameof(Clockwise), true);
    public static readonly StyledProperty<double> InnerRadiusProperty =
        AvaloniaProperty.Register<CircularSeries, double>(nameof(InnerRadius),
            validate: value => double.IsFinite(value) && value >= 0 && value < 1);
    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<CircularSeries, double>(nameof(Radius), 1,
            validate: value => double.IsFinite(value) && value >= 0 && value <= 1);
    public static readonly StyledProperty<double> SliceGapProperty =
        AvaloniaProperty.Register<CircularSeries, double>(nameof(SliceGap),
            validate: value => double.IsFinite(value) && value >= 0 && value <= 360);
    public static readonly StyledProperty<double> MinimumSliceAngleProperty =
        AvaloniaProperty.Register<CircularSeries, double>(nameof(MinimumSliceAngle),
            validate: value => double.IsFinite(value) && value >= 0 && value <= 360);
    public static readonly StyledProperty<ChartCircularSmallSliceMode> SmallSliceModeProperty =
        AvaloniaProperty.Register<CircularSeries, ChartCircularSmallSliceMode>(nameof(SmallSliceMode), validate: Enum.IsDefined);
    public static readonly StyledProperty<double> ExplodeOffsetProperty =
        AvaloniaProperty.Register<CircularSeries, double>(nameof(ExplodeOffset),
            validate: value => double.IsFinite(value) && value >= 0 && value <= 10_000);
    public static readonly StyledProperty<int> ExplodedIndexProperty =
        AvaloniaProperty.Register<CircularSeries, int>(nameof(ExplodedIndex), -1, validate: value => value >= -1);

    public ChartCircularNegativeValueMode NegativeValueMode { get => GetValue(NegativeValueModeProperty); set => SetValue(NegativeValueModeProperty, value); }
    /// <summary>Optional direct Color, IBrush or color string. Unrecognized values use the chart's category palette.</summary>
    public string? ColorPath { get => GetValue(ColorPathProperty); set => SetValue(ColorPathProperty, value); }
    /// <summary>Degrees from the rightward ray in chart coordinates. Positive clockwise angles point downward.</summary>
    public double StartAngle { get => GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    /// <summary>Total occupied angular range, from zero to 360 degrees. Contributions retain their share of this range.</summary>
    public double SweepAngle { get => GetValue(SweepAngleProperty); set => SetValue(SweepAngleProperty, value); }
    public bool Clockwise { get => GetValue(ClockwiseProperty); set => SetValue(ClockwiseProperty, value); }
    /// <summary>Hole radius as a fraction of this series' outer radius; zero creates a pie.</summary>
    public double InnerRadius { get => GetValue(InnerRadiusProperty); set => SetValue(InnerRadiusProperty, value); }
    /// <summary>Outer radius as a fraction of the chart's available circular radius. Zero hides the series.</summary>
    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
    /// <summary>Angular paint gap centered on each boundary. A gap never enlarges or renormalizes a sector.</summary>
    public double SliceGap { get => GetValue(SliceGapProperty); set => SetValue(SliceGapProperty, value); }
    /// <summary>Original angular share below which SmallSliceMode.Hide removes paint and hit testing.</summary>
    public double MinimumSliceAngle { get => GetValue(MinimumSliceAngleProperty); set => SetValue(MinimumSliceAngleProperty, value); }
    public ChartCircularSmallSliceMode SmallSliceMode { get => GetValue(SmallSliceModeProperty); set => SetValue(SmallSliceModeProperty, value); }
    /// <summary>Outward translation in DIPs for the original source index in ExplodedIndex.</summary>
    public double ExplodeOffset { get => GetValue(ExplodeOffsetProperty); set => SetValue(ExplodeOffsetProperty, value); }
    public int ExplodedIndex { get => GetValue(ExplodedIndexProperty); set => SetValue(ExplodedIndexProperty, value); }
}

/// <summary>Proportional sectors without a central hole by default.</summary>
public sealed class PieSeries : CircularSeries;

/// <summary>Proportional annular sectors with a 60% central hole by default.</summary>
public sealed class DonutSeries : CircularSeries
{
    static DonutSeries() => InnerRadiusProperty.OverrideDefaultValue<DonutSeries>(.6);
}

/// <summary>Top-half annular sectors, from 180 to 360 degrees, with a 60% central hole by default.</summary>
public sealed class SemiDonutSeries : CircularSeries
{
    static SemiDonutSeries()
    {
        InnerRadiusProperty.OverrideDefaultValue<SemiDonutSeries>(.6);
        StartAngleProperty.OverrideDefaultValue<SemiDonutSeries>(180);
        SweepAngleProperty.OverrideDefaultValue<SemiDonutSeries>(180);
    }
}

/// <summary>A current circular scene sector retaining its original item and normalized contribution.</summary>
/// <remarks>StartAngle and SweepAngle describe painted geometry, with a negative sweep for counterclockwise drawing.
/// Raw angles retain the allocated share before SliceGap. Hidden sectors remain in the scene with IsVisible false.</remarks>
public sealed record CircularSliceInfo(CircularSeries Series, ChartDataPoint Data, double Fraction,
    double StartAngle, double SweepAngle, Point Center, double InnerRadius, double OuterRadius)
{
    public bool IsVisible { get; init; } = true;
    public double RawStartAngle { get; init; }
    public double RawSweepAngle { get; init; }
    /// <summary>Resolved current-frame fill. Null leaves palette resolution to the owning chart.</summary>
    public IBrush? Fill { get; init; }
    /// <summary>Whether this is a retained outgoing animation sector, excluded from current selection and hit testing.</summary>
    public bool IsExiting { get; init; }
    public Point Anchor => CircularChartGeometry.Anchor(this);
    public ChartPointHit Hit => ChartPointHit.FromData(Series, Data, Anchor);
}
