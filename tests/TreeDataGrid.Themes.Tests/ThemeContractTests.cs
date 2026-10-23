using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using NT.AvaloniaControl.TreeDataGrid.Themes;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Themes.Tests;

public sealed class ThemeContractTests
{
    [Fact]
    public void ProductAssembliesHaveOneWayDependency()
    {
        Assert.DoesNotContain(typeof(GridControl).Assembly.GetReferencedAssemblies(), name => name.Name == "NT.AvaloniaControl.TreeDataGrid.Themes");
        Assert.Contains(typeof(TreeDataGridTheme).Assembly.GetReferencedAssemblies(), name => name.Name == "NT.AvaloniaControl.TreeDataGrid");
    }

    [AvaloniaFact]
    public void PublicResourceUriCompilesAndLoadsFromAnotherAssembly()
    {
        var styles = Application.Current!.Styles;
        var builtIn = styles.OfType<TreeDataGridTheme>().Single();
        var uriFixture = new UriThemeFixture();
        styles.Remove(builtIn);
        styles.Add(uriFixture);
        using var source = new FlatTreeDataGridSource<Item>([new Item("URI 主题入口")]);
        Window? window = null;
        try
        {
            var grid = CreateGrid(source);
            window = Show(grid);
            Assert.Equal(1, grid.RowsPresenter!.RealizedRowCount);
            Assert.Contains(grid.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "URI 主题入口");
        }
        finally { window?.Close(); styles.Remove(uriFixture); styles.Add(builtIn); }
    }

