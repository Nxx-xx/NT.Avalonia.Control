using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NT.AvaloniaControl.Charts;

public abstract partial class RelationsChartBase
{
    private GraphSnapshot ReadSnapshot(ChartDataSnapshot data, CancellationToken cancellationToken = default)
    {
        var owners = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var nodes = new List<GraphNode>(); var edges = new List<GraphEdge>();
        object? Read(object? item, string? path) => !string.IsNullOrWhiteSpace(path) && ChartDataAccessor.TryRead(item, path, out var value) ? value : null;
        void Observe(object? item, params string?[] paths) { if (item is not null) owners.Add(item); foreach (var path in paths) foreach (var owner in ChartDataAccessor.Owners(item, path)) owners.Add(owner); }
        static double Numeric(object? raw, double fallback, string name) => raw is null ? fallback : ChartDataAccessor.TryNumber(raw, out var number) ? number : throw new ArgumentException($"{name} must be numeric.");
        try
        {
            if (ItemsSource is { } nodeSource) owners.Add(nodeSource);
            for (var i = 0; i < data.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = data.Points[i].Item ?? throw new ArgumentException("Relation nodes cannot be null.");
                Observe(item, IdPath, LabelPath, GroupPath, ShapePath, XPath, YPath, NodeWeightPath, WidthPath, HeightPath, StagePath);
                if (item is GraphNode typed) { nodes.Add(typed); continue; }
                var id = Read(item, IdPath) ?? throw new ArgumentException("Relation nodes require a nonnull unique ID; configure IdPath or use GraphNode.");
                var shape = Read(item, ShapePath) is { } rawShape ? rawShape is RelationNodeShape s ? s : Enum.TryParse<RelationNodeShape>(rawShape.ToString(), true, out var parsed) ? parsed : throw new ArgumentException("Unknown relation node shape.") : RelationNodeShape.Rectangle;
                var x = Read(item, XPath); var y = Read(item, YPath); ChartPoint? position = null;
                if (x is not null || y is not null) { if (x is null || y is null) throw new ArgumentException("Node positions require both X and Y."); position = new(Numeric(x, 0, nameof(XPath)), Numeric(y, 0, nameof(YPath))); }
                var rawStage = Read(item, StagePath); int? stage = null;
                if (rawStage is not null) { var number = Numeric(rawStage, 0, nameof(StagePath)); if (!double.IsFinite(number) || number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue) throw new ArgumentException("Stage must be a finite integer."); stage = (int)number; }
                nodes.Add(new(id, Read(item, LabelPath)?.ToString(), item, Read(item, GroupPath)?.ToString(), shape, position,
                    Numeric(Read(item, NodeWeightPath), 1, nameof(NodeWeightPath)), Numeric(Read(item, WidthPath), .12, nameof(WidthPath)), Numeric(Read(item, HeightPath), .08, nameof(HeightPath)), stage));
            }
            if (EdgesSource is { } edgeSource)
            {
                owners.Add(edgeSource); var count = 0;
                foreach (var item in edgeSource)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++count > ChartRelations.MaximumEdgeCount) throw new ArgumentOutOfRangeException(nameof(EdgesSource), "Relation edge budget exceeded.");
                    if (item is null) throw new ArgumentException("Relation edges cannot be null.");
                    Observe(item, SourcePath, TargetPath, WeightPath, EdgeIdPath, EdgeLabelPath);
                    if (item is GraphEdge typed) { edges.Add(typed); continue; }
                    object Endpoint(string? path)
                    {
                        var value = Read(item, path) ?? throw new ArgumentException("Every edge needs source and target IDs or node source references.");
                        var match = nodes.FirstOrDefault(n => ReferenceEquals(n.Source ?? n, value)); return match?.Id ?? value;
                    }
                    edges.Add(new(Endpoint(SourcePath), Endpoint(TargetPath), Numeric(Read(item, WeightPath), 1, nameof(WeightPath)), Read(item, EdgeLabelPath)?.ToString(), item, Read(item, EdgeIdPath)));
                }
            }
            var edgeIdentities = new HashSet<Identity>();
            foreach (var edge in edges)
                if (!edgeIdentities.Add(new(new(RelationElementKind.Edge, edge.Id ?? edge.Source ?? edge, edge.Id is not null)))) throw new ArgumentException("Relation edge IDs or original source instances must be unique.");
            return ChartRelations.Snapshot(nodes, edges, cancellationToken);
        }
        finally { ObserveOwners(owners); }
    }
    private void ObserveOwners(HashSet<object> owners)
    {
        _desiredObservers.Clear(); if (_attached && !_disposed) foreach (var owner in owners) if (owner is INotifyPropertyChanged or INotifyCollectionChanged) _desiredObservers.Add(owner);
        SynchronizeObservers();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
    private void ReleaseObservers() { _desiredObservers.Clear(); SynchronizeObservers(); }
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
                    if (_desiredObservers.Contains(owner)) continue;
                    if (state.PropertyAttached && owner is INotifyPropertyChanged property) { state.PropertyAttached = false; property.PropertyChanged -= Changed; }
                    if (_resynchronize) break;
                    if (state.CollectionAttached && owner is INotifyCollectionChanged collection) { state.CollectionAttached = false; collection.CollectionChanged -= CollectionChanged; }
                    if (_resynchronize) break;
                    _observed.Remove(owner);
                }
                if (_resynchronize) continue;
                foreach (var owner in _desiredObservers.ToArray())
                {
                    if (!_observed.TryGetValue(owner, out var state)) { state = new(); _observed.Add(owner, state); }
                    if (!state.PropertyAttached && owner is INotifyPropertyChanged property) { state.PropertyAttached = true; property.PropertyChanged += Changed; }
                    if (_resynchronize) break;
                    if (!state.CollectionAttached && owner is INotifyCollectionChanged collection) { state.CollectionAttached = true; collection.CollectionChanged += CollectionChanged; }
                    if (_resynchronize) break;
                }
            }
        }
        finally { _synchronizing = false; }
    }
}
