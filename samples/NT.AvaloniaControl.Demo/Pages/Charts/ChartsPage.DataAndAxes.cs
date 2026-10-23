using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<CartesianChart> _advancedCharts = [];
    private readonly ObservableCollection<BusinessChartRecord> _dailyRequests = [];
    private readonly ObservableCollection<BusinessChartRecord> _channelResults = [];
    private readonly ObservableCollection<BusinessChartRecord> _processingTimes = [];
    private Grid? _advancedChartGrid;
    private Expander? _advancedExamples;
    private int _businessRevision;

    private Control CreateDataAndAxesExamples()
    {
        var dates = CreateAdvancedChart("BusinessDateChart");
        dates.XAxis = new DateTimeAxis { Title = "日期", DateFormat = "MM-dd" };
        dates.YAxis = new NumericAxis { Title = "请求 / 千次", Minimum = 0 };
        dates.Series.Add(new LineSeries { Title = "本期", ItemsSource = _dailyRequests, CategoryPath = nameof(BusinessChartRecord.X), ValuePath = nameof(BusinessChartRecord.Value), IdPath = nameof(BusinessChartRecord.Id), Stroke = Blue });
        dates.Series.Add(new LineSeries { Title = "上期", ItemsSource = _dailyRequests, CategoryPath = nameof(BusinessChartRecord.X), ValuePath = nameof(BusinessChartRecord.Secondary), IdPath = nameof(BusinessChartRecord.Id), Stroke = Teal });

        var channels = CreateAdvancedChart("BusinessMultiAxisChart");
        channels.XAxis = new CategoryAxis { Title = "渠道" };
        channels.YAxis = new NumericAxis { Title = "营收 / 万元", Minimum = 0 };
        channels.Axes.Add(new NumericAxis { Key = "conversion", Title = "转化率 / %", Minimum = 0, Maximum = 12, Position = ChartAxisPosition.Far });
        channels.Series.Add(new BarSeries { Title = "营收", ItemsSource = _channelResults, CategoryPath = nameof(BusinessChartRecord.Category), ValuePath = nameof(BusinessChartRecord.Value), IdPath = nameof(BusinessChartRecord.Id), Fill = Blue, Stroke = Blue });
        channels.Series.Add(new LineSeries { Title = "转化率", ItemsSource = _channelResults, CategoryPath = nameof(BusinessChartRecord.Category), ValuePath = nameof(BusinessChartRecord.Secondary), IdPath = nameof(BusinessChartRecord.Id), YAxisKey = "conversion", Stroke = Orange, StrokeThickness = 3 });

        var scale = CreateAdvancedChart("BusinessLogarithmicChart");
        scale.XAxis = new LogarithmicAxis { Title = "数据规模 / 条", Minimum = 100, Maximum = 1_000_000 };
        scale.YAxis = new LogarithmicAxis { Title = "处理耗时 / ms", Minimum = 1, Maximum = 10_000 };
        scale.Series.Add(new ScatterSeries { Title = "方案 A", ItemsSource = _processingTimes, CategoryPath = nameof(BusinessChartRecord.X), ValuePath = nameof(BusinessChartRecord.Value), IdPath = nameof(BusinessChartRecord.Id), Fill = Teal, Stroke = Teal, MarkerSize = 8 });
        scale.Series.Add(new ScatterSeries { Title = "方案 B", ItemsSource = _processingTimes, CategoryPath = nameof(BusinessChartRecord.X), ValuePath = nameof(BusinessChartRecord.Secondary), IdPath = nameof(BusinessChartRecord.Id), Fill = Orange, Stroke = Orange, MarkerSize = 8 });

        _advancedChartGrid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        _advancedChartGrid.Children.Add(CreateChartCard("日期趋势", "DATE", "按实际日期间距查看每日请求量", dates));
        _advancedChartGrid.Children.Add(CreateChartCard("渠道营收与转化", "MULTI AXIS", "左轴比较营收，右轴读取转化率", channels));
        _advancedChartGrid.Children.Add(CreateChartCard("规模与耗时", "LOG SCALE", "用对数刻度观察跨数量级的变化", scale));
        LayoutAdvancedCharts(_threeColumns);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        AddAction(actions, "更新业务数据", UpdateBusinessData, "修改业务对象属性，日期、分类和对数图同步重新读取数值。");
        AddAction(actions, "恢复全部范围", () => { foreach (var chart in _advancedCharts) chart.ResetViewport(); }, "重置这三张图的缩放和平移。");
        UpdateBusinessData();
        return _advancedExamples = new Expander
        {
            Name = "ChartDataAndAxesExamples", Header = "更多示例：业务对象、日期轴、对数轴与多坐标轴", HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0), Children =
            {
                new TextBlock { Text = "三张图直接读取业务记录。点击更新，观察同一记录的属性变化；系列保留稳定的数据 ID。", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = .7 },
                actions, _advancedChartGrid,
            } },
        };
    }

    private CartesianChart CreateAdvancedChart(string name)
    {
        var chart = new CartesianChart { Name = name, Height = 360, IsTooltipEnabled = true, IsZoomEnabled = true, IsPanEnabled = true };
        _advancedCharts.Add(chart);
        chart.ViewportChanged += ChartViewportChanged;
        return chart;
    }

    private void LayoutAdvancedCharts(bool threeColumns)
    {
        if (_advancedChartGrid is null) return;
        _advancedChartGrid.ColumnDefinitions = new ColumnDefinitions(threeColumns ? "*,*,*" : "*");
        _advancedChartGrid.RowDefinitions = new RowDefinitions(threeColumns ? "Auto" : "Auto,Auto,Auto");
        for (var index = 0; index < _advancedChartGrid.Children.Count; index++)
        {
            Grid.SetColumn(_advancedChartGrid.Children[index], threeColumns ? index : 0);
            Grid.SetRow(_advancedChartGrid.Children[index], threeColumns ? 0 : index);
        }
    }

    private void UpdateBusinessData()
    {
        _businessRevision++;
        if (_dailyRequests.Count == 0)
        {
            var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
            for (var index = 0; index < 12; index++) _dailyRequests.Add(new($"day-{index}", start.AddDays(index).ToUnixTimeMilliseconds(), string.Empty, 45 + index * 4, 34 + index * 3));
            var categories = new[] { "官网", "小程序", "门店", "合作方" };
            for (var index = 0; index < categories.Length; index++) _channelResults.Add(new($"channel-{index}", index, categories[index], 80 + index * 33, 4 + index * 1.5));
            for (var index = 0; index < 9; index++) { var x = 100 * Math.Pow(10, index / 2d); _processingTimes.Add(new($"scale-{index}", x, string.Empty, 2 + x / 400, 3 + x / 220)); }
        }
        var offset = _businessRevision % 4;
        for (var index = 0; index < _dailyRequests.Count; index++) { _dailyRequests[index].Value = 45 + index * 4 + offset * (index % 2 == 0 ? 7 : -4); _dailyRequests[index].Secondary = 34 + index * 3 + offset * 2; }
        for (var index = 0; index < _channelResults.Count; index++) { _channelResults[index].Value = 80 + index * 33 + offset * 13; _channelResults[index].Secondary = 4 + index * 1.5 + offset * .4; }
        for (var index = 0; index < _processingTimes.Count; index++) { _processingTimes[index].Value = 2 + _processingTimes[index].X / (400 - offset * 20); _processingTimes[index].Secondary = 3 + _processingTimes[index].X / (220 - offset * 10); }
        foreach (var chart in _advancedCharts) chart.ResetViewport();
    }
}

internal sealed class BusinessChartRecord(string id, double x, string category, double value, double secondary) : INotifyPropertyChanged
{
    private double _value = value;
    private double _secondary = secondary;
    public string Id { get; } = id;
    public double X { get; } = x;
    public string Category { get; } = category;
    public double Value { get => _value; set { if (_value == value) return; _value = value; Changed(); } }
    public double Secondary { get => _secondary; set { if (_secondary == value) return; _secondary = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
}
