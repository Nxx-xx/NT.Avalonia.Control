namespace NT.AvaloniaControl.Markdown;

internal static class MarkdownServiceCancellation
{
    internal static async Task CancelAsync(CancellationTokenSource request)
    {
        try { await request.CancelAsync().ConfigureAwait(false); }
        catch (AggregateException) { }
        finally { request.Dispose(); }
    }
}
