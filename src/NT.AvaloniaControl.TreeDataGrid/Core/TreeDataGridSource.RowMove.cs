using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace NT.AvaloniaControl.TreeDataGrid;

public abstract partial class TreeDataGridSource<TModel> where TModel : class
{
    private bool _rowMoveOperation;
    private object? _rowMoveCollection;
    private int _rowMoveOldIndex;
    private int _rowMoveNewIndex;
    private bool _rowMoveObserved;
    private NodeId _rowMoveNode;
    private NodeId? _rowMoveDestinationParent;
    private int _rowMoveDestinationIndex;
    private TModel? _rowMoveModel;
    private NodeId? _rowMoveResultNode;
    private bool _rowMoveIdentityCaptured;
    private Action? _captureBatchIdentity;

    public override TreeDataGridRowMoveResult MoveRow(NodeId node, NodeId target, TreeDataGridRowDropPosition position)
    {
        VerifyAccess();
        if (_rowMoveOperation || _publishing || _building || _draining || _projecting || _expansionOperation || _flushingExpansion || IsRefreshDeferred)
            return new(TreeDataGridRowMoveStatus.Stale);
        if (!Enum.IsDefined(position) || node == target || SortDirection != TreeDataGridSortDirection.None || Filter is not null
            || !_nodes.TryGetValue(node, out var moving) || !_nodes.TryGetValue(target, out var landing))
            return new(TreeDataGridRowMoveStatus.Rejected);

        var originParentPath = ParentPath(moving.ModelIndex);
        var destinationParentPath = position == TreeDataGridRowDropPosition.Inside ? landing.ModelIndex : ParentPath(landing.ModelIndex);
        if (IsPathPrefix(moving.ModelIndex, destinationParentPath)) return new(TreeDataGridRowMoveStatus.Rejected);
        var originCollection = GetCollection(originParentPath);
        var destinationCollection = position == TreeDataGridRowDropPosition.Inside ? landing.ChildrenCollection : GetCollection(destinationParentPath);
        var sameParent = originParentPath == destinationParentPath;
        var origin = RowMoveAdapters.Get(originCollection); var destination = RowMoveAdapters.Get(destinationCollection);
        if (origin is null || destination is null
            || !sameParent && (!origin.CanMoveBatch || !destination.CanMoveBatch || origin.Storage.GetType() != destination.Storage.GetType())
            || !sameParent && ReferenceEquals(origin.Storage, destination.Storage))
            return new(TreeDataGridRowMoveStatus.Unsupported);

        var oldIndex = moving.ModelIndex[^1];
        var insertionIndex = position switch
        {
            TreeDataGridRowDropPosition.Before => landing.ModelIndex[^1],
            TreeDataGridRowDropPosition.After => landing.ModelIndex[^1] + 1,
            _ => destination.MoveCount,
        };
        if (oldIndex < 0 || oldIndex >= origin.MoveCount || !ReferenceEquals(origin.MoveItem(oldIndex), moving.Model)) return new(TreeDataGridRowMoveStatus.Stale);
        if (sameParent && oldIndex < insertionIndex) insertionIndex--;
        if (sameParent && oldIndex == insertionIndex) return new(TreeDataGridRowMoveStatus.Rejected);
        if (insertionIndex < 0 || insertionIndex > destination.MoveCount - (sameParent ? 1 : 0)) return new(TreeDataGridRowMoveStatus.Stale);

        var models = new HashSet<TModel>(ReferenceEqualityComparer.Instance);
        var subtreeCollections = new HashSet<IEnumerable<TModel>>(ReferenceEqualityComparer.Instance);
        var subtreeDepth = CollectModels(moving, models, subtreeCollections);
        if (subtreeCollections.Contains(destinationCollection!)) return new(TreeDataGridRowMoveStatus.Rejected);
        if (destinationParentPath.Count + subtreeDepth > MaximumDepth) return new(TreeDataGridRowMoveStatus.Rejected);
        for (var length = 1; length <= destinationParentPath.Count; length++)
        {
            if (!TryGetNodeId(new IndexPath(destinationParentPath.Take(length).ToArray()), out var ancestor)
                || !_nodes.TryGetValue(ancestor, out var parent)) return new(TreeDataGridRowMoveStatus.Stale);
            if (models.Contains(parent.Model)) return new(TreeDataGridRowMoveStatus.Rejected);
        }

        NodeId? destinationParent = null;
        if (destinationParentPath.Count > 0)
        {
            if (!TryGetNodeId(destinationParentPath, out var parent)) return new(TreeDataGridRowMoveStatus.Stale);
            destinationParent = parent;
        }
        var beforeStorageVersion = origin.MoveVersion;
        var expected = sameParent && beforeStorageVersion is null ? Enumerable.Range(0, origin.MoveCount).Select(origin.MoveItem).ToList() : null;
        if (expected is not null)
        {
            var value = expected[oldIndex]; expected.RemoveAt(oldIndex); expected.Insert(insertionIndex, value);
        }
        _rowMoveOperation = true; _rowMoveCollection = originCollection;
        _rowMoveOldIndex = oldIndex; _rowMoveNewIndex = insertionIndex; _rowMoveObserved = false;
        _rowMoveNode = node; _rowMoveDestinationParent = destinationParent; _rowMoveDestinationIndex = insertionIndex;
        _rowMoveModel = moving.Model; _rowMoveResultNode = null; _rowMoveIdentityCaptured = false;
        var committed = false;
        List<Exception> errors = [];
        var scope = DeferRefresh();
        try
        {
            try
            {
                if (sameParent)
                {
                    origin.MoveSingle(oldIndex, insertionIndex);
                }
                else destination.MoveBatch([new(origin, oldIndex)], insertionIndex);
                committed = true;
            }
            catch (Exception error)
            {
                committed = beforeStorageVersion is not null ? origin.MoveVersion != beforeStorageVersion
                    : expected is not null && Enumerable.Range(0, origin.MoveCount).Select(origin.MoveItem).SequenceEqual(expected, ReferenceEqualityComparer.Instance);
                errors.Add(error);
            }
            if (committed && !_disposed && beforeStorageVersion is null && !_rowMoveObserved)
            {
                // ObservableCollection stops invoking listeners at the first exception. If our listener was skipped,
                // reconcile its exact indexed Move explicitly instead of relying on reference queues for duplicates.
                try { Rebuild(ProjectionChangeReason.CollectionChanged,
                    TakePendingChanges().Append(new CollectionChange(originCollection, NotifyCollectionChangedAction.Move, oldIndex, insertionIndex, 1, 1)).ToArray(), notify: true); }
                catch (Exception error) { errors.Add(error); }
            }
        }
        finally
        {
            try { scope.Dispose(); } catch (Exception error) { errors.Add(error); }
            _rowMoveOperation = false; _rowMoveCollection = null; _rowMoveModel = null;
        }

        if (!committed) return new(TreeDataGridRowMoveStatus.HandlerFailed, Error: CombineMoveErrors(errors));
        NodeId? resultNode = null;
        if (_disposed) errors.Add(new InvalidOperationException("The source was disposed by a committed move observer."));
        else if (_rowMoveResultNode is { } moved && TryGetRow(moved, out var row) && ReferenceEquals(row!.Model, moving.Model)) resultNode = moved;
        return new(errors.Count == 0 ? TreeDataGridRowMoveStatus.Committed : TreeDataGridRowMoveStatus.CommittedWithObserverError,
            resultNode, CombineMoveErrors(errors));
    }

