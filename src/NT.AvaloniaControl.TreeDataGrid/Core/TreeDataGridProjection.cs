namespace NT.AvaloniaControl.TreeDataGrid;

public enum TreeDataGridSortDirection
{
    None,
    Ascending,
    Descending,
}

public abstract partial class TreeDataGridSource
{
    /// <summary>The sort configuration of the last successfully published projection.</summary>
    public virtual ColumnId? SortColumn => null;
    public virtual TreeDataGridSortDirection SortDirection => TreeDataGridSortDirection.None;
    /// <summary>The predicate of the last successfully published projection.</summary>
    public virtual Func<object, bool>? Filter => null;

    /// <summary>Stably sorts siblings without changing model indices or node identities.</summary>
    public virtual void Sort(ColumnId column, Comparison<object> comparison, TreeDataGridSortDirection direction)
        => throw new NotSupportedException("This source does not support sorting.");

    public virtual void ClearSort() { }

    /// <summary>Filters each occurrence. A rejected parent hides its entire branch; ancestors are not retained.</summary>
    public virtual void SetFilter(Func<object, bool>? predicate)
    {
        if (predicate is not null) throw new NotSupportedException("This source does not support filtering.");
    }
}
