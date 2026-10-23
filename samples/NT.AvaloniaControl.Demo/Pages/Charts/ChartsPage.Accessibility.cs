using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Expander? _accessibilityExamples;
    private AccessibilityBoard? _accessibilityBoard;

    private Control CreateAccessibilityExamples()
    {
        _accessibilityBoard = CreateAccessibilityBoard(true);
        return _accessibilityExamples = new Expander
        {
            Name = "ChartAccessibilityExamples", Header = "辅助功能：点级语义、键盘与高对比",
            HorizontalAlignment = HorizontalAlignment.Stretch, Content = _accessibilityBoard.View,
        };
    }

    private AccessibilityBoard CreateAccessibilityBoard(bool register)
    {
        var chart = register ? CreateAdvancedChart("AccessibilityChart") : new CartesianChart { Name = "AccessibilityChart" };
        chart.Height = 360; chart.Title = "监测数据 · 可用键盘逐点浏览";
        chart.XAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "时间 / 分钟", LabelFormat = "0" };
        chart.YAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "负载 / %", LabelFormat = "0" };
        chart.Axes.Add(new NumericAxis { Key = "accessibility-pressure", Minimum = 1000, Maximum = 2000, Position = ChartAxisPosition.Far, Title = "压力 / kPa", LabelFormat = "0" });
        chart.IsKeyboardNavigationEnabled = true; chart.IsPointBrowsingEnabled = true; chart.SelectionMode = ChartSelectionMode.Multiple;
        chart.IsZoomEnabled = true; chart.IsPanEnabled = true; chart.LegendPosition = ChartLegendPosition.Bottom;
        var records = new[] { new AccessibilityRecord("load-a", "开始", 15, 25), new AccessibilityRecord("load-invalid", "缺失", 30, double.NaN),
            new AccessibilityRecord("load-b", "检查", 45, 65), new AccessibilityRecord("load-c", "结束", 80, 40) };
        var line = new LineSeries { Title = "负载", ItemsSource = records, CategoryPath = nameof(AccessibilityRecord.X), ValuePath = nameof(AccessibilityRecord.Y),
            IdPath = nameof(AccessibilityRecord.Id), StrokeThickness = 3, MarkerSize = 10, MarkerShape = ChartMarkerShape.Circle };
        var bars = new BarSeries { Title = "任务", ItemsSource = new ChartPoint[] { new(25, 35), new(60, 55), new(90, 75) } };
        var scatter = new ScatterSeries { Title = "压力", YAxisKey = "accessibility-pressure", ItemsSource = new ChartPoint[] { new(20, 1850), new(55, 1450), new(85, 1700) },
            MarkerSize = 14, MarkerShape = ChartMarkerShape.Diamond };
        chart.Series.Add(line); chart.Series.Add(bars); chart.Series.Add(scatter);
        var options = new WrapPanel();
        var enabled = new CheckBox { Name = "AccessibilityKeyboardEnabled", Content = "键盘浏览", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        enabled.IsCheckedChanged += (_, _) => chart.IsKeyboardNavigationEnabled = enabled.IsChecked == true;
        options.Children.Add(enabled);
        var contrast = new ComboBox { Name = "AccessibilityContrast", ItemsSource = new[] { "常规配色", "高对比 · 白底", "高对比 · 黑底" }, SelectedIndex = 0, MinWidth = 160, Margin = new Thickness(0, 0, 12, 6) };
        contrast.SelectionChanged += (_, _) => chart.HighContrastMode = contrast.SelectedIndex switch { 1 => ChartHighContrastMode.Light, 2 => ChartHighContrastMode.Dark, _ => ChartHighContrastMode.None };
        options.Children.Add(contrast);
        var actions = new WrapPanel();
        AddAction(actions, "聚焦图表", () => chart.Focus(), "将键盘焦点移至图表，然后使用方向键浏览。");
        AddAction(actions, "缩放当前视图", () => chart.Viewport = chart.Viewport is null ? new ChartViewport(10, 70, 0, 100) : null, "切换完整视图与局部视图。");
        AddAction(actions, "更新业务数值", () => records[2].Y = records[2].Y == 65 ? 80 : 65, "数据摘要与点级名称跟随业务对象更新。");
        var summary = new TextBlock { Name = "AccessibilitySummary", FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var active = new TextBlock { Name = "AccessibilityActivePoint", FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        chart.PointInvokedCommand = new AccessibilityActionCommand(value =>
        {
            if (value is ChartPointHit point) active.Text = $"已调用：{point.Series.Title} · X {point.Point.X:0.###} / Y {point.Point.Y:0.###} · 编号 {point.StableId}";
        });
        var view = new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = "Tab 聚焦后：←/→ 同系列逐点，↑/↓ 切换系列，Home/End 首末点，PageUp/PageDown 跳转；Space 选择、Ctrl+Space 追加或取消，Enter 调用当前点，Esc 清除读数，Ctrl+Home 恢复视图。折线圆点、柱形和菱形散点也能通过形状区分。缺失值不会成为可操作点；真实读屏器与系统偏好接入需另行验收。", TextWrapping = TextWrapping.Wrap, Opacity = .8 },
            options, actions, chart, active, summary,
        } };
        void UpdateReadings() { summary.Text = chart.AccessibilitySummaryText; active.Text = chart.CurrentPoint is { } point ? $"当前点：{point.Series.Title} · 源索引 {point.Index} · X {point.Point.X:0.###} / Y {point.Point.Y:0.###} · 编号 {point.StableId}" : "当前点：尚未浏览"; }
        EventHandler<AvaloniaPropertyChangedEventArgs> changed = (_, args) => { if (args.Property == CartesianChart.CurrentPointProperty || args.Property == CartesianChart.AccessibilitySummaryTextProperty) UpdateReadings(); };
        chart.PropertyChanged += changed;
        view.AttachedToVisualTree += (_, _) => UpdateReadings();
        return new(view, chart, line, bars, scatter, records, options, summary, active, () => chart.PropertyChanged -= changed);
    }

    private sealed record AccessibilityBoard(StackPanel View, CartesianChart Chart, LineSeries Line, BarSeries Bars, ScatterSeries Scatter,
        AccessibilityRecord[] Records, WrapPanel Options, TextBlock Summary, TextBlock Active, Action Release) : IDisposable
    {
        public void Dispose() { Release(); Chart.PointInvokedCommand = null; Chart.Series.Clear(); }
    }

    private sealed class AccessibilityRecord(string id, string caption, double x, double y) : INotifyPropertyChanged
    {
        private double _y = y;
        public string Id { get; } = id;
        public string Caption { get; } = caption;
        public double X { get; } = x;
        public double Y { get => _y; set { if (_y == value) return; _y = value; PropertyChanged?.Invoke(this, new(nameof(Y))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
