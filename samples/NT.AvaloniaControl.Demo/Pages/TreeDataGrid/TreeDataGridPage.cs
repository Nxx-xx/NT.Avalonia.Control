using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using NT.AvaloniaControl.Demo.Navigation;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage : UserControl, IDemoPage
{
    private const string ChineseProbe = "中文输入探针：树形表格，提交与取消。";
    private readonly Window _hostWindow;
    private readonly GridControl _grid = new() { MinHeight = 180 };
    private readonly TextBox _input = new() { PlaceholderText = "在此手动验证中文预编辑、候选框、Enter / Esc / Tab", MinWidth = 400 };
    private readonly TextBox _filterText = new() { PlaceholderText = "名称包含…", Width = 220 };
    private readonly TextBox _minimumQuantity = new() { PlaceholderText = "数量下限", Width = 100 };
    private readonly TextBlock _status = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _lastAction = new() { Text = "拖动列头右侧边缘可调宽；树操作按钮切换到树样例。", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Stopwatch _startup = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer;
    private TreeDataGridSource? _source;
    private string _scenario = "";
    private ObservableCollection<DemoItem>? _treeRoots;
    private int _lazyLoadCount;
    private int _nextDynamicId = 20_000;
    private bool _customTemplate;
    private bool _isActive;
    private bool _disposed;

    public TreeDataGridPage(Window hostWindow, string[] args)
    {
        _hostWindow = hostWindow;
        Content = CreateDemoLayout(args);
        if (args.Contains("--smoke", StringComparer.Ordinal)) ShowFlat(20);
        else ShowEditingScenario();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) =>
        {
            if (_isActive && !_disposed) UpdateStatus();
        });
        _timer.Stop();
    }

    public Control View => this;

    public void Activate()
    {
        if (_disposed || _isActive) return;
        _isActive = true;
        SetTemplateClockActive(_modeTabs.SelectedIndex == 0);
        UpdateStatus();
        _timer.Start();
    }

    public void Deactivate()
    {
        _isActive = false;
        _timer.Stop();
        SetTemplateClockActive(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Deactivate();
        StopTemplateClock();
        _templateClockTimer = null;
        _modeTabs.SelectionChanged -= DemoModeSelectionChanged;
        if (_observedState is not null) _observedState.Changed -= DemoStateChanged;
        _observedState = null;
        ClearNestedBindingScenario();
        _grid.Source = null;
        _source?.Dispose();
        _source = null;
        _treeRoots = null;
        _editingItems = null;
        _appearanceItems = null;
        _rowMoveRoots = null;
        _mvvmView.Dispose();
    }

    private static Button AddButton(Panel panel, string text, Action action, string? description = null, string? name = null)
    {
        var button = new Button { Content = text, Name = name, Command = new RelayCommand(action) };
        ToolTip.SetTip(button, description ?? text);
        panel.Children.Add(button);
        return button;
    }

    private void SetSource(TreeDataGridSource source, string scenario)
    {
        StopTemplateClock();
        if (_observedState is not null) _observedState.Changed -= DemoStateChanged;
        _grid.Source = null;
        _source?.Dispose();
        _source = source;
        _scenario = scenario;
        _grid.Source = source;
        if (source is not FlatTreeDataGridSource<TemplateDemoItem>) _grid.RowHeight = 28;
        _observedState = _grid.State;
        if (_observedState is not null) _observedState.Changed += DemoStateChanged;
        UpdateScenarioUi();
    }

    private void SetColumns(int count)
    {
        ClearNestedBindingScenario();
        _grid.Source = null;
        _grid.Columns.Clear();
        _grid.Columns.Add(new TreeDataGridTextColumn { Header = "编号 / 节点", Width = new GridLength(180), ValueGetter = model => ((DemoItem)model).Id });
        _grid.Columns.Add(new TreeDataGridTextColumn { Header = "名称", Width = new GridLength(240), ValueGetter = model => ((DemoItem)model).Name });
        for (var i = 2; i < count; i++)
        {
            var columnIndex = i;
            _grid.Columns.Add(new TreeDataGridTextColumn
            {
                Header = $"指标 {i - 1:00}", Width = new GridLength(145),
                ValueGetter = model => $"{((DemoItem)model).Id:N0} / {columnIndex:00}",
            });
        }
    }

    private void ShowFlat(int columnCount)
    {
        _treeRoots = null;
        _grid.AutoRowHeight = false;
        SetColumns(columnCount);
        _grid.RowHeightSelector = null;
        SetSource(new FlatTreeDataGridSource<DemoItem>(Enumerable.Range(0, 100_000).Select(i => new DemoItem(i, $"平面数据 {i:N0}")).ToArray()), $"100,000 行 × {columnCount} 列");
        _lastAction.Text = "横纵滚动观察行和单元格容器数量；开启冻结与可变行高，检查列边界和顶部节点锚点。";
    }

    private void ShowTree()
    {
        ConfigureTreeColumns();
        _grid.AutoRowHeight = false;
        _grid.RowHeightSelector = null;
        var roots = new ObservableCollection<DemoItem>();
        for (var root = 0; root < 100; root++)
        {
            var item = new DemoItem(root, $"部门 {root:00}") { IsExpanded = root < 3 };
            for (var child = 0; child < 8; child++)
                item.Children.Add(new DemoItem(1000 + root * 8 + child, $"成员 {root:00}.{child}"));
            roots.Add(item);
        }
        _treeRoots = roots;
        SetSource(CreateTreeSource(roots), "树样例 · 100 个根（每根初始 8 个子节点）");
        _lastAction.Text = "点击名称前的展开器，或使用批量 / 条件展开按钮。动态增删按钮固定操作首个根节点。";
    }

    private void UpdateStatus()
    {
        UpdateActionAvailability();
        var presenter = _grid.RowsPresenter;
        _status.Text = $"{_scenario} · 可见投影 {_source?.Rows.Count:N0} · 实现 {presenter?.RealizedRowCount ?? 0} 行 / {presenter?.RealizedRows.Sum(row => row.RealizedCellCount) ?? 0} 单元格 · 行创建 {presenter?.CreatedRowCount ?? 0} / 复用 {presenter?.RecycledRowCount ?? 0}\n" +
            $"{RuntimeInformation.OSDescription} · {RuntimeInformation.ProcessArchitecture} · .NET {Environment.Version} · 窗口 {_hostWindow.ClientSize.Width:0}×{_hostWindow.ClientSize.Height:0} DIP · 缩放 {_hostWindow.RenderScaling:0.##}";
        if (_grid.State?.EditSession?.ValidationError is { } validationError)
            _status.Text += "\n验证失败：" + validationError;
    }

}
