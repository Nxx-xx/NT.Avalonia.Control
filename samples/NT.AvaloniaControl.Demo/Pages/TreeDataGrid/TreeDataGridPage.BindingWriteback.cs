using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private Control _bindingWritebackTools = null!;
    private Control CreateBindingWritebackTools()
    {
        var actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        AddButton(actions, "编辑首行数量", () =>
        {
            if (_source is null) return;
            _grid.BeginEdit(new(_source.Rows[0].Id, _grid.Columns[1].Id));
            _lastAction.Text = "输入“数量 42”；转换后按整数 0–100 校验。草稿尚未写入模型。";
        }, "编译绑定适配器自动定位模型属性，ConvertBack 在校验前执行。", "BeginSafeBindingEditButton");
        AddButton(actions, "提交草稿", () => _lastAction.Text = _grid.CommitEdit() ? "已提交一次模型写入。" : _grid.EditSession?.ValidationError ?? "未提交。", "转换和验证成功后写入一次。", "CommitSafeBindingEditButton");
        AddButton(actions, "取消草稿", () => { _grid.CancelEdit(); _lastAction.Text = "已取消，未调用写回器。"; }, "丢弃草稿。", "CancelSafeBindingEditButton");
        AddButton(actions, "替换状态对象", ReplaceNestedBindingState, "编辑状态说明期间替换对象，旧草稿会被拒绝写入。", "ReplaceSafeBindingStateButton");
        AddButton(actions, "编辑状态说明", () =>
        {
            if (_source is not null) _grid.BeginEdit(new(_source.Rows[0].Id, _grid.Columns[2].Id));
        }, "先编辑，再替换状态，观察目标身份保护。", "BeginSafeBindingLabelButton");
        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "编译绑定适配器保留可验证的模型路径。数量通过 ConvertBack 转换为整数再校验；展开、子集合和提示使用同一适配器。替换嵌套目标后请取消旧草稿再编辑。", TextWrapping = TextWrapping.Wrap },
                actions,
            },
        };
    }
    private void ShowBindingWritebackScenario()
    {
        ShowNestedBindingScenario();
        var roots = _nestedBindingRoots!;
        if (_observedState is not null) _observedState.Changed -= DemoStateChanged;
        _grid.ItemsSource = null; _source = null; _grid.Columns.Clear();
        _grid.Columns.Add(new TreeDataGridHierarchicalExpanderColumn
        {
            Header = "名称（编译绑定适配器）", Width = new(300),
            InnerColumn = new TreeDataGridTextColumn { Binding = TreeDataGridBinding.Create<NestedBindingDemoItem, string>(item => item.Name) },
            ChildrenBinding = TreeDataGridBinding.Create<NestedBindingDemoItem, ObservableCollection<NestedBindingDemoItem>>(item => item.State!.Children),
            HasChildrenBinding = TreeDataGridBinding.Create<NestedBindingDemoItem, bool>(item => item.State!.HasChildren),
            IsExpandedBinding = TreeDataGridBinding.Create<NestedBindingDemoItem, bool>(item => item.State!.Expanded),
        });
        var quantityBinding = TreeDataGridBinding.Create<NestedBindingDemoItem, int>(item => item.Quantity);
        quantityBinding.Converter = new QuantityBindingConverter();
        quantityBinding.ConverterCulture = CultureInfo.GetCultureInfo("zh-CN");
        quantityBinding.ConverterParameter = "数量 ";
        _grid.Columns.Add(new TreeDataGridTextColumn
        {
            Header = "数量（0–100）", Width = new(210), Binding = quantityBinding,
            ValueValidator = value => value is int number && number is >= 0 and <= 100 ? null : "数量必须为 0 到 100 的整数。",
        });
        _grid.Columns.Add(new TreeDataGridTextColumn
        {
            Header = "状态说明（目标替换保护）", Width = new(1, GridUnitType.Star), MinWidth = 260,
            Binding = TreeDataGridBinding.Create<NestedBindingDemoItem, string>(item => item.State!.Label),
        });
        _grid.ItemsSource = roots; _source = _grid.Source; _observedState = _grid.State;
        if (_observedState is not null) _observedState.Changed += DemoStateChanged;
        _p6ScenarioId = "binding-writeback"; _scenario = "受控编译绑定 · ConvertBack · 嵌套目标保护";
        _lastAction.Text = "双击数量，输入“数量 42”；取消不写入。编辑状态说明时更换状态，旧草稿无法写入新对象。";
        UpdateScenarioUi();
    }
    private sealed class QuantityBindingConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (parameter?.ToString() ?? "") + System.Convert.ToString(value, culture);
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var text = value?.ToString() ?? ""; var prefix = parameter?.ToString() ?? "";
            if (!text.StartsWith(prefix, StringComparison.Ordinal)) throw new FormatException("请输入“数量 ”及一个整数。");
            return int.Parse(text[prefix.Length..], culture);
        }
    }
}
