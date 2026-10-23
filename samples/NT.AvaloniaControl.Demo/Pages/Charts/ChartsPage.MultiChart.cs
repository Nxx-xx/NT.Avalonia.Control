using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private MultiChartBoard? _multiChartBoard;
    private Control CreateMultiChartExamples() => new Expander
    {
        Name = "ChartMultiChartExamples", Header = "多图与后台布局：公平刷新、取消与进程预算",
        HorizontalAlignment = HorizontalAlignment.Stretch, Content = (_multiChartBoard = CreateMultiChartBoard()).View,
    };

    private MultiChartBoard CreateMultiChartBoard()
    {
        var grid = new Grid { ColumnDefinitions = new("*,*,*"), RowDefinitions = new("Auto,Auto"), ColumnSpacing = 10, RowSpacing = 10 };
        var charts = new CartesianChart[6];
        string[] colors = ["#4385F4", "#19A798", "#F2A33A", "#9A60D0", "#D64C82", "#427B81"];
        for (var i = 0; i < charts.Length; i++)
        {
            var chart = charts[i] = new CartesianChart
            {
                Name = "MultiChart" + i, Title = $"通道 {i + 1} · 后台平滑布局", Height = 245, ShowLegend = false,
                AnimationTriggers = ChartAnimationTriggers.None, UseBackgroundLayout = true, IsTooltipEnabled = false,
                IsZoomEnabled = false, IsPanEnabled = false, ResourceCacheBudgetBytes = 256 * 1024,
                XAxis = new NumericAxis { Minimum = 0, Maximum = 511 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
            };
            chart.Series.Add(new LineSeries
            {
                Title = "原始记录", ItemsSource = MultiChartRows(i, 0), CategoryPath = "X", ValuePath = "Y", IdPath = "Id",
                Interpolation = ChartLineInterpolation.Smooth, Stroke = Brush(colors[i]), StrokeThickness = 2.5,
                ShowMarkers = false, MarkerSize = 0, SamplingMode = ChartSamplingMode.None,
            });
            Grid.SetColumn(chart, i % 3); Grid.SetRow(chart, i / 3); grid.Children.Add(chart);
        }
        var status = new TextBlock { Name = "MultiChartStatus", TextWrapping = TextWrapping.Wrap, MinHeight = 75 };
        var view = new StackPanel { Spacing = 10 }; var actions = new WrapPanel(); var board = new MultiChartBoard(view, grid, charts, status);
        AddAction(actions, "六图同时刷新", () => _ = board.RefreshAsync(), "六张图独立排队，重复请求会合并；数字布局在后台处理，UI线程发布当前结果。");
        AddAction(actions, "快速切换数据", () => _ = board.RetargetAsync(), "连续替换三个版本，旧布局取消或丢弃；只发布最新版本。");
        AddAction(actions, "隐藏 / 显示图板", board.ToggleVisibility, "隐藏图板暂停新增布局和调度；恢复后重新请求当前版本。");
        AddAction(actions, "卸载 / 挂回图板", board.ToggleAttachment, "从视觉树移除图板，取消未完成工作并释放本地缓存；挂回可重建。");
        AddAction(actions, "刷新状态", board.RefreshStatus, "查看公开调度、后台布局和资源统计。");
        AddAction(actions, "进程缓存1MiB", () => board.SetProcessBudget(1024 * 1024, 8 * 1024 * 1024, 8), "调整所有图共享的五层估算缓存与数字任务预算，关闭本节时恢复原值。");
        AddAction(actions, "恢复进程预算", board.RestoreProcessBudget, "恢复打开本节时的进程预算，不修改调用方业务数据。");
        view.Children.Add(new TextBlock { Text = "六张平滑折线共享进程内公平队列和资源预算。后台仅处理捕获后的数字，不读取业务对象或绘制资源；支持的结果回到UI线程提交。隐藏、卸载或更新版本会停止旧工作。", TextWrapping = TextWrapping.Wrap });
        view.Children.Add(actions); view.Children.Add(status); view.Children.Add(grid); board.RefreshStatus();
        return board;
    }

    private static MultiChartRow[] MultiChartRows(int channel, int revision) => Enumerable.Range(0, 512)
        .Select(q => new MultiChartRow($"multi-{channel}-v{revision}-{q}", q, 15 + channel * 3 + q % 37 * .8 + revision * 4)).ToArray();
    private sealed record MultiChartRow(string Id, double X, double Y);

    private sealed class MultiChartBoard(StackPanel view, Grid grid, CartesianChart[] charts, TextBlock status) : IDisposable
    {
        public StackPanel View { get; } = view;
        public Grid Grid { get; } = grid;
        public CartesianChart[] Charts { get; } = charts;
        public TextBlock Status { get; } = status;
        public int Revision { get; private set; }
        private readonly long _originalCacheBudget = ChartWorkScheduler.ResourceCacheBudgetBytes;
        private readonly long _originalPayloadBudget = ChartWorkScheduler.BackgroundPayloadBudgetBytes;
        private readonly int _originalMaximumJobs = ChartWorkScheduler.MaximumBackgroundJobs;
        public void SetProcessBudget(long cache, long payload, int jobs) { ChartWorkScheduler.ResourceCacheBudgetBytes = cache; ChartWorkScheduler.BackgroundPayloadBudgetBytes = payload; ChartWorkScheduler.MaximumBackgroundJobs = jobs; RefreshStatus(); }
        public void RestoreProcessBudget() => SetProcessBudget(_originalCacheBudget, _originalPayloadBudget, _originalMaximumJobs);
        public async Task RefreshAsync()
        {
            await Task.WhenAll(Charts.Select(chart => chart.PrepareBackgroundLayoutAsync()));
            foreach (var chart in Charts) chart.RequestRender();
            RefreshStatus();
        }
        public async Task RetargetAsync()
        {
            var pending = new List<Task<bool>>();
            for (var n = 0; n < 3; n++)
            {
                Revision++;
                for (var i = 0; i < Charts.Length; i++) { Charts[i].Series[0].ItemsSource = MultiChartRows(i, Revision); pending.Add(Charts[i].PrepareBackgroundLayoutAsync()); }
            }
            await Task.WhenAll(pending); await RefreshAsync();
        }
        public void ToggleVisibility() { Grid.IsVisible = !Grid.IsVisible; if (Grid.IsVisible) _ = RefreshAsync(); else RefreshStatus(); }
        public void ToggleAttachment() { if (View.Children.Contains(Grid)) View.Children.Remove(Grid); else { View.Children.Add(Grid); _ = RefreshAsync(); } RefreshStatus(); }
        public void RefreshStatus()
        {
            var info = Charts.Select(chart => chart.GetBackgroundLayoutInfo()).ToArray();
            var budget = ChartWorkScheduler.GetBudgetInfo();
            Status.Text = $"数据版本 {Revision} · 图板{(View.Children.Contains(Grid) ? Grid.IsVisible ? "可见" : "隐藏" : "已卸载")} · 后台忙碌 {info.Count(item => item.IsBusy)}/6\n" +
                $"提交 {info.Sum(item => item.SubmittedCount)} · 发布 {info.Sum(item => item.CompletedCount)} · 取消 {info.Sum(item => item.CanceledCount)} · 过期 {info.Sum(item => item.StaleCount)} · 失败 {info.Sum(item => item.FailedCount)}\n" +
                "每图发布：" + string.Join(" / ", info.Select(item => item.CompletedCount)) + $"\n进程估算缓存 {budget.EstimatedResourceCacheBytes:N0}/{budget.ResourceCacheBudgetBytes:N0} B · 数字任务 {budget.ReservedBackgroundJobs}/{budget.MaximumBackgroundJobs} · 数字载荷 {budget.EstimatedBackgroundPayloadBytes:N0}/{budget.BackgroundPayloadBudgetBytes:N0} B；不含共享索引、原生或GPU资源。";
        }
        public void Dispose() { foreach (var chart in Charts) { chart.CancelBackgroundLayout(); chart.TrimResourceCaches(); } RestoreProcessBudget(); }
    }
}
