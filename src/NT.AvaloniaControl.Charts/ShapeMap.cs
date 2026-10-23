using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public enum MapSourceState { Idle, Loading, Completed, Canceled, Failed }
public enum MapElementKind { Region, Bubble, HeatmapCell, Marker, Connection, Vector, PieSlice }
/// <summary>Layer-scoped identity. Implicit identities compare by reference; explicit business IDs compare by value.</summary>
public sealed record MapGlyphKey(MapLayer Layer, MapElementKind Kind, object Identity, bool HasExplicitId)
{
    public bool Equals(MapGlyphKey? other) => other is not null && ReferenceEquals(Layer, other.Layer) && Kind == other.Kind && HasExplicitId == other.HasExplicitId && (HasExplicitId ? Equals(Identity, other.Identity) : ReferenceEquals(Identity, other.Identity));
    public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(Layer), Kind, HasExplicitId, HasExplicitId ? Identity.GetHashCode() : RuntimeHelpers.GetHashCode(Identity));
}
/// <summary>The immutable numerical result used to paint one original data layer.</summary>
public sealed record MapLayerSnapshot(MapLayer Layer, MapJoinResult? Regions, MapBubbleLayout? Bubbles, MapHeatmapLayout? Heatmap)
{
    public MapPointLayout? Points { get; init; }
    public MapConnectionLayout? Connections { get; init; }
    public MapVectorLayout? Vectors { get; init; }
    public MapPieLayout? Pies { get; init; }
}
/// <summary>One aspect-preserving world fit before the shared map viewport is applied.</summary>
public sealed record MapViewportInfo(MapBounds Bounds, Rect FittedArea, double Zoom, double PanX, double PanY);

