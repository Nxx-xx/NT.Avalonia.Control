using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private void ConfigureTreeColumns()
    {
        _grid.Source = null;
        _grid.Columns.Clear();
        _grid.Columns.Add(new TreeDataGridRowHeaderColumn { Header = "行号", Width = new GridLength(64), CanUserSort = false });
        _grid.Columns.Add(new TreeDataGridHierarchicalExpanderColumn
        {
            Header = "部门 / 成员", Width = new GridLength(260),
            InnerColumn = new TreeDataGridTextColumn { ValueGetter = model => ((DemoItem)model).Name },
        });
        _grid.Columns.Add(new TreeDataGridCheckBoxColumn { Header = "三态展示", Width = new GridLength(100), IsThreeState = true, ValueGetter = model => ((DemoItem)model).Enabled });
        _grid.Columns.Add(new TreeDataGridTextColumn { Header = "编号（Auto）", Width = GridLength.Auto, MaxWidth = 200, ValueGetter = model => ((DemoItem)model).Id, StringFormat = "{0:N0}" });
        _grid.Columns.Add(CreateBadgeColumn());
        _grid.Columns.Add(new TreeDataGridTextColumn { Header = "说明（2*）", Width = new GridLength(2, GridUnitType.Star), MinWidth = 220, ValueGetter = model => ((DemoItem)model).Description });
    }

    private static TreeDataGridTemplateColumn CreateBadgeColumn() => new()
    {
        Header = "模板列（*）", Width = new GridLength(1, GridUnitType.Star), MinWidth = 140,
        CanUserSort = true,
        Comparison = (left, right) => Nullable.Compare(((DemoItem)left).Enabled, ((DemoItem)right).Enabled),
        CellTemplate = new FuncDataTemplate<DemoItem>((item, _) => new Border
        {
            BorderBrush = Brushes.SteelBlue, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Child = new TextBlock { Text = item?.Enabled switch { true => "已启用", false => "未启用", _ => "待确认" } },
        }),
    };

    private void ShowColumnTypes()
    {
        _grid.Source = null;
        _treeRoots = null;
        _grid.RowHeightSelector = null;
        _grid.FrozenColumnCount = 1;
        _grid.AutoRowHeight = false;
        _grid.Columns.Clear();
        _grid.Columns.Add(new TreeDataGridRowHeaderColumn { Header = "行号", Width = new GridLength(64), CanUserSort = false });
        _grid.Columns.Add(new TreeDataGridTextColumn { Header = "名称（Auto）", Width = GridLength.Auto, MinWidth = 120, MaxWidth = 280, ValueGetter = model => ((DemoItem)model).Name });
        _grid.Columns.Add(new TreeDataGridCheckBoxColumn { Header = "三态展示", Width = new GridLength(100), IsThreeState = true, ValueGetter = model => ((DemoItem)model).Enabled });
        _grid.Columns.Add(CreateBadgeColumn());
        _grid.Columns.Add(new TreeDataGridTextColumn
        {
            Header = "换行说明（2*）", Width = new GridLength(2, GridUnitType.Star), MinWidth = 180,
            TextWrapping = TextWrapping.Wrap, ValueGetter = model => ((DemoItem)model).Description,
        });
        var items = Enumerable.Range(0, 64).Select(i => new DemoItem(i, i % 8 == 0 ? $"较长的中文名称样例 {i:00}" : $"记录 {i:00}")).ToArray();
        SetSource(new FlatTreeDataGridSource<DemoItem>(items), "64 行 · 文本 / 三态 / 模板 / 行号 · Auto / Star");
        _lastAction.Text = "此场景的复选框只展示数据；使用“编辑 / 验证样例”体验可编辑列。拖动列头右侧边缘调宽。";
    }

    private HierarchicalTreeDataGridSource<DemoItem> CreateTreeSource(ObservableCollection<DemoItem> roots)
    {
        var source = new HierarchicalTreeDataGridSource<DemoItem>(roots, item => item.Children,
            new HierarchicalTreeDataGridSourceOptions<DemoItem>
            {
                HasChildrenSelector = item => item.HasUnloadedChildren || item.Children.Count > 0,
                IsExpandedSelector = item => item.IsExpanded,
                IsExpandedWriter = (item, value) => item.IsExpanded = value,
            });
        source.RowExpanding += (_, args) =>
        {
            var item = (DemoItem)args.Row.Model;
            if (!item.HasUnloadedChildren) return;
            for (var i = 0; i < 3; i++) item.Children.Add(new DemoItem(10_000 + i, $"按需加载的子节点 {i + 1}"));
            item.HasUnloadedChildren = false;
            _lazyLoadCount++;
            _lastAction.Text = $"展开前事件已同步加载 {item.Children.Count} 个子节点；加载次数 {_lazyLoadCount}。";
        };
        return source;
    }

    private void WithTree(Action<HierarchicalTreeDataGridSource<DemoItem>> action)
    {
        if (_source is not HierarchicalTreeDataGridSource<DemoItem>) ShowTree();
        action((HierarchicalTreeDataGridSource<DemoItem>)_source!);
    }

    private void ExpandEvenRoots() => WithTree(source =>
    {
        source.CollapseAll();
        source.SetExpandedRecursive(true, row => row.Depth == 0 && ((DemoItem)row.Model).Id % 2 == 0);
        _lastAction.Text = "条件展开：只展开编号为偶数的根节点；条件不匹配时停止向下遍历。";
    });

    private void AddChild() => WithTree(source =>
    {
        var first = _treeRoots![0];
        first.Children.Add(new DemoItem(_nextDynamicId++, "动态增加的子节点"));
        if (source.TryGetNodeId(new IndexPath(0), out var id)) source.SetExpanded(id, true);
        _lastAction.Text = $"首个根节点现有 {first.Children.Count} 个子节点；新增节点立即进入可见投影。";
    });

    private void RemoveChild() => WithTree(_ =>
    {
        var children = _treeRoots![0].Children;
        if (children.Count > 0) children.RemoveAt(children.Count - 1);
        _lastAction.Text = $"首个根节点现有 {children.Count} 个子节点。";
    });

    private void ShowLazyTree()
    {
        ConfigureTreeColumns();
        _grid.RowHeightSelector = null;
        _grid.AutoRowHeight = false;
        _lazyLoadCount = 0;
        _treeRoots = [new DemoItem(0, "展开此分组：前事件加载 3 个子节点") { HasUnloadedChildren = true }];
        SetSource(CreateTreeSource(_treeRoots), "按需加载 · 同步展开前事件 · 展开状态双向模型绑定");
        _lastAction.Text = "点击树展开器。此示例在 RowExpanding 同步填充子集合，不模拟远程异步请求。";
    }

    private void ToggleApplicationTemplate()
    {
        if (_customTemplate) _grid.ClearValue(GridControl.TemplateProperty);
        else _grid.Template = CreateApplicationTemplate();
        _customTemplate = !_customTemplate;
        _lastAction.Text = _customTemplate ? "已加载应用自定义模板；源数据和展开状态保留。" : "已恢复主题默认模板。";
    }

    private static FuncControlTemplate<GridControl> CreateApplicationTemplate() => new((_, scope) =>
    {
        var rows = new TreeDataGridRowsPresenter();
        var headers = new TreeDataGridColumnHeadersPresenter { Height = 30 };
        var scroll = new ScrollViewer
        {
            Content = rows,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        scope.Register("PART_ScrollViewer", scroll);
        scope.Register("PART_RowsPresenter", rows);
        scope.Register("PART_ColumnHeadersPresenter", headers);
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(scroll, 1);
        body.Children.Add(headers);
        body.Children.Add(scroll);
        return new Border { BorderBrush = Brushes.SteelBlue, BorderThickness = new Thickness(2), Child = body };
    });
}
