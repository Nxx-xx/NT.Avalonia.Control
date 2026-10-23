using System.Globalization;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public enum ChartAnnotationLayer { BehindSeries, AboveSeries }
public enum ChartAnnotationCoordinateUnit { Data, Pixel }

/// <summary>Chart-local drawing inputs; Pixel coordinates are DIPs relative to PlotArea.TopLeft.</summary>
public sealed record ChartAnnotationRenderContext(Rect PlotArea, IChartProjection Projection, Typeface Typeface,
    double FontSize, IBrush Foreground)
{
    public Point Project(double x, double y) => Projection.Project(new ChartPoint(x, y));
    public double MapX(double value) => Projection.MapX(value);
    public double MapY(double value) => Projection.MapY(value);
}

/// <summary>An owned annotation. Named data axes select their dimension and may participate without a series.</summary>
public abstract class ChartAnnotation : StyledElement
{
    public static readonly StyledProperty<string?> XAxisKeyProperty = AvaloniaProperty.Register<ChartAnnotation, string?>(nameof(XAxisKey));
    public static readonly StyledProperty<string?> YAxisKeyProperty = AvaloniaProperty.Register<ChartAnnotation, string?>(nameof(YAxisKey));
    public static readonly StyledProperty<ChartAnnotationCoordinateUnit> CoordinateUnitProperty =
        AvaloniaProperty.Register<ChartAnnotation, ChartAnnotationCoordinateUnit>(nameof(CoordinateUnit), validate: Enum.IsDefined);
    public static readonly StyledProperty<ChartAnnotationLayer> LayerProperty =
        AvaloniaProperty.Register<ChartAnnotation, ChartAnnotationLayer>(nameof(Layer), ChartAnnotationLayer.AboveSeries, validate: Enum.IsDefined);
    public static readonly StyledProperty<bool> ClipToPlotAreaProperty = AvaloniaProperty.Register<ChartAnnotation, bool>(nameof(ClipToPlotArea), true);
    public static readonly StyledProperty<bool> IsVisibleProperty = AvaloniaProperty.Register<ChartAnnotation, bool>(nameof(IsVisible), true);
    public static readonly StyledProperty<double> OpacityProperty = AvaloniaProperty.Register<ChartAnnotation, double>(nameof(Opacity), 1,
        validate: value => double.IsFinite(value) && value >= 0 && value <= 1);
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<ChartAnnotation, IBrush?>(nameof(Stroke));
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<ChartAnnotation, IBrush?>(nameof(Fill));
    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<ChartAnnotation, IBrush?>(nameof(Foreground));
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<ChartAnnotation, double>(nameof(StrokeThickness), 1,
        validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<IDashStyle?> DashStyleProperty = AvaloniaProperty.Register<ChartAnnotation, IDashStyle?>(nameof(DashStyle));
    public static readonly StyledProperty<double?> FontSizeProperty = AvaloniaProperty.Register<ChartAnnotation, double?>(nameof(FontSize),
        validate: value => value is null || double.IsFinite(value.Value) && value > 0);
    public static readonly StyledProperty<FontFamily?> FontFamilyProperty = AvaloniaProperty.Register<ChartAnnotation, FontFamily?>(nameof(FontFamily));
    public string? XAxisKey { get => GetValue(XAxisKeyProperty); set => SetValue(XAxisKeyProperty, value); }
    public string? YAxisKey { get => GetValue(YAxisKeyProperty); set => SetValue(YAxisKeyProperty, value); }
    public ChartAnnotationCoordinateUnit CoordinateUnit { get => GetValue(CoordinateUnitProperty); set => SetValue(CoordinateUnitProperty, value); }
    public ChartAnnotationLayer Layer { get => GetValue(LayerProperty); set => SetValue(LayerProperty, value); }
    public bool ClipToPlotArea { get => GetValue(ClipToPlotAreaProperty); set => SetValue(ClipToPlotAreaProperty, value); }
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
    public double Opacity { get => GetValue(OpacityProperty); set => SetValue(OpacityProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public IDashStyle? DashStyle { get => GetValue(DashStyleProperty); set => SetValue(DashStyleProperty, value); }
    public double? FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontFamily? FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    internal event EventHandler? Changed;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); Changed?.Invoke(this, change); }
    public abstract void Render(DrawingContext drawing, ChartAnnotationRenderContext context);
    protected Pen MakePen(ChartAnnotationRenderContext context) => new(Stroke ?? Foreground ?? context.Foreground, StrokeThickness, DashStyle);
    protected void DrawText(DrawingContext drawing, ChartAnnotationRenderContext context, string? text, Point position)
    {
        if (string.IsNullOrEmpty(text) || !Finite(position)) return;
        drawing.DrawText(CreateFormattedText(context, text), position);
    }
    /// <summary>Creates label text using this annotation's typography and the chart's inherited defaults.</summary>
    protected FormattedText CreateFormattedText(ChartAnnotationRenderContext context, string text) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            FontFamily is { } family ? new Typeface(family, context.Typeface.Style, context.Typeface.Weight) : context.Typeface,
            FontSize ?? context.FontSize, Foreground ?? context.Foreground);
    protected static bool Finite(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
}

public sealed class LineAnnotation : ChartAnnotation
{
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<LineAnnotation, Orientation>(nameof(Orientation), Orientation.Horizontal, validate: Enum.IsDefined);
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<LineAnnotation, double>(nameof(Value));
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<LineAnnotation, string?>(nameof(Label));
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public override void Render(DrawingContext drawing, ChartAnnotationRenderContext context)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var value = horizontal ? context.MapY(Value) : context.MapX(Value);
        if (!double.IsFinite(value)) return;
        var plot = context.PlotArea;
        drawing.DrawLine(MakePen(context), horizontal ? new Point(plot.Left, value) : new Point(value, plot.Top),
            horizontal ? new Point(plot.Right, value) : new Point(value, plot.Bottom));
        DrawText(drawing, context, Label, horizontal ? new Point(plot.Left + 5, value + 3) : new Point(value + 5, plot.Top + 3));
    }
}

