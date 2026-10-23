using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private DispatcherTimer? _templateClockTimer;
    private IReadOnlyList<TemplateDemoItem>? _templateClockItems;
    private bool _templateClockActive;

    private void ShowTemplateScenario(TemplateDemoKind kind)
    {
        _grid.Source = null;
        _treeRoots = null;
        _grid.RowHeightSelector = null;
        _grid.AutoRowHeight = false;
        _grid.FrozenColumnCount = 1;
        _grid.IsReadOnly = true;
        _grid.SelectionUnit = TreeDataGridSelectionUnit.Cell;
        _grid.SelectionMode = TreeDataGridSelectionMode.Single;
        _grid.Columns.Clear();
        _grid.Columns.Add(new TreeDataGridRowHeaderColumn { Header = "行号", Width = new GridLength(60), IsReadOnly = true });
        _grid.Columns.Add(new TreeDataGridTextColumn
        {
            Header = "设备（可排序）", Width = new GridLength(150), IsReadOnly = true,
            ValueGetter = model => ((TemplateDemoItem)model).Name,
        });

        var (name, description) = kind switch
        {
            TemplateDemoKind.Buttons => ("操作按钮", "点击“暂停 / 启动”切换状态；运行时“执行 +1”累加次数，“重置”清零。按钮直接操作这一行的模型。"),
            TemplateDemoKind.Colors => ("选色盘", "点击 HEX 色值打开选色盘；选中颜色后，色块和右侧文字立即同步。排序或滚动后每行仍保留自己的颜色。"),
            TemplateDemoKind.Clock => ("时钟与时间选择", "模拟与数字时钟每秒同步；开关暂停本行时钟。TimePicker 设置独立的计划时间，可点击小时 / 分钟用键盘调整。"),
            _ => ("组合控件", "同一单元格组合操作按钮、选色盘、实时时钟、时间选择、开关和滑块；右列实时展示当前状态。拖动滑块调整进度，进度条同步更新。"),
        };
        switch (kind)
        {
            case TemplateDemoKind.Buttons:
                _grid.RowHeight = 60;
                _grid.Columns.Add(CreateTemplateDemoColumn("操作与执行次数", 410, BuildTemplateActions));
                _grid.Columns.Add(CreateTemplateDemoColumn("当前状态", 260, state => CreateTemplateText(state, nameof(TemplateDemoState.RunStatus))));
                break;
            case TemplateDemoKind.Colors:
                _grid.RowHeight = 60;
                _grid.Columns.Add(CreateTemplateDemoColumn("标签颜色 · 点击选择", 210, state => new TemplateColorPicker { DataContext = state }));
                _grid.Columns.Add(CreateTemplateDemoColumn("选中颜色预览", 290, BuildTemplateColorPreview));
                break;
            case TemplateDemoKind.Clock:
                _grid.RowHeight = 80;
                _grid.Columns.Add(CreateTemplateDemoColumn("实时时钟 · 计划时间 · 开关", 560, BuildTemplateClock));
                _grid.Columns.Add(CreateTemplateDemoColumn("计划时间", 190, state => CreateTemplateText(state, nameof(TemplateDemoState.TargetTimeText))));
                break;
            default:
                _grid.RowHeight = 110;
                _grid.Columns.Add(CreateTemplateDemoColumn("组合模板 · 控件直接交互", 730, BuildCompositeTemplate));
                _grid.Columns.Add(CreateTemplateDemoColumn("模型状态", 260, state => CreateTemplateText(state, nameof(TemplateDemoState.Summary))));
                break;
        }

        var items = Enumerable.Range(1, 24).Select(index => new TemplateDemoItem(index)).ToArray();
        SetSource(new FlatTreeDataGridSource<TemplateDemoItem>(items), $"自定义列模板 · {name} · 24 行");
        _filterText.Text = "";
        _minimumQuantity.Text = "";
        _lastAction.Text = description;
        if (kind is TemplateDemoKind.Clock or TemplateDemoKind.Composite)
        {
            _templateClockItems = items;
            SetTemplateClockActive(_templateClockActive);
        }
    }

    private static TreeDataGridTemplateColumn CreateTemplateDemoColumn(string header, double width, Func<TemplateDemoState, Control> build) => new()
    {
        Header = header, Width = new GridLength(width), IsReadOnly = true, CanUserSort = false,
        ValueGetter = model => ((TemplateDemoItem)model).State,
        CellTemplate = new FuncDataTemplate<TemplateDemoState>((state, _) => state is null ? new TextBlock() : build(state)),
    };

    private Control BuildTemplateActions(TemplateDemoState state)
    {
        var panel = new StackPanel { DataContext = state, Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var run = CreateTemplateButton("", nameof(TemplateDemoState.ToggleRunningCommand));
        run.Bind(ContentControl.ContentProperty, new ReflectionBinding(nameof(TemplateDemoState.RunActionText)));
        ToolTip.SetTip(run, "切换本行运行状态；暂停时不能执行 +1");
        var increment = CreateTemplateButton("执行 +1", nameof(TemplateDemoState.IncrementCommand));
        ToolTip.SetTip(increment, "本行执行次数加 1");
        var reset = CreateTemplateButton("重置", nameof(TemplateDemoState.ResetCommand));
        ToolTip.SetTip(reset, "清零本行执行次数和进度");
        panel.Children.Add(run);
        panel.Children.Add(increment);
        panel.Children.Add(reset);
        panel.Children.Add(CreateTemplateText(state, nameof(TemplateDemoState.CounterText)));
        return panel;
    }

    private Button CreateTemplateButton(string text, string commandProperty)
    {
        var button = new Button { Content = text, MinHeight = 30, Padding = new Thickness(9, 4) };
        // Bind to the current DataContext so recycled cells execute the current row's command.
        button.Bind(Button.CommandProperty, new ReflectionBinding(commandProperty));
        button.Click += (sender, _) =>
        {
            if (sender is not Button { DataContext: TemplateDemoState item }) return;
            // Button executes its bound command after raising Click; show the resulting state.
            Dispatcher.UIThread.Post(() => _lastAction.Text = $"{item.Name}：{item.RunStatus}，{item.CounterText}，{item.ProgressText}。");
        };
        return button;
    }

    private static Control BuildTemplateColorPreview(TemplateDemoState state)
    {
        var preview = new Border { Width = 48, Height = 28, CornerRadius = new CornerRadius(4) };
        preview.Bind(Border.BackgroundProperty, new ReflectionBinding(nameof(TemplateDemoState.ColorBrush)));
        return new StackPanel
        {
            DataContext = state, Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center,
            Children = { preview, CreateTemplateText(state, nameof(TemplateDemoState.ColorDescription)) },
        };
    }

    private static Control BuildTemplateClock(TemplateDemoState state)
    {
        var panel = new StackPanel { DataContext = state, Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(CreateClockFace(state));
        var digital = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Width = 100 };
        var time = CreateTemplateText(state, nameof(TemplateDemoState.CurrentTimeText));
        time.FontFamily = new FontFamily("Consolas");
        time.FontSize = 18;
        digital.Children.Add(time);
        digital.Children.Add(CreateTemplateText(state, nameof(TemplateDemoState.RunStatus), 12));
        panel.Children.Add(digital);
        panel.Children.Add(CreateTemplateTimePicker(state));
        panel.Children.Add(CreateTemplateClockSwitch(state));
        return panel;
    }

    private Control BuildCompositeTemplate(TemplateDemoState state)
    {
        var panel = new StackPanel { DataContext = state, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var upper = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        upper.Children.Add(BuildTemplateActions(state));
        upper.Children.Add(new TemplateColorPicker { DataContext = state });
        var lower = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        lower.Children.Add(BuildTemplateClock(state));
        var progress = new StackPanel { Spacing = 2, Width = 135, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider { Minimum = 0, Maximum = 100, Height = 22, MinHeight = 0 };
        slider.Bind(RangeBase.ValueProperty, new ReflectionBinding(nameof(TemplateDemoState.Progress)) { Mode = BindingMode.TwoWay });
        ToolTip.SetTip(slider, "调整本行完成进度；支持方向键");
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 4, MinHeight = 0 };
        bar.Bind(RangeBase.ValueProperty, new ReflectionBinding(nameof(TemplateDemoState.Progress)));
        progress.Children.Add(slider);
        progress.Children.Add(bar);
        progress.Children.Add(CreateTemplateText(state, nameof(TemplateDemoState.ProgressText), 11));
        lower.Children.Add(progress);
        panel.Children.Add(upper);
        panel.Children.Add(lower);
        return panel;
    }

    private static TemplateClockFace CreateClockFace(TemplateDemoState state)
    {
        var face = new TemplateClockFace { DataContext = state, VerticalAlignment = VerticalAlignment.Center };
        face.Bind(TemplateClockFace.TimeProperty, new ReflectionBinding(nameof(TemplateDemoState.ClockTime)));
        ToolTip.SetTip(face, "实时模拟时钟；与右侧数字时钟同步");
        return face;
    }

    private static Control CreateTemplateTimePicker(TemplateDemoState state)
    {
        var picker = new TimePicker { ClockIdentifier = "24HourClock", Width = 146, MinWidth = 146, MinHeight = 30 };
        picker.Bind(TimePicker.SelectedTimeProperty, new ReflectionBinding(nameof(TemplateDemoState.TargetTime)) { Mode = BindingMode.TwoWay });
        ToolTip.SetTip(picker, "设置本行计划时间，不改变当前时钟");
        return new StackPanel
        {
            DataContext = state, Spacing = 2, VerticalAlignment = VerticalAlignment.Center,
            Children = { new TextBlock { Text = "计划时间", FontSize = 11 }, picker },
        };
    }

    private static ToggleSwitch CreateTemplateClockSwitch(TemplateDemoState state)
    {
        var toggle = new ToggleSwitch { DataContext = state, OnContent = "运行", OffContent = "暂停", MinWidth = 98, VerticalAlignment = VerticalAlignment.Center };
        toggle.Bind(ToggleButton.IsCheckedProperty, new ReflectionBinding(nameof(TemplateDemoState.IsRunning)) { Mode = BindingMode.TwoWay });
        ToolTip.SetTip(toggle, "暂停 / 恢复本行时钟，其他行继续运行");
        return toggle;
    }

    private static TextBlock CreateTemplateText(TemplateDemoState state, string property, double fontSize = 13)
    {
        var text = new TextBlock { DataContext = state, FontSize = fontSize, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        text.Bind(TextBlock.TextProperty, new ReflectionBinding(property));
        return text;
    }

    private void StopTemplateClock()
    {
        _templateClockTimer?.Stop();
        _templateClockItems = null;
    }

    private void SetTemplateClockActive(bool active)
    {
        _templateClockActive = active && _isActive && !_disposed && _modeTabs.SelectedIndex == 0;
        if (!_templateClockActive || _templateClockItems is null)
        {
            _templateClockTimer?.Stop();
            return;
        }
        _templateClockTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => TickTemplateClocks());
        TickTemplateClocks();
        _templateClockTimer.Start();
    }

    private void TickTemplateClocks()
    {
        if (!_templateClockActive || _disposed || _templateClockItems is null) return;
        var now = DateTime.Now;
        foreach (var item in _templateClockItems) item.State.Tick(now);
    }
}
