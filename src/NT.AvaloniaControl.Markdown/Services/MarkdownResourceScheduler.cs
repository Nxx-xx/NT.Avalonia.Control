using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Markdown.Streaming.Tests")]

namespace NT.AvaloniaControl.Markdown;

/// <summary>
/// Per-reader bound on actual host service calls. A scheduled or running call holds its slot until
/// its delegate really finishes, even when cancellation is ignored. Waiting requests are FIFO.
/// </summary>
internal sealed class MarkdownResourceScheduler : IDisposable
{
    private readonly object _gate = new();
    private readonly LinkedList<WorkItem> _queue = new();
    private readonly int _maxConcurrency;
    private readonly int _maxQueue;
    private CancellationTokenSource _epochCancellation = new();
    private long _epoch;
    private int _active;
    private bool _disposed;

    public MarkdownResourceScheduler(int maxConcurrency = 4, int maxQueue = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);
        ArgumentOutOfRangeException.ThrowIfNegative(maxQueue);
        _maxConcurrency = maxConcurrency;
        _maxQueue = maxQueue;
    }

    /// <summary>Calls holding a slot, including those dispatched to the background pool.</summary>
    public int ActiveCount { get { lock (_gate) return _active; } }
    public int QueuedCount { get { lock (_gate) return _queue.Count; } }

    /// <summary>
    /// Invokes action on the background pool. On cancellation/epoch change, an unaccepted result
    /// is released with discardResult, or IDisposable.Dispose when no explicit callback is supplied.
    /// The caller owns an accepted result. Queue overflow faults explicitly instead of dropping work.
    /// </summary>
    public Task<T> RunAsync<T>(Func<CancellationToken, ValueTask<T>> action,
        CancellationToken token = default, Action<T>? discardResult = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        WorkItem<T> item;
        bool dispatch;
        lock (_gate)
        {
            if (_disposed) return Task.FromException<T>(new ObjectDisposedException(nameof(MarkdownResourceScheduler)));
            if (token.IsCancellationRequested) return Task.FromCanceled<T>(token);
            if (_active >= _maxConcurrency && _queue.Count >= _maxQueue)
                return Task.FromException<T>(new InvalidOperationException($"Markdown resource queue limit ({_maxQueue}) exceeded."));
            item = new(this, _epoch, _epochCancellation.Token, token, action, discardResult);
            dispatch = _active < _maxConcurrency;
            if (dispatch) { item.State = WorkState.Scheduled; _active++; }
            else { item.State = WorkState.Queued; item.QueueNode = _queue.AddLast(item); }
        }
        item.RegisterCancellation();
        if (dispatch) Dispatch(item);
        return item.Task;
    }

    /// <summary>Rejects pending/late work and cancels current calls without releasing their slots.</summary>
    public void AdvanceEpoch()
    {
        CancellationTokenSource cancellation;
        WorkItem[] pending;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellation = _epochCancellation;
            _epochCancellation = new();
            _epoch++;
            pending = DrainQueueLocked();
        }
        CancelEpoch(cancellation);
        CompletePending(pending);
    }

    public void Dispose()
    {
        CancellationTokenSource cancellation;
        WorkItem[] pending;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            cancellation = _epochCancellation;
            pending = DrainQueueLocked();
        }
        CancelEpoch(cancellation);
        CompletePending(pending);
    }

    private WorkItem[] DrainQueueLocked()
    {
        var pending = _queue.ToArray();
        _queue.Clear();
        foreach (var item in pending) { item.QueueNode = null; item.State = WorkState.Finished; }
        return pending;
    }

    private static void CompletePending(IEnumerable<WorkItem> pending)
    {
        foreach (var item in pending) { item.CompleteCancelled(); item.DisposeResources(); }
    }

    private void CancelQueued(WorkItem item)
    {
        lock (_gate)
        {
            if (item.State != WorkState.Queued) return;
            _queue.Remove(item.QueueNode!);
            item.QueueNode = null;
            item.State = WorkState.Finished;
        }
        item.CompleteCancelled();
        item.DisposeResources();
    }

    private bool TryBegin(WorkItem item)
    {
        lock (_gate)
        {
            if (!IsCurrentLocked(item)) return false;
            item.State = WorkState.Running;
            return true;
        }
    }

    private bool TryCompleteResult<T>(WorkItem<T> item, T result)
    {
        lock (_gate)
        {
            if (!IsCurrentLocked(item)) return false;
            item.CompleteResult(result);
            return true;
        }
    }

    private void CompleteError(WorkItem item, Exception error)
    {
        lock (_gate)
        {
            if (IsCurrentLocked(item)) item.CompleteError(error);
            else item.CompleteCancelled();
        }
    }

    private bool IsCurrentLocked(WorkItem item) => !_disposed && item.Epoch == _epoch && !item.IsCancellationRequested;

    private void Release(WorkItem item)
    {
        var dispatch = new List<WorkItem>();
        var cancelled = new List<WorkItem>();
        lock (_gate)
        {
            item.State = WorkState.Finished;
            _active--;
            while (!_disposed && _active < _maxConcurrency && _queue.First is { } first)
            {
                var next = first.Value;
                _queue.RemoveFirst();
                next.QueueNode = null;
                if (!IsCurrentLocked(next)) { next.State = WorkState.Finished; cancelled.Add(next); }
                else { next.State = WorkState.Scheduled; _active++; dispatch.Add(next); }
            }
        }
        CompletePending(cancelled);
        foreach (var next in dispatch) Dispatch(next);
    }

    private static void Dispatch(WorkItem item) => _ = Task.Run(item.ExecuteAsync);

    private static void CancelEpoch(CancellationTokenSource cancellation) => _ = CancelAndDisposeAsync(cancellation);
    private static async Task CancelAndDisposeAsync(CancellationTokenSource cancellation)
    {
        try { await cancellation.CancelAsync().ConfigureAwait(false); }
        // A host cancellation callback cannot prevent disposal or strand queued requests.
        catch (AggregateException) { }
        finally { cancellation.Dispose(); }
    }

    private enum WorkState { Queued, Scheduled, Running, Finished }

    private abstract class WorkItem
    {
        private readonly object _resourceGate = new();
        private readonly CancellationTokenSource _requestCancellation = new();
        private readonly CancellationToken _externalToken;
        private readonly CancellationToken _epochToken;
        private CancellationTokenRegistration _externalRegistration;
        private CancellationTokenRegistration _epochRegistration;
        private bool _resourcesDisposed;

        protected WorkItem(MarkdownResourceScheduler owner, long epoch, CancellationToken epochToken, CancellationToken externalToken)
        {
            Owner = owner; Epoch = epoch; _epochToken = epochToken; _externalToken = externalToken;
            Token = _requestCancellation.Token;
        }
        protected MarkdownResourceScheduler Owner { get; }
        public long Epoch { get; }
        public CancellationToken Token { get; }
        public bool IsCancellationRequested => _externalToken.IsCancellationRequested || _epochToken.IsCancellationRequested || Token.IsCancellationRequested;
        public WorkState State { get; set; }
        public LinkedListNode<WorkItem>? QueueNode { get; set; }
        public abstract Task ExecuteAsync();
        public abstract void CompleteError(Exception error);
        protected abstract void SetCancelled(CancellationToken token);
        public void CompleteCancelled() => SetCancelled(_externalToken.IsCancellationRequested ? _externalToken :
            _epochToken.IsCancellationRequested ? _epochToken : new CancellationToken(canceled: true));

        public void RegisterCancellation()
        {
            lock (_resourceGate)
            {
                if (_resourcesDisposed) return;
                _externalRegistration = _externalToken.UnsafeRegister(static state => ((WorkItem)state!).RequestCancellation(), this);
                // Register may have invoked cancellation inline and disposed this queued item.
                if (_resourcesDisposed) { _externalRegistration.Unregister(); return; }
                _epochRegistration = _epochToken.UnsafeRegister(static state => ((WorkItem)state!).RequestCancellation(), this);
                if (_resourcesDisposed) _epochRegistration.Unregister();
            }
        }

        private void RequestCancellation()
        {
            // Cancellation callbacks registered by a host may block or throw. CancelAsync marks
            // the token immediately and invokes those callbacks in the background, away from UI.
            _ = CancelRequestAsync();
            Owner.CancelQueued(this);
        }

        private async Task CancelRequestAsync()
        {
            try { await _requestCancellation.CancelAsync().ConfigureAwait(false); }
            catch (AggregateException) { }
            catch (ObjectDisposedException) { } // A just-completed request no longer needs cancellation.
        }

        public void DisposeResources()
        {
            lock (_resourceGate)
            {
                if (_resourcesDisposed) return;
                _resourcesDisposed = true;
                _externalRegistration.Unregister();
                _epochRegistration.Unregister();
            }
            _requestCancellation.Dispose();
        }
    }

    private sealed class WorkItem<T>(MarkdownResourceScheduler owner, long epoch, CancellationToken epochToken,
        CancellationToken externalToken, Func<CancellationToken, ValueTask<T>> action, Action<T>? discardResult)
        : WorkItem(owner, epoch, epochToken, externalToken)
    {
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<T> Task => _completion.Task;
        public void CompleteResult(T result) => _completion.TrySetResult(result);
        public override void CompleteError(Exception error) => _completion.TrySetException(error);
        protected override void SetCancelled(CancellationToken token) => _completion.TrySetCanceled(token);

        public override async Task ExecuteAsync()
        {
            try
            {
                if (!Owner.TryBegin(this)) { CompleteCancelled(); return; }
                var result = await action(Token).ConfigureAwait(false);
                if (!Owner.TryCompleteResult(this, result))
                {
                    if (discardResult is not null) discardResult(result);
                    else if (result is IDisposable disposable) disposable.Dispose();
                    CompleteCancelled();
                }
            }
            catch (OperationCanceledException) { CompleteCancelled(); }
            catch (Exception error) { Owner.CompleteError(this, error); }
            finally { Owner.Release(this); DisposeResources(); }
        }
    }
}
