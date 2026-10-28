using Avalonia.Controls;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private bool _batchMoveItemsSource;
    private Control _batchMoveTools = null!;
    private readonly CheckBox _cancelRowDrop = new() { Content = "在 RowDrop 取消（保留草稿）", Name = "CancelA4Drop" };
    private readonly TextBlock _batchMoveEvents = new() { TextWrapping = TextWrapping.Wrap };
    private int _dragStarts, _dragOvers, _rowDrops, _batchRequests, _batchCompletions;

    private Control CreateBatchMoveTools()
    {
        var actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        actions.Children.Add(_cancelRowDrop);
        AddButton(actions, "选中首组前两项", SelectBatchDemoRows, "Ctrl 单击可自行选择多行；已选祖先覆盖的后代不会重复移动。", "SelectA4BatchButton");
        AddButton(actions, "整批移入第二组", MoveBatchDemoRows, "一次事务提交多个节点，选择与当前项根据返回映射恢复。", "MoveA4BatchButton");
        _grid.RowDragStarted += (_, _) => { if (_p6ScenarioId == "rowdrag-batch") { _dragStarts++; UpdateBatchMoveEvents(); } };
        _grid.RowDragOver += (_, _) => { if (_p6ScenarioId == "rowdrag-batch") { _dragOvers++; UpdateBatchMoveEvents(); } };
        _grid.RowDrop += (_, args) =>
        { if (_p6ScenarioId == "rowdrag-batch") { _rowDrops++; args.Cancel = _cancelRowDrop.IsChecked == true; UpdateBatchMoveEvents(); } };
        _grid.RowsMoveRequested += (_, _) => { if (_p6ScenarioId == "rowdrag-batch") { _batchRequests++; UpdateBatchMoveEvents(); } };
        _grid.RowsMoveCompleted += (_, args) =>
        {
            if (_p6ScenarioId != "rowdrag-batch") return;
            _batchCompletions++; UpdateBatchMoveEvents();
            _lastAction.Text = $"批量结果：{args.Result.Status}；身份映射 {args.Result.IdentityMap.Count} 项。";
        };
        return new StackPanel { Spacing = 6, Children =
        {
            new TextBlock { Text = "ItemsSource 直接使用事务集合；可从整行非交互区域拖动。Ctrl 选择多行后拖动其中一行，整批保持源顺序。RowDrop 先于编辑提交；取消时保留草稿。按钮直接演示批量移动请求。", TextWrapping = TextWrapping.Wrap },
            actions, _batchMoveEvents,
        } };
    }

    private void ShowBatchMoveScenario()
    {
        ShowRowMovingScenario();
        if (_observedState is not null) _observedState.Changed -= DemoStateChanged;
        _grid.Source = null; _source!.Dispose(); _source = null;
        var tree = (TreeDataGridHierarchicalExpanderColumn)_grid.Columns[1];
        tree.ChildrenBinding = TreeDataGridBinding.Create<RowMoveDemoItem, TreeDataGridRowMoveCollection<RowMoveDemoItem>>(item => item.Children);
        tree.IsExpandedBinding = TreeDataGridBinding.Create<RowMoveDemoItem, bool>(item => item.IsExpanded);
        _batchMoveItemsSource = true; _grid.ItemsSource = _rowMoveRoots;
        _source = _grid.Source; _observedState = _grid.State;
        if (_observedState is not null) _observedState.Changed += DemoStateChanged;
        _grid.RowDragHandle = TreeDataGridRowDragHandle.WholeRow;
        _p6ScenarioId = "rowdrag-batch"; _scenario = "拖放事件 · ItemsSource 事务适配 · 多节点原子移动";
        _cancelRowDrop.IsChecked = false; _dragStarts = _dragOvers = _rowDrops = _batchRequests = _batchCompletions = 0;
        SelectBatchDemoRows(); UpdateBatchMoveEvents(); UpdateScenarioUi();
    }
    private void UpdateBatchMoveEvents() => _batchMoveEvents.Text =
        $"拖动开始 {_dragStarts} / 悬停 {_dragOvers} / 放下 {_rowDrops}；批量请求 {_batchRequests} / 完成 {_batchCompletions}";
    private void SelectBatchDemoRows()
    {
        if (_source is not { } source || !source.TryGetNodeId(new(0, 0), out var first) || !source.TryGetNodeId(new(0, 1), out var second)) return;
        var state = _grid.State!;
        using (state.DeferSelectionNotifications()) { state.Select(first); state.Select(second, true); state.SetCurrentCell(new(second, _grid.Columns[1].Id)); }
        _lastAction.Text = "已选择首组前两项，可拖动其中一行或整批移入第二组。";
    }
    private void MoveBatchDemoRows()
    {
        if (_source is not { } source || !source.TryGetNodeId(new(1), out var target)) return;
        _grid.TryMoveRows(_grid.State!.SelectedNodes.ToArray(), target, TreeDataGridRowDropPosition.Inside);
    }
}
