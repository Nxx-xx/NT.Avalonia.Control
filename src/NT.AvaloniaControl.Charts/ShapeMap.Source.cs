using System.Text;
using Avalonia.Platform;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

public partial class ShapeMap
{
    private MapGeometrySnapshot? _loadedSnapshot;
    private CancellationTokenSource? _sourceCancellation;
    private long _sourceGeneration;
    private MapSourceState _sourceState, _publishedSourceState;
    private Exception? _sourceError, _publishedSourceError;
    /// <summary>Reads only an explicitly supplied local file/avares URI. Latest generation wins; cancellation never publishes a partial map.</summary>
    public async Task<bool> LoadSourceAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); cancellationToken.ThrowIfCancellationRequested();
        CancelSourceCore(false); var generation = ++_sourceGeneration; var uri = SourceUri; var id = JsonIdProperty; var label = JsonLabelProperty;
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); _sourceCancellation = linked;
        _loadedSnapshot = null; _snapshot = null; _projected = null; _sourceError = null; _geometryDirty = true; DirtyData();
        _sourceState = MapSourceState.Loading; PublishSourceStatus();
        try
        {
            var candidate = await Task.Run(async () =>
            {
                linked.Token.ThrowIfCancellationRequested();
                if (uri is null || !uri.IsAbsoluteUri || !(uri.IsFile || uri.Scheme == "avares")) throw new ArgumentException("Map SourceUri must be an absolute local file or avares URI.");
                using var stream = uri.IsFile ? new FileStream(uri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true) : AssetLoader.Open(uri);
                if (stream.CanSeek && stream.Length > ChartMaps.MaximumJsonBytes) throw new ArgumentOutOfRangeException(nameof(SourceUri), "Map source exceeds the byte budget.");
                using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true, 8192, false);
                var text = new StringBuilder(); var buffer = new char[8192];
                while (true)
                {
                    var count = await reader.ReadAsync(buffer.AsMemory(), linked.Token).ConfigureAwait(false); if (count == 0) break;
                    if (text.Length > ChartMaps.MaximumJsonBytes - count) throw new ArgumentOutOfRangeException(nameof(SourceUri), "Map source exceeds the text budget.");
                    text.Append(buffer, 0, count);
                }
                return ChartMaps.ReadGeoJson(text.ToString(), id, label, linked.Token);
            }, linked.Token).ConfigureAwait(false);
            var accepted = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed || generation != _sourceGeneration || linked.IsCancellationRequested) return;
                _loadedSnapshot = candidate; _sourceError = null; _sourceState = MapSourceState.Completed; _sourceDirty = true; _geometryDirty = true; DirtyData(); PublishSourceStatus(); accepted = generation == _sourceGeneration && !linked.IsCancellationRequested;
            });
            return accepted;
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { if (generation == _sourceGeneration) { _sourceState = MapSourceState.Canceled; PublishSourceStatus(); } });
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            return false;
        }
        catch (Exception error)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (generation != _sourceGeneration || _disposed) return;
                _loadedSnapshot = null; _snapshot = null; _projected = null; _sourceError = error; _sourceState = MapSourceState.Failed; _geometryDirty = true; DirtyData(); PublishSourceStatus();
            });
            return false;
        }
        finally { await Dispatcher.UIThread.InvokeAsync(() => { if (ReferenceEquals(_sourceCancellation, linked)) _sourceCancellation = null; }); linked.Dispose(); }
    }
    public void CancelSourceLoad() { VerifyAccess(); CancelSourceCore(true); }
    private void CancelSourceCore(bool report)
    {
        ++_sourceGeneration; _sourceCancellation?.Cancel(); _sourceCancellation = null;
        if (_sourceState == MapSourceState.Loading || report) { _sourceState = MapSourceState.Canceled; PublishSourceStatus(); }
    }
    private void PublishSourceStatus()
    {
        var state = _sourceState; var error = _sourceError;
        if (_publishedSourceState != state) { var previous = _publishedSourceState; _publishedSourceState = state; RaisePropertyChanged(SourceStateProperty, previous, state); }
        if (_sourceState == state && ReferenceEquals(error, _sourceError) && !ReferenceEquals(error, _publishedSourceError)) { var previous = _publishedSourceError; _publishedSourceError = error; RaisePropertyChanged(SourceErrorProperty, previous, error); }
    }
}
