using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class ColumnVisibilityLayoutTests
{
    [AvaloniaFact]
    public void Layout_captures_visible_order_and_excludes_hidden_widths_from_star_allocation()
    {
        using var source = new FlatTreeDataGridSource<string>(["row"]);
        var hidden = new TreeDataGridTextColumn { Width = new GridLength(400), IsVisible = false };
        var fixedColumn = new TreeDataGridTextColumn { Width = new GridLength(80) };
        var auto = new TreeDataGridTextColumn { Width = GridLength.Auto, EstimatedAutoWidth = 60, IsVisible = false };
        var star = new TreeDataGridTextColumn { Width = new GridLength(1, GridUnitType.Star) };
        var columns = new[] { hidden, fixedColumn, auto, star };
        var layout = new TreeDataGridLayout(source.Rows, columns, 300, frozenColumnCount: 1);

        Assert.Equal(new[] { fixedColumn, star }, layout.Columns);
        Assert.Equal(2, layout.ColumnCount);
        Assert.Equal(80, layout.FrozenWidth);
        Assert.Equal(220, layout.GetColumnWidth(1));
        Assert.True(layout.HitTest(new Point(100, 10), default, new Size(300, 28), out _, out var index));
        Assert.Equal(1, index);

        fixedColumn.IsVisible = false;
        hidden.IsVisible = true;
        Assert.Equal(new[] { fixedColumn, star }, layout.Columns); // Captured mapping stays consistent with captured geometry.
        var next = new TreeDataGridLayout(source.Rows, columns, 300, frozenColumnCount: 10);
        Assert.Equal(new[] { hidden, star }, next.Columns);
        Assert.Equal(2, next.FrozenColumnCount);
        Assert.Equal(400, next.GetColumnWidth(0));
        Assert.Equal(24, next.GetColumnWidth(1));
    }

    [AvaloniaFact]
    public void Frozen_cells_headers_and_immediate_hits_follow_visible_display_indices()
    {
        using var source = new FlatTreeDataGridSource<string>(["row"]);
        var grid = Create(source);
        var columns = AddColumns(grid, 5, 100);
        grid.FrozenColumnCount = 1;
        var window = Show(grid);
        try
        {
            var presenter = grid.RowsPresenter!;
            presenter.Offset = new Vector(100, 0);
            window.UpdateLayout();
            columns[0].IsVisible = false;
            var frozen = presenter.HitTestCell(new Point(50, 10)); // Before a new layout pass.
            Assert.Equal(columns[1].Id, frozen!.Value.ColumnId);
            Assert.Equal(0, frozen.Value.ColumnIndex);
            window.UpdateLayout();

            Assert.Equal(columns.Skip(1), presenter.Layout!.Columns);
            Assert.Equal(100, presenter.Layout.FrozenWidth);
            var row = Assert.Single(presenter.RealizedRows);
            Assert.All(row.RealizedCells, cell => Assert.Same(presenter.Layout.Columns[cell.ColumnIndex], cell.Column));
            Assert.DoesNotContain(row.RealizedCells, cell => ReferenceEquals(cell.Column, columns[0]));
            Assert.DoesNotContain(grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>(), header => ReferenceEquals(header.Column, columns[0]));
            Assert.Contains(grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>(), header => ReferenceEquals(header.Column, columns[1]) && header.Bounds.X == 0);
            var scrollingHit = presenter.HitTestCell(new Point(150, 10))!.Value;
            var expectedDisplayIndex = (int)((150 + presenter.Offset.X) / 100);
            Assert.Equal(expectedDisplayIndex, scrollingHit.ColumnIndex);
            Assert.Equal(columns[expectedDisplayIndex + 1].Id, scrollingHit.ColumnId);

            columns[0].IsVisible = true;
            window.UpdateLayout();
            Assert.Equal(columns, presenter.Layout.Columns);
            Assert.Equal(columns[0].Id, presenter.HitTestCell(new Point(50, 10))!.Value.ColumnId);
            Assert.All(Assert.Single(presenter.RealizedRows).RealizedCells, cell => Assert.Same(presenter.Layout.Columns[cell.ColumnIndex], cell.Column));
            Assert.Equal(5, grid.Columns.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void All_hidden_columns_clear_cells_headers_and_horizontal_scroll_then_restore_same_identity()
    {
        using var source = new FlatTreeDataGridSource<string>(["row"]);
        var grid = Create(source);
        var columns = AddColumns(grid, 4, 120);
        grid.FrozenColumnCount = 3;
        var window = Show(grid);
        try
        {
            var presenter = grid.RowsPresenter!;
            presenter.Offset = new Vector(120, 0);
            foreach (var column in columns) column.IsVisible = false;
            Assert.Null(presenter.HitTestCell(new Point(10, 10)));
            window.UpdateLayout();
            Assert.Equal(0, presenter.Layout!.ColumnCount);
            Assert.Equal(0, presenter.Layout.FrozenColumnCount);
            Assert.Equal(0, presenter.Extent.Width);
            Assert.Equal(0, presenter.Offset.X);
            Assert.Equal(0, presenter.RealizedCellCount);
            Assert.Empty(grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>());

            columns[2].IsVisible = true;
            window.UpdateLayout();
            Assert.Same(columns[2], Assert.Single(presenter.Layout.Columns));
            Assert.Same(columns[2], Assert.Single(Assert.Single(presenter.RealizedRows).RealizedCells).Column);
            Assert.Equal(1, presenter.Layout.FrozenColumnCount);
            Assert.Equal(columns[2].Id, presenter.HitTestCell(new Point(10, 10))!.Value.ColumnId);
            Assert.Equal(columns, grid.Columns);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ItemsSource_entry_uses_the_same_visible_mapping_and_retains_source_adapter()
    {
        var grid = Create(null);
        grid.ItemsSource = new[] { "one", "two" };
        var columns = AddColumns(grid, 3, 90);
        columns[1].IsVisible = false;
        var window = Show(grid);
        try
        {
            var adapter = grid.Source;
            Assert.Equal(new[] { columns[0], columns[2] }, grid.RowsPresenter!.Layout!.Columns);
            Assert.Equal(columns[2].Id, grid.RowsPresenter.HitTestCell(new Point(100, 10))!.Value.ColumnId);
            columns[0].IsVisible = false;
            window.UpdateLayout();
            Assert.Same(adapter, grid.Source);
            Assert.Same(columns[2], Assert.Single(grid.RowsPresenter.Layout.Columns));
            Assert.All(grid.RowsPresenter.RealizedRows, row => Assert.Same(columns[2], Assert.Single(row.RealizedCells).Column));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Hiding_and_restoring_auto_column_retains_width_measured_on_an_offscreen_row()
    {
        using var source = new FlatTreeDataGridSource<string>(Enumerable.Range(0, 100).Select(index => index == 99 ? new string('W', 60) : "x"));
        var grid = Create(source);
        var auto = new TreeDataGridTextColumn { Header = "Auto", Width = GridLength.Auto, ValueGetter = item => item };
        grid.Columns.Add(auto);
        grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(70), ValueGetter = _ => "fixed" });
        var window = Show(grid);
        try
        {
            var presenter = grid.RowsPresenter!;
            var initial = presenter.Layout!.GetColumnWidth(0);
            presenter.ScrollIntoView(source.Rows[^1].Id);
            window.UpdateLayout();
            var measured = presenter.Layout!.GetColumnWidth(0);
            Assert.True(measured > initial + 100);
            presenter.Offset = default;
            window.UpdateLayout();
            auto.IsVisible = false;
            window.UpdateLayout();
            auto.IsVisible = true;
            window.UpdateLayout();
            Assert.Equal(measured, presenter.Layout.GetColumnWidth(0));
            Assert.Equal(GridLength.Auto, auto.Width);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Auto_row_height_is_recomputed_from_visible_cells_and_restored_after_showing_wrapped_column()
    {
        using var source = new FlatTreeDataGridSource<string>([string.Join(' ', Enumerable.Repeat("wrap", 35)), "small"]);
        var grid = Create(source);
        grid.AutoRowHeight = true;
        var wrapped = new TreeDataGridTextColumn { Width = new GridLength(100), TextWrapping = TextWrapping.Wrap, ValueGetter = item => item };
        grid.Columns.Add(wrapped);
        grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(100), ValueGetter = _ => "short" });
        var window = Show(grid);
        try
        {
            var presenter = grid.RowsPresenter!;
            var tall = presenter.Layout!.GetRowHeight(0);
            Assert.True(tall > 100);
            wrapped.IsVisible = false;
            window.UpdateLayout();
            Assert.Equal(grid.RowHeight, presenter.Layout!.GetRowHeight(0));
            wrapped.IsVisible = true;
            window.UpdateLayout();
            Assert.Equal(tall, presenter.Layout!.GetRowHeight(0));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BringIntoView_for_a_realized_cell_uses_the_visible_column_and_frozen_boundary()
    {
        using var source = new FlatTreeDataGridSource<string>(["row"]);
        var grid = Create(source);
        var columns = AddColumns(grid, 6, 100);
        columns[1].IsVisible = false;
        grid.FrozenColumnCount = 1;
        var window = Show(grid);
        try
        {
            var presenter = grid.RowsPresenter!;
            presenter.Offset = new Vector(140, 0);
            window.UpdateLayout();
            var cell = Assert.Single(presenter.RealizedRows).RealizedCells.Single(item => ReferenceEquals(item.Column, columns[3]));
            Assert.Equal(2, cell.ColumnIndex);
            Assert.True(cell.Bounds.X < presenter.Layout!.FrozenWidth);
            Assert.True(presenter.BringIntoView((Control)cell.Content!, default));
            window.UpdateLayout();
            Assert.Equal(100, presenter.Offset.X);
            Assert.Equal(100, presenter.Layout!.GetColumnLeft(2, presenter.Offset.X));
        }
        finally { window.Close(); }
    }

    private static TreeDataGridTextColumn[] AddColumns(GridControl grid, int count, double width)
    {
        var columns = Enumerable.Range(0, count).Select(index => new TreeDataGridTextColumn
        {
            Header = $"Column {index}", Width = new GridLength(width), ValueGetter = item => item
        }).ToArray();
        foreach (var column in columns) grid.Columns.Add(column);
        return columns;
    }

    private static GridControl Create(TreeDataGridSource? source) => new()
    {
        Source = source,
        Template = new FuncControlTemplate<GridControl>((_, scope) =>
        {
            var rows = new TreeDataGridRowsPresenter { Name = "PART_RowsPresenter" };
            var headers = new TreeDataGridColumnHeadersPresenter { Name = "PART_ColumnHeadersPresenter" };
            var scroll = new ScrollViewer { Name = "PART_ScrollViewer", Content = rows,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scope.Register("PART_RowsPresenter", rows);
            scope.Register("PART_ColumnHeadersPresenter", headers);
            scope.Register("PART_ScrollViewer", scroll);
            Grid.SetRow(scroll, 1);
            return new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { headers, scroll } };
        })
    };

    private static Window Show(Control content)
    {
        var window = new Window { Width = 360, Height = 240, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }
}
