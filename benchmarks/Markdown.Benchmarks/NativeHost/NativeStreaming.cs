using System.Diagnostics;
using System.Text;
using NT.AvaloniaControl.Markdown;

namespace MarkdownNativeBenchmarks;

internal sealed partial class NativeBenchmarkRunner
{
    private sealed record Receipt(int End, int Utf8Length, long Started, long Allocation, long SourceVersion);
    private async Task StreamAsync(int bytes, bool longSession)
    {
        _reader.CodeHighlighter = null;
        _reader.AutoScrollToEnd = true;
        var source = CorpusFactory.Mixed(bytes);
        const int chunkSize = 8192;
        // Calculate prefix lengths outside every session clock. Observers must not
        // repeatedly scan the growing document and distort the long-session cost.
        var utf8Ends = new Dictionary<int, int> { [0] = 0 };
        for (var offset = 0; offset < source.Length; offset += chunkSize)
        {
            var end = Math.Min(source.Length, offset + chunkSize);
            utf8Ends[end] = Encoding.UTF8.GetByteCount(source.AsSpan(0, end));
        }
        var finalBytes = utf8Ends[source.Length];
        var latency = new NativeGroup { 名称 = longSession ? "1MiB长会话-20chunk每秒最早未显示至原生帧" : "100KiB-20chunk每秒最早未显示至原生帧", 语料SHA256 = Hash(source), UTF8字节数 = Encoding.UTF8.GetByteCount(source), 预热次数 = _warmup, 总耗时P95预算毫秒 = longSession ? null : 150, UI批P95预算毫秒 = longSession ? 50 : null };
        var completionGroup = new NativeGroup { 名称 = longSession ? "1MiB长会话Complete至最终提交" : "100KiB流式Complete至最终提交", 语料SHA256 = Hash(source), UTF8字节数 = Encoding.UTF8.GetByteCount(source), 预热次数 = _warmup, 总耗时P95预算毫秒 = longSession ? 1500 : 500 };
        var batches = longSession ? _report.长会话流式批次 : _report.流式批次;
        var completions = longSession ? _report.长会话流式完成 : _report.流式完成;
        var savedReceipts = longSession ? _report.长会话流式接收 : _report.流式接收;
        var sessions = longSession ? _longStreamSessions : _streamSessions;
        _report.场景.Add(latency);
        _report.场景.Add(completionGroup);
        for (var sessionIndex = -_warmup; sessionIndex < sessions; sessionIndex++)
        {
            var baseSourceVersion = await ResetAsync();
            var receipts = new List<Receipt>();
            var receiptGate = new object();
            var rendered = 0;
            var frameTasks = new List<Task>();
            var observedBatchCount = 0;
            var completedTicks = 0L;
            var completionAllocation = 0L;
            var sessionBatches = new List<NativeStreamingBatch>();
            var startHeap = GC.GetTotalMemory(false);
            var startAllocation = GC.GetTotalAllocatedBytes(false);
            var commitCount = 0;
            using var session = _reader.BeginStreaming();
            async Task ObserveFrameAsync()
            {
                var frame = await _witness.ObserveAsync();
                // Attribute parse/UI/layout to the document captured by Render, which
                // can be newer than the event that initially requested this witness.
                var metric = frame.Metric;
                List<Receipt> covered;
                lock (receiptGate)
                {
                    covered = receipts.Where(receipt => receipt.End > rendered && receipt.End <= metric.SourceLength && receipt.SourceVersion <= metric.SourceVersion).ToList();
                    rendered = Math.Max(rendered, frame.Length);
                }
                if (covered.Count == 0) return;
                var earliest = covered.Min(receipt => receipt.Started);
                var elapsed = Stopwatch.GetElapsedTime(earliest, frame.RenderedStamp).TotalMilliseconds;
                var allocated = Math.Max(0, GC.GetTotalAllocatedBytes(false) - covered.Min(receipt => receipt.Allocation));
                bool matchesReceipt;
                lock (receiptGate) matchesReceipt = receipts.Any(receipt => receipt.SourceVersion == metric.SourceVersion && receipt.End == metric.SourceLength);
                var knownLength = utf8Ends.TryGetValue(frame.Length, out var renderedBytes);
                var valid = frame.Length <= source.Length && metric.SourceLength == frame.Length && matchesReceipt && knownLength;
                if (sessionIndex >= 0)
                {
                    var ordinal = latency.样本.Count;
                    latency.样本.Add(new(ordinal, elapsed, metric.ParseElapsedMilliseconds, metric.UiApplyElapsedMilliseconds, metric.LayoutElapsedMilliseconds, allocated, valid));
                    var batch = new NativeStreamingBatch(sessionIndex, observedBatchCount++, earliest,
                        frame.RenderedStamp, frame.Length, renderedBytes, finalBytes, covered.Count,
                        covered.Sum(receipt => receipt.Utf8Length), metric.SourceVersion, metric.SourceLength,
                        matchesReceipt, elapsed, metric.ParseElapsedMilliseconds, metric.UiApplyElapsedMilliseconds,
                        metric.LayoutElapsedMilliseconds, allocated, GC.GetTotalMemory(false), valid);
                    sessionBatches.Add(batch);
                    batches.Add(batch);
                }
            }
            void Measured(object? sender, MarkdownUpdateMetrics metric)
            {
                if (longSession && sessionIndex >= 0 && metric.SourceLength > 0)
                {
                    bool knownReceipt;
                    lock (receiptGate) knownReceipt = receipts.Any(receipt => receipt.SourceVersion == metric.SourceVersion && receipt.End == metric.SourceLength);
                    _report.长会话提交.Add(new(sessionIndex, commitCount++, metric.SourceVersion,
                        metric.SourceLength, utf8Ends.GetValueOrDefault(metric.SourceLength),
                        metric.ParseElapsedMilliseconds, metric.UiApplyElapsedMilliseconds,
                        metric.LayoutElapsedMilliseconds, _reader.ActiveParseCount, _reader.PendingRequestCount,
                        _reader.ExecutingResourceRequestCount, _reader.PendingResourceRequestCount, knownReceipt));
                }
                if (metric.SourceLength > 0) frameTasks.Add(ObserveFrameAsync());
            }
            _reader.UpdateMeasured += Measured;
            try
            {
                await Task.Run(async () =>
                {
                    var chunkNumber = 0;
                    var started = Stopwatch.GetTimestamp();
                    for (var offset = 0; offset < source.Length; offset += chunkSize)
                    {
                        var due = TimeSpan.FromMilliseconds(chunkNumber++ * 50);
                        var delay = due - Stopwatch.GetElapsedTime(started);
                        if (delay > TimeSpan.Zero) await Task.Delay(delay);
                        var end = Math.Min(source.Length, offset + chunkSize);
                        // Never rewrite this timestamp when later input merges with the chunk.
                        lock (receiptGate) receipts.Add(new(end, utf8Ends[end] - utf8Ends[offset], Stopwatch.GetTimestamp(), GC.GetTotalAllocatedBytes(false), baseSourceVersion + chunkNumber));
                        session.Append(source.AsMemory(offset, end - offset));
                    }
                    // Stop-receive is observed on the producer, before an awaited
                    // continuation can wait in the UI queue and hide final latency.
                    completedTicks = Stopwatch.GetTimestamp();
                    completionAllocation = GC.GetTotalAllocatedBytes(false);
                });
                var completed = await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(60));
                var applied = Stopwatch.GetTimestamp();
                var completionAllocated = GC.GetTotalAllocatedBytes(false) - completionAllocation;
                if (sessionIndex >= 0)
                {
                    lock (receiptGate)
                    {
                        var previousEnd = 0;
                        for (var receiptIndex = 0; receiptIndex < receipts.Count; receiptIndex++)
                        {
                            var receipt = receipts[receiptIndex];
                            savedReceipts.Add(new(sessionIndex, receiptIndex, receipt.SourceVersion, receipt.End,
                                utf8Ends[receipt.End], receipt.End - previousEnd, receipt.Utf8Length, receipt.Started));
                            previousEnd = receipt.End;
                        }
                    }
                }
                await Task.WhenAll(frameTasks.ToArray()).WaitAsync(TimeSpan.FromSeconds(60));
                if (rendered < source.Length)
                {
                    await ObserveFrameAsync();
                }
                var elapsed = Stopwatch.GetElapsedTime(completedTicks, applied).TotalMilliseconds;
                _reader.SelectAll();
                var completeSelection = _reader.SelectedText == _reader.Document.Text;
                _reader.ClearSelection();
                var semanticValid = completed.Status == MarkdownUpdateStatus.Completed && !completed.LayoutPending && _reader.Text == source && _reader.Document.Source == source && rendered == source.Length && _lastMetric.SourceVersion == completed.SourceVersion && _lastMetric.SourceLength == source.Length && completeSelection;
                var completionValid = semanticValid && elapsed <= (longSession ? 1500 : 500);
                if (sessionIndex >= 0)
                {
                    completionGroup.样本.Add(new(sessionIndex, elapsed, _lastMetric.ParseElapsedMilliseconds, _lastMetric.UiApplyElapsedMilliseconds, _lastMetric.LayoutElapsedMilliseconds, completionAllocated, completionValid));
                    completions.Add(new { 会话序号 = sessionIndex, BaseSourceVersion = baseSourceVersion, 接收结束Ticks = completedTicks, 最终应用Ticks = applied, 完成毫秒 = elapsed, SourceVersion = completed.SourceVersion, 源长度 = source.Length, 已绘制长度 = rendered, 全文选择完整 = completeSelection, 最终ActiveResourceRequestCount = _reader.ActiveResourceRequestCount, 是否通过 = completionValid });
                    if (longSession) _report.长会话退化.Add(NativeStreamGrowthSummary.Create(sessionIndex,
                        finalBytes, GC.GetTotalAllocatedBytes(false) - startAllocation, startHeap,
                        GC.GetTotalMemory(false), sessionBatches));
                    if ((sessionIndex + 1) % 10 == 0)
                        Console.WriteLine($"{bytes / 1024}KiB streaming progress: sessions={sessionIndex + 1}/{sessions}, batches={latency.样本数}, current-completion-P95={completionGroup.总耗时P95毫秒:F3}ms");
                }
                // A latency failure must retain all formal samples instead of masquerading
                // as a document mismatch or truncating the remaining sessions.
                if (!semanticValid) throw new InvalidOperationException("Streaming final document/frame mismatch.");
            }
            finally { _reader.UpdateMeasured -= Measured; }
        }
        _reader.AutoScrollToEnd = false;
        _report.检查[longSession ? "长会话100有效批次" : "流式100有效批次"] = !_report.正式采样 || latency.样本数 >= 100;
        _report.检查[longSession ? "长会话100完成样本" : "流式100完成样本"] = !_report.正式采样 || completionGroup.样本数 >= 100;
        if (longSession)
        {
            _report.检查["长会话提交UI预算"] = _report.长会话提交.Count > 0 && _report.长会话提交.All(commit => commit.接收版本对应长度) && _report.长会话提交UI批P95毫秒 <= 50;
            _report.检查["长会话100提交样本"] = !_report.正式采样 || _report.长会话提交.Count >= 100;
            _report.检查["长会话资源队列有界"] = _report.长会话提交.All(commit => commit.ActiveParseCount <= 1 && commit.PendingRequestCount <= 1 && commit.ExecutingResourceRequestCount <= 4 && commit.PendingResourceRequestCount <= 256) && _reader.ActiveResourceRequestCount == 0;
        }
        Console.WriteLine($"{bytes / 1024}KiB streaming: batch-P95={latency.总耗时P95毫秒:F3}ms, completion-P95={completionGroup.总耗时P95毫秒:F3}ms, batches={latency.样本数}, sessions={completionGroup.样本数}, fixed-earliest-chunk-latency-budget={!longSession}");
    }
}
