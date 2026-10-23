using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<CartesianChart> _axisShowcaseCharts = [];
    private Expander? _axisExamples;
    private Grid? _axisChartGrid;
    private NumericAxis? _independentAxis;

    private Control CreateAxisFeaturesExamples()
    {
        var labels = CreateAxisShowcaseChart("AxisLabelsChart");
        labels.XAxis = new CategoryAxis
        {
            Title = "服务区域", PlotMode = CategoryPlotMode.BetweenTicks, Gap = .5,
            LabelMode = ChartAxisLabelMode.Auto, LabelMaxWidth = 100,
            Labels = new[] { "华东 · 上海与周边", "华南 · 深圳与周边", "华北 · 北京与周边", "西南 · 成都与周边", "华中 · 武汉与周边", "西北 · 西安与周边" },
        };
        labels.YAxis = new NumericAxis
        {
            Title = "满意度 / 分", Minimum = 0, Maximum = 100, Interval = 20, MinorInterval = 5,
            ShowMinorGridLines = true, LabelBrush = Teal, TitleBrush = Teal, AxisBrush = Teal,
            GridLineBrush = Brush("#5019A798"), MinorGridLineBrush = Brush("#2019A798"),
            TickBrush = Teal, MinorTickBrush = Teal, TitleFontSize = 13, LabelFontSize = 11,
            GridLineDashStyle = new DashStyle(new[] { 5d, 3d }, 0), GridLineCap = PenLineCap.Round,
        };
        labels.Series.Add(new BarSeries { Title = "服务满意度", Fill = Teal, Stroke = Teal, ItemsSource = new ChartPoint[] { new(0, 78), new(1, 92), new(2, 85), new(3, 88), new(4, 81), new(5, 94) } });

        var breaks = CreateAxisShowcaseChart("AxisBreaksChart");
        breaks.XAxis = new NumericAxis { Title = "运行时间 / 小时", Minimum = 0, Maximum = 10, Interval = 2, MinorTickCount = 3, ShowMinorGridLines = true };
        breaks.YAxis = new NumericAxis { Title = "请求 / 千次", Minimum = 0, Maximum = 260, Interval = 50, LabelBrush = Blue, TitleBrush = Blue };
        breaks.YAxis.ScaleBreaks.Add(new ChartScaleBreak { Start = 70, End = 190, CompressionRatio = .08 });
        breaks.XAxis.PlotBands.Add(new ChartPlotBand
        {
            Start = 2.2, End = 2.8, RepeatInterval = 3, RepeatCount = 3, Text = "巡检",
            Fill = Brush("#304385F4"), Opacity = .85, Foreground = Blue, FontSize = 11, Layer = ChartPlotBandLayer.BehindSeries,
            HorizontalTextAlignment = ChartBandTextAlignment.Center, VerticalTextAlignment = ChartBandTextAlignment.Start,
        });
        breaks.YAxis.PlotBands.Add(new ChartPlotBand
        {
            Start = 220, End = 250, Text = "高峰区", Fill = Brush("#30F2A33A"), Opacity = .85, Stroke = Orange,
            Foreground = Orange, FontSize = 11, Layer = ChartPlotBandLayer.AboveSeries,
            HorizontalTextAlignment = ChartBandTextAlignment.End, VerticalTextAlignment = ChartBandTextAlignment.End,
        });
        breaks.Series.Add(new LineSeries
        {
            Title = "每小时请求", Stroke = Blue, StrokeThickness = 3, MarkerSize = 5,
            ItemsSource = new ChartPoint[] { new(0, 20), new(1, 25), new(2, 35), new(3, 210), new(4, 240), new(5, 230), new(6, 45), new(7, 35), new(8, 38), new(9, 52), new(10, 60) },
        });

        var independent = CreateAxisShowcaseChart("AxisIndependentViewChart");
        independent.XAxis = new NumericAxis { Title = "观察时间 / 小时", Minimum = 0, Maximum = 10, MaximumZoom = 8 };
        independent.YAxis = new NumericAxis { Title = "负载 / %", Minimum = 0, Maximum = 100, MaximumZoom = 4, TitleBrush = Blue, LabelBrush = Blue };
        _independentAxis = new NumericAxis
        {
            Key = "quality", Title = "成功率", Minimum = 0, Maximum = 1, ViewMinimum = .1, ViewMaximum = .9,
            Position = ChartAxisPosition.Far, LabelFormat = "P0", LabelBrush = Orange, TitleBrush = Orange,
            ShowGridLines = false, MaximumZoom = 10,
        };
        independent.Axes.Add(_independentAxis);
        independent.Axes.Add(new NumericAxis
        {
            Key = "pressure", Title = "压力 / kPa", Minimum = 100, Maximum = 200, Position = ChartAxisPosition.Near,
            LabelBrush = Teal, TitleBrush = Teal, ShowGridLines = false, DesiredTickCount = 4,
        });
        independent.Series.Add(new LineSeries { Title = "负载", Stroke = Blue, ItemsSource = new ChartPoint[] { new(0, 20), new(2, 35), new(4, 45), new(6, 55), new(8, 65), new(10, 75) } });
        independent.Series.Add(new ScatterSeries { Title = "成功率", Fill = Orange, Stroke = Orange, MarkerSize = 8, YAxisKey = "quality", ItemsSource = new ChartPoint[] { new(0, .25), new(2, .34), new(4, .42), new(6, .55), new(8, .63), new(10, .72) } });
        independent.Series.Add(new LineSeries { Title = "压力", Stroke = Teal, YAxisKey = "pressure", ItemsSource = new ChartPoint[] { new(0, 180), new(2, 160), new(4, 150), new(6, 140), new(8, 125), new(10, 115) } });

        _axisChartGrid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        _axisChartGrid.Children.Add(CreateChartCard("细分刻度与自适应标签", "AXIS DETAIL", "次网格帮助读数，长名称随宽度调整", labels));
        _axisChartGrid.Children.Add(CreateChartCard("高低峰与巡检时段", "BREAKS + BANDS", "压缩中间空档，巡检时段与高峰区分层标注", breaks));
        _axisChartGrid.Children.Add(CreateChartCard("多种指标与独立视图", "NEAR / FAR", "左右轴各读其值，成功率可单独聚焦", independent));
        LayoutAxisFeatures(_threeColumns);

        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        var minor = new CheckBox { Content = "显示次网格", IsChecked = true, Margin = new Thickness(0, 0, 14, 6) };
        minor.IsCheckedChanged += (_, _) => { labels.YAxis.ShowMinorGridLines = minor.IsChecked == true; breaks.XAxis.ShowMinorGridLines = minor.IsChecked == true; };
        var axes = new CheckBox { Content = "显示满意度轴", IsChecked = true, Margin = new Thickness(0, 0, 14, 6) };
        axes.IsCheckedChanged += (_, _) => { labels.YAxis.ShowAxis = axes.IsChecked == true; labels.YAxis.ShowTitle = axes.IsChecked == true; labels.YAxis.ShowLabels = axes.IsChecked == true; labels.YAxis.ShowTicks = axes.IsChecked == true; };
        var mode = new ComboBox { ItemsSource = new[] { "自动标签", "隐藏标签", "换行", "错行", "旋转" }, SelectedIndex = 0, MinWidth = 130, Margin = new Thickness(0, 0, 8, 6) };
        mode.SelectionChanged += (_, _) => labels.XAxis.LabelMode = (ChartAxisLabelMode)Math.Max(0, mode.SelectedIndex);
        actions.Children.Add(minor); actions.Children.Add(axes); actions.Children.Add(mode);
        AddAction(actions, "聚焦成功率 30%–70%", () => { _independentAxis.ViewMinimum = .3; _independentAxis.ViewMaximum = .7; }, "仅调整右侧成功率范围，负载和压力保留当前视图。");
        AddAction(actions, "恢复三图范围", () => { foreach (var chart in _axisShowcaseCharts) chart.ResetViewport(); }, "恢复主轴和每条独立次轴的全部范围。");
        return _axisExamples = new Expander
        {
            Name = "ChartAxisFeaturesExamples", Header = "坐标轴进阶：次网格、断轴、区间标注与独立次轴", HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0), Children =
            {
                new TextBlock { Text = "在图内滚轮缩放、拖动查看；各轴限制最大放大倍数。断轴两侧的折线在空档处分开，区间文字跟随对应轴的位置。", TextWrapping = TextWrapping.Wrap, Opacity = .7 },
                actions, _axisChartGrid,
            } },
        };
    }

    private CartesianChart CreateAxisShowcaseChart(string name)
    {
        var chart = CreateAdvancedChart(name);
        _axisShowcaseCharts.Add(chart);
        return chart;
    }

    private void LayoutAxisFeatures(bool threeColumns)
    {
        if (_axisChartGrid is null) return;
        _axisChartGrid.ColumnDefinitions = new ColumnDefinitions(threeColumns ? "*,*,*" : "*");
        _axisChartGrid.RowDefinitions = new RowDefinitions(threeColumns ? "Auto" : "Auto,Auto,Auto");
        for (var index = 0; index < _axisChartGrid.Children.Count; index++)
        {
            Grid.SetColumn(_axisChartGrid.Children[index], threeColumns ? index : 0);
            Grid.SetRow(_axisChartGrid.Children[index], threeColumns ? 0 : index);
        }
    }
}
