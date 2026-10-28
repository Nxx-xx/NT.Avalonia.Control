using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace TreeDataGridNativeBenchmarks;

internal sealed record RenderReceipt(long UiRenderTicks, long BatchRequestedTicks, long ProcessedTicks, long RenderedTicks, SceneSnapshot? Snapshot);

/// <summary>Observes a real Control.Render followed by public native CompositionBatch.Rendered, never physical scan-out.</summary>
internal sealed class FrameWitness : Control
{
    private TaskCompletionSource<(long Ticks, SceneSnapshot? Snapshot)>? _pending;
    private Func<SceneSnapshot?>? _snapshot;
    private static readonly IBrush Paint = new SolidColorBrush(0x01000000);
    internal async Task<RenderReceipt> ObserveAsync(Func<SceneSnapshot?> snapshot)
    {
        Dispatcher.UIThread.VerifyAccess();
        NativeFixture.Require(_pending is null, "Only one native render observation may be pending.");
        var pending = new TaskCompletionSource<(long Ticks, SceneSnapshot? Snapshot)>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = pending; _snapshot = snapshot; InvalidateVisual();
        try
        {
            var drawn = await pending.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var compositor = ElementComposition.GetElementVisual(this)?.Compositor ?? throw new InvalidOperationException("No native compositor.");
            var requested = Stopwatch.GetTimestamp(); var batch = compositor.RequestCompositionBatchCommitAsync();
            var processed = Stamp(batch.Processed); var rendered = Stamp(batch.Rendered);
            var renderedTicks = await rendered.WaitAsync(TimeSpan.FromSeconds(15));
            return new(drawn.Ticks, requested, await processed, renderedTicks, drawn.Snapshot);
        }
        finally { _pending = null; _snapshot = null; }
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context); context.FillRectangle(Paint, new Rect(0, 0, 1, 1));
        if (_pending is { } pending && !pending.Task.IsCompleted)
        {
            try { pending.TrySetResult((Stopwatch.GetTimestamp(), _snapshot?.Invoke())); }
            catch (Exception error) { pending.TrySetException(error); }
        }
    }
    private static Task<long> Stamp(Task task)
    {
        var completion = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = task.ContinueWith(result =>
        {
            if (result.IsFaulted) completion.TrySetException(result.Exception!.InnerExceptions);
            else if (result.IsCanceled) completion.TrySetCanceled();
            else completion.TrySetResult(Stopwatch.GetTimestamp());
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return completion.Task;
    }
}
