using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private TreeDataGridRowMoveCollection<RowMoveDemoItem>? _rowMoveRoots;
    private readonly CheckBox _cancelRowMoves = new() { Name = "CancelRowMoveRequests", Content = "取消移动请求" };
    private Control _rowMoveTools = null!;

    private Control CreateRowMoveTools()
    {
        var actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        actions.Children.Add(_cancelRowMoves);
        AddButton(actions, "首组内部重排", MoveFirstWithinGroup,
            "将首组第一个成员移到本组末位成员之后，保留节点身份。", "MoveWithinGroupButton");
        AddButton(actions, "首组成员移入第二组", () => MoveDemoRow(new IndexPath(0, 0), new IndexPath(1), TreeDataGridRowDropPosition.Inside),
            "两边集合使用事务适配；跨组后选择跟随新节点身份。", "MoveBetweenGroupsButton");
        _grid.RowMoveRequested += (_, args) =>
        {
            if (_p6ScenarioId == "rowmoving" && _cancelRowMoves.IsChecked == true) args.Cancel = true;
        };
        _grid.RowMoveCompleted += (_, args) =>
        {
            if (_p6ScenarioId == "rowmoving") _lastAction.Text = RowMoveDescription(args.Result);
        };
        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = "从行号单元格拖动：靠上/靠下插到前/后，中间区域移入目标。Esc 取消；排序或筛选时默认移动被拒绝。名称可双击编辑，校验失败会阻止移动。",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
                actions,
            },
        };
    }

    private void ShowRowMovingScenario()
    {
        _grid.Source = null; _grid.Columns.Clear(); _treeRoots = null;
        _grid.RowHeightSelector = null; _grid.AutoRowHeight = false;
        _grid.FrozenColumnCount = 1; _grid.CanUserMoveRows = true; _grid.CanUserReorderColumns = true;
        _grid.SelectionUnit = TreeDataGridSelectionUnit.Row; _grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        _grid.Columns.Add(new TreeDataGridRowHeaderColumn { Header = "拖动", Width = new GridLength(64), IsReadOnly = true, CanUserSort = false });
        _grid.Columns.Add(new TreeDataGridHierarchicalExpanderColumn
        {
            Header = "组 / 成员（名称必填）", Width = new GridLength(360),
            InnerColumn = new TreeDataGridTextColumn
            {
                ValueType = typeof(string), ValueGetter = model => ((RowMoveDemoItem)model).Name,
                ValueSetter = (model, value) => ((RowMoveDemoItem)model).Name = (string)value!,
                ValueValidator = value => string.IsNullOrWhiteSpace(value as string) ? "名称不能为空。" : null,
            },
        });
        _grid.Columns.Add(new TreeDataGridTextColumn { Header = "编号", Width = new GridLength(140), ValueGetter = model => ((RowMoveDemoItem)model).Id });
        _rowMoveRoots = [];
        for (var group = 0; group < 4; group++)
        {
            var parent = new RowMoveDemoItem(group, $"分组 {group + 1}");
            for (var child = 0; child < 3; child++) parent.Children.Add(new(group * 10 + child + 10, $"成员 {group + 1}.{child + 1}"));
            _rowMoveRoots.Add(parent);
        }
        _p6ScenarioId = "rowmoving";
        _cancelRowMoves.IsChecked = false;
        SetSource(new HierarchicalTreeDataGridSource<RowMoveDemoItem>(_rowMoveRoots, item => item.Children,
            new HierarchicalTreeDataGridSourceOptions<RowMoveDemoItem>
            {
                IsExpandedSelector = item => item.IsExpanded,
                IsExpandedWriter = (item, value) => item.IsExpanded = value,
            }), "行移动 · 同组保留身份 · 跨组事务集合");
        _lastAction.Text = "拖动行号体验落点，或使用按钮演示同组/跨组移动。勾选“取消移动请求”可观察取消行为。";
        UpdateScenarioUi();
    }

    private void MoveDemoRow(IndexPath node, IndexPath target, TreeDataGridRowDropPosition position)
    {
        if (_source is not { } source || !source.TryGetNodeId(node, out var moving) || !source.TryGetNodeId(target, out var landing))
        { _lastAction.Text = "该示例位置已没有可移动的成员，请重置示例。"; return; }
        _lastAction.Text = RowMoveDescription(_grid.TryMoveRow(moving, landing, position));
    }

    private void MoveFirstWithinGroup()
    {
        if (_rowMoveRoots is null || _rowMoveRoots.Count == 0 || _rowMoveRoots[0].Children.Count < 2)
        { _lastAction.Text = "首组需要至少两个成员才能重排，请重置示例。"; return; }
        MoveDemoRow(new IndexPath(0, 0), new IndexPath(0, _rowMoveRoots[0].Children.Count - 1), TreeDataGridRowDropPosition.After);
    }

    private static string RowMoveDescription(TreeDataGridRowMoveResult result) => result.Status switch
    {
        TreeDataGridRowMoveStatus.Committed => "移动完成；选择与当前项跟随移动后的节点。",
        TreeDataGridRowMoveStatus.CommittedWithObserverError => "集合已经移动，但某个通知处理器报错：" + result.Error?.Message,
        TreeDataGridRowMoveStatus.Cancelled => "移动请求已取消，集合位置保持不变。",
        TreeDataGridRowMoveStatus.ValidationFailed => "草稿校验失败，移动被拒绝。请修正或取消编辑。",
        TreeDataGridRowMoveStatus.Stale => "移动途中数据或位置发生变化，请重新开始。",
        TreeDataGridRowMoveStatus.Unsupported => "此集合不支持默认移动，请由应用处理请求。",
        TreeDataGridRowMoveStatus.HandlerFailed => "移动处理失败：" + result.Error?.Message,
        _ => "落点无效、顺序没有变化，或当前排序/筛选拒绝默认移动。",
    };
}

internal sealed class RowMoveDemoItem(int id, string name) : ObservableObject
{
    private string _name = name;
    private bool _isExpanded = true;
    public int Id { get; } = id;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }
    public TreeDataGridRowMoveCollection<RowMoveDemoItem> Children { get; } = [];
}
