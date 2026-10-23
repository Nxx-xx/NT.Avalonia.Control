using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Expander? _analysisExamples;
    private AnalysisBoard? _analysisBoard;
    private Control CreateAnalysisExamples()
    {
        _analysisBoard = CreateAnalysisBoard();
        return _analysisExamples = new Expander { Name = "ChartAnalysisExamples", Header = "基础分析图：漏斗、金字塔、热力矩阵、象形柱与主题河流", HorizontalAlignment = HorizontalAlignment.Stretch, Content = _analysisBoard.View };
    }
    private AnalysisBoard CreateAnalysisBoard()
    {
        var rows = new ObservableCollection<AnalysisDemoRow> { new("stage-a", "触达", 120), new("stage-b", "访问", 85), new("stage-c", "意向", 50), new("stage-d", "成交", 25) };
        var funnel = new FunnelChart { Name = "FunnelExample", ItemsSource = rows, CategoryPath = "Category", ValuePath = "Value", IdPath = "Id", Height = 250, Padding = new Thickness(10), NeckWidth = .2 };
        var pyramid = new PyramidChart { Name = "PyramidExample", ItemsSource = rows, CategoryPath = "Category", ValuePath = "Value", IdPath = "Id", Height = 250, Padding = new Thickness(10) };
        var cells = new ObservableCollection<AnalysisDemoCell> { new("heat-a", 1, "周一", 20), new("heat-b", 1, "周二", 55), new("heat-c", 1, "周三", double.NaN), new("heat-d", "1", "周一", 80), new("heat-e", "1", "周二", 40) };
        var heatmap = new HeatmapChart { Name = "HeatmapExample", ItemsSource = cells, RowPath = "Row", ColumnPath = "Column", ValuePath = "Value", IdPath = "Id", Height = 250, Padding = new Thickness(10), Minimum = 0, Maximum = 100, LowColor = Color.Parse("#DDEBFF"), HighColor = Color.Parse("#2455B5"), MissingBrush = Brush("#559AA7B8") };
        var symbols = new ObservableCollection<AnalysisDemoRow> { new("symbol-a", "线上", 75), new("symbol-b", "门店", -55), new("symbol-c", "订阅", 35) };
        var pictorial = Enum.GetValues<PictorialBarRenderMode>().Select(mode => new PictorialBarSeries { Title = "净变化", ItemsSource = symbols, CategoryPath = "Category", ValuePath = "Value", IdPath = "Id", RenderMode = mode, ShowDataLabels = true, Geometry = AnalysisRingGeometry(), Fill = mode == PictorialBarRenderMode.Clip ? Teal : mode == PictorialBarRenderMode.Repeat ? Orange : Blue, StrokeThickness = 0, SymbolSize = mode == PictorialBarRenderMode.Clip ? 100 : 20, SymbolSpacing = 5, BarWidth = 48 }).ToArray();
        var charts = pictorial.Select((series, index) =>
        {
            var chart = new CartesianChart { Name = "Pictorial" + series.RenderMode + "Example", Height = 250, ShowLegend = false, IsZoomEnabled = true, IsPanEnabled = true, SelectionMode = ChartSelectionMode.Single,
                XAxis = new CategoryAxis { Title = "渠道", PlotMode = CategoryPlotMode.BetweenTicks }, YAxis = new NumericAxis { Minimum = -100, Maximum = 100, Title = "净变化" } };
            chart.Series.Add(series); return chart;
        }).ToArray();
        var river = new CartesianChart { Name = "ThemeRiverExample", Height = 250, ShowLegend = true, IsLegendInteractive = false, IsZoomEnabled = true, IsPanEnabled = true, IsTooltipEnabled = false, SelectionMode = ChartSelectionMode.None, SeriesSelectionMode = ChartSelectionMode.None, IsPointBrowsingEnabled = false, IsKeyboardNavigationEnabled = false,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 6, Title = "日期" }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "围绕 50 居中" } };
        var riverAreas = new[] { new AreaSeries { Title = "居中底座", Fill = Brushes.Transparent, Stroke = Brushes.Transparent, StrokeThickness = 0, ShowInLegend = false, ShowMarkers = false }, new AreaSeries { Title = "搜索", Fill = Blue, Stroke = Blue, StrokeThickness = 0, ShowMarkers = false }, new AreaSeries { Title = "推荐", Fill = Teal, Stroke = Teal, StrokeThickness = 0, ShowMarkers = false } };
        foreach (var series in riverAreas) { series.StackMode = ChartStackMode.Stacked; series.StackGroup = "analysis-river"; river.Series.Add(series); }
        SetAnalysisRiver(riverAreas, [15, 24, 30, 18, 12, 25, 15], [20, 16, 22, 35, 26, 15, 20]);
        var labels = new CheckBox { Name = "AnalysisLabels", Content = "显示分类和值", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        labels.IsCheckedChanged += (_, _) => { foreach (var chart in new AnalysisChartBase[] { funnel, pyramid, heatmap }) chart.ShowLabels = chart.ShowValues = labels.IsChecked == true; foreach (var series in pictorial) series.ShowDataLabels = labels.IsChecked == true; };
        var selection = new CheckBox { Name = "AnalysisSelection", Content = "点击选择", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        selection.IsCheckedChanged += (_, _) => { foreach (var chart in new AnalysisChartBase[] { funnel, pyramid, heatmap }) chart.IsSelectionEnabled = selection.IsChecked == true; foreach (var chart in charts) chart.SelectionMode = selection.IsChecked == true ? ChartSelectionMode.Single : ChartSelectionMode.None; };
        var gap = new Slider { Name = "AnalysisGap", Minimum = 0, Maximum = 20, Value = 2, Width = 140, Margin = new Thickness(0, 0, 12, 6) };
        gap.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { funnel.SegmentGap = pyramid.SegmentGap = gap.Value; heatmap.CellGap = gap.Value; } };
        var neck = new Slider { Name = "AnalysisNeck", Minimum = 0, Maximum = 1, Value = .2, Width = 120, Margin = new Thickness(0, 0, 12, 6) };
        neck.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) funnel.NeckWidth = neck.Value; };
        var status = new TextBlock { Name = "AnalysisSelectionStatus", Text = "点击漏斗、金字塔或矩阵，使用方向键浏览、Enter 选择、Esc 清空。", TextWrapping = TextWrapping.Wrap, Opacity = .8 };
        foreach (var chart in new AnalysisChartBase[] { funnel, pyramid, heatmap }) chart.PropertyChanged += (_, e) =>
        { if (e.Property == AnalysisChartBase.SelectedPointProperty) status.Text = chart.SelectedPoint is { } hit ? $"{chart.Name} · {hit.StableId} · 原始值 {hit.Point.Y:0.#}" : "选择已清空"; };
        var revision = 0;
        void Update() { revision++; rows[1].Value = revision % 2 == 0 ? 85 : 105; cells[1].Value = revision % 2 == 0 ? 55 : 95; symbols[1].Value = revision % 2 == 0 ? -55 : -25; SetAnalysisRiver(riverAreas, revision % 2 == 0 ? [15, 24, 30, 18, 12, 25, 15] : [25, 34, 20, 28, 22, 15, 25], [20, 16, 22, 35, 26, 15, 20]); }
        var options = new WrapPanel { Children = { labels, selection, new TextBlock { Text = "分段间距", VerticalAlignment = VerticalAlignment.Center }, gap, new TextBlock { Text = "漏斗颈宽", VerticalAlignment = VerticalAlignment.Center }, neck } };
        AddAction(options, "更新业务数据", Update, "更新共享业务项和矩阵，并重新计算主题河流的透明居中底座。");
        AddAction(options, "清除选择", () => { funnel.ResetViewport(); pyramid.ResetViewport(); heatmap.ResetViewport(); foreach (var chart in charts) chart.ClearSelection(); }, "保留原始业务数据，清空当前选择。");
        Control Card(string title, string text, Control chart) => new Border { BorderThickness = new Thickness(1), BorderBrush = Brush("#80808055"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = new StackPanel { Spacing = 5, Children = { new TextBlock { Text = title, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = .75, FontSize = 12 }, chart } } };
        Control[] cards = [Card("漏斗", "源顺序、等高阶段、数值宽度与可调颈宽", funnel), Card("金字塔", "无间距时，各层面积与正值占比一致", pyramid), Card("Heatmap", "整数 1 与字符串 1 是独立行；灰色为显式缺失，留白为无数据组合", heatmap), Card("Pictorial · Stretch", "将带孔轮廓拉伸到正负数值范围", charts[0]), Card("Pictorial · Clip", "固定尺寸轮廓从基线裁剪，保留孔洞", charts[1]), Card("Pictorial · Repeat", "按固定 DIP 尺寸重复，裁剪末个图形", charts[2]), Card("Theme river 堆叠底座", "透明基座 + 两条堆叠面积；同一槽位围绕 50 居中", river)];
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), ColumnSpacing = 12, RowSpacing = 12 };
        void Layout(int columns) { grid.ColumnDefinitions = new ColumnDefinitions(columns == 2 ? "*,*" : "*"); grid.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", (cards.Length + columns - 1) / columns))); for (var i = 0; i < cards.Length; i++) { Grid.SetColumn(cards[i], i % columns); Grid.SetRow(cards[i], i / columns); } }
        foreach (var card in cards) grid.Children.Add(card); Layout(2); grid.SizeChanged += (_, _) => { var columns = grid.Bounds.Width >= 700 ? 2 : 1; if (grid.ColumnDefinitions.Count != columns) Layout(columns); };
        return new(new StackPanel { Spacing = 10, Children = { new TextBlock { Text = "基础分析图保留源项与业务 ID。漏斗与金字塔忽略非正值；热力矩阵使用离散行列和共享线性色域；象形柱支持正负值与孔洞。主题河流示例使用居中堆叠底座。", TextWrapping = TextWrapping.Wrap, Opacity = .8 }, options, status, grid } }, [funnel, pyramid, heatmap], [.. charts, river], rows, cells, symbols, pictorial, riverAreas, labels, selection, gap, neck, Update);
    }
    private static Geometry AnalysisRingGeometry() => new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(new Rect(0, 0, 1, 1)), new EllipseGeometry(new Rect(.3, .3, .4, .4)));
    private static void SetAnalysisRiver(AreaSeries[] series, double[] first, double[] second)
    {
        series[0].ItemsSource = ThemeRiverStack.CreateBaseline(new IReadOnlyList<double>[] { first, second }, 50);
        series[1].ItemsSource = first.Select((value, index) => new ChartPoint(index, value)).ToArray(); series[2].ItemsSource = second.Select((value, index) => new ChartPoint(index, value)).ToArray();
    }
    private sealed class AnalysisDemoRow(string id, object category, double value) : INotifyPropertyChanged
    {
        private double _value = value; public string Id { get; } = id; public object Category { get; } = category;
        public double Value { get => _value; set { if (_value.Equals(value)) return; _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class AnalysisDemoCell(string id, object row, object column, double value) : INotifyPropertyChanged
    {
        private double _value = value; public string Id { get; } = id; public object Row { get; } = row; public object Column { get; } = column;
        public double Value { get => _value; set { if (_value.Equals(value)) return; _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed record AnalysisBoard(StackPanel View, AnalysisChartBase[] Analysis, CartesianChart[] Cartesian, ObservableCollection<AnalysisDemoRow> Rows, ObservableCollection<AnalysisDemoCell> Cells, ObservableCollection<AnalysisDemoRow> Symbols, PictorialBarSeries[] Pictorial, AreaSeries[] River, CheckBox Labels, CheckBox Selection, Slider Gap, Slider Neck, Action UpdateData) : IDisposable
    { public void Dispose() { foreach (var chart in Analysis) chart.ItemsSource = null; foreach (var chart in Cartesian) chart.Series.Clear(); } }
}
