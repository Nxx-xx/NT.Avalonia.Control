using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal sealed partial class ChartPointSeriesRenderer : IChartSeriesRenderer
{
    internal static readonly ChartPointSeriesRenderer Instance = new();
    public bool IncludesZero => false;
    public bool UsesCategorySlots => false;
    public ChartLegendSymbol LegendSymbol => ChartLegendSymbol.Circle;
    public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data) => ChartDataExtrema.For(data).Bounds;

    public void Render(DrawingContext drawing, ChartSeriesRenderContext context)
    {
        if (context.Series is ScatterLineSeries { ShowLine: true, StrokeThickness: > 0 }) RenderLine(drawing, context);
        if (!context.Series.ShowMarkers) return;
        var geometries = new Dictionary<double, Geometry?>();
        foreach (var ordinal in ChartPointGeometry.PointOrder(context))
        {
            if (!ChartPointGeometry.TryCreate(context, ordinal, out var shape) || !shape.Bounds.Intersects(context.PlotArea)) continue;
            var series = context.Series;
            var fill = context.Data.Points[ordinal].PointFill ?? series.MarkerFill ?? series.Fill ?? context.Brush;
            var pen = new Pen(series.MarkerStroke ?? series.Stroke ?? fill, shape.StrokeThickness,
                lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            Geometry? geometry = null;
            if (shape.Shape != ChartMarkerShape.Circle && !geometries.TryGetValue(shape.Radius, out geometry))
            {
                geometry = ChartMarkerGeometry.Create(shape.Shape, shape.Radius);
                if (geometries.Count < 256) geometries.Add(shape.Radius, geometry);
            }
            ChartMarkerGeometry.Draw(drawing, shape.Shape, shape.Radius, fill, pen, shape.Center, geometry);
        }
    }

    private static void RenderLine(DrawingContext drawing, ChartSeriesRenderContext context)
    {
        var geometry = new StreamGeometry();
        var segments = 0;
        using (var path = geometry.Open())
        {
            foreach (var figure in ChartLineGeometry.Build(context))
            {
                if (figure.Upper.Count == 0) continue;
                path.BeginFigure(figure.Upper[0].Start, false);
                foreach (var part in figure.Upper) { part.Append(path); segments++; }
                path.EndFigure(false);
            }
        }
        if (segments > 0) drawing.DrawGeometry(null, new Pen(context.Series.Stroke ?? context.Brush,
            ChartMarkerGeometry.Size(context.Series.StrokeThickness)), geometry);
    }

    public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance)
    {
        if (!context.Series.ShowMarkers || !double.IsFinite(position.X) || !double.IsFinite(position.Y) || !double.IsFinite(tolerance) || tolerance < 0 || !context.PlotArea.Contains(position)) return null;
        var regions = context.PointHitRegions;
        if (regions is not null && !regions.Any(region => ContainsInclusive(region, position))) return null;
        var index = ChartPointSceneCache.ForHitTest(context);
        var candidates = index.CandidateRange(position, tolerance);
        if (candidates.End - candidates.Start > 128)
            return index.HitTest(context, position, tolerance);
        var bestDistance = double.PositiveInfinity;
        var bestPaintOrder = -1; var bestSourceIndex = -1; var bestPosition = default(Point);
        for (var candidate = candidates.Start; candidate < candidates.End; candidate++)
        {
            var entry = index[candidate]; var shape = entry.Shape;
            var distance = double.PositiveInfinity;
            if (regions is null) distance = shape.DistanceSquared(position, context.PlotArea);
            else
                foreach (var region in regions)
                    if (shape.Bounds.Intersects(region)) distance = Math.Min(distance, shape.DistanceSquared(position, region));
            if (!double.IsFinite(distance) || distance > tolerance * tolerance || distance > bestDistance ||
                distance == bestDistance && entry.PaintOrder < bestPaintOrder) continue;
            // Spatial order is independent from paint order. Preserve equality's
            // original last-painted choice, including equal-size stable ordering.
            bestDistance = distance; bestPaintOrder = entry.PaintOrder; bestSourceIndex = entry.SourceIndex;
            bestPosition = new(Math.Clamp(shape.Center.X, context.PlotArea.Left, context.PlotArea.Right),
                Math.Clamp(shape.Center.Y, context.PlotArea.Top, context.PlotArea.Bottom));
        }
        return bestPaintOrder < 0 ? null : new(bestSourceIndex, bestPosition, bestDistance);
    }

    private static bool ContainsInclusive(Rect region, Point position) => position.X >= region.Left && position.X <= region.Right &&
        position.Y >= region.Top && position.Y <= region.Bottom;
}
