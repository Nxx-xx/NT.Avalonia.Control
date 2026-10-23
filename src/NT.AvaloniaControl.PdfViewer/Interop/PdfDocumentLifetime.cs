namespace NT.AvaloniaControl.PdfViewer.Interop;

/// <summary>Separates admission from eventual native release without blocking a UI or finalizer thread.</summary>
internal sealed class PdfDocumentLifetime
{
    private readonly object _sync = new();
    private readonly Action _release;
    private readonly Func<Action, Task> _scheduleRelease;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly CancellationToken _disposeToken;
    private int _leases;
    private bool _disposeRequested;
    private bool _releaseScheduled;
    private bool _cancellationCompleted;
    private bool _releaseCompleted;
    private bool _completionStarted;
    private Exception? _releaseFailure;

    internal PdfDocumentLifetime(Action release, Func<Action, Task> scheduleRelease)
    {
        _release = release;
        _scheduleRelease = scheduleRelease;
        _disposeToken = _disposeCancellation.Token;
    }

    internal bool IsDisposeRequested
    {
        get { lock (_sync) return _disposeRequested; }
    }

    internal Task Completion => _completion.Task;
    internal CancellationToken DisposeToken => _disposeToken;

    internal IDisposable Acquire()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposeRequested, typeof(PdfDocument));
            _leases++;
            return new Lease(this);
        }
    }

    internal void RequestDispose()
    {
        var schedule = false;
        lock (_sync)
        {
            if (_disposeRequested)
                return;
            _disposeRequested = true;
            if (_leases == 0 && !_releaseScheduled)
                schedule = _releaseScheduled = true;
        }
        // This only signals cooperative cancellation. A running operation keeps its
        // lease until native teardown completes, even if the caller stops awaiting.
        try
        {
            _disposeCancellation.Cancel();
        }
        catch (Exception exception)
        {
            lock (_sync) _releaseFailure ??= exception;
        }
        finally
        {
            lock (_sync) _cancellationCompleted = true;
        }
        if (schedule)
            ScheduleRelease();
        TryCompleteRelease();
    }

    private void ReleaseLease()
    {
        var schedule = false;
        lock (_sync)
        {
            _leases--;
            if (_leases == 0 && _disposeRequested && !_releaseScheduled)
                schedule = _releaseScheduled = true;
        }
        if (schedule)
            ScheduleRelease();
    }

    private void ScheduleRelease()
    {
        try
        {
            _ = ObserveReleaseAsync(_scheduleRelease(_release));
        }
        catch (Exception exception)
        {
            FinishRelease(exception);
        }
    }

    private async Task ObserveReleaseAsync(Task release)
    {
        try
        {
            await release.ConfigureAwait(false);
            FinishRelease(null);
        }
        catch (Exception exception)
        {
            FinishRelease(exception);
        }
    }

    private void FinishRelease(Exception? failure)
    {
        lock (_sync)
        {
            _releaseCompleted = true;
            _releaseFailure ??= failure;
        }
        TryCompleteRelease();
    }

    private void TryCompleteRelease()
    {
        Exception? failure;
        lock (_sync)
        {
            // A final lease can finish on another thread while Cancel is still unwinding.
            // Dispose the source only after both sides have finished touching it.
            if (!_releaseCompleted || !_cancellationCompleted || _completionStarted)
                return;
            _completionStarted = true;
            failure = _releaseFailure;
        }
        _disposeCancellation.Dispose();
        if (failure is null)
            _completion.TrySetResult();
        else
            _completion.TrySetException(failure);
    }

    private sealed class Lease(PdfDocumentLifetime owner) : IDisposable
    {
        private PdfDocumentLifetime? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ReleaseLease();
    }
}
