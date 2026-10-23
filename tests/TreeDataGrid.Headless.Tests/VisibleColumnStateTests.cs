using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class VisibleColumnStateTests
{
    [AvaloniaFact]
    public void Hiding_edit_column_cancels_without_write_and_moves_current_right_then_left()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var grid = Create(source); var writes = 0;
        var columns = Enumerable.Range(0, 3).Select(_ => new TreeDataGridTextColumn
        { ValueGetter = model => ((Item)model).Name, ValueSetter = (_, _) => writes++ }).ToArray();
        foreach (var column in columns) grid.Columns.Add(column);
        var window = Show(grid);
        try
        {
            var id = source.Rows[0].Id;
            grid.State!.SetCurrentCell(new(id, columns[1].Id)); grid.State.Select(id);
            Assert.True(grid.BeginEdit(new(id, columns[1].Id)));
            grid.State.EditSession!.Value = "draft";
            columns[1].IsVisible = false;
            Assert.Null(grid.State.EditSession); Assert.Equal(0, writes);
            Assert.Equal(new CellAddress(id, columns[2].Id), grid.State.CurrentCell);
            columns[2].IsVisible = false;
            Assert.Equal(new CellAddress(id, columns[0].Id), grid.State.CurrentCell);
            Assert.Contains(id, grid.State.SelectedNodes);
            Assert.False(grid.BeginEdit(new(id, columns[1].Id)));
            Assert.Equal(3, grid.Columns.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void All_hidden_restores_last_current_row_at_first_visible_column_without_rebuilding_identity()
    {
        using var source = new FlatTreeDataGridSource<Item>([new(), new()]);
        var grid = Create(source); var first = new TreeDataGridTextColumn(); var second = new TreeDataGridTextColumn();
        grid.Columns.Add(first); grid.Columns.Add(second); var window = Show(grid);
        try
        {
            var id = source.Rows[1].Id; grid.State!.SetCurrentCell(new(id, second.Id));
            first.IsVisible = second.IsVisible = false;
            Assert.Null(grid.State.CurrentCell); Assert.Empty(grid.VisibleColumns);
            first.IsVisible = true;
            Assert.Equal(new CellAddress(id, first.Id), grid.State.CurrentCell);
            Assert.Same(first, grid.VisibleColumns[0]); Assert.Equal(-1, grid.GetColumnIndex(second.Id));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Visible_cell_range_converges_and_keyboard_and_search_skip_hidden_columns()
    {
        using var source = new FlatTreeDataGridSource<Item>([new(), new()]);
        var grid = Create(source); grid.SelectionUnit = TreeDataGridSelectionUnit.Cell;
        grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        var columns = Enumerable.Range(0, 3).Select(i => new TreeDataGridTextColumn { ValueGetter = _ => i == 1 ? "hidden" : "shown" }).ToArray();
        foreach (var column in columns) grid.Columns.Add(column);
        var window = Show(grid);
        try
        {
            grid.State!.SelectCellRange(new(source.Rows[0].Id, columns[0].Id), new(source.Rows[1].Id, columns[2].Id), columns.Select(c => c.Id).ToArray());
            columns[1].IsVisible = false;
            Assert.Equal(4, grid.State.SelectedCells.Count());
            Assert.DoesNotContain(grid.State.SelectedCells, cell => cell.Column == columns[1].Id);
            Assert.Null(grid.SearchText("hidden"));
            grid.State.SetCurrentCell(new(source.Rows[0].Id, columns[0].Id)); grid.Focus();
            Key(window, Avalonia.Input.Key.Right);
            Assert.Equal(columns[2].Id, grid.State.CurrentCell!.Value.Column);
            Key(window, Avalonia.Input.Key.A, RawInputModifiers.Control);
            Assert.Equal(4, grid.State.SelectedCells.Count());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Column_move_preserves_draft_and_identity_while_discontinuous_range_converges()
    {
        using var source = new FlatTreeDataGridSource<Item>([new(), new()]);
        var grid = Create(source); grid.SelectionUnit = TreeDataGridSelectionUnit.Cell;
        grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        var columns = Enumerable.Range(0, 3).Select(_ => new TreeDataGridTextColumn { ValueGetter = model => ((Item)model).Name, ValueSetter = (_, _) => { } }).ToArray();
        foreach (var column in columns) grid.Columns.Add(column);
        var window = Show(grid);
        try
        {
            grid.State!.SelectCellRange(new(source.Rows[0].Id, columns[0].Id), new(source.Rows[1].Id, columns[1].Id), columns.Select(c => c.Id).ToArray());
            Assert.True(grid.BeginEdit(new(source.Rows[1].Id, columns[1].Id))); var edit = grid.State.EditSession;
            edit!.Value = "kept"; grid.Columns.Move(0, 2);
            Assert.Same(edit, grid.State.EditSession); Assert.Equal("kept", edit.Value);
            Assert.Equal(new CellAddress(source.Rows[1].Id, columns[1].Id), grid.State.CurrentCell);
            Assert.Single(grid.State.SelectedCells);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Validator_hiding_its_column_cancels_before_the_writer()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var grid = Create(source); var writes = 0;
        var column = new TreeDataGridTextColumn { ValueGetter = model => ((Item)model).Name, ValueSetter = (_, _) => writes++ };
        column.ValueValidator = _ => { column.IsVisible = false; return null; };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, column.Id)));
            Assert.False(grid.CommitEdit()); Assert.Equal(0, writes); Assert.Null(grid.State!.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Visibility_changes_while_detached_are_reconciled_on_state_access()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var grid = Create(source); var column = new TreeDataGridTextColumn { ValueGetter = model => ((Item)model).Name, ValueSetter = (_, _) => { } };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            var address = new CellAddress(source.Rows[0].Id, column.Id);
            grid.State!.SetCurrentCell(address); Assert.True(grid.BeginEdit(address));
            window.Content = null; column.IsVisible = false;
            Assert.Null(grid.State.EditSession); Assert.Null(grid.State.CurrentCell);
            column.IsVisible = true; window.Content = grid; window.UpdateLayout();
            Assert.Equal(new CellAddress(source.Rows[0].Id, column.Id), grid.State.CurrentCell);
        }
        finally { window.Close(); }
    }

    private static GridControl Create(TreeDataGridSource source) => new() { Source = source, Template = new FuncControlTemplate<GridControl>((_, scope) =>
    {
        var rows = new TreeDataGridRowsPresenter(); var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        scope.Register("PART_RowsPresenter", rows); scope.Register("PART_ScrollViewer", scroll); return scroll;
    }) };
    private static Window Show(Control content) { var window = new Window { Width = 400, Height = 240, Content = content }; window.Show(); window.UpdateLayout(); return window; }
    private static void Key(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None) { window.KeyPress(key, modifiers, PhysicalKey.None, null); window.KeyRelease(key, modifiers, PhysicalKey.None, null); window.UpdateLayout(); }
    private sealed class Item { public string Name { get; set; } = "original"; }
}
