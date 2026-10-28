using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class AutoHeightFactoryPoolTests
{
    private static A5Model[] Models() => Enumerable.Range(0, 1000).Select(i => new A5Model(i % 11 == 0
        ? string.Join(' ', Enumerable.Repeat("自动行高 wrapped 文本", 12)) : "row " + i)).ToArray();
    private static GridControl Create(TreeDataGridSource source, A5RecordingFactory factory)
    {
        var grid = new GridControl { Source = source, ElementFactory = factory, Template = A1TestHost.Template(), AutoRowHeight = true, FrozenColumnCount = 2 };
        for (var i = 0; i < 30; i++) grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(110), TextWrapping = TextWrapping.Wrap, Binding = new ReflectionBinding(nameof(A5Model.Name)) });
        return grid;
    }
    private static void AssertScene(GridControl grid, A5RecordingFactory factory)
    {
        var rows = grid.RowsPresenter!;
        Assert.InRange(rows.RetainedRowCount, rows.RealizedRowCount, (int)Math.Ceiling(rows.Viewport.Height / grid.RowHeight) + 5);
        Assert.Equal(rows.RealizedRowCount, rows.Children.OfType<TreeDataGridRowPresenter>().Count());
        foreach (var row in rows.RealizedRows)
        {
            Assert.Same(grid.Source!.Rows[row.RowIndex].Model, row.DataContext);
            foreach (var cell in row.RealizedCells)
            {
                Assert.Same(row.DataContext, cell.DataContext); Assert.Same(rows.Layout!.Columns[cell.ColumnIndex], cell.Column);
                Assert.Equal(((A5Model)row.DataContext!).Name, Assert.IsType<TextBlock>(cell.Content).Text);
            }
        }
        foreach (var row in factory.Created.OfType<TreeDataGridRowPresenter>().Where(r => !factory.Active.ContainsKey(r)))
        {
            Assert.Null(row.Parent); Assert.Null(row.DataContext); Assert.Null(row.Row);
        }
        Assert.All(factory.Active.Values, c => Assert.Same(grid.Source, c.Source));
    }
    private static void Sweep(GridControl grid, Window window)
    {
        for (var step = 0; step < 90; step++)
        {
            grid.RowsPresenter!.Offset = new(step * 137 % 1800, step * 701 % 20000); window.UpdateLayout();
        }
    }
    private static void Retired(A5RecordingFactory factory)
    {
        Assert.Empty(factory.Active); Assert.Empty(factory.Subscriptions); Assert.Equal(factory.Created.Count, factory.Released.Count);
    }

    [AvaloniaFact]
    public void Repeated_short_tall_scrolls_stop_creating_rows_but_keep_detached_factory_lifetimes()
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Models()); var factory = new A5RecordingFactory(); var grid = Create(source, factory);
        var window = A1TestHost.Show(grid, 1000, 650); var detached = 0;
        foreach (var row in grid.RowsPresenter!.RealizedRows) row.DetachedFromVisualTree += (_, _) => detached++;
        try
        {
            Sweep(grid, window); var created = grid.RowsPresenter.CreatedRowCount;
            Sweep(grid, window); AssertScene(grid, factory);
            Assert.Equal(created, grid.RowsPresenter.CreatedRowCount);
            Assert.True(detached > 0); Assert.True(factory.Recycled > 0);
            Assert.True(grid.RowsPresenter.RetainedRowCount - grid.RowsPresenter.RealizedRowCount > 2);
        }
        finally { window.Close(); }
        Retired(factory);
    }

    [AvaloniaTheory]
    [InlineData("width")]
    [InlineData("height")]
    [InlineData("source")]
    [InlineData("factory")]
    [InlineData("template")]
    [InlineData("fixed")]
    public void Spare_rows_are_bounded_and_released_across_viewport_and_owner_changes(string change)
    {
        var models = Models(); using var source = new FlatTreeDataGridSource<A5Model>(models);
        using var next = new FlatTreeDataGridSource<A5Model>([new("replacement")]);
        var factory = new A5RecordingFactory(); var replacement = new A5RecordingFactory(); var grid = Create(source, factory);
        var window = A1TestHost.Show(grid, 1000, 650);
        try
        {
            Sweep(grid, window); Assert.True(grid.RowsPresenter!.RetainedRowCount - grid.RowsPresenter.RealizedRowCount > 2);
            var released = factory.Released.Count;
            if (change == "width") window.Width = 420;
            else if (change == "height") window.Height = 180;
            else if (change == "source") grid.Source = next;
            else if (change == "factory") grid.ElementFactory = replacement;
            else if (change == "template") grid.Template = A1TestHost.Template();
            else grid.AutoRowHeight = false;
            window.UpdateLayout();
            // Fixed-height mode may consume the spare rows immediately; only
            // unused spares need releasing when its two-row reserve is restored.
            if (change != "fixed") Assert.True(factory.Released.Count > released);
            AssertScene(grid, change == "factory" ? replacement : factory);
            if (change == "fixed") Assert.InRange(grid.RowsPresenter!.RetainedRowCount - grid.RowsPresenter.RealizedRowCount, 0, 2);
            if (change == "source") Assert.All(models, m => Assert.Equal(1, m.Subscribers));
        }
        finally { window.Close(); }
        Retired(factory); Retired(replacement);
    }

    [AvaloniaFact]
    public void Declining_recycle_does_not_fill_the_automatic_height_reserve()
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Models()); var factory = new A5RecordingFactory { AllowRecycle = false }; var grid = Create(source, factory);
        var window = A1TestHost.Show(grid, 700, 400);
        try { Sweep(grid, window); Assert.Equal(grid.RowsPresenter!.RealizedRowCount, grid.RowsPresenter.RetainedRowCount); AssertScene(grid, factory); }
        finally { window.Close(); }
        Retired(factory);
    }

    [AvaloniaTheory]
    [InlineData("source")]
    [InlineData("factory")]
    public void Width_release_callbacks_can_replace_the_owner_without_releasing_new_lifetimes(string change)
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Models());
        using var next = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 100).Select(i => new A5Model("next " + i)));
        var factory = new A5RecordingFactory(); var replacement = new A5RecordingFactory(); var grid = Create(source, factory);
        var window = A1TestHost.Show(grid, 1000, 650); var changed = false;
        try
        {
            Sweep(grid, window); Assert.True(grid.RowsPresenter!.RetainedRowCount - grid.RowsPresenter.RealizedRowCount > 2);
            factory.Releasing = (_, _, _) =>
            {
                if (changed) return; changed = true;
                if (change == "source") grid.Source = next; else grid.ElementFactory = replacement;
                window.UpdateLayout();
            };
            window.Width = 420; window.UpdateLayout(); Assert.True(changed);
            AssertScene(grid, change == "source" ? factory : replacement);
        }
        finally { window.Close(); }
        Retired(factory); Retired(replacement);
    }

    [AvaloniaFact]
    public void Throwing_resize_release_cleans_the_whole_retired_batch_and_can_recover()
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Models()); var factory = new A5RecordingFactory(); var grid = Create(source, factory);
        var window = A1TestHost.Show(grid, 1000, 650); var fail = true;
        try
        {
            Sweep(grid, window);
            factory.Releasing = (_, _, _) => { if (fail) { fail = false; throw new InvalidOperationException("resize release"); } };
            window.Width = 420; var error = Record.Exception(window.UpdateLayout);
            Assert.NotNull(error); Assert.Contains("resize release", error.ToString());
            window.UpdateLayout(); AssertScene(grid, factory);
        }
        finally { window.Close(); }
        Retired(factory);
    }
}
