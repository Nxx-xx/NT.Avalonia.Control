using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace NT.AvaloniaControl.Charts;

internal sealed partial class CartesianSeriesRenderer
{
    private const int MaximumCachedCircles = 16_384;

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
        if (bars && radius <= 0) return CreateRetainedBars(context, coordinates, allowSharedHistory: true);
        if (bars) return CreateRetainedBarsWithMarkers(context, coordinates, radius, allowSharedHistory: true);
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

    private static ChartRetainedSeriesDrawing CreateRetainedBars(ChartSeriesRenderContext context, ReadOnlySpan<ChartPoint> coordinates,
        bool allowSharedHistory = false)
        => new RetainedBars(ChartComparisonLayout.For(context, null), context.Series.Fill ?? context.Brush, context.Series, allowSharedHistory);

    private static ChartRetainedSeriesDrawing CreateRetainedBars(ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, ChartVisibleOrdinals visible, bool allowSharedHistory = false)
        => new RetainedBars(ChartComparisonLayout.For(context, null), context.Series.Fill ?? context.Brush, context.Series, allowSharedHistory);

    private static ChartRetainedSeriesDrawing CreateRetainedBarsWithMarkers(ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, double radius, bool allowSharedHistory = false)
        => new RetainedBarMarkers(ChartComparisonLayout.For(context, null), context, radius, allowSharedHistory);

    private static ChartRetainedSeriesDrawing CreateRetainedBarsWithMarkers(ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, double radius, ChartVisibleOrdinals visible, bool allowSharedHistory = false)
        => new RetainedBarMarkers(ChartComparisonLayout.For(context, null), context, radius, allowSharedHistory);

    private static bool ValidRectangle(Rect rectangle) => double.IsFinite(rectangle.X) && double.IsFinite(rectangle.Y) &&
        double.IsFinite(rectangle.Width) && double.IsFinite(rectangle.Height) && rectangle.Height > 0;

    private sealed class RetainedBars : ChartRetainedSeriesDrawing
    {
        private readonly ChartComparisonLayout _layout;
        private readonly IBrush _fill;
        private readonly RectangleGeometry?[]? _recordingRectangles;
        private readonly ChartSeries _recordingOwner;
        private readonly BarGeometryHistory? _recordingHistory;
        private readonly long _recordingRectangleBytes, _sharedRecordingHistoryBytes;

        public RetainedBars(ChartComparisonLayout layout, IBrush fill, ChartSeries owner, bool allowSharedHistory)
        {
            _layout = layout; _fill = fill; _recordingOwner = owner;
            _recordingRectangles = CreateRetainedBarRectangles(layout, fill);
            _recordingHistory = allowSharedHistory ? RetainBarGeometryHistory(owner, _recordingRectangles) : null;
            _sharedRecordingHistoryBytes = RetainedBarHistoryBytes(_recordingHistory);
            _recordingRectangleBytes = RetainedBarRectangleBytes(_recordingRectangles, _recordingHistory);
        }

        internal override long EstimatedBytes => 512L + _layout.Entries.Length * 128L + _recordingRectangleBytes + _sharedRecordingHistoryBytes;
        internal override long SharedCircleHistoryBytes => _sharedRecordingHistoryBytes;
        internal override bool SharedCircleHistoryActive => _recordingHistory?.Active ?? true;
        public override void Draw(DrawingContext drawing)
        {
            var rectangles = drawing.GetType() == DrawingGroupRecording.ContextType ? _recordingRectangles : null;
            var history = RecordingBarGeometryHistory(_recordingOwner, _recordingHistory, _layout.Entries.Length);
            var ordinal = 0;
            foreach (var entry in _layout.Entries)
            {
                var bounds = entry.Shape.Bounds;
                if (ValidRectangle(bounds)) DrawRetainedBar(drawing, _fill, bounds, rectangles, ordinal, history);
                ordinal++;
            }
        }
    }

    private static RectangleGeometry?[]? CreateRetainedBarRectangles(ChartComparisonLayout layout, IBrush fill) =>
        layout.Entries.Length <= MaximumCachedBars &&
        (fill.GetType() == typeof(SolidColorBrush) || fill.GetType() == typeof(Avalonia.Media.Immutable.ImmutableSolidColorBrush))
            ? new RectangleGeometry?[layout.Entries.Length] : null;

    // Reserve the full logical managed weight at cache admission, before lazily
    // constructing any geometry. Native paths remain outside accounting model 1.
    private static long RetainedBarRectangleBytes(RectangleGeometry?[]? rectangles, BarGeometryHistory? history) =>
        rectangles is null ? 0 : history is null ? 256L + rectangles.LongLength * 256L :
            256L + rectangles.LongLength * IntPtr.Size +
            (rectangles.Length < MinimumSharedBarHistoryEntries ? rectangles.LongLength * 256L : 0);

