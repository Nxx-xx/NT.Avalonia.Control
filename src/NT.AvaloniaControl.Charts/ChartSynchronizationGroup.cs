using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>How a chart group translates a source viewport into each target chart.</summary>
public enum ChartSynchronizationCoordinateMode
{
    /// <summary>Use the same ascending fraction of each chart's own transformed complete extent.</summary>
    Normalized = 0,
    /// <summary>Use the same raw primary-axis coordinates, constrained independently by each target.</summary>
    DataCoordinates = 1,
}

/// <summary>Explicit, UI-thread-affine viewport synchronization for a set of charts.
/// Membership is weak. Hidden or detached charts have no group event subscriptions and do not participate.
/// Adding a chart or changing configuration does not choose a source: call SynchronizeFrom explicitly.</summary>
public sealed class ChartSynchronizationGroup : IDisposable
{
    private readonly List<Member> _members = [];
    private ChartInteractionAxes _axes = ChartInteractionAxes.X;
    private ChartSynchronizationCoordinateMode _coordinateMode;
    private bool _disposed;
    private bool _dispatching;
    private Pending? _pending;
    private CartesianChart? _applyingTarget;
    private ChartViewport? _expectedData;
    private long _configurationVersion;

    /// <summary>Primary-axis dimensions to synchronize. Defaults to X.</summary>
    public ChartInteractionAxes Axes
    {
        get => _axes;
        set
        {
            VerifyUsable(); CartesianChart.ValidateSynchronizationAxes(value);
            if (_axes == value) return;
            _axes = value; _configurationVersion++; _pending = null;
        }
    }

    /// <summary>Fractional or raw-coordinate synchronization. Defaults to Normalized.</summary>
    public ChartSynchronizationCoordinateMode CoordinateMode
    {
        get => _coordinateMode;
        set
        {
            VerifyUsable();
            if (value is not (ChartSynchronizationCoordinateMode.Normalized or ChartSynchronizationCoordinateMode.DataCoordinates))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_coordinateMode == value) return;
            _coordinateMode = value; _configurationVersion++; _pending = null;
        }
    }

    /// <summary>Registers a chart without changing any viewport. Duplicate registration is harmless.</summary>
    public void Add(CartesianChart chart)
    {
        VerifyUsable(); ArgumentNullException.ThrowIfNull(chart);
        Prune();
        if (Find(chart) is not null) return;
        _members.Add(new(chart));
        chart.RegisterSynchronizationGroup(this);
        RefreshMembership(chart);
    }

    /// <summary>Removes a member and all subscriptions owned by this group.</summary>
    public bool Remove(CartesianChart chart)
    {
        VerifyUsable(); ArgumentNullException.ThrowIfNull(chart);
        var member = Find(chart);
        if (member is null) return false;
        Unsubscribe(member, chart); _members.Remove(member); chart.UnregisterSynchronizationGroup(this);
        if (_pending is { } pending && ReferenceEquals(pending.Source, chart)) _pending = null;
        return true;
    }

    /// <summary>Copies the current viewport of an active member to the other active members.</summary>
    public void SynchronizeFrom(CartesianChart chart)
    {
        VerifyUsable(); ArgumentNullException.ThrowIfNull(chart);
        if (Find(chart) is null) throw new ArgumentException("The source chart must belong to this group.", nameof(chart));
        Queue(chart, force: true);
    }

    internal void RefreshMembership(CartesianChart chart)
    {
        if (_disposed || Find(chart) is not { } member) return;
        if (chart.CanSynchronizeView && !member.Subscribed)
        {
            chart.ResetSynchronizationBaseline();
            if (_disposed || !ReferenceEquals(Find(chart), member) || !chart.CanSynchronizeView) return;
            chart.ViewSynchronizationChanged += OnViewChanged; member.Subscribed = true;
        }
        else if (!chart.CanSynchronizeView && member.Subscribed) Unsubscribe(member, chart);
        if (!chart.CanSynchronizeView && _pending is { } pending && ReferenceEquals(pending.Source, chart)) _pending = null;
    }

    private void OnViewChanged(object? sender, EventArgs e)
    {
        if (sender is CartesianChart chart) Queue(chart, force: false);
    }

    private void Queue(CartesianChart chart, bool force)
    {
        if (_disposed || _axes == ChartInteractionAxes.None || !chart.CanSynchronizeView ||
            Find(chart) is not { Subscribed: true } || chart.CaptureSynchronization() is not { } snapshot) return;
        // Ignore our own constrained application, but preserve a different viewport installed
        // synchronously by a business binding/event handler while that application was in progress.
        if (!force && ReferenceEquals(_applyingTarget, chart) && snapshot.Data == _expectedData) return;
        _pending = new(chart, snapshot);
        if (!_dispatching) Dispatch();
    }

    private void Dispatch()
    {
        if (_disposed || _dispatching) return;
        _dispatching = true;
        try
        {
            var iterations = 0;
            while (!_disposed && _pending is { } pending)
            {
                _pending = null;
                if (!pending.Source.CanSynchronizeView || Find(pending.Source) is not { Subscribed: true }) continue;
                var version = _configurationVersion;
                foreach (var member in _members.ToArray())
                {
                    if (_disposed || version != _configurationVersion || _pending is not null) break;
                    if (!member.Subscribed || !member.Chart.TryGetTarget(out var chart) ||
                        ReferenceEquals(chart, pending.Source) || !chart.CanSynchronizeView) continue;
                    if (chart.PrepareSynchronizedViewport(pending.Snapshot, _axes, _coordinateMode) is not { } prepared) continue;
                    if (_disposed || version != _configurationVersion || _pending is not null) break;
                    if (!member.Subscribed || !chart.CanSynchronizeView) continue;
                    _applyingTarget = chart; _expectedData = prepared;
                    try { chart.ApplySynchronizedViewport(prepared, _axes); }
                    finally { _applyingTarget = null; _expectedData = null; }
                }
                // A contradictory business binding must not consume this or subsequent UI
                // dispatcher turns forever. Ordinary reentrancy converges to the newest state.
                if (++iterations >= 64 && _pending is not null)
                {
                    _pending = null;
                    throw new InvalidOperationException("Chart viewport synchronization did not settle after 64 changes. Check contradictory viewport bindings.");
                }
            }
            Prune();
        }
        finally { _dispatching = false; }
    }

    /// <summary>Unregisters every chart. Calling Dispose more than once is harmless.</summary>
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed) return;
        _disposed = true; _pending = null;
        foreach (var member in _members)
            if (member.Chart.TryGetTarget(out var chart))
            { Unsubscribe(member, chart); chart.UnregisterSynchronizationGroup(this); }
        _members.Clear(); _applyingTarget = null; _expectedData = null;
    }

    private void Unsubscribe(Member member, CartesianChart chart)
    {
        if (member.Subscribed) chart.ViewSynchronizationChanged -= OnViewChanged;
        member.Subscribed = false;
    }

    private Member? Find(CartesianChart chart) => _members.FirstOrDefault(member =>
        member.Chart.TryGetTarget(out var value) && ReferenceEquals(value, chart));
    private void Prune() => _members.RemoveAll(member => !member.Chart.TryGetTarget(out _));
    private void VerifyUsable()
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class Member(CartesianChart chart)
    {
        internal readonly WeakReference<CartesianChart> Chart = new(chart);
        internal bool Subscribed;
    }
    private sealed record Pending(CartesianChart Source, ChartSynchronizationSnapshot Snapshot);
}
