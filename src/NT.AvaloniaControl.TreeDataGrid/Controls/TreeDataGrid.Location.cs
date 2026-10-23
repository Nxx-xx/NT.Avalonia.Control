namespace NT.AvaloniaControl.TreeDataGrid;

public partial class TreeDataGrid
{
    /// <summary>
    /// Scrolls a source node into the current viewport. Selection and current cell are unchanged unless
    /// <paramref name="select"/> is true. Collapsed ancestors are only expanded when requested.
    /// Returns false for a missing, filtered, deferred or stale target, or when no attached viewport is available.
    /// Expansion callbacks may run; earlier successful expansions are not rolled back when a later check fails.
    /// </summary>
    public bool TryScrollIntoView(NodeId node, bool expandAncestors = false, bool select = false)
    {
        VerifyAccess();
        var source = Source;
        var presenter = RowsPresenter;
        if (source is null || !_attached || presenter is null || presenter.Viewport.Width <= 0 || presenter.Viewport.Height <= 0
            || source.IsRefreshDeferred || !source.TryGetRow(node, out var row)) return false;

        var path = row!.ModelIndex;
        var model = row.Model;
        var columnsVersion = ColumnOperationVersion;
        ColumnId? column = null;
        if (select)
        {
            var currentColumn = State?.CurrentCell?.Column;
            column = currentColumn is { } current && GetColumnIndex(current) >= 0 ? current : VisibleColumns.FirstOrDefault()?.Id;
            if (column is null) return false;
            if (State?.EditSession is { } edit && edit.Cell != new CellAddress(node, column.Value)
                && !CommitEdit()) return false;
        }

        bool TargetIsCurrent() => ReferenceEquals(Source, source) && ReferenceEquals(RowsPresenter, presenter) && _attached
            && ColumnOperationVersion == columnsVersion && !source.IsRefreshDeferred
            && source.TryGetRow(node, out var current) && ReferenceEquals(current!.Model, model) && current.ModelIndex == path
            && (column is null || GetColumnIndex(column.Value) >= 0);

        if (!TargetIsCurrent()) return false;
        if (expandAncestors)
        {
            var ancestors = new List<(IndexPath Path, NodeId Id)>();
            for (var length = 1; length < path.Count; length++)
            {
                var ancestorPath = new IndexPath(path.Take(length).ToArray());
                if (!source.TryGetNodeId(ancestorPath, out var ancestor)) return false;
                ancestors.Add((ancestorPath, ancestor));
            }
            foreach (var (ancestorPath, ancestor) in ancestors)
            {
                if (!TargetIsCurrent() || !source.TryGetNodeId(ancestorPath, out var currentId) || currentId != ancestor
                    || !source.TryGetRow(ancestor, out var parent)) return false;
                if (!parent!.IsExpanded) source.SetExpanded(ancestor, true);
                if (!TargetIsCurrent() || !source.TryGetRow(ancestor, out parent) || !parent!.IsExpanded) return false;
            }
        }

        if (!TargetIsCurrent() || source.GetViewIndex(node) < 0) return false;
        var version = source.Version;
        presenter.EnsureCurrentLayout();
        if (!TargetIsCurrent() || source.Version != version || presenter.Layout is null) return false;
        if (!presenter.ScrollIntoView(node)) return false;
        if (!TargetIsCurrent() || source.Version != version) return false;
        if (!select) return true;
        SelectAddress(new CellAddress(node, column!.Value), extend: false, toggle: false);
        if (!TargetIsCurrent() || source.Version != version) return false;
        BringColumnIntoView(column.Value);
        return TargetIsCurrent() && source.Version == version;
    }

    /// <summary>
    /// Resolves a zero-based path in the source collections, independently of sorting and visible row order,
    /// then applies <see cref="TryScrollIntoView(NodeId, bool, bool)"/> to that node identity.
    /// </summary>
    public bool TryScrollIntoView(IndexPath sourcePath, bool expandAncestors = false, bool select = false)
    {
        VerifyAccess();
        var source = Source;
        if (source is null || source.IsRefreshDeferred) return false;
        var version = source.Version;
        return source.TryGetNodeId(sourcePath, out var node) && ReferenceEquals(Source, source) && source.Version == version
            && TryScrollIntoView(node, expandAncestors, select);
    }
}
