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
    private readonly object _sync = new();
    private readonly Queue<IWorkItem>[] _queues = [new(), new(), new()];
    private int _nextSlot;
    private bool _draining;

    internal Task<T> Enqueue<T>(Func<T> operation, CancellationToken cancellationToken = default,
        PdfiumWorkPriority priority = PdfiumWorkPriority.Foreground)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if ((uint)priority > (uint)PdfiumWorkPriority.Thumbnail)
            throw new ArgumentOutOfRangeException(nameof(priority));
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        var item = new WorkItem<T>(operation, cancellationToken);
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
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _registrationSync = new();
        private CancellationTokenRegistration _registration;
        private int _state; // 0 = queued, 1 = executing, 2 = finished/canceled before execution.

        internal WorkItem(Func<T> operation, CancellationToken token)
        {
            _operation = operation;
            _token = token;
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
            try
            {
                _token.ThrowIfCancellationRequested();
                var operation = Interlocked.Exchange(ref _operation, null)!;
                _completion.TrySetResult(operation());
            }
            catch (OperationCanceledException exception) when (exception.CancellationToken.IsCancellationRequested)
            {
                _completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
            }
            finally
            {
                Interlocked.Exchange(ref _state, 2);
                Interlocked.Exchange(ref _operation, null);
                UnregisterCancellation();
            }
        }
    }
}
