using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

[Collection("Spatial reading diagnostics")]
public sealed class SpatialReadingTests(ITestOutputHelper output)
{
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Hundred_thousand_same_axis_points_use_primary_then_orthogonal_distance_and_original_physical_ties(bool nearestY, bool earliest)
    {
        const int count = 100_000;
        var values = Enumerable.Range(0, count).Select(i => nearestY ? new ChartPoint(i * 100d / (count - 1), 50)
            : new ChartPoint(50, i * 100d / (count - 1))).ToArray();
        values[50_001] = values[50_000];
        var data = Data(values); var projection = Projection(data); var tree = ChartVisibilityIndex.For(data);
        var center = projection.Project(values[50_000]);
        Compare(data, projection, nearestY ? center + new Vector(0, 2.3) : center + new Vector(2.3, 0), nearestY, earliest);
        var builds = ChartVisibilityIndex.PointBuildCount; var queries = ChartVisibilityIndex.HitQueryCount;
        var nodes = ChartVisibilityIndex.HitNodeVisitCount; var candidates = ChartVisibilityIndex.HitCandidateCount;
        for (var i = 0; i < 24; i++)
        {
            var point = projection.Project(values[2000 + i * 3901]);
            Compare(data, projection, nearestY ? point + new Vector(0, 2.3) : point + new Vector(2.3, 0), nearestY, earliest);
        }
        Assert.Equal(builds, ChartVisibilityIndex.PointBuildCount); Assert.Equal(24, ChartVisibilityIndex.HitQueryCount - queries);
        Assert.InRange(ChartVisibilityIndex.HitNodeVisitCount - nodes, 24, 24 * 150);
        Assert.InRange(ChartVisibilityIndex.HitCandidateCount - candidates, 24, 24 * 150);
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            sourceCount = count, queries = 24, nearestY, earliest,
            pointTreeBuilds = ChartVisibilityIndex.PointBuildCount - builds,
            nodeVisits = ChartVisibilityIndex.HitNodeVisitCount - nodes,
            candidates = ChartVisibilityIndex.HitCandidateCount - candidates,
        }));
        var moved = new CartesianChart.CartesianProjection(new(13, 17, 800, 500),
            ChartScale.CreateForView(new NumericAxis(), 20, 80), ChartScale.CreateForView(new NumericAxis(), 20, 80), data);
        Compare(data, moved, moved.PlotArea.Center, nearestY, earliest);
        Assert.Equal(builds, ChartVisibilityIndex.PointBuildCount); Assert.Same(tree, ChartVisibilityIndex.For(data.WithRawPoints()));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_clipped_nearest_axis_group_does_not_hide_the_next_visible_original_group(bool nearestY)
    {
        var points = Enumerable.Range(0, 2048).Select(i => nearestY
            ? i < 1024 ? new ChartPoint(200, 50) : new ChartPoint(50, 51)
            : i < 1024 ? new ChartPoint(50, 200) : new ChartPoint(51, 50)).ToArray();
        var data = Data(points); var projection = Projection(data);
        var query = projection.Project(new(50, 50));
        Compare(data, projection, query, nearestY, false);
        var result = ChartVisibilityIndex.For(data).NearestReading(projection, query, nearestY, false);
        Assert.NotNull(result); var hit = result.GetValueOrDefault();
        Assert.Equal(2047, hit.Ordinal);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Inverse_logarithmic_domains_and_invalid_orthogonal_values_match_exhaustive_visible_readings(bool nearestY, bool inverse)
    {
        var random = new Random(404_040);
        var points = Enumerable.Range(0, 2048).Select(i =>
        {
            var variable = i % 4 == 0 ? -i : Math.Pow(10, random.NextDouble() * 6);
            return nearestY ? new ChartPoint(variable, 10) : new ChartPoint(10, variable);
        }).ToArray();
        points[^1] = points[1283];
        var data = Data(points);
        var projection = new CartesianChart.CartesianProjection(new(11, 17, 800, 500),
            ChartScale.CreateForView(new LogarithmicAxis { IsInversed = inverse }, 1, 1e6),
            ChartScale.CreateForView(new LogarithmicAxis { IsInversed = !inverse }, 1, 1e6), data);
        foreach (var earliest in new[] { false, true })
            for (var i = 0; i < 16; i++)
            {
                var value = nearestY ? new ChartPoint(Math.Pow(10, i / 3d), 10) : new ChartPoint(10, Math.Pow(10, i / 3d));
                Compare(data, projection, projection.Project(value), nearestY, earliest);
            }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Distinct_raw_coordinates_that_share_a_forward_mapping_plateau_keep_exact_lexicographic_order(bool nearestY)
    {
        var points = Enumerable.Range(0, 2048).Select(i => nearestY ? new ChartPoint(i * 100d / 2047, i % 2 == 0 ? 1e-300 : -1e-300)
            : new ChartPoint(i % 2 == 0 ? 1e-300 : -1e-300, i * 100d / 2047)).ToArray();
        var data = Data(points);
        var projection = new CartesianChart.CartesianProjection(new(11, 17, 800, 500),
            ChartScale.CreateForView(new NumericAxis(), nearestY ? 0 : -1e300, nearestY ? 100 : 1e300),
            ChartScale.CreateForView(new NumericAxis(), nearestY ? -1e300 : 0, nearestY ? 1e300 : 100), data);
        foreach (var earliest in new[] { false, true })
            foreach (var ordinal in new[] { 14, 300, 500, 1024, 1800 })
                Compare(data, projection, projection.Project(points[ordinal]) + (nearestY ? new Vector(0, 3.7) : new Vector(3.7, 0)), nearestY, earliest);
    }

    [AvaloniaTheory]
    [InlineData(false, 200)]
    [InlineData(false, 2000)]
    [InlineData(true, 200)]
    [InlineData(true, 2000)]
    public void Chart_readings_only_use_the_tree_for_a_dense_nearest_same_coordinate_group(bool nearestY, int count)
    {
        var rows = Enumerable.Range(0, count).Select(i => nearestY ? new Row($"row-{i}", i * 100d / (count - 1), 50)
            : new Row($"row-{i}", 50, i * 100d / (count - 1))).ToArray();
        var line = Series(rows);
        var chart = Chart(line); chart.TooltipMode = nearestY ? ChartTooltipMode.SharedY : ChartTooltipMode.SharedX;
        using var host = new TemplateFeaturesHost(chart);
        var context = Context(chart); var query = context.Projection.Project(new(50, 50));
        var queries = ChartVisibilityIndex.HitQueryCount;
        SegmentTestHost.RaiseExactMove(chart, query); host.Drain();
        Assert.NotNull(chart.HoveredPoint);
        Assert.Equal(count > 512, ChartVisibilityIndex.HitQueryCount > queries);
        var expected = Oracle(context.Data, context.Projection, query, nearestY, false);
        Assert.Equal(context.Data.Points[expected.Ordinal].SourceIndex, chart.HoveredPoint!.Index);
        Assert.Same(rows[chart.HoveredPoint.Index], chart.HoveredPoint.Item);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shared_tooltip_chooses_the_last_original_tie_and_nearest_point_trackball_chooses_the_first(bool nearestY)
    {
        var rows = Enumerable.Range(0, 2048).Select(i => new Row($"row-{i}", 50, 50)).ToArray();
        var line = Series(rows); var chart = Chart(line);
        chart.TooltipMode = nearestY ? ChartTooltipMode.SharedY : ChartTooltipMode.SharedX;
        using var host = new TemplateFeaturesHost(chart);
        var data = line.CreateSnapshot();
        var reversed = new ChartDataSnapshot(data.Version, data.Points.Reverse().Select(point => point with { RawPoint = new(123, 456) }).ToArray());
        Assert.True(line.TryPublishSnapshot(reversed, data.Version)); host.Drain();
        var query = Context(chart).Projection.Project(new(50, 50));
        SegmentTestHost.RaiseExactMove(chart, query); host.Drain();
        Assert.Equal(0, chart.HoveredPoint!.Index); Assert.Same(rows[0], chart.HoveredPoint.Item);
        Assert.Equal(new ChartPoint(123, 456), chart.HoveredPoint.RawPoint);
        var builds = ChartVisibilityIndex.PointBuildCount;
        chart.TrackballMode = nearestY ? ChartTrackballMode.Horizontal : ChartTrackballMode.Vertical;
        chart.TrackballSnapMode = ChartTrackballSnapMode.NearestPoint; host.Drain();
        SegmentTestHost.RaiseExactMove(chart, query); host.Drain();
        Assert.Equal(2047, chart.HoveredPoint!.Index); Assert.Same(rows[^1], chart.HoveredPoint.Item);
        Assert.Equal(new ChartPoint(123, 456), chart.HoveredPoint.RawPoint);
        Assert.Equal(builds, ChartVisibilityIndex.PointBuildCount);
    }

    [AvaloniaFact]
    public void Grouped_category_series_keep_their_existing_shifted_center_reading_path()
    {
        var firstRows = Enumerable.Range(0, 2048).Select(i => new Row($"a-{i}", 50, 40)).ToArray();
        var secondRows = Enumerable.Range(0, 2048).Select(i => new Row($"b-{i}", 50, 60)).ToArray();
        var first = new BarSeries { ItemsSource = firstRows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", ShowMarkers = false };
        var second = new BarSeries { ItemsSource = secondRows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", ShowMarkers = false };
        var chart = Chart(first); chart.Series.Add(second); chart.TooltipMode = ChartTooltipMode.SharedX;
        using var host = new TemplateFeaturesHost(chart);
        var query = Context(chart).Projection.Project(new(50, 50)); var queries = ChartVisibilityIndex.HitQueryCount;
        SegmentTestHost.RaiseExactMove(chart, query); host.Drain();
        Assert.Equal(queries, ChartVisibilityIndex.HitQueryCount); Assert.Equal(2, chart.HoveredPoints.Count);
        Assert.Same(firstRows[^1], chart.HoveredPoints.Single(point => ReferenceEquals(point.Series, first)).Item);
        Assert.Same(secondRows[^1], chart.HoveredPoints.Single(point => ReferenceEquals(point.Series, second)).Item);
    }

    [AvaloniaFact]
    public void A_mutable_custom_axis_keeps_fresh_exhaustive_shared_readings()
    {
        var rows = Enumerable.Range(0, 2048).Select(i => new Row($"row-{i}", 50, i * 100d / 2047)).ToArray();
        var line = Series(rows); var chart = Chart(line); var axis = new MutableReadingAxis { Minimum = 0, Maximum = 100 };
        chart.XAxis = axis; chart.TooltipMode = ChartTooltipMode.SharedX;
        using var host = new TemplateFeaturesHost(chart);
        var before = Context(chart).Projection.Project(new(50, 50)); var queries = ChartVisibilityIndex.HitQueryCount;
        SegmentTestHost.RaiseExactMove(chart, before); host.Drain(); Assert.NotNull(chart.HoveredPoint);
        axis.Offset = 20;
        var context = Context(chart); var after = context.Projection.Project(new(50, 50)); Assert.NotEqual(before, after);
        SegmentTestHost.RaiseExactMove(chart, after); host.Drain();
        var expected = Oracle(context.Data, context.Projection, after, nearestY: false, earliest: false);
        Assert.Equal(context.Data.Points[expected.Ordinal].SourceIndex, chart.HoveredPoint!.Index);
        Assert.Equal(expected.Position, chart.HoveredPoint.Position); Assert.Equal(queries, ChartVisibilityIndex.HitQueryCount);
    }

    private static void Compare(ChartDataSnapshot data, IChartProjection projection, Point query, bool nearestY, bool earliest)
    {
        var expected = Oracle(data, projection, query, nearestY, earliest);
        var actual = ChartVisibilityIndex.For(data).NearestReading(projection, query, nearestY, earliest);
        if (expected.Ordinal < 0) { Assert.Null(actual); return; }
        Assert.NotNull(actual); var winner = actual.GetValueOrDefault();
        Assert.Equal(expected.Ordinal, winner.Ordinal); Assert.Equal(expected.Position, winner.Position);
    }
    private static (int Ordinal, Point Position) Oracle(ChartDataSnapshot data, IChartProjection projection,
        Point query, bool nearestY, bool earliest)
    {
        var winner = -1; var bestMajor = double.PositiveInfinity; var bestMinor = double.PositiveInfinity; var position = default(Point);
        for (var ordinal = 0; ordinal < data.Count; ordinal++)
        {
            var point = data.Points[ordinal].Point;
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) continue;
            var screen = projection.Project(point);
            if (!double.IsFinite(screen.X) || !double.IsFinite(screen.Y) || !projection.PlotArea.Contains(screen)) continue;
            var major = Math.Abs(nearestY ? screen.Y - query.Y : screen.X - query.X);
            var minor = Math.Abs(nearestY ? screen.X - query.X : screen.Y - query.Y);
            var replace = major < bestMajor || major == bestMajor && (minor < bestMinor || minor == bestMinor && !earliest);
            if (!replace) continue;
            winner = ordinal; bestMajor = major; bestMinor = minor; position = screen;
        }
        return (winner, position);
    }
    private static ChartDataSnapshot Data(ChartPoint[] points) => new(0, points.Select((point, i) =>
        new ChartDataPoint(points.Length - 1 - i, $"original-{i}", null, null, point, false)).ToArray());
    private static CartesianChart.CartesianProjection Projection(ChartDataSnapshot data) => new(new(11, 17, 800, 500),
        ChartScale.CreateForView(new NumericAxis(), 0, 100), ChartScale.CreateForView(new NumericAxis(), 0, 100), data);
    private static LineSeries Series(Row[] rows) => new() { ItemsSource = rows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id",
        ShowMarkers = false, MarkerSize = 0, SamplingMode = ChartSamplingMode.MinMax };
    private static CartesianChart Chart(ChartSeries line)
    {
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } }; chart.Series.Add(line); return chart;
    }
    private static ChartSeriesRenderContext Context(CartesianChart chart)
    {
        typeof(CartesianChart).GetMethod("GetScene", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(chart, null);
        var scene = typeof(CartesianChart).GetField("_scene", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
        var snapshots = (Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!;
        var snapshot = snapshots.GetValue(0)!;
        return (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context")!.GetValue(snapshot)!;
    }
    private sealed record Row(string Id, double X, double Y);
    private sealed class MutableReadingAxis : ChartAxis
    {
        internal double Offset;
        public override double Transform(double value) => value > 0 && value < 100 ? value + Offset : value;
    }
}

[CollectionDefinition("Spatial reading diagnostics", DisableParallelization = true)]
public sealed class SpatialReadingDiagnosticsCollection;
