namespace NT.AvaloniaControl.TreeDataGrid;

public partial class TreeDataGrid
{
    private IReadOnlyList<TreeDataGridColumn> _visibleColumns = Array.AsReadOnly(Array.Empty<TreeDataGridColumn>());
    internal long ColumnOperationVersion { get; private set; }

    /// <summary>Columns in display order. All presentation indices refer to this sequence.</summary>
    public IReadOnlyList<TreeDataGridColumn> VisibleColumns
    {
        get { RefreshVisibleColumns(); return _visibleColumns; }
    }

    /// <summary>Returns the visible display index, or -1 for a hidden or absent column.</summary>
    public int GetColumnIndex(ColumnId column)
    {
        var visible = VisibleColumns;
        for (var i = 0; i < visible.Count; ++i) if (visible[i].Id == column) return i;
        return -1;
    }

    private void RefreshVisibleColumns()
    {
        var index = 0;
        var changed = false;
        foreach (var column in Columns)
        {
            if (!column.IsVisible) continue;
            if (index >= _visibleColumns.Count || !ReferenceEquals(column, _visibleColumns[index])) changed = true;
            index++;
        }
        if (!changed && index == _visibleColumns.Count) return;
        var visible = Columns.Where(column => column.IsVisible).ToArray();
        _visibleColumns = Array.AsReadOnly(visible);
        _state?.SynchronizeOwnerColumns(visible.Select(column => column.Id).ToArray());
    }
}
