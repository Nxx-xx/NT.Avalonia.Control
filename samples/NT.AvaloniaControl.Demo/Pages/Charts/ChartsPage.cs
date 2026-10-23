using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Demo.Navigation;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage : UserControl, IDemoPage
{
    private static readonly IBrush Blue = Brush("#4385F4");
    private static readonly IBrush Teal = Brush("#19A798");
    private static readonly IBrush Orange = Brush("#F2A33A");
    private readonly Window _hostWindow;
    private readonly List<Border> _cards = [];
    private readonly ObservableCollection<ChartPoint> _visits = [];
    private readonly ObservableCollection<ChartPoint> _previousVisits = [];
    private readonly ObservableCollection<ChartPoint> _online = [];
    private readonly ObservableCollection<ChartPoint> _retail = [];
    private readonly ObservableCollection<ChartPoint> _normalLatency = [];
    private readonly ObservableCollection<ChartPoint> _peakLatency = [];
    private readonly CartesianChart _lineChart;
    private readonly CartesianChart _barChart;
    private readonly CartesianChart _scatterChart;
    private readonly Grid _chartGrid = new() { ColumnSpacing = 16, RowSpacing = 16 };
    private readonly TextBlock _status = new() { Name = "ChartsStatus", FontSize = 12, Opacity = .7, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _viewportStatus = new() { Name = "ChartsViewportStatus", Text = "视图：自动适配全部数据", FontSize = 12, Opacity = .7, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 6) };
    private readonly CheckBox _tooltipOption = new() { Name = "ChartTooltipOption", Content = "悬停提示", IsChecked = true, Margin = new Thickness(0, 0, 16, 6) };
    private readonly CheckBox _zoomOption = new() { Name = "ChartZoomOption", Content = "滚轮缩放", IsChecked = true, Margin = new Thickness(0, 0, 16, 6) };
    private readonly CheckBox _panOption = new() { Name = "ChartPanOption", Content = "拖动平移", IsChecked = true, Margin = new Thickness(0, 0, 16, 6) };
    private readonly ComboBox _interactionAxes = new() { Name = "ChartInteractionAxes", ItemsSource = new[] { "仅横轴 X", "仅纵轴 Y", "横轴与纵轴" }, SelectedIndex = 2, MinWidth = 142, Margin = new Thickness(0, 0, 8, 6) };
    private readonly ScrollViewer _scroll;
    private readonly Border _lineCard;
    private readonly Border _barCard;
    private readonly Border _scatterCard;
    private readonly TextBlock _visitsMetric = new() { FontSize = 22, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _salesMetric = new() { FontSize = 22, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _latencyMetric = new() { FontSize = 22, FontWeight = FontWeight.SemiBold };
    private int _revision;
    private bool _active;
    private bool _disposed;
    private bool _threeColumns;

    internal ChartsPage(Window hostWindow, bool galleryMode = false)
    {
        _hostWindow = hostWindow;
        Name = "ChartsPage";
        _lineChart = new CartesianChart
        {
            Name = "VisitsLineChart", Height = 360,
            XAxis = new ChartAxis { Title = "月份", Labels = MonthLabels(12) },
            YAxis = new ChartAxis { Title = "日均请求 / 千次", Minimum = 0, LabelFormat = "0" },
        };
        _lineChart.Series.Add(new LineSeries { Title = "本期", Stroke = Blue, StrokeThickness = 3, MarkerSize = 6, ItemsSource = _visits });
        _lineChart.Series.Add(new LineSeries { Title = "上期", Stroke = Teal, StrokeThickness = 2, MarkerSize = 5, ItemsSource = _previousVisits });
        _barChart = new CartesianChart
        {
            Name = "SalesBarChart", Height = 360,
            XAxis = new ChartAxis { Title = "月份", Labels = MonthLabels(6) },
            YAxis = new ChartAxis { Title = "销售额 / 万元", Minimum = 0, LabelFormat = "0" },
        };
        _barChart.Series.Add(new BarSeries { Title = "线上", Fill = Blue, Stroke = Blue, ItemsSource = _online });
        _barChart.Series.Add(new BarSeries { Title = "门店", Fill = Teal, Stroke = Teal, ItemsSource = _retail });
        _scatterChart = new CartesianChart
        {
            Name = "LatencyScatterChart", Height = 360,
            XAxis = new ChartAxis { Title = "CPU 使用率 / %", Minimum = 0, Maximum = 100, LabelFormat = "0" },
            YAxis = new ChartAxis { Title = "响应时间 / ms", Minimum = 0, Maximum = 250, LabelFormat = "0" },
        };
        _scatterChart.Series.Add(new ScatterSeries { Title = "日常负载", Fill = Teal, Stroke = Teal, MarkerSize = 8, ItemsSource = _normalLatency });
        _scatterChart.Series.Add(new ScatterSeries { Title = "高峰负载", Fill = Orange, Stroke = Orange, MarkerSize = 9, ItemsSource = _peakLatency });
        foreach (var chart in AllCharts)
        {
            chart.IsTooltipEnabled = true;
            chart.IsZoomEnabled = true;
            chart.IsPanEnabled = true;
            chart.InteractionAxes = ChartInteractionAxes.Both;
            chart.ViewportChanged += ChartViewportChanged;
        }

        var heading = new StackPanel { Spacing = 5, Children =
        {
            new TextBlock { Text = "Charts", FontSize = 27, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "悬停查看数据 · 绘图区滚轮缩放 · 按住左键拖动平移 · 双击恢复视图", FontSize = 13, TextWrapping = TextWrapping.Wrap, Opacity = .7 },
        } };
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        AddAction(actions, "刷新示例数据", RefreshData, "切换到另一组示例数据，观察趋势、销售与负载分布的变化。");
        AddAction(actions, "追加一个月", AppendMonth, "向访问趋势中添加下一个月的数据。");
        AddAction(actions, "恢复初始数据", ResetData, "恢复最初的 12 个月趋势、6 个月销售及负载分布。");
        var gridLines = new CheckBox { Content = "显示网格线", IsChecked = true, Margin = new Thickness(8, 0, 16, 6) };
        gridLines.IsCheckedChanged += (_, _) => SetGridLines(gridLines.IsChecked == true);
        actions.Children.Add(gridLines);
        var legends = new CheckBox { Content = "显示图例", IsChecked = true, Margin = new Thickness(0, 0, 0, 6) };
        legends.IsCheckedChanged += (_, _) => SetLegends(legends.IsChecked == true);
        actions.Children.Add(legends);

        _tooltipOption.IsCheckedChanged += (_, _) => SetInteractionOptions();
        _zoomOption.IsCheckedChanged += (_, _) => SetInteractionOptions();
        _panOption.IsCheckedChanged += (_, _) => SetInteractionOptions();
        _interactionAxes.SelectionChanged += (_, _) => SetInteractionAxes();
        var interactions = new WrapPanel { Orientation = Orientation.Horizontal, Children =
        {
            _tooltipOption, _zoomOption, _panOption, _interactionAxes,
        } };
        ToolTip.SetTip(_interactionAxes, "选择滚轮缩放和拖动平移影响的坐标轴。");
        AddAction(interactions, "重置视图", ResetViews, "让三张图重新显示全部数据；也可双击某张图的绘图区单独重置。");
        interactions.Children.Add(_viewportStatus);

        var metrics = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 16 };
        AddMetric(metrics, "最新日均请求", _visitsMetric, "千次 / 日", Blue, 0);
        AddMetric(metrics, "本期累计销售", _salesMetric, "万元", Teal, 1);
        AddMetric(metrics, "样本平均响应", _latencyMetric, "ms", Orange, 2);

        _lineCard = CreateChartCard("访问趋势", "LINE", "对比本期与上期的月度日均请求量", _lineChart);
        _barCard = CreateChartCard("渠道销售", "BAR", "对比线上与门店的每月销售表现", _barChart);
        _scatterCard = CreateChartCard("负载与响应", "SCATTER", "观察日常与高峰负载下的响应时间", _scatterChart);
        _chartGrid.Children.Add(_lineCard);
        _chartGrid.Children.Add(_barCard);
        _chartGrid.Children.Add(_scatterCard);
        SetChartLayout(false);
        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 12, 16), Children =
            {
                heading, actions, interactions, metrics, _chartGrid, _status,
            } },
        };
        if (!galleryMode && _scroll.Content is StackPanel legacy)
            foreach (var section in ChartSections()) legacy.Children.Insert(legacy.Children.Count - 1, section.Create());
        Content = _scroll;
        SizeChanged += PageSizeChanged;
        _hostWindow.ActualThemeVariantChanged += HostThemeChanged;
        HostThemeChanged(null, EventArgs.Empty);
        ResetData();
    }

    public Control View => this;
    public void Activate() { if (!_disposed) _active = true; }
    public void Deactivate()
    {
        _active = false;
        _incrementalStreamingBoard?.Stop();
    }
    public void Dispose()
    {
        if (_disposed) return;
        Deactivate();
        _disposed = true;
        foreach (var chart in _relationsCharts) chart.Dispose();
        foreach (var chart in _mapCharts) chart.Dispose();
        foreach (var chart in _c6Charts.OfType<IDisposable>()) chart.Dispose();
        DisposeC5CompletionExamples();
        _selectionRangeBoard?.Group.Dispose();
        _inputGesturesBoard?.Dispose();
        _annotationCompletionBoard?.Dispose();
        _animationBoard?.Dispose();
        _exportCompletionBoard?.Dispose();
        _accessibilityBoard?.Dispose();
        _lineAreaBoard?.Dispose();
        _barComparisonBoard?.Dispose();
        _scatterBubbleBoard?.Dispose();
        _circularBoard?.Dispose();
        _polarBoard?.Dispose();
        _gaugeMicroBoard?.Dispose();
        _analysisBoard?.Dispose();
        _levelOfDetailBoard?.Dispose();
        _efficientHitBoard?.Dispose();
        _incrementalStreamingBoard?.Dispose();
        _renderResourceBoard?.Dispose();
        _multiChartBoard?.Dispose();
        SizeChanged -= PageSizeChanged;
        _hostWindow.ActualThemeVariantChanged -= HostThemeChanged;
        foreach (var chart in AllCharts.Concat(_advancedCharts))
        {
            chart.ViewportChanged -= ChartViewportChanged;
            chart.Series.Clear();
        }
    }

    private CartesianChart[] AllCharts => [_lineChart, _barChart, _scatterChart];

    private void ResetData()
    {
        if (_disposed) return;
        _revision = 0;
        PopulateData();
        ResetViews();
        _status.Text = "初始样例 · 使用固定演示数据；可通过主窗口切换亮 / 暗主题。";
    }

    private void RefreshData()
    {
        if (_disposed) return;
        _revision++;
        PopulateData();
        ResetViews();
        _status.Text = $"已刷新第 {_revision} 组样例 · 三张图表和汇总数字已同步更新。";
    }

    private void PopulateData()
    {
        var variation = _revision % 4;
        SetPoints(_visits, new double[] { 38, 46, 42, 61, 55, 72, 68, 84, 78, 96, 91, 112 }
            .Select((value, index) => new ChartPoint(index, value + variation * (index % 3 == 0 ? 5 : -3))));
        SetPoints(_previousVisits, new double[] { 29, 35, 33, 43, 47, 41, 54, 59, 56, 65, 69, 77 }
            .Select((value, index) => new ChartPoint(index, value + variation * 2)));
        _lineChart.XAxis.Labels = MonthLabels(12);
        SetPoints(_online, new double[] { 42, 58, 51, 74, 68, 89 }
            .Select((value, index) => new ChartPoint(index, value + variation * (index + 1))));
        SetPoints(_retail, new double[] { 35, 41, 46, 52, 57, 63 }
            .Select((value, index) => new ChartPoint(index, value - variation * 2)));
        SetPoints(_normalLatency, Enumerable.Range(0, 24).Select(index =>
            new ChartPoint(16 + index * 2.2 + (index % 3) * 2 + variation, 37 + index * 2.1 + ((index * 7) % 19) + variation * 3)));
        SetPoints(_peakLatency, Enumerable.Range(0, 16).Select(index =>
            new ChartPoint(49 + index * 2.7 + variation, 88 + index * 5.8 + ((index * 11) % 29) + variation * 3)));
        UpdateMetrics();
    }

    private void AppendMonth()
    {
        if (_disposed) return;
        var index = _visits.Count;
        _visits.Add(new ChartPoint(index, Math.Max(20, _visits[^1].Y + (index % 3 == 0 ? -8 : 11))));
        _previousVisits.Add(new ChartPoint(index, Math.Max(20, _previousVisits[^1].Y + (index % 2 == 0 ? 6 : -3))));
        _lineChart.XAxis.Labels = MonthLabels(index + 1);
        _lineChart.ResetViewport();
        UpdateMetrics();
        _status.Text = $"已追加 {MonthLabels(index + 1)[index]} · 当前显示 {_visits.Count} 个月的访问趋势。";
    }

    private void UpdateMetrics()
    {
        _visitsMetric.Text = _visits[^1].Y.ToString("0");
        _salesMetric.Text = (_online.Sum(point => point.Y) + _retail.Sum(point => point.Y)).ToString("0");
        _latencyMetric.Text = _normalLatency.Concat(_peakLatency).Average(point => point.Y).ToString("0");
    }

    private void SetGridLines(bool value) { foreach (var chart in AllCharts.Concat(_advancedCharts)) chart.ShowGridLines = value; }
    private void SetLegends(bool value) { foreach (var chart in AllCharts.Concat(_advancedCharts)) chart.ShowLegend = value; }

    private void SetInteractionOptions()
    {
        foreach (var chart in AllCharts.Concat(_advancedCharts))
        {
            chart.IsTooltipEnabled = _tooltipOption.IsChecked == true;
            chart.IsZoomEnabled = _zoomOption.IsChecked == true;
            chart.IsPanEnabled = _panOption.IsChecked == true;
        }
    }

    private void SetInteractionAxes()
    {
        var axes = _interactionAxes.SelectedIndex switch
        {
            0 => ChartInteractionAxes.X,
            1 => ChartInteractionAxes.Y,
            _ => ChartInteractionAxes.Both,
        };
        foreach (var chart in AllCharts.Concat(_advancedCharts)) chart.InteractionAxes = axes;
    }

    private void ResetViews()
    {
        if (_disposed) return;
        foreach (var chart in AllCharts.Concat(_advancedCharts)) chart.ResetViewport();
        UpdateViewportStatus();
    }

    private void ChartViewportChanged(object? sender, EventArgs args) => UpdateViewportStatus(sender as CartesianChart);

    private void UpdateViewportStatus(CartesianChart? changedChart = null)
    {
        var chart = changedChart?.Viewport is not null ? changedChart : AllCharts.Concat(_advancedCharts).FirstOrDefault(item => item.Viewport is not null);
        if (chart?.Viewport is not { } range)
        {
            _viewportStatus.Text = "视图：自动适配全部数据";
            return;
        }
        var name = chart.Name switch { "VisitsLineChart" => "趋势", "SalesBarChart" => "销售", "BusinessDateChart" => "日期", "BusinessMultiAxisChart" => "渠道", "BusinessLogarithmicChart" => "规模", _ => "负载" };
        _viewportStatus.Text = $"{name}视图：X {range.XMinimum:0.#}–{range.XMaximum:0.#} · Y {range.YMinimum:0.#}–{range.YMaximum:0.#}";
    }

    private void PageSizeChanged(object? sender, SizeChangedEventArgs args) => SetChartLayout(args.NewSize.Width >= 1050);

    private void SetChartLayout(bool threeColumns)
    {
        if (_chartGrid.ColumnDefinitions.Count > 0 && _threeColumns == threeColumns) return;
        _threeColumns = threeColumns;
        _chartGrid.ColumnDefinitions = new ColumnDefinitions(threeColumns ? "*,*,*" : "*");
        _chartGrid.RowDefinitions = new RowDefinitions(threeColumns ? "Auto" : "Auto,Auto,Auto");
        Grid.SetColumn(_barCard, threeColumns ? 1 : 0);
        Grid.SetRow(_barCard, threeColumns ? 0 : 1);
        Grid.SetColumn(_scatterCard, threeColumns ? 2 : 0);
        Grid.SetRow(_scatterCard, threeColumns ? 0 : 2);
        LayoutAdvancedCharts(threeColumns);
        LayoutAxisFeatures(threeColumns);
        LayoutCommonFeatures(threeColumns);
    }

    private Border CreateChartCard(string title, string kind, string description, CartesianChart chart)
    {
        var heading = new DockPanel { LastChildFill = true };
        var tag = new TextBlock { Text = kind, FontSize = 10, FontWeight = FontWeight.SemiBold, Opacity = .55, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(tag, Dock.Right);
        heading.Children.Add(tag);
        heading.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeight.SemiBold });
        var card = new Border
        {
            Padding = new Thickness(18, 15, 18, 12), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1),
            Child = new StackPanel { Spacing = 6, Children =
            {
                heading,
                new TextBlock { Text = description, FontSize = 12, Opacity = .6, TextWrapping = TextWrapping.Wrap },
                chart,
            } },
        };
        _cards.Add(card);
        return card;
    }

    private void AddMetric(Grid grid, string label, TextBlock value, string unit, IBrush accent, int column)
    {
        var number = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children =
        {
            value, new TextBlock { Text = unit, FontSize = 12, Opacity = .6, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 3) },
        } };
        var card = new Border
        {
            Padding = new Thickness(16, 8), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
            Child = new StackPanel { Spacing = 3, Children =
            {
                new TextBlock { Text = label, FontSize = 12, Opacity = .7 }, number,
            } },
        };
        value.Foreground = accent;
        _cards.Add(card);
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private void HostThemeChanged(object? sender, EventArgs args)
    {
        var dark = _hostWindow.ActualThemeVariant == ThemeVariant.Dark;
        var background = Brush(dark ? "#182232" : "#FFFFFF");
        var border = Brush(dark ? "#2C394A" : "#DCE4EE");
        foreach (var card in _cards) { card.Background = background; card.BorderBrush = border; }
    }

    private static void AddAction(Panel panel, string label, Action action, string tip)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 6) };
        button.Click += (_, _) => action();
        ToolTip.SetTip(button, tip);
        panel.Children.Add(button);
    }

    private static void SetPoints(ObservableCollection<ChartPoint> collection, IEnumerable<ChartPoint> points)
    {
        collection.Clear();
        foreach (var point in points) collection.Add(point);
    }

    private static string[] MonthLabels(int count) => Enumerable.Range(0, count)
        .Select(index => index < 12 ? $"{index + 1}月" : index < 24 ? $"次年{index % 12 + 1}月" : $"第{index / 12 + 1}年{index % 12 + 1}月").ToArray();
    private static SolidColorBrush Brush(string color) => new(Color.Parse(color));
}
