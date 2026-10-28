namespace NT.AvaloniaControl.TreeDataGrid;

public partial class TreeDataGrid
{
    private void NavigateTree(CellAddress current, bool forward)
    {
        var source = Source;
        if (source is null || !source.TryGetRow(current.Node, out var node)) return;
        NodeId? destination = null;
        if (forward)
        {
            if (node!.HasChildren && !node.IsExpanded)
            { source.SetExpanded(node.Id, true); return; }
            var index = source.GetViewIndex(node!.Id);
            if (node.IsExpanded && index >= 0 && index + 1 < source.Rows.Count && source.Rows[index + 1].Depth > node.Depth)
                destination = source.Rows[index + 1].Id;
        }
        else
        {
            if (node!.IsExpanded && node.HasChildren)
            { source.SetExpanded(node.Id, false); return; }
            var path = node.ModelIndex;
            if (path.Count > 1 && source.TryGetNodeId(new IndexPath(path.Take(path.Count - 1).ToArray()), out var parent) && source.GetViewIndex(parent) >= 0)
                destination = parent;
        }
        // Expansion callbacks can replace the control's source, columns or selection.
        if (destination is not { } id || !ReferenceEquals(Source, source) || GetColumnIndex(current.Column) < 0 || source.GetViewIndex(id) < 0) return;
        if (!SelectAddress(new CellAddress(id, current.Column), extend: false, toggle: false, TreeDataGridSelectionChangeReason.Keyboard)) return;
        RowsPresenter?.ScrollIntoView(id);
        BringColumnIntoView(current.Column);
    }

    private int GetPageRow(int current, bool forward)
    {
        if (Source is not { } source || source.Rows.Count == 0) return 0;
        var version = source.Version;
        RowsPresenter?.EnsureCurrentLayout();
        if (!ReferenceEquals(Source, source) || source.Version != version) return 0;
        var layout = RowsPresenter?.Layout;
        if (layout is null || layout.RowCount != source.Rows.Count)
            layout = new TreeDataGridLayout(source.Rows, VisibleColumns, Bounds.Width, RowHeight, RowHeightSelector, FrozenColumnCount);
        var height = RowsPresenter?.Viewport.Height ?? Bounds.Height;
        if (!double.IsFinite(height) || height <= 0) height = RowHeight;
        var y = layout.GetRowTop(current) + (forward ? height : -height);
        var target = y <= 0 ? 0 : y >= layout.Extent.Height ? source.Rows.Count - 1 : layout.FindRow(y);
        if (forward) target = Math.Max(current + 1, target);
        else target = Math.Min(current - 1, target);
        return Math.Clamp(target, 0, source.Rows.Count - 1);
    }
}
