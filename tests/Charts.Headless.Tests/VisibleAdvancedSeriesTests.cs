using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

[Collection("Visibility index diagnostics")]
public sealed class VisibleAdvancedSeriesTests
{
    [AvaloniaTheory]
    [InlineData(ChartLineInterpolation.Linear)]
    [InlineData(ChartLineInterpolation.Smooth)]
    [InlineData(ChartLineInterpolation.StepBefore)]
    [InlineData(ChartLineInterpolation.StepAfter)]
    [InlineData(ChartLineInterpolation.StepCenter)]
    public void Narrow_line_view_keeps_exact_original_primitives_and_smooth_tangent_neighbors(ChartLineInterpolation interpolation)
    {
        var series = new LineSeries { Interpolation = interpolation, MarkerSize = 0, StrokeThickness = 1 };
        var data = Data(Enumerable.Range(0, 10_000).Select(i => new ChartPoint(i, 10 + Math.Sin(i * .7) * 4)));
        var context = Context(series, data, 4_000, 4_010, 0, 20);
        var expected = ChartLineGeometry.Build(context with { Projection = new ForwardProjection(context.Projection) })
            .SelectMany(figure => figure.Upper).ToArray();
        var before = ChartVisibilityQuery.ProjectedCount;
        var actual = ChartLineGeometry.Build(context).SelectMany(figure => figure.Upper).ToArray();
        Assert.InRange(ChartVisibilityQuery.ProjectedCount - before, 2, 30);
        Assert.NotEmpty(actual);
        foreach (var part in actual.Where(part => part.StartOrdinal >= 4_000 && part.EndOrdinal <= 4_010))
            Assert.Contains(part, expected);
        foreach (var part in expected.Where(part => part.StartOrdinal >= 4_000 && part.EndOrdinal <= 4_010))
            Assert.Contains(part, actual);
    }

    [AvaloniaTheory]
    [InlineData(false, ChartLineInterpolation.Linear)]
    [InlineData(false, ChartLineInterpolation.Smooth)]
    [InlineData(true, ChartLineInterpolation.Linear)]
    [InlineData(true, ChartLineInterpolation.Smooth)]
    public void Band_fills_survive_when_both_boundaries_are_outside_the_Y_view(bool range, ChartLineInterpolation interpolation)
    {
        ChartSeries series = range ? new RangeAreaSeries { Interpolation = interpolation, MarkerSize = 0 }
            : new AreaSeries { Interpolation = interpolation, Baseline = -100, MarkerSize = 0 };
        var points = Enumerable.Range(0, 10_000).Select(i => new ChartPoint(i, 100 + i % 3)).ToArray();
        var data = Data(points);
        if (range) data = data.WithBandValues(points.Select((point, i) => new ChartDataValue(10_000 + i * 7, point, false, false)).ToArray(),
            points.Select(point => new ChartBandValue(-100, null, point)).ToArray());
        var context = Context(series, data, 4_000, 4_010, 0, 20);
        var original = ChartLineGeometry.Build(context with { Projection = new ForwardProjection(context.Projection) })
            .SelectMany(figure => figure.Upper.Zip(figure.Lower)).ToArray();
        var before = ChartVisibilityQuery.ProjectedCount;
        var clipped = ChartLineGeometry.Build(context).SelectMany(figure => figure.Upper.Zip(figure.Lower)).ToArray();
        Assert.InRange(ChartVisibilityQuery.ProjectedCount - before, 4, 60);
        foreach (var pair in clipped.Where(pair => pair.First.StartOrdinal >= 4_000 && pair.First.EndOrdinal <= 4_010))
            Assert.Contains(pair, original);
        Assert.Contains(clipped, pair => pair.First.StartOrdinal == 4_005 && pair.Second.EndOrdinal == 4_006);
    }

