using Avalonia.Threading;

namespace NT.AvaloniaControl.Markdown;

/// <summary>UI housekeeping must not retain a producer's expired ambient scope.</summary>
internal static class MarkdownUiDispatcher
{
    private static readonly Task<ExecutionContext> NeutralContext = CreateNeutralContext();

    private static Task<ExecutionContext> CreateNeutralContext()
    {
        var ready = new TaskCompletionSource<ExecutionContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        // Unsafe dispatch never imports the initializing host/test's AsyncLocals.
        // Capture once asynchronously; UI callers never block or create a thread.
        ThreadPool.UnsafeQueueUserWorkItem(static completion => completion.SetResult(ExecutionContext.Capture()!), ready, false);
        return ready.Task;
    }

    internal static async Task InvokeAsync(Action action)
    {
        var neutral = await NeutralContext.ConfigureAwait(false);
        DispatcherOperation operation;
        if (ExecutionContext.IsFlowSuppressed()) operation = Send();
        else { using (ExecutionContext.SuppressFlow()) operation = Send(); }
        await operation.GetTask().ConfigureAwait(false);

        DispatcherOperation Send() => Dispatcher.UIThread.InvokeAsync(() =>
            ExecutionContext.Run(neutral, static callback => ((Action)callback!).Invoke(), action));
    }

    internal static void Post(Action action) => Post(action, DispatcherPriority.Normal);

    internal static void Post(Action action, DispatcherPriority priority)
    {
        if (NeutralContext.IsCompletedSuccessfully) SendPost(action, priority, NeutralContext.Result);
        else _ = PostWhenReadyAsync(action, priority);
    }

    private static async Task PostWhenReadyAsync(Action action, DispatcherPriority priority) =>
        SendPost(action, priority, await NeutralContext.ConfigureAwait(false));

    private static void SendPost(Action action, DispatcherPriority priority, ExecutionContext neutral)
    {
        if (ExecutionContext.IsFlowSuppressed()) Send();
        else { using (ExecutionContext.SuppressFlow()) Send(); }

        void Send() => Dispatcher.UIThread.Post(() =>
            ExecutionContext.Run(neutral, static callback => ((Action)callback!).Invoke(), action), priority);
    }
}
