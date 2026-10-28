using Avalonia;
using Avalonia.Controls.Templates;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Specifies which grid lines the default cell theme displays.</summary>
public enum TreeDataGridGridLinesVisibility
{
    None,
    Horizontal,
    Vertical,
    All,
}

public partial class TreeDataGrid
{
    public static readonly StyledProperty<bool> ShowColumnHeadersProperty =
        AvaloniaProperty.Register<TreeDataGrid, bool>(nameof(ShowColumnHeaders), true);
    public static readonly StyledProperty<TreeDataGridGridLinesVisibility> GridLinesVisibilityProperty =
        AvaloniaProperty.Register<TreeDataGrid, TreeDataGridGridLinesVisibility>(nameof(GridLinesVisibility), TreeDataGridGridLinesVisibility.All,
            validate: value => Enum.IsDefined(value));
    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<TreeDataGrid, object?>(nameof(EmptyContent), "暂无数据");
    public static readonly StyledProperty<IDataTemplate?> EmptyContentTemplateProperty =
        AvaloniaProperty.Register<TreeDataGrid, IDataTemplate?>(nameof(EmptyContentTemplate));

    static TreeDataGrid()
    {
        GridLinesVisibilityProperty.Changed.AddClassHandler<TreeDataGrid>((grid, _) => grid.UpdateGridLines());
        ShowColumnHeadersProperty.Changed.AddClassHandler<TreeDataGrid>((grid, _) => grid.UpdateColumnHeaderVisibility());
    }

    /// <summary>Shows the column header region. Custom templates should bind the visibility of their header region to this property.</summary>
    public bool ShowColumnHeaders { get => GetValue(ShowColumnHeadersProperty); set => SetValue(ShowColumnHeadersProperty, value); }

    private void UpdateColumnHeaderVisibility()
    {
        ColumnOperationVersion++;
        CancelColumnReorder();
        _headers?.RefreshColumns();
        NotifyAutomationStateChanged();
    }

    /// <summary>Controls cell grid lines through theme pseudo classes; custom cell themes may choose their own appearance.</summary>
    public TreeDataGridGridLinesVisibility GridLinesVisibility
    {
        get => GetValue(GridLinesVisibilityProperty);
        set => SetValue(GridLinesVisibilityProperty, value);
    }

    /// <summary>Content shown when the source has no visible rows, including when a filter hides all rows.</summary>
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }

    /// <summary>Optional template used to display <see cref="EmptyContent"/>. Empty state does not represent loading state.</summary>
    public IDataTemplate? EmptyContentTemplate { get => GetValue(EmptyContentTemplateProperty); set => SetValue(EmptyContentTemplateProperty, value); }

    private void UpdateGridLines()
    {
        if (RowsPresenter is not { } presenter) return;
        foreach (var row in presenter.RealizedRows)
            foreach (var cell in row.RealizedCells)
                cell.ApplyGridLines();
        // Borders can change natural cell widths/heights. Reset the monotonic auto-size
        // caches so removing lines can shrink the geometry as well as adding them grows it.
        presenter.Refresh(resetMeasurements: true);
    }
}