    private static long RetainedBarHistoryBytes(BarGeometryHistory? history) =>
        history is null ? 0 : 512L + history.Capacity * 256L;

    private static void DrawRetainedBar(DrawingContext drawing, IBrush fill, Rect bounds,
        RectangleGeometry?[]? rectangles, int ordinal, BarGeometryHistory? history)
    {
        if (rectangles is null) { drawing.DrawRectangle(fill, null, bounds); return; }
        // DrawingGroup.DrawRectangle constructs a new native rectangle on every
        // recording. The retained layout is immutable, so reuse that rectangle
        // while leaving the brush live. Each recording still owns its public
        // geometry wrapper. Native DrawRect and spatial brushes keep their
        // original primitive because DrawPath can change edge pixels or bounds.
        var geometry = rectangles[ordinal] ??= history is null ? new RectangleGeometry(bounds) : history.GetOrCreate(bounds);
        drawing.DrawGeometry(fill, null, geometry);
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
        private readonly EllipseGeometry?[]? _recordingCircles;
        private readonly ChartSeries _recordingOwner;
        private readonly CircleGeometryHistory? _recordingHistory;
        private readonly long _recordingCircleBytes;
        private readonly long _sharedRecordingHistoryBytes;
        private readonly IChartNativeCirclePlan? _nativeCircles;
        private readonly long _lineBytes;

        public RetainedPoints(DrawingGroup? line, Point[] centers, ChartSeriesRenderContext context, double radius, long lineBytes)
        {
            ReleaseBarGeometryHistory(context.Series);
            _line = line; _centers = centers; _radius = radius; _lineBytes = lineBytes;
            _recordingOwner = context.Series;
            _shape = context.Series.MarkerShape;
            _fill = context.Series.MarkerFill ?? context.Series.Fill ?? context.Brush;
            _pen = _shape == ChartMarkerShape.Circle
                ? new Pen(context.Series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(context.Series))
                : new Pen(context.Series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(context.Series),
                    lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _geometry = radius > 0 ? ChartMarkerGeometry.Create(_shape, radius) : null;
            // DrawingGroup's ellipse command creates exactly this platform
            // ellipse before wrapping it in a GeometryDrawing. Reuse that
            // immutable geometry only for its sealed recorder; native contexts
            // must keep DrawEllipse, whose edge pixels can differ from DrawPath.
            // Reserve the complete bounded payload before cache admission.
            if (_shape == ChartMarkerShape.Circle && radius > 0 && centers.Length <= MaximumCachedCircles &&
                IsSimpleCirclePaint(_fill) && IsSimpleCirclePaint(_pen.Brush))
            {
                _recordingCircles = new EllipseGeometry?[centers.Length];
                if (centers.Length > 0 && context.Series.CanShareRenderingGeometryHistory)
                {
                    _recordingHistory = CircleHistories.GetValue(context.Series, static _ => new CircleGeometryHistory());
                    _recordingHistory.Reserve(centers.Length);
                    _sharedRecordingHistoryBytes = 512L + _recordingHistory.Capacity * 256L;
                    // Charge the complete historical capacity, including after
                    // zooming down to a smaller current point array. Capture
                    // this weight now; replay never grows admission accounting.
                    _recordingCircleBytes = 256L + centers.LongLength * IntPtr.Size + _sharedRecordingHistoryBytes +
                        (centers.Length < 2048 ? centers.LongLength * 256L : 0);
                }
                else
                {
                    ReleaseCircleGeometryHistory(context.Series);
                    _recordingCircleBytes = 256L + centers.LongLength * 256L;
                }
            }
            else ReleaseCircleGeometryHistory(context.Series);
            if (_shape == ChartMarkerShape.Circle && radius > 0 && centers.Length is > 0 and <= 16384 &&
                IsSimpleCirclePaint(_fill) && _fill.Transform is null &&
                IsSimpleCirclePaint(_pen.Brush) && _pen.Brush?.Transform is null)
                _nativeCircles = ChartNativeCircleBackend.TryCreate(context.Series, centers, radius, _fill, _pen);
            else ChartNativeCircleBackend.Clear(context.Series);
        }

        internal override long EstimatedBytes => 1024L + _centers.LongLength * 16 + _lineBytes +
            _recordingCircleBytes +
            (_nativeCircles?.EstimatedBytes ?? 0);

        internal override long SharedCircleHistoryBytes => _sharedRecordingHistoryBytes +
            (_nativeCircles?.SharedHistoryEstimatedBytes ?? 0);
        internal override bool SharedCircleHistoryActive => (_recordingHistory?.Active ?? true) &&
            (_nativeCircles?.SharedHistoryActive ?? true);

        public override void Draw(DrawingContext drawing)
        {
            _line?.Draw(drawing);
            if (_recordingCircles is { } circles && drawing.GetType() == DrawingGroupRecording.ContextType)
            {
                var history = _recordingHistory;
                if (history is not null && !_recordingOwner.CanShareRenderingGeometryHistory)
                {
                    ReleaseCircleGeometryHistory(_recordingOwner);
                    history = null;
                }
                // Small moving viewports would otherwise churn through the
                // dense view's two historical states. Keep their local array
                // and full reserved history weight without polluting its LRU.
                if (_centers.Length < 2048) history = null;
                for (var index = 0; index < _centers.Length; index++)
                {
                    var center = _centers[index];
                    // Keep DrawEllipse's subtraction/multiplication ordering.
                    // Never mutate a geometry referenced by an older frame.
                    var geometry = circles[index] ??= history is null
                        ? CreateRecordingCircle(center, _radius)
                        : history.GetOrCreate(center, _radius);
                    drawing.DrawGeometry(_fill, _pen, geometry);
                }
                return;
            }
            if (_centers.Length >= 2048 && _nativeCircles is { } native && IsSimpleCirclePaint(_fill) && _fill.Transform is null &&
                IsSimpleCirclePaint(_pen.Brush) && _pen.Brush?.Transform is null)
            {
                var type = drawing.GetType();
                if (type.Assembly == typeof(DrawingContext).Assembly && type.FullName is
                    "Avalonia.Media.PlatformDrawingContext" or "Avalonia.Rendering.Composition.Drawing.RenderDataDrawingContext")
                {
                    var frame = native.CreateFrame(_fill, _pen,
                        snapshotPaint: type.FullName != "Avalonia.Media.PlatformDrawingContext");
                    drawing.Custom(frame);
                    return;
                }
            }
            foreach (var center in _centers)
                ChartMarkerGeometry.Draw(drawing, _shape, _radius, _fill, _pen, center, _geometry);
        }

        private static bool IsSimpleCirclePaint(IBrush? brush) =>
            brush?.GetType() == typeof(SolidColorBrush) || brush?.GetType() == typeof(ImmutableSolidColorBrush);

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
        private readonly RectangleGeometry?[]? _recordingRectangles;
        private readonly ChartSeries _recordingOwner;
        private readonly BarGeometryHistory? _recordingHistory;
        private readonly long _recordingRectangleBytes, _sharedRecordingHistoryBytes;
        internal override long EstimatedBytes => 1536L + _layout.Entries.Length * 128L + _recordingRectangleBytes + _sharedRecordingHistoryBytes;
        internal override long SharedCircleHistoryBytes => _sharedRecordingHistoryBytes;
        internal override bool SharedCircleHistoryActive => _recordingHistory?.Active ?? true;

        public RetainedBarMarkers(ChartComparisonLayout layout, ChartSeriesRenderContext context, double radius, bool allowSharedHistory)
        {
            _layout = layout; _radius = radius; _shape = context.Series.MarkerShape; _recordingOwner = context.Series;
            _markerPlot = context.PlotArea.Inflate(_shape == ChartMarkerShape.Circle ? radius : ChartMarkerGeometry.HitRadius(context.Series));
            _bodyFill = context.Series.Fill ?? context.Brush;
            _markerFill = context.Series.MarkerFill ?? _bodyFill;
            _markerPen = _shape == ChartMarkerShape.Circle
                ? new Pen(context.Series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(context.Series))
                : new Pen(context.Series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(context.Series),
                    lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _markerGeometry = ChartMarkerGeometry.Create(_shape, radius);
            _recordingRectangles = CreateRetainedBarRectangles(layout, _bodyFill);
            _recordingHistory = allowSharedHistory ? RetainBarGeometryHistory(context.Series, _recordingRectangles) : null;
            _sharedRecordingHistoryBytes = RetainedBarHistoryBytes(_recordingHistory);
            _recordingRectangleBytes = RetainedBarRectangleBytes(_recordingRectangles, _recordingHistory);
        }

        public override void Draw(DrawingContext drawing)
        {
            var rectangles = drawing.GetType() == DrawingGroupRecording.ContextType ? _recordingRectangles : null;
            var history = RecordingBarGeometryHistory(_recordingOwner, _recordingHistory, _layout.Entries.Length);
            var ordinal = 0;
            foreach (var entry in _layout.Entries)
            {
                var shape = entry.Shape;
                if (ValidRectangle(shape.Bounds)) DrawRetainedBar(drawing, _bodyFill, shape.Bounds, rectangles, ordinal, history);
                if (_markerPlot.Contains(shape.End)) ChartMarkerGeometry.Draw(drawing, _shape, _radius, _markerFill, _markerPen, shape.End, _markerGeometry);
                ordinal++;
            }
        }
    }
}
