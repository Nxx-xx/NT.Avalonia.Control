using Avalonia;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>UI-thread snapshot of the opt-in numerical path preparation pipeline.</summary>
public sealed record ChartBackgroundLayoutInfo(long Generation, bool IsBusy, long SubmittedCount,
    long CompletedCount, long CanceledCount, long StaleCount, long FailedCount, long FallbackCount,
    long CapturedVertexCount, long PublishedVertexCount, int LastWorkerThreadId, string? LastError,
    string? LastFallbackReason);

public sealed partial class CartesianChart
{
    /// <summary>Prepare built-in shaped line and band path parts on bounded shared workers. Default false.</summary>
    /// <remarks>Data mapping, visible ordinal queries, projection, axes, native geometry and paint stay on the UI thread.
    /// Supported paths are absent until a current result is published. Unsupported, animated, detached and
    /// over-budget scenes retain synchronous rendering. This does not move complete chart layout off the UI thread.</remarks>
    public static readonly StyledProperty<bool> UseBackgroundLayoutProperty =
        AvaloniaProperty.Register<CartesianChart, bool>(nameof(UseBackgroundLayout));
    public bool UseBackgroundLayout { get => GetValue(UseBackgroundLayoutProperty); set => SetValue(UseBackgroundLayoutProperty, value); }

    private long _backgroundGeneration, _backgroundSubmitted, _backgroundCompleted, _backgroundCanceled,
        _backgroundStale, _backgroundFailed, _backgroundFallback, _backgroundCaptured, _backgroundPublished;
    private int _backgroundWorkerThread;
    private string? _backgroundError, _backgroundFallbackReason;
    private CancellationTokenSource? _backgroundCancellation;
    private ChartWorkLease? _backgroundLease;
    private TaskCompletionSource<bool>? _backgroundCompletion;
    private ChartSeriesRenderContext[] _backgroundContexts = [];

    /// <summary>Reads scalar counters without mapping or traversing application data. Requires the UI thread.</summary>
    public ChartBackgroundLayoutInfo GetBackgroundLayoutInfo()
    {
        VerifyAccess();
        return new(_backgroundGeneration, _backgroundCancellation is not null, _backgroundSubmitted,
            _backgroundCompleted, _backgroundCanceled, _backgroundStale, _backgroundFailed, _backgroundFallback,
            _backgroundCaptured, _backgroundPublished, _backgroundWorkerThread, _backgroundError, _backgroundFallbackReason);
    }

