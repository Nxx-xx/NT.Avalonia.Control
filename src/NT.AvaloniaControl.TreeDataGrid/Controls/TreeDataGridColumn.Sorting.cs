namespace NT.AvaloniaControl.TreeDataGrid;

public abstract partial class TreeDataGridColumn
{
    private bool _canUserSort = true;
    private Comparison<object>? _comparison;
    private bool? _allowTriStateSorting;
    private Comparison<object>? _compareAscending;
    private Comparison<object>? _compareDescending;

    /// <summary>Enables user sorting for this column when the grid also allows sorting.</summary>
    public bool CanUserSort { get => _canUserSort; set => Set(ref _canUserSort, value); }

    /// <summary>Optional comparison of model objects. Otherwise the column's displayed values are compared.</summary>
    public Comparison<object>? Comparison { get => _comparison; set => Set(ref _comparison, value); }
    /// <summary>Overrides the inner column or grid sort cycle. Null inherits.</summary>
    public bool? AllowTriStateSorting { get => _allowTriStateSorting; set => Set(ref _allowTriStateSorting, value); }
    /// <summary>Ascending comparison of model objects, preferred to the legacy Comparison.</summary>
    public Comparison<object>? CompareAscending { get => _compareAscending; set => Set(ref _compareAscending, value); }
    /// <summary>Comparison that directly expresses the final descending order. Otherwise the ascending comparison is reversed.</summary>
    public Comparison<object>? CompareDescending { get => _compareDescending; set => Set(ref _compareDescending, value); }
}
