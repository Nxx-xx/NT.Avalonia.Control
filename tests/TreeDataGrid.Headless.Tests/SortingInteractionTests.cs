using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class SortingInteractionTests
{
    [AvaloniaFact]
    public void Header_click_cycles_three_states_and_keeps_source_identity()
    {
        var models = new[] { new Item("c", 3), new Item("a", 1), new Item("b", 2) };
        using var source = new FlatTreeDataGridSource<Item>(models);
        var originalIds = source.Rows.Select(row => row.Id).ToArray();
        var grid = Create(source);
        grid.IsThreeStateSort = true;
        var column = grid.Columns[0];
        var window = Show(grid);
        try
        {
            ClickHeader(window, grid);
            Assert.Equal(new[] { "a", "b", "c" }, Names(source));
            Assert.Equal(TreeDataGridSortDirection.Ascending, Header(grid).SortDirection);
            Assert.Equal("▲", Header(grid).SortIndicator);
            ClickHeader(window, grid);
            Assert.Equal(new[] { "c", "b", "a" }, Names(source));
            Assert.Equal("▼", Header(grid).SortIndicator);
            ClickHeader(window, grid);
            Assert.Equal(models, source.Rows.Select(row => row.Model));
            Assert.Equal(originalIds, source.Rows.Select(row => row.Id));
            Assert.Null(source.SortColumn);
            Assert.Equal("", Header(grid).SortIndicator);
            Assert.Equal(column.Id, grid.Columns[0].Id);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Keyboard_and_user_sort_switches_share_the_same_header_contract()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("b", 2), new("a", 1)]);
        var grid = Create(source);
        var window = Show(grid);
        try
        {
            Header(grid).Focus();
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.UpdateLayout();
            Assert.Equal(TreeDataGridSortDirection.Ascending, source.SortDirection);
            grid.CanUserSortColumns = false;
            ClickHeader(window, grid);
            Assert.Equal(TreeDataGridSortDirection.Ascending, source.SortDirection);
            grid.CanUserSortColumns = true;
            grid.Columns[0].CanUserSort = false;
            ClickHeader(window, grid);
            Assert.Equal(TreeDataGridSortDirection.Ascending, source.SortDirection);
            // Explicit programmatic sorting remains available when user gestures are disabled.
            Assert.True(grid.SortByColumn(grid.Columns[0].Id, TreeDataGridSortDirection.Descending));
            window.UpdateLayout();
            Assert.Equal("▼", Header(grid).SortIndicator);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Resize_thumb_drag_changes_width_without_sorting()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("b", 2), new("a", 1)]);
        var grid = Create(source);
        var window = Show(grid);
        try
        {
            var header = Header(grid);
            var thumb = header.GetVisualDescendants().OfType<Thumb>().Single();
            var point = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            var width = grid.Columns[0].Width.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(20, 0));
            window.MouseUp(point + new Vector(20, 0), MouseButton.Left);
            window.UpdateLayout();
            Assert.True(grid.Columns[0].Width.Value > width);
            Assert.Null(source.SortColumn);
            Assert.Equal(new[] { "b", "a" }, Names(source));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Validation_failure_blocks_header_sort_and_preserves_draft()
    {
        var model = new Item("b", 2);
        using var source = new FlatTreeDataGridSource<Item>([model, new("a", 1)]);
        var grid = Create(source);
        grid.Columns[0].ValueValidator = value => string.IsNullOrWhiteSpace(value as string) ? "Name is required" : null;
        var window = Show(grid);
        try
        {
            var binding = ((TreeDataGridTextColumn)grid.Columns[0]).Binding as ReflectionBinding;
            Assert.True(grid.BeginEdit(new CellAddress(source.Rows[0].Id, grid.Columns[0].Id)), $"Binding Source = {binding?.Source ?? "null"}, Path = {binding?.Path}");
            var edit = Assert.IsType<TreeDataGridEditSession>(grid.State!.EditSession);
            edit.Value = "";
            ClickHeader(window, grid);
            Assert.Same(edit, grid.State.EditSession);
            Assert.Equal("Name is required", edit.ValidationError);
            Assert.Equal("", edit.Value);
            Assert.Equal("b", model.Name);
            Assert.Null(source.SortColumn);
            edit.Value = "z";
            ClickHeader(window, grid);
            Assert.Null(grid.State.EditSession);
            Assert.Equal("z", model.Name);
            Assert.Equal(new[] { "a", "z" }, Names(source));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Source_sort_during_edit_retains_model_target_and_filter_cancels_without_write()
    {
        var model = new Item("b", 2);
        using var source = new FlatTreeDataGridSource<Item>([model, new("a", 1)]);
        var grid = Create(source);
        var window = Show(grid);
        try
        {
            var original = source.Rows[0].Id;
            grid.State!.Select(original);
            grid.BeginEdit(new CellAddress(original, grid.Columns[0].Id));
            var edit = grid.State.EditSession!;
            edit.Value = "c";
            source.Sort(grid.Columns[0].Id, (left, right) => string.CompareOrdinal(((Item)left).Name, ((Item)right).Name), TreeDataGridSortDirection.Ascending);
            window.UpdateLayout();
            Assert.Same(edit, grid.State.EditSession);
            Assert.Equal(1, source.GetViewIndex(original));
            Assert.True(grid.CommitEdit());
            Assert.Equal("c", model.Name);
            Assert.Contains(original, grid.State.SelectedNodes);
            grid.BeginEdit(new CellAddress(original, grid.Columns[0].Id));
            grid.State.EditSession!.Value = "not committed";
            source.SetFilter(item => !ReferenceEquals(item, model));
            window.UpdateLayout();
            Assert.Null(grid.State.EditSession);
            Assert.Equal("c", model.Name);
            Assert.Contains(original, grid.State.SelectedNodes);
            source.SetFilter(null);
            window.UpdateLayout();
            Assert.Equal(1, source.GetViewIndex(original));
            Assert.Contains(original, grid.State.SelectedNodes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Sort_stops_if_edit_commit_callback_replaces_source()
    {
        var model = new Item("b", 2);
        using var source = new FlatTreeDataGridSource<Item>([model, new("a", 1)]);
        using var replacement = new FlatTreeDataGridSource<Item>([new("replacement", 0)]);
        var grid = Create(source);
        grid.Columns[0].ValueSetter = (item, value) => ((Item)item).Name = (string)value!;
        var window = Show(grid);
        try
        {
            grid.BeginEdit(new CellAddress(source.Rows[0].Id, grid.Columns[0].Id));
            grid.State!.EditSession!.Value = "changed";
            source.ProjectionChanged += (_, _) => grid.Source = replacement;
            Assert.False(grid.SortByColumn(grid.Columns[0].Id, TreeDataGridSortDirection.Ascending));
            Assert.Same(replacement, grid.Source);
            Assert.Null(source.SortColumn);
            Assert.Null(replacement.SortColumn);
            Assert.Equal("changed", model.Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Expander_column_reads_binding_values_and_uses_inner_custom_comparer()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("ten", 10), new("two", 2)]);
        var grid = Create(source);
        grid.Columns.Clear();
        var inner = new TreeDataGridTextColumn { Binding = new ReflectionBinding(nameof(Item.Number)) };
        var tree = new TreeDataGridHierarchicalExpanderColumn { Header = "Number", InnerColumn = inner };
        grid.Columns.Add(tree);
        var window = Show(grid);
        try
        {
            Assert.True(grid.SortByColumn(tree.Id, TreeDataGridSortDirection.Ascending));
            Assert.Equal(new[] { "two", "ten" }, Names(source));
            inner.Comparison = (left, right) => string.CompareOrdinal(((Item)left).Name, ((Item)right).Name);
            Assert.True(grid.SortByColumn(tree.Id, TreeDataGridSortDirection.Ascending));
            window.UpdateLayout();
            Assert.Equal(new[] { "ten", "two" }, Names(source));
            Assert.Equal(tree.Id, source.SortColumn);
            Assert.Equal("▲", Header(grid).SortIndicator);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Comparison_that_replaces_source_does_not_publish_old_projection()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("b", 2), new("a", 1)]);
        using var replacement = new FlatTreeDataGridSource<Item>([new("replacement", 0)]);
        var grid = Create(source);
        var rows = source.Rows;
        grid.Columns[0].Comparison = (_, _) => { grid.Source = replacement; return 0; };
        var window = Show(grid);
        try
        {
            Assert.Throws<InvalidOperationException>(() => grid.SortByColumn(grid.Columns[0].Id, TreeDataGridSortDirection.Ascending));
            Assert.Same(rows, source.Rows);
            Assert.Null(source.SortColumn);
            Assert.Same(replacement, grid.Source);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Explicit_null_binding_source_does_not_enable_model_writeback()
    {
        var item = new Item("unchanged", 0);
        using var source = new FlatTreeDataGridSource<Item>([item]);
        var grid = Create(source);
        ((TreeDataGridTextColumn)grid.Columns[0]).Binding = new ReflectionBinding(nameof(Item.Name)) { Source = null };
        var window = Show(grid);
        try
        {
            Assert.Null(grid.RowsPresenter!.RealizedRows[0].RealizedCells[0].Value);
            Assert.False(grid.BeginEdit(new CellAddress(source.Rows[0].Id, grid.Columns[0].Id)));
            Assert.Equal("unchanged", item.Name);
        }
        finally { window.Close(); }
    }

    private static string[] Names(TreeDataGridSource source) => source.Rows.Select(row => ((Item)row.Model).Name).ToArray();
    private static TreeDataGridColumnHeader Header(GridControl grid) => grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().First();
    private static void ClickHeader(Window window, GridControl grid)
    {
        window.UpdateLayout();
        var point = Header(grid).TranslatePoint(new Point(20, 12), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.UpdateLayout();
    }
    private static GridControl Create(TreeDataGridSource source)
    {
        var grid = new GridControl
        {
            Source = source,
            Template = new FuncControlTemplate<GridControl>((_, scope) =>
            {
                var rows = new TreeDataGridRowsPresenter();
                var headers = new TreeDataGridColumnHeadersPresenter();
                var scroll = new ScrollViewer { Content = rows };
                scope.Register("PART_RowsPresenter", rows); scope.Register("PART_ColumnHeadersPresenter", headers); scope.Register("PART_ScrollViewer", scroll);
                Grid.SetRow(scroll, 1);
                return new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { headers, scroll } };
            }),
        };
        grid.Columns.Add(new TreeDataGridTextColumn { Header = "Name", Width = new GridLength(180), Binding = new ReflectionBinding(nameof(Item.Name)) });
        return grid;
    }
    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 280, Content = content };
        window.Show(); window.UpdateLayout(); return window;
    }
    public sealed class Item(string name, int number)
    {
        public string Name { get; set; } = name;
        public int Number { get; set; } = number;
    }
}
