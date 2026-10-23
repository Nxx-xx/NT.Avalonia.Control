using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Expander? _scatterBubbleExamples;
    private ScatterBubbleBoard? _scatterBubbleBoard;

    private Control CreateScatterBubbleExamples()
    {
        _scatterBubbleBoard = CreateScatterBubbleBoard(true);
        return _scatterBubbleExamples = new Expander { Name = "ChartScatterBubbleExamples", Header = "散点与气泡：大小、颜色和源序连线", HorizontalAlignment = HorizontalAlignment.Stretch, Content = _scatterBubbleBoard.View };
    }

    private ScatterBubbleBoard CreateScatterBubbleBoard(bool register)
    {
        CartesianChart Chart(string name, string title)
        {
            var chart = register ? CreateAdvancedChart(name) : new CartesianChart { Name = name };
            chart.Height = 295; chart.Title = title; chart.LegendPosition = ChartLegendPosition.Bottom;
            chart.XAxis = new NumericAxis { Minimum = 0, Maximum = 10, Title = "真实 X" };
            chart.YAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "真实 Y" };
            chart.IsTooltipEnabled = true; chart.IsZoomEnabled = true; chart.IsPanEnabled = true;
            chart.SelectionMode = ChartSelectionMode.Multiple; chart.IsKeyboardNavigationEnabled = true;
            chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((context, _) => new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = context is null ? "" : string.Join("\n", context.Hits.Select(hit => $"{hit.Series.Title} · ({hit.Point.X:0.##}, {hit.Point.Y:0.##}) · 大小值 {hit.SizeValue:0.##} · 直径 {hit.MarkerDiameter:0.##} DIP · 源索引 {hit.Index}")),
            });
            return chart;
        }
        var records = new[] { new ScatterBubbleRecord("point-a", 2, 25, 0, 0), new ScatterBubbleRecord("point-b", 5, 55, 50, 50), new ScatterBubbleRecord("point-c", 8, 75, 100, 100) };
        var scatter = Chart("EncodedScatterChart", "散点 · 面积与数值渐变编码");
        var scatterSeries = new ScatterSeries { Title = "规模 / 热度", ItemsSource = records, CategoryPath = nameof(ScatterBubbleRecord.X), ValuePath = nameof(ScatterBubbleRecord.Y), IdPath = nameof(ScatterBubbleRecord.Id), SizePath = nameof(ScatterBubbleRecord.Size), ColorPath = nameof(ScatterBubbleRecord.Color),
            MinimumSize = 12, MaximumSize = 50, SizeMinimum = 0, SizeMaximum = 100, ColorMinimum = 0, ColorMaximum = 100, MinimumColor = Colors.Blue, MaximumColor = Colors.Red, MarkerStrokeThickness = 0 };
        scatter.Series.Add(scatterSeries);
        var area = Chart("AreaBubbleChart", "XY 气泡 · 面积尺度");
        var areaSeries = Bubble("业务规模", ChartSizeScale.Area); area.Series.Add(areaSeries);
        var diameter = Chart("DiameterBubbleChart", "相同源数据 · 直径尺度");
        var diameterSeries = Bubble("业务规模", ChartSizeScale.Diameter); diameter.Series.Add(diameterSeries);
        var line = Chart("ScatterLineVariantsChart", "Scatter line · 非单调 X 与缺失段");
        var lineSeries = new ScatterLineSeries { Title = "源序轨迹", ItemsSource = new ChartPoint[] { new(1, 20), new(7, 80), new(3, 60), new(4, double.NaN), new(7, 20), new(9, 40) }, Stroke = Teal, Fill = Teal, StrokeThickness = 3, MarkerSize = 10 };
        line.Series.Add(lineSeries); line.TrackballMode = ChartTrackballMode.Vertical; line.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        var overlap = Chart("BubbleOverlapChart", "重叠 · 源顺序与大小绘制顺序");
        var overlapping = new[] { new ScatterBubbleDirectRecord("large", 5, 50, 100, "#4385F4"), new ScatterBubbleDirectRecord("small", 5, 50, 25, "#19A798") };
        var overlapSeries = new BubbleSeries { Title = "后绘制图形优先", ItemsSource = overlapping, CategoryPath = nameof(ScatterBubbleDirectRecord.X), ValuePath = nameof(ScatterBubbleDirectRecord.Y), IdPath = nameof(ScatterBubbleDirectRecord.Id), SizePath = nameof(ScatterBubbleDirectRecord.Size), ColorPath = nameof(ScatterBubbleDirectRecord.Color), SizeMinimum = 0, SizeMaximum = 100, MinimumSize = 16, MaximumSize = 80, MarkerStrokeThickness = 0 };
        overlap.Series.Add(overlapSeries);
        var direct = Chart("DirectColorScatterChart", "直接颜色 · 十种标记底座");
        var directSeries = new ScatterSeries { Title = "业务颜色", ItemsSource = new[] { new ScatterBubbleDirectRecord("ok", 2, 70, 20, "#19A798"), new ScatterBubbleDirectRecord("warn", 5, 40, 50, "#F2A33A"), new ScatterBubbleDirectRecord("error", 8, 65, 90, "#D7256A") },
            CategoryPath = nameof(ScatterBubbleDirectRecord.X), ValuePath = nameof(ScatterBubbleDirectRecord.Y), IdPath = nameof(ScatterBubbleDirectRecord.Id), SizePath = nameof(ScatterBubbleDirectRecord.Size), ColorPath = nameof(ScatterBubbleDirectRecord.Color), SizeMinimum = 0, SizeMaximum = 100, MinimumSize = 10, MaximumSize = 42, MarkerShape = ChartMarkerShape.Diamond, MarkerStrokeThickness = 0 };
        direct.Series.Add(directSeries);
        var charts = new[] { scatter, area, diameter, line, overlap, direct };
        var scale = new ComboBox { Name = "ScatterBubbleSizeScale", ItemsSource = Enum.GetValues<ChartSizeScale>(), SelectedItem = ChartSizeScale.Area, MinWidth = 120, Margin = new Thickness(0, 0, 12, 6) };
        scale.SelectionChanged += (_, _) => { if (scale.SelectedItem is ChartSizeScale value) { scatterSeries.SizeScale = areaSeries.SizeScale = value; area.Title = "XY 气泡 · " + (value == ChartSizeScale.Area ? "面积尺度" : "直径尺度"); } };
        var order = new ComboBox { Name = "ScatterBubbleDrawOrder", ItemsSource = Enum.GetValues<ChartPointDrawOrder>(), SelectedItem = ChartPointDrawOrder.Source, MinWidth = 160, Margin = new Thickness(0, 0, 12, 6) };
        order.SelectionChanged += (_, _) => { if (order.SelectedItem is ChartPointDrawOrder value) overlapSeries.DrawOrder = value; };
        var labels = new CheckBox { Name = "ScatterBubbleLabels", Content = "显示业务大小标签", Margin = new Thickness(0, 0, 12, 6) };
        labels.IsCheckedChanged += (_, _) => { foreach (var chart in charts) foreach (var series in chart.Series) series.ShowDataLabels = labels.IsChecked == true; };
        var showLine = new CheckBox { Name = "ScatterBubbleShowLine", Content = "显示源序连线", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        showLine.IsCheckedChanged += (_, _) => lineSeries.ShowLine = showLine.IsChecked == true;
        foreach (var chart in charts) foreach (var series in chart.Series) series.DataLabelFormatter = context => context.Data.SizeValue is { } size ? size.ToString("0.##") : context.Data.Point.Y.ToString("0.##");
        var options = new WrapPanel { Children = { new TextBlock { Text = "散点 / 左气泡尺度", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) }, scale, new TextBlock { Text = "重叠绘制顺序", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) }, order, labels, showLine } };
        AddAction(options, "重置六图视图", () => { foreach (var chart in charts) chart.ResetViewport(); }, "恢复散点和气泡的完整 XY 视图。");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), ColumnSpacing = 16, RowSpacing = 10 };
        for (var index = 0; index < charts.Length; index++) { grid.Children.Add(charts[index]); Grid.SetColumn(charts[index], index % 2); Grid.SetRow(charts[index], index / 2); }
        var view = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = "大小映射为 DIP 直径，可选面积或直径线性尺度；颜色可从数值渐变或业务颜色读取。气泡保持真实 XY，源顺序和大小顺序决定重叠绘制及同距命中。Scatter line 按源顺序连接非单调 X，缺失 XY 断开；大小缺失只隐藏标记。支持悬停、选择、键盘、缩放平移与导出。", TextWrapping = TextWrapping.Wrap, Opacity = .8 }, options, grid } };
        return new(view, charts, scatterSeries, areaSeries, diameterSeries, lineSeries, overlapSeries, records, scale, order, labels, showLine);

        BubbleSeries Bubble(string title, ChartSizeScale sizeScale) => new() { Title = title, ItemsSource = records, CategoryPath = nameof(ScatterBubbleRecord.X), ValuePath = nameof(ScatterBubbleRecord.Y), IdPath = nameof(ScatterBubbleRecord.Id), WeightPath = nameof(ScatterBubbleRecord.Size), ColorPath = nameof(ScatterBubbleRecord.Color), MinimumSize = 12, MaximumSize = 60, SizeMinimum = 0, SizeMaximum = 100, ColorMinimum = 0, ColorMaximum = 100, MinimumColor = Colors.Blue, MaximumColor = Colors.Red, SizeScale = sizeScale, MarkerStrokeThickness = 0 };
    }

    private sealed record ScatterBubbleRecord(string Id, double X, double Y, double Size, double Color);
    private sealed record ScatterBubbleDirectRecord(string Id, double X, double Y, double Size, string Color);
    private sealed record ScatterBubbleBoard(StackPanel View, CartesianChart[] Charts, ScatterSeries Scatter, BubbleSeries Area, BubbleSeries Diameter, ScatterLineSeries Line, BubbleSeries Overlap,
        ScatterBubbleRecord[] Records, ComboBox SizeScale, ComboBox DrawOrder, CheckBox Labels, CheckBox ShowLine) : IDisposable
    { public void Dispose() { foreach (var chart in Charts) chart.Series.Clear(); } }
}
