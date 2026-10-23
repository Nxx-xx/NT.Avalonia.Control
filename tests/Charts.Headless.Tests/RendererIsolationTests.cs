using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class RendererIsolationTests
{
    [AvaloniaTheory]
    [InlineData(ChartMarkerShape.Circle)]
    [InlineData(ChartMarkerShape.Diamond)]
    public void Overlay_line_keeps_original_order_and_independent_marker_styles(ChartMarkerShape shape)
    {
        var series = new LineSeries
        {
            Stroke = Brushes.Red, StrokeThickness = 5, MarkerShape = shape, MarkerSize = 10,
            MarkerFill = Brushes.Blue, MarkerStroke = Brushes.Green, MarkerStrokeThickness = 2,
            ItemsSource = new ChartPoint[] { new(91, 91), new(92, 92), new(93, 93), new(94, 94), new(95, 95) },
        };
        ChartPoint[] values = [new(10, 10), new(20, 20), new(30, double.NaN), new(40, 40), new(50, 50)];
        var snapshot = series.CreateSnapshot().WithValues(values.Select((point, ordinal) =>
            new ChartDataValue(ordinal, point, !double.IsFinite(point.Y), false)).ToArray());
        Assert.True(snapshot.Coordinates.IsEmpty);
        var projection = new RecordingProjection();
        var context = new ChartSeriesRenderContext(series, snapshot, projection, Brushes.Red);

        var drawings = Draw(context);

        var line = Assert.Single(drawings, drawing => drawing.Brush is null && IsColor(drawing.Pen?.Brush, Colors.Red));
        Assert.Equal(5, line.Pen!.Thickness);
        var markers = drawings.Where(drawing => IsColor(drawing.Brush, Colors.Blue)).ToArray();
        Assert.Equal(4, markers.Length);
        Assert.All(markers, marker => { Assert.True(IsColor(marker.Pen?.Brush, Colors.Green)); Assert.Equal(2, marker.Pen!.Thickness); });
        var finite = values.Where(point => double.IsFinite(point.Y)).ToArray();
        Assert.Equal(finite.Concat(finite), projection.Projected);

        foreach (var zeroSize in new[] { false, true })
        {
            series.ShowMarkers = zeroSize;
            if (zeroSize) series.MarkerSize = 0;
            projection.Projected.Clear();
            Assert.Single(Draw(context));
            Assert.Equal(finite, projection.Projected);
        }
    }

    [AvaloniaFact]
    public void Clipped_grouped_bar_retains_body_marker_ties_and_last_physical_identity()
    {
        var series = new BarSeries { MarkerSize = 20, MarkerStrokeThickness = 0 };
        var point = new ChartPoint(-6, 40);
        var snapshot = new ChartDataSnapshot(1, new ChartDataPoint[]
        {
            new(41, "first", null, null, point, false), new(7, "last", null, null, point, false),
        });
        var context = new ChartSeriesRenderContext(series, snapshot, new RecordingProjection(), Brushes.Blue, 1, 2, 40);
        var rectangle = CartesianSeriesRenderer.BarRectangle(context, point);
        Assert.True(rectangle.Left < 0 && rectangle.Right > 0);
        var anchor = new Point(rectangle.Center.X, point.Y);
        Assert.Null(series.Renderer.HitTest(context, anchor - new Vector(0, 5), 0));

        series.ShowMarkers = true;
        var markerHit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, anchor - new Vector(0, 5), 0));
        Assert.Equal(7, markerHit.SourceIndex);
        Assert.Equal(anchor, markerHit.Position);
        Assert.Equal(25, markerHit.DistanceSquared);

        // Six pixels above the endpoint is equally distant from the body and marker.
        // Body wins equality, retaining the center of the clipped grouped rectangle.
        var tie = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, anchor - new Vector(0, 6), 6));
        Assert.Equal(7, tie.SourceIndex);
        Assert.Equal(36, tie.DistanceSquared);
        Assert.Equal(new Point(rectangle.Intersect(context.PlotArea).Center.X, point.Y), tie.Position);
    }

    private static bool IsColor(IBrush? brush, Color color) => brush is ISolidColorBrush solid && solid.Color == color;
    private static GeometryDrawing[] Draw(ChartSeriesRenderContext context)
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open()) context.Series.Renderer.Render(drawing, context);
        return Flatten(group).OfType<GeometryDrawing>().ToArray();
    }
    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var leaf in Flatten(child)) yield return leaf;
        else yield return drawing;
    }
    private sealed class RecordingProjection : IChartProjection
    {
        public List<ChartPoint> Projected { get; } = [];
        public Rect PlotArea => new(0, 0, 100, 100);
        public double YOrigin => 100;
        public Point Project(ChartPoint point) { Projected.Add(point); return new(point.X, point.Y); }
        public ChartPoint Unproject(Point position) => new(position.X, position.Y);
        public double MapX(double value) => value;
        public double MapY(double value) => value;
    }
}
