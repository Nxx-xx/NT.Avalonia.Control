using System.Text;
using System.Diagnostics;

namespace NT.AvaloniaControl.Markdown;

/// <summary>
/// Bounded, full-document background parser: at most one parse/apply operation and one
/// coalesced pending input. New append input never invalidates a completed valid prefix.
/// The host owns its dispatcher, layout boundary and binding-preserving Text synchronization.
/// </summary>
public sealed class MarkdownUpdateScheduler<T> : IDisposable
{
    public static TimeSpan DefaultMergeInterval { get; } = TimeSpan.FromMilliseconds(33);
    public const int DefaultMaximumTextLength = 16 * 1024 * 1024;

    private readonly object _gate = new();
    private readonly Func<MarkdownUpdateRequest, CancellationToken, Task<T>> _parseAsync;
    private readonly Func<MarkdownUpdate<T>, CancellationToken, Task<MarkdownApplyResult>> _applyAsync;
    private readonly TimeSpan _mergeInterval;
    private readonly int _maximumTextLength;
    private StringBuilder _buffer = new();
    private CancellationTokenSource _generationCancellation = new();
    private TaskCompletionSource<MarkdownUpdateResult>? _replacementCompletion;
    private Task<MarkdownUpdateResult>? _latestCompletion;
    private SessionState? _session;
    private PendingInput? _pending;
    private bool _running;
    private bool _disposed;
    private long _generation = 1;
    private long _sourceVersion;
    private long _configurationVersion;
    private long _sessionId;
    private long _lastAppliedVersion = -1;
    private MarkdownApplyResult _lastApplyResult;

    public MarkdownUpdateScheduler(
        Func<MarkdownUpdateRequest, CancellationToken, Task<T>> parseAsync,
        Func<MarkdownUpdate<T>, CancellationToken, Task<MarkdownApplyResult>> applyAsync,
        TimeSpan? mergeInterval = null,
        int maximumTextLength = DefaultMaximumTextLength)
    {
        ArgumentNullException.ThrowIfNull(parseAsync);
        ArgumentNullException.ThrowIfNull(applyAsync);
        if (maximumTextLength < 1) throw new ArgumentOutOfRangeException(nameof(maximumTextLength));
        _parseAsync = parseAsync;
        _applyAsync = applyAsync;
        _mergeInterval = ValidateInterval(mergeInterval ?? DefaultMergeInterval);
        _maximumTextLength = maximumTextLength;
    }

    public string SourceText { get { lock (_gate) return _buffer.ToString(); } }
    public bool HasActiveSession { get { lock (_gate) return _session is not null; } }
    public bool HasExplicitSession { get { lock (_gate) return _session is { IsExplicit: true }; } }
    public long DocumentGeneration { get { lock (_gate) return _generation; } }
    public long SourceVersion { get { lock (_gate) return _sourceVersion; } }
    public long LastAppliedSourceVersion { get { lock (_gate) return _lastAppliedVersion; } }
    public int ActiveParseCount { get { lock (_gate) return _running ? 1 : 0; } }
    public int PendingRequestCount { get { lock (_gate) return _pending is null ? 0 : 1; } }

    /// <summary>External complete replacement always wins over an existing session.</summary>
    public Task<MarkdownUpdateResult> ReplaceText(string? text, long configurationVersion = 0)
    {
        text ??= string.Empty;
        ValidateLength(text.Length);
        CancellationTokenSource previous;
        Task<MarkdownUpdateResult> completion;
        lock (_gate)
        {
            ThrowIfDisposed();
            previous = InvalidateLocked();
            _configurationVersion = configurationVersion;
            _buffer = new StringBuilder(text);
            _sourceVersion++;
            _replacementCompletion = NewCompletion();
            completion = _replacementCompletion.Task;
            _latestCompletion = completion;
            QueueLocked(true, TimeSpan.Zero);
        }
        CancelAndDispose(previous);
        return completion;
    }

