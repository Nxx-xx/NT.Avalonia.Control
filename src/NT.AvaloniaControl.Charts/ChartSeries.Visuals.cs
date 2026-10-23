using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public enum ChartMarkerShape { Circle, Square, Rectangle, Diamond, Triangle, InvertedTriangle, Cross, Pentagon, HorizontalLine, VerticalLine }
public enum ChartDataLabelPosition { Auto, Above, Below, Left, Right, Center, InsideEnd }
public enum ChartDataLabelOverlap { Hide, Allow }

/// <summary>A label candidate retaining the original item and stable source identity.</summary>
public sealed record ChartDataLabelContext(ChartSeries Series, ChartDataPoint Data, Point Position, string FormattedX, string FormattedY)
{
    /// <summary>The formatter's result, or the series value axis's formatted value. A template can use the original Data even when Text is null.</summary>
    public string? Text { get; init; }
}

public abstract partial class ChartSeries
{
    public static readonly StyledProperty<bool> ShowMarkersProperty = AvaloniaProperty.Register<ChartSeries, bool>(nameof(ShowMarkers), true);
    public static readonly StyledProperty<ChartMarkerShape> MarkerShapeProperty = AvaloniaProperty.Register<ChartSeries, ChartMarkerShape>(nameof(MarkerShape), validate: Enum.IsDefined);
    public static readonly StyledProperty<IBrush?> MarkerFillProperty = AvaloniaProperty.Register<ChartSeries, IBrush?>(nameof(MarkerFill));
    public static readonly StyledProperty<IBrush?> MarkerStrokeProperty = AvaloniaProperty.Register<ChartSeries, IBrush?>(nameof(MarkerStroke));
    public static readonly StyledProperty<double?> MarkerStrokeThicknessProperty = AvaloniaProperty.Register<ChartSeries, double?>(nameof(MarkerStrokeThickness),
        validate: value => value is null || double.IsFinite(value.Value) && value >= 0);
    public static readonly StyledProperty<bool> ShowDataLabelsProperty = AvaloniaProperty.Register<ChartSeries, bool>(nameof(ShowDataLabels));
    public static readonly StyledProperty<IDataTemplate?> DataLabelTemplateProperty =
        AvaloniaProperty.Register<ChartSeries, IDataTemplate?>(nameof(DataLabelTemplate));
    public static readonly StyledProperty<string?> DataLabelFormatProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(DataLabelFormat));
    public static readonly StyledProperty<Func<ChartDataLabelContext, string?>?> DataLabelFormatterProperty =
        AvaloniaProperty.Register<ChartSeries, Func<ChartDataLabelContext, string?>?>(nameof(DataLabelFormatter));
    public static readonly StyledProperty<ChartDataLabelPosition> DataLabelPositionProperty =
        AvaloniaProperty.Register<ChartSeries, ChartDataLabelPosition>(nameof(DataLabelPosition), validate: Enum.IsDefined);
    public static readonly StyledProperty<ChartDataLabelOverlap> DataLabelOverlapProperty =
        AvaloniaProperty.Register<ChartSeries, ChartDataLabelOverlap>(nameof(DataLabelOverlap), validate: Enum.IsDefined);
    public static readonly StyledProperty<Vector> DataLabelOffsetProperty = AvaloniaProperty.Register<ChartSeries, Vector>(nameof(DataLabelOffset),
        validate: value => double.IsFinite(value.X) && double.IsFinite(value.Y));
    public static readonly StyledProperty<IBrush?> DataLabelForegroundProperty = AvaloniaProperty.Register<ChartSeries, IBrush?>(nameof(DataLabelForeground));
    public static readonly StyledProperty<IBrush?> DataLabelBackgroundProperty = AvaloniaProperty.Register<ChartSeries, IBrush?>(nameof(DataLabelBackground));
    public static readonly StyledProperty<double?> DataLabelFontSizeProperty = AvaloniaProperty.Register<ChartSeries, double?>(nameof(DataLabelFontSize),
        validate: value => value is null || double.IsFinite(value.Value) && value > 0);
    public static readonly StyledProperty<FontFamily?> DataLabelFontFamilyProperty = AvaloniaProperty.Register<ChartSeries, FontFamily?>(nameof(DataLabelFontFamily));
    public static readonly StyledProperty<int> MaxDataLabelsProperty = AvaloniaProperty.Register<ChartSeries, int>(nameof(MaxDataLabels), 500,
        validate: value => value >= 0 && value <= 10_000);
    public bool ShowMarkers { get => GetValue(ShowMarkersProperty); set => SetValue(ShowMarkersProperty, value); }
    public ChartMarkerShape MarkerShape { get => GetValue(MarkerShapeProperty); set => SetValue(MarkerShapeProperty, value); }
    public IBrush? MarkerFill { get => GetValue(MarkerFillProperty); set => SetValue(MarkerFillProperty, value); }
    public IBrush? MarkerStroke { get => GetValue(MarkerStrokeProperty); set => SetValue(MarkerStrokeProperty, value); }
    public double? MarkerStrokeThickness { get => GetValue(MarkerStrokeThicknessProperty); set => SetValue(MarkerStrokeThicknessProperty, value); }
    public bool ShowDataLabels { get => GetValue(ShowDataLabelsProperty); set => SetValue(ShowDataLabelsProperty, value); }
    /// <summary>Optional passive control template with ChartDataLabelContext. Null uses drawn text; a null Build result hides that candidate.</summary>
    public IDataTemplate? DataLabelTemplate { get => GetValue(DataLabelTemplateProperty); set => SetValue(DataLabelTemplateProperty, value); }
    /// <summary>Numeric value format; horizontal comparison series use X, other series use Y. Null or invalid formats use that value axis's formatted label.</summary>
    public string? DataLabelFormat { get => GetValue(DataLabelFormatProperty); set => SetValue(DataLabelFormatProperty, value); }
    public Func<ChartDataLabelContext, string?>? DataLabelFormatter { get => GetValue(DataLabelFormatterProperty); set => SetValue(DataLabelFormatterProperty, value); }
    public ChartDataLabelPosition DataLabelPosition { get => GetValue(DataLabelPositionProperty); set => SetValue(DataLabelPositionProperty, value); }
    public ChartDataLabelOverlap DataLabelOverlap { get => GetValue(DataLabelOverlapProperty); set => SetValue(DataLabelOverlapProperty, value); }
    public Vector DataLabelOffset { get => GetValue(DataLabelOffsetProperty); set => SetValue(DataLabelOffsetProperty, value); }
    public IBrush? DataLabelForeground { get => GetValue(DataLabelForegroundProperty); set => SetValue(DataLabelForegroundProperty, value); }
    public IBrush? DataLabelBackground { get => GetValue(DataLabelBackgroundProperty); set => SetValue(DataLabelBackgroundProperty, value); }
    public double? DataLabelFontSize { get => GetValue(DataLabelFontSizeProperty); set => SetValue(DataLabelFontSizeProperty, value); }
    public FontFamily? DataLabelFontFamily { get => GetValue(DataLabelFontFamilyProperty); set => SetValue(DataLabelFontFamilyProperty, value); }
    /// <summary>Maximum visible label candidates per series. Dense data is sampled at deterministic original indices; zero disables labels.</summary>
    public int MaxDataLabels { get => GetValue(MaxDataLabelsProperty); set => SetValue(MaxDataLabelsProperty, value); }
}

