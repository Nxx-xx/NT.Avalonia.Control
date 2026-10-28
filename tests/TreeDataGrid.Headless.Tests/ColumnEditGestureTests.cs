using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class ColumnEditGestureTests
{
    [AvaloniaFact]
    public void Column_overrides_global_gestures_and_explicit_none_keeps_programmatic_editing_available()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("row")]);
        var enabled = Text(); enabled.BeginEditGestures = TreeDataGridBeginEditGestures.F2;
        var disabled = Text(); disabled.BeginEditGestures = TreeDataGridBeginEditGestures.None;
        var grid = A1TestHost.Create(source, enabled, disabled);
        grid.EditTriggers = TreeDataGridEditTriggers.None;
        var window = A1TestHost.Show(grid);
        try
        {
            A1TestHost.Click(window, A1TestHost.Cell(grid));
            Assert.Null(grid.State!.EditSession);
            grid.Focus(); A1TestHost.Key(window, Key.F2);
            Assert.Equal(enabled.Id, grid.State.EditSession!.Cell.Column);
            grid.CancelEdit();
            grid.EditTriggers = TreeDataGridEditTriggers.SingleClick | TreeDataGridEditTriggers.F2;
            A1TestHost.Click(window, A1TestHost.Cell(grid, column: 1));
            A1TestHost.Key(window, Key.F2);
            Assert.Null(grid.State.EditSession);
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, disabled.Id)));
            grid.CancelEdit();
            enabled.BeginEditGestures = null;
            A1TestHost.Click(window, A1TestHost.Cell(grid));
            Assert.Equal(enabled.Id, grid.State.EditSession!.Cell.Column);
            Assert.Throws<ArgumentOutOfRangeException>(() => enabled.BeginEditGestures = (TreeDataGridBeginEditGestures)16);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(TreeDataGridSelectionUnit.Row)]
    [InlineData(TreeDataGridSelectionUnit.Cell)]
    public void WhenSelected_uses_the_selection_before_the_click_and_does_not_edit_on_programmatic_selection(TreeDataGridSelectionUnit unit)
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("first"), new("second")]);
        var column = Text(); column.BeginEditGestures = TreeDataGridBeginEditGestures.WhenSelected;
        var grid = A1TestHost.Create(source, column);
        grid.SelectionUnit = unit; grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        var window = A1TestHost.Show(grid);
        try
        {
            var address = new CellAddress(source.Rows[0].Id, column.Id);
            grid.State!.SetCurrentCell(address);
            A1TestHost.Click(window, A1TestHost.Cell(grid));
            Assert.Null(grid.State.EditSession);
            A1TestHost.Click(window, A1TestHost.Cell(grid));
            Assert.Equal(address, grid.State.EditSession!.Cell);
            grid.CancelEdit();
            if (unit == TreeDataGridSelectionUnit.Row)
            {
                A1TestHost.Click(window, A1TestHost.Cell(grid), RawInputModifiers.Control);
                Assert.Empty(grid.State.SelectedNodes); Assert.Null(grid.State.EditSession);
                A1TestHost.Click(window, A1TestHost.Cell(grid));
                Assert.Null(grid.State.EditSession);
            }
            grid.State.SetCurrentCell(new(source.Rows[1].Id, column.Id));
            if (unit == TreeDataGridSelectionUnit.Cell) grid.State.SelectCell(new(source.Rows[1].Id, column.Id));
            else grid.State.Select(source.Rows[1].Id);
            Assert.Null(grid.State.EditSession);
            column.IsReadOnly = true; window.UpdateLayout();
            A1TestHost.Click(window, A1TestHost.Cell(grid, row: 1));
            Assert.Null(grid.State.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DoubleTap_and_WhenSelected_combination_requires_the_double_pointer_gesture()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("row")]);
        var column = Text(); column.BeginEditGestures = TreeDataGridBeginEditGestures.DoubleTap | TreeDataGridBeginEditGestures.WhenSelected;
        var grid = A1TestHost.Create(source, column);
        grid.EditTriggers = TreeDataGridEditTriggers.SingleClick;
        var window = A1TestHost.Show(grid);
        try
        {
            A1TestHost.Click(window, A1TestHost.Cell(grid));
            Assert.Null(grid.State!.EditSession);
            A1TestHost.Click(window, A1TestHost.Cell(grid));
            Assert.NotNull(grid.State.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Expander_outer_inner_and_global_precedence_updates_without_recreating_an_active_editor()
    {
        using var source = new HierarchicalTreeDataGridSource<SearchOptionRow>([new("row")], row => row.Children);
        var inner = Text(); inner.BeginEditGestures = TreeDataGridBeginEditGestures.F2;
        var outer = new TreeDataGridHierarchicalExpanderColumn { Width = new GridLength(180), InnerColumn = inner, BeginEditGestures = TreeDataGridBeginEditGestures.None };
        var grid = A1TestHost.Create(source, outer);
        grid.EditTriggers = TreeDataGridEditTriggers.None;
        var window = A1TestHost.Show(grid);
        try
        {
            grid.State!.SetCurrentCell(new(source.Rows[0].Id, outer.Id));
            grid.Focus(); A1TestHost.Key(window, Key.F2);
            Assert.Null(grid.State.EditSession);
            outer.BeginEditGestures = null;
            A1TestHost.Key(window, Key.F2);
            var edit = grid.State.EditSession!;
            Assert.NotNull(edit);
            var editor = grid.GetVisualDescendants().OfType<TextBox>().Single();
            editor.Text = "draft";
            inner.BeginEditGestures = TreeDataGridBeginEditGestures.None;
            outer.BeginEditGestures = TreeDataGridBeginEditGestures.F2;
            inner.IsTextSearchEnabled = false;
            window.UpdateLayout();
            Assert.Same(edit, grid.State.EditSession);
            Assert.Same(editor, grid.GetVisualDescendants().OfType<TextBox>().Single());
            Assert.Equal("draft", edit.Value); Assert.Equal("row", ((SearchOptionRow)source.Rows[0].Model).Name);
            grid.CancelEdit(); grid.Focus(); A1TestHost.Key(window, Key.F2);
            Assert.NotNull(grid.State.EditSession);
            grid.CancelEdit(); outer.BeginEditGestures = null; inner.BeginEditGestures = null;
            grid.Focus(); A1TestHost.Key(window, Key.F2);
            Assert.Null(grid.State.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Failed_old_editor_validation_blocks_a_different_columns_tap_gesture_before_write()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("original")]);
        var writes = 0; var valid = false;
        var first = Text(); first.ValueValidator = _ => valid ? null : "blocked";
        first.ValueSetter = (row, value) => { writes++; ((SearchOptionRow)row).Name = (string)value!; };
        var second = Text(); second.BeginEditGestures = TreeDataGridBeginEditGestures.Tap;
        var grid = A1TestHost.Create(source, first, second);
        var window = A1TestHost.Show(grid);
        try
        {
            var address = new CellAddress(source.Rows[0].Id, first.Id);
            Assert.True(grid.BeginEdit(address));
            var edit = grid.State!.EditSession!; edit.Value = "draft";
            A1TestHost.Click(window, A1TestHost.Cell(grid, column: 1));
            Assert.Same(edit, grid.State.EditSession); Assert.Equal("blocked", edit.ValidationError);
            Assert.Equal(address, grid.State.CurrentCell); Assert.Equal(0, writes);
            valid = true;
            A1TestHost.Click(window, A1TestHost.Cell(grid, column: 1));
            Assert.Equal(second.Id, grid.State.EditSession!.Cell.Column);
            Assert.Equal(1, writes); Assert.Equal("draft", ((SearchOptionRow)source.Rows[0].Model).Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Selection_callback_replacing_the_inner_column_does_not_start_an_editor_for_the_new_field()
    {
        using var source = new HierarchicalTreeDataGridSource<SearchOptionRow>([new("row")], row => row.Children);
        var tree = new TreeDataGridHierarchicalExpanderColumn { Width = new GridLength(220), InnerColumn = Text() };
        var grid = A1TestHost.Create(source, tree);
        grid.EditTriggers = TreeDataGridEditTriggers.SingleClick;
        var window = A1TestHost.Show(grid);
        try
        {
            grid.State!.SelectionChanged += (_, _) => tree.InnerColumn = Text();
            var text = ((Grid)A1TestHost.Cell(grid).Content!).Children.OfType<TextBlock>().Single();
            A1TestHost.Click(window, text);
            Assert.Null(grid.State.EditSession);
            Assert.Equal("row", ((SearchOptionRow)source.Rows[0].Model).Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Template_input_keeps_its_click_and_explicit_none_disables_checkbox_space_without_changing_legacy_global_space()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("row")]);
        var clicks = 0; var toggles = 0; var value = false;
        var check = new TreeDataGridCheckBoxColumn { Width = new GridLength(100), ValueGetter = _ => value, ValueSetter = (_, next) => { toggles++; value = (bool)next!; } };
        var template = new TreeDataGridTemplateColumn
        {
            Width = new GridLength(150), BeginEditGestures = TreeDataGridBeginEditGestures.Tap,
            CellTemplate = new FuncDataTemplate<SearchOptionRow>((_, _) =>
            {
                var button = new Button { Content = "Run", MinHeight = 0 };
                button.Click += (_, _) => clicks++;
                return button;
            }),
        };
        var grid = A1TestHost.Create(source, check, template);
        grid.EditTriggers = TreeDataGridEditTriggers.None;
        var window = A1TestHost.Show(grid);
        try
        {
            grid.State!.SetCurrentCell(new(source.Rows[0].Id, check.Id));
            grid.Focus(); A1TestHost.Key(window, Key.Space);
            Assert.Equal(1, toggles);
            check.BeginEditGestures = TreeDataGridBeginEditGestures.None;
            grid.Focus(); A1TestHost.Key(window, Key.Space);
            Assert.Equal(1, toggles); Assert.Null(grid.State.EditSession);
            var button = A1TestHost.Cell(grid, column: 1).GetVisualDescendants().OfType<Button>().Single();
            A1TestHost.Click(window, button);
            Assert.Equal(1, clicks); Assert.Null(grid.State.EditSession);
            Assert.Equal(check.Id, grid.State.CurrentCell!.Value.Column);
        }
        finally { window.Close(); }
    }

    private static TreeDataGridTextColumn Text() => new()
    {
        Width = new GridLength(160), ValueGetter = row => ((SearchOptionRow)row).Name,
        ValueSetter = (row, value) => ((SearchOptionRow)row).Name = (string)value!,
    };
}
