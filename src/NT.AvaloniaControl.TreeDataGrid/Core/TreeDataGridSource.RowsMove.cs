namespace NT.AvaloniaControl.TreeDataGrid;

public abstract partial class TreeDataGridSource<TModel> where TModel : class
{
    private Func<Node?, IndexPath, TModel, bool?>? _batchMoveExpansion;
    public override TreeDataGridRowsMoveResult MoveRows(IReadOnlyList<NodeId> nodes, NodeId target, TreeDataGridRowDropPosition position)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(nodes);
        if (_rowMoveOperation || _publishing || _building || _draining || _projecting || _expansionOperation || _flushingExpansion || IsRefreshDeferred)
            return new(TreeDataGridRowMoveStatus.Stale);
        var rows = RowMoveNormalization.Rows(this, nodes);
        if (rows is not { Length: > 0 } || !Enum.IsDefined(position) || SortDirection != TreeDataGridSortDirection.None || Filter is not null
            || !_nodes.TryGetValue(target, out var landing) || rows.Any(row => IsPathPrefix(row.ModelIndex, landing.ModelIndex)))
            return new(TreeDataGridRowMoveStatus.Rejected);
        var parentPath = position == TreeDataGridRowDropPosition.Inside ? landing.ModelIndex : ParentPath(landing.ModelIndex);
        var collection = position == TreeDataGridRowDropPosition.Inside ? landing.ChildrenCollection : GetCollection(parentPath);
        var destination = RowMoveAdapters.Get(collection);
        var origins = rows.Select(row => RowMoveAdapters.Get(GetCollection(ParentPath(row.ModelIndex)))).ToArray();
        // The single-node ObservableCollection contract remains available without implying batch atomicity.
        if (rows.Length == 1 && origins[0] is { CanMoveBatch: false } && ParentPath(rows[0].ModelIndex) == parentPath)
        {
            var original = Descendants(_nodes[rows[0].Id]).ToArray();
            var result = MoveRow(rows[0].Id, target, position);
            var map = result.IsCommitted && !_disposed ? original.Where(node => TryGetRow(node.Id, out var current) && ReferenceEquals(current!.Model, node.Model))
                .ToDictionary(node => node.Id, node => node.Id) : null;
            return new(result.Status, map, result.Error);
        }
        if (destination is not { CanMoveBatch: true } || origins.Any(origin => origin is not { CanMoveBatch: true }
            || origin.Storage.GetType() != destination.Storage.GetType())) return new(TreeDataGridRowMoveStatus.Unsupported);
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index]; var origin = origins[index]!;
            if (row.ModelIndex[^1] >= origin.MoveCount || !ReferenceEquals(origin.MoveItem(row.ModelIndex[^1]), row.Model))
                return new(TreeDataGridRowMoveStatus.Stale);
            if (ReferenceEquals(origin.Storage, destination.Storage) && ParentPath(row.ModelIndex) != parentPath)
                return new(TreeDataGridRowMoveStatus.Rejected);
            for (var other = 0; other < index; other++)
                if (ReferenceEquals(origin.Storage, origins[other]!.Storage) && ParentPath(row.ModelIndex) != ParentPath(rows[other].ModelIndex))
                    return new(TreeDataGridRowMoveStatus.Rejected);
            var models = new HashSet<TModel>(ReferenceEqualityComparer.Instance);
            var children = new HashSet<IEnumerable<TModel>>(ReferenceEqualityComparer.Instance);
            var depth = CollectModels(_nodes[row.Id], models, children);
            if (children.Contains(collection!) || parentPath.Count + depth > MaximumDepth) return new(TreeDataGridRowMoveStatus.Rejected);
            for (var length = 1; length <= parentPath.Count; length++)
            {
                if (!TryGetNodeId(new IndexPath(parentPath.Take(length).ToArray()), out var ancestor) || !_nodes.TryGetValue(ancestor, out var parent))
                    return new(TreeDataGridRowMoveStatus.Stale);
                if (models.Contains(parent.Model)) return new(TreeDataGridRowMoveStatus.Rejected);
            }
        }
        var insertion = position switch
        {
            TreeDataGridRowDropPosition.Before => landing.ModelIndex[^1],
            TreeDataGridRowDropPosition.After => landing.ModelIndex[^1] + 1,
            _ => destination.MoveCount,
        };
        var localIndices = rows.Where(row => ParentPath(row.ModelIndex) == parentPath).Select(row => row.ModelIndex[^1]).ToHashSet();
        insertion -= localIndices.Count(index => index < insertion);
        if (origins.All(origin => ReferenceEquals(origin!.Storage, destination.Storage)))
        {
            var order = Enumerable.Range(0, destination.MoveCount).Where(index => !localIndices.Contains(index)).ToList();
            order.InsertRange(insertion, localIndices.Order());
            if (order.SequenceEqual(Enumerable.Range(0, destination.MoveCount))) return new(TreeDataGridRowMoveStatus.Rejected);
        }
        NodeId? destinationParent = parentPath.Count > 0 && TryGetNodeId(parentPath, out var parentId) ? parentId : null;
        var entries = rows.SelectMany((row, root) => Descendants(_nodes[row.Id]).Select(node =>
            (Node: node, Root: root, Relative: node.ModelIndex.Skip(row.ModelIndex.Count).ToArray()))).ToArray();
        var entriesByPosition = entries.ToDictionary(entry => (entry.Root, Relative: new IndexPath(entry.Relative)));
        var entriesById = entries.ToDictionary(entry => entry.Node.Id);
        Dictionary<NodeId, NodeId> identityMap = [];
        var versions = origins.Append(destination).Select(origin => origin!).DistinctBy(origin => origin.Storage)
            .ToDictionary(origin => origin, origin => origin.MoveVersion);
        var slots = rows.Select((row, index) => new RowMoveSlot(origins[index]!, row.ModelIndex[^1])).ToArray();
        List<Exception> errors = [];
        var committed = false;
        _rowMoveOperation = true; _rowMoveIdentityCaptured = false;
        IndexPath? rebuildingParent = destinationParent is null ? default(IndexPath) : null;
        _batchMoveExpansion = (previous, path, model) =>
        {
            if (destinationParent is { } parentIdentity && previous?.Id == parentIdentity) rebuildingParent = path;
            if (rebuildingParent is not { } parent || !IsPathPrefix(parent, path) || path.Count <= parent.Count) return null;
            var root = path[parent.Count] - insertion;
            if (root < 0 || root >= rows.Length) return null;
            var relative = new IndexPath(path.Skip(parent.Count + 1).ToArray());
            return entriesByPosition.TryGetValue((root, relative), out var original) && ReferenceEquals(original.Node.Model, model) ? original.Node.IsExpanded : null;
        };
        _captureBatchIdentity = () =>
        {
            var currentParent = destinationParent is { } destinationId && _allRows.TryGetValue(destinationId, out var parent) ? parent.ModelIndex : default;
            if (destinationParent is not null && currentParent.Count == 0) return;
            foreach (var entry in entries)
            {
                var path = new IndexPath(currentParent.Append(insertion + entry.Root).Concat(entry.Relative).ToArray());
                if (_paths.TryGetValue(path, out var id) && _allRows.TryGetValue(id, out var moved) && ReferenceEquals(moved.Model, entry.Node.Model))
                    identityMap[entry.Node.Id] = id;
            }
        };
        var scope = DeferRefresh();
        try
        {
            try { destination.MoveBatch(slots, insertion); committed = true; }
            catch (Exception error)
            {
                committed = versions.Any(pair => pair.Key.MoveVersion != pair.Value); errors.Add(error);
            }
        }
        finally
        {
            try { scope.Dispose(); } catch (Exception error) { errors.Add(error); }
            _rowMoveOperation = false; _captureBatchIdentity = null; _batchMoveExpansion = null;
        }
        if (!committed) return new(TreeDataGridRowMoveStatus.HandlerFailed, error: CombineMoveErrors(errors));
        if (_disposed) { identityMap.Clear(); errors.Add(new InvalidOperationException("The source was disposed by a committed move observer.")); }
        else foreach (var old in identityMap.Keys.ToArray())
            if (!TryGetRow(identityMap[old], out var row) || !ReferenceEquals(row!.Model, entriesById[old].Node.Model)) identityMap.Remove(old);
        return new(errors.Count == 0 ? TreeDataGridRowMoveStatus.Committed : TreeDataGridRowMoveStatus.CommittedWithObserverError,
            identityMap, CombineMoveErrors(errors));
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
