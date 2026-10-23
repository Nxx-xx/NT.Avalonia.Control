using System.Runtime.CompilerServices;
using Avalonia;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartReadingIndexTests
{
    [Fact]
    public void Y_index_filters_both_coordinates_and_preserves_equal_y_physical_ordinals()
    {
        ChartPoint[] points = [new(1, 3), new(2, double.NaN), new(3, -0d), new(4, 0), new(double.NaN, 2), new(5, -1), new(6, 3)];
        var data = Snapshot(points);
        var index = ChartReadingIndex.For(data);
        Assert.Equal(5, index.Count);
        Assert.Equal(new[] { 5, 2, 3, 0, 6 }, Enumerable.Range(0, index.Count).Select(index.Ordinal));
        Assert.Equal((0, 2, true), index.ScreenRange(new ReverseYProjection(), -3, -3));
        Assert.Equal((2, 4, true), index.ScreenRange(new ReverseYProjection(), -0d, 0));
        Assert.Same(index, ChartReadingIndex.For(data));
        Assert.NotSame(index, ChartReadingIndex.For(Snapshot(points)));
    }

    [Fact]
    public void Y_index_weak_cache_does_not_keep_an_obsolete_snapshot_alive()
    {
        var weak = PopulateWeakCache();
        for (var attempt = 0; attempt < 5 && weak.IsAlive; attempt++)
        { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(weak.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference PopulateWeakCache()
    {
        var data = Snapshot(Enumerable.Range(0, 100).Select(i => new ChartPoint(i, i % 7)).ToArray());
        _ = ChartReadingIndex.For(data).Count;
        return new WeakReference(data);
    }

    private static ChartDataSnapshot Snapshot(ChartPoint[] points) => new(1, points.Select((point, ordinal) =>
        new ChartDataPoint(1000 - ordinal * 7, null, null, null, point, false)).ToArray());

    private sealed class ReverseYProjection : IChartProjection
    {
        public Rect PlotArea => new(0, 0, 100, 100);
        public double YOrigin => 0;
        public Point Project(ChartPoint point) => new(point.X, -point.Y);
        public ChartPoint Unproject(Point point) => new(point.X, -point.Y);
        public double MapX(double value) => value;
        public double MapY(double value) => -value;
    }
}
