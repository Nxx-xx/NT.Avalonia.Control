using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using NT.AvaloniaControl.Charts;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Charts.Headless.Tests;

public sealed class InRangeLinearProjectionTests
{
    private static readonly Rect Plot = new(30.125, 40.375, 600.625, 240.875);

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Proven_projection_keeps_general_endpoint_division_and_fma_bits(bool inverseX, bool inverseY)
    {
        foreach (var (minimum, maximum) in new[]
        {
            (0d, 1d), (-7.25, 13.5), (-1e300, 1e300),
            (1e300, Math.BitIncrement(Math.BitIncrement(1e300))),
            (double.Epsilon, double.Epsilon * 32),
        })
        {
            var probes = new[] { minimum, Math.BitIncrement(minimum),
                minimum + (maximum - minimum) * .375, Math.BitDecrement(maximum), maximum }
                .Where(value => value >= minimum && value <= maximum).ToArray();
            var points = probes.Select((value, index) => new ChartPoint(value, probes[probes.Length - 1 - index])).ToArray();
            var series = new LineSeries { ItemsSource = points, MarkerSize = 0 };
            var data = series.CreateSnapshot();
            var x = ChartScale.CreateForView(new NumericAxis { IsInversed = inverseX }, minimum, maximum);
            var y = ChartScale.CreateForView(new ChartAxis { IsInversed = inverseY }, minimum, maximum);
            ChartDataIndex.For(data);
            var general = new CartesianChart.CartesianProjection(Plot, x, y);
            var candidate = new CartesianChart.CartesianProjection(Plot, x, y, data);
            Assert.Null(general.LinearProjection);
            Assert.True(candidate.TryGetLinearProjection(data, out var linear));
            Assert.False(candidate.TryGetGappedLinearProjection(data, out _));
            foreach (var point in points) AssertPointBits(general.Project(point), linear.Project(point));
        }
    }

    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(8_191)]
    [InlineData(8_192)]
    [InlineData(8_193)]
    public void Production_figure_keeps_every_original_point_in_order_across_entry_sizes(int count)
    {
        var points = Enumerable.Range(0, count)
            .Select(index => new ChartPoint((long)index * 37 % count, index % 17 - 8)).ToArray();
        var series = new LineSeries { ItemsSource = points, MarkerSize = 0, Stroke = Brushes.Red };
        var data = series.CreateSnapshot();
        ChartDataIndex.For(data);
        var x = ChartScale.CreateForView(new NumericAxis(), -1, count);
        var y = ChartScale.CreateForView(new NumericAxis { IsInversed = true }, -9, 9);
        var general = new CartesianChart.CartesianProjection(Plot, x, y);
        var candidate = new CartesianChart.CartesianProjection(Plot, x, y, data);
        Assert.True(candidate.TryGetLinearProjection(data, out var linear));
        var recorder = new RecordingSink([]);
        var segments = CartesianSeriesRenderer.RenderInRangeLinearLineCore(recorder, data.Coordinates.Span, linear);
        Assert.Equal(count - 1, segments);
        Assert.Equal(count + 1, recorder.Commands.Count);
        Assert.Equal("Begin", recorder.Commands[0].Kind);
        Assert.False(recorder.Commands[0].Flag);
        AssertPointBits(general.Project(points[0]), recorder.Commands[0].Position);
        for (var index = 1; index < count; index++)
        {
            Assert.Equal("Line", recorder.Commands[index].Kind);
            Assert.True(recorder.Commands[index].Flag);
            AssertPointBits(general.Project(points[index]), recorder.Commands[index].Position);
        }
        Assert.Equal("End", recorder.Commands[^1].Kind);
        Assert.False(recorder.Commands[^1].Flag);

        // Exercise real renderer dispatch as well as the recorded production loop.
        var actual = Draw(new(series, data, candidate, Brushes.Red));
        var expected = Draw(new(series, data, new ForwardProjection(general), Brushes.Red));
        AssertDrawingGeometry(expected, actual);
        if (count == 1) Assert.Empty(actual);
        else Assert.Single(actual);
    }

    [AvaloniaFact]
    public void Cold_proof_never_builds_an_index_and_replaced_data_cannot_use_an_old_proof()
    {
        var series = new LineSeries
        {
            MarkerSize = 0, ItemsSource = new ChartPoint[] { new(0, 0), new(1, 1), new(2, 2) },
        };
        var data = series.CreateSnapshot();
        var x = ChartScale.CreateForView(new NumericAxis(), 0, 2);
        var y = ChartScale.CreateForView(new NumericAxis(), 0, 2);
        Assert.False(ChartDataIndex.TryGetExisting(data, out _));
        var cold = new CartesianChart.CartesianProjection(Plot, x, y, data);
        Assert.Null(cold.LinearProjection);
        Assert.False(cold.TryGetGappedLinearProjection(data, out _));
        Assert.False(ChartDataIndex.TryGetExisting(data, out _));
        ChartDataIndex.For(data);
        var candidate = new CartesianChart.CartesianProjection(Plot, x, y, data);
        Assert.True(candidate.TryGetLinearProjection(data, out _));
        Assert.Single(Draw(new(series, data, candidate, Brushes.Red)));

        var replacement = new LineSeries
        {
            MarkerSize = 0, ItemsSource = new ChartPoint[] { new(0, 0), new(1, double.NaN), new(2, 2) },
        }.CreateSnapshot();
        Assert.False(candidate.TryGetLinearProjection(replacement, out _));
        Assert.False(candidate.TryGetGappedLinearProjection(replacement, out _));
        var context = new ChartSeriesRenderContext(series, data, candidate, Brushes.Red) with { Data = replacement };
        // The replacement's two isolated points have no drawable segment. Reusing
        // the old proof would incorrectly emit one continuous three-point figure.
        Assert.Empty(Draw(context));
        AssertDrawingGeometry(Draw(context with { Projection = new ForwardProjection(candidate) }), Draw(context));
    }

    [AvaloniaTheory]
    [InlineData("empty")]
    [InlineData("all-invalid")]
    [InlineData("numeric-source")]
    [InlineData("mapped-source")]
    [InlineData("overlay")]
    [InlineData("outside-view")]
    [InlineData("break")]
    [InlineData("date")]
    [InlineData("category")]
    [InlineData("log")]
    [InlineData("custom-axis")]
    [InlineData("overflow-span")]
    [InlineData("saturation-boundary")]
    public void Unsupported_data_axis_and_screen_domains_preserve_general_fallback(string scenario)
    {
        ChartPoint[] points = [new(1, 1), new(2, 2), new(3, 3)];
        ChartAxis xAxis = new NumericAxis();
        var minimum = 0d; var maximum = 4d;
        var plot = Plot;
        ChartSeries series = new LineSeries { MarkerSize = 0 };
        switch (scenario)
        {
            case "empty": points = []; break;
            case "all-invalid": points = [new(double.NaN, 1), new(2, double.PositiveInfinity), new(double.NegativeInfinity, 3)]; break;
            case "outside-view": maximum = 2.5; break;
            case "break": xAxis.ScaleBreaks.Add(new ChartScaleBreak { Start = 1.25, End = 1.75 }); break;
            case "date": xAxis = new DateTimeAxis(); break;
            case "category": xAxis = new CategoryAxis(); break;
            case "log": xAxis = new LogarithmicAxis(); minimum = 1; maximum = 10; break;
            case "custom-axis": xAxis = new QuadraticAxis(); break;
            case "overflow-span": minimum = -double.MaxValue; maximum = double.MaxValue; break;
            case "saturation-boundary": plot = new Rect(999_999, 30, 1, 250); break;
        }
        if (scenario == "numeric-source") series.ItemsSource = new double[] { 1, 2, 3 };
        else if (scenario == "mapped-source")
        {
            series.CategoryPath = nameof(Row.X); series.ValuePath = nameof(Row.Y);
            series.ItemsSource = points.Select(point => new Row(point.X, point.Y)).ToArray();
        }
        else series.ItemsSource = points;
        var data = series.CreateSnapshot();
        if (scenario == "overlay")
            data = data.WithValues(Enumerable.Range(0, data.Count).Select(data.GetValue).ToArray());
        ChartDataIndex.For(data);
        var x = ChartScale.CreateForView(xAxis, minimum, maximum);
        var y = ChartScale.CreateForView(new NumericAxis(), 0, 4);
        var candidate = new CartesianChart.CartesianProjection(plot, x, y, data);
        Assert.Null(candidate.LinearProjection);
        Assert.False(candidate.TryGetLinearProjection(data, out _));
        Assert.False(candidate.TryGetGappedLinearProjection(data, out _));
        var general = new CartesianChart.CartesianProjection(plot, x, y);
        var context = new ChartSeriesRenderContext(series, data, candidate, Brushes.Red);
        AssertDrawingGeometry(Draw(context with { Projection = new ForwardProjection(general) }), Draw(context));
    }

    [AvaloniaTheory]
    [InlineData(12, false)]
    [InlineData(12, true)]
    [InlineData(8_191, false)]
    [InlineData(8_191, true)]
    [InlineData(8_192, false)]
    [InlineData(8_192, true)]
    [InlineData(8_193, false)]
    [InlineData(8_193, true)]
    public void Gapped_production_commands_keep_explicit_figures_duplicates_and_original_identities(int count, bool inverse)
    {
        var points = Enumerable.Range(0, count)
            .Select(index => new ChartPoint((long)index * 37 % 11, index % 7 - 3)).ToArray();
        points[0] = new(double.NaN, 0);
        points[1] = new(9, -3); points[2] = new(2, 1);
        points[3] = new(3, double.NaN); points[4] = new(4, double.PositiveInfinity);
        points[5] = new(4, 0); // A valid, isolated figure must still begin and end.
        points[6] = new(double.NegativeInfinity, 2);
        points[7] = new(2, 1); points[8] = points[7]; points[9] = new(7, -1);
        points[10] = new(double.PositiveInfinity, double.NaN);
        points[^1] = new(5, double.NegativeInfinity);
        // This oracle defines each known figure independently of the production
        // finite/gap scan. The long tail also crosses both entry-size boundaries.
        var figures = new List<int[]> { new[] { 1, 2 }, new[] { 5 }, new[] { 7, 8, 9 } };
        if (count > 12) figures.Add(Enumerable.Range(11, count - 12).ToArray());
        var series = new LineSeries
        {
            ItemsSource = points, MarkerSize = count == 12 ? 4 : 0,
            Stroke = Brushes.Red, StrokeThickness = 1.75,
            MarkerFill = Brushes.Blue, MarkerStroke = Brushes.Green, MarkerStrokeThickness = 1.25,
        };
        var data = series.CreateSnapshot();
        ChartDataIndex.For(data);
        var x = ChartScale.CreateForView(new NumericAxis { IsInversed = inverse }, 0, 10);
        var y = ChartScale.CreateForView(new NumericAxis { IsInversed = !inverse }, -3, 3);
        var general = new CartesianChart.CartesianProjection(Plot, x, y);
        var candidate = new CartesianChart.CartesianProjection(Plot, x, y, data);
        Assert.Null(candidate.LinearProjection);
        Assert.False(candidate.TryGetLinearProjection(data, out _));
        Assert.True(candidate.TryGetGappedLinearProjection(data, out var linear));
        var otherData = new LineSeries { ItemsSource = points }.CreateSnapshot();
        Assert.False(candidate.TryGetGappedLinearProjection(otherData, out _));

        var expectedCommands = new List<FigureCommand>();
        foreach (var figure in figures)
        {
            expectedCommands.Add(new("Begin", general.Project(points[figure[0]]), false));
            expectedCommands.AddRange(figure.Skip(1).Select(index => new FigureCommand("Line", general.Project(points[index]), true)));
            expectedCommands.Add(new("End", default, false));
        }
        var recorder = new RecordingSink([]);
        var segments = CartesianSeriesRenderer.RenderInRangeGappedLineCore(recorder, data.Coordinates.Span, linear);
        Assert.Equal(figures.Sum(figure => figure.Length - 1), segments);
        Assert.Equal(expectedCommands.Count, recorder.Commands.Count);
        foreach (var (expected, actual) in expectedCommands.Zip(recorder.Commands))
        {
            Assert.Equal(expected.Kind, actual.Kind); Assert.Equal(expected.Flag, actual.Flag);
            AssertPointBits(expected.Position, actual.Position);
        }

        var context = new ChartSeriesRenderContext(series, data, candidate, Brushes.Red);
        var fallback = context with { Projection = new ForwardProjection(general) };
        var drawings = Draw(context);
        AssertDrawingGeometry(Draw(fallback), drawings);
        Assert.Same(Brushes.Red, drawings[0].Pen!.Brush);
        Assert.Equal(1.75, drawings[0].Pen!.Thickness);
        if (count == 12)
        {
            Assert.Equal(7, drawings.Length); // One path plus six finite markers.
            Assert.All(drawings.Skip(1), drawing => Assert.Same(Brushes.Blue, drawing.Brush));
            var duplicatePosition = general.Project(points[8]);
            var hit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, duplicatePosition, 0));
            var expectedHit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(fallback, duplicatePosition, 0));
            Assert.Equal(expectedHit.SourceIndex, hit.SourceIndex);
            Assert.Equal(8, hit.SourceIndex);
            AssertPointBits(expectedHit.Position, hit.Position);
            Assert.True(data.TryGetPoint(hit.SourceIndex, out var original));
            Assert.Same(data.Points[8].StableId, original.StableId);
            Assert.Equal(points[8], original.Point);
        }
    }

    [AvaloniaTheory]
    [InlineData(false, false, "mixed")]
    [InlineData(false, true, "mixed")]
    [InlineData(true, false, "mixed")]
    [InlineData(true, true, "mixed")]
    [InlineData(false, false, "positive")]
    [InlineData(false, true, "positive")]
    [InlineData(true, false, "positive")]
    [InlineData(true, true, "positive")]
    [InlineData(false, false, "negative")]
    [InlineData(false, true, "negative")]
    [InlineData(true, false, "negative")]
    [InlineData(true, true, "negative")]
    public void Proven_grouped_bars_keep_general_rectangles_and_clamped_external_baseline(bool inverseX, bool inverseY, string range)
    {
        var (minimum, maximum) = range switch { "positive" => (1d, 5d), "negative" => (-5d, -1d), _ => (-2d, 4d) };
        ChartPoint[] points = [new(0, minimum), new(1.75, maximum), new(1.75, maximum), new(3, (minimum + maximum) / 2)];
        var series = new BarSeries { ItemsSource = points, ShowMarkers = false, Fill = Brushes.Blue };
        var data = series.CreateSnapshot(); ChartDataIndex.For(data);
        var x = ChartScale.CreateForView(new NumericAxis { IsInversed = inverseX }, 0, 3);
        var y = ChartScale.CreateForView(new NumericAxis { IsInversed = inverseY }, minimum, maximum);
        var general = new CartesianChart.CartesianProjection(Plot, x, y);
        var candidate = new CartesianChart.CartesianProjection(Plot, x, y, data);
        Assert.True(candidate.TryGetLinearProjection(data, out _));
        var context = new ChartSeriesRenderContext(series, data, candidate, Brushes.Red, 2, 3, 71.3);
        var fallback = context with { Projection = new ForwardProjection(general) };
        var actual = Draw(context);
        Assert.NotEmpty(actual);
        AssertDrawingGeometry(Draw(fallback), actual);
        Assert.All(actual, drawing => { Assert.Same(Brushes.Blue, drawing.Brush); Assert.Null(drawing.Pen); });
        if (range != "mixed") Assert.True(general.YOrigin < Plot.Top || general.YOrigin > Plot.Bottom);
        var location = general.Project(points[2]);
        var expectedHit = series.Renderer.HitTest(fallback, location, 40);
        var actualHit = series.Renderer.HitTest(context, location, 40);
        Assert.Equal(expectedHit, actualHit);
        if (actualHit is { } hit)
        {
            Assert.True(data.TryGetPoint(hit.SourceIndex, out var original));
            Assert.Same(data.Points[hit.SourceIndex].StableId, original.StableId);
        }
    }

    [AvaloniaTheory]
    [InlineData(71.3, 3, 2)]
    [InlineData(-71.3, 3, 2)]
    [InlineData(0d, 1, 0)]
    [InlineData(double.Epsilon, 0, -1)]
    [InlineData(1_000d, 3, -1)]
    [InlineData(1d, int.MaxValue, int.MaxValue)]
    [InlineData(double.NaN, 3, 1)]
    [InlineData(double.PositiveInfinity, 3, 2)]
    public void Proven_bar_group_parameter_edges_keep_per_primitive_general_bits(double groupWidth, int groupCount, int groupIndex)
    {
        var series = new BarSeries { ItemsSource = new ChartPoint[] { new(0, -1), new(1, 2), new(2, 1) }, ShowMarkers = false, Fill = Brushes.Blue };
        var data = series.CreateSnapshot(); ChartDataIndex.For(data);
        var x = ChartScale.CreateForView(new NumericAxis(), 0, 2);
        var y = ChartScale.CreateForView(new NumericAxis { IsInversed = true }, -1, 2);
        var general = new CartesianChart.CartesianProjection(Plot, x, y);
        var candidate = new CartesianChart.CartesianProjection(Plot, x, y, data);
        Assert.True(candidate.TryGetLinearProjection(data, out _));
        var context = new ChartSeriesRenderContext(series, data, candidate, Brushes.Red, groupIndex, groupCount, groupWidth);
        var actual = Draw(context);
        AssertDrawingGeometry(Draw(context with { Projection = new ForwardProjection(general) }), actual);
        if (double.IsFinite(groupWidth)) Assert.NotEmpty(actual);
        else Assert.Empty(actual);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Qualifying_bars_with_markers_keep_generic_marker_paint_and_anchor_hits(bool inverse)
    {
        ChartPoint[] points = [new(0, -1), new(1, 2), new(1, 2), new(2, 1)];
        var series = new BarSeries
        {
            ItemsSource = points, ShowMarkers = true, MarkerSize = 4, Fill = Brushes.Blue,
            MarkerShape = ChartMarkerShape.Circle, MarkerFill = Brushes.Yellow,
            MarkerStroke = Brushes.Green, MarkerStrokeThickness = 1.25,
        };
        var data = series.CreateSnapshot(); ChartDataIndex.For(data);
        var x = ChartScale.CreateForView(new NumericAxis { IsInversed = inverse }, 0, 2);
        var y = ChartScale.CreateForView(new NumericAxis { IsInversed = inverse }, -1, 2);
        var general = new CartesianChart.CartesianProjection(Plot, x, y);
        var candidate = new CartesianChart.CartesianProjection(Plot, x, y, data);
        Assert.True(candidate.TryGetLinearProjection(data, out _));
        var context = new ChartSeriesRenderContext(series, data, candidate, Brushes.Red, 2, 3, 71.3);
        var fallback = context with { Projection = new ForwardProjection(general) };
        var actual = Draw(context);
        AssertDrawingGeometry(Draw(fallback), actual);
        var markers = actual.Where(drawing => ReferenceEquals(drawing.Brush, Brushes.Yellow)).ToArray();
        Assert.NotEmpty(markers); // Entering the marker-free fast body would lose these.
        Assert.All(markers, marker => { Assert.Same(Brushes.Green, marker.Pen!.Brush); Assert.Equal(1.25, marker.Pen.Thickness); });
        var full = CartesianSeriesRenderer.BarRectangle(fallback, points[2]);
        var anchor = new Point(full.Center.X, general.Project(points[2]).Y);
        var expected = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(fallback, anchor, 0));
        var hit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, anchor, 0));
        Assert.Equal(expected, hit);
        Assert.Equal(2, hit.SourceIndex);
        Assert.True(data.TryGetPoint(hit.SourceIndex, out var original));
        Assert.Same(data.Points[2].StableId, original.StableId);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Qualified_and_metadata_fallback_keep_marker_order_style_and_hit_identity(bool nonordinalMetadata)
    {
        ChartPoint[] points = [new(0, -1), new(10, 1), new(3, -.5), new(3, -.5), new(7, .5)];
        var series = new LineSeries
        {
            ItemsSource = points, MarkerSize = 4, MarkerShape = ChartMarkerShape.Circle,
            Stroke = Brushes.Red, StrokeThickness = 1.75,
            MarkerFill = Brushes.Blue, MarkerStroke = Brushes.Green, MarkerStrokeThickness = 1.25,
        };
        var data = series.CreateSnapshot();
        if (nonordinalMetadata)
            data = new ChartDataSnapshot(data.Version, points.Select((point, ordinal) =>
                new ChartDataPoint(100 - ordinal * 17, new object(), new Row(point.X, point.Y), null, point, false)).ToArray());
        var identities = data.Points.Select(point => point.StableId).ToArray();
        ChartDataIndex.For(data);
        var x = ChartScale.CreateForView(new NumericAxis(), 0, 10);
        var y = ChartScale.CreateForView(new NumericAxis { IsInversed = true }, -1, 1);
        var general = new CartesianChart.CartesianProjection(Plot, x, y);
        var candidate = new CartesianChart.CartesianProjection(Plot, x, y, data);
        Assert.Equal(!nonordinalMetadata, candidate.TryGetLinearProjection(data, out _));
        var context = new ChartSeriesRenderContext(series, data, candidate, Brushes.Red);
        var fallback = context with { Projection = new ForwardProjection(general) };
        var expectedDrawings = Draw(fallback);
        var actualDrawings = Draw(context);
        AssertDrawingGeometry(expectedDrawings, actualDrawings);
        Assert.Equal(points.Length + 1, actualDrawings.Length);
        for (var index = 0; index < points.Length; index++)
        {
            var marker = actualDrawings[index + 1];
            Assert.Same(Brushes.Blue, marker.Brush);
            Assert.Same(Brushes.Green, marker.Pen!.Brush);
            Assert.Equal(1.25, marker.Pen.Thickness);
            var center = marker.Geometry!.Bounds.Center;
            var expectedCenter = general.Project(points[index]);
            // Bounds.Center reconstructs the center from left/width, which can
            // add one rounding step; projection bits are checked independently.
            Assert.InRange(Math.Abs(center.X - expectedCenter.X), 0, 1e-10);
            Assert.InRange(Math.Abs(center.Y - expectedCenter.Y), 0, 1e-10);
        }
        foreach (var point in points)
            foreach (var offset in new[] { default(Vector), new Vector(1.75, -.5) })
            {
                var position = general.Project(point) + offset;
                var expected = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(fallback, position, 0));
                var actual = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, position, 0));
                Assert.Equal(expected.SourceIndex, actual.SourceIndex);
                AssertPointBits(expected.Position, actual.Position);
                AssertBits(expected.DistanceSquared, actual.DistanceSquared);
                Assert.True(data.TryGetPoint(actual.SourceIndex, out var original));
                var physicalOrdinal = Array.FindLastIndex(points, item => item == original.Point);
                Assert.Same(identities[physicalOrdinal], original.StableId);
            }
        var duplicateHit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, general.Project(points[3]), 0));
        Assert.Equal(nonordinalMetadata ? 49 : 3, duplicateHit.SourceIndex);
    }

    [AvaloniaTheory]
    [InlineData(12, false, false)]
    [InlineData(12, false, true)]
    [InlineData(12, true, false)]
    [InlineData(12, true, true)]
    [InlineData(8_193, false, false)]
    [InlineData(8_193, false, true)]
    [InlineData(8_193, true, false)]
    [InlineData(8_193, true, true)]
    public void Proven_scatter_circles_keep_each_general_ellipse_style_order_and_original_hit(int count, bool inverseX, bool inverseY)
    {
        var points = Enumerable.Range(0, count)
            .Select(index => new ChartPoint((long)index * 37 % 997 / 100d, (index % 17 - 8) / 4d)).ToArray();
        points[0] = new(0, -2); points[1] = new(10, 2);
        points[2] = new(3.123456789, -.654321); points[3] = points[2];
        points[^1] = new(4.375, .75);
        var series = new ScatterSeries
        {
            ItemsSource = points, MarkerShape = ChartMarkerShape.Circle, MarkerSize = 4,
            MarkerFill = Brushes.Blue, MarkerStroke = Brushes.Green, MarkerStrokeThickness = 1.25,
        };
        var data = series.CreateSnapshot(); ChartDataIndex.For(data);
        var x = ChartScale.CreateForView(new NumericAxis { IsInversed = inverseX }, 0, 10);
        var y = ChartScale.CreateForView(new NumericAxis { IsInversed = inverseY }, -2, 2);
        var general = new CartesianChart.CartesianProjection(Plot, x, y);
        var candidate = new CartesianChart.CartesianProjection(Plot, x, y, data);
        Assert.True(candidate.TryGetLinearProjection(data, out var linear));
        var context = new ChartSeriesRenderContext(series, data, candidate, Brushes.Red);
        var fallback = context with { Projection = new ForwardProjection(general) };

        // Record a separate, explicit DrawEllipse for every source ordinal using
        // the original public projection. This checks both order and ellipse
        // bounds without assuming the platform records EllipseGeometry objects.
        var ellipseOracle = new DrawingGroup();
        var oraclePen = new Pen(Brushes.Green, 1.25);
        using (var drawing = ellipseOracle.Open())
            foreach (var point in points)
            {
                var center = general.Project(point);
                AssertPointBits(center, linear.Project(point));
                drawing.DrawEllipse(Brushes.Blue, oraclePen, center, 2, 2);
            }
        var actual = Draw(context);
        Assert.Equal(count, actual.Length);
        AssertDrawingGeometry(ellipseOracle.Children.OfType<GeometryDrawing>().ToArray(), actual);
        AssertDrawingGeometry(Draw(fallback), actual);
        Assert.All(actual, marker =>
        {
            Assert.Same(Brushes.Blue, marker.Brush); Assert.Same(Brushes.Green, marker.Pen!.Brush);
            AssertBits(1.25, marker.Pen.Thickness);
        });

        foreach (var ordinal in new[] { 0, 1, 3, count - 1 })
        {
            var original = data.Points[ordinal];
            var position = general.Project(points[ordinal]);
            var expected = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(fallback, position, 0));
            var hit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, position, 0));
            Assert.Equal(ordinal, hit.SourceIndex);
            Assert.Equal(expected.SourceIndex, hit.SourceIndex);
            AssertPointBits(expected.Position, hit.Position); AssertBits(expected.DistanceSquared, hit.DistanceSquared);
            Assert.True(data.TryGetPoint(hit.SourceIndex, out var fromHit));
            Assert.Same(original.StableId, fromHit.StableId);
            Assert.Equal(points[ordinal], fromHit.Point); Assert.Equal(points[ordinal], Assert.IsType<ChartPoint>(fromHit.Item));
        }

        if (count == 12)
        {
            var replacementPoints = points.ToArray();
            replacementPoints[2] = new(2, double.NaN);
            replacementPoints[3] = new(11, 0); // Beyond both the view and inflated marker plot.
            var replacement = new ScatterSeries { ItemsSource = replacementPoints }.CreateSnapshot();
            Assert.False(candidate.TryGetLinearProjection(replacement, out _));
            Assert.False(candidate.TryGetGappedLinearProjection(replacement, out _));
            var replacedContext = context with { Data = replacement };
            var replacementDrawings = Draw(replacedContext);
            Assert.Equal(count - 2, replacementDrawings.Length);
            AssertDrawingGeometry(Draw(replacedContext with { Projection = new ForwardProjection(general) }), replacementDrawings);
        }
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Bounded_outside_view_lines_keep_general_bits_commands_markers_and_hit_identity(bool inverseX, bool inverseY)
    {
        ChartPoint[] points = [new(-5, -4), new(Math.BitDecrement(0), -2), new(0, -2),
            new(Math.BitIncrement(0), -1), new(5, 0), new(Math.BitDecrement(10), 1),
            new(10, 2), new(Math.BitIncrement(10), 2), new(15, 4), new(5, 0)];
        var series = new LineSeries
        {
            ItemsSource = points, MarkerSize = 4, Stroke = Brushes.Red, StrokeThickness = 1.75,
            MarkerFill = Brushes.Blue, MarkerStroke = Brushes.Green, MarkerStrokeThickness = 1.25,
        };
        var data = series.CreateSnapshot(); ChartDataIndex.For(data);
        var x = ChartScale.CreateForView(new NumericAxis { IsInversed = inverseX }, 0, 10);
        var y = ChartScale.CreateForView(new NumericAxis { IsInversed = inverseY }, -2, 2);
        var general = new CartesianChart.CartesianProjection(Plot, x, y);
        var candidate = new CartesianChart.CartesianProjection(Plot, x, y, data);
        Assert.Null(candidate.LinearProjection);
        Assert.False(candidate.TryGetLinearProjection(data, out _));
        Assert.False(candidate.TryGetGappedLinearProjection(data, out _));
        Assert.True(candidate.TryGetLinearLineProjection(data, out var linear));
        AssertRecordedLinearLine(points, general, linear);
        var context = new ChartSeriesRenderContext(series, data, candidate, Brushes.Red);
        var fallback = context with { Projection = new ForwardProjection(general) };
        var drawings = Draw(context);
        AssertDrawingGeometry(Draw(fallback), drawings);
        Assert.Same(Brushes.Red, drawings[0].Pen!.Brush);
        Assert.Equal(1.75, drawings[0].Pen!.Thickness);
        Assert.All(drawings.Skip(1), marker => Assert.Same(Brushes.Blue, marker.Brush));
        foreach (var point in points)
            Assert.Equal(series.Renderer.HitTest(fallback, general.Project(point), 0),
                series.Renderer.HitTest(context, general.Project(point), 0));
        var duplicate = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, general.Project(points[^1]), 0));
        Assert.Equal(points.Length - 1, duplicate.SourceIndex);
        Assert.True(data.TryGetPoint(duplicate.SourceIndex, out var original));
        Assert.Same(data.Points[^1].StableId, original.StableId);
        Assert.Equal(points[^1], Assert.IsType<ChartPoint>(original.Item));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bounded_mapping_includes_only_inert_clamp_envelopes_and_keeps_endpoint_fma_bits(bool inverse)
    {
        var nearLimit = Math.BitDecrement(1_000_000d);
        var cases = new[]
        {
            (Minimum: 0d, Maximum: 10d, DataMinimum: -10d, DataMaximum: 20d, Start: 30.125, End: 630.75),
            (Minimum: 0d, Maximum: 10d, DataMinimum: -10_000_000d, DataMaximum: 10_000_000d, Start: 0d, End: .125),
            (Minimum: 0d, Maximum: 1d, DataMinimum: 0d, DataMaximum: 2d, Start: inverse ? 500_000d : 0d, End: inverse ? 0d : 500_000d),
            (Minimum: 0d, Maximum: 1d, DataMinimum: 0d, DataMaximum: 1d, Start: -nearLimit, End: nearLimit),
        };
        foreach (var item in cases)
        {
            var scale = ChartScale.CreateForView(new NumericAxis { IsInversed = inverse }, item.Minimum, item.Maximum);
            Assert.True(scale.TryCreateBoundedLinearMapping(item.DataMinimum, item.DataMaximum, item.Start, item.End, out var mapping));
            var probes = new[] { item.DataMinimum, Math.BitIncrement(item.DataMinimum),
                item.Minimum, Math.BitIncrement(item.Minimum), Math.BitDecrement(item.Maximum), item.Maximum,
                Math.BitDecrement(item.DataMaximum), item.DataMaximum, (item.DataMinimum + item.DataMaximum) / 2 };
            foreach (var value in probes.Where(value => value >= item.DataMinimum && value <= item.DataMaximum))
            {
                var original = Math.Clamp(scale.Map(value, item.Start, item.End), -1_000_000, 1_000_000);
                AssertBits(original, mapping.Map(value));
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bounded_mapping_rejects_unsupported_domains_raw_clamps_and_unshortened_fma_envelopes(bool inverse)
    {
        foreach (var scenario in new[] { "date", "category", "log", "custom", "break", "nan-bound", "reversed-bounds",
            "overflow-span", "overflow-offset", "overflow-fraction", "negative-fraction-limit", "positive-fraction-limit",
            "positive-screen-limit", "negative-screen-limit", "screen-endpoint-limit", "screen-nonfinite", "endpoint-neighbor-fma" })
        {
            ChartAxis axis = new NumericAxis { IsInversed = inverse };
            var minimum = 0d; var maximum = 10d; var dataMinimum = -1d; var dataMaximum = 11d;
            var start = 30.125; var end = 630.75;
            switch (scenario)
            {
                case "date": axis = new DateTimeAxis(); break;
                case "category": axis = new CategoryAxis(); break;
                case "log": axis = new LogarithmicAxis(); minimum = 1; break;
                case "custom": axis = new QuadraticAxis(); break;
                case "break": axis.ScaleBreaks.Add(new ChartScaleBreak { Start = 3, End = 4 }); break;
                case "nan-bound": dataMinimum = double.NaN; break;
                case "reversed-bounds": dataMinimum = 11; dataMaximum = -1; break;
                case "overflow-span": minimum = -double.MaxValue; maximum = double.MaxValue; break;
                case "overflow-offset": minimum = -double.MaxValue; maximum = -double.MaxValue / 2; dataMinimum = double.MaxValue / 2; dataMaximum = double.MaxValue; break;
                case "overflow-fraction": maximum = double.Epsilon; dataMinimum = 1; dataMaximum = 2; break;
                case "negative-fraction-limit": dataMinimum = Math.BitDecrement(-10_000_000d); dataMaximum = 0; start = 0; end = .125; break;
                case "positive-fraction-limit": dataMinimum = 0; dataMaximum = Math.BitIncrement(10_000_000d); start = 0; end = .125; break;
                case "positive-screen-limit": maximum = 1; dataMinimum = 0; dataMaximum = Math.BitIncrement(2); start = inverse ? 500_000 : 0; end = inverse ? 0 : 500_000; break;
                case "negative-screen-limit": maximum = 1; dataMinimum = Math.BitDecrement(-2); dataMaximum = 0; start = inverse ? 500_000 : 0; end = inverse ? 0 : 500_000; break;
                case "screen-endpoint-limit": start = -1_000_000; break;
                case "screen-nonfinite": end = double.PositiveInfinity; break;
                case "endpoint-neighbor-fma": maximum = 1; dataMinimum = 0; dataMaximum = Math.BitIncrement(1); start = -Math.BitDecrement(1_000_000d); end = Math.BitDecrement(1_000_000d); break;
            }
            axis.IsInversed = inverse;
            var scale = ChartScale.CreateForView(axis, minimum, maximum);
            Assert.False(scale.TryCreateBoundedLinearMapping(dataMinimum, dataMaximum, start, end, out _), scenario);
        }
    }

    [AvaloniaFact]
    public void Bounded_line_proof_still_requires_existing_index_original_snapshot_and_all_finite_data()
    {
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(-1, -1), new(5, 5), new(11, 11) }, MarkerSize = 0 };
        var data = series.CreateSnapshot();
        var scale = ChartScale.CreateForView(new NumericAxis(), 0, 10);
        var cold = new CartesianChart.CartesianProjection(Plot, scale, scale, data);
        Assert.False(cold.TryGetLinearLineProjection(data, out _));
        Assert.False(ChartDataIndex.TryGetExisting(data, out _));
        ChartDataIndex.For(data);
        var candidate = new CartesianChart.CartesianProjection(Plot, scale, scale, data);
        Assert.True(candidate.TryGetLinearLineProjection(data, out _));
        var replacement = new LineSeries { ItemsSource = new ChartPoint[] { new(-1, -1), new(5, double.NaN), new(11, 11) } }.CreateSnapshot();
        Assert.False(candidate.TryGetLinearLineProjection(replacement, out _));
        ChartDataIndex.For(replacement);
        var gappedOutsideView = new CartesianChart.CartesianProjection(Plot, scale, scale, replacement);
        Assert.False(gappedOutsideView.TryGetLinearLineProjection(replacement, out _));
        Assert.False(gappedOutsideView.TryGetGappedLinearProjection(replacement, out _));
        var context = new ChartSeriesRenderContext(series, replacement, candidate, Brushes.Red);
        Assert.Empty(Draw(context)); // Two isolated finite points cannot become one line.
        AssertDrawingGeometry(Draw(context with { Projection = new ForwardProjection(candidate) }), Draw(context));
        var overlay = data.WithValues(Enumerable.Range(0, data.Count).Select(data.GetValue).ToArray());
        ChartDataIndex.For(overlay);
        Assert.False(new CartesianChart.CartesianProjection(Plot, scale, scale, overlay).TryGetLinearLineProjection(overlay, out _));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Realtime_four_series_revisions_keep_the_original_fixed_y_axis_and_all_line_coordinates(bool inverse)
    {
        // Independent fixture with the published S-RT-POC integer generator and
        // seed. The fixed Y bounds intentionally come only from revision zero,
        // as in the unchanged host; later revisions grow outside that view.
        var initial = Enumerable.Range(0, 4).Select(series => RealtimePoints(series, 0)).ToArray();
        var yMinimum = Math.Min(0, initial.SelectMany(points => points).Min(point => point.Y)) - 1;
        var yMaximum = initial.SelectMany(points => points).Max(point => point.Y) + 1;
        var x = ChartScale.CreateForView(new ChartAxis { IsInversed = inverse }, -1, 400);
        var y = ChartScale.CreateForView(new ChartAxis { IsInversed = inverse }, yMinimum, yMaximum);
        for (var revision = 0; revision <= 5; revision++)
            for (var seriesIndex = 0; seriesIndex < 4; seriesIndex++)
            {
                var points = RealtimePoints(seriesIndex, revision);
                var series = new LineSeries { ItemsSource = points, MarkerSize = 0, Stroke = Brushes.Red };
                var data = series.CreateSnapshot(); ChartDataIndex.For(data);
                var general = new CartesianChart.CartesianProjection(Plot, x, y);
                var candidate = new CartesianChart.CartesianProjection(Plot, x, y, data);
                var allInView = points.All(point => point.Y >= yMinimum && point.Y <= yMaximum);
                Assert.Equal(allInView, candidate.TryGetLinearProjection(data, out _));
                if (revision > 0 && seriesIndex == 3) Assert.False(allInView);
                Assert.True(candidate.TryGetLinearLineProjection(data, out var linear));
                AssertRecordedLinearLine(points, general, linear);
                var context = new ChartSeriesRenderContext(series, data, candidate, Brushes.Red);
                AssertDrawingGeometry(Draw(context with { Projection = new ForwardProjection(general) }), Draw(context));
            }
    }

    [AvaloniaTheory]
    [InlineData(8_191)]
    [InlineData(8_192)]
    [InlineData(8_193)]
    public void Both_proven_bar_entry_sizes_keep_each_grouped_rectangle_and_original_source_identity(int count)
    {
        var points = Enumerable.Range(0, count)
            .Select(index => new ChartPoint((long)index * 37 % count, 1 + (index * 13 % 17) / 4d)).ToArray();
        points[^1] = points[count / 2];
        var series = new BarSeries { ItemsSource = points, ShowMarkers = false, Fill = Brushes.Blue };
        var data = series.CreateSnapshot(); ChartDataIndex.For(data);
        var x = ChartScale.CreateForView(new NumericAxis { IsInversed = true }, 0, count - 1);
        var y = ChartScale.CreateForView(new NumericAxis { IsInversed = true }, 1, 5);
        var general = new CartesianChart.CartesianProjection(Plot, x, y);
        var candidate = new CartesianChart.CartesianProjection(Plot, x, y, data);
        Assert.True(candidate.TryGetLinearProjection(data, out _));
        Assert.True(general.YOrigin < Plot.Top);
        var context = new ChartSeriesRenderContext(series, data, candidate, Brushes.Red, 2, 3, 71.3);
        var fallback = context with { Projection = new ForwardProjection(general) };
        var actual = Draw(context);
        // With a positive view, zero is clamped to its lower data edge. Points
        // exactly at that edge have zero height; every other source draws once.
        Assert.Equal(points.Count(point => point.Y > 1), actual.Length);
        AssertDrawingGeometry(Draw(fallback), actual);
        Assert.All(actual, rectangle => { Assert.Same(Brushes.Blue, rectangle.Brush); Assert.Null(rectangle.Pen); });
        foreach (var fraction in new[] { .25, .5, .75 })
        {
            var position = new Point(Plot.Left + Plot.Width * fraction, Plot.Center.Y);
            var expected = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(fallback, position, 0));
            var hit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, position, 0));
            Assert.Equal(expected.SourceIndex, hit.SourceIndex);
            AssertPointBits(expected.Position, hit.Position); AssertBits(expected.DistanceSquared, hit.DistanceSquared);
            var original = data.Points[expected.SourceIndex];
            Assert.True(data.TryGetPoint(hit.SourceIndex, out var fromHit));
            Assert.Same(original.StableId, fromHit.StableId);
            Assert.Equal(points[expected.SourceIndex], fromHit.Point);
            Assert.Equal(points[expected.SourceIndex], Assert.IsType<ChartPoint>(fromHit.Item));
        }
    }

    [AvaloniaFact]
    public void Cached_bar_geometries_reuse_equal_rectangles_without_mutating_older_drawings()
    {
        var series = new BarSeries { Fill = Brushes.Blue, ShowMarkers = false };
        var context = CacheBarContext(series);
        var first = Draw(context);
        var originalBounds = first.Select(drawing => drawing.Geometry!.Bounds).ToArray();
        var originals = Enumerable.Range(0, 3).Select(index => CachedBarGeometry(series, index)).ToArray();
        Assert.All(originals, geometry => Assert.NotNull(geometry));
        var second = Draw(context);
        for (var index = 0; index < 3; index++) Assert.Same(originals[index], CachedBarGeometry(series, index));
        AssertDrawingGeometry(Draw(OracleBarContext(context)), second);

        // The public recording wrapper may be modified by an export consumer;
        // that must not expose or poison the renderer's private source geometry.
        Assert.NotSame(originals[0], second[0].Geometry);
        second[0].Geometry!.Transform = new TranslateTransform(71.25, -19.5);
        AssertDrawingGeometry(Draw(OracleBarContext(context)), Draw(context));
        AssertRectBits(originals[0]!.Rect, originalBounds[0]);

        var changed = CacheBarContext(series, [new(1, 2), new(5.5, 7), new(9, 9)]);
        var updated = Draw(changed);
        Assert.Same(originals[0], CachedBarGeometry(series, 0));
        Assert.NotSame(originals[1], CachedBarGeometry(series, 1));
        Assert.Same(originals[2], CachedBarGeometry(series, 2));
        AssertDrawingGeometry(Draw(OracleBarContext(changed)), updated);
        for (var index = 0; index < first.Length; index++) AssertRectBits(originalBounds[index], first[index].Geometry!.Bounds);
    }

    [AvaloniaFact]
    public void Cached_bars_refresh_for_view_baseline_group_and_new_source_without_changing_old_frames()
    {
        var series = new BarSeries { Fill = Brushes.Blue, ShowMarkers = false };
        var firstContext = CacheBarContext(series);
        var firstDrawing = Draw(firstContext);
        var firstBounds = firstDrawing.Select(drawing => drawing.Geometry!.Bounds).ToArray();
        var firstGeometry = CachedBarGeometry(series, 1);
        var view = CacheBarContext(series, yMinimum: 1, yMaximum: 20, plot: new Rect(42.5, 31.25, 410.75, 210.5));
        AssertDrawingGeometry(Draw(OracleBarContext(view)), Draw(view));
        Assert.NotSame(firstGeometry, CachedBarGeometry(series, 1));
        var beforeGroup = CachedBarGeometry(series, 1);
        var grouped = view with { GroupIndex = 2, GroupCount = 3, GroupWidth = 71.3 };
        AssertDrawingGeometry(Draw(OracleBarContext(grouped)), Draw(grouped));
        Assert.NotSame(beforeGroup, CachedBarGeometry(series, 1));
        var geometries = Enumerable.Range(0, 3).Select(index => CachedBarGeometry(series, index)).ToArray();
        var replaced = CacheBarContext(series, yMinimum: 1, yMaximum: 20, plot: new Rect(42.5, 31.25, 410.75, 210.5)) with
        { GroupIndex = 2, GroupCount = 3, GroupWidth = 71.3 };
        AssertDrawingGeometry(Draw(OracleBarContext(replaced)), Draw(replaced));
        for (var index = 0; index < 3; index++)
        {
            Assert.Same(geometries[index], CachedBarGeometry(series, index));
            AssertRectBits(firstBounds[index], firstDrawing[index].Geometry!.Bounds);
        }
    }

    [AvaloniaFact]
    public void Bar_cache_resets_at_count_changes_and_releases_for_large_or_empty_inputs()
    {
        var series = new BarSeries { Fill = Brushes.Blue, ShowMarkers = false };
        Array? previousEntries = null;
        foreach (var count in new[] { 8_191, 8_192, 8_193, 2, 0 })
        {
            var points = Enumerable.Range(0, count).Select(index => new ChartPoint(index % 11, 1 + index % 9)).ToArray();
            var context = CacheBarContext(series, points);
            var drawing = Draw(context);
            Assert.Equal(count, drawing.Length);
            AssertDrawingGeometry(Draw(OracleBarContext(context)), drawing);
            if (count is > 0 and <= 8_192)
            {
                var entries = CachedBarEntries(Assert.IsType<BarSeries>(context.Series));
                Assert.NotNull(entries); Assert.Equal(count, entries.Length);
                if (previousEntries is not null) Assert.NotSame(previousEntries, entries);
                previousEntries = entries;
            }
            else Assert.Null(BarCache(series));
        }
    }

    [AvaloniaTheory]
    [InlineData("marker")]
    [InlineData("custom")]
    [InlineData("outside-view")]
    [InlineData("gap")]
    [InlineData("log")]
    [InlineData("gradient")]
    [InlineData("image")]
    public void Bar_fallbacks_clear_geometry_cache_and_keep_the_original_draw_path(string scenario)
    {
        var series = new BarSeries { Fill = Brushes.Blue, ShowMarkers = false };
        var context = CacheBarContext(series);
        Draw(context); Assert.NotNull(BarCache(series));
        switch (scenario)
        {
            case "marker": series.ShowMarkers = true; series.MarkerSize = 4; break;
            case "custom": context = context with { Projection = new ForwardProjection(context.Projection) }; break;
            case "outside-view": context = CacheBarContext(series, yMaximum: 4); break;
            case "gap": context = CacheBarContext(series, [new(1, 2), new(5, double.NaN), new(9, 9)]); break;
            case "log":
                context = context with { Projection = new CartesianChart.CartesianProjection(Plot,
                    ChartScale.CreateForView(new LogarithmicAxis(), 1, 10), ChartScale.CreateForView(new NumericAxis(), 0, 10), context.Data) };
                break;
            case "gradient": series.Fill = new LinearGradientBrush { GradientStops = [new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1)] }; break;
            case "image": series.Fill = new ImageBrush(); break;
        }
        var expected = Draw(OracleBarContext(context));
        var actual = Draw(context);
        Assert.Null(BarCache(series));
        AssertBarBoundsEqual(expected, actual);
    }

    [AvaloniaFact]
    public void Cached_bar_slots_are_cleared_when_rectangles_become_zero_height_or_nonfinite()
    {
        var series = new BarSeries { Fill = Brushes.Blue, ShowMarkers = false };
        Draw(CacheBarContext(series));
        var unchanged = CachedBarGeometry(series, 2);
        var zero = CacheBarContext(series, [new(1, 0), new(5, 6), new(9, 9)]);
        var actual = Draw(zero);
        Assert.Equal(2, actual.Length); Assert.Null(CachedBarGeometry(series, 0));
        Assert.Same(unchanged, CachedBarGeometry(series, 2));
        AssertDrawingGeometry(Draw(OracleBarContext(zero)), actual);
        var invalid = zero with { GroupWidth = double.NaN };
        Assert.Empty(Draw(invalid));
        Assert.NotNull(CachedBarEntries(series));
        for (var index = 0; index < 3; index++) Assert.Null(CachedBarGeometry(series, index));
    }

    [AvaloniaFact]
    public void Cached_bar_geometry_uses_current_mutable_solid_color_and_opacity()
    {
        var fill = new SolidColorBrush(Colors.Red) { Opacity = .75 };
        var series = new BarSeries { Fill = fill, ShowMarkers = false };
        var context = CacheBarContext(series);
        Draw(context);
        var geometry = CachedBarGeometry(series, 1);
        fill.Color = Color.FromArgb(149, 17, 139, 73); fill.Opacity = .375;
        var actual = Draw(context);
        Assert.Same(geometry, CachedBarGeometry(series, 1));
        AssertBarBoundsEqual(Draw(OracleBarContext(context)), actual);
        Assert.All(actual, drawing =>
        {
            var brush = Assert.IsAssignableFrom<ISolidColorBrush>(drawing.Brush);
            Assert.Equal(fill.Color, brush.Color); AssertBits(fill.Opacity, brush.Opacity);
        });
    }

    [AvaloniaFact]
    public void Cached_bar_record_obeys_current_outer_clip_transform_and_opacity()
    {
        var series = new BarSeries { Fill = Brushes.Blue, ShowMarkers = false };
        var context = CacheBarContext(series);
        Draw(context);
        var geometry = CachedBarGeometry(series, 1);
        var actual = BarFrameEvidence(RecordBarFrame(context), Matrix.Identity, 1, []).ToArray();
        var expected = BarFrameEvidence(RecordBarFrame(OracleBarContext(context)), Matrix.Identity, 1, []).ToArray();
        Assert.Same(geometry, CachedBarGeometry(series, 1));
        Assert.Equal(expected.Length, actual.Length); Assert.NotEmpty(actual);
        foreach (var (left, right) in expected.Zip(actual))
        {
            AssertRectBits(left.Bounds, right.Bounds); AssertBits(left.Opacity, right.Opacity);
            Assert.NotEmpty(right.Clips); Assert.Equal(left.Clips.Length, right.Clips.Length);
            foreach (var (first, last) in left.Clips.Zip(right.Clips)) AssertRectBits(first, last);
        }
    }

    [AvaloniaFact]
    public void Warm_bar_cache_does_not_retain_replaced_source_or_snapshot_while_series_remains_alive()
    {
        var series = new BarSeries { Fill = Brushes.Blue, ShowMarkers = false };
        var weak = CacheAfterSourceReplacement(series);
        CollectCacheReferences();
        Assert.False(weak.Source.IsAlive); Assert.False(weak.Snapshot.IsAlive);
        Assert.NotNull(BarCache(series));
        GC.KeepAlive(series);
    }

    [AvaloniaFact]
    public void Bar_cache_key_value_and_private_geometries_are_released_with_the_series()
    {
        var weak = ReleasedBarCache();
        CollectCacheReferences();
        Assert.All(weak, reference => Assert.False(reference.IsAlive));
    }

    [AvaloniaTheory]
    [InlineData("hide")]
    [InlineData("remove")]
    [InlineData("series-source")]
    [InlineData("detach")]
    public void Chart_lifecycle_retires_bar_cache_while_view_model_and_old_drawings_remain_alive(string action)
    {
        var series = new BarSeries { Fill = Brushes.Blue, ShowMarkers = false };
        var chart = new CartesianChart { Width = 600, Height = 300, ShowLegend = false };
        chart.Series.Add(series);
        var window = new Avalonia.Controls.Window { Width = 600, Height = 300, Content = chart };
        window.Show(); window.UpdateLayout();
        try
        {
            var context = CacheBarContext(series);
            var oldDrawing = Draw(context);
            var oldCache = BarCache(series);
            var oldGeometry = CachedBarGeometry(series, 1);
            Assert.NotNull(oldCache); Assert.NotNull(oldGeometry);
            switch (action)
            {
                case "hide": chart.IsVisible = false; break;
                case "remove": chart.Series.Remove(series); break;
                case "series-source": chart.SeriesSource = Array.Empty<ChartSeries>(); break;
                case "detach": window.Content = null; break;
            }
            Assert.Null(BarCache(series));
            AssertDrawingGeometry(Draw(OracleBarContext(context)), oldDrawing);
            switch (action)
            {
                case "hide": chart.IsVisible = true; break;
                case "remove": chart.Series.Add(series); break;
                case "series-source": chart.SeriesSource = null; break;
                case "detach": window.Content = chart; break;
            }
            AssertDrawingGeometry(oldDrawing, Draw(context));
            Assert.NotSame(oldCache, BarCache(series));
            Assert.NotSame(oldGeometry, CachedBarGeometry(series, 1));
            GC.KeepAlive(series); GC.KeepAlive(oldDrawing);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Removing_an_unattached_chart_series_also_retires_its_recording_cache()
    {
        var series = new BarSeries { Fill = Brushes.Blue, ShowMarkers = false };
        var chart = new CartesianChart(); chart.Series.Add(series);
        var context = CacheBarContext(series);
        var oldDrawing = Draw(context); Assert.NotNull(BarCache(series));
        chart.Series.Clear();
        Assert.Null(BarCache(series));
        AssertDrawingGeometry(Draw(OracleBarContext(context)), oldDrawing);
        chart.Series.Add(series);
        AssertDrawingGeometry(oldDrawing, Draw(context)); Assert.NotNull(BarCache(series));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recorder_circles_share_one_immutable_pen_for_palette_and_explicit_strokes(bool explicitStroke)
    {
        var palette = new ImmutableSolidColorBrush(Color.FromArgb(177, 50, 71, 95), .625);
        var stroke = new ImmutableSolidColorBrush(Color.FromArgb(193, 17, 135, 42), .45);
        var series = CirclePenSeries();
        if (explicitStroke) series.MarkerStroke = stroke;
        var context = CirclePenContext(series, palette);
        var actual = AssertCirclePenGeometryAgainstGeneral(context);
        var pen = Assert.IsType<ImmutablePen>(actual[0].Pen);
        AssertPenValues(new Pen(explicitStroke ? stroke : palette, 1.375), pen);
        Assert.All(actual, marker => Assert.Same(pen, marker.Pen));
        Assert.Equal(5, actual.Length);
    }

    [AvaloniaFact]
    public void Changing_an_immutable_circle_stroke_in_the_next_frame_does_not_mutate_the_previous_frame()
    {
        var originalBrush = new ImmutableSolidColorBrush(Colors.Green, .375);
        var replacementBrush = new ImmutableSolidColorBrush(Colors.Purple, .75);
        var series = CirclePenSeries();
        series.MarkerStroke = originalBrush;
        var context = CirclePenContext(series, Brushes.Red);
        var original = AssertCirclePenGeometryAgainstGeneral(context);
        var originalBounds = original.Select(marker => marker.Geometry!.Bounds).ToArray();
        var originalPen = Assert.IsType<ImmutablePen>(original[0].Pen);

        series.MarkerStroke = replacementBrush;
        series.MarkerStrokeThickness = 3.625;
        var next = AssertCirclePenGeometryAgainstGeneral(context);
        var nextPen = Assert.IsType<ImmutablePen>(next[0].Pen);
        Assert.NotSame(originalPen, nextPen);
        AssertPenValues(new Pen(originalBrush, 1.375), originalPen);
        AssertPenValues(new Pen(replacementBrush, 3.625), nextPen);
        Assert.All(original, marker => Assert.Same(originalPen, marker.Pen));
        Assert.All(next, marker => Assert.Same(nextPen, marker.Pen));
        for (var index = 0; index < original.Length; index++)
        {
            Assert.Equal(originalBounds[index], original[index].Geometry!.Bounds);
            Assert.Equal(originalBounds[index], next[index].Geometry!.Bounds);
        }
    }

    [AvaloniaFact]
    public void Mutable_circle_brushes_keep_the_original_pen_and_remain_observable_in_old_drawings()
    {
        var brush = new SolidColorBrush(Colors.Green, .375);
        var series = CirclePenSeries();
        series.MarkerStroke = brush;
        var context = CirclePenContext(series, Brushes.Red);
        var original = AssertCirclePenGeometryAgainstGeneral(context);
        var originalPen = Assert.IsType<Pen>(original[0].Pen);
        Assert.Same(brush, originalPen.Brush);
        Assert.All(original, marker => Assert.Same(originalPen, marker.Pen));

        brush.Color = Colors.Purple;
        brush.Opacity = .75;
        series.MarkerStrokeThickness = 2.875;
        var next = AssertCirclePenGeometryAgainstGeneral(context);
        var nextPen = Assert.IsType<Pen>(next[0].Pen);
        Assert.NotSame(originalPen, nextPen);
        AssertPenValues(new Pen(brush, 1.375), originalPen);
        AssertPenValues(new Pen(brush, 2.875), nextPen);
        Assert.All(original, marker =>
        {
            var oldBrush = Assert.IsType<SolidColorBrush>(marker.Pen!.Brush);
            Assert.Same(brush, oldBrush);
            Assert.Equal(Colors.Purple, oldBrush.Color);
            AssertBits(.75, oldBrush.Opacity);
        });
    }

    [AvaloniaFact]
    public void Non_circle_symbols_retain_their_mutable_round_cap_and_join_pen()
    {
        var brush = new ImmutableSolidColorBrush(Colors.Green, .625);
        var series = CirclePenSeries();
        series.MarkerShape = ChartMarkerShape.Diamond;
        series.MarkerStroke = brush;
        var context = CirclePenContext(series, Brushes.Red);
        var expectedFrame = RecordBarFrame(context with { Projection = new ForwardProjection(context.Projection) });
        var actualFrame = RecordBarFrame(context);
        var expected = NestedGeometryDrawings(expectedFrame).ToArray();
        var actual = NestedGeometryDrawings(actualFrame).ToArray();
        AssertDrawingGeometry(expected, actual);
        Assert.Equal(5, actual.Length);
        // Diamond symbols use a translated child group. Compare the complete
        // transform/clip/opacity walk as well as the local geometry and paint.
        var expectedPositions = BarFrameEvidence(expectedFrame, Matrix.Identity, 1, []).ToArray();
        var actualPositions = BarFrameEvidence(actualFrame, Matrix.Identity, 1, []).ToArray();
        Assert.Equal(5, expectedPositions.Length);
        Assert.Equal(5, actualPositions.Length);
        for (var index = 0; index < actual.Length; index++)
        {
            AssertRectBits(expectedPositions[index].Bounds, actualPositions[index].Bounds);
            AssertBits(expectedPositions[index].Opacity, actualPositions[index].Opacity);
            Assert.Equal(expectedPositions[index].Clips.Length, actualPositions[index].Clips.Length);
            for (var clip = 0; clip < expectedPositions[index].Clips.Length; clip++)
                AssertRectBits(expectedPositions[index].Clips[clip], actualPositions[index].Clips[clip]);
            AssertPenValues(expected[index].Pen!, actual[index].Pen!);
        }
        var pen = Assert.IsType<Pen>(actual[0].Pen);
        AssertPenValues(new Pen(brush, 1.375, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), pen);
        Assert.All(actual, marker => Assert.Same(pen, marker.Pen));
    }

    [AvaloniaFact]
    public void Custom_projection_circles_retain_the_original_pen_even_for_an_immutable_brush()
    {
        var brush = new ImmutableSolidColorBrush(Colors.Green, .625);
        var series = CirclePenSeries();
        series.MarkerStroke = brush;
        var native = CirclePenContext(series, Brushes.Red);
        var custom = native with { Projection = new ForwardProjection(native.Projection) };
        var actual = AssertCirclePenGeometryAgainstGeneral(custom);
        var pen = Assert.IsType<Pen>(actual[0].Pen);
        AssertPenValues(new Pen(brush, 1.375), pen);
        Assert.All(actual, marker => Assert.Same(pen, marker.Pen));
        Assert.Equal(5, actual.Length);
    }

    [AvaloniaFact]
    public void Line_circles_share_the_immutable_marker_pen_without_changing_the_line_or_hit_identity()
    {
        var brush = new ImmutableSolidColorBrush(Colors.Green, .625);
        var series = new LineSeries
        {
            ShowMarkers = true, MarkerShape = ChartMarkerShape.Circle, MarkerSize = 6.5,
            MarkerFill = Brushes.Blue, MarkerStroke = brush, MarkerStrokeThickness = 1.375,
            Stroke = Brushes.Red, StrokeThickness = 2.75,
        };
        var context = CirclePenContext(series, Brushes.Purple);
        var expected = Draw(context with { Projection = new ForwardProjection(context.Projection) });
        var actual = Draw(context);
        AssertDrawingGeometry(expected, actual);
        Assert.Equal(6, actual.Length);
        AssertPenValues(expected[0].Pen!, actual[0].Pen!);
        var pen = Assert.IsType<ImmutablePen>(actual[1].Pen);
        AssertPenValues(new Pen(brush, 1.375), pen);
        Assert.All(actual.Skip(1), marker => Assert.Same(pen, marker.Pen));
        var last = context.Data.Points[4];
        var position = context.Projection.Project(last.Point);
        var hit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, position, 0));
        Assert.Equal(last.SourceIndex, hit.SourceIndex);
        Assert.True(context.Data.TryGetPoint(hit.SourceIndex, out var original));
        Assert.Same(last.StableId, original.StableId);
        Assert.Equal(last.Item, original.Item);
        AssertPointBits(position, hit.Position);
    }

    [AvaloniaFact]
    public void General_cartesian_circle_mapping_keeps_gaps_and_breaks_while_sharing_the_immutable_pen()
    {
        var brush = new ImmutableSolidColorBrush(Colors.Green, .625);
        var series = CirclePenSeries();
        series.MarkerStroke = brush;
        var axis = new NumericAxis();
        axis.ScaleBreaks.Add(new ChartScaleBreak { Start = 3, End = 5 });
        var context = CirclePenContext(series, Brushes.Red,
            [new(0, 1), new(2, 4), new(4, 6), new(6, double.NaN), new(10, 10)], axis);
        var projection = Assert.IsType<CartesianChart.CartesianProjection>(context.Projection);
        Assert.False(projection.TryGetLinearProjection(context.Data, out _));
        Assert.False(projection.TryGetGappedLinearProjection(context.Data, out _));
        var actual = AssertCirclePenGeometryAgainstGeneral(context);
        Assert.Equal(3, actual.Length);
        var pen = Assert.IsType<ImmutablePen>(actual[0].Pen);
        AssertPenValues(new Pen(brush, 1.375), pen);
        Assert.All(actual, marker => Assert.Same(pen, marker.Pen));
    }

    private static ScatterSeries CirclePenSeries() => new()
    {
        MarkerShape = ChartMarkerShape.Circle, MarkerSize = 6.5,
        MarkerFill = Brushes.Blue, MarkerStrokeThickness = 1.375,
    };

    private static ChartSeriesRenderContext CirclePenContext(ChartSeries series, IBrush palette,
        ChartPoint[]? points = null, NumericAxis? xAxis = null)
    {
        series.ItemsSource = points ?? [new(0, 1), new(2, 4), new(2, 4), new(5, 7), new(10, 10)];
        var data = series.CreateSnapshot();
        ChartDataIndex.For(data);
        var x = ChartScale.CreateForView(xAxis ?? new NumericAxis(), 0, 10);
        var y = ChartScale.CreateForView(new NumericAxis(), 0, 10);
        return new(series, data, new CartesianChart.CartesianProjection(Plot, x, y, data), palette);
    }

    private static GeometryDrawing[] AssertCirclePenGeometryAgainstGeneral(ChartSeriesRenderContext context)
    {
        var expected = Draw(context with { Projection = new ForwardProjection(context.Projection) });
        var actual = Draw(context);
        AssertDrawingGeometry(expected, actual);
        for (var index = 0; index < expected.Length; index++)
            AssertPenValues(expected[index].Pen!, actual[index].Pen!);
        return actual;
    }

    private static void AssertPenValues(IPen expected, IPen actual)
    {
        Assert.Same(expected.Brush, actual.Brush);
        AssertBits(expected.Thickness, actual.Thickness);
        Assert.Equal(expected.LineCap, actual.LineCap);
        Assert.Equal(expected.LineJoin, actual.LineJoin);
        AssertBits(expected.MiterLimit, actual.MiterLimit);
        if (expected.DashStyle is null) Assert.Null(actual.DashStyle);
        else
        {
            Assert.NotNull(actual.DashStyle);
            AssertBits(expected.DashStyle.Offset, actual.DashStyle.Offset);
            Assert.Equal(expected.DashStyle.Dashes, actual.DashStyle.Dashes);
        }
    }

    private static IEnumerable<GeometryDrawing> NestedGeometryDrawings(Drawing drawing)
    {
        if (drawing is GeometryDrawing shape) yield return shape;
        else if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var nested in NestedGeometryDrawings(child)) yield return nested;
    }

    private static ChartSeriesRenderContext CacheBarContext(BarSeries series, ChartPoint[]? points = null,
        double yMinimum = 0, double yMaximum = 10, Rect? plot = null)
    {
        series.ItemsSource = points ?? [new(1, 2), new(5, 6), new(9, 9)];
        var data = series.CreateSnapshot(); ChartDataIndex.For(data);
        var projection = new CartesianChart.CartesianProjection(plot ?? Plot,
            ChartScale.CreateForView(new NumericAxis(), 0, 10), ChartScale.CreateForView(new NumericAxis(), yMinimum, yMaximum), data);
        return new(series, data, projection, Brushes.Red, 1, 2, 31.25);
    }

    private static ChartSeriesRenderContext OracleBarContext(ChartSeriesRenderContext context)
    {
        var original = context.Series;
        var oracle = new BarSeries
        {
            Fill = original.Fill, ShowMarkers = original.ShowMarkers, MarkerSize = original.MarkerSize,
            MarkerShape = original.MarkerShape, MarkerFill = original.MarkerFill,
            MarkerStroke = original.MarkerStroke, MarkerStrokeThickness = original.MarkerStrokeThickness,
        };
        return context with { Series = oracle, Projection = new ForwardProjection(context.Projection) };
    }

    private static object? BarCache(BarSeries series)
    {
        var table = typeof(CartesianSeriesRenderer).GetField("BarGeometries", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object?[] arguments = [series, null];
        return (bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, arguments)! ? arguments[1] : null;
    }

    private static Array? CachedBarEntries(BarSeries series)
    {
        var cache = BarCache(series);
        return cache is null ? null : (Array)cache.GetType().GetField("Entries")!.GetValue(cache)!;
    }

    private static RectangleGeometry? CachedBarGeometry(BarSeries series, int ordinal)
    {
        var entry = CachedBarEntries(series)!.GetValue(ordinal)!;
        return (RectangleGeometry?)entry.GetType().GetProperty("Geometry")!.GetValue(entry);
    }

    private static void AssertBarBoundsEqual(GeometryDrawing[] expected, GeometryDrawing[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        foreach (var (first, last) in expected.Zip(actual))
        {
            AssertRectBits(first.Geometry!.Bounds, last.Geometry!.Bounds);
            Assert.Equal(first.Pen?.Thickness, last.Pen?.Thickness);
        }
    }

    private static void AssertRectBits(Rect first, Rect last)
    {
        AssertBits(first.X, last.X); AssertBits(first.Y, last.Y);
        AssertBits(first.Width, last.Width); AssertBits(first.Height, last.Height);
    }

    private static DrawingGroup RecordBarFrame(ChartSeriesRenderContext context)
    {
        var result = new DrawingGroup();
        using var drawing = result.Open();
        using var transform = drawing.PushTransform(Matrix.CreateScale(.875, 1.125) * Matrix.CreateTranslation(3.25, -7.5));
        using var opacity = drawing.PushOpacity(.625);
        using var clip = drawing.PushClip(new Rect(50.125, 41.25, 400.5, 170.75));
        context.Series.Renderer.Render(drawing, context);
        return result;
    }

    private readonly record struct BarFrame(Rect Bounds, double Opacity, Rect[] Clips);
    private static IEnumerable<BarFrame> BarFrameEvidence(Drawing drawing, Matrix transform, double opacity, Rect[] clips)
    {
        if (drawing is DrawingGroup group)
        {
            var combined = (group.Transform?.Value ?? Matrix.Identity) * transform;
            var current = group.ClipGeometry is { } clip ? clips.Append(clip.Bounds.TransformToAABB(combined)).ToArray() : clips;
            foreach (var child in group.Children)
                foreach (var item in BarFrameEvidence(child, combined, opacity * group.Opacity, current)) yield return item;
        }
        else if (drawing is GeometryDrawing shape) yield return new(shape.Geometry!.Bounds.TransformToAABB(transform), opacity, clips);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Source, WeakReference Snapshot) CacheAfterSourceReplacement(BarSeries series)
    {
        var source = new ChartPoint[] { new(1, 2), new(5, 6), new(9, 9) };
        var context = CacheBarContext(series, source);
        Draw(context);
        var sourceReference = new WeakReference(source); var snapshotReference = new WeakReference(context.Data);
        Draw(CacheBarContext(series));
        return (sourceReference, snapshotReference);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] ReleasedBarCache()
    {
        var series = new BarSeries { Fill = Brushes.Blue, ShowMarkers = false };
        Draw(CacheBarContext(series));
        return [new(series), new(BarCache(series)!), new(CachedBarGeometry(series, 1)!)];
    }

    private static void CollectCacheReferences()
    {
        // Never wait for Avalonia finalizers on the UI thread. These application
        // objects have no finalizer; collect twice to clear weak-table ephemerons.
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private static ChartPoint[] RealtimePoints(int seriesIndex, int revision)
    {
        var state = 0x1020304050607088UL + (ulong)seriesIndex * 37;
        var points = new ChartPoint[250 + revision * 25];
        for (var index = 0; index < points.Length; index++)
        {
            state ^= state >> 12; state ^= state << 25; state ^= state >> 27;
            var random = unchecked(state * 2685821657736338717UL);
            var wave = index % 1024;
            var triangle = wave < 512 ? wave : 1024 - wave;
            var value = (triangle * 32 + (int)(random & 511) - 255) / 256d + seriesIndex * 3;
            if (index % 997 == 431) value += 128;
            points[index] = new(index, value);
        }
        return points;
    }

    private static void AssertRecordedLinearLine(ChartPoint[] points, IChartProjection general, InRangeLinearProjection linear)
    {
        var sink = new RecordingSink([]);
        var segments = CartesianSeriesRenderer.RenderInRangeLinearLineCore(sink, points, linear);
        Assert.Equal(points.Length - 1, segments);
        Assert.Equal(points.Length + 1, sink.Commands.Count);
        Assert.Equal("Begin", sink.Commands[0].Kind);
        for (var index = 0; index < points.Length; index++)
        {
            Assert.Equal(index == 0 ? "Begin" : "Line", sink.Commands[index].Kind);
            AssertPointBits(general.Project(points[index]), sink.Commands[index].Position);
        }
        Assert.Equal("End", sink.Commands[^1].Kind);
    }

    private static GeometryDrawing[] Draw(ChartSeriesRenderContext context)
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open()) context.Series.Renderer.Render(drawing, context);
        return group.Children.OfType<GeometryDrawing>().ToArray();
    }

    private static void AssertDrawingGeometry(GeometryDrawing[] expected, GeometryDrawing[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        foreach (var (left, right) in expected.Zip(actual))
        {
            var original = left.Geometry!.Bounds; var candidate = right.Geometry!.Bounds;
            AssertBits(original.X, candidate.X); AssertBits(original.Y, candidate.Y);
            AssertBits(original.Width, candidate.Width); AssertBits(original.Height, candidate.Height);
            Assert.Equal(left.Pen?.Thickness, right.Pen?.Thickness);
            Assert.Same(left.Brush, right.Brush);
        }
    }

    private static void AssertPointBits(Point expected, Point actual)
    { AssertBits(expected.X, actual.X); AssertBits(expected.Y, actual.Y); }

    private static void AssertBits(double expected, double actual) => Assert.True(
        BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
        $"Expected {expected:R} [{BitConverter.DoubleToInt64Bits(expected):X16}], actual {actual:R} [{BitConverter.DoubleToInt64Bits(actual):X16}].");

    private sealed class ForwardProjection(IChartProjection original) : IChartProjection
    {
        public Rect PlotArea => original.PlotArea;
        public double YOrigin => original.YOrigin;
        public Point Project(ChartPoint point) => original.Project(point);
        public ChartPoint Unproject(Point position) => original.Unproject(position);
        public double MapX(double value) => original.MapX(value);
        public double MapY(double value) => original.MapY(value);
    }

    private sealed record Row(double X, double Y);
    private sealed class QuadraticAxis : ChartAxis
    {
        public override double Transform(double value) => value * value;
        public override double Inverse(double value) => Math.Sqrt(value);
    }

    private readonly record struct FigureCommand(string Kind, Point Position, bool Flag);
    private readonly struct RecordingSink(List<FigureCommand> commands) : IChartLineSink
    {
        public List<FigureCommand> Commands => commands;
        public void Begin(Point position) => commands.Add(new("Begin", position, false));
        public void Line(Point position) => commands.Add(new("Line", position, true));
        public void End() => commands.Add(new("End", default, false));
    }
}