/// <summary>Offline layered shape map with original-data selection, holes, projection and shared pan/zoom.</summary>
public partial class ShapeMap : EngineeringChartBase, IDisposable
{
    public static readonly StyledProperty<IEnumerable<MapFeature>?> FeaturesSourceProperty = AvaloniaProperty.Register<ShapeMap, IEnumerable<MapFeature>?>(nameof(FeaturesSource));
    public static readonly StyledProperty<string?> GeoJsonProperty = AvaloniaProperty.Register<ShapeMap, string?>(nameof(GeoJson));
    public static readonly StyledProperty<Uri?> SourceUriProperty = AvaloniaProperty.Register<ShapeMap, Uri?>(nameof(SourceUri));
    public static readonly StyledProperty<string?> JsonIdPropertyProperty = AvaloniaProperty.Register<ShapeMap, string?>(nameof(JsonIdProperty));
    public static readonly StyledProperty<string?> JsonLabelPropertyProperty = AvaloniaProperty.Register<ShapeMap, string?>(nameof(JsonLabelProperty), "name");
    public static readonly StyledProperty<MapProjection> ProjectionProperty = AvaloniaProperty.Register<ShapeMap, MapProjection>(nameof(Projection), MapProjection.Equirectangular, validate: Enum.IsDefined);
    public static readonly StyledProperty<MapTopologyRule> TopologyRuleProperty = AvaloniaProperty.Register<ShapeMap, MapTopologyRule>(nameof(TopologyRule), MapTopologyRule.StrictSimple, validate: Enum.IsDefined);
    public static readonly StyledProperty<bool> IsGeographicProperty = AvaloniaProperty.Register<ShapeMap, bool>(nameof(IsGeographic), true);
    public static readonly StyledProperty<bool> ShowLabelsProperty = AvaloniaProperty.Register<ShapeMap, bool>(nameof(ShowLabels), true);
    public static readonly StyledProperty<bool> ShowLegendProperty = AvaloniaProperty.Register<ShapeMap, bool>(nameof(ShowLegend), true);
    public static readonly DirectProperty<ShapeMap, MapSourceState> SourceStateProperty = AvaloniaProperty.RegisterDirect<ShapeMap, MapSourceState>(nameof(SourceState), c => c.SourceState);
    public static readonly DirectProperty<ShapeMap, Exception?> SourceErrorProperty = AvaloniaProperty.RegisterDirect<ShapeMap, Exception?>(nameof(SourceError), c => c.SourceError);
    private bool _attached, _disposed, _sourceDirty = true, _geometryDirty = true, _dataDirty = true, _synchronizing, _resynchronize;
    private long _dataRevision;
    private MapGeometrySnapshot? _snapshot;
    private MapProjectedGeometry? _projected;
    private IReadOnlyList<MapLayerSnapshot> _layerSnapshots = [];
    private MapBounds? _worldBounds;
    private MapViewportInfo? _viewport;
    private readonly Dictionary<EngineeringMarkInfo, PaintedMapMark> _painted = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<MapGlyphKey, int> _indices = [];
    private readonly List<MapPaintCommand> _commands = [];
    private readonly Dictionary<EngineeringMarkInfo, int> _commandIndices = new(ReferenceEqualityComparer.Instance);
    private readonly List<(string Text, Point Position)> _labels = [];
    private readonly List<(MapLayer Layer, Color Minimum, Color Maximum, double Low, double High)> _legends = [];
    private readonly List<(MapLayer Layer, object Category, IBrush Brush)> _categoryLegends = [];
    private readonly Dictionary<(ShapeLayer Layer, object Category), int> _categoryIndices = [];
    private readonly HashSet<(MapLayer Layer, object Category)> _categoryLegendKeys = [];
    private sealed class ObserverState { public bool PropertyAttached; public bool CollectionAttached; }
    private readonly Dictionary<object, ObserverState> _observed = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _desired = new(ReferenceEqualityComparer.Instance);
    public ShapeMap() { ShowGrid = false; Layers = new(this); Layers.CollectionChanged += LayersChanged; }
    public MapLayerCollection Layers { get; }
    public IEnumerable<MapFeature>? FeaturesSource { get => GetValue(FeaturesSourceProperty); set => SetValue(FeaturesSourceProperty, value); }
    public string? GeoJson { get => GetValue(GeoJsonProperty); set => SetValue(GeoJsonProperty, value); }
    /// <summary>An explicit local file or avares URI; call LoadSourceAsync to read it. Network schemes are rejected.</summary>
    public Uri? SourceUri { get => GetValue(SourceUriProperty); set => SetValue(SourceUriProperty, value); }
    public string? JsonIdProperty { get => GetValue(JsonIdPropertyProperty); set => SetValue(JsonIdPropertyProperty, value); }
    public string? JsonLabelProperty { get => GetValue(JsonLabelPropertyProperty); set => SetValue(JsonLabelPropertyProperty, value); }
    public MapProjection Projection { get => GetValue(ProjectionProperty); set => SetValue(ProjectionProperty, value); }
    /// <summary>StrictSimple rejects crossings; explicit EvenOdd permits nonzero signed-area crossings with a diagnostic count.</summary>
    public MapTopologyRule TopologyRule { get => GetValue(TopologyRuleProperty); set => SetValue(TopologyRuleProperty, value); }
    public bool IsGeographic { get => GetValue(IsGeographicProperty); set => SetValue(IsGeographicProperty, value); }
    public bool ShowLabels { get => GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    public MapSourceState SourceState => _sourceState;
    public Exception? SourceError => _sourceError;
    public MapGeometrySnapshot? Snapshot { get { _ = GetMarks(); return _snapshot; } }
    public MapProjectedGeometry? ProjectedGeometry { get { _ = GetMarks(); return _projected; } }
    public IReadOnlyList<MapLayerSnapshot> LayerSnapshots { get { _ = GetMarks(); return _layerSnapshots; } }
    public MapViewportInfo? MapViewport { get { _ = GetMarks(); return _viewport; } }
    public IReadOnlyList<MapValueEntry> UnknownRegions { get { _ = GetMarks(); return Array.AsReadOnly(_layerSnapshots.Where(s => s.Regions is not null).SelectMany(s => s.Regions!.UnknownValues).ToArray()); } }
    public MapProjection EffectiveProjection => IsGeographic ? Projection : MapProjection.Identity;
    /// <summary>Selects a business observation in a specified layer, never an invented feature observation.</summary>
    public bool SelectItem(MapLayer layer, object idOrSource)
    {
        VerifyAccess(); var marks = GetMarks();
        for (var i = 0; i < marks.Count; i++) if (marks[i].Key is MapGlyphKey key && ReferenceEquals(key.Layer, layer) && (key.HasExplicitId && Equals(key.Identity, idOrSource) || ReferenceEquals(key.Identity, idOrSource) || marks[i].Members.Any(m => ReferenceEquals(m.Item, idOrSource) || Equals(m.StableId, idOrSource)))) { SelectedIndex = i; return true; }
        SelectedIndex = -1; return false;
    }
    internal void AttachLayer(MapLayer layer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); layer.Owner = this;
        ((ISetInheritanceParent)layer).SetParent(this); LogicalChildren.Add(layer); layer.Changed += LayerChanged; DirtyData();
    }
    internal void VerifyLayerMutation() { VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); }
    internal void DetachLayer(MapLayer layer)
    {
        layer.Changed -= LayerChanged; if (ReferenceEquals(((ILogical)layer).LogicalParent, this)) LogicalChildren.Remove(layer); ((ISetInheritanceParent)layer).SetParent(null); layer.Owner = null; DirtyData(); ObserveSourceRoots();
    }
    private void LayersChanged(object? sender, NotifyCollectionChangedEventArgs e) { DirtyData(); ObserveSourceRoots(); }
    private void LayerChanged(object? sender, EventArgs e) { DirtyData(); ObserveSourceRoots(); }
    private void DirtyData() { ++_dataRevision; _dataDirty = true; InvalidateEngineering(); }
    protected override void OnDataInvalidated() => DirtyData();
    protected override ChartDataSnapshot CreateEngineeringSnapshot() => new(_dataRevision, Array.Empty<ChartDataPoint>());
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == FeaturesSourceProperty || change.Property == GeoJsonProperty || change.Property == SourceUriProperty || change.Property == JsonIdPropertyProperty || change.Property == JsonLabelPropertyProperty)
        {
            CancelSourceCore(false); _loadedSnapshot = null; _sourceError = null; _sourceState = MapSourceState.Idle; _sourceDirty = true; _geometryDirty = true; DirtyData(); ObserveSourceRoots(); PublishSourceStatus();
        }
        if (change.Property == ProjectionProperty || change.Property == IsGeographicProperty || change.Property == TopologyRuleProperty) { _geometryDirty = true; DirtyData(); }
        base.OnPropertyChanged(change);
    }
    protected override EngineeringPointHit? FindFollowedMark(EngineeringPointHit hit, IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (hit.Mark.Key is not MapGlyphKey key) return null;
        if (key.Kind != MapElementKind.HeatmapCell && _indices.TryGetValue(key, out var index) && index < marks.Count) return new(index, marks[index]);
        // A heat cell can split after a data update; follow its first surviving original observation.
        foreach (var member in hit.Members) for (var i = 0; i < marks.Count; i++)
            if (marks[i].Key is MapGlyphKey current && ReferenceEquals(current.Layer, key.Layer) && marks[i].Members.Any(m => ReferenceEquals(m.Item, member.Item) || member.StableId is not null && Equals(m.StableId, member.StableId))) return new(i, marks[i]);
        return null;
    }
    protected override void ClearProjection(Exception error) { _snapshot = null; _projected = null; _layerSnapshots = []; _worldBounds = null; _viewport = null; _painted.Clear(); _indices.Clear(); _commands.Clear(); _commandIndices.Clear(); _labels.Clear(); _legends.Clear(); _categoryLegends.Clear(); _categoryLegendKeys.Clear(); _geometryDirty = true; _dataDirty = true; }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { _attached = true; _geometryDirty = true; _dataDirty = true; base.OnAttachedToVisualTree(e); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _attached = false; CancelSourceCore(true); ReleaseObservers(); _dataDirty = true; base.OnDetachedFromVisualTree(e); }
    public void Dispose()
    {
        VerifyAccess(); if (_disposed) return; _disposed = true; CancelSourceCore(true); ReleaseObservers(); Layers.ClearForOwnerDispose(); SetCurrentValue(ItemsSourceProperty, null); SetCurrentValue(FeaturesSourceProperty, null); SetCurrentValue(GeoJsonProperty, null); _snapshot = null; _projected = null; _layerSnapshots = []; InvalidateEngineering(); GC.SuppressFinalize(this);
    }
}

/// <summary>A planar map for seats, floor plans and custom diagram regions.</summary>
public sealed partial class SeatMap : ShapeMap
{
    public SeatMap() { IsGeographic = false; Projection = MapProjection.Identity; }
}
