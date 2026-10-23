using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class IndexedHitTests
{
    [AvaloniaTheory]
    [InlineData("line")]
    [InlineData("scatter")]
    [InlineData("bar")]
    public void Random_unsorted_repeated_and_missing_values_match_a_full_geometric_scan(string kind)
    {
        var random = new Random(20261015);
        var values = Enumerable.Range(0, 768).Select(_ => new ChartPoint(random.Next(101) / 10d, random.NextDouble() * 10)).ToArray();
        values[31] = new(double.NaN, 4); values[253] = new(4, double.NaN);
        values[412] = values[22]; values[604] = values[22];
        var series = new ProbeSeries(kind) { ItemsSource = values, MarkerSize = 11, StrokeThickness = 3 };
        using var fixture = new HitFixture(series);
        var context = fixture.Capture(series);
        foreach (var tolerance in new[] { 0d, 3d, 12d, 35d })
            for (var probe = 0; probe < 90; probe++)
            {
                var plot = context.PlotArea;
                var position = new Point(plot.Left + random.NextDouble() * plot.Width, plot.Top + random.NextDouble() * plot.Height);
                CompareRenderer(context, kind, position, tolerance);
                CompareChart(fixture, position, tolerance);
            }
    }

    [AvaloniaFact]
    public void Published_reverse_storage_keeps_source_indices_and_last_physical_ordinal_wins_exact_ties()
    {
        var series = new ProbeSeries("scatter") { ItemsSource = new ChartPoint[] { new(5, 5), new(5, 5), new(5, 5), new(8, 8) } };
        using var fixture = new HitFixture(series);
        var before = series.CreateSnapshot();
        var reversed = new ChartDataSnapshot(before.Version, before.Points.Reverse().ToArray());
        Assert.True(series.TryPublishSnapshot(reversed, before.Version));
        var context = fixture.Capture(series);
        Assert.Equal(new[] { 3, 2, 1, 0 }, context.Data.Points.Select(item => item.SourceIndex));
        var position = context.Projection.Project(new(5, 5));
        CompareRenderer(context, "scatter", position, 0);
        var hit = Assert.IsType<ChartPointHit>(fixture.Chart.HitTestPoint(position, 0));
        Assert.Equal(0, hit.Index);
        Assert.Equal(before.Points[0].StableId, hit.StableId);
    }

    [AvaloniaFact]
    public void Equal_hits_across_series_keep_the_later_series_and_its_original_identity()
    {
        var first = new ProbeSeries("scatter") { ItemsSource = new ChartPoint[] { new(5, 5), new(5, 5) } };
        var last = new ProbeSeries("line") { ItemsSource = new ChartPoint[] { new(5, 5), new(5, 5), new(9, 9) } };
        using var fixture = new HitFixture(first, last);
        var position = fixture.Capture(first).Projection.Project(new(5, 5));
        CompareChart(fixture, position, 0);
        var hit = Assert.IsType<ChartPointHit>(fixture.Chart.HitTestPoint(position, 0));
        Assert.Same(last, hit.Series); Assert.Equal(1, hit.Index);
        Assert.Equal(last.CreateSnapshot().Points[1].StableId, hit.StableId);
    }

    [AvaloniaFact]
    public void Equal_distance_at_distinct_x_values_uses_physical_order_instead_of_sorted_x_order()
    {
        foreach (var values in new[]
        {
            new ChartPoint[] { new(6, 5), new(2, 5) },
            new ChartPoint[] { new(2, 5), new(6, 5) },
        })
        {
            var series = new ProbeSeries("scatter") { ItemsSource = values };
            using var fixture = new HitFixture(new NumericAxis { Minimum = 0, Maximum = 8 }, new NumericAxis { Minimum = 0, Maximum = 10 }, series);
            var context = fixture.Capture(series);
            var first = context.Projection.Project(values[0]); var last = context.Projection.Project(values[1]);
            var position = new Point((first.X + last.X) / 2, first.Y);
            Assert.Equal(Squared(position - first), Squared(position - last));
            var tolerance = Math.Abs(first.X - last.X) / 2 + 1;
            CompareRenderer(context, "scatter", position, tolerance);
            Assert.Equal(1, Assert.IsType<ChartPointHit>(fixture.Chart.HitTestPoint(position, tolerance)).Index);
        }
    }

    [AvaloniaTheory]
    [InlineData("line")]
    [InlineData("scatter")]
    public void NaN_x_in_the_middle_and_missing_y_do_not_hide_valid_neighbors_or_create_a_hit(string kind)
    {
        var values = Enumerable.Range(0, 51).Select(index => new ChartPoint(index / 5d, 5)).ToArray();
        values[25] = new(double.NaN, 5); values[31] = new(6.2, double.NaN);
        var series = new ProbeSeries(kind) { ItemsSource = values, MarkerSize = 4, StrokeThickness = 1 };
        using var fixture = new HitFixture(series);
        var context = fixture.Capture(series);
        foreach (var index in new[] { 24, 26, 30, 32 })
        {
            var position = context.Projection.Project(values[index]);
            CompareRenderer(context, kind, position, 0);
            Assert.Equal(index, Assert.IsType<ChartPointHit>(fixture.Chart.HitTestPoint(position, 0)).Index);
        }
        foreach (var x in new[] { 5d, 6.2 })
        {
            var position = context.Projection.Project(new(x, 5));
            CompareRenderer(context, kind, position, 0);
            Assert.Null(fixture.Chart.HitTestPoint(position, 0));
        }
    }

    [AvaloniaTheory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Reversed_logarithmic_and_broken_axes_match_full_scan_in_screen_space(bool reversed, bool logarithmic, bool breaks)
    {
        var random = new Random(7331);
        var values = Enumerable.Range(0, 320).Select(_ => new ChartPoint(1 + random.NextDouble() * 99, 1 + random.NextDouble() * 99)).ToArray();
        values[8] = new(10, 10); values[199] = new(10, 10);
        var series = new ProbeSeries("scatter") { ItemsSource = values, MarkerSize = 13, StrokeThickness = 4 };
        ChartAxis x = logarithmic ? new LogarithmicAxis() : new NumericAxis();
        ChartAxis y = logarithmic ? new LogarithmicAxis() : new NumericAxis();
        foreach (var axis in new[] { x, y }) { axis.Minimum = 1; axis.Maximum = 100; axis.IsInversed = reversed; }
        if (breaks)
        {
            x.ScaleBreaks.Add(new ChartScaleBreak { Start = 20, End = 40, CompressionRatio = .15 });
            y.ScaleBreaks.Add(new ChartScaleBreak { Start = 50, End = 70, CompressionRatio = .2 });
        }
        using var fixture = new HitFixture(x, y, series);
        var context = fixture.Capture(series);
        for (var index = 0; index < 150; index++)
        {
            var position = new Point(context.PlotArea.Left + random.NextDouble() * context.PlotArea.Width, context.PlotArea.Top + random.NextDouble() * context.PlotArea.Height);
            CompareRenderer(context, "scatter", position, 12);
            CompareChart(fixture, position, 12);
        }
        var duplicate = context.Projection.Project(new(10, 10));
        CompareChart(fixture, duplicate, 0);
        Assert.Equal(199, Assert.IsType<ChartPointHit>(fixture.Chart.HitTestPoint(duplicate, 0)).Index);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Wide_logarithmic_axes_keep_the_exact_projected_tolerance_boundary(bool reversed)
    {
        var series = new ProbeSeries("scatter")
        {
            ItemsSource = new ChartPoint[] { new(1e-200, 5), new(1e100, 5), new(1e250, 5) }, MarkerSize = 0, StrokeThickness = 0,
        };
        using var fixture = new HitFixture(new LogarithmicAxis { Minimum = 1e-300, Maximum = 1e300, IsInversed = reversed },
            new NumericAxis { Minimum = 0, Maximum = 10 }, series);
        var context = fixture.Capture(series);
        var projected = context.Projection.Project(new(1e100, 5));
        var position = projected + new Vector(12, 0);
        var expected = Assert.IsType<OracleHit>(Scan(context, "scatter", position, 12));
        Assert.Equal(1, expected.Row.SourceIndex); Assert.Equal(144, expected.DistanceSquared);
        CompareRenderer(context, "scatter", position, 12); CompareChart(fixture, position, 12);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Huge_linear_ranges_keep_physical_ties_when_distinct_raw_values_project_to_one_pixel(bool reversed)
    {
        var series = new ProbeSeries("scatter")
        {
            ItemsSource = new ChartPoint[] { new(1e299, 4), new(0, 5), new(-1e-300, 5), new(-1e299, 6) }, MarkerSize = 0, StrokeThickness = 0,
        };
        using var fixture = new HitFixture(new NumericAxis { Minimum = -1e300, Maximum = 1e300, IsInversed = reversed },
            new NumericAxis { Minimum = 0, Maximum = 10 }, series);
        var context = fixture.Capture(series);
        var zero = context.Projection.Project(new(0, 5)); var tiny = context.Projection.Project(new(-1e-300, 5));
        Assert.Equal(zero, tiny);
        var position = zero + new Vector(12, 0);
        var expected = Assert.IsType<OracleHit>(Scan(context, "scatter", position, 12));
        Assert.Equal(2, expected.Row.SourceIndex); Assert.Equal(144, expected.DistanceSquared);
        CompareRenderer(context, "scatter", position, 12); CompareChart(fixture, position, 12);
        Assert.Equal(2, Assert.IsType<ChartPointHit>(fixture.Chart.HitTestPoint(position, 12)).Index);
    }

    [AvaloniaTheory]
    [InlineData(-12d, 12d)]
    [InlineData(12d, 12d)]
    [InlineData(-500_000d, 500_000d)]
    public void Large_chart_queries_touching_the_saturated_projection_plateau_match_full_scan(double offset, double tolerance)
    {
        var series = new ProbeSeries("scatter") { ItemsSource = new ChartPoint[] { new(9, 5), new(8, 5) }, MarkerSize = 0, StrokeThickness = 0 };
        using var fixture = new HitFixture(series);
        // A vector-only headless layout exercises a huge logical chart without allocating a huge bitmap.
        fixture.Chart.Measure(new Size(2_100_000, 600)); fixture.Chart.Arrange(new Rect(0, 0, 2_100_000, 600));
        var context = fixture.Capture(series);
        Assert.True(context.PlotArea.Right > 1_100_000);
        var first = context.Projection.Project(new(9, 5)); var last = context.Projection.Project(new(8, 5));
        Assert.Equal(1_000_000, first.X); Assert.Equal(first, last);
        var position = first + new Vector(offset, 0);
        var expected = Assert.IsType<OracleHit>(Scan(context, "scatter", position, tolerance));
        Assert.Equal(1, expected.Row.SourceIndex);
        CompareRenderer(context, "scatter", position, tolerance); CompareChart(fixture, position, tolerance);
        Assert.Equal(1, Assert.IsType<ChartPointHit>(fixture.Chart.HitTestPoint(position, tolerance)).Index);
    }

    [AvaloniaFact]
    public void Custom_renderer_without_bounds_can_display_data_and_zoom_against_explicit_axes()
    {
        var series = new NoBoundsSeries { ItemsSource = new ChartPoint[] { new(5, 5) } };
        var chart = new CartesianChart
        {
            XAxis = new NumericAxis { Minimum = 0, Maximum = 10, ShowLabels = false },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 10, ShowLabels = false },
            ShowLegend = false, IsZoomEnabled = true, Foreground = Brushes.Magenta, EmptyText = "Empty renderer data",
        };
        chart.Series.Add(series);
        var window = new Window { Width = 900, Height = 600, Content = chart };
        try
        {
            window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            var group = new DrawingGroup(); using (var drawing = group.Open()) chart.Render(drawing);
            Assert.Contains(Drawings(group).OfType<GeometryDrawing>(), drawing => drawing.Brush is ISolidColorBrush brush && brush.Color == Colors.Blue);
            Assert.DoesNotContain(Drawings(group).OfType<GlyphRunDrawing>(), drawing => drawing.Foreground is ISolidColorBrush brush && brush.Color == Colors.Magenta);
            window.MouseWheel(chart.TranslatePoint(chart.PlotArea.Center, window)!.Value, new Vector(0, 1)); Dispatcher.UIThread.RunJobs();
            var view = Assert.IsType<ChartViewport>(chart.Viewport);
            Assert.True(view.XMaximum - view.XMinimum < 10 && view.YMaximum - view.YMinimum < 10);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(20d, 1, 0, 20.5)]
    [InlineData(80d, 4, 3, 50.5)]
    [InlineData(120d, 2, 0, -67.7)]
    public void Bar_candidates_include_group_offset_half_width_and_pointer_tolerance(double groupWidth, int groupCount, int groupIndex, double deltaX)
    {
        var series = new ProbeSeries("bar") { ItemsSource = new ChartPoint[] { new(5, 8) } };
        using var fixture = new HitFixture(series);
        var context = fixture.Capture(series) with { GroupWidth = groupWidth, GroupCount = groupCount, GroupIndex = groupIndex };
        var anchor = context.Projection.Project(new(5, 8));
        var position = new Point(anchor.X + deltaX, (anchor.Y + context.Projection.YOrigin) / 2);
        var expected = Assert.IsType<OracleHit>(Scan(context, "bar", position, 12));
        Assert.InRange(expected.DistanceSquared, 140, 144);
        CompareRenderer(context, "bar", position, 12);
        Assert.NotNull(series.Renderer.HitTest(context, position, 12));
    }

    [AvaloniaTheory]
    [InlineData("scatter")]
    [InlineData("bar")]
    public void Partially_clipped_shapes_and_large_markers_keep_candidates_outside_the_axis_extent(string kind)
    {
        var series = new ProbeSeries(kind) { ItemsSource = new ChartPoint[] { new(-.05, 5), new(5, 5) }, MarkerSize = 40, StrokeThickness = 12 };
        using var fixture = new HitFixture(series);
        var context = fixture.Capture(series);
        if (kind == "bar") context = context with { GroupWidth = 80, GroupCount = 1, GroupIndex = 0 };
        var position = new Point(context.PlotArea.Left + 1, context.Projection.Project(new(0, 5)).Y + (kind == "bar" ? 10 : 0));
        CompareRenderer(context, kind, position, 0);
        Assert.Equal(0, Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, position, 0)).SourceIndex);
        if (kind == "scatter")
        {
            var center = context.Projection.Project(new(5, 5));
            CompareRenderer(context, kind, center + new Vector(25.5, 0), 0);
            Assert.Equal(1, Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, center + new Vector(25.5, 0), 0)).SourceIndex);
        }
    }

    [AvaloniaFact]
    public void Different_published_snapshots_at_the_same_version_do_not_reuse_hits_or_bounds()
    {
        var series = new ProbeSeries("scatter") { ItemsSource = new ChartPoint[] { new(2, 2), new(8, 8) } };
        using var fixture = new HitFixture(series);
        var original = fixture.Capture(series);
        var originalPosition = original.Projection.Project(new(2, 2));
        CompareRenderer(original, "scatter", originalPosition, 0);
        var snapshot = series.CreateSnapshot();
        var moved = new ChartDataSnapshot(snapshot.Version, snapshot.Points.Select(item => item with { Point = new ChartPoint(item.Point.X + 1, item.Point.Y + 1) }).Reverse().ToArray());
        Assert.True(series.TryPublishSnapshot(moved, snapshot.Version));
        var replacement = fixture.Capture(series);
        Assert.NotSame(original.Data, replacement.Data); Assert.Equal(original.Data.Version, replacement.Data.Version);
        Assert.Equal(new ChartSeriesBounds(2, 8, 2, 8), series.Renderer.GetBounds(series, original.Data));
        Assert.Equal(new ChartSeriesBounds(3, 9, 3, 9), series.Renderer.GetBounds(series, replacement.Data));
        Assert.Null(series.Renderer.HitTest(replacement, originalPosition, 0));
        CompareRenderer(original, "scatter", originalPosition, 0);
        Assert.NotNull(series.Renderer.HitTest(original, originalPosition, 0));
        var newPosition = replacement.Projection.Project(new(3, 3));
        CompareChart(fixture, newPosition, 0);
        Assert.Equal(0, Assert.IsType<ChartPointHit>(fixture.Chart.HitTestPoint(newPosition, 0)).Index);
    }

    [AvaloniaFact]
    public void Bounds_are_a_full_finite_value_scan_for_each_independent_snapshot()
    {
        var series = new ProbeSeries("line");
        var first = Snapshot(7, new ChartPoint[] { new(9, -4), new(double.NaN, 99), new(-2, 8), new(5, double.NaN) });
        var second = Snapshot(7, new ChartPoint[] { new(100, 200), new(100, 200) });
        var missing = Snapshot(7, new ChartPoint[] { new(double.NaN, 1), new(1, double.NaN) });
        Assert.Equal(new ChartSeriesBounds(-2, 9, -4, 8), series.Renderer.GetBounds(series, first));
        Assert.Equal(new ChartSeriesBounds(100, 100, 200, 200), series.Renderer.GetBounds(series, second));
        Assert.Null(series.Renderer.GetBounds(series, missing));
        Assert.Equal(new ChartSeriesBounds(-2, 9, -4, 8), series.Renderer.GetBounds(series, first));
    }

    [AvaloniaTheory]
    [InlineData("line")]
    [InlineData("scatter")]
    [InlineData("bar")]
    public void External_non_monotonic_coupled_projection_matches_full_scan(string kind)
    {
        var random = new Random(1986);
        var points = Enumerable.Range(0, 180).Select(_ => new ChartPoint(random.Next(-2000, 2001), 1 + random.NextDouble() * 8)).ToArray();
        points[11] = new(1000, 3); points[91] = points[11];
        var series = new ProbeSeries(kind) { MarkerSize = 8, StrokeThickness = 2 };
        var projection = new FoldedProjection();
        var context = new ChartSeriesRenderContext(series, Snapshot(1, points), projection, Brushes.Blue, GroupIndex: 1, GroupCount: 2, GroupWidth: 20);
        foreach (var raw in points.Where((_, index) => index % 7 == 0).Append(points[11]))
        {
            var position = projection.Project(raw);
            CompareRenderer(context, kind, position, 0);
            CompareRenderer(context, kind, position + new Vector(7, 3), 12);
        }
    }

    // Reference geometry evaluates every physical snapshot row. It never reads a candidate index,
    // orders rows by SourceIndex, or calls a renderer to decide which row is nearest.
    private static OracleHit? Scan(ChartSeriesRenderContext context, string kind, Point position, double tolerance)
    {
        OracleHit? result = null;
        var radius = Math.Min(100, context.Series.MarkerSize) / 2 + Math.Min(100, context.Series.StrokeThickness) / 2;
        foreach (var row in context.Data.Points)
        {
            if (!double.IsFinite(row.Point.X) || !double.IsFinite(row.Point.Y)) continue;
            var projected = context.Projection.Project(row.Point);
            if (!double.IsFinite(projected.X) || !double.IsFinite(projected.Y)) continue;
            double distance;
            if (kind == "bar")
            {
                var slot = context.GroupWidth / Math.Max(1, context.GroupCount);
                var width = Math.Max(.5, slot * .86);
                var center = projected.X - context.GroupWidth / 2 + (context.GroupIndex + .5) * slot;
                var baseline = double.IsFinite(context.Projection.YOrigin) ? context.Projection.YOrigin : context.PlotArea.Bottom;
                baseline = Math.Clamp(baseline, context.PlotArea.Top, context.PlotArea.Bottom);
                var rectangle = new Rect(center - width / 2, Math.Min(projected.Y, baseline), width, Math.Abs(projected.Y - baseline)).Intersect(context.PlotArea);
                if (rectangle.Width <= 0 || rectangle.Height <= 0) continue;
                var nearest = new Point(Math.Clamp(position.X, rectangle.Left, rectangle.Right), Math.Clamp(position.Y, rectangle.Top, rectangle.Bottom));
                distance = Squared(position - nearest);
                if (distance > tolerance * tolerance) continue;
                projected = new Point(rectangle.Center.X, Math.Clamp(projected.Y, rectangle.Top, rectangle.Bottom));
            }
            else
            {
                if (!context.PlotArea.Inflate(radius).Contains(projected)) continue;
                distance = Squared(position - projected);
                if (distance > Math.Max(tolerance, radius) * Math.Max(tolerance, radius)) continue;
            }
            if (result is null || distance <= result.DistanceSquared) result = new(context.Series, row, projected, distance);
        }
        return result;
    }

    private static void CompareRenderer(ChartSeriesRenderContext context, string kind, Point position, double tolerance)
    {
        var expected = Scan(context, kind, position, tolerance);
        var actual = context.Series.Renderer.HitTest(context, position, tolerance);
        if (expected is null) { Assert.Null(actual); return; }
        Assert.NotNull(actual); Assert.Equal(expected.Row.SourceIndex, actual.SourceIndex);
        Assert.Equal(expected.Position.X, actual.Position.X, 7); Assert.Equal(expected.Position.Y, actual.Position.Y, 7);
        Assert.Equal(expected.DistanceSquared, actual.DistanceSquared, 7);
    }

    private static void CompareChart(HitFixture fixture, Point position, double tolerance)
    {
        OracleHit? expected = null;
        if (fixture.Chart.PlotArea.Contains(position))
            foreach (var series in fixture.Series)
            {
                var context = series.Recorder.Context!;
                var raw = context.Projection.Unproject(position);
                if (fixture.Chart.XAxis.IsInScaleBreak(raw.X) || fixture.Chart.YAxis.IsInScaleBreak(raw.Y)) continue;
                var candidate = Scan(context, series.Kind, position, tolerance);
                if (candidate is not null && (expected is null || candidate.DistanceSquared <= expected.DistanceSquared)) expected = candidate;
            }
        var actual = fixture.Chart.HitTestPoint(position, tolerance);
        if (expected is null) { Assert.Null(actual); return; }
        Assert.NotNull(actual); Assert.Same(expected.Series, actual.Series); Assert.Equal(expected.Row.SourceIndex, actual.Index);
        Assert.Equal(expected.Row.Point, actual.Point); Assert.Equal(expected.Row.StableId, actual.StableId); Assert.Equal(expected.Row.Item, actual.Item);
        Assert.Equal(expected.Position.X, actual.Position.X, 7); Assert.Equal(expected.Position.Y, actual.Position.Y, 7);
    }

    private static double Squared(Vector vector) => vector.X * vector.X + vector.Y * vector.Y;
    private static IEnumerable<Drawing> Drawings(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var item in Drawings(child)) yield return item;
    }
    private static ChartDataSnapshot Snapshot(long version, IReadOnlyList<ChartPoint> points) => new(version,
        points.Select((point, index) => new ChartDataPoint(index, index.ToString(), point, point.X, point, !double.IsFinite(point.X) || !double.IsFinite(point.Y))).ToArray());
    private sealed record OracleHit(ChartSeries Series, ChartDataPoint Row, Point Position, double DistanceSquared);

    private sealed class HitFixture : IDisposable
    {
        public HitFixture(params ProbeSeries[] series) : this(new NumericAxis { Minimum = 0, Maximum = 10 }, new NumericAxis { Minimum = 0, Maximum = 10 }, series) { }
        public HitFixture(ChartAxis x, ChartAxis y, params ProbeSeries[] series)
        {
            Series = series; Chart = new CartesianChart { ShowLegend = false, XAxis = x, YAxis = y };
            foreach (var item in series) Chart.Series.Add(item);
            Window = new Window { Width = 900, Height = 600, Content = Chart }; Window.Show(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); CaptureAll();
        }
        public CartesianChart Chart { get; }
        public Window Window { get; }
        public ProbeSeries[] Series { get; }
        public ChartSeriesRenderContext Capture(ProbeSeries series) { CaptureAll(); return series.Recorder.Context ?? throw new InvalidOperationException("The chart did not render its series context."); }
        private void CaptureAll() { using var drawing = new DrawingGroup().Open(); Chart.Render(drawing); }
        public void Dispose() => Window.Close();
    }

    private sealed class ProbeSeries : ChartSeries
    {
        public ProbeSeries(string kind)
        {
            Kind = kind;
            ChartSeries builtin = kind switch { "line" => new LineSeries(), "bar" => new BarSeries(), _ => new ScatterSeries() };
            ShowMarkers = builtin.ShowMarkers;
            Recorder = new ProbeRenderer(builtin.Renderer);
        }
        public string Kind { get; }
        public ProbeRenderer Recorder { get; }
        public override IChartSeriesRenderer Renderer => Recorder;
    }

    private sealed class ProbeRenderer(IChartSeriesRenderer builtin) : IChartSeriesRenderer
    {
        public ChartSeriesRenderContext? Context { get; private set; }
        public bool IncludesZero => builtin.IncludesZero;
        public bool UsesCategorySlots => builtin.UsesCategorySlots;
        public ChartLegendSymbol LegendSymbol => builtin.LegendSymbol;
        public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data) => builtin.GetBounds(series, data);
        public void Render(DrawingContext drawing, ChartSeriesRenderContext context) => Context = context;
        public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance) => builtin.HitTest(context, position, tolerance);
    }

    private sealed class FoldedProjection : IChartProjection
    {
        public Rect PlotArea => new(0, 0, 340, 220);
        public double YOrigin => 210;
        public Point Project(ChartPoint point) => new(150 + Math.Sin(point.X) * 70 + point.Y * 4, 170 - point.Y * 12 + Math.Cos(point.X) * 15);
        public ChartPoint Unproject(Point position) => new((position.X - 150) / 70, (170 - position.Y) / 12);
        public double MapX(double value) => 150 + Math.Sin(value) * 70;
        public double MapY(double value) => 170 - value * 12;
    }

    private sealed class NoBoundsSeries : ChartSeries
    {
        public override IChartSeriesRenderer Renderer { get; } = new NoBoundsRenderer();
    }

    private sealed class NoBoundsRenderer : IChartSeriesRenderer
    {
        public bool IncludesZero => false;
        public bool UsesCategorySlots => false;
        public ChartLegendSymbol LegendSymbol => ChartLegendSymbol.Circle;
        public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data) => null;
        public void Render(DrawingContext drawing, ChartSeriesRenderContext context)
        {
            foreach (var row in context.Data.Points) drawing.DrawEllipse(Brushes.Blue, null, context.Projection.Project(row.Point), 5, 5);
        }
        public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance) => null;
    }
}
