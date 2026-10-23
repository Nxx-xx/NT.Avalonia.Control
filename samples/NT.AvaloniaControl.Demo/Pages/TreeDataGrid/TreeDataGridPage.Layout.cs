using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private readonly ComboBox _scenarioPicker = new() { Name = "ScenarioPicker", Width = 320 };
    private readonly TextBlock _scenarioDescription = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _frozenColumns = new() { Width = 150, ItemsSource = new[] { "不冻结列", "冻结首列", "冻结前两列" } };
    private readonly ComboBox _rowHeightMode = new() { Width = 180, ItemsSource = new[] { "固定行高", "按内容自动行高", "每七行增加行高" } };
    private readonly CheckBox _threeStateSort = new() { Content = "排序可回到原顺序" };
    private readonly List<DemoScenarioChoice> _scenarioChoices = [];
    private Mvvm.MvvmView _mvvmView = null!;
    private TabControl _modeTabs = null!;
    private Control _editingTools = null!;
    private Control _treeTools = null!;
    private Control _filterTools = null!;
    private Button _beginEditButton = null!;
    private Button _commitEditButton = null!;
    private Button _cancelEditButton = null!;
    private TreeDataGridState? _observedState;
    private bool _synchronizingOptions;

    private Control CreateDemoLayout(string[] args)
    {
        var title = new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = "TreeDataGrid 控件示例", FontSize = 24, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "切换使用模式，再选择想体验的功能。每个示例都有操作说明。", TextWrapping = TextWrapping.Wrap },
            },
        };
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12), LastChildFill = true };
        heading.Children.Add(title);

        _mvvmView = Mvvm.MvvmDemoLauncher.CreateView();
        _modeTabs = new TabControl
        {
            Name = "DemoModeTabs",
            ItemsSource = new[]
            {
                new TabItem { Name = "NormalModeTab", Header = "普通模式", Content = CreateNormalPage() },
                new TabItem { Name = "MvvmModeTab", Header = "MVVM 模式", Content = _mvvmView },
            },
            SelectedIndex = args.Contains("--mvvm", StringComparer.Ordinal) || args.Contains("--mvvm-smoke", StringComparer.Ordinal) ? 1 : 0,
        };
        _modeTabs.SelectionChanged += DemoModeSelectionChanged;
        var root = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(heading, Dock.Top);
        root.Children.Add(heading);
        root.Children.Add(_modeTabs);
        return root;
    }

    private void DemoModeSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.Source, _modeTabs))
            SetTemplateClockActive(_isActive && _modeTabs.SelectedIndex == 0);
    }

    private Control CreateNormalPage()
    {
        AddScenario("editing", "编辑与校验", "双击名称、数量或说明单元格开始编辑。Enter 提交，Esc 取消；空名称或 0–100 以外的数量会显示错误。", ShowEditingScenario);
        AddScenario("tree", "树结构与节点增删", "点击名称前的箭头展开部门。下面的操作可以批量展开，或向第一个部门添加、删除成员。", ShowTree);
        AddScenario("lazy-tree", "按需加载树", "展开唯一的分组时才创建三个子节点。再次展开不会重复加载。", ShowLazyTree);
        AddScenario("columns", "列宽与自动换行", "比较文本、三态勾选和标签模板列。拖动列头右侧调宽；选择“按内容自动行高”查看完整换行说明。", ShowColumnTypes);
        AddScenario("visibility", "列显隐与冻结", "勾选要显示的列，观察冻结边界、当前单元格和选区；隐藏编辑列会丢弃草稿。", ShowVisibilityScenario);
        AddScenario("appearance", "网格线与空内容", "切换四种网格线，比较空源与筛选为空；试用自定义文字或带操作按钮的空内容。", ShowAppearanceScenario);
        AddScenario("rowmoving", "行与树节点移动", "拖动行号可在同组排序或移入其它组；按钮提供相同请求，编辑校验和取消请求会阻止移动。", ShowRowMovingScenario);
        AddScenario("nestedbindings", "嵌套绑定与类型化写回", "替换状态对象或子集合，清空后恢复引用；名称和数量以编译绑定显示，草稿校验成功后调用明确的类型化 setter。", ShowNestedBindingScenario);
        AddScenario("flat", "大数据 · 10 万行", "纵向滚动观察大数据表格。可在“布局选项”冻结列或改变行高，在“诊断与输入测试”查看容器复用。", () => ShowFlat(20));
        AddScenario("wide", "横向滚动 · 100 列", "向右滚动查看更多指标，冻结前两列后，编号和名称会一直留在左侧。", () => ShowFlat(100));
        AddScenario("template-buttons", "自定义列模板 · 按钮", "直接点击单元格里的按钮，体验行内操作和数据反馈。无需进入文本编辑。", () => ShowTemplateScenario(TemplateDemoKind.Buttons));
        AddScenario("template-colors", "自定义列模板 · 选色盘", "点击颜色按钮打开选色盘，挑选颜色后会同步显示颜色值和预览。", () => ShowTemplateScenario(TemplateDemoKind.Colors));
        AddScenario("template-clock", "自定义列模板 · 时钟", "查看实时更新的时钟，设置每行的计划时间，并用开关暂停或恢复时钟。", () => ShowTemplateScenario(TemplateDemoKind.Clock));
        AddScenario("template-composite", "自定义列模板 · 控件组合", "在一行中组合操作按钮、颜色、时钟和进度控件。各控件可直接操作，滚动后数据仍会保留。", () => ShowTemplateScenario(TemplateDemoKind.Composite));
        _scenarioPicker.ItemsSource = _scenarioChoices;
        _scenarioPicker.SelectionChanged += (_, _) =>
        {
            if (!_synchronizingOptions && _scenarioPicker.SelectedItem is DemoScenarioChoice choice) RunScenario(choice);
        };
        var scenarioTools = new WrapPanel { ItemSpacing = 10, LineSpacing = 6 };
        scenarioTools.Children.Add(new TextBlock { Text = "选择示例", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold });
        scenarioTools.Children.Add(_scenarioPicker);
        AddButton(scenarioTools, "重置当前示例", () =>
        {
            if (_scenarioPicker.SelectedItem is DemoScenarioChoice choice) RunScenario(choice);
        }, "重新生成当前示例的数据，丢弃该示例的修改和草稿。", "ResetScenarioButton");
        var scenarioHeader = new StackPanel { Spacing = 8, Children = { scenarioTools, _scenarioDescription } };

        _editingTools = ToolGroup("选择与编辑", "先点击单元格，再编辑；选择按钮用于快速体验范围和整行多选。", CreateEditingToolbar());
        var treeActions = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        AddButton(treeActions, "全部展开", () => WithTree(source => source.ExpandAll()), "展开每个根节点及其所有子节点。");
        AddButton(treeActions, "全部折叠", () => WithTree(source => source.CollapseAll()), "收起树节点，仅保留根节点。");
        AddButton(treeActions, "仅展开偶数部门", ExpandEvenRoots, "先折叠全部，再展开编号为偶数的部门。");
        AddButton(treeActions, "首个部门添加成员", AddChild, "向第一个部门添加一个成员，并展开该部门。");
        AddButton(treeActions, "首个部门删除末位成员", RemoveChild, "移除第一个部门的最后一个成员。");
        _treeTools = ToolGroup("树节点操作", "批量操作当前树；成员增删固定作用于第一个部门。", treeActions);

        var filters = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        filters.Children.Add(new TextBlock { Text = "名称", VerticalAlignment = VerticalAlignment.Center });
        filters.Children.Add(_filterText);
        filters.Children.Add(_minimumQuantity);
        ToolTip.SetTip(_minimumQuantity, "仅保留数量大于或等于此整数的记录，可留空。");
        AddButton(filters, "筛选", ApplyFilter, "按名称包含文字和数量下限筛选当前数据。", "ApplyFilterButton");
        AddButton(filters, "清除筛选", ClearFilter, "显示全部记录，不清除已有排序。");
        AddButton(filters, "恢复原顺序", () => { _source?.ClearSort(); _lastAction.Text = "排序已清除，恢复原始数据顺序。"; }, "清除列排序，按原始数据顺序显示。");
        _filterTools = ToolGroup("筛选与排序", "点击列头切换升序、降序；筛选只改变当前显示的数据。", filters);
        _p6Tools = CreateP6Tools();

        _frozenColumns.SelectionChanged += (_, _) =>
        {
            if (!_synchronizingOptions && _frozenColumns.SelectedIndex >= 0) _grid.FrozenColumnCount = _frozenColumns.SelectedIndex;
        };
        _rowHeightMode.SelectionChanged += (_, _) =>
        {
            if (_synchronizingOptions || _rowHeightMode.SelectedIndex < 0) return;
            _grid.AutoRowHeight = _rowHeightMode.SelectedIndex == 1;
            _grid.RowHeightSelector = _rowHeightMode.SelectedIndex == 2
                ? row => row.Model is DemoItem item && item.Id % 7 == 0 ? 52 : 28
                : null;
        };
        _threeStateSort.IsCheckedChanged += (_, _) =>
        {
            if (!_synchronizingOptions) _grid.IsThreeStateSort = _threeStateSort.IsChecked == true;
        };
        ToolTip.SetTip(_threeStateSort, "勾选后列头按“升序 → 降序 → 原顺序”循环；取消后只切换升序和降序。");
        var layoutOptions = new WrapPanel { ItemSpacing = 10, LineSpacing = 6 };
        layoutOptions.Children.Add(new TextBlock { Text = "冻结列", VerticalAlignment = VerticalAlignment.Center });
        layoutOptions.Children.Add(_frozenColumns);
        layoutOptions.Children.Add(new TextBlock { Text = "行高", VerticalAlignment = VerticalAlignment.Center });
        layoutOptions.Children.Add(_rowHeightMode);
        layoutOptions.Children.Add(_threeStateSort);
        var layoutExpander = new Expander { Header = "布局选项 · 冻结列、行高与排序循环", Content = layoutOptions, HorizontalAlignment = HorizontalAlignment.Stretch };
        var header = new StackPanel
        {
            Spacing = 10, Margin = new Thickness(0, 10, 0, 12),
            Children = { scenarioHeader, _editingTools, _treeTools, _filterTools, _p6Tools, layoutExpander },
        };

        var diagnostics = new StackPanel { Spacing = 8, Children = { _status } };
        diagnostics.Children.Add(new TextBlock { Text = "中文输入法测试：在下面输入文字，检查候选框以及 Enter / Esc / Tab。", TextWrapping = TextWrapping.Wrap });
        diagnostics.Children.Add(_input);
        var diagnosticTools = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        AddButton(diagnosticTools, "切换表格外观模板", ToggleApplicationTemplate, "切换整个表格的外观模板，保留数据与展开状态；与下拉框中的单元格控件模板不同。");
        diagnostics.Children.Add(diagnosticTools);
        diagnostics.Children.Add(new TextBlock { Text = "程序化中文赋值不替代真实输入法测试；其他平台和 DPI 的验收记录见项目文档。", TextWrapping = TextWrapping.Wrap });
        var footer = new StackPanel { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
        footer.Children.Add(_lastAction);
        footer.Children.Add(new Expander { Header = "诊断与输入测试", Content = diagnostics, HorizontalAlignment = HorizontalAlignment.Stretch });
        var page = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        page.Children.Add(header);
        page.Children.Add(footer);
        page.Children.Add(_grid);
        return page;
    }

    private static Control ToolGroup(string title, string description, Control actions) => new StackPanel
    {
        Spacing = 5,
        Children =
        {
            new TextBlock { Text = title, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Opacity = .8 },
            actions,
        },
    };

    private void AddScenario(string id, string label, string description, Action show) => _scenarioChoices.Add(new DemoScenarioChoice(id, label, description, show));

    private void RunScenario(DemoScenarioChoice choice)
    {
        _grid.CancelEdit();
        ClearNestedBindingScenario();
        _p6ScenarioId = null;
        _appearanceItems = null;
        _rowMoveRoots = null;
        _grid.GridLinesVisibility = TreeDataGridGridLinesVisibility.All;
        _grid.EmptyContent = "暂无数据";
        _grid.EmptyContentTemplate = null;
        _grid.CanUserReorderColumns = false;
        _grid.CanUserMoveRows = false;
        if (_customTemplate) ToggleApplicationTemplate();
        _grid.RowHeight = 28;
        _grid.IsReadOnly = false;
        _grid.SelectionUnit = TreeDataGridSelectionUnit.Row;
        _grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        _grid.FrozenColumnCount = 0;
        _grid.RowHeightSelector = null;
        _grid.AutoRowHeight = false;
        choice.Show();
        UpdateScenarioUi();
    }

    private void UpdateScenarioUi()
    {
        var editing = _source is FlatTreeDataGridSource<EditingItem>;
        var tree = _source is HierarchicalTreeDataGridSource<DemoItem>;
        var templates = _source is FlatTreeDataGridSource<TemplateDemoItem>;
        var choiceId = _p6ScenarioId ?? (editing ? "editing" : tree ? (_scenario.StartsWith("按需加载", StringComparison.Ordinal) ? "lazy-tree" : "tree")
            : templates ? (_scenarioPicker.SelectedItem as DemoScenarioChoice)?.Id
            : _grid.Columns.Count == 5 ? "columns" : _grid.Columns.Count == 100 ? "wide" : "flat");
        _synchronizingOptions = true;
        try
        {
            var choice = _scenarioChoices.FirstOrDefault(item => item.Id == choiceId);
            if (choice is not null) _scenarioPicker.SelectedItem = choice;
            _scenarioDescription.Text = choice?.Description ?? _scenario;
            _editingTools.IsVisible = editing;
            _treeTools.IsVisible = tree;
            _filterTools.IsVisible = !templates && _p6ScenarioId != "nestedbindings";
            _minimumQuantity.IsVisible = editing;
            _frozenColumns.SelectedIndex = Math.Min(2, _grid.FrozenColumnCount);
            _rowHeightMode.SelectedIndex = _grid.AutoRowHeight ? 1 : _grid.RowHeightSelector is not null ? 2 : 0;
            _rowHeightMode.IsEnabled = !templates;
            ToolTip.SetTip(_rowHeightMode, templates ? "控件模板示例已设置适合组合控件的行高。" : "固定行高、自动换行行高，或每七行增加高度。");
            _threeStateSort.IsChecked = _grid.IsThreeStateSort;
            RefreshP6Options();
        }
        finally { _synchronizingOptions = false; }
        UpdateActionAvailability();
    }

    private void DemoStateChanged(object? sender, EventArgs args) => UpdateActionAvailability();

    private void UpdateActionAvailability()
    {
        var editing = _grid.State?.EditSession is not null;
        _beginEditButton.IsEnabled = !editing && _source?.Rows.Count > 0 && _grid.VisibleColumns.Any(column => !column.IsReadOnly);
        _commitEditButton.IsEnabled = editing;
        _cancelEditButton.IsEnabled = editing;
    }

    private sealed record DemoScenarioChoice(string Id, string Label, string Description, Action Show)
    {
        public override string ToString() => Label;
    }
}
