using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Expander? _lineAreaExamples;
    private LineAreaBoard? _lineAreaBoard;

    private Control CreateLineAreaExamples()
    {
        _lineAreaBoard = CreateLineAreaBoard(true);
        return _lineAreaExamples = new Expander
        {
            Name = "ChartLineAreaExamples", Header = "折线与面积：平滑、阶梯、范围和堆叠",
            HorizontalAlignment = HorizontalAlignment.Stretch, Content = _lineAreaBoard.View,
        };
    }

    private LineAreaBoard CreateLineAreaBoard(bool register)
    {
        CartesianChart Chart(string name, string title)
        {
            var chart = register ? CreateAdvancedChart(name) : new CartesianChart { Name = name };
            chart.Height = 285; chart.Title = title; chart.LegendPosition = ChartLegendPosition.Bottom;
            chart.XAxis = new NumericAxis { Minimum = 0, Maximum = 6, Title = "时间" };
            chart.YAxis = new NumericAxis { Title = "数值" };
            chart.IsTooltipEnabled = true; chart.TooltipMode = ChartTooltipMode.SharedX;
            chart.IsZoomEnabled = true; chart.IsPanEnabled = true; chart.SelectionMode = ChartSelectionMode.Multiple;
            return chart;
        }
        var lines = Chart("LineVariantsChart", "五种折线插值 · 逐行错开便于比较");
        lines.YAxis.Minimum = -5; lines.YAxis.Maximum = 115;
        var modes = Enum.GetValues<ChartLineInterpolation>();
        IBrush[] colors = [Blue, Teal, Orange, Brush("#D7259F"), Brush("#7756C8")];
        for (var index = 0; index < modes.Length; index++)
            lines.Series.Add(new LineSeries { Title = modes[index].ToString(), Interpolation = modes[index], Stroke = colors[index % colors.Length],
                StrokeThickness = 2.5, MarkerSize = 4, ItemsSource = new ChartPoint[] { new(0, 5 + index * 18), new(1, 20 + index * 18), new(2, 8 + index * 18), new(4, 28 + index * 18), new(6, 12 + index * 18) } });
        var area = Chart("AreaVariantsChart", "面积 · 切换插值与零点以外的基线");
        area.YAxis.Minimum = -40; area.YAxis.Maximum = 70;
        var filled = new AreaSeries { Title = "可切换面积", Interpolation = ChartLineInterpolation.Smooth, Baseline = 0, Stroke = Blue, Fill = Brush("#704385F4"), MarkerSize = 5,
            ItemsSource = new ChartPoint[] { new(0, 15), new(1, 55), new(2, 20), new(3, -25), new(4, 35), new(6, 45) } };
        area.Series.Add(filled);
        var ranges = Chart("RangeAreaVariantsChart", "范围面积 · 原始高低值与独立纵轴");
        ranges.YAxis.Minimum = -20; ranges.YAxis.Maximum = 100;
        ranges.Axes.Add(new NumericAxis { Key = "line-area-pressure", Minimum = 1000, Maximum = 2000, Position = ChartAxisPosition.Far, Title = "压力 / kPa" });
        var rangeRecords = new[] { new LineAreaRange("range-0", 0, 15, 45), new LineAreaRange("range-1", 1, 30, 85), new LineAreaRange("range-2", 2, -10, 30),
            new LineAreaRange("range-3", 3, 20, 75), new LineAreaRange("range-4", 4, 5, 40), new LineAreaRange("range-6", 6, 25, 65) };
        var range = new RangeAreaSeries { Title = "上下界", ItemsSource = rangeRecords, CategoryPath = nameof(LineAreaRange.X), ValuePath = nameof(LineAreaRange.High),
            HighPath = nameof(LineAreaRange.High), LowPath = nameof(LineAreaRange.Low), IdPath = nameof(LineAreaRange.Id), Interpolation = ChartLineInterpolation.Smooth, Stroke = Teal, Fill = Brush("#7019A798"), MarkerSize = 4 };
        ranges.Series.Add(range);
        ranges.Series.Add(new LineSeries { Title = "压力 · 右轴", YAxisKey = "line-area-pressure", Interpolation = ChartLineInterpolation.Smooth,
            Stroke = Orange, MarkerSize = 4, ItemsSource = new ChartPoint[] { new(0, 1200), new(1, 1700), new(2, 1350), new(3, 1800), new(4, 1450), new(6, 1650) } });
        var stacks = Chart("StackedLineAreaChart", "面积与折线分组堆叠 · 正负分别累加");
        var a = new AreaSeries { Title = "渠道 A", Interpolation = ChartLineInterpolation.Smooth, StackMode = ChartStackMode.Stacked, StackGroup = "areas", Stroke = Blue, Fill = Brush("#A04385F4"), MarkerSize = 4 };
        var b = new AreaSeries { Title = "渠道 B", Interpolation = ChartLineInterpolation.Smooth, StackMode = ChartStackMode.Stacked, StackGroup = "areas", Stroke = Teal, Fill = Brush("#A019A798"), MarkerSize = 4 };
        var c = new AreaSeries { Title = "渠道 C", Interpolation = ChartLineInterpolation.Smooth, StackMode = ChartStackMode.Stacked, StackGroup = "areas", Stroke = Orange, Fill = Brush("#A0F2A33A"), MarkerSize = 4 };
        stacks.Series.Add(a); stacks.Series.Add(b); stacks.Series.Add(c);
        stacks.Series.Add(new LineSeries { Title = "折线 A", Interpolation = ChartLineInterpolation.Smooth, StackMode = ChartStackMode.Stacked, StackGroup = "lines", Stroke = Brush("#D7259F"), MarkerSize = 4,
            ItemsSource = new ChartPoint[] { new(0, 10), new(1, 20), new(2, -10), new(3, 15), new(4, 25), new(6, 10) } });
        stacks.Series.Add(new LineSeries { Title = "折线 B", Interpolation = ChartLineInterpolation.Smooth, StackMode = ChartStackMode.Stacked, StackGroup = "lines", Stroke = Brush("#7756C8"), MarkerSize = 4,
            ItemsSource = new ChartPoint[] { new(0, 15), new(1, 10), new(2, -15), new(3, 20), new(4, 10), new(6, 25) } });
        void SetStackData(bool gap)
        {
            a.ItemsSource = new ChartPoint[] { new(0, 20), new(1, 35), new(2, -20), new(3, 25), new(4, 30), new(6, 20) };
            b.ItemsSource = new ChartPoint[] { new(0, 30), new(1, 20), new(2, -30), new(3, gap ? double.NaN : 35), new(4, 20), new(6, 30) };
            c.ItemsSource = new ChartPoint[] { new(0, 10), new(1, 15), new(2, -10), new(3, 20), new(4, 25), new(6, 15) };
        }
        SetStackData(true);
        var options = new WrapPanel();
        var interpolation = new ComboBox { Name = "LineAreaInterpolation", ItemsSource = modes, SelectedItem = ChartLineInterpolation.Smooth, MinWidth = 140, Margin = new Thickness(0, 0, 12, 6) };
        interpolation.SelectionChanged += (_, _) =>
        {
            if (interpolation.SelectedItem is not ChartLineInterpolation value) return;
            filled.Interpolation = value; range.Interpolation = value;
            foreach (var series in stacks.Series) { if (series is AreaSeries ar) ar.Interpolation = value; else if (series is LineSeries li) li.Interpolation = value; }
        };
        var mode = new ComboBox { Name = "LineAreaStackMode", ItemsSource = Enum.GetValues<ChartStackMode>(), SelectedItem = ChartStackMode.Stacked, MinWidth = 130, Margin = new Thickness(0, 0, 12, 6) };
        mode.SelectionChanged += (_, _) =>
        {
            if (mode.SelectedItem is not ChartStackMode value) return;
            foreach (var series in stacks.Series) { if (series is AreaSeries ar) ar.StackMode = value; else if (series is LineSeries li) li.StackMode = value; }
            stacks.YAxis.LabelFormat = value == ChartStackMode.Percent ? "0'%'" : "0";
            stacks.ResetViewport();
        };
        var gap = new CheckBox { Name = "LineAreaGap", Content = "渠道 B 保留缺失点", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        gap.IsCheckedChanged += (_, _) => SetStackData(gap.IsChecked == true);
        var baseline = new CheckBox { Name = "LineAreaBaseline", Content = "面积基线 = -20", Margin = new Thickness(0, 0, 12, 6) };
        baseline.IsCheckedChanged += (_, _) => filled.Baseline = baseline.IsChecked == true ? -20 : 0;
        options.Children.Add(new TextBlock { Text = "面积/范围/堆叠插值", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) }); options.Children.Add(interpolation);
        options.Children.Add(new TextBlock { Text = "堆叠方式", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) }); options.Children.Add(mode); options.Children.Add(gap); options.Children.Add(baseline);
        AddAction(options, "重置四图视图", () => { foreach (var chart in new[] { lines, area, ranges, stacks }) chart.ResetViewport(); }, "同时恢复四张图的自动范围。");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 16, RowSpacing = 10 };
        grid.Children.Add(lines); grid.Children.Add(area); Grid.SetColumn(area, 1); grid.Children.Add(ranges); Grid.SetRow(ranges, 1);
        grid.Children.Add(stacks); Grid.SetColumn(stacks, 1); Grid.SetRow(stacks, 1);
        var view = new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = "五种插值：直线、平滑、前/后/中间阶梯。面积可更换基线；范围面积使用业务记录的 High/Low。堆叠按同组、同 X 值分别累计正负贡献；Percent 的上下界为 ±100，空缺保持断开。可悬停、点击选择、滚轮缩放和拖动平移。", TextWrapping = TextWrapping.Wrap, Opacity = .8 },
            options, grid,
        } };
        return new(view, lines, area, ranges, stacks, filled, range, rangeRecords, interpolation, mode, gap, baseline);
    }

    private sealed record LineAreaRange(string Id, double X, double Low, double High);
    private sealed record LineAreaBoard(StackPanel View, CartesianChart Lines, CartesianChart Area, CartesianChart Ranges, CartesianChart Stacks,
        AreaSeries Filled, RangeAreaSeries Range, LineAreaRange[] RangeRecords, ComboBox Interpolation, ComboBox StackMode, CheckBox Gap, CheckBox Baseline) : IDisposable
    {
        internal CartesianChart[] Charts => [Lines, Area, Ranges, Stacks];
        public void Dispose() { foreach (var chart in Charts) chart.Series.Clear(); }
    }
}
