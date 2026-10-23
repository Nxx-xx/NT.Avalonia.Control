using Avalonia;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

/// <summary>UI-thread snapshot of chart refresh admission and effective visual-tree activity.</summary>
public sealed record ChartSchedulingInfo(bool IsActive, bool IsRegistered, bool RefreshPending, long RefreshRequests,
    long RefreshDispatches, long CoalescedRefreshes, long ActivityRevision);

public sealed partial class CartesianChart
{
    private readonly List<Visual> _schedulingAncestors = [];
    private bool _schedulingActive;
    private bool _schedulingReconcilePending, _schedulingResetViewportPending;
    private int _schedulingPostPending;
    private long _schedulingRequests, _schedulingDispatches, _schedulingCoalesced, _schedulingActivityRevision;

    internal bool CanRunScheduledWork => _schedulingActive;

    /// <summary>Returns this chart's refresh scheduling counters without preparing or enumerating data.</summary>
    public ChartSchedulingInfo GetWorkSchedulingInfo()
    {
        VerifyAccess();
        return new(_schedulingActive, _attached, ChartWorkScheduler.IsRefreshPending(this), _schedulingRequests,
            _schedulingDispatches, _schedulingCoalesced, _schedulingActivityRevision);
    }

    private void SchedulingAttachedChanged()
    {
        foreach (var ancestor in _schedulingAncestors) ancestor.PropertyChanged -= SchedulingAncestorChanged;
        _schedulingAncestors.Clear();
        if (_attached)
            foreach (var ancestor in this.GetVisualAncestors())
            { _schedulingAncestors.Add(ancestor); ancestor.PropertyChanged += SchedulingAncestorChanged; }
        UpdateSchedulingActivity();
    }

    private void SchedulingAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsVisibleProperty) UpdateSchedulingActivity();
    }

    private void UpdateSchedulingActivity()
    {
        var active = _attached && IsVisible && _schedulingAncestors.All(ancestor => ancestor.IsVisible);
        if (active == _schedulingActive) return;
        _schedulingActive = active; _schedulingActivityRevision++;
        if (!active)
        {
            ChartWorkScheduler.Cancel(this);
            BackgroundLayoutActivityChanged(false);
            ReleaseResourceCaches();
        }
        else
        {
            // Hidden data notifications were intentionally not prepared. Re-enter with the latest source/view state.
            InvalidateScene(clearData: true);
            if (_schedulingResetViewportPending) { _schedulingResetViewportPending = false; ResetViewportCore(pauseFollowing: false); }
            if (_schedulingReconcilePending) { _schedulingReconcilePending = false; ReconcileSelection(); }
            BackgroundLayoutActivityChanged(true);
            QueueScheduledRefresh();
        }
    }

    private void QueueScheduledRefresh()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            if (Interlocked.Exchange(ref _schedulingPostPending, 1) == 0)
                Dispatcher.UIThread.Post(() => { Interlocked.Exchange(ref _schedulingPostPending, 0); QueueScheduledRefresh(); });
            return;
        }
        _schedulingRequests++; ChartWorkScheduler.RequestRefresh(this);
    }

    internal void SchedulingRefreshCoalesced() => _schedulingCoalesced++;
    internal void DispatchScheduledRefresh()
    {
        _schedulingDispatches++;
        _foregroundTooltipPresentation?.InvalidateVisual(); InvalidateVisual();
    }
}
