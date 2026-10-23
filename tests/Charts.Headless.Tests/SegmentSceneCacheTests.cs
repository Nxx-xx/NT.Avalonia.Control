using System.Collections;
using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class SegmentSceneCacheTests
{
    [AvaloniaTheory]
    [InlineData(ChartLineInterpolation.Linear)]
    [InlineData(ChartLineInterpolation.Smooth)]
    [InlineData(ChartLineInterpolation.StepCenter)]
    public void Repeated_segment_queries_read_only_winning_metadata_and_keep_one_geometry_per_view(ChartLineInterpolation interpolation)
    {
        var series = new LineSeries { Interpolation = interpolation, MarkerSize = 0,
            ItemsSource = Enumerable.Range(0, 512).Select(i => new ChartPoint(i, i)).ToArray() };
        var chart = new CartesianChart { ShowLegend = false,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 511 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 511 } };
        chart.Series.Add(series);
        using var host = new TemplateFeaturesHost(chart);
        var source = new CountingData(series.CreateSnapshot());
        var version = series.SourceVersion;
        Assert.True(series.TryPublishSnapshot(ChartDataSnapshot.FromCompact(version, source), version));
        host.Drain();
        TemplateFeaturesHost.Record(chart);
        var plot = chart.PlotArea;
        var query = new Point(plot.Left + plot.Width * .375, plot.Bottom - plot.Height * .375);
        var initial = Assert.IsType<ChartSegmentHit>(chart.HitTestSegment(query, 2));
        var geometryCount = Counter(chart, "_segmentCacheBuildCount");
        var pathCount = ChartLineGeometry.GeometryBuildCount;
        var valueReads = source.ValueReads;
        var metadataReads = source.MetadataReads;
        for (var queryIndex = 0; queryIndex < 40; queryIndex++)
        {
            var hit = Assert.IsType<ChartSegmentHit>(chart.HitTestSegment(query, 2));
            Assert.Equal(initial.Start.Index, hit.Start.Index); Assert.Equal(initial.End.Index, hit.End.Index);
            Assert.Equal(initial.Point, hit.Point);
            TemplateFeaturesHost.Record(chart);
        }
        Assert.Equal(geometryCount, Counter(chart, "_segmentCacheBuildCount"));
        Assert.Equal(pathCount, ChartLineGeometry.GeometryBuildCount);
        Assert.InRange(source.ValueReads - valueReads, 0, 40 * 4);
        Assert.InRange(source.MetadataReads - metadataReads, 40 * 2, 40 * 4);

        chart.Viewport = new ChartViewport(100, 400, 100, 400);
        host.Drain();
        var nextPlot = chart.PlotArea;
        Assert.NotNull(chart.HitTestSegment(nextPlot.Center, 1));
        Assert.True(Counter(chart, "_segmentCacheBuildCount") > geometryCount);
        host.Window.Content = null; host.Drain();
        Assert.Empty((IDictionary)typeof(CartesianChart).GetField("_segmentCaches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!);
        Assert.Null(typeof(CartesianChart).GetField("_segmentCacheScene", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart));
    }

    [AvaloniaTheory]
    [InlineData(ChartLineInterpolation.Linear)]
    [InlineData(ChartLineInterpolation.Smooth)]
    public void Nonstacked_segment_geometry_keeps_live_custom_axis_transforms_out_of_owned_projection_caches(ChartLineInterpolation interpolation)
    {
        var axis = new FoldedAxis { Minimum = 0, Maximum = 10 };
        var chart = new CartesianChart { ShowLegend = false, XAxis = axis,
            YAxis = new NumericAxis { Minimum = 0, Maximum = 10 } };
        chart.Series.Add(new LineSeries { Interpolation = interpolation, MarkerSize = 0,
            ItemsSource = new ChartPoint[] { new(2, 3), new(4, 7) } });
        using var host = new TemplateFeaturesHost(chart);
        var plot = chart.PlotArea;
        var original = new Point(plot.Left + plot.Width * .3, plot.Bottom - plot.Height * .5);
        Assert.NotNull(chart.HitTestSegment(original, .01));
        var before = Counter(chart, "_segmentCacheBuildCount");
        axis.FoldInterior = true;
        var folded = new Point(plot.Left + plot.Width * .7, plot.Bottom - plot.Height * .5);
        var hit = Assert.IsType<ChartSegmentHit>(chart.HitTestSegment(folded, .01));
        if (interpolation == ChartLineInterpolation.Linear)
        { Assert.Equal(folded.X, hit.Position.X, 7); Assert.Equal(folded.Y, hit.Position.Y, 7); }
        else
        {
            // Smooth nearest hits use the existing 40-iteration curve solver;
            // its subpixel approximation is independent of view-cache freshness.
            Assert.InRange(Math.Abs(folded.X - hit.Position.X), 0, .00001);
            Assert.InRange(Math.Abs(folded.Y - hit.Position.Y), 0, .00001);
        }
        Assert.Null(chart.HitTestSegment(original, .01));
        Assert.True(Counter(chart, "_segmentCacheBuildCount") > before);
        Assert.Empty((IDictionary)typeof(CartesianChart).GetField("_segmentCaches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!);
    }

    private static long Counter(CartesianChart chart, string name) => (long)typeof(CartesianChart)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;

    private sealed class CountingData(ChartDataSnapshot original) : IChartSnapshotData
    {
        public int ValueReads { get; private set; }
        public int MetadataReads { get; private set; }
        public int Count => original.Count;
        public ReadOnlyMemory<ChartPoint> Coordinates => ReadOnlyMemory<ChartPoint>.Empty;
        public bool HasOrdinalSourceIndices => true;
        public ChartDataValue GetValue(int ordinal)
        { ValueReads++; return original.GetValue(ordinal); }
        public ChartDataPoint GetPoint(int ordinal)
        { MetadataReads++; return original.Points[ordinal]; }
        public bool TryFindIdentity(object identity, bool valueEquality, out int ordinal) { ordinal = -1; return false; }
    }
    private sealed class FoldedAxis : ChartAxis
    {
        public bool FoldInterior;
        public override double Transform(double value) => FoldInterior && value > 0 && value < 10 ? 10 - value : value;
    }
}
