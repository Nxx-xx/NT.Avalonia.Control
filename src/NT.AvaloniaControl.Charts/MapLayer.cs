using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>A data layer owned by exactly one map; binding values inherit the map's DataContext.</summary>
public abstract class MapLayer : StyledElement
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<MapLayer, IEnumerable?>(nameof(ItemsSource));
    public static readonly StyledProperty<string?> IdPathProperty = AvaloniaProperty.Register<MapLayer, string?>(nameof(IdPath), "Id");
    public static readonly StyledProperty<string?> RegionPathProperty = AvaloniaProperty.Register<MapLayer, string?>(nameof(RegionPath), "RegionId");
    public static readonly StyledProperty<string?> ValuePathProperty = AvaloniaProperty.Register<MapLayer, string?>(nameof(ValuePath), "Value");
    public static readonly StyledProperty<string?> LongitudePathProperty = AvaloniaProperty.Register<MapLayer, string?>(nameof(LongitudePath), "Longitude");
    public static readonly StyledProperty<string?> LatitudePathProperty = AvaloniaProperty.Register<MapLayer, string?>(nameof(LatitudePath), "Latitude");
    public static readonly StyledProperty<string?> SizePathProperty = AvaloniaProperty.Register<MapLayer, string?>(nameof(SizePath));
    public static readonly StyledProperty<string?> LabelPathProperty = AvaloniaProperty.Register<MapLayer, string?>(nameof(LabelPath), "Label");
    public static readonly StyledProperty<string?> FillPathProperty = AvaloniaProperty.Register<MapLayer, string?>(nameof(FillPath));
    public static readonly StyledProperty<bool> IsVisibleProperty = AvaloniaProperty.Register<MapLayer, bool>(nameof(IsVisible), true);
    public static readonly StyledProperty<double> OpacityProperty = AvaloniaProperty.Register<MapLayer, double>(nameof(Opacity), 1, validate: x => double.IsFinite(x) && x >= 0 && x <= 1);
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<MapLayer, IBrush?>(nameof(Fill), Brush.Parse("#1598B6"));
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<MapLayer, IBrush?>(nameof(Stroke), Brushes.White);
    internal ShapeMap? Owner { get; set; }
    internal event EventHandler? Changed;
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public string? IdPath { get => GetValue(IdPathProperty); set => SetValue(IdPathProperty, value); }
    public string? RegionPath { get => GetValue(RegionPathProperty); set => SetValue(RegionPathProperty, value); }
    public string? ValuePath { get => GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
    public string? LongitudePath { get => GetValue(LongitudePathProperty); set => SetValue(LongitudePathProperty, value); }
    public string? LatitudePath { get => GetValue(LatitudePathProperty); set => SetValue(LatitudePathProperty, value); }
    public string? SizePath { get => GetValue(SizePathProperty); set => SetValue(SizePathProperty, value); }
    public string? LabelPath { get => GetValue(LabelPathProperty); set => SetValue(LabelPathProperty, value); }
    public string? FillPath { get => GetValue(FillPathProperty); set => SetValue(FillPathProperty, value); }
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
    public double Opacity { get => GetValue(OpacityProperty); set => SetValue(OpacityProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) { base.OnPropertyChanged(change); Changed?.Invoke(this, EventArgs.Empty); }
}

/// <summary>Feature-keyed colors. Missing/null values draw only the nonselectable no-data background.</summary>
public sealed class ShapeLayer : MapLayer
{
    public static readonly StyledProperty<MapColorMode> ColorModeProperty = AvaloniaProperty.Register<ShapeLayer, MapColorMode>(nameof(ColorMode), MapColorMode.Continuous, validate: Enum.IsDefined);
    public static readonly StyledProperty<string?> CategoryPathProperty = AvaloniaProperty.Register<ShapeLayer, string?>(nameof(CategoryPath));
    public static readonly StyledProperty<IReadOnlyDictionary<object, IBrush>?> CategoryColorsProperty = AvaloniaProperty.Register<ShapeLayer, IReadOnlyDictionary<object, IBrush>?>(nameof(CategoryColors));
    public static readonly StyledProperty<Color> MinimumColorProperty = AvaloniaProperty.Register<ShapeLayer, Color>(nameof(MinimumColor), Color.Parse("#D8EEF2"));
    public static readonly StyledProperty<Color> MaximumColorProperty = AvaloniaProperty.Register<ShapeLayer, Color>(nameof(MaximumColor), Color.Parse("#087C98"));
    public static readonly StyledProperty<IBrush?> NoDataBrushProperty = AvaloniaProperty.Register<ShapeLayer, IBrush?>(nameof(NoDataBrush), Brush.Parse("#D5DCE2"));
    public Color MinimumColor { get => GetValue(MinimumColorProperty); set => SetValue(MinimumColorProperty, value); }
    public Color MaximumColor { get => GetValue(MaximumColorProperty); set => SetValue(MaximumColorProperty, value); }
    public IBrush? NoDataBrush { get => GetValue(NoDataBrushProperty); set => SetValue(NoDataBrushProperty, value); }
    public MapColorMode ColorMode { get => GetValue(ColorModeProperty); set => SetValue(ColorModeProperty, value); }
    public string? CategoryPath { get => GetValue(CategoryPathProperty); set => SetValue(CategoryPathProperty, value); }
    /// <summary>Explicit immutable category-to-brush mapping; replace this value to notify a binding update.</summary>
    public IReadOnlyDictionary<object, IBrush>? CategoryColors { get => GetValue(CategoryColorsProperty); set => SetValue(CategoryColorsProperty, value); }
}

/// <summary>Area encodes nonnegative size; zero is retained in the snapshot and has no visible mark.</summary>
public sealed class BubbleLayer : MapLayer
{
    public static readonly StyledProperty<double> MinimumRadiusProperty = AvaloniaProperty.Register<BubbleLayer, double>(nameof(MinimumRadius), 0, validate: x => double.IsFinite(x) && x >= 0 && x <= .5);
    public static readonly StyledProperty<double> MaximumRadiusProperty = AvaloniaProperty.Register<BubbleLayer, double>(nameof(MaximumRadius), .06, validate: x => double.IsFinite(x) && x >= 0 && x <= .5);
    public double MinimumRadius { get => GetValue(MinimumRadiusProperty); set => SetValue(MinimumRadiusProperty, value); }
    public double MaximumRadius { get => GetValue(MaximumRadiusProperty); set => SetValue(MaximumRadiusProperty, value); }
}

/// <summary>A bounded grid kernel whose cells retain all original contributing observations.</summary>
public sealed class HeatmapLayer : MapLayer
{
    public static readonly StyledProperty<int> ColumnsProperty = AvaloniaProperty.Register<HeatmapLayer, int>(nameof(Columns), 32, validate: x => x >= 1 && x <= 256);
    public static readonly StyledProperty<int> RowsProperty = AvaloniaProperty.Register<HeatmapLayer, int>(nameof(Rows), 32, validate: x => x >= 1 && x <= 256);
    public static readonly StyledProperty<int> RadiusProperty = AvaloniaProperty.Register<HeatmapLayer, int>(nameof(Radius), 1, validate: x => x >= 0 && x <= ChartMaps.MaximumKernelRadius);
    public static readonly StyledProperty<Color> MinimumColorProperty = AvaloniaProperty.Register<HeatmapLayer, Color>(nameof(MinimumColor), Color.Parse("#FFF3B0"));
    public static readonly StyledProperty<Color> MaximumColorProperty = AvaloniaProperty.Register<HeatmapLayer, Color>(nameof(MaximumColor), Color.Parse("#D33145"));
    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    public int Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public int Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
    public Color MinimumColor { get => GetValue(MinimumColorProperty); set => SetValue(MinimumColorProperty, value); }
    public Color MaximumColor { get => GetValue(MaximumColorProperty); set => SetValue(MaximumColorProperty, value); }
}

/// <summary>A bounded ordered layer collection. A layer cannot be shared by maps or inserted twice.</summary>
public sealed class MapLayerCollection : ObservableCollection<MapLayer>
{
    private readonly ShapeMap _owner;
    private bool _mutating;
    internal MapLayerCollection(ShapeMap owner) => _owner = owner;
    protected override void InsertItem(int index, MapLayer item) => Mutate(() => { _owner.VerifyLayerMutation(); Validate(item, false); Items.Insert(index, item); _owner.AttachLayer(item); Notify(new(NotifyCollectionChangedAction.Add, item, index), true); });
    protected override void SetItem(int index, MapLayer item) => Mutate(() => { _owner.VerifyLayerMutation(); if (ReferenceEquals(this[index], item)) return; Validate(item, true); var old = this[index]; Items[index] = item; _owner.DetachLayer(old); _owner.AttachLayer(item); Notify(new(NotifyCollectionChangedAction.Replace, item, old, index), false); });
    protected override void RemoveItem(int index) => Mutate(() => { _owner.VerifyLayerMutation(); var old = this[index]; Items.RemoveAt(index); _owner.DetachLayer(old); Notify(new(NotifyCollectionChangedAction.Remove, old, index), true); });
    protected override void ClearItems() => Mutate(() => { _owner.VerifyLayerMutation(); ClearCore(); });
    internal void ClearForOwnerDispose() => Mutate(ClearCore);
    private void ClearCore() { var old = this.ToArray(); Items.Clear(); foreach (var layer in old) _owner.DetachLayer(layer); Notify(new(NotifyCollectionChangedAction.Reset), true); }
    protected override void MoveItem(int oldIndex, int newIndex) => Mutate(() => { _owner.VerifyLayerMutation(); var item = this[oldIndex]; Items.RemoveAt(oldIndex); Items.Insert(newIndex, item); Notify(new(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex), false); });
    private void Mutate(Action action) { if (_mutating) throw new InvalidOperationException("A map layer collection cannot be mutated during its own change notification."); CheckReentrancy(); _mutating = true; try { action(); } finally { _mutating = false; } }
    private void Notify(NotifyCollectionChangedEventArgs args, bool count) { if (count) OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count))); OnPropertyChanged(new PropertyChangedEventArgs("Item[]")); OnCollectionChanged(args); }
    private void Validate(MapLayer layer, bool replacing) { ArgumentNullException.ThrowIfNull(layer); if (layer.Owner is not null || Contains(layer)) throw new InvalidOperationException("A map layer belongs to exactly one map."); if (!replacing && Count >= 16) throw new ArgumentOutOfRangeException(nameof(layer), "A map supports at most 16 layers."); }
}
