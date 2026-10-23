using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Expander? _inputGesturesExamples;
    private InputGesturesBoard? _inputGesturesBoard;

    private Control CreateInputGesturesExamples()
    {
        _inputGesturesBoard = CreateInputGesturesBoard(true);
        return _inputGesturesExamples = new Expander
        {
            Name = "ChartInputGesturesExamples", Header = "输入完整性：触摸捏合、平移、触控板与嵌套滚动",
            HorizontalAlignment = HorizontalAlignment.Stretch, Content = _inputGesturesBoard.View,
        };
    }

    private InputGesturesBoard CreateInputGesturesBoard(bool register)
    {
        var chart = register ? CreateAdvancedChart("InputGesturesChart") : new CartesianChart { Name = "InputGesturesChart" };
        chart.Height = 380; chart.Title = "双指缩放观察区间 · 单指平移趋势";
        chart.XAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "时间 / 分钟", LabelFormat = "0.#" };
        chart.YAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "负载 / %", LabelFormat = "0.#" };
        chart.IsZoomEnabled = true; chart.IsPanEnabled = true; chart.IsTouchEnabled = true;
        chart.InteractionAxes = ChartInteractionAxes.Both;
        chart.WheelMode = ChartWheelMode.Auto; chart.InputRoutingMode = ChartInputRoutingMode.Auto;
        chart.WheelPanSensitivity = 48; chart.WheelZoomSensitivity = 1.2; chart.TouchPanThreshold = 8;
        chart.Viewport = new ChartViewport(20, 80, 20, 80);
        chart.Series.Add(new LineSeries { Title = "服务负载", Stroke = Blue, StrokeThickness = 3, MarkerSize = 6,
            ItemsSource = Enumerable.Range(0, 21).Select(i => new ChartPoint(i * 5, 25 + i * 1.6 + Math.Sin(i * .8) * 20)).ToArray() });
        chart.Series.Add(new ScatterSeries { Title = "检查点", Fill = Orange, Stroke = Orange, MarkerSize = 11,
            ItemsSource = new ChartPoint[] { new(15, 45), new(35, 65), new(55, 35), new(75, 72), new(95, 55) } });
        var status = new TextBlock { Name = "InputGesturesStatus", FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var lastInput = "等待输入";
        var host = new Border { Name = "InputGesturesChartHost", Padding = new Thickness(8), BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, Child = chart };
        var scroll = new ScrollViewer
        {
            Name = "InputGesturesNestedScroll", Height = 450,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = new StackPanel { Spacing = 12, Children =
            {
                new Border { Height = 140, Padding = new Thickness(16), Child = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
                    Text = "这是独立的可滚动容器。图优先时，图表能改变视图就处理输入；到达边界后，滚轮继续交给本容器。切换“嵌套自动”时，普通双指滚动交给容器，Ctrl / Meta 加滚动缩放图表。" } },
                host,
                new Border { Height = 360, Padding = new Thickness(16), Child = new TextBlock { VerticalAlignment = VerticalAlignment.Top, TextWrapping = TextWrapping.Wrap,
                    Text = "滚动容器的下方内容\n\n在图表之外滚动会继续移动页面；“容器优先”让图表内的滚轮同样用于滚动容器。触摸捏合由两个触点的距离决定，移动中心可以同时平移。物理触屏、触控板和 Linux 实机仍需单独体验验收。" } },
            } },
        };
        void Update()
        {
            var range = chart.VisibleRange;
            status.Text = $"最近输入：{lastInput}\n可见 X {range?.XMinimum:0.###}–{range?.XMaximum:0.###}；Y {range?.YMinimum:0.###}–{range?.YMaximum:0.###}；容器偏移 {scroll.Offset.Y:0.##} DIP\n路由 {chart.InputRoutingMode}；滚轮 {chart.WheelMode}；轴 {chart.InteractionAxes}；触摸阈值 {chart.TouchPanThreshold:0.#} DIP；平移灵敏度 {chart.WheelPanSensitivity:0.#} DIP/单位";
        }
        void ViewChanged(object? sender, EventArgs args) => Update();
        chart.ViewportChanged += ViewChanged;
        void ScrollChanged(object? sender, ScrollChangedEventArgs args) => Update();
        scroll.ScrollChanged += ScrollChanged;
        void PointerChanged(object? sender, PointerEventArgs args) { lastInput = $"{args.Pointer.Type} · {args.RoutedEvent?.Name}"; Update(); }
        void WheelChanged(object? sender, PointerWheelEventArgs args) { lastInput = $"连续滚动 Δ({args.Delta.X:0.###}, {args.Delta.Y:0.###}) · {args.KeyModifiers}"; Update(); }
        chart.AddHandler(InputElement.PointerPressedEvent, PointerChanged, RoutingStrategies.Bubble, true);
        chart.AddHandler(InputElement.PointerMovedEvent, PointerChanged, RoutingStrategies.Bubble, true);
        chart.AddHandler(InputElement.PointerReleasedEvent, PointerChanged, RoutingStrategies.Bubble, true);
        chart.AddHandler(InputElement.PointerWheelChangedEvent, WheelChanged, RoutingStrategies.Bubble, true);
        var touch = new CheckBox { Name = "InputGesturesTouchOption", Content = "触摸输入", IsChecked = true, Margin = new Thickness(0, 0, 8, 6) };
        touch.IsCheckedChanged += (_, _) => { chart.IsTouchEnabled = touch.IsChecked == true; Update(); };
        var axes = new ComboBox { Name = "InputGesturesAxes", ItemsSource = new[] { "双轴", "仅 X", "仅 Y", "关闭操作轴" }, SelectedIndex = 0, MinWidth = 120, Margin = new Thickness(0, 0, 8, 6) };
        axes.SelectionChanged += (_, _) => { chart.InteractionAxes = axes.SelectedIndex switch { 1 => ChartInteractionAxes.X, 2 => ChartInteractionAxes.Y, 3 => ChartInteractionAxes.None, _ => ChartInteractionAxes.Both }; Update(); };
        var wheel = new ComboBox { Name = "InputGesturesWheelMode", ItemsSource = new[] { "平移，Ctrl / Meta 缩放", "滚动缩放", "滚动平移", "关闭滚轮" }, SelectedIndex = 0, MinWidth = 195, Margin = new Thickness(0, 0, 8, 6) };
        wheel.SelectionChanged += (_, _) => { chart.WheelMode = wheel.SelectedIndex switch { 1 => ChartWheelMode.Zoom, 2 => ChartWheelMode.Pan, 3 => ChartWheelMode.Disabled, _ => ChartWheelMode.Auto }; Update(); };
        var routing = new ComboBox { Name = "InputGesturesRouting", ItemsSource = new[] { "图优先，边界交父", "嵌套自动", "容器优先" }, SelectedIndex = 1, MinWidth = 170, Margin = new Thickness(0, 0, 8, 6) };
        routing.SelectionChanged += (_, _) => { chart.InputRoutingMode = routing.SelectedIndex switch { 1 => ChartInputRoutingMode.Auto, 2 => ChartInputRoutingMode.ScrollViewer, _ => ChartInputRoutingMode.Chart }; Update(); };
        var sensitivity = new ComboBox { Name = "InputGesturesSensitivity", ItemsSource = new[] { "平移 48 DIP / 单位", "平移 24 DIP / 单位", "平移 96 DIP / 单位" }, SelectedIndex = 0, MinWidth = 170, Margin = new Thickness(0, 0, 8, 6) };
        sensitivity.SelectionChanged += (_, _) => { chart.WheelPanSensitivity = sensitivity.SelectedIndex switch { 1 => 24, 2 => 96, _ => 48 }; Update(); };
        var threshold = new ComboBox { Name = "InputGesturesThreshold", ItemsSource = new[] { "触摸阈值 8 DIP", "触摸阈值 16 DIP" }, SelectedIndex = 0, MinWidth = 155, Margin = new Thickness(0, 0, 8, 6) };
        threshold.SelectionChanged += (_, _) => { chart.TouchPanThreshold = threshold.SelectedIndex == 0 ? 8 : 16; Update(); };
        var actions = new WrapPanel { Children = { touch, axes, wheel, routing, sensitivity, threshold } };
        AddAction(actions, "取消手势并恢复视图", () => { chart.ResetViewport(); chart.Viewport = new ChartViewport(20, 80, 20, 80); lastInput = "已取消并恢复 20–80"; Update(); }, "释放正在捕获的触点，并回到演示观察区间。");
        AddAction(actions, "全部范围 / 边界交父", () => { chart.ResetViewport(); lastInput = "全部范围；继续滚动以体验容器接管"; Update(); }, "全范围下无法继续平移，输入交给滚动容器。");
        var view = new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = "单指拖动平移，双指捏合缩放并移动中心。触控板连续 X / Y 滚动不取整；图优先模式下用于平移，Ctrl / Meta 加滚动缩放，Shift 加纵向滚动改为横向平移。平台没有可靠的硬件种类标识，模式由此处明确选择。", TextWrapping = TextWrapping.Wrap, Opacity = .8 },
            actions, scroll, status,
        } };
        view.AttachedToVisualTree += (_, _) => Update();
        return new(view, chart, host, scroll, status, () =>
        {
            chart.ViewportChanged -= ViewChanged; scroll.ScrollChanged -= ScrollChanged;
            chart.RemoveHandler(InputElement.PointerPressedEvent, PointerChanged);
            chart.RemoveHandler(InputElement.PointerMovedEvent, PointerChanged);
            chart.RemoveHandler(InputElement.PointerReleasedEvent, PointerChanged);
            chart.RemoveHandler(InputElement.PointerWheelChangedEvent, WheelChanged);
            chart.ResetViewport();
        });
    }

    private sealed record InputGesturesBoard(StackPanel View, CartesianChart Chart, Border ChartHost, ScrollViewer Scroll,
        TextBlock Status, Action Release) : IDisposable
    { public void Dispose() => Release(); }
}
