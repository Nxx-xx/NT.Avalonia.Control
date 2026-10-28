using Avalonia.Data;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private bool _a1Scenario;

    private void ShowColumnOptionsScenario()
    {
        ShowEditingScenario();
        _a1Scenario = true;
        var name = _grid.Columns[1];
        name.Header = "名称（选中后单击 / F2）";
        name.BeginEditGestures = TreeDataGridBeginEditGestures.F2 | TreeDataGridBeginEditGestures.WhenSelected;
        name.AllowTriStateSorting = true;
        var quantity = (TreeDataGridTextColumn)_grid.Columns[2];
        quantity.Header = "数量（单击 / 两态排序）";
        quantity.BeginEditGestures = TreeDataGridBeginEditGestures.Tap;
        quantity.AllowTriStateSorting = false;
        quantity.IsTextSearchEnabled = false;
        quantity.TextAlignment = TextAlignment.Right;
        quantity.CompareAscending = (left, right) => ((EditingItem)left).Quantity.CompareTo(((EditingItem)right).Quantity);
        quantity.CompareDescending = (left, right) => ((EditingItem)right).Quantity.CompareTo(((EditingItem)left).Quantity);
        var note = (TreeDataGridTemplateColumn)_grid.Columns[4];
        note.Header = "说明（可搜索 / 手势只读）";
        note.BeginEditGestures = TreeDataGridBeginEditGestures.None;
        note.TextSearchBinding = new ReflectionBinding(nameof(EditingItem.Note));
        _editingItems![7].Note = "模板搜索定位示例";
        _lastAction.Text = "名称支持三态排序，数量仅升降序；数量不参与搜索，说明列支持前缀定位。布局选项中的“显示列头”可隐藏整个列头。";
    }

    private void LocateSearchPrefix()
    {
        var found = _grid.SearchText(_prefixSearch.Text?.Trim() ?? "");
        _lastAction.Text = found is { } cell
            ? $"已定位到第 {_source!.GetViewIndex(cell.Node) + 1:N0} 行，列“{_grid.Columns.Single(column => column.Id == cell.Column).Header}”。"
            : "没有找到可定位的匹配项；请检查前缀、列搜索配置或当前编辑校验。";
    }
}
