using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Expander? _animationExamples;
    private AnimationBoard? _animationBoard;

    private Control CreateAnimationExamples()
    {
        _animationBoard = CreateAnimationBoard(true);
        return _animationExamples = new Expander
        {
            Name = "ChartAnimationExamples", Header = "动画：入场、数据与视图变化",
            HorizontalAlignment = HorizontalAlignment.Stretch, Content = _animationBoard.View,
        };
    }

    private AnimationBoard CreateAnimationBoard(bool register)
    {
        var chart = register ? CreateAdvancedChart("AnimationChart") : new CartesianChart { Name = "AnimationChart" };
        chart.Height = 360; chart.Title = "折线、柱状与散点共用动画时间线";
        chart.XAxis = new NumericAxis { Minimum = 0, Maximum = 12, Title = "时间 / 小时" };
        chart.YAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "负载 / %" };
        chart.IsTooltipEnabled = true; chart.IsZoomEnabled = true; chart.IsPanEnabled = true;
        chart.AnimationTriggers = ChartAnimationTriggers.All;
        chart.AnimationDuration = TimeSpan.FromMilliseconds(1200);
        chart.AnimationEasing = new CubicEaseOut();
        var line = new LineSeries { Title = "趋势", Stroke = Blue, StrokeThickness = 3, MarkerSize = 5 };
        var bars = new BarSeries { Title = "采样", Fill = Brush("#6019A798"), Stroke = Teal };
        var scatter = new ScatterSeries { Title = "观察点", Fill = Orange, Stroke = Orange, MarkerSize = 12 };
        chart.Series.Add(bars); chart.Series.Add(line); chart.Series.Add(scatter);
        var revision = 0;
        void SetData(bool large = false)
        {
            revision++;
            var points = large ? 12000 : 12;
            line.ItemsSource = Enumerable.Range(0, points).Select(index => new ChartPoint((index + 1) * 12d / points,
                45 + Math.Sin(index * (large ? .012 : .57) + revision * .8) * 27)).ToArray();
            bars.ItemsSource = Enumerable.Range(1, 6).Select(index => new ChartPoint(index * 2 - .5, 28 + (index * 13 + revision * 17) % 45)).ToArray();
            scatter.ItemsSource = new ChartPoint[] { new(2.5, revision % 2 == 0 ? 28 : 78), new(6.5, revision % 2 == 0 ? 76 : 36), new(10.5, revision % 2 == 0 ? 42 : 86) };
        }
        SetData();
        var options = new WrapPanel();
        var enabled = new CheckBox { Name = "AnimationEnabled", Content = "启用动画", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        enabled.IsCheckedChanged += (_, _) => chart.AnimationTriggers = enabled.IsChecked == true ? ChartAnimationTriggers.All : ChartAnimationTriggers.None;
        var reduced = new CheckBox { Name = "AnimationReducedMotion", Content = "减少动态效果", IsChecked = false, Margin = new Thickness(0, 0, 12, 6) };
        reduced.IsCheckedChanged += (_, _) => chart.ReduceMotion = reduced.IsChecked == true;
        var pause = new CheckBox { Name = "AnimationPause", Content = "暂停", IsChecked = false, Margin = new Thickness(0, 0, 12, 6) };
        pause.IsCheckedChanged += (_, _) => chart.IsAnimationPaused = pause.IsChecked == true;
        var duration = new ComboBox { Name = "AnimationDuration", ItemsSource = new[] { "时长 1.2 秒", "时长 0.35 秒", "时长 3 秒" }, SelectedIndex = 0, MinWidth = 132, Margin = new Thickness(0, 0, 8, 6) };
        duration.SelectionChanged += (_, _) => chart.AnimationDuration = TimeSpan.FromMilliseconds(duration.SelectedIndex switch { 1 => 350, 2 => 3000, _ => 1200 });
        var easing = new ComboBox { Name = "AnimationEasing", ItemsSource = new[] { "三次减速", "线性", "三次加减速" }, SelectedIndex = 0, MinWidth = 124, Margin = new Thickness(0, 0, 8, 6) };
        easing.SelectionChanged += (_, _) => chart.AnimationEasing = easing.SelectedIndex switch { 1 => new LinearEasing(), 2 => new CubicEaseInOut(), _ => new CubicEaseOut() };
        var delay = new ComboBox { Name = "AnimationDelay", ItemsSource = new[] { "无延迟", "延迟 0.5 秒", "延迟 1 秒" }, SelectedIndex = 0, MinWidth = 132, Margin = new Thickness(0, 0, 8, 6) };
        delay.SelectionChanged += (_, _) => chart.AnimationDelay = TimeSpan.FromMilliseconds(delay.SelectedIndex switch { 1 => 500, 2 => 1000, _ => 0 });
        options.Children.Add(enabled); options.Children.Add(reduced); options.Children.Add(pause);
        options.Children.Add(duration); options.Children.Add(easing); options.Children.Add(delay);
        var actions = new WrapPanel();
        AddAction(actions, "重播入场", () => chart.ReplayEntranceAnimation(), "从基线重播当前数据的入场动画。");
        AddAction(actions, "更新数据", () => SetData(), "替换折线、柱状和散点数据；连续更新以最新目标为准。");
        AddAction(actions, "缩放视图", () => chart.Viewport = chart.Viewport is null ? new ChartViewport(2, 10, 10, 90) : null, "切换完整与局部视图；坐标轴、标注和系列沿同一视图更新。");
        AddAction(actions, "取消并完成", () => { chart.CancelAnimations(); pause.IsChecked = false; }, "结束当前时间线，直接显示最新目标。");
        AddAction(actions, "大数据跳过示例", () => SetData(true), "12000 个折线点超过本例的 10000 点动画上限，直接显示结果。");
        AddAction(actions, "恢复小数据", () => SetData(), "返回 12 点折线、6 个柱和 3 个散点。");
        var status = new TextBlock { Name = "AnimationStatus", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = .8 };
        void UpdateStatus() => status.Text = $"状态：{chart.AnimationState} · 进度 {chart.AnimationProgress:P0} · 当前折线 {((ChartPoint[])line.ItemsSource!).Length:N0} 点 · 动画上限 {chart.AnimationPointLimit:N0} 点";
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        void Tick(object? sender, EventArgs args) => UpdateStatus();
        timer.Tick += Tick;
        var export = new Button { Name = "AnimationExport", Content = "导出当前画面 PNG", Margin = new Thickness(0, 0, 8, 6) };
        export.Click += async (_, _) =>
        {
            var file = await _hostWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            { Title = "导出动画当前画面", SuggestedFileName = "chart-animation-frame.png", DefaultExtension = "png" });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(1200, 700), Background = chart.Background });
        };
        actions.Children.Add(export);
        var view = new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = "控件默认关闭动画，本例显式开启入场、数据和视图动画。调整时长、缓动与延迟后操作按钮；暂停后可导出当前帧。减少动态效果会直接显示目标，应用可将系统或用户的动态效果偏好绑定到此开关。超过点数上限时跳过动画。", TextWrapping = TextWrapping.Wrap, Opacity = .8 },
            options, actions, chart, status,
        } };
        view.AttachedToVisualTree += (_, _) => { UpdateStatus(); timer.Start(); };
        view.DetachedFromVisualTree += (_, _) => timer.Stop();
        UpdateStatus();
        return new(view, chart, line, bars, scatter, () => SetData(), () => { timer.Stop(); timer.Tick -= Tick; chart.CancelAnimations(); chart.Series.Clear(); });
    }

    private sealed record AnimationBoard(StackPanel View, CartesianChart Chart, LineSeries Line, BarSeries Bars, ScatterSeries Scatter,
        Action SetData, Action Release) : IDisposable
    { public void Dispose() => Release(); }
}
