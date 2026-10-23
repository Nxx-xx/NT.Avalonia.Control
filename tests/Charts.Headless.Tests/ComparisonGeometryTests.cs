using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ComparisonGeometryTests
{
    [AvaloniaTheory]
    [InlineData(false, 40d)]
    [InlineData(false, -40d)]
    [InlineData(true, 40d)]
    [InlineData(true, -40d)]
    public void Horizontal_auto_labels_follow_the_visible_bar_direction_even_on_an_inverse_value_axis(bool inverse, double number)
    {
        var series = new LollipopSeries
        {
            Orientation = Orientation.Horizontal, ItemsSource = new ChartPoint[] { new(5, number) },
            ShowDataLabels = true, DataLabelPosition = ChartDataLabelPosition.Auto,
            DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => new Border { Name = "AutoComparisonLabel", Width = 42, Height = 18 }),
        };
        var chart = new CartesianChart
        {
            ShowLegend = false, XAxis = new NumericAxis { Minimum = -100, Maximum = 100, IsInversed = inverse },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        };
        chart.Series.Add(series);
        using var host = new TemplateFeaturesHost(chart);
        var card = Assert.Single(chart.GetVisualDescendants().OfType<Border>(), child => child.Name == "AutoComparisonLabel");
        var context = Assert.IsType<ChartDataLabelContext>(card.DataContext);
        var bounds = TemplateFeaturesHost.BoundsInChart(Assert.IsType<Border>(card.GetVisualParent()), chart);
        var towardLeft = number < 0 ^ inverse;
        Assert.True(towardLeft ? bounds.Right < context.Position.X : bounds.Left > context.Position.X);
    }

    [AvaloniaTheory]
    [InlineData(false, 60d)]
    [InlineData(false, -60d)]
    [InlineData(true, 60d)]
    [InlineData(true, -60d)]
    public void Horizontal_inside_end_labels_use_the_value_axis_and_stay_between_baseline_and_endpoint(bool template, double number)
    {
        var series = new BarSeries
        {
            Orientation = Orientation.Horizontal, ItemsSource = new ChartPoint[] { new(5, number) }, ShowDataLabels = true,
            DataLabelFormat = "F0", DataLabelPosition = ChartDataLabelPosition.InsideEnd, DataLabelBackground = Brushes.Magenta,
        };
        if (template) series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) =>
            new Border { Name = "ComparisonLabel", Width = 42, Height = 18, Child = new TextBlock { Text = "value" } });
        var chart = new CartesianChart
        {
            ShowLegend = false, XAxis = new NumericAxis { Minimum = -100, Maximum = 100 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        };
        chart.Series.Add(series);
        using var host = new TemplateFeaturesHost(chart);
        var recording = TemplateFeaturesHost.Record(chart);
        Rect bounds;
        if (template)
        {
            var card = Assert.Single(chart.GetVisualDescendants().OfType<Border>(), child => child.Name == "ComparisonLabel");
            var context = Assert.IsType<ChartDataLabelContext>(card.DataContext);
            Assert.Equal(number.ToString("F0"), context.Text);
            Assert.Equal(number, context.Data.Point.X); Assert.Equal(5, context.Data.Point.Y);
            bounds = TemplateFeaturesHost.BoundsInChart(Assert.IsType<Border>(card.GetVisualParent()), chart);
        }
        else bounds = Assert.Single(TemplateFeaturesHost.Walk(recording).OfType<GeometryDrawing>(), d => IsColor(d.Brush, Colors.Magenta)).Geometry!.Bounds;
        var zero = chart.PlotArea.Center.X;
        var endpoint = chart.PlotArea.Left + (number + 100) / 200 * chart.PlotArea.Width;
        Assert.True(bounds.Left >= Math.Min(zero, endpoint) && bounds.Right <= Math.Max(zero, endpoint));
    }

    [AvaloniaFact]
    public void Comparison_options_validate_values_and_preserve_the_default_bar_fast_path()
    {
        var bar = new BarSeries();
        Assert.Same(CartesianSeriesRenderer.Bar, bar.Renderer);
        Assert.Equal(Orientation.Vertical, bar.Orientation); Assert.Equal(0, bar.Baseline);
        Assert.Null(bar.BarWidth); Assert.Equal(.86, bar.WidthRatio); Assert.Equal(ChartSeriesSortMode.Source, bar.SortMode);
        Assert.Equal(ChartStackMode.None, bar.StackMode); Assert.Null(bar.StackGroup);
        bar.Orientation = Orientation.Horizontal;
        Assert.Same(ChartComparisonSeriesRenderer.Bar, bar.Renderer);
        bar.Orientation = Orientation.Vertical; bar.StackMode = ChartStackMode.Stacked;
        Assert.Same(ChartComparisonSeriesRenderer.Bar, bar.Renderer);
        Assert.Equal(ChartSeriesSortMode.ValueDescending, new ParetoSeries().SortMode);
        Assert.True(new WaterfallSeries().ShowConnectors); Assert.False(new RangeBarSeries().ShowMarkers);
        Assert.True(new DotSeries().ShowMarkers); Assert.True(new LollipopSeries().ShowMarkers);
        var range = new RangeBarSeries();
        Assert.Throws<ArgumentException>(() => range.Orientation = (Orientation)999);
        Assert.Throws<ArgumentException>(() => range.SortMode = (ChartSeriesSortMode)999);
        Assert.Throws<ArgumentException>(() => range.Baseline = double.NaN);
        Assert.Throws<ArgumentException>(() => range.Baseline = double.PositiveInfinity);
        Assert.Throws<ArgumentException>(() => range.WidthRatio = 0);
        Assert.Throws<ArgumentException>(() => range.WidthRatio = 1.001);
        Assert.Throws<ArgumentException>(() => range.BarWidth = .49);
        Assert.Throws<ArgumentException>(() => range.BarWidth = double.PositiveInfinity);
        Assert.Throws<ArgumentException>(() => new WaterfallSeries().ConnectorThickness = -1);
        Assert.Throws<ArgumentException>(() => new ParetoSeries().CumulativeStrokeThickness = double.NaN);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Grouped_rectangle_uses_the_value_axis_baseline_and_shared_endpoint(bool horizontal)
    {
        var bar = new BarSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, Baseline = 30, WidthRatio = .5 };
        var context = Context(bar, [horizontal ? new(75, 40) : new(40, 75)], width: 40, index: 1, count: 2);
        Assert.True(ChartComparisonGeometry.TryCreate(context, 0, out var shape));
        Assert.Equal(horizontal ? new Rect(30, 45, 45, 10) : new Rect(45, 30, 10, 45), shape.Bounds);
        Assert.Equal(horizontal ? new Point(30, 50) : new Point(50, 30), shape.Start);
        Assert.Equal(horizontal ? new Point(75, 50) : new Point(50, 75), shape.End);
        var hit = Assert.IsType<ChartSeriesHit>(bar.Renderer.HitTest(context, shape.Bounds.Center, 0));
        Assert.Equal(shape.End, hit.Position); Assert.Equal(0, hit.DistanceSquared);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_pixel_width_can_exceed_a_slot_and_does_not_change_group_center(bool horizontal)
    {
        var bar = new BarSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, BarWidth = 50 };
        var context = Context(bar, [new(40, 40)], width: 20, index: 0, count: 2);
        Assert.True(ChartComparisonGeometry.TryCreate(context, 0, out var shape));
        Assert.Equal(50, horizontal ? shape.Bounds.Height : shape.Bounds.Width);
        Assert.Equal(35, horizontal ? shape.End.Y : shape.End.X);
    }

    [AvaloniaFact]
    public void Nondefault_renderer_retains_the_default_rectangles_arithmetic()
    {
        var bar = new BarSeries { SortMode = ChartSeriesSortMode.XAscending };
        var context = Context(bar, [new(48, 60)], width: 35, index: 2, count: 3);
        Assert.True(ChartComparisonGeometry.TryCreate(context, 0, out var shape));
        var expected = CartesianSeriesRenderer.BarRectangle(context, new(48, 60));
        Assert.Equal(expected.X, shape.Bounds.X, 12); Assert.Equal(expected.Width, shape.Bounds.Width, 12);
        Assert.Equal(expected.Y, shape.Bounds.Y); Assert.Equal(expected.Height, shape.Bounds.Height);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Range_geometry_uses_both_endpoints_and_not_the_shared_baseline(bool horizontal)
    {
        var range = new RangeBarSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, Baseline = 95 };
        var context = Context(range, [horizontal ? new(70, 40) : new(40, 70)], lower: [25], width: 10);
        Assert.True(ChartComparisonGeometry.TryCreate(context, 0, out var shape));
        Assert.Equal(horizontal ? new Point(25, 40) : new Point(40, 25), shape.Start);
        Assert.Equal(horizontal ? new Point(70, 40) : new Point(40, 70), shape.End);
        var bounds = range.Renderer.GetBounds(range, context.Data)!.Value;
        Assert.Equal(25, horizontal ? bounds.XMinimum : bounds.YMinimum);
        Assert.Equal(70, horizontal ? bounds.XMaximum : bounds.YMaximum);
        Assert.Null(range.Renderer.HitTest(context, horizontal ? new(15, 40) : new(40, 15), .1));
        Assert.IsType<ChartSeriesHit>(range.Renderer.HitTest(context, horizontal ? new(35, 40) : new(40, 35), 0));
    }

    [AvaloniaFact]
    public void Body_hit_is_clipped_and_returns_the_last_physical_source_identity_on_ties()
    {
        var bar = new BarSeries { Orientation = Orientation.Horizontal, BarWidth = 20 };
        var context = Context(bar, [new(140, 40), new(140, 40)], lower: [-30, -30], sources: [41, 7]);
        Assert.Null(bar.Renderer.HitTest(context, new(125, 40), 0));
        var hit = Assert.IsType<ChartSeriesHit>(bar.Renderer.HitTest(context, new(80, 40), 0));
        Assert.Equal(7, hit.SourceIndex); Assert.Equal(new Point(100, 40), hit.Position);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Lollipop_stem_is_hittable_while_dot_only_exposes_the_endpoint(bool horizontal)
    {
        var lollipop = new LollipopSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, MarkerSize = 8, Baseline = 20 };
        var dot = new DotSeries { Orientation = lollipop.Orientation, MarkerSize = 8, Baseline = 20 };
        var values = new[] { horizontal ? new ChartPoint(80, 40) : new ChartPoint(40, 80) };
        var middle = horizontal ? new Point(50, 40) : new Point(40, 50);
        Assert.IsType<ChartSeriesHit>(lollipop.Renderer.HitTest(Context(lollipop, values), middle, 0));
        Assert.Null(dot.Renderer.HitTest(Context(dot, values), middle, 0));
        Assert.IsType<ChartSeriesHit>(dot.Renderer.HitTest(Context(dot, values), horizontal ? new(80, 40) : new(40, 80), 0));
        lollipop.ShowMarkers = false;
        Assert.Single(Draw(Context(lollipop, values)));
        dot.ShowMarkers = false;
        Assert.Empty(Draw(Context(dot, values)));
    }

    [AvaloniaFact]
    public void Dot_geometry_ignores_an_inapplicable_baseline_outside_a_log_axis_domain()
    {
        var dot = new DotSeries { Baseline = -20 };
        var context = Context(dot, [new(40, 10)]) with { Projection = new TestProjection(logY: true) };
        Assert.True(ChartComparisonGeometry.TryCreate(context, 0, out var shape));
        Assert.Equal(new Point(40, 1), shape.End); Assert.Equal(shape.End, shape.Start);
    }

    [AvaloniaFact]
    public void Zero_baseline_uses_log_origin_but_an_invalid_range_endpoint_does_not()
    {
        var bar = new BarSeries { WidthRatio = .8 };
        var context = Context(bar, [new(40, 10)]) with { Projection = new TestProjection(logY: true) };
        Assert.True(ChartComparisonGeometry.TryCreate(context, 0, out var shape)); Assert.Equal(0, shape.Start.Y);
        var range = new RangeBarSeries();
        Assert.False(ChartComparisonGeometry.TryCreate(Context(range, [new(40, 10)], [0]) with { Projection = context.Projection }, 0, out _));
        bar.Baseline = -3;
        Assert.False(ChartComparisonGeometry.TryCreate(context, 0, out _));
    }

    [AvaloniaFact]
    public void Inverted_projection_and_zoom_apply_to_both_range_endpoints_and_pixel_width_stays_fixed()
    {
        var series = new RangeBarSeries { BarWidth = 12 };
        var projection = new TestProjection(scale: 2, inverse: true);
        var context = Context(series, [new(25, 40)], [10]) with { Projection = projection };
        Assert.True(ChartComparisonGeometry.TryCreate(context, 0, out var shape));
        Assert.Equal(new Point(50, 80), shape.Start); Assert.Equal(new Point(50, 20), shape.End);
        Assert.Equal(new Rect(44, 20, 12, 60), shape.Bounds);
    }

    [AvaloniaFact]
    public void Missing_points_and_nonfinite_geometry_have_no_render_or_hit_footprint()
    {
        var bar = new RangeBarSeries();
        var context = Context(bar, [new(40, double.NaN), new(double.NaN, 40)], [10, 10]);
        Assert.Empty(Draw(context)); Assert.Null(bar.Renderer.HitTest(context, new(40, 40), 100));
        Assert.False(ChartComparisonGeometry.TryCreate(context, -1, out _));
        Assert.False(ChartComparisonGeometry.TryCreate(context, 2, out _));
    }

    [AvaloniaFact]
    public void Variance_retains_directional_endpoints_positive_and_negative_styles_and_both_marker_hits()
    {
        var series = new VarianceSeries { PositiveFill = Brushes.Green, NegativeFill = Brushes.Red, MarkerStrokeThickness = 0, MarkerSize = 8 };
        var context = Context(series, [new(20, 80), new(60, 20)], [20, 80], high: [80, 20]);
        var draws = Draw(context);
        Assert.Equal(6, draws.Length); Assert.Equal(2, draws.Count(d => IsColor(d.Brush, Colors.Green)));
        Assert.Equal(2, draws.Count(d => IsColor(d.Brush, Colors.Red)));
        Assert.Equal(0, Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, new(20, 20), 0)).SourceIndex);
        Assert.Equal(1, Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, new(60, 80), 0)).SourceIndex);
    }

    [AvaloniaFact]
    public void Waterfall_uses_delta_direction_total_style_and_does_not_connect_across_a_gap()
    {
        var series = new WaterfallSeries { PositiveFill = Brushes.Green, NegativeFill = Brushes.Red, TotalFill = Brushes.Blue };
        var data = new ChartDataSnapshot(0, new ChartDataPoint[]
        {
            new(0, 0, null, 20, new(20, 50), false) { RawPoint = new(20, 50) },
            new(1, 1, null, 40, new(40, 30), false) { RawPoint = new(40, -20) },
            new(2, 2, null, 60, new(60, double.NaN), true),
            new(3, 3, null, 80, new(80, 30), false) { RawPoint = new(80, 30), IsTotal = true },
        }).WithBandValues([
            new(0, new(20, 50), false, false), new(1, new(40, 30), false, false), new(2, new(60, double.NaN), true, true), new(3, new(80, 30), false, false)
        ], [new(0, null, new(20, 50)), new(50, null, new(40, -20)), new(30, null, new(60, double.NaN)), new(0, null, new(80, 30))]);
        var context = new ChartSeriesRenderContext(series, data, new TestProjection(), Brushes.Gray, GroupWidth: 10);
        var drawings = Draw(context);
        Assert.Equal(4, drawings.Length);
        Assert.Single(drawings, d => IsColor(d.Brush, Colors.Green)); Assert.Single(drawings, d => IsColor(d.Brush, Colors.Red));
        Assert.Single(drawings, d => IsColor(d.Brush, Colors.Blue)); Assert.Single(drawings, d => d.Brush is null);
        series.ShowConnectors = false; Assert.Equal(3, Draw(context).Length);
    }

    [AvaloniaFact]
    public void Waterfall_connectors_follow_source_adjacency_for_nonmonotonic_category_coordinates()
    {
        var series = new WaterfallSeries();
        var context = Context(series, [new(70, 30), new(20, 50), new(45, 70)], [0, 30, 50], width: 8);
        var connectors = Draw(context).Where(drawing => drawing.Brush is null).ToArray();
        Assert.Equal(2, connectors.Length);
        Assert.Equal(30, connectors[0].Geometry!.Bounds.Y); Assert.Equal(50, connectors[1].Geometry!.Bounds.Y);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pareto_line_uses_secondary_projection_and_category_order_instead_of_source_order(bool horizontal)
    {
        var series = new ParetoSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, CumulativeStroke = Brushes.Red };
        var points = horizontal ? new ChartPoint[] { new(10, 70), new(50, 20), new(30, 45) } : [new(70, 10), new(20, 50), new(45, 30)];
        var secondary = horizontal ? new ChartPoint[] { new(100, 70), new(50, 20), new(90, 45) } : [new(70, 100), new(20, 50), new(45, 90)];
        var context = Context(series, points, width: 8, index: 1, count: 2) with
        {
            SecondaryProjection = new SecondaryTestProjection(horizontal), SecondaryData = Context(series, secondary).Data,
        };
        var line = Assert.Single(Draw(context), d => IsColor(d.Pen?.Brush, Colors.Red));
        Assert.Equal(horizontal ? new Rect(25, 22, 25, 50) : new Rect(22, 25, 50, 25), line.Geometry!.Bounds);
        var emptyPrimary = context.Data.WithValues(points.Select((point, i) => new ChartDataValue(i, point, true, false)).ToArray());
        var independent = Assert.Single(Draw(context with { Data = emptyPrimary }), d => IsColor(d.Pen?.Brush, Colors.Red));
        Assert.Equal(line.Geometry!.Bounds, independent.Geometry!.Bounds);
        series.ShowCumulativeLine = false; Assert.Equal(3, Draw(context).Length);
    }

    [AvaloniaFact]
    public void Pareto_bars_and_cumulative_line_are_recorded_under_their_own_independent_break_clips()
    {
        var series = new ParetoSeries { Fill = Brushes.Blue, CumulativeStroke = Brushes.Red };
        var primary = new RectangleGeometry(new Rect(0, 0, 100, 20));
        var secondary = new RectangleGeometry(new Rect(0, 70, 100, 20));
        var context = Context(series, [new(20, 60), new(60, 30)], width: 8) with
        {
            SecondaryProjection = new TestProjection(), SecondaryData = Context(series, [new(20, 60), new(60, 100)]).Data,
            PrimaryBreakClip = primary, SecondaryBreakClip = secondary,
        };
        var recording = new DrawingGroup(); using (var drawing = recording.Open()) series.Renderer.Render(drawing, context);
        var groups = TemplateFeaturesHost.Walk(recording).OfType<DrawingGroup>().ToArray();
        var primaryGroup = Assert.Single(groups, group => ReferenceEquals(group.ClipGeometry, primary));
        var secondaryGroup = Assert.Single(groups, group => ReferenceEquals(group.ClipGeometry, secondary));
        Assert.Equal(2, Flatten(primaryGroup).OfType<GeometryDrawing>().Count(drawing => IsColor(drawing.Brush, Colors.Blue)));
        Assert.DoesNotContain(Flatten(primaryGroup).OfType<GeometryDrawing>(), drawing => IsColor(drawing.Pen?.Brush, Colors.Red));
        Assert.Single(Flatten(secondaryGroup).OfType<GeometryDrawing>(), drawing => IsColor(drawing.Pen?.Brush, Colors.Red));
        Assert.DoesNotContain(Flatten(secondaryGroup).OfType<GeometryDrawing>(), drawing => IsColor(drawing.Brush, Colors.Blue));
    }

    private static ChartSeriesRenderContext Context(ChartSeries series, ChartPoint[] points, double[]? lower = null,
        double width = 0, int index = 0, int count = 1, int[]? sources = null, double[]? high = null)
    {
        var data = new ChartDataSnapshot(0, points.Select((p, i) => new ChartDataPoint(sources?[i] ?? i, i, p, p.X, p,
            !double.IsFinite(p.X) || !double.IsFinite(p.Y), high?[i], lower?[i])).ToArray());
        if (lower is not null) data = data.WithBandValues(points.Select((p, i) => new ChartDataValue(sources?[i] ?? i, p,
            !double.IsFinite(p.X) || !double.IsFinite(p.Y), false)).ToArray(),
            points.Select((p, i) => new ChartBandValue(lower[i], null, p)).ToArray());
        return new(series, data, new TestProjection(), Brushes.Gray, index, count, width);
    }
    private static bool IsColor(IBrush? brush, Color color) => brush is ISolidColorBrush solid && solid.Color == color;
    private static GeometryDrawing[] Draw(ChartSeriesRenderContext context)
    {
        var group = new DrawingGroup(); using (var drawing = group.Open()) context.Series.Renderer.Render(drawing, context);
        return Flatten(group).OfType<GeometryDrawing>().ToArray();
    }
    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children) foreach (var item in Flatten(child)) yield return item;
        else yield return drawing;
    }
    private sealed class TestProjection(double scale = 1, bool inverse = false, bool logY = false) : IChartProjection
    {
        public Rect PlotArea => new(0, 0, 100, 100);
        public double YOrigin => 0;
        public Point Project(ChartPoint point) => new(MapX(point.X), MapY(point.Y));
        public ChartPoint Unproject(Point point) => new(point.X / scale, logY ? Math.Pow(10, point.Y) : (inverse ? 100 - point.Y : point.Y) / scale);
        public double MapX(double value) => value * scale;
        public double MapY(double value) => logY ? value > 0 ? Math.Log10(value) : double.NaN : inverse ? 100 - value * scale : value * scale;
    }
    private sealed class SecondaryTestProjection(bool horizontal) : IChartProjection
    {
        public Rect PlotArea => new(0, 0, 100, 100);
        public double YOrigin => 0;
        public Point Project(ChartPoint point) => new(MapX(point.X), MapY(point.Y));
        public ChartPoint Unproject(Point point) => new(point.X * (horizontal ? 2 : 1), point.Y * (horizontal ? 1 : 2));
        public double MapX(double value) => value * (horizontal ? .5 : 1);
        public double MapY(double value) => value * (horizontal ? 1 : .5);
    }
}
