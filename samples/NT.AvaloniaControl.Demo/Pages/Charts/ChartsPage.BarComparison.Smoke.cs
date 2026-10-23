using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _barComparisonEvidence = [];

    private async Task RunBarComparisonSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _barComparisonExamples!.IsExpanded = true;
        await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
            string Image(string scenario) => Path.Combine(directory, stem + $"-bar-comparison-{(dark ? "dark" : "light")}-{scenario}.png");
            void Check(string name, bool value) => checks[$"柱条比较{label}{name}"] = value;
            using var board = CreateBarComparisonBoard(false);
            var window = new Window { Title = "Charts · 柱条与比较", Width = 1160, Height = 1140, RequestedThemeVariant = theme,
                Background = Brush(dark ? "#111722" : "#F8FAFC"), Foreground = Brush(dark ? "#EAF0FB" : "#172238"), Content = new Border { Padding = new Thickness(16), Child = board.View } };
            CartesianChart? chart = null;
            var probes = new List<object>(); var exports = new List<object>();
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                var native = window.TryGetPlatformHandle() is { Handle: not 0 };
                var boardLayouts = board.Charts.Select(value => new { value.Name, value.Bounds, value.PlotArea }).ToArray();
                Check("六图在原生窗口实际布局", native && board.Charts.All(value => value.Bounds.Width > 400 && value.PlotArea.Height > 100));
                Check("六类新系列和业务身份展示", board.Charts.SelectMany(value => value.Series).Any(value => value is DotSeries) && board.Charts.SelectMany(value => value.Series).Any(value => value is LollipopSeries) && board.Charts.SelectMany(value => value.Series).Any(value => value is VarianceSeries) && ReferenceEquals(board.First.CreateSnapshot().Points[0].Item, board.Records[0]));
                var pageImage = Image("page"); SaveWindow(pageImage);
                board.Orientation.SelectedItem = Orientation.Horizontal; board.StackMode.SelectedItem = ChartStackMode.Percent; board.FixedWidth.IsChecked = true; board.Sort.SelectedItem = ChartSeriesSortMode.ValueDescending; board.Labels.IsChecked = true;
                FlushControlTemplateWindow(window);
                Check("用户方向宽度排序标签选择同步系列", board.First.Orientation == Orientation.Horizontal && board.Second.Orientation == Orientation.Horizontal && board.First.BarWidth == 12 && board.Second.BarWidth == 12 && board.First.SortMode == ChartSeriesSortMode.ValueDescending && board.Charts.SelectMany(value => value.Series).All(value => value.ShowDataLabels));
                Check("用户百分比切换保持正负一百", board.StackA.StackMode == ChartStackMode.Percent && board.StackB.StackMode == ChartStackMode.Percent && board.Charts[1].VisibleRange is { YMinimum: -100, YMaximum: 100 });
                Check("横向分类轴和原始业务映射保持", board.Charts[0].XAxis is NumericAxis && board.Charts[0].YAxis is CategoryAxis && board.First.CreateSnapshot().Points[0] is { Point.X: 1, Point.Y: 40, SourceIndex: 0 } raw && ReferenceEquals(raw.Item, board.Records[0]) && Equals(raw.StableId, "bar-a"));
                var changedPageImage = Image("page-horizontal-percent"); SaveWindow(changedPageImage);

                chart = new CartesianChart { Background = window.Background, Foreground = window.Foreground, Margin = new Thickness(12), ShowLegend = false, ShowGridLines = false,
                    IsZoomEnabled = true, IsPanEnabled = true, SelectionMode = ChartSelectionMode.Multiple };
                window.Content = chart; Configure(false); FlushControlTemplateWindow(window);
                var records = new[] { new BarComparisonRecord("native-bar-a", 1, 40), new BarComparisonRecord("native-bar-b", 3, -40), new BarComparisonRecord("native-bar-c", 5, 60) };
                var bar = new BarSeries { Fill = Blue, Stroke = Blue, StrokeThickness = 0, BarWidth = 40, ItemsSource = records, CategoryPath = nameof(BarComparisonRecord.Category), ValuePath = nameof(BarComparisonRecord.Value), IdPath = nameof(BarComparisonRecord.Id) };
                chart.Series.Add(bar); FlushControlTemplateWindow(window);
                var root = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(chart)!;
                foreach (var horizontal in new[] { false, true })
                {
                    Configure(horizontal); bar.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical; FlushControlTemplateWindow(window);
                    var scenario = horizontal ? "horizontal-bar" : "vertical-bar";
                    Probe(scenario, horizontal, horizontal ? new(20, 1) : new(1, 20), horizontal ? new(-20, 1) : new(1, -20), Color.Parse("#4385F4"));
                    var hit = root.GetPointPeer(bar, 0)?.CurrentPoint;
                    Check($"{scenario}显示值与原身份分离", hit is not null && hit.Point == (horizontal ? new ChartPoint(40, 1) : new ChartPoint(1, 40)) && hit.RawPoint == new ChartPoint(1, 40) && hit.Index == 0 && Equals(hit.StableId, "native-bar-a") && ReferenceEquals(hit.Item, records[0]));
                    if (hit is not null) chart.SelectedPoints = new[] { hit }; FlushControlTemplateWindow(window);
                    Check($"{scenario}选择保留原始数据", chart.SelectedPoint is { Index: 0 } selected && selected.RawPoint == new ChartPoint(1, 40) && ReferenceEquals(selected.Item, records[0]));
                    chart.ClearSelection();
                }
                Configure(false); bar.Orientation = Orientation.Vertical; bar.BarWidth = 12; FlushControlTemplateWindow(window);
                Probe("fixed-width", false, new(1, 20), new(1.15, 20), Color.Parse("#4385F4"));
                var groupedSecond = new BarSeries { Fill = Teal, Stroke = Teal, StrokeThickness = 0, BarWidth = 12, ItemsSource = new ChartPoint[] { new(1, 60) } };
                chart.Series.Add(groupedSecond); FlushControlTemplateWindow(window);
                Check("固定宽度分组独立数学前提", chart.PlotArea.Width >= 500);
                // Fixed 12-DIP bars use the 120-DIP group cap with two lanes:
                // category centers +/-30 DIP, leaving a 48-DIP middle gap.
                var laneOffset = 30d / chart.PlotArea.Width * 6;
                Probe("grouped-fixed-blue", false, new(1 - laneOffset, 20), new(1, 20), Color.Parse("#4385F4"));
                Probe("grouped-fixed-teal", false, new(1 + laneOffset, 20), new(1, 20), Color.Parse("#19A798"));
                chart.Series.Clear();
                var first = new BarSeries { Title = "first", StackMode = ChartStackMode.Stacked, StackGroup = "native-bars", Fill = Blue, Stroke = Blue, StrokeThickness = 0, ItemsSource = new ChartPoint[] { new(1, 20), new(3, -20), new(5, 30) } };
                var second = new BarSeries { Title = "second", StackMode = ChartStackMode.Stacked, StackGroup = "native-bars", Fill = Teal, Stroke = Teal, StrokeThickness = 0, ItemsSource = new ChartPoint[] { new(1, 30), new(3, -30), new(5, 70) } };
                chart.Series.Add(first); chart.Series.Add(second);
                foreach (var horizontal in new[] { false, true })
                {
                    Configure(horizontal); first.Orientation = second.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
                    foreach (var mode in new[] { ChartStackMode.Stacked, ChartStackMode.Percent })
                    {
                        first.StackMode = second.StackMode = mode; FlushControlTemplateWindow(window);
                        var percent = mode == ChartStackMode.Percent;
                        foreach (var sign in new[] { 1, -1 })
                        {
                            var category = sign == 1 ? 1 : 3; var end = sign * (percent ? 100 : 50); var baseline = sign * (percent ? 40 : 20); var contribution = sign * (percent ? 60 : 30);
                            var inside = sign * (percent ? 70 : 35); var outside = sign * (percent ? 110 : 65);
                            var scenario = $"{(horizontal ? "horizontal-" : "")}{(percent ? "percent" : "stacked")}-{(sign == 1 ? "positive" : "negative")}";
                            Probe(scenario, horizontal, horizontal ? new(inside, category) : new(category, inside), horizontal ? new(outside, category) : new(category, outside), Color.Parse("#19A798"));
                            var point = root.GetPointPeer(second, sign == 1 ? 0 : 1)?.CurrentPoint;
                            Check($"{scenario}端点基线与贡献正确", point is not null && point.Point == (horizontal ? new ChartPoint(end, category) : new ChartPoint(category, end)) && point.RawPoint == new ChartPoint(category, sign * 30) && Near(point.BaseValue, baseline) && Near(point.StackValue, contribution));
                        }
                    }
                }
                Configure(false); first.Orientation = second.Orientation = Orientation.Vertical; FlushControlTemplateWindow(window);
                var exportSource = Image("stack-percent-export-source"); var sourceRaster = CaptureControlTemplateRaster(chart, exportSource);
                foreach (var dpi in new[] { 96d, 144d, 192d })
                {
                    using var stream = new MemoryStream();
                    var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(sourceRaster.Width, sourceRaster.Height), Dpi = dpi, Background = chart.Background });
                    var exportImage = Image($"stack-percent-export-{dpi:0}"); File.WriteAllBytes(exportImage, stream.ToArray()); stream.Position = 0;
                    using var bitmap = new Bitmap(stream); var raster = ReadShapePixels(bitmap);
                    var changed = CountControlTemplateExactChanges(sourceRaster, raster, chart.PlotArea);
                    var density = TryReadCommonPngDensity(stream.ToArray(), out var dx, out var dy, out var unit) && dx == (uint)Math.Round(dpi / .0254) && dx == dy && unit == 1;
                    Check($"{dpi:0}DPI导出保持像素尺寸及密度", exported && changed == 0 && density && raster.Width == sourceRaster.Width && raster.Height == sourceRaster.Height);
                    exports.Add(new { dpi, image = exportImage, originalImage = exportSource, pixelSize = new { width = raster.Width, height = raster.Height }, plotArea = chart.PlotArea, visibleRange = chart.VisibleRange, changedPixels = changed, densityMatches = density, exported });
                }
                chart.Series.Clear();
                var rangeRecord = new BarComparisonRange("native-range", 2, 20, 80);
                var range = new RangeBarSeries { ItemsSource = new[] { rangeRecord }, CategoryPath = nameof(BarComparisonRange.Category), LowPath = nameof(BarComparisonRange.Low), HighPath = nameof(BarComparisonRange.High), IdPath = nameof(BarComparisonRange.Id), Fill = Teal, Stroke = Teal, StrokeThickness = 0 };
                chart.Series.Add(range); FlushControlTemplateWindow(window); Probe("range", false, new(2, 50), new(2, 0), Color.Parse("#19A798"));
                var rangeHit = root.GetPointPeer(range, 0)?.CurrentPoint;
                Check("范围柱无ValuePath也保留两界及身份", range.ValuePath is null && range.CreateSnapshot().Points[0] is { High: 80, Low: 20, SourceIndex: 0 } && rangeHit is not null && ReferenceEquals(rangeHit.Item, rangeRecord) && Equals(rangeHit.StableId, "native-range"));
                chart.Series.Clear();
                var waterfall = new WaterfallSeries { ItemsSource = new[] { new BarComparisonChange("a", 1, 40, false), new BarComparisonChange("b", 2, -15, false), new BarComparisonChange("c", 3, 20, false), new BarComparisonChange("total", 4, 45, true) }, CategoryPath = nameof(BarComparisonChange.Category), ValuePath = nameof(BarComparisonChange.Value), TotalPath = nameof(BarComparisonChange.IsTotal), IdPath = nameof(BarComparisonChange.Id), PositiveFill = Teal, NegativeFill = Brush("#D7256A"), TotalFill = Blue, StrokeThickness = 0 };
                chart.Series.Add(waterfall); FlushControlTemplateWindow(window); Probe("waterfall", false, new(3, 35), new(3, 10), Color.Parse("#19A798"));
                var waterfallHit = root.GetPointPeer(waterfall, 2)?.CurrentPoint; var totalHit = root.GetPointPeer(waterfall, 3)?.CurrentPoint;
                Check("瀑布累计端点和原贡献正确", waterfallHit is { Index: 2, Point.Y: 45 } && waterfallHit.RawPoint == new ChartPoint(3, 20) && Near(waterfallHit.BaseValue, 25));
                Check("瀑布总计从基线绘制并保留原项", totalHit is { Index: 3, Point.Y: 45 } && Near(totalHit.BaseValue, 0) && Equals(totalHit.StableId, "total"));
                chart.Series.Clear();
                var dot = new DotSeries { ItemsSource = new ChartPoint[] { new(2, 60) }, MarkerSize = 12, Fill = Blue, Stroke = Blue };
                chart.Series.Add(dot); FlushControlTemplateWindow(window); Probe("dot", false, new(2, 60), new(2, 30), Color.Parse("#4385F4"));
                chart.Series.Clear();
                var lollipop = new LollipopSeries { ItemsSource = new ChartPoint[] { new(2, 60) }, MarkerSize = 12, StrokeThickness = 3, Fill = Teal, Stroke = Teal };
                chart.Series.Add(lollipop); FlushControlTemplateWindow(window); Probe("lollipop", false, new(2, 30), new(2, -30), Color.Parse("#19A798"), true);
                chart.Series.Clear();
                var variance = new VarianceSeries { ItemsSource = new[] { rangeRecord }, CategoryPath = nameof(BarComparisonRange.Category), LowPath = nameof(BarComparisonRange.Low), HighPath = nameof(BarComparisonRange.High), IdPath = nameof(BarComparisonRange.Id), MarkerSize = 12, StrokeThickness = 3, Stroke = Orange, Fill = Orange };
                chart.Series.Add(variance); FlushControlTemplateWindow(window); Probe("variance", false, new(2, 50), new(2, 0), Color.Parse("#F2A33A"), true);
                var varianceHit = root.GetPointPeer(variance, 0)?.CurrentPoint;
                Check("Variance保留有向两界和源身份", variance.CreateSnapshot().Points[0] is { Low: 20, High: 80 } && varianceHit is not null && ReferenceEquals(varianceHit.Item, rangeRecord));
                chart.Series.Clear();
                chart.XAxis = new CategoryAxis { Minimum = -.5, Maximum = 2.5 }; chart.YAxis = new NumericAxis { Minimum = 0, Maximum = 60 };
                var pareto = new ParetoSeries { ItemsSource = new ChartPoint[] { new(1, 30), new(2, 50), new(3, 20) }, Fill = Blue, Stroke = Blue, CumulativeStroke = Orange, CumulativeStrokeThickness = 3 };
                chart.Series.Add(pareto); FlushControlTemplateWindow(window);
                var paretoImage = Image("pareto"); var paretoRaster = CaptureControlTemplateRaster(chart, paretoImage);
                var paretoFirst = root.GetPointPeer(pareto, 1)?.CurrentPoint; var paretoLast = root.GetPointPeer(pareto, 2)?.CurrentPoint;
                Check("Pareto降序槽保留原始索引", paretoFirst is { Index: 1, Point.X: 0, Point.Y: 50 } && paretoFirst.RawPoint == new ChartPoint(2, 50) && paretoLast is { Index: 2, Point.X: 2, Point.Y: 20 });
                var cumulative = new Point(chart.PlotArea.Left + chart.PlotArea.Width / 3, chart.PlotArea.Bottom - chart.PlotArea.Height * .65);
                var rejectedCumulative = new Point(cumulative.X, chart.PlotArea.Bottom - chart.PlotArea.Height * .2);
                var cumulativeInk = PixelInk(paretoRaster, cumulative, Color.Parse("#F2A33A")); var cumulativeEmpty = PixelInk(paretoRaster, rejectedCumulative, Color.Parse("#F2A33A"));
                Check("Pareto累计线使用独立百分比轴", cumulativeInk >= 4 && cumulativeEmpty == 0);
                probes.Add(new { scenario = "pareto-cumulative", image = paretoImage, plotArea = chart.PlotArea, visibleRange = chart.VisibleRange, orientation = "Vertical", color = "#F2A33A", positive = new { x = .5, y = 65 }, negative = new { x = .5, y = 20 }, percentAxisMinimum = 0, percentAxisMaximum = 100, positivePixel = cumulative, negativePixel = rejectedCumulative, positiveInkPixels = cumulativeInk, negativeInkPixels = cumulativeEmpty, minimumInk = 4 });
                var full = chart.VisibleRange; chart.Viewport = new ChartViewport(-.25, 1.5, 0, 55); FlushControlTemplateWindow(window);
                Check("比较系列可缩放并保留原身份", chart.VisibleRange != full && root.GetPointPeer(pareto, 1)?.CurrentPoint is { Index: 1, Point.Y: 50 } zoom && zoom.RawPoint == new ChartPoint(2, 50));
                chart.ResetViewport(); FlushControlTemplateWindow(window);
                _barComparisonEvidence.Add(new { theme = label, nativeWindowCreated = native, pageImage, changedPageImage, boardLayouts, probes, exports, pixelSquareDip = 6, colorChannelTolerance = 24, derivedMetadataAbsoluteTolerance = 1e-9,
                    rawPointAndIdentityExact = true, physicalInputValidated = false, performanceMeasured = false, evidenceKind = "native-window-rendertargetbitmap-preregistered-math-coordinate-positive-negative-probes-and-public-hit-export-api" });
            }
            finally { window.Close(); chart?.Series.Clear(); }

            void SaveWindow(string image)
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)); bitmap.Render(window); bitmap.Save(image);
            }
            void Configure(bool horizontal)
            {
                chart!.XAxis = new NumericAxis { Minimum = horizontal ? -120 : 0, Maximum = horizontal ? 120 : 6 };
                chart.YAxis = new NumericAxis { Minimum = horizontal ? 0 : -120, Maximum = horizontal ? 6 : 120 };
                chart.ResetViewport();
            }
            Point Project(ChartPoint point)
            {
                var range = chart!.VisibleRange!.Value; var plot = chart.PlotArea;
                return new(plot.Left + (point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width, plot.Bottom - (point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
            }
            void Probe(string scenario, bool horizontal, ChartPoint positive, ChartPoint negative, Color color, bool stroke = false)
            {
                var image = Image(scenario); var raster = CaptureControlTemplateRaster(chart!, image); var positivePixel = Project(positive); var negativePixel = Project(negative);
                var positiveInk = PixelInk(raster, positivePixel, color); var negativeInk = PixelInk(raster, negativePixel, color); var minimum = stroke ? 4 : 10;
                Check($"{scenario}独立坐标填充与空白像素", positiveInk >= minimum && negativeInk == 0);
                probes.Add(new { scenario, image, plotArea = chart!.PlotArea, visibleRange = chart.VisibleRange, orientation = horizontal ? "Horizontal" : "Vertical", positive, negative, color = color.ToString(), positivePixel, negativePixel,
                    positiveInkPixels = positiveInk, negativeInkPixels = negativeInk, minimumInk = minimum, pixelSquareDip = 6, colorChannelTolerance = 24 });
            }
            int PixelInk(ShapeRaster raster, Point point, Color color) => raster.Count(TemplatePixelRect(new Rect(point.X - 3, point.Y - 3, 6, 6)), color, 24);
        }
        _barComparisonExamples.IsExpanded = false;
        static bool Near(double? value, double expected) => value is { } number && double.IsFinite(number) && Math.Abs(number - expected) <= 1e-9;
    }
}
