using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>The unit used by a numeric angular domain and its mapped data coordinates.</summary>
public enum ChartAngleUnit { Degrees, Radians }

/// <summary>Observable angle/radius mapping and appearance for polar and categorical radial projections.</summary>
/// <remarks>AnglePath and RadiusPath alias CategoryPath and ValuePath. Negative or nonfinite radii are gaps;
/// categorical projections share the chart's first-seen category union. Angles are never sorted implicitly.</remarks>
public abstract class PolarSeries : ChartSeries
{
    public static readonly StyledProperty<string?> AnglePathProperty = CategoryPathProperty;
    public static readonly StyledProperty<string?> RadiusPathProperty = ValuePathProperty;
    public static readonly StyledProperty<string?> ColorPathProperty = AvaloniaProperty.Register<PolarSeries, string?>(nameof(ColorPath));
    public static readonly StyledProperty<bool> IsClosedProperty = AvaloniaProperty.Register<PolarSeries, bool>(nameof(IsClosed));
    public static readonly StyledProperty<double> FillOpacityProperty = AvaloniaProperty.Register<PolarSeries, double>(nameof(FillOpacity), 1,
        validate: value => double.IsFinite(value) && value >= 0 && value <= 1);
    public static readonly StyledProperty<double> InnerRadiusProperty = AvaloniaProperty.Register<PolarSeries, double>(nameof(InnerRadius),
        validate: value => double.IsFinite(value) && value >= 0 && value < 1);
    public static readonly StyledProperty<double> SectorGapProperty = AvaloniaProperty.Register<PolarSeries, double>(nameof(SectorGap), 2,
        validate: value => double.IsFinite(value) && value >= 0 && value <= 360);

    public string? AnglePath { get => CategoryPath; set => CategoryPath = value; }
    public string? RadiusPath { get => ValuePath; set => ValuePath = value; }
    /// <summary>Optional direct Color, IBrush or color string. Other values use the chart palette.</summary>
    public string? ColorPath { get => GetValue(ColorPathProperty); set => SetValue(ColorPathProperty, value); }
    public bool IsClosed { get => GetValue(IsClosedProperty); set => SetValue(IsClosedProperty, value); }
    public double FillOpacity { get => GetValue(FillOpacityProperty); set => SetValue(FillOpacityProperty, value); }
    /// <summary>Mapped radial minimum as a fraction of the available radius. Sectors retain a hole;
    /// connected polygon edges and fills can cross this inner boundary.</summary>
    public double InnerRadius { get => GetValue(InnerRadiusProperty); set => SetValue(InnerRadiusProperty, value); }
    /// <summary>Angular paint gap in degrees, applied without changing a sector's original source value.</summary>
    public double SectorGap { get => GetValue(SectorGapProperty); set => SetValue(SectorGapProperty, value); }
    internal bool IsCategorical => this is RadarSeries or RadialLineSeries or PolarAreaSeries or NightingaleRoseSeries or RadialBarSeries;
}

/// <summary>A numeric angular/radial polyline in source order.</summary>
public sealed class PolarLineSeries : PolarSeries;

/// <summary>Numeric angular/radial markers without connecting lines.</summary>
public sealed class PolarScatterSeries : PolarSeries;

/// <summary>A numeric angular/radial outline with polygon fill when it forms a complete closed contour.</summary>
public sealed class PolarFilledSeries : PolarSeries
{
    static PolarFilledSeries()
    {
        IsClosedProperty.OverrideDefaultValue<PolarFilledSeries>(true);
        FillOpacityProperty.OverrideDefaultValue<PolarFilledSeries>(.25);
    }
}

/// <summary>A category-aligned radial polygon, with polygonal axis rings.</summary>
public sealed class RadarSeries : PolarSeries
{
    public static readonly StyledProperty<double?> MaxValueProperty = AvaloniaProperty.Register<RadarSeries, double?>(nameof(MaxValue), 100,
        validate: value => value is null || double.IsFinite(value.Value) && value.Value > 0);
    static RadarSeries()
    {
        IsClosedProperty.OverrideDefaultValue<RadarSeries>(true);
        FillOpacityProperty.OverrideDefaultValue<RadarSeries>(.3);
    }
    /// <summary>Explicit series radial maximum. Null uses the chart's shared radial domain; the default is 100.</summary>
    public double? MaxValue { get => GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }
}

/// <summary>A category-aligned radial polyline using circular axis rings.</summary>
public sealed class RadialLineSeries : PolarSeries
{
    static RadialLineSeries() => IsClosedProperty.OverrideDefaultValue<RadialLineSeries>(true);
}

/// <summary>Equal angular sectors whose annular area is proportional to the nonnegative radial value.</summary>
public sealed class PolarAreaSeries : PolarSeries;

/// <summary>Equal angular sectors whose radial distance above the inner baseline is proportional to value.</summary>
public sealed class NightingaleRoseSeries : PolarSeries;

/// <summary>Concentric category lanes whose occupied angular length is proportional to value.</summary>
public sealed class RadialBarSeries : PolarSeries;

/// <summary>A projected point retaining its original source identity. Angle is displayed degrees and Radius is in DIPs.</summary>
public sealed record PolarPointInfo(PolarSeries Series, ChartDataPoint Data, double Angle, double Radius, Point Position)
{
    public bool IsVisible { get; init; } = true;
    public IBrush? Fill { get; init; }
    public ChartPointHit Hit => ChartPointHit.FromData(Series, Data, Position);
}

/// <summary>A projected annular sector. Angles are displayed degrees; a negative sweep is counterclockwise.</summary>
public sealed record PolarSectorInfo(PolarSeries Series, ChartDataPoint Data, Point Center, double InnerRadius,
    double OuterRadius, double StartAngle, double SweepAngle)
{
    public bool IsVisible { get; init; } = true;
    public IBrush? Fill { get; init; }
    public Point Anchor => PolarChartGeometry.PointAt(Center, StartAngle + SweepAngle / 2, (InnerRadius + OuterRadius) / 2);
    public ChartPointHit Hit => ChartPointHit.FromData(Series, Data, Anchor);
}
