using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal sealed partial class CartesianSeriesRenderer
{
    internal ChartRetainedSeriesDrawing CreateRetainedDrawing(ChartSeriesRenderContext context)
    {
        var radius = ChartMarkerGeometry.Radius(context.Series);
        var coordinates = context.Data.Coordinates.Span;
        DrawingGroup? line = null;
        var lineBytes = 0L;
        if (connect)
        {
            line = new DrawingGroup();
            using var recording = line.Open();
            RenderLine(recording, context, coordinates);
            lineBytes = ChartRetainedSeriesDrawing.EstimateLinearGeometry(context);
        }
        if (bars && radius <= 0) return CreateRetainedBars(context, coordinates);
        if (bars) return CreateRetainedBarsWithMarkers(context, coordinates, radius);
        if (radius <= 0) return new RetainedPoints(line, [], context, radius, lineBytes);

        // One value-type center per visible point. Recording a DrawingGroup here
        // would retain a GeometryDrawing and EllipseGeometry for every circle.
        var visible = ChartVisibilityQuery.Points(context, inflation: MarkerExtent(context.Series, radius) + 1);
        var centers = new Point[visible.Count];
        var count = 0;
        var plot = context.PlotArea.Inflate(context.Series.MarkerShape == ChartMarkerShape.Circle
            ? radius : ChartMarkerGeometry.HitRadius(context.Series));
        var linear = context.Series.MarkerShape == ChartMarkerShape.Circle && context.Projection is CartesianChart.CartesianProjection projection &&
            projection.TryGetLinearProjection(context.Data, out var mapping) ? mapping : (InRangeLinearProjection?)null;
        long projected = 0;
        foreach (var ordinal in visible)
        {
            var point = coordinates.IsEmpty ? context.Data.GetValue(ordinal).Point : coordinates[ordinal];
            if (!Valid(point)) continue;
            var position = linear is { } fast ? fast.Project(point) : context.Projection.Project(point);
            projected++;
            if (Valid(position) && plot.Contains(position)) centers[count++] = position;
        }
        ChartVisibilityQuery.RecordProjected(projected);
        if (centers.Length != count) Array.Resize(ref centers, count);
        return new RetainedPoints(line, centers, context, radius, lineBytes);
    }

    private static ChartRetainedSeriesDrawing CreateRetainedBars(ChartSeriesRenderContext context, ReadOnlySpan<ChartPoint> coordinates)
        => new RetainedBars(ChartComparisonLayout.For(context, null), context.Series.Fill ?? context.Brush);

    private static ChartRetainedSeriesDrawing CreateRetainedBars(ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, ChartVisibleOrdinals visible)
        => new RetainedBars(ChartComparisonLayout.For(context, null), context.Series.Fill ?? context.Brush);

    private static ChartRetainedSeriesDrawing CreateRetainedBarsWithMarkers(ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, double radius)
        => new RetainedBarMarkers(ChartComparisonLayout.For(context, null), context, radius);

    private static ChartRetainedSeriesDrawing CreateRetainedBarsWithMarkers(ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, double radius, ChartVisibleOrdinals visible)
        => new RetainedBarMarkers(ChartComparisonLayout.For(context, null), context, radius);

    private static bool ValidRectangle(Rect rectangle) => double.IsFinite(rectangle.X) && double.IsFinite(rectangle.Y) &&
        double.IsFinite(rectangle.Width) && double.IsFinite(rectangle.Height) && rectangle.Height > 0;

    private sealed class RetainedBars(ChartComparisonLayout layout, IBrush fill) : ChartRetainedSeriesDrawing
    {
        internal override long EstimatedBytes => 512L + layout.Entries.Length * 128L;
        public override void Draw(DrawingContext drawing)
        {
            foreach (var entry in layout.Entries)
                if (ValidRectangle(entry.Shape.Bounds)) drawing.DrawRectangle(fill, null, entry.Shape.Bounds);
        }
    }

    private sealed class RetainedPoints : ChartRetainedSeriesDrawing
    {
        private readonly DrawingGroup? _line;
        private readonly Point[] _centers;
        private readonly double _radius;
        private readonly ChartMarkerShape _shape;
        private readonly IBrush _fill;
        private readonly Pen _pen;
        private readonly Geometry? _geometry;
        private readonly long _lineBytes;

        public RetainedPoints(DrawingGroup? line, Point[] centers, ChartSeriesRenderContext context, double radius, long lineBytes)
        {
            _line = line; _centers = centers; _radius = radius; _lineBytes = lineBytes;
            _shape = context.Series.MarkerShape;
            _fill = context.Series.MarkerFill ?? context.Series.Fill ?? context.Brush;
            _pen = _shape == ChartMarkerShape.Circle
                ? new Pen(context.Series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(context.Series))
                : new Pen(context.Series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(context.Series),
                    lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _geometry = radius > 0 ? ChartMarkerGeometry.Create(_shape, radius) : null;
        }

        internal override long EstimatedBytes => 1024L + _centers.LongLength * 16 + _lineBytes;

        public override void Draw(DrawingContext drawing)
        {
            _line?.Draw(drawing);
            foreach (var center in _centers)
                ChartMarkerGeometry.Draw(drawing, _shape, _radius, _fill, _pen, center, _geometry);
        }
    }

    private sealed class RetainedBarMarkers : ChartRetainedSeriesDrawing
    {
        private readonly ChartComparisonLayout _layout;
        private readonly Rect _markerPlot;
        private readonly IBrush _bodyFill;
        private readonly double _radius;
        private readonly ChartMarkerShape _shape;
        private readonly IBrush _markerFill;
        private readonly Pen _markerPen;
        private readonly Geometry? _markerGeometry;
        internal override long EstimatedBytes => 1536L + _layout.Entries.Length * 128L;

        public RetainedBarMarkers(ChartComparisonLayout layout, ChartSeriesRenderContext context, double radius)
        {
            _layout = layout; _radius = radius; _shape = context.Series.MarkerShape;
            _markerPlot = context.PlotArea.Inflate(_shape == ChartMarkerShape.Circle ? radius : ChartMarkerGeometry.HitRadius(context.Series));
            _bodyFill = context.Series.Fill ?? context.Brush;
            _markerFill = context.Series.MarkerFill ?? _bodyFill;
            _markerPen = _shape == ChartMarkerShape.Circle
                ? new Pen(context.Series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(context.Series))
                : new Pen(context.Series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(context.Series),
                    lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _markerGeometry = ChartMarkerGeometry.Create(_shape, radius);
        }

        public override void Draw(DrawingContext drawing)
        {
            foreach (var entry in _layout.Entries)
            {
                var shape = entry.Shape;
                if (ValidRectangle(shape.Bounds)) drawing.DrawRectangle(_bodyFill, null, shape.Bounds);
                if (_markerPlot.Contains(shape.End)) ChartMarkerGeometry.Draw(drawing, _shape, _radius, _markerFill, _markerPen, shape.End, _markerGeometry);
            }
        }
    }
}
