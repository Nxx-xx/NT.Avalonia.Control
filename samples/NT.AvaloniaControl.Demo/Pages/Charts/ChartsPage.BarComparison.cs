using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Expander? _barComparisonExamples;
    private BarComparisonBoard? _barComparisonBoard;

    private Control CreateBarComparisonExamples()
    {
        _barComparisonBoard = CreateBarComparisonBoard(true);
        return _barComparisonExamples = new Expander
        {
            Name = "ChartBarComparisonExamples", Header = "柱条与比较：横条、范围、瀑布和 Pareto",
            HorizontalAlignment = HorizontalAlignment.Stretch, Content = _barComparisonBoard.View,
        };
    }

    private BarComparisonBoard CreateBarComparisonBoard(bool register)
    {
        CartesianChart Chart(string name, string title)
        {
            var chart = register ? CreateAdvancedChart(name) : new CartesianChart { Name = name };
            chart.Height = 285; chart.Title = title; chart.LegendPosition = ChartLegendPosition.Bottom;
            chart.XAxis = new NumericAxis { Minimum = 0, Maximum = 6, Title = "类别" };
            chart.YAxis = new NumericAxis { Title = "数值" };
            chart.IsTooltipEnabled = true; chart.IsZoomEnabled = true; chart.IsPanEnabled = true;
            chart.SelectionMode = ChartSelectionMode.Multiple;
            return chart;
        }
        var records = new[] { new BarComparisonRecord("bar-a", 1, 40), new BarComparisonRecord("bar-b", 3, -25), new BarComparisonRecord("bar-c", 5, 65) };
        var grouped = Chart("GroupedBarVariantsChart", "分组柱 / 横条 · 排序与宽度");
        grouped.XAxis = new CategoryAxis { Minimum = -.5, Maximum = 2.5, Title = "类别" };
        var first = new BarSeries { Title = "业务渠道 A", ItemsSource = records, CategoryPath = nameof(BarComparisonRecord.Category), ValuePath = nameof(BarComparisonRecord.Value), IdPath = nameof(BarComparisonRecord.Id), Fill = Blue, Stroke = Blue };
        var second = new BarSeries { Title = "渠道 B", ItemsSource = new ChartPoint[] { new(1, 25), new(3, -40), new(5, 45) }, Fill = Teal, Stroke = Teal };
        grouped.Series.Add(first); grouped.Series.Add(second);
        var stacks = Chart("StackedBarVariantsChart", "正负堆叠 · 可切换百分比");
        var stackA = new BarSeries { Title = "第一贡献", StackMode = ChartStackMode.Stacked, StackGroup = "comparison", Fill = Blue, Stroke = Blue,
            ItemsSource = new ChartPoint[] { new(1, 20), new(3, -20), new(5, 30) } };
        var stackB = new BarSeries { Title = "第二贡献", StackMode = ChartStackMode.Stacked, StackGroup = "comparison", Fill = Teal, Stroke = Teal,
            ItemsSource = new ChartPoint[] { new(1, 30), new(3, -30), new(5, 70) } };
        stacks.Series.Add(stackA); stacks.Series.Add(stackB);
        var range = Chart("RangeBarVariantsChart", "范围柱 · 原始上下界");
        var rangeRecords = new[] { new BarComparisonRange("range-a", 1, 10, 50), new BarComparisonRange("range-b", 3, 30, 85), new BarComparisonRange("range-c", 5, -30, 25) };
        var rangeSeries = new RangeBarSeries { Title = "交付区间", ItemsSource = rangeRecords, CategoryPath = nameof(BarComparisonRange.Category), HighPath = nameof(BarComparisonRange.High), LowPath = nameof(BarComparisonRange.Low), IdPath = nameof(BarComparisonRange.Id), Fill = Orange, Stroke = Orange };
        range.Series.Add(rangeSeries);
        var waterfall = Chart("WaterfallVariantsChart", "瀑布 · 增减贡献与总计");
        var changes = new[] { new BarComparisonChange("start", 1, 40, false), new BarComparisonChange("cost", 2, -15, false), new BarComparisonChange("growth", 3, 20, false), new BarComparisonChange("total", 4, 45, true), new BarComparisonChange("later", 5, -10, false) };
        var waterfallSeries = new WaterfallSeries { Title = "余额变化", ItemsSource = changes, CategoryPath = nameof(BarComparisonChange.Category), ValuePath = nameof(BarComparisonChange.Value), IdPath = nameof(BarComparisonChange.Id), TotalPath = nameof(BarComparisonChange.IsTotal),
            PositiveFill = Teal, NegativeFill = Brush("#D7256A"), TotalFill = Blue, ConnectorStroke = Brush("#8A94A5"), StrokeThickness = 1 };
        waterfall.Series.Add(waterfallSeries);
        var pareto = Chart("ParetoVariantsChart", "Pareto · 值降序与累计百分比");
        pareto.XAxis = new CategoryAxis { Minimum = -.5, Maximum = 2.5, Title = "类别（按贡献排序）" };
        var paretoSeries = new ParetoSeries { Title = "缺陷贡献 / 累计占比", ItemsSource = new ChartPoint[] { new(1, 30), new(2, 50), new(3, 20) }, Fill = Blue, Stroke = Blue,
            CumulativeStroke = Orange, CumulativeStrokeThickness = 3 };
        pareto.Series.Add(paretoSeries);
        var comparison = Chart("ComparisonVariantsChart", "Dot / Lollipop / Variance · 同一值域比较");
        comparison.YAxis.Minimum = -10; comparison.YAxis.Maximum = 100;
        var dot = new DotSeries { Title = "Dot", ItemsSource = new ChartPoint[] { new(1, 55), new(2, 75) }, Fill = Blue, Stroke = Blue, MarkerSize = 10 };
        var lollipop = new LollipopSeries { Title = "Lollipop", ItemsSource = new ChartPoint[] { new(3, 45), new(4, 70) }, Fill = Teal, Stroke = Teal, StrokeThickness = 3, MarkerSize = 10 };
        var variance = new VarianceSeries { Title = "Variance", ItemsSource = new[] { new BarComparisonRange("variance", 5, 25, 85) }, CategoryPath = nameof(BarComparisonRange.Category), LowPath = nameof(BarComparisonRange.Low), HighPath = nameof(BarComparisonRange.High), Stroke = Orange, Fill = Orange, StrokeThickness = 3, MarkerSize = 10 };
        comparison.Series.Add(dot); comparison.Series.Add(lollipop); comparison.Series.Add(variance);

        var options = new WrapPanel();
        var orientation = new ComboBox { Name = "BarComparisonOrientation", ItemsSource = new[] { Orientation.Vertical, Orientation.Horizontal }, SelectedItem = Orientation.Vertical, MinWidth = 110, Margin = new Thickness(0, 0, 12, 6) };
        orientation.SelectionChanged += (_, _) =>
        {
            if (orientation.SelectedItem is not Orientation value) return;
            first.Orientation = second.Orientation = value;
            grouped.XAxis = value == Orientation.Vertical ? new CategoryAxis { Minimum = -.5, Maximum = 2.5, Title = "类别" } : new NumericAxis { Minimum = -60, Maximum = 90, Title = "数值" };
            grouped.YAxis = value == Orientation.Vertical ? new NumericAxis { Minimum = -60, Maximum = 90, Title = "数值" } : new CategoryAxis { Minimum = -.5, Maximum = 2.5, Title = "类别" };
            grouped.ResetViewport();
        };
        grouped.YAxis.Minimum = -60; grouped.YAxis.Maximum = 90;
        var stackMode = new ComboBox { Name = "BarComparisonStackMode", ItemsSource = new[] { ChartStackMode.Stacked, ChartStackMode.Percent }, SelectedItem = ChartStackMode.Stacked, MinWidth = 110, Margin = new Thickness(0, 0, 12, 6) };
        stackMode.SelectionChanged += (_, _) => { if (stackMode.SelectedItem is ChartStackMode value) { stackA.StackMode = stackB.StackMode = value; stacks.YAxis.LabelFormat = value == ChartStackMode.Percent ? "0'%'" : "0"; stacks.ResetViewport(); } };
        var sort = new ComboBox { Name = "BarComparisonSort", ItemsSource = Enum.GetValues<ChartSeriesSortMode>(), SelectedItem = ChartSeriesSortMode.Source, MinWidth = 150, Margin = new Thickness(0, 0, 12, 6) };
        sort.SelectionChanged += (_, _) => { if (sort.SelectedItem is ChartSeriesSortMode value) first.SortMode = second.SortMode = value; };
        var narrow = new CheckBox { Name = "BarComparisonFixedWidth", Content = "固定宽度 12 DIP", Margin = new Thickness(0, 0, 12, 6) };
        narrow.IsCheckedChanged += (_, _) => first.BarWidth = second.BarWidth = narrow.IsChecked == true ? 12 : null;
        var labels = new CheckBox { Name = "BarComparisonLabels", Content = "显示数值标签", Margin = new Thickness(0, 0, 12, 6) };
        labels.IsCheckedChanged += (_, _) => { foreach (var chart in new[] { grouped, stacks, range, waterfall, pareto, comparison }) foreach (var series in chart.Series) series.ShowDataLabels = labels.IsChecked == true; };
        options.Children.Add(new TextBlock { Text = "分组方向", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) }); options.Children.Add(orientation);
        options.Children.Add(new TextBlock { Text = "堆叠", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) }); options.Children.Add(stackMode);
        options.Children.Add(new TextBlock { Text = "分组排序", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) }); options.Children.Add(sort); options.Children.Add(narrow); options.Children.Add(labels);
        var charts = new[] { grouped, stacks, range, waterfall, pareto, comparison };
        foreach (var chart in charts)
        {
            chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((context, _) => new TextBlock
            {
                Name = "BarComparisonRawTooltip", TextWrapping = TextWrapping.Wrap,
                Text = context is null ? "" : string.Join("\n", context.Hits.Select(hit => $"{hit.Series.Title} · 原类别 {(hit.RawPoint ?? hit.Point).X:0.##} · 原数值 {(hit.RawPoint ?? hit.Point).Y:0.##} · 源索引 {hit.Index}")),
            });
            foreach (var series in chart.Series)
                series.DataLabelFormatter = context => (context.Data.RawPoint ?? context.Data.Point).Y.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
        }
        AddAction(options, "重置六图视图", () => { foreach (var chart in charts) chart.ResetViewport(); }, "恢复所有图表的完整视图。");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), ColumnSpacing = 16, RowSpacing = 10 };
        for (var index = 0; index < charts.Length; index++) { grid.Children.Add(charts[index]); Grid.SetColumn(charts[index], index % 2); Grid.SetRow(charts[index], index / 2); }
        var view = new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = "柱与横条共享原始业务身份；可切换分组方向、显示排序和固定 DIP 宽度。正负贡献分别堆叠或归一到 ±100；范围柱只填上下界，瀑布保持增减累积，Pareto 叠加累计占比。下图同时展示 Dot、Lollipop 和 Variance。支持悬停、选择、缩放、平移及导出。", TextWrapping = TextWrapping.Wrap, Opacity = .8 },
            options, grid,
        } };
        return new(view, charts, first, second, stackA, stackB, rangeSeries, waterfallSeries, paretoSeries, records, rangeRecords, changes, orientation, stackMode, sort, narrow, labels);
    }

    private sealed record BarComparisonRecord(string Id, double Category, double Value);
    private sealed record BarComparisonRange(string Id, double Category, double Low, double High);
    private sealed record BarComparisonChange(string Id, double Category, double Value, bool IsTotal);
    private sealed record BarComparisonBoard(StackPanel View, CartesianChart[] Charts, BarSeries First, BarSeries Second, BarSeries StackA, BarSeries StackB,
        RangeBarSeries Range, WaterfallSeries Waterfall, ParetoSeries Pareto, BarComparisonRecord[] Records, BarComparisonRange[] RangeRecords, BarComparisonChange[] Changes,
        ComboBox Orientation, ComboBox StackMode, ComboBox Sort, CheckBox FixedWidth, CheckBox Labels) : IDisposable
    {
        public void Dispose() { foreach (var chart in Charts) chart.Series.Clear(); }
    }
}
