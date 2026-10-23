using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class VirtualizationTests
{
    [AvaloniaFact]
    public void Hundred_thousand_rows_are_viewport_bounded_and_recycled()
    {
        using var source = new FlatTreeDataGridSource<string>(Enumerable.Range(0, 100_000).Select(i => $"Row {i}"));
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            var rows = Assert.IsType<TreeDataGridRowsPresenter>(grid.RowsPresenter);
            Assert.InRange(rows.RealizedRowCount, 1, 14);
            Assert.Equal(2_800_000, rows.Extent.Height);
            var initialContainers = rows.RealizedRows.ToArray();
            var created = rows.CreatedRowCount;
            var clears = 0;
            grid.RowClearing += (_, e) => { Assert.NotNull(e.Container.DataContext); Assert.Same(e.Row, e.Container.Row); clears++; };
            for (var i = 1; i <= 20; ++i)
            {
                rows.Offset = new Vector(0, i * 8000);
                grid.UpdateLayout();
                Assert.InRange(rows.RealizedRowCount, 1, 14);
                Assert.InRange(rows.RetainedRowCount, 1, 16);
            }
            Assert.True(clears > 100);
            Assert.True(rows.RecycledRowCount > 100);
            Assert.InRange(rows.CreatedRowCount, created, created + 2);
            Assert.Contains(rows.RealizedRows, row => initialContainers.Contains(row));
            Assert.All(rows.RealizedRows, row => Assert.Equal($"Row {row.RowIndex}", row.DataContext));
            Assert.True(rows.ScrollIntoView(source.Rows[99_999].Id));
            grid.UpdateLayout();
            Assert.Contains(rows.RealizedRows, row => row.RowIndex == 99_999);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ScrollViewer_offsets_variable_heights_and_frozen_cells_remain_aligned()
    {
        using var source = new FlatTreeDataGridSource<string>(Enumerable.Range(0, 100).Select(i => $"Row {i}"));
        var grid = CreateGrid(source);
        grid.RowHeightSelector = row => (int)row.ModelIndex[0] % 2 == 0 ? 20 : 40;
        grid.FrozenColumnCount = 1;
        var window = Show(grid);
        try
        {
            var rows = grid.RowsPresenter!;
            var scroll = (ScrollViewer)grid.GetTemplateDescendants().Single(c => c.Name == "PART_ScrollViewer");
            scroll.Offset = new Vector(170, 115);
            grid.UpdateLayout();
            Assert.Equal(170, rows.Offset.X);
            Assert.Equal(115, rows.Offset.Y);
            Assert.Equal(3000, rows.Extent.Height);
            var frozen = rows.HitTestCell(new Point(50, 10));
            Assert.NotNull(frozen);
            Assert.Equal(0, frozen.Value.ColumnIndex);
            Assert.Equal(4, frozen.Value.RowIndex);
            var scrolling = rows.HitTestCell(new Point(220, 10));
            Assert.NotNull(scrolling);
            Assert.Equal(1, scrolling.Value.ColumnIndex);
            Assert.Equal(0, rows.Layout!.GetCellBounds(4, 0, rows.Offset).X);
            Assert.Equal(30, rows.Layout.GetCellBounds(4, 1, rows.Offset).X);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Source_replacement_and_unload_release_subscriptions_and_reconnect()
    {
        var oldItems = new ObservableCollection<string>(["old"]);
        using var oldSource = new CountingSource(oldItems);
        var items = new ObservableCollection<string>(["new"]);
        using var newSource = new CountingSource(items);
        var grid = CreateGrid(oldSource);
        var window = Show(grid);
        try
        {
            Assert.Equal(2, oldSource.SubscriberCount);
            grid.Source = newSource;
            grid.UpdateLayout();
            Assert.Equal(0, oldSource.SubscriberCount);
            Assert.Equal(2, newSource.SubscriberCount);
            Assert.False(oldSource.Disposed);
            var oldContainers = grid.RowsPresenter!.RealizedRows.ToArray();
            window.Content = null;
            Assert.Equal(0, newSource.SubscriberCount);
            Assert.All(oldContainers, row => { Assert.Null(row.DataContext); Assert.Null(row.Row); Assert.Empty(row.Children); });
            Assert.Equal(0, grid.RowsPresenter.RealizedRowCount);
            items.Add("while detached");
            window.Content = grid;
            grid.UpdateLayout();
            Assert.Equal(2, newSource.SubscriberCount);
            Assert.Equal(2, grid.RowsPresenter.RealizedRowCount);
            Assert.False(newSource.Disposed);
        }
        finally { window.Close(); }
        Assert.Equal(0, newSource.SubscriberCount);
    }

    [AvaloniaFact]
    public void Binding_values_refresh_after_recycling_and_old_bindings_are_removed()
    {
        using var source = new FlatTreeDataGridSource<Item>(Enumerable.Range(0, 1000).Select(i => new Item($"Item {i}")));
        var grid = CreateGrid(source);
        grid.Columns.Clear();
        grid.Columns.Add(new TreeDataGridTextColumn { Binding = new ReflectionBinding(nameof(Item.Name)), Width = new GridLength(200) });
        var window = Show(grid);
        try
        {
            var rows = grid.RowsPresenter!;
            Assert.All(rows.RealizedRows, row => Assert.Equal($"Item {row.RowIndex}", ((TextBlock)row.RealizedCells[0].Content!).Text));
            rows.Offset = new Vector(0, 20_000);
            grid.UpdateLayout();
            Assert.All(rows.RealizedRows, row => Assert.Equal($"Item {row.RowIndex}", ((TextBlock)row.RealizedCells[0].Content!).Text));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Column_changes_update_geometry_and_empty_state_is_exposed()
    {
        var items = new ObservableCollection<string>();
        using var source = new FlatTreeDataGridSource<string>(items);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            Assert.Contains(":empty", grid.Classes);
            items.Add("a");
            grid.UpdateLayout();
            Assert.DoesNotContain(":empty", grid.Classes);
            var id = grid.Columns[0].Id;
            grid.Columns[0].Width = new GridLength(280);
            grid.Columns.Move(0, 1);
            grid.UpdateLayout();
            Assert.Equal(id, grid.Columns[1].Id);
            Assert.Equal(280, grid.RowsPresenter!.Layout!.GetColumnWidth(1));
            items.Clear();
            grid.UpdateLayout();
            Assert.Contains(":empty", grid.Classes);
            Assert.Equal(0, grid.RowsPresenter.RealizedRowCount);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Missing_required_template_part_has_a_clear_diagnostic()
    {
        var grid = new GridControl { Template = new FuncControlTemplate<GridControl>((_, _) => new Border()) };
        var error = Assert.Throws<InvalidOperationException>(() => grid.ApplyTemplate());
        Assert.Contains("PART_ScrollViewer", error.Message);
    }

    [AvaloniaFact]
    public void Replacing_template_clears_old_presenters_without_duplicate_source_subscriptions()
    {
        using var source = new CountingSource(Enumerable.Range(0, 100).Select(i => $"Row {i}"));
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            var oldPresenter = grid.RowsPresenter!;
            var oldRows = oldPresenter.RealizedRows.ToArray();
            grid.Template = CreateGrid(source).Template;
            grid.ApplyTemplate();
            grid.UpdateLayout();
            Assert.NotSame(oldPresenter, grid.RowsPresenter);
            Assert.Equal(0, oldPresenter.RealizedRowCount);
            Assert.All(oldRows, row => Assert.Null(row.DataContext));
            Assert.Equal(2, source.SubscriberCount);
            Assert.True(grid.RowsPresenter!.RealizedRowCount > 0);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Invalid_offsets_and_row_height_are_contained_and_shrink_releases_pool()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 100).Select(i => $"Row {i}"));
        using var source = new FlatTreeDataGridSource<string>(items);
        var grid = CreateGrid(source);
        Assert.Throws<ArgumentException>(() => grid.RowHeight = double.NaN);
        Assert.Throws<ArgumentException>(() => grid.RowHeight = 0);
        var window = Show(grid);
        try
        {
            var rows = grid.RowsPresenter!;
            rows.Offset = new Vector(double.NaN, double.PositiveInfinity);
            grid.UpdateLayout();
            Assert.Equal(default, rows.Offset);
            rows.Offset = new Vector(double.MaxValue, double.MaxValue);
            grid.UpdateLayout();
            Assert.Equal(rows.Extent.Height - rows.Viewport.Height, rows.Offset.Y);
            items.Clear();
            grid.UpdateLayout();
            Assert.Equal(0, rows.Offset.Y);
            Assert.InRange(rows.RetainedRowCount, 0, 2);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Compiled_axaml_columns_and_code_columns_display_the_same_models()
    {
        using var source = new FlatTreeDataGridSource<Item>([new Item("中文节点"), new Item("Second"), new Item(null)]);
        var view = new XamlConsumerView();
        var xaml = view.FindControl<GridControl>("XamlGrid")!;
        var code = view.FindControl<GridControl>("CodeGrid")!;
        code.Template = xaml.Template;
        code.Columns.Add(new TreeDataGridTextColumn { Header = "名称", Width = new GridLength(180), ValueGetter = item => ((Item)item).Name });
        xaml.Source = code.Source = source;
        var window = Show(view);
        try
        {
            Assert.IsAssignableFrom<BindingBase>(((TreeDataGridTextColumn)xaml.Columns[0]).Binding);
            var xamlTexts = xaml.RowsPresenter!.RealizedRows.Select(row => ((TextBlock)row.RealizedCells[0].Content!).Text).ToArray();
            var codeTexts = code.RowsPresenter!.RealizedRows.Select(row => ((TextBlock)row.RealizedCells[0].Content!).Text).ToArray();
            Assert.Equal(new[] { "中文节点", "Second", null }, xamlTexts);
            Assert.Equal(xamlTexts, codeTexts);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Hundred_columns_clip_scrolling_content_at_frozen_and_viewport_edges()
    {
        using var source = new FlatTreeDataGridSource<string>(["row"]);
        var grid = CreateGrid(source);
        grid.Columns.Clear();
        for (var i = 0; i < 100; ++i) grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(80), ValueGetter = x => x });
        grid.FrozenColumnCount = 2;
        var window = Show(grid);
        try
        {
            var presenter = grid.RowsPresenter!;
            presenter.Offset = new Vector(805, 0);
            grid.UpdateLayout();
            var row = Assert.Single(presenter.RealizedRows);
            Assert.Equal(5, row.Children.Count); // two frozen + three intersecting viewport columns
            var foundPartiallyClippedRightCell = false;
            foreach (var cell in row.RealizedCells.Where(x => x.ColumnIndex >= 2))
            {
                var clip = Assert.IsType<RectangleGeometry>(cell.Clip).Rect;
                if (clip.Width <= 0) continue;
                Assert.True(cell.Bounds.X + clip.X >= presenter.Layout!.FrozenWidth);
                Assert.True(cell.Bounds.X + clip.Right <= presenter.Viewport.Width);
                if (cell.Bounds.Right > presenter.Viewport.Width)
                {
                    foundPartiallyClippedRightCell = true;
                    Assert.Equal(presenter.Viewport.Width, cell.Bounds.X + clip.Right);
                }
            }
            Assert.True(foundPartiallyClippedRightCell);
            Assert.Equal(0, presenter.HitTestCell(new Point(30, 10))!.Value.ColumnIndex);
            Assert.Equal(1, presenter.HitTestCell(new Point(110, 10))!.Value.ColumnIndex);
            Assert.Equal(12, presenter.HitTestCell(new Point(170, 10))!.Value.ColumnIndex);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Immediate_hit_testing_and_scroll_after_projection_changes_use_current_geometry()
    {
        var parent = new TreeItem("parent", [new TreeItem("child")]);
        using var source = new HierarchicalTreeDataGridSource<TreeItem>([parent], item => item.Children);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            var rows = grid.RowsPresenter!;
            source.SetExpanded(source.Rows[0].Id, true);
            var childId = source.Rows[1].Id;
            Assert.True(rows.ScrollIntoView(childId)); // deliberately before the next layout pass
            Assert.Equal(childId, rows.HitTestCell(new Point(30, 35))!.Value.NodeId);
            source.SetExpanded(source.Rows[0].Id, false);
            Assert.Null(rows.HitTestCell(new Point(30, 35)));
            Assert.False(rows.ScrollIntoView(childId));
            grid.Columns.Clear();
            Assert.Null(rows.HitTestCell(new Point(30, 5)));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Row_clearing_callback_can_replace_source_without_recursive_cleanup()
    {
        using var source = new CountingSource(Enumerable.Range(0, 100).Select(i => $"old {i}"));
        using var replacement = new CountingSource(["replacement"]);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            var replaced = false;
            grid.RowClearing += (_, _) =>
            {
                if (replaced) return;
                replaced = true;
                grid.Source = replacement;
            };
            grid.RowsPresenter!.Offset = new Vector(0, 1500);
            grid.UpdateLayout();
            grid.UpdateLayout();
            Assert.True(replaced);
            Assert.Equal(0, source.SubscriberCount);
            Assert.Equal(2, replacement.SubscriberCount);
            Assert.Equal("replacement", Assert.Single(grid.RowsPresenter.RealizedRows).DataContext);
        }
        finally { window.Close(); }
    }

    private static GridControl CreateGrid(TreeDataGridSource source)
    {
        var grid = new GridControl
        {
            Source = source,
            Template = new FuncControlTemplate<GridControl>((_, scope) =>
            {
                var rows = new TreeDataGridRowsPresenter { Name = "PART_RowsPresenter" };
                var scroll = new ScrollViewer { Name = "PART_ScrollViewer", Content = rows,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                scope.Register("PART_RowsPresenter", rows);
                scope.Register("PART_ScrollViewer", scroll);
                return scroll;
            })
        };
        for (var i = 0; i < 4; ++i)
            grid.Columns.Add(new TreeDataGridTextColumn { Header = $"Column {i}", Width = new GridLength(200), ValueGetter = x => x });
        return grid;
    }
    private static Window Show(Control content)
    {
        var window = new Window { Width = 360, Height = 240, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }
    private sealed record Item(string? Name);
    private sealed record TreeItem(string Name, IEnumerable<TreeItem>? Children = null);
    private sealed class CountingSource : TreeDataGridSource
    {
        private readonly FlatTreeDataGridSource<string> _inner;
        private EventHandler<ProjectionChangedEventArgs>? _changed;
        public CountingSource(IEnumerable<string> items)
        {
            _inner = new FlatTreeDataGridSource<string>(items);
            _inner.ProjectionChanged += (_, e) => _changed?.Invoke(this, e);
        }
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public bool Disposed { get; private set; }
        public override IReadOnlyList<TreeDataGridRow> Rows => _inner.Rows;
        public override long Version => _inner.Version;
        public override bool IsRefreshDeferred => _inner.IsRefreshDeferred;
        public override event EventHandler<ProjectionChangedEventArgs>? ProjectionChanged { add => _changed += value; remove => _changed -= value; }
        public override bool TryGetRow(NodeId id, out TreeDataGridRow? row) => _inner.TryGetRow(id, out row);
        public override bool TryGetNodeId(IndexPath path, out NodeId id) => _inner.TryGetNodeId(path, out id);
        public override int GetViewIndex(NodeId id) => _inner.GetViewIndex(id);
        public override bool SetExpanded(NodeId id, bool expanded) => _inner.SetExpanded(id, expanded);
        public override IDisposable DeferRefresh() => _inner.DeferRefresh();
        public override void Refresh() => _inner.Refresh();
        public override void Dispose() { Disposed = true; _inner.Dispose(); }
    }
}

