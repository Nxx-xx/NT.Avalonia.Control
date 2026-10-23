using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _lineAreaEvidence = [];

    private async Task RunLineAreaSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _lineAreaExamples!.IsExpanded = true;
        await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
            string Image(string scenario, string extension = "png") => Path.Combine(directory, stem + $"-line-area-{(dark ? "dark" : "light")}-{scenario}.{extension}");
            void Check(string name, bool value) => checks[$"折线面积{label}{name}"] = value;
            static bool SameMetadata(double? actual, double expected) => actual is { } number && double.IsFinite(number) && Math.Abs(number - expected) <= 1e-9;
            using var board = CreateLineAreaBoard(false);
            var window = new Window { Title = "Charts · 折线与面积", Width = 1160, Height = 850, RequestedThemeVariant = theme,
                Background = Brush(dark ? "#111722" : "#F8FAFC"), Foreground = Brush(dark ? "#EAF0FB" : "#172238"), Content = new Border { Padding = new Thickness(16), Child = board.View } };
            var probes = new List<object>();
            var exports = new List<object>();
            CartesianChart? chart = null;
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                var native = window.TryGetPlatformHandle() is { Handle: not 0 };
                Check("四图在原生窗口实际布局", native && board.Charts.All(value => value.Bounds.Width > 400 && value.PlotArea.Height > 100));
                Check("五种插值与两类新系列展示", board.Lines.Series.OfType<LineSeries>().Select(value => value.Interpolation).SequenceEqual(Enum.GetValues<ChartLineInterpolation>()) && board.Filled is AreaSeries && board.Range is RangeAreaSeries);
                var boardStackedVisibleRange = board.Stacks.VisibleRange;
                var pageImage = Image("page"); SaveWindow(pageImage);
                board.Interpolation.SelectedItem = ChartLineInterpolation.StepCenter; board.Baseline.IsChecked = true; board.StackMode.SelectedItem = ChartStackMode.Percent;
                FlushControlTemplateWindow(window);
                Check("用户选择同步改变面积范围插值基线及堆叠", board.Filled.Interpolation == ChartLineInterpolation.StepCenter && board.Range.Interpolation == ChartLineInterpolation.StepCenter && board.Filled.Baseline == -20 &&
                    board.Stacks.Series.OfType<AreaSeries>().All(value => value.StackMode == ChartStackMode.Percent && value.Interpolation == ChartLineInterpolation.StepCenter) &&
                    board.Stacks.Series.OfType<LineSeries>().All(value => value.StackMode == ChartStackMode.Percent && value.Interpolation == ChartLineInterpolation.StepCenter));
                board.Gap.IsChecked = false; FlushControlTemplateWindow(window);
                Check("用户开关恢复缺失贡献", !board.Stacks.Series[1].CreateSnapshot().Points[3].IsEmpty);
                var rangeData = board.Range.CreateSnapshot().Points[1];
                Check("范围业务对象和原始上下界保留", ReferenceEquals(rangeData.Item, board.RangeRecords[1]) && Equals(rangeData.StableId, "range-1") && rangeData.Low == 30 && rangeData.High == 85 && board.Ranges.Series[1].YAxisKey == "line-area-pressure");
                var boardPercentVisibleRange = board.Stacks.VisibleRange;
                Check("百分比自动纵轴精确到达正负一百", boardPercentVisibleRange is { YMinimum: -100, YMaximum: 100 });
                var percentPageImage = Image("page-percent"); SaveWindow(percentPageImage);

                chart = new CartesianChart { Background = window.Background, Foreground = window.Foreground, Margin = new Thickness(12), ShowLegend = false, ShowGridLines = false,
                    IsZoomEnabled = true, IsPanEnabled = true, SelectionMode = ChartSelectionMode.Multiple,
                    XAxis = new NumericAxis { Minimum = 0, Maximum = 6 }, YAxis = new NumericAxis { Minimum = -120, Maximum = 120 } };
                window.Content = chart; FlushControlTemplateWindow(window);
                var line = new LineSeries { Stroke = Blue, StrokeThickness = 3, MarkerSize = 0, ItemsSource = new ChartPoint[] { new(0, 20), new(2, 80), new(4, 40), new(6, 70) } };
                chart.Series.Add(line); FlushControlTemplateWindow(window);
                ShapeRaster? linear = null;
                foreach (var interpolation in Enum.GetValues<ChartLineInterpolation>())
                {
                    line.Interpolation = interpolation; FlushControlTemplateWindow(window);
                    var image = Image("line-" + interpolation); var raster = CaptureControlTemplateRaster(chart, image);
                    // At t=.25 the first Hermite interval has m0=30, m1=0:
                    // h00*20 + h10*(2*30) + h01*80 = 37.8125.
                    var expected = interpolation switch { ChartLineInterpolation.Linear => 35d, ChartLineInterpolation.Smooth => 37.8125d, ChartLineInterpolation.StepBefore => 80d, ChartLineInterpolation.StepAfter => 20d, _ => 20d };
                    var wrong = interpolation == ChartLineInterpolation.StepBefore ? 20d : interpolation == ChartLineInterpolation.Linear ? 20d : 35d;
                    var ink = double.IsFinite(expected) ? Ink(raster, .5, expected, Color.Parse("#4385F4")) : -1;
                    var empty = double.IsFinite(expected) ? Ink(raster, .5, wrong, Color.Parse("#4385F4")) : -1;
                    var difference = linear is null ? 0 : CountControlTemplateExactChanges(linear, raster, chart.PlotArea);
                    if (interpolation == ChartLineInterpolation.Linear) linear = raster;
                    Check($"{interpolation}真实路径像素", ink >= 4 && empty == 0 && (interpolation != ChartLineInterpolation.Smooth || difference > 30));
                    if (interpolation == ChartLineInterpolation.StepCenter)
                        Check("中间阶梯在中点之后使用新值", Ink(raster, 1.5, 80, Color.Parse("#4385F4")) >= 4 && Ink(raster, 1.5, 20, Color.Parse("#4385F4")) == 0);
                    probes.Add(new { scenario = "line", interpolation = interpolation.ToString(), image, plotArea = chart.PlotArea, queryX = .5, expectedY = double.IsFinite(expected) ? expected : (double?)null, rejectedY = wrong, inkPixels = ink, rejectedInkPixels = empty, changedFromLinear = difference });
                }
                line.Interpolation = ChartLineInterpolation.Linear; line.MarkerSize = 8; FlushControlTemplateWindow(window);
                var hit = chart.HitTestPoint(Project(2, 80), 8);
                if (hit is not null) chart.SelectedPoints = new[] { hit };
                Check("原始折线点仍可命中并选择", hit is { Index: 1, Point.Y: 80 } && chart.SelectedPoint?.Index == 1);
                chart.ClearSelection();
                chart.Series.Clear();
                var area = new AreaSeries { Stroke = Blue, Fill = Blue, StrokeThickness = 2, MarkerSize = 0, Baseline = 0, ItemsSource = new ChartPoint[] { new(0, 40), new(2, 70), new(4, -20), new(6, 30) } };
                chart.Series.Add(area); FlushControlTemplateWindow(window);
                var areaImage = Image("area-baseline-zero"); var areaRaster = CaptureControlTemplateRaster(chart, areaImage);
                Check("面积内部填充且基线下方留空", Ink(areaRaster, 1, 25, Color.Parse("#4385F4")) >= 10 && Ink(areaRaster, 1, -10, Color.Parse("#4385F4")) == 0);
                area.Baseline = -20; FlushControlTemplateWindow(window);
                var baselineImage = Image("area-baseline-negative"); var baselineRaster = CaptureControlTemplateRaster(chart, baselineImage);
                Check("非零基线扩展填充而不改变源值", Ink(baselineRaster, 1, -10, Color.Parse("#4385F4")) >= 10 && area.CreateSnapshot().Points[0].Point.Y == 40);
                probes.Add(new { scenario = "area-baseline", image = areaImage, changedImage = baselineImage, plotArea = chart.PlotArea, upperAtQuery = 55, queryX = 1, initiallyEmptyY = -10, initialInk = Ink(areaRaster, 1, -10, Color.Parse("#4385F4")), changedInk = Ink(baselineRaster, 1, -10, Color.Parse("#4385F4")) });
                foreach (var interpolation in Enum.GetValues<ChartLineInterpolation>())
                {
                    area.Interpolation = interpolation; FlushControlTemplateWindow(window);
                    var image = Image("area-" + interpolation); var raster = CaptureControlTemplateRaster(chart, image);
                    Check($"{interpolation}面积保留填充", Ink(raster, 1, 0, Color.Parse("#4385F4")) >= 10);
                    probes.Add(new { scenario = "area", interpolation = interpolation.ToString(), image, plotArea = chart.PlotArea, queryX = 1, queryY = 0, inkPixels = Ink(raster, 1, 0, Color.Parse("#4385F4")) });
                }
                area.Interpolation = ChartLineInterpolation.Linear;
                area.ItemsSource = new ChartPoint[] { new(0, 40), new(2, 70), new(3, double.NaN), new(4, 50), new(6, 30) };
                FlushControlTemplateWindow(window);
                var gapImage = Image("area-gap"); var gapRaster = CaptureControlTemplateRaster(chart, gapImage);
                Check("缺失面积在两段之间保留空白", Ink(gapRaster, 3, 20, Color.Parse("#4385F4")) == 0 && Ink(gapRaster, 1, 20, Color.Parse("#4385F4")) >= 10 && Ink(gapRaster, 5, 20, Color.Parse("#4385F4")) >= 10);
                probes.Add(new { scenario = "area-gap", image = gapImage, plotArea = chart.PlotArea, queryY = 20, gapX = 3, beforeX = 1, afterX = 5,
                    gapPixels = Ink(gapRaster, 3, 20, Color.Parse("#4385F4")), beforePixels = Ink(gapRaster, 1, 20, Color.Parse("#4385F4")), afterPixels = Ink(gapRaster, 5, 20, Color.Parse("#4385F4")) });
                chart.Series.Clear();
                var records = new[] { new LineAreaRange("native-r0", 0, 20, 60), new LineAreaRange("native-r1", 2, 30, 80), new LineAreaRange("native-r2", 4, 10, 50), new LineAreaRange("native-r3", 6, 25, 70) };
                var range = new RangeAreaSeries { Stroke = Teal, Fill = Teal, StrokeThickness = 2, MarkerSize = 0, ItemsSource = records, CategoryPath = nameof(LineAreaRange.X), IdPath = nameof(LineAreaRange.Id),
                    HighPath = nameof(LineAreaRange.High), LowPath = nameof(LineAreaRange.Low) };
                chart.Series.Add(range); FlushControlTemplateWindow(window);
                foreach (var interpolation in Enum.GetValues<ChartLineInterpolation>())
                {
                    range.Interpolation = interpolation; FlushControlTemplateWindow(window);
                    var image = Image("range-" + interpolation); var raster = CaptureControlTemplateRaster(chart, image);
                    var inside = Ink(raster, 1, 45, Color.Parse("#19A798")); var outside = Ink(raster, 1, 0, Color.Parse("#19A798")) + Ink(raster, 1, 100, Color.Parse("#19A798"));
                    Check($"{interpolation}范围只填充上下界之间", inside >= 10 && outside == 0 && range.CreateSnapshot().Points[1].High == 80 && range.CreateSnapshot().Points[1].Low == 30);
                    probes.Add(new { scenario = "range", interpolation = interpolation.ToString(), image, plotArea = chart.PlotArea, queryX = 1, insideY = 45, outsideY = new[] { 0, 100 }, insidePixels = inside, outsidePixels = outside });
                }
                chart.Series.Clear();
                var first = new AreaSeries { Title = "第一贡献", StackMode = ChartStackMode.Stacked, StackGroup = "native", Fill = Blue, Stroke = Blue, MarkerSize = 8,
                    ItemsSource = new ChartPoint[] { new(1, 20), new(3, -20), new(5, 30) } };
                var second = new AreaSeries { Title = "第二贡献", StackMode = ChartStackMode.Stacked, StackGroup = "native", Fill = Teal, Stroke = Teal, MarkerSize = 8,
                    ItemsSource = new ChartPoint[] { new(1, 30), new(3, -30), new(5, 70) } };
                chart.Series.Add(first); chart.Series.Add(second); FlushControlTemplateWindow(window);
                foreach (var mode in new[] { ChartStackMode.Stacked, ChartStackMode.Percent })
                {
                    first.StackMode = second.StackMode = mode; FlushControlTemplateWindow(window);
                    var percent = mode == ChartStackMode.Percent;
                    foreach (var sign in new[] { 1, -1 })
                    {
                        var x = sign == 1 ? 1 : 3; var y = sign * (percent ? 100 : 50); var point = chart.HitTestPoint(Project(x, y), 6);
                        var expectedBase = sign * (percent ? 40 : 20); var expectedContribution = sign * (percent ? 60 : 30);
                        Check($"{mode}{(sign == 1 ? "正" : "负")}贡献命中保留原值和基线", point is not null && ReferenceEquals(point.Series, second) && point.Point == new ChartPoint(x, y) &&
                            point.RawPoint == new ChartPoint(x, sign * 30) && SameMetadata(point.BaseValue, expectedBase) && SameMetadata(point.StackValue, expectedContribution));
                        probes.Add(new { scenario = "stack-hit", mode = mode.ToString(), sign, query = new ChartPoint(x, y), expectedBase, expectedContribution, actual = point is null ? null : new { point.Point, point.RawPoint, point.BaseValue, point.StackValue, point.Index } });
                    }
                    var image = Image("stack-" + mode); var baseline = CaptureControlTemplateRaster(chart, image);
                    var width = baseline.Width; var height = baseline.Height;
                    foreach (var dpi in new[] { 96d, 144d, 192d })
                    {
                        using var stream = new MemoryStream();
                        var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(width, height), Dpi = dpi, Background = chart.Background });
                        var exportImage = Image($"stack-{mode}-export-{dpi:0}"); File.WriteAllBytes(exportImage, stream.ToArray()); stream.Position = 0;
                        using var bitmap = new Bitmap(stream); var raster = ReadShapePixels(bitmap);
                        var changed = CountControlTemplateExactChanges(baseline, raster, chart.PlotArea);
                        var density = TryReadCommonPngDensity(stream.ToArray(), out var dx, out var dy, out var unit) && dx == (uint)Math.Round(dpi / .0254) && dx == dy && unit == 1;
                        Check($"{mode}{dpi:0}DPI导出保持堆叠像素和密度", exported && changed == 0 && density && raster.Width == width && raster.Height == height);
                        exports.Add(new { mode = mode.ToString(), dpi, image = exportImage, exported, width = raster.Width, height = raster.Height, changedPixels = changed, densityMatches = density });
                    }
                }
                var full = chart.VisibleRange; chart.Viewport = new ChartViewport(.5, 5.5, -110, 110); FlushControlTemplateWindow(window);
                Check("堆叠缩放使用最新显示坐标", chart.VisibleRange != full && chart.HitTestPoint(Project(1, 100), 6)?.RawPoint == new ChartPoint(1, 30));
                chart.ResetViewport(); FlushControlTemplateWindow(window);
                _lineAreaEvidence.Add(new { theme = label, nativeWindowCreated = native, pageImage, percentPageImage, boardStackedVisibleRange, boardPercentVisibleRange, plotArea = chart.PlotArea, visibleRange = chart.VisibleRange, probes, exports,
                    derivedMetadataAbsoluteTolerance = 1e-9, rawPointAndIdentityExact = true,
                    physicalInputValidated = false, performanceMeasured = false, evidenceKind = "native-window-rendertargetbitmap-independent-coordinate-probes-and-public-hit-export-api" });
            }
            finally { window.Close(); chart?.Series.Clear(); }

            void SaveWindow(string image)
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
                bitmap.Render(window); bitmap.Save(image);
            }
            Point Project(double x, double y)
            {
                var range = chart!.VisibleRange!.Value; var plot = chart.PlotArea;
                return new(plot.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
                    plot.Bottom - (y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
            }
            int Ink(ShapeRaster raster, double x, double y, Color color)
            {
                var point = Project(x, y);
                return raster.Count(TemplatePixelRect(new Rect(point.X - 3, point.Y - 3, 6, 6)), color, 24);
            }
        }
        _lineAreaExamples.IsExpanded = false;
    }
}
