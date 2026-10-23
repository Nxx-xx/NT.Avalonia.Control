using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class TreeKeyboardNavigationTests
{
    [AvaloniaFact]
    public void Plain_arrows_expand_enter_collapse_and_return_to_the_source_parent()
    {
        var grandchild = new TreeNode("grandchild");
        var child = new TreeNode("child", grandchild);
        var root = new TreeNode("root", child, new TreeNode("sibling"));
        using var source = new HierarchicalTreeDataGridSource<TreeNode>([root], item => item.Children);
        var grid = Create(source);
        var tree = AddTreeColumn(grid);
        var window = Show(grid);
        try
        {
            var rootId = source.Rows[0].Id;
            SetCurrent(grid, rootId, tree.Id);
            PressKey(window, Key.Right);
            AssertCurrent(grid, rootId, tree.Id);
            Assert.True(source.Rows[0].IsExpanded);
            var childId = source.Rows[1].Id;
            PressKey(window, Key.Right);
            AssertCurrent(grid, childId, tree.Id);
            PressKey(window, Key.Right);
            AssertCurrent(grid, childId, tree.Id);
            Assert.True(source.Rows[1].IsExpanded);
            var grandchildId = source.Rows[2].Id;
            PressKey(window, Key.Right);
            AssertCurrent(grid, grandchildId, tree.Id);
            PressKey(window, Key.Right);
            AssertCurrent(grid, grandchildId, tree.Id); // A leaf keeps its current tree cell.
            PressKey(window, Key.Left);
            AssertCurrent(grid, childId, tree.Id);
            PressKey(window, Key.Left);
            AssertCurrent(grid, childId, tree.Id);
            Assert.False(source.Rows[1].IsExpanded);
            PressKey(window, Key.Left);
            AssertCurrent(grid, rootId, tree.Id);
            PressKey(window, Key.Left);
            AssertCurrent(grid, rootId, tree.Id);
            Assert.Single(source.Rows);
            PressKey(window, Key.Left);
            AssertCurrent(grid, rootId, tree.Id);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Root_leaf_arrows_do_not_enter_adjacent_root_or_another_column()
    {
        using var source = new HierarchicalTreeDataGridSource<TreeNode>([new("first"), new("second")], item => item.Children);
        var grid = Create(source);
        var tree = AddTreeColumn(grid);
        grid.Columns.Add(TextColumn());
        var window = Show(grid);
        try
        {
            var id = source.Rows[1].Id;
            SetCurrent(grid, id, tree.Id);
            PressKey(window, Key.Left);
            AssertCurrent(grid, id, tree.Id);
            PressKey(window, Key.Right);
            AssertCurrent(grid, id, tree.Id);
            Assert.All(source.Rows, row => Assert.False(row.IsExpanded));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Right_enters_the_first_sorted_visible_child_and_left_uses_its_source_path()
    {
        var rejected = new TreeNode("rejected");
        var zed = new TreeNode("zed");
        var alpha = new TreeNode("alpha");
        var root = new TreeNode("root", rejected, zed, alpha);
        using var source = new HierarchicalTreeDataGridSource<TreeNode>([root], item => item.Children);
        var grid = Create(source);
        var tree = AddTreeColumn(grid);
        var window = Show(grid);
        try
        {
            source.SetFilter(item => !ReferenceEquals(item, rejected));
            source.Sort(tree.Id, (left, right) => string.CompareOrdinal(((TreeNode)left).Name, ((TreeNode)right).Name), TreeDataGridSortDirection.Ascending);
            var rootId = source.Rows[0].Id;
            SetCurrent(grid, rootId, tree.Id);
            PressKey(window, Key.Right);
            Assert.Equal(new[] { root, alpha, zed }, source.Rows.Select(row => (TreeNode)row.Model));
            PressKey(window, Key.Right);
            AssertCurrent(grid, source.Rows[1].Id, tree.Id);
            Assert.Equal(new IndexPath(0, 2), source.Rows[1].ModelIndex);
            PressKey(window, Key.Left);
            AssertCurrent(grid, rootId, tree.Id);
            source.SetFilter(item => ReferenceEquals(item, root));
            PressKey(window, Key.Right);
            AssertCurrent(grid, rootId, tree.Id); // All children are filtered from the expanded node.
            Assert.Single(source.Rows);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Cancelled_expansion_keeps_current_node_and_projection_unchanged()
    {
        using var source = new HierarchicalTreeDataGridSource<TreeNode>([new("root", new TreeNode("child"))], item => item.Children);
        var grid = Create(source);
        var tree = AddTreeColumn(grid);
        var window = Show(grid);
        try
        {
            source.RowExpanding += (_, args) => args.Cancel = true;
            var id = source.Rows[0].Id;
            SetCurrent(grid, id, tree.Id);
            PressKey(window, Key.Right);
            AssertCurrent(grid, id, tree.Id);
            Assert.Single(source.Rows);
            Assert.False(source.Rows[0].IsExpanded);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Other_columns_and_modified_arrows_keep_grid_navigation_with_hidden_columns()
    {
        using var source = new HierarchicalTreeDataGridSource<TreeNode>([new("root", new TreeNode("child"))], item => item.Children);
        var grid = Create(source);
        grid.SelectionUnit = TreeDataGridSelectionUnit.Cell;
        grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        grid.Columns.Add(new TreeDataGridTextColumn { IsVisible = false });
        var tree = AddTreeColumn(grid);
        var detail = TextColumn();
        grid.Columns.Add(detail);
        var window = Show(grid);
        try
        {
            var id = source.Rows[0].Id;
            SetCurrent(grid, id, tree.Id);
            grid.State!.SelectCell(new(id, tree.Id));
            PressKey(window, Key.Right, RawInputModifiers.Shift);
            AssertCurrent(grid, id, detail.Id);
            Assert.Equal(2, grid.State.SelectedCells.Count());
            Assert.False(source.Rows[0].IsExpanded);
            PressKey(window, Key.Left);
            AssertCurrent(grid, id, tree.Id);
            Assert.False(source.Rows[0].IsExpanded);
            PressKey(window, Key.Right, RawInputModifiers.Control);
            AssertCurrent(grid, id, detail.Id);
            Assert.False(source.Rows[0].IsExpanded);
            PressKey(window, Key.Left, RawInputModifiers.Shift);
            AssertCurrent(grid, id, tree.Id);
            Assert.False(source.Rows[0].IsExpanded);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Tree_editor_arrows_and_preedit_keys_keep_the_draft_and_do_not_expand_or_page()
    {
        var root = new TreeNode("root", new TreeNode("child"));
        using var source = new HierarchicalTreeDataGridSource<TreeNode>([root, new("next")], item => item.Children);
        var grid = Create(source);
        var writes = 0;
        var tree = AddTreeColumn(grid);
        ((TreeDataGridTextColumn)tree.InnerColumn).ValueSetter = (item, value) => { writes++; ((TreeNode)item).Name = (string)value!; };
        var window = Show(grid);
        try
        {
            var id = source.Rows[0].Id;
            SetCurrent(grid, id, tree.Id);
            Assert.True(grid.BeginEdit(new(id, tree.Id)));
            window.UpdateLayout();
            var editor = grid.GetVisualDescendants().OfType<TextBox>().Single();
            editor.Text = "draft";
            editor.CaretIndex = 1;
            editor.Focus();
            var edit = grid.State!.EditSession;
            PressKey(window, Key.Right);
            Assert.Equal(2, editor.CaretIndex);
            PressKey(window, Key.Left);
            Assert.Equal(1, editor.CaretIndex);
            Assert.Same(edit, grid.State.EditSession);
            AssertCurrent(grid, id, tree.Id);
            Assert.False(source.Rows[0].IsExpanded);

            var request = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
            editor.RaiseEvent(request);
            Assert.NotNull(request.Client);
            foreach (var key in new[] { Key.Right, Key.Left, Key.PageDown, Key.PageUp, Key.Enter, Key.Escape, Key.Tab })
            {
                request.Client.SetPreeditText("拼音", 2);
                PressKey(window, key);
                Assert.Same(edit, grid.State.EditSession);
                AssertCurrent(grid, id, tree.Id);
                Assert.False(source.Rows[0].IsExpanded);
                Assert.Equal("root", root.Name);
                Assert.Equal(0, writes);
            }
            request.Client.SetPreeditText(null);
            grid.CancelEdit();
            grid.Focus();
            PressKey(window, Key.Right);
            Assert.True(source.Rows[0].IsExpanded);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Interactive_template_inside_tree_column_owns_arrows_and_buttons()
    {
        using var source = new HierarchicalTreeDataGridSource<TreeNode>([new("root", new TreeNode("child"))], item => item.Children);
        var grid = Create(source);
        var actions = 0;
        var tree = new TreeDataGridHierarchicalExpanderColumn
        {
            Width = new GridLength(300),
            InnerColumn = new TreeDataGridTemplateColumn
            {
                CellTemplate = new FuncDataTemplate<TreeNode>((_, _) =>
                {
                    var button = new Button { Content = "Apply", Padding = default, MinHeight = 0 };
                    button.Click += (_, _) => actions++;
                    return new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Children = { new TextBox { Width = 150, Text = "abc", Padding = default, MinHeight = 0 }, button } };
                })
            }
        };
        grid.Columns.Add(tree);
        var window = Show(grid);
        try
        {
            var id = source.Rows[0].Id;
            SetCurrent(grid, id, tree.Id);
            var box = grid.GetVisualDescendants().OfType<TextBox>().Single();
            box.Focus();
            box.CaretIndex = 3;
            PressKey(window, Key.Left);
            Assert.Equal(2, box.CaretIndex);
            PressKey(window, Key.Right);
            Assert.Equal(3, box.CaretIndex);
            AssertCurrent(grid, id, tree.Id);
            Assert.False(source.Rows[0].IsExpanded);
            var button = grid.GetVisualDescendants().OfType<Button>().Single(item => Equals(item.Content, "Apply"));
            button.Focus();
            PressKey(window, Key.Right);
            PressKey(window, Key.Left);
            PressKey(window, Key.Space);
            Assert.Equal(1, actions);
            AssertCurrent(grid, id, tree.Id);
            Assert.False(source.Rows[0].IsExpanded);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Page_keys_follow_mixed_row_coordinates_preserve_visible_column_and_clamp_at_edges()
    {
        using var source = new FlatTreeDataGridSource<TreeNode>(Enumerable.Range(0, 16).Select(index => new TreeNode(index.ToString())));
        var grid = Create(source);
        grid.RowHeightSelector = row => new[] { 20d, 100d, 40d, 80d }[(int)row.ModelIndex[0] % 4];
        grid.Columns.Add(new TreeDataGridTextColumn { IsVisible = false });
        grid.Columns.Add(TextColumn());
        var currentColumn = TextColumn();
        grid.Columns.Add(currentColumn);
        var window = Show(grid);
        try
        {
            Assert.Equal(180, grid.RowsPresenter!.Viewport.Height);
            SetCurrent(grid, source.Rows[0].Id, currentColumn.Id);
            PressKey(window, Key.PageDown);
            AssertCurrent(grid, source.Rows[3].Id, currentColumn.Id); // y=180 lies in [160,240).
            PressKey(window, Key.PageDown);
            AssertCurrent(grid, source.Rows[5].Id, currentColumn.Id); // y=340 lies in [260,360).
            PressKey(window, Key.PageUp);
            AssertCurrent(grid, source.Rows[1].Id, currentColumn.Id); // y=80 lies in [20,120).
            PressKey(window, Key.PageUp);
            AssertCurrent(grid, source.Rows[0].Id, currentColumn.Id);
            PressKey(window, Key.PageUp);
            AssertCurrent(grid, source.Rows[0].Id, currentColumn.Id);
            SetCurrent(grid, source.Rows[^1].Id, currentColumn.Id);
            PressKey(window, Key.PageDown);
            AssertCurrent(grid, source.Rows[^1].Id, currentColumn.Id);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PageDown_advances_at_least_one_row_when_the_current_row_is_taller_than_the_viewport()
    {
        using var source = new FlatTreeDataGridSource<TreeNode>(Enumerable.Range(0, 10).Select(index => new TreeNode(index.ToString())));
        var grid = Create(source);
        grid.RowHeightSelector = row => row.ModelIndex[0] == 0 ? 400 : 30;
        var column = TextColumn();
        grid.Columns.Add(column);
        var window = Show(grid);
        try
        {
            SetCurrent(grid, source.Rows[0].Id, column.Id);
            PressKey(window, Key.PageDown);
            AssertCurrent(grid, source.Rows[1].Id, column.Id);
            PressKey(window, Key.PageUp);
            AssertCurrent(grid, source.Rows[0].Id, column.Id);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Page_keys_use_measured_auto_heights_and_recompute_after_hiding_wrapped_column()
    {
        using var source = new FlatTreeDataGridSource<TreeNode>(Enumerable.Range(0, 20).Select(index => new TreeNode(index == 0 ? string.Join(' ', Enumerable.Repeat("wrap", 45)) : "short")));
        var grid = Create(source);
        grid.AutoRowHeight = true;
        var wrapped = TextColumn();
        wrapped.Width = new GridLength(80);
        wrapped.TextWrapping = TextWrapping.Wrap;
        grid.Columns.Add(wrapped);
        var currentColumn = new TreeDataGridTextColumn { Width = new GridLength(100), ValueGetter = _ => "short" };
        grid.Columns.Add(currentColumn);
        var window = Show(grid);
        try
        {
            Assert.True(grid.RowsPresenter!.Layout!.GetRowHeight(0) > grid.RowsPresenter.Viewport.Height);
            SetCurrent(grid, source.Rows[0].Id, currentColumn.Id);
            PressKey(window, Key.PageDown);
            AssertCurrent(grid, source.Rows[1].Id, currentColumn.Id);
            PressKey(window, Key.PageUp);
            AssertCurrent(grid, source.Rows[0].Id, currentColumn.Id);
            wrapped.IsVisible = false;
            window.UpdateLayout();
            Assert.Equal(28, grid.RowsPresenter.Layout!.GetRowHeight(0));
            PressKey(window, Key.PageDown);
            AssertCurrent(grid, source.Rows[6].Id, currentColumn.Id);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PageDown_uses_current_geometry_when_height_selector_changes_before_layout()
    {
        using var source = new FlatTreeDataGridSource<TreeNode>(Enumerable.Range(0, 16).Select(index => new TreeNode(index.ToString())));
        var grid = Create(source);
        var column = TextColumn();
        grid.Columns.Add(column);
        var window = Show(grid);
        try
        {
            SetCurrent(grid, source.Rows[0].Id, column.Id);
            grid.RowHeightSelector = row => new[] { 20d, 100d, 40d, 80d }[(int)row.ModelIndex[0] % 4];
            PressKey(window, Key.PageDown); // No intervening layout after setting the selector.
            AssertCurrent(grid, source.Rows[3].Id, column.Id);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Page_key_does_not_overwrite_replacement_sources_current_cell_from_a_height_callback()
    {
        using var source = new FlatTreeDataGridSource<TreeNode>([new("old"), new("old next")]);
        using var replacement = new FlatTreeDataGridSource<TreeNode>([new("replacement"), new("replacement current")]);
        var grid = Create(source);
        var column = TextColumn();
        grid.Columns.Add(column);
        var window = Show(grid);
        try
        {
            SetCurrent(grid, source.Rows[0].Id, column.Id);
            var changed = false;
            grid.RowHeightSelector = _ =>
            {
                if (!changed)
                {
                    changed = true;
                    grid.Source = replacement;
                    grid.State!.SetCurrentCell(new(replacement.Rows[1].Id, column.Id));
                }
                return 28;
            };
            PressKey(window, Key.PageDown);
            Assert.True(changed);
            Assert.Same(replacement, grid.Source);
            AssertCurrent(grid, replacement.Rows[1].Id, column.Id);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Page_key_cancels_when_a_height_callback_removes_the_current_row()
    {
        var items = new ObservableCollection<TreeNode>(Enumerable.Range(0, 12).Select(index => new TreeNode(index.ToString())));
        using var source = new FlatTreeDataGridSource<TreeNode>(items);
        var grid = Create(source);
        var column = TextColumn();
        grid.Columns.Add(column);
        var window = Show(grid);
        try
        {
            SetCurrent(grid, source.Rows[^1].Id, column.Id);
            var changed = false;
            grid.RowHeightSelector = _ =>
            {
                if (!changed)
                {
                    changed = true;
                    while (items.Count > 1) items.RemoveAt(items.Count - 1);
                }
                return 28;
            };
            Assert.False(changed);
            grid.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.PageUp, Source = grid });
            Assert.True(changed);
            Assert.Single(source.Rows);
            Assert.Null(grid.State!.CurrentCell);
        }
        finally { window.Close(); }
    }

    private static TreeDataGridHierarchicalExpanderColumn AddTreeColumn(GridControl grid)
    {
        var tree = new TreeDataGridHierarchicalExpanderColumn { Width = new GridLength(180), InnerColumn = TextColumn() };
        grid.Columns.Add(tree);
        return tree;
    }

    private static TreeDataGridTextColumn TextColumn() => new() { Width = new GridLength(120), ValueGetter = item => ((TreeNode)item).Name };

    private static GridControl Create(TreeDataGridSource source) => new()
    {
        Source = source, Height = 180,
        Template = new FuncControlTemplate<GridControl>((_, scope) =>
        {
            var rows = new TreeDataGridRowsPresenter();
            var scroll = new ScrollViewer { Content = rows,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scope.Register("PART_RowsPresenter", rows);
            scope.Register("PART_ScrollViewer", scroll);
            return scroll;
        })
    };

    private static Window Show(Control content)
    {
        var window = new Window { Width = 420, Height = 250, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void SetCurrent(GridControl grid, NodeId node, ColumnId column)
    {
        grid.State!.SetCurrentCell(new(node, column));
        grid.Focus();
    }

    private static void AssertCurrent(GridControl grid, NodeId node, ColumnId column) => Assert.Equal(new CellAddress(node, column), grid.State!.CurrentCell);

    private static void PressKey(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        window.UpdateLayout();
    }

    public sealed class TreeNode(string name, params TreeNode[] children)
    {
        public string Name { get; set; } = name;
        public TreeNode[] Children { get; } = children;
    }
}