    [AvaloniaFact]
    public void DefaultThemeLoadsAndEmptyStateTracksSourceChanges()
    {
        var items = new ObservableCollection<Item>();
        using var source = new FlatTreeDataGridSource<Item>(items);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            Assert.NotNull(grid.RowsPresenter);
            Assert.Contains(grid.GetVisualDescendants(), visual => visual is TreeDataGridColumnHeadersPresenter);
            var empty = grid.GetVisualDescendants().OfType<ContentPresenter>().Single(text => text.Name == "PART_EmptyMessage");
            Assert.True(empty.IsVisible);
            items.Add(new Item("中文样例"));
            window.UpdateLayout();
            Assert.False(empty.IsVisible);
            Assert.Equal(1, grid.RowsPresenter.RealizedRowCount);
            Assert.Contains(grid.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "中文样例");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ThemeAndTemplateChangesPreserveSourceIdentityAndExpansion()
    {
        var root = new Item("根") { Children = [new Item("子节点")] };
        using var source = new HierarchicalTreeDataGridSource<Item>([root], item => item.Children);
        Assert.True(source.TryGetNodeId(new IndexPath(0), out var rootId));
        source.SetExpanded(rootId, true);
        var ids = source.Rows.Select(row => row.Id).ToArray();
        var version = source.Version;
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            window.RequestedThemeVariant = ThemeVariant.Light;
            window.UpdateLayout();
            var lightColor = Assert.IsAssignableFrom<ISolidColorBrush>(grid.Background).Color;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.UpdateLayout();
            var darkColor = Assert.IsAssignableFrom<ISolidColorBrush>(grid.Background).Color;
            Assert.NotEqual(lightColor, darkColor);
            var oldPresenter = grid.RowsPresenter;
            grid.Template = CustomTemplate();
            grid.ApplyTemplate();
            window.UpdateLayout();
            Assert.NotSame(oldPresenter, grid.RowsPresenter);
            Assert.Equal(0, oldPresenter!.RealizedRowCount);
            Assert.Same(source, grid.Source);
            Assert.Equal(version, source.Version);
            Assert.Equal(ids, source.Rows.Select(row => row.Id));
            Assert.True(source.Rows[0].IsExpanded);
            Assert.Equal(2, grid.RowsPresenter!.RealizedRowCount);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ThemeAndTemplateChangesPreserveApplicationOwnedStateAndCommittedValue()
    {
        var first = new Item("初始值");
        using var source = new FlatTreeDataGridSource<Item>([first, new Item("第二行")]);
        using var state = new TreeDataGridState(source);
        var grid = CreateGrid(source);
        var cell = new CellAddress(source.Rows[0].Id, grid.Columns[0].Id);
        var selectedNodes = source.Rows.Select(row => row.Id).ToArray();
        state.Select(selectedNodes[0]);
        state.Select(selectedNodes[1], additive: true);
        var writes = 0;
        void Write(object model, object? value) { ((Item)model).Name = (string)value!; writes++; }
        state.BeginEdit(cell, model => ((Item)model).Name, Write);
        Assert.True(state.CommitEdit("已提交值"));
        state.BeginEdit(cell, model => ((Item)model).Name, Write);
        var activeSession = state.EditSession;
        var version = source.Version;
        var stateChanges = 0;
        state.Changed += (_, _) => stateChanges++;
        var window = Show(grid);
        try
        {
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = variant;
                window.UpdateLayout();
                AssertApplicationState();
            }
            grid.Template = CustomTemplate();
            grid.ApplyTemplate();
            window.UpdateLayout();
            AssertApplicationState();
            Assert.Contains(grid.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "已提交值");
        }
        finally { window.Close(); }

        void AssertApplicationState()
        {
            Assert.Same(source, grid.Source);
            Assert.Equal(version, source.Version);
            Assert.Equal(2, state.SelectedNodes.Count);
            Assert.All(selectedNodes, id => Assert.Contains(id, state.SelectedNodes));
            Assert.Equal(cell, state.CurrentCell);
            Assert.Same(activeSession, state.EditSession);
            Assert.Equal("已提交值", state.EditSession!.OriginalValue);
            Assert.Equal("已提交值", first.Name);
            Assert.Equal(1, writes);
            Assert.Equal(0, stateChanges);
        }
    }

    [AvaloniaFact]
    public void ApplicationCanSupplyTemplateWithoutBuiltInTreeGridTheme()
    {
        var styles = Application.Current!.Styles;
        var builtIn = styles.OfType<TreeDataGridTheme>().Single();
        styles.Remove(builtIn);
        using var source = new FlatTreeDataGridSource<Item>([new Item("应用模板")]);
        var grid = CreateGrid(source);
        grid.Template = CustomTemplate();
        Window? window = null;
        try
        {
            window = Show(grid);
            Assert.Equal(1, grid.RowsPresenter!.RealizedRowCount);
            Assert.DoesNotContain(grid.GetVisualDescendants(), visual => visual is TreeDataGridColumnHeadersPresenter);
            Assert.Contains(grid.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "应用模板");
        }
        finally { window?.Close(); styles.Add(builtIn); }
    }

    [AvaloniaFact]
    public void MissingScrollViewerReportsRequiredNameAndType()
    {
        var grid = new GridControl { Template = new FuncControlTemplate<GridControl>((_, _) => new Border()) };
        var exception = Assert.Throws<InvalidOperationException>(() => grid.ApplyTemplate());
        Assert.Contains("PART_ScrollViewer", exception.Message);
        Assert.Contains(nameof(ScrollViewer), exception.Message);
    }

    [AvaloniaFact]
    public void MissingRowsPresenterReportsRequiredNameAndType()
    {
        var grid = new GridControl
        {
            Template = new FuncControlTemplate<GridControl>((_, scope) =>
            {
                var scroll = new ScrollViewer();
                scope.Register("PART_ScrollViewer", scroll);
                return scroll;
            }),
        };
        var exception = Assert.Throws<InvalidOperationException>(() => grid.ApplyTemplate());
        Assert.Contains("PART_RowsPresenter", exception.Message);
        Assert.Contains(nameof(TreeDataGridRowsPresenter), exception.Message);
    }

    private static GridControl CreateGrid(TreeDataGridSource source)
    {
        var grid = new GridControl { Source = source };
        grid.Columns.Add(new TreeDataGridTextColumn { Header = "名称", Width = new GridLength(200), ValueGetter = item => ((Item)item).Name });
        return grid;
    }

    private static FuncControlTemplate<GridControl> CustomTemplate() => new((_, scope) =>
    {
        var rows = new TreeDataGridRowsPresenter();
        var scroll = new ScrollViewer { Content = rows };
        scope.Register("PART_ScrollViewer", scroll);
        scope.Register("PART_RowsPresenter", rows);
        return scroll;
    });

    private static Window Show(Control control)
    {
        var window = new Window { Width = 600, Height = 350, Content = control };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private sealed class Item(string name)
    {
        public string Name { get; set; } = name;
        public ObservableCollection<Item> Children { get; init; } = [];
    }
}
