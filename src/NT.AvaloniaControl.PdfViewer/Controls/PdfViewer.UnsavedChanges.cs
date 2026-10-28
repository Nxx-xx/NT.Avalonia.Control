using Avalonia;

namespace NT.AvaloniaControl.PdfViewer;

public partial class PdfViewer
{
    private (long Version, CancellationToken Token) BeginDocumentChange(CancellationToken token)
    {
        ++_changeRequestVersion;
        _changeRequestCancellation?.Cancel();
        _changeRequestCancellation?.Dispose();
        _changeRequestCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        return (_changeRequestVersion, _changeRequestCancellation.Token);
    }

    private async Task<bool> ConfirmDocumentChangeAsync(PdfDocumentChangeReason reason, long request, CancellationToken token)
    {
        // Normal clean loads / closes remain synchronous until their first native await.
        // Pending edits must finish before inspecting Dirty; dialogs never hold the edit queue.
        while (!_editTail.IsCompleted)
        {
            var pending = _editTail;
            try { await pending.WaitAsync(token); }
            catch (Exception) when (!token.IsCancellationRequested) { }
        }
        token.ThrowIfCancellationRequested();
        if (request != _changeRequestVersion || _disposed) throw new OperationCanceledException(token);
        if (!IsDirty) return true;
        _autoSaveTimer.Stop();
        var context = new PdfUnsavedChangesContext(DocumentName, SavePath, reason);
        var args = new PdfUnsavedChangesEventArgs(context);
        UnsavedChangesRequested?.Invoke(this, args);
        token.ThrowIfCancellationRequested();
        PdfUnsavedChangesDecision decision;
        if (args.Decision is { } supplied) decision = await supplied.WaitAsync(token);
        else if (UnsavedChangesService is { } service) decision = await service.ConfirmAsync(context, token).WaitAsync(token);
        else
        {
            var completion = new TaskCompletionSource<PdfUnsavedChangesDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            _unsavedDecision = completion;
            SetAndRaise(IsUnsavedChangesPromptVisibleProperty, ref _isUnsavedChangesPromptVisible, true);
            try { decision = await completion.Task.WaitAsync(token); }
            finally
            {
                if (ReferenceEquals(_unsavedDecision, completion))
                {
                    _unsavedDecision = null;
                    SetAndRaise(IsUnsavedChangesPromptVisibleProperty, ref _isUnsavedChangesPromptVisible, false);
                }
            }
        }
        token.ThrowIfCancellationRequested();
        if (request != _changeRequestVersion || _disposed) throw new OperationCanceledException(token);
        if (decision == PdfUnsavedChangesDecision.Cancel) return false;
        if (decision == PdfUnsavedChangesDecision.Discard) return true;
        if (decision != PdfUnsavedChangesDecision.Save) return false;
        try
        {
            if (!await SaveAsync(token)) return false;
            // Edits queued while the save dialog / I/O was pending must not be silently lost.
            while (!_editTail.IsCompleted)
            {
                var pending = _editTail;
                try { await pending.WaitAsync(token); }
                catch (Exception) when (!token.IsCancellationRequested) { }
            }
            token.ThrowIfCancellationRequested();
            return !IsDirty && request == _changeRequestVersion && !_disposed;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { return false; } // Keep the open document and its dirty history after a failed save.
    }

    /// <summary>Completes the built-in unsaved changes prompt. No-op when no prompt is pending.</summary>
    public void RespondToUnsavedChanges(PdfUnsavedChangesDecision decision)
    {
        VerifyAccess();
        if (!Enum.IsDefined(decision)) throw new ArgumentOutOfRangeException(nameof(decision));
        _unsavedDecision?.TrySetResult(decision);
    }

    /// <summary>Requests close, respecting unsaved changes. False means canceled / failed to save.
    /// Window hosts should await this before closing their window. Dispose is unconditional resource release.</summary>
    public async Task<bool> RequestCloseDocumentAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        if (_disposed) return true;
        var (request, token) = BeginDocumentChange(cancellationToken);
        if (!await ConfirmDocumentChangeAsync(PdfDocumentChangeReason.Close, request, token)) return false;
        token.ThrowIfCancellationRequested();
        if (request != _changeRequestVersion) throw new OperationCanceledException(token);
        CloseDocumentCore();
        await AwaitDocumentReleasesAsync(_pendingLoads.ToArray());
        return true;
    }
}
