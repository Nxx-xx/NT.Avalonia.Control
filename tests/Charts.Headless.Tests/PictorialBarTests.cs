using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class PictorialBarTests
{
    [AvaloniaFact]
    public void Defaults_and_validation_keep_comparison_contracts()
    {
        var series = new PictorialBarSeries();
        Assert.Equal(PictorialBarRenderMode.Stretch, series.RenderMode);
        Assert.Equal(20, series.SymbolSize); Assert.Equal(4, series.SymbolSpacing);
        Assert.Null(series.Geometry); Assert.False(series.ShowMarkers);
        Assert.Equal(Orientation.Vertical, series.Orientation); Assert.Equal(0, series.Baseline);
        Assert.True(series.Renderer.UsesCategorySlots); Assert.False(series.Renderer.IncludesZero);
        Assert.Equal(ChartLegendSymbol.Rectangle, series.Renderer.LegendSymbol);
        Assert.Throws<ArgumentException>(() => series.RenderMode = (PictorialBarRenderMode)99);
        Assert.Throws<ArgumentException>(() => series.SymbolSize = .99);
        Assert.Throws<ArgumentException>(() => series.SymbolSize = double.NaN);
        Assert.Throws<ArgumentException>(() => series.SymbolSize = 4097);
        Assert.Throws<ArgumentException>(() => series.SymbolSpacing = -.01);
        Assert.Throws<ArgumentException>(() => series.SymbolSpacing = double.PositiveInfinity);
    }

    [AvaloniaTheory]
    [InlineData(false, 20d, 75d)]
    [InlineData(false, 75d, 20d)]
    [InlineData(true, 20d, 75d)]
    [InlineData(true, 75d, 20d)]
    public void Stretch_geometry_is_the_full_signed_bar_in_either_orientation(bool horizontal, double baseline, double value)
    {
        var series = new PictorialBarSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, Baseline = baseline, BarWidth = 12 };
        var context = Context(series, horizontal ? new(value, 40) : new(40, value));
        var symbol = Assert.Single(PictorialBarRenderer.Symbols(context, 0));
        var expected = horizontal ? new Rect(Math.Min(baseline, value), 34, Math.Abs(value - baseline), 12)
            : new Rect(34, Math.Min(baseline, value), 12, Math.Abs(value - baseline));
        Assert.Equal(expected, symbol.Target); Assert.Equal(expected, symbol.Clip);
        Assert.Equal(expected, symbol.Geometry.Bounds);
        var hit = Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, expected.Center, 0));
        Assert.Equal(31, hit.SourceIndex); Assert.Equal(0, hit.DistanceSquared);
        Assert.Null(series.Renderer.HitTest(context, new Point(5, 5), 100));
    }

    [AvaloniaTheory]
    [InlineData(false, 1d)]
    [InlineData(false, -1d)]
    [InlineData(true, 1d)]
    [InlineData(true, -1d)]
    public void Repeat_keeps_pixel_size_spacing_and_last_partial_for_both_signed_directions(bool horizontal, double sign)
    {
        var series = new PictorialBarSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
            Baseline = 50, RenderMode = PictorialBarRenderMode.Repeat, SymbolSize = 10, SymbolSpacing = 4, BarWidth = 12 };
        var value = 50 + sign * 32;
        var context = Context(series, horizontal ? new(value, 40) : new(40, value));
        var symbols = PictorialBarRenderer.Symbols(context, 0).ToArray();
        Assert.Equal(3, symbols.Length);
        Assert.All(symbols, symbol => Assert.Equal(10, horizontal ? symbol.Target.Width : symbol.Target.Height));
        Assert.Equal(4, horizontal ? symbols[2].Clip.Width : symbols[2].Clip.Height);
        Point At(double distance) => horizontal ? new(50 + sign * distance, 40) : new(40, 50 + sign * distance);
        Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, At(5), 0));
        Assert.Null(series.Renderer.HitTest(context, At(12), 200));
        Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, At(30), 0));
        Assert.Null(series.Renderer.HitTest(context, At(34), 200));
    }

    [AvaloniaTheory]
    [InlineData(false, 8d)]
    [InlineData(false, 60d)]
    [InlineData(true, 8d)]
    [InlineData(true, 60d)]
    public void Clip_uses_one_fixed_pixel_symbol_and_clips_only_at_numeric_endpoint(bool horizontal, double value)
    {
        var series = new PictorialBarSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
            RenderMode = PictorialBarRenderMode.Clip, SymbolSize = 20, BarWidth = 10 };
        var context = Context(series, horizontal ? new(value, 40) : new(40, value));
        var symbol = Assert.Single(PictorialBarRenderer.Symbols(context, 0));
        Assert.Equal(20, horizontal ? symbol.Target.Width : symbol.Target.Height);
        Assert.Equal(Math.Min(value, 20), horizontal ? symbol.Clip.Width : symbol.Clip.Height);
        Assert.Null(series.Renderer.HitTest(context, horizontal ? new(22, 40) : new(40, 22), 100));
    }

    [AvaloniaFact]
    public void Repetition_is_anchored_at_baseline_after_viewport_clipping()
    {
        var series = new PictorialBarSeries { Baseline = -1000, BarWidth = 20, RenderMode = PictorialBarRenderMode.Repeat,
            SymbolSize = 10, SymbolSpacing = 4 };
        var context = Context(series, new(40, 1000));
        var symbols = PictorialBarRenderer.Symbols(context, 0).ToArray();
        Assert.Equal(8, symbols.Length);
        Assert.Equal(-6, symbols[0].Target.Top); Assert.Equal(0, symbols[0].Clip.Top);
        Assert.Equal(4, symbols[0].Clip.Height); Assert.Equal(92, symbols[^1].Target.Top);
        Assert.Equal(8, symbols[^1].Clip.Height);
        Assert.Null(series.Renderer.HitTest(context, new(40, 6), 100));
        Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, new(40, 10), 0));
    }

    [AvaloniaFact]
    public void Custom_geometry_is_normalized_without_mutating_its_transform_or_bounds()
    {
        var transform = new MatrixTransform(new Matrix(2, 0, 0, 3, 11, -7));
        var geometry = new RectangleGeometry(new Rect(10, 20, 5, 10)) { Transform = transform };
        var oldBounds = geometry.Bounds; var oldTransform = transform.Value;
        var series = new PictorialBarSeries { Geometry = geometry, Baseline = 10, BarWidth = 20 };
        var context = Context(series, new(40, 70));
        var first = Assert.Single(PictorialBarRenderer.Symbols(context, 0));
        var second = Assert.Single(PictorialBarRenderer.Symbols(context, 0));
        Assert.Equal(new Rect(30, 10, 20, 60), first.Target);
        Assert.Equal(first.Target, first.Geometry.Bounds); Assert.Equal(first.Target, second.Geometry.Bounds);
        Assert.NotSame(geometry, first.Geometry); Assert.NotSame(first.Geometry, second.Geometry);
        Assert.Same(transform, geometry.Transform); Assert.Equal(oldTransform, transform.Value); Assert.Equal(oldBounds, geometry.Bounds);
        Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, first.Target.Center, 0));
    }

    [AvaloniaTheory]
    [InlineData(PictorialBarRenderMode.Stretch)]
    [InlineData(PictorialBarRenderMode.Clip)]
    [InlineData(PictorialBarRenderMode.Repeat)]
    public void Holes_and_unpainted_corners_are_never_hit_even_with_large_tolerance(PictorialBarRenderMode mode)
    {
        var donut = new CombinedGeometry(GeometryCombineMode.Exclude,
            new EllipseGeometry(new Rect(0, 0, 10, 10)), new EllipseGeometry(new Rect(3, 3, 4, 4)));
        var series = new PictorialBarSeries { Geometry = donut, RenderMode = mode, SymbolSize = 20, SymbolSpacing = 5, BarWidth = 20 };
        var context = Context(series, new(40, 80));
        var symbol = PictorialBarRenderer.Symbols(context, 0).First();
        Assert.Null(series.Renderer.HitTest(context, symbol.Target.Center, 500));
        Assert.Null(series.Renderer.HitTest(context, symbol.Target.TopLeft + new Vector(.1, .1), 500));
        var painted = new Point(symbol.Target.Center.X - symbol.Target.Width * .35, symbol.Target.Center.Y);
        Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, painted, 0));
    }

    [AvaloniaFact]
    public void Polygon_fill_rules_preserve_custom_path_holes_and_concavity_on_headless()
    {
        var geometry = PathGeometry.Parse("M 0,0 L 10,0 10,10 0,10 Z M 3,3 L 7,3 7,7 3,7 Z");
        geometry.FillRule = FillRule.EvenOdd;
        var series = new PictorialBarSeries { Geometry = geometry, BarWidth = 20 };
        var context = Context(series, new(40, 80));
        Assert.Null(series.Renderer.HitTest(context, new Point(40, 40), 500));
        Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, new Point(32, 40), 0));
        geometry.FillRule = FillRule.NonZero;
        Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, new Point(40, 40), 0));
        series.Geometry = PathGeometry.Parse("M 0,0 L 10,0 10,4 4,4 4,10 0,10 Z");
        Assert.Null(series.Renderer.HitTest(context, new Point(46, 64), 500));
        Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, new Point(34, 64), 0));
    }

    [AvaloniaFact]
    public void Zero_missing_or_empty_geometry_has_no_body_or_hit()
    {
        var series = new PictorialBarSeries { Baseline = 20, BarWidth = 20 };
        foreach (var point in new ChartPoint[] { new(40, 20), new(40, double.NaN), new(double.PositiveInfinity, 50) })
        {
            var context = Context(series, point);
            Assert.Empty(PictorialBarRenderer.Symbols(context, 0)); Assert.Empty(Draw(context));
            Assert.Null(series.Renderer.HitTest(context, new Point(40, 20), 1000));
        }
        series.Geometry = new RectangleGeometry(new Rect(0, 0, 0, 10));
        Assert.Empty(PictorialBarRenderer.Symbols(Context(series, new(40, 70)), 0));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Filled_polyline_custom_symbols_preserve_affine_mirror_and_exact_unpainted_triangle(bool mirror)
    {
        var transform = new MatrixTransform(new Matrix(mirror ? -2 : 2, 0, 0, 3, 30, -7));
        var geometry = new PolylineGeometry([new(0, 0), new(10, 0), new(0, 10)], true) { Transform = transform };
        var original = geometry.Bounds;
        var series = new PictorialBarSeries { Geometry = geometry, BarWidth = 20 };
        var context = Context(series, new(40, 80));
        var painted = mirror ? new Point(46, 16) : new Point(34, 16);
        var empty = mirror ? new Point(34, 64) : new Point(46, 64);
        Assert.IsType<ChartSeriesHit>(series.Renderer.HitTest(context, painted, 0));
        Assert.Null(series.Renderer.HitTest(context, empty, 1000));
        Assert.Same(transform, geometry.Transform); Assert.Equal(original, geometry.Bounds);
        geometry.IsFilled = false;
        Assert.Null(series.Renderer.HitTest(context, painted, 1000));
    }

    [AvaloniaFact]
    public void Numeric_bounds_include_finite_baseline_and_extreme_values_without_subtracting_domain()
    {
        var series = new PictorialBarSeries { Baseline = -double.MaxValue };
        var context = Context(series, new(40, double.MaxValue));
        var bounds = series.Renderer.GetBounds(series, context.Data)!.Value;
        Assert.True(bounds.IsValid); Assert.Equal(-double.MaxValue, bounds.YMinimum); Assert.Equal(double.MaxValue, bounds.YMaximum);
        series.Orientation = Orientation.Horizontal;
        context = Context(series, new(double.MaxValue, 40));
        bounds = series.Renderer.GetBounds(series, context.Data)!.Value;
        Assert.Equal(-double.MaxValue, bounds.XMinimum); Assert.Equal(double.MaxValue, bounds.XMaximum);
    }

    [AvaloniaFact]
    public void Rendering_records_real_geometry_under_numeric_and_plot_clips()
    {
        var series = new PictorialBarSeries { Baseline = -10, BarWidth = 20, Fill = Brushes.Magenta,
            RenderMode = PictorialBarRenderMode.Repeat, SymbolSize = 20, SymbolSpacing = 5 };
        var context = Context(series, new(40, 120));
        var drawing = new DrawingGroup(); using (var recorder = drawing.Open()) series.Renderer.Render(recorder, context);
        Assert.Equal(5, Draw(context).Length);
        var clips = TemplateFeaturesHost.Walk(drawing).OfType<DrawingGroup>().Where(group => group.ClipGeometry is not null).ToArray();
        Assert.Equal(5, clips.Length);
        Assert.All(clips, clip => Assert.True(context.PlotArea.Contains(clip.ClipGeometry!.Bounds)));
    }

    private sealed record Row(string Id, double Category, double Amount);

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cartesian_pipeline_retains_original_item_id_raw_point_baseline_hover_and_selection(bool horizontal)
    {
        var rows = new ObservableCollection<Row> { new("business", 1, 60) };
        var series = new PictorialBarSeries { ItemsSource = rows, CategoryPath = nameof(Row.Category), ValuePath = nameof(Row.Amount),
            IdPath = nameof(Row.Id), Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, Baseline = 10, BarWidth = 24 };
        using var host = horizontal ? new SegmentTestHost(new(0, 100), new(0, 2), series) : new SegmentTestHost(new(0, 2), new(0, 100), series);
        var chart = host.Chart;
        var inside = host.Project(horizontal ? new ChartPoint(35, 1) : new ChartPoint(1, 35));
        var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(inside, 0));
        Assert.Same(series, hit.Series); Assert.Same(rows[0], hit.Item); Assert.Equal("business", hit.StableId);
        Assert.Equal(new ChartPoint(1, 60), hit.RawPoint); Assert.Equal(10, hit.BaseValue);
        Assert.Equal(horizontal ? new ChartPoint(60, 1) : new ChartPoint(1, 60), hit.Point);
        Assert.Null(series.CreateSnapshot().Points[0].RawPoint);
        host.Move(inside); Assert.Equal("business", Assert.Single(chart.HoveredPoints).StableId);
        chart.SelectionMode = ChartSelectionMode.Single; chart.SelectedPoint = hit;
        Assert.Equal("business", chart.SelectedPoint!.StableId);
        var replacement = new Row("business", 1, 80); rows[0] = replacement; host.Drain();
        // Source notifications first reconcile against the canonical snapshot. A real
        // paint rebuilds the shared Cartesian scene and its oriented comparison bands.
        _ = TemplateFeaturesHost.Record(chart);
        // Publication is posted while Render is active, so run its finite UI queue
        // before inspecting metadata projected from the frame just recorded.
        host.Drain();
        Assert.Same(replacement, chart.SelectedPoint!.Item); Assert.Equal(new ChartPoint(1, 80), chart.SelectedPoint.RawPoint);
    }

    [AvaloniaFact]
    public void Pictorial_and_ordinary_bars_share_category_groups_without_overlapping_or_mutating_old_bar_renderer()
    {
        var pictorial = new PictorialBarSeries { ItemsSource = new ChartPoint[] { new(1, 60) } };
        var bar = new BarSeries { ItemsSource = new ChartPoint[] { new(1, 60) } };
        using var host = new SegmentTestHost(new(0, 2), new(0, 100), pictorial, bar);
        var center = host.Project(new(1, 30));
        var left = Assert.IsType<ChartPointHit>(host.Chart.HitTestPoint(center - new Vector(30, 0), 0));
        var right = Assert.IsType<ChartPointHit>(host.Chart.HitTestPoint(center + new Vector(30, 0), 0));
        Assert.Same(pictorial, left.Series); Assert.Same(bar, right.Series);
        Assert.Same(CartesianSeriesRenderer.Bar, bar.Renderer);
    }

    private static ChartSeriesRenderContext Context(PictorialBarSeries series, ChartPoint point)
    {
        var data = new ChartDataSnapshot(0, [new ChartDataPoint(31, "id", point, point.X, point,
            !double.IsFinite(point.X) || !double.IsFinite(point.Y))]);
        return new(series, data, new IdentityProjection(), Brushes.Blue, GroupWidth: 40);
    }
    private static GeometryDrawing[] Draw(ChartSeriesRenderContext context)
    {
        var group = new DrawingGroup(); using (var drawing = group.Open()) context.Series.Renderer.Render(drawing, context);
        return TemplateFeaturesHost.Walk(group).OfType<GeometryDrawing>().ToArray();
    }
    private sealed class IdentityProjection : IChartProjection
    {
        public Rect PlotArea => new(0, 0, 100, 100);
        public double YOrigin => 0;
        public Point Project(ChartPoint point) => new(point.X, point.Y);
        public ChartPoint Unproject(Point point) => new(point.X, point.Y);
        public double MapX(double value) => value;
        public double MapY(double value) => value;
    }
}
