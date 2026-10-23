using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private ObservableCollection<NestedBindingDemoItem>? _nestedBindingRoots;
    private Control _nestedBindingTools = null!;

    private Control CreateNestedBindingTools()
    {
        var actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        AddButton(actions, "替换状态对象", ReplaceNestedBindingState,
            "为首个根节点更换状态对象；旧状态和子集合立即解除订阅。", "ReplaceNestedStateButton");
        AddButton(actions, "替换子集合", ReplaceNestedBindingChildren,
            "替换当前状态中的子节点集合，观察树立即更新。", "ReplaceNestedChildrenButton");
        AddButton(actions, "清空状态引用", () =>
        {
            if (_nestedBindingRoots is not { Count: > 0 } roots) return;
            roots[0].State = null;
            _lastAction.Text = "状态引用为空，根节点仍在；当前没有子节点。点击“恢复状态”重新接入。";
        }, "null 状态显示为空层级，数量字段仍可编辑。", "NullNestedStateButton");
        AddButton(actions, "恢复状态", () =>
        {
            if (_nestedBindingRoots is not { Count: > 0 } roots) return;
            roots[0].State = NewNestedBindingState("恢复状态", "恢复的子节点");
            _lastAction.Text = "状态和子集合已恢复，嵌套绑定重新订阅。";
        }, "创建一个状态对象并展开其子节点。", "RestoreNestedStateButton");
        AddButton(actions, "编辑首行数量", () =>
        {
            if (_source is null || _source.Rows.Count == 0) return;
            var column = _grid.Columns.Single(column => Equals(column.Header, "数量（0–100）"));
            var address = new CellAddress(_source.Rows[0].Id, column.Id);
            _grid.State!.SetCurrentCell(address);
            _lastAction.Text = _grid.BeginEdit(address) ? "数量草稿已开始，Enter 提交或 Esc 取消。" : "当前无法开始编辑，请先处理已有草稿。";
        }, "数量显示使用编译绑定，写入使用类型化 setter。", "BeginNestedQuantityEditButton");
        AddButton(actions, "提交草稿", () =>
            _lastAction.Text = _grid.CommitEdit() ? "草稿已提交，模型和显示一致。" : _grid.EditSession?.ValidationError ?? "提交被拒绝，请取消后重新开始。",
            "转换和校验成功后才调用业务 writer。", "CommitNestedEditButton");
        AddButton(actions, "取消草稿", () => { _grid.CancelEdit(); _lastAction.Text = "草稿已取消，模型保持原值。"; },
            "取消当前草稿，不写入模型。", "CancelNestedEditButton");
        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = "树接入使用 State.Children / State.HasChildren / State.Expanded，每层对象均通知属性变化。名称和数量使用编译绑定显示；编辑通过明确的类型化 setter 写入。状态说明使用嵌套反射绑定，编辑期间若换了状态对象，旧草稿提交会被拒绝。",
                    TextWrapping = TextWrapping.Wrap,
                },
                actions,
            },
        };
    }

    private void ShowNestedBindingScenario()
    {
        StopTemplateClock();
        ClearNestedBindingScenario();
        if (_observedState is not null) _observedState.Changed -= DemoStateChanged;
        _observedState = null;
        _grid.Source = null;
        _source?.Dispose();
        _source = null;
        _grid.Columns.Clear();
        _treeRoots = null;
        _grid.IsReadOnly = false;
        _grid.AutoRowHeight = false;
        _grid.RowHeightSelector = null;
        _grid.FrozenColumnCount = 1;
        _grid.SelectionUnit = TreeDataGridSelectionUnit.Cell;
        _grid.SelectionMode = TreeDataGridSelectionMode.Multiple;

        var name = new TreeDataGridTextColumn { Binding = CompiledBinding.Create<NestedBindingDemoItem, string>(item => item.Name) };
        name.SetValueAccessors<NestedBindingDemoItem, string>(item => item.Name, (item, value) => item.Name = value);
        _grid.Columns.Add(new TreeDataGridHierarchicalExpanderColumn
        {
            Header = "名称（编译绑定显示）", Width = new GridLength(320), InnerColumn = name,
            ChildrenBinding = new ReflectionBinding("State.Children"),
            HasChildrenBinding = new ReflectionBinding("State.HasChildren"),
            IsExpandedBinding = new ReflectionBinding("State.Expanded") { Mode = BindingMode.TwoWay },
        });
        var quantity = new TreeDataGridTextColumn
        {
            Header = "数量（0–100）", Width = new GridLength(180),
            Binding = CompiledBinding.Create<NestedBindingDemoItem, int>(item => item.Quantity),
            ValueValidator = value => value is int number && number is >= 0 and <= 100 ? null : "数量必须为 0 到 100 的整数。",
        };
        quantity.SetValueAccessors<NestedBindingDemoItem, int>(item => item.Quantity, (item, value) => item.Quantity = value);
        _grid.Columns.Add(quantity);
        _grid.Columns.Add(new TreeDataGridTextColumn
        {
            Header = "状态说明（嵌套绑定）", Width = new GridLength(1, GridUnitType.Star), MinWidth = 260,
            Binding = new ReflectionBinding("State.Label") { Mode = BindingMode.TwoWay },
        });
        _nestedBindingRoots = [new NestedBindingDemoItem("嵌套绑定根节点")
            { State = NewNestedBindingState("原状态", "初始子节点") }];
        _grid.ItemsSource = _nestedBindingRoots;
        _source = _grid.Source;
        _scenario = "嵌套属性路径 · 状态替换 / null · 编译显示与类型化写入";
        _p6ScenarioId = "nestedbindings";
        _observedState = _grid.State;
        if (_observedState is not null) _observedState.Changed += DemoStateChanged;
        _lastAction.Text = "展开或折叠树，替换状态 / 子集合，或清空后恢复。双击数量编辑，确认错误草稿不修改模型。";
        UpdateScenarioUi();
    }

    private static NestedBindingDemoState NewNestedBindingState(string label, string childPrefix) => new()
    {
        Label = label, Expanded = true,
        Children = [new NestedBindingDemoItem(childPrefix + " 1"), new NestedBindingDemoItem(childPrefix + " 2")],
    };

    private void ReplaceNestedBindingState()
    {
        if (_nestedBindingRoots is not { Count: > 0 } roots) return;
        roots[0].State = NewNestedBindingState("替换后的状态", "替换状态的子节点");
        _lastAction.Text = "首行状态对象已替换，旧对象不再更新树和单元格。若有旧状态草稿，请取消后重新编辑。";
    }

    private void ReplaceNestedBindingChildren()
    {
        if (_nestedBindingRoots is not { Count: > 0 } roots) return;
        if (roots[0].State is not { } state) { _lastAction.Text = "请先恢复状态，再替换子集合。"; return; }
        state.Children = [new NestedBindingDemoItem("替换集合的子节点 1"), new NestedBindingDemoItem("替换集合的子节点 2")];
        state.Expanded = true;
        _lastAction.Text = "子集合已替换，旧集合变更不再影响页面。";
    }

    private void ClearNestedBindingScenario()
    {
        if (_nestedBindingRoots is null) return;
        if (_observedState is not null) _observedState.Changed -= DemoStateChanged;
        _observedState = null;
        _grid.ItemsSource = null;
        _source = null; // The ItemsSource adapter owns and disposes this source.
        _nestedBindingRoots = null;
    }
}

