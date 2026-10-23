using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class BackgroundLayoutTests
{
    [AvaloniaTheory]
    [InlineData("line", ChartLineInterpolation.Smooth)]
    [InlineData("line", ChartLineInterpolation.StepBefore)]
    [InlineData("line", ChartLineInterpolation.StepAfter)]
    [InlineData("line", ChartLineInterpolation.StepCenter)]
    [InlineData("area", ChartLineInterpolation.Linear)]
    [InlineData("area", ChartLineInterpolation.Smooth)]
    [InlineData("area", ChartLineInterpolation.StepBefore)]
    [InlineData("area", ChartLineInterpolation.StepAfter)]
    [InlineData("area", ChartLineInterpolation.StepCenter)]
    [InlineData("range", ChartLineInterpolation.Linear)]
    [InlineData("range", ChartLineInterpolation.Smooth)]
    [InlineData("range", ChartLineInterpolation.StepBefore)]
    [InlineData("range", ChartLineInterpolation.StepAfter)]
    [InlineData("range", ChartLineInterpolation.StepCenter)]
    public async Task Pure_worker_geometry_matches_synchronous_parts_gaps_duplicate_and_reversed_X(string kind, ChartLineInterpolation interpolation)
    {
        var chart = MakeChart(kind, interpolation); using var host = new TemplateFeaturesHost(chart);
        _ = chart.PlotArea; var context = Contexts(chart)[0];
        var expected = Describe(ChartLineGeometry.Build(context));
        var batch = ChartLineGeometry.CaptureBackground([context], []);
        var uiThread = Environment.CurrentManagedThreadId;
        var result = await Task.Run(() => (ChartLineGeometry.BuildBackground(batch, default), Environment.CurrentManagedThreadId));
        Assert.NotEqual(uiThread, result.Item2); Assert.Equal(expected, Describe(result.Item1[0]));
        Assert.Contains(result.Item1[0].SelectMany(figure => figure.Upper), part => part.StartOrdinal > 3);
        Assert.DoesNotContain(result.Item1[0].SelectMany(figure => figure.Upper), part => part.StartOrdinal < 3 && part.EndOrdinal > 3);
    }

    [AvaloniaTheory]
    [InlineData(ChartStackMode.Stacked)]
    [InlineData(ChartStackMode.Percent)]
    public async Task Smooth_stack_worker_uses_the_same_shared_boundary_limiter(ChartStackMode mode)
    {
        var chart = MakeChart("area", ChartLineInterpolation.Smooth);
        ((AreaSeries)chart.Series[0]).StackMode = mode;
        chart.Series.Add(new AreaSeries { ItemsSource = Points().Select(point => point with { Y = point.Y * .001 }).ToArray(),
            Interpolation = ChartLineInterpolation.Smooth, StackMode = mode, Fill = Brushes.Green });
        using var host = new TemplateFeaturesHost(chart); _ = chart.PlotArea;
        var contexts = Contexts(chart); var expected = contexts.Select(context => Describe(ChartLineGeometry.Build(context))).ToArray();
        var batch = ChartLineGeometry.CaptureBackground(contexts, [[0, 1]]);
        var actual = await Task.Run(() => ChartLineGeometry.BuildBackground(batch, default));
        for (var index = 0; index < contexts.Length; index++) Assert.Equal(expected[index], Describe(actual[index]));
    }

    [AvaloniaFact]
    public async Task External_cancellation_restores_shared_stack_limiter_before_synchronous_fallback_drawing()
    {
        var chart = MakeChart("area", ChartLineInterpolation.Smooth); ((AreaSeries)chart.Series[0]).StackMode = ChartStackMode.Stacked;
        chart.Series.Add(new AreaSeries { ItemsSource = Points().Select(point => point with { Y = point.Y * .001 }).ToArray(),
            Interpolation = ChartLineInterpolation.Smooth, StackMode = ChartStackMode.Stacked, Fill = Brushes.Green });
        using var host = new TemplateFeaturesHost(chart); var expected = Contexts(chart).Select(context => Describe(ChartLineGeometry.Build(context))).ToArray();
        chart.UseBackgroundLayout = true; using var cancellation = new CancellationTokenSource();
        var pending = chart.PrepareBackgroundLayoutAsync(cancellation.Token); cancellation.Cancel(); Assert.False(await pending);
        var actual = Contexts(chart).Select(context => Describe(ChartLineGeometry.Build(context))).ToArray();
        for (var index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }

    [AvaloniaFact]
    public async Task Opt_in_preparation_publishes_actual_geometry_and_preserves_original_segment_identity()
    {
        var chart = MakeChart("line", ChartLineInterpolation.Smooth); using var host = new TemplateFeaturesHost(chart);
        var expected = Describe(ChartLineGeometry.Build(Contexts(chart)[0]));
        var uiThread = Environment.CurrentManagedThreadId; chart.UseBackgroundLayout = true;
        Assert.True(await chart.PrepareBackgroundLayoutAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        var info = chart.GetBackgroundLayoutInfo();
        Assert.False(info.IsBusy); Assert.True(info.CompletedCount > 0); Assert.True(info.PublishedVertexCount > 0);
        Assert.NotEqual(uiThread, info.LastWorkerThreadId); Assert.Null(info.LastError);
        var context = Contexts(chart)[0]; Assert.Equal(expected, Describe(ChartLineGeometry.Build(context)));
        var part = ChartLineGeometry.Build(context)[0].Upper[0];
        var hit = Assert.IsType<ChartSegmentHit>(chart.HitTestSegment(part.At(.5), .01));
        Assert.Equal(part.StartOrdinal, hit.Start.Index); Assert.Equal(part.EndOrdinal, hit.End.Index);
        Assert.Equal(context.Data.Points[part.StartOrdinal].Item, hit.Start.Item);
        Assert.Equal(context.Data.Points[part.StartOrdinal].StableId, hit.Start.StableId);
    }

    [AvaloniaFact]
    public async Task Retargeting_before_UI_publication_cannot_replace_the_latest_scene_with_old_geometry()
    {
        var chart = MakeChart("line", ChartLineInterpolation.Smooth); using var host = new TemplateFeaturesHost(chart);
        chart.UseBackgroundLayout = true; var old = chart.PrepareBackgroundLayoutAsync();
        // Keep this UI turn: a completed numerical result can only queue its UI callback.
        Thread.Sleep(30);
        chart.Series[0].ItemsSource = Points().Select(point => point with { Y = point.Y * .2 + 2 }).ToArray();
        var latest = chart.PrepareBackgroundLayoutAsync();
        Assert.False(await old.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(await latest.WaitAsync(TimeSpan.FromSeconds(10)));
        var context = Contexts(chart)[0]; var actual = Describe(ChartLineGeometry.Build(context));
        chart.UseBackgroundLayout = false; _ = chart.PlotArea;
        Assert.Equal(actual, Describe(ChartLineGeometry.Build(Contexts(chart)[0])));
        Assert.True(chart.GetBackgroundLayoutInfo().CanceledCount > 0);
    }

    [AvaloniaFact]
    public async Task Admission_rejection_happens_before_projection_capture_and_returns_synchronous_geometry()
    {
        var budget = ChartWorkScheduler.BackgroundPayloadBudgetBytes;
        try
        {
            ChartWorkScheduler.BackgroundPayloadBudgetBytes = 0;
            var chart = MakeChart("area", ChartLineInterpolation.Smooth); chart.UseBackgroundLayout = true;
            using var host = new TemplateFeaturesHost(chart);
            Assert.False(await chart.PrepareBackgroundLayoutAsync());
            var info = chart.GetBackgroundLayoutInfo(); Assert.Equal(0, info.CapturedVertexCount);
            Assert.Equal(0, info.SubmittedCount); Assert.True(info.FallbackCount > 0);
            Assert.NotEmpty(ChartLineGeometry.Build(Contexts(chart)[0]));
            Assert.Equal(0, ChartWorkScheduler.GetBudgetInfo().EstimatedBackgroundPayloadBytes);
        }
        finally { ChartWorkScheduler.BackgroundPayloadBudgetBytes = budget; }
    }

    [AvaloniaFact]
    public async Task Hidden_and_detached_charts_cancel_pending_results_and_reenter_with_latest_data()
    {
        var chart = MakeChart("line", ChartLineInterpolation.Smooth); using var host = new TemplateFeaturesHost(chart);
        chart.UseBackgroundLayout = true; var pending = chart.PrepareBackgroundLayoutAsync();
        chart.IsVisible = false;
        Assert.False(await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.False(await chart.PrepareBackgroundLayoutAsync()); Assert.False(chart.GetBackgroundLayoutInfo().IsBusy);
        var completed = chart.GetBackgroundLayoutInfo().CompletedCount;
        chart.Series[0].ItemsSource = Points().Select(point => point with { Y = point.Y * .75 }).ToArray();
        chart.IsVisible = true; host.Drain(); Assert.True(await chart.PrepareBackgroundLayoutAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(chart.GetBackgroundLayoutInfo().CompletedCount > completed);
        chart.Viewport = new(1, 7, 0, 10); var detached = chart.PrepareBackgroundLayoutAsync();
        var shell = host.Window.Content; host.Window.Content = null;
        Assert.False(await detached.WaitAsync(TimeSpan.FromSeconds(10))); Assert.False(chart.GetBackgroundLayoutInfo().IsBusy);
        host.Window.Content = shell; host.Drain(); Assert.True(await chart.PrepareBackgroundLayoutAsync().WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [AvaloniaFact]
    public async Task Explicit_cancel_and_external_cancellation_release_pending_markers_without_partial_geometry()
    {
        var chart = MakeChart("area", ChartLineInterpolation.Smooth); using var host = new TemplateFeaturesHost(chart);
        chart.UseBackgroundLayout = true; var pending = chart.PrepareBackgroundLayoutAsync(); chart.CancelBackgroundLayout();
        Assert.False(await pending); Assert.False(chart.GetBackgroundLayoutInfo().IsBusy);
        Assert.NotEmpty(ChartLineGeometry.Build(Contexts(chart)[0]));
        using var cancellation = new CancellationTokenSource();
        var next = chart.PrepareBackgroundLayoutAsync(cancellation.Token); cancellation.Cancel();
        Assert.False(await next.WaitAsync(TimeSpan.FromSeconds(10))); Assert.False(chart.GetBackgroundLayoutInfo().IsBusy);
        Assert.NotEmpty(ChartLineGeometry.Build(Contexts(chart)[0]));
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => { _ = chart.PrepareBackgroundLayoutAsync(cancellation.Token); });
    }

    [AvaloniaFact]
    public async Task Numeric_work_cancellation_throws_before_returning_any_partial_path()
    {
        var vertices = Enumerable.Range(0, 10_000).Select(index => new ChartLineGeometry.NumericVertex(index, new(index, index % 7), new(index, 0))).ToArray();
        var batch = new ChartLineGeometry.NumericBatch([new(vertices, ChartLineInterpolation.Smooth, true)], []);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Run(() => ChartLineGeometry.BuildBackground(batch, cancellation.Token)));
    }

    [AvaloniaFact]
    public async Task Linear_and_animated_fallbacks_are_explicit_and_default_is_synchronous()
    {
        var chart = MakeChart("line", ChartLineInterpolation.Linear); using var host = new TemplateFeaturesHost(chart);
        Assert.False(chart.UseBackgroundLayout); chart.UseBackgroundLayout = true;
        Assert.False(await chart.PrepareBackgroundLayoutAsync()); Assert.Equal(0, chart.GetBackgroundLayoutInfo().SubmittedCount);
        ((LineSeries)chart.Series[0]).Interpolation = ChartLineInterpolation.Smooth;
        chart.AnimationTriggers = ChartAnimationTriggers.Data;
        Assert.False(await chart.PrepareBackgroundLayoutAsync()); Assert.Equal(0, chart.GetBackgroundLayoutInfo().SubmittedCount);
        Assert.Contains("Animated", chart.GetBackgroundLayoutInfo().LastFallbackReason);
    }

    [AvaloniaFact]
    public async Task Worker_never_reenters_application_getters_and_published_drawings_keep_live_paint()
    {
        var rows = Enumerable.Range(0, 512).Select(index => new UiRow(index, index % 13)).ToArray();
        var brush = new SolidColorBrush(Colors.Red);
        var chart = MakeChart("area", ChartLineInterpolation.Smooth);
        chart.Series[0].ItemsSource = rows; chart.Series[0].CategoryPath = "X"; chart.Series[0].ValuePath = "Y";
        chart.Series[0].Stroke = brush; chart.Series[0].Fill = brush;
        using var host = new TemplateFeaturesHost(chart); _ = chart.PlotArea;
        var reads = rows.Sum(row => row.Reads); chart.UseBackgroundLayout = true;
        var pending = chart.PrepareBackgroundLayoutAsync(); brush.Color = Colors.Purple;
        Assert.True(await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(reads, rows.Sum(row => row.Reads));
        var drawing = TemplateFeaturesHost.Record(chart);
        Assert.Contains(TemplateFeaturesHost.Walk(drawing).OfType<GeometryDrawing>(), candidate =>
            ReferenceEquals(candidate.Brush, brush) && ((ISolidColorBrush)candidate.Brush).Color == Colors.Purple);
    }

    [AvaloniaFact]
    public async Task Explicit_source_vertex_cap_rejects_before_numeric_payload_capture()
    {
        var chart = MakeChart("line", ChartLineInterpolation.Smooth);
        chart.Series[0].ItemsSource = Enumerable.Range(0, 131_073).Select(index => new ChartPoint(index, index % 7)).ToArray();
        chart.UseBackgroundLayout = true; using var host = new TemplateFeaturesHost(chart);
        Assert.False(await chart.PrepareBackgroundLayoutAsync());
        var info = chart.GetBackgroundLayoutInfo(); Assert.Equal(0, info.CapturedVertexCount); Assert.Equal(0, info.SubmittedCount);
        Assert.Contains("131072", info.LastFallbackReason); Assert.NotEmpty(ChartLineGeometry.Build(Contexts(chart)[0]));
    }

    [AvaloniaTheory]
    [InlineData("line")]
    [InlineData("area")]
    [InlineData("range")]
    public void Export_after_scene_invalidation_prepares_synchronously_before_any_background_capture(string kind)
    {
        var chart = MakeChart(kind, ChartLineInterpolation.Smooth); using var host = new TemplateFeaturesHost(chart);
        var expected = Describe(ChartLineGeometry.Build(Contexts(chart)[0]));
        chart.UseBackgroundLayout = true;
        var drawing = ExportDrawing(chart);
        Assert.Equal(0, chart.GetBackgroundLayoutInfo().SubmittedCount);
        Assert.Equal(expected, Describe(ChartLineGeometry.Build(Contexts(chart)[0])));
        Assert.Contains(TemplateFeaturesHost.Walk(drawing).OfType<GeometryDrawing>(), entry =>
            ReferenceEquals(entry.Pen?.Brush, chart.Series[0].Stroke) && entry.Geometry?.Bounds.Width > 0);
    }

    [AvaloniaTheory]
    [InlineData("line")]
    [InlineData("area")]
    [InlineData("range")]
    public async Task Export_while_pending_ignores_empty_screen_drawings_and_keeps_pending_result_isolated(string kind)
    {
        var chart = MakeChart(kind, ChartLineInterpolation.Smooth); using var host = new TemplateFeaturesHost(chart);
        chart.UseBackgroundLayout = true; var pending = chart.PrepareBackgroundLayoutAsync();
        var context = Contexts(chart)[0]; Assert.True(ChartLineGeometry.IsBackgroundPending(context));
        _ = TemplateFeaturesHost.Record(chart); // A cached empty screen path must never be exported.
        var before = ExportDrawing(chart); Assert.True(ChartLineGeometry.IsBackgroundPending(context));
        Assert.Contains(TemplateFeaturesHost.Walk(before).OfType<GeometryDrawing>(), entry =>
            ReferenceEquals(entry.Pen?.Brush, chart.Series[0].Stroke) && entry.Geometry?.Bounds.Width > 0);
        Assert.True(await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        var after = ExportDrawing(chart);
        Assert.Equal(DrawingDescription(before), DrawingDescription(after));
    }

    private static CartesianChart MakeChart(string kind, ChartLineInterpolation mode)
    {
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
            YAxis = new NumericAxis { Minimum = -5, Maximum = 15 } };
        ChartSeries series = kind switch
        {
            "area" => new AreaSeries { ItemsSource = Points(), Interpolation = mode, Baseline = -2 },
            "range" => new RangeAreaSeries { ItemsSource = Points().Select(point => new BandRow(point.X, point.Y - 1, point.Y + 2)).ToArray(),
                CategoryPath = "X", ValuePath = "High", HighPath = "High", LowPath = "Low", Interpolation = mode },
            _ => new LineSeries { ItemsSource = Points(), Interpolation = mode },
        };
        series.Stroke = Brushes.Blue; series.Fill = Brushes.CornflowerBlue; series.MarkerSize = 0; chart.Series.Add(series); return chart;
    }
    private static ChartPoint[] Points() => [new(0, 1), new(1, 8), new(1, 3), new(2, double.NaN), new(4, 4), new(5, 9), new(4.5, 2), new(7, 7), new(10, 1)];
    private sealed record BandRow(double X, double Low, double High);
    private sealed class UiRow(double x, double y)
    {
        public int Reads;
        public double X { get { Dispatcher.UIThread.VerifyAccess(); Reads++; return x; } }
        public double Y { get { Dispatcher.UIThread.VerifyAccess(); Reads++; return y; } }
    }
    private static ChartLinePathPart[] Describe(IReadOnlyList<ChartLinePathFigure> figures) =>
        figures.SelectMany(figure => figure.Upper.Concat(figure.Lower)).ToArray();
    private static DrawingGroup ExportDrawing(CartesianChart chart)
    {
        var result = new DrawingGroup(); using (var drawing = result.Open())
            chart.RenderExport(drawing, chart.GetExportLayout(null), null);
        return result;
    }
    private static string[] DrawingDescription(DrawingGroup drawing) => TemplateFeaturesHost.Walk(drawing)
        .OfType<GeometryDrawing>().Select(entry => $"{entry.Geometry?.Bounds}|{entry.Brush}|{entry.Pen?.Brush}|{entry.Pen?.Thickness}").ToArray();
    private static ChartSeriesRenderContext[] Contexts(CartesianChart chart)
    {
        _ = chart.PlotArea;
        var scene = typeof(CartesianChart).GetField("_scene", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(chart)!;
        var snapshots = (Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!;
        return snapshots.Cast<object>().Select(snapshot => (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context")!.GetValue(snapshot)!).ToArray();
    }
}
