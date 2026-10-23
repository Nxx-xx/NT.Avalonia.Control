using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public partial class ShapeMap
{
    private sealed record LayerInput(MapLayer Layer, MapValue[] Values, MapObservation[] Observations, IReadOnlyDictionary<object, (double Value, IBrush? Fill)> Original)
    {
        public MapConnection[] Connections { get; init; } = [];
        public MapVector[] Vectors { get; init; } = [];
        public MapPie[] Pies { get; init; } = [];
        public IReadOnlyDictionary<object, object> Categories { get; init; } = new Dictionary<object, object>();
    }
    private IReadOnlyList<LayerInput> _inputs = [];
    private static object? Read(object? item, string? path) => !string.IsNullOrWhiteSpace(path) && ChartDataAccessor.TryRead(item, path, out var value) ? value : null;
    private static double Numeric(object? value, double fallback, string name) => value is null ? fallback : ChartDataAccessor.TryNumber(value, out var n) && double.IsFinite(n) ? n : throw new ArgumentException($"{name} must be finite and numeric.");
    private LayerInput ReadLayer(MapLayer layer, HashSet<object> owners)
    {
        var values = new List<MapValue>(); var points = new List<MapObservation>(); var originals = new Dictionary<object, (double, IBrush?)>();
        var connections = new List<MapConnection>(); var vectors = new List<MapVector>(); var pies = new List<MapPie>(); var categories = new Dictionary<object, object>(); var sliceCount = 0;
        var source = layer.ItemsSource; if (source is not null) owners.Add(source);
        if (source is not null) foreach (var item in source)
        {
            if (values.Count + points.Count + connections.Count + vectors.Count + pies.Count >= ChartMaps.MaximumObservationCount) throw new ArgumentOutOfRangeException(nameof(layer.ItemsSource), "Map layer observation budget exceeded.");
            if (item is null) throw new ArgumentException("Map observations cannot be null.");
            owners.Add(item);
            foreach (var path in new[] { layer.IdPath, layer.RegionPath, layer.ValuePath, layer.LongitudePath, layer.LatitudePath, layer.SizePath, layer.LabelPath, layer.FillPath })
                foreach (var owner in ChartDataAccessor.Owners(item, path)) owners.Add(owner);
            IBrush? fill;
            try { fill = Read(item, layer.FillPath) switch { IBrush brush => brush, Color color => new SolidColorBrush(color), string text => Brush.Parse(text), _ => null }; }
            catch (FormatException error) { throw new ArgumentException("Map fill must be a valid color or brush.", error); }
            if (layer is VectorLayer)
            {
                if (item is not MapVector vector) throw new ArgumentException("VectorLayer requires typed MapVector items.");
                if (vectors.Count >= ChartMaps.MaximumVectorCount) throw new ArgumentOutOfRangeException(nameof(layer.ItemsSource));
                if (vector is MapPolylineVector polyline) { ArgumentNullException.ThrowIfNull(polyline.Points); owners.Add(polyline.Points); }
                if (vector is MapPolygonVector polygon) ArgumentNullException.ThrowIfNull(polygon.Polygon);
                vectors.Add(vector); originals[vector.Id] = (0, fill); continue;
            }
            if (layer is LineLayer line)
            {
                foreach (var path in new[] { line.FromLongitudePath, line.FromLatitudePath, line.ToLongitudePath, line.ToLatitudePath }) foreach (var owner in ChartDataAccessor.Owners(item, path)) owners.Add(owner);
                var typed = item as MapConnection; var id = typed?.Id ?? Read(item, layer.IdPath) ?? item;
                var start = typed?.Start ?? new MapCoordinate(Numeric(Read(item, line.FromLongitudePath), double.NaN, nameof(line.FromLongitudePath)), Numeric(Read(item, line.FromLatitudePath), double.NaN, nameof(line.FromLatitudePath)));
                var end = typed?.End ?? new MapCoordinate(Numeric(Read(item, line.ToLongitudePath), double.NaN, nameof(line.ToLongitudePath)), Numeric(Read(item, line.ToLatitudePath), double.NaN, nameof(line.ToLatitudePath)));
                var value = typed?.Value ?? Numeric(Read(item, line.ValuePath), 1, nameof(line.ValuePath));
                connections.Add(typed ?? new(id, start, end, value, item, Read(item, layer.LabelPath)?.ToString())); originals[id] = (value, fill); continue;
            }
            if (layer is PieLayer pieLayer)
            {
                var typed = item as MapPie; var id = typed?.Id ?? Read(item, layer.IdPath) ?? item;
                var position = typed?.Position ?? new MapCoordinate(Numeric(Read(item, layer.LongitudePath), double.NaN, nameof(layer.LongitudePath)), Numeric(Read(item, layer.LatitudePath), double.NaN, nameof(layer.LatitudePath)));
                foreach (var owner in ChartDataAccessor.Owners(item, pieLayer.SlicesPath)) owners.Add(owner);
                var sourceSlices = typed is not null ? typed.Slices : Read(item, pieLayer.SlicesPath) as IEnumerable;
                if (sourceSlices is null) throw new ArgumentException("Pie SlicesPath must resolve to a collection."); owners.Add(sourceSlices);
                var slices = new List<MapPieSlice>();
                foreach (var slice in sourceSlices)
                {
                    if (++sliceCount > ChartMaps.MaximumPieSlices) throw new ArgumentOutOfRangeException(nameof(layer.ItemsSource), "Map pie slice budget exceeded.");
                    if (slice is null) throw new ArgumentException("Pie slices cannot be null."); owners.Add(slice);
                    foreach (var path in new[] { pieLayer.SliceValuePath, pieLayer.SliceLabelPath }) foreach (var owner in ChartDataAccessor.Owners(slice, path)) owners.Add(owner);
                    slices.Add(slice is MapPieSlice given ? given : new MapPieSlice(ChartDataAccessor.TryNumber(slice, out var direct) ? direct : Numeric(Read(slice, pieLayer.SliceValuePath), double.NaN, nameof(pieLayer.SliceValuePath)), Read(slice, pieLayer.SliceLabelPath)?.ToString(), slice));
                }
                pies.Add(new(id, position, slices.AsReadOnly(), typed?.Source ?? item, typed?.Label ?? Read(item, layer.LabelPath)?.ToString())); continue;
            }
            if (layer is ShapeLayer)
            {
                var typed = item as MapValue;
                var id = typed?.RegionId ?? Read(item, layer.RegionPath) ?? Read(item, layer.IdPath) ?? throw new ArgumentException("Map region observations require a region ID.");
                var raw = typed is not null ? typed.Value : Read(item, layer.ValuePath);
                double? value = raw is null ? null : Numeric(raw, 0, nameof(layer.ValuePath));
                if (layer is ShapeLayer { ColorMode: MapColorMode.Categorical } categorical)
                {
                    foreach (var owner in ChartDataAccessor.Owners(item, categorical.CategoryPath)) owners.Add(owner);
                    var category = Read(item, categorical.CategoryPath); if (category is not null) { categories[id] = category; value ??= 0; }
                    else value = null;
                }
                values.Add(typed is not null && value == typed.Value ? typed : new(id, value, typed?.Source ?? item));
                originals[id] = (value ?? 0, fill);
            }
            else
            {
                var typed = item as MapObservation;
                var id = typed?.Id ?? Read(item, layer.IdPath) ?? item;
                var value = typed?.Value ?? Numeric(Read(item, layer.ValuePath), 1, nameof(layer.ValuePath));
                if (!double.IsFinite(value)) throw new ArgumentException("Map business values must be finite, independently of their displayed size.");
                var size = layer is not BubbleLayer || string.IsNullOrWhiteSpace(layer.SizePath) ? value : Numeric(Read(item, layer.SizePath), value, nameof(layer.SizePath));
                var position = typed?.Position ?? new MapCoordinate(Numeric(Read(item, layer.LongitudePath), double.NaN, nameof(layer.LongitudePath)), Numeric(Read(item, layer.LatitudePath), double.NaN, nameof(layer.LatitudePath)));
                if (!double.IsFinite(position.X) || !double.IsFinite(position.Y)) throw new ArgumentException("Map observations require both coordinate paths.");
                points.Add(typed is not null && size == typed.Value ? typed : new(id, position, layer is BubbleLayer ? size : value, typed?.Source ?? item, typed?.Label ?? Read(item, layer.LabelPath)?.ToString()));
                originals[id] = (value, fill);
            }
        }
        return new(layer, values.ToArray(), points.ToArray(), originals) { Connections = connections.ToArray(), Vectors = vectors.ToArray(), Pies = pies.ToArray(), Categories = categories };
    }
    private void EnsureData()
    {
        if (!_geometryDirty && !_dataDirty) return;
        var owners = new HashSet<object>(ReferenceEqualityComparer.Instance); var revision = _dataRevision;
        try
        {
            if (FeaturesSource is { } features) owners.Add(features);
            if (_sourceError is { } sourceError) throw new ArgumentException("Map source load failed.", sourceError);
            var snapshot = _snapshot;
            if (_sourceDirty || snapshot is null)
            {
                if (FeaturesSource is { } source) snapshot = ChartMaps.Snapshot(source);
                else if (GeoJson is { } json) snapshot = ChartMaps.ReadGeoJson(json, JsonIdProperty, JsonLabelProperty);
                else snapshot = _loadedSnapshot ?? ChartMaps.Snapshot(Array.Empty<MapFeature>());
            }
            var projected = _geometryDirty || _projected is null ? ChartMaps.Project(snapshot, EffectiveProjection, topologyRule: TopologyRule) : _projected;
            var inputs = new List<LayerInput>();
            foreach (var layer in Layers.ToArray()) inputs.Add(ReadLayer(layer, owners));
            long overlayWork = 0;
            foreach (var input in inputs.Where(i => i.Layer.IsVisible && i.Layer.Opacity > 0))
            {
                overlayWork += input.Layer is MarkerLayer marker ? input.Observations.Length * (marker.MarkerType == MapMarkerType.Pin ? 26L : 4L) : 0;
                overlayWork += input.Connections.Length * (input.Layer is LineLayer { IsCurved: true } ? 129L : 2L);
                overlayWork += input.Pies.Sum(p => (long)p.Slices.Count * 4);
                foreach (var vector in input.Vectors) overlayWork += vector switch { MapPolylineVector p => p.Points.Count * 2L, MapPolygonVector p => p.Polygon.Outer.Count + p.Polygon.Holes.Sum(h => h.Count), MapArcVector => 129, _ => 2 };
                if (overlayWork > ChartMaps.MaximumOverlayPrimitives) throw new ArgumentOutOfRangeException(nameof(Layers), "Map overlay primitive budget exceeded.");
            }
            long retainedContributions = 0;
            foreach (var input in inputs)
            {
                if (input.Layer is not HeatmapLayer { IsVisible: true, Opacity: > 0 } heat) continue;
                var side = 2L * heat.Radius + 1;
                var estimate = input.Observations.LongLength * side * side;
                if (estimate > ChartMaps.MaximumRetainedHeatmapContributions - retainedContributions)
                    throw new ArgumentOutOfRangeException(nameof(Layers), "Visible heatmap layers exceed the map's retained point-kernel contribution budget.");
                retainedContributions += estimate;
            }
            var bounds = new List<MapBounds>();
            var vectorLayouts = inputs.Where(i => i.Layer is VectorLayer && i.Layer.IsVisible && i.Layer.Opacity > 0)
                .ToDictionary(i => i.Layer, i => ChartMaps.Vectors(i.Vectors, EffectiveProjection, TopologyRule));
            if (projected.Features.Count > 0) bounds.Add(projected.Bounds);
            foreach (var input in inputs.Where(i => i.Layer.IsVisible && i.Layer.Opacity > 0))
            {
                void Include(MapCoordinate coordinate) { var v = ChartMaps.ProjectCoordinate(coordinate, EffectiveProjection); bounds.Add(new(v.X, v.Y, v.X, v.Y)); }
                foreach (var p in input.Observations) Include(p.Position);
                foreach (var connection in input.Connections) { Include(connection.Start); Include(connection.End); }
                foreach (var pie in input.Pies) Include(pie.Position);
                if (vectorLayouts.TryGetValue(input.Layer, out var vectorLayout)) foreach (var vector in vectorLayout.Vectors)
                {
                    void IncludeProjected(ChartPoint p) => bounds.Add(new(p.X, p.Y, p.X, p.Y));
                    foreach (var segment in vector.Segments) { IncludeProjected(segment.Start); IncludeProjected(segment.End); }
                    foreach (var polygon in vector.Polygons) foreach (var p in polygon.Outer) IncludeProjected(p);
                    if (vector.Center is { } center) IncludeProjected(center);
                }
            }
            var world = bounds.Count == 0 ? new MapBounds(0, 0, 1, 1) : new(bounds.Min(b => b.MinX), bounds.Min(b => b.MinY), bounds.Max(b => b.MaxX), bounds.Max(b => b.MaxY));
            static (double Low, double High) Expand(double value)
            {
                if (value == 0) return (-1, 1);
                var low = value > 0 ? value * .99 : value * 1.01;
                var high = value > 0 ? value * 1.01 : value * .99;
                if (!double.IsFinite(low)) low = value;
                if (!double.IsFinite(high)) high = value;
                // Subnormal and very small coordinates can round both percentage endpoints to the
                // same Double. Adjacent finite representable values still provide a real fit span.
                if (low >= value && double.IsFinite(Math.BitDecrement(value))) low = Math.BitDecrement(value);
                if (high <= value && double.IsFinite(Math.BitIncrement(value))) high = Math.BitIncrement(value);
                return (low, high);
            }
            if (world.MinX == world.MaxX) { var axis = Expand(world.MinX); world = world with { MinX = axis.Low, MaxX = axis.High }; }
            if (world.MinY == world.MaxY) { var axis = Expand(world.MinY); world = world with { MinY = axis.Low, MaxY = axis.High }; }
            var results = new List<MapLayerSnapshot>();
            foreach (var input in inputs)
            {
                if (!input.Layer.IsVisible || input.Layer.Opacity <= 0) { results.Add(new(input.Layer, null, null, null)); continue; }
                switch (input.Layer)
                {
                    case ShapeLayer: results.Add(new(input.Layer, ChartMaps.Join(snapshot, input.Values), null, null)); break;
                    case BubbleLayer bubble: results.Add(new(input.Layer, null, ChartMaps.Bubbles(input.Observations, EffectiveProjection, bubble.MinimumRadius, bubble.MaximumRadius), null)); break;
                    case HeatmapLayer heat: results.Add(new(input.Layer, null, null, ChartMaps.Heatmap(input.Observations, EffectiveProjection, heat.Columns, heat.Rows, heat.Radius, world))); break;
                    case MarkerLayer: results.Add(new(input.Layer, null, null, null) { Points = ChartMaps.Markers(input.Observations, EffectiveProjection) }); break;
                    case LineLayer: results.Add(new(input.Layer, null, null, null) { Connections = ChartMaps.Connections(input.Connections, EffectiveProjection) }); break;
                    case VectorLayer: results.Add(new(input.Layer, null, null, null) { Vectors = vectorLayouts[input.Layer] }); break;
                    case PieLayer: results.Add(new(input.Layer, null, null, null) { Pies = ChartMaps.Pies(input.Pies, EffectiveProjection) }); break;
                }
            }
            if (revision != _dataRevision) { InvalidateEngineering(); return; }
            _snapshot = snapshot; _projected = projected; _inputs = inputs.AsReadOnly(); _layerSnapshots = results.AsReadOnly(); _worldBounds = world; _sourceDirty = false; _geometryDirty = false; _dataDirty = false;
        }
        catch (System.Text.Json.JsonException error) { throw new ArgumentException("Map GeoJSON is invalid.", error); }
        finally { if (revision == _dataRevision) ObserveOwners(owners); else ObserveSourceRoots(); }
    }
    private void ObserveOwners(HashSet<object> owners)
    {
        _desired.Clear(); if (_attached && !_disposed) foreach (var owner in owners) if (owner is INotifyPropertyChanged or INotifyCollectionChanged) _desired.Add(owner);
        SynchronizeObservers();
    }
    private void ReleaseObservers() { _desired.Clear(); SynchronizeObservers(); }
    private void Changed(object? sender, PropertyChangedEventArgs e) => DirtyData();
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, FeaturesSource)) { _sourceDirty = true; _geometryDirty = true; }
        DirtyData();
        // Retired observations must not stay subscribed merely because a panel is hidden.
        // The scene is already dirty, so its next capture reads all latest values; retain
        // root source notifications meanwhile, and rebuild item/nested observers then.
        ObserveSourceRoots();
    }
    private void ObserveSourceRoots()
    {
        var roots = new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (FeaturesSource is { } features) roots.Add(features);
        foreach (var layer in Layers.ToArray()) if (layer.ItemsSource is { } source) roots.Add(source);
        ObserveOwners(roots);
    }
    private void SynchronizeObservers()
    {
        _resynchronize = true; if (_synchronizing) return; _synchronizing = true;
        try
        {
            while (_resynchronize)
            {
                _resynchronize = false;
                foreach (var (owner, state) in _observed.ToArray())
                {
                    if (_desired.Contains(owner)) continue;
                    if (state.PropertyAttached && owner is INotifyPropertyChanged p) { state.PropertyAttached = false; p.PropertyChanged -= Changed; }
                    if (_resynchronize) break;
                    if (state.CollectionAttached && owner is INotifyCollectionChanged c) { state.CollectionAttached = false; c.CollectionChanged -= CollectionChanged; }
                    if (_resynchronize) break;
                    _observed.Remove(owner);
                }
                if (_resynchronize) continue;
                foreach (var owner in _desired.ToArray())
                {
                    if (!_observed.TryGetValue(owner, out var state)) { state = new(); _observed.Add(owner, state); }
                    if (!state.PropertyAttached && owner is INotifyPropertyChanged p) { state.PropertyAttached = true; p.PropertyChanged += Changed; }
                    if (_resynchronize) break;
                    if (!state.CollectionAttached && owner is INotifyCollectionChanged c) { state.CollectionAttached = true; c.CollectionChanged += CollectionChanged; }
                    if (_resynchronize) break;
                }
            }
        }
        finally { _synchronizing = false; }
    }
}
