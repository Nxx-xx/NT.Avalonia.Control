using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Themes.Tests;

public sealed class ColumnHeaderVisibilityTests
{
    [AvaloniaFact]
    public void Default_theme_collapses_the_entire_header_border_and_restores_frozen_alignment_in_both_variants()
    {
        using var source = new FlatTreeDataGridSource<string>(Enumerable.Range(0, 40).Select(i => "row " + i));
        var grid = new GridControl { Source = source, FrozenColumnCount = 1 };
        grid.Columns.Add(new TreeDataGridTextColumn { Header = "Auto header", Width = GridLength.Auto, ValueGetter = row => row });
        grid.Columns.Add(new TreeDataGridTextColumn { Header = "Scrolling", Width = new GridLength(600), ValueGetter = row => row });
        var window = new Window { Width = 450, Height = 280, Content = grid };
        window.Show(); window.UpdateLayout();
        try
        {
            var shownHeight = grid.RowsPresenter!.Viewport.Height;
            var width = grid.RowsPresenter.Layout!.GetColumnWidth(0);
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = variant;
                grid.ShowColumnHeaders = false; window.UpdateLayout();
                Assert.Empty(grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>());
                Assert.True(grid.RowsPresenter.Viewport.Height >= shownHeight + 28);
                Assert.Equal(width, grid.RowsPresenter.Layout!.GetColumnWidth(0), 4);
                grid.RowsPresenter.Offset = new Vector(120, 0); window.UpdateLayout();
                grid.ShowColumnHeaders = true; window.UpdateLayout();
                var header = grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Single(item => item.Column?.Id == grid.Columns[0].Id);
                var cell = grid.RowsPresenter.RealizedRows[0].RealizedCells.Single(item => item.ColumnIndex == 0);
                Assert.True(header.IsEffectivelyVisible);
                Assert.Equal(header.Bounds.X, cell.Bounds.X, 3);
                Assert.Equal(header.Bounds.Width, cell.Bounds.Width, 3);
                Assert.Equal(shownHeight, grid.RowsPresenter.Viewport.Height, 4);
            }
        }
        finally { window.Close(); }
    }
}
