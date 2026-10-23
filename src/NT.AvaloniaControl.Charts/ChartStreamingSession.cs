using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>Sample accounting for a bounded streaming session. Committed means the UI callback returned successfully, not that a frame was presented.</summary>
public readonly record struct ChartStreamingStatistics(long AcceptedSamples, long CommittedSamples,
    long CanceledSamples, long FaultedSamples, long RejectedSamples, long PendingSamples,
    int PendingBatches, int MaximumObservedPendingBatches, int WaitingWriters,
    long AppliedUiBatches, long CoalescedBatches);

/// <summary>
/// Copies accepted batches into a bounded FIFO and combines them in one UI-thread callback.
/// At most two accepted batches, including an executing callback, and one waiting writer are retained.
/// </summary>
/// <remarks>
/// Create on the UI thread. Producer methods and lifetime controls are thread safe.
/// A paused session continues accepting up to its limit. AppendAsync completes on acceptance;
/// retain its input memory unchanged until then. FlushAsync waits for UI commits.
/// The callback is not a transaction: a failure can follow partial caller-owned changes;
/// the session reports that cohort as faulted, stops, and never retries it.
/// </remarks>
public sealed class ChartStreamingSession<T> : IDisposable
{
    private readonly object _gate = new();
    private readonly Queue<T[]> _queue = new();
    private readonly TaskCompletionSource _completion = NewSignal();
    private TaskCompletionSource _progress = NewSignal();
    private Action<IReadOnlyList<T>>? _applyBatch;
    private bool _accepting = true, _paused, _canceled, _disposed, _posted;
    private long _generation;
    private long _accepted, _committed, _canceledSamples, _faulted, _rejected, _pendingSamples;
    private int _pendingBatches, _maximumObserved, _waitingWriters, _inflightBatches;
    private long _uiBatches, _coalesced;
    private Exception? _error;

