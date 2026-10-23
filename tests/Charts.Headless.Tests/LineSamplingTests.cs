using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

[CollectionDefinition("Line sampling diagnostics", DisableParallelization = true)]
public sealed class LineSamplingDiagnosticCollection;

[Collection("Line sampling diagnostics")]
public sealed class LineSamplingTests
{
    [Theory]
    [InlineData(false, false, 1, 1d)] [InlineData(true, false, 1, 1d)]
    [InlineData(false, true, 1, 1d)] [InlineData(true, true, 1, 1d)]
    [InlineData(false, false, 1, 2d)] [InlineData(true, true, 3, 1.5d)]
    [InlineData(false, false, 64, 2d)]
    public void Sampling_matches_an_independent_bucket_scan_with_duplicates_gaps_and_reversed_axes(
        bool descending, bool inverse, int pixelWidth, double scale)
    {
        var random = new Random(8221);
        var points = Enumerable.Range(0, 16_417).Select(i => new ChartPoint(i / 4, random.Next(-500, 501))).ToArray();
        for (var i = 137; i < points.Length; i += 613) points[i] = new(i, double.NaN);
        if (descending) Array.Reverse(points);
        Func<double, double> map = inverse ? x => 603.75 - x / 7 : x => 16.25 + x / 7;
        Verify(points, map, 67.125, 397.875, scale, pixelWidth);
        Verify(points, map, -100, 1000, scale, pixelWidth);
        Verify(points, map, 198.25, 198.25, scale, pixelWidth);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Logarithmic_buckets_use_actual_forward_mapping_without_inverse_rounding(bool inverse)
    {
        var points = Enumerable.Range(0, 25_001).Select(i => new ChartPoint(Math.Pow(10, -250 + i / 50d),
            i % 53 == 0 ? 600 : i % 67 == 0 ? -700 : Math.Sin(i * .13))).ToArray();
        Func<double, double> map = inverse ? x => 350 - Math.Log10(x) : x => 350 + Math.Log10(x);
        Verify(points, map, 100.123, 581.37, 2, 1);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void A_crossing_edge_is_kept_when_no_source_center_is_inside_the_view(bool descending)
    {
        ChartPoint[] points = [new(-100, 1), new(-10, 20), new(10, -20), new(100, 3)];
        if (descending) Array.Reverse(points);
        var result = Query(points, x => x, -1, 1);
        Assert.Equal(new[] { 1, 2 }, result.ToArray());
        Assert.Equal(0, result.RawVisibleCount); Assert.Equal(2, result.InputCount);
        Assert.Equal(0, result.BucketCount); Assert.Equal(1, result.FigureCount);
        Assert.True(result.StartsFigure(0)); Assert.False(result.StartsFigure(1));
    }

    [Fact]
    public void Peaks_valleys_and_first_last_rows_survive_in_source_order_in_one_column()
    {
        var points = Enumerable.Range(0, 10_000).Select(i => new ChartPoint(i / 20_000d, 0)).ToArray();
        points[4231] = points[4231] with { Y = 10_000 };
        points[7231] = points[7231] with { Y = -10_000 };
        var result = Query(points, x => x, 0, 1);
        Assert.Equal(new[] { 0, 4231, 7231, 9999 }, result.ToArray());
        Assert.Equal(10_000, result.RawVisibleCount); Assert.Equal(10_000, result.InputCount);
        Assert.Equal(1, result.BucketCount); Assert.True(result.IsReduced);
    }

    [Fact]
    public void Every_original_gap_keeps_separate_topology_even_when_figure_endpoints_share_a_pixel()
    {
        ChartPoint[] points = [new(0, 1), new(.1, 3), new(.2, -3), new(double.NaN, 0),
            new(.3, 5), new(.4, -5), new(.5, 2), new(.6, double.PositiveInfinity), new(.7, 9)];
        var result = Query(points, x => x, 0, 1);
        Assert.Equal(new[] { 0, 1, 2, 4, 5, 6, 8 }, result.ToArray());
        Assert.Equal(new[] { 0, 3, 6 }, Enumerable.Range(0, result.Count).Where(result.StartsFigure).ToArray());
        Assert.Equal(3, result.FigureCount); Assert.Equal(3, result.BucketCount);
    }

    [Fact]
    public void Duplicate_X_columns_keep_distinct_extrema_and_stable_first_occurrence_ties()
    {
        var points = Enumerable.Range(0, 4096).Select(i => new ChartPoint(17, 5)).ToArray();
        points[13] = new(17, -5); points[4000] = new(17, -5);
        var result = Query(points, x => x, 16, 18);
        Assert.Equal(new[] { 0, 13, 4095 }, result.ToArray());
        Assert.Equal(1, result.BucketCount);
    }

    [Fact]
    public void Finite_published_empty_points_match_legacy_line_semantics_and_original_metadata()
    {
        var rows = Enumerable.Range(0, 4096).Select(_ => new object()).ToArray();
        var source = rows.Select((item, i) => new ChartDataPoint(2000 + i * 3, $"id:{i}", item, $"cat:{i}",
            new(i / 10_000d, i == 151 ? 500 : i == 254 ? -500 : 0), i == 151)
            { RawPoint = new(i, i + .25), High = i + 7, Low = i - 7 }).ToArray();
        var data = ChartDataSnapshot.FromOwned(7, source);
        Assert.True(ChartSamplingIndex.For(data, TestContext.Current.CancellationToken).TryQuery(x => x, 0, 1, 1, 1, out var result, TestContext.Current.CancellationToken));
        Assert.Equal(new[] { 0, 151, 254, 4095 }, result.ToArray());
        Assert.Equal(7, result.SourceVersion); Assert.Equal(source.Length, result.SourceCount);
        foreach (var ordinal in result)
        {
            var point = data.Points[ordinal];
            Assert.Equal(source[ordinal], point); Assert.Same(rows[ordinal], point.Item);
            Assert.True(data.TryGetPoint(2000 + ordinal * 3, out var bySource)); Assert.Equal(point, bySource);
        }
        Assert.Equal(source, data.Points); Assert.Equal(4096, data.Count);
    }

    [Fact]
    public void Metadata_overlays_reuse_hierarchy_but_new_revisions_and_coordinate_overlays_do_not()
    {
        var data = Snapshot(Enumerable.Range(0, 4096).Select(i => new ChartPoint(i, i)).ToArray());
        var index = ChartSamplingIndex.For(data, TestContext.Current.CancellationToken);
        Assert.Same(index, ChartSamplingIndex.For(data.WithRawPoints(), TestContext.Current.CancellationToken));
        Assert.Same(index, ChartSamplingIndex.For(data.WithPointStyles(Enumerable.Repeat(new ChartPointStyle(12, Brushes.Red), data.Count).ToArray()), TestContext.Current.CancellationToken));
        var changed = data.WithValues(Enumerable.Range(0, data.Count).Select(i => new ChartDataValue(i, new(i, -i), false, false)).ToArray());
        Assert.NotSame(index, ChartSamplingIndex.For(changed, TestContext.Current.CancellationToken));
        Assert.NotSame(index, ChartSamplingIndex.For(new ChartDataSnapshot(2, data.Points), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Million_point_warm_queries_have_bounded_work_and_reuse_the_numeric_hierarchy()
    {
        var points = Enumerable.Range(0, 1_000_000).Select(i => new ChartPoint(i,
            i % 991 == 0 ? 5000 : i % 997 == 0 ? -5000 : Math.Sin(i * .01))).ToArray();
        var index = ChartSamplingIndex.For(Snapshot(points), TestContext.Current.CancellationToken);
        Assert.True(index.TryQuery(x => x / 1000, 0, 1000, 1, 1, out _, TestContext.Current.CancellationToken));
        var builds = ChartSamplingIndex.BuildCount; var visited = ChartSamplingIndex.VisitedCount;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(index.TryQuery(x => x / 1000, 0, 1000, 1, 1, out var result, TestContext.Current.CancellationToken));
        var allocation = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(builds, ChartSamplingIndex.BuildCount);
        Assert.InRange(ChartSamplingIndex.VisitedCount - visited, 1, 100_000);
        Assert.InRange(result.Count, 1000, 4000); Assert.Equal(1000, result.BucketCount);
        Assert.InRange(index.EstimatedBytes, 1, 1_000_000);
        Assert.InRange(allocation, 0, 200_000);
        Assert.Equal(points.Length, result.RawVisibleCount); Assert.True(result.IsReduced);
    }

    [Fact]
    public void Sparse_views_visit_occupied_columns_without_iterating_the_empty_pixel_budget()
    {
        ChartPoint[] points = [new(0, 0), new(500_000, 3), new(999_999, -3)];
        var index = ChartSamplingIndex.For(Snapshot(points), TestContext.Current.CancellationToken);
        var visited = ChartSamplingIndex.VisitedCount;
        Assert.True(index.TryQuery(x => x, 0, 999_999, 1, 1, out var result, TestContext.Current.CancellationToken));
        Assert.Equal(1_000_000, result.BucketBudget); Assert.Equal(3, result.BucketCount);
        Assert.InRange(ChartSamplingIndex.VisitedCount - visited, 1, 32);
        Assert.Equal(new[] { 0, 1, 2 }, result.ToArray());
    }

    [Fact]
    public void Unsupported_ranges_and_nonmonotonic_X_request_a_conservative_fallback()
    {
        var index = ChartSamplingIndex.For(Snapshot([new(0, 0), new(2, 2), new(1, 1)]), TestContext.Current.CancellationToken);
        Assert.False(index.IsMonotonicX); Assert.False(index.TryQuery(x => x, 0, 2, 1, 1, out _, TestContext.Current.CancellationToken));
        index = ChartSamplingIndex.For(Snapshot([new(0, 0), new(1, 1)]), TestContext.Current.CancellationToken);
        foreach (var (left, right, scale, pixels) in new[] { (1d, 0d, 1d, 1), (0d, 1d, 0d, 1),
            (double.NaN, 1d, 1d, 1), (0d, double.PositiveInfinity, 1d, 1), (0d, 1d, 1d, 0),
            (0d, 1d, 1d, 65), (0d, 1_000_000d, 1d, 1) })
            Assert.False(index.TryQuery(x => x, left, right, scale, pixels, out _, TestContext.Current.CancellationToken));
        Assert.False(index.TryQuery(_ => double.NaN, 0, 1, 1, 1, out _, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Empty_all_gap_and_fully_offscreen_data_publish_empty_results()
    {
        foreach (var points in new ChartPoint[][] { [], [new(double.NaN, 0), new(1, double.NaN)], [new(10, 10), new(20, 20)] })
        {
            var result = Query(points, x => x, 0, 1);
            Assert.Empty(result); Assert.Equal(0, result.FigureCount);
            Assert.Equal(0, result.InputCount); Assert.Equal(0, result.RawVisibleCount);
        }
    }

    [Fact]
    public void Cancellation_during_build_does_not_publish_a_partial_hierarchy()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var values = Enumerable.Range(0, 16_384).Select(i => new ChartPoint(i, i % 53)).ToArray();
        var source = new InstrumentedSource(values) { OnRead = count => { if (count == 1024) cancellation.Cancel(); } };
        var data = ChartDataSnapshot.FromCompact(8, source);
        var builds = ChartSamplingIndex.BuildCount;
        Assert.Throws<OperationCanceledException>(() => ChartSamplingIndex.For(data, cancellation.Token));
        Assert.Equal(builds, ChartSamplingIndex.BuildCount);
        source.OnRead = null;
        var index = ChartSamplingIndex.For(data, TestContext.Current.CancellationToken);
        Assert.Equal(builds + 1, ChartSamplingIndex.BuildCount);
        Assert.True(index.TryQuery(x => x / 100, 0, 164, 1, 1, out var result, TestContext.Current.CancellationToken));
        Assert.Equal(16_384, result.SourceCount); Assert.Equal(1, result.FigureCount);
        Assert.Same(index, ChartSamplingIndex.For(data, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Cancellation_during_query_does_not_publish_output_or_success_counters()
    {
        var index = ChartSamplingIndex.For(Snapshot(Enumerable.Range(0, 65_536).Select(i => new ChartPoint(i, i % 97)).ToArray()), TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var reads = 0; var queries = ChartSamplingIndex.QueryCount; var outputs = ChartSamplingIndex.OutputCount;
        Assert.Throws<OperationCanceledException>(() => index.TryQuery(x =>
        { if (++reads == 100) cancellation.Cancel(); return x / 100; }, 0, 655, 1, 1, out _, cancellation.Token));
        Assert.Equal(queries, ChartSamplingIndex.QueryCount); Assert.Equal(outputs, ChartSamplingIndex.OutputCount);
        Assert.True(index.TryQuery(x => x / 100, 0, 655, 1, 1, out var result, TestContext.Current.CancellationToken));
        Assert.NotEmpty(result);
    }

    private static ChartLineSampling Query(ChartPoint[] points, Func<double, double> map, double left, double right)
    {
        Assert.True(ChartSamplingIndex.For(Snapshot(points), TestContext.Current.CancellationToken).TryQuery(map, left, right, 1, 1, out var result, TestContext.Current.CancellationToken)); return result;
    }

    private static void Verify(ChartPoint[] points, Func<double, double> map, double left, double right, double scale, int pixelWidth)
    {
        Assert.True(ChartSamplingIndex.For(Snapshot(points), TestContext.Current.CancellationToken).TryQuery(map, left, right, scale, pixelWidth, out var actual, TestContext.Current.CancellationToken));
        var expected = new List<int>(); var starts = new List<int>();
        var visible = 0; var input = 0; var buckets = 0;
        for (var start = 0; start < points.Length;)
        {
            if (!Finite(points[start])) { start++; continue; }
            var end = start + 1; while (end < points.Length && Finite(points[end])) end++;
            var inside = Enumerable.Range(start, end - start).Where(i => map(points[i].X) >= left && map(points[i].X) <= right).ToArray();
            var kept = new SortedSet<int>();
            if (inside.Length > 0)
            {
                visible += inside.Length;
                var inputStart = Math.Max(start, inside[0] - 1); var inputEnd = Math.Min(end, inside[^1] + 2);
                input += inputEnd - inputStart;
                if (inputStart < inside[0]) kept.Add(inputStart);
                if (inputEnd > inside[^1] + 1) kept.Add(inputEnd - 1);
                foreach (var bucket in inside.GroupBy(i => Math.Floor((map(points[i].X) - left) * scale / pixelWidth)))
                {
                    kept.Add(bucket.First()); kept.Add(bucket.Last());
                    kept.Add(bucket.MinBy(i => points[i].Y)); kept.Add(bucket.MaxBy(i => points[i].Y)); buckets++;
                }
            }
            else
            {
                for (var edge = start; edge + 1 < end; edge++)
                    if (Math.Min(map(points[edge].X), map(points[edge + 1].X)) <= right &&
                        Math.Max(map(points[edge].X), map(points[edge + 1].X)) >= left)
                    { kept.Add(edge); kept.Add(edge + 1); }
                input += kept.Count;
            }
            if (kept.Count > 0) { starts.Add(expected.Count); expected.AddRange(kept); }
            start = end;
        }
        Assert.Equal(expected, actual.ToArray());
        Assert.Equal(starts, Enumerable.Range(0, actual.Count).Where(actual.StartsFigure));
        Assert.Equal(visible, actual.RawVisibleCount); Assert.Equal(input, actual.InputCount);
        Assert.Equal(buckets, actual.BucketCount); Assert.Equal(starts.Count, actual.FigureCount);
        Assert.Equal(actual.Count < input, actual.IsReduced);
        Assert.True(actual.Count <= 4 * actual.BucketCount + 2 * actual.FigureCount);
    }

    private static bool Finite(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static ChartDataSnapshot Snapshot(ChartPoint[] points) => ChartDataSnapshot.FromOwned(1,
        points.Select((point, i) => new ChartDataPoint(i, null, null, null, point, !Finite(point))).ToArray());

    private sealed class InstrumentedSource(ChartPoint[] points) : IChartSnapshotData
    {
        private int _reads;
        internal Action<int>? OnRead { get; set; }
        public int Count => points.Length;
        public bool HasOrdinalSourceIndices => true;
        public ReadOnlyMemory<ChartPoint> Coordinates => points;
        public ChartDataValue GetValue(int index)
        { OnRead?.Invoke(++_reads); return new(index, points[index], false, false); }
        public ChartDataPoint GetPoint(int index) => new(index, null, null, null, points[index], false);
        public bool TryFindIdentity(object identity, bool valueEquality, out int ordinal) { ordinal = -1; return false; }
    }
}
