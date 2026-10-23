using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class LayoutTests
{
    [AvaloniaFact]
    public void Variable_heights_frozen_columns_and_hit_testing_share_geometry()
    {
        using var source = new FlatTreeDataGridSource<string>(["a", "b", "c"]);
        TreeDataGridColumn[] columns = [new TreeDataGridTextColumn { Width = new GridLength(100) },
            new TreeDataGridTextColumn { Width = new GridLength(120) }, new TreeDataGridTextColumn { Width = new GridLength(140) }];
        var layout = new TreeDataGridLayout(source.Rows, columns, 250, rowHeightSelector: row => row.Model.ToString() == "b" ? 40 : 20, frozenColumnCount: 1);
        Assert.Equal(new Size(360, 80), layout.Extent);
        var offset = new Vector(70, 10);
        Assert.Equal(new Rect(0, 10, 100, 40), layout.GetCellBounds(1, 0, offset));
        Assert.Equal(new Rect(30, 10, 120, 40), layout.GetCellBounds(1, 1, offset));
        Assert.True(layout.HitTest(new Point(50, 15), offset, new Size(250, 60), out var row, out var column));
        Assert.Equal(1, row);
        Assert.Equal(0, column); // frozen cell wins over the scrolling cell beneath it
        Assert.True(layout.HitTest(new Point(160, 15), offset, new Size(250, 60), out row, out column));
        Assert.Equal(2, column);
        Assert.Equal(1, layout.FindRow(20));
        Assert.False(layout.HitTest(new Point(-1, 0), offset, new Size(250, 60), out _, out _));
        Assert.False(layout.HitTest(new Point(250, 0), offset, new Size(250, 60), out _, out _));
    }

    [AvaloniaFact]
    public void Auto_star_limits_and_invalid_height_estimates_have_finite_geometry()
    {
        using var source = new FlatTreeDataGridSource<string>(["a", "b", "c"]);
        var fixedColumn = new TreeDataGridTextColumn { Width = new GridLength(10), MinWidth = 40 };
        var auto = new TreeDataGridTextColumn { Width = GridLength.Auto, EstimatedAutoWidth = 90, MaxWidth = 70 };
        var star = new TreeDataGridTextColumn { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 100 };
        var layout = new TreeDataGridLayout(source.Rows, [fixedColumn, auto, star], 400,
            rowHeightSelector: row => row.Model.ToString() == "a" ? double.NaN : -1);
        Assert.Equal(new Size(210, 84), layout.Extent);
        Assert.Equal(40, layout.GetColumnWidth(0));
        Assert.Equal(70, layout.GetColumnWidth(1));
        Assert.Equal(100, layout.GetColumnWidth(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => fixedColumn.MinWidth = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => auto.MaxWidth = 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TreeDataGridLayout(source.Rows, [], 100, rowHeight: 0));
        var empty = new TreeDataGridLayout([], [], double.PositiveInfinity, frozenColumnCount: 100);
        Assert.Equal(default, empty.Extent);
        Assert.Equal(-1, empty.FindRow(0));
    }
}
