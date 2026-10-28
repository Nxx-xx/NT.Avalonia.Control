namespace NT.AvaloniaControl.PdfViewer.Interop;

internal enum PdfiumWorkPriority
{
    Foreground,
    Search,
    Thumbnail
}

/// <summary>
/// Runs synchronous native calls on one worker. Weighted round robin bounds starvation
/// while giving visible page work more opportunities than speculative thumbnails.
/// Cancellation can remove queued work; a running native call owns its result until return.
/// </summary>
internal sealed class PdfiumWorkScheduler
{
    private static readonly int[] Slots = [0, 0, 0, 0, 1, 1, 2];
    private static int s_workCounter;
    private readonly object _sync = new();
    private readonly Queue<IWorkItem>[] _queues = [new(), new(), new()];
    private int _nextSlot;
    private bool _draining;

    internal Task<T> Enqueue<T>(Func<T> operation, CancellationToken cancellationToken = default,
        PdfiumWorkPriority priority = PdfiumWorkPriority.Foreground,
        PdfPerformanceStage stage = PdfPerformanceStage.NativeExec, string correlationId = "")
    {
        ArgumentNullException.ThrowIfNull(operation);
        if ((uint)priority > (uint)PdfiumWorkPriority.Thumbnail)
            throw new ArgumentOutOfRangeException(nameof(priority));
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        // PERF01: every item carries a correlation id so queue-wait and execution spans group
        // with the caller-side stage spans (decode, upload, submit) of the same operation.
        var item = new WorkItem<T>(operation, cancellationToken, stage,
            correlationId.Length == 0 ? "work-" + Interlocked.Increment(ref s_workCounter) : correlationId);
        lock (_sync)
        {
            _queues[(int)priority].Enqueue(item);
            if (!_draining)
            {
                _draining = true;
                _ = Task.Run(Drain);
            }
        }
        return item.Task;
    }

    private void Drain()
    {
        while (true)
        {
            IWorkItem? item = null;
            lock (_sync)
            {
                for (var attempt = 0; attempt < Slots.Length; attempt++)
                {
                    var queue = _queues[Slots[_nextSlot]];
                    _nextSlot = (_nextSlot + 1) % Slots.Length;
                    if (queue.Count != 0)
                    {
                        item = queue.Dequeue();
                        break;
                    }
                }
                if (item is null)
                {
                    _draining = false;
                    return;
                }
            }
            item.Execute();
        }
    }

    private interface IWorkItem
    {
        void Execute();
    }

    private sealed class WorkItem<T> : IWorkItem
    {
        private Func<T>? _operation;
        private readonly CancellationToken _token;
        private readonly PdfPerformanceStage _stage;
        private readonly string _correlationId;
        private readonly long _enqueuedAt;
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _registrationSync = new();
        private CancellationTokenRegistration _registration;
        private int _state; // 0 = queued, 1 = executing, 2 = finished/canceled before execution.

        internal WorkItem(Func<T> operation, CancellationToken token, PdfPerformanceStage stage, string correlationId)
        {
            _operation = operation;
            _token = token;
            _stage = stage;
            _correlationId = correlationId;
            _enqueuedAt = PdfPerformanceSession.Now;
            if (token.CanBeCanceled)
            {
                // A canceled token may invoke its callback synchronously inside registration.
                // Attach only after Register returns, and release an already canceled registration.
                var registration = token.UnsafeRegister(static state => ((WorkItem<T>)state!).CancelQueued(), this);
                lock (_registrationSync)
                {
                    if (Volatile.Read(ref _state) == 2)
                        registration.Unregister();
                    else
                        _registration = registration;
                }
            }
        }

        internal Task<T> Task => _completion.Task;

        private void CancelQueued()
        {
            if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
            {
                // Do not retain an abandoned document byte array behind a long native call.
                Interlocked.Exchange(ref _operation, null);
                UnregisterCancellation();
                // PERF01: a dropped queue item still reports where cancellation was observed;
                // the zero-length span is the observation instant on the monotonic clock.
                if (PdfPerformanceSession.Current is { } session)
                {
                    var observed = PdfPerformanceSession.Now;
                    session.Record(_correlationId, PdfPerformanceStage.CancelObserved, observed, observed);
                }
                _completion.TrySetCanceled(_token);
            }
        }

        private void UnregisterCancellation()
        {
            lock (_registrationSync)
            {
                // Never wait for another cancellation callback on the native worker/finalizer.
                _registration.Unregister();
                _registration = default;
            }
        }

        public void Execute()
        {
            if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            {
                UnregisterCancellation();
                return;
            }
            var startedAt = PdfPerformanceSession.Now;
            if (PdfPerformanceSession.Current is { } sessionAtStart)
                sessionAtStart.Record(_correlationId, PdfPerformanceStage.QueueWait, _enqueuedAt, startedAt);
            try
            {
                _token.ThrowIfCancellationRequested();
                var operation = Interlocked.Exchange(ref _operation, null)!;
                _completion.TrySetResult(operation());
            }
            catch (OperationCanceledException exception) when (exception.CancellationToken.IsCancellationRequested)
            {
                if (PdfPerformanceSession.Current is { } canceled)
                {
                    var observed = PdfPerformanceSession.Now;
                    canceled.Record(_correlationId, PdfPerformanceStage.CancelObserved, observed, observed);
                }
                _completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
            }
            finally
            {
                var finishedAt = PdfPerformanceSession.Now;
                if (PdfPerformanceSession.Current is { } sessionAtEnd)
                    sessionAtEnd.Record(_correlationId, _stage, startedAt, finishedAt);
                Interlocked.Exchange(ref _state, 2);
                Interlocked.Exchange(ref _operation, null);
                UnregisterCancellation();
            }
        }
    }
}
