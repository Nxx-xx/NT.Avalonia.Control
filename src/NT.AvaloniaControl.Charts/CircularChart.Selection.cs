using Avalonia;
using Avalonia.Data;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CircularChart
{
    public static readonly StyledProperty<ChartSelectionMode> SelectionModeProperty = AvaloniaProperty.Register<CircularChart, ChartSelectionMode>(nameof(SelectionMode), ChartSelectionMode.SingleDeselect, validate: Enum.IsDefined);
    public static readonly StyledProperty<ChartPointHit?> SelectedPointProperty = AvaloniaProperty.Register<CircularChart, ChartPointHit?>(nameof(SelectedPoint), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<IReadOnlyList<ChartPointHit>?> SelectedPointsProperty = AvaloniaProperty.Register<CircularChart, IReadOnlyList<ChartPointHit>?>(nameof(SelectedPoints), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<double> SelectedExplodeOffsetProperty = AvaloniaProperty.Register<CircularChart, double>(nameof(SelectedExplodeOffset), 8, validate: v => double.IsFinite(v) && v >= 0 && v <= 1024);
    public ChartSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
    public ChartPointHit? SelectedPoint { get => GetValue(SelectedPointProperty); set => SetValue(SelectedPointProperty, value); }
    public IReadOnlyList<ChartPointHit> SelectedPoints { get => GetValue(SelectedPointsProperty) ?? Array.Empty<ChartPointHit>(); set => SetValue(SelectedPointsProperty, value); }
    public double SelectedExplodeOffset { get => GetValue(SelectedExplodeOffsetProperty); set => SetValue(SelectedExplodeOffsetProperty, value); }
    public event EventHandler<ChartSelectionChangingEventArgs>? SelectionChanging;
    public event EventHandler<ChartSelectionChangedEventArgs>? SelectionChanged;
    private IReadOnlyList<ChartPointHit> _selection = Array.Empty<ChartPointHit>();
    private bool _syncSelection;
    private long _selectionRevision;
    private long _selectionIntent;
    private ChartPointHit[]? _pendingSelection;
    internal static bool SameIdentity(ChartPointHit? a, ChartPointHit? b) => a is not null && b is not null && ReferenceEquals(a.Series, b.Series) &&
        (a.StableId is null || b.StableId is null ? a.Index == b.Index : string.IsNullOrWhiteSpace(a.Series.IdPath) ? ReferenceEquals(a.StableId, b.StableId) : Equals(a.StableId, b.StableId));
    private bool IsSelected(CircularSeries series, ChartDataPoint data) => (_pendingSelection as IReadOnlyList<ChartPointHit> ?? _selection).Any(s => SameIdentity(s, ChartPointHit.FromData(series, data, default)));
    private bool SelectionPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == SelectedPointProperty || change.Property == SelectedPointsProperty)
        {
            if (!_syncSelection)
            {
                var requested = change.Property == SelectedPointProperty ? SelectedPoint is { } p ? new[] { p } : [] : SelectedPoints.ToArray();
                if (Bounds.Width <= 0 || Bounds.Height <= 0) _pendingSelection = requested;
                else PublishSelection(requested, ChartSelectionChangeReason.ExternalValue);
            }
            return true;
        }
        if (change.Property == SelectionModeProperty)
        { PublishSelection(_selection, ChartSelectionChangeReason.Configuration); InvalidateCircular(); return true; }
        return false;
    }
    public bool SelectSlice(CircularSliceInfo slice, bool toggle = false) => SelectSlice(slice, toggle, ChartSelectionChangeReason.ExternalValue);
    private bool SelectSlice(CircularSliceInfo slice, bool toggle, ChartSelectionChangeReason reason)
    {
        ArgumentNullException.ThrowIfNull(slice);
        return SelectPointCore(slice.Hit, toggle, reason);
    }
    public bool SelectPoint(ChartPointHit point, bool toggle = false) => SelectPointCore(point, toggle, ChartSelectionChangeReason.ExternalValue);
    private bool SelectPointCore(ChartPointHit point, bool toggle, ChartSelectionChangeReason reason)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(point);
        if (SelectionMode == ChartSelectionMode.None) return false;
        var current = GetSlices().FirstOrDefault(s => s.IsVisible && !s.IsExiting && SameIdentity(s.Hit, point));
        if (current is null) return false;
        var selected = _selection.Any(p => SameIdentity(p, current.Hit));
        var next = SelectionMode == ChartSelectionMode.Multiple ? _selection.ToList() : new List<ChartPointHit>();
        if (selected && toggle && SelectionMode is ChartSelectionMode.Multiple or ChartSelectionMode.SingleDeselect)
            next.RemoveAll(p => SameIdentity(p, current.Hit));
        else if (!selected || SelectionMode != ChartSelectionMode.Multiple) next.Add(current.Hit);
        return PublishSelection(next, reason);
    }
    public void ClearSelection() { VerifyAccess(); PublishSelection([], ChartSelectionChangeReason.Clear); }
    private void ReconcileSelection(IReadOnlyList<CircularSliceInfo> target)
    {
        if (_rendering)
        {
            if (!_selectionUpdateQueued)
            {
                _selectionUpdateQueued = true;
                Dispatcher.UIThread.Post(() => { _selectionUpdateQueued = false; ReconcileSelection(GetSlices()); });
            }
            return;
        }
        if (_pendingSelection is not null && (Bounds.Width <= 0 || Bounds.Height <= 0)) return;
        if (_pendingSelection is { } pending && Bounds.Width > 0 && Bounds.Height > 0)
        { _pendingSelection = null; PublishSelection(pending, ChartSelectionChangeReason.ExternalValue, target); }
        else PublishSelection(_selection, ChartSelectionChangeReason.Reconcile, target);
    }
    private bool PublishSelection(IEnumerable<ChartPointHit> requested, ChartSelectionChangeReason reason, IReadOnlyList<CircularSliceInfo>? target = null)
    {
        if (_syncSelection) return false;
        if (_rendering && reason != ChartSelectionChangeReason.Reconcile)
        {
            _pendingSelection = null;
            var pending = requested.ToArray(); var intent = ++_selectionIntent;
            Dispatcher.UIThread.Post(() => { if (intent == _selectionIntent) PublishSelection(pending, reason); });
            return true;
        }
        if (reason != ChartSelectionChangeReason.Reconcile) _selectionIntent++;
        target ??= GetSlices();
        var revision = ++_selectionRevision;
        var normalized = new List<ChartPointHit>();
        if (SelectionMode != ChartSelectionMode.None)
            foreach (var point in requested.ToArray())
            {
                var current = target.FirstOrDefault(s => s.IsVisible && !s.IsExiting && SameIdentity(s.Hit, point));
                if (current is null || normalized.Any(p => SameIdentity(p, current.Hit))) continue;
                normalized.Add(current.Hit);
                if (SelectionMode != ChartSelectionMode.Multiple) break;
            }
        var identityChanged = normalized.Count != _selection.Count || !normalized.Zip(_selection).All(pair => SameIdentity(pair.First, pair.Second));
        if (!identityChanged && normalized.SequenceEqual(_selection)) { SyncSelectionProperties(); return true; }
        var next = Array.AsReadOnly(normalized.ToArray());
        if (identityChanged)
        {
            var args = new ChartSelectionChangingEventArgs(_selection, next, reason, reason is not (ChartSelectionChangeReason.Reconcile or ChartSelectionChangeReason.Configuration));
            SelectionChanging?.Invoke(this, args);
            if (_selectionRevision != revision || args.CanCancel && args.Cancel) { SyncSelectionProperties(); return false; }
            // A callback may replace source data without changing the selection revision.
            if (reason is not (ChartSelectionChangeReason.Reconcile or ChartSelectionChangeReason.Configuration))
            {
                var latest = GetSlices();
                if (_selectionRevision != revision || next.Any(p => !latest.Any(s => s.IsVisible && !s.IsExiting && SameIdentity(s.Hit, p))))
                { SyncSelectionProperties(); return false; }
            }
        }
        var previous = _selection;
        _selection = next;
        SyncSelectionProperties();
        if (reason != ChartSelectionChangeReason.Reconcile) { _pendingTrigger |= ChartAnimationTriggers.Viewport; InvalidateCircular(); }
        if (identityChanged) SelectionChanged?.Invoke(this, new(previous, next));
        return true;
    }
    private void SyncSelectionProperties()
    {
        _syncSelection = true;
        try { SetCurrentValue(SelectedPointsProperty, _selection); SetCurrentValue(SelectedPointProperty, _selection.LastOrDefault()); }
        finally { _syncSelection = false; }
    }
}
