using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal sealed partial class ChartLineSeriesRenderer
{
    internal ChartRetainedSeriesDrawing CreateRetainedDrawing(ChartSeriesRenderContext context)
    {
        var group = new DrawingGroup();
        using (var recording = group.Open()) RenderGeometry(recording, context);
        var geometry = new ChartRetainedSeriesDrawing.Recorded(group, ChartRetainedSeriesDrawing.EstimateShapedGeometry(context));
        return ChartMarkerGeometry.Radius(context.Series) > 0
            ? new ChartRetainedSeriesDrawing.Composite(geometry, CartesianSeriesRenderer.Scatter.CreateRetainedDrawing(context))
            : geometry;
    }
}

internal sealed partial class ChartPointSeriesRenderer
{
    private readonly record struct RetainedPointMark(Point Center, double Radius, IBrush Fill, Pen Pen, Geometry? Geometry);

    internal ChartRetainedSeriesDrawing CreateRetainedDrawing(ChartSeriesRenderContext context)
    {
        DrawingGroup? line = null;
        var lineBytes = 0L;
        if (context.Series is ScatterLineSeries { ShowLine: true, StrokeThickness: > 0 })
        {
            line = new DrawingGroup();
            using var recording = line.Open();
            RenderLine(recording, context);
            lineBytes = ChartRetainedSeriesDrawing.EstimateShapedGeometry(context);
        }
        var marks = context.Series.ShowMarkers ? new RetainedPointMark[ChartPointSceneCache.VisiblePointCount(context)] : [];
        var count = 0;
        var geometries = new Dictionary<double, Geometry?>();
        var pens = new Dictionary<(IBrush Brush, double Thickness), Pen>();
        if (context.Series.ShowMarkers)
            foreach (var ordinal in ChartPointGeometry.PointOrder(context))
            {
                if (!ChartPointGeometry.TryCreate(context, ordinal, out var shape) || !shape.Bounds.Intersects(context.PlotArea)) continue;
                var series = context.Series;
                var fill = context.Data.Points[ordinal].PointFill ?? series.MarkerFill ?? series.Fill ?? context.Brush;
                var penKey = (series.MarkerStroke ?? series.Stroke ?? fill, shape.StrokeThickness);
                if (!pens.TryGetValue(penKey, out var pen))
                {
                    pen = new Pen(penKey.Item1, penKey.StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                    pens.Add(penKey, pen);
                }
                Geometry? geometry = null;
                if (shape.Shape != ChartMarkerShape.Circle && !geometries.TryGetValue(shape.Radius, out geometry))
                {
                    geometry = ChartMarkerGeometry.Create(shape.Shape, shape.Radius);
                    if (geometries.Count < 256) geometries.Add(shape.Radius, geometry);
                }
                marks[count++] = new(shape.Center, shape.Radius, fill, pen, geometry);
            }
        if (marks.Length != count) Array.Resize(ref marks, count);
        return new RetainedPoints(line, marks, context.Series.MarkerShape, lineBytes);
    }

    private sealed class RetainedPoints(DrawingGroup? line, RetainedPointMark[] marks, ChartMarkerShape shape, long lineBytes) : ChartRetainedSeriesDrawing
    {
        internal override long EstimatedBytes => 512L + marks.LongLength * (shape == ChartMarkerShape.Circle ? 128 : 384) + lineBytes;
        public override void Draw(DrawingContext drawing)
        {
            line?.Draw(drawing);
            foreach (var mark in marks) ChartMarkerGeometry.Draw(drawing, shape, mark.Radius, mark.Fill, mark.Pen, mark.Center, mark.Geometry);
        }
    }
}

internal sealed partial class ChartComparisonSeriesRenderer
{
    private readonly record struct RetainedComparisonMark(ChartComparisonShape Shape, IBrush Fill, Pen? Stem,
        IBrush MarkerFill, bool EndMarker, bool StartMarker);
    private readonly record struct RetainedConnector(Point First, Point Last);

