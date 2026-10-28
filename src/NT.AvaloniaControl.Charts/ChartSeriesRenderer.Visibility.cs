using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal sealed partial class CartesianSeriesRenderer
{
    private static void RenderSampledLine(DrawingContext drawing, ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, ChartLineSampling sampling)
    {
        var linear = context.Projection is CartesianChart.CartesianProjection projection &&
            (projection.TryGetLinearLineProjection(context.Data, out var mapping) ||
             projection.TryGetGappedLinearProjection(context.Data, out mapping)) ? mapping : (InRangeLinearProjection?)null;
        var geometry = new StreamGeometry(); var started = false; var segments = 0;
        using (var path = geometry.Open())
        {
            for (var i = 0; i < sampling.Count; i++)
            {
                if (sampling.StartsFigure(i) && started) { path.EndFigure(false); started = false; }
                var ordinal = sampling[i];
                var point = coordinates.IsEmpty ? context.Data.GetValue(ordinal).Point : coordinates[ordinal];
                var screen = linear is { } fast ? fast.Project(point) : context.Projection.Project(point);
                if (!started) { path.BeginFigure(screen, false); started = true; }
                else { path.LineTo(screen); segments++; }
            }
            if (started) path.EndFigure(false);
        }
        ChartVisibilityQuery.RecordProjected(sampling.Count);
        if (segments > 0)
        {
            drawing.DrawGeometry(null, new Pen(context.Brush, Size(context.Series.StrokeThickness)), geometry);
            // Immediate native backends can pass only the path handle to a long
            // unmanaged draw. Keep its owning geometry alive until that draw returns.
            GC.KeepAlive(geometry);
        }
    }
    private static double MarkerExtent(ChartSeries series, double radius) =>
        series.MarkerShape == ChartMarkerShape.Circle ? radius : ChartMarkerGeometry.HitRadius(series);

    private static ChartVisibleOrdinals VisibleBars(ChartSeriesRenderContext context)
    {
        var width = context.GroupWidth / Math.Max(1, context.GroupCount);
        var barWidth = Math.Max(.5, width * .86);
        var offset = -context.GroupWidth / 2 + context.GroupIndex * width + (width - barWidth) / 2;
        var extent = Math.Max(Math.Abs(offset), Math.Abs(offset + barWidth));
        extent += MarkerExtent(context.Series, ChartMarkerGeometry.Radius(context.Series)) + 1;
        // A bar can intersect the view with its value endpoint far above/below it.
        return ChartVisibilityQuery.Points(context, inflation: extent, xOnly: true);
    }

    private static int RenderVisibleLine(StreamGeometryContext path, ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, ChartVisibleOrdinals visible)
    {
        var linear = context.Projection is CartesianChart.CartesianProjection projection &&
            (projection.TryGetLinearLineProjection(context.Data, out var mapping) ||
             projection.TryGetGappedLinearProjection(context.Data, out mapping)) ? mapping : (InRangeLinearProjection?)null;
        var started = false; var previous = -2; var segments = 0; long projected = 0;
        foreach (var ordinal in visible)
        {
            if (started && ordinal != previous + 1) { path.EndFigure(false); started = false; }
            previous = ordinal;
            var point = coordinates.IsEmpty ? context.Data.GetValue(ordinal).Point : coordinates[ordinal];
            if (!Valid(point)) { if (started) path.EndFigure(false); started = false; continue; }
            var screen = linear is { } fast ? fast.Project(point) : context.Projection.Project(point);
            projected++;
            if (!Valid(screen)) { if (started) path.EndFigure(false); started = false; continue; }
            if (!started) { path.BeginFigure(screen, false); started = true; }
            else { path.LineTo(screen); segments++; }
        }
        if (started) path.EndFigure(false);
        ChartVisibilityQuery.RecordProjected(projected);
        return segments;
    }

    private static void RenderVisibleMarkers(DrawingContext drawing, ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, double radius, ChartVisibleOrdinals visible)
    {
        var series = context.Series; var shape = series.MarkerShape;
        var pen = shape == ChartMarkerShape.Circle
            ? new Pen(series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(series))
            : new Pen(series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(series),
                lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var fill = series.MarkerFill ?? series.Fill ?? context.Brush;
        var geometry = ChartMarkerGeometry.Create(shape, radius);
        var plot = context.PlotArea.Inflate(MarkerExtent(series, radius));
        // Circle fast projection is the same one used by the original full loop;
        // transformed symbols retain their original general projection arithmetic.
        var linear = shape == ChartMarkerShape.Circle && context.Projection is CartesianChart.CartesianProjection projection &&
            projection.TryGetLinearProjection(context.Data, out var mapping) ? mapping : (InRangeLinearProjection?)null;
        long projected = 0;
        foreach (var ordinal in visible)
        {
            var point = coordinates.IsEmpty ? context.Data.GetValue(ordinal).Point : coordinates[ordinal];
            if (!Valid(point)) continue;
            var position = linear is { } fast ? fast.Project(point) : context.Projection.Project(point);
            projected++;
            if (Valid(position) && plot.Contains(position))
                ChartMarkerGeometry.Draw(drawing, shape, radius, fill, pen, position, geometry);
        }
        ChartVisibilityQuery.RecordProjected(projected);
    }

    private static void RenderVisibleBars(DrawingContext drawing, ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, ChartVisibleOrdinals visible)
    {
        // Retain native rectangle/ellipse primitives and their original paint order.
        BarGeometries.Remove(context.Series);
        var radius = ChartMarkerGeometry.Radius(context.Series);
        if (radius > 0) CreateRetainedBarsWithMarkers(context, coordinates, radius, visible).Draw(drawing);
        else CreateRetainedBars(context, coordinates, visible).Draw(drawing);
    }
}
