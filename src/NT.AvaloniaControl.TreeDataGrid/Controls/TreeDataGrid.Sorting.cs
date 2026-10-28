using System.Collections;
using Avalonia;

namespace NT.AvaloniaControl.TreeDataGrid;

public partial class TreeDataGrid
{
    public static readonly StyledProperty<bool> CanUserSortColumnsProperty =
        AvaloniaProperty.Register<TreeDataGrid, bool>(nameof(CanUserSortColumns), true);
    public static readonly StyledProperty<bool> IsThreeStateSortProperty =
        AvaloniaProperty.Register<TreeDataGrid, bool>(nameof(IsThreeStateSort));

    public bool CanUserSortColumns { get => GetValue(CanUserSortColumnsProperty); set => SetValue(CanUserSortColumnsProperty, value); }
    /// <summary>Cycles ascending, descending and unsorted when enabled; otherwise toggles ascending/descending.</summary>
    public bool IsThreeStateSort { get => GetValue(IsThreeStateSortProperty); set => SetValue(IsThreeStateSortProperty, value); }

    /// <summary>Commits an active editor before sorting. Validation failure prevents the sort and returns false.</summary>
    public bool SortByColumn(ColumnId columnId, TreeDataGridSortDirection direction)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        var column = Columns.FirstOrDefault(candidate => candidate.Id == columnId)
            ?? throw new ArgumentException("The column does not belong to this grid.", nameof(columnId));
        var source = Source;
        if (source is null) return false;
        var canSort = true;
        TryCommitBeforeSort(ref canSort);
        // Editing callbacks can replace Source or remove the clicked column.
        if (!canSort || !ReferenceEquals(source, Source) || !Columns.Contains(column)) return false;
        if (direction == TreeDataGridSortDirection.None) source.ClearSort();
        else
        {
            var inner = column is TreeDataGridHierarchicalExpanderColumn tree ? tree.InnerColumn : column;
            var custom = direction == TreeDataGridSortDirection.Descending ? column.CompareDescending ?? inner.CompareDescending : null;
            var descendingComparison = custom is not null;
            custom ??= column.CompareAscending ?? inner.CompareAscending ?? column.Comparison ?? inner.Comparison;
            var columnVersion = ColumnOperationVersion;
            TreeDataGrid? activeGrid = this;
            void VerifyOwner()
            {
                if (activeGrid is null) return;
                if (!ReferenceEquals(source, activeGrid.Source) || activeGrid.ColumnOperationVersion != columnVersion || !activeGrid.Columns.Contains(column)
                    || column is TreeDataGridHierarchicalExpanderColumn wrapper && !ReferenceEquals(wrapper.InnerColumn, inner))
                    throw new InvalidOperationException("The grid source or sorted column changed during comparison.");
            }
            Comparison<object> comparison = (left, right) =>
            {
                VerifyOwner();
                if (custom is not null)
                {
                    // Source.Sort reverses arguments for descending. Cancel that reversal for a final-order comparer.
                    var result = descendingComparison ? custom(right, left) : custom(left, right);
                    VerifyOwner();
                    return result;
                }
                var leftValue = column.ReadValue(left);
                VerifyOwner();
                var rightValue = column.ReadValue(right);
                VerifyOwner();
                var compared = Comparer.Default.Compare(leftValue, rightValue);
                VerifyOwner();
                return compared;
            };
            // The source retains the comparer for later refreshes; release the temporary grid guard.
            try { source.Sort(columnId, comparison, direction); }
            finally { activeGrid = null; }
        }
        InvalidateHeaders();
        return true;
    }

    internal void ToggleColumnSort(TreeDataGridColumn column)
    {
        if (!CanUserSortColumns || !column.CanUserSort || Source is not { } source || !Columns.Contains(column)) return;
        var triState = column.AllowTriStateSorting ?? column.EffectiveColumn.AllowTriStateSorting ?? IsThreeStateSort;
        var direction = source.SortColumn != column.Id ? TreeDataGridSortDirection.Ascending : source.SortDirection switch
        {
            TreeDataGridSortDirection.Ascending => TreeDataGridSortDirection.Descending,
            TreeDataGridSortDirection.Descending when triState => TreeDataGridSortDirection.None,
            _ => TreeDataGridSortDirection.Ascending,
        };
        SortByColumn(column.Id, direction);
    }

    private void OnSortingPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == CanUserSortColumnsProperty || change.Property == IsThreeStateSortProperty)
            InvalidateHeaders();
    }

    partial void TryCommitBeforeSort(ref bool canSort);
}
