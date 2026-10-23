using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class RowMoveInteractionTests
{
    [AvaloniaFact]
    public void Move_commits_once_preserves_same_parent_identity_and_restores_current()
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items);
        var grid = Create(source); var writes = 0;
        var column = new TreeDataGridTextColumn { ValueGetter = model => ((Item)model).Name, ValueSetter = (model, value) => { writes++; ((Item)model).Name = (string)value!; } };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            var moving = source.Rows[0].Id; var target = source.Rows[2].Id;
            grid.State!.SetCurrentCell(new(moving, column.Id)); grid.State.Select(moving);
            Assert.True(grid.BeginEdit(new(moving, column.Id))); grid.State.EditSession!.Value = "committed";
            var result = grid.TryMoveRow(moving, target, TreeDataGridRowDropPosition.After);
            Assert.True(result.IsCommitted); Assert.Equal(moving, result.NodeId); Assert.Equal(1, writes);
            Assert.Equal("committed", items[^1].Name); Assert.Equal(moving, source.Rows[^1].Id);
            Assert.Equal(new CellAddress(moving, column.Id), grid.State.CurrentCell); Assert.Contains(moving, grid.State.SelectedNodes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Invalid_edit_refuses_move_before_request_or_model_change()
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items);
        var grid = Create(source); var calls = 0;
        var column = new TreeDataGridTextColumn { ValueGetter = model => ((Item)model).Name, ValueSetter = (_, _) => calls++, ValueValidator = _ => "blocked" };
        grid.Columns.Add(column); grid.RowMoveRequested += (_, _) => calls++;
        var window = Show(grid);
        try
        {
            var ids = source.Rows.Select(row => row.Id).ToArray();
            Assert.True(grid.BeginEdit(new(ids[0], column.Id))); var edit = grid.State!.EditSession;
            var result = grid.TryMoveRow(ids[0], ids[2], TreeDataGridRowDropPosition.After);
            Assert.Equal(TreeDataGridRowMoveStatus.ValidationFailed, result.Status); Assert.Equal(0, calls);
            Assert.Same(edit, grid.State.EditSession); Assert.Equal(ids, source.Rows.Select(row => row.Id));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Commit_callback_replacing_source_never_moves_the_old_collection()
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items);
        using var replacement = new FlatTreeDataGridSource<Item>(Items()); var grid = Create(source);
        var column = new TreeDataGridTextColumn { ValueGetter = model => ((Item)model).Name, ValueSetter = (model, value) => { ((Item)model).Name = (string)value!; grid.Source = replacement; } };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            var first = items[0]; var ids = source.Rows.Select(row => row.Id).ToArray();
            grid.BeginEdit(new(ids[0], column.Id)); grid.State!.EditSession!.Value = "written";
            Assert.Equal(TreeDataGridRowMoveStatus.Stale, grid.TryMoveRow(ids[0], ids[2], TreeDataGridRowDropPosition.After).Status);
            Assert.Same(first, items[0]); Assert.Equal("written", first.Name); Assert.Same(replacement, grid.Source);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Request_cancel_reentrancy_and_source_mutation_are_checked_before_default_move()
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(source);
        grid.Columns.Add(new TreeDataGridTextColumn()); var window = Show(grid);
        try
        {
            var ids = source.Rows.Select(row => row.Id).ToArray();
            EventHandler<TreeDataGridRowMoveRequestedEventArgs> cancel = (_, args) =>
            {
                Assert.Equal(TreeDataGridRowMoveStatus.Rejected, grid.TryMoveRow(ids[0], ids[2], TreeDataGridRowDropPosition.After).Status);
                args.Cancel = true;
            };
            grid.RowMoveRequested += cancel;
            Assert.Equal(TreeDataGridRowMoveStatus.Cancelled, grid.TryMoveRow(ids[0], ids[2], TreeDataGridRowDropPosition.After).Status);
            Assert.Equal(ids, source.Rows.Select(row => row.Id)); grid.RowMoveRequested -= cancel;
            grid.RowMoveRequested += (_, _) => items.RemoveAt(1);
            Assert.Equal(TreeDataGridRowMoveStatus.Stale, grid.TryMoveRow(ids[0], ids[2], TreeDataGridRowDropPosition.After).Status);
            Assert.Equal(ids[0], source.Rows[0].Id);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Sorted_default_is_rejected_but_explicit_application_transaction_can_handle_request()
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(source);
        var column = new TreeDataGridTextColumn(); grid.Columns.Add(column); var window = Show(grid);
        try
        {
            var moving = source.Rows[0].Id; var target = source.Rows[2].Id;
            source.Sort(column.Id, (left, right) => string.CompareOrdinal(((Item)left).Name, ((Item)right).Name), TreeDataGridSortDirection.Ascending);
            Assert.Equal(TreeDataGridRowMoveStatus.Rejected, grid.TryMoveRow(moving, target, TreeDataGridRowDropPosition.After).Status);
            grid.RowMoveRequested += (_, args) => { items.Move(0, 2); args.Handled = true; args.Result = new(TreeDataGridRowMoveStatus.Committed, moving); };
            Assert.True(grid.TryMoveRow(moving, target, TreeDataGridRowDropPosition.After).IsCommitted);
            Assert.Equal("a", items[^1].Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Pointer_drag_from_row_header_moves_after_target_without_writing_other_cells()
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(source);
        grid.Columns.Add(new TreeDataGridRowHeaderColumn { Width = new GridLength(48) });
        grid.Columns.Add(new TreeDataGridTextColumn { ValueGetter = model => ((Item)model).Name });
        var window = Show(grid);
        try
        {
            var id = source.Rows[0].Id; var grip = Grip(grid, 0);
            var start = grip.TranslatePoint(new Point(20, 12), window)!.Value;
            var finish = Grip(grid, 2).TranslatePoint(new Point(20, 26), window)!.Value;
            window.MouseDown(start, MouseButton.Left); window.MouseMove(finish); window.MouseUp(finish, MouseButton.Left); window.UpdateLayout();
            Assert.Equal(id, source.Rows[^1].Id); Assert.Equal("a", items[^1].Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Escape_or_capture_loss_or_source_replacement_cancels_drag()
    {
        foreach (var cancel in new[] { "escape", "capture", "source" })
        {
            var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items);
            using var replacement = new FlatTreeDataGridSource<Item>(Items()); var grid = Create(source);
            grid.Columns.Add(new TreeDataGridRowHeaderColumn()); var window = Show(grid);
            try
            {
                var grip = Grip(grid, 0); var start = grip.TranslatePoint(new Point(20, 12), window)!.Value;
                var finish = Grip(grid, 2).TranslatePoint(new Point(20, 26), window)!.Value;
                window.MouseDown(start, MouseButton.Left); window.MouseMove(finish);
                if (cancel == "escape") { window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.None, null); window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.None, null); }
                else if (cancel == "source") grid.Source = replacement;
                else
                {
                    // Capture loss through a real pointer event observed from the input pipeline.
                    IPointer? pointer = null;
                    grid.AddHandler(InputElement.PointerMovedEvent, (_, e) => pointer = e.Pointer, handledEventsToo: true);
                    window.MouseMove(finish + new Vector(1, 0)); pointer!.Capture(null);
                }
                window.MouseUp(finish, MouseButton.Left); Assert.Equal(new[] { "a", "b", "c" }, items.Select(item => item.Name));
            }
            finally { window.Close(); }
        }
    }

    private static ObservableCollection<Item> Items() => new([new("a"), new("b"), new("c")]);
    private static GridControl Create(TreeDataGridSource source) => new() { Source = source, CanUserMoveRows = true, Template = new FuncControlTemplate<GridControl>((_, scope) =>
    {
        var rows = new TreeDataGridRowsPresenter(); var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        scope.Register("PART_RowsPresenter", rows); scope.Register("PART_ScrollViewer", scroll); return scroll;
    }) };
    private static TreeDataGridCellPresenter Grip(GridControl grid, int row) => grid.RowsPresenter!.RealizedRows.Single(item => item.RowIndex == row).RealizedCells.Single(cell => cell.Column is TreeDataGridRowHeaderColumn);
    private static Window Show(Control content) { var window = new Window { Width = 400, Height = 240, Content = content }; window.Show(); window.UpdateLayout(); return window; }
    private sealed class Item(string name) { public string Name { get; set; } = name; }
}