internal abstract class NestedBindingObservable : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    public int SubscriptionCount { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { _changed += value; SubscriptionCount++; }
        remove { _changed -= value; SubscriptionCount--; }
    }
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        _changed?.Invoke(this, new(property));
    }
}

internal sealed class NestedBindingDemoItem(string name) : NestedBindingObservable
{
    private string _name = name;
    private int _quantity = 10;
    private NestedBindingDemoState? _state = new();
    public string Name { get => _name; set => Set(ref _name, value); }
    public int Quantity { get => _quantity; set { Set(ref _quantity, value); QuantityWrites++; } }
    public int QuantityWrites { get; private set; }
    public NestedBindingDemoState? State { get => _state; set => Set(ref _state, value); }
}

internal sealed class NestedBindingDemoState : NestedBindingObservable
{
    private ObservableCollection<NestedBindingDemoItem> _children = [];
    private string _label = "子节点状态";
    private bool _expanded;
    private bool _hasChildren;
    public ObservableCollection<NestedBindingDemoItem> Children { get => _children; set => Set(ref _children, value); }
    public string Label { get => _label; set => Set(ref _label, value); }
    public bool Expanded { get => _expanded; set => Set(ref _expanded, value); }
    public bool HasChildren { get => _hasChildren; set => Set(ref _hasChildren, value); }
}
