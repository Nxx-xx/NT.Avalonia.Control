using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

/// <summary>Read-only chart summary, lazy series controls and bounded virtual point pages.</summary>
public sealed class ChartAutomationPeer : ControlAutomationPeer, IValueProvider, ISelectionProvider
{
    public const int DefaultPointPageSize = 64;
    public const int MaximumCachedPointPeers = 256;
    private readonly CartesianChart _chart;
    private readonly Dictionary<ChartSeries, ChartSeriesAutomationPeer> _series = new(ReferenceEqualityComparer.Instance);
    private readonly LinkedList<ChartPointAutomationPeer> _points = [];
    private long _nextIdentity;
    private string _lastValue;
    private string _lastName;
    private ChartSeries[] _lastSeries;
    private IReadOnlyList<ChartPointHit> _lastSelection;
    private bool _lastCanSelectMultiple;
    private bool _updating;
    private bool _pendingNotificationWanted;
    private bool _pendingNotificationPosted;
    private bool _flushPendingNotificationRequested;
    private readonly Dictionary<ChartSeries, bool> _lastVisibility = new(ReferenceEqualityComparer.Instance);
    public ChartAutomationPeer(CartesianChart owner) : base(owner)
    {
        _chart = owner;
        _lastSeries = owner.AutomationSeries;
        foreach (var series in _lastSeries) _lastVisibility.Add(series, series.EffectiveVisibility);
        _lastSelection = owner.AccessibilitySelection;
        _lastCanSelectMultiple = owner.SelectionMode == ChartSelectionMode.Multiple;
        _lastValue = owner.AccessibilitySummaryText;
        _lastName = GetName();
    }
    public bool IsReadOnly => true;
    public string Value { get { _chart.VerifyAccess(); return _chart.AccessibilitySummaryText; } }
    public void SetValue(string? value) => throw new InvalidOperationException("The chart summary is read-only.");
    public int CachedSeriesPeerCount { get { _chart.VerifyAccess(); return _series.Count; } }
    public int CachedPointPeerCount { get { _chart.VerifyAccess(); return _points.Count; } }
    public bool CanSelectMultiple { get { _chart.VerifyAccess(); return _chart.SelectionMode == ChartSelectionMode.Multiple; } }
    public bool IsSelectionRequired => false;
    internal CartesianChart Chart => _chart;
    public IReadOnlyList<AutomationPeer> GetSelection()
    {
        _chart.VerifyAccess();
        _chart.AccessibilityEnsureScene();
        // Exact explicit selection, already bounded by MaxSelectionCount. Only
        // the last 256 requested peers are retained by the chart's LRU.
        return _chart.AccessibilitySelection.Select(point => _chart.AccessibilityTryResolvePoint(point, out var current) ? GetPointPeer(current) : null)
            .Where(point => point is not null).Cast<AutomationPeer>().ToArray();
    }
    /// <summary>Requests one source index without constructing a full-source identity index.</summary>
    public ChartPointAutomationPeer? GetPointPeer(ChartSeries series, int sourceIndex)
    {
        _chart.VerifyAccess(); ArgumentNullException.ThrowIfNull(series);
        _chart.AccessibilityEnsureScene();
        return _chart.AccessibilityTryGetPoint(series, sourceIndex, out var point) ? GetPointPeer(point) : null;
    }
    /// <summary>Changes this series' accessible page. Start/count address snapshot ordinals; invalid points are omitted without scanning past the page.</summary>
    public IReadOnlyList<ChartPointAutomationPeer> GetPointPage(ChartSeries series, int startIndex, int count = DefaultPointPageSize)
    {
        _chart.VerifyAccess(); ArgumentNullException.ThrowIfNull(series);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        if (count is < 1 or > MaximumCachedPointPeers) throw new ArgumentOutOfRangeException(nameof(count));
        _chart.AccessibilityEnsureScene();
        if (!_chart.AutomationSeries.Contains(series)) return [];
        var parent = GetSeriesPeer(series);
        parent.SetPage(startIndex, count);
        return PointPage(parent);
    }
    internal IReadOnlyList<ChartPointAutomationPeer> PointPage(ChartSeriesAutomationPeer parent) =>
        _chart.AccessibilityPointPage(parent.Series, parent.PageStart, parent.PageSize).Select(GetPointPeer).ToArray();
    private ChartPointAutomationPeer GetPointPeer(ChartPointHit point)
    {
        for (var node = _points.First; node is not null; node = node.Next)
            if (node.Value.Matches(point))
            {
                var peer = node.Value; peer.UpdatePoint(point);
                _points.Remove(node); _points.AddLast(peer); return peer;
            }
        var created = new ChartPointAutomationPeer(this, GetSeriesPeer(point.Series), point, ++_nextIdentity);
        _points.AddLast(created);
        while (_points.Count > MaximumCachedPointPeers) _points.RemoveFirst();
        return created;
    }
    protected override string GetClassNameCore() => nameof(CartesianChart);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : _chart.Title ?? "Cartesian chart";
    protected override string GetItemStatusCore() => $"selected={_chart.SelectedPoints?.Count ?? (_chart.SelectedPoint is null ? 0 : 1)}";
    protected override string? GetHelpTextCore() => base.GetHelpTextCore() is { Length: > 0 } help ? help :
        "Read-only chart summary. Series children toggle visibility and expose bounded point pages with previous/next controls. Point selection and invocation follow chart policy.";
    protected override IReadOnlyList<AutomationPeer> GetChildrenCore()
    {
        _chart.VerifyAccess(); var active = _chart.AutomationSeries; Prune(active);
        return active.Select(series => (AutomationPeer)GetSeriesPeer(series)).ToArray();
    }
    private ChartSeriesAutomationPeer GetSeriesPeer(ChartSeries series)
    {
        if (!_series.TryGetValue(series, out var peer)) _series.Add(series, peer = new(this, series, ++_nextIdentity));
        return peer;
    }
    private void Prune(ChartSeries[] active)
    {
        var current = active.ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var old in _series.Keys.Where(series => !current.Contains(series)).ToArray()) _series.Remove(old);
        for (var node = _points.First; node is not null;)
        {
            var next = node.Next;
            if (!current.Contains(node.Value.Series) || node.Value.RefreshAndIsRetired())
            { node.Value.Retire(); _points.Remove(node); }
            node = next;
        }
    }
    internal void ChartChanged(bool flushPending = false)
    {
        if (_updating) return;
        flushPending |= _flushPendingNotificationRequested;
        _flushPendingNotificationRequested = false;
        _updating = true;
        try
        {
            // Scene invalidation and its synchronous layout publication form one
            // UI turn. Do not announce every realized point as unavailable and
            // immediately available again during that transient gap. Providers
            // still report the current pending state when queried directly.
            var deferUnavailable = !_chart.AccessibilityHasScene && !flushPending &&
                _chart.IsEffectivelyVisible && TopLevel.GetTopLevel(_chart) is not null;
            _pendingNotificationWanted = deferUnavailable;
            if (deferUnavailable) QueuePendingNotification();
            var active = _chart.AutomationSeries;
            var availabilityChanged = !_lastSeries.SequenceEqual(active, ReferenceEqualityComparer.Instance);
            foreach (var old in _lastVisibility.Keys.Where(series => !active.Contains(series)).ToArray()) _lastVisibility.Remove(old);
            foreach (var series in active)
            {
                if (!_lastVisibility.TryGetValue(series, out var visible) || visible != series.EffectiveVisibility) availabilityChanged = true;
                _lastVisibility[series] = series.EffectiveVisibility;
                // An explicitly unbound source is not a transient viewport gap.
                if (series.ItemsSource is null) availabilityChanged = true;
            }
            Prune(active);
            foreach (var peer in _series.Values.ToArray()) peer.ChartChanged(deferUnavailable);
            foreach (var peer in _points.ToArray())
            {
                // Hidden/removed items are real availability changes. Selection
                // is also authoritative before the next scene is published.
                if (deferUnavailable && peer.Series.EffectiveVisibility && peer.Series.ItemsSource is not null)
                    peer.SelectionChangedWhilePending();
                else peer.ChartChanged();
            }
            if (!_lastSeries.SequenceEqual(active, ReferenceEqualityComparer.Instance))
            { _lastSeries = active; InvalidateChildren(); }
            var selection = _chart.AccessibilitySelection;
            if (!ReferenceEquals(_lastSelection, selection))
            {
                // Match SelectingItemsControlAutomationPeer's invalidation event.
                // Do not realize every selected item to build an event payload.
                _lastSelection = selection;
                RaisePropertyChangedEvent(SelectionPatternIdentifiers.SelectionProperty, null, null);
            }
            var canSelectMultiple = CanSelectMultiple;
            if (_lastCanSelectMultiple != canSelectMultiple)
            {
                var previous = _lastCanSelectMultiple;
                _lastCanSelectMultiple = canSelectMultiple;
                RaisePropertyChangedEvent(SelectionPatternIdentifiers.CanSelectMultipleProperty, previous, canSelectMultiple);
            }
            var value = Value; var name = GetName();
            if ((!deferUnavailable || availabilityChanged) && _lastValue != value) RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, _lastValue, value);
            if (_lastName != name) RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, _lastName, name);
            if (!deferUnavailable || availabilityChanged) _lastValue = value;
            _lastName = name;
        }
        finally { _updating = false; }
    }

    private void QueuePendingNotification()
    {
        if (_pendingNotificationPosted) return;
        _pendingNotificationPosted = true;
        // A chart closed before the dispatcher turn must not be kept alive by
        // this notification alone.
        var weak = new WeakReference<ChartAutomationPeer>(this);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!weak.TryGetTarget(out var peer)) return;
            peer._pendingNotificationPosted = false;
            if (peer._pendingNotificationWanted)
            {
                // Keep the owner's reentrant notification loop: a native event
                // handler may synchronously request and publish a fresh scene.
                peer._flushPendingNotificationRequested = true;
                peer._chart.NotifyAutomationStateChanged();
            }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
}

