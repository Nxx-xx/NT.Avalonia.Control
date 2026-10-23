using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class StreamingChartTests
{
    [AvaloniaFact]
    public void Following_tracks_original_primary_data_and_keeps_surviving_selected_identity_after_eviction()
    {
        var source = Buffer(); var series = Series(source); var chart = Chart(series);
        using var host = new Host(chart);
        var old = series.CreateSnapshot(); var selected = old.Points[75];
        chart.SelectedPoint = ChartPointHit.FromData(series, selected, default);
        Range(chart, 79, 99);
        source.AppendRange(Rows(100, 10));
        Range(chart, 89, 109);
        Assert.Equal(100, old.Points.Count); Assert.Equal(0, old.Points[0].Point.X);
        Assert.Equal(99, old.Points[99].Point.X);
        Assert.NotNull(chart.SelectedPoint);
        Assert.Same(selected.Item, chart.SelectedPoint.Item); Assert.Equal(selected.StableId, chart.SelectedPoint.StableId);
        Assert.Equal(65, chart.SelectedPoint.Index);
        Assert.Equal(10, series.CreateSnapshot().Points[0].Point.X);
    }

    [AvaloniaFact]
    public void Turning_follow_off_freezes_the_actual_window_and_enabling_it_resumes_at_latest()
    {
        var source = Buffer(); var chart = Chart(Series(source)); using var host = new Host(chart);
        var before = chart.VisibleRange!.Value;
        chart.FollowLatestData = false;
        source.AppendRange(Rows(100, 10));
        Assert.Equal(before, chart.VisibleRange!.Value);
        chart.FollowLatestData = true;
        Range(chart, 89, 109);
    }

    [AvaloniaFact]
    public void An_explicit_viewport_pauses_follow_without_overwriting_the_requested_destination()
    {
        var source = Buffer(); var chart = Chart(Series(source)); using var host = new Host(chart);
        var requested = new ChartViewport(20, 40, -1, 10);
        chart.Viewport = requested;
        Assert.False(chart.FollowLatestData); Assert.Equal(requested, chart.Viewport); Assert.Equal(requested, chart.VisibleRange);
        source.AppendRange(Rows(100, 10));
        Assert.Equal(requested, chart.Viewport); Assert.Equal(requested, chart.VisibleRange);
        chart.FollowLatestData = true; Range(chart, 89, 109);
        Assert.Equal(requested.YMinimum, chart.VisibleRange!.Value.YMinimum);
        Assert.Equal(requested.YMaximum, chart.VisibleRange!.Value.YMaximum);
    }

    [AvaloniaFact]
    public void Axis_replacement_and_domain_reset_preserve_the_requested_follow_policy()
    {
        var chart = Chart(Series(Buffer())); using var host = new Host(chart);
        Assert.True(chart.FollowLatestData); Range(chart, 79, 99);
        chart.XAxis = new NumericAxis { Minimum = 0, Maximum = 300 };
        Assert.True(chart.FollowLatestData); Range(chart, 79, 99);
        chart.XAxis.Maximum = 400;
        Assert.True(chart.FollowLatestData); Range(chart, 79, 99);
        chart.XAxis.ViewMinimum = 20;
        Assert.False(chart.FollowLatestData);
    }

    [AvaloniaFact]
    public void Reset_viewport_stops_follow_even_when_requested_viewport_was_already_null()
    {
        var chart = Chart(Series(Buffer())); using var host = new Host(chart);
        Assert.Null(chart.Viewport); Range(chart, 79, 99);
        chart.ResetViewport();
        Assert.False(chart.FollowLatestData); Assert.Null(chart.Viewport); Range(chart, 0, 200);
    }

    [AvaloniaFact]
    public async Task Paused_streaming_and_paused_follow_are_independent_and_two_queued_batches_merge()
    {
        var source = Buffer(); var chart = Chart(Series(source)); using var host = new Host(chart);
        using var session = new ChartStreamingSession<Row>(items =>
        { using (chart.DeferDataUpdates()) source.AppendRange(items); }, maximumBatchSize: 10);
        session.Pause(); var commits = Counter(chart, "_deferredDataCommitCount");
        Assert.True(session.TryAppend(Rows(100, 5))); Assert.True(session.TryAppend(Rows(105, 5)));
        Dispatcher.UIThread.RunJobs(); Range(chart, 79, 99);
        Assert.True(chart.FollowLatestData); Assert.Equal(10, session.Statistics.PendingSamples);
        var flush = session.FlushAsync(); Assert.False(flush.IsCompleted);
        session.Resume(); Dispatcher.UIThread.RunJobs(); await flush;
        Range(chart, 89, 109); Assert.Equal(1, session.Statistics.AppliedUiBatches);
        Assert.Equal(1, session.Statistics.CoalescedBatches); Assert.Equal(commits + 1, Counter(chart, "_deferredDataCommitCount"));
        chart.FollowLatestData = false; var paused = chart.VisibleRange;
        await session.AppendAsync(Rows(110, 1)); Dispatcher.UIThread.RunJobs(); await session.FlushAsync();
        Assert.Equal(paused, chart.VisibleRange); Assert.Equal(11, session.Statistics.CommittedSamples);
    }

    [AvaloniaFact]
    public void Nested_data_scopes_coalesce_once_and_queries_inside_the_scope_never_reuse_stale_prepared_data()
    {
        var source = Buffer(); var series = Series(source); var chart = Chart(series); using var host = new Host(chart);
        var commits = Counter(chart, "_deferredDataCommitCount");
        using (chart.DeferDataUpdates())
        {
            source.AppendRange(Rows(100, 1)); Range(chart, 80, 100);
            using (chart.DeferDataUpdates()) { source.AppendRange(Rows(101, 1)); Range(chart, 81, 101); }
            Assert.Equal(commits, Counter(chart, "_deferredDataCommitCount"));
        }
        Assert.Equal(commits + 1, Counter(chart, "_deferredDataCommitCount")); Range(chart, 81, 101);
        Assert.Equal(2, series.CreateSnapshot().Points[0].Point.X);
    }

    [AvaloniaFact]
    public void Scope_disposal_after_detach_does_not_build_an_unowned_view_and_reattach_observes_latest_data()
    {
        var source = Buffer(); var chart = Chart(Series(source)); using var host = new Host(chart);
        var scope = chart.DeferDataUpdates(); source.AppendRange(Rows(100, 1));
        host.Window.Content = null;
        var views = Counter(chart, "_viewBuildCount");
        scope.Dispose(); scope.Dispose();
        Assert.Equal(views, Counter(chart, "_viewBuildCount"));
        source.AppendRange(Rows(101, 1)); host.Window.Content = chart; host.Window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        Range(chart, 81, 101);
    }

    [AvaloniaFact]
    public void Secondary_x_data_does_not_move_the_primary_follow_window()
    {
        var chart = Chart(Series(Buffer()));
        chart.Axes.Add(new NumericAxis { Key = "secondary", Minimum = 0, Maximum = 20_000 });
        chart.Series.Add(new LineSeries { XAxisKey = "secondary", ItemsSource = new[] { new ChartPoint(10_000, 1), new ChartPoint(10_100, 2) } });
        using var host = new Host(chart); Range(chart, 79, 99);
    }

    [AvaloniaFact]
    public void Date_follow_width_uses_axis_data_units_and_log_follow_respects_valid_domain()
    {
        var start = (double)new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        const double day = 86_400_000;
        var dates = new ChartDataBuffer<Row>(); dates.AppendRange(Rows(0, 100).Select(row => row with { X = start + row.X * day }));
        var chart = Chart(Series(dates)); chart.XAxis = new DateTimeAxis { Minimum = start, Maximum = start + 365 * day };
        chart.FollowLatestWindow = 7 * day; chart.FollowLatestData = true;
        using (var host = new Host(chart)) Range(chart, start + 92 * day, start + 99 * day);
        var positive = new ChartDataBuffer<Row>(); positive.AppendRange(Rows(1, 100));
        var log = Chart(Series(positive)); log.XAxis = new LogarithmicAxis { Minimum = 1, Maximum = 200 };
        log.FollowLatestData = true;
        using var logHost = new Host(log); Range(log, 80, 100);
    }

    [AvaloniaFact]
    public void Unknown_and_categorical_axes_keep_their_existing_range_contract()
    {
        foreach (ChartAxis axis in new ChartAxis[] { new ExternalAxis { Minimum = 0, Maximum = 200 }, new CategoryAxis() })
        {
            var chart = Chart(Series(Buffer())); chart.FollowLatestData = false; chart.XAxis = axis;
            using var host = new Host(chart); var normal = chart.VisibleRange;
            chart.FollowLatestData = true; Assert.Equal(normal, chart.VisibleRange);
        }
    }

    [AvaloniaFact]
    public void Real_input_transaction_pauses_follow_and_business_reentrant_viewport_wins()
    {
        var chart = Chart(Series(Buffer())); using var host = new Host(chart);
        var plot = chart.PlotArea; var scene = Scene(chart);
        Assert.True(Transform(chart, scene, plot.Center, plot.Center + new Vector(40, 0)));
        Assert.False(chart.FollowLatestData); Assert.NotNull(chart.Viewport);
        chart.FollowLatestData = true;
        var business = new ChartViewport(10, 20, -1, 10);
        chart.PropertyChanged += (_, change) =>
        {
            if (change.Property == CartesianChart.FollowLatestDataProperty && !chart.FollowLatestData) chart.Viewport = business;
        };
        scene = Scene(chart); plot = chart.PlotArea;
        Assert.False(Transform(chart, scene, plot.Center, plot.Center + new Vector(40, 0)));
        Assert.Equal(business, chart.Viewport); Assert.Equal(business, chart.VisibleRange);
    }

    private static ChartDataBuffer<Row> Buffer()
    { var result = new ChartDataBuffer<Row>(100); result.AppendRange(Rows(0, 100)); return result; }
    private static Row[] Rows(int start, int count) => Enumerable.Range(start, count).Select(i => new Row($"row-{i}", i, i % 5)).ToArray();
    private static LineSeries Series(ChartDataBuffer<Row> source) => new()
    { ItemsSource = source, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y), IdPath = nameof(Row.Id), MarkerSize = 0 };
    private static CartesianChart Chart(ChartSeries series)
    {
        var result = new CartesianChart { Width = 720, Height = 360, ShowLegend = false, FollowLatestData = true,
            FollowLatestWindow = 20, XAxis = new NumericAxis { Minimum = 0, Maximum = 200 }, YAxis = new NumericAxis { Minimum = -1, Maximum = 10 } };
        result.Series.Add(series); return result;
    }
    private static void Range(CartesianChart chart, double minimum, double maximum)
    { Assert.Equal(minimum, chart.VisibleRange!.Value.XMinimum, 8); Assert.Equal(maximum, chart.VisibleRange!.Value.XMaximum, 8); }
    private static object Scene(CartesianChart chart) => typeof(CartesianChart).GetMethod("GetScene", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(chart, null)!;
    private static bool Transform(CartesianChart chart, object scene, Point start, Point end) =>
        (bool)typeof(CartesianChart).GetMethod("TryApplyInputTransform", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(chart, [scene, start, end, 1d, ChartInteractionAxes.X])!;
    private static long Counter(CartesianChart chart, string name) => (long)typeof(CartesianChart).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
    private sealed record Row(string Id, double X, double Y);
    private sealed class ExternalAxis : ChartAxis;
    private sealed class Host : IDisposable
    {
        internal Window Window { get; }
        internal Host(CartesianChart chart)
        { Window = new Window { Width = 720, Height = 360, Content = chart }; Window.Show(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        public void Dispose() => Window.Close();
    }
}