    private void TrackRowMoveNotification(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_rowMoveOperation && ReferenceEquals(sender, _rowMoveCollection) && args.Action == NotifyCollectionChangedAction.Move
            && args.OldStartingIndex == _rowMoveOldIndex && args.NewStartingIndex == _rowMoveNewIndex) _rowMoveObserved = true;
    }

    private void CaptureCommittedRowMoveIdentity()
    {
        if (!_rowMoveOperation || _rowMoveIdentityCaptured) return;
        _rowMoveIdentityCaptured = true;
        if (_captureBatchIdentity is { } capture) { capture(); return; }
        if (_allRows.TryGetValue(_rowMoveNode, out var existing) && ReferenceEquals(existing.Model, _rowMoveModel))
        { _rowMoveResultNode = _rowMoveNode; return; }
        var parentPath = default(IndexPath);
        if (_rowMoveDestinationParent is { } parent)
        {
            if (!_allRows.TryGetValue(parent, out var row)) return;
            parentPath = row.ModelIndex;
        }
        if (_paths.TryGetValue(parentPath.Append(_rowMoveDestinationIndex), out var node)
            && _allRows.TryGetValue(node, out var moved) && ReferenceEquals(moved.Model, _rowMoveModel)) _rowMoveResultNode = node;
    }

    private IEnumerable<TModel>? GetCollection(IndexPath parentPath) => parentPath.Count == 0 ? _items
        : TryGetNodeId(parentPath, out var parent) && _nodes.TryGetValue(parent, out var node) ? node.ChildrenCollection : null;
    private static IndexPath ParentPath(IndexPath path) => new(path.Take(Math.Max(0, path.Count - 1)).ToArray());
    private static bool IsPathPrefix(IndexPath prefix, IndexPath path) => path.Count >= prefix.Count && prefix.SequenceEqual(path.Take(prefix.Count));
    private static int CollectModels(Node node, HashSet<TModel> models, HashSet<IEnumerable<TModel>> collections)
    {
        models.Add(node.Model);
        if (node.ChildrenCollection is { } children) collections.Add(children);
        return 1 + node.Children.Select(child => CollectModels(child, models, collections)).DefaultIfEmpty(0).Max();
    }
    private static Exception? CombineMoveErrors(List<Exception> errors) => errors.Count switch
    {
        0 => null, 1 => errors[0], _ => new AggregateException("Row move observers failed after storage commit.", errors),
    };
}
