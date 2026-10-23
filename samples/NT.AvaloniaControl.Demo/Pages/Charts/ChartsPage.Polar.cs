using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Expander? _polarExamples;
    private PolarBoard? _polarBoard;

    private Control CreatePolarExamples()
    {
        _polarBoard = CreatePolarBoard();
        return _polarExamples = new Expander { Name = "ChartPolarExamples", Header = "极坐标与雷达：角度、半径和八种系列", HorizontalAlignment = HorizontalAlignment.Stretch, Content = _polarBoard.View };
    }

    private PolarBoard CreatePolarBoard()
    {
        PolarChart Chart(string name, string title, bool radar = false)
        {
            PolarChart result = radar ? new RadarChart() : new PolarChart();
            result.Name = name; result.Title = title; result.Height = 300; result.Padding = new Thickness(4); result.ShowLegend = true;
            result.AngularAxis = new NumericAxis { Minimum = 0, Maximum = 360, Interval = 90 };
            result.RadialAxis = new NumericAxis { Minimum = 0, Maximum = 100, Interval = 25 };
            result.SelectionMode = ChartSelectionMode.Multiple; result.IsTooltipEnabled = true;
            return result;
        }
        var records = PolarRecords(); var categories = PolarCategories();
        T Numeric<T>(T series) where T : PolarSeries { series.ItemsSource = records; series.AnglePath = nameof(PolarRecord.Angle); return Map(series); }
        T Categorical<T>(T series) where T : PolarSeries { series.ItemsSource = categories; series.CategoryPath = nameof(PolarRecord.Label); return Map(series); }
        static T Map<T>(T series) where T : PolarSeries { series.RadiusPath = nameof(PolarRecord.Value); series.IdPath = nameof(PolarRecord.Id); series.ColorPath = nameof(PolarRecord.Color); return series; }
        var line = Chart("PolarLineChart", "极坐标线 · 原始角度顺序");
        var lineSeries = Numeric(new PolarLineSeries { Title = "角度轨迹", Stroke = Blue, StrokeThickness = 3, MarkerSize = 8, IsClosed = true }); line.Series.Add(lineSeries);
        var scatter = Chart("PolarScatterChart", "极坐标散点 · 直接业务颜色");
        var scatterSeries = Numeric(new PolarScatterSeries { Title = "测量值", MarkerSize = 14 }); scatter.Series.Add(scatterSeries);
        var filled = Chart("PolarFilledChart", "极坐标填充 · 闭合轮廓");
        var filledSeries = Numeric(new PolarFilledSeries { Title = "测量包络", Stroke = Teal, Fill = Teal, FillOpacity = .35, StrokeThickness = 2 }); filled.Series.Add(filledSeries);
        var filledScatter = Numeric(new PolarScatterSeries { Title = "原始测量", MarkerSize = 8 }); filled.Series.Add(filledScatter);
        var radar = Chart("RadarComparisonChart", "雷达 · 多系列共享类别", true);
        var radarSeries = Categorical(new RadarSeries { Title = "本期", Stroke = Blue, Fill = Blue, FillOpacity = .25 }); radar.Series.Add(radarSeries);
        var comparison = Categorical(new RadarSeries { Title = "上期 · 重排类别", Stroke = Teal, Fill = Teal, FillOpacity = .15 });
        comparison.ColorPath = null; comparison.ItemsSource = new[] { categories[2] with { Value = 45 }, categories[0] with { Value = 65 }, categories[3] with { Value = 85 }, categories[1] with { Value = 40 } }; radar.Series.Add(comparison);
        var radial = Chart("RadialLineChart", "径向折线 · 圆形网格");
        var radialSeries = Categorical(new RadialLineSeries { Title = "分类测量", Stroke = Teal, StrokeThickness = 3 }); radial.Series.Add(radialSeries);
        var area = Chart("PolarAreaChart", "极面积 · 扇区面积正比于值");
        var areaSeries = Categorical(new PolarAreaSeries { Title = "面积编码", InnerRadius = .18, SectorGap = 3 }); area.Series.Add(areaSeries);
        var mixedLine = Numeric(new PolarLineSeries { Title = "数值折线", Stroke = Brush("#D7259F"), StrokeThickness = 3, ShowMarkers = false, IsClosed = true });
        var rose = Chart("NightingaleRoseChart", "玫瑰图 · 半径线性编码");
        var roseSeries = Categorical(new NightingaleRoseSeries { Title = "半径编码", InnerRadius = .18, SectorGap = 3 }); rose.Series.Add(roseSeries);
        var bars = Chart("RadialBarChart", "径向条 · 分轨弧长编码");
        var barSeries = Categorical(new RadialBarSeries { Title = "进度", InnerRadius = .18, SectorGap = 3 }); bars.Series.Add(barSeries);
        var charts = new[] { line, scatter, filled, radar, radial, area, rose, bars };
        var gridShape = new ComboBox { Name = "PolarGridShape", ItemsSource = Enum.GetValues<PolarGridShape>(), SelectedItem = PolarGridShape.Circle, MinWidth = 100, Margin = new Thickness(0, 0, 12, 6) };
        gridShape.SelectionChanged += (_, _) => { if (gridShape.SelectedItem is PolarGridShape shape) foreach (var chart in charts) chart.GridShape = shape; };
        var unit = new ComboBox { Name = "PolarAngleUnit", ItemsSource = Enum.GetValues<ChartAngleUnit>(), SelectedItem = ChartAngleUnit.Degrees, MinWidth = 100, Margin = new Thickness(0, 0, 12, 6) };
        unit.SelectionChanged += (_, _) =>
        {
            if (unit.SelectedItem is not ChartAngleUnit value) return;
            var factor = value == ChartAngleUnit.Radians ? Math.PI / 180 : 1;
            foreach (var chart in charts) { chart.AngleUnit = value; chart.AngularAxis.Maximum = 360 * factor; chart.AngularAxis.Interval = 90 * factor; }
            foreach (var series in new PolarSeries[] { lineSeries, scatterSeries, filledSeries, filledScatter, mixedLine }) series.ItemsSource = records.Select(row => row with { Angle = row.Angle * factor }).ToArray();
        };
        var clockwise = new CheckBox { Name = "PolarClockwise", Content = "顺时针", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        clockwise.IsCheckedChanged += (_, _) => { foreach (var chart in charts) chart.Clockwise = clockwise.IsChecked == true; };
        var closed = new CheckBox { Name = "PolarClosed", Content = "闭合线 / 面", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        closed.IsCheckedChanged += (_, _) => { foreach (var series in new PolarSeries[] { lineSeries, filledSeries, radarSeries, comparison, radialSeries }) series.IsClosed = closed.IsChecked == true; };
        var labels = new CheckBox { Name = "PolarLabels", Content = "数据标签", Margin = new Thickness(0, 0, 12, 6) };
        labels.IsCheckedChanged += (_, _) => { foreach (var chart in charts) chart.ShowDataLabels = labels.IsChecked == true; };
        var mixed = new CheckBox { Name = "PolarMixedReference", Content = "叠加折线（r/R 参考轴）", Margin = new Thickness(0, 0, 12, 6) };
        mixed.IsCheckedChanged += (_, _) => { if (mixed.IsChecked == true) { if (!area.Series.Contains(mixedLine)) area.Series.Add(mixedLine); } else area.Series.Remove(mixedLine); area.Title = mixed.IsChecked == true ? "极面积 + 折线 · 归一半径参考" : "极面积 · 扇区面积正比于值"; };
        ToolTip.SetTip(mixed, "面积与数值折线的半径编码不同；叠加后用 r/R 和显示角 θ 作为公共参考，不把同一圆环标成两种业务值。");
        var status = new TextBlock { Name = "PolarSelectionStatus", Text = "点击真实源点或扇区选择；角度单位转换保留业务 ID。负半径与非有限值形成缺口。", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = .8 };
        foreach (var chart in charts) chart.SelectionChanged += (_, _) => status.Text = chart.SelectedPoint is { } point ? $"{chart.Title} · {point.StableId} · 原始值 {point.Point.Y:0.##} · 源索引 {point.Index}" : "选择已清除。";
        var options = new WrapPanel { Children = { new TextBlock { Text = "角度单位", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) }, unit, new TextBlock { Text = "网格", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) }, gridShape, clockwise, closed, labels, mixed } };
        AddAction(options, "起点旋转 45°", () => { foreach (var chart in charts) chart.StartAngle += 45; }, "角度轴的原始业务值不变，旋转显示起点。");
        AddAction(options, "270° / 全圆", () => { foreach (var chart in charts) chart.SweepAngle = chart.SweepAngle == 360 ? 270 : 360; }, "切换完整周期和部分角度投影。");
        var revision = 0;
        void UpdateData() { revision++; var values = revision % 2 == 0 ? new[] { 80d, 50, 70, 90 } : new[] { 40d, 85, 55, 70 }; foreach (var series in new PolarSeries[] { radarSeries, radialSeries, areaSeries, roseSeries, barSeries }) series.ItemsSource = categories.Select((row, index) => row with { Value = values[index] }).ToArray(); }
        AddAction(options, "更新分类数据", UpdateData, "雷达、径向折线、面积、玫瑰与径向条继续使用相同业务 ID。");
        AddAction(options, "清除选择", () => { foreach (var chart in charts) chart.ClearSelection(); }, "清除八图的业务项选择。");
        var export = new Button { Content = "导出雷达 PNG", Margin = new Thickness(0, 0, 8, 6) };
        export.Click += async (_, _) => { var file = await _hostWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions { Title = "导出雷达图", SuggestedFileName = "radar-chart.png", DefaultExtension = "png" }); if (file is null) return; await using var stream = await file.OpenWriteAsync(); await radar.TryExportPngAsync(stream, new ChartExportOptions { PixelSize = new PixelSize(1000, 700), Background = radar.Background }); };
        options.Children.Add(export);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), ColumnSpacing = 16, RowSpacing = 8 };
        for (var index = 0; index < charts.Length; index++) { grid.Children.Add(charts[index]); Grid.SetColumn(charts[index], index % 2); Grid.SetRow(charts[index], index / 2); }
        return new(new StackPanel { Spacing = 8, Children = { new TextBlock { Text = "数值角度支持度、弧度、方向、起点与部分圆；分类系列共享首见类别顺序。雷达用多边形网格，径向线用圆形网格。极面积按面积编码，玫瑰按半径编码，径向条按各自轨道的弧长编码。异构半径叠加可切换为 r/R 与 θ 参考。选择和导出保留真实业务项。", TextWrapping = TextWrapping.Wrap, Opacity = .8 }, options, grid, status } }, charts, records, categories, lineSeries, scatterSeries, filledSeries, radarSeries, radialSeries, areaSeries, roseSeries, barSeries, unit, gridShape, clockwise, closed, labels, mixed, UpdateData);
    }

    private static PolarRecord[] PolarRecords() => [new("polar-a", "北", 0, 80, "#4385F4"), new("polar-b", "东", 90, 50, "#19A798"), new("polar-c", "南", 180, 70, "#F2A33A"), new("polar-d", "西", 270, 90, "#D7259F")];
    private static PolarRecord[] PolarCategories() => [new("radar-a", "速度", 0, 80, "#4385F4"), new("radar-b", "质量", 90, 50, "#19A798"), new("radar-c", "成本", 180, 70, "#F2A33A"), new("radar-d", "交付", 270, 90, "#D7259F")];
    private sealed record PolarRecord(string Id, string Label, double Angle, double Value, string Color);
    private sealed record PolarBoard(StackPanel View, PolarChart[] Charts, PolarRecord[] Records, PolarRecord[] Categories, PolarLineSeries Line, PolarScatterSeries Scatter, PolarFilledSeries Filled, RadarSeries Radar, RadialLineSeries Radial, PolarAreaSeries Area, NightingaleRoseSeries Rose, RadialBarSeries Bars, ComboBox Unit, ComboBox GridShape, CheckBox Clockwise, CheckBox Closed, CheckBox Labels, CheckBox MixedReference, Action UpdateData) : IDisposable
    { public void Dispose() { foreach (var chart in Charts) chart.Series.Clear(); } }
}
