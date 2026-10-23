using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private IncrementalStreamingBoard? _incrementalStreamingBoard;
    private Control CreateIncrementalStreamingExamples() => new Expander
    {
        Name = "ChartIncrementalStreamingExamples", Header = "增量与实时调度：有限窗口、背压、暂停与跟随",
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Content = (_incrementalStreamingBoard = CreateIncrementalStreamingBoard()).View,
    };

    private IncrementalStreamingBoard CreateIncrementalStreamingBoard()
    {
        var chart = new CartesianChart
        {
            Name = "IncrementalStreamingChart", Height = 400, ShowLegend = true, SelectionMode = ChartSelectionMode.Multiple,
            IsZoomEnabled = true, IsPanEnabled = true, InteractionAxes = ChartInteractionAxes.X,
            FollowLatestData = true, FollowLatestWindow = 128,
            XAxis = new NumericAxis { Title = "绝对采样序号 · 拖动后暂停跟随" },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "四路原始信号" },
            TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) =>
            {
                var id = new TextBlock { Name = "StreamingTooltipId" }; id.Bind(TextBlock.TextProperty, new Binding("Primary.StableId"));
                var source = new TextBlock(); source.Bind(TextBlock.TextProperty, new Binding("Primary.Index") { StringFormat = "当前窗口源索引 {0}" });
                return new StackPanel { Children = { new TextBlock { Text = "原始实时记录" }, id, source } };
            }),
        };
        var buffers = Enumerable.Range(0, 4).Select(_ => new ChartDataBuffer<StreamingRow>(512)).ToArray();
        var colors = new[] { "#4385F4", "#19A798", "#F2A33A", "#D760A5" };
        for (var stream = 0; stream < 4; stream++)
        {
            buffers[stream].AppendRange(Enumerable.Range(0, 256).Select(i => StreamingRecord(stream, i)));
            chart.Series.Add(new LineSeries { Title = $"信号 {stream + 1}", ItemsSource = buffers[stream], CategoryPath = "X", ValuePath = "Y", IdPath = "Id",
                Stroke = Brush(colors[stream]), StrokeThickness = 2.5, ShowMarkers = false, MarkerSize = 0, SamplingMode = ChartSamplingMode.MinMax });
        }
        var status = new TextBlock { Name = "StreamingQueueStatus", TextWrapping = TextWrapping.Wrap, MinHeight = 44 };
        var identity = new TextBlock { Name = "StreamingIdentity", Text = "悬停或选择查看业务 ID。窗口淘汰改变当前源索引，原始业务对象与 ID 保持不变。", TextWrapping = TextWrapping.Wrap, MinHeight = 36 };
        var actions = new WrapPanel(); var view = new StackPanel { Spacing = 10 };
        var board = new IncrementalStreamingBoard(view, chart, buffers, status, identity);
        AddAction(actions, "开始后台四路数据", board.Start, "每批每路追加64条；每路最多512条，生产者等待有界队列，不在UI线程逐点追加。");
        AddAction(actions, "暂停提交", () => { board.Session.Pause(); board.Refresh(); }, "暂停UI提交，最多保留2批；满后异步生产者等待，不丢弃已接收的数据。");
        AddAction(actions, "恢复提交", () => { board.Session.Resume(); board.Refresh(); }, "继续提交已接收批次；跟随视口开关独立控制。");
        AddAction(actions, "停止生产", board.Stop, "取消后台生产者与尚未提交的队列；再次开始会创建新会话。");
        AddAction(actions, "追加一批", () => board.TryAppend(), "非阻塞尝试，队列满时明确拒绝并计数。");
        AddAction(actions, "查看完整窗口", () => { chart.ResetViewport(); board.Refresh(); }, "暂停跟随并显示全部保留记录；重新勾选跟随可回到最新。");
        AddAction(actions, "删除最早16条", () => { using (chart.DeferDataUpdates()) foreach (var buffer in buffers) buffer.RemoveRange(0, Math.Min(16, buffer.Count)); board.Refresh(); }, "按批删除并合并场景通知。");
        AddAction(actions, "批量替换8条", () => { using (chart.DeferDataUpdates()) for (var s = 0; s < 4; s++) { var source = buffers[s]; if (source.Count >= 8) source.ReplaceRange(0, 8, source.Take(8).Select(row => row with { Id = "replacement-" + row.Id, Y = row.Y + 3 }).ToArray()); } board.Refresh(); }, "替换保留原始X，业务ID与对象随新记录更新。");
        var follow = new CheckBox { Name = "StreamingFollowLatest", Content = "跟随最新数据（手动拖动/缩放可暂停）", IsChecked = true };
        follow.IsCheckedChanged += (_, _) => { chart.FollowLatestData = follow.IsChecked == true; board.Refresh(); };
        chart.PropertyChanged += (_, e) => { if (e.Property == CartesianChart.FollowLatestDataProperty) follow.IsChecked = chart.FollowLatestData; };
        chart.SelectionChanged += (_, _) => identity.Text = chart.SelectedPoint is { } hit ? $"原始 {hit.StableId} · 当前源索引 {hit.Index} · X={hit.Point.X:0} · Y={hit.Point.Y:0.00}" : "选择已清除；当前窗口继续保持原始业务对象。";
        view.Children.Add(new TextBlock { Text = "四路后台数据 → 有界批队列 → UI合并提交。每路仅保留512条；暂停提交与暂停跟随相互独立。拖动可检查历史，再勾选跟随回到最新。", TextWrapping = TextWrapping.Wrap });
        view.Children.Add(actions); view.Children.Add(follow); view.Children.Add(status); view.Children.Add(chart); view.Children.Add(identity); board.Refresh();
        return board;
    }

    private static StreamingRow StreamingRecord(int stream, int ordinal) => new($"stream-{stream}-sample-{ordinal}", ordinal, 10 + stream * 22 + ordinal % 16 * .55);
    private sealed record StreamingRow(string Id, double X, double Y);
    private sealed record StreamingSample(int Stream, StreamingRow Row);
    private sealed class IncrementalStreamingBoard : IDisposable
    {
        private CancellationTokenSource? _producerCancellation;
        private Task? _producer;
        private int _nextOrdinal = 256;
        private string? _lastProducerError;
        public IncrementalStreamingBoard(StackPanel view, CartesianChart chart, ChartDataBuffer<StreamingRow>[] buffers, TextBlock status, TextBlock identity)
        { View = view; Chart = chart; Buffers = buffers; Status = status; Identity = identity; Session = NewSession(); }
        public StackPanel View { get; }
        public CartesianChart Chart { get; }
        public ChartDataBuffer<StreamingRow>[] Buffers { get; }
        public TextBlock Status { get; }
        public TextBlock Identity { get; }
        public ChartStreamingSession<StreamingSample> Session { get; private set; }
        public int NextOrdinal => _nextOrdinal;
        public int LastApplyThreadId { get; private set; }
        private ChartStreamingSession<StreamingSample> NewSession() => new(batch =>
        {
            LastApplyThreadId = Environment.CurrentManagedThreadId;
            using (Chart.DeferDataUpdates())
                for (var s = 0; s < 4; s++) Buffers[s].AppendRange(batch.Where(sample => sample.Stream == s).Select(sample => sample.Row));
            Dispatcher.UIThread.Post(Refresh);
        }, maximumBatchSize: 1024, maximumPendingBatches: 2);
        public static StreamingSample[] Batch(int start, int count = 64) => Enumerable.Range(0, 4).SelectMany(s => Enumerable.Range(start, count).Select(i => new StreamingSample(s, StreamingRecord(s, i)))).ToArray();
        public bool TryAppend()
        {
            if (_producer is { IsCompleted: false }) { Identity.Text = "后台生产期间由同一生产者安排序号；停止后可手动追加。"; return false; }
            var result = Session.TryAppend(Batch(_nextOrdinal)); if (result) _nextOrdinal += 64; Refresh(); return result;
        }
        public void Start()
        {
            if (_producer is { IsCompleted: false }) return;
            if (Session.IsCompleted || Session.IsDisposed) { Session.Dispose(); Session = NewSession(); }
            Session.Resume(); _lastProducerError = null; _producerCancellation?.Dispose(); _producerCancellation = new(); var token = _producerCancellation.Token;
            _producer = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await Session.AppendAsync(Batch(_nextOrdinal), token); _nextOrdinal += 64;
                        await Task.Delay(80, token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception error) { _lastProducerError = error.Message; }
                finally { Dispatcher.UIThread.Post(Refresh); }
            }); Refresh();
        }
        public void Stop() { _producerCancellation?.Cancel(); Session.Cancel(); Refresh(); }
        public void Refresh()
        {
            var stats = Session.Statistics;
            Status.Text = $"保留 {string.Join(" / ", Buffers.Select(buffer => buffer.Count))} 条 · 接收 {stats.AcceptedSamples} / 已提交 {stats.CommittedSamples} / 取消 {stats.CanceledSamples} / 拒绝 {stats.RejectedSamples}\n" +
                $"待提交 {stats.PendingBatches}/2 批 · 峰值 {stats.MaximumObservedPendingBatches} 批 · 等待生产者 {stats.WaitingWriters} · UI批次 {stats.AppliedUiBatches} · 合并 {stats.CoalescedBatches} · {(Session.IsCompleted ? "会话已结束" : Session.IsPaused ? "提交暂停" : "提交开启")} · {(Chart.FollowLatestData ? "跟随最新" : "检查历史")}" + (_lastProducerError is null ? "" : " · " + _lastProducerError);
        }
        public void Dispose() { _producerCancellation?.Cancel(); Session.Dispose(); _producerCancellation?.Dispose(); }
    }
}
