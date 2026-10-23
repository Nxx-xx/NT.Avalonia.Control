using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>
/// Adapts a model graph into stable node occurrences. P1 rebuilds the full graph after changes (O(n));
/// expansion only rebuilds the projection. Child selectors should return stable collection instances.
/// </summary>
public abstract partial class TreeDataGridSource<TModel> : TreeDataGridSource where TModel : class
{
    /// <summary>Maximum supported number of model levels, including the root level.</summary>
    public const int MaximumDepth = 256;

    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly IEnumerable<TModel> _items;
    private readonly Func<TModel, IEnumerable<TModel>?>? _childrenSelector;
    private readonly Func<TModel, bool>? _hasChildrenSelector;
    private readonly Func<TModel, bool>? _isExpandedSelector;
    private readonly Action<TModel, bool>? _isExpandedWriter;
    private readonly HashSet<INotifyCollectionChanged> _collectionSubscriptions = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<INotifyPropertyChanged> _modelSubscriptions = new(ReferenceEqualityComparer.Instance);
    private readonly List<CollectionChange> _pendingCollectionChanges = [];
    private List<Node> _roots = [];
    private Dictionary<NodeId, Node> _nodes = [];
    private Dictionary<TModel, List<Node>> _modelOccurrences = new(ReferenceEqualityComparer.Instance);
    private Dictionary<NodeId, TreeDataGridRow> _allRows = [];
    private Dictionary<IndexPath, NodeId> _paths = [];
    private Dictionary<NodeId, int> _viewIndices = [];
    private IReadOnlyList<TreeDataGridRow> _rows = Array.Empty<TreeDataGridRow>();
    private long _version;
    private bool _disposed;
    private bool _building;
    private bool _publishing;
    private bool _draining;
    private bool _pendingRefresh;
    private bool _pendingModelChange;
    private int _deferDepth;
    private bool _deferredChange;
    private ProjectionChangeReason _deferredReason = ProjectionChangeReason.Refresh;

    protected TreeDataGridSource(IEnumerable<TModel> items, Func<TModel, IEnumerable<TModel>?>? childrenSelector)
        : this(items, childrenSelector, null) { }

    protected TreeDataGridSource(IEnumerable<TModel> items, Func<TModel, IEnumerable<TModel>?>? childrenSelector,
        HierarchicalTreeDataGridSourceOptions<TModel>? options)
    {
        ArgumentNullException.ThrowIfNull(items);
        if ((options?.IsExpandedSelector is null) != (options?.IsExpandedWriter is null))
            throw new ArgumentException("Expansion selectors and writers must be supplied together.", nameof(options));
        _items = items;
        _childrenSelector = childrenSelector;
        _hasChildrenSelector = options?.HasChildrenSelector;
        _isExpandedSelector = options?.IsExpandedSelector;
        _isExpandedWriter = options?.IsExpandedWriter;
        Rebuild(ProjectionChangeReason.Refresh, [], notify: false);
    }

    public override IReadOnlyList<TreeDataGridRow> Rows
    {
        get { VerifyAccess(); return _rows; }
    }

    public override long Version
    {
        get { VerifyAccess(); return _version; }
    }

    public override bool IsRefreshDeferred
    {
        get
        {
            VerifyAccess();
            return _deferDepth != 0 || _building || _projecting || _projectionFaulted || _pendingCollectionChanges.Count != 0 || _pendingModelChange || _pendingRefresh
                || HasPendingCollectionChange();
        }
    }

    public override event EventHandler<ProjectionChangedEventArgs>? ProjectionChanged;

    public override bool TryGetRow(NodeId id, out TreeDataGridRow? row)
    {
        VerifyAccess();
        return _allRows.TryGetValue(id, out row);
    }

    public override bool TryGetNodeId(IndexPath modelIndex, out NodeId id)
    {
        VerifyAccess();
        return _paths.TryGetValue(modelIndex, out id);
    }

