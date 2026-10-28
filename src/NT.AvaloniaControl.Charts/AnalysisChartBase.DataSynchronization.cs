using Avalonia;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public abstract partial class AnalysisChartBase
{
    private readonly List<WeakReference<ChartDataSynchronizationGroup>> _dataSynchronizationGroups = [];
    private readonly List<Visual> _dataSynchronizationAncestors = [];
    private readonly List<(WeakReference<ChartDataSynchronizationGroup> Group, ChartDataSynchronizationStateAnchor State)> _dataSynchronizationStateAnchors = [];
    private long _dataSynchronizationVersion;
    internal bool CanSynchronizeData => _attached && IsVisible && IsEffectivelyEnabled && this.GetVisualAncestors().All(visual => visual.IsVisible);
    internal long DataSynchronizationVersion => _revision + _dataSynchronizationVersion;
    internal void SetDataSynchronizationStateAnchor(ChartDataSynchronizationGroup group, ChartDataSynchronizationStateAnchor? state)
    {
        _dataSynchronizationStateAnchors.RemoveAll(entry => !entry.Group.TryGetTarget(out var owner) || ReferenceEquals(owner, group));
        if (state is not null) _dataSynchronizationStateAnchors.Add((new(group), state));
    }

    internal void RegisterDataSynchronizationGroup(ChartDataSynchronizationGroup group)
    {
        _dataSynchronizationGroups.RemoveAll(reference => !reference.TryGetTarget(out _));
        if (!_dataSynchronizationGroups.Any(reference => reference.TryGetTarget(out var current) && ReferenceEquals(current, group))) _dataSynchronizationGroups.Add(new(group));
        RefreshDataSynchronizationMembership();
    }
    internal void UnregisterDataSynchronizationGroup(ChartDataSynchronizationGroup group)
    {
        _dataSynchronizationGroups.RemoveAll(reference => !reference.TryGetTarget(out var current) || ReferenceEquals(current, group));
        RefreshDataSynchronizationMembership();
    }
    private void DataSynchronizationAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    { if (change.Property == IsVisibleProperty || change.Property == IsEnabledProperty || change.Property == IsEffectivelyEnabledProperty) RefreshDataSynchronizationMembership(); }
    private void RefreshDataSynchronizationMembership()
    {
        _dataSynchronizationGroups.RemoveAll(reference => !reference.TryGetTarget(out _));
        var ancestors = _attached && _dataSynchronizationGroups.Count != 0 ? this.GetVisualAncestors().ToArray() : [];
        foreach (var removed in _dataSynchronizationAncestors.Except(ancestors).ToArray())
        { removed.PropertyChanged -= DataSynchronizationAncestorChanged; _dataSynchronizationAncestors.Remove(removed); }
        foreach (var added in ancestors.Except(_dataSynchronizationAncestors))
        { added.PropertyChanged += DataSynchronizationAncestorChanged; _dataSynchronizationAncestors.Add(added); }
        foreach (var reference in _dataSynchronizationGroups.ToArray()) if (reference.TryGetTarget(out var group)) group.RefreshMembership(this);
    }
    private void DataSynchronizationPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsVisibleProperty || change.Property == IsEnabledProperty || change.Property == IsEffectivelyEnabledProperty) RefreshDataSynchronizationMembership();
        if (change.Property == SelectedDataKeysProperty || change.Property == DataSelectionRangeProperty ||
            change.Property == ItemsSourceProperty || change.Property == IdPathProperty) _dataSynchronizationVersion++;
        if (change.Property == ItemsSourceProperty || change.Property == IdPathProperty)
            foreach (var entry in _dataSynchronizationStateAnchors) { entry.State.LastSelection = null; entry.State.LastKeys = null; }
    }
}
