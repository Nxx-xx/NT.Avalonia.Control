using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<CartesianChart> _commonCharts = [];
    private readonly TextBlock _commonStatus = new() { Text = "点击图例切换渠道；在监测图中悬停读数、选择或框选数据。", Opacity = .7, TextWrapping = TextWrapping.Wrap };
    private readonly ChartPoint[] _commonResponse = [new(0, 25), new(1, 34), new(2, 30), new(3, 48), new(4, 44), new(5, 55), new(6, 67), new(7, 57), new(8, 68), new(9, 60), new(10, 76)];
    private Grid? _commonChartGrid;
    private Expander? _commonExamples;

    private Control CreateCommonFeaturesExamples()
    {
        var sales = CreateCommonChart("CommonSalesChart");
        sales.XAxis = new CategoryAxis { Title = "月份", Labels = MonthLabels(6), PlotMode = CategoryPlotMode.BetweenTicks };
        sales.YAxis = new NumericAxis { Title = "销售额 / 万元", Minimum = 0 };
        sales.Series.Add(new BarSeries
        {
            Title = "线上", ItemsSource = new ChartPoint[] { new(0, 45), new(1, 60), new(2, 55), new(3, 75), new(4, 88), new(5, 95) },
            ShowDataLabels = true, DataLabelFormat = "0", DataLabelPosition = ChartDataLabelPosition.InsideEnd, DataLabelForeground = Brushes.White,
        });
        sales.Series.Add(new LineSeries
        {
            Title = "门店", ItemsSource = new ChartPoint[] { new(0, 33), new(1, 43), new(2, 38), new(3, 55), new(4, 64), new(5, 71) },
            MarkerShape = ChartMarkerShape.Diamond, MarkerSize = 9, StrokeThickness = 3,
        });
        sales.Series.Add(new ScatterSeries
        {
            Title = "合作", ItemsSource = new ChartPoint[] { new(0, 22), new(1, 30), new(2, 26), new(3, 36), new(4, 44), new(5, 52) },
            MarkerShape = ChartMarkerShape.Triangle, MarkerSize = 11,
        });
        sales.Annotations.Add(new LineAnnotation { Orientation = Orientation.Horizontal, Value = 80, Label = "阶段目标 80 万元", Stroke = Orange, StrokeThickness = 1.5 });

        var monitor = CreateCommonChart("CommonMonitorChart");
        monitor.XAxis = new NumericAxis { Title = "观察时间 / 小时", Minimum = 0, Maximum = 10, Interval = 2 };
        monitor.YAxis = new NumericAxis { Title = "响应时间 / ms", Minimum = 0, Maximum = 100 };
        monitor.TooltipMode = ChartTooltipMode.SharedX; monitor.ShowCrosshair = true; monitor.SelectionMode = ChartSelectionMode.Multiple;
        monitor.TooltipFormatter = points => string.Join(Environment.NewLine, points.Select(point => $"{point.Series.Title} · {point.Point.Y.ToString("0.#", CultureInfo.CurrentCulture)} ms"));
        monitor.Series.Add(new LineSeries { Title = "接口 A", ItemsSource = _commonResponse, MarkerSize = 8, StrokeThickness = 3 });
        monitor.Series.Add(new LineSeries { Title = "接口 B", ItemsSource = new ChartPoint[] { new(0, 20), new(1, 28), new(2, 24), new(3, 32), new(4, 38), new(5, 44), new(6, 52), new(7, 49), new(8, 56), new(9, 50), new(10, 62) }, MarkerShape = ChartMarkerShape.Square, MarkerSize = 8 });
        monitor.Series.Add(new ScatterSeries { Title = "观测样本", ItemsSource = new ChartPoint[] { new(0, 18), new(1, 26), new(2, 22), new(3, 36), new(4, 35), new(5, 46), new(6, 50), new(7, 47), new(8, 54), new(9, 48), new(10, 59) }, MarkerShape = ChartMarkerShape.Diamond, MarkerSize = 9 });
        monitor.Annotations.Add(new BandAnnotation { Orientation = Orientation.Horizontal, FromValue = 40, ToValue = 65, Label = "常态区间", Fill = Brush("#1819A798"), Foreground = Teal, Layer = ChartAnnotationLayer.BehindSeries });
        monitor.Annotations.Add(new LineAnnotation { Orientation = Orientation.Horizontal, Value = 75, Label = "响应目标 75 ms", Stroke = Orange, StrokeThickness = 1.5 });
        monitor.Annotations.Add(new TextAnnotation { X = 8, Y = 8, Text = "Shift + 拖动：框选", CoordinateUnit = ChartAnnotationCoordinateUnit.Pixel });
        monitor.PropertyChanged += CommonInteractionChanged;

        _commonChartGrid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        _commonChartGrid.Children.Add(CreateChartCard("渠道销售与目标", "LEGEND + LABELS", "点击图例显隐渠道，恢复后仍沿用原来的颜色", sales));
        _commonChartGrid.Children.Add(CreateChartCard("服务监测与读数", "SHARED + SELECT", "同一时刻读取三组数据，选择后继续查看和比较", monitor));
        LayoutCommonFeatures(_threeColumns);
        var actions = new WrapPanel();
        var labels = new CheckBox { Content = "显示数值标签", IsChecked = true, Margin = new Thickness(0, 0, 14, 6) };
        labels.IsCheckedChanged += (_, _) => sales.Series[0].ShowDataLabels = labels.IsChecked == true;
        var shared = new CheckBox { Content = "共享读数", IsChecked = true, Margin = new Thickness(0, 0, 14, 6) };
        shared.IsCheckedChanged += (_, _) => monitor.TooltipMode = shared.IsChecked == true ? ChartTooltipMode.SharedX : ChartTooltipMode.NearestPoint;
        var crosshair = new CheckBox { Content = "十字线", IsChecked = true, Margin = new Thickness(0, 0, 14, 6) };
        crosshair.IsCheckedChanged += (_, _) => monitor.ShowCrosshair = crosshair.IsChecked == true;
        var legend = new ComboBox { ItemsSource = new[] { "图例在上", "图例在下", "图例在左", "图例在右" }, SelectedIndex = 1, MinWidth = 120, Margin = new Thickness(0, 0, 8, 6) };
        legend.SelectionChanged += (_, _) => { foreach (var chart in _commonCharts) chart.LegendPosition = (ChartLegendPosition)Math.Max(0, legend.SelectedIndex); };
        var selection = new ComboBox { ItemsSource = new[] { "关闭选择", "单点选择", "多点选择" }, SelectedIndex = 2, MinWidth = 120, Margin = new Thickness(0, 0, 8, 6) };
        selection.SelectionChanged += (_, _) => monitor.SelectionMode = (ChartSelectionMode)Math.Max(0, selection.SelectedIndex);
        actions.Children.Add(labels); actions.Children.Add(shared); actions.Children.Add(crosshair); actions.Children.Add(legend); actions.Children.Add(selection);
        AddLegendLayoutActions(actions);
        AddAction(actions, "切换图例排列", () => { foreach (var chart in _commonCharts) chart.LegendOrientation = chart.LegendOrientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal; }, "在横排和竖排图例之间切换。");
        AddAction(actions, "框选后半程", () => { var plot = monitor.PlotArea; monitor.SelectRectangle(new Rect(plot.Left + plot.Width * .5, plot.Top, plot.Width * .4, plot.Height)); }, "选择监测图后半程的数据；也可按住 Shift 在绘图区拖动。");
        AddAction(actions, "清除选择", monitor.ClearSelection, "清除监测图上的选中数据。");
        AddSelectionChangingActions(actions, monitor);
        var export = new Button { Content = "导出监测图 PNG", Margin = new Thickness(0, 0, 8, 6) };
        export.Click += async (_, _) => await ExportCommonChartAsync(monitor);
        actions.Children.Add(export);
        return _commonExamples = new Expander
        {
            Name = "ChartCommonFeaturesExamples", Header = "交互与呈现：图例、数据标签、共享读数、选择和注释", HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0), Children =
            {
                new TextBlock { Text = "点击图例切换系列；点击图表后使用方向键浏览数据，Home 恢复视图，Esc 取消当前操作；Shift + 拖动可框选。", TextWrapping = TextWrapping.Wrap, Opacity = .7 },
                actions, _commonChartGrid, _commonStatus,
            } },
        };
    }

    private CartesianChart CreateCommonChart(string name)
    {
        var chart = CreateAdvancedChart(name);
        chart.Height = 380; chart.ColorPalette = new[] { Blue, Teal, Orange }; chart.LegendPosition = ChartLegendPosition.Bottom;
        chart.IsLegendInteractive = true; chart.IsKeyboardNavigationEnabled = true; _commonCharts.Add(chart);
        return chart;
    }

    private void CommonInteractionChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (sender is CartesianChart chart && args.Property == CartesianChart.SelectedPointsProperty)
            _commonStatus.Text = CommonSelection(chart).Count == 0 ? "尚未选择数据；可点击、使用方向键或 Shift + 拖动框选。" : $"已选择 {CommonSelection(chart).Count} 个数据点；点击“清除选择”可取消。";
    }

    private void LayoutCommonFeatures(bool twoColumns)
    {
        if (_commonChartGrid is null) return;
        _commonChartGrid.ColumnDefinitions = new ColumnDefinitions(twoColumns ? "*,*" : "*");
        _commonChartGrid.RowDefinitions = new RowDefinitions(twoColumns ? "Auto" : "Auto,Auto");
        Grid.SetColumn(_commonChartGrid.Children[1], twoColumns ? 1 : 0); Grid.SetRow(_commonChartGrid.Children[1], twoColumns ? 0 : 1);
    }

    private async Task ExportCommonChartAsync(CartesianChart chart)
    {
        try
        {
            var file = await _hostWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出图表", SuggestedFileName = "服务监测.png", DefaultExtension = "png",
                FileTypeChoices = new[] { new FilePickerFileType("PNG 图片") { Patterns = new[] { "*.png" } } },
            });
            if (file is null) return;
            var path = file.TryGetLocalPath();
            bool saved;
            if (path is not null) saved = chart.TryExportPng(path, new ChartExportOptions { PixelSize = new PixelSize(1600, 900) });
            else { await using var stream = await file.OpenWriteAsync(); saved = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(1600, 900) }); }
            _commonStatus.Text = saved ? $"已导出 {file.Name}（1600 × 900）。" : "图表导出失败，请检查保存位置后重试。";
        }
        catch (Exception exception) { _commonStatus.Text = $"图表导出失败：{exception.Message}"; }
    }

    private static IReadOnlyList<ChartPointHit> CommonSelection(CartesianChart chart) => chart.SelectedPoints ?? Array.Empty<ChartPointHit>();
}
