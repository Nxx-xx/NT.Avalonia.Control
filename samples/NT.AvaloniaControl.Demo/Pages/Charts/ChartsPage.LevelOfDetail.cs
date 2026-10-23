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
    private Expander? _levelOfDetailExamples;
    private LevelOfDetailBoard? _levelOfDetailBoard;

    private Control CreateLevelOfDetailExamples()
    {
        _levelOfDetailBoard = CreateLevelOfDetailBoard();
        return _levelOfDetailExamples = new Expander
        {
            Name = "ChartLevelOfDetailExamples", Header = "保真抽样与层级细节：峰谷、断点和原始记录", HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = _levelOfDetailBoard.View,
        };
    }

    private LevelOfDetailBoard CreateLevelOfDetailBoard()
    {
        var rows = LevelOfDetailData(100_000);
        var series = new LineSeries
        {
            Title = "原始采样信号", ItemsSource = rows, CategoryPath = nameof(LevelOfDetailRow.X), ValuePath = nameof(LevelOfDetailRow.Y), IdPath = nameof(LevelOfDetailRow.Id),
            Stroke = Blue, StrokeThickness = 3, ShowMarkers = false, MarkerSize = 0, SamplingMode = ChartSamplingMode.MinMax,
        };
        var chart = new CartesianChart
        {
            Name = "LevelOfDetailExample", Height = 390, ShowLegend = false, IsTooltipEnabled = true,
            IsZoomEnabled = true, IsPanEnabled = true, InteractionAxes = ChartInteractionAxes.X, SelectionMode = ChartSelectionMode.Multiple,
            XAxis = new NumericAxis { Minimum = 0, Maximum = rows.Length - 1, Title = "原始采样序号", LabelFormat = "0" },
            YAxis = new NumericAxis { Minimum = -110, Maximum = 110, Title = "信号值", LabelFormat = "0" },
            TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) =>
            {
                var id = new TextBlock { Name = "LevelOfDetailTooltipId" }; id.Bind(TextBlock.TextProperty, new Binding("Primary.StableId"));
                var index = new TextBlock { Name = "LevelOfDetailTooltipIndex" }; index.Bind(TextBlock.TextProperty, new Binding("Primary.Index") { StringFormat = "原始源索引 {0}" });
                var value = new TextBlock(); value.Bind(TextBlock.TextProperty, new Binding("Primary.Point.Y") { StringFormat = "原始值 {0:0.000}" });
                return new StackPanel { Spacing = 3, Children = { new TextBlock { Text = "原始采样记录", FontWeight = Avalonia.Media.FontWeight.SemiBold }, id, index, value } };
            }),
        };
        chart.Series.Add(series);
        var enabled = new CheckBox { Name = "LevelOfDetailEnabled", Content = "启用 MinMax 保真抽样", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        var pixelWidth = new Slider { Name = "LevelOfDetailPixelWidth", Minimum = 1, Maximum = 8, TickFrequency = 1, IsSnapToTickEnabled = true, Value = 1, Width = 140, Margin = new Thickness(0, 0, 12, 6) };
        var summary = new TextBlock { Name = "LevelOfDetailSummary", TextWrapping = TextWrapping.Wrap, Opacity = .8 };
        var identity = new TextBlock { Name = "LevelOfDetailIdentity", Text = "悬停或点击数据，查看原始源索引、业务 ID 和数值；Shift + 拖动可框选原始点。", TextWrapping = TextWrapping.Wrap, Height = 44 };
        void Refresh()
        {
            if (chart.Bounds.Width <= 0 || chart.Bounds.Height <= 0) return;
            var info = chart.GetSamplingInfo(series);
            var figures = info.FigureCount > 0 ? $" · 独立线段组 {info.FigureCount:N0}" : "";
            var detail = info.FallbackReason switch { "Disabled" => "抽样已关闭", "BelowPixelBudget" => "当前密度已足够", _ => "当前配置保持完整路径" };
            summary.Text = $"源记录 {info.SourceCount:N0} · 当前候选 {info.VisibleCount:N0} · 折线顶点 {info.RenderedCount:N0}{figures} · 每桶 {info.PixelWidth} 物理像素 · " +
                (info.IsApplied ? "当前视图已抽样，放大后逐步恢复原始细节" : $"当前使用原始细节（{detail}）");
        }
        enabled.IsCheckedChanged += (_, _) => { series.SamplingMode = enabled.IsChecked == true ? ChartSamplingMode.MinMax : ChartSamplingMode.None; Refresh(); };
        pixelWidth.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { series.SamplingPixelWidth = (int)Math.Round(pixelWidth.Value); Refresh(); } };
        chart.ViewportChanged += (_, _) => Refresh(); chart.SizeChanged += (_, _) => Refresh();
        chart.PropertyChanged += (_, e) =>
        {
            if (e.Property == CartesianChart.HoveredPointProperty || e.Property == CartesianChart.SelectedPointProperty)
                if ((chart.HoveredPoint ?? chart.SelectedPoint) is { } hit) identity.Text = $"原始源索引 {hit.Index:N0} · 业务 ID {hit.StableId} · X {hit.Point.X:0} · 原始值 {hit.Point.Y:0.000}";
        };
        void Detail(int index) { chart.Viewport = new ChartViewport(index - 24, index + 24, -110, 110); Refresh(); }
        void Replace(int count) { rows = LevelOfDetailData(count); series.ItemsSource = rows; chart.XAxis.Maximum = count - 1; chart.ClearSelection(); chart.ResetViewport(); Refresh(); }
        var actions = new WrapPanel { Children = { enabled, new TextBlock { Text = "每桶物理像素", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6) }, pixelWidth } };
        AddAction(actions, "全量概览", () => { chart.ResetViewport(); Refresh(); }, "MinMax 按显示密度选择原始顶点，保留每桶峰谷和缺失数据的断点。");
        AddAction(actions, "单点峰值细节", () => Detail(rows.Length / 3 + 1), "放大到只有一个采样周期宽度的峰值，恢复该范围的原始记录。");
        AddAction(actions, "单点谷值细节", () => Detail(rows.Length * 2 / 3 + 1), "放大到单采样点谷值，比较原始相邻记录。");
        AddAction(actions, "缺失断点细节", () => Detail(rows.Length / 2), "NaN 缺失记录两边保留独立线段，不能跨越缺口连接。");
        AddAction(actions, "10 万点", () => Replace(100_000), "加载 100,000 条具有稳定业务 ID 的原始采样记录。");
        AddAction(actions, "100 万点", () => Replace(1_000_000), "加载 1,000,000 条原始记录。首次建立层级索引后，按当前视图选择绘制细节。");
        var view = new StackPanel { Spacing = 9, Children =
        {
            new TextBlock { Text = "概览保留单采样点峰谷，放大后恢复更多原始细节。缺失值始终分段；Tooltip、点选与框选直接读取原始数据，不会把抽样顶点当成新的业务记录。", TextWrapping = TextWrapping.Wrap, Opacity = .8 },
            actions, summary, identity, chart,
        } };
        return new(view, chart, series, enabled, pixelWidth, summary, identity, () => rows, Refresh, Detail, Replace);
    }

    private static LevelOfDetailRow[] LevelOfDetailData(int count)
    {
        var result = new LevelOfDetailRow[count];
        for (var i = 0; i < count; i++)
        {
            var y = Math.Sin(i * .0123) * 14 + Math.Sin(i * .173) * 3;
            if (i == count / 3 + 1) y = 95;
            if (i == count * 2 / 3 + 1) y = -95;
            if (i >= count / 2 - 2 && i <= count / 2 + 2) y = double.NaN;
            result[i] = new(i, y);
        }
        return result;
    }

    private sealed record LevelOfDetailRow(int Index, double Y)
    {
        public double X => Index;
        public string Id => "sample-" + Index;
    }
    private sealed record LevelOfDetailBoard(StackPanel View, CartesianChart Chart, LineSeries Series, CheckBox Enabled, Slider PixelWidth,
        TextBlock Summary, TextBlock Identity, Func<LevelOfDetailRow[]> GetRows, Action Refresh, Action<int> Detail, Action<int> Replace) : IDisposable
    {
        public void Dispose() => Chart.Series.Clear();
    }
}
