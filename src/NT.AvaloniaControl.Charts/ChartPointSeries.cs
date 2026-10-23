using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>How a numeric size fraction is converted to a marker diameter in DIPs.</summary>
public enum ChartSizeScale { Diameter, Area }

/// <summary>Stable marker paint order. Connecting lines always retain source order.</summary>
public enum ChartPointDrawOrder { Source, SizeDescending, SizeAscending }

internal interface IChartPointSeries
{
    string? SizePath { get; }
    string? ColorPath { get; }
    double MinimumSize { get; }
    double MaximumSize { get; }
    double? SizeMinimum { get; }
    double? SizeMaximum { get; }
    ChartSizeScale SizeScale { get; }
    double? ColorMinimum { get; }
    double? ColorMaximum { get; }
    Color MinimumColor { get; }
    Color MaximumColor { get; }
    ChartPointDrawOrder DrawOrder { get; }
}

/// <summary>Shared size, color and overlap configuration for XY point series.</summary>
/// <remarks>Numeric encoding domains use all valid XY samples, independent of the viewport.
/// Null size paths use MarkerSize. Invalid mapped sizes hide only that marker; they do not remove the XY sample.</remarks>
public abstract class PointSeries : ChartSeries, IChartPointSeries
{
    public static readonly StyledProperty<string?> SizePathProperty = AvaloniaProperty.Register<PointSeries, string?>(nameof(SizePath));
    public static readonly StyledProperty<string?> ColorPathProperty = AvaloniaProperty.Register<PointSeries, string?>(nameof(ColorPath));
    public static readonly StyledProperty<double> MinimumSizeProperty = AvaloniaProperty.Register<PointSeries, double>(nameof(MinimumSize), 4,
        validate: value => double.IsFinite(value) && value >= 0 && value <= 1024);
    public static readonly StyledProperty<double> MaximumSizeProperty = AvaloniaProperty.Register<PointSeries, double>(nameof(MaximumSize), 40,
        validate: value => double.IsFinite(value) && value >= 0 && value <= 1024);
    public static readonly StyledProperty<double?> SizeMinimumProperty = AvaloniaProperty.Register<PointSeries, double?>(nameof(SizeMinimum),
        validate: value => value is null || double.IsFinite(value.Value) && value >= 0);
    public static readonly StyledProperty<double?> SizeMaximumProperty = AvaloniaProperty.Register<PointSeries, double?>(nameof(SizeMaximum),
        validate: value => value is null || double.IsFinite(value.Value) && value >= 0);
    public static readonly StyledProperty<ChartSizeScale> SizeScaleProperty = AvaloniaProperty.Register<PointSeries, ChartSizeScale>(nameof(SizeScale), ChartSizeScale.Area,
        validate: Enum.IsDefined);
    public static readonly StyledProperty<double?> ColorMinimumProperty = AvaloniaProperty.Register<PointSeries, double?>(nameof(ColorMinimum),
        validate: value => value is null || double.IsFinite(value.Value));
    public static readonly StyledProperty<double?> ColorMaximumProperty = AvaloniaProperty.Register<PointSeries, double?>(nameof(ColorMaximum),
        validate: value => value is null || double.IsFinite(value.Value));
    public static readonly StyledProperty<Color> MinimumColorProperty = AvaloniaProperty.Register<PointSeries, Color>(nameof(MinimumColor), Color.FromRgb(0x3B, 0x82, 0xF6));
    public static readonly StyledProperty<Color> MaximumColorProperty = AvaloniaProperty.Register<PointSeries, Color>(nameof(MaximumColor), Color.FromRgb(0xF5, 0x9E, 0x0B));
    public static readonly StyledProperty<ChartPointDrawOrder> DrawOrderProperty = AvaloniaProperty.Register<PointSeries, ChartPointDrawOrder>(nameof(DrawOrder),
        validate: Enum.IsDefined);

    public string? SizePath { get => GetValue(SizePathProperty); set => SetValue(SizePathProperty, value); }
    /// <summary>Maps a finite numeric gradient value, or a Color, IBrush or color string. Invalid colors use the series fill.</summary>
    public string? ColorPath { get => GetValue(ColorPathProperty); set => SetValue(ColorPathProperty, value); }
    /// <summary>Smallest encoded marker diameter in DIPs. Must not exceed MaximumSize.</summary>
    public double MinimumSize { get => GetValue(MinimumSizeProperty); set => SetValue(MinimumSizeProperty, value); }
    /// <summary>Largest encoded marker diameter in DIPs, up to 1024.</summary>
    public double MaximumSize { get => GetValue(MaximumSizeProperty); set => SetValue(MaximumSizeProperty, value); }
    /// <summary>Numeric size domain minimum. Null uses zero.</summary>
    public double? SizeMinimum { get => GetValue(SizeMinimumProperty); set => SetValue(SizeMinimumProperty, value); }
    /// <summary>Numeric size domain maximum. Null uses the largest valid, nonnegative mapped size.</summary>
    public double? SizeMaximum { get => GetValue(SizeMaximumProperty); set => SetValue(SizeMaximumProperty, value); }
    public ChartSizeScale SizeScale { get => GetValue(SizeScaleProperty); set => SetValue(SizeScaleProperty, value); }
    public double? ColorMinimum { get => GetValue(ColorMinimumProperty); set => SetValue(ColorMinimumProperty, value); }
    public double? ColorMaximum { get => GetValue(ColorMaximumProperty); set => SetValue(ColorMaximumProperty, value); }
    public Color MinimumColor { get => GetValue(MinimumColorProperty); set => SetValue(MinimumColorProperty, value); }
    public Color MaximumColor { get => GetValue(MaximumColorProperty); set => SetValue(MaximumColorProperty, value); }
    public ChartPointDrawOrder DrawOrder { get => GetValue(DrawOrderProperty); set => SetValue(DrawOrderProperty, value); }
}

