using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Themes.Tests;

public sealed class CellEventThemeTests
{
    [AvaloniaFact]
    public void Default_theme_lifetimes_stay_paired_across_variants_scrolling_and_template_reapplication()
    {
        using var source = new FlatTreeDataGridSource<string>(Enumerable.Range(0, 120).Select(i => "row " + i));
        var grid = new GridControl { Source = source, FrozenColumnCount = 1 };
        for (var i = 0; i < 12; i++) grid.Columns.Add(new TreeDataGridTextColumn { Header = "Column " + i, ValueGetter = row => row, Width = new(120) });
        var active = new HashSet<TreeDataGridCellPresenter>(); var prepared = 0; var clearing = 0; var values = 0;
        grid.CellPrepared += (_, e) => { Assert.True(active.Add(e.Container)); Assert.Same(e.Model, e.Container.DataContext); prepared++; };
        grid.CellClearing += (_, e) => { Assert.True(active.Remove(e.Container)); Assert.Same(e.Model, e.Container.DataContext); clearing++; };
        grid.CellValueChanged += (_, _) => values++;
        var window = new Window { Width = 460, Height = 300, Content = grid }; window.Show(); window.UpdateLayout();
        try
        {
            foreach (var variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
            {
                window.RequestedThemeVariant = variant; window.UpdateLayout();
                grid.RowsPresenter!.Offset = new Vector(600, variant == ThemeVariant.Dark ? 2000 : 0); window.UpdateLayout();
                grid.ShowColumnHeaders = !grid.ShowColumnHeaders; window.UpdateLayout();
                grid.GridLinesVisibility = variant == ThemeVariant.Dark ? TreeDataGridGridLinesVisibility.None : TreeDataGridGridLinesVisibility.All; window.UpdateLayout();
                Assert.Equal(grid.RowsPresenter.RealizedCellCount, active.Count);
            }
            window.Content = null; Assert.Empty(active); Assert.Equal(prepared, clearing); Assert.Equal(0, values);
        }
        finally { window.Close(); }
    }
}
