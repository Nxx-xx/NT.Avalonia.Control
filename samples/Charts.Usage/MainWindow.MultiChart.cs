using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal List<object> MultiChartBindingDiagnostics { get; } = [];
    private CartesianChart[] MultiCharts => [this.FindControl<CartesianChart>("UsageMultiChartA")!, this.FindControl<CartesianChart>("UsageMultiChartB")!];
    private async void RefreshMultiCharts(object? sender, RoutedEventArgs args)
    {
        await Task.WhenAll(MultiCharts.Select(chart => chart.PrepareBackgroundLayoutAsync()));
        foreach (var chart in MultiCharts) chart.RequestRender();
        UpdateMultiChartStatus();
    }
    private void UpdateMultiChartStatus()
    {
        var budget = ChartWorkScheduler.GetBudgetInfo(); var info = MultiCharts.Select(chart => chart.GetBackgroundLayoutInfo()).ToArray();
        Model.MultiChartStatus = $"版本 {Model.MultiChartRevision} · 发布 {string.Join(" / ", info.Select(item => item.CompletedCount))} · 取消 {info.Sum(item => item.CanceledCount)} / 过期 {info.Sum(item => item.StaleCount)} · 进程任务 {budget.ReservedBackgroundJobs}/{budget.MaximumBackgroundJobs} · 估算缓存 {budget.EstimatedResourceCacheBytes:N0}/{budget.ResourceCacheBudgetBytes:N0} B（不含原生/GPU与共享索引）";
    }
    internal async Task<Dictionary<string, bool>> VerifyMultiChartBindingsAsync()
    {
        var checks = new Dictionary<string, bool>(); void Check(string name, bool value) => checks["multichart-compiled-" + name] = value;
        var expander = this.FindControl<Expander>("MultiChartExamples")!; expander.IsExpanded = true; FlushBindings();
        var charts = MultiCharts; var first = await Task.WhenAll(charts.Select(chart => chart.PrepareBackgroundLayoutAsync())); FlushBindings();
        Check("background-styled-property-and-original-source-bound", charts.All(chart => chart.UseBackgroundLayout && ReferenceEquals(chart.Series[0].ItemsSource, Model.MultiChartRecords)));
        Check("both-charts-attached-active-and-layout-published", charts.All(chart => chart.GetWorkSchedulingInfo().IsActive && chart.GetBackgroundLayoutInfo() is { IsBusy: false, CompletedCount: > 0, FailedCount: 0 }));
        Check("worker-distinct-from-ui-thread", charts.All(chart => chart.GetBackgroundLayoutInfo().LastWorkerThreadId > 0 && chart.GetBackgroundLayoutInfo().LastWorkerThreadId != Environment.CurrentManagedThreadId));
        var before = charts.Select(chart => chart.GetWorkSchedulingInfo()).ToArray();
        for (var q = 0; q < 10; q++) foreach (var chart in charts) chart.RequestRender();
        var pending = charts.Select(chart => chart.GetWorkSchedulingInfo()).ToArray();
        Check("refresh-coalesces-independently-for-two-charts", pending.Select((item, i) => item.RefreshPending && item.RefreshRequests >= before[i].RefreshRequests + 10 && item.CoalescedRefreshes >= before[i].CoalescedRefreshes + 9).All(value => value));
        await Task.Delay(25); FlushBindings();
        Check("both-charts-receive-refresh-dispatch", charts.Select((chart, i) => chart.GetWorkSchedulingInfo().RefreshDispatches > before[i].RefreshDispatches).All(value => value));
        charts[0].SetCurrentValue(CartesianChart.UseBackgroundLayoutProperty, false); FlushBindings();
        Check("background-option-is-two-way-compiled-binding", !Model.MultiChartBackground && charts.All(chart => !chart.UseBackgroundLayout));
        Model.MultiChartBackground = true; FlushBindings();
        Check("model-restores-background-on-both-charts", charts.All(chart => chart.UseBackgroundLayout));
        var superseded = new List<Task<bool>>();
        for (var i = 0; i < 3; i++) { Model.MultiChartRetargetCommand.Execute(null); FlushBindings(); superseded.AddRange(charts.Select(chart => chart.PrepareBackgroundLayoutAsync())); }
        await Task.WhenAll(superseded); await Task.WhenAll(charts.Select(chart => chart.PrepareBackgroundLayoutAsync())); FlushBindings();
        Check("latest-command-records-bound-to-both-charts", Model.MultiChartRevision == 3 && charts.All(chart => ReferenceEquals(chart.Series[0].ItemsSource, Model.MultiChartRecords) && chart.Series[0].CreateSnapshot().Points.Select((point, q) => point.SourceIndex == q && ReferenceEquals(point.Item, Model.MultiChartRecords[q]) && Equals(point.StableId, $"usage-multi-v3-{q}") && point.Point.X == q && point.Point.Y == 29 + q % 31).All(value => value)));
        Check("latest-layout-settles-without-failure", charts.All(chart => chart.GetBackgroundLayoutInfo() is { IsBusy: false, FailedCount: 0, PublishedVertexCount: > 0 }));
        var budget = ChartWorkScheduler.GetBudgetInfo();
        Check("process-model-exposes-bounded-cache-and-tasks", budget.EstimatedResourceCacheBytes <= budget.ResourceCacheBudgetBytes && budget.EstimatedBackgroundPayloadBytes <= budget.BackgroundPayloadBudgetBytes && budget.ReservedBackgroundJobs <= budget.MaximumBackgroundJobs && !budget.NativeMemoryIncluded && !budget.SharedIndexCachesIncluded);
        UpdateMultiChartStatus(); FlushBindings(); Check("status-is-compiled-bound", this.FindControl<TextBlock>("UsageMultiChartSummary")!.Text == Model.MultiChartStatus);
        expander.IsExpanded = false; FlushBindings();
        Check("collapsed-ancestor-deactivates-both-charts", charts.All(chart => !chart.GetWorkSchedulingInfo().IsActive));
        var hidden = await Task.WhenAll(charts.Select(chart => chart.PrepareBackgroundLayoutAsync()));
        Check("collapsed-layout-request-cannot-publish", hidden.All(value => !value) && charts.All(chart => !chart.GetBackgroundLayoutInfo().IsBusy));
        MultiChartBindingDiagnostics.Add(new { first, before, pending, budget, hidden, charts = charts.Select(chart => new { scheduling = chart.GetWorkSchedulingInfo(), layout = chart.GetBackgroundLayoutInfo() }).ToArray(), revision = Model.MultiChartRevision, physicalInputValidated = false, performanceMeasured = false });
        return checks;
    }
}
