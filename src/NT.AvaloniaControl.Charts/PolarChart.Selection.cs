using Avalonia;
using Avalonia.Data;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

public partial class PolarChart
{
    public static readonly StyledProperty<ChartSelectionMode> SelectionModeProperty = AvaloniaProperty.Register<PolarChart, ChartSelectionMode>(nameof(SelectionMode), ChartSelectionMode.SingleDeselect, validate: Enum.IsDefined);
    public static readonly StyledProperty<ChartPointHit?> SelectedPointProperty = AvaloniaProperty.Register<PolarChart, ChartPointHit?>(nameof(SelectedPoint), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<IReadOnlyList<ChartPointHit>?> SelectedPointsProperty = AvaloniaProperty.Register<PolarChart, IReadOnlyList<ChartPointHit>?>(nameof(SelectedPoints), defaultBindingMode: BindingMode.TwoWay);
    public ChartSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
    public ChartPointHit? SelectedPoint { get => GetValue(SelectedPointProperty); set => SetValue(SelectedPointProperty, value); }
    public IReadOnlyList<ChartPointHit> SelectedPoints { get => GetValue(SelectedPointsProperty) ?? Array.Empty<ChartPointHit>(); set => SetValue(SelectedPointsProperty, value); }
    public event EventHandler<ChartSelectionChangingEventArgs>? SelectionChanging;
    public event EventHandler<ChartSelectionChangedEventArgs>? SelectionChanged;
    private IReadOnlyList<ChartPointHit> _selection = Array.Empty<ChartPointHit>();
    private bool _syncSelection;
    private long _selectionRevision, _selectionIntent;
    private ChartPointHit[]? _pendingSelection;
    private static bool SameIdentity(ChartPointHit? a, ChartPointHit? b) => CircularChart.SameIdentity(a, b);
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
        if (change.Property == SelectionModeProperty) { PublishSelection(_selection, ChartSelectionChangeReason.Configuration); RequestPolarRender(); return true; }
        return false;
    }
    public bool SelectPoint(ChartPointHit point, bool toggle = false) => SelectPointCore(point, toggle, ChartSelectionChangeReason.ExternalValue);
    private bool SelectPointCore(ChartPointHit point, bool toggle, ChartSelectionChangeReason reason)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(point);
        if (SelectionMode == ChartSelectionMode.None) return false;
        var current = CurrentHits(GetScene()).FirstOrDefault(p => SameIdentity(p, point));
        if (current is null) return false;
        var selected = _selection.Any(p => SameIdentity(p, current));
        var next = SelectionMode == ChartSelectionMode.Multiple ? _selection.ToList() : new List<ChartPointHit>();
        if (selected && toggle && SelectionMode is ChartSelectionMode.Multiple or ChartSelectionMode.SingleDeselect) next.RemoveAll(p => SameIdentity(p, current));
        else if (!selected || SelectionMode != ChartSelectionMode.Multiple) next.Add(current);
        return PublishSelection(next, reason);
    }
    public void ClearSelection() { VerifyAccess(); PublishSelection([], ChartSelectionChangeReason.Clear); }
    private void ReconcileSelection(IReadOnlyList<ChartPointHit> target)
    {
        if (_rendering)
        {
            if (!_selectionUpdateQueued) { _selectionUpdateQueued = true; Dispatcher.UIThread.Post(() => { _selectionUpdateQueued = false; ReconcileSelection(CurrentHits(GetScene())); }); }
            return;
        }
        if (_pendingSelection is not null && (Bounds.Width <= 0 || Bounds.Height <= 0)) return;
        if (_pendingSelection is { } pending && Bounds.Width > 0 && Bounds.Height > 0) { _pendingSelection = null; PublishSelection(pending, ChartSelectionChangeReason.ExternalValue, target); }
        else PublishSelection(_selection, ChartSelectionChangeReason.Reconcile, target);
    }
    private bool PublishSelection(IEnumerable<ChartPointHit> requested, ChartSelectionChangeReason reason, IReadOnlyList<ChartPointHit>? target = null)
    {
        if (_syncSelection) return false;
        if (_rendering && reason != ChartSelectionChangeReason.Reconcile)
        {
            _pendingSelection = null; var pending = requested.ToArray(); var intent = ++_selectionIntent;
            Dispatcher.UIThread.Post(() => { if (intent == _selectionIntent) PublishSelection(pending, reason); }); return true;
        }
        if (reason != ChartSelectionChangeReason.Reconcile) _selectionIntent++;
        target ??= CurrentHits(GetScene());
        var revision = ++_selectionRevision; var normalized = new List<ChartPointHit>();
        if (SelectionMode != ChartSelectionMode.None) foreach (var point in requested.ToArray())
        {
            var current = target.FirstOrDefault(p => SameIdentity(p, point));
            if (current is null || normalized.Any(p => SameIdentity(p, current))) continue;
            normalized.Add(current); if (SelectionMode != ChartSelectionMode.Multiple) break;
        }
        var changed = normalized.Count != _selection.Count || !normalized.Zip(_selection).All(p => SameIdentity(p.First, p.Second));
        if (!changed && normalized.SequenceEqual(_selection)) { SyncSelectionProperties(); return true; }
        var next = Array.AsReadOnly(normalized.ToArray());
        if (changed)
        {
            var args = new ChartSelectionChangingEventArgs(_selection, next, reason, reason is not (ChartSelectionChangeReason.Reconcile or ChartSelectionChangeReason.Configuration));
            SelectionChanging?.Invoke(this, args);
            if (_selectionRevision != revision || args.CanCancel && args.Cancel) { SyncSelectionProperties(); return false; }
            if (reason is not (ChartSelectionChangeReason.Reconcile or ChartSelectionChangeReason.Configuration))
            {
                var latest = CurrentHits(GetScene());
                if (_selectionRevision != revision || next.Any(p => !latest.Any(q => SameIdentity(q, p)))) { SyncSelectionProperties(); return false; }
            }
        }
        var previous = _selection; _selection = next; SyncSelectionProperties(); RequestPolarRender();
        if (changed) SelectionChanged?.Invoke(this, new(previous, next)); return true;
    }
    private void SyncSelectionProperties()
    {
        _syncSelection = true;
        try { SetCurrentValue(SelectedPointsProperty, _selection); SetCurrentValue(SelectedPointProperty, _selection.LastOrDefault()); }
        finally { _syncSelection = false; }
    }
}
