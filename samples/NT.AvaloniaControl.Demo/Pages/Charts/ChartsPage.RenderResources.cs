using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private RenderResourceBoard? _renderResourceBoard;
    private Control CreateRenderResourceExamples() => new Expander
    {
        Name = "ChartRenderResourceExamples", Header = "绘图与资源预算：本地缓存、清理、可变画刷与分页压实",
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Content = (_renderResourceBoard = CreateRenderResourceBoard()).View,
    };

    private RenderResourceBoard CreateRenderResourceBoard()
    {
        var chart = new CartesianChart
        {
            Name = "RenderResourceChart", Height = 400, ShowLegend = true, IsZoomEnabled = false, IsPanEnabled = false,
            SelectionMode = ChartSelectionMode.Multiple, AnimationTriggers = ChartAnimationTriggers.None,
            ResourceCacheBudgetBytes = 128 * 1024,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 256, Title = "原始记录序号" },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "三路信号" },
        };
        var colors = new[] { "#4385F4", "#19A798", "#F2A33A" };
        var mutable = new SolidColorBrush(Color.Parse(colors[0]));
        var buffers = new ChartDataBuffer<RenderResourceRow>[3];
        for (var series = 0; series < 3; series++)
        {
            var source = buffers[series] = new(512);
            for (var q = 0; q < 256; q++) source.AppendRange(new[] { RenderResourceRecord(series, q) });
            chart.Series.Add(new LineSeries
            {
                Title = $"信号 {series + 1}", ItemsSource = source, CategoryPath = "X", ValuePath = "Y", IdPath = "Id",
                Stroke = series == 0 ? mutable : Brush(colors[series]), StrokeThickness = 2.5, ShowMarkers = false, MarkerSize = 0,
                SamplingMode = ChartSamplingMode.None,
            });
        }
        var status = new TextBlock { Name = "RenderResourceStatus", MinHeight = 100, TextWrapping = TextWrapping.Wrap };
        var actions = new WrapPanel(); var view = new StackPanel { Spacing = 10 };
        var board = new RenderResourceBoard(view, chart, buffers, mutable, status);
        AddAction(actions, "预算128KiB", () => board.SetBudget(128 * 1024), "允许保留本图受控绘图与文字缓存；数值是估算模型，不是原生或GPU硬上限。");
        AddAction(actions, "预算4KiB", () => board.SetBudget(4 * 1024), "降低本地缓存预算，观察淘汰或大对象绕过；图形仍应完整。");
        AddAction(actions, "禁用本地缓存", () => board.SetBudget(0), "设为0，受控本地缓存不保留对象，保留通用绘制实现。");
        AddAction(actions, "清理并重绘", board.Trim, "清理受控本地缓存；下一次绘制按当前预算重新建立。");
        AddAction(actions, "切换可变画刷", board.ToggleBrush, "修改同一SolidColorBrush实例的颜色，检查保留绘图没有重放旧颜色。");
        AddAction(actions, "压实数据页", board.Compact, "显式复制保留数据以压实碎片页，业务对象、坐标和ID保持；旧快照仍可用。");
        AddAction(actions, "刷新统计", board.Refresh, "统计只覆盖公开Info中列出的本地缓存，未计共享源索引和原生/GPU资源。");
        chart.SelectionChanged += (_, _) => board.Refresh();
        view.Children.Add(new TextBlock { Text = "预算控制本图绘图、轴文字、交互文字、画笔和线段几何的估算保留成本。降低预算、清理或禁用缓存后，原始数据和绘制结果应保持。数据页从单点批次故意生成碎片，可显式压实。", TextWrapping = TextWrapping.Wrap });
        view.Children.Add(actions); view.Children.Add(status); view.Children.Add(chart); board.Refresh();
        return board;
    }

    private static RenderResourceRow RenderResourceRecord(int series, int q) => new($"resource-{series}-sample-{q}", q, 10 + series * 28 + q % 32 * .8);
    private sealed record RenderResourceRow(string Id, double X, double Y);
    private sealed class RenderResourceBoard(StackPanel view, CartesianChart chart, ChartDataBuffer<RenderResourceRow>[] buffers, SolidColorBrush mutableBrush, TextBlock status) : IDisposable
    {
        public StackPanel View { get; } = view;
        public CartesianChart Chart { get; } = chart;
        public ChartDataBuffer<RenderResourceRow>[] Buffers { get; } = buffers;
        public SolidColorBrush MutableBrush { get; } = mutableBrush;
        public TextBlock Status { get; } = status;
        public void SetBudget(long bytes) { Chart.ResourceCacheBudgetBytes = bytes; Redraw(); }
        public void Trim() { Chart.TrimResourceCaches(); Redraw(); }
        public void ToggleBrush() { MutableBrush.Color = MutableBrush.Color == Color.Parse("#4385F4") ? Color.Parse("#9A60D0") : Color.Parse("#4385F4"); Redraw(); }
        public void Compact() { using (Chart.DeferDataUpdates()) foreach (var source in Buffers) source.Compact(); Redraw(); }
        private void Redraw() { Chart.InvalidateVisual(); Refresh(); Dispatcher.UIThread.Post(Refresh, DispatcherPriority.Background); }
        public void Refresh()
        {
            var info = Chart.GetResourceCacheInfo();
            Status.Text = $"预算 {info.BudgetBytes:N0} B · 估算保留 {info.EstimatedBytes:N0} B · 模型 {info.ModelVersion}\n" +
                $"绘图 {info.SeriesDrawingCount}/{info.SeriesDrawingBytes:N0} B · 轴文字 {info.AxisTextCount}/{info.AxisTextBytes:N0} B · 交互文字 {info.InteractionTextCount}/{info.InteractionTextBytes:N0} B · 画笔 {info.AxisPenCount}/{info.AxisPenBytes:N0} B · 线段 {info.SegmentGeometryCount}/{info.SegmentGeometryBytes:N0} B\n" +
                $"命中 {info.CacheHits} / 未命中 {info.CacheMisses} · 淘汰 {info.EvictionCount} · 绕过 {info.BypassCount} · 清理 {info.TrimCount} · 数据页 {string.Join(" / ", Buffers.Select(source => source.StoragePageCount))}\n" +
                (Chart.SelectedPoint is { } hit ? $"原始 {hit.StableId} · 当前索引 {hit.Index}。" : "悬停或选择查看原始业务记录。") + " 统计不含共享数据索引、原生和GPU内存。";
        }
        public void Dispose() => Chart.TrimResourceCaches();
    }
}