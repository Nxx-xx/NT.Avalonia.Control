using Avalonia;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>Explicit linking of original business data between professional chart families.
/// Membership is weak, and hidden, disabled or detached charts neither send nor receive changes.
/// Registering a member never chooses a source; call SynchronizeFrom when an initial copy is needed.
/// Explicit selectors and layout-domain tokens are retained; callers control references captured by those values.</summary>
public sealed class ChartDataSynchronizationGroup : IDisposable
{
    private readonly List<Member> _members = [];
    private bool _selection = true, _range, _viewport, _disposed, _dispatching;
    private long _configuration;
    private ChartBase? _requestedSource, _applyingTarget;
    private Channels _requestedChannels;
    private long _viewIntentSequence;

    /// <summary>Link original selected members by business key. Defaults to true.</summary>
    public bool SynchronizeSelection { get => _selection; set => Configure(ref _selection, value); }
    /// <summary>Copy a top-left normalized layout brush between members with the same explicit non-null layout domain.
    /// Each target selects its own real glyphs. Defaults to false.</summary>
    public bool SynchronizeRange { get => _range; set => Configure(ref _range, value); }
    /// <summary>Copy engineering Zoom, PanX and PanY between members with the same explicit non-null layout domain.
    /// Analytical charts have no equivalent viewport and never receive an invented axis range. Defaults to false.</summary>
    public bool SynchronizeViewport { get => _viewport; set => Configure(ref _viewport, value); }

    /// <summary>Register an engineering chart. A selector maps each original member to a business key;
    /// null keys are excluded. Without a selector, explicit IDs compare by value and implicit original items by reference.
    /// A layout domain opts this member into comparable-layout range/viewport linking.</summary>
    public void Add(EngineeringChartBase chart, Func<ChartDataPoint, object?>? keySelector = null, object? layoutDomain = null) => AddCore(chart, keySelector, layoutDomain);
    /// <summary>Register an analytical chart with the same original-member key contract.</summary>
    public void Add(AnalysisChartBase chart, Func<ChartDataPoint, object?>? keySelector = null, object? layoutDomain = null) => AddCore(chart, keySelector, layoutDomain);
    /// <summary>Unregister an engineering chart and release all owned subscriptions.</summary>
    public bool Remove(EngineeringChartBase chart) => RemoveCore(chart);
    /// <summary>Unregister an analytical chart and release all owned subscriptions.</summary>
    public bool Remove(AnalysisChartBase chart) => RemoveCore(chart);
    /// <summary>Copy an active member's current state to other active members.</summary>
    public void SynchronizeFrom(EngineeringChartBase chart) => SynchronizeCore(chart);
    /// <summary>Copy an active member's current state to other active members.</summary>
    public void SynchronizeFrom(AnalysisChartBase chart) => SynchronizeCore(chart);

    private void Configure(ref bool field, bool value)
    { VerifyUsable(); if (field == value) return; field = value; _configuration++; _viewIntentSequence++; _requestedSource = null; }
    private void AddCore(ChartBase chart, Func<ChartDataPoint, object?>? selector, object? domain)
    {
        VerifyUsable(); ArgumentNullException.ThrowIfNull(chart); Prune();
        if (Find(chart) is not null) return;
        _members.Add(new(chart, selector, domain)); _configuration++;
        if (chart is EngineeringChartBase e) e.RegisterDataSynchronizationGroup(this);
        else ((AnalysisChartBase)chart).RegisterDataSynchronizationGroup(this);
        RefreshMembership(chart);
    }
    private bool RemoveCore(ChartBase chart)
    {
        VerifyUsable(); ArgumentNullException.ThrowIfNull(chart);
        if (Find(chart) is not { } member) return false;
        Unsubscribe(member, chart); _members.Remove(member); _configuration++;
        if (chart is EngineeringChartBase e) e.UnregisterDataSynchronizationGroup(this);
        else ((AnalysisChartBase)chart).UnregisterDataSynchronizationGroup(this);
        if (ReferenceEquals(_requestedSource, chart)) _requestedSource = null;
        return true;
    }
    private void SynchronizeCore(ChartBase chart)
    {
        VerifyUsable(); ArgumentNullException.ThrowIfNull(chart);
        if (Find(chart) is null) throw new ArgumentException("The source chart must belong to this group.", nameof(chart));
        if (Find(chart) is { } member) member.ViewIntent = ++_viewIntentSequence;
        Queue(chart, Channels.All);
    }