    /// <summary>Ensures preparation for the current arranged scene. True means a current worker result was published.</summary>
    /// <remarks>False denotes synchronous fallback or cancellation. One caller's cancellation cancels this chart's shared
    /// current request. Canceled work can finish numerically but cannot publish. Requires an attached visible chart and UI thread.</remarks>
    public Task<bool> PrepareBackgroundLayoutAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess(); cancellationToken.ThrowIfCancellationRequested();
        var scene = GetScene();
        if (_backgroundCancellation is null && scene is not null && _backgroundContexts.Length == 0)
            PrepareScenePaths(scene.Snapshots);
        var completion = _backgroundCompletion;
        if (completion is null) return Task.FromResult(false);
        return AwaitPreparation(completion.Task, cancellationToken, _backgroundCancellation);
    }
    private static async Task<bool> AwaitPreparation(Task<bool> completion, CancellationToken cancellation,
        CancellationTokenSource? request)
    {
        using var registration = cancellation.Register(() => { try { request?.Cancel(); } catch (ObjectDisposedException) { } });
        return await completion.ConfigureAwait(false);
    }

    /// <summary>Cancels preparation and returns the current scene to synchronous rendering. Requires the UI thread.</summary>
    public void CancelBackgroundLayout()
    {
        VerifyAccess(); BackgroundLayoutInvalidated();
        if (_scene is { } scene) PrepareStackPaths(scene.Snapshots);
        ReleaseSeriesDrawingCache(); ReleaseSegmentCaches(); RequestRender();
    }

    private bool BackgroundLayoutPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != UseBackgroundLayoutProperty) return false;
        InvalidateScene(clearAxisText: false); RequestRender(); return true;
    }
    private void BackgroundLayoutScenePrepared(ChartScene scene) { }
    private void BackgroundLayoutActivityChanged(bool active)
    {
        if (!active) BackgroundLayoutInvalidated();
        else if (UseBackgroundLayout && _scene is { } scene && _backgroundContexts.Length == 0) PrepareScenePaths(scene.Snapshots);
    }
    private void BackgroundLayoutInvalidated()
    {
        _backgroundGeneration++;
        if (_backgroundCancellation is { } cancellation)
        {
            _backgroundCanceled++; _backgroundCancellation = null; cancellation.Cancel();
            _backgroundCompletion?.TrySetResult(false);
        }
        _backgroundLease?.Dispose(); _backgroundLease = null;
        foreach (var context in _backgroundContexts) ChartLineGeometry.ClearBackgroundPending(context);
        _backgroundContexts = []; _backgroundCompletion = null;
    }

    private static bool SupportsBackgroundGeometry(ChartSeriesRenderContext context) =>
        ChartLineGeometry.IsBuiltIn(context.Series) && context.Series.Renderer is not CartesianSeriesRenderer &&
        context.Projection is CartesianProjection { HasStableTransform: true };

    private void PrepareScenePaths(SeriesSnapshot[] snapshots)
    {
        if (UseBackgroundLayout && !ChartLineGeometry.IsSynchronousPreparation && _backgroundCancellation is null)
        {
            var candidates = snapshots.Where(item => item.Context is { } context && SupportsBackgroundGeometry(context))
                .Select(item => item.Context!).ToArray();
            if (candidates.Length == 0) Fallback("No supported shaped line or band paths.");
            else if (candidates.Length > 128 || candidates.Sum(context => (long)context.Data.Count) > 131_072)
                Fallback("Background capture is limited to 128 supported series and 131072 source vertices per scene.");
            else if (!_attached || !IsEffectivelyVisible) Fallback("The chart is detached or hidden.");
            else if (AnimationTriggers != ChartAnimationTriggers.None && !ReduceMotion)
                Fallback("Animated scenes use synchronous path preparation.");
            else
            {
                // Conservative peak logical weights include captured vertices, worker lists,
                // worst-case step band parts and grouping overhead. Reserve before any O(N)
                // capture/topology work. This is not a native/GPU or whole-process byte cap.
                long estimate = 4096;
                foreach (var context in candidates)
                    estimate = estimate > long.MaxValue - context.Data.Count * 2304L ? long.MaxValue : estimate + context.Data.Count * 2304L;
                if (ChartWorkScheduler.TryReserve(this, estimate, out var lease))
                {
                    try
                    {
                        var groups = candidates.Select((context, index) => (context, index))
                            .Where(item => item.context.Series is IChartLineSeries
                                { Interpolation: ChartLineInterpolation.Smooth, StackMode: not ChartStackMode.None })
                            .GroupBy(item => new StackKey(snapshots.First(snapshot => ReferenceEquals(snapshot.Context, item.context)).XAxis,
                                snapshots.First(snapshot => ReferenceEquals(snapshot.Context, item.context)).YAxis,
                                ((IChartLineSeries)item.context.Series).StackGroup ?? string.Empty,
                                ((IChartLineSeries)item.context.Series).StackMode))
                            .Select(group => group.Select(item => item.index).ToArray()).ToArray();
                        var batch = ChartLineGeometry.CaptureBackground(candidates, groups);
                        var count = batch.Inputs.Sum(input => (long)input.Vertices.Length);
                        _backgroundContexts = candidates; _backgroundCancellation = new(); _backgroundLease = lease;
                        _backgroundCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        _backgroundSubmitted++; _backgroundCaptured += count; _backgroundError = null; _backgroundFallbackReason = null;
                        foreach (var context in candidates) ChartLineGeometry.MarkBackgroundPending(context);
                        RunBackgroundLayout(lease!, batch, candidates, _backgroundGeneration, count,
                            _backgroundCancellation, _backgroundCompletion);
                    }
                    catch (Exception exception)
                    {
                        lease!.Dispose();
                        foreach (var context in candidates) ChartLineGeometry.ClearBackgroundPending(context);
                        _backgroundContexts = []; _backgroundCancellation?.Cancel(); _backgroundCancellation = null;
                        _backgroundCompletion?.TrySetResult(false); _backgroundCompletion = null;
                        _backgroundFailed++; _backgroundError = exception.GetType().Name + ": " + exception.Message;
                        Fallback("Numeric capture failed; the current scene uses synchronous preparation.");
                    }
                }
                else Fallback("Shared worker count or payload budget rejected this scene before numeric capture.");
            }
        }
        PrepareStackPaths(snapshots);
        void Fallback(string reason) { _backgroundFallback++; _backgroundFallbackReason = reason; }
    }

    private async void RunBackgroundLayout(ChartWorkLease lease, ChartLineGeometry.NumericBatch batch,
        ChartSeriesRenderContext[] contexts, long generation, long vertices, CancellationTokenSource cancellation,
        TaskCompletionSource<bool> completion)
    {
        try
        {
            var result = await lease.RunAsync(token => BuildNumericResult(batch, token), cancellation.Token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (generation != _backgroundGeneration || !ReferenceEquals(_backgroundCancellation, cancellation) ||
                    cancellation.IsCancellationRequested || !_attached || !IsEffectivelyVisible || _scene is not { } scene ||
                    !contexts.All(context => scene.Snapshots.Any(snapshot => ReferenceEquals(snapshot.Context, context))))
                {
                    _backgroundStale++;
                    if (ReferenceEquals(_backgroundCancellation, cancellation))
                    {
                        if (cancellation.IsCancellationRequested) _backgroundCanceled++;
                        _backgroundCancellation = null; _backgroundLease = null; _backgroundContexts = [];
                        foreach (var context in contexts) ChartLineGeometry.ClearBackgroundPending(context);
                        if (_scene is { } fallbackScene) PrepareStackPaths(fallbackScene.Snapshots);
                        ReleaseSeriesDrawingCache(); ReleaseSegmentCaches(); RequestRender();
                    }
                    completion.TrySetResult(false); return;
                }
                for (var index = 0; index < contexts.Length; index++) ChartLineGeometry.PublishBackground(contexts[index], result.Figures[index]);
                _backgroundCompleted++; _backgroundPublished += vertices; _backgroundWorkerThread = result.ThreadId;
                _backgroundCancellation = null; _backgroundLease = null;
                ReleaseSeriesDrawingCache(); ReleaseSegmentCaches(); RequestRender(); completion.TrySetResult(true);
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ReferenceEquals(_backgroundCancellation, cancellation))
                {
                    _backgroundCanceled++; _backgroundCancellation = null; _backgroundLease = null;
                    foreach (var context in contexts) ChartLineGeometry.ClearBackgroundPending(context);
                    _backgroundContexts = [];
                    if (_scene is { } fallbackScene) PrepareStackPaths(fallbackScene.Snapshots);
                    ReleaseSeriesDrawingCache(); ReleaseSegmentCaches(); RequestRender();
                }
                completion.TrySetResult(false);
            });
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ReferenceEquals(_backgroundCancellation, cancellation))
                {
                    _backgroundFailed++; _backgroundError = exception.GetType().Name + ": " + exception.Message;
                    _backgroundCancellation = null; _backgroundLease = null;
                    foreach (var context in contexts) ChartLineGeometry.ClearBackgroundPending(context);
                    _backgroundContexts = [];
                    if (_scene is { } fallbackScene) PrepareStackPaths(fallbackScene.Snapshots);
                    ReleaseSeriesDrawingCache(); ReleaseSegmentCaches(); RequestRender();
                }
                completion.TrySetResult(false);
            });
        }
        finally { await Dispatcher.UIThread.InvokeAsync(() => { cancellation.Dispose(); lease.Dispose(); }); }
    }
    private sealed record NumericLayoutResult(IReadOnlyList<ChartLinePathFigure>[] Figures, int ThreadId);
    private static NumericLayoutResult BuildNumericResult(ChartLineGeometry.NumericBatch batch, CancellationToken cancellation) =>
        new(ChartLineGeometry.BuildBackground(batch, cancellation), Environment.CurrentManagedThreadId);

    private IDisposable BeginBackgroundLayoutExport()
    {
        if (!UseBackgroundLayout) return new BackgroundExportScope(this, null, false);
        var scope = ChartLineGeometry.BeginSynchronousScope();
        try
        {
            ReleaseSeriesDrawingCache();
            if (GetScene() is { } scene) PrepareStackPaths(scene.Snapshots);
            return new BackgroundExportScope(this, scope, true);
        }
        catch { scope.Dispose(); throw; }
    }
    private sealed class BackgroundExportScope(CartesianChart chart, IDisposable? scope, bool clear) : IDisposable
    {
        public void Dispose() { scope?.Dispose(); if (clear) chart.ReleaseSeriesDrawingCache(); }
    }
}
