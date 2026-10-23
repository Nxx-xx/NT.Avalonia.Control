using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class CircularGeometryTests
{
    [AvaloniaFact]
    public void Circular_defaults_and_validation_distinguish_pie_donut_and_top_half_ring()
    {
        var pie = new PieSeries();
        Assert.Equal(-90, pie.StartAngle); Assert.Equal(360, pie.SweepAngle); Assert.True(pie.Clockwise);
        Assert.Equal(0, pie.InnerRadius); Assert.Equal(1, pie.Radius);
        Assert.Equal(ChartCircularNegativeValueMode.Ignore, pie.NegativeValueMode);
        Assert.Equal(ChartCircularSmallSliceMode.Keep, pie.SmallSliceMode); Assert.Equal(-1, pie.ExplodedIndex);
        Assert.Equal(.6, new DonutSeries().InnerRadius);
        var half = new SemiDonutSeries();
        Assert.Equal(.6, half.InnerRadius); Assert.Equal(180, half.StartAngle); Assert.Equal(180, half.SweepAngle);
        Assert.Throws<ArgumentException>(() => pie.StartAngle = double.PositiveInfinity);
        Assert.Throws<ArgumentException>(() => pie.SweepAngle = -1);
        Assert.Throws<ArgumentException>(() => pie.SweepAngle = 361);
        Assert.Throws<ArgumentException>(() => pie.InnerRadius = 1);
        Assert.Throws<ArgumentException>(() => pie.InnerRadius = double.NaN);
        Assert.Throws<ArgumentException>(() => pie.Radius = -1);
        Assert.Throws<ArgumentException>(() => pie.Radius = 1.01);
        Assert.Throws<ArgumentException>(() => pie.SliceGap = double.NaN);
        Assert.Throws<ArgumentException>(() => pie.MinimumSliceAngle = 361);
        Assert.Throws<ArgumentException>(() => pie.ExplodeOffset = -1);
        Assert.Throws<ArgumentException>(() => pie.ExplodedIndex = -2);
        Assert.Throws<ArgumentException>(() => pie.NegativeValueMode = (ChartCircularNegativeValueMode)99);
        Assert.Throws<ArgumentException>(() => pie.SmallSliceMode = (ChartCircularSmallSliceMode)99);
    }

    [AvaloniaFact]
    public void Ignored_zero_negative_missing_and_nonfinite_rows_never_create_a_sector_or_change_identity()
    {
        var source = Data([0, -4, double.NaN, double.PositiveInfinity, 2, 6]);
        var slices = CircularChartGeometry.Build(new PieSeries(), source, new Point(100, 100), 80);
        Assert.Equal(2, slices.Count);
        Assert.Equal(new[] { 14, 15 }, slices.Select(slice => slice.Data.SourceIndex));
        Assert.Equal(.25, slices[0].Fraction); Assert.Equal(.75, slices[1].Fraction);
        Assert.Equal(90, slices[0].SweepAngle); Assert.Equal(270, slices[1].SweepAngle);
        Assert.Same(source.Points[4].Item, slices[0].Data.Item);
        Assert.Equal(source.Points[4].StableId, slices[0].Data.StableId);
        Assert.Equal(source.Points[4].SourceIndex, slices[0].Hit.Index);
        Assert.Equal(source.Points[4].Point, slices[0].Hit.Point);
        Assert.Equal(-4, source.Points[1].Point.Y);
        Assert.All(slices, slice => Assert.True(slice.IsVisible));
        Assert.Empty(CircularChartGeometry.Build(new PieSeries(), Data([0, -1, double.NaN]), default, 10));
    }

    [AvaloniaFact]
    public void Absolute_negative_policy_retains_the_original_signed_value_and_throw_names_the_source_index()
    {
        var source = Data([-1, 3]);
        var series = new PieSeries { NegativeValueMode = ChartCircularNegativeValueMode.Absolute };
        var slices = CircularChartGeometry.Build(series, source, default, 100);
        Assert.Equal(.25, slices[0].Fraction); Assert.Equal(-1, slices[0].Data.Point.Y);
        series.NegativeValueMode = ChartCircularNegativeValueMode.Throw;
        var error = Assert.Throws<InvalidOperationException>(() => CircularChartGeometry.Build(series, source, default, 100));
        Assert.Contains("10", error.Message);
        Assert.Empty(CircularChartGeometry.Build(series, Data([double.NegativeInfinity]), default, 100));
    }

    [AvaloniaFact]
    public void Published_finite_empty_rows_remain_absent_even_with_a_positive_coordinate()
    {
        var source = new ChartDataSnapshot(0, new ChartDataPoint[]
        {
            new(5, "gap", null, "gap", new ChartPoint(0, 20), true) { WasEmpty = true },
            new(8, "value", null, "value", new ChartPoint(1, 4), false),
        });
        var slice = Assert.Single(CircularChartGeometry.Build(new PieSeries(), source, default, 100));
        Assert.Equal(8, slice.Data.SourceIndex); Assert.Equal(1, slice.Fraction);
        Assert.True(source.Points[0].IsEmpty); Assert.Equal(20, source.Points[0].Point.Y);
    }

    [AvaloniaTheory]
    [InlineData(double.MaxValue, double.MaxValue)]
    [InlineData(double.Epsilon, double.Epsilon)]
    [InlineData(1e-300, 1e-300)]
    public void Max_scaled_normalization_handles_overflow_and_subnormal_totals(double first, double second)
    {
        var slices = CircularChartGeometry.Build(new DonutSeries(), Data([first, second]), default, 100);
        Assert.Equal(2, slices.Count);
        Assert.All(slices, slice => { Assert.Equal(.5, slice.Fraction); Assert.Equal(180, slice.SweepAngle); Assert.True(slice.IsVisible); });
        Assert.Equal(360, slices.Sum(slice => slice.RawSweepAngle));
    }

    [AvaloniaFact]
    public void Fraction_underflow_is_not_replaced_by_a_fictional_minimum_sector()
    {
        var slices = CircularChartGeometry.Build(new PieSeries { MinimumSliceAngle = 8 }, Data([double.MaxValue, double.Epsilon]), default, 100);
        Assert.Equal(2, slices.Count); Assert.Equal(1, slices[0].Fraction);
        Assert.Equal(0, slices[1].Fraction); Assert.False(slices[1].IsVisible);
        Assert.Equal(0, slices[1].SweepAngle); Assert.Equal(360, slices[0].SweepAngle);
    }

    [AvaloniaTheory]
    [InlineData(true, 90d)]
    [InlineData(false, -90d)]
    public void Direction_start_angle_and_partial_sweep_preserve_source_order(bool clockwise, double firstSweep)
    {
        var series = new PieSeries { StartAngle = 450, SweepAngle = 270, Clockwise = clockwise };
        var slices = CircularChartGeometry.Build(series, Data([1, 2]), default, 100);
        Assert.Equal(90, slices[0].StartAngle); Assert.Equal(firstSweep, slices[0].SweepAngle);
        Assert.Equal(90 + firstSweep, slices[1].StartAngle);
        Assert.Equal(firstSweep * 2, slices[1].SweepAngle);
        Assert.Equal(new[] { 10, 11 }, slices.Select(slice => slice.Data.SourceIndex));
        Assert.Equal(1, slices.Sum(slice => slice.Fraction), 13);
    }

    [AvaloniaFact]
    public void Gaps_and_small_sector_hiding_do_not_redistribute_original_shares_or_swallow_neighbors()
    {
        var series = new PieSeries { StartAngle = 0, SliceGap = 4, MinimumSliceAngle = 5 };
        var source = Data([99, 1]);
        var slices = CircularChartGeometry.Build(series, source, default, 100);
        Assert.Equal(.99, slices[0].Fraction); Assert.Equal(.01, slices[1].Fraction);
        Assert.Equal(356.4, slices[0].RawSweepAngle, 10);
        Assert.Equal(352.4, slices[0].SweepAngle, 10);
        Assert.False(slices[1].IsVisible); // The 4-degree gap consumes only this 3.6-degree paint span.
        Assert.Equal(0, slices[1].SweepAngle);
        series.SliceGap = 0;
        slices = CircularChartGeometry.Build(series, source, default, 100);
        Assert.True(slices[1].IsVisible); Assert.Equal(3.6, slices[1].SweepAngle, 10);
        series.SmallSliceMode = ChartCircularSmallSliceMode.Hide;
        slices = CircularChartGeometry.Build(series, source, default, 100);
        Assert.False(slices[1].IsVisible);
        Assert.Equal(.01, slices[1].Fraction); Assert.Equal(3.6, slices[1].RawSweepAngle, 10);
        Assert.Equal(356.4, slices[0].SweepAngle, 10);
        Assert.False(CircularChartGeometry.Contains(slices[1], CircularChartGeometry.Anchor(slices[1])));
        Assert.Equal(double.PositiveInfinity, CircularChartGeometry.DistanceSquared(slices[1], default));
    }

    [AvaloniaFact]
    public void Small_sector_keep_renders_its_real_share_without_minimum_angle_inflation()
    {
        var series = new PieSeries { MinimumSliceAngle = 10, SmallSliceMode = ChartCircularSmallSliceMode.Keep };
        var slice = CircularChartGeometry.Build(series, Data([9999, 1]), default, 100)[1];
        Assert.True(slice.IsVisible); Assert.Equal(.0001, slice.Fraction);
        Assert.Equal(.036, slice.SweepAngle, 10);
        Assert.True(CircularChartGeometry.Contains(slice, CircularChartGeometry.Anchor(slice)));
    }

    [AvaloniaFact]
    public void Exploded_index_is_an_original_source_index_and_additional_offsets_use_the_raw_bisector()
    {
        var source = Data([0, 1, 1]);
        var series = new PieSeries { StartAngle = 0, ExplodedIndex = 11, ExplodeOffset = 10, SliceGap = 20 };
        var slices = CircularChartGeometry.Build(series, source, new Point(100, 100), 80,
            point => point.SourceIndex == 11 ? 5 : 0);
        Assert.Equal(100, slices[0].Center.X, 10); Assert.Equal(115, slices[0].Center.Y, 10);
        Assert.Equal(new Point(100, 100), slices[1].Center);
        Assert.Equal(new Point(100, 100), CircularChartGeometry.Build(series, source, new Point(100, 100), 80)[1].Center);
        Assert.Throws<InvalidOperationException>(() => CircularChartGeometry.Build(series, source, default, 10, _ => double.NaN));
    }

    [AvaloniaFact]
    public void Ring_hole_outer_edge_and_radial_boundaries_have_analytic_distance()
    {
        var slice = new CircularSliceInfo(new DonutSeries(), Data([1]).Points[0], 1, 0, 90, default, 50, 100);
        Assert.True(CircularChartGeometry.Contains(slice, new Point(75, 0)));
        Assert.True(CircularChartGeometry.Contains(slice, new Point(50, 0)));
        Assert.True(CircularChartGeometry.Contains(slice, new Point(100, 0)));
        Assert.True(CircularChartGeometry.Contains(slice, new Point(0, 75)));
        Assert.False(CircularChartGeometry.Contains(slice, new Point(49, 0)));
        Assert.False(CircularChartGeometry.Contains(slice, new Point(101, 0)));
        Assert.False(CircularChartGeometry.Contains(slice, new Point(75, -1)));
        Assert.Equal(2500, CircularChartGeometry.DistanceSquared(slice, default), 9);
        Assert.Equal(100, CircularChartGeometry.DistanceSquared(slice, new Point(110, 0)), 9);
        Assert.Equal(1, CircularChartGeometry.DistanceSquared(slice, new Point(75, -1)), 9);
        Assert.Equal(6250, CircularChartGeometry.DistanceSquared(slice, new Point(-25, -25)), 9);
        Assert.Equal(new Rect(0, 0, 100, 100), CircularChartGeometry.Bounds(slice));
    }

    [AvaloniaTheory]
    [InlineData(330d, 60d, 0d, 180d)]
    [InlineData(30d, -60d, 0d, 180d)]
    [InlineData(30d, 300d, 180d, 0d)]
    [InlineData(330d, -300d, 180d, 0d)]
    public void Wraparound_and_reflex_sectors_share_the_same_hit_domain(double start, double sweep, double inside, double outside)
    {
        var slice = new CircularSliceInfo(new PieSeries(), Data([1]).Points[0], 1, start, sweep, default, 0, 100);
        Assert.True(CircularChartGeometry.Contains(slice, CircularChartGeometry.PointAt(slice, inside, 50)));
        Assert.False(CircularChartGeometry.Contains(slice, CircularChartGeometry.PointAt(slice, outside, 50)));
        Assert.Equal(0, CircularChartGeometry.DistanceSquared(slice, CircularChartGeometry.PointAt(slice, inside, 50)));
        Assert.True(CircularChartGeometry.DistanceSquared(slice, CircularChartGeometry.PointAt(slice, outside, 50)) > 0);
        Assert.True(CircularChartGeometry.Contains(slice, default));
    }

    [AvaloniaTheory]
    [InlineData(.7, .001)]
    [InlineData(.7, 180d)]
    [InlineData(2.1, -30d)]
    [InlineData(47.3, 130.7)]
    [InlineData(310.8, -279.3)]
    public void Noncardinal_radial_and_arc_boundaries_are_inclusive_without_a_fixed_angular_hit_epsilon(double start, double sweep)
    {
        var slice = new CircularSliceInfo(new DonutSeries(), Data([1]).Points[0], 1, start, sweep, default, 35, 90);
        foreach (var angle in new[] { start, start + sweep, start + sweep / 2 })
        foreach (var radius in new[] { 35d, 60d, 90d })
            Assert.True(CircularChartGeometry.Contains(slice, CircularChartGeometry.PointAt(slice, angle, radius)), $"angle {angle}; radius {radius}");
        var tiny = slice with { StartAngle = 0, SweepAngle = 1e-15 };
        Assert.False(CircularChartGeometry.Contains(tiny, CircularChartGeometry.PointAt(tiny, 1e-13, 60)));
    }

    [AvaloniaFact]
    public void Top_half_donut_and_radius_options_have_expected_bounds_and_do_not_hit_the_lower_half()
    {
        var slice = Assert.Single(CircularChartGeometry.Build(new SemiDonutSeries { Radius = .8 }, Data([1]), new Point(100, 100), 100));
        Assert.Equal(80, slice.OuterRadius); Assert.Equal(48, slice.InnerRadius);
        Assert.True(CircularChartGeometry.Contains(slice, new Point(100, 40)));
        Assert.False(CircularChartGeometry.Contains(slice, new Point(100, 160)));
        Assert.False(CircularChartGeometry.Contains(slice, new Point(100, 100)));
        var bounds = CircularChartGeometry.Bounds(slice);
        Assert.Equal(20, bounds.Left, 9); Assert.Equal(20, bounds.Top, 9);
        Assert.Equal(180, bounds.Right, 9); Assert.Equal(100, bounds.Bottom, 9);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Full_circle_geometry_has_a_real_hole_and_has_no_artificial_radial_seam(bool clockwise)
    {
        var series = new DonutSeries { Clockwise = clockwise };
        var slice = Assert.Single(CircularChartGeometry.Build(series, Data([1]), default, 100));
        var geometry = Record(slice);
        Assert.Equal(FillRule.NonZero, geometry.FillRule);
        Assert.Equal(2, geometry.Figures.Count); Assert.Equal(4, geometry.Arcs.Count); Assert.Equal(0, geometry.Lines);
        var outerDirection = clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
        var innerDirection = clockwise ? SweepDirection.CounterClockwise : SweepDirection.Clockwise;
        Assert.All(geometry.Arcs.Take(2), arc => { Assert.Equal(outerDirection, arc.Direction); Assert.Equal(100, arc.Radius); Assert.False(arc.Large); });
        Assert.All(geometry.Arcs.Skip(2), arc => { Assert.Equal(innerDirection, arc.Direction); Assert.Equal(60, arc.Radius); Assert.False(arc.Large); });
        Assert.False(geometry.Contains(default));
        Assert.True(geometry.Contains(new Point(80, 0)));
        Assert.True(geometry.Contains(new Point(-80, 0)));
        Assert.True(geometry.Contains(new Point(0, 80)));
        Assert.True(geometry.Contains(new Point(0, -80)));
        Assert.False(geometry.Contains(new Point(101, 0)));
        Assert.Equal(new Rect(-100, -100, 200, 200), CircularChartGeometry.Bounds(slice));
    }

    [AvaloniaFact]
    public void Partial_annular_geometry_and_analytic_hit_agree_away_from_antialias_edges()
    {
        var slice = new CircularSliceInfo(new DonutSeries(), Data([1]).Points[0], 1, 310, -235, new Point(100, 100), 35, 90);
        var geometry = Record(slice);
        Assert.Single(geometry.Figures); Assert.Equal(2, geometry.Arcs.Count); Assert.Equal(1, geometry.Lines);
        Assert.All(geometry.Arcs, arc => Assert.True(arc.Large));
        foreach (var angle in new[] { 0d, 40d, 100d, 160d, 220d, 280d, 340d })
        foreach (var radius in new[] { 10d, 55d, 110d })
        {
            var point = CircularChartGeometry.PointAt(slice, angle, radius);
            Assert.Equal(CircularChartGeometry.Contains(slice, point), geometry.Contains(point));
        }
    }

    [AvaloniaFact]
    public void Zero_radius_zero_sweep_hidden_and_exiting_sectors_are_not_hittable()
    {
        var zeroRadius = Assert.Single(CircularChartGeometry.Build(new PieSeries { Radius = 0 }, Data([1]), default, 100));
        var zeroSweep = Assert.Single(CircularChartGeometry.Build(new PieSeries { SweepAngle = 0 }, Data([1]), default, 100));
        Assert.False(zeroRadius.IsVisible); Assert.False(zeroSweep.IsVisible);
        Assert.False(CircularChartGeometry.Contains(zeroRadius, default));
        Assert.False(CircularChartGeometry.Contains(zeroSweep, default));
        var visible = Assert.Single(CircularChartGeometry.Build(new PieSeries(), Data([1]), default, 100));
        var exiting = visible with { IsExiting = true };
        Assert.False(CircularChartGeometry.Contains(exiting, new Point(50, 0)));
        Assert.Equal(double.PositiveInfinity, CircularChartGeometry.DistanceSquared(exiting, new Point(50, 0)));
        Assert.True(Record(exiting).Contains(new Point(50, 0)));
        Assert.False(CircularChartGeometry.Contains(visible, new Point(double.NaN, 0)));
        Assert.Equal(double.PositiveInfinity, CircularChartGeometry.DistanceSquared(visible, new Point(double.PositiveInfinity, 0)));
    }

    [AvaloniaFact]
    public void Invalid_layout_is_rejected_before_projecting_or_drawing()
    {
        var series = new PieSeries(); var data = Data([1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => CircularChartGeometry.Build(series, data, default, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CircularChartGeometry.Build(series, data, default, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => CircularChartGeometry.Build(series, data, new Point(double.NaN, 0), 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => CircularChartGeometry.Build(series, data, default, double.MaxValue));
    }

    private static ChartDataSnapshot Data(double[] values) => new(0, values.Select((value, index) =>
        new ChartDataPoint(index + 10, $"id-{index}", new object(), $"category-{index}", new ChartPoint(index, value), !double.IsFinite(value))).ToArray());

    private static PathRecorder Record(CircularSliceInfo slice)
    {
        var recorder = new PathRecorder();
        CircularChartGeometry.WriteGeometry(slice, recorder);
        recorder.Dispose();
        return recorder;
    }

    // Independently flatten the recorded circular SVG arcs. This validates the
    // production commands under the endpoint-only Headless geometry backend; actual
    // renderer pixels are covered by the native Demo smoke on both platforms.
    private sealed class PathRecorder : ICircularGeometrySink
    {
        internal sealed record Arc(double Radius, bool Large, SweepDirection Direction);
        internal List<Arc> Arcs { get; } = [];
        internal List<List<Point>> Figures { get; } = [];
        internal int Lines { get; private set; }
        internal FillRule FillRule { get; private set; }
        private List<Point>? _current;
        public void BeginFigure(Point startPoint, bool isFilled)
        { Assert.True(isFilled); Assert.Null(_current); _current = [startPoint]; }
        public void EndFigure(bool isClosed)
        { Assert.True(isClosed); Assert.NotNull(_current); Figures.Add(_current); _current = null; }
        public void SetFillRule(FillRule fillRule) => FillRule = fillRule;
        public void LineTo(Point point) { Assert.NotNull(_current); Lines++; _current.Add(point); }
        public void ArcTo(Point point, Size size, double rotationAngle, bool isLargeArc, SweepDirection sweepDirection)
        {
            Assert.NotNull(_current); Assert.Equal(size.Width, size.Height); Assert.Equal(0, rotationAngle);
            var radius = size.Width; Arcs.Add(new(radius, isLargeArc, sweepDirection));
            var start = _current[^1]; var chord = new Vector(point.X - start.X, point.Y - start.Y); var chordLength = chord.Length;
            Assert.True(chordLength > 0);
            var middle = new Point(start.X / 2 + point.X / 2, start.Y / 2 + point.Y / 2);
            var perpendicular = new Vector(-chord.Y / chordLength, chord.X / chordLength);
            var distance = Math.Sqrt(Math.Max(0, radius * radius - chordLength * chordLength / 4));
            var candidates = new[] { middle + perpendicular * distance, middle - perpendicular * distance };
            var clockwise = sweepDirection == SweepDirection.Clockwise;
            var center = candidates.OrderBy(candidate =>
            {
                var sweep = DirectedAngle(start, point, candidate, clockwise);
                return isLargeArc ? Math.Abs(sweep) < Math.PI - 1e-7 ? 1 : 0 : Math.Abs(sweep) > Math.PI + 1e-7 ? 1 : 0;
            }).First();
            var sweepAngle = DirectedAngle(start, point, center, clockwise);
            var firstAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            var steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweepAngle) * 180 / Math.PI));
            for (var index = 1; index <= steps; index++)
            {
                var angle = firstAngle + sweepAngle * index / steps;
                _current.Add(index == steps ? point : new Point(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius));
            }
        }
        private static double DirectedAngle(Point start, Point end, Point center, bool clockwise)
        {
            var result = Math.Atan2(end.Y - center.Y, end.X - center.X) - Math.Atan2(start.Y - center.Y, start.X - center.X);
            if (clockwise) { while (result < 0) result += Math.PI * 2; }
            else { while (result > 0) result -= Math.PI * 2; }
            return result;
        }
        internal bool Contains(Point point)
        {
            var winding = 0;
            foreach (var figure in Figures)
            for (var index = 0; index < figure.Count; index++)
            {
                var start = figure[index]; var end = figure[(index + 1) % figure.Count];
                var cross = (end.X - start.X) * (point.Y - start.Y) - (point.X - start.X) * (end.Y - start.Y);
                if (start.Y <= point.Y && end.Y > point.Y && cross > 0) winding++;
                else if (start.Y > point.Y && end.Y <= point.Y && cross < 0) winding--;
            }
            return winding != 0;
        }
        public void Dispose() { Assert.Null(_current); }
    }
}
