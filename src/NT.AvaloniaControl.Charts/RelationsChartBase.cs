using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

public enum RelationElementKind { Node, Edge }
public enum RelationLayoutState { Idle, Running, Completed, Canceled, Failed }
/// <summary>Separate node and edge identity domains. Explicit IDs compare by value; other identities are original source instances.</summary>
public sealed record RelationGlyphKey(RelationElementKind Kind, object Identity, bool HasExplicitId)
{
    public bool Equals(RelationGlyphKey? other) => other is not null && Kind == other.Kind && HasExplicitId == other.HasExplicitId
        && (HasExplicitId ? Equals(Identity, other.Identity) : ReferenceEquals(Identity, other.Identity));
    public override int GetHashCode() => HashCode.Combine(Kind, HasExplicitId, HasExplicitId ? Identity.GetHashCode() : RuntimeHelpers.GetHashCode(Identity));
}

/// <summary>Observable original node/edge data with source-preserving interaction, cancellable background layout and export.</summary>
public abstract partial class RelationsChartBase : EngineeringChartBase, IDisposable
{
    public static readonly StyledProperty<IEnumerable?> EdgesSourceProperty = AvaloniaProperty.Register<RelationsChartBase, IEnumerable?>(nameof(EdgesSource));
    public static readonly StyledProperty<string?> SourcePathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(SourcePath), "SourceId");
    public static readonly StyledProperty<string?> TargetPathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(TargetPath), "TargetId");
    public static readonly StyledProperty<string?> WeightPathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(WeightPath), "Weight");
    public static readonly StyledProperty<string?> NodeWeightPathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(NodeWeightPath), "Weight");
    public static readonly StyledProperty<string?> EdgeIdPathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(EdgeIdPath), "Id");
    public static readonly StyledProperty<string?> EdgeLabelPathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(EdgeLabelPath), "Label");
    public static readonly StyledProperty<string?> GroupPathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(GroupPath), "Group");
    public static readonly StyledProperty<string?> ShapePathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(ShapePath), "Shape");
    public static readonly StyledProperty<string?> XPathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(XPath), "Position.X");
    public static readonly StyledProperty<string?> YPathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(YPath), "Position.Y");
    public static readonly StyledProperty<string?> WidthPathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(WidthPath), "Width");
    public static readonly StyledProperty<string?> HeightPathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(HeightPath), "Height");
    public static readonly StyledProperty<string?> StagePathProperty = AvaloniaProperty.Register<RelationsChartBase, string?>(nameof(StagePath), "Stage");
    public static readonly StyledProperty<bool> ShowLabelsProperty = AvaloniaProperty.Register<RelationsChartBase, bool>(nameof(ShowLabels), true);
    public static readonly StyledProperty<bool> ShowEdgeLabelsProperty = AvaloniaProperty.Register<RelationsChartBase, bool>(nameof(ShowEdgeLabels));
    public static readonly StyledProperty<bool> ShowGroupsProperty = AvaloniaProperty.Register<RelationsChartBase, bool>(nameof(ShowGroups), true);
    public static readonly DirectProperty<RelationsChartBase, bool> IsLayoutRunningProperty = AvaloniaProperty.RegisterDirect<RelationsChartBase, bool>(nameof(IsLayoutRunning), c => c.IsLayoutRunning);
    public static readonly DirectProperty<RelationsChartBase, RelationLayoutState> LayoutStateProperty = AvaloniaProperty.RegisterDirect<RelationsChartBase, RelationLayoutState>(nameof(LayoutState), c => c.LayoutState);
    public static readonly DirectProperty<RelationsChartBase, Exception?> LayoutErrorProperty = AvaloniaProperty.RegisterDirect<RelationsChartBase, Exception?>(nameof(LayoutError), c => c.LayoutError);
    private sealed class ObserverState { public bool PropertyAttached; public bool CollectionAttached; }
    private readonly Dictionary<object, ObserverState> _observed = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _desiredObservers = new(ReferenceEqualityComparer.Instance);
    private bool _synchronizing, _resynchronize, _attached, _disposed, _dataDirty = true, _layoutDirty = true, _running, _layoutSuppressed, _buildingRelation, _statusQueued, _publishedRunning;
    private long _dataRevision, _layoutGeneration;
    private GraphSnapshot? _snapshot;
    private RelationGraphLayout? _graphLayout;
    private RelationChordLayout? _chordLayout;
    private CancellationTokenSource? _layoutCancellation;
    private RelationLayoutState _layoutState;
    private Exception? _layoutError;
    private Exception? _publishedError;
    private RelationLayoutState _publishedState;
    private readonly Dictionary<EngineeringMarkInfo, PaintedRelation> _painted = new(ReferenceEqualityComparer.Instance);
    private readonly ConditionalWeakTable<EngineeringMarkInfo, RelationGlyphKey> _keys = new();
    private readonly Dictionary<Identity, int> _indices = [];
    protected RelationsChartBase() { IdPath = "Id"; LabelPath = "Label"; ShowGrid = false; }
    protected override int SourceItemLimit => ChartRelations.MaximumNodeCount;
    public IEnumerable? EdgesSource { get => GetValue(EdgesSourceProperty); set => SetValue(EdgesSourceProperty, value); }
    public string? SourcePath { get => GetValue(SourcePathProperty); set => SetValue(SourcePathProperty, value); }
    public string? TargetPath { get => GetValue(TargetPathProperty); set => SetValue(TargetPathProperty, value); }
    public string? WeightPath { get => GetValue(WeightPathProperty); set => SetValue(WeightPathProperty, value); }
    public string? NodeWeightPath { get => GetValue(NodeWeightPathProperty); set => SetValue(NodeWeightPathProperty, value); }
    public string? EdgeIdPath { get => GetValue(EdgeIdPathProperty); set => SetValue(EdgeIdPathProperty, value); }
    public string? EdgeLabelPath { get => GetValue(EdgeLabelPathProperty); set => SetValue(EdgeLabelPathProperty, value); }
    public string? GroupPath { get => GetValue(GroupPathProperty); set => SetValue(GroupPathProperty, value); }
    public string? ShapePath { get => GetValue(ShapePathProperty); set => SetValue(ShapePathProperty, value); }
    public string? XPath { get => GetValue(XPathProperty); set => SetValue(XPathProperty, value); }
    public string? YPath { get => GetValue(YPathProperty); set => SetValue(YPathProperty, value); }
    public string? WidthPath { get => GetValue(WidthPathProperty); set => SetValue(WidthPathProperty, value); }
    public string? HeightPath { get => GetValue(HeightPathProperty); set => SetValue(HeightPathProperty, value); }
    public string? StagePath { get => GetValue(StagePathProperty); set => SetValue(StagePathProperty, value); }
    public bool ShowLabels { get => GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    public bool ShowEdgeLabels { get => GetValue(ShowEdgeLabelsProperty); set => SetValue(ShowEdgeLabelsProperty, value); }
    public bool ShowGroups { get => GetValue(ShowGroupsProperty); set => SetValue(ShowGroupsProperty, value); }
    public bool IsLayoutRunning => _running;
    public RelationLayoutState LayoutState => _layoutState;
    public Exception? LayoutError => _layoutError;
    public GraphSnapshot? Snapshot { get { _ = GetMarks(); return _snapshot; } }
    public RelationGraphLayout? GraphLayout { get { _ = GetMarks(); return _graphLayout; } }
    public RelationChordLayout? ChordLayout { get { _ = GetMarks(); return _chordLayout; } }
    public GraphNodeEntry? SelectedNode => NodeFor(SelectedPoint);
    public GraphEdgeEntry? SelectedEdge => EdgeFor(SelectedPoint);
    public GraphNodeEntry? ActiveNode => NodeFor(ActivePoint);
    public GraphEdgeEntry? ActiveEdge => EdgeFor(ActivePoint);
    private GraphNodeEntry? NodeFor(EngineeringPointHit? hit) => hit?.Mark.Key is RelationGlyphKey { Kind: RelationElementKind.Node } key ? _snapshot?.Nodes.FirstOrDefault(n => KeyMatches(key, NodeKey(n))) : null;
    private GraphEdgeEntry? EdgeFor(EngineeringPointHit? hit) => hit?.Mark.Key is RelationGlyphKey { Kind: RelationElementKind.Edge } key ? _snapshot?.Edges.FirstOrDefault(n => KeyMatches(key, EdgeKey(n))) : null;
    public bool SelectNode(object idOrSource) => Select(RelationElementKind.Node, idOrSource);
    public bool SelectEdge(object idOrSource) => Select(RelationElementKind.Edge, idOrSource);
    private bool Select(RelationElementKind kind, object idOrSource)
    {
        VerifyAccess(); var marks = GetMarks();
        for (var i = 0; i < marks.Count; i++) if (marks[i].Key is RelationGlyphKey key && key.Kind == kind && (key.HasExplicitId && Equals(key.Identity, idOrSource) || ReferenceEquals(key.Identity, idOrSource) || marks[i].Members.Any(p => ReferenceEquals(p.Item, idOrSource)))) { SelectedIndex = i; return true; }
        SelectedIndex = -1; return false;
    }
    private sealed class Identity(RelationGlyphKey key) : IEquatable<Identity>
    {
        public bool Equals(Identity? other) => other is not null && KeyMatches(key, other.Key);
        public override bool Equals(object? obj) => obj is Identity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(key.Kind, key.HasExplicitId, key.HasExplicitId ? key.Identity.GetHashCode() : RuntimeHelpers.GetHashCode(key.Identity));
        private RelationGlyphKey Key => key;
    }
    private static bool KeyMatches(RelationGlyphKey a, RelationGlyphKey b) => a.Kind == b.Kind && a.HasExplicitId == b.HasExplicitId && (a.HasExplicitId ? Equals(a.Identity, b.Identity) : ReferenceEquals(a.Identity, b.Identity));
    private static RelationGlyphKey NodeKey(GraphNodeEntry entry) => new(RelationElementKind.Node, entry.Id, true);
    private static RelationGlyphKey EdgeKey(GraphEdgeEntry entry) => new(RelationElementKind.Edge, entry.Edge.Id ?? entry.Source, entry.Edge.Id is not null);
    protected override EngineeringPointHit? FindFollowedMark(EngineeringPointHit hit, IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (hit.Mark.Key is not RelationGlyphKey key) return null;
        return _indices.TryGetValue(new(key), out var index) && index < marks.Count ? new(index, marks[index]) : null;
    }
    protected override void OnDataInvalidated() { ++_dataRevision; _dataDirty = true; _layoutDirty = true; CancelLayoutCore(false); _layoutSuppressed = false; }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsLayoutRunningProperty || change.Property == LayoutStateProperty || change.Property == LayoutErrorProperty) { base.OnPropertyChanged(change); return; }
        if (change.Property == EdgesSourceProperty || change.Property == ItemsSourceProperty) ReleaseObservers();
        if (change.Property == EdgesSourceProperty || change.Property == SourcePathProperty || change.Property == TargetPathProperty || change.Property == WeightPathProperty || change.Property == NodeWeightPathProperty || change.Property == EdgeIdPathProperty || change.Property == EdgeLabelPathProperty || change.Property == GroupPathProperty || change.Property == ShapePathProperty || change.Property == XPathProperty || change.Property == YPathProperty || change.Property == WidthPathProperty || change.Property == HeightPathProperty || change.Property == StagePathProperty) OnDataInvalidated();
        base.OnPropertyChanged(change);
    }
    protected void InvalidateRelationLayout() { CancelLayoutCore(false); _layoutDirty = true; _layoutSuppressed = false; InvalidateEngineering(); }
    protected override ChartDataSnapshot CreateEngineeringSnapshot() => CaptureSourceData(CancellationToken.None);
    private ChartDataSnapshot CaptureSourceData(CancellationToken cancellationToken)
    {
        var points = new List<ChartDataPoint>();
        if (ItemsSource is { } source) foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (points.Count >= ChartRelations.MaximumNodeCount) throw new ArgumentOutOfRangeException(nameof(ItemsSource), "Relation node budget exceeded.");
            points.Add(new(points.Count, null, item, null, default, false));
        }
        return new(_dataRevision, points);
    }
    protected abstract object CalculateLayout(GraphSnapshot snapshot, CancellationToken cancellationToken);
    private void AcceptLayout(object result)
    {
        _graphLayout = result as RelationGraphLayout; _chordLayout = result as RelationChordLayout;
        if (_graphLayout is null && _chordLayout is null) throw new ArgumentException("Unsupported relation layout.");
        _layoutDirty = false;
    }
    /// <summary>Snapshots sources on the UI thread, computes on a worker, and publishes only the latest uncanceled generation on the UI thread.</summary>
    public async Task<bool> LayoutAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); cancellationToken.ThrowIfCancellationRequested();
        if (_dataDirty || _snapshot is null)
        {
            var dataRevision = _dataRevision;
            try
            {
                var candidate = ReadSnapshot(CaptureSourceData(cancellationToken), cancellationToken);
                if (dataRevision != _dataRevision) return false;
                _snapshot = candidate; _dataDirty = false; _layoutDirty = true; _graphLayout = null; _chordLayout = null;
            }
            catch (Exception error) when (error is ArgumentException or OverflowException)
            { SetLayoutState(RelationLayoutState.Failed, error); InvalidateEngineering(); return false; }
        }
        CancelLayoutCore(false); var generation = ++_layoutGeneration; var revision = _dataRevision; var snapshot = _snapshot;
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); _layoutCancellation = linked;
        // Capture every layout option before leaving the UI thread.
        Func<CancellationToken, object> calculate;
        try { calculate = CreateLayoutWorker(snapshot); }
        catch (Exception error) { _layoutCancellation = null; linked.Dispose(); _layoutSuppressed = true; SetLayoutState(RelationLayoutState.Failed, error); return false; }
        _layoutSuppressed = false;
        _layoutState = RelationLayoutState.Running; _layoutError = null; _running = true; PublishStatus();
        try
        {
            if (generation != _layoutGeneration || revision != _dataRevision || linked.IsCancellationRequested) return false;
            var result = await Task.Run(() => calculate(linked.Token), linked.Token).ConfigureAwait(false);
            var accepted = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed || linked.IsCancellationRequested || generation != _layoutGeneration || revision != _dataRevision) return;
                AcceptLayout(result); SetRunning(false);
                if (generation != _layoutGeneration || revision != _dataRevision || linked.IsCancellationRequested) return;
                SetLayoutState(RelationLayoutState.Completed, null); InvalidateEngineering(); accepted = generation == _layoutGeneration && revision == _dataRevision && !linked.IsCancellationRequested;
            });
            return accepted;
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { if (generation == _layoutGeneration) { _layoutSuppressed = true; _running = false; _layoutState = RelationLayoutState.Canceled; _layoutError = null; PublishStatus(); } });
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            return false;
        }
        catch (Exception error)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { if (generation == _layoutGeneration) { _layoutSuppressed = true; _running = false; _layoutState = RelationLayoutState.Failed; _layoutError = error; PublishStatus(); } });
            return false;
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => { if (ReferenceEquals(_layoutCancellation, linked)) _layoutCancellation = null; }); linked.Dispose();
        }
    }
    /// <summary>Captures UI-owned layout settings. The returned delegate must access only immutable captured data.</summary>
    protected abstract Func<CancellationToken, object> CreateLayoutWorker(GraphSnapshot snapshot);
    public void CancelLayout() { VerifyAccess(); CancelLayoutCore(true); }
    private void CancelLayoutCore(bool report)
    {
        ++_layoutGeneration; _layoutCancellation?.Cancel(); _layoutCancellation = null;
        if (_running || report) { _layoutSuppressed = true; _running = false; _layoutState = RelationLayoutState.Canceled; _layoutError = null; PublishStatus(); }
    }
    private void SetRunning(bool running) { _running = running; PublishStatus(); }
    private void SetLayoutState(RelationLayoutState state, Exception? error) { _layoutState = state; _layoutError = error; PublishStatus(); }
    private void PublishStatus()
    {
        if (_buildingRelation)
        {
            if (!_statusQueued) { _statusQueued = true; Dispatcher.UIThread.Post(() => { _statusQueued = false; PublishStatus(); }); }
            return;
        }
        var running = _running; var state = _layoutState; var error = _layoutError;
        if (_publishedRunning != running) { var previous = _publishedRunning; _publishedRunning = running; RaisePropertyChanged(IsLayoutRunningProperty, previous, running); }
        if (_layoutState == state && _publishedState != state) { var previous = _publishedState; _publishedState = state; RaisePropertyChanged(LayoutStateProperty, previous, state); }
        if (ReferenceEquals(_layoutError, error) && !ReferenceEquals(_publishedError, error)) { var previous = _publishedError; _publishedError = error; RaisePropertyChanged(LayoutErrorProperty, previous, error); }
    }
    protected override void ClearProjection(Exception error) { _snapshot = null; _graphLayout = null; _chordLayout = null; _painted.Clear(); _indices.Clear(); _buildingRelation = true; try { SetLayoutState(RelationLayoutState.Failed, error); } finally { _buildingRelation = false; } }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        _buildingRelation = true;
        try
        {
        if (_disposed) return [];
        if (_dataDirty || _snapshot is null)
        {
            var revision = _dataRevision; var candidate = ReadSnapshot(data);
            if (revision != _dataRevision) { InvalidateEngineering(); return []; }
            _snapshot = candidate; _dataDirty = false; _layoutDirty = true;
        }
        if ((_running || _layoutSuppressed) && _layoutDirty) { _painted.Clear(); _indices.Clear(); return []; }
        if (_layoutDirty || _graphLayout is null && _chordLayout is null) { AcceptLayout(CalculateLayout(_snapshot, CancellationToken.None)); SetLayoutState(RelationLayoutState.Completed, null); }
        return PaintLayout(plot);
        }
        finally { _buildingRelation = false; }
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { _attached = true; _dataDirty = true; _layoutSuppressed = false; base.OnAttachedToVisualTree(e); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _attached = false; CancelLayoutCore(true); ReleaseObservers(); _dataDirty = true; base.OnDetachedFromVisualTree(e); }
    public void Dispose() { VerifyAccess(); if (_disposed) return; _disposed = true; CancelLayoutCore(true); ReleaseObservers(); SetCurrentValue(ItemsSourceProperty, null); SetCurrentValue(EdgesSourceProperty, null); _snapshot = null; _graphLayout = null; _chordLayout = null; _painted.Clear(); _indices.Clear(); InvalidateEngineering(); GC.SuppressFinalize(this); }
}