    public ChartStreamingSession(Action<IReadOnlyList<T>> applyBatch, int maximumBatchSize = 4096,
        int maximumPendingBatches = 2)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentNullException.ThrowIfNull(applyBatch);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBatchSize, 1);
        if (maximumPendingBatches is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(maximumPendingBatches), "Use one or two pending batches.");
        if (maximumBatchSize > Array.MaxLength / maximumPendingBatches)
            throw new ArgumentOutOfRangeException(nameof(maximumBatchSize), "The coalesced batch must fit in one array.");
        _applyBatch = applyBatch;
        MaximumBatchSize = maximumBatchSize;
        MaximumPendingBatches = maximumPendingBatches;
    }

    public int MaximumBatchSize { get; }
    public int MaximumPendingBatches { get; }
    public Task Completion => _completion.Task;
    public bool IsPaused { get { lock (_gate) return _paused; } }
    public bool IsCompleted => _completion.Task.IsCompleted;
    public bool IsDisposed { get { lock (_gate) return _disposed; } }
    public Exception? Error { get { lock (_gate) return _error; } }
    public ChartStreamingStatistics Statistics
    {
        get
        {
            lock (_gate) return new(_accepted, _committed, _canceledSamples, _faulted, _rejected,
                _pendingSamples, _pendingBatches, _maximumObserved, _waitingWriters, _uiBatches, _coalesced);
        }
    }

    /// <summary>Returns false for a full or closed session; rejected samples are counted. Accepted input is copied before return.</summary>
    public bool TryAppend(ReadOnlyMemory<T> items)
    {
        ValidateBatch(items);
        lock (_gate)
        {
            if (!_accepting || _pendingBatches >= MaximumPendingBatches)
            { _rejected += items.Length; return false; }
            AcceptLocked(items);
            return true;
        }
    }

    /// <summary>
    /// Waits for capacity without copying first. A second waiting writer is explicitly rejected;
    /// callers should await each write rather than create an unbounded producer task list.
    /// Cancellation before acceptance leaves no batch in the queue.
    /// </summary>
    public Task AppendAsync(ReadOnlyMemory<T> items, CancellationToken cancellationToken = default)
    {
        ValidateBatch(items);
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
        lock (_gate)
        {
            ThrowIfClosedLocked(items.Length);
            if (_pendingBatches < MaximumPendingBatches)
            { AcceptLocked(items); return Task.CompletedTask; }
            if (_waitingWriters != 0)
            {
                _rejected += items.Length;
                throw new InvalidOperationException("Only one producer may wait for capacity; await the previous write.");
            }
            _waitingWriters++;
            return WaitAndAppendAsync(items, cancellationToken);
        }
    }

    private async Task WaitAndAppendAsync(ReadOnlyMemory<T> items, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                Task signal;
                lock (_gate)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ThrowIfClosedLocked(items.Length);
                    if (_pendingBatches < MaximumPendingBatches)
                    { AcceptLocked(items); return; }
                    signal = _progress.Task;
                }
                await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally { lock (_gate) _waitingWriters--; }
    }

    public void Pause()
    {
        lock (_gate) if (_accepting) _paused = true;
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (_canceled || _disposed || _error is not null) return;
            _paused = false;
            ScheduleLocked();
        }
    }

    /// <summary>Waits for batches accepted before this call. Cancellation cancels this wait, not accepted data.</summary>
    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) return WaitForCommitAsync(_accepted, cancellationToken);
    }

    private async Task WaitForCommitAsync(long target, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task signal;
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_committed >= target) return;
                if (_error is { } error) throw new InvalidOperationException("The streaming callback failed.", error);
                if (_canceled || _disposed) throw new OperationCanceledException("The streaming session was canceled.", new CancellationToken(true));
                signal = _progress.Task;
            }
            await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Closes input and resumes pending commits. Cancellation cancels only the completion wait.</summary>
    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _accepting = false; _paused = false;
            SignalLocked();
            FinishIfIdleLocked();
            ScheduleLocked();
            return cancellationToken.CanBeCanceled ? Completion.WaitAsync(cancellationToken) : Completion;
        }
    }

    /// <summary>Cancels queued batches and invalidates a posted callback. An already executing callback may finish.</summary>
    public void Cancel()
    {
        lock (_gate) CancelLocked();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            CancelLocked();
        }
    }

    private void ValidateBatch(ReadOnlyMemory<T> items)
    {
        if (items.Length > MaximumBatchSize)
            throw new ArgumentOutOfRangeException(nameof(items), "Split input into batches no larger than MaximumBatchSize.");
    }

    private void ThrowIfClosedLocked(int sampleCount)
    {
        if (!_accepting) _rejected += sampleCount;
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_error is { } error) throw new InvalidOperationException("The streaming callback failed.", error);
        if (_canceled) throw new OperationCanceledException("The streaming session was canceled.", new CancellationToken(true));
        if (!_accepting) throw new InvalidOperationException("The streaming input is complete.");
    }

    private void AcceptLocked(ReadOnlyMemory<T> items)
    {
        if (items.IsEmpty) return;
        // Reserve/copy only after capacity is available. No caller array is kept
        // in an accepted batch, including a sliced ReadOnlyMemory.
        var owned = items.ToArray();
        _queue.Enqueue(owned);
        _accepted += owned.Length; _pendingSamples += owned.Length; _pendingBatches++;
        _maximumObserved = Math.Max(_maximumObserved, _pendingBatches);
        ScheduleLocked();
    }

    private void ScheduleLocked()
    {
        if (_posted || _inflightBatches != 0 || _paused || _canceled || _disposed || _error is not null || _queue.Count == 0) return;
        var generation = _generation;
        _posted = true;
        try { Dispatcher.UIThread.Post(() => Drain(generation), DispatcherPriority.Background); }
        catch (Exception exception)
        {
            _posted = false; _error = exception; _accepting = false;
            DiscardQueuedLocked(); SignalLocked(); FinishIfIdleLocked();
        }
    }

    private void Drain(long generation)
    {
        T[] values;
        int batches;
        Action<IReadOnlyList<T>> apply;
        lock (_gate)
        {
            if (generation != _generation) return;
            _posted = false;
            if (_paused || _canceled || _disposed || _error is not null || _queue.Count == 0) return;
            batches = _queue.Count;
            // Keep the queue intact until the bounded merge allocation succeeds.
            // A failed merge must not strand accepted samples outside its ledger.
            try
            {
                var first = _queue.Peek();
                if (batches == 1) values = first;
                else
                {
                    using var enumerator = _queue.GetEnumerator();
                    enumerator.MoveNext(); enumerator.MoveNext();
                    var second = enumerator.Current;
                    values = new T[checked(first.Length + second.Length)];
                    first.CopyTo(values, 0); second.CopyTo(values, first.Length);
                }
            }
            catch (Exception exception)
            {
                _error = exception; _accepting = false;
                DiscardQueuedLocked(); SignalLocked(); FinishIfIdleLocked(); return;
            }
            for (var index = 0; index < batches; index++) _queue.Dequeue();
            _inflightBatches += batches;
            apply = _applyBatch!;
        }
        Exception? failure = null;
        try { apply(values); }
        catch (Exception exception) { failure = exception; }
        lock (_gate)
        {
            _pendingBatches -= batches; _pendingSamples -= values.Length; _inflightBatches -= batches;
            if (failure is null)
            { _committed += values.Length; _uiBatches++; _coalesced += batches - 1; }
            else
            {
                _faulted += values.Length; _error = failure; _accepting = false;
                _generation++; _posted = false; DiscardQueuedLocked();
            }
            SignalLocked(); FinishIfIdleLocked(); ScheduleLocked();
        }
    }

    private void CancelLocked()
    {
        if (_canceled || _completion.Task.IsCompleted) return;
        _accepting = false; _canceled = true; _paused = false;
        _generation++; _posted = false;
        DiscardQueuedLocked(); SignalLocked(); FinishIfIdleLocked();
    }

    private void DiscardQueuedLocked()
    {
        while (_queue.TryDequeue(out var values))
        { _canceledSamples += values.Length; _pendingSamples -= values.Length; _pendingBatches--; }
    }

    private void FinishIfIdleLocked()
    {
        if (_accepting || _pendingBatches != 0 || _inflightBatches != 0) return;
        _applyBatch = null;
        if (_error is { } error) _completion.TrySetException(error);
        else if (_canceled || _disposed) _completion.TrySetCanceled(new CancellationToken(true));
        else _completion.TrySetResult();
    }

    private void SignalLocked()
    {
        var old = _progress;
        _progress = NewSignal();
        old.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
