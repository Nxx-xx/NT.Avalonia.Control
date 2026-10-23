using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Control CreateStatisticsExamples() => new Expander
    {
        Name = "ChartStatisticsExamples", Header = "统计与分布：箱线、误差、直方图与核密度",
        HorizontalAlignment = HorizontalAlignment.Stretch, Content = CreateStatisticsBoard(),
    };

    private Control CreateStatisticsBoard()
    {
        var box = StatisticsChart("StatisticsBox", "箱线与离群值", "组别", "响应 / ms");
        box.XAxis = new CategoryAxis(); box.YAxis.Minimum = 0;
        var groups = new[] { new StatisticsGroup("A", "A 组", [1,2,3,4,5,6,7,8,9,30]),
            new StatisticsGroup("B", "B 组", [5,7,8,9,10,11,13,16,18]), new StatisticsGroup("C", "零方差", [12,12,12,12]) };
        var boxes = new BoxPlotSeries { Title = "原始样本", ItemsSource = groups, CategoryPath = "Name", SamplesPath = "Samples", IdPath = "Id", Stroke = Blue, StrokeThickness = 2.5, BarWidth = 42, ShowMean = true };
        box.Series.Add(boxes);
        box.TooltipFormatter = hits => hits.FirstOrDefault() is { BoxPlot: { } s } hit ? $"{hit.StableId} · n={s.Count}\nQ1 {s.LowerQuartile:0.##} · 中位 {s.Median:0.##} · Q3 {s.UpperQuartile:0.##}\n须 {s.LowerWhisker:0.##}–{s.UpperWhisker:0.##} · 离群 {s.Outliers.Count}" : null;
        var error = StatisticsChart("StatisticsError", "测量误差与不确定性", "样本", "测量值"); error.XAxis = new CategoryAxis();
        var errors = new[] { new StatisticsErrorRow("e0","样本 A", 5, 3, 3), new StatisticsErrorRow("e1","样本 B", 9, 1.5, 3), new StatisticsErrorRow("e2","样本 C", 13, 2, 1) };
        var errorSeries = new ErrorBarSeries { Title = "非对称误差", ItemsSource = errors, CategoryPath = "Name", ValuePath = "Value", LowErrorPath = "Lower", HighErrorPath = "Upper", IdPath = "Id", Stroke = Teal, StrokeThickness = 2.5, ShowMarkers = true, MarkerSize = 8, CapWidth = 18 };
        error.Series.Add(errorSeries);
        error.TooltipFormatter = hits => hits.FirstOrDefault() is { ErrorInterval: { } interval } hit ? $"{hit.StableId} · {interval.Center:0.##}\n下界 {interval.Lower:0.##} · 上界 {interval.Upper:0.##}" : null;
        var distribution = Enumerable.Range(0, 240).Select(i => 10 + ((i * 37 % 101) + (i * 17 % 97)) / 10d).ToArray();
        var histogram = StatisticsChart("StatisticsHistogram", "直方图 · 概率密度", "响应 / ms", "密度");
        var bins = new HistogramSeries { Title = "分箱密度", Fill = Brush("#19A798"), Stroke = Teal, StrokeThickness = 1 };
        histogram.Series.Add(bins);
        var density = StatisticsChart("StatisticsDensity", "Gaussian 核密度", "响应 / ms", "密度");
        var curve = new DensitySeries { Title = "估计密度", Stroke = Blue, StrokeThickness = 2.5, Fill = Brush("#304385F4") };
        density.Series.Add(curve);
        var status = new TextBlock { Name = "StatisticsSummary", TextWrapping = TextWrapping.Wrap };
        var bandwidth = new Slider { Name = "StatisticsBandwidth", Minimum = .25, Maximum = 4, Value = 1.2, Width = 170 };
        var binCount = new ComboBox { ItemsSource = new[] { 6, 12, 24 }, SelectedIndex = 1, Width = 74 };
        var outliers = new CheckBox { Content = "显示离群点", IsChecked = true };
        var means = new CheckBox { Content = "显示均值", IsChecked = true };
        var mode = new ComboBox { ItemsSource = new[] { "双侧误差", "仅正向", "仅负向" }, SelectedIndex = 0, Width = 115 };
        var preaggregated = false;
        void Recompute()
        {
            var h = ChartStatistics.Histogram(distribution, (int)(binCount.SelectedItem ?? 12), ChartHistogramNormalization.Density);
            var d = ChartStatistics.KernelDensity(distribution, 129, bandwidth.Value);
            bins.ItemsSource = h.Bins; curve.ItemsSource = d.Points;
            var ci = ChartStatistics.MeanConfidenceInterval(distribution);
            status.Text = $"n={distribution.Length} · 均值 {ci.Center:0.00} · 95% 正态近似均值区间 [{ci.Lower:0.00}, {ci.Upper:0.00}]\n带宽 h={d.Bandwidth:0.00} · 当前显示域积分 {d.SampledArea:0.000} · 分箱面积 {h.Bins.Sum(b => b.Value * (b.Upper - b.Lower)):0.000}";
        }
        bandwidth.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) Recompute(); };
        binCount.SelectionChanged += (_, _) => Recompute();
        outliers.IsCheckedChanged += (_, _) => boxes.ShowOutliers = outliers.IsChecked == true;
        means.IsCheckedChanged += (_, _) => boxes.ShowMean = means.IsChecked == true;
        mode.SelectionChanged += (_, _) => errorSeries.ErrorMode = (ChartErrorMode)Math.Max(0, mode.SelectedIndex);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 10, LineSpacing = 6 };
        actions.Children.Add(outliers); actions.Children.Add(means); actions.Children.Add(mode);
        actions.Children.Add(new TextBlock { Text = "分箱数", VerticalAlignment = VerticalAlignment.Center }); actions.Children.Add(binCount);
        actions.Children.Add(new TextBlock { Text = "密度带宽", VerticalAlignment = VerticalAlignment.Center }); actions.Children.Add(bandwidth);
        AddAction(actions, "原始 / 预聚合箱线", () =>
        {
            preaggregated = !preaggregated;
            boxes.SamplesPath = preaggregated ? null : "Samples"; boxes.SummaryPath = preaggregated ? "Summary" : null;
            boxes.ItemsSource = preaggregated ? groups.Select(g => new StatisticsSummaryRow(g.Id, g.Name, ChartStatistics.BoxPlot(g.Samples))).ToArray() : groups;
        }, "切换原始观测和预聚合摘要输入，保持箱体与须线一致。");
        AddAction(actions, "恢复四图视图", () => { foreach (var chart in new[] { box, error, histogram, density }) chart.ResetViewport(); }, "恢复显示全部数据。");
        var grid = new Grid { ColumnDefinitions = new("*,*"), RowDefinitions = new("Auto,Auto"), ColumnSpacing = 12, RowSpacing = 12 };
        var charts = new[] { box, error, histogram, density };
        for (var i = 0; i < charts.Length; i++) { Grid.SetColumn(charts[i], i % 2); Grid.SetRow(charts[i], i / 2); grid.Children.Add(charts[i]); }
        Recompute();
        return new StackPanel { Spacing = 10, Children =
        {
            new TextBlock { Text = "箱线使用 Type 7 分位数与 1.5×IQR 须线，点标记离群值，圆点可显示均值。误差长度和置信区间应由数据含义决定；这里的 95% 区间是大样本正态近似。密度曲线面积随显示域截断，未强制缩放为 1。", TextWrapping = TextWrapping.Wrap },
            actions, status, grid,
        } };
    }

    private static CartesianChart StatisticsChart(string name, string title, string x, string y) => new()
    {
        Name = name, Title = title, Height = 300, ShowLegend = false, AnimationTriggers = ChartAnimationTriggers.None,
        IsTooltipEnabled = true, IsZoomEnabled = true, IsPanEnabled = true, SelectionMode = ChartSelectionMode.Single,
        XAxis = new NumericAxis { Title = x }, YAxis = new NumericAxis { Title = y },
    };
    private sealed record StatisticsGroup(string Id, string Name, double[] Samples);
    private sealed record StatisticsSummaryRow(string Id, string Name, ChartBoxPlotSummary Summary);
    private sealed record StatisticsErrorRow(string Id, string Name, double Value, double Lower, double Upper);
}
