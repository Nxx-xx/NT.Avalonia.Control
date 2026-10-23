using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class TemplateInvalidationTests
{
    [AvaloniaFact]
    public void Column_request_template_assignment_rejects_stale_move_before_layout_applies_it()
    {
        using var source = new FlatTreeDataGridSource<Item>([new(), new()]);
        var grid = Create(source); var columns = grid.Columns.ToArray();
        var window = Show(grid);
        try
        {
            grid.ColumnReordering += (_, _) => grid.Template = Template();
            Assert.False(grid.TryReorderColumn(columns[0].Id, 1));
            Assert.Equal(columns, grid.Columns);
            window.UpdateLayout();
            Assert.Equal(columns, grid.VisibleColumns);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Row_request_template_assignment_rejects_stale_move_before_layout_applies_it()
    {
        var items = new ObservableCollection<Item>([new(), new()]);
        using var source = new FlatTreeDataGridSource<Item>(items);
        var grid = Create(source); var window = Show(grid);
        try
        {
            var ids = source.Rows.Select(row => row.Id).ToArray();
            grid.RowMoveRequested += (_, _) => grid.Template = Template();
            Assert.Equal(TreeDataGridRowMoveStatus.Stale, grid.TryMoveRow(ids[0], ids[1], TreeDataGridRowDropPosition.After).Status);
            Assert.Equal(ids, source.Rows.Select(row => row.Id));
        }
        finally { window.Close(); }
    }

    private static GridControl Create(TreeDataGridSource source)
    {
        var grid = new GridControl { Source = source, Template = Template(), CanUserReorderColumns = true, CanUserMoveRows = true };
        grid.Columns.Add(new TreeDataGridTextColumn()); grid.Columns.Add(new TreeDataGridTextColumn()); return grid;
    }
    private static FuncControlTemplate<GridControl> Template() => new((_, scope) =>
    {
        var rows = new TreeDataGridRowsPresenter(); var scroll = new ScrollViewer { Content = rows };
        scope.Register("PART_RowsPresenter", rows); scope.Register("PART_ScrollViewer", scroll); return scroll;
    });
    private static Window Show(Control content) { var window = new Window { Width = 400, Height = 240, Content = content }; window.Show(); window.UpdateLayout(); return window; }
    private sealed class Item;
}
