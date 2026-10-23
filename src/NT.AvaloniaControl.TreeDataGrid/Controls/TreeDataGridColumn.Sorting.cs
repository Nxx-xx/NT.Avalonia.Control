namespace NT.AvaloniaControl.TreeDataGrid;

public abstract partial class TreeDataGridColumn
{
    private bool _canUserSort = true;
    private Comparison<object>? _comparison;

    /// <summary>Enables user sorting for this column when the grid also allows sorting.</summary>
    public bool CanUserSort { get => _canUserSort; set => Set(ref _canUserSort, value); }

    /// <summary>Optional comparison of model objects. Otherwise the column's displayed values are compared.</summary>
    public Comparison<object>? Comparison { get => _comparison; set => Set(ref _comparison, value); }
}
