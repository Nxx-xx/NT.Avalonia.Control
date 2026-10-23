using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

[Collection("Advanced point spatial hit diagnostics")]
public sealed class AdvancedPointSpatialHitTests
{
    [AvaloniaTheory]
    [InlineData(ChartPointDrawOrder.Source)]
    [InlineData(ChartPointDrawOrder.SizeAscending)]
    [InlineData(ChartPointDrawOrder.SizeDescending)]
    public void Dense_spatial_dispatch_matches_original_full_footprint_scan_for_all_shapes_orders_and_regions(ChartPointDrawOrder order)
    {
        foreach (var marker in Enum.GetValues<ChartMarkerShape>())
        {
            var random = new Random(404_031);
            var points = Enumerable.Range(0, 640).Select(i => new ChartDataPoint(100_000 - i * 7, $"raw-{i}", null, null,
                new(48 + random.NextDouble() * 4, random.NextDouble() * 110 - 5), i % 37 == 0)
            { MarkerDiameter = i % 9 == 0 ? 24 : 6 + random.NextDouble() * 30 }).ToArray();
            points[3] = points[3] with { Point = new(50, 50), MarkerDiameter = 24 };
            points[7] = points[7] with { Point = new(50, 50), MarkerDiameter = 24 };
            var series = new BubbleSeries { MarkerShape = marker, DrawOrder = order, MarkerStrokeThickness = 4 };
            var data = new ChartDataSnapshot(0, points);
            foreach (var regions in new IReadOnlyList<Rect>?[] { null, [new(0, 0, 49, 100), new(51, 0, 49, 100)], [new(0, 20, 100, 60)] })
            {
                var context = Context(series, data) with { PointHitRegions = regions };
                var index = ChartPointSceneCache.ForHitTest(context);
                Assert.True(index.CandidateRange(new(50, 50), 4).End - index.CandidateRange(new(50, 50), 4).Start > 128);
                var visits = ChartPointSceneCache.HitNodeVisitCount;
                for (var i = 0; i < 90; i++)
                {
                    var position = i == 0 ? new Point(50, 50) : new Point(47 + random.NextDouble() * 6, random.NextDouble() * 100);
                    var tolerance = i % 4 switch { 0 => 0d, 1 => .25, 2 => 4d, _ => 20d };
                    AssertHit(OriginalFullShapeHit(context, position, tolerance), series.Renderer.HitTest(context, position, tolerance));
                }
                Assert.True(ChartPointSceneCache.HitNodeVisitCount > visits);
                foreach (var point in points.Where(point => !point.IsEmpty).Take(12))
                {
                    var shape = new ChartPointShape(context.Projection.Project(point.Point), point.MarkerDiameter!.Value, 4, marker);
                    foreach (var edge in new[] { shape.Bounds.TopLeft, shape.Bounds.TopRight, shape.Bounds.BottomLeft, shape.Bounds.BottomRight })
                    {
                        if (!context.PlotArea.Contains(edge)) continue;
                        AssertHit(OriginalFullShapeHit(context, edge, 0), series.Renderer.HitTest(context, edge, 0));
                        var adjacent = new Point(Math.BitIncrement(edge.X), Math.BitDecrement(edge.Y));
                        AssertHit(OriginalFullShapeHit(context, adjacent, 0), series.Renderer.HitTest(context, adjacent, 0));
                    }
                }
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(ChartPointDrawOrder.Source)]
    [InlineData(ChartPointDrawOrder.SizeAscending)]
    [InlineData(ChartPointDrawOrder.SizeDescending)]
    public void One_hundred_thousand_concentric_sizes_keep_exact_original_source_and_stable_paint_ties(ChartPointDrawOrder order)
    {
        const int count = 100_000;
        var points = Enumerable.Range(0, count).Select(i => new ChartDataPoint(1_000_000 - i * 3, i, null, null, new(50, 50), false)
        { MarkerDiameter = 8 + i % 23 }).ToArray();
        var context = Context(new BubbleSeries { MarkerStrokeThickness = 0, DrawOrder = order }, new(0, points));
        Assert.True(ChartPointSceneCache.ForHitTest(context).CandidateRange(new(50, 50), 4).End > 128);
        foreach (var position in new[] { new Point(50, 50), new Point(65.25, 52.75), new Point(50, 66) })
        {
            var expected = OriginalAnalyticCircleHit(context, position, 4);
            AssertHit(expected, context.Series.Renderer.HitTest(context, position, 4));
            Assert.NotNull(expected);
            Assert.Equal(expected.SourceIndex, context.Series.Renderer.HitTest(context, position, 4)!.SourceIndex);
        }
    }

    [AvaloniaFact]
    public void One_hundred_thousand_identical_off_center_footprints_evaluate_one_shape_and_return_the_last_physical_source()
    {
        const int count = 100_000;
        var points = Enumerable.Range(0, count).Select(i => new ChartDataPoint(1_000_000 - i * 3, i, null, null, new(50, 50), false)
        { MarkerDiameter = 20 }).ToArray();
        var context = Context(new BubbleSeries { MarkerStrokeThickness = 0 }, new(0, points));
        var position = new Point(64, 50);
        var index = ChartPointSceneCache.ForHitTest(context);
        Assert.Equal(count, index.CandidateRange(position, 4).End - index.CandidateRange(position, 4).Start);
        var visits = ChartPointSceneCache.HitNodeVisitCount; var evaluated = ChartPointSceneCache.HitCandidateCount;
        for (var i = 0; i < 16; i++)
        {
            var hit = Assert.IsType<ChartSeriesHit>(context.Series.Renderer.HitTest(context, position, 4));
            Assert.Equal(points[^1].SourceIndex, hit.SourceIndex); Assert.Equal(16, hit.DistanceSquared, 8);
            Assert.Equal(new Point(50, 50), hit.Position);
        }
        Assert.Equal(16, ChartPointSceneCache.HitNodeVisitCount - visits);
        Assert.Equal(16, ChartPointSceneCache.HitCandidateCount - evaluated);
    }

    [AvaloniaFact]
    public void Same_X_varied_Y_warm_queries_use_the_spatial_index_without_source_reads_or_rebuilds()
    {
        var storage = new CountingData(100_000); var data = ChartDataSnapshot.FromCompact(0, storage);
        var series = new BubbleSeries { MarkerStrokeThickness = 0 };
        var context = Context(series, data);
        Assert.NotNull(series.Renderer.HitTest(context, new(50, 50), 0));
        var index = ChartPointSceneCache.ForHitTest(context);
        Assert.Equal(100_000, index.CandidateRange(new(50, 50), 0).End - index.CandidateRange(new(50, 50), 0).Start);
        var builds = ChartPointSceneCache.HitIndexBuildCount; var orders = ChartPointSceneCache.PointOrderBuildCount;
        var values = storage.ValueReads; var metadata = storage.MetadataReads;
        var evaluated = ChartPointSceneCache.HitCandidateCount;
        for (var i = 0; i < 50; i++) Assert.NotNull(series.Renderer.HitTest(context, new(50, 5 + i * 1.8), 0));
        Assert.Same(index, ChartPointSceneCache.ForHitTest(context));
        Assert.Equal(builds, ChartPointSceneCache.HitIndexBuildCount); Assert.Equal(orders, ChartPointSceneCache.PointOrderBuildCount);
        Assert.Equal(values, storage.ValueReads); Assert.Equal(metadata, storage.MetadataReads);
        Assert.InRange(ChartPointSceneCache.HitCandidateCount - evaluated, 1, 50 * 128);
    }

    [AvaloniaFact]
    public void Independent_numeric_and_inverse_log_axis_contexts_keep_their_own_original_hits_and_regions()
    {
        var data = new ChartDataSnapshot(0, Enumerable.Range(0, 640).Select(i => new ChartDataPoint(9000 - i * 3, i, null, null,
            new(50, Math.Pow(10, i / 639d * 4)), false) { MarkerDiameter = 8 }).ToArray());
        var series = new BubbleSeries { MarkerStrokeThickness = 1 };
        var numeric = Context(series, data, yAxis: new NumericAxis(), yMinimum: 0, yMaximum: 10000);
        var logarithmic = Context(series, data, yAxis: new LogarithmicAxis { IsInversed = true }, yMinimum: 1, yMaximum: 10000);
        Assert.NotSame(ChartPointSceneCache.ForHitTest(numeric), ChartPointSceneCache.ForHitTest(logarithmic));
        foreach (var context in new[] { numeric, logarithmic })
            foreach (var regions in new IReadOnlyList<Rect>?[] { null, [new(0, 0, 100, 45), new(0, 55, 100, 45)] })
            {
                var current = context with { PointHitRegions = regions };
                for (var i = 0; i <= 100; i += 2)
                    AssertHit(OriginalFullShapeHit(current, new(50, i), 2), series.Renderer.HitTest(current, new(50, i), 2));
            }
        Assert.NotEqual(numeric.Series.Renderer.HitTest(numeric, new(50, 50), 0)!.SourceIndex,
            logarithmic.Series.Renderer.HitTest(logarithmic, new(50, 50), 0)!.SourceIndex);
    }

    [AvaloniaFact]
    public void Chart_multi_axis_hover_and_selection_return_the_exact_original_record_from_dense_spatial_hits()
    {
        var leftRows = Enumerable.Range(0, 512).Select(i => new Record($"left-{i}", 50, 5 + i / 511d * 90)).ToArray();
        var rightRows = Enumerable.Range(0, 512).Select(i => new Record($"log-{i}", 50, Math.Pow(10, i / 511d * 4))).ToArray();
        var left = new BubbleSeries { ItemsSource = leftRows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", MarkerSize = 8, MarkerStrokeThickness = 0 };
        var right = new BubbleSeries { ItemsSource = rightRows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", MarkerSize = 8, MarkerStrokeThickness = 0, YAxisKey = "log" };
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 100 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Axes.Add(new LogarithmicAxis { Key = "log", Minimum = 1, Maximum = 10000, Position = ChartAxisPosition.Far, IsInversed = true });
        chart.Series.Add(left); chart.Series.Add(right);
        using var host = new TemplateFeaturesHost(chart);
        var contexts = Contexts(chart);
        foreach (var position in new[] { chart.PlotArea.Center, new Point(chart.PlotArea.Center.X, chart.PlotArea.Top + chart.PlotArea.Height * .25) })
        {
            var expected = contexts.Select((context, paintOrder) => (context, paintOrder, hit: OriginalFullShapeHit(context, position, 2)))
                .Where(candidate => candidate.hit is not null).OrderBy(candidate => candidate.hit!.DistanceSquared).ThenByDescending(candidate => candidate.paintOrder).First();
            var actual = Assert.IsType<ChartPointHit>(chart.HitTestPoint(position, 2));
            Assert.Same(expected.context.Series, actual.Series); Assert.Equal(expected.hit!.SourceIndex, actual.Index);
            var record = ReferenceEquals(actual.Series, left) ? leftRows[actual.Index] : rightRows[actual.Index];
            Assert.Same(record, actual.Item); Assert.Equal(record.Id, actual.StableId); Assert.Equal(new ChartPoint(record.X, record.Y), actual.Point);
            chart.SelectedPoint = actual; Assert.Same(record, chart.SelectedPoint!.Item);
            SegmentTestHost.RaiseExactMove(chart, position); host.Drain();
            Assert.Same(record, chart.HoveredPoint!.Item); Assert.Equal(record.Id, chart.HoveredPoint.StableId);
        }
    }

    private sealed record Record(string Id, double X, double Y);
    private static ChartSeriesRenderContext Context(ChartSeries series, ChartDataSnapshot data, ChartAxis? yAxis = null, double yMinimum = 0, double yMaximum = 100) =>
        new(series, data, new CartesianChart.CartesianProjection(new(0, 0, 100, 100), ChartScale.CreateForView(new NumericAxis(), 0, 100),
            ChartScale.CreateForView(yAxis ?? new NumericAxis(), yMinimum, yMaximum)), Brushes.Blue);

    private static ChartSeriesRenderContext[] Contexts(CartesianChart chart)
    {
        _ = chart.PlotArea;
        var scene = typeof(CartesianChart).GetField("_scene", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
        var snapshots = (Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!;
        return snapshots.Cast<object>().Select(snapshot => (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context")!.GetValue(snapshot)!).ToArray();
    }

    // The reference enumerates every original physical row and stable paint order,
    // without the view candidate list, HitIndex, spatial sort, or BVH. Exact marker
    // footprint mathematics is the existing independently tested C3 kernel.
    private static ChartSeriesHit? OriginalFullShapeHit(ChartSeriesRenderContext context, Point position, double tolerance)
    {
        if (!context.Series.ShowMarkers || !context.PlotArea.Contains(position) || context.PointHitRegions is { } regions && !regions.Any(region => Inclusive(region, position))) return null;
        var best = double.PositiveInfinity; var sourceIndex = -1; var anchor = default(Point);
        foreach (var ordinal in OriginalOrder(context))
        {
            var point = context.Data.Points[ordinal];
            if (point.IsEmpty || !double.IsFinite(point.Point.X) || !double.IsFinite(point.Point.Y)) continue;
            var diameter = point.MarkerDiameter ?? Math.Clamp(context.Series.MarkerSize, 0, 100);
            if (!double.IsFinite(diameter) || diameter <= 0) continue;
            var center = context.Projection.Project(point.Point);
            var stroke = Math.Clamp(context.Series.MarkerStrokeThickness ?? context.Series.StrokeThickness, 0, 100);
            var shape = new ChartPointShape(center, diameter, stroke, context.Series.MarkerShape);
            if (!shape.Bounds.Intersects(context.PlotArea)) continue;
            var distance = context.PointHitRegions is not { } clips ? shape.DistanceSquared(position, context.PlotArea)
                : clips.Where(region => shape.Bounds.Intersects(region)).Select(region => shape.DistanceSquared(position, region)).DefaultIfEmpty(double.PositiveInfinity).Min();
            if (!double.IsFinite(distance) || distance > tolerance * tolerance || distance > best) continue;
            best = distance; sourceIndex = point.SourceIndex;
            anchor = new(Math.Clamp(center.X, context.PlotArea.Left, context.PlotArea.Right), Math.Clamp(center.Y, context.PlotArea.Top, context.PlotArea.Bottom));
        }
        return sourceIndex < 0 ? null : new(sourceIndex, anchor, best);
    }

    private static ChartSeriesHit? OriginalAnalyticCircleHit(ChartSeriesRenderContext context, Point position, double tolerance)
    {
        var best = double.PositiveInfinity; var sourceIndex = -1;
        foreach (var ordinal in OriginalOrder(context))
        {
            var point = context.Data.Points[ordinal];
            var dx = position.X - 50; var dy = position.Y - 50;
            var distance = Math.Pow(Math.Max(0, Math.Sqrt(dx * dx + dy * dy) - point.MarkerDiameter!.Value / 2), 2);
            if (distance > tolerance * tolerance || distance > best) continue;
            best = distance; sourceIndex = point.SourceIndex;
        }
        return sourceIndex < 0 ? null : new(sourceIndex, new(50, 50), best);
    }

    private static IEnumerable<int> OriginalOrder(ChartSeriesRenderContext context)
    {
        var source = Enumerable.Range(0, context.Data.Count);
        return ((IChartPointSeries)context.Series).DrawOrder switch
        {
            ChartPointDrawOrder.SizeAscending => source.OrderBy(i => context.Data.Points[i].MarkerDiameter ?? context.Series.MarkerSize),
            ChartPointDrawOrder.SizeDescending => source.OrderByDescending(i => context.Data.Points[i].MarkerDiameter ?? context.Series.MarkerSize),
            _ => source,
        };
    }
    private static bool Inclusive(Rect region, Point point) => point.X >= region.Left && point.X <= region.Right && point.Y >= region.Top && point.Y <= region.Bottom;
    private static void AssertHit(ChartSeriesHit? expected, ChartSeriesHit? actual)
    {
        if (expected is null) { Assert.Null(actual); return; }
        Assert.NotNull(actual); Assert.Equal(expected.SourceIndex, actual.SourceIndex);
        Assert.Equal(expected.Position, actual.Position); Assert.Equal(expected.DistanceSquared, actual.DistanceSquared, 8);
    }
    private sealed class CountingData(int count) : IChartSnapshotData
    {
        internal int ValueReads, MetadataReads;
        public int Count => count;
        public bool HasOrdinalSourceIndices => true;
        public ReadOnlyMemory<ChartPoint> Coordinates => ReadOnlyMemory<ChartPoint>.Empty;
        public ChartDataValue GetValue(int index) { ValueReads++; return new(index, new(50, index / (count - 1d) * 100), false, false); }
        public ChartDataPoint GetPoint(int index) { MetadataReads++; return new(index, index, null, null, new(50, index / (count - 1d) * 100), false) { MarkerDiameter = 2 }; }
        public bool TryFindIdentity(object identity, bool valueEquality, out int ordinal) { ordinal = -1; return false; }
    }
}

[CollectionDefinition("Advanced point spatial hit diagnostics", DisableParallelization = true)]
public sealed class AdvancedPointSpatialHitDiagnosticsCollection;