    [AvaloniaTheory]
    [InlineData(ChartLineInterpolation.Linear)]
    [InlineData(ChartLineInterpolation.Smooth)]
    [InlineData(ChartLineInterpolation.StepBefore)]
    public void Unordered_crossings_keep_original_adjacency_and_never_join_across_missing_rows(ChartLineInterpolation interpolation)
    {
        var series = new LineSeries { Interpolation = interpolation, MarkerSize = 0 };
        var data = Data([new(-10, -10), new(10, 10), new(30, 30), new(double.NaN, double.NaN), new(30, -30), new(-30, 30)]);
        var context = Context(series, data, -1, 1, -1, 1);
        var parts = ChartLineGeometry.Build(context).SelectMany(figure => figure.Upper).ToArray();
        Assert.Contains(parts, part => part.StartOrdinal == 0 && part.EndOrdinal == 1);
        Assert.Contains(parts, part => part.StartOrdinal == 4 && part.EndOrdinal == 5);
        Assert.All(parts, part => Assert.Equal(part.StartOrdinal + 1, part.EndOrdinal));
        Assert.DoesNotContain(parts, part => part.StartOrdinal == 2 && part.EndOrdinal == 4);
    }

    [AvaloniaTheory]
    [InlineData(ChartPointDrawOrder.Source)]
    [InlineData(ChartPointDrawOrder.SizeAscending)]
    [InlineData(ChartPointDrawOrder.SizeDescending)]
    public void Encoded_marker_footprints_and_hits_preserve_raw_identity_while_retained_arrays_are_view_sized(ChartPointDrawOrder order)
    {
        var series = new BubbleSeries { DrawOrder = order, MarkerStrokeThickness = 0 };
        var points = Enumerable.Range(0, 10_000).Select(i => new ChartDataPoint(10_000 + i * 7, $"id-{i}", new object(), null,
            new(i, 50), false) { MarkerDiameter = 2 }).ToArray();
        points[3_999] = points[3_999] with { MarkerDiameter = 80 };
        var data = new ChartDataSnapshot(0, points);
        var context = Context(series, data, 4_000, 4_010, 0, 100);
        var paintOrder = ChartPointGeometry.PointOrder(context).ToArray();
        Assert.Contains(3_999, paintOrder);
        Assert.InRange(paintOrder.Length, 11, 100);
        var retained = ChartPointSeriesRenderer.Instance.CreateRetainedDrawing(context);
        var marks = retained.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(field => field.GetValue(retained)).OfType<Array>().Single();
        Assert.InRange(marks.Length, 11, 100);
        var hit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, new(1, 50), 0));
        Assert.True(data.TryGetPoint(hit.SourceIndex, out var raw));
        Assert.Same(points[(hit.SourceIndex - 10_000) / 7].Item, raw.Item);
        Assert.Equal(points[(hit.SourceIndex - 10_000) / 7].StableId, raw.StableId);
        Assert.Equal(points[(hit.SourceIndex - 10_000) / 7].Point, raw.Point);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Category_clipping_keeps_value_spanning_bodies_stems_and_pictorials(bool horizontal)
    {
        ChartSeries[] series = [new BarSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, Baseline = -100 },
            new LollipopSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, Baseline = -100 },
            new PictorialBarSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, Baseline = -100 }];
        var points = Enumerable.Range(0, 10_000).Select(i => horizontal ? new ChartPoint(100, i) : new ChartPoint(i, 100)).ToArray();
        foreach (var item in series)
        {
            var context = horizontal ? Context(item, Data(points), 0, 20, 4_000, 4_010) : Context(item, Data(points), 4_000, 4_010, 0, 20);
            context = context with { GroupWidth = 5 };
            var ordinals = ChartComparisonGeometry.VisibleCandidates(context);
            Assert.Contains(4_005, ordinals);
            Assert.InRange(ordinals.Count, 11, 30);
            Assert.True(ChartComparisonGeometry.TryCreate(context, 4_005, out var body));
            Assert.True(body.Bounds.Intersects(context.PlotArea));
            if (item is PictorialBarSeries) Assert.NotEmpty(PictorialBarRenderer.Symbols(context, 4_005));
        }
    }

    [AvaloniaFact]
    public void Waterfall_connector_crossings_keep_two_outside_endpoints_without_bridging_disjoint_source_runs()
    {
        var series = new WaterfallSeries { ShowConnectors = true, Baseline = 0, MarkerSize = 0 };
        var context = Context(series, Data([new(-10, 5), new(20, 5), new(30, 5), new(double.NaN, double.NaN), new(-30, 5), new(-20, 5)]), 0, 10, 0, 10)
            with { GroupWidth = 1 };
        var candidates = ChartComparisonGeometry.VisibleCandidates(context, connectors: true);
        Assert.Contains(0, candidates); Assert.Contains(1, candidates);
        Assert.DoesNotContain(3, candidates);
        Assert.DoesNotContain(4, candidates); Assert.DoesNotContain(5, candidates);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Smooth_stacks_keep_the_original_complete_topology_limiter_across_viewport_changes(bool remoteGap)
    {
        double[] lows = [13.1, 15.7, 55.1, 78];
        double[] highs = [13.185, 15.702, 55.111, 78.035];
        var lower = Enumerable.Range(0, 10_000).Select(i => new ChartPoint(i, lows[i % 4])).ToArray();
        var upper = Enumerable.Range(0, 10_000).Select(i => new ChartPoint(i, highs[i % 4])).ToArray();
        if (remoteGap) upper[100] = upper[100] with { Y = double.NaN };
        var a = Band(lower, lower.Select(_ => 0d).ToArray());
        var b = Band(upper, lower.Select(point => point.Y).ToArray());
        var originals = new[] { a, b }.Select(context => context with { Projection = new ForwardProjection(context.Projection) }).ToArray();
        ChartLineGeometry.PrepareStackGeometry(originals);
        var expected = originals.Select(context => ChartLineGeometry.Build(context).SelectMany(figure => figure.Upper).ToArray()).ToArray();
        ChartLineGeometry.PrepareStackGeometry([a, b]);
        for (var member = 0; member < 2; member++)
            foreach (var part in ChartLineGeometry.Build(member == 0 ? a : b).SelectMany(figure => figure.Upper)
                .Where(part => part.StartOrdinal >= 4_000 && part.EndOrdinal <= 4_010))
                Assert.Contains(part, expected[member]);
        var topologyBuilds = ChartStackTopology.BuildCount;
        var next = new[] { a, b }.Select(context => Context(context.Series, context.Data, 5_000, 5_010, 0, 100)).ToArray();
        var before = ChartVisibilityQuery.ProjectedCount;
        ChartLineGeometry.PrepareStackGeometry(next);
        Assert.Equal(topologyBuilds, ChartStackTopology.BuildCount);
        Assert.InRange(ChartVisibilityQuery.ProjectedCount - before, 8, 120);

        ChartSeriesRenderContext Band(ChartPoint[] points, double[] baseline)
        {
            var data = Data(points).WithBandValues(points.Select((point, i) => new ChartDataValue(10_000 + i * 7, point,
                !double.IsFinite(point.Y), false)).ToArray(), points.Select((point, i) => new ChartBandValue(baseline[i], null, point)).ToArray());
            return Context(new AreaSeries { StackMode = ChartStackMode.Stacked, Interpolation = ChartLineInterpolation.Smooth, MarkerSize = 0 },
                data, 4_000, 4_010, 0, 100);
        }
    }

    [AvaloniaTheory]
    [InlineData("smooth")]
    [InlineData("area")]
    [InlineData("range")]
    [InlineData("scatter-line")]
    public void Spatial_brushes_preserve_the_original_complete_native_geometry_paint_bounds(string kind)
    {
        ChartSeries series = kind switch
        {
            "smooth" => new LineSeries { Interpolation = ChartLineInterpolation.Smooth },
            "area" => new AreaSeries(),
            "range" => new RangeAreaSeries(),
            _ => new ScatterLineSeries(),
        };
        var data = Data(Enumerable.Range(0, 1_000).Select(i => new ChartPoint(i, 30 + i % 10)));
        if (series is RangeAreaSeries) data = data.WithBandValues(Enumerable.Range(0, data.Count).Select(data.GetValue).ToArray(),
            Enumerable.Range(0, data.Count).Select(_ => new ChartBandValue(20, null, default)).ToArray());
        var context = Context(series, data, 400, 410, 0, 100);
        Assert.InRange(ChartLineGeometry.Build(context).SelectMany(figure => figure.Upper).Count(), 10, 100);
        var gradient = new LinearGradientBrush { GradientStops = [new(Colors.Red, 0), new(Colors.Blue, 1)] };
        series.Stroke = gradient; series.Fill = gradient;
        var original = ChartLineGeometry.Build(context with { Projection = new ForwardProjection(context.Projection) });
        var preserved = ChartLineGeometry.Build(context);
        Assert.Equal(999, preserved.SelectMany(figure => figure.Upper).Count());
        Assert.Equal(original.SelectMany(figure => figure.Upper), preserved.SelectMany(figure => figure.Upper));
        Assert.Equal(original.SelectMany(figure => figure.Lower), preserved.SelectMany(figure => figure.Lower));
    }

    [AvaloniaFact]
    public void Complete_stack_topology_profiles_stay_bounded_during_long_lived_domain_and_baseline_edits()
    {
        var series = new AreaSeries { StackMode = ChartStackMode.Stacked, Interpolation = ChartLineInterpolation.Smooth };
        var data = Data([new(0, 100), new(1, 101), new(2, 102)]);
        for (var revision = 0; revision < 32; revision++)
        {
            var axis = new NumericAxis();
            axis.ScaleBreaks.Add(new ChartScaleBreak { Start = 200 + revision, End = 200.5 + revision });
            var context = Context(series, data, 0, 10, 0, 150) with { Projection = new CartesianChart.CartesianProjection(
                new(0, 0, 100, 100), ChartScale.CreateForView(axis, 0, 10), ChartScale.CreateForView(new NumericAxis(), 0, 150)) };
            Assert.Equal(new ChartStackTopology.Match(true, true), ChartStackTopology.Compare(context, context));
            Assert.InRange(ChartStackTopology.CachedProfileCount(data), 1, 8);
        }
        for (var revision = 0; revision < 32; revision++)
        {
            series.Baseline = revision;
            var context = Context(series, data, 0, 10, 0, 150);
            Assert.Equal(new ChartStackTopology.Match(true, true), ChartStackTopology.Compare(context, context));
            Assert.Equal(8, ChartStackTopology.CachedProfileCount(data));
        }
        var current = Context(series, data, 0, 10, 0, 150);
        var builds = ChartStackTopology.BuildCount;
        for (var repeat = 0; repeat < 20; repeat++) ChartStackTopology.Compare(current, current);
        Assert.Equal(builds, ChartStackTopology.BuildCount);
    }

    private static ChartDataSnapshot Data(IEnumerable<ChartPoint> points) => new(0, points.Select((point, i) =>
        new ChartDataPoint(10_000 + i * 7, $"id-{i}", null, null, point, !double.IsFinite(point.X) || !double.IsFinite(point.Y))).ToArray());

    private static ChartSeriesRenderContext Context(ChartSeries series, ChartDataSnapshot data, double x0, double x1, double y0, double y1) =>
        new(series, data, new CartesianChart.CartesianProjection(new(0, 0, 100, 100),
            ChartScale.CreateForView(new NumericAxis(), x0, x1), ChartScale.CreateForView(new NumericAxis(), y0, y1)), Brushes.Blue);

    private sealed class ForwardProjection(IChartProjection original) : IChartProjection
    {
        public Rect PlotArea => original.PlotArea;
        public double YOrigin => original.YOrigin;
        public Point Project(ChartPoint point) => original.Project(point);
        public ChartPoint Unproject(Point point) => original.Unproject(point);
        public double MapX(double value) => original.MapX(value);
        public double MapY(double value) => original.MapY(value);
    }
}
