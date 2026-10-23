using System.Runtime.CompilerServices;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class DataExtremaCacheTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bounds_are_constant_size_and_do_not_build_or_sort_a_hit_index(bool compact)
    {
        var source = new CountingData([new(4, -8), new(1, 3), new(3, 9), new(2, 2), new(double.NaN, 100)], compact);
        var data = ChartDataSnapshot.FromCompact(3, source);

        var extrema = ChartDataExtrema.For(data);

        Assert.Equal(new ChartSeriesBounds(1, 4, -8, 9), extrema.Bounds);
        Assert.Equal(4, extrema.Count); Assert.Equal(5, extrema.SourceCount);
        Assert.False(extrema.AscendingX); Assert.False(extrema.DescendingX);
        Assert.False(ChartDataIndex.TryGetExisting(data, out _));
        Assert.False(ChartReadingIndex.TryGetExisting(data, out _));
        Assert.Equal(compact ? 0 : 5, source.ValueReads);
        Assert.Equal(0, source.MetadataReads);
        Assert.Same(extrema, ChartDataExtrema.For(data));
        Assert.Equal(compact ? 0 : 5, source.ValueReads);

        var index = ChartDataIndex.For(data);
        Assert.Equal(new[] { 1, 3, 2, 0 }, Enumerable.Range(0, index.Count).Select(index.Ordinal));
        Assert.Equal(extrema.Bounds, index.Bounds);
        Assert.Equal(compact ? 0 : 10, source.ValueReads);
        Assert.Equal(1, source.CountReads);
    }

    [Fact]
    public void Monotonic_x_and_y_indexes_reuse_one_extrema_pass()
    {
        var source = new CountingData([new(1, 30), new(2, 20), new(3, 10)], false);
        var data = ChartDataSnapshot.FromCompact(1, source);
        var extrema = ChartDataExtrema.For(data);
        Assert.True(extrema.AscendingX); Assert.True(extrema.DescendingY);
        var x = ChartDataIndex.For(data); var y = ChartReadingIndex.For(data);
        Assert.Equal(new[] { 0, 1, 2 }, Enumerable.Range(0, x.Count).Select(x.Ordinal));
        Assert.Equal(new[] { 2, 1, 0 }, Enumerable.Range(0, y.Count).Select(y.Ordinal));
        Assert.Equal(3, source.ValueReads); Assert.Equal(1, source.CountReads); Assert.Equal(0, source.MetadataReads);
    }

    [Fact]
    public void Marker_style_overlays_share_numeric_and_band_caches_without_sharing_paint()
    {
        var data = new ChartDataSnapshot(4, new ChartDataPoint[]
        {
            new(0, "first", null, null, new(3, 7), false),
            new(1, "second", null, null, new(1, 8), false),
        });
        var band = data.WithBandValues([new(0, new(3, 7), false, false), new(1, new(1, 8), false, false)],
            [new(-3, null, new(3, 7)), new(2, null, new(1, 8))]);
        var first = band.WithPointStyles([new(5, null), new(7, null)]);
        var second = first.WithPointStyles([new(11, null), new(13, null)]);

        Assert.Same(band, first.CoordinateCacheOwner); Assert.Same(band, second.CoordinateCacheOwner);
        Assert.Same(ChartDataExtrema.For(band), ChartDataExtrema.For(second));
        Assert.Same(ChartDataIndex.For(band), ChartDataIndex.For(second));
        Assert.Same(ChartReadingIndex.For(band), ChartReadingIndex.For(second));
        Assert.Same(ChartLowerExtrema.For(band), ChartLowerExtrema.For(second));
        Assert.Equal(5, first.Points[0].MarkerDiameter); Assert.Equal(11, second.Points[0].MarkerDiameter);
        Assert.Null(band.Points[0].MarkerDiameter);
        Assert.Equal(-3, second.Points[0].BaseValue); Assert.Equal("first", second.Points[0].StableId);
    }

    [Fact]
    public void Coordinate_and_band_changes_get_fresh_extrema_even_with_the_same_version()
    {
        var data = new ChartDataSnapshot(4, new ChartDataPoint[]
        { new(0, null, null, null, new(1, 5), false), new(1, null, null, null, new(2, 8), false) });
        var original = ChartDataExtrema.For(data);
        var styled = data.WithPointStyles([new(8, null), new(9, null)]);
        var projected = styled.WithBandValues([new(0, new(10, 50), false, false), new(1, new(20, 80), false, false)],
            [new(-5, null, new(1, 5)), new(-8, null, new(2, 8))]);

        Assert.NotSame(original, ChartDataExtrema.For(projected));
        Assert.Equal(new ChartSeriesBounds(10, 20, 50, 80), ChartDataExtrema.For(projected).Bounds);
        Assert.Equal(new ChartSeriesBounds(1, 2, 5, 8), original.Bounds);
        Assert.Equal(new ChartValueBounds(-8, -5), ChartLowerExtrema.For(projected).Bounds);
        Assert.Null(ChartLowerExtrema.For(styled).Bounds);
        Assert.Equal(8, projected.Points[0].MarkerDiameter);
    }

    [Fact]
    public void Raw_point_metadata_capture_is_lazy_shares_numeric_caches_and_keeps_capture_order()
    {
        var data = new ChartDataSnapshot(4, new ChartDataPoint[]
        { new(0, "identity", null, null, new(1, 5), false) { RawPoint = new(999, 999) } });
        var firstProjection = data.WithValues([new(0, new(10, 50), false, false)]);
        var extrema = ChartDataExtrema.For(firstProjection);
        var raw = firstProjection.WithRawPoints();
        Assert.Same(extrema, ChartDataExtrema.For(raw));
        Assert.Equal(new ChartPoint(10, 50), raw.Points[0].RawPoint);
        var displayed = raw.WithValues([new(0, new(20, 80), false, false)]).WithPointStyles([new(12, null)]);
        Assert.Equal(new ChartPoint(20, 80), displayed.Points[0].Point);
        Assert.Equal(new ChartPoint(10, 50), displayed.Points[0].RawPoint);
        Assert.Equal(new ChartPoint(999, 999), data.Points[0].RawPoint);
        var band = raw.WithBandValues([new(0, new(20, 80), false, false)], [new(7, null, new(3, 4))]);
        Assert.Equal(new ChartPoint(3, 4), band.Points[0].RawPoint);
    }

    [Fact]
    public void Capturing_raw_points_does_not_visit_a_large_snapshot()
    {
        var source = new CountingData(Enumerable.Range(0, 100_000).Select(i => new ChartPoint(i, i)).ToArray(), false);
        var data = ChartDataSnapshot.FromCompact(1, source);
        var raw = data.WithRawPoints();
        Assert.Same(data, raw.CoordinateCacheOwner);
        Assert.Equal(0, source.CountReads); Assert.Equal(0, source.ValueReads); Assert.Equal(0, source.MetadataReads);
    }

    [Fact]
    public void Lower_extrema_cache_all_builtin_domains_and_apply_current_baseline()
    {
        ChartDataValue[] values = Enumerable.Range(0, 7).Select(i => new ChartDataValue(i, new(i, 10), i == 6, false)).ToArray();
        var data = new ChartDataSnapshot(2, values.Select(v => new ChartDataPoint(v.SourceIndex, null, null, null, v.Point, v.IsEmpty)).ToArray())
            .WithBandValues(values, [new(-100, null, default), new(4, null, default), new(12, null, default),
                new(DateTimeAxis.MaximumUnixMilliseconds + 1, null, default), new(double.NaN, null, default),
                new(double.NegativeInfinity, null, default), new(-999, null, default)]);
        var lower = ChartLowerExtrema.For(data);
        Assert.Equal(new ChartValueBounds(-100, DateTimeAxis.MaximumUnixMilliseconds + 1), lower.ForAxis(new NumericAxis()));
        Assert.Equal(new ChartValueBounds(4, DateTimeAxis.MaximumUnixMilliseconds + 1), lower.ForAxis(new LogarithmicAxis()));
        Assert.Equal(new ChartValueBounds(-100, 12), lower.ForAxis(new DateTimeAxis()));
        Assert.Same(lower, ChartLowerExtrema.For(data));
        // A band supplied NaN/Infinity is not a missing band and cannot acquire a fallback.
        Assert.Equal(lower.Bounds, lower.BoundsWithFallback(-500));

        var raw = new ChartDataSnapshot(2, new ChartDataPoint[] { new(0, null, null, null, new(1, 4), false) });
        var rawLower = ChartLowerExtrema.For(raw);
        Assert.Equal(new ChartValueBounds(-5, -5), rawLower.BoundsWithFallback(-5));
        Assert.Equal(new ChartValueBounds(50, 50), rawLower.BoundsWithFallback(50));
        Assert.Null(rawLower.Bounds);
    }

    [Fact]
    public void Source_low_fallback_does_not_override_explicit_band_or_empty_row()
    {
        var data = new ChartDataSnapshot(1, new ChartDataPoint[]
        {
            new(0, null, null, null, new(0, 10), false, Low: -3),
            new(1, null, null, null, new(1, 12), false, Low: 6),
            new(2, null, null, null, new(2, 20), true, Low: -100),
        });
        Assert.Null(ChartLowerExtrema.For(data).Bounds);
        Assert.Equal(new ChartValueBounds(-3, 6), ChartLowerExtrema.For(data, true).Bounds);
        var band = data.WithBandValues([new(0, new(0, 10), false, false), new(1, new(1, 12), false, false), new(2, new(2, 20), true, false)],
            [new(4, null, default), new(8, null, default), new(-200, null, default)]);
        Assert.Equal(new ChartValueBounds(4, 8), ChartLowerExtrema.For(band, true).Bounds);
    }

    [Fact]
    public void Custom_axis_validity_is_evaluated_again_after_caller_state_changes()
    {
        var data = new ChartDataSnapshot(1, new ChartDataPoint[]
        { new(0, null, null, null, new(0, 10), false), new(1, null, null, null, new(1, 12), false) })
            .WithBandValues([new(0, new(0, 10), false, false), new(1, new(1, 12), false, false)],
                [new(-4, null, default), new(8, null, default)]);
        var axis = new CustomAxis { Positive = true }; var lower = ChartLowerExtrema.For(data);
        Assert.Equal(new ChartValueBounds(8, 8), lower.ForAxis(axis));
        axis.Positive = false;
        Assert.Equal(new ChartValueBounds(-4, -4), lower.ForAxis(axis));
    }

    [Fact]
    public void Extrema_and_style_shared_indexes_do_not_root_obsolete_snapshots()
    {
        var references = PopulateCaches();
        for (var attempt = 0; attempt < 5 && references.Any(reference => reference.IsAlive); attempt++)
        { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.All(references, reference => Assert.False(reference.IsAlive));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] PopulateCaches()
    {
        var data = new ChartDataSnapshot(1, Enumerable.Range(0, 100).Select(i => new ChartDataPoint(i, null, null, null, new(i, i % 7), false)).ToArray());
        var styled = data.WithPointStyles(Enumerable.Repeat(new ChartPointStyle(7, null), data.Count).ToArray());
        _ = ChartDataExtrema.For(styled); _ = ChartDataIndex.For(styled); _ = ChartReadingIndex.For(styled); _ = ChartLowerExtrema.For(styled);
        return [new(data), new(styled)];
    }

    private sealed class CustomAxis : ChartAxis
    {
        public bool Positive { get; set; }
        public override bool IsValidValue(double value) => double.IsFinite(value) && (Positive ? value > 0 : value < 0);
    }

    private sealed class CountingData(ChartPoint[] points, bool compact) : IChartSnapshotData
    {
        public int CountReads { get; private set; }
        public int ValueReads { get; private set; }
        public int MetadataReads { get; private set; }
        public int Count { get { CountReads++; return points.Length; } }
        public bool HasOrdinalSourceIndices => true;
        public ReadOnlyMemory<ChartPoint> Coordinates => compact ? points : ReadOnlyMemory<ChartPoint>.Empty;
        public ChartDataValue GetValue(int ordinal) { ValueReads++; return new(ordinal, points[ordinal], false, false); }
        public ChartDataPoint GetPoint(int ordinal) { MetadataReads++; throw new InvalidOperationException("Numeric caches must not read metadata."); }
        public bool TryFindIdentity(object identity, bool valueEquality, out int ordinal) { ordinal = -1; return false; }
    }
}
