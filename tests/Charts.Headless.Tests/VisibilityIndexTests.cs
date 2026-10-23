using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

[CollectionDefinition("Visibility index diagnostics", DisableParallelization = true)]
public sealed class VisibilityIndexDiagnosticCollection;

[Collection("Visibility index diagnostics")]
public sealed class VisibilityIndexTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Point_queries_match_an_independent_inclusive_source_scan(int order)
    {
        var random = new Random(1707);
        var points = Enumerable.Range(0, 8193).Select(i => new ChartPoint(i / 3, random.Next(-1000, 1001))).ToArray();
        if (order == 1) Array.Reverse(points);
        if (order == 2) random.Shuffle(points);
        if (order == 3)
            for (var i = 0; i < points.Length; i += 29) points[i] = new(double.NaN, i);
        var data = Snapshot(points);
        var index = ChartVisibilityIndex.For(data);
        for (var window = 0; window < 48; window++)
        {
            var minimumX = random.Next(-200, 2800); var minimumY = random.Next(-1200, 1000);
            var bounds = new ChartVisibilityBounds(minimumX, minimumX + random.Next(0, 500), minimumY, minimumY + random.Next(0, 500));
            var expected = Enumerable.Range(0, points.Length).Where(i => Finite(points[i]) && bounds.Contains(points[i])).ToArray();
            Assert.Equal(expected, index.QueryPoints(bounds).ToArray());
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Segment_queries_never_drop_a_crossing_edge_or_its_tangent_neighbors(int order)
    {
        var random = new Random(9941);
        var points = Enumerable.Range(0, 8193).Select(i => new ChartPoint(i, random.Next(-500, 501))).ToArray();
        if (order == 1) Array.Reverse(points);
        if (order == 2) random.Shuffle(points);
        if (order == 3)
            for (var i = 0; i < points.Length; i += 31) points[i] = new(i, double.NaN);
        var index = ChartVisibilityIndex.For(Snapshot(points));
        for (var window = 0; window < 24; window++)
        {
            var minimumX = random.Next(0, 8000); var minimumY = random.Next(-600, 500);
            var bounds = new ChartVisibilityBounds(minimumX, minimumX + random.Next(0, 200), minimumY, minimumY + random.Next(0, 200));
            for (var neighbors = 0; neighbors <= 1; neighbors++)
            {
                var actual = index.QuerySegments(bounds, neighbors).ToArray();
                Assert.Equal(actual.Distinct().Order(), actual);
                var actualSet = actual.ToHashSet();
                for (var edge = 0; edge + 1 < points.Length; edge++)
                {
                    var a = points[edge]; var b = points[edge + 1];
                    if (!Finite(a) || !Finite(b) || !bounds.Intersects(Math.Min(a.X, b.X), Math.Max(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y))) continue;
                    for (var ordinal = Math.Max(0, edge - neighbors); ordinal <= Math.Min(points.Length - 1, edge + 1 + neighbors); ordinal++)
                        Assert.Contains(ordinal, actualSet);
                }
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Ordered_queries_keep_duplicate_boundary_values_and_offscreen_crossing_endpoints(bool descending)
    {
        ChartPoint[] points = [new(-100, 1), new(-10, 2), new(0, 3), new(0, 4), new(10, 5), new(100, 6)];
        if (descending) Array.Reverse(points);
        var index = ChartVisibilityIndex.For(Snapshot(points));
        Assert.Equal(new[] { 2, 3 }, index.QueryPoints(new(0, 0, double.NegativeInfinity, double.PositiveInfinity)).ToArray());
        var between = index.QuerySegments(new(1, 2, double.NegativeInfinity, double.PositiveInfinity)).ToArray();
        Assert.Equal(descending ? new[] { 1, 2 } : new[] { 3, 4 }, between);
        Assert.Equal(descending ? new[] { 0, 1, 2, 3 } : new[] { 2, 3, 4, 5 },
            index.QuerySegments(new(1, 2, double.NegativeInfinity, double.PositiveInfinity), 1).ToArray());
    }

    [Fact]
    public void Unordered_crossings_include_endpoints_even_when_no_source_point_is_inside()
    {
        ChartPoint[] points = [new(100, 100), new(-100, -100), new(double.NaN, 0), new(-100, 100), new(100, -100), new(200, 200)];
        var index = ChartVisibilityIndex.For(Snapshot(points));
        var bounds = new ChartVisibilityBounds(-1, 1, -1, 1);
        Assert.Empty(index.QueryPoints(bounds));
        Assert.Equal(new[] { 0, 1, 3, 4 }, index.QuerySegments(bounds).ToArray());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Spatial_tree_handles_extreme_finite_coordinates_without_overflow(bool segments)
    {
        var points = Enumerable.Range(0, 257).Select(i => new ChartPoint(i % 2 == 0 ? double.MaxValue : -double.MaxValue,
            i % 3 == 0 ? double.MaxValue : -double.MaxValue)).ToArray();
        var index = ChartVisibilityIndex.For(Snapshot(points));
        var bounds = new ChartVisibilityBounds(-1, 1, -1, 1);
        if (segments)
        {
            var actual = index.QuerySegments(bounds).ToHashSet();
            for (var i = 0; i + 1 < points.Length; i++)
                if (points[i].X != points[i + 1].X && points[i].Y != points[i + 1].Y)
                { Assert.Contains(i, actual); Assert.Contains(i + 1, actual); }
        }
        else Assert.Empty(index.QueryPoints(bounds));
    }

    [Fact]
    public void Queries_preserve_original_metadata_nonordinal_indices_and_finite_empty_points()
    {
        var rows = Enumerable.Range(0, 128).Select(i => new object()).ToArray();
        var source = rows.Select((row, i) => new ChartDataPoint(1000 + i * 3, $"id:{i}", row, $"category:{i}", new((i * 17) % 128, i), i == 17)
            { RawPoint = new(i + .25, i + .5), High = i + 1, Low = i - 1 }).ToArray();
        var data = ChartDataSnapshot.FromOwned(7, source);
        var bounds = new ChartVisibilityBounds(0, 80, 0, 127);
        var selected = ChartVisibilityIndex.For(data).QueryPoints(bounds);
        Assert.Contains(17, selected);
        foreach (var ordinal in selected)
        {
            var point = data.Points[ordinal];
            Assert.Equal(1000 + ordinal * 3, point.SourceIndex);
            Assert.Same(rows[ordinal], point.Item);
            Assert.Equal($"id:{ordinal}", point.StableId);
            Assert.Equal(new ChartPoint(ordinal + .25, ordinal + .5), point.RawPoint);
            Assert.Equal(source[ordinal], point);
        }
        Assert.Equal(128, data.Points.Count);
        Assert.Equal(source, data.Points);
    }

    [Fact]
    public void Paint_and_raw_metadata_overlays_reuse_indexes_but_coordinate_changes_replace_them()
    {
        var data = Snapshot(Enumerable.Range(0, 128).Select(i => new ChartPoint(i, i)).ToArray());
        var index = ChartVisibilityIndex.For(data);
        Assert.Same(index, ChartVisibilityIndex.For(data.WithRawPoints()));
        Assert.Same(index, ChartVisibilityIndex.For(data.WithPointStyles(Enumerable.Repeat(new ChartPointStyle(12, null), 128).ToArray())));
        var transformed = data.WithValues(Enumerable.Range(0, 128).Select(i => new ChartDataValue(i, new(i * 2, i), false, false)).ToArray());
        Assert.NotSame(index, ChartVisibilityIndex.For(transformed));
        Assert.Equal(new[] { 5 }, ChartVisibilityIndex.For(transformed).QueryPoints(new(10, 10, 5, 5)).ToArray());
    }

    [Fact]
    public void Narrow_ordered_X_range_uses_logarithmic_work_and_no_candidate_array()
    {
        var data = Snapshot(Enumerable.Range(0, 262_144).Select(i => new ChartPoint(i, i % 97)).ToArray());
        var index = ChartVisibilityIndex.For(data);
        var bounds = new ChartVisibilityBounds(100_000, 100_031, double.NegativeInfinity, double.PositiveInfinity);
        index.QueryPoints(bounds);
        var builds = ChartVisibilityIndex.PointBuildCount;
        var visited = ChartVisibilityIndex.VisitedCount;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var result = index.QueryPoints(bounds);
        var allocation = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(32, result.Count);
        Assert.Equal(100_000, result[0]); Assert.Equal(100_031, result[^1]);
        Assert.InRange(ChartVisibilityIndex.VisitedCount - visited, 1, 40);
        Assert.Equal(builds, ChartVisibilityIndex.PointBuildCount);
        Assert.Equal(0, allocation);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Two_dimensional_and_unordered_windows_prune_work_and_keep_warm_allocations_proportional_to_matches(bool shuffle)
    {
        var points = Enumerable.Range(0, 512 * 512).Select(i => new ChartPoint(i / 512, i % 512)).ToArray();
        if (shuffle) new Random(51).Shuffle(points);
        var index = ChartVisibilityIndex.For(Snapshot(points));
        // Full X is intentional: a sorted-X-only filter would still visit all 262144 rows.
        var bounds = new ChartVisibilityBounds(double.NegativeInfinity, double.PositiveInfinity, 250, 250);
        var expected = Enumerable.Range(0, points.Length).Where(i => points[i].Y == 250).ToArray();
        Assert.Equal(expected, index.QueryPoints(bounds).ToArray());
        var builds = ChartVisibilityIndex.PointBuildCount;
        var visited = ChartVisibilityIndex.VisitedCount;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var actual = index.QueryPoints(bounds);
        var allocation = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(expected, actual.ToArray());
        Assert.InRange(ChartVisibilityIndex.VisitedCount - visited, 1, 16_384);
        Assert.Equal(builds, ChartVisibilityIndex.PointBuildCount);
        Assert.InRange(allocation, 0, 16_384);
    }

    [Fact]
    public void Sparse_unordered_local_segments_reuse_the_spatial_tree_without_a_source_sized_candidate_array()
    {
        // Each source pair is local in space, but the physical pair order is deliberately unordered.
        var random = new Random(721);
        var clusters = Enumerable.Range(0, 32_768).ToArray(); random.Shuffle(clusters);
        var points = clusters.SelectMany(i => new[] { new ChartPoint(i % 256 * 10, i / 256 * 10),
            new ChartPoint(i % 256 * 10 + 1, i / 256 * 10 + 1), new ChartPoint(double.NaN, double.NaN) }).ToArray();
        var index = ChartVisibilityIndex.For(Snapshot(points));
        var bounds = new ChartVisibilityBounds(100, 111, 100, 111);
        var expected = index.QuerySegments(bounds).ToArray();
        var builds = ChartVisibilityIndex.SegmentBuildCount;
        var visited = ChartVisibilityIndex.VisitedCount;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var actual = index.QuerySegments(bounds);
        var allocation = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(8, actual.Count);
        Assert.Equal(expected, actual.ToArray());
        Assert.Equal(builds, ChartVisibilityIndex.SegmentBuildCount);
        Assert.InRange(ChartVisibilityIndex.VisitedCount - visited, 1, 1024);
        Assert.InRange(allocation, 0, 8192);
    }

    [Fact]
    public void Invalid_bounds_fall_back_conservatively_and_full_ranges_enumerate_without_copying()
    {
        var index = ChartVisibilityIndex.For(Snapshot([new(0, 0), new(1, 1), new(double.NaN, 2)]));
        Assert.True(index.QueryPoints(new(double.NaN, 1, 0, 1)).IsFull);
        Assert.Equal(new[] { 0, 1, 2 }, index.QuerySegments(new(2, 1, 0, 1)).ToArray());
        Assert.Empty(index.QueryPoints(new(2, 3, 2, 3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.QuerySegments(new(0, 1, 0, 1), -1));
        var range = ChartVisibleOrdinals.All(3);
        Assert.True(range.IsFull); Assert.Equal(new[] { 0, 1, 2 }, range.ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => range[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => range[3]);
    }

    private static bool Finite(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static ChartDataSnapshot Snapshot(ChartPoint[] points) => ChartDataSnapshot.FromOwned(1,
        points.Select((point, i) => new ChartDataPoint(i, $"id:{i}", null, null, point, !Finite(point))).ToArray());
}
