using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace MarkdownNativeBenchmarks;

internal sealed class FrameWitness(Reader reader, Func<MarkdownUpdateMetrics> metrics) : Control
{
    private readonly List<TaskCompletionSource<(int Length, long Stamp, MarkdownUpdateMetrics Metric)>> _pending = [];
    private static readonly JsonSerializerOptions DiagnosticJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private long _requestCount;
    private long _renderCount;
    private UiSnapshot? _lastRender;

    // These contain values only. A timer/fault continuation never reads UI objects.
    private sealed record UiSnapshot(long 捕获ticks, string 来源, long 请求次数, long Render次数, int 待观察请求数,
        bool Witness已挂载, bool WitnessVisible, bool WitnessEffectivelyVisible, bool WitnessMeasureValid, bool WitnessArrangeValid,
        bool Reader已挂载, bool ReaderPresentationEnvironment, bool ReaderVisible, bool ReaderEffectivelyVisible, bool ReaderMeasureValid, bool ReaderArrangeValid,
        bool? WindowVisible, bool? WindowEffectivelyVisible, bool? WindowMeasureValid, bool? WindowArrangeValid, bool? WindowActive,
        string? WindowState, int 文档实例identity, long? 文档generation, string generation来源, long MetricSourceVersion,
        int MetricSourceLength, int 实际文档SourceLength, double Witness宽DIP, double Witness高DIP);

    private UiSnapshot CaptureUiSnapshot(string origin)
    {
        Dispatcher.UIThread.VerifyAccess();
        var window = TopLevel.GetTopLevel(this) as Window;
        var metric = metrics();
        return new(Stopwatch.GetTimestamp(), origin, _requestCount, _renderCount, _pending.Count,
            TopLevel.GetTopLevel(this) is not null, IsVisible, IsEffectivelyVisible, IsMeasureValid, IsArrangeValid,
            TopLevel.GetTopLevel(reader) is not null, reader.HasPresentationEnvironment, reader.IsVisible, reader.IsEffectivelyVisible, reader.IsMeasureValid, reader.IsArrangeValid,
            window?.IsVisible, window?.IsEffectivelyVisible, window?.IsMeasureValid, window?.IsArrangeValid, window?.IsActive,
            window?.WindowState.ToString(), RuntimeHelpers.GetHashCode(reader.Document), null, "控件公开API未暴露generation；identity不是generation",
            metric.SourceVersion, metric.SourceLength, reader.Document.Source.Length, Bounds.Width, Bounds.Height);
    }

    private string TimeoutDiagnostic(string phase, UiSnapshot request, UiSnapshot? current, long timeoutStamp)
    {
        var last = Volatile.Read(ref _lastRender);
        return JsonSerializer.Serialize(new
        {
            阶段 = phase,
            超时秒数 = 30,
            超时ticks = timeoutStamp,
            当前UI快照可用 = current is not null,
            当前UI快照说明 = current is null ? "未读取UI对象；仅保留请求与最后实际Render快照，不新增UI派发等待" : "在UI线程直接捕获，没有额外等待",
            请求快照 = request,
            最后实际Render快照 = last,
            最后实际Render距超时毫秒 = last is null ? (double?)null : Stopwatch.GetElapsedTime(last.捕获ticks, timeoutStamp).TotalMilliseconds,
            当前UI快照 = current
        }, DiagnosticJson);
    }

    private async Task<T> WaitObservedAsync<T>(Task<T> task, string phase, UiSnapshot request)
    {
        var timed = task.WaitAsync(TimeSpan.FromSeconds(30));
        // If the UI is no longer responding, emit immutable evidence from the timer
        // thread rather than waiting for another UI dispatch to collect diagnostics.
        _ = timed.ContinueWith(failed =>
        {
            if (failed.Exception?.InnerException is TimeoutException)
                Console.Error.WriteLine("FrameWitness timeout (UI状态未另行读取): " + TimeoutDiagnostic(phase, request, null, Stopwatch.GetTimestamp()));
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        try { return await timed; }
        catch (TimeoutException error)
        {
            var snapshot = Dispatcher.UIThread.CheckAccess() ? CaptureUiSnapshot("Timeout") : null;
            throw new TimeoutException("FrameWitness: " + TimeoutDiagnostic(phase, request, snapshot, Stopwatch.GetTimestamp()), error);
        }
    }

    public async Task<(int Length, long RenderedStamp, MarkdownUpdateMetrics Metric)> ObserveAsync()
    {
        Dispatcher.UIThread.VerifyAccess();
        var completion = new TaskCompletionSource<(int Length, long Stamp, MarkdownUpdateMetrics Metric)>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requestCount++;
        _pending.Add(completion);
        var request = CaptureUiSnapshot("ObserveRequest");
        InvalidateVisual();
        var drawn = await WaitObservedAsync(completion.Task, "UI Control.Render", request);
        // This public Avalonia batch provides render-thread completion, stronger than a
        // UI Render callback. It still does not certify physical display scan-out.
        var compositor = ElementComposition.GetElementVisual(this)?.Compositor ?? throw new InvalidOperationException("No native compositor.");
        var batch = compositor.RequestCompositionBatchCommitAsync();
        var stamp = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = batch.Rendered.ContinueWith(task =>
        {
            if (task.IsFaulted) stamp.TrySetException(task.Exception!.InnerExceptions);
            else if (task.IsCanceled) stamp.TrySetCanceled();
            else stamp.TrySetResult(Stopwatch.GetTimestamp());
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return (drawn.Length, await WaitObservedAsync(stamp.Task, "CompositionBatch.Rendered", request), drawn.Metric);
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(0x01000000), new Rect(0, 0, 1, 1));
        _renderCount++;
        Volatile.Write(ref _lastRender, CaptureUiSnapshot("ActualRender"));
        if (_pending.Count == 0 || !reader.HasPresentationEnvironment || !reader.IsEffectivelyVisible) return;
        var observation = (reader.Document.Source.Length, Stopwatch.GetTimestamp(), metrics());
        foreach (var completion in _pending) completion.TrySetResult(observation);
        _pending.Clear();
    }
}
