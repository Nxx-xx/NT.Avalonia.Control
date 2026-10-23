using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class PolarAxisPresentationTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Homogeneous_area_and_rose_ticks_follow_the_business_value_radius(bool area)
    {
        PolarSeries series = area ? new PolarAreaSeries() : new NightingaleRoseSeries();
        series.InnerRadius = .15;
        var scene = Scene(series); var profile = Profile(scene);
        Assert.Equal(PolarAxisPresentationKind.ValueRadius, profile.Kind);
        Assert.Equal(area ? Math.Sqrt(.266875) * 100 : 36.25, profile.RadiusAtFraction(.25), 10);
        Assert.Equal(25, profile.ValueAtFraction(.25));
        Assert.Equal(scene.Sectors[1].OuterRadius, profile.RadiusAtFraction(.25), 10);
        // Four equal categories place the first sector's label at 315°, not its 270° boundary.
        var expected = 116 / Math.Sqrt(2);
        Near(new Point(expected, -expected), profile.CategoryLabelPosition(0));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Point_and_radar_ticks_include_the_inner_mapping_and_effective_series_maximum(bool inverted)
    {
        var radar = new RadarSeries { InnerRadius = .1, MaxValue = 200 };
        var scene = Scene(radar); var profile = PolarAxisPresentation.Create(scene, -90, 360, true, false, inverted);
        Assert.Equal(PolarAxisPresentationKind.ValueRadius, profile.Kind);
        Assert.Equal(200, profile.Maximum); Assert.Equal(50, profile.ValueAtFraction(.25));
        Assert.Equal(inverted ? 77.5 : 32.5, profile.RadiusAtFraction(.25), 10);
        Near(new Point(0, -116), profile.CategoryLabelPosition(0));
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Incompatible_mappings_use_explicit_physical_radius_reference_instead_of_raw_value_ticks(int kind)
    {
        (PolarSeries First, PolarSeries Second) pair = kind switch
        {
            0 => (new PolarAreaSeries(), new PolarLineSeries()),
            1 => (new PolarLineSeries { InnerRadius = .1 }, new PolarLineSeries { InnerRadius = .3 }),
            2 => (new RadarSeries { MaxValue = 100 }, new RadarSeries { MaxValue = 200 }),
            3 => (new RadialBarSeries(), new RadialLineSeries()),
            _ => (new NightingaleRoseSeries(), new RadialLineSeries()),
        };
        var profile = Profile(Scene(pair.First, pair.Second));
        Assert.Equal(PolarAxisPresentationKind.NormalizedReference, profile.Kind);
        Assert.Equal(25, profile.RadiusAtFraction(.25));
        var inverted = profile with { RadiusInversed = true };
        Assert.Equal(25, inverted.RadiusAtFraction(.25));
    }

    [AvaloniaTheory]
    [InlineData(false, false, 342)]
    [InlineData(true, false, 198)]
    [InlineData(false, true, 198)]
    [InlineData(true, true, 342)]
    public void Radial_bar_value_ticks_follow_angular_and_radial_inversion_and_categories_stay_on_lanes(
        bool angleInversed, bool radiusInversed, double expectedAngle)
    {
        var bar = new RadialBarSeries { InnerRadius = .2 };
        var scene = Scene(bar, 3);
        var profile = PolarAxisPresentation.Create(scene, -90, 360, true, angleInversed, radiusInversed);
        Assert.Equal(PolarAxisPresentationKind.RadialBar, profile.Kind);
        Assert.Equal(expectedAngle, PolarChartGeometry.NormalizeAngle(profile.ValueAngleAtFraction(.2)), 10);
        Assert.Equal(20, profile.LaneBoundaryRadius(0), 10); Assert.Equal(100, profile.LaneBoundaryRadius(3), 10);
        Near(new Point(-14, -100d / 3), profile.CategoryLabelPosition(0));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Native_recording_draws_correct_business_value_rings_and_never_the_old_linear_radius(bool area)
    {
        PolarSeries series = area ? new PolarAreaSeries() : new NightingaleRoseSeries();
        series.InnerRadius = .15;
        var chart = Chart(series); chart.RadialAxis.Interval = 25; chart.GridShape = PolarGridShape.Polygon; chart.StartAngle = -45;
        using var host = new Host(chart); var drawing = Record(chart);
        var rings = CenteredGridBounds(drawing, chart.PolarCenter).ToArray();
        Assert.NotEmpty(rings);
        var radius = chart.PolarRadius * (area ? Math.Sqrt(.266875) : .3625);
        Assert.Contains(rings, bounds => Math.Abs(bounds.Width / 2 - radius) < 1e-8 && Math.Abs(bounds.Height / 2 - radius) < 1e-8);
        Assert.DoesNotContain(rings, bounds => Math.Abs(bounds.Width / 2 - chart.PolarRadius * .25) < 1e-8);
        Assert.All(rings, bounds => Near(chart.PolarCenter, bounds.Center));
        var text = GlyphText(drawing);
        Assert.Contains("25", text); Assert.DoesNotContain("r/R=", text);
    }

    [AvaloniaFact]
    public void Radial_bar_recording_has_value_spokes_and_lane_labels_without_old_angular_category_spokes()
    {
        var series = new RadialBarSeries { InnerRadius = .2 };
        var chart = Chart(series, 3); chart.RadialAxis.Interval = 20; chart.GridShape = PolarGridShape.Polygon; chart.StartAngle = -45;
        using var host = new Host(chart); var drawing = Record(chart);
        var lines = GridDrawings(drawing).Select(g => g.Geometry!.Bounds).Where(bounds => Distance(bounds.Center, chart.PolarCenter) > 1e-8).ToArray();
        Assert.Equal(5, lines.Length);
        var rings = CenteredGridBounds(drawing, chart.PolarCenter).ToArray();
        Assert.Equal(4, rings.Length); Assert.Contains(rings, bounds => Math.Abs(bounds.Width - chart.PolarRadius * 2) < 1e-8 && Math.Abs(bounds.Height - chart.PolarRadius * 2) < 1e-8);
        var positive = RayBounds(chart.PolarCenter, chart.PolarRadius * .2, chart.PolarRadius, 27);
        Assert.Contains(lines, bounds => SameBounds(bounds, positive));
        var oldSpoke = RayBounds(chart.PolarCenter, chart.PolarRadius * .2, chart.PolarRadius, 75);
        Assert.DoesNotContain(lines, bounds => SameBounds(bounds, oldSpoke));
        var labels = Glyphs(drawing, Matrix.Identity).Where(g => g.Text == "LaneA").ToArray();
        Assert.NotEmpty(labels);
        var anchor = new Point(chart.PolarCenter.X + (chart.PolarRadius / 3 - 14) / Math.Sqrt(2), chart.PolarCenter.Y - (chart.PolarRadius / 3 + 14) / Math.Sqrt(2));
        Assert.All(labels, glyph => Assert.True(Distance(glyph.Bounds.Center, anchor) < 20));
        Assert.DoesNotContain(Leaves(drawing).OfType<GlyphRunDrawing>(), glyph => glyph.GlyphRun is { } run && new string(run.Characters.Span) == "100");
    }

    [AvaloniaFact]
    public void Mixed_axis_recording_labels_reference_radius_and_actual_display_angle_and_hides_caption_on_request()
    {
        var area = new PolarAreaSeries { InnerRadius = .15 }; var line = new PolarLineSeries();
        var chart = Chart(area); Configure(line, 4); chart.Series.Add(line); chart.GridLevels = 4; chart.GridShape = PolarGridShape.Polygon; chart.StartAngle = -22.5;
        using var host = new Host(chart); var drawing = Record(chart);
        var text = GlyphText(drawing);
        Assert.Contains("r/R=25%", text); Assert.Contains("θ=", text); Assert.Contains("归一半径参考", text);
        Assert.Contains(CenteredGridBounds(drawing, chart.PolarCenter), bounds => Math.Abs(bounds.Width / 2 - chart.PolarRadius * .25) < 1e-8 && Math.Abs(bounds.Height / 2 - chart.PolarRadius * .25) < 1e-8);
        chart.ShowAxisLabels = false; host.Drain();
        Assert.Empty(Leaves(Record(chart)).OfType<GlyphRunDrawing>());
    }

    [AvaloniaFact]
    public void Radar_polygon_grid_remains_a_vertex_reference_instead_of_becoming_a_circle()
    {
        var chart = Chart(new RadarSeries()); chart.GridShape = PolarGridShape.Polygon; chart.RadialAxis.Interval = 25; chart.StartAngle = -45;
        using var host = new Host(chart); var drawing = Record(chart);
        var polygons = CenteredGridBounds(drawing, chart.PolarCenter).ToArray();
        Assert.NotEmpty(polygons);
        Assert.Contains(polygons, bounds => Math.Abs(bounds.Width - Math.Sqrt(2) * chart.PolarRadius * .25) < 1e-8 &&
            Math.Abs(bounds.Height - Math.Sqrt(2) * chart.PolarRadius * .25) < 1e-8);
        Assert.DoesNotContain(polygons, bounds => Math.Abs(bounds.Width - chart.PolarRadius * 2) < 1e-8);
        Assert.Equal(PolarAxisPresentationKind.ValueRadius, chart.GetAxisPresentation().Kind);
        Assert.Contains("LaneA", GlyphText(drawing));
    }

    private static PolarScene Scene(PolarSeries series, int count = 4) => Scene([series], count);
    private static PolarScene Scene(params PolarSeries[] series) => Scene(series, 4);
    private static PolarScene Scene(PolarSeries[] series, int count)
    {
        foreach (var item in series) Configure(item, count);
        return PolarChartGeometry.Build(series.Select(s => (s, s.CreateSnapshot())).ToArray(), default, 100, radiusMaximum: 100, startAngle: -90);
    }
    private static PolarAxisPresentation Profile(PolarScene scene) => PolarAxisPresentation.Create(scene, -90, 360, true, false, false);
    private static PolarChart Chart(PolarSeries series, int count = 4)
    {
        Configure(series, count);
        var chart = new PolarChart
        {
            Width = 700, Height = 520, Padding = new(0), ShowLegend = false, ShowAxisLabels = true,
            ShowGridLines = true, GridLineBrush = Brushes.Magenta, GridLineStrokeThickness = 2, AxisBrush = null,
            GridShape = PolarGridShape.Circle, RadialAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
        };
        chart.Series.Add(series); return chart;
    }
    private static void Configure(PolarSeries series, int count)
    {
        var values = new[] { 100d, 25, 64, 0 };
        series.ItemsSource = Enumerable.Range(0, count).Select(i => new AxisRow($"Lane{(char)('A' + i)}", i * 90, values[i])).ToArray();
        series.CategoryPath = series.IsCategorical ? nameof(AxisRow.Category) : nameof(AxisRow.Angle);
        series.ValuePath = nameof(AxisRow.Value); series.StrokeThickness = 0; series.ShowMarkers = false; series.FillOpacity = 0;
    }
    private static DrawingGroup Record(PolarChart chart) { var drawing = new DrawingGroup(); using (var context = drawing.Open()) chart.Render(context); return drawing; }
    private static IEnumerable<GeometryDrawing> GridDrawings(Drawing drawing) => Leaves(drawing).OfType<GeometryDrawing>()
        .Where(g => g.Pen?.Brush is ISolidColorBrush brush && brush.Color == Colors.Magenta);
    // DrawingGroup wraps every input primitive in PlatformGeometry; its public Bounds survives that wrapper.
    // Rotated polygons have a strictly smaller envelope than an equal-radius circle in these fixtures.
    private static IEnumerable<Rect> CenteredGridBounds(Drawing drawing, Point center) => GridDrawings(drawing)
        .Select(g => g.Geometry!.Bounds).Where(bounds => Distance(bounds.Center, center) < 1e-8);
    private static Rect RayBounds(Point center, double inner, double outer, double degrees)
    {
        var unit = new Vector(Math.Cos(degrees * Math.PI / 180), Math.Sin(degrees * Math.PI / 180));
        var a = center + unit * inner; var b = center + unit * outer;
        return new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }
    private static bool SameBounds(Rect a, Rect b) => Math.Abs(a.X - b.X) < 1e-8 && Math.Abs(a.Y - b.Y) < 1e-8 && Math.Abs(a.Width - b.Width) < 1e-8 && Math.Abs(a.Height - b.Height) < 1e-8;
    private static string GlyphText(Drawing drawing) => string.Concat(Leaves(drawing).OfType<GlyphRunDrawing>()
        .Where(g => g.GlyphRun is not null).Select(g => new string(g.GlyphRun!.Characters.Span)));
    private static IEnumerable<Drawing> Leaves(Drawing drawing)
    {
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var leaf in Leaves(child)) yield return leaf;
        else yield return drawing;
    }
    private static IEnumerable<(string Text, Rect Bounds)> Glyphs(Drawing drawing, Matrix toRoot)
    {
        if (drawing is DrawingGroup group)
        {
            var current = (group.Transform?.Value ?? Matrix.Identity) * toRoot;
            foreach (var child in group.Children) foreach (var glyph in Glyphs(child, current)) yield return glyph;
        }
        else if (drawing is GlyphRunDrawing glyph && glyph.GlyphRun is { } run)
            yield return (new string(run.Characters.Span), glyph.GetBounds().TransformToAABB(toRoot));
    }
    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static void Near(Point expected, Point actual) { Assert.Equal(expected.X, actual.X, 8); Assert.Equal(expected.Y, actual.Y, 8); }
    private sealed record AxisRow(string Category, double Angle, double Value);
    private sealed class Host : IDisposable
    {
        private readonly Window _window;
        public Host(PolarChart chart) { _window = new Window { Width = 700, Height = 520, Content = chart }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
