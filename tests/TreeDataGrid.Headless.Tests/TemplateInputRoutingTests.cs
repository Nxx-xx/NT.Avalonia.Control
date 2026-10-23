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
using Avalonia.Layout;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class TemplateInputRoutingTests
{
    [AvaloniaFact]
    public void Display_button_keeps_pointer_focus_and_acts_on_its_row_after_sorting()
    {
        var alpha = new Item { Name = "alpha" };
        var beta = new Item { Name = "beta" };
        using var source = new FlatTreeDataGridSource<Item>([beta, alpha]);
        var grid = Create(source);
        grid.EditTriggers = TreeDataGridEditTriggers.SingleClick;
        var text = TextColumn();
        grid.Columns.Add(text);
        grid.Columns.Add(new TreeDataGridTemplateColumn
        {
            Width = new GridLength(220), ValueType = typeof(string), ValueSetter = (model, value) => ((Item)model).Name = (string)value!,
            CellTemplate = new FuncDataTemplate<Item>((item, _) =>
            {
                var button = new Button { Content = new TextBlock { Text = "Run " + item!.Name }, MinHeight = 0, Padding = new Thickness(4, 0) };
                button.Click += (_, _) => item!.Actions++;
                return button;
            })
        });
        var window = Show(grid);
        try
        {
            source.Sort(text.Id, (left, right) => string.CompareOrdinal(((Item)left).Name, ((Item)right).Name), TreeDataGridSortDirection.Ascending);
            window.UpdateLayout();
            var current = new CellAddress(source.Rows[0].Id, text.Id);
            grid.State!.SetCurrentCell(current);
            var button = Descendant<Button>(grid, 1, 1);
            // Hit the TextBlock inside the button, so input ownership must consider ancestors.
            Click(window, button.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == "Run beta"));
            Assert.Equal(1, beta.Actions);
            Assert.Equal(0, alpha.Actions);
            Assert.True(button.IsFocused);
            Assert.Equal(current, grid.State.CurrentCell);
            Assert.Null(grid.State.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Display_button_space_enter_and_unhandled_text_do_not_toggle_edit_navigate_or_search()
    {
        var alpha = new Item { Name = "alpha" };
        var beta = new Item { Name = "beta" };
        using var source = new FlatTreeDataGridSource<Item>([alpha, beta]);
        var grid = Create(source);
        var check = new TreeDataGridCheckBoxColumn { Width = new GridLength(80), ValueGetter = model => ((Item)model).Flag, ValueSetter = (model, value) => ((Item)model).Flag = (bool)value! };
        grid.Columns.Add(check);
        grid.Columns.Add(TextColumn());
        grid.Columns.Add(new TreeDataGridTemplateColumn
        {
            Width = new GridLength(220),
            CellTemplate = new FuncDataTemplate<Item>((item, _) =>
            {
                var button = new Button { Content = "Run", MinHeight = 0, Padding = default };
                button.Click += (_, _) => item!.Actions++;
                return button;
            })
        });
        var window = Show(grid);
        try
        {
            var current = new CellAddress(source.Rows[0].Id, check.Id);
            grid.State!.SetCurrentCell(current);
            var button = Descendant<Button>(grid, 1, 2);
            button.Focus();
            PressKey(window, Key.Space);
            PressKey(window, Key.Enter);
            Assert.Equal(2, beta.Actions);
            Assert.False(alpha.Flag);
            Assert.Equal(current, grid.State.CurrentCell);
            Assert.Null(grid.State.EditSession);
            string? observed = null;
            button.AddHandler(InputElement.TextInputEvent, (_, e) => observed = e.Text);
            window.KeyTextInput("b");
            Assert.Equal("b", observed);
            Assert.Equal(current, grid.State.CurrentCell);
            PressKey(window, Key.F2);
            Assert.Null(grid.State.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Display_button_leaves_another_cells_staged_edit_uncommitted()
    {
        var alpha = new Item { Name = "alpha" };
        var beta = new Item { Name = "beta" };
        using var source = new FlatTreeDataGridSource<Item>([alpha, beta]);
        var grid = Create(source);
        var text = TextColumn();
        text.ValueSetter = (model, value) => ((Item)model).Name = (string)value!;
        grid.Columns.Add(text);
        grid.Columns.Add(new TreeDataGridTemplateColumn
        {
            Width = new GridLength(220),
            CellTemplate = new FuncDataTemplate<Item>((item, _) =>
            {
                var button = new Button { Content = "Run", MinHeight = 0, Padding = default };
                button.Click += (_, _) => item!.Actions++;
                return button;
            })
        });
        var window = Show(grid);
        try
        {
            var address = new CellAddress(source.Rows[0].Id, text.Id);
            grid.State!.SetCurrentCell(address);
            Assert.True(grid.BeginEdit(address));
            var editor = Descendant<TextBox>(grid, 0, 0);
            editor.Text = "pending";
            var edit = grid.State.EditSession;
            var button = Descendant<Button>(grid, 1, 1);
            Click(window, button);
            Assert.Equal(1, beta.Actions);
            Assert.True(button.IsFocused);
            Assert.Same(edit, grid.State.EditSession);
            Assert.Equal("pending", grid.State.EditSession!.Value);
            Assert.Equal("alpha", alpha.Name);
            PressKey(window, Key.Enter);
            Assert.Equal(2, beta.Actions);
            Assert.Same(edit, grid.State.EditSession);
            Assert.Equal("alpha", alpha.Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Display_slider_uses_its_arrow_keys_without_moving_the_grid_current_cell()
    {
        using var source = new FlatTreeDataGridSource<Item>([new() { Name = "alpha" }, new() { Name = "beta" }]);
        var grid = Create(source);
        var text = TextColumn();
        grid.Columns.Add(text);
        grid.Columns.Add(new TreeDataGridTemplateColumn
        {
            Width = new GridLength(220),
            CellTemplate = new FuncDataTemplate<Item>((_, _) => new Slider { Minimum = 0, Maximum = 10, SmallChange = 1, Value = 5 })
        });
        var window = Show(grid);
        try
        {
            var current = new CellAddress(source.Rows[0].Id, text.Id);
            grid.State!.SetCurrentCell(current);
            var slider = Descendant<Slider>(grid, 0, 1);
            slider.Focus();
            PressKey(window, Key.Right);
            Assert.Equal(6, slider.Value);
            PressKey(window, Key.Left);
            Assert.Equal(5, slider.Value);
            Assert.Equal(current, grid.State.CurrentCell);
            Assert.True(slider.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Display_textbox_keeps_text_caret_and_native_tab_focus_navigation()
    {
        using var source = new FlatTreeDataGridSource<Item>([new() { Name = "alpha" }, new() { Name = "beta" }]);
        var grid = Create(source);
        var text = TextColumn();
        grid.Columns.Add(text);
        grid.Columns.Add(new TreeDataGridTemplateColumn
        {
            Width = new GridLength(260),
            CellTemplate = new FuncDataTemplate<Item>((_, _) => new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { new TextBox { Width = 150, MinHeight = 0, Padding = default }, new Button { Content = "Apply", MinHeight = 0, Padding = default } }
            })
        });
        var window = Show(grid);
        try
        {
            var current = new CellAddress(source.Rows[0].Id, text.Id);
            grid.State!.SetCurrentCell(current);
            var box = Descendant<TextBox>(grid, 0, 1);
            Click(window, box);
            Assert.True(box.IsFocused);
            window.KeyTextInput("beta");
            Assert.Equal("beta", box.Text);
            Assert.Equal(current, grid.State.CurrentCell);
            PressKey(window, Key.Left);
            Assert.Equal(3, box.CaretIndex);
            Assert.Equal(current, grid.State.CurrentCell);
            PressKey(window, Key.Tab);
            Assert.True(Descendant<Button>(grid, 0, 1).IsFocused);
            Assert.Equal(current, grid.State.CurrentCell);
            Assert.Null(grid.State.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Static_template_text_and_cell_padding_still_select_and_use_grid_keyboard_search()
    {
        using var source = new FlatTreeDataGridSource<Item>([new() { Name = "alpha" }, new() { Name = "beta" }]);
        var grid = Create(source);
        var template = new TreeDataGridTemplateColumn
        {
            Width = new GridLength(220), TextSearchValueSelector = model => ((Item)model).Name,
            CellTemplate = new FuncDataTemplate<Item>((item, _) => new TextBlock { Text = item!.Name })
        };
        grid.Columns.Add(template);
        var window = Show(grid);
        try
        {
            Click(window, Descendant<TextBlock>(grid, 0, 0));
            Assert.Equal(new CellAddress(source.Rows[0].Id, template.Id), grid.State!.CurrentCell);
            Assert.True(grid.IsFocused);
            PressKey(window, Key.Down);
            Assert.Equal(source.Rows[1].Id, grid.State.CurrentCell!.Value.Node);
            Click(window, Cell(grid, 0, 0), new Point(1, 1));
            Assert.Equal(source.Rows[0].Id, grid.State.CurrentCell!.Value.Node);
            window.KeyTextInput("b");
            Assert.Equal(source.Rows[1].Id, grid.State.CurrentCell!.Value.Node);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Expander_and_readonly_display_checkbox_keep_their_existing_grid_behavior()
    {
        var parent = new Item { Name = "parent", Children = [new() { Name = "child" }] };
        using var source = new HierarchicalTreeDataGridSource<Item>([parent], item => item.Children);
        var grid = Create(source);
        var tree = new TreeDataGridHierarchicalExpanderColumn { InnerColumn = TextColumn(), Width = new GridLength(220) };
        var check = new TreeDataGridCheckBoxColumn { Width = new GridLength(80), ValueGetter = model => ((Item)model).Flag };
        grid.Columns.Add(tree);
        grid.Columns.Add(check);
        var window = Show(grid);
        try
        {
            var expander = Cell(grid, 0, 0).GetVisualDescendants().OfType<Button>().Single();
            Click(window, expander);
            Assert.Equal(2, source.Rows.Count);
            Assert.Null(grid.State!.CurrentCell);
            Click(window, Cell(grid, 1, 0).GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == "child"));
            Assert.Equal(source.Rows[1].Id, grid.State.CurrentCell!.Value.Node);
            Click(window, Cell(grid, 0, 1));
            Assert.Equal(new CellAddress(source.Rows[0].Id, check.Id), grid.State.CurrentCell);
            Assert.True(grid.IsFocused);
            Assert.False(parent.Flag);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Custom_editing_template_retains_enter_escape_tab_and_composition_contract()
    {
        var alpha = new Item { Name = "alpha" };
        using var source = new FlatTreeDataGridSource<Item>([alpha, new() { Name = "beta" }]);
        var grid = Create(source);
        var template = new TreeDataGridTemplateColumn
        {
            Width = new GridLength(220), ValueGetter = model => ((Item)model).Name, ValueSetter = (model, value) => ((Item)model).Name = (string)value!,
            CellTemplate = new FuncDataTemplate<string>((value, _) => new TextBlock { Text = value }),
            CellEditingTemplate = new FuncDataTemplate<TreeDataGridEditSession>((_, _) =>
            {
                var box = new TextBox { MinHeight = 0, Padding = default };
                box.Bind(TextBox.TextProperty, new ReflectionBinding(nameof(TreeDataGridEditSession.Value)) { Mode = BindingMode.TwoWay });
                return box;
            })
        };
        grid.Columns.Add(template);
        var text = TextColumn();
        grid.Columns.Add(text);
        var window = Show(grid);
        try
        {
            var first = new CellAddress(source.Rows[0].Id, template.Id);
            grid.State!.SetCurrentCell(first);
            Assert.True(grid.BeginEdit(first));
            var box = Descendant<TextBox>(grid, 0, 0);
            box.Focus();
            box.Text = "committed";
            Assert.Equal("alpha", alpha.Name);
            var request = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
            box.RaiseEvent(request);
            Assert.NotNull(request.Client);
            var edit = grid.State.EditSession;
            foreach (var key in new[] { Key.Enter, Key.Escape, Key.Tab })
            {
                request.Client.SetPreeditText("拼音", 2);
                PressKey(window, key);
                Assert.Same(edit, grid.State.EditSession);
                Assert.Equal("alpha", alpha.Name);
            }
            request.Client.SetPreeditText(null);
            box.Focus();
            PressKey(window, Key.Enter);
            Assert.Equal("committed", alpha.Name);
            Assert.Null(grid.State.EditSession);
            Assert.Equal(source.Rows[1].Id, grid.State.CurrentCell!.Value.Node);

            grid.State.SetCurrentCell(first);
            Assert.True(grid.BeginEdit(first));
            box = Descendant<TextBox>(grid, 0, 0);
            box.Focus();
            box.Text = "cancelled";
            PressKey(window, Key.Escape);
            Assert.Equal("committed", alpha.Name);
            Assert.Null(grid.State.EditSession);

            Assert.True(grid.BeginEdit(first));
            box = Descendant<TextBox>(grid, 0, 0);
            box.Focus();
            box.Text = "tabbed";
            PressKey(window, Key.Tab);
            Assert.Equal("tabbed", alpha.Name);
            Assert.Null(grid.State.EditSession);
            Assert.Equal(new CellAddress(source.Rows[0].Id, text.Id), grid.State.CurrentCell);
        }
        finally { window.Close(); }
    }

    private static TreeDataGridTextColumn TextColumn() => new() { Width = new GridLength(120), ValueGetter = model => ((Item)model).Name };
    private static GridControl Create(TreeDataGridSource source) => new()
    {
        Source = source, RowHeight = 40,
        Template = new FuncControlTemplate<GridControl>((_, scope) =>
        {
            var rows = new TreeDataGridRowsPresenter();
            var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scope.Register("PART_RowsPresenter", rows);
            scope.Register("PART_ScrollViewer", scroll);
            return scroll;
        })
    };
    private static Window Show(Control content) { var window = new Window { Width = 620, Height = 240, Content = content }; window.Show(); window.UpdateLayout(); return window; }
    private static TreeDataGridCellPresenter Cell(GridControl grid, int row, int column) => grid.RowsPresenter!.RealizedRows.Single(item => item.RowIndex == row).RealizedCells.Single(item => item.ColumnIndex == column);
    private static T Descendant<T>(GridControl grid, int row, int column) where T : Control { grid.UpdateLayout(); return Cell(grid, row, column).GetVisualDescendants().OfType<T>().Single(); }
    private static void Click(Window window, Control control, Point? localPoint = null)
    {
        var point = control.TranslatePoint(localPoint ?? new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.UpdateLayout();
    }
    private static void PressKey(Window window, Key key) { window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null); window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null); window.UpdateLayout(); }
    public sealed class Item
    {
        public string Name { get; set; } = "";
        public bool Flag { get; set; }
        public int Actions { get; set; }
        public Item[] Children { get; set; } = [];
    }
}
