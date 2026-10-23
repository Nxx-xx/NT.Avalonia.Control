using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private EfficientHitBoard? _efficientHitBoard;
    private Control CreateEfficientHitExamples() => new Expander
    {
        Name = "ChartEfficientHitExamples", Header = "高效命中：重叠、相同 X、多轴与抽样原始记录", HorizontalAlignment = HorizontalAlignment.Stretch,
        Content = (_efficientHitBoard = CreateEfficientHitBoard()).View,
    };

    private EfficientHitBoard CreateEfficientHitBoard()
    {
        CartesianChart Chart(string name, double xmax, double ymax) => new()
        {
            Name = name, Height = 265, ShowLegend = true, SelectionMode = ChartSelectionMode.Multiple,
            IsZoomEnabled = true, IsPanEnabled = true, XAxis = new NumericAxis { Minimum = 0, Maximum = xmax },
            YAxis = new NumericAxis { Minimum = 0, Maximum = ymax },
            TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) =>
            {
                var id = new TextBlock { Name = "EfficientHitTooltipId" }; id.Bind(TextBlock.TextProperty, new Binding("Primary.StableId"));
                var value = new TextBlock(); value.Bind(TextBlock.TextProperty, new Binding("Primary.Point.Y") { StringFormat = "原始值 {0:0.000}" });
                return new StackPanel { Children = { new TextBlock { Text = "原始业务记录" }, id, value } };
            }),
        };
        var firstRows = new EfficientHitRow[] { new("back-0", 2, 2), new("back-1", 5, 3), new("back-2", 5, 7), new("back-3", 8, 8) };
        var lastRows = new EfficientHitRow[] { new("front-0", 2, 2), new("front-1", 2, 2), new("front-2", 5, 3), new("front-3", 5, 7) };
        var axisRows = new EfficientHitRow[] { new("right-axis-0", 8, 800), new("right-axis-1", 6, 500) };
        ScatterSeries Scatter(string title, EfficientHitRow[] rows, IBrush fill) => new()
        { Title = title, ItemsSource = rows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", Fill = fill, Stroke = fill, MarkerSize = 13 };
        var points = Chart("EfficientHitPoints", 10, 10);
        var first = Scatter("底层", firstRows, Blue); var last = Scatter("重叠与相同 X", lastRows, Teal);
        var axis = Scatter("右轴原始值", axisRows, Orange); axis.YAxisKey = "hit-right";
        points.Axes.Add(new NumericAxis { Key = "hit-right", Minimum = 0, Maximum = 1000, Position = ChartAxisPosition.Far, Title = "右轴 / 原始值", ShowGridLines = false });
        points.Series.Add(first); points.Series.Add(last); points.Series.Add(axis);
        var rows = EfficientHitData(20_000);
        var dense = Chart("EfficientHitSampled", rows.Length - 1, 100); dense.ShowLegend = false;
        var line = new LineSeries { Title = "原始稠密记录", ItemsSource = rows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", Stroke = Blue, StrokeThickness = 3, ShowMarkers = false, MarkerSize = 0, SamplingMode = ChartSamplingMode.MinMax, SamplingPixelWidth = 8 };
        dense.Series.Add(line);
        var bars = Chart("EfficientHitBars", 8, 100); bars.Height = 230;
        var barRows = Enumerable.Range(0, 8).Select(i => new EfficientHitRow("bar-a-" + i, i + .5, 20 + i * 7)).ToArray();
        var otherBarRows = Enumerable.Range(0, 8).Select(i => new EfficientHitRow("bar-b-" + i, i + .5, 70 - i * 4)).ToArray();
        var bar = new BarSeries { Title = "甲组", ItemsSource = barRows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", Fill = Blue, Stroke = Blue };
        var otherBar = new BarSeries { Title = "乙组", ItemsSource = otherBarRows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", Fill = Teal, Stroke = Teal };
        bars.Series.Add(bar); bars.Series.Add(otherBar);
        var identity = new TextBlock { Name = "EfficientHitIdentity", Height = 44, TextWrapping = TextWrapping.Wrap, Text = "悬停/点击查看原始索引和 ID。重叠点按绘制顺序确定；相同 X 仍按实际屏幕距离命中。" };
        var summary = new TextBlock { Name = "EfficientHitSummary", TextWrapping = TextWrapping.Wrap, Opacity = .8 };
        void Refresh()
        {
            if (dense.Bounds.Width <= 0) return;
            var info = dense.GetSamplingInfo(line);
            summary.Text = $"稠密源 {info.SourceCount:N0} 条，当前绘制 {info.RenderedCount:N0} 顶点。抽样省略的原始记录仍可命中、框选和插值；结果不创建新的业务记录。";
        }
        foreach (var chart in new[] { points, dense, bars }) chart.PropertyChanged += (_, e) =>
        {
            if ((e.Property == CartesianChart.HoveredPointProperty || e.Property == CartesianChart.SelectedPointProperty) && (chart.HoveredPoint ?? chart.SelectedPoint) is { } hit)
                identity.Text = $"{hit.Series.Title} · 原始源索引 {hit.Index:N0} · ID {hit.StableId} · 原始值 ({hit.Point.X:0.###}, {hit.Point.Y:0.###})";
        };
        dense.SizeChanged += (_, _) => Refresh(); dense.ViewportChanged += (_, _) => Refresh();
        var actions = new WrapPanel();
        AddAction(actions, "稠密概览", () => dense.ResetViewport(), "保持抽样显示，查询完整原始记录。");
        AddAction(actions, "放大原始段", () => dense.Viewport = new ChartViewport(12320, 12370, 60, 63), "线段插值返回两个相邻原始端点。");
        AddAction(actions, "替换同坐标记录", () => { rows = EfficientHitData(20_000, "replacement-"); line.ItemsSource = rows; dense.ClearSelection(); Refresh(); }, "坐标相同，原始 Item 与业务 ID 随新数据源更新。");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 16 };
        grid.Children.Add(new StackPanel { Children = { new TextBlock { Text = "重叠点 / 相同 X / 独立右轴", FontWeight = FontWeight.SemiBold }, points } });
        var densePanel = new StackPanel { Children = { new TextBlock { Text = "抽样显示 / 完整原始点与线段", FontWeight = FontWeight.SemiBold }, dense } }; Grid.SetColumn(densePanel, 1); grid.Children.Add(densePanel);
        return new(new StackPanel { Spacing = 8, Children = { actions, summary, identity, grid, new TextBlock { Text = "共享分组柱布局：命中实际柱体并返回各自原始记录", FontWeight = FontWeight.SemiBold }, bars } }, points, dense, bars, first, last, axis, line, bar, otherBar, firstRows, lastRows, axisRows, barRows, otherBarRows, () => rows, Refresh);
    }

    private static EfficientHitRow[] EfficientHitData(int count, string prefix = "original-") => Enumerable.Range(0, count).Select(i => new EfficientHitRow(prefix + i, i, i * .005)).ToArray();
    private sealed record EfficientHitRow(string Id, double X, double Y);
    private sealed record EfficientHitBoard(StackPanel View, CartesianChart Points, CartesianChart Dense, CartesianChart Bars, ScatterSeries First, ScatterSeries Last, ScatterSeries AxisSeries, LineSeries Line, BarSeries Bar, BarSeries OtherBar,
        EfficientHitRow[] FirstRows, EfficientHitRow[] LastRows, EfficientHitRow[] AxisRows, EfficientHitRow[] BarRows, EfficientHitRow[] OtherBarRows, Func<EfficientHitRow[]> GetRows, Action Refresh) : IDisposable
    {
        public void Dispose() { Points.Series.Clear(); Dense.Series.Clear(); Bars.Series.Clear(); }
    }
}
