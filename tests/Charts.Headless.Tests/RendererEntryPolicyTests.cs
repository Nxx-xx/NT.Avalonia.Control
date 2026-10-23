using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class RendererEntryPolicyTests
{
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Native_circle_record_keeps_original_ellipse_order_style_and_boundary_math(bool inverse, bool logarithmic)
    {
        var x = logarithmic ? (ChartAxis)new LogarithmicAxis() : new NumericAxis();
        x.IsInversed = inverse;
        x.ScaleBreaks.Add(new ChartScaleBreak { Start = 12, End = 24 });
        ChartPoint[] points = [new(.8, 0), new(1, 1), new(8, -1), new(15, 0), new(24, 2),
            new(50, double.NaN), new(75, -2), new(100, 1), new(110, 0), new(8, -1)];
        var series = new ScatterSeries
        {
            ItemsSource = points, MarkerSize = 4, MarkerShape = ChartMarkerShape.Circle,
            MarkerFill = Brushes.Blue, MarkerStroke = Brushes.Green, MarkerStrokeThickness = 1.75,
        };
        var projection = new CartesianChart.CartesianProjection(new Rect(30.125, 40.375, 600.625, 240.875),
            ChartScale.CreateForView(x, 1, 100), ChartScale.CreateForView(new NumericAxis { IsInversed = inverse }, -2, 2));
        var standard = new ChartSeriesRenderContext(series, series.CreateSnapshot(), projection, Brushes.Red);
        var expected = Draw(standard with { Projection = new ForwardProjection(projection) });
        var actual = Draw(standard);
        Assert.NotEmpty(actual);
        Assert.Equal(expected.Length, actual.Length);
        foreach (var (original, native) in expected.Zip(actual))
        {
            Assert.Equal(Assert.IsAssignableFrom<Geometry>(original.Geometry).Bounds,
                Assert.IsAssignableFrom<Geometry>(native.Geometry).Bounds);
            Assert.Same(Brushes.Blue, native.Brush); Assert.Same(Brushes.Green, native.Pen!.Brush);
            Assert.Equal(original.Pen!.Thickness, native.Pen.Thickness); Assert.Equal(1.75, native.Pen.Thickness);
        }
    }

    [AvaloniaTheory]
    [InlineData(8_191)]
    [InlineData(8_192)]
    [InlineData(8_193)]
    public void Both_point_hit_window_sizes_keep_last_duplicate_identity(int count)
    {
        var series = new LineSeries { MarkerSize = 0, ItemsSource = Enumerable.Repeat(new ChartPoint(5, 1), count).ToArray() };
        var projection = new CartesianChart.CartesianProjection(new Rect(30, 40, 600, 240),
            ChartScale.CreateForView(new NumericAxis(), 0, 10), ChartScale.CreateForView(new NumericAxis(), -2, 2));
        var context = new ChartSeriesRenderContext(series, series.CreateSnapshot(), projection, Brushes.Red);
        var location = projection.Project(new(5, 1));
        var actual = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, location, 0));
        Assert.Equal(count - 1, actual.SourceIndex);
        Assert.Equal(0, actual.DistanceSquared);
        Assert.Equal(location, actual.Position);
        Assert.Equal(series.Renderer.HitTest(context with { Projection = new ForwardProjection(projection) }, location, 0), actual);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Native_point_hit_loop_matches_full_scan_for_nonmonotonic_ties_boundaries_and_metadata(bool scatter, bool inverse)
    {
        ChartSeries series = scatter ? new ScatterSeries() : new LineSeries();
        ChartPoint[] points = [new(12, 1), new(-1, 0), new(5, 1), new(3, double.NaN),
            new(0, -2), new(5, 1), new(10, 2), new(7, -1)];
        var snapshot = new ChartDataSnapshot(1, points.Select((point, ordinal) =>
            new ChartDataPoint(100 - ordinal * 7, $"item-{ordinal}", null, null, point, false)).ToArray());
        var projection = new CartesianChart.CartesianProjection(new Rect(30.125, 40.375, 600.625, 240.875),
            ChartScale.CreateForView(new NumericAxis { IsInversed = inverse }, 0, 10),
            ChartScale.CreateForView(new NumericAxis { IsInversed = inverse }, -2, 2));
        var native = new ChartSeriesRenderContext(series, snapshot, projection, Brushes.Red);
        var original = native with { Projection = new ForwardProjection(projection) };
        var duplicate = projection.Project(new(5, 1));
        var tie = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(native, duplicate, 0));
        Assert.Equal(65, tie.SourceIndex); // Last physical duplicate, not the largest source identity.
        foreach (var location in new[] { duplicate, duplicate + new Vector(3.125, -2.375),
            projection.Project(new(0, -2)), projection.Project(new(10, 2)), projection.Project(new(-1, 0)),
            new Point(-1_000_001, 40), native.PlotArea.Center })
            foreach (var tolerance in new[] { 0d, 6d, 1_000_000d })
                Assert.Equal(series.Renderer.HitTest(original, location, tolerance),
                    series.Renderer.HitTest(native, location, tolerance));
    }

    [AvaloniaTheory]
    [InlineData(8_191)]
    [InlineData(8_192)]
    [InlineData(8_193)]
    public void Both_line_entry_sizes_keep_geometry_and_endpoint_hits_equal_to_custom_projection(int count)
    {
        var values = Enumerable.Range(0, count).Select(index => new ChartPoint(index, index % 11 - 5)).ToArray();
        values[count / 2] = new(count / 2, double.NaN);
        var series = new LineSeries { ItemsSource = values, MarkerSize = 0, Stroke = Brushes.Red };
        var data = series.CreateSnapshot();
        var plot = new Rect(30, 40, 600, 240);
        var projection = new CartesianChart.CartesianProjection(plot,
            ChartScale.CreateForView(new NumericAxis(), 0, count - 1),
            ChartScale.CreateForView(new NumericAxis { IsInversed = true }, -5, 5));
        var standard = new ChartSeriesRenderContext(series, data, projection, Brushes.Red);
        var custom = standard with { Projection = new ForwardProjection(projection) };

        var expected = Assert.Single(Draw(custom));
        var actual = Assert.Single(Draw(standard));

        Assert.Equal(Assert.IsAssignableFrom<Geometry>(expected.Geometry).Bounds,
            Assert.IsAssignableFrom<Geometry>(actual.Geometry).Bounds);
        Assert.Equal(expected.Pen!.Thickness, actual.Pen!.Thickness);
        var endpoint = projection.Project(values[^1]);
        var hit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(standard, endpoint, 0));
        Assert.Equal(count - 1, hit.SourceIndex);
        Assert.Equal(endpoint, hit.Position);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Markerless_standard_bars_match_original_custom_projection_rectangles_bit_for_bit(bool inverse)
    {
        ChartPoint[] points = [new(-.2, -2), new(0, 1), new(1, 3), new(2, -1), new(3, double.NaN), new(4, 4)];
        var series = new BarSeries { ItemsSource = points, MarkerSize = 0, Fill = Brushes.Blue };
        var projection = new CartesianChart.CartesianProjection(new Rect(20, 30, 500, 250),
            ChartScale.CreateForView(new NumericAxis { IsInversed = inverse }, 0, 4),
            ChartScale.CreateForView(new NumericAxis { IsInversed = inverse }, -2, 4));
        var standard = new ChartSeriesRenderContext(series, series.CreateSnapshot(), projection, Brushes.Red, 2, 3, 71.3);
        var custom = standard with { Projection = new ForwardProjection(projection) };

        var expected = Draw(custom);
        var actual = Draw(standard);

        Assert.Equal(5, actual.Length);
        Assert.Equal(expected.Select(item => Assert.IsAssignableFrom<Geometry>(item.Geometry).Bounds),
            actual.Select(item => Assert.IsAssignableFrom<Geometry>(item.Geometry).Bounds));
        Assert.All(actual, item => Assert.Same(Brushes.Blue, item.Brush));
    }

    private static GeometryDrawing[] Draw(ChartSeriesRenderContext context)
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open()) context.Series.Renderer.Render(drawing, context);
        return group.Children.OfType<GeometryDrawing>().ToArray();
    }

    private sealed class ForwardProjection(IChartProjection original) : IChartProjection
    {
        public Rect PlotArea => original.PlotArea;
        public double YOrigin => original.YOrigin;
        public Point Project(ChartPoint point) => original.Project(point);
        public ChartPoint Unproject(Point position) => original.Unproject(position);
        public double MapX(double value) => original.MapX(value);
        public double MapY(double value) => original.MapY(value);
    }
}
