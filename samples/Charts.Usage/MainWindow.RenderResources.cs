using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal List<object> RenderResourceBindingDiagnostics { get; } = [];
    private CartesianChart ResourceChart => this.FindControl<CartesianChart>("UsageRenderResourceChart")!;
    private void ConfigureRenderResourceBindings()
    {
        ResourceChart.PropertyChanged += (_, change) => { if (change.Property == CartesianChart.ResourceCacheBudgetBytesProperty) Dispatcher.UIThread.Post(RefreshResourceCacheStatus, DispatcherPriority.Background); };
        Closed += (_, _) => ResourceChart.TrimResourceCaches();
    }
    private void RefreshResourceCache(object? sender, RoutedEventArgs args) { WarmResourceChart(); RefreshResourceCacheStatus(); }
    private void TrimResourceCache(object? sender, RoutedEventArgs args) { ResourceChart.TrimResourceCaches(); RefreshResourceCacheStatus(); }
    private void RefreshResourceCacheStatus()
    {
        var info = ResourceChart.GetResourceCacheInfo();
        Model.ResourceCacheSummary = $"估算保留 {info.EstimatedBytes:N0}/{info.BudgetBytes:N0} B · 绘图 {info.SeriesDrawingCount} / 轴文字 {info.AxisTextCount} / 交互文字 {info.InteractionTextCount} / 画笔 {info.AxisPenCount} / 线段 {info.SegmentGeometryCount}\n" +
            $"命中 {info.CacheHits} / 未命中 {info.CacheMisses} · 淘汰 {info.EvictionCount} / 绕过 {info.BypassCount} / 清理 {info.TrimCount} · 模型 {info.ModelVersion}；未计原生/GPU和共享数据索引。";
        Model.NotifyRenderResourcePages();
    }
    private void WarmResourceChart() { FlushBindings(); var drawing = new DrawingGroup(); using var context = drawing.Open(); ResourceChart.Render(context); }
    internal void VerifyRenderResourceBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("RenderResourceExamples")!; expander.IsExpanded = true; FlushBindings(); WarmResourceChart();
        var chart = ResourceChart; var source = Model.ResourceRecords; var rows = source.ToArray(); var series = chart.Series.Single(); var old = series.CreateSnapshot();
        void Check(string name, bool value) => checks["resource-compiled-" + name] = value;
        Check("budget-source-and-mutable-stroke-bindings", chart.ResourceCacheBudgetBytes == 128 * 1024 && ReferenceEquals(series.ItemsSource, source) && ReferenceEquals(series.Stroke, Model.ResourceStroke));
        Check("intentional-fragmented-original-source", source.Count == 64 && source.StoragePageCount == 64 && rows.Select((row, q) => row.Id == "usage-resource-" + q && row.X == q && row.Y == 20 + q % 16).All(value => value));
        var warm = chart.GetResourceCacheInfo();
        Check("public-accounted-model-and-exclusions", warm.EstimatedBytes > 0 && warm.EstimatedBytes <= warm.BudgetBytes && warm.ModelVersion == 1 && !warm.NativeMemoryIncluded && !warm.SharedIndexCachesIncluded);
        Check("five-layer-byte-total", warm.EstimatedBytes == warm.SeriesDrawingBytes + warm.AxisTextBytes + warm.InteractionTextBytes + warm.AxisPenBytes + warm.SegmentGeometryBytes);
        WarmResourceChart(); var repeated = chart.GetResourceCacheInfo();
        Check("unchanged-render-reuses-resources", repeated.CacheHits > warm.CacheHits);
        Model.ResourceSmallBudgetCommand.Execute(null); FlushBindings(); WarmResourceChart(); var small = chart.GetResourceCacheInfo();
        Check("small-budget-command-and-styled-binding", chart.ResourceCacheBudgetBytes == 4096 && small.EstimatedBytes <= 4096);
        chart.SetCurrentValue(CartesianChart.ResourceCacheBudgetBytesProperty, 8192L); FlushBindings();
        Check("budget-is-two-way-bound", Model.ResourceBudgetBytes == 8192);
        Model.ResourceDisableCacheCommand.Execute(null); FlushBindings(); WarmResourceChart(); var disabled = chart.GetResourceCacheInfo();
        Check("zero-budget-command-disables-accounted-cache", chart.ResourceCacheBudgetBytes == 0 && disabled.EstimatedBytes == 0 && disabled.SeriesDrawingCount + disabled.AxisTextCount + disabled.InteractionTextCount + disabled.AxisPenCount + disabled.SegmentGeometryCount == 0);
        Model.ResourceNormalBudgetCommand.Execute(null); FlushBindings(); WarmResourceChart(); var restored = chart.GetResourceCacheInfo();
        Check("restored-budget-rebuilds-within-limit", restored.EstimatedBytes > 0 && restored.EstimatedBytes <= restored.BudgetBytes);
        chart.TrimResourceCaches(); var trimmed = chart.GetResourceCacheInfo();
        Check("explicit-trim-releases-all-accounted-bytes", trimmed.EstimatedBytes == 0 && trimmed.TrimCount > restored.TrimCount);
        WarmResourceChart(); Check("trim-can-rebuild-for-next-render", chart.GetResourceCacheInfo().EstimatedBytes > 0);
        const int q = 31; var range = chart.VisibleRange!.Value; var plot = chart.PlotArea; var point = new Point(plot.Left + (rows[q].X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width, plot.Bottom - (rows[q].Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
        var hit = chart.HitTestPoint(point, .001); chart.SetCurrentValue(CartesianChart.SelectedPointProperty, hit); FlushBindings();
        Check("selection-preserves-business-record-and-binding", hit is { Index: q } && ReferenceEquals(hit.Item, rows[q]) && Equals(hit.StableId, rows[q].Id) && ReferenceEquals(Model.ResourceSelectedPoint?.Item, rows[q]));
        var beforeVersion = source.Version; Model.ResourceCompactCommand.Execute(null); FlushBindings();
        var current = series.CreateSnapshot();
        Check("explicit-compact-command-packs-and-publishes-once", source.StoragePageCount == 1 && source.Version == beforeVersion + 1);
        Check("compact-keeps-current-original-id-item-and-index", current.Points.Count == 64 && current.Points.Select((item, i) => item.SourceIndex == i && ReferenceEquals(item.Item, rows[i]) && Equals(item.StableId, rows[i].Id) && item.Point == new ChartPoint(rows[i].X, rows[i].Y)).All(value => value));
        Check("old-snapshot-survives-compact", old.Points.Count == 64 && old.Points.Select((item, i) => item.SourceIndex == i && ReferenceEquals(item.Item, rows[i]) && Equals(item.StableId, rows[i].Id) && item.Point == new ChartPoint(rows[i].X, rows[i].Y)).All(value => value));
        Check("compact-selection-still-original", chart.SelectedPoint is { Index: q } selected && ReferenceEquals(selected.Item, rows[q]) && Equals(selected.StableId, rows[q].Id));
        var packedVersion = source.Version; Check("already-packed-compact-no-op", !source.Compact() && source.Version == packedVersion);
        var brush = Model.ResourceStroke; Model.ResourceBrushCommand.Execute(null); FlushBindings(); WarmResourceChart();
        Check("same-mutable-brush-instance-can-change", ReferenceEquals(series.Stroke, brush) && brush.Color == Color.Parse("#9A60D0"));
        RefreshResourceCacheStatus(); FlushBindings();
        Check("diagnostic-text-is-compiled-bound", this.FindControl<TextBlock>("UsageResourceCacheSummary")!.Text == Model.ResourceCacheSummary && this.FindControl<TextBlock>("UsageResourcePageSummary")!.Text == Model.ResourcePageSummary && Model.ResourcePageSummary.Contains("当前数据页 1", StringComparison.Ordinal));
        RenderResourceBindingDiagnostics.Add(new { warm, repeated, small, disabled, restored, trimmed, current = chart.GetResourceCacheInfo(), beforePages = 64, afterPages = source.StoragePageCount, beforeVersion, afterVersion = source.Version, oldSnapshotCount = old.Points.Count, hitIndex = hit?.Index, hitId = hit?.StableId, physicalInputValidated = false, totalMemoryQualified = false });
        chart.ClearSelection(); expander.IsExpanded = false; FlushBindings();
    }
}