using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class MultiChartSchedulerTests
{
    [AvaloniaFact]
    public void Logical_process_budgets_and_scope_are_explicit_and_invalid_values_are_rejected()
    {
        var info = ChartWorkScheduler.GetBudgetInfo();
        Assert.Equal(512L * 1024 * 1024, info.ResourceCacheBudgetBytes);
        Assert.Equal(64L * 1024 * 1024, info.BackgroundPayloadBudgetBytes);
        Assert.Equal(16, info.MaximumBackgroundJobs); Assert.Equal(2, info.MaximumBackgroundWorkers);
        Assert.False(info.NativeMemoryIncluded); Assert.False(info.SharedIndexCachesIncluded);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartWorkScheduler.ResourceCacheBudgetBytes = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartWorkScheduler.BackgroundPayloadBudgetBytes = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartWorkScheduler.MaximumBackgroundJobs = -1);
    }

    [AvaloniaFact]
    public void Repeated_requests_coalesce_and_each_queued_chart_receives_a_turn()
    {
        var charts = Enumerable.Range(0, 6).Select(_ => MakeChart()).ToArray();
        var panel = new StackPanel(); foreach (var chart in charts) panel.Children.Add(chart);
        using var host = new TemplateFeaturesHost(charts[0], panel);
        var before = charts.Select(chart => chart.GetWorkSchedulingInfo()).ToArray();
        for (var index = 0; index < 1000; index++) Request(charts[0]);
        foreach (var chart in charts.Skip(1)) Request(chart);
        Assert.All(charts, chart => Assert.True(chart.GetWorkSchedulingInfo().RefreshPending));
        Assert.Equal(6, ChartWorkScheduler.GetBudgetInfo().PendingRefreshCount);
        Dispatcher.UIThread.RunJobs();
        for (var index = 0; index < charts.Length; index++)
        {
            var after = charts[index].GetWorkSchedulingInfo();
            Assert.Equal(before[index].RefreshDispatches + 1, after.RefreshDispatches);
            Assert.False(after.RefreshPending);
        }
        Assert.Equal(before[0].CoalescedRefreshes + 999, charts[0].GetWorkSchedulingInfo().CoalescedRefreshes);
    }

    [AvaloniaFact]
    public void Reentrant_storm_requests_join_behind_already_waiting_charts()
    {
        var charts = Enumerable.Range(0, 3).Select(_ => MakeChart()).ToArray();
        var panel = new StackPanel(); foreach (var chart in charts) panel.Children.Add(chart);
        using var host = new TemplateFeaturesHost(charts[0], panel);
        var before = charts.Select(chart => chart.GetWorkSchedulingInfo().RefreshDispatches).ToArray();
        foreach (var chart in charts) chart.RequestRender();
        var dispatch = typeof(ChartWorkScheduler).GetMethod("DispatchRefresh", BindingFlags.Static | BindingFlags.NonPublic)!;
        dispatch.Invoke(null, null);
        for (var index = 0; index < 1000; index++) charts[0].RequestRender();
        dispatch.Invoke(null, null);
        Assert.Equal(before[0] + 1, charts[0].GetWorkSchedulingInfo().RefreshDispatches);
        Assert.Equal(before[1] + 1, charts[1].GetWorkSchedulingInfo().RefreshDispatches);
        Assert.Equal(before[2], charts[2].GetWorkSchedulingInfo().RefreshDispatches);
        dispatch.Invoke(null, null);
        Assert.Equal(before[2] + 1, charts[2].GetWorkSchedulingInfo().RefreshDispatches);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(before[0] + 2, charts[0].GetWorkSchedulingInfo().RefreshDispatches);
    }

    [AvaloniaFact]
    public void Worker_request_storm_uses_one_posted_UI_handoff()
    {
        var chart = MakeChart(); using var host = new TemplateFeaturesHost(chart);
        var before = chart.GetWorkSchedulingInfo();
        var worker = new Thread(() => { for (var index = 0; index < 10_000; index++) chart.RequestRender(); });
        worker.Start(); Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
        Assert.Equal(before.RefreshRequests, chart.GetWorkSchedulingInfo().RefreshRequests);
        Dispatcher.UIThread.RunJobs();
        var after = chart.GetWorkSchedulingInfo();
        Assert.Equal(before.RefreshRequests + 1, after.RefreshRequests);
        Assert.Equal(before.RefreshDispatches + 1, after.RefreshDispatches);
    }

    [AvaloniaFact]
    public void Process_budget_retires_actual_oldest_entries_across_charts_and_shrinks_immediately()
    {
        var original = ChartWorkScheduler.ResourceCacheBudgetBytes;
        var first = MakeChart(); var second = MakeChart(); var third = MakeChart();
        using var host1 = new TemplateFeaturesHost(first); using var host2 = new TemplateFeaturesHost(second);
        using var host3 = new TemplateFeaturesHost(third);
        try
        {
            ChartWorkScheduler.TrimResourceCaches(); ChartWorkScheduler.ResourceCacheBudgetBytes = 2200;
            var one = Text(first, "first"); _ = Text(second, "second");
            Assert.Same(one, Text(first, "first"));
            _ = Text(third, "third");
            Assert.Equal(1, first.GetResourceCacheInfo().InteractionTextCount);
            Assert.Equal(0, second.GetResourceCacheInfo().InteractionTextCount);
            Assert.Equal(1, third.GetResourceCacheInfo().InteractionTextCount);
            Assert.Equal(first.GetResourceCacheInfo().EstimatedBytes + third.GetResourceCacheInfo().EstimatedBytes,
                ChartWorkScheduler.GetBudgetInfo().EstimatedResourceCacheBytes);
            ChartWorkScheduler.ResourceCacheBudgetBytes = 0;
            Assert.Equal(0, ChartWorkScheduler.GetBudgetInfo().EstimatedResourceCacheBytes);
            Assert.All(new[] { first, second, third }, chart => Assert.Equal(0, chart.GetResourceCacheInfo().EstimatedBytes));
            _ = Text(first, "uncached"); Assert.Equal(0, first.GetResourceCacheInfo().InteractionTextCount);
        }
        finally { ChartWorkScheduler.ResourceCacheBudgetBytes = original; }
    }

    [AvaloniaFact]
    public void Ancestor_hiding_cancels_refreshes_and_releases_resources_then_returns_to_latest_data()
    {
        var values = new ObservableCollection<ChartPoint> { new(0, 1), new(1, 2) };
        var chart = MakeChart(); chart.Series[0].ItemsSource = values;
        var parent = new Border { Child = chart }; using var host = new TemplateFeaturesHost(chart, parent);
        Draw(chart); Assert.True(chart.GetResourceCacheInfo().EstimatedBytes > 0);
        var before = chart.GetWorkSchedulingInfo(); Request(chart); parent.IsVisible = false;
        Assert.False(chart.GetWorkSchedulingInfo().IsActive); Assert.False(chart.GetWorkSchedulingInfo().RefreshPending);
        Assert.Equal(0, chart.GetResourceCacheInfo().EstimatedBytes);
        var prepared = Counter(chart, "_preparedDataBuildCount");
        for (var index = 2; index < 50; index++) values.Add(new(index, index + 10));
        host.Drain();
        Assert.Equal(prepared, Counter(chart, "_preparedDataBuildCount"));
        Assert.Equal(before.RefreshDispatches, chart.GetWorkSchedulingInfo().RefreshDispatches);
        parent.IsVisible = true; host.Drain(); Draw(chart);
        Assert.True(chart.GetWorkSchedulingInfo().IsActive);
        Assert.True(chart.GetWorkSchedulingInfo().RefreshDispatches > before.RefreshDispatches);
        Assert.Equal(50, chart.Series[0].CreateSnapshot().Count);
        Assert.True(chart.VisibleRange!.Value.YMaximum >= 59);
    }

    [AvaloniaFact]
    public void Hidden_deferred_commit_and_series_source_swap_do_not_remap_selected_rows()
    {
        var getterCalls = 0;
        var buffer = new ChartDataBuffer<GetterRow>();
        buffer.AppendRange([new(0, () => { getterCalls++; return 10; }), new(1, () => { getterCalls++; return 20; })]);
        var series = new LineSeries { ItemsSource = buffer, CategoryPath = nameof(GetterRow.X), ValuePath = nameof(GetterRow.Y), MarkerSize = 0 };
        var chart = MakeChart(); chart.Series.Clear(); chart.Series.Add(series);
        var parent = new Border { Child = chart }; using var host = new TemplateFeaturesHost(chart, parent);
        Draw(chart); chart.SelectedPoint = ChartPointHit.FromData(series, series.CreateSnapshot().Points[0], default);
        var scope = chart.DeferDataUpdates();
        buffer.AppendRange([new(2, () => { getterCalls++; return 30; })]);
        parent.IsVisible = false; var before = getterCalls;
        scope.Dispose(); Assert.Equal(before, getterCalls);
        var mappedBefore = ChartSeries.BufferedMappedRowCount;
        buffer.AppendRange([new(3, () => { getterCalls++; return 40; })]);
        // Maintaining the series' nested-property observer ledger reads the new item's leaf once.
        // The chart must not add a snapshot mapping/read during the hidden source change or deferred commit.
        var afterObserverUpdate = getterCalls;
        chart.SeriesSource = new[] { series }; host.Drain();
        Assert.Equal(afterObserverUpdate, getterCalls); Assert.Equal(mappedBefore, ChartSeries.BufferedMappedRowCount);
        Assert.NotNull(chart.SelectedPoint);
        parent.IsVisible = true; host.Drain(); Draw(chart);
        Assert.True(getterCalls > afterObserverUpdate); Assert.Equal(4, series.CreateSnapshot().Count);
        Assert.NotNull(chart.SelectedPoint); Assert.Equal(0, chart.SelectedPoint.Index);
        Assert.Equal(10, chart.SelectedPoint.Point.Y); Assert.True(chart.VisibleRange!.Value.YMaximum >= 40);
    }

    [AvaloniaFact]
    public void Detached_charts_leave_no_refresh_or_resource_registration_and_can_attach_again()
    {
        var chart = MakeChart(); using var host = new TemplateFeaturesHost(chart); Draw(chart);
        Request(chart); var shell = host.Window.Content; host.Window.Content = null;
        Assert.False(chart.GetWorkSchedulingInfo().IsRegistered); Assert.False(chart.GetWorkSchedulingInfo().IsActive);
        Assert.False(chart.GetWorkSchedulingInfo().RefreshPending); Assert.Equal(0, chart.GetResourceCacheInfo().EstimatedBytes);
        host.Window.Content = shell; host.Drain(); Draw(chart);
        Assert.True(chart.GetWorkSchedulingInfo().IsRegistered); Assert.True(chart.GetWorkSchedulingInfo().IsActive);
        Assert.True(chart.GetResourceCacheInfo().EstimatedBytes > 0);
    }

    [AvaloniaFact]
    public void Reservation_rejection_precedes_numeric_capture_and_counts_reserved_unstarted_payloads()
    {
        var originalBytes = ChartWorkScheduler.BackgroundPayloadBudgetBytes;
        var originalJobs = ChartWorkScheduler.MaximumBackgroundJobs;
        var chart = MakeChart(); var second = MakeChart(); using var host = new TemplateFeaturesHost(chart);
        using var secondHost = new TemplateFeaturesHost(second);
        try
        {
            ChartWorkScheduler.BackgroundPayloadBudgetBytes = 100; ChartWorkScheduler.MaximumBackgroundJobs = 1;
            Assert.True(ChartWorkScheduler.TryReserve(chart, 80, out var lease));
            using (lease)
            {
                Assert.Equal(80, ChartWorkScheduler.GetBudgetInfo().EstimatedBackgroundPayloadBytes);
                Assert.Equal(1, ChartWorkScheduler.GetBudgetInfo().ReservedBackgroundJobs);
                Assert.False(ChartWorkScheduler.TryReserve(second, 1, out _));
                Assert.False(ChartWorkScheduler.TryReserve(chart, 1, out _));
            }
            Assert.Equal(0, ChartWorkScheduler.GetBudgetInfo().EstimatedBackgroundPayloadBytes);
            ChartWorkScheduler.MaximumBackgroundJobs = 2;
            Assert.False(ChartWorkScheduler.TryReserve(second, 101, out _));
            ChartWorkScheduler.BackgroundPayloadBudgetBytes = 0;
            Assert.False(ChartWorkScheduler.TryReserve(second, 1, out _));
        }
        finally { ChartWorkScheduler.BackgroundPayloadBudgetBytes = originalBytes; ChartWorkScheduler.MaximumBackgroundJobs = originalJobs; }
    }

    [AvaloniaFact]
    public void At_most_two_workers_run_and_waiting_chart_starts_when_one_slot_is_released()
    {
        var charts = Enumerable.Range(0, 3).Select(_ => MakeChart()).ToArray();
        var panel = new StackPanel(); foreach (var chart in charts) panel.Children.Add(chart);
        using var host = new TemplateFeaturesHost(charts[0], panel);
        using var release = new ManualResetEventSlim(); using var started = new CountdownEvent(2);
        var leases = new List<ChartWorkLease>();
        try
        {
            foreach (var chart in charts) { Assert.True(ChartWorkScheduler.TryReserve(chart, 100, out var lease)); leases.Add(lease!); }
            var first = leases[0].RunAsync(_ => { started.Signal(); release.Wait(); return 11; });
            var second = leases[1].RunAsync(_ => { started.Signal(); release.Wait(); return 22; });
            var third = leases[2].RunAsync(_ => 33);
            Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
            var info = ChartWorkScheduler.GetBudgetInfo(); Assert.Equal(2, info.RunningBackgroundJobs); Assert.Equal(1, info.QueuedBackgroundJobs);
            Assert.False(third.IsCompleted); Assert.Equal(300, info.EstimatedBackgroundPayloadBytes);
            release.Set(); Drain(Task.WhenAll(first, second, third));
            Assert.Equal(11, first.Result); Assert.Equal(22, second.Result); Assert.Equal(33, third.Result);
            Assert.Equal(0, ChartWorkScheduler.GetBudgetInfo().RunningBackgroundJobs);
            Assert.Equal(300, ChartWorkScheduler.GetBudgetInfo().EstimatedBackgroundPayloadBytes);
        }
        finally { release.Set(); foreach (var lease in leases) lease.Dispose(); DrainWorkers(); }
        Assert.Equal(0, ChartWorkScheduler.GetBudgetInfo().EstimatedBackgroundPayloadBytes);
    }

    [AvaloniaFact]
    public void Cancelled_queued_work_drops_its_capture_without_running_a_third_worker()
    {
        var charts = Enumerable.Range(0, 3).Select(_ => MakeChart()).ToArray();
        var panel = new StackPanel(); foreach (var chart in charts) panel.Children.Add(chart);
        using var host = new TemplateFeaturesHost(charts[0], panel);
        using var release = new ManualResetEventSlim(); using var started = new CountdownEvent(2);
        var leases = new List<ChartWorkLease>(); var thirdRan = false;
        try
        {
            foreach (var chart in charts) { Assert.True(ChartWorkScheduler.TryReserve(chart, 10, out var lease)); leases.Add(lease!); }
            var first = leases[0].RunAsync(_ => { started.Signal(); release.Wait(); return 1; });
            var second = leases[1].RunAsync(_ => { started.Signal(); release.Wait(); return 2; });
            var third = leases[2].RunAsync(_ => { thirdRan = true; return 3; });
            Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
            leases[2].Dispose(); WaitUntil(() => third.IsCompleted);
            Assert.True(third.IsCanceled); Assert.False(thirdRan);
            Assert.Equal(2, ChartWorkScheduler.GetBudgetInfo().RunningBackgroundJobs);
            Assert.Equal(0, ChartWorkScheduler.GetBudgetInfo().QueuedBackgroundJobs);
            Assert.Equal(20, ChartWorkScheduler.GetBudgetInfo().EstimatedBackgroundPayloadBytes);
            release.Set(); Drain(Task.WhenAll(first, second)); Assert.False(thirdRan);
        }
        finally { release.Set(); foreach (var lease in leases) lease.Dispose(); DrainWorkers(); }
        Assert.Equal(0, ChartWorkScheduler.GetBudgetInfo().EstimatedBackgroundPayloadBytes);
    }

    [AvaloniaFact]
    public void Cancelled_noncooperative_worker_keeps_its_payload_charged_until_it_really_exits()
    {
        var chart = MakeChart(); using var host = new TemplateFeaturesHost(chart);
        using var started = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        Assert.True(ChartWorkScheduler.TryReserve(chart, 4096, out var lease));
        var task = lease!.RunAsync(_ => { started.Set(); release.Wait(); return 7; });
        try
        {
            Assert.True(started.Wait(TimeSpan.FromSeconds(10))); chart.IsVisible = false;
            Assert.True(lease.IsDisposed); WaitUntil(() => task.IsCompleted); Assert.True(task.IsCanceled);
            Assert.Equal(4096, ChartWorkScheduler.GetBudgetInfo().EstimatedBackgroundPayloadBytes);
            Assert.Equal(1, ChartWorkScheduler.GetBudgetInfo().RunningBackgroundJobs);
            Assert.False(ChartWorkScheduler.TryReserve(chart, 1, out _));
        }
        finally { release.Set(); lease.Dispose(); DrainWorkers(); }
        Assert.Equal(0, ChartWorkScheduler.GetBudgetInfo().EstimatedBackgroundPayloadBytes);
    }

    [AvaloniaFact]
    public void Faults_release_after_the_caller_disposes()
    {
        var chart = MakeChart(); using var host = new TemplateFeaturesHost(chart);
        Assert.True(ChartWorkScheduler.TryReserve(chart, 17, out var lease));
        var failures = ChartWorkScheduler.GetBudgetInfo().FailedBackgroundJobs;
        var task = lease!.RunAsync<int>(_ => throw new InvalidOperationException("numeric fixture"));
        try
        {
            WaitUntil(() => task.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
            Assert.Equal(failures + 1, ChartWorkScheduler.GetBudgetInfo().FailedBackgroundJobs);
            Assert.Equal(17, ChartWorkScheduler.GetBudgetInfo().EstimatedBackgroundPayloadBytes);
        }
        finally { lease.Dispose(); DrainWorkers(); }
        Assert.Equal(0, ChartWorkScheduler.GetBudgetInfo().EstimatedBackgroundPayloadBytes);
    }

    [AvaloniaFact]
    public async Task Process_configuration_diagnostics_and_chart_scheduling_reject_worker_access()
    {
        var chart = MakeChart();
        await Task.Run(() =>
        {
            Assert.Throws<InvalidOperationException>(() => ChartWorkScheduler.GetBudgetInfo());
            Assert.Throws<InvalidOperationException>(() => ChartWorkScheduler.ResourceCacheBudgetBytes = 1);
            Assert.Throws<InvalidOperationException>(() => ChartWorkScheduler.TrimResourceCaches());
            Assert.Throws<InvalidOperationException>(() => chart.GetWorkSchedulingInfo());
        });
    }

    private static CartesianChart MakeChart()
    {
        var chart = new CartesianChart { Width = 620, Height = 300, ShowLegend = false };
        chart.Series.Add(new LineSeries { ItemsSource = Enumerable.Range(0, 100).Select(index => new ChartPoint(index, index)).ToArray(), MarkerSize = 0 });
        return chart;
    }
    private sealed class GetterRow(double x, Func<double> getter) { public double X => x; public double Y => getter(); }
    private static void Draw(CartesianChart chart) { _ = chart.PlotArea; using var drawing = new DrawingGroup().Open(); chart.Render(drawing); }
    private static void Request(CartesianChart chart) => chart.RequestRender();
    private static object Text(CartesianChart chart, string text) => typeof(CartesianChart).GetMethod("InteractionText", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(chart, [text, false])!;
    private static long Counter(CartesianChart chart, string field) => (long)typeof(CartesianChart).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
    private static void Drain(Task task) { WaitUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void DrainWorkers() => WaitUntil(() => ChartWorkScheduler.GetBudgetInfo().RunningBackgroundJobs == 0);
    private static void WaitUntil(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            Dispatcher.UIThread.RunJobs();
            if (timer.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("Chart worker did not finish.");
            Thread.Sleep(1);
        }
        Dispatcher.UIThread.RunJobs();
    }
}
