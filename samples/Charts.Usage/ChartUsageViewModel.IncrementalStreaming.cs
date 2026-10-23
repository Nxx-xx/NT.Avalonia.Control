using System.Windows.Input;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private bool _streamingFollowLatest = true;
    private ChartViewport? _streamingViewport;
    private ChartPointHit? _streamingSelectedPoint;
    private ChartStreamingSession<LevelOfDetailUsageRecord>? _streamingSession;
    private CancellationTokenSource? _streamingProducerCancellation;
    private Task? _streamingProducer;
    private int _streamingNextOrdinal = 128;
    private string _streamingStatus = "256条有限窗口 · 后台批数据通过最多2批队列提交";
    public ChartDataBuffer<LevelOfDetailUsageRecord> StreamingRecords { get; } = CreateStreamingRecords();
    public bool StreamingFollowLatest { get => _streamingFollowLatest; set { if (_streamingFollowLatest == value) return; _streamingFollowLatest = value; Notify(); } }
    public ChartViewport? StreamingViewport { get => _streamingViewport; set { if (_streamingViewport == value) return; _streamingViewport = value; Notify(); } }
    public ChartPointHit? StreamingSelectedPoint { get => _streamingSelectedPoint; set { if (ReferenceEquals(_streamingSelectedPoint, value)) return; _streamingSelectedPoint = value; Notify(); Notify(nameof(StreamingSelectionText)); } }
    public string StreamingSelectionText => StreamingSelectedPoint is { } hit ? $"原始业务 {hit.StableId} · 当前窗口索引 {hit.Index}" : "悬停/选择查看保留的原始业务记录";
    public string StreamingStatus { get => _streamingStatus; private set { _streamingStatus = value; Notify(); } }
    public ICommand StreamingStartCommand => new ActionCommand(StartStreaming);
    public ICommand StreamingPauseCommand => new ActionCommand(() => { StreamingSession.Pause(); RefreshStreamingStatus(); });
    public ICommand StreamingResumeCommand => new ActionCommand(() => { StreamingSession.Resume(); RefreshStreamingStatus(); });
    public ICommand StreamingStopCommand => new ActionCommand(StopStreaming);
    public ICommand StreamingAppendCommand => new ActionCommand(() =>
    {
        if (_streamingProducer is { IsCompleted: false }) return;
        if (StreamingSession.TryAppend(StreamingRows(_streamingNextOrdinal, 32))) _streamingNextOrdinal += 32;
        RefreshStreamingStatus();
    });
    internal ChartStreamingSession<LevelOfDetailUsageRecord> StreamingSession => _streamingSession ??= new(rows => { StreamingRecords.AppendRange(rows); Dispatcher.UIThread.Post(RefreshStreamingStatus); }, maximumBatchSize: 128, maximumPendingBatches: 2);
    internal static LevelOfDetailUsageRecord[] StreamingRows(int start, int count) => Enumerable.Range(start, count).Select(i => new LevelOfDetailUsageRecord("usage-stream-" + i, i, 20 + i % 32)).ToArray();
    private static ChartDataBuffer<LevelOfDetailUsageRecord> CreateStreamingRecords() { var buffer = new ChartDataBuffer<LevelOfDetailUsageRecord>(256); buffer.AppendRange(StreamingRows(0, 128)); return buffer; }
    private void StartStreaming()
    {
        if (_streamingProducer is { IsCompleted: false }) return;
        if (_streamingSession is { IsCompleted: true } or { IsDisposed: true }) { _streamingSession.Dispose(); _streamingSession = null; }
        var session = StreamingSession; session.Resume(); _streamingProducerCancellation?.Dispose(); _streamingProducerCancellation = new(); var token = _streamingProducerCancellation.Token;
        _streamingProducer = Task.Run(async () =>
        {
            try { while (!token.IsCancellationRequested) { await session.AppendAsync(StreamingRows(_streamingNextOrdinal, 32), token); _streamingNextOrdinal += 32; await Task.Delay(80, token); } }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception error) { Dispatcher.UIThread.Post(() => StreamingStatus = error.Message); }
            finally { Dispatcher.UIThread.Post(RefreshStreamingStatus); }
        }); RefreshStreamingStatus();
    }
    private void StopStreaming() { _streamingProducerCancellation?.Cancel(); _streamingSession?.Cancel(); RefreshStreamingStatus(); }
    internal void RefreshStreamingStatus()
    {
        if (_streamingSession is null) return;
        var stats = _streamingSession.Statistics;
        StreamingStatus = $"保留 {StreamingRecords.Count}/256 条 · 接收 {stats.AcceptedSamples} / 提交 {stats.CommittedSamples} / 取消 {stats.CanceledSamples} · 待提交 {stats.PendingBatches}/2 批 · 等待 {stats.WaitingWriters} · UI提交 {stats.AppliedUiBatches} · 合并 {stats.CoalescedBatches} · {(_streamingSession.IsCompleted ? "会话已结束" : _streamingSession.IsPaused ? "暂停提交" : "正常提交")}";
    }
    internal void DisposeStreaming() { _streamingProducerCancellation?.Cancel(); _streamingSession?.Dispose(); _streamingProducerCancellation?.Dispose(); }
}
