namespace NT.AvaloniaControl.TreeDataGrid;

public abstract partial class TreeDataGridSource<TModel> where TModel : class
{
    private readonly List<(NodeId Id, bool Expanded)> _expansionCompletions = [];
    private bool _expansionOperation;
    private bool _writingExpansion;
    private bool _flushingExpansion;

    public override bool SetExpanded(NodeId id, bool expanded) =>
        RunExpansion(() => ChangeExpansion(id, expanded)) != 0;

    public override int SetExpandedRecursive(bool expanded, Func<TreeDataGridRow, bool>? predicate = null)
    {
        return RunExpansion(() =>
        {
            var visited = new HashSet<NodeId>();
            var changed = 0;
            void Visit(NodeId id)
            {
                if (_disposed || !visited.Add(id) || !_nodes.TryGetValue(id, out var node)) return;
                if (predicate?.Invoke(Snapshot(node)) == false) return;
                if (_disposed || !_nodes.TryGetValue(id, out node)) return;
                if (expanded)
                {
                    if (node.HasChildren && !node.IsExpanded) changed += ChangeExpansion(id, true);
                    // Cancellation prunes the branch; newly loaded children are included after the before event.
                    if (!_nodes.TryGetValue(id, out node) || !node.IsExpanded) return;
                    foreach (var child in node.Children.Select(child => child.Id).ToArray()) Visit(child);
                }
                else
                {
                    // Children first keeps collapsing notifications in a deterministic postorder.
                    foreach (var child in node.Children.Select(child => child.Id).ToArray()) Visit(child);
                    if (!_disposed) changed += ChangeExpansion(id, false);
                }
            }
            foreach (var root in _roots.Select(root => root.Id).ToArray()) Visit(root);
            return changed;
        });
    }

    private int RunExpansion(Func<int> action)
    {
        VerifyAccess();
        VerifyNotProjecting();
        if (_publishing || _building || _expansionOperation || _flushingExpansion || _rowMoveOperation)
            throw new InvalidOperationException("Expansion cannot be reentered from source or expansion callbacks.");
        _expansionOperation = true;
        var ownsBatch = _deferDepth == 0;
        var scope = DeferRefresh();
        if (ownsBatch) _deferredReason = ProjectionChangeReason.ExpansionChanged;
        var result = 0;
        List<Exception>? errors = null;
        try
        {
            try { result = action(); }
            catch (Exception error) { (errors ??= []).Add(error); }
            try { if (!_disposed) DrainPendingChanges(); }
            catch (Exception error) { (errors ??= []).Add(error); }
            try { scope.Dispose(); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        finally { _expansionOperation = false; }
        ThrowListenerErrors(errors);
        return result;
    }

    private int ChangeExpansion(NodeId id, bool expanded)
    {
        if (_disposed || !_nodes.TryGetValue(id, out var target) || target.IsExpanded == expanded) return 0;
        var model = target.Model;
        var targets = _isExpandedSelector is null ? [id] : _modelOccurrences[model]
            .Where(node => node.IsExpanded != expanded).Select(node => node.Id).ToArray();
        var cancelled = false;
        foreach (var targetId in targets)
        {
            if (_disposed || !_nodes.TryGetValue(targetId, out var node) || node.IsExpanded == expanded) continue;
            var args = new TreeDataGridRowCancelEventArgs(Snapshot(node));
            if (expanded) OnRowExpanding(args);
            else OnRowCollapsing(args);
            cancelled |= args.Cancel;
        }
        if (_disposed || cancelled || !_nodes.TryGetValue(id, out target) || !ReferenceEquals(target.Model, model)) return 0;
        if (target.IsExpanded == expanded) return 0; // An authoritative model change already handled the request.

        if (_isExpandedWriter is not null)
        {
            _writingExpansion = true;
            try { _isExpandedWriter(model, expanded); }
            finally { _writingExpansion = false; }
            if (_disposed) return 0;
            if (_isExpandedSelector!(model) != expanded)
                throw new InvalidOperationException("The expansion writer did not store the requested state.");
            // Collection side effects must be reconciled before recursive traversal. Ordinary expansion
            // PropertyChanged notifications remain batched, keeping ExpandAll linear for bound trees too.
            if (_pendingCollectionChanges.Count != 0) DrainPendingChanges();
        }

        var changed = 0;
        foreach (var targetId in targets)
        {
            if (!_nodes.TryGetValue(targetId, out var node) || !ReferenceEquals(node.Model, model)) continue;
            // Binding may have rebuilt the graph to the requested state already; it still belongs to this command.
            node.IsExpanded = expanded;
            _expansionCompletions.Add((targetId, expanded));
            changed++;
        }
        if (changed != 0) _deferredChange = true;
        return changed;
    }

    private static TreeDataGridRow Snapshot(Node node) => new(node.Id, node.Model, node.ModelIndex,
        node.ModelIndex.Count - 1, node.IsExpanded, node.HasChildren);

    private void FlushExpansionCompletions()
    {
        if (_disposed || _projectionFaulted || _deferDepth != 0 || _publishing || _draining || _flushingExpansion || _expansionCompletions.Count == 0) return;
        _flushingExpansion = true;
        List<Exception>? errors = null;
        // One completion per identity per published batch, representing its last successful command.
        var seen = new HashSet<NodeId>();
        var completions = _expansionCompletions.AsEnumerable().Reverse()
            .Where(completion => seen.Add(completion.Id)).Reverse().ToArray();
        _expansionCompletions.Clear();
        try
        {
            for (var index = 0; index < completions.Length; index++)
            {
                if (_disposed) break;
                if (_deferDepth != 0)
                {
                    _expansionCompletions.AddRange(completions.AsSpan(index).ToArray());
                    break;
                }
                var completion = completions[index];
                if (!_allRows.TryGetValue(completion.Id, out var row) || row.IsExpanded != completion.Expanded) continue;
                _publishing = true;
                try
                {
                    var args = new TreeDataGridRowModelEventArgs(row);
                    if (completion.Expanded) OnRowExpanded(args);
                    else OnRowCollapsed(args);
                }
                catch (Exception error) { (errors ??= []).Add(error); }
                finally { _publishing = false; }
                try { DrainPendingChanges(); }
                catch (Exception error) { (errors ??= []).Add(error); }
            }
        }
        finally { _flushingExpansion = false; }
        ThrowListenerErrors(errors);
    }
}
