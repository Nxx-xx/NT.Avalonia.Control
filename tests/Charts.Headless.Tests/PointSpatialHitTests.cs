using System.Collections;
using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

[Collection("Point spatial hit diagnostics")]
public sealed class PointSpatialHitTests(ITestOutputHelper output)
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Same_x_hundred_thousand_original_points_use_spatial_pruning_and_last_physical_ties(bool scatter)
    {
        const int count = 100_000;
        var data = Snapshot(Enumerable.Range(0, count).Select(i => new ChartPoint(50, i * 100d / (count - 1))).ToArray());
        var series = Series(scatter); var context = Context(series, data);
        var initial = context.Projection.Project(new(50, 40));
        Compare(context, initial, 24);
        var builds = ChartVisibilityIndex.PointBuildCount; var nodes = ChartVisibilityIndex.HitNodeVisitCount;
        var candidates = ChartVisibilityIndex.HitCandidateCount; var queries = ChartVisibilityIndex.HitQueryCount;
        for (var i = 0; i < 24; i++)
        {
            var query = context.Projection.Project(new(50, 10 + i * 3.1));
            Compare(context, query, 24);
        }
        Assert.Equal(builds, ChartVisibilityIndex.PointBuildCount);
        Assert.Equal(24, ChartVisibilityIndex.HitQueryCount - queries);
        Assert.InRange(ChartVisibilityIndex.HitNodeVisitCount - nodes, 24, 24 * 100);
        Assert.InRange(ChartVisibilityIndex.HitCandidateCount - candidates, 24, 24 * 100);
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            scenario = scatter ? "scatter-same-x-100k" : "line-same-x-100k", sourceCount = count, queries = 24,
            pointTreeBuilds = ChartVisibilityIndex.PointBuildCount - builds,
            nodes = ChartVisibilityIndex.HitNodeVisitCount - nodes,
            candidates = ChartVisibilityIndex.HitCandidateCount - candidates,
        }));
    }

    [AvaloniaFact]
    public void Off_center_coincident_points_choose_last_physical_row_with_one_exact_evaluation()
    {
        const int count = 100_000;
        var data = Snapshot(Enumerable.Repeat(new ChartPoint(50, 50), count).ToArray());
        var context = Context(Series(true), data);
        var query = context.Projection.Project(new(50, 50)) + new Vector(2, 3);
        Compare(context, query, 4);
        var nodes = ChartVisibilityIndex.HitNodeVisitCount; var candidates = ChartVisibilityIndex.HitCandidateCount;
        for (var i = 0; i < 16; i++)
        {
            var hit = Assert.IsType<ChartSeriesHit>(context.Series.Renderer.HitTest(context, query, 4));
            Assert.Equal(0, hit.SourceIndex); Assert.Equal(13, hit.DistanceSquared, 8);
        }
        Assert.Equal(16, ChartVisibilityIndex.HitNodeVisitCount - nodes);
        Assert.Equal(16, ChartVisibilityIndex.HitCandidateCount - candidates);
    }

    [AvaloniaTheory]
    [InlineData("linear")]
    [InlineData("inverse")]
    [InlineData("log")]
    [InlineData("invalid-log")]
    [InlineData("wide-log")]
    [InlineData("break")]
    [InlineData("clamp")]
    [InlineData("marker-edge")]
    public void Spatial_hits_match_full_original_screen_scan_for_domains_breaks_inverse_clamps_and_marker_edges(string kind)
    {
        const int count = 2048;
        var random = new Random(404004);
        var values = Enumerable.Range(0, count).Select(_ => new ChartPoint(random.NextDouble() * 100, random.NextDouble() * 100)).ToArray();
        ChartAxis x = new NumericAxis(); ChartAxis y = new NumericAxis();
        var minimum = 0d; var maximum = 100d; var yMinimum = 0d; var yMaximum = 100d;
        var plot = new Rect(17.25, 23.125, 700.375, 420.625);
        var series = Series(true);
        if (kind is "log" or "invalid-log" or "wide-log")
        {
            x = new LogarithmicAxis(); y = new LogarithmicAxis();
            minimum = yMinimum = kind == "wide-log" ? 1e-300 : 1;
            maximum = yMaximum = kind == "wide-log" ? 1e300 : 1e6;
            values = Enumerable.Range(0, count).Select(i => kind == "wide-log"
                ? new ChartPoint(Math.Pow(10, -280 + random.NextDouble() * 560), Math.Pow(10, -280 + random.NextDouble() * 560))
                : new ChartPoint(kind == "invalid-log" && i < count / 2 ? i - count / 2 : Math.Pow(10, random.NextDouble() * 6),
                    Math.Pow(10, random.NextDouble() * 6))).ToArray();
        }
        if (kind == "inverse") x.IsInversed = y.IsInversed = true;
        if (kind == "break")
        {
            x.ScaleBreaks.Add(new() { Start = 20, End = 40, CompressionRatio = .15 });
            y.ScaleBreaks.Add(new() { Start = 50, End = 70, CompressionRatio = .2 });
        }
        if (kind == "clamp")
        {
            minimum = yMinimum = -1; maximum = yMaximum = 1;
            plot = new(-1_000_010, -1_000_010, 2_000_020, 2_000_020);
            values = Enumerable.Range(0, count).Select(i => new ChartPoint(i % 2 == 0 ? 1e300 : -1e300, i % 3 == 0 ? 1e300 : -1e300)).ToArray();
        }
        if (kind == "marker-edge")
        {
            series.ShowMarkers = true; series.MarkerSize = 20; series.StrokeThickness = 0;
            values = Enumerable.Range(0, count).Select(i => new ChartPoint(50, 100 + i % 10 * .1)).ToArray();
        }
        values[35] = new(double.NaN, 5); values[785] = new(5, double.NaN); values[^1] = values[15];
        var data = Snapshot(values);
        var projection = new CartesianChart.CartesianProjection(plot, ChartScale.CreateForView(x, minimum, maximum),
            ChartScale.CreateForView(y, yMinimum, yMaximum), data);
        var context = new ChartSeriesRenderContext(series, data, projection, Brushes.Blue);
        var queries = ChartVisibilityIndex.HitQueryCount;
        for (var i = 0; i < 32; i++)
        {
            var point = values[i * 61]; if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) continue;
            var query = projection.Project(point); if (!double.IsFinite(query.X) || !double.IsFinite(query.Y)) continue;
            Compare(context, query + new Vector(i % 3, i % 5), 2_000_001);
        }
        Assert.True(ChartVisibilityIndex.HitQueryCount > queries);
    }

    [AvaloniaFact]
    public void A_mutable_custom_axis_keeps_exhaustive_fresh_point_projection()
    {
        var axis = new MutableAxis();
        var points = Enumerable.Range(0, 2048).Select(i => new ChartPoint(50, 10 + i * 80d / 2048)).ToArray();
        var data = Snapshot(points); var series = Series(true);
        var projection = new CartesianChart.CartesianProjection(new(0, 0, 800, 500),
            ChartScale.CreateForView(axis, 0, 100), ChartScale.CreateForView(new NumericAxis(), 0, 100), data);
        var context = new ChartSeriesRenderContext(series, data, projection, Brushes.Blue);
        var queries = ChartVisibilityIndex.HitQueryCount;
        var before = projection.Project(points[1400]); Compare(context, before, 0);
        axis.Offset = 20;
        var after = projection.Project(points[1400]); Assert.NotEqual(before, after);
        Compare(context, after, 0); Assert.Null(series.Renderer.HitTest(context, before, 0));
        Assert.Equal(queries, ChartVisibilityIndex.HitQueryCount);
    }

    [AvaloniaFact]
    public void View_and_sampling_changes_keep_source_mapping_and_point_tree_while_source_replacement_rebuilds_them()
    {
        var rows = Enumerable.Range(0, 20_000).Select(i => new Row($"row-{i}", 50, i * 100d / 19_999)).ToArray();
        var source = new CountedRows(rows);
        var line = new LineSeries { ItemsSource = source, CategoryPath = "X", ValuePath = "Y", IdPath = "Id",
            ShowMarkers = false, SamplingMode = ChartSamplingMode.MinMax };
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(line);
        using var host = new TemplateFeaturesHost(chart);
        var context = Context(chart); var query = context.Projection.Project(new(50, 50));
        Assert.NotNull(chart.HitTestPoint(query, 12));
        var original = context.Data; var builds = ChartVisibilityIndex.PointBuildCount; var enumerations = source.Enumerations;
        chart.Viewport = new(40, 60, 20, 80); host.Drain();
        Assert.Same(original, Context(chart).Data); Assert.NotNull(chart.HitTestPoint(Context(chart).Projection.Project(new(50, 50)), 12));
        line.SamplingMode = ChartSamplingMode.None; host.Drain();
        Assert.Same(original, Context(chart).Data); Assert.NotNull(chart.HitTestPoint(Context(chart).Projection.Project(new(50, 50)), 12));
        Assert.Equal(builds, ChartVisibilityIndex.PointBuildCount); Assert.Equal(enumerations, source.Enumerations);
        line.ItemsSource = new CountedRows(rows.ToArray()); host.Drain();
        Assert.NotSame(original, Context(chart).Data); Assert.NotNull(chart.HitTestPoint(Context(chart).Projection.Project(new(50, 50)), 12));
        Assert.True(ChartVisibilityIndex.PointBuildCount > builds);
    }

    [AvaloniaFact]
    public void Published_reverse_physical_order_keeps_raw_item_identity_for_tooltip_and_selection()
    {
        var rows = Enumerable.Range(0, 2048).Select(i => new Row($"id-{i}", 50, 50)).ToArray();
        var line = new LineSeries { ItemsSource = rows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", ShowMarkers = false,
            SamplingMode = ChartSamplingMode.MinMax };
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } }; chart.Series.Add(line);
        using var host = new TemplateFeaturesHost(chart);
        var data = line.CreateSnapshot();
        var reversed = new ChartDataSnapshot(data.Version, data.Points.Reverse().Select(point => point with { RawPoint = new(123, 456) }).ToArray());
        Assert.True(line.TryPublishSnapshot(reversed, data.Version)); host.Drain();
        var query = Context(chart).Projection.Project(new(50, 50));
        var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(query, 0));
        Assert.Equal(0, hit.Index); Assert.Same(rows[0], hit.Item); Assert.Equal(rows[0].Id, hit.StableId); Assert.Equal(new ChartPoint(123, 456), hit.RawPoint);
        chart.SelectedPoint = hit; Assert.Same(rows[0], chart.SelectedPoint!.Item);
        SegmentTestHost.RaiseExactMove(chart, query); host.Drain();
        Assert.Same(rows[0], chart.HoveredPoint!.Item); Assert.Equal(new ChartPoint(123, 456), chart.HoveredPoint.RawPoint);
    }

    [AvaloniaFact]
    public void Multiaxis_coincident_paths_return_later_series_original_value_without_cross_axis_normalization()
    {
        var firstRows = Enumerable.Range(0, 1024).Select(i => new Row($"a-{i}", 50, 50)).ToArray();
        var secondRows = Enumerable.Range(0, 1024).Select(i => new Row($"b-{i}", 50, 500)).ToArray();
        var first = new LineSeries { ItemsSource = firstRows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", ShowMarkers = false };
        var second = new LineSeries { ItemsSource = secondRows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", ShowMarkers = false, YAxisKey = "pressure" };
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Axes.Add(new NumericAxis { Key = "pressure", Minimum = 0, Maximum = 1000, Position = ChartAxisPosition.Far });
        chart.Series.Add(first); chart.Series.Add(second);
        using var host = new TemplateFeaturesHost(chart);
        var query = Context(chart, 1).Projection.Project(new(50, 500));
        var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(query, 0));
        Assert.Same(second, hit.Series); Assert.Same(secondRows[^1], hit.Item); Assert.Equal(500, hit.Point.Y);
        second.IsVisible = false; host.Drain();
        hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(Context(chart).Projection.Project(new(50, 50)), 0));
        Assert.Same(first, hit.Series); Assert.Same(firstRows[^1], hit.Item); Assert.Equal(50, hit.Point.Y);
    }

    private static ChartSeries Series(bool scatter)
    {
        ChartSeries result = scatter ? new ScatterSeries() : new LineSeries();
        result.ShowMarkers = false; result.MarkerSize = 0; result.StrokeThickness = 0;
        return result;
    }
    private static ChartDataSnapshot Snapshot(ChartPoint[] points) => new(0, points.Select((point, i) =>
        new ChartDataPoint(points.Length - 1 - i, $"identity-{i}", point, null, point, !double.IsFinite(point.X) || !double.IsFinite(point.Y))).ToArray());
    private static ChartSeriesRenderContext Context(ChartSeries series, ChartDataSnapshot data) => new(series, data,
        new CartesianChart.CartesianProjection(new(17.25, 23.125, 700.375, 420.625), ChartScale.CreateForView(new NumericAxis(), 0, 100),
            ChartScale.CreateForView(new NumericAxis(), 0, 100), data), Brushes.Blue);
    private static void Compare(ChartSeriesRenderContext context, Point query, double tolerance)
    {
        // Exhaust every original physical row and evaluate only the established
        // screen distance/visibility contract. No candidate tree participates.
        var radius = ChartMarkerGeometry.HitRadius(context.Series); var limit = Math.Max(tolerance, radius);
        var plot = context.PlotArea.Inflate(radius); var best = double.PositiveInfinity; var sourceIndex = -1; var position = default(Point);
        foreach (var row in context.Data.Points)
        {
            if (!double.IsFinite(row.Point.X) || !double.IsFinite(row.Point.Y)) continue;
            var screen = context.Projection.Project(row.Point);
            if (!double.IsFinite(screen.X) || !double.IsFinite(screen.Y) || !plot.Contains(screen)) continue;
            var dx = screen.X - query.X; var dy = screen.Y - query.Y; var distance = dx * dx + dy * dy;
            if (distance > limit * limit || distance > best) continue;
            best = distance; sourceIndex = row.SourceIndex; position = screen;
        }
        var hit = context.Series.Renderer.HitTest(context, query, tolerance);
        if (sourceIndex < 0) { Assert.Null(hit); return; }
        Assert.NotNull(hit); Assert.Equal(sourceIndex, hit.SourceIndex); Assert.Equal(best, hit.DistanceSquared, 8);
        Assert.Equal(position, hit.Position);
    }
    private static ChartSeriesRenderContext Context(CartesianChart chart, int index = 0)
    {
        typeof(CartesianChart).GetMethod("GetScene", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(chart, null);
        var scene = typeof(CartesianChart).GetField("_scene", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
        var snapshots = (Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!;
        var snapshot = snapshots.GetValue(index)!;
        return (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context")!.GetValue(snapshot)!;
    }
    private sealed record Row(string Id, double X, double Y);
    private sealed class CountedRows(Row[] rows) : IEnumerable<Row>
    {
        internal int Enumerations;
        public IEnumerator<Row> GetEnumerator() { Enumerations++; return ((IEnumerable<Row>)rows).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class MutableAxis : ChartAxis
    {
        internal double Offset;
        public override double Transform(double value) => value > 0 && value < 100 ? value + Offset : value;
    }
}

[CollectionDefinition("Point spatial hit diagnostics", DisableParallelization = true)]
public sealed class PointSpatialHitDiagnosticsCollection;