    /// <summary>Reparses the retained source under a new immutable host configuration.</summary>
    public Task<MarkdownUpdateResult> Reconfigure(long configurationVersion)
    {
        CancellationTokenSource previous;
        Task<MarkdownUpdateResult> completion;
        lock (_gate)
        {
            ThrowIfDisposed();
            previous = InvalidateLocked();
            _configurationVersion = configurationVersion;
            _replacementCompletion = NewCompletion();
            completion = _replacementCompletion.Task;
            _latestCompletion = completion;
            QueueLocked(true, TimeSpan.Zero);
        }
        CancelAndDispose(previous);
        return completion;
    }

    public MarkdownStreamingSession BeginStreaming()
    {
        CancellationTokenSource previous;
        MarkdownStreamingSession result;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_session is not null) throw new InvalidOperationException("A streaming session is already active.");
            previous = InvalidateLocked();
            var state = new SessionState(++_sessionId, _generation, true, _buffer, _sourceVersion);
            _session = state;
            _latestCompletion = state.Completion.Task;
            result = CreateSession(state);
        }
        CancelAndDispose(previous);
        return result;
    }

    /// <summary>UI-host convenience API. Explicit sessions cannot be mixed with this implicit session.</summary>
    public void AppendText(string chunk, TimeSpan? mergeInterval = null)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        var interval = ValidateInterval(mergeInterval ?? _mergeInterval);
        CancellationTokenSource? previous = null;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_session is { IsExplicit: true })
                throw new InvalidOperationException("AppendText cannot be mixed with an explicit streaming session.");
            ValidateLength((long)_buffer.Length + chunk.Length);
            if (chunk.Length == 0) return;
            if (_session is null)
            {
                previous = InvalidateLocked();
                _session = new SessionState(++_sessionId, _generation, false, _buffer, _sourceVersion);
                _latestCompletion = _session.Completion.Task;
            }
            AppendLocked(_session, chunk, interval);
        }
        if (previous is not null) CancelAndDispose(previous);
    }

    public Task<MarkdownUpdateResult> CompleteStreamingAsync()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_session is not null) return CompleteLocked(_session);
            return _latestCompletion ?? Task.FromResult(CurrentResult(MarkdownUpdateStatus.Completed));
        }
    }

    public void CancelStreaming()
    {
        CancellationTokenSource? previous = null;
        lock (_gate)
        {
            if (_disposed) return;
            if (_session is not null) previous = InvalidateLocked();
        }
        if (previous is not null) CancelAndDispose(previous);
    }

    /// <summary>Invalidates work on detach/template removal, retaining accepted source for remount.</summary>
    public void Invalidate()
    {
        CancellationTokenSource previous;
        lock (_gate)
        {
            if (_disposed) return;
            previous = InvalidateLocked();
        }
        CancelAndDispose(previous);
    }

    public void Dispose()
    {
        CancellationTokenSource previous;
        lock (_gate)
        {
            if (_disposed) return;
            previous = InvalidateLocked();
            _disposed = true;
            _generationCancellation.Dispose();
        }
        CancelAndDispose(previous);
    }

    private MarkdownStreamingSession CreateSession(SessionState state) => new(state.Id,
        chunk => Append(state, chunk), () => Complete(state), () => Cancel(state), state.Completion.Task);

    private void Append(SessionState state, string chunk)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            AppendLocked(state, chunk, _mergeInterval);
        }
    }

    private void AppendLocked(SessionState state, string chunk, TimeSpan interval)
    {
        if (!ReferenceEquals(_session, state) || !state.Accepting)
            throw new InvalidOperationException("The streaming session has ended or was cancelled.");
        ValidateLength((long)_buffer.Length + chunk.Length);
        if (chunk.Length == 0) return;
        _buffer.Append(chunk);
        state.SourceVersion = ++_sourceVersion;
        QueueLocked(false, interval);
    }

    private Task<MarkdownUpdateResult> Complete(SessionState state)
    {
        lock (_gate) return CompleteLocked(state);
    }

    private Task<MarkdownUpdateResult> CompleteLocked(SessionState state)
    {
        if (!ReferenceEquals(_session, state) || !state.Accepting) return state.Completion.Task;
        state.Accepting = false;
        if (_lastAppliedVersion == state.SourceVersion && !_running)
        {
            FinishSessionLocked(state, _lastApplyResult);
        }
        else
        {
            // Finishing removes the merge delay, including any latest appended tail.
            QueueLocked(true, TimeSpan.Zero);
        }
        return state.Completion.Task;
    }

    private void Cancel(SessionState state)
    {
        CancellationTokenSource? previous = null;
        lock (_gate)
            if (ReferenceEquals(_session, state)) previous = InvalidateLocked();
        if (previous is not null) CancelAndDispose(previous);
    }

    private void QueueLocked(bool final, TimeSpan interval)
    {
        // Preserve the first outstanding arrival time: sustained input must not push the deadline out.
        var readyAt = final ? 0 : _pending?.ReadyAt ?? Stopwatch.GetTimestamp() + (long)(interval.TotalSeconds * Stopwatch.Frequency);
        _pending = new PendingInput(_generation, _sourceVersion, _configurationVersion,
            _session?.Id ?? 0, final, readyAt);
        if (_running) return;
        _running = true;
        _ = Task.Run(ProcessAsync);
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            MarkdownUpdateRequest? request = null;
            CancellationToken token;
            TimeSpan delay;
            lock (_gate)
            {
                if (_disposed || _pending is null)
                {
                    _running = false;
                    return;
                }
                if (_pending.Final && _pending.SourceVersion == _lastAppliedVersion &&
                    _session is { Accepting: false } completedState)
                {
                    // A prefix finished just before CompleteAsync acquired the input lock.
                    _pending = null;
                    FinishSessionLocked(completedState, _lastApplyResult);
                    continue;
                }
                token = _generationCancellation.Token;
                delay = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), _pending.ReadyAt);
                if (delay <= TimeSpan.Zero)
                {
                    var pending = _pending;
                    _pending = null;
                    request = new MarkdownUpdateRequest(_buffer.ToString(), pending.Generation,
                        pending.SourceVersion, pending.ConfigurationVersion, pending.SessionId, pending.Final);
                }
            }
            if (request is null)
            {
                // A completion request may shorten the deadline; bounded slices recheck it promptly.
                try { await Task.Delay(delay < TimeSpan.FromMilliseconds(10) ? delay : TimeSpan.FromMilliseconds(10), token).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                continue;
            }

            try
            {
                var snapshot = await _parseAsync(request, token).ConfigureAwait(false);
                if (!IsCurrent(request)) continue;
                var update = new MarkdownUpdate<T>(request, snapshot,
                    commit => TryCommit(request, commit), () => IsCurrent(request));
                var applied = await _applyAsync(update, token).ConfigureAwait(false);
                CancellationTokenSource? failedCancellation = null;
                lock (_gate)
                {
                    if (!IsCurrentLocked(request)) continue;
                    if (applied == MarkdownApplyResult.NoPresentationEnvironment)
                    {
                        failedCancellation = FailLocked(request,
                            new InvalidOperationException("The streaming host has no mounted, valid presentation environment."));
                    }
                    else if (_lastAppliedVersion < request.SourceVersion)
                    {
                        failedCancellation = FailLocked(request,
                            new InvalidOperationException("The host returned an applied result without committing the snapshot."));
                    }
                    else
                    {
                        _lastApplyResult = applied;
                        if (_session is { Accepting: false } state && state.SourceVersion == request.SourceVersion)
                        {
                            // Completion during an in-flight prefix reuses that final version.
                            _pending = null;
                            FinishSessionLocked(state, applied);
                        }
                        else if (!request.IsStreaming && request.IsFinal)
                        {
                            _replacementCompletion?.TrySetResult(CurrentResult(MarkdownUpdateStatus.Completed,
                                applied == MarkdownApplyResult.LayoutPending));
                        }
                    }
                }
                if (failedCancellation is not null) CancelAndDispose(failedCancellation);
            }
            catch (Exception error)
            {
                CancellationTokenSource? failedCancellation = null;
                lock (_gate)
                    if (IsCurrentLocked(request)) failedCancellation = FailLocked(request, error);
                if (failedCancellation is not null) CancelAndDispose(failedCancellation);
            }
        }
    }

    private bool IsCurrent(MarkdownUpdateRequest request)
    {
        lock (_gate) return IsCurrentLocked(request);
    }

    private bool IsCurrentLocked(MarkdownUpdateRequest request) => !_disposed &&
        request.DocumentGeneration == _generation && request.ConfigurationVersion == _configurationVersion &&
        request.SessionId == (_session?.Id ?? _sessionIdForCompletedGeneration);

    // The final apply stays valid until another generation starts, even after the session result completes.
    private long _sessionIdForCompletedGeneration;

    private bool TryCommit(MarkdownUpdateRequest request, Action commit)
    {
        lock (_gate)
        {
            if (!IsCurrentLocked(request) || request.SourceVersion <= _lastAppliedVersion) return false;
            commit();
            // Binding callbacks can synchronously request an external replacement.
            if (!IsCurrentLocked(request)) return false;
            _lastAppliedVersion = request.SourceVersion;
            return true;
        }
    }

    private void FinishSessionLocked(SessionState state, MarkdownApplyResult applied)
    {
        state.Completion.TrySetResult(new MarkdownUpdateResult(MarkdownUpdateStatus.Completed,
            state.Buffer.ToString(), state.SourceVersion, applied == MarkdownApplyResult.LayoutPending));
        _sessionIdForCompletedGeneration = state.Id;
        _session = null;
    }

    private CancellationTokenSource FailLocked(MarkdownUpdateRequest request, Exception error)
    {
        var result = new MarkdownUpdateResult(MarkdownUpdateStatus.Error, _buffer.ToString(), _sourceVersion, Error: error);
        _session?.Completion.TrySetResult(result);
        _replacementCompletion?.TrySetResult(result);
        return InvalidateLocked();
    }

    private CancellationTokenSource InvalidateLocked()
    {
        var result = CurrentResult(MarkdownUpdateStatus.Cancelled);
        if (_session is not null)
        {
            _session.Accepting = false;
            _session.Completion.TrySetResult(result);
            _session = null;
        }
        _replacementCompletion?.TrySetResult(result);
        _replacementCompletion = null;
        _pending = null;
        _generation++;
        _lastAppliedVersion = -1;
        _sessionIdForCompletedGeneration = 0;
        var previous = _generationCancellation;
        _generationCancellation = new CancellationTokenSource();
        return previous;
    }

    private MarkdownUpdateResult CurrentResult(MarkdownUpdateStatus status, bool layoutPending = false) =>
        new(status, _buffer.ToString(), _sourceVersion, layoutPending);

    private static TaskCompletionSource<MarkdownUpdateResult> NewCompletion() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TimeSpan ValidateInterval(TimeSpan interval)
    {
        if (interval < TimeSpan.Zero || interval > TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(interval), "Merge interval must be between zero and one second.");
        return interval;
    }

    private void ValidateLength(long length)
    {
        if (length > _maximumTextLength)
            throw new InvalidOperationException($"The source exceeds the configured {_maximumTextLength} UTF-16 character limit.");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static void CancelAndDispose(CancellationTokenSource cancellation)
    {
        try { cancellation.Cancel(); }
        catch (AggregateException) { /* A consumer's cancellation callback must not strand the bounded worker. */ }
        finally { cancellation.Dispose(); }
    }

    private sealed record PendingInput(long Generation, long SourceVersion, long ConfigurationVersion,
        long SessionId, bool Final, long ReadyAt);

    private sealed class SessionState(long id, long generation, bool isExplicit, StringBuilder buffer, long sourceVersion)
    {
        public long Id { get; } = id;
        public long Generation { get; } = generation;
        public bool IsExplicit { get; } = isExplicit;
        public StringBuilder Buffer { get; } = buffer;
        public long SourceVersion { get; set; } = sourceVersion;
        public bool Accepting { get; set; } = true;
        public TaskCompletionSource<MarkdownUpdateResult> Completion { get; } = NewCompletion();
    }
}
