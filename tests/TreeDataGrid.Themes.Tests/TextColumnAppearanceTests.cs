using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Themes.Tests;

public sealed class TextColumnAppearanceTests
{
    [AvaloniaFact]
    public void Text_layout_survives_light_dark_theme_changes_frozen_columns_and_horizontal_scrolling()
    {
        using var source = new FlatTreeDataGridSource<string>(["a long description that requires trimming in a narrow column"]);
        var grid = new GridControl { Source = source, FrozenColumnCount = 1 };
        grid.Columns.Add(new TreeDataGridTextColumn
        {
            Header = "Frozen", Width = new GridLength(120), ValueGetter = value => value,
            TextAlignment = TextAlignment.Right, TextTrimming = TextTrimming.None,
        });
        for (var i = 0; i < 6; i++)
            grid.Columns.Add(new TreeDataGridTextColumn
            {
                Header = $"Column {i}", Width = new GridLength(120), ValueGetter = value => value,
                TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.WordEllipsis,
            });
        var window = new Window { Width = 440, Height = 220, Content = grid };
        window.Show();
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = theme;
                window.UpdateLayout();
                foreach (var offset in new[] { 0, 260 })
                {
                    grid.RowsPresenter!.Offset = new Vector(offset, 0);
                    window.UpdateLayout();
                    var cells = grid.RowsPresenter.RealizedRows[0].RealizedCells;
                    Assert.Contains(cells, cell => cell.ColumnIndex == 0);
                    Assert.Contains(cells, cell => cell.ColumnIndex > 0);
                    foreach (var cell in cells)
                    {
                        Assert.Contains(cell.GetVisualDescendants().OfType<ContentPresenter>(), presenter => presenter.Name == "PART_ContentPresenter");
                        var column = (TreeDataGridTextColumn)cell.Column!;
                        var text = Assert.IsType<TextBlock>(cell.Content);
                        Assert.Equal(column.TextAlignment, text.TextAlignment);
                        Assert.Same(column.TextTrimming, text.TextTrimming);
                        Assert.Equal(cell.ColumnIndex > 0, text.TextLayout.TextLines[0].HasCollapsed);
                    }
                }
            }
        }
        finally { window.Close(); }
    }
}
