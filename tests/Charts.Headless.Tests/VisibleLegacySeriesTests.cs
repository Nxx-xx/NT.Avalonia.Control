using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

[Collection("Visibility index diagnostics")]
public sealed class VisibleLegacySeriesTests
{
    [AvaloniaTheory]
    [InlineData(false, ChartMarkerShape.Circle)]
    [InlineData(true, ChartMarkerShape.Circle)]
    [InlineData(false, ChartMarkerShape.Rectangle)]
    [InlineData(true, ChartMarkerShape.Diamond)]
    public void Legacy_marker_candidates_match_exhaustive_native_operations_and_original_hits(bool unordered, ChartMarkerShape shape)
    {
        var points = Enumerable.Range(0, 20_000).Select(i => new ChartPoint(i, i % 127)).ToArray();
        if (unordered) new Random(971).Shuffle(points);
        var series = new ScatterSeries { MarkerSize = 8, MarkerShape = shape };
        var data = Data(points);
        var context = Context(series, data, 9_990, 10_010, 80, 110);
        using var exhaustive = new SeriesDrawingLayerCacheTests.NativeBoundary();
        series.Renderer.Render(exhaustive.Drawing, context with { Projection = new ForwardProjection(context.Projection) });
        var before = ChartVisibilityQuery.ProjectedCount;
        using var clipped = new SeriesDrawingLayerCacheTests.NativeBoundary();
        series.Renderer.Render(clipped.Drawing, context);
        Assert.InRange(ChartVisibilityQuery.ProjectedCount - before, 1, 200);
        Assert.Equal(exhaustive.Recorder.Operations, clipped.Recorder.Operations);
        Assert.Equal(points.Length, data.Points.Count);
        var ordinal = Array.FindIndex(points, point => point.X >= 9_990 && point.X <= 10_010 && point.Y >= 80 && point.Y <= 110);
        Assert.True(ordinal >= 0);
        var original = data.Points[ordinal];
        var hit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, context.Projection.Project(original.Point), 0));
        Assert.Equal(original.SourceIndex, hit.SourceIndex);
        Assert.True(data.TryGetPoint(hit.SourceIndex, out var returned));
        Assert.Same(original.Item, returned.Item); Assert.Equal(original.StableId, returned.StableId); Assert.Equal(original.Point, returned.Point);
        var retained = ((CartesianSeriesRenderer)series.Renderer).CreateRetainedDrawing(context);
        using var replay = new SeriesDrawingLayerCacheTests.NativeBoundary();
        retained.Draw(replay.Drawing);
        Assert.Equal(clipped.Recorder.Operations, replay.Recorder.Operations);
        var centers = retained.GetType().GetField("_centers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(retained) as Point[];
        Assert.InRange(centers!.Length, 1, 100);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Legacy_bar_category_query_keeps_spanning_bodies_and_exact_visible_rectangle_arithmetic(bool markers)
    {
        var series = new BarSeries { ShowMarkers = markers, MarkerSize = 6 };
        var context = Context(series, Data(Enumerable.Range(0, 20_000).Select(i => new ChartPoint(i, 1000)).ToArray()),
            9_990, 10_010, 10, 20) with { GroupIndex = 1, GroupCount = 2, GroupWidth = 8.375 };
        using var exhaustive = new SeriesDrawingLayerCacheTests.NativeBoundary();
        series.Renderer.Render(exhaustive.Drawing, context with { Projection = new ForwardProjection(context.Projection) });
        var before = ChartVisibilityQuery.ProjectedCount;
        using var clipped = new SeriesDrawingLayerCacheTests.NativeBoundary();
        series.Renderer.Render(clipped.Drawing, context);
        Assert.InRange(ChartVisibilityQuery.ProjectedCount - before, 1, 200);
        var expected = exhaustive.Recorder.Rectangles.Where(rectangle => rectangle.Intersects(context.PlotArea)).ToArray();
        Assert.NotEmpty(expected);
        Assert.Equal(expected, clipped.Recorder.Rectangles.Where(rectangle => rectangle.Intersects(context.PlotArea)));
        using var replay = new SeriesDrawingLayerCacheTests.NativeBoundary();
        ((CartesianSeriesRenderer)series.Renderer).CreateRetainedDrawing(context).Draw(replay.Drawing);
        Assert.Equal(clipped.Recorder.Operations, replay.Recorder.Operations);
    }

    [AvaloniaFact]
    public void Crossing_line_keeps_both_original_endpoints_even_with_no_visible_points()
    {
        var series = new LineSeries { MarkerSize = 0, StrokeThickness = 2 };
        var data = Data(new ChartPoint[] { new(-100, 5), new(100, 5), new(200, 50), new(double.NaN, double.NaN) }
            .Concat(Enumerable.Range(0, 10_000).Select(i => new ChartPoint(1000 + i, 1000))).ToArray());
        var context = Context(series, data, -1, 1, 0, 10);
        var before = ChartVisibilityQuery.ProjectedCount;
        var group = new DrawingGroup();
        using (var drawing = group.Open()) series.Renderer.Render(drawing, context);
        Assert.InRange(ChartVisibilityQuery.ProjectedCount - before, 2, 3);
        var geometry = Assert.IsType<GeometryDrawing>(Assert.Single(group.Children));
        Assert.True(geometry.Geometry!.Bounds.Left < context.PlotArea.Left);
        Assert.True(geometry.Geometry.Bounds.Right > context.PlotArea.Right);
        Assert.DoesNotContain(data.Points, point => context.PlotArea.Contains(context.Projection.Project(point.Point)));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Query_bounds_never_exclude_forward_visible_logarithmic_points_at_extreme_domains(bool reversed)
    {
        var data = Data(Enumerable.Range(0, 4001).Select(i => new ChartPoint(Math.Pow(10, -250 + i * .125), i % 7 + 1)).ToArray());
        var projection = new CartesianChart.CartesianProjection(new(13.125, 17.875, 617.375, 311.625),
            ChartScale.CreateForView(new LogarithmicAxis { IsInversed = reversed }, 1e-20, 1e20),
            ChartScale.CreateForView(new NumericAxis { IsInversed = reversed }, 2, 5));
        var context = new ChartSeriesRenderContext(new ScatterSeries(), data, projection, Brushes.Blue);
        var expected = data.Points.Select((point, ordinal) => (point, ordinal))
            .Where(item => context.PlotArea.Contains(projection.Project(item.point.Point))).Select(item => item.ordinal).ToArray();
        var actual = ChartVisibilityQuery.Points(context).ToHashSet();
        Assert.NotEmpty(expected); Assert.All(expected, ordinal => Assert.Contains(ordinal, actual));
    }

    [AvaloniaFact]
    public void Unknown_projection_and_screen_saturation_keep_exhaustive_extension_semantics()
    {
        var data = Data([new(-10, -10), new(10, 10)]);
        var context = Context(new LineSeries(), data, -1, 1, -1, 1);
        Assert.True(ChartVisibilityQuery.Points(context with { Projection = new ForwardProjection(context.Projection) }).IsFull);
        Assert.True(ChartVisibilityQuery.Points(context, new Rect(-1_000_000, 0, 1, 1)).IsFull);
    }

    private static ChartDataSnapshot Data(ChartPoint[] points) => new(0, points.Select((point, i) =>
        new ChartDataPoint(1000 + i * 7, $"raw-{i}", new object(), null, point, !double.IsFinite(point.X) || !double.IsFinite(point.Y))).ToArray());
    private static ChartSeriesRenderContext Context(ChartSeries series, ChartDataSnapshot data, double x0, double x1, double y0, double y1) =>
        new(series, data, new CartesianChart.CartesianProjection(new(0, 0, 100, 100),
            ChartScale.CreateForView(new NumericAxis(), x0, x1), ChartScale.CreateForView(new NumericAxis(), y0, y1)), Brushes.Blue);
    private sealed class ForwardProjection(IChartProjection original) : IChartProjection
    {
        public Rect PlotArea => original.PlotArea;
        public double YOrigin => original.YOrigin;
        public Point Project(ChartPoint point) => original.Project(point);
        public ChartPoint Unproject(Point point) => original.Unproject(point);
        public double MapX(double value) => original.MapX(value);
        public double MapY(double value) => original.MapY(value);
    }
}
