using Avalonia.Threading;
using System.Diagnostics;

namespace NT.AvaloniaControl.Markdown;

public partial class Markdown
{
    private MarkdownUpdateScheduler<ParsedMarkdown> _updates = null!;
    private Task<MarkdownUpdateResult>? _lastUpdate;
    private long _configurationVersion;
    private bool _replacePending;
    private MarkdownParser? _activeParser;

    private void InitializeUpdates()
    {
        _activeParser = _parser;
        var ownerReference = new WeakReference<Markdown>(this);
        _updates = new MarkdownUpdateScheduler<ParsedMarkdown>(
            (request, cancellationToken) =>
            {
                if (!ownerReference.TryGetTarget(out var owner))
                    return Task.FromException<ParsedMarkdown>(new ObjectDisposedException(nameof(Markdown)));
                var parser = owner._parser;
                var previous = Volatile.Read(ref owner._replacePending) ? null : Volatile.Read(ref owner._document);
                return Task.Run(() =>
                {
                    var watch = Stopwatch.StartNew();
                    var snapshot = parser.Parse(request.Source, previous);
                    // PRIVATE EXPERIMENT: retain this pure DTO preparation in the
                    // background metric. Replacements do not migrate selection.
                    if (previous is not null && !cancellationToken.IsCancellationRequested)
                        _ = MarkdownSnapshotIdentityIndex.Get(snapshot);
                    return new ParsedMarkdown(snapshot, previous, watch.Elapsed.TotalMilliseconds);
                }, cancellationToken);
            },
            async (update, cancellationToken) =>
            {
                var result = MarkdownApplyResult.NoPresentationEnvironment;
                await MarkdownUiDispatcher.InvokeAsync(() =>
                {
                    if (!ownerReference.TryGetTarget(out var owner)) return;
                    result = MarkdownApplyResult.Applied;
                    var watch = Stopwatch.StartNew();
                    var layoutMilliseconds = 0d;
                    var committed = update.TryCommit(() =>
                    {
                        var next = owner._replacePending || ReferenceEquals(owner.Document, update.Snapshot.Previous) ? update.Snapshot.Document : MarkdownDocumentMatcher.Reconcile(owner.Document, update.Snapshot.Document);
                        var replace = owner._replacePending;
                        owner._replacePending = false;
                        layoutMilliseconds = owner.ApplyDocument(next, replace, update.Request.Source, () => update.IsCurrent, out var followingLayoutPending);
                        if (update.Request.IsStreaming)
                            result = !owner.HasPresentationEnvironment ? MarkdownApplyResult.NoPresentationEnvironment : !owner.IsEffectivelyVisible || followingLayoutPending ? MarkdownApplyResult.LayoutPending : MarkdownApplyResult.Applied;
                        else if (!owner.HasPresentationEnvironment || !owner.IsEffectivelyVisible || followingLayoutPending)
                            result = MarkdownApplyResult.LayoutPending;
                    });
                    watch.Stop();
                    if (committed && update.IsCurrent) owner.UpdateMeasured?.Invoke(owner, new(update.Request.SourceVersion, update.Request.Source.Length, update.Snapshot.ParseMilliseconds, Math.Max(0, watch.Elapsed.TotalMilliseconds - layoutMilliseconds), layoutMilliseconds));
                });
                return result;
            });
    }

    private void ReconfigureUpdates()
    {
        if (_updates is null) return;
        Dispatcher.UIThread.VerifyAccess();
        _configurationVersion++;
        _resourceRequests.AdvanceEpoch();
        _activeParser = _parser;
        _replacePending = false;
        var generation = _updates.DocumentGeneration;
        var source = _updates.SourceText;
        _syncTextValue = source; _syncText = true;
        try { SetCurrentValue(TextProperty, source); }
        finally { _syncText = false; }
        if (generation != _updates.DocumentGeneration) return;
        _presenter?.ResetPresentation();
        _lastUpdate = _updates.Reconfigure(_configurationVersion);
        _ = ObserveUpdateAsync(_lastUpdate);
    }

    private void RequestParse(bool replace)
    {
        if (_updates is null) return;
        Dispatcher.UIThread.VerifyAccess();
        if (replace) { _replacePending = true; _resourceRequests.AdvanceEpoch(); CancelInteraction(); ResetLinkPresentation(); ResetReadingLayoutTracking(); ClearSelection(); }
        if (!ReferenceEquals(_activeParser, _parser))
        {
            _configurationVersion++;
            _activeParser = _parser;
        }
        _lastUpdate = _updates.ReplaceText(Text, _configurationVersion);
        _ = ObserveUpdateAsync(_lastUpdate);
    }
    private Task ObserveUpdateAsync(Task<MarkdownUpdateResult> task) =>
        ObserveUpdateAsync(new WeakReference<Markdown>(this), task, _updates.DocumentGeneration);

    private static async Task ObserveUpdateAsync(WeakReference<Markdown> ownerReference, Task<MarkdownUpdateResult> task, long generation)
    {
        var result = await task.ConfigureAwait(false);
        if (result.Status == MarkdownUpdateStatus.Error && result.Error is { } error)
            await MarkdownUiDispatcher.InvokeAsync(() =>
            {
                // Failure invalidates its own scheduler generation once. A later
                // replacement/session/detach advances it again and rejects this error.
                if (ownerReference.TryGetTarget(out var owner) && ReferenceEquals(task, owner._lastUpdate) && generation + 1 == owner._updates.DocumentGeneration)
                    owner.ReportError("Update", error);
            });
    }
    private void SuspendUpdates()
    {
        _resourceRequests.AdvanceEpoch();
        var source = _updates.SourceText;
        _updates.Invalidate();
        _syncTextValue = source;
        _syncText = true;
        try { SetCurrentValue(TextProperty, source); }
        finally { _syncText = false; }
    }
    private void ResumeUpdates() { if (Document.Source != (Text ?? string.Empty)) RequestParse(false); }

    /// <summary>Waits for the last complete Text replacement. Streaming uses its own completion.</summary>
    public Task<MarkdownUpdateResult> WhenUpdatedAsync() => _lastUpdate ?? Task.FromResult(new MarkdownUpdateResult(MarkdownUpdateStatus.Completed, Text ?? string.Empty, 0));

    public MarkdownStreamingSession BeginStreaming()
    {
        Dispatcher.UIThread.VerifyAccess();
        return _updates.BeginStreaming();
    }
    public void AppendText(string chunk, TimeSpan? mergeInterval = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        _updates.AppendText(chunk, mergeInterval);
    }
    public Task<MarkdownUpdateResult> CompleteStreamingAsync() => _updates.CompleteStreamingAsync();
    public void CancelStreaming()
    {
        Dispatcher.UIThread.VerifyAccess();
        var source = _updates.SourceText;
        _updates.CancelStreaming();
        _syncTextValue = source;
        _syncText = true;
        try { SetCurrentValue(TextProperty, source); }
        finally { _syncText = false; }
    }
    private sealed record ParsedMarkdown(MarkdownDocumentSnapshot Document, MarkdownDocumentSnapshot? Previous, double ParseMilliseconds);
}
