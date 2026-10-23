using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private ObservableCollection<EditingItem>? _editingItems;

    private WrapPanel CreateEditingToolbar()
    {
        var panel = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        AddButton(panel, "选中 3 × 3 区域", SelectDemoRectangle, "选中第 2–4 行的名称、数量和布尔列；Shift 可继续扩展。", "SelectRectangleButton");
        AddButton(panel, "选中前两行", SelectDemoRows, "切换到整行多选，并选中前两条记录。", "SelectRowsButton");
        AddButton(panel, "清除选择", () => _grid.State?.ClearSelection(), "清除高亮的行或单元格，保留数据。", "ClearSelectionButton");
        _beginEditButton = AddButton(panel, "编辑当前单元格", BeginCurrentEdit, "选择名称、数量或说明单元格后开始编辑；也可双击或按 F2。", "NormalBeginEditButton");
        _commitEditButton = AddButton(panel, "提交草稿", () => _lastAction.Text = _grid.CommitEdit() ? "已提交；排序和筛选会随数据更新。" : _grid.State?.EditSession?.ValidationError ?? "没有可提交的编辑。", "校验通过后将草稿写入数据；失败时保留草稿并显示原因。", "NormalCommitEditButton");
        _cancelEditButton = AddButton(panel, "取消草稿", () => { _grid.CancelEdit(); _lastAction.Text = "已取消草稿，原数据保持不变。"; }, "丢弃当前编辑草稿，恢复编辑前的值。", "NormalCancelEditButton");
        return panel;
    }

    private void ShowEditingScenario()
    {
        _grid.Source = null;
        _treeRoots = null;
        _grid.RowHeightSelector = null;
        _grid.AutoRowHeight = false;
        _grid.FrozenColumnCount = 1;
        _grid.IsReadOnly = false;
        _grid.SelectionUnit = TreeDataGridSelectionUnit.Cell;
        _grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        _grid.Columns.Clear();
        _grid.Columns.Add(new TreeDataGridRowHeaderColumn { Header = "行号", Width = new GridLength(60), IsReadOnly = true, CanUserSort = false });
        _grid.Columns.Add(new TreeDataGridTextColumn
        {
            Header = "名称（必填）", Width = new GridLength(240), ValueType = typeof(string),
            ValueGetter = model => ((EditingItem)model).Name,
            ValueSetter = (model, value) => ((EditingItem)model).Name = (string)value!,
            ValueValidator = value => string.IsNullOrWhiteSpace(value as string) ? "名称不能为空。" : ((string)value!).Length > 32 ? "名称最多 32 个字符。" : null,
        });
        _grid.Columns.Add(new TreeDataGridTextColumn
        {
            Header = "数量（0–100）", Width = new GridLength(160), ValueType = typeof(int),
            ValueGetter = model => ((EditingItem)model).Quantity,
            ValueSetter = (model, value) => ((EditingItem)model).Quantity = (int)value!,
            ValueValidator = value => value is int quantity && quantity is >= 0 and <= 100 ? null : "数量必须为 0 到 100 的整数。",
        });
        _grid.Columns.Add(new TreeDataGridCheckBoxColumn
        {
            Header = "可空布尔", Width = new GridLength(120), ValueType = typeof(bool?), IsThreeState = true,
            ValueGetter = model => ((EditingItem)model).Enabled,
            ValueSetter = (model, value) => ((EditingItem)model).Enabled = (bool?)value,
        });
        _grid.Columns.Add(new TreeDataGridTemplateColumn
        {
            Header = "说明（自定义编辑模板）", Width = new GridLength(1, GridUnitType.Star), MinWidth = 260, ValueType = typeof(string),
            CanUserSort = true,
            ValueGetter = model => ((EditingItem)model).Note,
            ValueSetter = (model, value) => ((EditingItem)model).Note = value?.ToString() ?? "",
            ValueValidator = value => value?.ToString()?.Length > 80 ? "说明最多 80 个字符。" : null,
            CellTemplate = new FuncDataTemplate<string>((value, _) => new TextBlock { Text = value, TextTrimming = TextTrimming.CharacterEllipsis }),
            CellEditingTemplate = new FuncDataTemplate<TreeDataGridEditSession>((session, _) =>
            {
                var editor = new TextBox { Name = "PART_TemplateEditor", MinHeight = 0, Padding = new Thickness(4, 1), DataContext = session };
                editor.Bind(TextBox.TextProperty, new ReflectionBinding(nameof(TreeDataGridEditSession.Value)) { Mode = BindingMode.TwoWay });
                return editor;
            }),
        });
        _editingItems = new ObservableCollection<EditingItem>(Enumerable.Range(0, 2000).Select(i => new EditingItem(i)));
        SetSource(new FlatTreeDataGridSource<EditingItem>(_editingItems), "2,000 条可编辑记录 · 单矩形单元格选择 / 行多选");
        _filterText.Text = "";
        _minimumQuantity.Text = "";
        _lastAction.Text = "双击或 F2 编辑。空名称、越界数量会被拒绝；Esc 取消；编辑值会影响当前排序 / 筛选。";
    }

    private void EnsureEditingScenario()
    {
        if (_source is not FlatTreeDataGridSource<EditingItem>) ShowEditingScenario();
    }

    private void SelectDemoRectangle()
    {
        EnsureEditingScenario();
        _grid.SelectionUnit = TreeDataGridSelectionUnit.Cell;
        _grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        if (_source!.Rows.Count < 4) { ClearFilter(); }
        var columns = _grid.VisibleColumns.Where(column => column is not TreeDataGridRowHeaderColumn).Take(3).ToArray();
        if (columns.Length == 0) { _lastAction.Text = "请先恢复至少一列，再选择区域。"; return; }
        _grid.State!.SelectCellRange(new CellAddress(_source.Rows[1].Id, columns[0].Id),
            new CellAddress(_source.Rows[3].Id, columns[^1].Id), _grid.VisibleColumns.Select(column => column.Id).ToArray());
        _grid.Focus();
        _lastAction.Text = $"已选择一个 3 × {columns.Length} 矩形；Shift 点击或方向键扩展，新的区域替换旧矩形。";
    }

    private void SelectDemoRows()
    {
        EnsureEditingScenario();
        _grid.SelectionUnit = TreeDataGridSelectionUnit.Row;
        _grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        if (_source!.Rows.Count < 2) ClearFilter();
        using (_grid.State!.DeferSelectionNotifications())
        {
            _grid.State.Select(_source.Rows[0].Id);
            _grid.State.Select(_source.Rows[1].Id, additive: true);
        }
        _grid.Focus();
        _lastAction.Text = "已选中前两行；Ctrl 点击切换行，Shift 扩展连续行。";
    }

    private void BeginCurrentEdit()
    {
        EnsureEditingScenario();
        var firstEditableColumn = _grid.VisibleColumns.FirstOrDefault(column => column is not TreeDataGridRowHeaderColumn && !column.IsReadOnly);
        var cell = _grid.State?.CurrentCell ?? (_source!.Rows.Count > 0 && firstEditableColumn is not null
            ? new CellAddress(_source.Rows[0].Id, firstEditableColumn.Id) : (CellAddress?)null);
        if (cell is { } address)
            _lastAction.Text = _grid.BeginEdit(address) ? "正在编辑：Enter 提交，Esc 取消。" : "当前单元格为只读；请选择名称、数量或说明列。";
    }

    private void ApplyFilter()
    {
        if (_source is null) return;
        var term = _filterText.Text?.Trim() ?? "";
        var quantityText = _minimumQuantity.Text?.Trim();
        if (!string.IsNullOrEmpty(quantityText) && !int.TryParse(quantityText, out _))
        {
            _lastAction.Text = "数量下限应为整数；筛选未修改。";
            return;
        }
        var minimum = int.TryParse(quantityText, out var value) ? value : (int?)null;
        _source.SetFilter(model => model switch
        {
            EditingItem item => item.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) && (minimum is null || item.Quantity >= minimum),
            DemoItem item => item.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase),
            RowMoveDemoItem item => item.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase),
            _ => true,
        });
        _lastAction.Text = $"筛选后 {_source.Rows.Count:N0} 个可见行；隐藏的活动编辑会取消，选择按节点身份保留。";
    }

    private void ClearFilter()
    {
        _filterText.Text = "";
        _minimumQuantity.Text = "";
        _source?.SetFilter(null);
        _lastAction.Text = "筛选已清除。";
    }
}
