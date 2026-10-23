using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class PointSceneCacheTests
{
    [AvaloniaFact]
    public void Warm_point_hits_and_paint_order_do_not_read_or_copy_the_snapshot()
    {
        var source = new CountingData(10_000);
        var data = ChartDataSnapshot.FromCompact(1, source);
        var series = new BubbleSeries { DrawOrder = ChartPointDrawOrder.SizeDescending, MarkerStrokeThickness = 0 };
        var context = Context(series, data, 10_000);
        var order = ChartPointGeometry.PointOrder(context);
        _ = order.Count();
        var metadataAfterOrder = source.MetadataReads;
        Assert.Equal(10_000, metadataAfterOrder);
        Assert.Same(order, ChartPointGeometry.PointOrder(context));
        Assert.Equal(metadataAfterOrder, source.MetadataReads);

        Assert.NotNull(series.Renderer.HitTest(context, new(5_000, 50), 0));
        var index = ChartPointSceneCache.ForHitTest(context);
        var valuesAfterIndex = source.ValueReads; var metadataAfterIndex = source.MetadataReads;
        for (var query = 0; query < 100; query++)
            Assert.NotNull(series.Renderer.HitTest(context, new(100 + query * 50, 50), 0));
        Assert.Same(index, ChartPointSceneCache.ForHitTest(context));
        Assert.Equal(valuesAfterIndex, source.ValueReads);
        Assert.Equal(metadataAfterIndex, source.MetadataReads);
        Assert.InRange(index.CandidateRange(new(5_000, 50), 0).End - index.CandidateRange(new(5_000, 50), 0).Start, 1, 50);
    }

    [AvaloniaTheory]
    [InlineData(ChartPointDrawOrder.Source)]
    [InlineData(ChartPointDrawOrder.SizeAscending)]
    [InlineData(ChartPointDrawOrder.SizeDescending)]
    public void Indexed_footprint_hits_match_the_original_full_scan_for_shapes_clips_and_ties(ChartPointDrawOrder order)
    {
        foreach (var marker in Enum.GetValues<ChartMarkerShape>())
        {
            var random = new Random(20261018);
            var points = Enumerable.Range(0, 140).Select(i => new ChartDataPoint(10_000 - i * 3, null, null, null,
                new(random.NextDouble() * 150 - 25, random.NextDouble() * 150 - 25), i % 31 == 0)
            { MarkerDiameter = i % 9 == 0 ? 20 : 2 + random.NextDouble() * 38 }).ToArray();
            // Exact overlaps exercise physical order within equal-size sort keys.
            points[3] = points[3] with { Point = new(50, 50), MarkerDiameter = 20 };
            points[7] = points[7] with { Point = new(50, 50), MarkerDiameter = 20 };
            var data = new ChartDataSnapshot(0, points);
            var series = new BubbleSeries { MarkerShape = marker, DrawOrder = order, MarkerStrokeThickness = 5 };
            var context = Context(series, data, 100);
            foreach (var regions in new IReadOnlyList<Rect>?[] { null, [new(0, 0, 45, 100), new(55, 0, 45, 100)] })
            {
                context = context with { PointHitRegions = regions };
                for (var query = 0; query < 45; query++)
                {
                    var position = query == 0 ? new Point(50, 50) : new Point(random.NextDouble() * 100, random.NextDouble() * 100);
                    var tolerance = query % 4 switch { 0 => 0d, 1 => .25, 2 => 4d, _ => 100d };
                    Assert.Equal(OriginalHit(context, position, tolerance), series.Renderer.HitTest(context, position, tolerance));
                }
            }
        }
    }

    [AvaloniaFact]
    public void Standalone_context_observes_marker_option_and_paint_order_replacements()
    {
        var series = new BubbleSeries { DrawOrder = ChartPointDrawOrder.SizeDescending, MarkerStrokeThickness = 0 };
        var context = Context(series, new ChartDataSnapshot(0, new ChartDataPoint[]
        {
            new(7, null, null, null, new(50, 50), false) { MarkerDiameter = 60 },
            new(9, null, null, null, new(50, 50), false) { MarkerDiameter = 20 },
        }), 100);
        Assert.Equal(9, series.Renderer.HitTest(context, new(50, 50), 0)!.SourceIndex);
        var before = ChartPointSceneCache.ForHitTest(context);
        series.DrawOrder = ChartPointDrawOrder.SizeAscending;
        Assert.Equal(7, series.Renderer.HitTest(context, new(50, 50), 0)!.SourceIndex);
        Assert.NotSame(before, ChartPointSceneCache.ForHitTest(context));
        series.ShowMarkers = false; Assert.Null(series.Renderer.HitTest(context, new(50, 50), 100));
        series.ShowMarkers = true; series.MarkerShape = ChartMarkerShape.VerticalLine;
        Assert.Null(series.Renderer.HitTest(context, new(55, 50), 0));
        series.MarkerStrokeThickness = 12;
        Assert.NotNull(series.Renderer.HitTest(context, new(55, 50), 0));
    }

    [AvaloniaFact]
    public void Caller_owned_projection_keeps_live_transform_behavior()
    {
        var projection = new MutableProjection(); var series = new BubbleSeries { MarkerStrokeThickness = 0 };
        var data = new ChartDataSnapshot(0, new ChartDataPoint[] { new(5, null, null, null, new(20, 50), false) { MarkerDiameter = 4 } });
        var context = new ChartSeriesRenderContext(series, data, projection, Brushes.Blue);
        Assert.NotNull(series.Renderer.HitTest(context, new(20, 50), 0));
        projection.Offset = 30;
        Assert.Null(series.Renderer.HitTest(context, new(20, 50), 0));
        Assert.NotNull(series.Renderer.HitTest(context, new(50, 50), 0));
    }

    [AvaloniaFact]
    public void Chart_projection_with_a_custom_axis_keeps_the_callers_live_transform()
    {
        var axis = new MutableAxis();
        var projection = new CartesianChart.CartesianProjection(new(0, 0, 100, 100),
            ChartScale.CreateForView(axis, 0, 100), ChartScale.CreateForView(new NumericAxis(), 0, 100));
        var series = new BubbleSeries { MarkerStrokeThickness = 0 };
        var data = new ChartDataSnapshot(0, new ChartDataPoint[] { new(5, null, null, null, new(20, 50), false) { MarkerDiameter = 4 } });
        var context = new ChartSeriesRenderContext(series, data, projection, Brushes.Blue);
        Assert.NotNull(series.Renderer.HitTest(context, new(20, 50), 0));
        axis.Offset = 30;
        Assert.Null(series.Renderer.HitTest(context, new(20, 50), 0));
        Assert.NotNull(series.Renderer.HitTest(context, new(50, 50), 0));
        Assert.NotSame(ChartPointSceneCache.ForHitTest(context), ChartPointSceneCache.ForHitTest(context));
    }

    [AvaloniaFact]
    public void Context_cache_does_not_root_retired_geometry_or_snapshots()
    {
        var references = Populate();
        for (var attempt = 0; attempt < 5 && references.Any(reference => reference.IsAlive); attempt++)
        { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.All(references, reference => Assert.False(reference.IsAlive));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] Populate()
    {
        var data = new ChartDataSnapshot(0, new ChartDataPoint[] { new(5, null, null, null, new(50, 50), false) { MarkerDiameter = 4 } });
        var context = Context(new BubbleSeries { DrawOrder = ChartPointDrawOrder.SizeDescending }, data, 100);
        _ = ChartPointGeometry.PointOrder(context); _ = ChartPointSceneCache.ForHitTest(context);
        return [new(data), new(context)];
    }

    private static ChartSeriesRenderContext Context(ChartSeries series, ChartDataSnapshot data, double width) => new(series, data,
        new CartesianChart.CartesianProjection(new(0, 0, width, 100), ChartScale.CreateForView(new NumericAxis(), 0, width),
            ChartScale.CreateForView(new NumericAxis(), 0, 100)), Brushes.Blue);

    private static ChartSeriesHit? OriginalHit(ChartSeriesRenderContext context, Point position, double tolerance)
    {
        var regions = context.PointHitRegions;
        if (regions is not null && !regions.Any(region => position.X >= region.Left && position.X <= region.Right && position.Y >= region.Top && position.Y <= region.Bottom)) return null;
        var source = Enumerable.Range(0, context.Data.Count);
        var order = ((IChartPointSeries)context.Series).DrawOrder switch
        {
            ChartPointDrawOrder.SizeAscending => source.OrderBy(i => context.Data.Points[i].MarkerDiameter),
            ChartPointDrawOrder.SizeDescending => source.OrderByDescending(i => context.Data.Points[i].MarkerDiameter),
            _ => (IEnumerable<int>)source,
        };
        var bestDistance = double.PositiveInfinity; var bestOrdinal = -1; var anchor = default(Point);
        foreach (var ordinal in order)
        {
            if (!ChartPointGeometry.TryCreate(context, ordinal, out var shape) || !shape.Bounds.Intersects(context.PlotArea)) continue;
            var distance = regions is null ? shape.DistanceSquared(position, context.PlotArea) :
                regions.Where(region => shape.Bounds.Intersects(region)).Select(region => shape.DistanceSquared(position, region)).DefaultIfEmpty(double.PositiveInfinity).Min();
            if (!double.IsFinite(distance) || distance > tolerance * tolerance || distance > bestDistance) continue;
            bestDistance = distance; bestOrdinal = ordinal;
            anchor = new(Math.Clamp(shape.Center.X, context.PlotArea.Left, context.PlotArea.Right), Math.Clamp(shape.Center.Y, context.PlotArea.Top, context.PlotArea.Bottom));
        }
        return bestOrdinal < 0 ? null : new(context.Data.GetValue(bestOrdinal).SourceIndex, anchor, bestDistance);
    }

    private sealed class CountingData(int count) : IChartSnapshotData
    {
        public int MetadataReads; public int ValueReads;
        public int Count => count;
        public bool HasOrdinalSourceIndices => true;
        public ReadOnlyMemory<ChartPoint> Coordinates => ReadOnlyMemory<ChartPoint>.Empty;
        public ChartDataValue GetValue(int ordinal) { ValueReads++; return new(ordinal, new(ordinal, 50), false, false); }
        public ChartDataPoint GetPoint(int ordinal) { MetadataReads++; return new(ordinal, null, null, null, new(ordinal, 50), false) { MarkerDiameter = 2 + ordinal % 20 }; }
        public bool TryFindIdentity(object identity, bool valueEquality, out int ordinal) { ordinal = -1; return false; }
    }
    private sealed class MutableProjection : IChartProjection
    {
        public double Offset;
        public Rect PlotArea => new(0, 0, 100, 100);
        public double YOrigin => 0;
        public Point Project(ChartPoint point) => new(point.X + Offset, point.Y);
        public ChartPoint Unproject(Point point) => new(point.X - Offset, point.Y);
        public double MapX(double value) => value + Offset;
        public double MapY(double value) => value;
    }
    private sealed class MutableAxis : ChartAxis
    {
        public double Offset;
        public override double Transform(double value) => value + Offset;
        public override double Inverse(double value) => value - Offset;
    }
}
