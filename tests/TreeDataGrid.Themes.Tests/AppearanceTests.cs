using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using NT.AvaloniaControl.TreeDataGrid.Themes;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Themes.Tests;

public sealed class AppearanceTests
{
    [AvaloniaFact]
    public void GridLinesUpdateEveryRealizedCellAcrossThemesAndFrozenBoundary()
    {
        using var source = new FlatTreeDataGridSource<string>(["first", "second"]);
        var grid = CreateGrid(source);
        grid.FrozenColumnCount = 1;
        var window = Show(grid);
        try
        {
            Assert.Equal(TreeDataGridGridLinesVisibility.All, grid.GridLinesVisibility);
            var identities = source.Rows.Select(row => row.Id).ToArray();
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = variant;
                foreach (var mode in Enum.GetValues<TreeDataGridGridLinesVisibility>())
                {
                    grid.GridLinesVisibility = mode;
                    window.UpdateLayout();
                    AssertLines(grid, mode);
                    grid.RowsPresenter!.Offset = new Vector(160, 0);
                    window.UpdateLayout();
                    AssertLines(grid, mode);
                }
            }
            Assert.Equal(identities, source.Rows.Select(row => row.Id));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CustomCellThemeRetainsBorderAuthorityForAllGridLineModes()
    {
        using var source = new FlatTreeDataGridSource<string>(["custom"]);
        var border = new Thickness(3, 4, 5, 6);
        var grid = CreateGrid(source);
        grid.CellTheme = new ControlTheme(typeof(TreeDataGridCellPresenter))
        {
            Setters = { new Setter(TemplatedControl.BorderThicknessProperty, border) },
        };
        var window = Show(grid);
        try
        {
            foreach (var mode in Enum.GetValues<TreeDataGridGridLinesVisibility>())
            {
                grid.GridLinesVisibility = mode;
                window.UpdateLayout();
                Assert.All(Cells(grid), cell => Assert.Equal(border, cell.BorderThickness));
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RemovingHorizontalLineRecalculatesNaturalRowHeight()
    {
        using var source = new FlatTreeDataGridSource<string>(["tall"]);
        var grid = CreateGrid(source);
        grid.AutoRowHeight = true;
        grid.RowHeight = 1;
        grid.Columns.Clear();
        grid.Columns.Add(new TreeDataGridTemplateColumn
        {
            Width = new GridLength(200), ValueGetter = item => item,
            CellTemplate = new FuncDataTemplate<string>((_, _) => new Border { Height = 80 }),
        });
        var window = Show(grid);
        try
        {
            var withLines = grid.RowsPresenter!.Layout!.GetRowHeight(0);
            grid.GridLinesVisibility = TreeDataGridGridLinesVisibility.None;
            window.UpdateLayout();
            var withoutLines = grid.RowsPresenter.Layout!.GetRowHeight(0);
            Assert.InRange(withLines - withoutLines, 0.9, 1.1);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ConfigurableEmptyContentTracksFilteringAndDataReturning()
    {
        var items = new ObservableCollection<string>();
        using var source = new FlatTreeDataGridSource<string>(items);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            var empty = EmptyPresenter(grid);
            Assert.Equal("暂无数据", grid.EmptyContent);
            Assert.Null(grid.EmptyContentTemplate);
            Assert.True(empty.IsVisible);
            Assert.Equal("暂无数据", empty.Content);
            grid.EmptyContent = "无符合条件的记录";
            window.UpdateLayout();
            Assert.Equal(grid.EmptyContent, empty.Content);
            Assert.Contains(empty.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "无符合条件的记录");

            items.Add("visible");
            window.UpdateLayout();
            Assert.False(empty.IsVisible);
            source.SetFilter(_ => false);
            window.UpdateLayout();
            Assert.True(empty.IsVisible);
            source.SetFilter(null);
            window.UpdateLayout();
            Assert.False(empty.IsVisible);
            grid.Source = null;
            window.UpdateLayout();
            Assert.True(empty.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void EmptyTemplateReceivesConfiguredObjectAndSurvivesThemeSwitch()
    {
        using var source = new FlatTreeDataGridSource<string>([]);
        var model = new EmptyModel("重新选择筛选条件");
        var grid = CreateGrid(source);
        grid.EmptyContent = model;
        grid.EmptyContentTemplate = new FuncDataTemplate<EmptyModel>((value, _) =>
            new TextBlock { Name = "CustomEmptyText", Text = value?.Message });
        var window = Show(grid);
        try
        {
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = variant;
                window.UpdateLayout();
                var empty = EmptyPresenter(grid);
                Assert.True(empty.IsVisible);
                Assert.Same(model, empty.Content);
                Assert.Same(grid.EmptyContentTemplate, empty.ContentTemplate);
                Assert.Equal(model.Message, empty.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "CustomEmptyText").Text);
            }
            grid.EmptyContent = null;
            grid.EmptyContentTemplate = null;
            window.UpdateLayout();
            Assert.Null(EmptyPresenter(grid).Content);
            Assert.DoesNotContain(grid.GetVisualDescendants().OfType<TextBlock>(), text => text.Name == "CustomEmptyText");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AppearancePropertiesWorkWithApplicationTemplateWithoutOptionalTheme()
    {
        var styles = Application.Current!.Styles;
        var builtIn = styles.OfType<TreeDataGridTheme>().Single();
        styles.Remove(builtIn);
        using var source = new FlatTreeDataGridSource<string>([]);
        var grid = CreateGrid(source);
        grid.EmptyContent = "应用提供的空内容";
        grid.GridLinesVisibility = TreeDataGridGridLinesVisibility.Horizontal;
        grid.Template = new FuncControlTemplate<GridControl>((_, scope) =>
        {
            var rows = new TreeDataGridRowsPresenter();
            var scroll = new ScrollViewer { Content = rows };
            scope.Register("PART_ScrollViewer", scroll);
            scope.Register("PART_RowsPresenter", rows);
            var empty = new ContentPresenter { Name = "ApplicationEmptyContent" };
            empty.Bind(ContentPresenter.ContentProperty, new TemplateBinding(GridControl.EmptyContentProperty));
            empty.Bind(ContentPresenter.ContentTemplateProperty, new TemplateBinding(GridControl.EmptyContentTemplateProperty));
            return new Grid { Children = { scroll, empty } };
        });
        Window? window = null;
        try
        {
            window = Show(grid);
            Assert.NotNull(grid.RowsPresenter);
            var empty = grid.GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Name == "ApplicationEmptyContent");
            Assert.Equal(grid.EmptyContent, empty.Content);
            grid.EmptyContent = "更新后的空内容";
            window.UpdateLayout();
            Assert.Equal(grid.EmptyContent, empty.Content);
        }
        finally { window?.Close(); styles.Add(builtIn); }
    }

    private static void AssertLines(GridControl grid, TreeDataGridGridLinesVisibility mode)
    {
        var expected = mode switch
        {
            TreeDataGridGridLinesVisibility.None => default,
            TreeDataGridGridLinesVisibility.Horizontal => new Thickness(0, 0, 0, 1),
            TreeDataGridGridLinesVisibility.Vertical => new Thickness(0, 0, 1, 0),
            _ => new Thickness(0, 0, 1, 1),
        };
        var cells = Cells(grid);
        Assert.NotEmpty(cells);
        Assert.All(cells, cell => Assert.Equal(expected, cell.BorderThickness));
        Assert.Contains(cells, cell => cell.ColumnIndex == 0);
        Assert.Contains(cells, cell => cell.ColumnIndex > 0);
    }

    private static TreeDataGridCellPresenter[] Cells(GridControl grid) =>
        grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().ToArray();
    private static ContentPresenter EmptyPresenter(GridControl grid) =>
        grid.GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Name == "PART_EmptyMessage");
    private static GridControl CreateGrid(TreeDataGridSource source)
    {
        var grid = new GridControl { Source = source };
        for (var i = 0; i < 6; i++)
            grid.Columns.Add(new TreeDataGridTextColumn { Header = $"Column {i}", Width = new GridLength(140), ValueGetter = item => item });
        return grid;
    }
    private static Window Show(Control content)
    {
        var window = new Window { Width = 500, Height = 280, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }
    private sealed record EmptyModel(string Message);
}
