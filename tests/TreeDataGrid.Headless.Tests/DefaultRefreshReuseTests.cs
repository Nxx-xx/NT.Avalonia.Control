using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class DefaultRefreshReuseTests
{
    private static GridControl Create(TreeDataGridSource source)
    {
        var grid = new GridControl { Source = source, Template = A1TestHost.Template(), FrozenColumnCount = 1 };
        for (var i = 0; i < 12; i++) grid.Columns.Add(new TreeDataGridTextColumn
        { Header = "C" + i, Width = new GridLength(110), Binding = new ReflectionBinding(nameof(A5Model.Name)) });
        return grid;
    }

    private static void AssertScene(GridControl grid)
    {
        var presenter = grid.RowsPresenter!;
        Assert.Equal(presenter.RealizedRowCount, presenter.Children.OfType<TreeDataGridRowPresenter>().Count());
        Assert.InRange(presenter.RetainedRowCount, 1, (int)Math.Ceiling(presenter.Viewport.Height / grid.RowHeight) + 5);
        foreach (var row in presenter.RealizedRows)
        {
            Assert.Same(grid.Source!.Rows[row.RowIndex].Model, row.DataContext);
            Assert.Equal(row.RealizedCellCount, row.Children.OfType<TreeDataGridCellPresenter>().Count());
            foreach (var cell in row.RealizedCells)
            {
                Assert.Same(row, cell.Parent); Assert.Same(row.DataContext, cell.DataContext);
                Assert.Same(presenter.Layout!.Columns[cell.ColumnIndex], cell.Column);
                if (cell.Content is TextBlock text) Assert.Equal(((A5Model)row.DataContext!).Name, text.Text);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Source_refresh_clears_bindings_synchronously_and_reprepares_attached_default_hosts(bool autoHeight)
    {
        var models = Enumerable.Range(0, 100).Select(i => new A5Model("row " + i)).ToArray();
        using var source = new FlatTreeDataGridSource<A5Model>(models); var grid = Create(source); grid.AutoRowHeight = autoHeight;
        var active = new HashSet<TreeDataGridCellPresenter>();
        grid.CellPrepared += (_, e) => Assert.True(active.Add(e.Container));
        grid.CellClearing += (_, e) => { Assert.True(active.Remove(e.Container)); Assert.Same(e.Model, e.Container.DataContext); Assert.NotNull(e.Container.Content); };
        var window = A1TestHost.Show(grid); var rows = grid.RowsPresenter!.RealizedRows.ToArray();
        var cells = rows.SelectMany(r => r.RealizedCells).ToArray(); var detaches = 0;
        foreach (var host in rows.Cast<Control>().Concat(cells)) host.DetachedFromVisualTree += (_, _) => detaches++;
        try
        {
            source.Refresh(); Assert.Empty(active);
            Assert.All(cells, c => { Assert.Null(c.DataContext); Assert.Null(c.Content); });
            Assert.All(models, m => Assert.Equal(1, m.Subscribers));
            window.UpdateLayout(); AssertScene(grid); Assert.Equal(0, detaches);
            Assert.All(rows, r => Assert.Contains(r, grid.RowsPresenter.RealizedRows));
            Assert.Equal(grid.RowsPresenter.RealizedCellCount, active.Count);
        }
        finally { window.Close(); }
        Assert.Empty(active); Assert.All(models, m => Assert.Equal(1, m.Subscribers));
    }

    [AvaloniaFact]
    public void Repeated_navigation_cancel_refresh_and_commit_write_only_the_logical_target_once()
    {
        var models = Enumerable.Range(0, 100).Select(i => new A5Model("row " + i)).ToArray();
        using var source = new FlatTreeDataGridSource<A5Model>(models); var grid = Create(source);
        var commits = new List<CellAddress>(); grid.CellValueChanged += (_, e) => { if (e.Reason == TreeDataGridCellValueChangeReason.EditCommit) commits.Add(e.Cell); };
        var window = A1TestHost.Show(grid);
        try
        {
            for (var i = 0; i < 25; i++)
            {
                var address = new CellAddress(source.Rows[i].Id, grid.Columns[0].Id);
                Assert.True(grid.BeginEdit(address)); grid.EditSession!.Value = "cancelled"; grid.CancelEdit(); Assert.Equal(0, models[i].Writes);
                Assert.True(grid.BeginEdit(address)); var draft = grid.EditSession!; draft.Value = "edited " + i;
                source.Refresh(); window.UpdateLayout(); Assert.Same(draft, grid.EditSession);
                Assert.True(grid.CommitEdit()); window.UpdateLayout(); AssertScene(grid);
                Assert.Equal("edited " + i, models[i].Name); Assert.Equal(1, models[i].Writes); Assert.Equal(address, commits[i]);
            }
            Assert.Equal(25, commits.Count); Assert.All(models.Skip(25), m => Assert.Equal(0, m.Writes));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("source", false)]
    [InlineData("factory", false)]
    [InlineData("template", false)]
    [InlineData("close", false)]
    [InlineData("source", true)]
    [InlineData("factory", true)]
    [InlineData("template", true)]
    [InlineData("close", true)]
    public void Pending_refresh_hosts_are_released_when_the_owner_changes_before_layout(string change, bool autoHeight)
    {
        var models = Enumerable.Range(0, 100).Select(i => new A5Model("old " + i)).ToArray();
        using var source = new FlatTreeDataGridSource<A5Model>(models);
        using var replacement = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 50).Select(i => new A5Model("new " + i)));
        var grid = Create(source); grid.AutoRowHeight = autoHeight; var window = A1TestHost.Show(grid); var factory = new A5RecordingFactory();
        var retired = grid.RowsPresenter!.RealizedRows.ToArray(); var cells = retired.SelectMany(r => r.RealizedCells).ToArray();
        try
        {
            source.Refresh();
            if (change == "source") grid.Source = replacement;
            else if (change == "factory") grid.ElementFactory = factory;
            else if (change == "template") grid.Template = A1TestHost.Template();
            else window.Close();
            window.UpdateLayout();
            Assert.All(retired, r => { Assert.Null(r.Parent); Assert.Null(r.DataContext); });
            Assert.All(cells, c => { Assert.Null(c.Parent); Assert.Null(c.DataContext); });
            if (change != "close") AssertScene(grid);
            if (change is "source" or "close") Assert.All(models, m => Assert.Equal(1, m.Subscribers));
        }
        finally { window.Close(); }
        Assert.Empty(factory.Active); Assert.Empty(factory.Subscriptions); Assert.Equal(factory.Created.Count, factory.Released.Count);
        Assert.All(models, m => Assert.Equal(1, m.Subscribers));
    }

    [AvaloniaTheory]
    [InlineData("source", false)]
    [InlineData("factory", false)]
    [InlineData("refresh", false)]
    [InlineData("layout", false)]
    [InlineData("source", true)]
    [InlineData("factory", true)]
    [InlineData("refresh", true)]
    [InlineData("layout", true)]
    public void Refresh_clearing_reentry_leaves_only_current_mapped_hosts(string change, bool autoHeight)
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 100).Select(i => new A5Model("old " + i)));
        using var replacement = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 50).Select(i => new A5Model("new " + i)));
        var grid = Create(source); grid.AutoRowHeight = autoHeight; var factory = new A5RecordingFactory(); var window = A1TestHost.Show(grid); var changed = false;
        grid.CellClearing += (_, _) =>
        {
            if (changed) return; changed = true;
            if (change == "source") grid.Source = replacement;
            else if (change == "factory") grid.ElementFactory = factory;
            else if (change == "refresh") source.Refresh();
            window.UpdateLayout();
        };
        try { source.Refresh(); window.UpdateLayout(); Assert.True(changed); AssertScene(grid); }
        finally { window.Close(); }
        Assert.Empty(factory.Active); Assert.Empty(factory.Subscriptions); Assert.Equal(factory.Created.Count, factory.Released.Count);
    }

    [AvaloniaFact]
    public void A_clearing_failure_does_not_leave_attached_unmapped_hosts_after_recovery()
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 100).Select(i => new A5Model("row " + i)));
        var grid = Create(source); var active = new HashSet<TreeDataGridCellPresenter>(); var fail = false;
        grid.CellPrepared += (_, e) => Assert.True(active.Add(e.Container));
        grid.CellClearing += (_, e) => { Assert.True(active.Remove(e.Container)); if (fail) { fail = false; throw new InvalidOperationException("refresh clearing"); } };
        var window = A1TestHost.Show(grid);
        try
        {
            fail = true; var error = Record.Exception(source.Refresh); Assert.NotNull(error); Assert.Contains("refresh clearing", error.ToString());
            window.UpdateLayout(); AssertScene(grid); Assert.Equal(grid.RowsPresenter!.RealizedCellCount, active.Count);
        }
        finally { window.Close(); }
        Assert.Empty(active);
    }

    [AvaloniaFact]
    public void Filtering_to_empty_detaches_cleared_spares_before_the_next_render()
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 100).Select(i => new A5Model("row " + i)));
        var grid = Create(source); var window = A1TestHost.Show(grid); var retired = grid.RowsPresenter!.RealizedRows.ToArray();
        try
        {
            source.SetFilter(_ => false); window.UpdateLayout();
            Assert.Empty(grid.RowsPresenter.RealizedRows); Assert.Empty(grid.RowsPresenter.Children.OfType<TreeDataGridRowPresenter>());
            Assert.All(retired, r => { Assert.Null(r.Parent); Assert.Null(r.DataContext); });
            Assert.InRange(grid.RowsPresenter.RetainedRowCount, 0, 2);
            source.SetFilter(null); window.UpdateLayout(); AssertScene(grid);
        }
        finally { window.Close(); }
    }
}
