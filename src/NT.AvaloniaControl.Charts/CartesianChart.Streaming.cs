using Avalonia;
using Avalonia.Data;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<bool> FollowLatestDataProperty =
        AvaloniaProperty.Register<CartesianChart, bool>(nameof(FollowLatestData), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<double> FollowLatestWindowProperty =
        AvaloniaProperty.Register<CartesianChart, double>(nameof(FollowLatestWindow), 100,
            validate: value => double.IsFinite(value) && value > 0);

    private int _deferredDataDepth;
    private bool _deferredDataChanged, _pausingFollowForNavigation, _writingFollowViewport, _resettingViewportForDataPolicy;
    private long _deferredDataCommitCount;

    /// <summary>Follows the greatest visible primary numeric/date X in a trailing data-unit window. Explicit navigation pauses following.</summary>
    public bool FollowLatestData { get => GetValue(FollowLatestDataProperty); set => SetValue(FollowLatestDataProperty, value); }
    /// <summary>Trailing width in primary X data units, subject to axis domain and zoom constraints.</summary>
    public double FollowLatestWindow { get => GetValue(FollowLatestWindowProperty); set => SetValue(FollowLatestWindowProperty, value); }

    /// <summary>
    /// Coalesces observed series data changes into one selection reconciliation and refresh request.
    /// Buffers and series versions change immediately; axis/property writes are not deferred.
    /// Dispose on the UI thread, including when an update fails.
    /// </summary>
    public IDisposable DeferDataUpdates()
    {
        VerifyAccess();
        _deferredDataDepth++;
        return new ChartDataUpdateScope(this);
    }

    private sealed class ChartDataUpdateScope(CartesianChart chart) : IDisposable
    {
        private CartesianChart? _chart = chart;
        public void Dispose()
        {
            var current = _chart;
            if (current is null) return;
            current.VerifyAccess();
            _chart = null;
            if (--current._deferredDataDepth != 0 || !current._deferredDataChanged) return;
            current._deferredDataChanged = false;
            current._deferredDataCommitCount++;
            if (!current._attached) return;
            if (!current.CanRunScheduledWork) { current._schedulingReconcilePending = true; return; }
            current.ReconcileSelection();
            current.RequestRender();
            current.NotifyViewSynchronizationChanged();
        }
    }

    private bool DeferStreamingDataChange(ChartSeries series)
    {
        if (_deferredDataDepth == 0) return false;
        if (!_deferredDataChanged) PrepareAnimationChange(ChartAnimationTriggers.Data);
        _deferredDataChanged = true;
        _observedVersions[series] = (series.SourceVersion, series.PublishedSnapshotRevision);
        StopRectangle(); StopPan();
        // A synchronous query inside the scope must still see the latest data.
        // Only publication work is coalesced; an old prepared snapshot is never
        // reused after a second buffer changes in the same callback.
        InvalidateScene(clearAxisText: false, clearData: true);
        return true;
    }

    private bool StreamingPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ViewportProperty && !_writingFollowViewport && FollowLatestData)
            PauseFollowingForNavigation();
        if (change.Property != FollowLatestDataProperty && change.Property != FollowLatestWindowProperty) return false;
        if (!_pausingFollowForNavigation) InvalidateInputTransform();
        if (change.Property == FollowLatestDataProperty && !FollowLatestData && !_pausingFollowForNavigation && _scene is { } scene)
        {
            // Turning off automatic following freezes the current window.
            // An explicit Viewport write already supplied its own destination.
            _writingFollowViewport = true;
            try { SetCurrentValue(ViewportProperty, scene.Range); }
            finally { _writingFollowViewport = false; }
        }
        InvalidateScene(clearAxisText: false);
        RequestRender();
        NotifyViewSynchronizationChanged();
        return true;
    }

    private void PauseFollowingForNavigation()
    {
        if (!FollowLatestData) return;
        var previous = _pausingFollowForNavigation;
        _pausingFollowForNavigation = true;
        try { SetCurrentValue(FollowLatestDataProperty, false); }
        finally { _pausingFollowForNavigation = previous; }
    }

    private ChartViewport FollowingRange(ChartViewport requested, ChartViewport extent, SeriesSnapshot[] snapshots)
    {
        // A categorical or caller-defined domain has no guaranteed numeric
        // trailing-window meaning. Keep its existing requested-range contract.
        if (!FollowLatestData || XAxis is CategoryAxis || !IsBuiltInAxis(XAxis)) return requested;
        var latest = double.NegativeInfinity;
        foreach (var snapshot in snapshots)
        {
            if (!ReferenceEquals(snapshot.XAxis, XAxis) || HorizontalComparison(snapshot.Series)) continue;
            if (ChartDataExtrema.For(snapshot.Data).Bounds is { } bounds) latest = Math.Max(latest, bounds.XMaximum);
        }
        if (!double.IsFinite(latest)) return requested;
        latest = Math.Clamp(latest, extent.XMinimum, extent.XMaximum);
        var lower = latest - FollowLatestWindow;
        if (!double.IsFinite(lower) || lower < extent.XMinimum) lower = extent.XMinimum;
        if (lower >= latest) lower = Math.Max(extent.XMinimum, Math.BitDecrement(latest));
        if (!XAxis.IsValidValue(lower) || !XAxis.IsValidValue(latest) || lower >= latest) return requested;
        return requested with { XMinimum = lower, XMaximum = latest };
    }
}
