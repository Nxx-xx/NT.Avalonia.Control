using Avalonia;
using Avalonia.Data;
using Avalonia.Input;

namespace NT.AvaloniaControl.Charts;

public sealed partial class SeatMap
{
    public static readonly StyledProperty<bool> IsMultiSelectionEnabledProperty = AvaloniaProperty.Register<SeatMap, bool>(nameof(IsMultiSelectionEnabled));
    public static readonly StyledProperty<IReadOnlyList<object>> SelectedItemsProperty = AvaloniaProperty.Register<SeatMap, IReadOnlyList<object>>(nameof(SelectedItems), Array.Empty<object>(), defaultBindingMode: BindingMode.TwoWay);
    private readonly HashSet<MapGlyphKey> _selectedSeatKeys = [];
    private bool _publishingSeats, _seatsFromItems = true;
    public bool IsMultiSelectionEnabled { get => GetValue(IsMultiSelectionEnabledProperty); set => SetValue(IsMultiSelectionEnabledProperty, value); }
    /// <summary>Original selected region business rows. Replaced immutable lists support TwoWay binding; item INCC is not used as a selection command.</summary>
    public IReadOnlyList<object> SelectedItems { get { _ = GetMarks(); return GetValue(SelectedItemsProperty); } set => SetValue(SelectedItemsProperty, value ?? Array.Empty<object>()); }
    public bool ToggleSeat(object idOrSource)
    {
        VerifyAccess(); if (!IsMultiSelectionEnabled || !IsSelectionEnabled) return false;
        var mark = GetMarks().FirstOrDefault(m => m.Key is MapGlyphKey { Kind: MapElementKind.Region } key &&
            (key.HasExplicitId && Equals(key.Identity, idOrSource) || ReferenceEquals(key.Identity, idOrSource) || m.Members.Any(p => ReferenceEquals(p.Item, idOrSource))));
        return mark is not null && Toggle(mark);
    }
    public void ClearSeatSelection() { VerifyAccess(); _selectedSeatKeys.Clear(); _seatsFromItems = false; PublishSeats([]); InvalidateEngineering(); }
    private bool Toggle(EngineeringMarkInfo mark)
    {
        if (mark.Key is not MapGlyphKey { Kind: MapElementKind.Region } key) return false;
        _seatsFromItems = false; if (!_selectedSeatKeys.Remove(key)) _selectedSeatKeys.Add(key);
        SetCurrentValue(SelectedIndexProperty, -1); ReconcileMapSelection(GetMarks()); InvalidateEngineering(); return true;
    }
    protected override bool IsMapMarkSelected(EngineeringMarkInfo mark) => IsMultiSelectionEnabled && mark.Key is MapGlyphKey key && _selectedSeatKeys.Contains(key);
    protected override void ReconcileMapSelection(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (!IsMultiSelectionEnabled) return;
        var regions = marks.Where(m => m.Key is MapGlyphKey { Kind: MapElementKind.Region }).ToArray();
        if (_seatsFromItems)
        {
            _selectedSeatKeys.Clear();
            foreach (var mark in regions) if (SelectedItems.Any(item => mark.Members.Any(m => ReferenceEquals(m.Item, item)))) _selectedSeatKeys.Add((MapGlyphKey)mark.Key!);
            _seatsFromItems = false;
        }
        _selectedSeatKeys.IntersectWith(regions.Select(m => (MapGlyphKey)m.Key!));
        var sources = regions.Where(m => _selectedSeatKeys.Contains((MapGlyphKey)m.Key!)).SelectMany(m => m.Members.Select(p => p.Item)).Where(item => item is not null).Cast<object>().Distinct(ReferenceEqualityComparer.Instance).ToArray();
        PublishSeats(sources);
    }
    private void PublishSeats(IReadOnlyList<object> items)
    {
        if (SelectedItems.Count == items.Count && SelectedItems.Zip(items).All(p => ReferenceEquals(p.First, p.Second))) return;
        _publishingSeats = true; try { SetCurrentValue(SelectedItemsProperty, Array.AsReadOnly(items.ToArray())); } finally { _publishingSeats = false; }
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == SelectedItemsProperty && !_publishingSeats) { _seatsFromItems = true; _selectedSeatKeys.Clear(); }
        if (change.Property == IsMultiSelectionEnabledProperty)
        {
            if (IsMultiSelectionEnabled) { _seatsFromItems = true; SetCurrentValue(SelectedIndexProperty, -1); }
            else { _selectedSeatKeys.Clear(); PublishSeats([]); }
        }
        base.OnPropertyChanged(change);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (IsMultiSelectionEnabled && IsSelectionEnabled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && HitTest(e.GetPosition(this)) is { } hit && hit.Mark.Key is MapGlyphKey { Kind: MapElementKind.Region })
        { Focus(); Toggle(hit.Mark); e.Handled = true; return; }
        base.OnPointerPressed(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsMultiSelectionEnabled && IsSelectionEnabled && e.Key == Key.Space && (ActivePoint ?? SelectedPoint) is { } hit && Toggle(hit.Mark)) { e.Handled = true; return; }
        if (IsMultiSelectionEnabled && e.Key == Key.Escape) ClearSeatSelection();
        base.OnKeyDown(e);
    }
    protected override void ClearProjection(Exception error) { _selectedSeatKeys.Clear(); _seatsFromItems = false; PublishSeats([]); base.ClearProjection(error); }
}
