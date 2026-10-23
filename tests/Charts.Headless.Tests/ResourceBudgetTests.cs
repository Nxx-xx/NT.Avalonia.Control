using System.Collections;
using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ResourceBudgetTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    [AvaloniaFact]
    public void Default_budget_and_accounting_scope_are_explicit_and_negative_budgets_are_invalid()
    {
        var chart = MakeChart(); var info = chart.GetResourceCacheInfo();
        Assert.Equal(128L * 1024 * 1024, info.BudgetBytes);
        Assert.Equal(0, info.EstimatedBytes); Assert.Equal(1, info.ModelVersion);
        Assert.False(info.NativeMemoryIncluded); Assert.False(info.SharedIndexCachesIncluded);
        Assert.ThrowsAny<ArgumentException>(() => chart.ResourceCacheBudgetBytes = -1);
    }

    [AvaloniaFact]
    public void Warm_drawing_reuses_geometry_axis_text_and_pens_and_reports_consistent_local_estimates()
    {
        var chart = MakeChart(20_000); using var host = new TemplateFeaturesHost(chart);
        Draw(chart); var before = chart.GetResourceCacheInfo();
        var builds = Counter(chart, "_seriesDrawingBuildCount"); var textBuilds = Counter(chart, "_axisTextBuildCount");
        var penBuilds = Counter(chart, "_axisPenBuildCount");
        for (var index = 0; index < 8; index++) Draw(chart);
        var after = chart.GetResourceCacheInfo();
        Assert.Equal(builds, Counter(chart, "_seriesDrawingBuildCount"));
        Assert.Equal(textBuilds, Counter(chart, "_axisTextBuildCount"));
        Assert.Equal(penBuilds, Counter(chart, "_axisPenBuildCount"));
        Assert.True(after.CacheHits > before.CacheHits);
        Assert.Equal(1, after.SeriesDrawingCount); Assert.True(after.AxisTextCount > 0); Assert.True(after.AxisPenCount > 0);
        Consistent(chart);
    }

    [AvaloniaFact]
    public void Trim_releases_resources_without_replacing_source_snapshot_scene_or_selection()
    {
        var chart = MakeChart(); using var host = new TemplateFeaturesHost(chart); Draw(chart);
        var snapshot = chart.Series[0].CreateSnapshot(); var scene = Field(chart, "_scene");
        var position = Project(chart, new(50, 50));
        var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(position, .001)); chart.SelectedPoint = hit;
        var selected = chart.SelectedPoint;
        var builds = Counter(chart, "_seriesDrawingBuildCount"); var prepared = Counter(chart, "_preparedDataBuildCount");
        chart.TrimResourceCaches();
        Assert.Equal(0, chart.GetResourceCacheInfo().EstimatedBytes);
        Assert.Equal(1, chart.GetResourceCacheInfo().TrimCount);
        Assert.Same(scene, Field(chart, "_scene")); Assert.Same(snapshot, chart.Series[0].CreateSnapshot()); Assert.Equal(selected, chart.SelectedPoint);
        Draw(chart);
        Assert.True(Counter(chart, "_seriesDrawingBuildCount") > builds);
        Assert.Equal(prepared, Counter(chart, "_preparedDataBuildCount"));
        Assert.Equal(selected?.StableId, chart.SelectedPoint?.StableId); Consistent(chart);
    }

    [AvaloniaFact]
    public void Zero_budget_disables_all_five_local_layers_while_original_hits_still_work()
    {
        var chart = MakeChart(); using var host = new TemplateFeaturesHost(chart); Draw(chart);
        var position = Project(chart, new(50, 50)); var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(position, .001));
        chart.ResourceCacheBudgetBytes = 0;
        for (var index = 0; index < 3; index++)
        {
            Draw(chart); _ = InteractionText(chart, "Current overlay " + index);
            Assert.NotNull(chart.HitTestSegment(position, 1));
            Assert.Equal(hit.Index, chart.HitTestPoint(position, .001)?.Index);
            var info = chart.GetResourceCacheInfo();
            Assert.Equal(0, info.EstimatedBytes); Assert.Equal(0, info.SeriesDrawingCount + info.AxisTextCount + info.InteractionTextCount + info.AxisPenCount + info.SegmentGeometryCount);
            Assert.Equal(0, info.SegmentProjectionCount); Consistent(chart);
        }
        Assert.True(chart.GetResourceCacheInfo().BypassCount > 0);
    }

    [AvaloniaFact]
    public void Lowering_the_budget_trims_immediately_and_oversized_drawings_are_not_retained()
    {
        var chart = MakeChart(20_000); using var host = new TemplateFeaturesHost(chart); Draw(chart);
        Assert.True(chart.GetResourceCacheInfo().SeriesDrawingBytes > 32_768);
        var evictions = chart.GetResourceCacheInfo().EvictionCount;
        chart.ResourceCacheBudgetBytes = 32_768;
        Assert.InRange(chart.GetResourceCacheInfo().EstimatedBytes, 0, 32_768);
        Assert.True(chart.GetResourceCacheInfo().EvictionCount > evictions);
        Draw(chart);
        Assert.Equal(0, chart.GetResourceCacheInfo().SeriesDrawingCount);
        Assert.True(chart.GetResourceCacheInfo().BypassCount > 0); Consistent(chart);
        chart.ResourceCacheBudgetBytes = 128L * 1024 * 1024; Draw(chart);
        Assert.Equal(1, chart.GetResourceCacheInfo().SeriesDrawingCount); Consistent(chart);
    }

    [AvaloniaFact]
    public void Large_text_is_returned_for_the_current_draw_but_cannot_escape_its_budget()
    {
        var chart = MakeChart(); using var host = new TemplateFeaturesHost(chart);
        chart.ResourceCacheBudgetBytes = 8192;
        var text = new string('x', 10_000); var before = chart.GetResourceCacheInfo().BypassCount;
        var formatted = InteractionText(chart, text);
        Assert.NotNull(formatted); Assert.True(chart.GetResourceCacheInfo().BypassCount > before);
        Assert.DoesNotContain(Cache(chart, "_interactionTexts").Keys.Cast<object>(), key => Equals(key.GetType().GetProperty("Text")!.GetValue(key), text));
        Consistent(chart);
    }

    [AvaloniaFact]
    public void Global_lru_keeps_recently_used_text_and_retires_the_oldest_estimate()
    {
        var chart = MakeChart(); using var host = new TemplateFeaturesHost(chart);
        chart.TrimResourceCaches(); chart.ResourceCacheBudgetBytes = 4096;
        var first = InteractionText(chart, "one"); var second = InteractionText(chart, "two");
        Assert.Same(first, InteractionText(chart, "one"));
        _ = InteractionText(chart, new string('z', 40));
        Assert.Same(first, InteractionText(chart, "one"));
        Assert.NotSame(second, InteractionText(chart, "two"));
        Assert.True(chart.GetResourceCacheInfo().EvictionCount >= 2); Consistent(chart);
    }

    [AvaloniaFact]
    public void Reused_axis_pen_keeps_live_brush_and_dash_references_and_zero_budget_returns_fresh_pens()
    {
        var chart = MakeChart(); using var host = new TemplateFeaturesHost(chart);
        var brush = new SolidColorBrush(Colors.Red); var dash = new DashStyle(new[] { 4d, 2d }, 1);
        var first = Pen(chart, brush, dash); brush.Color = Colors.Green; brush.Opacity = .375; dash.Offset = 3;
        var second = Pen(chart, brush, dash);
        Assert.Same(first, second); Assert.Same(brush, second.Brush); Assert.Same(dash, second.DashStyle);
        Assert.Equal(Colors.Green, ((ISolidColorBrush)second.Brush!).Color); Assert.Equal(3, second.DashStyle!.Offset);
        chart.ResourceCacheBudgetBytes = 0;
        Assert.NotSame(Pen(chart, brush, dash), Pen(chart, brush, dash));
        Assert.Equal(0, chart.GetResourceCacheInfo().AxisPenCount); Consistent(chart);
    }

    [AvaloniaFact]
    public void Cached_and_disabled_native_primitive_operations_keep_current_mutable_paints()
    {
        var chart = MakeChart(512); var brush = new SolidColorBrush(Colors.Red);
        chart.AxisBrush = brush; chart.GridLineBrush = brush; chart.Series[0].Stroke = brush;
        chart.Series[0].MarkerSize = 4; chart.Series[0].MarkerFill = brush; chart.Series[0].MarkerStroke = brush;
        using var host = new TemplateFeaturesHost(chart); Draw(chart);
        brush.Color = Colors.Purple; brush.Opacity = .41;
        _ = chart.PlotArea;
        using var cached = new SeriesDrawingLayerCacheTests.NativeBoundary(); chart.Render(cached.Drawing);
        chart.ResourceCacheBudgetBytes = 0;
        _ = chart.PlotArea;
        using var disabled = new SeriesDrawingLayerCacheTests.NativeBoundary(); chart.Render(disabled.Drawing);
        Assert.Equal(cached.Recorder.Operations, disabled.Recorder.Operations);
        Assert.Contains(cached.Recorder.Brushes, candidate => ReferenceEquals(candidate, brush)); Consistent(chart);
    }

    [AvaloniaFact]
    public void Unload_releases_all_local_resources_and_reattach_rebuilds_with_current_style()
    {
        var chart = MakeChart(); using var host = new TemplateFeaturesHost(chart); Draw(chart);
        _ = InteractionText(chart, "old"); Assert.NotNull(chart.HitTestSegment(Project(chart, new(50.5, 50.5)), 1));
        var builds = Counter(chart, "_seriesDrawingBuildCount");
        var shell = host.Window.Content;
        host.Window.Content = null; host.Drain();
        Assert.Equal(0, chart.GetResourceCacheInfo().EstimatedBytes); Consistent(chart);
        chart.FontSize = 23; chart.Series[0].Stroke = Brushes.Green;
        host.Window.Content = shell; host.Drain(); Draw(chart);
        Assert.True(Counter(chart, "_seriesDrawingBuildCount") > builds);
        Assert.Equal(1, chart.GetResourceCacheInfo().SeriesDrawingCount); Consistent(chart);
    }

    [AvaloniaFact]
    public void Segment_projection_memo_remains_bounded_and_trim_rebuilds_correct_original_endpoints()
    {
        var chart = MakeChart(20_000); using var host = new TemplateFeaturesHost(chart); Draw(chart);
        ChartSegmentHit? last = null;
        for (var index = 0; index < 3000; index++)
        {
            var value = index * 3 + 1.25;
            last = chart.HitTestSegment(Project(chart, new(value, value)), .001);
            Assert.NotNull(last);
        }
        var info = chart.GetResourceCacheInfo();
        Assert.Equal(1, info.SegmentGeometryCount); Assert.InRange(info.SegmentProjectionCount, 1, 4096);
        Assert.True(Counter(chart, "_segmentMemoEvictionCount") > 0); Consistent(chart);
        var query = Project(chart, new(8998.25, 8998.25));
        last = chart.HitTestSegment(query, .001); chart.TrimResourceCaches();
        Assert.Equal(0, chart.GetResourceCacheInfo().SegmentProjectionCount);
        var fresh = Assert.IsType<ChartSegmentHit>(chart.HitTestSegment(query, .001));
        Assert.Equal(last?.Start.Index, fresh.Start.Index); Assert.Equal(last?.End.Index, fresh.End.Index);
        Assert.Equal(last?.Start.StableId, fresh.Start.StableId); Assert.Equal(last?.End.StableId, fresh.End.StableId); Consistent(chart);
    }

    [AvaloniaFact]
    public void Segment_memo_growth_cannot_leave_a_stale_over_budget_reservation()
    {
        var chart = MakeChart(20_000); using var host = new TemplateFeaturesHost(chart);
        chart.ResourceCacheBudgetBytes = 1024;
        for (var index = 0; index < 30; index++)
        {
            var value = 500 + index * 17.25;
            Assert.NotNull(chart.HitTestSegment(Project(chart, new(value, value)), .001));
            Consistent(chart);
        }
        chart.TrimResourceCaches(); Assert.Equal(0, chart.GetResourceCacheInfo().EstimatedBytes);
    }

    [AvaloniaFact]
    public void Pareto_gradient_cumulative_geometry_accounts_for_its_independent_full_secondary_source()
    {
        const int primaryCount = 10_000, secondaryCount = 12_000;
        var chart = MakeChart(primaryCount); chart.Viewport = new(100, 200, 0, primaryCount - 1);
        using var host = new TemplateFeaturesHost(chart); Draw(chart);
        var series = new ParetoSeries { Stroke = Brushes.Blue, Fill = Brushes.Blue, CumulativeStroke = Gradient(), ShowMarkers = false };
        var secondary = new LineSeries { ItemsSource = Enumerable.Range(0, secondaryCount)
            .Select(index => new ChartPoint(index, (index + 1d) * 100 / secondaryCount)).ToArray() }.CreateSnapshot();
        var primary = Context(chart);
        var context = primary with { Series = series, Brush = Brushes.Blue, SecondaryData = secondary, SecondaryProjection = primary.Projection };
        Assert.Equal(primaryCount, context.Data.Count); Assert.Equal(secondaryCount, context.SecondaryData.Count);
        Assert.True(ChartVisibilityQuery.Segments(context, 11, neighbors: 1).Count < primaryCount / 10);
        var retained = ChartRetainedSeriesDrawing.Record(series.Renderer, context);
        Assert.True(retained.EstimatedBytes >= 1536L + 512L + secondaryCount * 32L);
        using var original = new SeriesDrawingLayerCacheTests.NativeBoundary(); series.Renderer.Render(original.Drawing, context);
        using var replay = new SeriesDrawingLayerCacheTests.NativeBoundary(); retained.Draw(replay.Drawing);
        Assert.NotEmpty(original.Recorder.Operations); Assert.Equal(original.Recorder.Operations, replay.Recorder.Operations);
    }

    [AvaloniaFact]
    public void Standalone_scatter_line_estimate_uses_gradient_stroke_geometry_instead_of_the_context_fallback_brush()
    {
        const int count = 10_000;
        var chart = MakeChart(count); chart.Viewport = new(100, 200, 0, count - 1);
        using var host = new TemplateFeaturesHost(chart); Draw(chart);
        var series = new ScatterLineSeries { Stroke = Gradient(), ShowMarkers = false };
        var context = Context(chart) with { Series = series, Brush = Brushes.Blue };
        Assert.IsAssignableFrom<ISolidColorBrush>(context.Brush);
        Assert.False(series.Stroke is ISolidColorBrush);
        Assert.True(ChartVisibilityQuery.Segments(context, 11, neighbors: 1).Count < count / 10);
        var figure = Assert.Single(ChartLineGeometry.Build(context));
        Assert.Equal(count - 1, figure.Upper.Count); Assert.Empty(figure.Lower);
        var retained = ChartRetainedSeriesDrawing.Record(series.Renderer, context);
        Assert.True(retained.EstimatedBytes >= 512L + 512L + 128L + (count - 1L) * 192);
        using var original = new SeriesDrawingLayerCacheTests.NativeBoundary(); series.Renderer.Render(original.Drawing, context);
        using var replay = new SeriesDrawingLayerCacheTests.NativeBoundary(); retained.Draw(replay.Drawing);
        Assert.NotEmpty(original.Recorder.Operations); Assert.Equal(original.Recorder.Operations, replay.Recorder.Operations);
    }

    private static LinearGradientBrush Gradient() => new() { GradientStops = [new(Colors.Red, 0), new(Colors.Blue, 1)] };
    private static ChartSeriesRenderContext Context(CartesianChart chart)
    {
        var scene = Field(chart, "_scene")!;
        var snapshot = ((Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!).GetValue(0)!;
        return (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context")!.GetValue(snapshot)!;
    }

    private static CartesianChart MakeChart(int count = 101)
    {
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = count - 1 }, YAxis = new NumericAxis { Minimum = 0, Maximum = count - 1 } };
        chart.Series.Add(new LineSeries { MarkerSize = 0, ItemsSource = Enumerable.Range(0, count).Select(index => new ChartPoint(index, index)).ToArray() });
        return chart;
    }
    private static void Draw(CartesianChart chart) { _ = chart.PlotArea; var drawing = TemplateFeaturesHost.Record(chart); drawing.Children.Clear(); }
    private static Point Project(CartesianChart chart, ChartPoint point)
    {
        var range = chart.VisibleRange!.Value; var plot = chart.PlotArea;
        return new(plot.Left + (point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
            plot.Bottom - (point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
    }
    private static FormattedText InteractionText(CartesianChart chart, string text) => (FormattedText)typeof(CartesianChart).GetMethod("InteractionText", Instance)!.Invoke(chart, [text, false])!;
    private static Pen Pen(CartesianChart chart, IBrush brush, IDashStyle dash) => (Pen)typeof(CartesianChart).GetMethod("CachedAxisPen", Instance)!.Invoke(chart,
        [brush, 2d, dash, PenLineCap.Round, PenLineJoin.Bevel])!;
    private static object? Field(CartesianChart chart, string name) => typeof(CartesianChart).GetField(name, Instance)!.GetValue(chart);
    private static long Counter(CartesianChart chart, string name) => Convert.ToInt64(Field(chart, name), CultureInfo.InvariantCulture);
    private static IDictionary Cache(CartesianChart chart, string name) => (IDictionary)Field(chart, name)!;
    private static void Consistent(CartesianChart chart)
    {
        var info = chart.GetResourceCacheInfo();
        Assert.Equal(info.EstimatedBytes, info.SeriesDrawingBytes + info.AxisTextBytes + info.InteractionTextBytes + info.AxisPenBytes + info.SegmentGeometryBytes);
        Assert.InRange(info.EstimatedBytes, 0, info.BudgetBytes);
        Assert.Equal(Cache(chart, "_seriesDrawings").Count, info.SeriesDrawingCount); Assert.Equal(Cache(chart, "_axisTexts").Count, info.AxisTextCount);
        Assert.Equal(Cache(chart, "_interactionTexts").Count, info.InteractionTextCount); Assert.Equal(Cache(chart, "_axisPens").Count, info.AxisPenCount);
        Assert.Equal(Cache(chart, "_segmentCaches").Count, info.SegmentGeometryCount);
    }
}
