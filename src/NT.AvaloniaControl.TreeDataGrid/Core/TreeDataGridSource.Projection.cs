namespace NT.AvaloniaControl.TreeDataGrid;

public abstract partial class TreeDataGridSource<TModel> where TModel : class
{
    private ProjectionConfiguration _projectionConfiguration = new(null, null, TreeDataGridSortDirection.None, null);
    private ProjectionConfiguration _publishedConfiguration = new(null, null, TreeDataGridSortDirection.None, null);
    private bool _projecting;
    private bool _projectionInvalidated;
    private bool _projectionFaulted;

    public override ColumnId? SortColumn { get { VerifyAccess(); return _publishedConfiguration.Column; } }
    public override TreeDataGridSortDirection SortDirection { get { VerifyAccess(); return _publishedConfiguration.Direction; } }
    public override Func<object, bool>? Filter { get { VerifyAccess(); return _publishedConfiguration.Filter; } }

    public override void Sort(ColumnId column, Comparison<object> comparison, TreeDataGridSortDirection direction)
    {
        VerifyProjectionConfigurationAccess();
        if (column.Value <= 0) throw new ArgumentException("A positive column identity is required.", nameof(column));
        ArgumentNullException.ThrowIfNull(comparison);
        if (direction is not (TreeDataGridSortDirection.Ascending or TreeDataGridSortDirection.Descending))
            throw new ArgumentOutOfRangeException(nameof(direction), "Use ClearSort to remove sorting.");
        ChangeProjectionConfiguration(_projectionConfiguration with { Column = column, Comparison = comparison, Direction = direction },
            ProjectionChangeReason.SortChanged);
    }

    public override void ClearSort()
    {
        VerifyProjectionConfigurationAccess();
        if (_projectionConfiguration.Column is null && !_projectionFaulted) return;
        ChangeProjectionConfiguration(_projectionConfiguration with { Column = null, Comparison = null, Direction = TreeDataGridSortDirection.None },
            ProjectionChangeReason.SortChanged);
    }

    public override void SetFilter(Func<object, bool>? predicate)
    {
        VerifyProjectionConfigurationAccess();
        if (ReferenceEquals(predicate, _projectionConfiguration.Filter) && !_projectionFaulted) return;
        ChangeProjectionConfiguration(_projectionConfiguration with { Filter = predicate }, ProjectionChangeReason.FilterChanged);
    }

    private void VerifyProjectionConfigurationAccess()
    {
        VerifyAccess();
        VerifyNotProjecting();
        if (_building || _publishing || _expansionOperation || _flushingExpansion)
            throw new InvalidOperationException("Sorting and filtering cannot be changed from source callbacks.");
    }

    private void VerifyNotProjecting()
    {
        if (!_projecting) return;
        _projectionInvalidated = true;
        throw new InvalidOperationException("Sorting comparers and filter predicates must not modify the source or its models.");
    }

    private void ChangeProjectionConfiguration(ProjectionConfiguration configuration, ProjectionChangeReason reason)
    {
        var previous = _projectionConfiguration;
        var version = _version;
        _projectionConfiguration = configuration;
        try
        {
            if (_projectionFaulted || _pendingCollectionChanges.Count != 0 || _pendingModelChange || _pendingRefresh)
            {
                // Reconcile callback side effects before recovery, retaining exact indexed occurrence identities.
                Rebuild(reason, TakePendingChanges(), notify: true);
                if (_deferDepth == 0) return;
            }
            var projection = PrepareProjection(_roots, configuration);
            if (_deferDepth != 0)
            {
                _deferredChange = true;
                // An external mixed batch is a Refresh; the published configuration remains unchanged until its end.
                return;
            }
            PublishProjection(projection, reason, notify: true);
        }
        catch
        {
            // Observer failures happen after commitment and must not roll back a successfully delivered version.
            if (_version == version) _projectionConfiguration = previous;
            throw;
        }
    }

    private CollectionChange[] TakePendingChanges()
    {
        var changes = _pendingCollectionChanges.ToArray();
        _pendingCollectionChanges.Clear();
        _pendingModelChange = false;
        _pendingRefresh = false;
        return changes;
    }

    private PreparedProjection PrepareProjection(List<Node> roots, ProjectionConfiguration configuration)
    {
        var nodes = new Dictionary<NodeId, Node>();
        var allRows = new Dictionary<NodeId, TreeDataGridRow>();
        var paths = new Dictionary<IndexPath, NodeId>();
        var viewIndices = new Dictionary<NodeId, int>();
        var rows = new List<TreeDataGridRow>();
        _projecting = true;
        _projectionInvalidated = false;
        try
        {
            void Project(List<Node> siblings, bool visible)
            {
                // Ordering affects only traversal. The model lists and every source IndexPath remain untouched.
                foreach (var node in OrderSiblings(siblings, configuration))
                {
                    var row = Snapshot(node);
                    nodes.Add(node.Id, node);
                    allRows.Add(node.Id, row);
                    paths.Add(node.ModelIndex, node.Id);
                    // Predicates run once per occurrence, including collapsed or rejected-parent descendants.
                    var accepted = configuration.Filter?.Invoke(node.Model) != false;
                    if (visible && accepted)
                    {
                        viewIndices.Add(node.Id, rows.Count);
                        rows.Add(row);
                    }
                    Project(node.Children, visible && accepted && node.IsExpanded);
                }
            }
            Project(roots, visible: true);
            if (_projectionInvalidated)
                throw new InvalidOperationException("A projection callback attempted to modify the source.");
            return new PreparedProjection(roots, nodes, allRows, paths, viewIndices, rows.AsReadOnly(), configuration);
        }
        finally { _projecting = false; }
    }

    private static IEnumerable<Node> OrderSiblings(List<Node> nodes, ProjectionConfiguration configuration)
    {
        if (configuration.Comparison is not { } comparison || nodes.Count < 2) return nodes;
        var result = nodes.ToArray();
        var buffer = new Node[result.Length];
        int Compare(Node left, Node right) => configuration.Direction == TreeDataGridSortDirection.Descending
            ? comparison(right.Model, left.Model) : comparison(left.Model, right.Model);
        // Stable merge sort preserves source occurrence order for equal keys and propagates callback exceptions unchanged.
        void MergeSort(int start, int length)
        {
            if (length < 2) return;
            var middle = start + length / 2;
            var end = start + length;
            MergeSort(start, middle - start);
            MergeSort(middle, end - middle);
            var left = start;
            var right = middle;
            for (var index = start; index < end; index++)
                buffer[index] = right == end || (left < middle && Compare(result[left], result[right]) <= 0)
                    ? result[left++] : result[right++];
            Array.Copy(buffer, start, result, start, length);
        }
        MergeSort(0, result.Length);
        return result;
    }

    private sealed record ProjectionConfiguration(ColumnId? Column, Comparison<object>? Comparison,
        TreeDataGridSortDirection Direction, Func<object, bool>? Filter);

    private sealed record PreparedProjection(List<Node> Roots, Dictionary<NodeId, Node> Nodes,
        Dictionary<NodeId, TreeDataGridRow> AllRows, Dictionary<IndexPath, NodeId> Paths,
        Dictionary<NodeId, int> ViewIndices, IReadOnlyList<TreeDataGridRow> Rows, ProjectionConfiguration Configuration);
}