internal sealed class ChartSeriesAutomationPeer : UnrealizedElementAutomationPeer, IToggleProvider
{
    private readonly ChartAutomationPeer _root;
    private readonly long _identity;
    private ToggleState _lastState;
    private string _lastName;
    private (long Source, long Publication, int Count, bool Visible) _lastChildren;
    private ChartPointPageAutomationPeer? _previousPage;
    private ChartPointPageAutomationPeer? _nextPage;
    public ChartSeries Series { get; }
    public int PageStart { get; private set; }
    public int PageSize { get; private set; } = ChartAutomationPeer.DefaultPointPageSize;
    public ChartSeriesAutomationPeer(ChartAutomationPeer root, ChartSeries series, long identity)
    {
        _root = root; Series = series; _identity = identity; SetParent(root);
        _lastState = ToggleState; _lastName = GetName();
        _lastChildren = (series.SourceVersion, series.PublishedSnapshotRevision, root.Chart.AccessibilityPointCount(series), series.EffectiveVisibility);
    }
    internal bool Exists => _root.Chart.AutomationSeries.Contains(Series);
    public ToggleState ToggleState { get { _root.Chart.VerifyAccess(); return Series.EffectiveVisibility ? ToggleState.On : ToggleState.Off; } }
    public void Toggle()
    {
        _root.Chart.VerifyAccess(); EnsureEnabled();
        if (!Exists) throw new InvalidOperationException("The series no longer belongs to this chart.");
        _root.Chart.ToggleSeriesVisibility(Series); _root.Chart.NotifyAutomationStateChanged();
    }
    internal void SetPage(int start, int count)
    { if (PageStart == start && PageSize == count) return; PageStart = start; PageSize = count; RaiseChildrenChangedEvent(); }
    internal void ChangePage(bool forward)
    {
        _root.Chart.VerifyAccess(); var total = _root.Chart.AccessibilityPointCount(Series);
        if (!Exists || !Series.EffectiveVisibility || !_root.Chart.IsEffectivelyEnabled) throw new InvalidOperationException("The point page is unavailable.");
        var start = forward ? Math.Min((long)PageStart + PageSize, Math.Max(0, total - 1)) : Math.Max(0, PageStart - PageSize);
        SetPage((int)start, PageSize);
    }
    protected override string GetClassNameCore() => Series.GetType().Name;
    protected override string GetAutomationIdCore() => $"Chart.Series.{_identity}";
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;
    protected override bool IsControlElementCore() => true;
    protected override bool IsContentElementCore() => true;
    protected override string GetNameCore() => Series.Title ?? $"Series {Array.IndexOf(_root.Chart.AutomationSeries, Series) + 1}";
    protected override string GetItemStatusCore() => Exists ? Series.EffectiveVisibility ? "visible" : "hidden" : "removed";
    protected override bool IsEnabledCore() => Exists && _root.Chart.IsEffectivelyEnabled && _root.Chart.IsLegendInteractive;
    protected override bool IsOffscreenCore() => !Exists || _root.IsOffscreen() || GetBoundingRectangleCore() == default;
    protected override Rect GetBoundingRectangleCore() => Exists ? _root.Chart.AutomationSeriesBounds(Series) : default;
    protected override AutomationPeer GetParentCore() => _root;
    protected override string? GetAcceleratorKeyCore() => null;
    protected override string? GetAccessKeyCore() => null;
    protected override AutomationPeer? GetLabeledByCore() => null;
    protected override bool IsKeyboardFocusableCore() => false;
    protected override bool HasKeyboardFocusCore() => false;
    protected override void SetFocusCore() => throw new InvalidOperationException("Series summary nodes do not take keyboard focus.");
    protected override void BringIntoViewCore() { _root.Chart.VerifyAccess(); EnsureEnabled(); _root.BringIntoView(); }
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore()
    {
        _root.Chart.VerifyAccess(); _root.Chart.AccessibilityEnsureScene();
        if (!Exists || !Series.EffectiveVisibility) return [];
        var result = _root.PointPage(this).Cast<AutomationPeer>().ToList();
        // Native sibling navigation looks up the current peer by reference in a
        // fresh parent.GetChildren() result. Paging controls must retain identity.
        if (PageStart > 0) result.Insert(0, _previousPage ??= new ChartPointPageAutomationPeer(_root, this, false));
        if ((long)PageStart + PageSize < _root.Chart.AccessibilityPointCount(Series)) result.Add(_nextPage ??= new ChartPointPageAutomationPeer(_root, this, true));
        return result;
    }
    internal void ChartChanged(bool deferUnavailable = false)
    {
        var state = ToggleState; var name = GetName();
        if (_lastState != state) RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, _lastState, state);
        if (_lastName != name) RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, _lastName, name);
        _lastState = state; _lastName = name;
        if (deferUnavailable && Exists && Series.EffectiveVisibility) return;
        var count = _root.Chart.AccessibilityPointCount(Series);
        var previousStart = PageStart;
        // A scene invalidation precedes publication and temporarily has no rows.
        // Only a published, visible series may normalize a now-out-of-range page.
        if (_root.Chart.AccessibilityHasScene && Exists && Series.EffectiveVisibility && (count == 0 || PageStart >= count))
            PageStart = count == 0 ? 0 : (count - 1) / PageSize * PageSize;
        var children = (Series.SourceVersion, Series.PublishedSnapshotRevision, count, Series.EffectiveVisibility);
        if (_lastChildren != children || previousStart != PageStart)
        { _lastChildren = children; RaiseChildrenChangedEvent(); }
    }
}