public sealed partial class BarSeries
{
    static BarSeries() => ShowMarkersProperty.OverrideDefaultValue<BarSeries>(false);
}

internal static class ChartMarkerGeometry
{
    internal static double Size(double value) => double.IsFinite(value) && value >= 0 ? Math.Min(100, value) : 0;
    internal static double Radius(ChartSeries series) => series.ShowMarkers ? Size(series.MarkerSize) / 2 : 0;
    internal static double StrokeThickness(ChartSeries series) => Size(series.MarkerStrokeThickness ?? series.StrokeThickness);
    internal static double HitRadius(ChartSeries series)
    {
        var radius = Radius(series);
        if (!series.ShowMarkers) return 0;
        if (series.MarkerShape == ChartMarkerShape.Circle) return radius + StrokeThickness(series) / 2;
        if (radius <= 0) return 0;
        return (series.MarkerShape switch
        {
            ChartMarkerShape.Square or ChartMarkerShape.Cross => radius * Math.Sqrt(2),
            ChartMarkerShape.Rectangle => radius * Math.Sqrt(1.36),
            _ => radius,
        }) + StrokeThickness(series) / 2;
    }
    internal static Geometry? Create(ChartMarkerShape shape, double radius)
    {
        if (shape == ChartMarkerShape.Circle) return null;
        if (shape is ChartMarkerShape.Square or ChartMarkerShape.Rectangle)
            return new RectangleGeometry(new Rect(-radius, -radius * (shape == ChartMarkerShape.Rectangle ? .6 : 1), radius * 2,
                radius * (shape == ChartMarkerShape.Rectangle ? 1.2 : 2)));
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        if (shape is ChartMarkerShape.Cross or ChartMarkerShape.HorizontalLine or ChartMarkerShape.VerticalLine)
        {
            if (shape == ChartMarkerShape.Cross)
            {
                path.BeginFigure(new Point(-radius, -radius), false); path.LineTo(new Point(radius, radius)); path.EndFigure(false);
                path.BeginFigure(new Point(radius, -radius), false); path.LineTo(new Point(-radius, radius)); path.EndFigure(false);
            }
            else
            {
                var horizontal = shape == ChartMarkerShape.HorizontalLine;
                path.BeginFigure(horizontal ? new Point(-radius, 0) : new Point(0, -radius), false);
                path.LineTo(horizontal ? new Point(radius, 0) : new Point(0, radius)); path.EndFigure(false);
            }
            return geometry;
        }
        var sides = shape is ChartMarkerShape.Triangle or ChartMarkerShape.InvertedTriangle ? 3 : shape == ChartMarkerShape.Pentagon ? 5 : 4;
        var angle = shape == ChartMarkerShape.InvertedTriangle ? Math.PI / 2 : -Math.PI / 2;
        for (var index = 0; index < sides; index++)
        {
            var point = new Point(Math.Cos(angle + index * Math.Tau / sides) * radius, Math.Sin(angle + index * Math.Tau / sides) * radius);
            if (index == 0) path.BeginFigure(point, true); else path.LineTo(point);
        }
        path.EndFigure(true);
        return geometry;
    }
    internal static void Draw(DrawingContext drawing, ChartMarkerShape shape, double radius, IBrush fill, Pen pen, Point center, Geometry? geometry)
    {
        if (shape == ChartMarkerShape.Circle) { drawing.DrawEllipse(fill, pen, center, radius, radius); return; }
        DrawTransformed(drawing, shape, fill, pen, center, geometry!);
    }
    private static void DrawTransformed(DrawingContext drawing, ChartMarkerShape shape, IBrush fill, Pen pen, Point center, Geometry geometry)
    {
        using var transform = drawing.PushTransform(Matrix.CreateTranslation(center.X, center.Y));
        drawing.DrawGeometry(shape is ChartMarkerShape.Cross or ChartMarkerShape.HorizontalLine or ChartMarkerShape.VerticalLine ? null : fill, pen, geometry);
    }
}