    public override int GetViewIndex(NodeId id)
    {
        VerifyAccess();
        return _viewIndices.GetValueOrDefault(id, -1);
    }
    internal override bool IsEditNodeCurrent(NodeId id, object model)
    {
        VerifyAccess();
        return _nodes.TryGetValue(id, out var node) && ReferenceEquals(node.Model, model);
    }

    public override IDisposable DeferRefresh()
    {
        VerifyAccess();
        VerifyNotProjecting();
        if (_building)
            throw new InvalidOperationException("Refresh cannot be deferred during graph enumeration.");
        if (_deferDepth == 0) _deferredReason = ProjectionChangeReason.Refresh;
        _deferDepth++;
        return new RefreshScope(this);
    }

    private void EndDeferRefresh()
    {
        VerifyThread();
        if (_disposed)
            return;
        // A rejected projection callback may have already mutated a notifying collection. Reconcile
        // those queued operations while the outer scope still prevents an intermediate publication.
        try
        {
            if (_deferDepth == 1) DrainPendingChanges();
        }
        catch
        {
            _projectionConfiguration = _publishedConfiguration;
            _projectionFaulted = true;
            throw;
        }
        finally { _deferDepth--; }
        if (_deferDepth == 0 && _deferredChange)
        {
            _deferredChange = false;
            CommitProjection(_roots, _deferredReason, notify: true);
            DrainPendingChanges();
        }
        else if (_deferDepth == 0)
        {
            FlushExpansionCompletions();
        }
    }

    public override void Refresh()
    {
        VerifyAccess();
        VerifyNotProjecting();
        if (_building)
            throw new InvalidOperationException("The model graph changed while it was being enumerated.");
        if (_publishing || _rowMoveOperation || HasPendingCollectionChange())
        {
            _pendingRefresh = true;
            return;
        }
        Rebuild(ProjectionChangeReason.Refresh, TakePendingChanges(), notify: true);
        DrainPendingChanges();
    }

    public override void Dispose()
    {
        VerifyThread();
        if (_disposed)
            return;
        VerifyNotProjecting();
        if (_building)
            throw new InvalidOperationException("The source cannot be disposed during graph enumeration.");
        _disposed = true;
        foreach (var collection in _collectionSubscriptions)
        {
            collection.CollectionChanged -= OnCollectionChanged;
            if (collection is ITreeDataGridCollectionChangeTransaction transaction) transaction.ChangeCompleted -= CollectionChangeCompleted;
        }
        foreach (var model in _modelSubscriptions)
            model.PropertyChanged -= OnPropertyChanged;
        _collectionSubscriptions.Clear();
        _modelSubscriptions.Clear();
        _pendingCollectionChanges.Clear();
        _roots.Clear();
        _nodes.Clear();
        _modelOccurrences.Clear();
        _allRows.Clear();
        _paths.Clear();
        _viewIndices.Clear();
        _rows = Array.Empty<TreeDataGridRow>();
        _projectionConfiguration = _publishedConfiguration = new(null, null, TreeDataGridSortDirection.None, null);
        ProjectionChanged = null;
        ClearRefreshResumedHandlers();
        _expansionCompletions.Clear();
        ClearExpansionEventHandlers();
    }

