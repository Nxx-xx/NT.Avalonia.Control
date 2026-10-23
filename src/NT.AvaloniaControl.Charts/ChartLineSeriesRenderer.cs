using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal sealed partial class ChartLineSeriesRenderer(bool band, bool range) : IChartSeriesRenderer
{
    internal static readonly ChartLineSeriesRenderer Line = new(false, false);
    internal static readonly ChartLineSeriesRenderer Area = new(true, false);
    internal static readonly ChartLineSeriesRenderer RangeArea = new(true, true);
    public bool IncludesZero => false;
    public bool UsesCategorySlots => false;
    public ChartLegendSymbol LegendSymbol => band ? ChartLegendSymbol.Rectangle : ChartLegendSymbol.Line;

    public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data)
    {
        var bounds = ChartDataExtrema.For(data).Bounds;
        if (!band || bounds is not { } value) return bounds;
        var low = value.YMinimum; var high = value.YMaximum;
        if (ChartLowerExtrema.For(data).BoundsWithFallback(series is AreaSeries area ? area.Baseline : double.NaN) is { } lower)
        {
            low = Math.Min(low, lower.Minimum); high = Math.Max(high, lower.Maximum);
        }
        return value with { YMinimum = low, YMaximum = high };
    }

    public void Render(DrawingContext drawing, ChartSeriesRenderContext context)
    {
        RenderGeometry(drawing, context);
        if (ChartMarkerGeometry.Radius(context.Series) > 0)
            CartesianSeriesRenderer.Scatter.Render(drawing, context);
    }

    private void RenderGeometry(DrawingContext drawing, ChartSeriesRenderContext context)
    {
        var figures = ChartLineGeometry.Build(context);
        var outline = new StreamGeometry();
        using (var path = outline.Open())
            foreach (var figure in figures)
            {
                Append(path, figure.Upper);
                if (range) Append(path, figure.Lower);
            }
        if (band)
        {
            var fill = new StreamGeometry();
            using (var path = fill.Open())
                foreach (var figure in figures)
                {
                    path.BeginFigure(figure.Upper[0].Start, true);
                    foreach (var part in figure.Upper) part.Append(path);
                    path.LineTo(figure.Lower[^1].End);
                    for (var i = figure.Lower.Count - 1; i >= 0; i--) figure.Lower[i].Append(path, reverse: true);
                    path.EndFigure(true);
                }
            if (context.Series.Fill is { } explicitFill) drawing.DrawGeometry(explicitFill, null, fill);
            else
            {
                using var opacity = drawing.PushOpacity(.25);
                drawing.DrawGeometry(context.Brush, null, fill);
            }
        }
        if (context.Series.StrokeThickness > 0)
            drawing.DrawGeometry(null, new Pen(context.Series.Stroke ?? context.Brush,
                ChartMarkerGeometry.Size(context.Series.StrokeThickness)), outline);
    }

    private static void Append(StreamGeometryContext path, List<ChartLinePathPart> parts)
    {
        if (parts.Count == 0) return;
        path.BeginFigure(parts[0].Start, false);
        foreach (var part in parts) part.Append(path);
        path.EndFigure(false);
    }

    // Point selection/commands retain canonical source identity. Segment readouts
    // are handled separately by CartesianChart using this renderer's primitives.
    public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance)
        => CartesianSeriesRenderer.Scatter.HitTest(context, position, tolerance);
}
