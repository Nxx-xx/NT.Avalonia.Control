using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private string? _p6ScenarioId;
    private ObservableCollection<DemoItem>? _appearanceItems;
    private readonly WrapPanel _columnVisibilityOptions = new() { ItemSpacing = 10, LineSpacing = 6 };
    private readonly ComboBox _gridLinesMode = new()
    {
        Name = "GridLinesMode", Width = 140,
        ItemsSource = new[] { "无网格线", "仅横线", "仅竖线", "横线与竖线" },
    };
    private readonly ComboBox _emptyPresentation = new()
    {
        Name = "EmptyPresentation", Width = 170,
        ItemsSource = new[] { "默认空内容", "自定义文字", "自定义内容模板" },
    };
    private Control _p6Tools = null!;
    private Control _appearanceTools = null!;

    private Control CreateP6Tools()
    {
        var columns = new StackPanel { Spacing = 6 };
        columns.Children.Add(new TextBlock
        {
            Text = "勾选要显示的列；冻结数量按当前可见列计算。隐藏正在编辑的列会取消草稿。拖动列头可在冻结区或滚动区内重排，不能跨冻结边界。",
            TextWrapping = TextWrapping.Wrap,
        });
        columns.Children.Add(_columnVisibilityOptions);
        var columnActions = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        AddButton(columnActions, "全部隐藏", () => SetAllColumnVisibility(false), "隐藏全部列，保留行选择；恢复后回到该行的首个可见列。", "HideAllColumnsButton");
        AddButton(columnActions, "全部恢复", () => SetAllColumnVisibility(true), "按原有顺序恢复全部列与宽度。", "RestoreAllColumnsButton");
        columns.Children.Add(columnActions);

        var appearance = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        appearance.Children.Add(new TextBlock { Text = "网格线", VerticalAlignment = VerticalAlignment.Center });
        appearance.Children.Add(_gridLinesMode);
        appearance.Children.Add(new TextBlock { Text = "空内容", VerticalAlignment = VerticalAlignment.Center });
        appearance.Children.Add(_emptyPresentation);
        _gridLinesMode.SelectionChanged += (_, _) =>
        {
            if (!_synchronizingOptions && _gridLinesMode.SelectedIndex >= 0)
                _grid.GridLinesVisibility = (TreeDataGridGridLinesVisibility)_gridLinesMode.SelectedIndex;
        };
        _emptyPresentation.SelectionChanged += (_, _) =>
        {
            if (!_synchronizingOptions) ApplyEmptyPresentation(_emptyPresentation.SelectedIndex);
        };
        var dataActions = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        AddButton(dataActions, "筛选为无匹配项", () =>
        {
            _source?.SetFilter(_ => false);
            _lastAction.Text = "数据仍在源集合中，当前筛选没有匹配项。";
        }, "保留源数据，观察筛选为空时的空内容。", "FilterToEmptyButton");
        AddButton(dataActions, "清空数据", () =>
        {
            _appearanceItems?.Clear();
            _source?.SetFilter(null);
            _lastAction.Text = "源集合已清空；点击“重新填充数据”恢复此示例。";
        }, "实际清空此示例的源集合。", "ClearAppearanceDataButton");
        AddButton(dataActions, "重新填充数据", RestoreAppearanceData, "恢复 32 条示例记录并清除筛选。", "RestoreAppearanceDataButton");
        _appearanceTools = new StackPanel { Spacing = 6, Children = { appearance, dataActions } };
        _rowMoveTools = CreateRowMoveTools();
        _nestedBindingTools = CreateNestedBindingTools();
        return new Expander
        {
            Name = "P6Options", Header = "列显示、外观与数据操作", IsExpanded = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { Spacing = 8, Children = { columns, _appearanceTools, _rowMoveTools, _nestedBindingTools } },
        };
    }

    private void ShowVisibilityScenario()
    {
        ShowEditingScenario();
        _grid.CanUserReorderColumns = true;
        _p6ScenarioId = "visibility";
        _scenario = "列显隐 · 可见列冻结 · 隐藏草稿零写回";
        _lastAction.Text = "选择单元格后隐藏该列，观察当前项移到右侧再左侧；编辑名称时隐藏名称列，草稿不会写回。";
        UpdateScenarioUi();
    }

    private void ShowAppearanceScenario()
    {
        ShowColumnTypes();
        _grid.CanUserReorderColumns = true;
        _p6ScenarioId = "appearance";
        _appearanceItems = new ObservableCollection<DemoItem>(CreateAppearanceItems());
        SetSource(new FlatTreeDataGridSource<DemoItem>(_appearanceItems), "四种网格线 · 空源 / 筛选为空 · 自定义空内容");
        _lastAction.Text = "切换四种网格线，再筛选为空或清空源集合；空内容可用文字或可操作模板。";
        UpdateScenarioUi();
    }

    private static IEnumerable<DemoItem> CreateAppearanceItems() => Enumerable.Range(0, 32)
        .Select(i => new DemoItem(i, $"外观样例 {i:00}"));

    private void RestoreAppearanceData()
    {
        if (_appearanceItems is null) return;
        if (_appearanceItems.Count == 0)
            foreach (var item in CreateAppearanceItems()) _appearanceItems.Add(item);
        _source?.SetFilter(null);
        _lastAction.Text = "已显示 32 条示例数据，空内容已收起。";
    }

    private void ApplyEmptyPresentation(int mode)
    {
        _grid.EmptyContentTemplate = null;
        _grid.EmptyContent = mode switch
        {
            1 => "没有匹配的记录，请清除筛选或重新填充数据。",
            2 => "当前没有可显示的记录",
            _ => "暂无数据",
        };
        if (mode != 2) return;
        _grid.EmptyContentTemplate = new FuncDataTemplate<string>((text, _) =>
        {
            var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            AddButton(actions, "显示示例数据", RestoreAppearanceData, "清除筛选，必要时重新填充示例记录。", "EmptyRestoreDataButton");
            return new StackPanel
            {
                Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = text, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
                    actions,
                },
            };
        });
    }

    private void SetAllColumnVisibility(bool visible)
    {
        foreach (var column in _grid.Columns) column.IsVisible = visible;
        RefreshColumnVisibilityOptions();
        _lastAction.Text = visible ? "全部列按原顺序恢复，列宽保留。" : "全部列已隐藏，行选择保留。";
    }

    private void RefreshP6Options()
    {
        _p6Tools.IsVisible = _p6ScenarioId is not null;
        _appearanceTools.IsVisible = _p6ScenarioId == "appearance";
        _rowMoveTools.IsVisible = _p6ScenarioId == "rowmoving";
        _nestedBindingTools.IsVisible = _p6ScenarioId == "nestedbindings";
        if (_p6ScenarioId is null) return;
        RefreshColumnVisibilityOptions();
        _gridLinesMode.SelectedIndex = (int)_grid.GridLinesVisibility;
        _emptyPresentation.SelectedIndex = _grid.EmptyContentTemplate is not null ? 2
            : Equals(_grid.EmptyContent, "暂无数据") ? 0 : 1;
    }

    private void RefreshColumnVisibilityOptions()
    {
        _columnVisibilityOptions.Children.Clear();
        foreach (var column in _grid.Columns)
        {
            var toggle = new CheckBox { Content = column.Header, IsChecked = column.IsVisible };
            toggle.IsCheckedChanged += (_, _) =>
            {
                column.IsVisible = toggle.IsChecked == true;
                _lastAction.Text = column.IsVisible ? $"已恢复“{column.Header}”。" : $"已隐藏“{column.Header}”；该列草稿取消，数据保持原值。";
            };
            _columnVisibilityOptions.Children.Add(toggle);
        }
    }
}