    internal void RefreshMembership(ChartBase chart)
    {
        if (_disposed || Find(chart) is not { } member) return;
        if (Active(chart) && !member.Subscribed)
        {
            var anchor = new ChartDataSynchronizationStateAnchor { ActivationIntentVersion = IntentVersion(chart), SuppressActivationPublication = true };
            SetAnchor(chart, anchor); member.State = new(anchor);
            if (chart is EngineeringChartBase e) { e.ResetDataSynchronizationBaseline(); e.DataSelectionChanged += SelectionChanged; e.DataViewSynchronizationIntentChanged += ViewIntentChanged; e.DataViewSynchronizationChanged += ViewChanged; }
            else ((AnalysisChartBase)chart).DataSelectionChanged += SelectionChanged;
            member.Subscribed = true;
        }
        else if (!Active(chart) && member.Subscribed) Unsubscribe(member, chart);
        if (!Active(chart) && ReferenceEquals(_requestedSource, chart)) _requestedSource = null;
    }
    private void SelectionChanged(object? sender, EventArgs args)
    {
        if (sender is not ChartBase chart || Find(chart) is not { } member || member.State is not { } stateReference || !stateReference.TryGetTarget(out var state)) return;
        var version = Version(chart); var current = Selected(chart); var nativeKeys = Keys(chart); var range = Range(chart);
        if (current is null || version != Version(chart) || !SelectionCurrent(chart)) return;
        var same = state.LastSelection is { } previous && ChartDataSelection.Same(previous, current) &&
            (state.LastKeys ?? []).SequenceEqual(nativeKeys) && state.LastRange == range;
        if (!SelectionCurrent(chart) || version != Version(chart)) return;
        state.LastSelection = current; state.LastKeys = NativeKeys(chart); state.LastRange = range;
        if (state.SuppressActivationPublication)
        {
            state.SuppressActivationPublication = false;
            // Reappearing members publish their own retained selection after rebuilding.
            // That lifecycle publication must not elect the returning member as a source.
            if (state.ActivationIntentVersion == IntentVersion(chart)) return;
        }
        if (!same && !ReferenceEquals(chart, _applyingTarget)) Queue(chart, Channels.Selection | Channels.Range);
    }
    private void ViewIntentChanged(object? sender, EventArgs args)
    { if (sender is ChartBase chart && !ReferenceEquals(chart, _applyingTarget) && Find(chart) is { } member) member.ViewIntent = ++_viewIntentSequence; }
    private void ViewChanged(object? sender, EventArgs args)
    { if (sender is ChartBase chart && !ReferenceEquals(chart, _applyingTarget) && Find(chart) is { } member && member.ViewIntent == _viewIntentSequence) Queue(chart, Channels.Viewport); }
    private void Queue(ChartBase chart, Channels channels)
    {
        if (_disposed || !Active(chart) || Find(chart) is not { Subscribed: true } || !(_selection || _range || _viewport)) return;
        var enabled = (_selection ? Channels.Selection : 0) | (_range ? Channels.Range : 0) | (_viewport ? Channels.Viewport : 0);
        channels &= enabled; if (channels == 0) return;
        _requestedChannels = ReferenceEquals(_requestedSource, chart) ? _requestedChannels | channels : channels;
        _requestedSource = chart;
        if (!_dispatching) Dispatch();
    }
    private void Dispatch()
    {
        if (_disposed || _dispatching) return;
        _dispatching = true;
        try
        {
            var iterations = 0;
            while (!_disposed && _requestedSource is { } source)
            {
                _requestedSource = null;
                var channels = _requestedChannels; _requestedChannels = 0;
                if (++iterations > 64) throw new InvalidOperationException("Professional chart synchronization did not settle after 64 changes. Check contradictory selection or viewport bindings.");
                var configuration = _configuration;
                if (Find(source) is not { Subscribed: true } sourceMember || !Active(source)) continue;
                var version = Version(source);
                var selector = Selector(sourceMember, source);
                var copySelection = _selection && (channels & Channels.Selection) != 0;
                var selected = copySelection ? Selected(source) : [];
                if (selected is null || copySelection && !SelectionCurrent(source)) continue;
                var keys = new List<object>(); var distinctKeys = new HashSet<object>();
                foreach (var datum in selected)
                {
                    var key = selector(datum);
                    if (_disposed || configuration != _configuration || version != Version(source) || !Active(source) || _requestedSource is not null) break;
                    if (key is not null && distinctKeys.Add(key)) keys.Add(key);
                }
                if (_disposed || configuration != _configuration || version != Version(source) || !Active(source) || _requestedSource is not null) continue;
                var range = Range(source); var view = Viewport(source);
                foreach (var member in _members.ToArray())
                {
                    if (_disposed || configuration != _configuration || version != Version(source) || _requestedSource is not null) break;
                    if (!member.Subscribed || !member.Chart.TryGetTarget(out var target) || ReferenceEquals(target, source) || !Active(target)) continue;
                    var comparable = sourceMember.Domain is not null && member.Domain is not null && Equals(sourceMember.Domain, member.Domain);
                    if (_disposed || configuration != _configuration || version != Version(source) || _requestedSource is not null || !Active(target)) break;
                    var copyRange = _range && comparable && (channels & Channels.Range) != 0;
                    IReadOnlyList<object>? expected = null;
                    if (copySelection && !(copyRange && range is not null))
                    {
                        expected = Prepare(target, keys, Selector(member, target));
                        if (expected is null || _disposed || configuration != _configuration || version != Version(source) || _requestedSource is not null || !Active(target)) continue;
                    }
                    var expectedView = _viewport && comparable && (channels & Channels.Viewport) != 0 && target is EngineeringChartBase ? view : null;
                    var mutated = false;
                    _applyingTarget = target;
                    try
                    {
                        if (copyRange && (range is not null || !copySelection)) { SelectRange(target, range); mutated = true; }
                        if (expected is not null) { ApplyKeys(target, expected); mutated = true; }
                        // A synchronous business binding owns any different state it installs.
                        // Do not overwrite that newer intent with another part of this transaction.
                        if (mutated && !ExpectedSelection(target, expected, copyRange ? range : null, copyRange && range is not null))
                        { QueueAfterApplication(target, Channels.Selection | Channels.Range); continue; }
                        if (expectedView is { } viewport && target is EngineeringChartBase engineering) engineering.ApplyDataSynchronizedViewport(viewport);
                    }
                    finally { _applyingTarget = null; }
                    if (!Active(target)) continue;
                    if (mutated && !ExpectedSelection(target, expected, copyRange ? range : null, copyRange && range is not null)) QueueAfterApplication(target, Channels.Selection | Channels.Range);
                    if (expectedView is { } desired && Viewport(target) != desired) QueueAfterApplication(target, Channels.Viewport);
                }
            }
            Prune();
        }
        catch { _requestedSource = null; throw; }
        finally { _applyingTarget = null; _dispatching = false; }
    }
    private void QueueAfterApplication(ChartBase chart, Channels channels)
    { if (!_disposed && Active(chart) && Find(chart) is { Subscribed: true }) { _requestedChannels = ReferenceEquals(_requestedSource, chart) ? _requestedChannels | channels : channels; _requestedSource = chart; } }
    private static bool ExpectedSelection(ChartBase chart, IReadOnlyList<object>? expected, Rect? range, bool rangeOwnsSelection)
    {
        if (rangeOwnsSelection) return Range(chart) == range;
        return expected is null ? Range(chart) == range : Range(chart) is null && Keys(chart).SequenceEqual(expected);
    }
    private static Func<ChartDataPoint, object?> Selector(Member member, ChartBase chart)
    {
        if (member.Selector is { } selector) return selector;
        return point => point.StableId is { } identity && !ReferenceEquals(identity, point.Item)
            ? identity : (point.Item ?? point.StableId) is { } item ? new ReferenceKey(item) : null;
    }
    private static IReadOnlyList<ChartDataPoint>? Selected(ChartBase chart) => chart is EngineeringChartBase e ? e.GetSelectedDataForSynchronization() : ((AnalysisChartBase)chart).GetSelectedDataForSynchronization();
    private static IReadOnlyList<object>? NativeKeys(ChartBase chart) => chart is EngineeringChartBase e ? e.SelectedDataKeys : ((AnalysisChartBase)chart).SelectedDataKeys;
    private static IReadOnlyList<object> Keys(ChartBase chart) => NativeKeys(chart) ?? [];
    private static bool SelectionCurrent(ChartBase chart) => chart is EngineeringChartBase e ? e.IsDataSelectionCurrent : ((AnalysisChartBase)chart).IsDataSelectionCurrent;
    private static Rect? Range(ChartBase chart) => chart is EngineeringChartBase e ? e.DataSelectionRange : ((AnalysisChartBase)chart).DataSelectionRange;
    private static long Version(ChartBase chart) => chart is EngineeringChartBase e ? e.DataSynchronizationVersion : ((AnalysisChartBase)chart).DataSynchronizationVersion;
    private static long IntentVersion(ChartBase chart) => chart is EngineeringChartBase e ? e.DataSelectionIntentVersion : ((AnalysisChartBase)chart).DataSelectionIntentVersion;
    private static bool Active(ChartBase chart) => chart is EngineeringChartBase e ? e.CanSynchronizeData : ((AnalysisChartBase)chart).CanSynchronizeData;
    private static ChartDataSynchronizedViewport? Viewport(ChartBase chart) => chart is EngineeringChartBase e ? new(e.Zoom, e.PanX, e.PanY) : null;
    private static IReadOnlyList<object>? Prepare(ChartBase chart, IReadOnlyList<object> keys, Func<ChartDataPoint, object?> selector) => chart is EngineeringChartBase e ? e.PrepareSynchronizedSelection(keys, selector) : ((AnalysisChartBase)chart).PrepareSynchronizedSelection(keys, selector);
    private static void ApplyKeys(ChartBase chart, IReadOnlyList<object> keys)
    { if (chart is EngineeringChartBase e) e.ApplyPreparedSynchronizedSelection(keys); else ((AnalysisChartBase)chart).ApplyPreparedSynchronizedSelection(keys); }
    private static void SelectRange(ChartBase chart, Rect? range)
    { if (chart is EngineeringChartBase e) e.SelectDataRange(range); else ((AnalysisChartBase)chart).SelectDataRange(range); }
    private void SetAnchor(ChartBase chart, ChartDataSynchronizationStateAnchor? anchor)
    { if (chart is EngineeringChartBase e) e.SetDataSynchronizationStateAnchor(this, anchor); else ((AnalysisChartBase)chart).SetDataSynchronizationStateAnchor(this, anchor); }

