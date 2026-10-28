using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class DefaultScrollReuseTests
{
    private static GridControl Create(TreeDataGridSource source, int columns = 20)
    {
        var grid = new GridControl { Source = source, Template = A1TestHost.Template(), FrozenColumnCount = 2 };
        for (var i = 0; i < columns; i++) grid.Columns.Add(new TreeDataGridTextColumn { Header = "C" + i, Width = new GridLength(110), Binding = new ReflectionBinding(nameof(A5Model.Name)) });
        return grid;
    }
    private static void AssertScene(GridControl grid)
    {
        var rows = grid.RowsPresenter!;
        Assert.InRange(rows.RetainedRowCount, 1, (int)Math.Ceiling(rows.Viewport.Height / grid.RowHeight) + 5);
        foreach (var row in rows.RealizedRows)
        {
            Assert.Same(grid.Source!.Rows[row.RowIndex].Model, row.DataContext);
            Assert.Equal(row.RealizedCellCount, row.Children.OfType<TreeDataGridCellPresenter>().Count());
            foreach (var cell in row.RealizedCells)
            {
                Assert.Same(row, cell.Parent); Assert.Same(row.DataContext, cell.DataContext);
                Assert.Same(rows.Layout!.Columns[cell.ColumnIndex], cell.Column);
                if (cell.Content is TextBlock text) Assert.Equal(((A5Model)row.DataContext!).Name, text.Text);
            }
        }
    }

    [AvaloniaFact]
    public void Default_vertical_reuse_preserves_attached_hosts_but_pairs_old_and_new_cell_lifetimes()
    {
        var models = Enumerable.Range(0, 300).Select(i => new A5Model("row " + i)).ToArray();
        using var source = new FlatTreeDataGridSource<A5Model>(models); var grid = Create(source);
        var active = new HashSet<TreeDataGridCellPresenter>(); var cleared = 0;
        grid.CellPrepared += (_, e) => { Assert.True(active.Add(e.Container)); Assert.Same(e.Model, e.Container.DataContext); };
        grid.CellClearing += (_, e) => { Assert.True(active.Remove(e.Container)); Assert.NotNull(e.Container.Parent); Assert.Same(e.Model, e.Container.DataContext); Assert.Equal(((A5Model)e.Model).Name, Assert.IsType<TextBlock>(e.Container.Content).Text); cleared++; };
        var window = A1TestHost.Show(grid, 500, 230);
        var originals = grid.RowsPresenter!.RealizedRows.ToArray(); var cells = originals.SelectMany(r => r.RealizedCells).ToArray();
        var hostDetaches = 0;
        foreach (var element in originals.Cast<Control>().Concat(cells)) element.DetachedFromVisualTree += (_, _) => hostDetaches++;
        try
        {
            grid.RowsPresenter.Offset = new(0, 1400); window.UpdateLayout(); AssertScene(grid);
            Assert.Equal(originals.Length, originals.Count(r => grid.RowsPresenter.RealizedRows.Contains(r)));
            Assert.Equal(0, hostDetaches); Assert.True(cleared > 0); Assert.Equal(active.Count, grid.RowsPresenter.RealizedCellCount);
            Assert.All(models.Take(20), model => Assert.Equal(1, model.Subscribers));
        }
        finally { window.Close(); }
        Assert.Empty(active); Assert.All(models, model => Assert.Equal(1, model.Subscribers));
    }

    [AvaloniaFact]
    public void Default_horizontal_reuse_keeps_hosts_and_frozen_column_lifetimes_attached()
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 100).Select(i => new A5Model("row " + i)));
        var grid = Create(source); var window = A1TestHost.Show(grid, 500, 230);
        var cells = grid.RowsPresenter!.RealizedRows.SelectMany(r => r.RealizedCells).ToArray();
        var frozen = cells.Where(c => c.ColumnIndex < 2).Select(c => (Cell: c, Row: c.Row, Column: c.Column)).ToArray();
        var detached = 0; foreach (var cell in cells) cell.DetachedFromVisualTree += (_, _) => detached++;
        try
        {
            grid.RowsPresenter.Offset = new(800, 0); window.UpdateLayout(); AssertScene(grid);
            Assert.Equal(0, detached);
            Assert.All(frozen, item => { Assert.Same(item.Row, item.Cell.Row); Assert.Same(item.Column, item.Cell.Column); Assert.NotNull(item.Cell.Parent); });
            Assert.All(cells, cell => Assert.Contains(cell, grid.RowsPresenter.RealizedRows.SelectMany(r => r.RealizedCells)));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Horizontal_width_changes_and_viewport_shrink_detach_unused_cleared_hosts()
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 300).Select(i => new A5Model("row " + i)));
        var grid = Create(source, 30);
        for (var i = 2; i < 16; i++) grid.Columns[i].Width = new GridLength(25);
        grid.Columns[16].Width = new GridLength(1000); var window = A1TestHost.Show(grid, 600, 300);
        try
        {
            for (var step = 0; step < 6; step++)
            {
                grid.RowsPresenter!.Offset = new(step % 2 == 0 ? 900 : 0, 1000 + step * 600); window.UpdateLayout(); AssertScene(grid);
            }
            window.Width = 300; window.Height = 140; window.UpdateLayout(); AssertScene(grid);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Switching_factory_during_default_clearing_discards_inflight_hosts_and_honors_custom_hooks()
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 300).Select(i => new A5Model("row " + i)));
        var grid = Create(source); var factory = new A5RecordingFactory(); var changed = false;
        var window = A1TestHost.Show(grid);
        grid.CellClearing += (_, _) => { if (!changed) { changed = true; grid.ElementFactory = factory; } };
        try
        {
            grid.RowsPresenter!.Offset = new(500, 1800); window.UpdateLayout(); AssertScene(grid); Assert.True(changed);
            Assert.All(grid.RowsPresenter.RealizedRows.SelectMany(r => r.RealizedCells), cell => Assert.Equal("factory:Cell", cell.Tag));
            grid.RowsPresenter.Offset = new(900, 3000); window.UpdateLayout(); AssertScene(grid); Assert.True(factory.Recycled > 0);
        }
        finally { window.Close(); }
        Assert.Empty(factory.Active); Assert.Empty(factory.Subscriptions); Assert.Equal(factory.Created.Count, factory.Released.Count);
    }

    [AvaloniaFact]
    public void Source_change_during_row_clearing_cleans_the_unmapped_inflight_row()
    {
        var models = Enumerable.Range(0, 300).Select(i => new A5Model("old " + i)).ToArray();
        using var source = new FlatTreeDataGridSource<A5Model>(models);
        using var replacement = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 200).Select(i => new A5Model("new " + i)));
        var grid = Create(source); var changed = false; var window = A1TestHost.Show(grid);
        var retired = grid.RowsPresenter!.RealizedRows.ToArray();
        grid.RowClearing += (_, _) => { if (!changed) { changed = true; grid.Source = replacement; window.UpdateLayout(); } };
        try
        {
            grid.RowsPresenter.Offset = new(0, 1600); window.UpdateLayout(); AssertScene(grid); Assert.True(changed);
            Assert.All(retired, row => { Assert.Null(row.Parent); Assert.Null(row.DataContext); });
            Assert.All(models, model => Assert.Equal(1, model.Subscribers));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Nested_scroll_from_cell_clearing_does_not_duplicate_or_orphan_realized_rows()
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 500).Select(i => new A5Model("row " + i)));
        var grid = Create(source); var nested = false; var window = A1TestHost.Show(grid);
        grid.CellClearing += (_, _) => { if (!nested) { nested = true; grid.RowsPresenter!.Offset = new(600, 6000); window.UpdateLayout(); } };
        try
        {
            grid.RowsPresenter!.Offset = new(0, 1600); window.UpdateLayout(); AssertScene(grid); Assert.True(nested);
            Assert.Equal(grid.RowsPresenter.RealizedRowCount, grid.RowsPresenter.Children.OfType<TreeDataGridRowPresenter>().Count());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Throwing_clearing_observer_leaves_no_attached_spares_or_unpaired_lifetimes()
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 300).Select(i => new A5Model("row " + i)));
        var grid = Create(source); var active = new HashSet<TreeDataGridCellPresenter>(); var fail = false;
        grid.CellPrepared += (_, e) => Assert.True(active.Add(e.Container));
        grid.CellClearing += (_, e) => { Assert.True(active.Remove(e.Container)); if (fail) { fail = false; throw new InvalidOperationException("clearing observer"); } };
        var window = A1TestHost.Show(grid);
        try
        {
            fail = true; grid.RowsPresenter!.Offset = new(0, 1800);
            Assert.Contains("clearing observer", Assert.Throws<InvalidOperationException>(() => grid.RowsPresenter.Measure(new Size(491, 233))).Message);
            window.UpdateLayout(); AssertScene(grid); Assert.Equal(active.Count, grid.RowsPresenter.RealizedCellCount);
        }
        finally { window.Close(); }
        Assert.Empty(active);
    }

    [AvaloniaFact]
    public void An_offscreen_draft_survives_default_host_reuse_and_commits_to_its_original_model_once()
    {
        var models = Enumerable.Range(0, 300).Select(i => new A5Model("row " + i)).ToArray();
        using var source = new FlatTreeDataGridSource<A5Model>(models); var grid = Create(source); var window = A1TestHost.Show(grid);
        try
        {
            var id = source.Rows[0].Id; Assert.True(grid.BeginEdit(new(id, grid.Columns[0].Id))); var draft = grid.EditSession!; draft.Value = "draft";
            grid.RowsPresenter!.Offset = new(500, 1800); window.UpdateLayout(); Assert.Same(draft, grid.EditSession); Assert.Equal(0, models[0].Writes);
            Assert.True(grid.TryScrollIntoView(id)); window.UpdateLayout(); Assert.Same(draft, grid.EditSession);
            Assert.True(grid.CommitEdit()); Assert.Equal("draft", models[0].Name); Assert.Equal(1, models[0].Writes); Assert.All(models.Skip(1), m => Assert.Equal(0, m.Writes));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Wrapped_auto_height_reuse_agrees_with_the_detached_factory_path()
    {
        var models = Enumerable.Range(0, 1000).Select(i => new A5Model(i % 11 == 0 ? string.Join(' ', Enumerable.Repeat("自动行高 wrap", 12)) : "row " + i)).ToArray();
        using var source = new FlatTreeDataGridSource<A5Model>(models);
        var optimized = Create(source, 30); var detached = Create(source, 30); detached.ElementFactory = new DetachedFactory();
        foreach (var grid in new[] { optimized, detached })
        {
            grid.AutoRowHeight = true;
            foreach (var column in grid.Columns.Cast<TreeDataGridTextColumn>()) column.TextWrapping = TextWrapping.Wrap;
        }
        var a = A1TestHost.Show(optimized, 640, 360); var b = A1TestHost.Show(detached, 640, 360);
        try
        {
            for (var frame = 0; frame < 60; frame++)
            {
                var offset = new Vector(frame * 137 % 1800, frame * 701 % 20000);
                optimized.RowsPresenter!.Offset = offset; detached.RowsPresenter!.Offset = offset; a.UpdateLayout(); b.UpdateLayout();
                Assert.Equal(detached.RowsPresenter.RealizedRows.Select(r => r.RowIndex), optimized.RowsPresenter.RealizedRows.Select(r => r.RowIndex));
                foreach (var row in optimized.RowsPresenter.RealizedRows)
                    Assert.Equal(detached.RowsPresenter.Layout!.GetRowHeight(row.RowIndex), optimized.RowsPresenter.Layout!.GetRowHeight(row.RowIndex));
                AssertScene(optimized); AssertScene(detached);
            }
        }
        finally { a.Close(); b.Close(); }
    }
    [AvaloniaFact]
    public void Auto_height_and_fixed_height_both_reuse_default_hosts()
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 300).Select(i => new A5Model("row " + i)));
        var grid = Create(source); grid.AutoRowHeight = true; var window = A1TestHost.Show(grid, 500, 230);
        try
        {
            var detached = 0;
            foreach (var cell in grid.RowsPresenter!.RealizedRows.SelectMany(r => r.RealizedCells)) cell.DetachedFromVisualTree += (_, _) => detached++;
            grid.RowsPresenter.Offset = new(800, 1800); window.UpdateLayout(); AssertScene(grid); Assert.Equal(0, detached);
            grid.AutoRowHeight = false; window.UpdateLayout();
            var fixedDetaches = 0;
            foreach (var cell in grid.RowsPresenter.RealizedRows.SelectMany(r => r.RealizedCells)) cell.DetachedFromVisualTree += (_, _) => fixedDetaches++;
            grid.RowsPresenter.Offset = new(0, 4000); window.UpdateLayout(); AssertScene(grid); Assert.Equal(0, fixedDetaches);
        }
        finally { window.Close(); }
    }
    private sealed class DetachedFactory : TreeDataGridElementFactory;
}
