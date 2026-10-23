namespace NT.AvaloniaControl.Markdown;

/// <summary>
/// One thread-safe append session. Chunks are accepted in input-lock acquisition order;
/// callers producing concurrent chunks remain responsible for their business ordering.
/// </summary>
public sealed class MarkdownStreamingSession : IDisposable, IAsyncDisposable
{
    private readonly Action<string> _append;
    private readonly Func<Task<MarkdownUpdateResult>> _complete;
    private readonly Action _cancel;

    internal MarkdownStreamingSession(long id, Action<string> append,
        Func<Task<MarkdownUpdateResult>> complete, Action cancel,
        Task<MarkdownUpdateResult> completion)
    {
        Id = id;
        _append = append;
        _complete = complete;
        _cancel = cancel;
        Completion = completion;
    }

    public long Id { get; }
    public Task<MarkdownUpdateResult> Completion { get; }

    public void Append(string chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        _append(chunk);
    }

    /// <summary>Copies the supplied memory before returning; no borrowed memory is retained.</summary>
    public void Append(ReadOnlyMemory<char> chunk) => _append(chunk.ToString());

    /// <summary>Stops accepting input and waits for the last snapshot's host completion boundary.</summary>
    public Task<MarkdownUpdateResult> CompleteAsync() => _complete();

    /// <summary>Cancels pending work while retaining already accepted source text.</summary>
    public void Cancel() => _cancel();

    /// <summary>Requests normal completion without synchronously waiting on the dispatcher.</summary>
    public void Dispose() => _ = _complete();

    public async ValueTask DisposeAsync() => await _complete().ConfigureAwait(false);
}