    /// <summary>Unregister every chart and release all owned subscriptions. Repeated disposal is harmless.</summary>
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess(); if (_disposed) return;
        _disposed = true; _requestedSource = null; _configuration++;
        foreach (var member in _members.ToArray()) if (member.Chart.TryGetTarget(out var chart))
        {
            Unsubscribe(member, chart);
            if (chart is EngineeringChartBase e) e.UnregisterDataSynchronizationGroup(this); else ((AnalysisChartBase)chart).UnregisterDataSynchronizationGroup(this);
        }
        _members.Clear(); _applyingTarget = null;
    }
    private void Unsubscribe(Member member, ChartBase chart)
    {
        if (member.Subscribed)
        {
            if (chart is EngineeringChartBase e) { e.DataSelectionChanged -= SelectionChanged; e.DataViewSynchronizationIntentChanged -= ViewIntentChanged; e.DataViewSynchronizationChanged -= ViewChanged; }
            else ((AnalysisChartBase)chart).DataSelectionChanged -= SelectionChanged;
        }
        member.Subscribed = false;
        SetAnchor(chart, null); member.State = null;
    }
    private Member? Find(ChartBase chart) => _members.FirstOrDefault(member => member.Chart.TryGetTarget(out var found) && ReferenceEquals(found, chart));
    private void Prune() => _members.RemoveAll(member => !member.Chart.TryGetTarget(out _));
    private void VerifyUsable() { Dispatcher.UIThread.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); }
    private sealed class Member(ChartBase chart, Func<ChartDataPoint, object?>? selector, object? domain)
    { internal readonly WeakReference<ChartBase> Chart = new(chart); internal readonly Func<ChartDataPoint, object?>? Selector = selector; internal readonly object? Domain = domain; internal bool Subscribed; internal WeakReference<ChartDataSynchronizationStateAnchor>? State; internal long ViewIntent; }
    private sealed class ReferenceKey(object value)
    { public override bool Equals(object? obj) => obj is ReferenceKey other && ReferenceEquals(value, other.Value); private object Value => value; public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value); }
    [Flags] private enum Channels { Selection = 1, Range = 2, Viewport = 4, All = Selection | Range | Viewport }
}

internal readonly record struct ChartDataSynchronizedViewport(double Zoom, double PanX, double PanY);

// A chart owns this bounded state while active. The group holds only a weak reference,
// even when an implicit item/key (for example a KPI datum) refers to the chart itself.
internal sealed class ChartDataSynchronizationStateAnchor
{
    internal IReadOnlyList<object>? LastKeys;
    internal IReadOnlyList<ChartDataPoint>? LastSelection;
    internal Rect? LastRange;
    internal long ActivationIntentVersion;
    internal bool SuppressActivationPublication;
}
