using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class PolarGeometryTests
{
    [AvaloniaFact]
    public void Defaults_and_validation_distinguish_closed_polygons_area_and_numeric_lines()
    {
        var line = new PolarLineSeries();
        Assert.False(line.IsClosed); Assert.True(line.ShowMarkers); Assert.Equal(1, line.FillOpacity);
        Assert.Equal(0, line.InnerRadius); Assert.Equal(2, line.SectorGap);
        Assert.True(new PolarFilledSeries().IsClosed); Assert.Equal(.25, new PolarFilledSeries().FillOpacity); Assert.True(new RadialLineSeries().IsClosed);
        var radar = new RadarSeries(); Assert.True(radar.IsClosed); Assert.Equal(100, radar.MaxValue); Assert.Equal(.3, radar.FillOpacity);
        radar.MaxValue = null; Assert.Null(radar.MaxValue);
        Assert.Throws<ArgumentException>(() => radar.MaxValue = 0);
        Assert.Throws<ArgumentException>(() => radar.MaxValue = double.PositiveInfinity);
        Assert.Throws<ArgumentException>(() => line.InnerRadius = 1);
        Assert.Throws<ArgumentException>(() => line.InnerRadius = -.1);
        Assert.Throws<ArgumentException>(() => line.SectorGap = 361);
        Assert.Throws<ArgumentException>(() => line.SectorGap = double.NaN);
        Assert.Throws<ArgumentException>(() => line.FillOpacity = 1.1);
    }

    [AvaloniaFact]
    public void Numeric_axes_preserve_source_order_identity_and_distinguish_zero_from_invalid_radii()
    {
        var source = Data((270, 50), (0, 0), (90, -1), (180, double.NaN), (45, 100));
        var scene = Build(new PolarLineSeries(), source);
        Assert.Equal(5, scene.Points.Count); Assert.Equal(new[] { true, true, false, false, true }, scene.Points.Select(p => p.IsVisible));
        Assert.Equal(270, scene.Points[0].Data.Point.X); Near(new Point(0, -50), scene.Points[0].Position);
        Near(default, scene.Points[1].Position);
        Assert.Equal(new[] { 10, 11, 12, 13, 14 }, scene.Points.Select(p => p.Data.SourceIndex));
        Assert.Same(source.Points[0].Item, scene.Points[0].Hit.Item);
        Assert.Equal(source.Points[0].StableId, scene.Points[0].Hit.StableId);
        Assert.Equal(2, scene.Series[0].Paths.Count); Assert.Equal(2, scene.Series[0].Paths[0].Points.Count);
        Assert.Single(scene.Series[0].Paths[1].Points);
    }

    [AvaloniaFact]
    public void Radians_and_degrees_use_equivalent_explicit_domains_and_current_display_degrees()
    {
        var degrees = Build(new PolarScatterSeries(), Data((90, 50), (180, 100)));
        var radians = PolarChartGeometry.Build([(new PolarScatterSeries(), Data((Math.PI / 2, 50), (Math.PI, 100)))],
            default, 100, angleMaximum: Math.PI * 2, radiusMaximum: 100, startAngle: 0, angleUnit: ChartAngleUnit.Radians);
        for (var i = 0; i < degrees.Points.Count; i++)
        {
            Near(degrees.Points[i].Position, radians.Points[i].Position);
            Assert.Equal(degrees.Points[i].Angle, radians.Points[i].Angle, 12);
        }
        Assert.Equal(Math.PI / 2, radians.Points[0].Hit.Point.X);
        Assert.Empty(radians.Series[0].Paths);
    }

    [AvaloniaFact]
    public void Wrapping_keeps_exact_domain_endpoint_and_can_be_disabled_for_partial_arcs()
    {
        var series = new PolarLineSeries(); var data = Data((-90, 100), (0, 100), (360, 100), (450, 100));
        var wrapped = PolarChartGeometry.Build([(series, data)], default, 100, radiusMaximum: 100, startAngle: 0, sweepAngle: 180);
        Assert.Equal(new[] { 135d, 0, 180, 45 }, wrapped.Points.Select(p => p.Angle));
        Assert.All(wrapped.Points, p => Assert.True(p.IsVisible));
        var clipped = PolarChartGeometry.Build([(series, data)], default, 100, radiusMaximum: 100, startAngle: 0, sweepAngle: 180, wrapAngles: false);
        Assert.Equal(new[] { false, true, true, false }, clipped.Points.Select(p => p.IsVisible));
        Assert.Equal(new[] { 11, 12 }, Assert.Single(clipped.Series).Paths.Single().Points.Select(p => p.Data.SourceIndex));
    }

    [AvaloniaFact]
    public void Radial_domain_clips_outside_points_and_does_not_invent_boundary_source_items()
    {
        var data = Data((0, 10), (90, 20), (180, 80), (270, 100));
        var scene = PolarChartGeometry.Build([(new PolarLineSeries(), data)], default, 100, radiusMinimum: 20, radiusMaximum: 80, startAngle: 0);
        Assert.Equal(new[] { false, true, true, false }, scene.Points.Select(p => p.IsVisible));
        Assert.Equal(0, scene.Points[1].Radius); Assert.Equal(100, scene.Points[2].Radius);
        Assert.Equal(2, Assert.Single(scene.Series[0].Paths).Points.Count);
        Assert.Equal(4, scene.Points.Count);
    }

    [AvaloniaFact]
    public void Clockwise_and_axis_inversion_transform_geometry_without_mutating_source_coordinates()
    {
        var data = Data((90, 25)); var series = new PolarScatterSeries();
        var clockwise = Build(series, data);
        var counterclockwise = PolarChartGeometry.Build([(series, data)], default, 100, radiusMaximum: 100, startAngle: 0, clockwise: false);
        var inversed = PolarChartGeometry.Build([(series, data)], default, 100, radiusMaximum: 100, startAngle: 0, angleInversed: true, radiusInversed: true);
        Near(new Point(0, 25), clockwise.Points[0].Position); Near(new Point(0, -25), counterclockwise.Points[0].Position);
        Near(new Point(0, -75), inversed.Points[0].Position);
        Assert.Equal(data.Points[0].Point, inversed.Points[0].Hit.Point);
    }

    [AvaloniaFact]
    public void Shared_category_union_aligns_reordered_series_and_missing_categories_break_paths()
    {
        var a = new RadarSeries { MaxValue = null }; var b = new RadialLineSeries();
        var scene = PolarChartGeometry.Build([(a, Categories(("A", 100), ("B", 50), ("C", 25))),
            (b, Categories(("C", 75), ("A", 50), ("D", 100)))], default, 100, radiusMaximum: 100, startAngle: 0);
        Assert.Equal(new object?[] { "A", "B", "C", "D" }, scene.Categories);
        Assert.Equal(180, scene.Series[1].Points[0].Angle); Assert.Equal(0, scene.Series[1].Points[1].Angle);
        Assert.Equal(270, scene.Series[1].Points[2].Angle);
        Assert.Equal(new[] { 11 }, scene.Series[1].Paths[0].Points.Select(p => p.Data.SourceIndex));
        Assert.Equal(new[] { 10, 12 }, scene.Series[1].Paths[1].Points.Select(p => p.Data.SourceIndex));
        Assert.All(scene.Series.SelectMany(s => s.Paths), path => Assert.False(path.IsClosed));
    }

    [AvaloniaFact]
    public void Arbitrary_category_keys_and_null_use_value_equality_and_do_not_discard_duplicate_rows()
    {
        var source = Categories((new Category("one"), 10), (new Category("one"), 20), (null, 30));
        var scene = Build(new RadialLineSeries(), source);
        Assert.Equal(2, scene.Categories.Count); Assert.Equal(3, scene.Points.Count);
        Assert.Equal(scene.Points[0].Angle, scene.Points[1].Angle); Assert.NotEqual(scene.Points[1].Angle, scene.Points[2].Angle);
        Assert.Equal(3, scene.Points.Select(p => p.Data.SourceIndex).Distinct().Count());
    }

    [AvaloniaFact]
    public void Radar_default_maximum_is_explicit_and_null_uses_the_shared_axis_domain()
    {
        var radar = new RadarSeries(); var data = Categories(("A", 25), ("B", 50), ("C", 75));
        var fixedScale = PolarChartGeometry.Build([(radar, data)], default, 100, radiusMaximum: 200);
        Assert.Equal(25, fixedScale.Points[0].Radius); Assert.Equal(200, fixedScale.RadiusMaximum);
        radar.MaxValue = null;
        var shared = PolarChartGeometry.Build([(radar, data)], default, 100, radiusMaximum: 200);
        Assert.Equal(12.5, shared.Points[0].Radius);
        Assert.Equal(75, PolarChartGeometry.Build([(radar, data)], default, 100).RadiusMaximum);
    }

    [AvaloniaFact]
    public void Closed_paths_require_three_valid_points_and_never_close_across_a_gap()
    {
        var series = new PolarFilledSeries();
        var full = Build(series, Data((0, 50), (120, 100), (240, 75)));
        Assert.True(Assert.Single(full.Series[0].Paths).IsClosed);
        Assert.False(Assert.Single(Build(series, Data((0, 50), (120, 100))).Series[0].Paths).IsClosed);
        var gap = Build(series, Data((0, 50), (90, -1), (180, 75), (270, 100), (315, 50)));
        Assert.Equal(2, gap.Series[0].Paths.Count); Assert.All(gap.Series[0].Paths, p => Assert.False(p.IsClosed));
        var sink = new RecordingSink(); PolarChartGeometry.WritePathGeometry(gap.Series[0], true, sink);
        Assert.Equal(0, sink.BeginCount); Assert.Equal(0, sink.LineCount);
        var lineSink = new RecordingSink(); PolarChartGeometry.WritePathGeometry(gap.Series[0], false, lineSink);
        Assert.Equal(1, lineSink.BeginCount); Assert.Equal(2, lineSink.LineCount); Assert.Equal(0, lineSink.ClosedCount);
    }

    [AvaloniaFact]
    public void Equal_angle_area_and_rose_encode_distinct_radius_and_keep_zero_negative_and_empty_slots()
    {
        var values = Categories(("A", 100), ("B", 25), ("C", 64), ("D", 0), ("E", -10), ("F", double.NaN));
        var area = Build(new PolarAreaSeries { SectorGap = 0 }, values);
        var rose = Build(new NightingaleRoseSeries { SectorGap = 0 }, values);
        Assert.Equal(6, area.Sectors.Count); Assert.Equal(50, area.Sectors[1].OuterRadius); Assert.Equal(25, rose.Sectors[1].OuterRadius);
        Assert.Equal(80, area.Sectors[2].OuterRadius); Assert.Equal(64, rose.Sectors[2].OuterRadius);
        Assert.All(area.Sectors, sector => Assert.Equal(60, sector.SweepAngle));
        Assert.Equal(new[] { true, true, true, false, false, false }, area.Sectors.Select(s => s.IsVisible));
        Assert.Equal(new[] { 10, 11, 12, 13, 14, 15 }, area.Sectors.Select(s => s.Hit.Index));
        Assert.Empty(area.Series[0].Paths); Assert.Empty(area.Points); Assert.Empty(rose.Points);
    }

    [AvaloniaFact]
    public void Area_uses_annular_area_and_rose_uses_distance_above_the_inner_baseline()
    {
        var source = Categories(("A", 25));
        var area = Assert.Single(Build(new PolarAreaSeries { InnerRadius = .5 }, source).Sectors);
        var rose = Assert.Single(Build(new NightingaleRoseSeries { InnerRadius = .5 }, source).Sectors);
        Assert.Equal(50, area.InnerRadius); Assert.Equal(Math.Sqrt(4375), area.OuterRadius, 12);
        Assert.Equal(62.5, rose.OuterRadius); Assert.Equal(358, area.SweepAngle);
        Assert.False(PolarChartGeometry.Contains(area, default));
    }

    [AvaloniaFact]
    public void Sector_gap_is_centered_on_boundaries_and_does_not_renormalize_hidden_paint()
    {
        var series = new PolarAreaSeries { SectorGap = 10 };
        var sectors = Build(series, Categories(("A", 100), ("B", 100), ("C", 100), ("D", 100))).Sectors;
        Assert.Equal(5, sectors[0].StartAngle); Assert.Equal(80, sectors[0].SweepAngle);
        Assert.True(PolarChartGeometry.Contains(sectors[0], PolarChartGeometry.PointAt(default, 45, 50)));
        Assert.False(PolarChartGeometry.Contains(sectors[0], PolarChartGeometry.PointAt(default, 1, 50)));
        Assert.False(PolarChartGeometry.Contains(sectors[0], PolarChartGeometry.PointAt(default, 89, 50)));
        series.SectorGap = 100;
        Assert.All(Build(series, Categories(("A", 100), ("B", 100), ("C", 100), ("D", 100))).Sectors, p => Assert.False(p.IsVisible));
    }

    [AvaloniaFact]
    public void Radial_bar_uses_shared_category_lanes_fixed_gaps_and_value_proportional_arc_lengths()
    {
        var series = new RadialBarSeries { SectorGap = 0, InnerRadius = .2 };
        var scene = Build(series, Categories(("A", 25), ("B", 50), ("C", 75), ("D", 100)));
        var sectors = scene.Sectors;
        Assert.Equal(new[] { 90d, 180, 270, 360 }, sectors.Select(s => s.SweepAngle));
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(20 + (i + .04) * 20, sectors[i].InnerRadius, 12);
            Assert.Equal(20 + (i + .96) * 20, sectors[i].OuterRadius, 12);
            Assert.True(PolarChartGeometry.Contains(sectors[i], sectors[i].Anchor));
            Assert.Equal(sectors[i].Data.SourceIndex, sectors[i].Hit.Index);
        }
        Assert.False(PolarChartGeometry.Contains(sectors[0], PolarChartGeometry.PointAt(default, 45, 40)));
        Assert.False(PolarChartGeometry.Contains(sectors[0], PolarChartGeometry.PointAt(default, 135, 30)));
    }

    [AvaloniaFact]
    public void Reversed_and_counterclockwise_sector_hits_follow_signed_sweeps()
    {
        var data = Categories(("A", 100), ("B", 100), ("C", 100), ("D", 100));
        var series = new PolarAreaSeries { SectorGap = 0 };
        var reversed = PolarChartGeometry.Build([(series, data)], default, 100, radiusMaximum: 100, startAngle: 0, angleInversed: true);
        Assert.Equal(360, reversed.Sectors[0].StartAngle); Assert.Equal(-90, reversed.Sectors[0].SweepAngle);
        Assert.True(PolarChartGeometry.Contains(reversed.Sectors[0], PolarChartGeometry.PointAt(default, 315, 50)));
        Assert.False(PolarChartGeometry.Contains(reversed.Sectors[0], PolarChartGeometry.PointAt(default, 45, 50)));
        var ccw = PolarChartGeometry.Build([(series, data)], default, 100, radiusMaximum: 100, startAngle: 0, clockwise: false);
        Assert.Equal(-90, ccw.Sectors[0].SweepAngle); Near(reversed.Sectors[0].Anchor, ccw.Sectors[0].Anchor);
    }

    [AvaloniaFact]
    public void Full_ring_geometry_emits_two_closed_opposite_winding_figures_and_no_radial_seam()
    {
        var sector = Assert.Single(Build(new RadialBarSeries { SectorGap = 0, InnerRadius = .2 }, Categories(("A", 100))).Sectors);
        var sink = new RecordingSink(); PolarChartGeometry.WriteGeometry(sector, sink);
        Assert.Equal(FillRule.NonZero, sink.FillRule); Assert.Equal(2, sink.BeginCount); Assert.Equal(2, sink.ClosedCount);
        Assert.Equal(4, sink.Arcs.Count); Assert.Equal(0, sink.LineCount);
        Assert.Equal(new[] { SweepDirection.Clockwise, SweepDirection.Clockwise, SweepDirection.CounterClockwise, SweepDirection.CounterClockwise }, sink.Arcs);
        Assert.True(PolarChartGeometry.Contains(sector, sector.Anchor));
        Assert.False(PolarChartGeometry.Contains(sector, default));
    }

    [AvaloniaFact]
    public void Sector_distance_respects_holes_curved_edges_and_radial_ends()
    {
        var sector = new PolarSectorInfo(new PolarAreaSeries(), Data((0, 1)).Points[0], default, 20, 80, 0, 90);
        Assert.Equal(0, PolarChartGeometry.DistanceSquared(sector, new Point(30, 30)));
        Assert.Equal(400, PolarChartGeometry.DistanceSquared(sector, default));
        Assert.Equal(100, PolarChartGeometry.DistanceSquared(sector, new Point(90, 0)), 9);
        Assert.Equal(100, PolarChartGeometry.DistanceSquared(sector, new Point(40, -10)), 9);
        Assert.True(PolarChartGeometry.Contains(sector, new Point(20, 0)));
        Assert.False(PolarChartGeometry.Contains(sector, new Point(19.999, 0)));
        Assert.False(PolarChartGeometry.Contains(sector, new Point(80.001, 0)));
        Assert.Equal(double.PositiveInfinity, PolarChartGeometry.DistanceSquared(sector with { IsVisible = false }, default));
        Assert.False(PolarChartGeometry.Contains(sector, new Point(double.NaN, 0)));
    }

    [AvaloniaFact]
    public void Layout_and_domain_validation_reject_nonfinite_unrepresentable_and_reversed_inputs()
    {
        var sources = new[] { ((PolarSeries)new PolarLineSeries(), Data((0, 1))) };
        Assert.Throws<ArgumentOutOfRangeException>(() => PolarChartGeometry.Build(sources, default, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PolarChartGeometry.Build(sources, default, double.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => PolarChartGeometry.Build(sources, new Point(double.NaN, 0), 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => PolarChartGeometry.Build(sources, default, 10, angleMaximum: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PolarChartGeometry.Build(sources, default, 10, radiusMinimum: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PolarChartGeometry.Build(sources, default, 10, radiusMaximum: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PolarChartGeometry.Build(sources, default, 10, sweepAngle: 361));
        Assert.Throws<ArgumentOutOfRangeException>(() => PolarChartGeometry.Build(sources, default, 10, angleUnit: (ChartAngleUnit)7));
        Assert.Empty(PolarChartGeometry.Build([], default, 100).Points);
        Assert.False(Build(new PolarScatterSeries(), Data((0, 1)), 0).Points[0].IsVisible);
    }

    [AvaloniaFact]
    public void Extreme_finite_radial_values_and_subnormal_domains_stay_finite_without_fake_minimum_radius()
    {
        var scene = PolarChartGeometry.Build([(new PolarScatterSeries(), Data((0, double.MaxValue), (90, double.Epsilon)))], default, 100, radiusMaximum: double.MaxValue, startAngle: 0);
        Assert.Equal(100, scene.Points[0].Radius); Assert.Equal(0, scene.Points[1].Radius);
        Assert.True(double.IsFinite(scene.Points[0].Position.X));
        var tiny = PolarChartGeometry.Build([(new PolarScatterSeries(), Data((0, double.Epsilon)))], default, 100, radiusMaximum: double.Epsilon);
        Assert.Equal(100, tiny.Points[0].Radius); Assert.True(tiny.Points[0].IsVisible);
        Assert.Equal(1e-15, PolarChartGeometry.NormalizeAngle(1e-15));
    }

    private static PolarScene Build(PolarSeries series, ChartDataSnapshot data, double radius = 100) =>
        PolarChartGeometry.Build([(series, data)], default, radius, radiusMaximum: 100, startAngle: 0);
    private static ChartDataSnapshot Data(params (double Angle, double Radius)[] points) => new(0,
        points.Select((p, i) => new ChartDataPoint(i + 10, $"id-{i}", new object(), p.Angle, new ChartPoint(p.Angle, p.Radius), !double.IsFinite(p.Angle) || !double.IsFinite(p.Radius))).ToArray());
    private static ChartDataSnapshot Categories(params (object? Category, double Radius)[] points) => new(0,
        points.Select((p, i) => new ChartDataPoint(i + 10, $"id-{i}", new object(), p.Category, new ChartPoint(i, p.Radius), !double.IsFinite(p.Radius))).ToArray());
    private static void Near(Point expected, Point actual)
    { Assert.Equal(expected.X, actual.X, 9); Assert.Equal(expected.Y, actual.Y, 9); }
    private sealed record Category(string Name);
    private sealed class RecordingSink : ICircularGeometrySink
    {
        public FillRule FillRule { get; private set; }
        public int BeginCount { get; private set; }
        public int ClosedCount { get; private set; }
        public int LineCount { get; private set; }
        public List<SweepDirection> Arcs { get; } = [];
        public void SetFillRule(FillRule rule) => FillRule = rule;
        public void BeginFigure(Point start, bool isFilled) => BeginCount++;
        public void ArcTo(Point end, Size radii, double rotation, bool largeArc, SweepDirection direction) => Arcs.Add(direction);
        public void LineTo(Point end) => LineCount++;
        public void EndFigure(bool close) { if (close) ClosedCount++; }
    }
}
