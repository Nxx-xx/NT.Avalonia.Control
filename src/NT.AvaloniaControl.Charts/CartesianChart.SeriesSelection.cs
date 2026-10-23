using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>A proposed change to the selected series, identified by reference rather than source records.</summary>
public sealed class ChartSeriesSelectionChangingEventArgs(IReadOnlyList<ChartSeries> oldSelection,
    IReadOnlyList<ChartSeries> newSelection, ChartSelectionChangeReason reason, bool canCancel) : EventArgs
{
    public IReadOnlyList<ChartSeries> OldSelection { get; } = oldSelection;
    public IReadOnlyList<ChartSeries> NewSelection { get; } = newSelection;
    public ChartSelectionChangeReason Reason { get; } = reason;
    /// <summary>False for mode changes and removal or hiding of selected series.</summary>
    public bool CanCancel { get; } = canCancel;
    /// <summary>Ignored when CanCancel is false.</summary>
    public bool Cancel { get; set; }
}

/// <summary>A committed change to the immutable series selection.</summary>
public sealed class ChartSeriesSelectionChangedEventArgs(IReadOnlyList<ChartSeries> oldSelection,
    IReadOnlyList<ChartSeries> newSelection, ChartSelectionChangeReason reason) : EventArgs
{
    public IReadOnlyList<ChartSeries> OldSelection { get; } = oldSelection;
    public IReadOnlyList<ChartSeries> NewSelection { get; } = newSelection;
    public ChartSelectionChangeReason Reason { get; } = reason;
}

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<ChartSelectionMode> SeriesSelectionModeProperty =
        AvaloniaProperty.Register<CartesianChart, ChartSelectionMode>(nameof(SeriesSelectionMode), ChartSelectionMode.None, validate: Enum.IsDefined);
    public static readonly StyledProperty<IReadOnlyList<ChartSeries>?> SelectedSeriesProperty =
        AvaloniaProperty.Register<CartesianChart, IReadOnlyList<ChartSeries>?>(nameof(SelectedSeries), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<double> UnselectedSeriesOpacityProperty =
        AvaloniaProperty.Register<CartesianChart, double>(nameof(UnselectedSeriesOpacity), .25,
            validate: value => double.IsFinite(value) && value >= 0 && value <= 1);

    private IReadOnlyList<ChartSeries> _publishedSeriesSelection = Array.Empty<ChartSeries>();
    private HashSet<ChartSeries> _selectedSeriesIdentities = new(ReferenceEqualityComparer.Instance);
    private long _seriesSelectionRevision;
    private bool _synchronizingSeriesSelection;
    private bool _raisingSeriesSelectionChanging;
    private bool _seriesSelectionBindingRollbackPending;

    /// <summary>Independent of point selection. None preserves the chart's existing click behavior.</summary>
    public ChartSelectionMode SeriesSelectionMode { get => GetValue(SeriesSelectionModeProperty); set => SetValue(SeriesSelectionModeProperty, value); }
    /// <summary>Immutable, reference-identified series selection. Replace the list to update a binding.</summary>
    public IReadOnlyList<ChartSeries>? SelectedSeries { get => GetValue(SelectedSeriesProperty); set => SetValue(SelectedSeriesProperty, value); }
    /// <summary>Opacity of rendered unselected series when any series is selected. No data or axis state is changed.</summary>
    public double UnselectedSeriesOpacity { get => GetValue(UnselectedSeriesOpacityProperty); set => SetValue(UnselectedSeriesOpacityProperty, value); }

    /// <summary>
    /// Callback selections supersede the outer request and do not recursively raise Changing.
    /// Canceling an external value restores the canonical value without replacing its binding.
    /// </summary>
    public event EventHandler<ChartSeriesSelectionChangingEventArgs>? SeriesSelectionChanging;
    public event EventHandler<ChartSeriesSelectionChangedEventArgs>? SeriesSelectionChanged;

    /// <summary>
    /// Selects a visible series owned by this chart. In Multiple mode, additive retains existing series;
    /// repeated calls are idempotent. Pointer Ctrl-click toggles an individual series instead.
    /// Returns false for an invalid target, a canceled change, or a superseded request.
    /// </summary>
    public bool SelectSeries(ChartSeries series, bool additive = false)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(series);
        if (SeriesSelectionMode == ChartSelectionMode.None || !series.EffectiveVisibility || !ActiveSeries.Any(value => ReferenceEquals(value, series))) return false;
        return PublishSeriesSelection(additive && SeriesSelectionMode == ChartSelectionMode.Multiple
            ? _publishedSeriesSelection.Append(series) : new[] { series }, ChartSelectionChangeReason.ExternalValue);
    }

    public bool ClearSeriesSelection()
    {
        VerifyAccess();
        return PublishSeriesSelection(Array.Empty<ChartSeries>(), ChartSelectionChangeReason.Clear);
    }

    private List<ChartSeries> NormalizeSeriesSelection(IEnumerable<ChartSeries> values)
    {
        var result = new List<ChartSeries>();
        if (SeriesSelectionMode == ChartSelectionMode.None) return result;
        var owned = new HashSet<ChartSeries>(ActiveSeries.Where(value => value is not null), ReferenceEqualityComparer.Instance);
        var seen = new HashSet<ChartSeries>(ReferenceEqualityComparer.Instance);
        foreach (var series in values)
        {
            if (series is null || !series.EffectiveVisibility || !owned.Contains(series) || !seen.Add(series)) continue;
            result.Add(series);
            if (SeriesSelectionMode != ChartSelectionMode.Multiple) break;
        }
        return result;
    }

    private static bool SameSeriesSelection(IReadOnlyList<ChartSeries> left, IReadOnlyList<ChartSeries> right)
    {
        if (left.Count != right.Count) return false;
        for (var index = 0; index < left.Count; index++)
            if (!ReferenceEquals(left[index], right[index])) return false;
        return true;
    }

    private bool PublishSeriesSelection(IEnumerable<ChartSeries> values, ChartSelectionChangeReason reason)
    {
        var revision = reason == ChartSelectionChangeReason.Reconcile ? _seriesSelectionRevision : ++_seriesSelectionRevision;
        List<ChartSeries> normalized;
        try { normalized = NormalizeSeriesSelection(values); }
        catch { SynchronizeSeriesSelectionProperty(); throw; }
        if (_seriesSelectionRevision != revision) { SynchronizeSeriesSelectionProperty(); return false; }
        if (SameSeriesSelection(_publishedSeriesSelection, normalized))
        {
            SynchronizeSeriesSelectionProperty();
            return true;
        }
        if (reason == ChartSelectionChangeReason.Reconcile) revision = ++_seriesSelectionRevision;
        var previous = _publishedSeriesSelection;
        var current = Array.AsReadOnly(normalized.ToArray());
        if (!_raisingSeriesSelectionChanging && SeriesSelectionChanging is { } changing)
        {
            var canCancel = reason is not (ChartSelectionChangeReason.Reconcile or ChartSelectionChangeReason.Configuration);
            var args = new ChartSeriesSelectionChangingEventArgs(previous, current, reason, canCancel);
            _raisingSeriesSelectionChanging = true;
            try
            {
                changing(this, args);
                if (_seriesSelectionRevision != revision) { SynchronizeSeriesSelectionProperty(); return false; }
                var stillCurrent = NormalizeSeriesSelection(current);
                if (_seriesSelectionRevision != revision) { SynchronizeSeriesSelectionProperty(); return false; }
                if (!SameSeriesSelection(current, stillCurrent) || canCancel && args.Cancel)
                {
                    ReconcileSeriesSelection();
                    SynchronizeSeriesSelectionProperty();
                    return false;
                }
            }
            catch
            {
                ReconcileSeriesSelection();
                SynchronizeSeriesSelectionProperty();
                throw;
            }
            finally { _raisingSeriesSelectionChanging = false; }
        }
        _publishedSeriesSelection = current;
        _selectedSeriesIdentities = new(current, ReferenceEqualityComparer.Instance);
        SynchronizeSeriesSelectionProperty();
        // A binding or property observer may synchronously choose a newer state.
        if (_seriesSelectionRevision != revision) return false;
        try { SeriesSelectionChanged?.Invoke(this, new(previous, current, reason)); }
        finally { RequestRender(); }
        return _seriesSelectionRevision == revision;
    }

    private void SynchronizeSeriesSelectionProperty()
    {
        if (ReferenceEquals(SelectedSeries, _publishedSeriesSelection)) return;
        var wasSynchronizing = _synchronizingSeriesSelection;
        _synchronizingSeriesSelection = true;
        try { SetCurrentValue(SelectedSeriesProperty, _publishedSeriesSelection); }
        finally { _synchronizingSeriesSelection = wasSynchronizing; }
    }

    private bool SeriesSelectionPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == SelectedSeriesProperty)
        {
            if (_synchronizingSeriesSelection && ReferenceEquals(SelectedSeries, _publishedSeriesSelection)) return true;
            try { PublishSeriesSelection(SelectedSeries ?? Array.Empty<ChartSeries>(), ChartSelectionChangeReason.ExternalValue); }
            finally { QueueSeriesSelectionBindingRollback(); }
            return true;
        }
        if (change.Property == SeriesSelectionModeProperty)
        {
            PublishSeriesSelection(_publishedSeriesSelection, ChartSelectionChangeReason.Configuration);
            RequestRender();
            return true;
        }
        if (change.Property == UnselectedSeriesOpacityProperty) { RequestRender(); return true; }
        return false;
    }

    private void QueueSeriesSelectionBindingRollback()
    {
        if (_seriesSelectionBindingRollbackPending) return;
        _seriesSelectionBindingRollbackPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _seriesSelectionBindingRollbackPending = false;
            SynchronizeSeriesSelectionProperty();
            BindingOperations.GetBindingExpressionBase(this, SelectedSeriesProperty)?.UpdateSource();
        });
    }

    private void ReconcileSeriesSelection() => PublishSeriesSelection(_publishedSeriesSelection, ChartSelectionChangeReason.Reconcile);

    private void SelectSeriesFromPointer(ChartSeries? series, KeyModifiers modifiers)
    {
        if (SeriesSelectionMode == ChartSelectionMode.None) return;
        if (series is null) { PublishSeriesSelection(Array.Empty<ChartSeries>(), ChartSelectionChangeReason.Pointer); return; }
        if (SeriesSelectionMode == ChartSelectionMode.SingleDeselect && _selectedSeriesIdentities.Contains(series))
        { PublishSeriesSelection(Array.Empty<ChartSeries>(), ChartSelectionChangeReason.Pointer); return; }
        if (SeriesSelectionMode == ChartSelectionMode.Multiple && (modifiers & KeyModifiers.Control) != 0)
        {
            var values = _publishedSeriesSelection.ToList();
            var index = values.FindIndex(value => ReferenceEquals(value, series));
            if (index >= 0) values.RemoveAt(index); else values.Add(series);
            PublishSeriesSelection(values, ChartSelectionChangeReason.Pointer);
            return;
        }
        PublishSeriesSelection(new[] { series }, ChartSelectionChangeReason.Pointer);
    }

    private void SelectSeriesFromKeyboard(ChartSeries series)
    {
        if (SeriesSelectionMode != ChartSelectionMode.None)
            PublishSeriesSelection(new[] { series }, ChartSelectionChangeReason.Keyboard);
    }

    private bool HandleSeriesSelectionKey(KeyEventArgs e)
    {
        // With point navigation enabled the existing point path supplies the
        // corresponding series. Series-only navigation never enumerates records.
        if (!IsKeyboardNavigationEnabled || e.Handled || SelectionMode != ChartSelectionMode.None ||
            SeriesSelectionMode == ChartSelectionMode.None || e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return false;
        var series = ActiveSeries.Where(value => value is not null && value.EffectiveVisibility).ToArray();
        if (series.Length == 0) return false;
        var forward = e.Key is Key.Right or Key.Down;
        var current = _publishedSeriesSelection.LastOrDefault();
        var index = Array.FindIndex(series, value => ReferenceEquals(value, current));
        var target = index < 0 ? (forward ? 0 : series.Length - 1) : Math.Clamp(index + (forward ? 1 : -1), 0, series.Length - 1);
        PublishSeriesSelection(new[] { series[target] }, ChartSelectionChangeReason.Keyboard);
        e.Handled = true;
        return true;
    }

    private double GetSeriesSelectionOpacity(ChartSeries series) => _publishedSeriesSelection.Count == 0 || _selectedSeriesIdentities.Contains(series) ? 1 : UnselectedSeriesOpacity;
}
