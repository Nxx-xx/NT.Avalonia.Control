using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Themes.Tests;

public sealed class BatchRowMoveThemeTests
{
    [AvaloniaFact]
    public void Default_theme_whole_row_batch_keeps_identity_and_selection_after_variant_and_header_changes()
    {
        var items = new TreeDataGridRowMoveCollection<string> { "a", "b", "c", "d" };
        using var source = new FlatTreeDataGridSource<string>(items);
        var grid = new GridControl { Source = source, CanUserMoveRows = true, RowDragHandle = TreeDataGridRowDragHandle.WholeRow,
            SelectionMode = TreeDataGridSelectionMode.Multiple, SelectionUnit = TreeDataGridSelectionUnit.Row };
        grid.Columns.Add(new TreeDataGridTextColumn { ValueGetter = value => value, Width = new GridLength(240) });
        var window = new Window { Content = grid, Width = 420, Height = 280 }; window.Show(); window.UpdateLayout();
        try
        {
            var first = source.Rows[0].Id; var second = source.Rows[1].Id;
            grid.State!.Select(first); grid.State.Select(second, true);
            window.RequestedThemeVariant = ThemeVariant.Dark; grid.ShowColumnHeaders = false; window.UpdateLayout();
            window.RequestedThemeVariant = ThemeVariant.Light; window.UpdateLayout();
            var cells = grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().OrderBy(cell => source.GetViewIndex(cell.Row!.Id)).ToArray();
            var start = cells[0].TranslatePoint(new Point(30, 12), window)!.Value;
            var end = cells[^1].TranslatePoint(new Point(30, 26), window)!.Value;
            window.MouseDown(start, MouseButton.Left); window.MouseMove(end); window.MouseUp(end, MouseButton.Left); window.UpdateLayout();
            Assert.Equal(new[] { "c", "d", "a", "b" }, items); Assert.Equal(first, source.Rows[2].Id);
            Assert.Contains(first, grid.State.SelectedNodes); Assert.Contains(second, grid.State.SelectedNodes);
        }
        finally { window.Close(); }
    }
}