internal sealed class ChartPointPageAutomationPeer : UnrealizedElementAutomationPeer, IInvokeProvider
{
    private readonly ChartAutomationPeer _root;
    private readonly ChartSeriesAutomationPeer _series;
    private readonly bool _forward;
    internal ChartPointPageAutomationPeer(ChartAutomationPeer root, ChartSeriesAutomationPeer series, bool forward)
    { _root = root; _series = series; _forward = forward; SetParent(series); }
    public void Invoke() { _root.Chart.VerifyAccess(); EnsureEnabled(); _series.ChangePage(_forward); }
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
    protected override bool IsControlElementCore() => true;
    protected override bool IsContentElementCore() => true;
    protected override string GetNameCore() => $"{(_forward ? "Next" : "Previous")} {_series.PageSize} source rows of {_series.Series.Title ?? "series"}";
    protected override string GetAutomationIdCore() => $"{_series.GetAutomationId()}.Page.{(_forward ? "Next" : "Previous")}";
    protected override string GetClassNameCore() => "ChartPointPage";
    protected override bool IsEnabledCore() => _root.Chart.IsEffectivelyEnabled && _series.Exists && _series.Series.EffectiveVisibility &&
        (_forward ? (long)_series.PageStart + _series.PageSize < _root.Chart.AccessibilityPointCount(_series.Series) : _series.PageStart > 0);
    protected override bool IsOffscreenCore() => !IsEnabledCore() || _root.IsOffscreen();
    protected override Rect GetBoundingRectangleCore() => _series.GetBoundingRectangle();
    protected override AutomationPeer GetParentCore() => _series;
    protected override string? GetAcceleratorKeyCore() => null;
    protected override string? GetAccessKeyCore() => null;
    protected override AutomationPeer? GetLabeledByCore() => null;
    protected override bool IsKeyboardFocusableCore() => false;
    protected override bool HasKeyboardFocusCore() => false;
    protected override void SetFocusCore() => throw new InvalidOperationException("Page controls do not take keyboard focus.");
    protected override void BringIntoViewCore() { _root.Chart.VerifyAccess(); EnsureEnabled(); _root.BringIntoView(); }
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => [];
}