public sealed class BandAnnotation : ChartAnnotation
{
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<BandAnnotation, Orientation>(nameof(Orientation), Orientation.Horizontal, validate: Enum.IsDefined);
    public static readonly StyledProperty<double> FromValueProperty = AvaloniaProperty.Register<BandAnnotation, double>(nameof(FromValue));
    public static readonly StyledProperty<double> ToValueProperty = AvaloniaProperty.Register<BandAnnotation, double>(nameof(ToValue), 1);
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<BandAnnotation, string?>(nameof(Label));
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public double FromValue { get => GetValue(FromValueProperty); set => SetValue(FromValueProperty, value); }
    public double ToValue { get => GetValue(ToValueProperty); set => SetValue(ToValueProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public override void Render(DrawingContext drawing, ChartAnnotationRenderContext context)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var first = horizontal ? context.MapY(FromValue) : context.MapX(FromValue);
        var last = horizontal ? context.MapY(ToValue) : context.MapX(ToValue);
        if (!double.IsFinite(first) || !double.IsFinite(last) || first == last) return;
        var plot = context.PlotArea;
        var rect = horizontal ? new Rect(plot.Left, Math.Min(first, last), plot.Width, Math.Abs(last - first)) :
            new Rect(Math.Min(first, last), plot.Top, Math.Abs(last - first), plot.Height);
        if (!double.IsFinite(rect.Width) || !double.IsFinite(rect.Height)) return;
        drawing.DrawRectangle(Fill, Stroke is null ? null : MakePen(context), rect);
        DrawText(drawing, context, Label, rect.TopLeft + new Vector(5, 3));
    }
}

public sealed class TextAnnotation : ChartAnnotation
{
    public static readonly StyledProperty<double> XProperty = AvaloniaProperty.Register<TextAnnotation, double>(nameof(X));
    public static readonly StyledProperty<double> YProperty = AvaloniaProperty.Register<TextAnnotation, double>(nameof(Y));
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<TextAnnotation, string?>(nameof(Text));
    public double X { get => GetValue(XProperty); set => SetValue(XProperty, value); }
    public double Y { get => GetValue(YProperty); set => SetValue(YProperty, value); }
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public override void Render(DrawingContext drawing, ChartAnnotationRenderContext context) => DrawText(drawing, context, Text, context.Project(X, Y));
}

public sealed class CustomAnnotation : ChartAnnotation
{
    public static readonly StyledProperty<Action<DrawingContext, ChartAnnotationRenderContext>?> RendererProperty =
        AvaloniaProperty.Register<CustomAnnotation, Action<DrawingContext, ChartAnnotationRenderContext>?>(nameof(Renderer));
    public Action<DrawingContext, ChartAnnotationRenderContext>? Renderer { get => GetValue(RendererProperty); set => SetValue(RendererProperty, value); }
    public override void Render(DrawingContext drawing, ChartAnnotationRenderContext context) => Renderer?.Invoke(drawing, context);
}
