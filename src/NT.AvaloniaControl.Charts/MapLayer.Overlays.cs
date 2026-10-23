using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public enum MapMarkerType { Circle, Diamond, Triangle, Rectangle, Pin }
public enum MapColorMode { Continuous, Categorical }

/// <summary>A fixed-size point symbol; its business value does not change its size.</summary>
public sealed class MarkerLayer : MapLayer
{
    public static readonly StyledProperty<double> MarkerSizeProperty = AvaloniaProperty.Register<MarkerLayer, double>(nameof(MarkerSize), 10, validate: x => double.IsFinite(x) && x > 0 && x <= 1024);
    public static readonly StyledProperty<MapMarkerType> MarkerTypeProperty = AvaloniaProperty.Register<MarkerLayer, MapMarkerType>(nameof(MarkerType), MapMarkerType.Circle, validate: Enum.IsDefined);
    public double MarkerSize { get => GetValue(MarkerSizeProperty); set => SetValue(MarkerSizeProperty, value); }
    public MapMarkerType MarkerType { get => GetValue(MarkerTypeProperty); set => SetValue(MarkerTypeProperty, value); }
}

/// <summary>Business connection pairs. Curves are quadratic screen-space routes and are split at the geographic date line before drawing.</summary>
public sealed class LineLayer : MapLayer
{
    public LineLayer() { Stroke = Brush.Parse("#2196F3"); }
    public static readonly StyledProperty<string?> FromLongitudePathProperty = AvaloniaProperty.Register<LineLayer, string?>(nameof(FromLongitudePath), "FromLongitude");
    public static readonly StyledProperty<string?> FromLatitudePathProperty = AvaloniaProperty.Register<LineLayer, string?>(nameof(FromLatitudePath), "FromLatitude");
    public static readonly StyledProperty<string?> ToLongitudePathProperty = AvaloniaProperty.Register<LineLayer, string?>(nameof(ToLongitudePath), "ToLongitude");
    public static readonly StyledProperty<string?> ToLatitudePathProperty = AvaloniaProperty.Register<LineLayer, string?>(nameof(ToLatitudePath), "ToLatitude");
    public static readonly StyledProperty<double> MinimumThicknessProperty = AvaloniaProperty.Register<LineLayer, double>(nameof(MinimumThickness), 1, validate: x => double.IsFinite(x) && x > 0 && x <= 128);
    public static readonly StyledProperty<double> MaximumThicknessProperty = AvaloniaProperty.Register<LineLayer, double>(nameof(MaximumThickness), 6, validate: x => double.IsFinite(x) && x > 0 && x <= 128);
    public static readonly StyledProperty<bool> IsCurvedProperty = AvaloniaProperty.Register<LineLayer, bool>(nameof(IsCurved), true);
    public static readonly StyledProperty<bool> ShowEndpointsProperty = AvaloniaProperty.Register<LineLayer, bool>(nameof(ShowEndpoints), true);
    public string? FromLongitudePath { get => GetValue(FromLongitudePathProperty); set => SetValue(FromLongitudePathProperty, value); }
    public string? FromLatitudePath { get => GetValue(FromLatitudePathProperty); set => SetValue(FromLatitudePathProperty, value); }
    public string? ToLongitudePath { get => GetValue(ToLongitudePathProperty); set => SetValue(ToLongitudePathProperty, value); }
    public string? ToLatitudePath { get => GetValue(ToLatitudePathProperty); set => SetValue(ToLatitudePathProperty, value); }
    public double MinimumThickness { get => GetValue(MinimumThicknessProperty); set => SetValue(MinimumThicknessProperty, value); }
    public double MaximumThickness { get => GetValue(MaximumThicknessProperty); set => SetValue(MaximumThicknessProperty, value); }
    public bool IsCurved { get => GetValue(IsCurvedProperty); set => SetValue(IsCurvedProperty, value); }
    public bool ShowEndpoints { get => GetValue(ShowEndpointsProperty); set => SetValue(ShowEndpointsProperty, value); }
}

/// <summary>Typed lines, polylines, polygons with holes, screen circles and screen arcs. Each vector is an original selectable item.</summary>
public sealed class VectorLayer : MapLayer
{
    public VectorLayer() { Stroke = Brush.Parse("#2563EB"); }
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<VectorLayer, double>(nameof(StrokeThickness), 2, validate: x => double.IsFinite(x) && x > 0 && x <= 128);
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
}

/// <summary>A geographic pie per original business row. SlicesPath resolves a bounded nonnegative sequence; radius is a screen DIP preference.</summary>
public sealed class PieLayer : MapLayer
{
    public static readonly StyledProperty<string?> SlicesPathProperty = AvaloniaProperty.Register<PieLayer, string?>(nameof(SlicesPath), "Slices");
    public static readonly StyledProperty<string?> SliceValuePathProperty = AvaloniaProperty.Register<PieLayer, string?>(nameof(SliceValuePath), "Value");
    public static readonly StyledProperty<string?> SliceLabelPathProperty = AvaloniaProperty.Register<PieLayer, string?>(nameof(SliceLabelPath), "Label");
    public static readonly StyledProperty<double> RadiusProperty = AvaloniaProperty.Register<PieLayer, double>(nameof(Radius), 20, validate: x => double.IsFinite(x) && x > 0 && x <= 1024);
    public string? SlicesPath { get => GetValue(SlicesPathProperty); set => SetValue(SlicesPathProperty, value); }
    public string? SliceValuePath { get => GetValue(SliceValuePathProperty); set => SetValue(SliceValuePathProperty, value); }
    public string? SliceLabelPath { get => GetValue(SliceLabelPathProperty); set => SetValue(SliceLabelPathProperty, value); }
    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
}