public sealed partial class CartesianChart
{
    private ChartAutomationPeer? _chartAutomationPeer;
    private bool _notifyingAccessibility;
    private bool _accessibilityNotificationPending;
    protected override AutomationPeer OnCreateAutomationPeer() => _chartAutomationPeer = new(this);
    internal void NotifyAutomationStateChanged()
    {
        if (_notifyingAccessibility) { _accessibilityNotificationPending = true; return; }
        _notifyingAccessibility = true;
        try
        {
            for (var pass = 0; pass < 8; pass++)
            {
                _accessibilityNotificationPending = false;
                ReconcileCurrentPoint(); UpdateAccessibilitySummary(); _chartAutomationPeer?.ChartChanged();
                if (!_accessibilityNotificationPending) return;
            }
            Avalonia.Threading.Dispatcher.UIThread.Post(NotifyAutomationStateChanged);
        }
        finally { _notifyingAccessibility = false; }
    }
    internal ChartSeries[] AutomationSeries => ActiveSeries.Where(series => series is not null).Distinct().ToArray();
    internal string AutomationSummary => AccessibilitySummaryText;
    internal Rect AutomationSeriesBounds(ChartSeries series)
    {
        var legend = _scene?.Legend.FirstOrDefault(item => ReferenceEquals(item.Series, series));
        if (legend is null || TopLevel.GetTopLevel(this) is not Visual root || this.TransformToVisual(root) is not { } transform) return default;
        return legend.Bounds.TransformToAABB(transform);
    }
}
