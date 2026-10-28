using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

internal static class A1TestHost
{
    internal static GridControl Create(TreeDataGridSource source, params TreeDataGridColumn[] columns)
    {
        var grid = new GridControl { Source = source, Template = Template() };
        foreach (var column in columns) grid.Columns.Add(column);
        return grid;
    }

    internal static FuncControlTemplate<GridControl> Template() => new((_, scope) =>
    {
        var rows = new TreeDataGridRowsPresenter();
        var headers = new TreeDataGridColumnHeadersPresenter { Height = 28 };
        var region = new Border { Child = headers, [!Control.IsVisibleProperty] = new TemplateBinding(GridControl.ShowColumnHeadersProperty) };
        var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scope.Register("PART_RowsPresenter", rows);
        scope.Register("PART_ColumnHeadersPresenter", headers);
        scope.Register("PART_ScrollViewer", scroll);
        Grid.SetRow(scroll, 1);
        return new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { region, scroll } };
    });

    internal static Window Show(Control content, int width = 500, int height = 260)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show(); window.UpdateLayout();
        return window;
    }

    internal static TreeDataGridCellPresenter Cell(GridControl grid, int row = 0, int column = 0) =>
        grid.RowsPresenter!.RealizedRows.Single(item => item.RowIndex == row).RealizedCells.Single(cell => cell.ColumnIndex == column);

    internal static TreeDataGridColumnHeader Header(GridControl grid, int column = 0) =>
        grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Single(header => header.Column == grid.VisibleColumns[column]);

    internal static void Click(Window window, Control target, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var position = target.TranslatePoint(new Point(Math.Min(20, target.Bounds.Width / 2), target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(position, MouseButton.Left, modifiers);
        window.MouseUp(position, MouseButton.Left, modifiers);
        window.UpdateLayout();
    }

    internal static void Key(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        window.UpdateLayout();
    }
}
