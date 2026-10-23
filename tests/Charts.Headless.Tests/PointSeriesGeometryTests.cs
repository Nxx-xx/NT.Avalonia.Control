using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class PointSeriesGeometryTests
{
    [AvaloniaFact]
    public void Point_options_preserve_the_ordinary_scatter_renderer_and_validate_individual_values()
    {
        var series = new ScatterSeries();
        Assert.Same(CartesianSeriesRenderer.Scatter, series.Renderer);
        Assert.Equal(4, series.MinimumSize); Assert.Equal(40, series.MaximumSize);
        Assert.Equal(ChartSizeScale.Area, series.SizeScale); Assert.Equal(ChartPointDrawOrder.Source, series.DrawOrder);
        series.SizePath = "Size"; Assert.Same(ChartPointSeriesRenderer.Instance, series.Renderer);
        series.SizePath = " "; series.ColorPath = "Color"; Assert.Same(ChartPointSeriesRenderer.Instance, series.Renderer);
        series.ColorPath = null; series.DrawOrder = ChartPointDrawOrder.SizeDescending;
        Assert.Same(ChartPointSeriesRenderer.Instance, series.Renderer);
        series.DrawOrder = ChartPointDrawOrder.Source; Assert.Same(CartesianSeriesRenderer.Scatter, series.Renderer);
        Assert.Equal(0, new BubbleSeries().MinimumSize); Assert.Equal(60, new BubbleSeries().MaximumSize);
        Assert.True(new ScatterLineSeries().ShowLine);
        Assert.Throws<ArgumentException>(() => series.MinimumSize = -1);
        Assert.Throws<ArgumentException>(() => series.MaximumSize = 1025);
        Assert.Throws<ArgumentException>(() => series.MaximumSize = double.NaN);
        Assert.Throws<ArgumentException>(() => series.SizeMinimum = -1);
        Assert.Throws<ArgumentException>(() => series.SizeMaximum = double.PositiveInfinity);
        Assert.Throws<ArgumentException>(() => series.ColorMinimum = double.NaN);
        Assert.Throws<ArgumentException>(() => series.ColorMaximum = double.NegativeInfinity);
        Assert.Throws<ArgumentException>(() => series.SizeScale = (ChartSizeScale)99);
        Assert.Throws<ArgumentException>(() => series.DrawOrder = (ChartPointDrawOrder)99);
    }

    [AvaloniaTheory]
    [InlineData(ChartSizeScale.Diameter, 13d)]
    [InlineData(ChartSizeScale.Area, 20.29778313018444)]
    public void Size_encoding_uses_explicit_diameter_or_area_and_clamps_finite_nonnegative_values(ChartSizeScale scale, double expected)
    {
        var series = new ScatterSeries { SizePath = "Size", MinimumSize = 4, MaximumSize = 40, SizeMinimum = 0, SizeMaximum = 100, SizeScale = scale };
        var data = Data([0, 25, 100, 250, -1, double.NaN, null]);
        var styles = ChartPointEncoding.CreateStyles(series, data, Brushes.Gray);
        Assert.Equal(4, styles[0].Diameter); Assert.Equal(expected, styles[1].Diameter, 12);
        Assert.Equal(40, styles[2].Diameter); Assert.Equal(40, styles[3].Diameter);
        Assert.All(styles.Skip(4), style => Assert.Equal(0, style.Diameter));
        Assert.All(data.Points, point => { Assert.False(point.IsEmpty); Assert.Null(point.MarkerDiameter); });
    }

    [AvaloniaFact]
    public void Auto_zero_domains_subnormal_sizes_and_extreme_sizes_are_finite()
    {
        var series = new BubbleSeries { SizePath = "Size" };
        var styles = ChartPointEncoding.CreateStyles(series, Data([0, 0, -1, null]), Brushes.Gray);
        Assert.All(styles, style => Assert.Equal(0, style.Diameter));
        styles = ChartPointEncoding.CreateStyles(series, Data([0, double.Epsilon]), Brushes.Gray);
        Assert.Equal(0, styles[0].Diameter); Assert.Equal(60, styles[1].Diameter);
        styles = ChartPointEncoding.CreateStyles(series, Data([0, double.MaxValue / 4, double.MaxValue]), Brushes.Gray);
        Assert.Equal(30, styles[1].Diameter); Assert.Equal(60, styles[2].Diameter);
    }

    [AvaloniaFact]
    public void Auto_domains_exclude_missing_xy_but_keep_valid_offscreen_samples()
    {
        var series = new BubbleSeries { SizePath = "Size", ColorPath = "Color", MinimumColor = Colors.Black, MaximumColor = Colors.White };
        var points = new ChartDataPoint[]
        {
            new(0, 0, null, 0, new(10, 10), false) { SizeValue = 25, ColorValue = 0d },
            new(1, 1, null, 1, new(5000, 5000), false) { SizeValue = 100, ColorValue = 100d },
            new(2, 2, null, 2, new(20, double.NaN), true) { SizeValue = 10000, ColorValue = 10000d },
        };
        var styles = ChartPointEncoding.CreateStyles(series, new(0, points), Brushes.Gray);
        Assert.Equal(30, styles[0].Diameter); Assert.Equal(60, styles[1].Diameter);
        Assert.Equal(Colors.White, ColorOf(styles[1].Fill));
    }

    [AvaloniaFact]
    public void Incompatible_resolved_ranges_fail_explicitly_and_equal_output_diameters_remain_valid()
    {
        var series = new BubbleSeries { SizePath = "Size", MinimumSize = 70, MaximumSize = 60 };
        Assert.Throws<InvalidOperationException>(() => ChartPointEncoding.CreateStyles(series, Data([1]), Brushes.Gray));
        series.MinimumSize = 60;
        Assert.Equal(60, Assert.Single(ChartPointEncoding.CreateStyles(series, Data([1]), Brushes.Gray)).Diameter);
        series.SizeMinimum = 2;
        Assert.Throws<InvalidOperationException>(() => ChartPointEncoding.CreateStyles(series, Data([1]), Brushes.Gray));
        series.SizeMinimum = null; series.SizeMaximum = 0;
        Assert.Throws<InvalidOperationException>(() => ChartPointEncoding.CreateStyles(series, Data([0]), Brushes.Gray));
        series.SizeMaximum = null; series.ColorMinimum = 2; series.ColorMaximum = 1;
        Assert.Throws<InvalidOperationException>(() => ChartPointEncoding.CreateStyles(series, Data([1]), Brushes.Gray));
    }

    [AvaloniaFact]
    public void Color_encoding_accepts_direct_colors_brushes_strings_and_finite_gradient_values()
    {
        var direct = new SolidColorBrush(Colors.Magenta);
        var series = new ScatterSeries
        {
            ColorPath = "Color", MarkerFill = Brushes.Lime, MinimumColor = Colors.Black, MaximumColor = Colors.White,
            ColorMinimum = -double.MaxValue, ColorMaximum = double.MaxValue,
        };
        var data = Data([1, 1, 1, 1, 1, 1, 1, 1], [-double.MaxValue, 0d, double.MaxValue, Colors.Red, direct, "#123456", "invalid", double.NaN]);
        var styles = ChartPointEncoding.CreateStyles(series, data, Brushes.Gray);
        Assert.Equal(Colors.Black, ColorOf(styles[0].Fill)); Assert.Equal(Color.FromRgb(128, 128, 128), ColorOf(styles[1].Fill));
        Assert.Equal(Colors.White, ColorOf(styles[2].Fill)); Assert.Equal(Colors.Red, ColorOf(styles[3].Fill));
        Assert.Same(direct, styles[4].Fill); Assert.Equal(Color.Parse("#123456"), ColorOf(styles[5].Fill));
        Assert.Same(Brushes.Lime, styles[6].Fill); Assert.Same(Brushes.Lime, styles[7].Fill);
    }

    [AvaloniaFact]
    public void Constant_color_domains_use_the_minimum_color_and_do_not_change_source_metadata()
    {
        var series = new ScatterSeries { ColorPath = "Color", MinimumColor = Colors.Aqua, MaximumColor = Colors.Red };
        var data = Data([1, 1], [3d, 3d]);
        var styles = ChartPointEncoding.CreateStyles(series, data, Brushes.Gray);
        Assert.All(styles, style => Assert.Equal(Colors.Aqua, ColorOf(style.Fill)));
        var overlay = data.WithPointStyles(styles);
        Assert.All(overlay.Points, point => { Assert.Equal(3d, point.ColorValue); Assert.Equal(series.MarkerSize, point.MarkerDiameter); });
        Assert.All(data.Points, point => { Assert.Null(point.PointFill); Assert.Null(point.MarkerDiameter); });
    }

    [AvaloniaFact]
    public void No_size_path_uses_constant_marker_size_and_only_bubbles_use_weight_fallback()
    {
        var bubble = new BubbleSeries { MarkerSize = 18, WeightPath = "Weight" };
        var styles = ChartPointEncoding.CreateStyles(bubble, Data([0, 4]), Brushes.Gray);
        Assert.Equal(0, styles[0].Diameter); Assert.Equal(60, styles[1].Diameter);
        bubble.WeightPath = null;
        Assert.All(ChartPointEncoding.CreateStyles(bubble, Data([0, 4]), Brushes.Gray), style => Assert.Equal(18, style.Diameter));
        var scatter = new ScatterSeries { MarkerSize = 18, ColorPath = "Color", WeightPath = "Weight" };
        Assert.All(ChartPointEncoding.CreateStyles(scatter, Data([0, 4]), Brushes.Gray), style => Assert.Equal(18, style.Diameter));
    }

    [AvaloniaTheory]
    [InlineData(ChartPointDrawOrder.Source, 41)]
    [InlineData(ChartPointDrawOrder.SizeDescending, 9)]
    [InlineData(ChartPointDrawOrder.SizeAscending, 7)]
    public void Overlap_hits_choose_the_last_painted_marker_while_source_indices_and_equal_size_order_are_preserved(ChartPointDrawOrder order, int expected)
    {
        var series = new BubbleSeries { DrawOrder = order, MarkerStrokeThickness = 0 };
        var data = new ChartDataSnapshot(0, new ChartDataPoint[]
        {
            new(7, "large", null, null, new(50, 50), false) { MarkerDiameter = 60, PointFill = Brushes.Red },
            new(19, "small-first", null, null, new(50, 50), false) { MarkerDiameter = 20, PointFill = Brushes.Blue },
            new(9, "small-last", null, null, new(50, 50), false) { MarkerDiameter = 20, PointFill = Brushes.Green },
            new(41, "medium", null, null, new(50, 50), false) { MarkerDiameter = 40, PointFill = Brushes.Yellow },
        });
        var context = Context(series, data);
        var hit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, new(50, 50), 0));
        Assert.Equal(expected, hit.SourceIndex); Assert.Equal(0, hit.DistanceSquared);
        Assert.Equal(new[] { 7, 19, 9, 41 }, data.Points.Select(point => point.SourceIndex));
        var drawings = Draw(context).Where(drawing => drawing.Brush is not null).ToArray();
        Assert.Equal(4, drawings.Length);
        var last = data.Points.Single(point => point.SourceIndex == expected);
        Assert.Equal(ColorOf(last.PointFill), ColorOf(drawings[^1].Brush));
    }

    [AvaloniaFact]
    public void Circle_hit_uses_the_outer_edge_and_clipped_footprint_instead_of_center_distance()
    {
        var series = new BubbleSeries { MarkerStrokeThickness = 4 };
        var context = Context(series, new(0, new ChartDataPoint[]
        { new(35, 35, null, null, new(50, 50), false) { MarkerDiameter = 40 } }));
        Assert.Equal(0, Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, new(71, 50), 0)).DistanceSquared);
        Assert.Null(series.Renderer.HitTest(context, new(74, 50), 1));
        Assert.Equal(4, Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, new(74, 50), 2)).DistanceSquared);

        var edge = Context(series, new(0, new ChartDataPoint[] { new(9, 9, null, null, new(-10, 50), false) { MarkerDiameter = 40 } }));
        var edgeHit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(edge, new(5, 50), 0));
        Assert.Equal(new Point(0, 50), edgeHit.Position); Assert.Equal(9, edgeHit.SourceIndex);
        Assert.Null(series.Renderer.HitTest(edge, new(-1, 50), 0));
        var corner = Context(series, new(0, new ChartDataPoint[] { new(8, 8, null, null, new(-10, -10), false) { MarkerDiameter = 20 } }));
        Assert.Null(series.Renderer.HitTest(corner, new(0, 0), 100));
    }

    [AvaloniaTheory]
    [InlineData(ChartMarkerShape.Square)]
    [InlineData(ChartMarkerShape.Cross)]
    [InlineData(ChartMarkerShape.HorizontalLine)]
    [InlineData(ChartMarkerShape.VerticalLine)]
    public void Round_stroke_corner_that_never_reaches_the_plot_cannot_be_hit_at_any_tolerance(ChartMarkerShape marker)
    {
        var series = new BubbleSeries { MarkerShape = marker, MarkerStrokeThickness = 12 };
        var context = Context(series, new(0, new ChartDataPoint[]
        { new(2, 2, null, null, new(-10, -10), false) { MarkerDiameter = 10 } }));
        Assert.True(ChartPointGeometry.TryCreate(context, 0, out var shape));
        Assert.True(double.IsPositiveInfinity(shape.DistanceSquared(new(0, 0), context.PlotArea)));
        Assert.Null(series.Renderer.HitTest(context, new(0, 0), 2));
        Assert.Null(series.Renderer.HitTest(context, new(0, 0), 1000));
    }

    [AvaloniaTheory]
    [InlineData(ChartMarkerShape.Square)]
    [InlineData(ChartMarkerShape.Cross)]
    public void Partially_clipped_round_stroke_uses_the_visible_corner_arc_not_an_inflated_clip_approximation(ChartMarkerShape marker)
    {
        var series = new BubbleSeries { MarkerShape = marker, MarkerStrokeThickness = 12 };
        var context = Context(series, new(0, new ChartDataPoint[]
        { new(2, 2, null, null, new(-10, 10), false) { MarkerDiameter = 10 } }));
        var query = new Point(2, 0);
        var expected = 4 + Math.Pow(5 - Math.Sqrt(11), 2);
        Assert.True(ChartPointGeometry.TryCreate(context, 0, out var shape));
        Assert.Equal(expected, shape.DistanceSquared(query, context.PlotArea), 10);
        Assert.Null(series.Renderer.HitTest(context, query, 2.61));
        Assert.NotNull(series.Renderer.HitTest(context, query, 2.62));
    }

    [AvaloniaTheory]
    [InlineData(ChartMarkerShape.Circle)]
    [InlineData(ChartMarkerShape.Square)]
    public void Scale_break_regions_exclude_hidden_glyph_edges_from_tolerance_distance(ChartMarkerShape marker)
    {
        var series = new BubbleSeries { MarkerShape = marker, MarkerStrokeThickness = 0 };
        var context = Context(series, new(0, new ChartDataPoint[]
        { new(2, 2, null, null, new(30, 50), false) { MarkerDiameter = 30 } })) with
        { PointHitRegions = [new Rect(0, 0, 40, 100), new Rect(60, 0, 40, 100)] };
        var query = new Point(61, 50);
        Assert.NotNull(series.Renderer.HitTest(context with { PointHitRegions = null }, query, 18));
        Assert.Null(series.Renderer.HitTest(context, query, 18));
        var hit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, query, 22));
        Assert.Equal(441, hit.DistanceSquared);
        Assert.Null(series.Renderer.HitTest(context, new(50, 50), 1000));
    }

    [AvaloniaFact]
    public void Scale_break_regions_also_clip_cross_capsules_before_testing_their_tolerance()
    {
        var series = new BubbleSeries { MarkerShape = ChartMarkerShape.Cross, MarkerStrokeThickness = 2 };
        var context = Context(series, new(0, new ChartDataPoint[]
        { new(2, 2, null, null, new(30, 50), false) { MarkerDiameter = 30 } })) with
        { PointHitRegions = [new Rect(0, 0, 40, 100), new Rect(60, 0, 40, 100)] };
        var query = new Point(61, 50);
        Assert.NotNull(series.Renderer.HitTest(context with { PointHitRegions = null }, query, 21.5));
        Assert.Null(series.Renderer.HitTest(context, query, 21.5));
        Assert.NotNull(series.Renderer.HitTest(context, query, 23));
    }

    [AvaloniaTheory]
    [InlineData(ChartMarkerShape.Circle)]
    [InlineData(ChartMarkerShape.Square)]
    [InlineData(ChartMarkerShape.HorizontalLine)]
    public void Query_inside_the_full_glyph_but_outside_a_visible_cell_is_measured_to_that_cell(ChartMarkerShape marker)
    {
        var series = new BubbleSeries { MarkerShape = marker, MarkerStrokeThickness = 6 };
        var context = Context(series, new(0, new ChartDataPoint[]
        { new(2, 2, null, null, new(30, 50), false) { MarkerDiameter = 70 } }));
        Assert.True(ChartPointGeometry.TryCreate(context, 0, out var shape));
        Assert.Equal(0, shape.DistanceSquared(new(50, 50), context.PlotArea));
        Assert.Equal(100, shape.DistanceSquared(new(50, 50), new Rect(0, 0, 40, 100)), 10);
    }

    [AvaloniaTheory]
    [InlineData(ChartMarkerShape.Square, 59d, 59d, true)]
    [InlineData(ChartMarkerShape.Diamond, 59d, 59d, false)]
    [InlineData(ChartMarkerShape.Triangle, 50d, 42d, true)]
    [InlineData(ChartMarkerShape.Triangle, 50d, 59d, false)]
    [InlineData(ChartMarkerShape.InvertedTriangle, 50d, 58d, true)]
    [InlineData(ChartMarkerShape.Rectangle, 50d, 57d, false)]
    [InlineData(ChartMarkerShape.Cross, 58d, 58d, true)]
    [InlineData(ChartMarkerShape.Cross, 58d, 50d, false)]
    [InlineData(ChartMarkerShape.HorizontalLine, 58d, 50d, true)]
    [InlineData(ChartMarkerShape.VerticalLine, 50d, 58d, true)]
    public void Encoded_marker_hits_follow_each_shape_instead_of_a_bounding_circle(ChartMarkerShape shape, double x, double y, bool expected)
    {
        var series = new BubbleSeries { MarkerShape = shape, MarkerStrokeThickness = 1 };
        var context = Context(series, new(0, new ChartDataPoint[] { new(0, 0, null, null, new(50, 50), false) { MarkerDiameter = 20 } }));
        Assert.Equal(expected, series.Renderer.HitTest(context, new(x, y), 0) is not null);
    }

    [AvaloniaFact]
    public void Hidden_and_zero_size_markers_are_not_hit_and_extents_include_current_marker_style()
    {
        var series = new BubbleSeries { MarkerShape = ChartMarkerShape.Square, MarkerStrokeThickness = 4 };
        var context = Context(series, new(0, new ChartDataPoint[] { new(0, 0, null, null, new(50, 50), false) { MarkerDiameter = 20 } }));
        Assert.True(ChartPointGeometry.TryCreate(context, 0, out var shape));
        Assert.Equal(10, shape.Radius); Assert.Equal(10 * Math.Sqrt(2) + 2, shape.Extent);
        Assert.Equal(new Rect(38, 38, 24, 24), shape.Bounds);
        series.ShowMarkers = false;
        Assert.False(ChartPointGeometry.TryCreate(context, 0, out _)); Assert.Null(series.Renderer.HitTest(context, new(50, 50), 12));
        series.ShowMarkers = true;
        context = context with { Data = context.Data.WithPointStyles([new(0, Brushes.Red)]) };
        Assert.False(ChartPointGeometry.TryCreate(context, 0, out _)); Assert.Empty(Draw(context));
    }

    [AvaloniaFact]
    public void Scatter_line_keeps_source_order_and_only_xy_gaps_break_its_path()
    {
        var series = new ScatterLineSeries { Stroke = Brushes.Red, StrokeThickness = 3, DrawOrder = ChartPointDrawOrder.SizeDescending };
        var points = new ChartDataPoint[]
        {
            new(12, "a", null, null, new(80, 20), false) { MarkerDiameter = 5 },
            new(4, "b", null, null, new(20, 80), false) { MarkerDiameter = 0 },
            new(3, "gap", null, null, new(30, 30), true) { MarkerDiameter = 25 },
            new(8, "c", null, null, new(40, 10), false) { MarkerDiameter = 10 },
            new(7, "d", null, null, new(70, 40), false) { MarkerDiameter = 5 },
        };
        var projection = new IdentityProjection();
        var context = new ChartSeriesRenderContext(series, new(0, points), projection, Brushes.Gray);
        var drawings = Draw(context);
        var line = Assert.Single(drawings, drawing => drawing.Brush is null);
        Assert.Equal(3, line.Pen!.Thickness); Assert.Equal(new Rect(20, 10, 60, 70), line.Geometry!.Bounds);
        Assert.Equal(points.Where(point => !point.IsEmpty).Select(point => point.Point), projection.Projected.Take(4));
        Assert.Equal(3, drawings.Count(drawing => drawing.Brush is not null));
        series.ShowMarkers = false; Assert.Single(Draw(context));
        series.ShowLine = false; Assert.Empty(Draw(context));
    }

    private static ChartDataSnapshot Data(double?[] sizes, object?[]? colors = null) => new(0, sizes.Select((size, i) =>
        new ChartDataPoint(i, i, null, i, new(i * 10 + 10, 50), false) { SizeValue = size, ColorValue = colors?[i] }).ToArray());
    private static ChartSeriesRenderContext Context(ChartSeries series, ChartDataSnapshot data) => new(series, data, new IdentityProjection(), Brushes.Gray);
    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
    private static GeometryDrawing[] Draw(ChartSeriesRenderContext context)
    {
        var group = new DrawingGroup(); using (var drawing = group.Open()) context.Series.Renderer.Render(drawing, context);
        return Flatten(group).OfType<GeometryDrawing>().ToArray();
    }
    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children) foreach (var value in Flatten(child)) yield return value;
        else yield return drawing;
    }
    private sealed class IdentityProjection : IChartProjection
    {
        public List<ChartPoint> Projected { get; } = [];
        public Rect PlotArea => new(0, 0, 100, 100);
        public double YOrigin => 0;
        public Point Project(ChartPoint point) { Projected.Add(point); return new(point.X, point.Y); }
        public ChartPoint Unproject(Point point) => new(point.X, point.Y);
        public double MapX(double value) => value;
        public double MapY(double value) => value;
    }
}