public sealed partial class ScatterSeries : IChartPointSeries
{
    public static readonly StyledProperty<string?> SizePathProperty = PointSeries.SizePathProperty.AddOwner<ScatterSeries>();
    public static readonly StyledProperty<string?> ColorPathProperty = PointSeries.ColorPathProperty.AddOwner<ScatterSeries>();
    public static readonly StyledProperty<double> MinimumSizeProperty = PointSeries.MinimumSizeProperty.AddOwner<ScatterSeries>();
    public static readonly StyledProperty<double> MaximumSizeProperty = PointSeries.MaximumSizeProperty.AddOwner<ScatterSeries>();
    public static readonly StyledProperty<double?> SizeMinimumProperty = PointSeries.SizeMinimumProperty.AddOwner<ScatterSeries>();
    public static readonly StyledProperty<double?> SizeMaximumProperty = PointSeries.SizeMaximumProperty.AddOwner<ScatterSeries>();
    public static readonly StyledProperty<ChartSizeScale> SizeScaleProperty = PointSeries.SizeScaleProperty.AddOwner<ScatterSeries>();
    public static readonly StyledProperty<double?> ColorMinimumProperty = PointSeries.ColorMinimumProperty.AddOwner<ScatterSeries>();
    public static readonly StyledProperty<double?> ColorMaximumProperty = PointSeries.ColorMaximumProperty.AddOwner<ScatterSeries>();
    public static readonly StyledProperty<Color> MinimumColorProperty = PointSeries.MinimumColorProperty.AddOwner<ScatterSeries>();
    public static readonly StyledProperty<Color> MaximumColorProperty = PointSeries.MaximumColorProperty.AddOwner<ScatterSeries>();
    public static readonly StyledProperty<ChartPointDrawOrder> DrawOrderProperty = PointSeries.DrawOrderProperty.AddOwner<ScatterSeries>();
    public string? SizePath { get => GetValue(SizePathProperty); set => SetValue(SizePathProperty, value); }
    public string? ColorPath { get => GetValue(ColorPathProperty); set => SetValue(ColorPathProperty, value); }
    public double MinimumSize { get => GetValue(MinimumSizeProperty); set => SetValue(MinimumSizeProperty, value); }
    public double MaximumSize { get => GetValue(MaximumSizeProperty); set => SetValue(MaximumSizeProperty, value); }
    public double? SizeMinimum { get => GetValue(SizeMinimumProperty); set => SetValue(SizeMinimumProperty, value); }
    public double? SizeMaximum { get => GetValue(SizeMaximumProperty); set => SetValue(SizeMaximumProperty, value); }
    public ChartSizeScale SizeScale { get => GetValue(SizeScaleProperty); set => SetValue(SizeScaleProperty, value); }
    public double? ColorMinimum { get => GetValue(ColorMinimumProperty); set => SetValue(ColorMinimumProperty, value); }
    public double? ColorMaximum { get => GetValue(ColorMaximumProperty); set => SetValue(ColorMaximumProperty, value); }
    public Color MinimumColor { get => GetValue(MinimumColorProperty); set => SetValue(MinimumColorProperty, value); }
    public Color MaximumColor { get => GetValue(MaximumColorProperty); set => SetValue(MaximumColorProperty, value); }
    public ChartPointDrawOrder DrawOrder { get => GetValue(DrawOrderProperty); set => SetValue(DrawOrderProperty, value); }
    internal bool UsesLegacyPointRenderer => string.IsNullOrWhiteSpace(SizePath) && string.IsNullOrWhiteSpace(ColorPath) && DrawOrder == ChartPointDrawOrder.Source;
}

/// <summary>XY markers connected by straight segments in the original snapshot order. Only missing XY samples break the line.</summary>
public sealed class ScatterLineSeries : PointSeries
{
    public static readonly StyledProperty<bool> ShowLineProperty = AvaloniaProperty.Register<ScatterLineSeries, bool>(nameof(ShowLine), true);
    public bool ShowLine { get => GetValue(ShowLineProperty); set => SetValue(ShowLineProperty, value); }
    public override IChartSeriesRenderer Renderer => ChartPointSeriesRenderer.Instance;
}

/// <summary>XY markers whose third value encodes their area or diameter. SizePath takes precedence over WeightPath.</summary>
/// <remarks>A zero mapped size has a zero diameter with the default MinimumSize. Negative, nonfinite and missing sizes hide the marker.</remarks>
public sealed class BubbleSeries : PointSeries
{
    static BubbleSeries()
    {
        MinimumSizeProperty.OverrideDefaultValue<BubbleSeries>(0);
        MaximumSizeProperty.OverrideDefaultValue<BubbleSeries>(60);
    }
    public override IChartSeriesRenderer Renderer => ChartPointSeriesRenderer.Instance;
}
