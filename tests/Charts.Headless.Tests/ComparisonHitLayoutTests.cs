using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

[Collection("Comparison hit layout diagnostics")]
public sealed class ComparisonHitLayoutTests
{
    [AvaloniaTheory]
    [InlineData("legacy", false)]
    [InlineData("bar", false)]
    [InlineData("bar", true)]
    [InlineData("range", false)]
    [InlineData("range", true)]
    [InlineData("waterfall", false)]
    [InlineData("waterfall", true)]
    [InlineData("dot", false)]
    [InlineData("dot", true)]
    [InlineData("lollipop", false)]
    [InlineData("lollipop", true)]
    [InlineData("variance", false)]
    [InlineData("variance", true)]
    [InlineData("pareto", false)]
    [InlineData("pareto", true)]
    public void Indexed_geometry_hits_equal_an_exhaustive_original_ordinal_oracle(string name, bool horizontal)
    {
        var (series, kind) = Series(name, horizontal);
        series.ShowMarkers = true; series.MarkerSize = 9; series.MarkerShape = ChartMarkerShape.Diamond;
        var data = Data(193, horizontal, kind);
        var context = Context(series, data, 1, 3, 33, inverse: horizontal);
        var layout = ChartComparisonLayout.For(context, kind);
        // Unordered categories, same categories, overlaps, negative and clipped
        // values, gaps, transformed bands and nonordinal source IDs are included.
        var random = new Random(404_013);
        for (var i = 0; i < 600; i++)
        {
            var position = new Point(context.PlotArea.Left - 10 + random.NextDouble() * (context.PlotArea.Width + 20),
                context.PlotArea.Top - 10 + random.NextDouble() * (context.PlotArea.Height + 20));
            var tolerance = (i % 4) switch { 0 => 0, 1 => .25, 2 => 4, _ => 20 };
            AssertHit(OriginalHit(context, kind, position, tolerance), layout.HitTest(position, tolerance));
        }
        foreach (var entry in layout.Entries)
        {
            var expected = OriginalShape(context, kind, entry.Ordinal);
            Assert.NotNull(expected); Assert.Equal(expected.Value.Bounds, entry.Shape.Bounds);
            Assert.Equal(expected.Value.Start, entry.Shape.Start); Assert.Equal(expected.Value.End, entry.Shape.End);
            AssertHit(OriginalHit(context, kind, entry.Shape.End, 0), layout.HitTest(entry.Shape.End, 0));
            var body = entry.Shape.Bounds.Intersect(context.PlotArea);
            if (body.Width <= 0 || body.Height <= 0) continue;
            foreach (var edge in new[] { body.TopLeft, body.TopRight, body.BottomLeft, body.BottomRight })
            {
                AssertHit(OriginalHit(context, kind, edge, 0), layout.HitTest(edge, 0));
                var outside = new Point(Math.BitDecrement(edge.X), Math.BitIncrement(edge.Y));
                AssertHit(OriginalHit(context, kind, outside, 0), layout.HitTest(outside, 0));
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Coincident_category_and_body_ties_choose_the_last_original_row_with_bounded_warm_work(bool legacy)
    {
        const int count = 100_000;
        var data = new ChartDataSnapshot(0, Enumerable.Range(0, count)
            .Select(i => new ChartDataPoint(i * 3 + 7, $"row-{i}", null, 50d, new(50, 80), false)).ToArray());
        var series = new BarSeries { WidthRatio = legacy ? .86 : .8, ShowMarkers = false };
        var context = Context(series, data, 0, 1, 12);
        var kind = legacy ? (ChartComparisonKind?)null : ChartComparisonKind.Bar;
        var layout = ChartComparisonLayout.For(context, kind);
        var position = context.Projection.Project(new(50, 40));
        Assert.Equal((count - 1) * 3 + 7, layout.HitTest(position, 0)!.SourceIndex);
        var visited = ChartComparisonLayout.VisitedCount; var evaluated = ChartComparisonLayout.EvaluatedCount;
        for (var i = 0; i < 32; i++) Assert.Equal((count - 1) * 3 + 7, layout.HitTest(position, 0)!.SourceIndex);
        Assert.InRange(ChartComparisonLayout.VisitedCount - visited, 1, 32 * 50);
        Assert.Equal(32, ChartComparisonLayout.EvaluatedCount - evaluated);
    }

    [AvaloniaFact]
    public void Coincident_markers_away_from_the_pointer_center_resolve_the_last_original_row_in_one_exact_test()
    {
        const int count = 100_000;
        var data = new ChartDataSnapshot(0, Enumerable.Range(0, count)
            .Select(i => new ChartDataPoint(7 + i * 3, $"dot-{i}", null, null, new(50, 50), false)).ToArray());
        var series = new DotSeries { MarkerSize = 10 };
        var context = Context(series, data);
        var layout = ChartComparisonLayout.For(context, ChartComparisonKind.Dot);
        var position = context.Projection.Project(new(50, 50)) + new Vector(2, 3);
        var visited = ChartComparisonLayout.VisitedCount; var evaluated = ChartComparisonLayout.EvaluatedCount;
        for (var i = 0; i < 16; i++)
        {
            var hit = Assert.IsType<ChartSeriesHit>(layout.HitTest(position, 0));
            Assert.Equal(7 + (count - 1) * 3, hit.SourceIndex); Assert.Equal(13, hit.DistanceSquared, 8);
        }
        Assert.Equal(16, ChartComparisonLayout.VisitedCount - visited);
        Assert.Equal(16, ChartComparisonLayout.EvaluatedCount - evaluated);
    }

    [AvaloniaFact]
    public void Shared_render_and_hit_reuse_geometry_without_source_traversal_and_style_mutations_replace_only_layout()
    {
        var storage = new CountingData(2000); var data = ChartDataSnapshot.FromCompact(0, storage);
        var series = new BarSeries { WidthRatio = .8, ShowMarkers = false };
        var context = Context(series, data, 0, 1, 8);
        var layout = ChartComparisonLayout.For(context, ChartComparisonKind.Bar);
        var position = context.Projection.Project(new(50, 50));
        _ = layout.HitTest(position, 4);
        var originalBuilds = ChartComparisonLayout.BuildCount; storage.ValueReads = 0; storage.MetadataReads = 0;
        using (var drawing = new DrawingGroup().Open()) series.Renderer.Render(drawing, context);
        for (var i = 0; i < 20; i++) _ = series.Renderer.HitTest(context, position, 4);
        Assert.Same(layout, ChartComparisonLayout.For(context, ChartComparisonKind.Bar));
        Assert.Equal(originalBuilds, ChartComparisonLayout.BuildCount);
        Assert.InRange(storage.ValueReads, 0, 20); Assert.Equal(0, storage.MetadataReads);
        series.BarWidth = 30;
        var width = ChartComparisonLayout.For(context, ChartComparisonKind.Bar); Assert.NotSame(layout, width);
        Assert.Same(context.Data, data); Assert.Equal(30, width.Entries[0].Shape.Bounds.Width);
        series.ShowMarkers = true; series.MarkerSize = 40;
        Assert.NotSame(width, ChartComparisonLayout.For(context, ChartComparisonKind.Bar));
        var moved = context with { Projection = Projection(data, true, 5, 105) };
        Assert.NotSame(width, ChartComparisonLayout.For(moved, ChartComparisonKind.Bar));
        var replaced = context with { Data = Data(10, false, ChartComparisonKind.Bar) };
        Assert.NotSame(width, ChartComparisonLayout.For(replaced, ChartComparisonKind.Bar));
    }

    [AvaloniaFact]
    public void Mutable_extension_projection_is_projected_fresh_and_never_reuses_stale_hit_geometry()
    {
        var series = new BarSeries { WidthRatio = .8, ShowMarkers = false };
        var data = new ChartDataSnapshot(0, [new(41, "business", null, null, new(50, 80), false)]);
        var projection = new MutableProjection(); var context = new ChartSeriesRenderContext(series, data, projection, Brushes.Blue, GroupWidth: 12);
        var before = ChartComparisonLayout.For(context, ChartComparisonKind.Bar);
        Assert.NotNull(before.HitTest(new(50, 60), 0));
        projection.Offset = 20;
        var after = ChartComparisonLayout.For(context, ChartComparisonKind.Bar);
        Assert.NotSame(before, after); Assert.Null(after.HitTest(new(50, 60), 0));
        Assert.Equal(41, after.HitTest(new(70, 60), 0)!.SourceIndex);
    }

    [AvaloniaFact]
    public void Legacy_rectangle_helper_preserves_original_fast_and_general_projection_arithmetic_bit_for_bit()
    {
        var series = new BarSeries { ShowMarkers = false, ItemsSource = Enumerable.Range(0, 100).Select(i => new ChartPoint(i, i / 3d)).ToArray() };
        var data = series.CreateSnapshot();
        _ = ChartDataExtrema.For(data);
        var context = Context(series, data, 2, 7, 27.125);
        var projection = Assert.IsType<CartesianChart.CartesianProjection>(context.Projection);
        Assert.True(projection.TryGetLinearProjection(data, out var fast));
        foreach (var point in data.Points.Select(point => point.Point))
        {
            foreach (var screen in new[] { fast.Project(point), context.Projection.Project(point) })
            {
                var width = context.GroupWidth / Math.Max(1, context.GroupCount); var barWidth = Math.Max(.5, width * .86);
                var baseline = Math.Clamp(context.Projection.YOrigin, context.PlotArea.Top, context.PlotArea.Bottom);
                var expected = new Rect(screen.X - context.GroupWidth / 2 + context.GroupIndex * width + (width - barWidth) / 2,
                    Math.Min(screen.Y, baseline), barWidth, Math.Abs(screen.Y - baseline));
                var actual = ChartComparisonLayout.LegacyRectangle(context, screen);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected.X), BitConverter.DoubleToInt64Bits(actual.X));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Y), BitConverter.DoubleToInt64Bits(actual.Y));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Width), BitConverter.DoubleToInt64Bits(actual.Width));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Height), BitConverter.DoubleToInt64Bits(actual.Height));
            }
        }
    }

    [AvaloniaFact]
    public void Compact_markerless_bar_boundary_hits_follow_the_exact_native_fast_paint_layout()
    {
        var series = new BarSeries { ShowMarkers = false, ItemsSource = Enumerable.Range(0, 193)
            .Select(i => new ChartPoint(.1 + i * .37, .1 + i * .23)).ToArray() };
        var data = series.CreateSnapshot(); _ = ChartDataExtrema.For(data);
        var context = Context(series, data, 1, 3, .75);
        Assert.True(((CartesianChart.CartesianProjection)context.Projection).TryGetLinearProjection(data, out _));
        var layout = ChartComparisonLayout.For(context, null);
        foreach (var entry in layout.Entries)
        {
            var painted = OriginalShape(context, null, entry.Ordinal, legacyPaint: true)!.Value;
            Assert.Equal(painted.Bounds, entry.Shape.Bounds);
            foreach (var edge in new[] { painted.Bounds.TopLeft, painted.Bounds.TopRight, painted.Bounds.BottomLeft, painted.Bounds.BottomRight })
            {
                AssertHit(OriginalHit(context, null, edge, 0, legacyPaint: true), layout.HitTest(edge, 0));
                var outside = new Point(Math.BitDecrement(edge.X), Math.BitDecrement(edge.Y));
                AssertHit(OriginalHit(context, null, outside, 0, legacyPaint: true), layout.HitTest(outside, 0));
            }
        }
    }

    [AvaloniaFact]
    public void Logarithmic_multi_axis_grouped_stacked_rectangles_hit_their_own_original_records()
    {
        var first = new BarSeries { ItemsSource = new[] { new Row("first", 1, 2) }, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", StackMode = ChartStackMode.Stacked };
        var secondRows = new[] { new Row("second", 1, 3) };
        var second = new BarSeries { ItemsSource = secondRows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", StackMode = ChartStackMode.Stacked };
        var thirdRows = new[] { new Row("right-axis", 2, 1000) };
        var third = new BarSeries { ItemsSource = thirdRows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", YAxisKey = "right", WidthRatio = .75 };
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 3 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 10 } };
        chart.Axes.Add(new LogarithmicAxis { Key = "right", Minimum = 1, Maximum = 10000, Position = ChartAxisPosition.Far });
        chart.Series.Add(first); chart.Series.Add(second); chart.Series.Add(third);
        using var host = new TemplateFeaturesHost(chart);
        Assert.True(chart.NavigateToPoint(second, 0)); var stacked = chart.CurrentPoint!;
        var stackHit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(stacked.Position - new Vector(0, 3), 4));
        Assert.Same(secondRows[0], stackHit.Item); Assert.Equal("second", stackHit.StableId); Assert.Equal(2, stackHit.BaseValue); Assert.Equal(3, stackHit.StackValue);
        Assert.True(chart.NavigateToPoint(third, 0)); var logarithmic = chart.CurrentPoint!;
        var logHit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(logarithmic.Position + new Vector(0, 3), 4));
        Assert.Same(thirdRows[0], logHit.Item); Assert.Equal("right-axis", logHit.StableId); Assert.Equal(new ChartPoint(2, 1000), logHit.Point);
    }

    [AvaloniaTheory]
    [InlineData("lollipop", false, 28, 1196)]
    [InlineData("lollipop", true, 35, 1245)]
    [InlineData("variance", true, 98, 1686)]
    public void Zero_distance_endpoint_marker_preserves_historical_stem_round_trip_and_original_identity(string name, bool horizontal, int queryOrdinal, int expectedSource)
    {
        var (series, kind) = Series(name, horizontal);
        series.ShowMarkers = true; series.MarkerSize = 9; series.MarkerShape = ChartMarkerShape.Diamond;
        var context = Context(series, Data(193, horizontal, kind), 1, 3, 33, inverse: horizontal);
        var position = OriginalShape(context, kind, queryOrdinal)!.Value.End;
        var expected = OriginalHit(context, kind, position, 0);
        Assert.NotNull(expected); Assert.Equal(expectedSource, expected.SourceIndex); Assert.Equal(0, expected.DistanceSquared);
        AssertHit(expected, ChartComparisonLayout.For(context, kind).HitTest(position, 0));
    }

    private sealed record Row(string Id, double X, double Y);
    private static (ChartSeries Series, ChartComparisonKind? Kind) Series(string name, bool horizontal)
    {
        ChartSeries series = name switch
        {
            "legacy" => new BarSeries(), "bar" => new BarSeries { WidthRatio = .78 }, "range" => new RangeBarSeries(),
            "waterfall" => new WaterfallSeries(), "dot" => new DotSeries(), "lollipop" => new LollipopSeries(),
            "variance" => new VarianceSeries(), _ => new ParetoSeries(),
        };
        if (series is ComparisonSeries comparison) { comparison.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical; comparison.Baseline = -15; }
        if (series is BarSeries bar && name != "legacy") { bar.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical; bar.Baseline = -15; }
        return (series, name switch { "legacy" => null, "bar" => ChartComparisonKind.Bar, "range" => ChartComparisonKind.Range,
            "waterfall" => ChartComparisonKind.Waterfall, "dot" => ChartComparisonKind.Dot, "lollipop" => ChartComparisonKind.Lollipop,
            "variance" => ChartComparisonKind.Variance, _ => ChartComparisonKind.Pareto });
    }

    private static ChartDataSnapshot Data(int count, bool horizontal, ChartComparisonKind? kind)
    {
        var random = new Random(404_011);
        var rows = Enumerable.Range(0, count).Select(i =>
        {
            var category = i % 7 == 0 ? 50 : random.NextDouble() * 140 - 20;
            var value = i % 29 == 0 ? double.NaN : random.NextDouble() * 180 - 40;
            return new ChartDataPoint(1000 + i * 7, $"source-{i}", new Row($"source-{i}", category, value), category,
                horizontal ? new(value, category) : new(category, value), !double.IsFinite(value), High: value, Low: value - 20);
        }).ToArray();
        var data = new ChartDataSnapshot(0, rows);
        if (kind is ChartComparisonKind.Range or ChartComparisonKind.Waterfall or ChartComparisonKind.Variance)
        {
            var values = rows.Select(row => new ChartDataValue(row.SourceIndex, row.Point, row.IsEmpty, row.WasEmpty)).ToArray();
            var bands = rows.Select(row => new ChartBandValue((horizontal ? row.Point.X : row.Point.Y) - 20, null, row.Point)).ToArray();
            data = data.WithBandValues(values, bands);
        }
        return data;
    }

    private static ChartSeriesRenderContext Context(ChartSeries series, ChartDataSnapshot data, int groupIndex = 0, int groupCount = 1,
        double groupWidth = 12, bool inverse = false) => new(series, data, Projection(data, inverse), Brushes.Blue, groupIndex, groupCount, groupWidth);
    private static IChartProjection Projection(ChartDataSnapshot data, bool inverse, double minimum = 0, double maximum = 100) =>
        new CartesianChart.CartesianProjection(new(17.25, 23.125, 700.375, 420.625), ChartScale.CreateForView(new NumericAxis { IsInversed = inverse }, minimum, maximum),
            ChartScale.CreateForView(new NumericAxis { IsInversed = inverse }, 0, 100), data);

    private static (Rect Bounds, Point Start, Point End, bool Horizontal)? OriginalShape(ChartSeriesRenderContext context, ChartComparisonKind? kind, int ordinal, bool legacyPaint = false)
    {
        var value = context.Data.GetValue(ordinal); var point = value.Point;
        if (kind is not null && value.IsEmpty || !double.IsFinite(point.X) || !double.IsFinite(point.Y)) return null;
        var options = (IChartComparisonSeries)context.Series; var horizontal = kind is not null && options.Orientation == Orientation.Horizontal;
        var screen = legacyPaint ? OriginalFixedDomainPaintPoint(context.PlotArea, point) : context.Projection.Project(point);
        if (!double.IsFinite(screen.X) || !double.IsFinite(screen.Y)) return null;
        double start, center, width;
        if (kind is null)
        {
            var slot = context.GroupWidth / Math.Max(1, context.GroupCount); width = Math.Max(.5, slot * .86);
            start = context.Projection.YOrigin; if (!double.IsFinite(start)) start = context.PlotArea.Bottom;
            start = Math.Clamp(start, context.PlotArea.Top, context.PlotArea.Bottom);
            var left = screen.X - context.GroupWidth / 2 + context.GroupIndex * slot + (slot - width) / 2;
            var rect = new Rect(left, Math.Min(screen.Y, start), width, Math.Abs(screen.Y - start));
            return (rect, new(rect.Center.X, start), new(rect.Center.X, screen.Y), false);
        }
        var baseline = context.Data.GetBandLower(ordinal) ?? options.Baseline;
        if (context.Data.GetBandLower(ordinal) is null && kind is ChartComparisonKind.Range or ChartComparisonKind.Variance)
            baseline = context.Data.Points[ordinal].Low ?? double.NaN;
        start = horizontal ? context.Projection.MapX(baseline) : context.Projection.MapY(baseline);
        if (kind == ChartComparisonKind.Dot && !double.IsFinite(start)) start = horizontal ? screen.X : screen.Y;
        if (!double.IsFinite(start)) return null;
        var group = double.IsFinite(context.GroupWidth) ? Math.Abs(context.GroupWidth) : 0; var groupSlot = group / Math.Max(1, context.GroupCount);
        width = options.BarWidth ?? Math.Max(.5, groupSlot * options.WidthRatio);
        center = (horizontal ? screen.Y : screen.X) - group / 2 + context.GroupIndex * groupSlot + groupSlot / 2;
        var end = horizontal ? screen.X : screen.Y;
        return horizontal ? (new Rect(Math.Min(start, end), center - width / 2, Math.Abs(end - start), width), new(start, center), new(end, center), true)
            : (new Rect(center - width / 2, Math.Min(start, end), width, Math.Abs(end - start)), new(center, start), new(center, end), false);
    }

    private static ChartSeriesHit? OriginalHit(ChartSeriesRenderContext context, ChartComparisonKind? kind, Point position, double tolerance, bool legacyPaint = false)
    {
        var best = double.PositiveInfinity; var ordinal = -1; var anchor = default(Point);
        var rectangle = kind is null or ChartComparisonKind.Bar or ChartComparisonKind.Range or ChartComparisonKind.Waterfall or ChartComparisonKind.Pareto;
        var stem = kind is ChartComparisonKind.Lollipop or ChartComparisonKind.Variance;
        var series = context.Series; var radius = ChartMarkerGeometry.HitRadius(series);
        for (var i = 0; i < context.Data.Count; i++)
        {
            if (OriginalShape(context, kind, i, legacyPaint) is not { } shape) continue;
            var distance = double.PositiveInfinity; var selected = shape.End;
            if (rectangle)
            {
                var body = shape.Bounds.Intersect(context.PlotArea);
                if (body.Width > 0 && body.Height > 0)
                {
                    var dx = Math.Max(0, Math.Max(body.Left - position.X, position.X - body.Right));
                    var dy = Math.Max(0, Math.Max(body.Top - position.Y, position.Y - body.Bottom));
                    var candidate = dx * dx + dy * dy;
                    if (candidate <= tolerance * tolerance)
                    {
                        distance = candidate; selected = shape.Horizontal ? new(Math.Clamp(shape.End.X, body.Left, body.Right), body.Center.Y)
                            : new(body.Center.X, Math.Clamp(shape.End.Y, body.Top, body.Bottom));
                    }
                }
            }
            if (stem && series.StrokeThickness > 0)
            {
                // The historical contract includes clipping and dot/fraction
                // interpolation rounding. Replacing this with an axis-aligned
                // Clamp invents zero distances at last-ULP endpoint boundaries
                // and can change which original row wins an exact-distance tie.
                var a = shape.Start; var b = shape.End;
                if (ClipOriginalStem(context.PlotArea, ref a, ref b))
                {
                    var dx = b.X - a.X; var dy = b.Y - a.Y;
                    var lengthSquared = dx * dx + dy * dy;
                    var fraction = lengthSquared > 0 ? Math.Clamp(((position.X - a.X) * dx + (position.Y - a.Y) * dy) / lengthSquared, 0, 1) : 0;
                    var nearest = new Point(a.X + dx * fraction, a.Y + dy * fraction);
                    var qx = nearest.X - position.X; var qy = nearest.Y - position.Y;
                    var candidate = qx * qx + qy * qy;
                    var extent = Math.Max(tolerance, series.StrokeThickness / 2);
                    if (candidate <= extent * extent && candidate < distance) { distance = candidate; selected = nearest; }
                }
            }
            if (series.ShowMarkers && ChartMarkerGeometry.Radius(series) > 0)
            { Marker(shape.End); if (kind == ChartComparisonKind.Variance) Marker(shape.Start); }
            if (!double.IsFinite(distance) || distance > best) continue;
            best = distance; ordinal = i; anchor = selected;
            void Marker(Point center)
            {
                if (!context.PlotArea.Inflate(radius).Contains(center)) return;
                var delta = center - position; var candidate = delta.X * delta.X + delta.Y * delta.Y;
                if (candidate <= Math.Pow(Math.Max(tolerance, radius), 2) && candidate < distance) { distance = candidate; selected = center; }
            }
        }
        return ordinal < 0 ? null : new(context.Data.GetValue(ordinal).SourceIndex, anchor, best);
    }

    private static void AssertHit(ChartSeriesHit? expected, ChartSeriesHit? actual)
    {
        if (expected is null) { Assert.Null(actual); return; }
        Assert.NotNull(actual); Assert.Equal(expected.SourceIndex, actual.SourceIndex);
        Assert.Equal(expected.DistanceSquared, actual.DistanceSquared, 8);
        Assert.Equal(expected.Position.X, actual.Position.X, 8); Assert.Equal(expected.Position.Y, actual.Position.Y, 8);
    }

    private static bool ClipOriginalStem(Rect plot, ref Point a, ref Point b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var enter = 0d; var exit = 1d;
        if (!Clip(-dx, a.X - plot.Left) || !Clip(dx, plot.Right - a.X) ||
            !Clip(-dy, a.Y - plot.Top) || !Clip(dy, plot.Bottom - a.Y)) return false;
        var x = a.X; var y = a.Y;
        a = new(x + enter * dx, y + enter * dy); b = new(x + exit * dx, y + exit * dy);
        return true;
        bool Clip(double slope, double distance)
        {
            if (slope == 0) return distance >= 0;
            var fraction = distance / slope;
            if (slope < 0) { if (fraction > exit) return false; enter = Math.Max(enter, fraction); }
            else { if (fraction < enter) return false; exit = Math.Min(exit, fraction); }
            return true;
        }
    }

    private static Point OriginalFixedDomainPaintPoint(Rect plot, ChartPoint point)
    {
        // The dedicated compact fixture has numeric [0,100] axes and no inversion.
        // This independent endpoint/division/FMA oracle calls no chart projection,
        // mapping helper or layout implementation.
        return new(Map(point.X, plot.Left, plot.Right), Map(point.Y, plot.Bottom, plot.Top));
        static double Map(double value, double start, double end) => value == 0 ? start : value == 100 ? end
            : Math.FusedMultiplyAdd(end - start, value / 100, start);
    }

    private sealed class CountingData(int count) : IChartSnapshotData
    {
        internal int ValueReads, MetadataReads;
        public int Count => count;
        public bool HasOrdinalSourceIndices => true;
        public ReadOnlyMemory<ChartPoint> Coordinates => ReadOnlyMemory<ChartPoint>.Empty;
        public ChartDataValue GetValue(int index) { ValueReads++; return new(index, new(index % 100, 20 + index % 70), false, false); }
        public ChartDataPoint GetPoint(int index) { MetadataReads++; return new(index, index, null, null, new(index % 100, 20 + index % 70), false); }
        public bool TryFindIdentity(object identity, bool valueEquality, out int ordinal) { ordinal = -1; return false; }
    }
    private sealed class MutableProjection : IChartProjection
    {
        internal double Offset;
        public Rect PlotArea => new(0, 0, 100, 100);
        public Point Project(ChartPoint point) => new(point.X + Offset, 100 - point.Y);
        public ChartPoint Unproject(Point point) => new(point.X - Offset, 100 - point.Y);
        public double MapX(double value) => value + Offset;
        public double MapY(double value) => 100 - value;
        public double YOrigin => 100;
    }
}

[CollectionDefinition("Comparison hit layout diagnostics", DisableParallelization = true)]
public sealed class ComparisonHitLayoutDiagnosticsCollection;
