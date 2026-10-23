using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

[Collection("Segment index diagnostics")]
public sealed class SegmentIndexTests(ITestOutputHelper output)
{
    [AvaloniaTheory]
    [InlineData("ascending")]
    [InlineData("descending")]
    [InlineData("same-x")]
    [InlineData("unordered")]
    [InlineData("overlap")]
    [InlineData("gaps")]
    public void Indexed_original_segments_match_an_independent_exhaustive_screen_oracle(string kind)
    {
        var random = new Random(4507);
        var points = Enumerable.Range(0, 768).Select(i => new ChartPoint(1 + i * 98d / 767, 1 + random.NextDouble() * 98)).ToArray();
        switch (kind)
        {
            case "descending": Array.Reverse(points); break;
            case "same-x": points = points.Select(p => p with { X = 40 }).ToArray(); break;
            case "unordered": random.Shuffle(points); break;
            case "overlap": points = Enumerable.Repeat(new ChartPoint(40, 60), points.Length).ToArray(); break;
            case "gaps": for (var i = 13; i < points.Length; i += 29) points[i] = new(double.NaN, double.NaN); break;
        }
        var rows = points.Select((point, ordinal) => new SegmentRow($"original-{ordinal}", point.X, point.Y)).ToArray();
        var line = LineInterpolationTests.CreateLine(rows);
        using var host = new SegmentTestHost(new(0, 100), new(0, 100), line);
        var context = Context(host.Chart);
        var projected = points.Select(context.Projection.Project).ToArray();
        for (var queryIndex = 0; queryIndex < 60; queryIndex++)
        {
            var query = kind == "overlap" ? context.Projection.Project(new(40, 60)) :
                new Point(host.Chart.PlotArea.Left + random.NextDouble() * host.Chart.PlotArea.Width,
                    host.Chart.PlotArea.Top + random.NextDouble() * host.Chart.PlotArea.Height);
            var expected = Oracle(projected, query);
            var actual = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(query, 100_000));
            Assert.Equal(expected.Ordinal, actual.Start.Index); Assert.Equal(expected.Ordinal + 1, actual.End.Index);
            Assert.Equal(expected.Fraction, actual.Fraction, 10); Assert.Equal(expected.Distance, actual.DistanceSquared, 7);
            Assert.Same(rows[expected.Ordinal], actual.Start.Item); Assert.Same(rows[expected.Ordinal + 1], actual.End.Item);
            Assert.Equal(rows[expected.Ordinal].Id, actual.Start.StableId);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dense_original_segment_queries_reuse_one_data_index_across_zoom_and_sampling(bool sameX)
    {
        const int count = 100_000;
        var points = Enumerable.Range(0, count).Select(i => sameX ? new ChartPoint(50, i) : new ChartPoint(i, i)).ToArray();
        var line = new LineSeries { ItemsSource = points, ShowMarkers = false, SamplingMode = ChartSamplingMode.MinMax };
        using var host = new SegmentTestHost(sameX ? new(0, 100) : new(0, count), new(0, count), line);
        var context = Context(host.Chart);
        var initial = context.Projection.Project(points[45_678]);
        Assert.NotNull(host.Chart.HitTestSegment(initial, .01));
        var builds = ChartSegmentIndex.BuildCount; var nodes = ChartSegmentIndex.NodeVisitCount;
        var candidates = ChartSegmentIndex.CandidateCount; var projected = Counter(host.Chart, "_segmentProjectedVertexCount");
        for (var i = 0; i < 50; i++)
        {
            var ordinal = 10_000 + i * 1_501;
            var query = context.Projection.Project(points[ordinal]);
            var hit = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(query, .01));
            // The immediately preceding physical edge owns an exact interior vertex.
            Assert.Equal(ordinal - 1, hit.Start.Index); Assert.Equal(ordinal, hit.End.Index);
        }
        Assert.Equal(builds, ChartSegmentIndex.BuildCount);
        Assert.InRange(ChartSegmentIndex.NodeVisitCount - nodes, 1, 10_000);
        Assert.InRange(ChartSegmentIndex.CandidateCount - candidates, 1, 10_000);
        Assert.InRange(Counter(host.Chart, "_segmentProjectedVertexCount") - projected, 1, 10_000);
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            scenario = sameX ? "same-x-100k" : "ascending-100k", sourceCount = count, queries = 50,
            hierarchyBuilds = ChartSegmentIndex.BuildCount - builds,
            nodeVisits = ChartSegmentIndex.NodeVisitCount - nodes,
            adjacentEdgeCandidates = ChartSegmentIndex.CandidateCount - candidates,
            projectedOriginalEndpoints = Counter(host.Chart, "_segmentProjectedVertexCount") - projected,
            ownedHierarchyPayloadBytes = ChartSegmentIndex.For(context.Data).EstimatedBytes,
        }));
        host.Chart.Viewport = sameX ? new(45, 55, 45_000, 46_000) : new(45_000, 46_000, 45_000, 46_000);
        host.Drain();
        projected = Counter(host.Chart, "_segmentProjectedVertexCount");
        var next = Context(host.Chart).Projection.Project(points[45_678]);
        Assert.NotNull(host.Chart.HitTestSegment(next, .01));
        Assert.Equal(builds, ChartSegmentIndex.BuildCount);
        Assert.InRange(Counter(host.Chart, "_segmentProjectedVertexCount") - projected, 1, 1000);
        line.SamplingMode = ChartSamplingMode.None; host.Drain();
        Assert.NotNull(host.Chart.HitTestSegment(Context(host.Chart).Projection.Project(points[45_678]), .01));
        Assert.Equal(builds, ChartSegmentIndex.BuildCount);
        line.ItemsSource = points.ToArray(); host.Drain();
        Assert.NotNull(host.Chart.HitTestSegment(Context(host.Chart).Projection.Project(points[45_678]), .01));
        Assert.True(ChartSegmentIndex.BuildCount > builds);
    }

    [AvaloniaFact]
    public void Sampling_omitted_segments_return_physical_adjacent_records_with_nonordinal_identity_and_raw_values()
    {
        const int count = 20_000;
        var rows = Enumerable.Range(0, count).Select(i => new SegmentRow($"id-{i}", i, 50 + Math.Sin(i * .001))).ToArray();
        var line = LineInterpolationTests.CreateLine(rows); line.SamplingMode = ChartSamplingMode.MinMax;
        using var host = new SegmentTestHost(new(0, count), new(0, 100), line);
        var original = line.CreateSnapshot();
        // Publication permits a physical reorder while source indices must still
        // address matching original source items within the existing source.
        var replacement = new ChartDataSnapshot(original.Version, original.Points.Reverse().Select(p => p with
        { RawPoint = new ChartPoint(p.Point.X + .25, p.Point.Y + 1000) }).ToArray());
        Assert.True(line.TryPublishSnapshot(replacement, original.Version)); host.Drain();
        var context = Context(host.Chart); var sampled = ChartSamplingPlanner.For(context).Sampling!;
        var ordinals = sampled.ToHashSet();
        var ordinal = Enumerable.Range(2000, 1000).First(i => !ordinals.Contains(i) && !ordinals.Contains(i + 1));
        var a = context.Projection.Project(replacement.Points[ordinal].Point);
        var b = context.Projection.Project(replacement.Points[ordinal + 1].Point);
        var query = new Point(a.X + (b.X - a.X) * .37, a.Y + (b.Y - a.Y) * .37);
        var hit = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(query, .001));
        Assert.Equal(count - 1 - ordinal, hit.Start.Index); Assert.Equal(count - 2 - ordinal, hit.End.Index);
        Assert.Same(rows[count - 1 - ordinal], hit.Start.Item); Assert.Same(rows[count - 2 - ordinal], hit.End.Item);
        Assert.Equal(rows[count - 1 - ordinal].Id, hit.Start.StableId); Assert.Equal(rows[count - 2 - ordinal].Id, hit.End.StableId);
        Assert.Equal(replacement.Points[ordinal].RawPoint, hit.Start.RawPoint);
        Assert.Equal(replacement.Points[ordinal + 1].RawPoint, hit.End.RawPoint);
        Assert.Equal(.37, hit.Fraction, 7);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Trackball_axis_interpolation_uses_original_duplicate_coordinate_edges(bool horizontal)
    {
        var points = Enumerable.Range(0, 4096).Select(i => horizontal ? new ChartPoint(i, 40) : new ChartPoint(40, i)).ToArray();
        var line = new LineSeries { ItemsSource = points, ShowMarkers = false };
        using var host = new SegmentTestHost(horizontal ? new(0, 4096) : new(0, 100), horizontal ? new(0, 100) : new(0, 4096), line);
        host.Chart.TooltipMode = horizontal ? ChartTooltipMode.InterpolateY : ChartTooltipMode.InterpolateX;
        var value = horizontal ? new ChartPoint(2345.4, 40) : new ChartPoint(40, 2345.4);
        host.Move(Context(host.Chart).Projection.Project(value));
        var hit = Assert.Single(host.Chart.HoveredSegments);
        Assert.Equal(2345, hit.Start.Index); Assert.Equal(2346, hit.End.Index);
        Assert.Equal(.4, hit.Fraction, 8); SegmentTestHost.AssertPoint(value, hit.Point);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hierarchy_bounds_remain_conservative_on_inverse_logarithmic_axes(bool inverse)
    {
        var random = new Random(52004);
        var points = Enumerable.Range(0, 1024).Select(i => new ChartPoint(Math.Pow(10, i * 4d / 1023),
            Math.Pow(10, random.NextDouble() * 6))).ToArray();
        var line = new LineSeries { ItemsSource = points, ShowMarkers = false };
        using var host = new SegmentTestHost(new(1, 10_000, inverse, true), new(1, 1_000_000, !inverse, true), line);
        var context = Context(host.Chart); var projected = points.Select(context.Projection.Project).ToArray();
        for (var i = 0; i < 40; i++)
        {
            var query = new Point(host.Chart.PlotArea.Left + random.NextDouble() * host.Chart.PlotArea.Width,
                host.Chart.PlotArea.Top + random.NextDouble() * host.Chart.PlotArea.Height);
            var expected = Oracle(projected, query);
            var actual = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(query, 100_000));
            Assert.Equal(expected.Ordinal, actual.Start.Index); Assert.Equal(expected.Fraction, actual.Fraction, 10);
            Assert.Equal(expected.Distance, actual.DistanceSquared, 7);
        }
    }

    [AvaloniaFact]
    public void Multiaxis_intersections_keep_series_order_and_independent_original_axis_values()
    {
        var first = LineInterpolationTests.CreateLine([new("a", 0, 20), new("b", 10, 80)]);
        var second = LineInterpolationTests.CreateLine([new("c", 0, 200), new("d", 10, 800)]);
        second.YAxisKey = "pressure";
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100 }, TrackballMode = ChartTrackballMode.Vertical,
            TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation };
        chart.Axes.Add(new NumericAxis { Key = "pressure", Minimum = 0, Maximum = 1000, Position = ChartAxisPosition.Far });
        chart.Series.Add(first); chart.Series.Add(second);
        using var host = new TemplateFeaturesHost(chart);
        var center = chart.PlotArea.Center;
        var nearest = Assert.IsType<ChartSegmentHit>(chart.HitTestSegment(center, .01));
        Assert.Same(first, nearest.Series); Assert.Equal(50, nearest.Point.Y, 8);
        SegmentTestHost.RaiseExactMove(chart, center); host.Drain();
        Assert.Equal(2, chart.HoveredSegments.Count);
        var a = chart.HoveredSegments.Single(hit => ReferenceEquals(hit.Series, first));
        var b = chart.HoveredSegments.Single(hit => ReferenceEquals(hit.Series, second));
        Assert.Equal(50, a.Point.Y, 8); Assert.Equal(500, b.Point.Y, 8);
        Assert.Equal(20, a.Start.Point.Y); Assert.Equal(200, b.Start.Point.Y);
        Assert.Equal("a", a.Start.StableId); Assert.Equal("c", b.Start.StableId);
    }

    [AvaloniaFact]
    public void Invalid_logarithmic_node_extrema_do_not_exclude_valid_interior_segments_or_bridge_invalid_vertices()
    {
        var points = Enumerable.Range(0, 1024).Select(i => new ChartPoint(i - 512, 20 + i % 73)).ToArray();
        var line = new LineSeries { ItemsSource = points, ShowMarkers = false };
        using var host = new SegmentTestHost(new(1, 1000, Log: true), new(0, 100), line);
        var context = Context(host.Chart); var projected = points.Select(context.Projection.Project).ToArray();
        foreach (var value in new[] { new ChartPoint(2, 30), new ChartPoint(40, 90), new ChartPoint(200, 10), new ChartPoint(500, 50) })
        {
            var query = context.Projection.Project(value); var expected = Oracle(projected, query);
            var actual = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(query, 100_000));
            Assert.Equal(expected.Ordinal, actual.Start.Index); Assert.Equal(expected.Fraction, actual.Fraction, 10);
            Assert.True(actual.Start.Point.X > 0); Assert.True(actual.End.Point.X > 0);
        }
    }

    [AvaloniaFact]
    public void Segment_coordinate_hierarchy_is_shared_by_metadata_style_overlays_but_not_changed_coordinates()
    {
        var data = new ChartDataSnapshot(0, Enumerable.Range(0, 100).Select(i =>
            new ChartDataPoint(i, null, null, null, new(i, i), false)).ToArray());
        var index = ChartSegmentIndex.For(data);
        Assert.Same(index, ChartSegmentIndex.For(data.WithPointStyles(new ChartPointStyle[data.Count])));
        Assert.Same(index, ChartSegmentIndex.For(data.WithRawPoints()));
        Assert.NotSame(index, ChartSegmentIndex.For(data.WithValues(Enumerable.Range(0, data.Count).Select(i =>
            new ChartDataValue(i, new(i, i * 2), false, false)).ToArray())));
        Assert.Equal(99, index.Count); Assert.InRange(index.EstimatedBytes, 1, 100_000);
    }

    private static (int Ordinal, double Fraction, double Distance) Oracle(Point[] vertices, Point query)
    {
        var best = (Ordinal: -1, Fraction: 0d, Distance: double.PositiveInfinity);
        for (var ordinal = 0; ordinal + 1 < vertices.Length; ordinal++)
        {
            var a = vertices[ordinal]; var b = vertices[ordinal + 1];
            if (!double.IsFinite(a.X) || !double.IsFinite(a.Y) || !double.IsFinite(b.X) || !double.IsFinite(b.Y)) continue;
            var dx = b.X - a.X; var dy = b.Y - a.Y; var norm = dx * dx + dy * dy;
            var fraction = norm == 0 ? 0 : Math.Clamp(((query.X - a.X) * dx + (query.Y - a.Y) * dy) / norm, 0, 1);
            var x = fraction == 0 ? a.X : fraction == 1 ? b.X : Math.FusedMultiplyAdd(dx, fraction, a.X);
            var y = fraction == 0 ? a.Y : fraction == 1 ? b.Y : Math.FusedMultiplyAdd(dy, fraction, a.Y);
            var distance = (x - query.X) * (x - query.X) + (y - query.Y) * (y - query.Y);
            if (distance < best.Distance) best = (ordinal, fraction, distance);
        }
        return best;
    }
    private static long Counter(CartesianChart chart, string name) => (long)typeof(CartesianChart)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
    private static ChartSeriesRenderContext Context(CartesianChart chart)
    {
        typeof(CartesianChart).GetMethod("GetScene", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(chart, null);
        var scene = typeof(CartesianChart).GetField("_scene", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
        var snapshots = (Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!;
        return (ChartSeriesRenderContext)snapshots.GetValue(0)!.GetType().GetProperty("Context")!.GetValue(snapshots.GetValue(0))!;
    }
}

[CollectionDefinition("Segment index diagnostics", DisableParallelization = true)]
public sealed class SegmentIndexDiagnosticsCollection;
