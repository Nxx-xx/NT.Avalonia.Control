namespace NT.AvaloniaControl.Markdown;

/// <summary>An immutable input captured immediately before a background parse.</summary>
public sealed record MarkdownUpdateRequest(
    string Source,
    long DocumentGeneration,
    long SourceVersion,
    long ConfigurationVersion,
    long SessionId,
    bool IsFinal)
{
    public bool IsStreaming => SessionId != 0;
}

/// <summary>The host's completion boundary after committing a snapshot.</summary>
public enum MarkdownApplyResult
{
    /// <summary>Snapshot and, when visible, its final layout have been applied.</summary>
    Applied,
    /// <summary>The snapshot is applied; a final visible layout has not yet been confirmed.</summary>
    LayoutPending,
    /// <summary>A streaming host has no mounted, valid presentation environment.</summary>
    NoPresentationEnvironment
}

public enum MarkdownUpdateStatus { Completed, Cancelled, Error }

/// <summary>Observable completion, including non-blocking disposal and lifecycle cancellation.</summary>
public sealed record MarkdownUpdateResult(
    MarkdownUpdateStatus Status,
    string Source,
    long SourceVersion,
    bool LayoutPending = false,
    Exception? Error = null);

/// <summary>Parsed data and a generation guard for the host's UI-thread commit.</summary>
public sealed class MarkdownUpdate<T>
{
    private readonly Func<Action, bool> _commit;
    private readonly Func<bool> _isCurrent;

    internal MarkdownUpdate(MarkdownUpdateRequest request, T snapshot,
        Func<Action, bool> commit, Func<bool> isCurrent)
    {
        Request = request;
        Snapshot = snapshot;
        _commit = commit;
        _isCurrent = isCurrent;
    }

    public MarkdownUpdateRequest Request { get; }
    public T Snapshot { get; }
    public bool IsCurrent => _isCurrent();

    /// <summary>
    /// Executes the short synchronous UI mutation only if this generation/configuration/session
    /// is still valid and its source version is newer than the last committed version.
    /// Call inside the host's dispatcher callback. Await layout after this method returns.
    /// </summary>
    public bool TryCommit(Action commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        return _commit(commit);
    }
}
