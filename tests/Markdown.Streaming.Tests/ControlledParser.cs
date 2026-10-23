using System.Threading.Channels;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Streaming.Tests;

internal sealed class ControlledParser
{
    private readonly Channel<ParseCall> _calls = Channel.CreateUnbounded<ParseCall>();
    private int _active;
    private int _maximum;

    public int MaximumActive => Volatile.Read(ref _maximum);

    // Deliberately ignores cancellation: generation validation must reject the eventual result.
    public async Task<string> ParseAsync(MarkdownUpdateRequest request, CancellationToken cancellationToken)
    {
        var count = Interlocked.Increment(ref _active);
        Interlocked.Exchange(ref _maximum, Math.Max(count, MaximumActive));
        var call = new ParseCall(request);
        await _calls.Writer.WriteAsync(call);
        try { return await call.Result.Task; }
        finally { Interlocked.Decrement(ref _active); }
    }

    public Task<ParseCall> NextAsync() => _calls.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    public sealed class ParseCall(MarkdownUpdateRequest request)
    {
        public MarkdownUpdateRequest Request { get; } = request;
        public TaskCompletionSource<string> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Finish() => Result.TrySetResult(Request.Source);
        public void Fail(Exception error) => Result.TrySetException(error);
    }
}
