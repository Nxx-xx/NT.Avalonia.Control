using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Input.TextInput;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class P4InteractionTests
{
    [AvaloniaFact]
    public void F2_conversion_validation_enter_and_escape_use_a_single_staged_transaction()
    {
        var item = new Item { Number = 12 }; using var source = new FlatTreeDataGridSource<Item>([item]);
        var grid = Create(source); var writes = 0;
        var column = new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Number, ValueSetter = (x, value) => { writes++; ((Item)x).Number = (int)value!; }, ValueType = typeof(int), ValueValidator = value => (int)value! < 0 ? "positive only" : null };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            grid.State!.SetCurrentCell(new(source.Rows[0].Id, column.Id)); grid.Focus(); PressKey(window, Key.F2);
            var editor = Editor(grid); editor.Text = "wrong"; PressKey(window, Key.Enter);
            Assert.Equal(12, item.Number); Assert.Equal(0, writes); Assert.NotNull(grid.State.EditSession!.ValidationError);
            editor.Text = "-1"; PressKey(window, Key.Enter); Assert.Equal("positive only", grid.State.EditSession.ValidationError);
            Assert.Contains(":invalid", Cell(grid).Classes);
            editor.Text = "25"; PressKey(window, Key.Enter); Assert.Equal(25, item.Number); Assert.Equal(1, writes); Assert.Null(grid.State.EditSession);
            PressKey(window, Key.F2); Editor(grid).Text = "100"; PressKey(window, Key.Escape); Assert.Equal(25, item.Number); Assert.Equal(1, writes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Binding_editor_writes_simple_path_only_on_commit_and_readonly_blocks_edits()
    {
        var item = new Item { Name = "original" }; using var source = new FlatTreeDataGridSource<Item>([item]);
        var grid = Create(source); var column = new TreeDataGridTextColumn { Binding = new ReflectionBinding(nameof(Item.Name)) { Mode = BindingMode.TwoWay } };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            var address = new CellAddress(source.Rows[0].Id, column.Id);
            Assert.True(grid.BeginEdit(address)); Editor(grid).Text = "draft"; Assert.Equal("original", item.Name);
            grid.CancelEdit(); Assert.Equal("original", item.Name);
            Assert.True(grid.BeginEdit(address)); Editor(grid).Text = "committed"; Assert.True(grid.CommitEdit()); Assert.Equal("committed", item.Name);
            grid.IsReadOnly = true; Assert.False(grid.BeginEdit(address)); grid.IsReadOnly = false;
            column.IsReadOnly = true; Assert.False(grid.BeginEdit(address));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Draft_survives_scroll_template_replacement_and_unload_then_cancels_if_hidden()
    {
        var items = Enumerable.Range(0, 200).Select(i => new Item { Name = "row " + i }).ToArray();
        using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(source);
        var column = new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Name, ValueSetter = (x, value) => ((Item)x).Name = (string)value! };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            var id = source.Rows[0].Id; Assert.True(grid.BeginEdit(new(id, column.Id))); Editor(grid).Text = "kept";
            var session = grid.State!.EditSession;
            grid.RowsPresenter!.Offset = new Vector(0, 3000); window.UpdateLayout(); Assert.Same(session, grid.State.EditSession);
            Assert.Empty(grid.GetVisualDescendants().OfType<TextBox>());
            grid.Template = Template(); grid.ApplyTemplate(); window.UpdateLayout();
            grid.RowsPresenter!.ScrollIntoView(id); window.UpdateLayout(); Assert.Equal("kept", Editor(grid).Text);
            window.Content = null; Assert.Same(session, grid.State.EditSession);
            window.Content = grid; window.UpdateLayout(); Assert.Equal("kept", Editor(grid).Text);
            window.Content = null; source.SetFilter(model => !ReferenceEquals(model, items[0]));
            window.Content = grid; window.UpdateLayout(); Assert.Null(grid.State.EditSession); Assert.Equal("row 0", items[0].Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Row_click_control_shift_and_cell_rectangle_keyboard_are_identity_based()
    {
        using var source = new FlatTreeDataGridSource<Item>(Enumerable.Range(0, 5).Select(i => new Item { Name = "row " + i }));
        var grid = Create(source); grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        grid.Columns.Add(new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Name });
        grid.Columns.Add(new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Name });
        var window = Show(grid);
        try
        {
            ClickCell(window, grid, 0, 0); ClickCell(window, grid, 2, 0, RawInputModifiers.Control); Assert.Equal(2, grid.State!.SelectedNodes.Count);
            ClickCell(window, grid, 4, 0, RawInputModifiers.Shift); Assert.Equal(5, grid.State.SelectedNodes.Count);
            grid.SelectionUnit = TreeDataGridSelectionUnit.Cell;
            ClickCell(window, grid, 0, 0); grid.Focus(); PressKey(window, Key.Right, RawInputModifiers.Shift); PressKey(window, Key.Down, RawInputModifiers.Shift);
            Assert.Equal(4, grid.State.SelectedCells.Count());
            Assert.All(grid.State.SelectedCells, cell => Assert.True(cell.Node == source.Rows[0].Id || cell.Node == source.Rows[1].Id));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Commit_hiding_the_last_row_is_safe_for_enter_and_tab()
    {
        foreach (var key in new[] { Key.Enter, Key.Tab })
        {
            var item = new Item { Name = "visible" }; using var source = new FlatTreeDataGridSource<Item>([item]);
            source.SetFilter(model => ((Item)model).Name == "visible");
            var grid = Create(source); var column = new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Name, ValueSetter = (x, value) => ((Item)x).Name = (string)value! };
            grid.Columns.Add(column); var window = Show(grid);
            try { grid.BeginEdit(new(source.Rows[0].Id, column.Id)); Editor(grid).Text = "hidden"; PressKey(window, key); Assert.Empty(source.Rows); Assert.Null(grid.State!.EditSession); }
            finally { window.Close(); }
        }
    }

    [AvaloniaFact]
    public void Active_preedit_does_not_commit_cancel_or_navigate_on_enter_escape_tab()
    {
        var item = new Item { Name = "before" }; using var source = new FlatTreeDataGridSource<Item>([item]);
        var grid = Create(source); var column = new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Name, ValueSetter = (x, value) => ((Item)x).Name = (string)value! };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            grid.BeginEdit(new(source.Rows[0].Id, column.Id)); var editor = Editor(grid); editor.Focus();
            var request = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
            editor.RaiseEvent(request); Assert.NotNull(request.Client);
            var session = grid.State!.EditSession;
            foreach (var key in new[] { Key.Enter, Key.Escape, Key.Tab })
            {
                request.Client.SetPreeditText("拼音", 2); PressKey(window, key);
                Assert.Same(session, grid.State.EditSession); Assert.Equal("before", item.Name);
            }
            request.Client.SetPreeditText(null); editor.Text = "中文"; grid.Focus(); PressKey(window, Key.Enter); Assert.Equal("中文", item.Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Text_search_supports_text_and_explicit_template_search_values()
    {
        using var source = new FlatTreeDataGridSource<Item>([new() { Name = "alpha" }, new() { Name = "中文节点" }, new() { Name = "template" }]);
        var grid = Create(source);
        var text = new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Name };
        var template = new TreeDataGridTemplateColumn { TextSearchValueSelector = x => ((Item)x).Name == "template" ? "badge" : "" };
        grid.Columns.Add(text); grid.Columns.Add(template); var window = Show(grid);
        try
        {
            Assert.Equal(source.Rows[1].Id, grid.SearchText("中文")!.Value.Node);
            Assert.Equal(new CellAddress(source.Rows[2].Id, template.Id), grid.SearchText("badge"));
            Assert.Null(grid.SearchText("missing"));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Programmatic_draft_updates_the_editor_without_changing_its_value_type()
    {
        var item = new Item { Number = 2 }; using var source = new FlatTreeDataGridSource<Item>([item]);
        var grid = Create(source); var column = new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Number, ValueSetter = (x, value) => ((Item)x).Number = (int)value!, ValueType = typeof(int) };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, column.Id)));
            grid.State!.EditSession!.Value = 42;
            Assert.Equal("42", Editor(grid).Text); Assert.IsType<int>(grid.State.EditSession.Value); Assert.Equal(2, item.Number);
            column.IsReadOnly = true; Assert.Null(grid.State.EditSession); Assert.Equal(2, item.Number);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Validator_changing_the_source_prevents_writing_the_released_owner()
    {
        var item = new Item { Name = "old" }; using var source = new FlatTreeDataGridSource<Item>([item]);
        using var replacement = new FlatTreeDataGridSource<Item>([new() { Name = "replacement" }]);
        var grid = Create(source); var writes = 0;
        var column = new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Name, ValueSetter = (x, value) => { writes++; ((Item)x).Name = (string)value!; }, ValueValidator = _ => { grid.Source = replacement; return null; } };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            grid.BeginEdit(new(source.Rows[0].Id, column.Id)); Editor(grid).Text = "bad write";
            Assert.False(grid.CommitEdit()); Assert.Equal(0, writes); Assert.Equal("old", item.Name); Assert.Null(grid.State!.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Editing_triggers_can_disable_F2_and_enable_single_click()
    {
        var item = new Item { Name = "click" }; using var source = new FlatTreeDataGridSource<Item>([item]);
        var grid = Create(source); var column = new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Name, ValueSetter = (x, value) => ((Item)x).Name = (string)value! };
        grid.Columns.Add(column); grid.EditTriggers = TreeDataGridEditTriggers.None; var window = Show(grid);
        try
        {
            ClickCell(window, grid, 0, 0); PressKey(window, Key.F2); Assert.Null(grid.State!.EditSession);
            grid.EditTriggers = TreeDataGridEditTriggers.SingleClick;
            ClickCell(window, grid, 0, 0); Assert.NotNull(grid.State.EditSession); Assert.Contains(":editing", Cell(grid).Classes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Validator_removing_columns_cancels_before_write_and_keeps_the_grid_usable()
    {
        var item = new Item { Name = "old" }; using var source = new FlatTreeDataGridSource<Item>([item]);
        var grid = Create(source); var writes = 0;
        var column = new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Name, ValueSetter = (_, _) => writes++, ValueValidator = _ => { grid.Columns.Clear(); return null; } };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            grid.BeginEdit(new(source.Rows[0].Id, column.Id)); Editor(grid).Text = "draft";
            Assert.False(grid.CommitEdit()); Assert.Equal(0, writes); Assert.Null(grid.State!.EditSession);
            window.UpdateLayout(); Assert.Equal(0, grid.RowsPresenter!.RealizedCellCount);
            grid.Columns.Add(new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Name }); window.UpdateLayout();
            Assert.Equal(1, grid.RowsPresenter.RealizedCellCount);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Successful_writer_can_unload_the_grid_without_losing_the_commit()
    {
        var item = new Item { Name = "old" }; using var source = new FlatTreeDataGridSource<Item>([item]);
        var grid = Create(source); Window? window = null;
        var column = new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Name, ValueSetter = (x, value) => { ((Item)x).Name = (string)value!; window!.Content = null; } };
        grid.Columns.Add(column); window = Show(grid);
        try
        {
            grid.BeginEdit(new(source.Rows[0].Id, column.Id)); Editor(grid).Text = "committed";
            Assert.True(grid.CommitEdit()); Assert.Equal("committed", item.Name); Assert.Null(grid.State!.EditSession);
            window.Content = grid; window.UpdateLayout(); Assert.Equal("committed", ((TextBlock)grid.RowsPresenter!.RealizedRows[0].RealizedCells[0].Content!).Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Reader_changing_the_source_does_not_create_a_released_edit_session()
    {
        using var source = new FlatTreeDataGridSource<Item>([new() { Name = "old" }]);
        using var replacement = new FlatTreeDataGridSource<Item>([new() { Name = "new" }]);
        var grid = Create(source); var armed = false;
        var column = new TreeDataGridTextColumn { ValueGetter = x => { if (armed) { armed = false; grid.Source = replacement; } return ((Item)x).Name; }, ValueSetter = (_, _) => { } };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            armed = true;
            Assert.False(grid.BeginEdit(new(source.Rows[0].Id, column.Id)));
            Assert.Same(replacement, grid.Source); Assert.Null(grid.State!.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Validation_error_notification_changing_source_prevents_the_previous_writer()
    {
        var item = new Item { Name = "old" }; using var source = new FlatTreeDataGridSource<Item>([item]);
        using var replacement = new FlatTreeDataGridSource<Item>([new() { Name = "new" }]);
        var grid = Create(source); var valid = false; var writes = 0;
        var column = new TreeDataGridTextColumn { ValueGetter = x => ((Item)x).Name, ValueSetter = (_, _) => writes++, ValueValidator = _ => valid ? null : "retry" };
        grid.Columns.Add(column); var window = Show(grid);
        try
        {
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, column.Id)));
            Assert.False(grid.CommitEdit());
            var session = grid.State!.EditSession!;
            session.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TreeDataGridEditSession.ValidationError)) grid.Source = replacement; };
            valid = true;
            Assert.False(grid.CommitEdit()); Assert.Equal(0, writes); Assert.Null(grid.State!.EditSession);
        }
        finally { window.Close(); }
    }

    private static GridControl Create(TreeDataGridSource source) => new() { Source = source, Template = Template() };
    private static FuncControlTemplate<GridControl> Template() => new((_, scope) =>
    {
        var rows = new TreeDataGridRowsPresenter(); var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scope.Register("PART_RowsPresenter", rows); scope.Register("PART_ScrollViewer", scroll); return scroll;
    });
    private static Window Show(Control content) { var window = new Window { Width = 400, Height = 240, Content = content }; window.Show(); window.UpdateLayout(); return window; }
    private static TreeDataGridCellPresenter Cell(GridControl grid) => grid.RowsPresenter!.RealizedRows.SelectMany(row => row.RealizedCells).First(cell => cell.Row!.Id == grid.State!.CurrentCell!.Value.Node && cell.Column!.Id == grid.State.CurrentCell.Value.Column);
    private static TextBox Editor(GridControl grid) { grid.UpdateLayout(); return grid.GetVisualDescendants().OfType<TextBox>().Single(); }
    private static void PressKey(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None) { window.KeyPress(key, modifiers, PhysicalKey.None, null); window.KeyRelease(key, modifiers, PhysicalKey.None, null); window.UpdateLayout(); }
    private static void ClickCell(Window window, GridControl grid, int row, int column, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var cell = grid.RowsPresenter!.RealizedRows.Single(item => item.RowIndex == row).RealizedCells.Single(item => item.ColumnIndex == column);
        var text = cell.GetVisualDescendants().OfType<TextBlock>().First();
        var point = text.TranslatePoint(new Point(3, 3), window)!.Value;
        window.MouseDown(point, MouseButton.Left, modifiers); window.MouseUp(point, MouseButton.Left, modifiers); window.UpdateLayout();
    }
    public sealed class Item { public string Name { get; set; } = ""; public int Number { get; set; } }
}

