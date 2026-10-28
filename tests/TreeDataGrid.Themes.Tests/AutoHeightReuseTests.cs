using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Themes.Tests;

public sealed class AutoHeightReuseTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Wrapped_geometry_matches_detachment_through_scroll_refresh_and_template_changes(bool typed)
    {
        using var source = new FlatTreeDataGridSource<Model>(Enumerable.Range(0, 1000).Select(i => new Model
        { Name = i % 11 == 0 ? string.Join(' ', Enumerable.Repeat("自动行高 wrapped 文本", 12)) : "row " + i }));
        var a = Create(source, typed); var b = Create(source, typed); b.ElementFactory = new DetachedFactory();
        var wa = new Window { Content = a, Width = 1000, Height = 650 };
        var wb = new Window { Content = b, Width = 1000, Height = 650 };
        wa.Show(); wb.Show(); wa.UpdateLayout(); wb.UpdateLayout();
        var reused = new HashSet<TreeDataGridCellPresenter>();
        try
        {
            for (var step = 0; step < 90; step++)
            {
                var old = a.RowsPresenter!.RealizedRows.SelectMany(r => r.RealizedCells).ToHashSet();
                if (step % 9 == 0) source.Refresh();
                foreach (var grid in new[] { a, b })
                {
                    if (step == 20) grid.Columns[4].Width = new GridLength(80);
                    if (step == 40) grid.FontSize = 18;
                    if (step == 60) grid.CellTheme = null; // Force a template refresh through the theme variant below.
                    grid.RowsPresenter!.Offset = new(step * 137 % 1800, step * 701 % 20000);
                }
                if (step == 60) { wa.RequestedThemeVariant = ThemeVariant.Dark; wb.RequestedThemeVariant = ThemeVariant.Dark; }
                wa.UpdateLayout(); wb.UpdateLayout();
                reused.UnionWith(a.RowsPresenter.RealizedRows.SelectMany(r => r.RealizedCells).Where(old.Contains));
                Assert.Equal(b.RowsPresenter!.Offset, a.RowsPresenter.Offset);
                Assert.Equal(b.RowsPresenter.Extent, a.RowsPresenter.Extent);
                Assert.Equal(Geometry(b), Geometry(a));
                foreach (var row in a.RowsPresenter.RealizedRows)
                {
                    Assert.Same(source.Rows[row.RowIndex].Model, row.DataContext);
                    foreach (var cell in row.RealizedCells)
                    {
                        Assert.Same(row.DataContext, cell.DataContext);
                        Assert.Same(a.RowsPresenter.Layout!.Columns[cell.ColumnIndex], cell.Column);
                        Assert.Equal(((Model)row.DataContext!).Name, Assert.IsType<TextBlock>(cell.Content).Text);
                        Assert.Contains(cell.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>(), cp => ReferenceEquals(cp.Child, cell.Content));
                    }
                }
            }
            Assert.NotEmpty(reused);
        }
        finally { wa.Close(); wb.Close(); }
    }
    private static GridControl Create(TreeDataGridSource source, bool typed)
    {
        var grid = new GridControl { Source = source, AutoRowHeight = true, FrozenColumnCount = 2 };
        for (var i = 0; i < 100; i++) grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(110), TextWrapping = TextWrapping.Wrap,
            Binding = typed ? TreeDataGridBinding.Create<Model, string>(m => m.Name) : new ReflectionBinding(nameof(Model.Name)) });
        return grid;
    }
    private static object[] Geometry(GridControl grid) => grid.RowsPresenter!.RealizedRows.OrderBy(r => r.RowIndex)
        .Select(r => (object)(r.RowIndex, grid.RowsPresenter.Layout!.GetRowHeight(r.RowIndex), r.DesiredSize, r.Bounds,
            string.Join(';', r.RealizedCells.Select(c => $"{c.ColumnIndex}:{c.DesiredSize}:{c.Bounds}:{((TextBlock)c.Content!).DesiredSize}")))).ToArray();
    private sealed class Model { public string Name { get; init; } = ""; }
    private sealed class DetachedFactory : TreeDataGridElementFactory;
}
