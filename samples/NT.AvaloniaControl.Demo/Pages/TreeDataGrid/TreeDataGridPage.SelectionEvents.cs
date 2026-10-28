using Avalonia.Controls;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private bool _a2Scenario;
    private readonly CheckBox _cancelSelection = new() { Name = "CancelSelectionToggle", Content = "阻止离开当前选择" };
    private readonly TextBlock _cellEventStatus = new() { Name = "CellEventStatus", TextWrapping = TextWrapping.Wrap };
    private Control _a2Tools = null!;
    private int _a2Before;
    private int _a2Completed;
    private int _a2Prepared;
    private int _a2Cleared;
    private int _a2ValueChanges;
    private TreeDataGridCellValueChangedEventArgs? _a2LastValue;
    private readonly HashSet<TreeDataGridCellPresenter> _a2ActiveCells = [];

    private Control CreateSelectionEventTools()
    {
        _grid.SelectionChanging += (_, e) =>
        {
            if (!_a2Scenario) return;
            _a2Before++;
            e.Cancel = _cancelSelection.IsChecked == true;
            _lastAction.Text = e.Cancel ? $"已取消 {e.Reason} 选择请求，保留原选区与草稿。" : $"允许 {e.Reason} 选择请求；接下来校验旧草稿。";
            UpdateCellEventStatus();
        };
        _grid.SelectionChangeCompleted += (_, e) =>
        {
            if (!_a2Scenario) return;
            _a2Completed++;
            _lastAction.Text = $"选择完成：{e.Reason}；系统清理：{e.IsForced}。";
            UpdateCellEventStatus();
        };
        _grid.CellPrepared += (_, e) => { if (_a2Scenario) { _a2Prepared++; _a2ActiveCells.Add(e.Container); UpdateCellEventStatus(); } };
        _grid.CellClearing += (_, e) => { if (_a2Scenario) { _a2Cleared++; _a2ActiveCells.Remove(e.Container); UpdateCellEventStatus(); } };
        _grid.CellValueChanged += (_, e) =>
        {
            if (!_a2Scenario) return;
            _a2ValueChanges++; _a2LastValue = e;
            _lastAction.Text = $"值变化 {e.Reason}：{e.OldValue} → {e.NewValue}（节点 {e.Cell.Node.Value}，列 {e.Cell.Column.Value}）。";
            UpdateCellEventStatus();
        };
        var tools = new WrapPanel { ItemSpacing = 8, LineSpacing = 6, Children = { _cancelSelection } };
        AddButton(tools, "修改当前行名称", () =>
        {
            if (_grid.CurrentCell is { } cell && _source?.TryGetRow(cell.Node, out var row) == true && row!.Model is EditingItem item)
                item.Name = "外部模型更新示例";
        }, "修改可观察模型，查看值变化通知；滚动准备和清理本身不算值变化。", "ChangeObservedValueButton");
        return ToolGroup("选择事务与单元格事件", "勾选后点击其它单元格、按导航键或定位前缀；取消请求不会提交草稿。取消勾选后可继续操作。", new StackPanel { Spacing = 4, Children = { tools, _cellEventStatus } });
    }

    private void ShowSelectionEventsScenario()
    {
        ShowEditingScenario();
        _a2Scenario = true;
        _cancelSelection.IsChecked = false;
        _a2Before = _a2Completed = _a2Prepared = _a2Cleared = _a2ValueChanges = 0;
        _a2LastValue = null; _a2ActiveCells.Clear();
        UpdateCellEventStatus();
        _lastAction.Text = "先选择并编辑名称，再勾选“阻止离开当前选择”。观察取消时模型与草稿保持不变；滚动可观察准备与清理计数。";
    }

    private void UpdateCellEventStatus() => _cellEventStatus.Text =
        $"选择请求 {_a2Before} / 完成 {_a2Completed}；单元格准备 {_a2Prepared} / 清理 {_a2Cleared} / 当前 {_a2ActiveCells.Count}；值变化 {_a2ValueChanges}。";
}
