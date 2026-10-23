using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Themes.Tests;

public sealed class P2P3IntegrationTests
{
    [AvaloniaFact]
    public void ThemedExpanderLoadsChildrenAndKeepsBindingAfterTemplateReplacement()
    {
        var root = new Item(0, "按需加载") { Unloaded = true };
        using var source = new HierarchicalTreeDataGridSource<Item>([root], item => item.Children,
            new HierarchicalTreeDataGridSourceOptions<Item>
            {
                HasChildrenSelector = item => item.Unloaded,
                IsExpandedSelector = item => item.IsExpanded,
                IsExpandedWriter = (item, value) => item.IsExpanded = value,
            });
        var loads = 0;
        source.RowExpanding += (_, args) =>
        {
            var item = (Item)args.Row.Model;
            if (!item.Unloaded) return;
            item.Children.Add(new Item(1, "加载的子节点"));
            item.Unloaded = false;
            loads++;
        };
        var grid = new GridControl { Source = source };
        grid.Columns.Add(new TreeDataGridHierarchicalExpanderColumn
        {
            Header = "名称", Width = new GridLength(300),
            InnerColumn = new TreeDataGridTextColumn { ValueGetter = model => ((Item)model).Name },
        });
        var window = Show(grid);
        try
        {
            var expander = grid.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("tree-data-grid-expander"));
            Assert.True(expander.IsHitTestVisible);
            expander.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();
            Assert.True(root.IsExpanded);
            Assert.Equal(2, source.Rows.Count);
            Assert.Equal(1, loads);
            window.RequestedThemeVariant = ThemeVariant.Dark;
            grid.Template = MinimalTemplate();
            window.UpdateLayout();
            root.Children.Add(new Item(2, "动态子节点"));
            window.UpdateLayout();
            Assert.Contains(grid.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "动态子节点");
            root.IsExpanded = false;
            window.UpdateLayout();
            Assert.Single(source.Rows);
            root.IsExpanded = true;
            window.UpdateLayout();
            Assert.Equal(3, source.Rows.Count);
            Assert.Equal(1, loads);
            Assert.Equal(3, grid.RowsPresenter!.RealizedRowCount);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DefaultThemeRendersAllDisplayColumnKindsAndResizesThroughThumb()
    {
        using var source = new FlatTreeDataGridSource<Item>([new Item(0, "alpha"), new Item(1, "beta"), new Item(2, "gamma")]);
        var grid = new GridControl { Source = source };
        grid.Columns.Add(new TreeDataGridRowHeaderColumn { Header = "#", Width = new GridLength(50) });
        var textColumn = new TreeDataGridTextColumn { Header = "名称", Width = new GridLength(160), MinWidth = 80, MaxWidth = 260, ValueGetter = model => ((Item)model).Name };
        grid.Columns.Add(textColumn);
        grid.Columns.Add(new TreeDataGridCheckBoxColumn { Header = "三态", Width = new GridLength(100), IsThreeState = true, ValueGetter = model => ((Item)model).Flag });
        grid.Columns.Add(new TreeDataGridTemplateColumn
        {
            Header = "模板", Width = new GridLength(160),
            CellTemplate = new FuncDataTemplate<Item>((item, _) => new TextBlock { Text = "badge:" + item!.Name }),
        });
        var window = Show(grid);
        try
        {
            var checks = grid.GetVisualDescendants().OfType<CheckBox>().ToArray();
            Assert.Equal(3, checks.Length);
            Assert.Contains(checks, check => check.IsChecked is null);
            Assert.Contains(checks, check => check.IsChecked == true);
            Assert.Contains(checks, check => check.IsChecked == false);
            Assert.All(checks, check => Assert.False(check.IsHitTestVisible));
            Assert.Contains(grid.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "badge:beta");
            var rowNumber = grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().First(cell => cell.Column is TreeDataGridRowHeaderColumn);
            Assert.Contains(rowNumber.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "1");
            var header = grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Single(control => ReferenceEquals(control.Column, textColumn));
            var thumb = header.GetVisualDescendants().OfType<Thumb>().Single(control => control.Name == "PART_ResizeThumb");
            var point = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(20, 0));
            window.UpdateLayout();
            window.MouseMove(point + new Vector(40, 0));
            window.UpdateLayout();
            window.MouseUp(point + new Vector(40, 0), MouseButton.Left);
            window.UpdateLayout();
            Assert.InRange(textColumn.Width.Value, 195, 205);
            Assert.Equal(header.Bounds.Width, grid.RowsPresenter!.Layout!.GetColumnWidth(1), 1);
            grid.CanUserResizeColumns = false;
            window.UpdateLayout();
            Assert.All(grid.GetVisualDescendants().OfType<Thumb>().Where(control => control.Name == "PART_ResizeThumb"), control => Assert.False(control.IsVisible));
            grid.CanUserResizeColumns = true;
            window.UpdateLayout();
            Assert.All(grid.GetVisualDescendants().OfType<Thumb>().Where(control => control.Name == "PART_ResizeThumb"), control => Assert.True(control.IsVisible));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ThemedHundredColumnsStayBoundedAndDisplayCorrectRecycledContent()
    {
        using var source = new FlatTreeDataGridSource<Item>(Enumerable.Range(0, 2000).Select(i => new Item(i, "row:" + i)).ToArray());
        var grid = new GridControl { Source = source, FrozenColumnCount = 2 };
        for (var i = 0; i < 100; i++)
        {
            var columnIndex = i;
            grid.Columns.Add(new TreeDataGridTextColumn { Header = "C" + i, Width = new GridLength(100), ValueGetter = model => ((Item)model).Name + ":C" + columnIndex });
        }
        var window = Show(grid);
        try
        {
            var before = grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().ToHashSet();
            grid.RowsPresenter!.Offset = new Vector(5000, 4000);
            window.UpdateLayout();
            var rows = grid.RowsPresenter.RealizedRows;
            Assert.InRange(rows.Count, 1, 25);
            Assert.All(rows, row => Assert.InRange(row.Children.OfType<TreeDataGridCellPresenter>().Count(), 1, 15));
            var cells = grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().ToArray();
            Assert.Contains(cells, before.Contains);
            Assert.Contains(cells, cell => cell.ColumnIndex == 0);
            Assert.Contains(cells, cell => cell.ColumnIndex > 40);
            Assert.All(cells, cell => Assert.Equal(((Item)cell.Row!.Model).Name + ":C" + cell.ColumnIndex, cell.Value));
            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.UpdateLayout();
            Assert.All(grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>(), cell => Assert.NotNull(cell.Background));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AutoStarAndWrappedRowsUseTheActualCellThemePadding()
    {
        using var source = new FlatTreeDataGridSource<Item>([new Item(0, "较长中文名称"), new Item(1, "短")]);
        var grid = new GridControl { Source = source, AutoRowHeight = true };
        grid.Columns.Add(new TreeDataGridTextColumn { Header = "自动列", Width = GridLength.Auto, MinWidth = 40, MaxWidth = 180, ValueGetter = model => ((Item)model).Name });
        grid.Columns.Add(new TreeDataGridTextColumn { Header = "换行列", Width = new GridLength(1, GridUnitType.Star), TextWrapping = TextWrapping.Wrap, ValueGetter = _ => string.Join(" ", Enumerable.Repeat("wrap", 70)) });
        var window = Show(grid);
        try
        {
            var layout = grid.RowsPresenter!.Layout!;
            Assert.InRange(layout.GetColumnWidth(0), 40, 180);
            Assert.True(layout.GetColumnWidth(1) > layout.GetColumnWidth(0));
            Assert.Contains(grid.RowsPresenter.RealizedRows, row => row.Bounds.Height > grid.RowHeight);
            var sourceVersion = source.Version;
            grid.CellTheme = new ControlTheme(typeof(TreeDataGridCellPresenter))
            {
                Setters = { new Setter(ContentControl.PaddingProperty, new Thickness(20, 8)) },
            };
            window.UpdateLayout();
            Assert.Equal(sourceVersion, source.Version);
            Assert.All(grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>(), cell => Assert.Equal(new Thickness(20, 8), cell.Padding));
        }
        finally { window.Close(); }
    }

    private static Window Show(Control content)
    {
        var window = new Window { Width = 720, Height = 380, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static FuncControlTemplate<GridControl> MinimalTemplate() => new((_, scope) =>
    {
        var rows = new TreeDataGridRowsPresenter();
        var scroll = new ScrollViewer { Content = rows };
        scope.Register("PART_ScrollViewer", scroll);
        scope.Register("PART_RowsPresenter", rows);
        return scroll;
    });

    private sealed class Item(int id, string name) : INotifyPropertyChanged
    {
        private bool _expanded;
        public string Name { get; } = name;
        public bool? Flag { get; } = id % 3 == 0 ? null : id % 3 == 1;
        public bool Unloaded { get; set; }
        public ObservableCollection<Item> Children { get; } = [];
        public bool IsExpanded
        {
            get => _expanded;
            set { if (_expanded == value) return; _expanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
