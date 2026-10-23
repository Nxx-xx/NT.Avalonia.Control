using System.Collections;
using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

[Collection("Line sampling diagnostics")]
public sealed class SamplingIntegrationTests
{
    [AvaloniaFact]
    public void Public_defaults_validation_and_cancellation_are_explicit()
    {
        var line = new LineSeries();
        Assert.Equal(ChartSamplingMode.None, line.SamplingMode); Assert.Equal(1, line.SamplingPixelWidth);
        Assert.False(new ChartExportOptions().UseSampling);
        Assert.Throws<ArgumentException>(() => line.SamplingMode = (ChartSamplingMode)42);
        Assert.Throws<ArgumentException>(() => line.SamplingPixelWidth = 0);
        Assert.Throws<ArgumentException>(() => line.SamplingPixelWidth = 65);
        var chart = new CartesianChart(); using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => chart.GetSamplingInfo(line, canceled.Token));
        Assert.Throws<ArgumentException>(() => chart.GetSamplingInfo(line));
    }

    [AvaloniaFact]
    public void An_omitted_original_item_still_drives_exact_hit_tooltip_point_and_rectangle_selection()
    {
        var rows = Rows(20_000); var line = Series(rows); var chart = Chart(line, rows.Length);
        using var host = new TemplateFeaturesHost(chart);
        TemplateFeaturesHost.Record(chart);
        var context = Context(chart); var plan = ChartSamplingPlanner.For(context);
        Assert.True(plan.Info.IsApplied); Assert.NotNull(plan.Sampling);
        Assert.Equal(2, plan.Info.FigureCount);
        var selected = plan.Sampling!.ToHashSet();
        Assert.Contains(rows.Length / 3, selected); Assert.Contains(rows.Length * 2 / 3, selected);
        var ordinal = Enumerable.Range(100, rows.Length / 3 - 200).First(i => !selected.Contains(i));
        var position = context.Projection.Project(new(rows[ordinal].X, rows[ordinal].Y));
        var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(position, 0));
        Assert.Equal(ordinal, hit.Index); Assert.Same(rows[ordinal], hit.Item); Assert.Equal(rows[ordinal].Id, hit.StableId);
        host.Window.MouseMove(chart.TranslatePoint(position, host.Window)!.Value); host.Drain();
        Assert.Same(rows[ordinal], chart.HoveredPoint!.Item); Assert.Equal(rows[ordinal].Y, chart.HoveredPoint.Point.Y);
        chart.SelectedPoint = hit; Assert.Same(rows[ordinal], chart.SelectedPoint!.Item);
        var rectangle = new Rect(position.X - .1, position.Y - .1, .2, .2);
        var expected = rows.Select((row, i) => (row, i)).Where(item => double.IsFinite(item.row.Y) &&
            rectangle.Contains(context.Projection.Project(new(item.row.X, item.row.Y)))).Select(item => item.i).ToArray();
        var result = chart.SelectRectangle(rectangle);
        Assert.Equal(expected.Length, result.MatchedCount);
        Assert.Equal(expected, chart.SelectedPoints!.Select(point => point.Index));
        foreach (var point in chart.SelectedPoints!) Assert.Same(rows[point.Index], point.Item);
        Assert.Equal(rows.Length, context.Data.Count);
    }

    [AvaloniaFact]
    public void Zoom_style_and_pixel_budget_changes_reuse_numeric_data_and_rebuild_only_the_display_plan()
    {
        var rows = Rows(20_000); var source = new CountedRows(rows); var line = Series(source); var chart = Chart(line, rows.Length);
        using var host = new TemplateFeaturesHost(chart); TemplateFeaturesHost.Record(chart);
        var original = Context(chart).Data; var dense = chart.GetSamplingInfo(line);
        var builds = ChartSamplingIndex.BuildCount; var enumerations = source.Enumerations;
        line.SamplingPixelWidth = 4; host.Drain(); TemplateFeaturesHost.Record(chart);
        var coarse = chart.GetSamplingInfo(line);
        Assert.Same(original, Context(chart).Data); Assert.True(coarse.RenderedCount < dense.RenderedCount);
        chart.Viewport = new(1000, 1010, -110, 110); host.Drain(); TemplateFeaturesHost.Record(chart);
        var narrow = chart.GetSamplingInfo(line);
        Assert.InRange(narrow.RenderedCount, 10, 30); Assert.False(narrow.IsApplied);
        Assert.Same(original, Context(chart).Data); Assert.Equal(builds, ChartSamplingIndex.BuildCount);
        Assert.Equal(enumerations, source.Enumerations);
        line.SamplingMode = ChartSamplingMode.None; host.Drain();
        Assert.Equal("Disabled", chart.GetSamplingInfo(line).FallbackReason);
        Assert.Same(original, Context(chart).Data); Assert.Equal(enumerations, source.Enumerations);
        line.ItemsSource = Rows(20_000); host.Drain(); line.SamplingMode = ChartSamplingMode.MinMax; chart.ResetViewport();
        host.Drain(); TemplateFeaturesHost.Record(chart);
        Assert.NotSame(original, Context(chart).Data); Assert.True(ChartSamplingIndex.BuildCount > builds);
    }

    [AvaloniaTheory]
    [InlineData("smooth", "InterpolationRequiresOriginalData")]
    [InlineData("stack", "StackRequiresOriginalData")]
    [InlineData("markers", "MarkersRequireOriginalData")]
    [InlineData("labels", "LabelsRequireOriginalData")]
    [InlineData("gradient", "SpatialBrushRequiresOriginalData")]
    [InlineData("unordered", "UnorderedX")]
    [InlineData("break", "ScaleBreakRequiresOriginalData")]
    public void Unsupported_semantics_keep_original_geometry_and_report_the_reason(string kind, string reason)
    {
        var rows = Rows(10_000); if (kind == "unordered") new Random(517).Shuffle(rows);
        var line = Series(rows); var chart = Chart(line, rows.Length);
        switch (kind)
        {
            case "smooth": line.Interpolation = ChartLineInterpolation.Smooth; break;
            case "stack": line.StackMode = ChartStackMode.Stacked; break;
            case "markers": line.ShowMarkers = true; line.MarkerSize = 4; break;
            case "labels": line.ShowDataLabels = true; break;
            case "gradient": line.Stroke = new LinearGradientBrush { GradientStops = [new(Colors.Red, 0), new(Colors.Blue, 1)] }; break;
            case "break": chart.XAxis.ScaleBreaks.Add(new ChartScaleBreak { Start = 2000, End = 2100 }); break;
        }
        using var host = new TemplateFeaturesHost(chart); TemplateFeaturesHost.Record(chart);
        var info = chart.GetSamplingInfo(line); Assert.False(info.IsApplied); Assert.Equal(reason, info.FallbackReason);
        Assert.Null(ChartSamplingPlanner.For(Context(chart)).Sampling);
        Assert.Equal(rows.Length, Context(chart).Data.Count);
    }

    [AvaloniaFact]
    public void Export_sampling_uses_output_pixels_cancellation_and_preserves_the_screen_plan()
    {
        var rows = Rows(20_000); var line = Series(rows); var chart = Chart(line, rows.Length);
        using var host = new TemplateFeaturesHost(chart); TemplateFeaturesHost.Record(chart);
        var before = chart.GetSamplingInfo(line); var context = Context(chart);
        long Export(bool sampled, double factor, CancellationToken token = default)
        {
            var layout = chart.GetExportLayout(new() { PixelSize = new((int)(chart.Bounds.Width * factor), (int)(chart.Bounds.Height * factor)), UseSampling = sampled });
            var projected = ChartVisibilityQuery.ProjectedCount;
            var group = new DrawingGroup(); using (var drawing = group.Open()) chart.RenderExport(drawing, layout, null, sampled, token);
            return ChartVisibilityQuery.ProjectedCount - projected;
        }
        var original = Export(false, 1); var one = Export(true, 1); var two = Export(true, 2); var half = Export(true, .5);
        Assert.True(one < original / 2); Assert.True(two > one); Assert.True(two < original);
        Assert.InRange(half, 1, one - 1);
        Assert.Equal(before, chart.GetSamplingInfo(line)); Assert.Same(context, Context(chart));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => Export(true, 2, canceled.Token));
        Assert.Equal(before, chart.GetSamplingInfo(line));
        TemplateFeaturesHost.Record(chart); Assert.Equal(before, chart.GetSamplingInfo(line));
    }

    [AvaloniaFact]
    public void Standalone_render_scale_changes_pixel_density_and_native_replay_does_not_resample()
    {
        var rows = Rows(20_000); var line = Series(rows); var data = line.CreateSnapshot();
        ChartDataExtrema.For(data);
        ChartSeriesRenderContext ContextAt(double scale) => new(line, data, new CartesianChart.CartesianProjection(new(13.25, 21.375, 300.125, 200.625),
            ChartScale.CreateForView(new NumericAxis(), 0, rows.Length - 1), ChartScale.CreateForView(new NumericAxis(), -110, 110), data), Brushes.Blue) { RenderScaling = scale };
        var one = ContextAt(1); var two = ContextAt(2);
        Assert.True(ChartSamplingPlanner.For(two).Info.RenderedCount > ChartSamplingPlanner.For(one).Info.RenderedCount);
        using var actual = new SeriesDrawingLayerCacheTests.NativeBoundary(); line.Renderer.Render(actual.Drawing, two);
        var retained = ((CartesianSeriesRenderer)line.Renderer).CreateRetainedDrawing(two);
        var queries = ChartSamplingIndex.QueryCount; var projections = ChartVisibilityQuery.ProjectedCount;
        using var replay = new SeriesDrawingLayerCacheTests.NativeBoundary(); retained.Draw(replay.Drawing);
        Assert.Equal(actual.Recorder.Operations, replay.Recorder.Operations);
        Assert.Equal(queries, ChartSamplingIndex.QueryCount); Assert.Equal(projections, ChartVisibilityQuery.ProjectedCount);
    }

    [AvaloniaFact]
    public void Reduced_output_scale_keeps_one_physical_antialias_pixel_beyond_the_miter_guard()
    {
        var rows = Rows(20_000); var line = Series(rows); var data = line.CreateSnapshot();
        var context = new ChartSeriesRenderContext(line, data, new CartesianChart.CartesianProjection(new(0, 0, 300, 200),
            ChartScale.CreateForView(new NumericAxis(), 0, rows.Length - 1), ChartScale.CreateForView(new NumericAxis(), -110, 110)), Brushes.Blue)
            { RenderScaling = .25 };
        var info = ChartSamplingPlanner.For(context).Info;
        var guard = 5 * line.StrokeThickness + 1 / context.RenderScaling;
        Assert.Equal((int)Math.Floor((context.PlotArea.Width + 2 * guard) * context.RenderScaling) + 1, info.PixelBucketCount);
        Assert.True(info.IsApplied);
    }

    [AvaloniaFact]
    public void Warm_tooltip_moves_reuse_the_display_plan_and_segment_readouts_keep_original_adjacent_endpoints()
    {
        var rows = Rows(10_000); var source = new CountedRows(rows); var line = Series(source); var chart = Chart(line, rows.Length);
        using var host = new TemplateFeaturesHost(chart); TemplateFeaturesHost.Record(chart);
        var context = Context(chart); var plan = ChartSamplingPlanner.For(context); var kept = plan.Sampling!.ToHashSet();
        var ordinal = Enumerable.Range(500, 1000).First(i => !kept.Contains(i) && !kept.Contains(i + 1));
        var first = context.Projection.Project(new(rows[ordinal].X, rows[ordinal].Y));
        var last = context.Projection.Project(new(rows[ordinal + 1].X, rows[ordinal + 1].Y));
        var query = new Point((first.X + last.X) / 2, (first.Y + last.Y) / 2);
        var segment = Assert.IsType<ChartSegmentHit>(chart.HitTestSegment(query, .00001));
        Assert.Equal(ordinal, segment.Start.Index); Assert.Equal(ordinal + 1, segment.End.Index);
        Assert.Same(rows[ordinal], segment.Start.Item); Assert.Same(rows[ordinal + 1], segment.End.Item);
        // Entering the chart changes its visual hover state once. Measure warm
        // overlay moves after that normal initial view transition has completed.
        host.Window.MouseMove(chart.TranslatePoint(first, host.Window)!.Value); host.Drain(); TemplateFeaturesHost.Record(chart);
        var queries = ChartSamplingIndex.QueryCount; var plans = ChartSamplingPlanner.PlanBuildCount; var enumerations = source.Enumerations;
        for (var i = 0; i < 8; i++)
        {
            var position = context.Projection.Project(new(rows[ordinal + i].X, rows[ordinal + i].Y));
            host.Window.MouseMove(chart.TranslatePoint(position, host.Window)!.Value); host.Drain(); TemplateFeaturesHost.Record(chart);
        }
        Assert.Equal(queries, ChartSamplingIndex.QueryCount); Assert.Equal(plans, ChartSamplingPlanner.PlanBuildCount);
        Assert.Equal(enumerations, source.Enumerations);
    }

    private sealed record Row(double X, double Y, string Id);
    private static Row[] Rows(int count) => Enumerable.Range(0, count).Select(i => new Row(i,
        i >= count / 2 - 2 && i <= count / 2 + 2 ? double.NaN : i == count / 3 ? 100 : i == count * 2 / 3 ? -100 : Math.Sin(i * .31) * 12,
        $"raw-{i}")).ToArray();
    private static LineSeries Series(IEnumerable rows) => new() { ItemsSource = rows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id",
        ShowMarkers = false, MarkerSize = 0, SamplingMode = ChartSamplingMode.MinMax };
    private static CartesianChart Chart(LineSeries line, int count)
    {
        var chart = new CartesianChart { Width = 700, Height = 390, ShowLegend = false, IsTooltipEnabled = true, SelectionMode = ChartSelectionMode.Multiple,
            MaxSelectionCount = 20_000, XAxis = new NumericAxis { Minimum = 0, Maximum = count - 1 }, YAxis = new NumericAxis { Minimum = -110, Maximum = 110 } };
        chart.Series.Add(line); return chart;
    }
    private static ChartSeriesRenderContext Context(CartesianChart chart)
    {
        _ = chart.PlotArea;
        var scene = typeof(CartesianChart).GetField("_scene", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
        var snapshot = ((Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!).GetValue(0)!;
        return (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context")!.GetValue(snapshot)!;
    }
    private sealed class CountedRows(Row[] rows) : IEnumerable<Row>
    {
        public int Enumerations;
        public IEnumerator<Row> GetEnumerator() { Enumerations++; return ((IEnumerable<Row>)rows).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
