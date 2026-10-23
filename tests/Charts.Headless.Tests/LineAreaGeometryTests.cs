using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class LineAreaGeometryTests
{
    [AvaloniaFact]
    public void New_series_properties_have_safe_defaults_validate_values_and_preserve_the_linear_renderer()
    {
        var line = new LineSeries(); var area = new AreaSeries(); var range = new RangeAreaSeries();
        Assert.Equal(ChartLineInterpolation.Linear, line.Interpolation);
        Assert.Equal(ChartStackMode.None, line.StackMode); Assert.Null(line.StackGroup);
        Assert.Equal(0, area.Baseline); Assert.Equal(ChartStackMode.None, area.StackMode);
        Assert.Equal(ChartLineInterpolation.Linear, range.Interpolation);
        Assert.Same(CartesianSeriesRenderer.Line, line.Renderer);
        line.StackMode = ChartStackMode.Stacked;
        Assert.Same(CartesianSeriesRenderer.Line, line.Renderer);
        line.Interpolation = ChartLineInterpolation.Smooth;
        Assert.Same(ChartLineSeriesRenderer.Line, line.Renderer);
        Assert.Throws<ArgumentException>(() => line.Interpolation = (ChartLineInterpolation)99);
        Assert.Throws<ArgumentException>(() => area.StackMode = (ChartStackMode)99);
        Assert.Throws<ArgumentException>(() => range.Interpolation = (ChartLineInterpolation)99);
        Assert.Throws<ArgumentException>(() => area.Baseline = double.NaN);
        Assert.Throws<ArgumentException>(() => area.Baseline = double.PositiveInfinity);
    }

    [AvaloniaTheory]
    [InlineData(ChartLineInterpolation.StepBefore, 0, 8)]
    [InlineData(ChartLineInterpolation.StepAfter, 6, 2)]
    [InlineData(ChartLineInterpolation.StepCenter, 3, 2)]
    public void Step_paths_have_explicit_source_order_corners(ChartLineInterpolation mode, double cornerX, double cornerY)
    {
        var series = new LineSeries { Interpolation = mode };
        var figure = Assert.Single(ChartLineGeometry.Build(Context(series, [new(0, 2), new(6, 8)])));
        Assert.All(figure.Upper, part => Assert.False(part.IsCubic));
        Assert.Equal(new Point(0, 2), figure.Upper[0].Start);
        Assert.Equal(new Point(cornerX, cornerY), figure.Upper[0].End);
        Assert.Equal(new Point(6, 8), figure.Upper[^1].End);
        Assert.Equal(0, figure.Upper[0].StartFraction); Assert.Equal(1, figure.Upper[^1].EndFraction);
        Assert.All(figure.Upper, part => { Assert.Equal(0, part.StartOrdinal); Assert.Equal(1, part.EndOrdinal); });
        for (var i = 1; i < figure.Upper.Count; i++) Assert.Equal(figure.Upper[i - 1].End, figure.Upper[i].Start);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Smooth_paths_pass_through_samples_have_continuous_tangents_and_do_not_overshoot(bool reversed)
    {
        ChartPoint[] points = [new(0, 0), new(2, 8), new(9, 9), new(10, 3), new(20, 4)];
        if (reversed) points = points.Reverse().ToArray();
        var figure = Assert.Single(ChartLineGeometry.Build(Context(new LineSeries { Interpolation = ChartLineInterpolation.Smooth }, points)));
        Assert.Equal(points.Length - 1, figure.Upper.Count);
        for (var i = 0; i < figure.Upper.Count; i++)
        {
            var part = figure.Upper[i]; Assert.True(part.IsCubic);
            Assert.Equal(new Point(points[i].X, points[i].Y), part.Start);
            Assert.Equal(new Point(points[i + 1].X, points[i + 1].Y), part.End);
            for (var sample = 0; sample <= 100; sample++)
            {
                var value = part.At(sample / 100d);
                Assert.InRange(value.X, Math.Min(part.Start.X, part.End.X) - 1e-12, Math.Max(part.Start.X, part.End.X) + 1e-12);
                Assert.InRange(value.Y, Math.Min(part.Start.Y, part.End.Y) - 1e-12, Math.Max(part.Start.Y, part.End.Y) + 1e-12);
            }
            if (i > 0)
            {
                var before = figure.Upper[i - 1];
                var previousSlope = (before.End.Y - before.Control2.Y) / (before.End.X - before.Control2.X);
                var nextSlope = (part.Control1.Y - part.Start.Y) / (part.Control1.X - part.Start.X);
                Assert.Equal(previousSlope, nextSlope, 10);
            }
        }
    }

    [AvaloniaFact]
    public void Source_turns_duplicate_x_and_missing_values_keep_their_physical_adjacency()
    {
        var context = Context(new LineSeries { Interpolation = ChartLineInterpolation.Smooth },
            [new(0, 1), new(4, 8), new(4, 2), new(1, 3), new(2, double.NaN), new(3, 5), new(5, 9)]);
        var figures = ChartLineGeometry.Build(context);
        Assert.Equal(2, figures.Count); Assert.Equal(3, figures[0].Upper.Count);
        Assert.False(figures[0].Upper[1].IsCubic);
        Assert.Equal(5, Assert.Single(figures[1].Upper).StartOrdinal);
        Assert.Equal(6, figures[1].Upper[0].EndOrdinal);
        Assert.All(figures.SelectMany(f => f.Upper), part =>
            Assert.All(part.Flatten(), edge => { Assert.True(double.IsFinite(edge.Start.X)); Assert.True(double.IsFinite(edge.End.Y)); }));
    }

    [AvaloniaTheory]
    [InlineData(ChartLineInterpolation.Linear)]
    [InlineData(ChartLineInterpolation.Smooth)]
    [InlineData(ChartLineInterpolation.StepBefore)]
    [InlineData(ChartLineInterpolation.StepAfter)]
    [InlineData(ChartLineInterpolation.StepCenter)]
    public void Area_lower_path_uses_the_configured_baseline_and_bounds_include_it(ChartLineInterpolation mode)
    {
        var area = new AreaSeries { Interpolation = mode, Baseline = 10 };
        var context = Context(area, [new(1, 12), new(5, 18), new(9, 11)]);
        var figure = Assert.Single(ChartLineGeometry.Build(context));
        Assert.Equal(figure.Upper.Count, figure.Lower.Count);
        Assert.All(figure.Lower, part => { Assert.Equal(10, part.Start.Y); Assert.Equal(10, part.End.Y); });
        var bounds = area.Renderer.GetBounds(area, context.Data)!.Value;
        Assert.Equal(10, bounds.YMinimum); Assert.Equal(18, bounds.YMaximum);
        Assert.False(area.Renderer.IncludesZero);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Range_smooth_upper_and_lower_control_polygons_never_invert(bool inverted)
    {
        ChartPoint[] upper = [new(0, 9), new(2, 10), new(8, 7), new(10, 11)];
        double[] lower = [8, 2, 6.9, 1];
        var range = new RangeAreaSeries { Interpolation = ChartLineInterpolation.Smooth };
        var context = Context(range, upper, lower, inverted);
        var figure = Assert.Single(ChartLineGeometry.Build(context));
        for (var i = 0; i < figure.Upper.Count; i++)
            for (var sample = 0; sample <= 100; sample++)
            {
                var a = figure.Upper[i].At(sample / 100d); var b = figure.Lower[i].At(sample / 100d);
                Assert.True(inverted ? a.Y <= b.Y + 1e-12 : a.Y >= b.Y - 1e-12);
            }
    }

    [AvaloniaFact]
    public void Logarithmic_zero_baseline_uses_the_axis_origin_while_other_invalid_lower_values_split_the_band()
    {
        var area = new AreaSeries();
        var context = Context(area, [new(1, 4), new(2, 9)]) with { Projection = new GeometryProjection(logarithmic: true) };
        var figure = Assert.Single(ChartLineGeometry.Build(context));
        Assert.All(figure.Lower, part => { Assert.Equal(1, part.Start.Y); Assert.Equal(1, part.End.Y); });
        area.Baseline = -2;
        Assert.Empty(ChartLineGeometry.Build(context));
    }

    [AvaloniaTheory]
    [InlineData(ChartLineInterpolation.StepBefore, .5, 80)]
    [InlineData(ChartLineInterpolation.StepAfter, .5, 20)]
    [InlineData(ChartLineInterpolation.StepCenter, .5, 20)]
    [InlineData(ChartLineInterpolation.StepCenter, 1.5, 80)]
    public void Step_hit_testing_and_trackball_follow_the_actual_path(ChartLineInterpolation mode, double x, double y)
    {
        SegmentRow[] rows = [new("start", 0, 20), new("end", 2, 80)];
        var line = LineInterpolationTests.CreateLine(rows); line.Interpolation = mode;
        using var host = new SegmentTestHost(new(0, 2), new(0, 100), line);
        var hit = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(host.Project(new(x, y)), .1));
        Assert.Equal(y, hit.Point.Y, 7); Assert.Equal(x, hit.Point.X, 7);
        Assert.Same(rows[0], hit.Start.Item); Assert.Same(rows[1], hit.End.Item);
        Assert.Null(host.Chart.HitTestSegment(host.Project(new(x, 45)), .1));
        host.Chart.TrackballMode = ChartTrackballMode.Vertical;
        host.Chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        host.Move(host.Project(new(x, 45)));
        var reading = Assert.Single(host.Chart.TrackballReadings);
        Assert.Equal(y, reading.Point.Y, 7); Assert.NotNull(reading.Segment);
    }

    [AvaloniaFact]
    public void Smooth_trackball_matches_an_independent_cubic_evaluation_and_preserves_original_endpoints()
    {
        SegmentRow[] rows = [new("a", 0, 0), new("b", 2, 8), new("c", 8, 9)];
        var line = LineInterpolationTests.CreateLine(rows); line.Interpolation = ChartLineInterpolation.Smooth;
        using var host = new SegmentTestHost(new(0, 8), new(0, 10), line);
        // At source x=1, t=.5: endpoint tangent4 and interior tangent1/6.
        var expectedY = .125 * 0 + .375 * (8d / 3) + .375 * (8 - 1d / 9) + .125 * 8;
        host.Chart.TooltipMode = ChartTooltipMode.InterpolateX;
        host.Move(host.Project(new(1, 5)));
        var segment = Assert.Single(host.Chart.HoveredSegments);
        Assert.Equal(1, segment.Point.X, 7); Assert.Equal(expectedY, segment.Point.Y, 7);
        Assert.Same(rows[0], segment.Start.Item); Assert.Same(rows[1], segment.End.Item);
        Assert.True(segment.IsInterpolated);
        Assert.True(Math.Abs(segment.Point.Y - 4) > .4);
        var nearest = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(host.Project(new(1, expectedY)), .001));
        Assert.InRange(nearest.DistanceSquared, 0, .000001);
    }

    [AvaloniaTheory]
    [InlineData(ChartLineInterpolation.Smooth)]
    [InlineData(ChartLineInterpolation.StepBefore)]
    [InlineData(ChartLineInterpolation.StepAfter)]
    [InlineData(ChartLineInterpolation.StepCenter)]
    public void Shaped_hits_do_not_bridge_missing_points_or_axis_break_interiors(ChartLineInterpolation mode)
    {
        var line = LineInterpolationTests.CreateLine([new("a", 0, 20), new("b", 2, 80), new("gap", 4, double.NaN), new("c", 6, 20), new("d", 8, 80)]);
        line.Interpolation = mode;
        using var host = new SegmentTestHost(new(0, 8, BreakStart: 6.5, BreakEnd: 7.5, Compression: .2), new(0, 100), line);
        Assert.Null(host.Chart.HitTestSegment(host.Project(new(4, 50)), 1));
        Assert.Null(host.Chart.HitTestSegment(host.Project(new(7, 20)), 1000));
        Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(host.Project(new(0, 20)), .1));
    }

    [AvaloniaFact]
    public void Range_trackball_exposes_the_lower_path_and_raw_source_identity()
    {
        GeometryRangeRow[] rows = [new("a", 0, 20, 80), new("b", 2, 40, 60)];
        var range = new RangeAreaSeries
        {
            ItemsSource = rows, CategoryPath = nameof(GeometryRangeRow.X), LowPath = nameof(GeometryRangeRow.Low),
            HighPath = nameof(GeometryRangeRow.High), IdPath = nameof(GeometryRangeRow.Id), ShowMarkers = false,
        };
        using var host = new SegmentTestHost(new(0, 2), new(0, 100), range);
        host.Chart.TrackballMode = ChartTrackballMode.Vertical;
        host.Chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        host.Move(host.Project(new(1, 50)));
        var reading = Assert.Single(host.Chart.TrackballReadings);
        Assert.Equal(70, reading.Point.Y, 7); Assert.Equal(30, reading.BaseValue!.Value, 7);
        Assert.Equal(20, reading.Segment!.Start.BaseValue); Assert.Equal(40, reading.Segment.End.BaseValue);
        Assert.Same(rows[0], reading.Segment.Start.Item); Assert.Same(rows[1], reading.Segment.End.Item);
        Assert.Equal("a", reading.Segment.Start.StableId); Assert.Equal("b", reading.Segment.End.StableId);
    }

    [AvaloniaTheory]
    [InlineData(ChartLineInterpolation.StepBefore)]
    [InlineData(ChartLineInterpolation.StepAfter)]
    [InlineData(ChartLineInterpolation.StepCenter)]
    public void Degenerate_step_legs_at_a_real_endpoint_keep_that_endpoint_identity(ChartLineInterpolation mode)
    {
        SegmentRow[] rows = [new("start", 0, 20), new("end", 2, 20)];
        var line = LineInterpolationTests.CreateLine(rows); line.Interpolation = mode;
        using var host = new SegmentTestHost(new(0, 2), new(0, 100), line);
        var segment = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(host.Project(new(2, 20)), .1));
        Assert.Equal(1, segment.Fraction); Assert.False(segment.IsInterpolated);
        Assert.Same(rows[1], segment.Original.Item);
    }

    [AvaloniaTheory]
    [InlineData(ChartLineInterpolation.Linear)]
    [InlineData(ChartLineInterpolation.Smooth)]
    [InlineData(ChartLineInterpolation.StepBefore)]
    [InlineData(ChartLineInterpolation.StepAfter)]
    [InlineData(ChartLineInterpolation.StepCenter)]
    public void Compatible_stacks_share_exact_boundaries_including_nonuniform_x_and_gaps(ChartLineInterpolation mode)
    {
        ChartPoint[] first = [new(0, 2), new(1, 8), new(2, double.NaN), new(4, 7), new(8, 5), new(10, 9)];
        var second = first.Select(p => p with { Y = p.Y + 3 }).ToArray();
        var a = Context(new AreaSeries { StackMode = ChartStackMode.Stacked, Interpolation = mode }, first, first.Select(_ => 0d).ToArray());
        var b = Context(new AreaSeries { StackMode = ChartStackMode.Stacked, Interpolation = mode }, second, first.Select(p => p.Y).ToArray());
        ChartLineGeometry.PrepareStackGeometry([a, b]);
        var upper = ChartLineGeometry.Build(a); var next = ChartLineGeometry.Build(b);
        Assert.Equal(upper.Count, next.Count);
        for (var i = 0; i < upper.Count; i++) Assert.Equal(upper[i].Upper, next[i].Lower);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Common_smooth_stack_limiter_preserves_positive_thickness_shared_edges_and_continuous_tangents(bool inverseY)
    {
        // Independent, shape-preserving cumulative splines have a real crossing:
        // their four ordered samples alone are insufficient to prevent it.
        double[] lower = [13.1, 15.7, 55.1, 78];
        double[] upper = [13.185, 15.702, 55.111, 78.035];
        const double t = .03;
        var u = 1 - t;
        var oldLower = u * u * u * 15.7 + 3 * u * u * t * (15.7 + 2.6 / 3) +
            3 * u * t * t * (55.1 - 22.9 / 3) + t * t * t * 55.1;
        var oldUpper = u * u * u * 15.702 + 3 * u * u * t * (15.702 + 2.517 / 3) +
            3 * u * t * t * (55.111 - 22.924 / 3) + t * t * t * 55.111;
        Assert.True(oldUpper < oldLower - .0003);
        var a = Context(new AreaSeries { StackMode = ChartStackMode.Stacked, Interpolation = ChartLineInterpolation.Smooth },
            lower.Select((y, i) => new ChartPoint(i, y)).ToArray(), [0, 0, 0, 0], inverseY);
        var b = Context(new AreaSeries { StackMode = ChartStackMode.Stacked, Interpolation = ChartLineInterpolation.Smooth },
            upper.Select((y, i) => new ChartPoint(i, y)).ToArray(), lower, inverseY);
        var c = Context(new AreaSeries { StackMode = ChartStackMode.Stacked, Interpolation = ChartLineInterpolation.Smooth },
            upper.Select((y, i) => new ChartPoint(i, y + .002)).ToArray(), upper, inverseY);
        ChartLineGeometry.PrepareStackGeometry([a, b, c]);
        var figures = new[] { a, b, c }.Select(context => Assert.Single(ChartLineGeometry.Build(context))).ToArray();
        Assert.Equal(figures[0].Upper, figures[1].Lower); Assert.Equal(figures[1].Upper, figures[2].Lower);
        foreach (var figure in figures)
            for (var i = 0; i < figure.Upper.Count; i++)
            {
                for (var sample = 0; sample <= 1000; sample++)
                {
                    var high = figure.Upper[i].At(sample / 1000d).Y; var low = figure.Lower[i].At(sample / 1000d).Y;
                    Assert.True(inverseY ? high <= low + 1e-12 : high >= low - 1e-12);
                }
                if (i > 0)
                {
                    var previous = figure.Upper[i - 1]; var current = figure.Upper[i];
                    Assert.Equal((previous.End.Y - previous.Control2.Y) / (previous.End.X - previous.Control2.X),
                        (current.Control1.Y - current.Start.Y) / (current.Control1.X - current.Start.X), 9);
                }
            }
    }

    [AvaloniaFact]
    public void Smooth_curve_clipping_uses_the_true_cubic_intersection_at_the_viewport_edge()
    {
        var line = LineInterpolationTests.CreateLine([new("before", -2, -2), new("inside", 2, 8), new("last", 4, 9)]);
        line.Interpolation = ChartLineInterpolation.Smooth;
        using var host = new SegmentTestHost(new(0, 4), new(0, 10), line);
        // Independent Bezier at t=.5: Y=(-2)/8+3*(4/3)/8+3*(22/3)/8+8/8=4.
        var position = host.Project(new(0, 4));
        var hit = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(position, .000001));
        Assert.Equal(.5, hit.Fraction, 10); Assert.Equal(4, hit.Point.Y, 9);
        Assert.Equal(position.X, hit.Position.X, 9);
    }

    [AvaloniaFact]
    public void Smooth_curve_touching_the_plot_edge_keeps_its_real_endpoint_identity()
    {
        SegmentRow[] rows = [new("a", 0, 4), new("peak", 2, 10), new("c", 4, 4)];
        var line = LineInterpolationTests.CreateLine(rows); line.Interpolation = ChartLineInterpolation.Smooth;
        using var host = new SegmentTestHost(new(0, 4), new(0, 10), line);
        var hit = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(host.Project(new(2, 10)), .000001));
        Assert.False(hit.IsInterpolated); Assert.Same(rows[1], hit.Original.Item);
        Assert.Equal(rows[1].Point, hit.Point);
    }

    [AvaloniaFact]
    public void Extreme_finite_custom_projection_coordinates_do_not_publish_nan_geometry()
    {
        var context = Context(new LineSeries { Interpolation = ChartLineInterpolation.Smooth },
            [new(-1e308, -1e308), new(0, 0), new(1e308, 1e308)]);
        var figure = Assert.Single(ChartLineGeometry.Build(context));
        foreach (var part in figure.Upper)
        {
            Assert.True(double.IsFinite(part.Start.X)); Assert.True(double.IsFinite(part.End.Y));
            for (var i = 0; i <= 10; i++)
            {
                var point = part.At(i / 10d);
                Assert.True(double.IsFinite(point.X)); Assert.True(double.IsFinite(point.Y));
            }
        }
    }

    private static ChartSeriesRenderContext Context(ChartSeries series, ChartPoint[] points, double[]? lower = null, bool inverseY = false)
    {
        var data = new ChartDataSnapshot(0, points.Select((point, i) => new ChartDataPoint(i, i, point, point.X, point,
            !double.IsFinite(point.Y))).ToArray());
        if (lower is not null) data = data.WithBandValues(points.Select((p, i) => new ChartDataValue(i, p, false, false)).ToArray(),
            points.Select((p, i) => new ChartBandValue(lower[i], null, p)).ToArray());
        return new(series, data, new GeometryProjection(inverseY), Brushes.Navy);
    }
    private sealed class GeometryProjection(bool inverseY = false, bool logarithmic = false) : IChartProjection
    {
        public Rect PlotArea => new(-100, -100, 200, 200);
        public double YOrigin => 1;
        public Point Project(ChartPoint point) => new(MapX(point.X), MapY(point.Y));
        public ChartPoint Unproject(Point point) => new(point.X, logarithmic ? Math.Pow(10, point.Y) : inverseY ? -point.Y : point.Y);
        public double MapX(double value) => value;
        public double MapY(double value) => logarithmic ? value > 0 ? Math.Log10(value) : double.NaN : inverseY ? -value : value;
    }
    private sealed record GeometryRangeRow(string Id, double X, double Low, double High);
}
