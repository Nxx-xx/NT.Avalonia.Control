using NT.AvaloniaControl.PdfViewer.Interop;

namespace NT.AvaloniaControl.PdfViewer;

public sealed partial class PdfDocument
{
    /// <summary>
    /// Captures a caller-owned stream once for viewer password retries. Each native
    /// document gets its own pin or file handle; disposing this source never closes
    /// the caller's stream or invalidates an already opened document.
    /// </summary>
    internal sealed class StreamSource(Stream source) : IDisposable
    {
        private readonly object _sync = new();
        private Task<StreamInput>? _capture;
        private bool _disposed;

        internal string? StagedPath
        {
            get { lock (_sync) return _capture is { IsCompletedSuccessfully: true } ? _capture.Result.Staged?.StagedPath : null; }
        }

        internal async Task<PdfDocument> OpenAsync(string? password, CancellationToken token)
        {
            var correlationId = BeginLoad();
            TaskCompletionSource<StreamInput>? capture = null;
            Task<StreamInput> pending;
            lock (_sync)
            {
                token.ThrowIfCancellationRequested();
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_capture is null)
                {
                    // Publish before calling arbitrary Stream methods: these can
                    // synchronously reenter the viewer and close the source.
                    capture = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    _capture = capture.Task;
                }
                pending = _capture;
            }
            if (capture is not null) CaptureAsync(capture, token, correlationId);
            var input = await pending.ConfigureAwait(false);
            Task<PdfDocument> loading;
            lock (_sync)
            {
                token.ThrowIfCancellationRequested();
                ObjectDisposedException.ThrowIf(_disposed, this);
                loading = input.Bytes is { } bytes
                    ? LoadOwnedAsync(bytes, password, token, correlationId)
                    : LoadInputAsync(PdfFileInput.OpenFile(input.Staged!.StagedPath!), password, token, correlationId);
            }
            return await CompleteLoadAsync(loading, correlationId).ConfigureAwait(false);
        }

        private async void CaptureAsync(TaskCompletionSource<StreamInput> completion, CancellationToken token, string correlationId)
        {
            StreamInput? captured = null;
            try
            {
                token.ThrowIfCancellationRequested();
                var started = PdfPerformanceSession.Now;
                if (source.CanSeek && source.Length - source.Position is > 0 and <= int.MaxValue)
                {
                    var bytes = GC.AllocateUninitializedArray<byte>((int)(source.Length - source.Position));
                    try { await source.ReadExactlyAsync(bytes, token).ConfigureAwait(false); }
                    catch (EndOfStreamException) { throw new PdfDocumentException(PdfViewerStrings.ErrorLoadInvalid, 3); }
                    captured = new(bytes, null);
                }
                else
                {
                    // Non-seekable/large input retains the existing bounded-memory
                    // staging path. The owner keeps the temp copy between attempts.
                    captured = new(null, await PdfFileInput.StageStreamAsync(source, token).ConfigureAwait(false));
                }
                RecordInputRead(correlationId, started);
                lock (_sync)
                {
                    token.ThrowIfCancellationRequested();
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    completion.SetResult(captured);
                    captured = null; // The completed task now owns the captured input.
                }
            }
            catch (OperationCanceledException error) { completion.TrySetCanceled(error.CancellationToken); }
            catch (Exception error) { completion.TrySetException(error); }
            finally { captured?.Dispose(); }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                if (_capture is { IsCompletedSuccessfully: true }) _capture.Result.Dispose();
                _capture = null;
                // An in-flight capture observes _disposed and disposes its own result.
            }
        }

        private sealed record StreamInput(byte[]? Bytes, PdfFileInput? Staged) : IDisposable
        {
            public void Dispose() => Staged?.Dispose();
        }
    }
}
