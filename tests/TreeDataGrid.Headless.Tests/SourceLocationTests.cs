using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class SourceLocationTests
{
    [AvaloniaFact]
    public void Source_path_uses_identity_after_sorting_without_changing_selection_or_current_cell()
    {
        using var source = new FlatTreeDataGridSource<Item>(Enumerable.Range(0, 100).Select(i => new Item(i.ToString("D3"))));
        var grid = Create(source);
        var window = Show(grid);
        try
        {
            var first = source.Rows[0].Id;
            grid.State!.Select(first);
            grid.State.SetCurrentCell(new(first, grid.Columns[0].Id));
            source.Sort(grid.Columns[0].Id, (a, b) => StringComparer.Ordinal.Compare(((Item)a).Name, ((Item)b).Name), TreeDataGridSortDirection.Descending);
            Assert.True(source.TryGetNodeId(new IndexPath(0), out var target));
            Assert.Equal(first, target);
            Assert.True(grid.TryScrollIntoView(new IndexPath(0)));
            window.UpdateLayout();
            Assert.True(grid.RowsPresenter!.Offset.Y > 0);
            Assert.Equal(new[] { first }, grid.State.SelectedNodes);
            Assert.Equal(new CellAddress(first, grid.Columns[0].Id), grid.CurrentCell);
            Assert.Contains(grid.RowsPresenter.RealizedRows, row => row.Row?.Id == first);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Collapsed_ancestors_require_explicit_expansion_and_selection_preserves_visible_column()
    {
        var child = new Item("child");
        var branch = new Item("branch"); branch.Children.Add(child);
        var root = new Item("root"); root.Children.Add(branch);
        using var source = new HierarchicalTreeDataGridSource<Item>(new[] { root }, item => item.Children);
        var grid = Create(source); var window = Show(grid);
        try
        {
            Assert.True(source.TryGetNodeId(new IndexPath(0, 0, 0), out var target));
            grid.State!.SetCurrentCell(new(source.Rows[0].Id, grid.Columns[1].Id));
            Assert.False(grid.TryScrollIntoView(target));
            Assert.Single(source.Rows);
            Assert.True(grid.TryScrollIntoView(new IndexPath(0, 0, 0), expandAncestors: true, select: true));
            window.UpdateLayout();
            Assert.Equal(3, source.Rows.Count);
            Assert.Equal(new CellAddress(target, grid.Columns[1].Id), grid.CurrentCell);
            Assert.Equal(new[] { target }, grid.State.SelectedNodes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Filtered_invalid_and_deferred_paths_return_false_without_selecting_other_rows()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("a"), new("b")]);
        var grid = Create(source); var window = Show(grid);
        try
        {
            var first = source.Rows[0].Id;
            grid.State!.Select(first);
            source.SetFilter(item => ((Item)item).Name == "a");
            Assert.False(grid.TryScrollIntoView(new IndexPath(1), expandAncestors: true, select: true));
            Assert.False(grid.TryScrollIntoView(new IndexPath(9), select: true));
            Assert.False(grid.TryScrollIntoView(default(NodeId), select: true));
            using (source.DeferRefresh()) Assert.False(grid.TryScrollIntoView(first, select: true));
            Assert.Equal(new[] { first }, grid.State.SelectedNodes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Locator_rebuilds_dirty_geometry_and_handles_hidden_columns_and_frozen_boundary()
    {
        using var source = new FlatTreeDataGridSource<Item>(Enumerable.Range(0, 100).Select(i => new Item(i.ToString())));
        var grid = Create(source); var window = Show(grid);
        try
        {
            grid.FrozenColumnCount = 1;
            grid.Columns[0].IsVisible = false;
            grid.RowHeightSelector = row => source.GetViewIndex(row.Id) % 2 == 0 ? 60 : 28;
            Assert.True(grid.TryScrollIntoView(new IndexPath(90), select: true)); // No UpdateLayout before the call.
            window.UpdateLayout();
            Assert.True(source.TryGetNodeId(new IndexPath(90), out var id));
            Assert.Equal(new CellAddress(id, grid.Columns[1].Id), grid.CurrentCell);
            Assert.Equal(1, grid.RowsPresenter!.Layout!.FrozenColumnCount);
            Assert.Equal(60, grid.RowsPresenter.Layout.GetRowHeight(90));
            Assert.Contains(grid.RowsPresenter.RealizedRows, row => row.Row?.Id == id);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Expansion_callback_replacing_source_does_not_select_or_scroll_replacement()
    {
        var root = new Item("root"); root.Children.Add(new("target"));
        using var source = new HierarchicalTreeDataGridSource<Item>(new[] { root }, item => item.Children);
        using var replacement = new FlatTreeDataGridSource<Item>([new("replacement")]);
        var grid = Create(source); var window = Show(grid);
        try
        {
            source.RowExpanding += (_, _) => grid.Source = replacement;
            Assert.False(grid.TryScrollIntoView(new IndexPath(0, 0), expandAncestors: true, select: true));
            Assert.Same(replacement, grid.Source);
            Assert.Empty(grid.State!.SelectedNodes);
            Assert.Null(grid.CurrentCell);
            Assert.Equal(default, grid.RowsPresenter!.Offset);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Expansion_callback_shifting_source_path_rejects_stale_target()
    {
        var root = new Item("root"); var target = new Item("target"); root.Children.Add(target);
        using var source = new HierarchicalTreeDataGridSource<Item>(new[] { root }, item => item.Children);
        var grid = Create(source); var window = Show(grid);
        try
        {
            source.RowExpanding += (_, _) => root.Children.Insert(0, new("inserted"));
            Assert.False(grid.TryScrollIntoView(new IndexPath(0, 0), expandAncestors: true, select: true));
            Assert.Empty(grid.State!.SelectedNodes);
            Assert.Null(grid.CurrentCell);
            Assert.Equal("inserted", ((Item)source.Rows[1].Model).Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Selecting_another_row_commits_edit_or_refuses_invalid_draft_while_scroll_only_keeps_session()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("a"), new("b")]);
        var grid = Create(source); var column = grid.Columns[0];
        column.ValueType = typeof(string); column.ValueSetter = (model, value) => ((Item)model).Name = (string)value!;
        column.ValueValidator = value => Equals(value, "invalid") ? "rejected" : null;
        var window = Show(grid);
        try
        {
            var first = source.Rows[0].Id; var second = source.Rows[1].Id;
            Assert.True(grid.BeginEdit(new(first, column.Id)));
            var session = grid.State!.EditSession!; session.Value = "invalid";
            Assert.True(grid.TryScrollIntoView(second));
            Assert.Same(session, grid.State.EditSession);
            Assert.False(grid.TryScrollIntoView(second, select: true));
            Assert.Same(session, grid.State.EditSession);
            Assert.Equal(new CellAddress(first, column.Id), grid.CurrentCell);
            session.Value = "valid";
            Assert.True(grid.TryScrollIntoView(second, select: true));
            Assert.Null(grid.State.EditSession);
            Assert.Equal("valid", ((Item)source.Rows[0].Model).Name);
            Assert.Equal(new CellAddress(second, column.Id), grid.CurrentCell);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Missing_viewport_returns_false_and_all_hidden_columns_only_prevent_requested_selection()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("row")]);
        var grid = Create(source);
        Assert.False(grid.TryScrollIntoView(source.Rows[0].Id));
        var window = Show(grid);
        try
        {
            foreach (var column in grid.Columns) column.IsVisible = false;
            Assert.True(grid.TryScrollIntoView(source.Rows[0].Id));
            Assert.False(grid.TryScrollIntoView(source.Rows[0].Id, select: true));
            Assert.Null(grid.CurrentCell);
        }
        finally { window.Close(); }
    }

    private static GridControl Create(TreeDataGridSource source)
    {
        var grid = new GridControl
        {
            Source = source,
            Template = new FuncControlTemplate<GridControl>((_, scope) =>
            {
                var rows = new TreeDataGridRowsPresenter();
                var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
                scope.Register("PART_RowsPresenter", rows); scope.Register("PART_ScrollViewer", scroll); return scroll;
            }),
        };
        grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(100), ValueGetter = model => ((Item)model).Name });
        grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(100), ValueGetter = model => ((Item)model).Name });
        return grid;
    }

    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 240, Content = content };
        window.Show(); window.UpdateLayout(); return window;
    }

    private sealed class Item(string name)
    {
        public string Name { get; set; } = name;
        public ObservableCollection<Item> Children { get; } = [];
    }
}
