using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class IndexConstructionTests
{
    [Theory]
    [InlineData(false, 10_000)]
    [InlineData(true, 10_000)]
    [InlineData(true, 100_000)]
    public void Eager_index_keeps_full_precision_physical_ties_and_only_finite_xy(bool compact, int count)
    {
        var random = new Random(20261015);
        var points = Enumerable.Range(0, count)
            .Select(index => new ChartPoint(index % 13 == 0 ? Math.BitIncrement(1d) : random.Next(-100, 101), index))
            .ToArray();
        points[1] = new(-0d, 10); points[2] = new(0d, 11);
        points[3] = new(double.NaN, 1); points[4] = new(1, double.NaN);
        points[5] = new(double.PositiveInfinity, 1); points[6] = new(1, double.NegativeInfinity);
        points[7] = new(double.MinValue, 1); points[8] = new(double.MaxValue, 1);
        points[9] = new(double.Epsilon, 1); points[10] = new(-double.Epsilon, 1);
        var originalBits = Bits(points);
        var source = new CountingData(points, compact);
        var snapshot = ChartDataSnapshot.FromCompact(1, source);

        var index = ChartDataIndex.For(snapshot);
        Assert.Equal(1, source.CountReads);

        var expected = Enumerable.Range(0, points.Length)
            .Where(ordinal => double.IsFinite(points[ordinal].X) && double.IsFinite(points[ordinal].Y))
            .OrderBy(ordinal => points[ordinal].X).ThenBy(ordinal => ordinal).ToArray();
        Assert.Equal(expected, Enumerable.Range(0, index.Count).Select(index.Ordinal));
        Assert.Equal(new ChartSeriesBounds(double.MinValue, double.MaxValue, 0, points.Length - 1), index.Bounds);
        Assert.Equal(originalBits, Bits(points));
        // Bounds and the complete sort are ready before the first query. Mapped values are
        // read a constant number of times instead of once per sorting comparison.
        Assert.InRange(source.ValueReads, compact ? 0 : points.Length, compact ? 0 : 2 * points.Length);
        var readsAfterConstruction = source.ValueReads;
        Assert.Same(index, ChartDataIndex.For(snapshot));
        Assert.Equal(expected, Enumerable.Range(0, index.Count).Select(index.Ordinal));
        Assert.Equal(readsAfterConstruction, source.ValueReads);
        Assert.Equal(0, source.MetadataReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Filtered_descending_values_preserve_equal_x_order_and_original_ordinals(bool compact)
    {
        ChartPoint[] points = [new(9, 1), new(8, 2), new(8, 3), new(double.NaN, 4), new(6, double.NaN), new(2, 6), new(2, 7)];
        var source = new CountingData(points, compact);
        var snapshot = ChartDataSnapshot.FromCompact(3, source);

        var index = ChartDataIndex.For(snapshot);
        Assert.Equal(1, source.CountReads);

        Assert.Equal(new[] { 5, 6, 1, 2, 0 }, Enumerable.Range(0, index.Count).Select(index.Ordinal));
        Assert.Equal(new ChartSeriesBounds(2, 9, 1, 7), index.Bounds);
        Assert.InRange(source.ValueReads, 0, 2 * points.Length);
        Assert.Equal(0, source.MetadataReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Snapshot_local_cache_does_not_reuse_an_equal_version_or_read_metadata(bool compact)
    {
        var firstSource = new CountingData([new(3, 10), new(1, 20), new(2, 30)], compact);
        var secondSource = new CountingData([new(30, 1), new(10, 2), new(20, 3)], compact);
        var first = ChartDataIndex.For(ChartDataSnapshot.FromCompact(7, firstSource));
        var second = ChartDataIndex.For(ChartDataSnapshot.FromCompact(7, secondSource));

        Assert.NotSame(first, second);
        Assert.Equal(new ChartSeriesBounds(1, 3, 10, 30), first.Bounds);
        Assert.Equal(new ChartSeriesBounds(10, 30, 1, 3), second.Bounds);
        Assert.Equal(new[] { 1, 2, 0 }, Enumerable.Range(0, first.Count).Select(first.Ordinal));
        Assert.Equal(new[] { 1, 2, 0 }, Enumerable.Range(0, second.Count).Select(second.Ordinal));
        Assert.Equal(0, firstSource.MetadataReads + secondSource.MetadataReads);
    }

    [Fact]
    public void An_overlay_indexes_its_projected_values_instead_of_the_original_compact_coordinates()
    {
        var source = new CountingData([new(1, 1), new(2, 2), new(3, 3), new(4, 4), new(5, 5)], true);
        var original = ChartDataSnapshot.FromCompact(4, source);
        ChartPoint[] projected = [new(5, 0), new(1, double.NaN), new(3, 20), new(2, 30), new(3, 40)];
        var overlay = original.WithValues(projected.Select((point, ordinal) =>
            new ChartDataValue(projected.Length - ordinal - 1, point, false, false)).ToArray());
        var readsAfterOverlay = source.ValueReads;

        var index = ChartDataIndex.For(overlay);

        Assert.Equal(new[] { 3, 2, 4, 0 }, Enumerable.Range(0, index.Count).Select(index.Ordinal));
        Assert.Equal(new ChartSeriesBounds(2, 5, 0, 40), index.Bounds);
        Assert.Equal(readsAfterOverlay, source.ValueReads);
        Assert.Equal(0, source.MetadataReads);
        Assert.Equal(new ChartSeriesBounds(1, 5, 1, 5), ChartDataIndex.For(original).Bounds);
    }

    private static long[] Bits(ChartPoint[] points) => points.SelectMany(point =>
        new[] { BitConverter.DoubleToInt64Bits(point.X), BitConverter.DoubleToInt64Bits(point.Y) }).ToArray();

    private sealed class CountingData(ChartPoint[] points, bool compact) : IChartSnapshotData
    {
        public int ValueReads { get; private set; }
        public int MetadataReads { get; private set; }
        public int CountReads { get; private set; }
        public int Count { get { CountReads++; return points.Length; } }
        public bool HasOrdinalSourceIndices => false;
        public ReadOnlyMemory<ChartPoint> Coordinates => compact ? points : ReadOnlyMemory<ChartPoint>.Empty;
        public ChartDataValue GetValue(int index)
        {
            ValueReads++;
            return new(points.Length - index - 1, points[index], false, false);
        }
        public ChartDataPoint GetPoint(int index)
        {
            MetadataReads++;
            throw new InvalidOperationException("Index construction must not materialize identities or original items.");
        }
        public bool TryFindIdentity(object identity, bool valueEquality, out int ordinal)
        { ordinal = -1; return false; }
    }
}