    internal ChartRetainedSeriesDrawing CreateRetainedDrawing(ChartSeriesRenderContext context)
    {
        var series = context.Series;
        var radius = ChartMarkerGeometry.Radius(series);
        var markerPlot = context.PlotArea.Inflate(ChartMarkerGeometry.HitRadius(series));
        var markerGeometry = ChartMarkerGeometry.Create(series.MarkerShape, radius);
        var markerPen = new Pen(series.MarkerStroke ?? series.Stroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(series),
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var ordinals = ChartComparisonGeometry.VisibleCandidates(context, connectors: kind == ChartComparisonKind.Waterfall &&
            series is WaterfallSeries { ShowConnectors: true, ConnectorThickness: > 0 });
        var marks = new RetainedComparisonMark[ordinals.Count];
        var markCount = 0;
        var stemPens = new Dictionary<IBrush, Pen>();
        var connectors = new List<RetainedConnector>();
        var connectorOptions = kind == ChartComparisonKind.Waterfall && series is WaterfallSeries
            { ShowConnectors: true, ConnectorThickness: > 0 } waterfall ? waterfall : null;
        ChartComparisonShape? previous = null;
        var previousOrdinal = -2;
        foreach (var ordinal in ordinals)
        {
            if (ordinal != previousOrdinal + 1) previous = null;
            previousOrdinal = ordinal;
            if (!ChartComparisonGeometry.TryCreate(context, ordinal, out var shape)) { previous = null; continue; }
            var fill = Fill(context, ordinal);
            Pen? stem = null;
            if (HasStem && series.StrokeThickness > 0)
            {
                var stroke = series.Stroke ?? fill;
                if (!stemPens.TryGetValue(stroke, out stem))
                {
                    stem = new Pen(stroke, series.StrokeThickness, lineCap: PenLineCap.Round);
                    stemPens.Add(stroke, stem);
                }
            }
            marks[markCount++] = new(shape, fill, stem, series.MarkerFill ?? (IsRectangle ? fill : series.Fill ?? fill),
                radius > 0 && markerPlot.Contains(shape.End), radius > 0 && kind == ChartComparisonKind.Variance && markerPlot.Contains(shape.Start));
            if (connectorOptions is not null && previous is { } before)
            {
                var forward = before.Horizontal ? shape.End.Y >= before.End.Y : shape.End.X >= before.End.X;
                var a = before.Horizontal ? new Point(before.End.X, forward ? before.Bounds.Bottom : before.Bounds.Top)
                    : new Point(forward ? before.Bounds.Right : before.Bounds.Left, before.End.Y);
                var b = shape.Horizontal ? new Point(before.End.X, forward ? shape.Bounds.Top : shape.Bounds.Bottom)
                    : new Point(forward ? shape.Bounds.Left : shape.Bounds.Right, before.End.Y);
                connectors.Add(new(a, b));
            }
            previous = shape;
        }
        if (marks.Length != markCount) Array.Resize(ref marks, markCount);
        Pen? connectorPen = connectorOptions is null ? null : new Pen(connectorOptions.ConnectorStroke ?? series.Stroke ?? context.Brush,
            connectorOptions.ConnectorThickness, DashStyle.Dash);
        DrawingGroup? cumulative = null;
        if (kind == ChartComparisonKind.Pareto && series is ParetoSeries { ShowCumulativeLine: true, CumulativeStrokeThickness: > 0 } pareto)
        {
            cumulative = new DrawingGroup();
            using var recording = cumulative.Open();
            RenderCumulative(recording, context, pareto);
        }
        // The cumulative line has independent data, coordinates and paint bounds.
        // Model 1 reserves its full secondary row count in O(1), conservatively
        // including rows cropped by a solid stroke, rather than using primary bars.
        var cumulativeBytes = cumulative is null ? 0 : 512L + (context.SecondaryData?.Count ?? 0) * 32L;
        return new RetainedComparison(marks, connectors.ToArray(), IsRectangle, radius, series.MarkerShape, markerGeometry,
            markerPen, connectorPen, cumulative, cumulativeBytes, context.PrimaryBreakClip, context.SecondaryBreakClip);
    }

    private sealed class RetainedComparison(RetainedComparisonMark[] marks, RetainedConnector[] connectors, bool rectangles,
        double radius, ChartMarkerShape markerShape, Geometry? markerGeometry, Pen markerPen, Pen? connectorPen,
        DrawingGroup? cumulative, long cumulativeBytes, Geometry? primaryClip, Geometry? secondaryClip) : ChartRetainedSeriesDrawing
    {
        internal override long EstimatedBytes => 1536L + marks.LongLength * 256 + connectors.LongLength * 32 + cumulativeBytes;
        public override void Draw(DrawingContext drawing)
        {
            using (IDisposable? clip = primaryClip is null ? null : drawing.PushGeometryClip(primaryClip))
            {
                foreach (var mark in marks)
                {
                    if (rectangles && mark.Shape.HasBody) drawing.DrawRectangle(mark.Fill, null, mark.Shape.Bounds);
                    if (mark.Stem is { } stem) drawing.DrawLine(stem, mark.Shape.Start, mark.Shape.End);
                    if (mark.EndMarker) ChartMarkerGeometry.Draw(drawing, markerShape, radius, mark.MarkerFill, markerPen, mark.Shape.End, markerGeometry);
                    if (mark.StartMarker) ChartMarkerGeometry.Draw(drawing, markerShape, radius, mark.MarkerFill, markerPen, mark.Shape.Start, markerGeometry);
                }
                if (connectorPen is not null)
                    foreach (var connector in connectors) drawing.DrawLine(connectorPen, connector.First, connector.Last);
            }
            if (cumulative is null) return;
            using IDisposable? secondary = secondaryClip is null ? null : drawing.PushGeometryClip(secondaryClip);
            cumulative.Draw(drawing);
        }
    }
}

internal sealed partial class PictorialBarRenderer
{
    private readonly record struct RetainedSymbol(Geometry Geometry, Rect Clip);

    internal ChartRetainedSeriesDrawing CreateRetainedDrawing(ChartSeriesRenderContext context)
    {
        var symbols = new List<RetainedSymbol>();
        foreach (var ordinal in ChartComparisonGeometry.VisibleCandidates(context))
            foreach (var symbol in Symbols(context, ordinal)) symbols.Add(new(symbol.Geometry, symbol.Clip));
        return new RetainedPictorial(symbols.ToArray(), context.Series.Fill ?? context.Brush);
    }

    private sealed class RetainedPictorial(RetainedSymbol[] symbols, IBrush fill) : ChartRetainedSeriesDrawing
    {
        internal override long EstimatedBytes => 512L + symbols.LongLength * 256;
        public override void Draw(DrawingContext drawing)
        {
            foreach (var symbol in symbols)
            {
                using var clip = drawing.PushClip(symbol.Clip);
                drawing.DrawGeometry(fill, null, symbol.Geometry);
            }
        }
    }
}