    private void VerifyThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("A TreeDataGridSource must be accessed and notified on its creating thread.");
    }

    private void VerifyAccess()
    {
        VerifyThread();
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        VerifyAccess();
        TrackRowMoveNotification(sender, args);
        if (_building)
            throw new InvalidOperationException("The model graph changed while it was being enumerated.");
        var change = new CollectionChange(sender, args.Action, args.OldStartingIndex, args.NewStartingIndex,
            args.OldItems?.Count ?? 0, args.NewItems?.Count ?? 0);
        if (_rowMoveOperation)
        {
            // Transaction adapters publish after both collections have committed. Reconcile both indexed
            // notifications together so the first graph rebuild cannot apply the second insertion twice.
            _pendingCollectionChanges.Add(change);
            return;
        }
        if (_projecting)
        {
            _pendingCollectionChanges.Add(change);
            _projectionFaulted = true;
            VerifyNotProjecting();
        }
        if (_publishing || _writingExpansion || _rowMoveOperation || HasPendingCollectionChange(sender))
        {
            _pendingCollectionChanges.Add(change);
            return;
        }
        _pendingCollectionChanges.Add(change);
        _pendingRefresh = false;
        _pendingModelChange = false;
        Rebuild(ProjectionChangeReason.CollectionChanged, TakePendingChanges(), notify: true);
        DrainPendingChanges();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        VerifyAccess();
        if (_building)
            throw new InvalidOperationException("The model graph changed while it was being enumerated.");
        if (_projecting)
        {
            _pendingModelChange = true;
            _projectionFaulted = true;
            VerifyNotProjecting();
        }
        if (_publishing || _writingExpansion || _rowMoveOperation || HasPendingCollectionChange())
        {
            _pendingModelChange = true;
            return;
        }
        Rebuild(ProjectionChangeReason.ModelChanged, TakePendingChanges(), notify: true);
        DrainPendingChanges();
    }

    private void DrainPendingChanges()
    {
        if (_draining || HasPendingCollectionChange())
            return;
        // A subscriber may change an ObservableCollection during delivery. Publish that batch only after
        // all subscribers have observed the preceding version, and apply every indexed operation in order.
        _draining = true;
        List<Exception>? errors = null;
        try
        {
            while (!_disposed && (_pendingCollectionChanges.Count != 0 || _pendingModelChange || _pendingRefresh))
            {
                var changes = _pendingCollectionChanges.ToArray();
                var reason = changes.Length != 0 ? ProjectionChangeReason.CollectionChanged
                    : _pendingModelChange ? ProjectionChangeReason.ModelChanged : ProjectionChangeReason.Refresh;
                _pendingCollectionChanges.Clear();
                _pendingModelChange = false;
                _pendingRefresh = false;
                var previousVersion = _version;
                try { Rebuild(reason, changes, notify: true); }
                catch (Exception error)
                {
                    (errors ??= []).Add(error);
                    // A failed listener follows a successful commit; continue draining any mutations it
                    // queued. A graph validation failure requires the owner to correct the source data.
                    if (_version == previousVersion) break;
                }
            }
        }
        finally { _draining = false; }
        ThrowListenerErrors(errors);
    }

    private void Rebuild(ProjectionChangeReason reason, IReadOnlyList<CollectionChange> changes, bool notify)
    {
        _building = true;
        List<Node> roots;
        var collections = new HashSet<INotifyCollectionChanged>(ReferenceEqualityComparer.Instance);
        var models = new HashSet<INotifyPropertyChanged>(ReferenceEqualityComparer.Instance);
        try
        {
            roots = BuildNodes(_items, _items, _roots, default,
                new HashSet<TModel>(ReferenceEqualityComparer.Instance), changes, collections, models);
        }
        finally
        {
            _building = false;
        }
        // Validation happens before subscriptions and the current projection are changed. A bad graph
        // leaves the last valid snapshot available; correct the model and call Refresh to reconcile it.
        UpdateSubscriptions(collections, models);
        if (_deferDepth != 0)
        {
            _roots = roots;
            IndexNodes(roots);
            _deferredChange = true;
        }
        else
        {
            CommitProjection(roots, reason, notify);
        }
    }

    private static IEnumerable<Node> EnumerateNodes(List<Node> roots)
    {
        foreach (var node in roots)
        {
            yield return node;
            foreach (var child in EnumerateNodes(node.Children))
                yield return child;
        }
    }

    private void IndexNodes(List<Node> roots)
    {
        _nodes = new Dictionary<NodeId, Node>();
        _modelOccurrences = new Dictionary<TModel, List<Node>>(ReferenceEqualityComparer.Instance);
        foreach (var node in EnumerateNodes(roots))
        {
            _nodes.Add(node.Id, node);
            if (_isExpandedSelector is not null)
            {
                if (!_modelOccurrences.TryGetValue(node.Model, out var occurrences))
                    _modelOccurrences.Add(node.Model, occurrences = []);
                occurrences.Add(node);
            }
        }
    }

    private List<Node> BuildNodes(IEnumerable<TModel>? items, IEnumerable<TModel>? previousItems,
        List<Node> previousNodes, IndexPath parentPath, HashSet<TModel> ancestors, IReadOnlyList<CollectionChange> changes,
        HashSet<INotifyCollectionChanged> collections, HashSet<INotifyPropertyChanged> models)
    {
        if (items is null)
            return [];
        if (items is INotifyCollectionChanged observable)
            collections.Add(observable);
        var values = items.ToArray();
        if (values.Length != 0 && parentPath.Count >= MaximumDepth)
            throw new InvalidOperationException($"The model graph exceeds the maximum depth of {MaximumDepth} levels.");

        var candidates = ReferenceEquals(items, previousItems)
            ? ReconcileCandidates(items, values, previousNodes, changes)
            : new Node?[values.Length];
        var result = new List<Node>(values.Length);
        for (var index = 0; index < values.Length; index++)
        {
            var model = values[index] ?? throw new InvalidOperationException("Null models are not supported.");
            if (!ancestors.Add(model))
                throw new InvalidOperationException("The model graph contains a cycle along an ancestor path.");
            try
            {
                if (model is INotifyPropertyChanged notifyingModel)
                    models.Add(notifyingModel);
                var previous = candidates[index];
                var path = parentPath.Append(index);
                var children = _childrenSelector?.Invoke(model);
                var childNodes = BuildNodes(children, previous?.ChildrenCollection, previous?.Children ?? [],
                    path, ancestors, changes, collections, models);
                result.Add(new Node(previous?.Id ?? NodeIdentity.Allocate(), model, children, childNodes,
                    _isExpandedSelector?.Invoke(model) ?? previous?.IsExpanded ?? false,
                    childNodes.Count != 0 || _hasChildrenSelector?.Invoke(model) == true, path));
            }
            finally
            {
                ancestors.Remove(model);
            }
        }
        return result;
    }

    private static Node?[] ReconcileCandidates(IEnumerable<TModel> items, TModel[] values,
        List<Node> previousNodes, IReadOnlyList<CollectionChange> changes)
    {
        var applicableChanges = changes.Where(change => ReferenceEquals(change.Collection, items)).ToArray();
        if (applicableChanges.Length == 0)
        {
            // A Refresh can reconcile a non-notifying collection. Reference queues preserve separate
            // identities for repeated objects, in occurrence order; indexed Move notifications are exact.
            var occurrences = new Dictionary<TModel, Queue<Node>>(ReferenceEqualityComparer.Instance);
            foreach (var node in previousNodes)
            {
                if (!occurrences.TryGetValue(node.Model, out var queue))
                    occurrences.Add(node.Model, queue = new Queue<Node>());
                queue.Enqueue(node);
            }
            return values.Select(model => model is not null && occurrences.TryGetValue(model, out var queue)
                && queue.Count != 0 ? queue.Dequeue() : null).ToArray();
        }

        var slots = previousNodes.Cast<Node?>().ToList();
        foreach (var change in applicableChanges)
        {
            if (!ApplyChange(slots, change))
            {
                // Custom notifiers can omit indices. Resetting identities is safer than associating a
                // model with an unrelated edit or selection target.
                return new Node?[values.Length];
            }
        }
        if (slots.Count != values.Length)
            return new Node?[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            if (slots[index] is { } node && !ReferenceEquals(node.Model, values[index]))
                slots[index] = null;
        }
        return slots.ToArray();
    }

    private static bool ApplyChange(List<Node?> slots, CollectionChange change)
    {
        static bool InRange(int index, int count, int length) => index >= 0 && count >= 0 && index <= length - count;
        switch (change.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (!InRange(change.NewIndex, 0, slots.Count)) return false;
                slots.InsertRange(change.NewIndex, new Node?[change.NewCount]);
                return true;
            case NotifyCollectionChangedAction.Remove:
                if (!InRange(change.OldIndex, change.OldCount, slots.Count)) return false;
                slots.RemoveRange(change.OldIndex, change.OldCount);
                return true;
            case NotifyCollectionChangedAction.Replace:
                if (!InRange(change.OldIndex, change.OldCount, slots.Count)) return false;
                slots.RemoveRange(change.OldIndex, change.OldCount);
                if (!InRange(change.NewIndex, 0, slots.Count)) return false;
                slots.InsertRange(change.NewIndex, new Node?[change.NewCount]);
                return true;
            case NotifyCollectionChangedAction.Move:
                if (!InRange(change.OldIndex, change.OldCount, slots.Count)) return false;
                var moving = slots.GetRange(change.OldIndex, change.OldCount);
                slots.RemoveRange(change.OldIndex, change.OldCount);
                if (!InRange(change.NewIndex, 0, slots.Count)) return false;
                slots.InsertRange(change.NewIndex, moving);
                return true;
            case NotifyCollectionChangedAction.Reset:
                // A reset's current count may include later queued operations. Discard all candidates;
                // a subsequent operation falls back to fresh identities for the final collection.
                slots.Clear();
                return false;
            default:
                return false;
        }
    }

    private void UpdateSubscriptions(HashSet<INotifyCollectionChanged> collections, HashSet<INotifyPropertyChanged> models)
    {
        foreach (var collection in _collectionSubscriptions.Where(collection => !collections.Contains(collection)).ToArray())
        {
            collection.CollectionChanged -= OnCollectionChanged;
            if (collection is ITreeDataGridCollectionChangeTransaction transaction) transaction.ChangeCompleted -= CollectionChangeCompleted;
            _collectionSubscriptions.Remove(collection);
        }
        foreach (var collection in collections)
        {
            if (_collectionSubscriptions.Add(collection))
            {
                collection.CollectionChanged += OnCollectionChanged;
                if (collection is ITreeDataGridCollectionChangeTransaction transaction) transaction.ChangeCompleted += CollectionChangeCompleted;
            }
        }
        foreach (var model in _modelSubscriptions.Where(model => !models.Contains(model)).ToArray())
        {
            model.PropertyChanged -= OnPropertyChanged;
            _modelSubscriptions.Remove(model);
        }
        foreach (var model in models)
        {
            if (_modelSubscriptions.Add(model))
                model.PropertyChanged += OnPropertyChanged;
        }
    }

    private bool HasPendingCollectionChange(object? excluding = null) => _collectionSubscriptions.Any(collection =>
        !ReferenceEquals(collection, excluding) && collection is ITreeDataGridCollectionChangeTransaction { IsChangePending: true });

    private void CollectionChangeCompleted(object? sender, EventArgs args)
    {
        VerifyThread();
        if (_disposed || _rowMoveOperation || _publishing || _building || HasPendingCollectionChange()) return;
        List<Exception>? errors = null;
        try { DrainPendingChanges(); } catch (Exception error) { (errors ??= []).Add(error); }
        if (!_disposed && !IsRefreshDeferred)
            try { NotifyRefreshResumed(); } catch (Exception error) { (errors ??= []).Add(error); }
        ThrowListenerErrors(errors);
    }

    private void CommitProjection(List<Node> roots, ProjectionChangeReason reason, bool notify)
    {
        PreparedProjection projection;
        try { projection = PrepareProjection(roots, _projectionConfiguration); }
        catch
        {
            // Structural updates already happened in the model. Keep their reconciled identities and
            // subscriptions, but leave every published map/configuration at its last successful version.
            _roots = roots;
            IndexNodes(roots);
            _projectionFaulted = true;
            _projectionConfiguration = _publishedConfiguration;
            throw;
        }
        PublishProjection(projection, reason, notify);
    }

    private void PublishProjection(PreparedProjection projection, ProjectionChangeReason reason, bool notify)
    {
        var removed = _allRows.Keys.Where(id => !projection.Nodes.ContainsKey(id)).ToArray();
        _roots = projection.Roots;
        _nodes = projection.Nodes;
        _modelOccurrences = new Dictionary<TModel, List<Node>>(ReferenceEqualityComparer.Instance);
        if (_isExpandedSelector is not null)
        {
            // Shared-model expansion events retain source occurrence order even in a sorted view.
            foreach (var node in EnumerateNodes(_roots))
            {
                if (!_modelOccurrences.TryGetValue(node.Model, out var occurrences))
                    _modelOccurrences.Add(node.Model, occurrences = []);
                occurrences.Add(node);
            }
        }
        _allRows = projection.AllRows;
        _paths = projection.Paths;
        _viewIndices = projection.ViewIndices;
        _rows = projection.Rows;
        _projectionConfiguration = _publishedConfiguration = projection.Configuration;
        _projectionFaulted = false;
        _deferredChange = false;
        _version++;
        CaptureCommittedRowMoveIdentity();
        if (!notify)
            return;
        _publishing = true;
        List<Exception>? errors = null;
        try
        {
            var args = new ProjectionChangedEventArgs(_version, reason, removed);
            if (ProjectionChanged is { } handlers)
            {
                foreach (EventHandler<ProjectionChangedEventArgs> handler in handlers.GetInvocationList())
                {
                    if (_disposed) break;
                    try { handler(this, args); }
                    catch (Exception error) { (errors ??= []).Add(error); }
                }
            }
        }
        finally
        {
            _publishing = false;
        }
        try { DrainPendingChanges(); }
        catch (Exception error) { (errors ??= []).Add(error); }
        try { FlushExpansionCompletions(); }
        catch (Exception error) { (errors ??= []).Add(error); }
        ThrowListenerErrors(errors);
    }

    private static void ThrowListenerErrors(List<Exception>? errors)
    {
        if (errors is { Count: 1 })
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is { Count: > 1 })
            throw new AggregateException("Projection change listeners failed.", errors);
    }

    private sealed class Node(NodeId id, TModel model, IEnumerable<TModel>? childrenCollection, List<Node> children,
        bool expanded, bool hasChildren, IndexPath modelIndex)
    {
        public NodeId Id { get; } = id;
        public TModel Model { get; } = model;
        public IEnumerable<TModel>? ChildrenCollection { get; } = childrenCollection;
        public List<Node> Children { get; } = children;
        public bool IsExpanded { get; set; } = expanded;
        public bool HasChildren { get; } = hasChildren;
        public IndexPath ModelIndex { get; } = modelIndex;
    }

    private sealed record CollectionChange(object? Collection, NotifyCollectionChangedAction Action,
        int OldIndex, int NewIndex, int OldCount, int NewCount);

    private sealed class RefreshScope(TreeDataGridSource<TModel> source) : IDisposable
    {
        private TreeDataGridSource<TModel>? _source = source;

        public void Dispose()
        {
            if (_source is not { } owner)
                return;
            owner.VerifyThread();
            owner.VerifyNotProjecting();
            _source = null;
            owner.EndDeferRefresh();
        }
    }
}

/// <summary>A flat model collection with stable identities and observable collection/property updates.</summary>
public sealed class FlatTreeDataGridSource<TModel>(IEnumerable<TModel> items) : TreeDataGridSource<TModel>(items, null)
    where TModel : class;

/// <summary>An eagerly indexed tree. Cyclic ancestor paths and trees exceeding 256 levels are rejected.</summary>
public sealed class HierarchicalTreeDataGridSource<TModel> : TreeDataGridSource<TModel> where TModel : class
{
    public HierarchicalTreeDataGridSource(IEnumerable<TModel> items, Func<TModel, IEnumerable<TModel>?> childrenSelector)
        : base(items, childrenSelector ?? throw new ArgumentNullException(nameof(childrenSelector))) { }

    public HierarchicalTreeDataGridSource(IEnumerable<TModel> items, Func<TModel, IEnumerable<TModel>?> childrenSelector,
        HierarchicalTreeDataGridSourceOptions<TModel> options)
        : base(items, childrenSelector ?? throw new ArgumentNullException(nameof(childrenSelector)),
            options ?? throw new ArgumentNullException(nameof(options))) { }
}
