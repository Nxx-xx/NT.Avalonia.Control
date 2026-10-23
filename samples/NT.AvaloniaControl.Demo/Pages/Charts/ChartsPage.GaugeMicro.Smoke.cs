using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _gaugeMicroEvidence = [];

    private async Task RunGaugeMicroSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _gaugeMicroExamples!.IsExpanded = true; await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
            string Image(string scenario) => Path.Combine(directory, stem + $"-gauge-micro-{(dark ? "dark" : "light")}-{scenario}.png");
            void Check(string name, bool value) => checks[$"仪表微图{label}{name}"] = value;
            using var board = CreateGaugeMicroBoard();
            var window = new Window { Title = "Charts · 仪表与微图", Width = 1160, Height = 1480, RequestedThemeVariant = theme,
                Background = Brush(dark ? "#111722" : "#F8FAFC"), Foreground = Brush(dark ? "#EAF0FB" : "#172238"),
                Content = new Viewbox { Stretch = Stretch.Uniform, Child = new Border { Width = 1160, Padding = new Thickness(16), Child = board.View } } };
            var probes = new List<object>(); var exports = new List<object>();
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                var native = window.TryGetPlatformHandle() is { Handle: not 0 };
                var layouts = board.Gauges.Cast<Control>().Concat(board.Sparklines).Select(value => new { value.Name, value.Bounds }).ToArray();
                Check("十图原生布局和业务源", native && board.Gauges.All(value => value.Bounds.Width > 400 && value.Bounds.Height >= 230) && board.Sparklines.All(value => value.Bounds.Width > 400 && value.PlotArea.Height > 100) && ReferenceEquals(board.Sparklines[0].ItemsSource, board.Rows));
                var pageImage = Image("page"); SaveWindow(pageImage);
                board.Value.Value = 38; board.Target.IsChecked = false; board.Vertical.IsChecked = true; board.Rectangle.IsChecked = true; board.Reduce.IsChecked = true; board.Paused.IsChecked = true; board.Wave.IsChecked = true; board.UpdateData(); FlushControlTemplateWindow(window);
                Check("数值目标方向形状和减少动态效果输入生效", board.Gauges.All(value => value.Value == 38 && value.TargetValue is null) && board.Gauges[3] is LinearGaugeChart { Orientation: Orientation.Vertical } && board.Gauges[4] is LiquidFillGaugeChart { Shape: GaugeLiquidShape.Rectangle, ReduceMotion: true, IsPaused: true, IsWaveAnimationEnabled: true });
                Check("实时业务INPC和集合更新生效", board.Sparklines.All(value => value.GetPoints().Count == 8 && value.GetPoints()[1].Data.Point.Y == -35) && board.Rings[0].Value == 88 && board.Ranges[0].End == 30);
                var changedPageImage = Image("page-changed"); SaveWindow(changedPageImage);
                board.Wave.IsChecked = false;
                var magenta = Brush("#D7259F");
                var cases = new List<(string Name, ChartBase Chart, (double X, double Y, string Color)[] Positive, (double X, double Y, string Color)[] Negative)>();
                var circular = Gauge(new CircularGaugeChart { Value = 25, StartAngle = 0, SweepAngle = 360, ShowNeedle = false, ShowMajorTicks = false });
                cases.Add(("circular-arc", circular, [(296.97413408594537, 226.97413408594537, "#D7259F")], [(123.02586591405466, 226.97413408594537, "#D7259F")]));
                cases.Add(("circular-needle", Gauge(new CircularGaugeChart { Value = 25, StartAngle = 0, SweepAngle = 360, ValueBrush = Brushes.Transparent, ShowNeedle = true, ShowMajorTicks = false, NeedleBrush = magenta }), [(210, 210, "#D7259F")], [(280, 140, "#D7259F")]));
                var range = Gauge(new CircularGaugeChart { Value = 0, StartAngle = 0, SweepAngle = 360, ValueBrush = Brushes.Transparent, ShowNeedle = false, ShowMajorTicks = false }); range.Ranges.Add(new() { Start = 25, End = 50, Brush = magenta });
                cases.Add(("circular-range", range, [(123.02586591405466, 226.97413408594537, "#D7259F")], [(296.97413408594537, 226.97413408594537, "#D7259F")]));
                cases.Add(("semi", Gauge(new GaugeChart { Value = 50, StartAngle = 180, SweepAngle = 180, ShowNeedle = false, ShowMajorTicks = false }), [(73.5283912309963, 123.52839123099633, "#D7259F")], [(346.47160876900364, 123.5283912309963, "#D7259F")]));
                var rings = Gauge(new GradientRingChart { RingSpacing = 6, StartAngle = 0 }); rings.Values.Add(new() { Value = 25, Brush = magenta }); rings.Values.Add(new() { Value = 75, Brush = magenta });
                cases.Add(("rings", rings, [(296.97413408594537, 226.97413408594537, "#D7259F"), (139.9964286625318, 69.9964286625318, "#D7259F")], [(123.02586591405466, 226.97413408594537, "#D7259F"), (280.0035713374682, 69.99642866253178, "#D7259F")]));
                cases.Add(("linear-horizontal", Gauge(new LinearGaugeChart { Value = 25, Orientation = Orientation.Horizontal, ShowNeedle = false }), [(50, 140, "#D7259F")], [(210, 140, "#D7259F")]));
                cases.Add(("linear-vertical", Gauge(new LinearGaugeChart { Value = 50, Orientation = Orientation.Vertical, ShowNeedle = false }), [(210, 220, "#D7259F")], [(210, 50, "#D7259F")]));
                cases.Add(("linear-reverse", Gauge(new LinearGaugeChart { Value = 25, Orientation = Orientation.Horizontal, Clockwise = false, ShowNeedle = false }), [(370, 140, "#D7259F")], [(40, 140, "#D7259F")]));
                cases.Add(("progress", Gauge(new ProgressDonutChart { Value = 75, StartAngle = 0, SweepAngle = 360, ShowBounds = false }), [(123.02586591405463, 53.02586591405466, "#D7259F")], [(296.9741340859453, 53.025865914054634, "#D7259F")]));
                cases.Add(("progress-empty", Gauge(new ProgressDonutChart { Value = 0, StartAngle = 0, SweepAngle = 360, ShowBounds = false }), [], [(296.97413408594537, 226.97413408594537, "#D7259F"), (123.02586591405463, 53.02586591405466, "#D7259F")]));
                cases.Add(("progress-full", Gauge(new ProgressDonutChart { Value = 100, StartAngle = 0, SweepAngle = 360, ShowBounds = false }), [(296.9741340859453, 53.025865914054634, "#D7259F")], [(210, 140, "#D7259F")]));
                cases.Add(("liquid-circle", Gauge(new LiquidFillGaugeChart { Value = 50, Shape = GaugeLiquidShape.Circle, WaveAmplitude = 0, IsWaveAnimationEnabled = false }), [(210, 220, "#D7259F")], [(210, 50, "#D7259F"), (100, 260, "#D7259F")]));
                cases.Add(("liquid-rectangle", Gauge(new LiquidFillGaugeChart { Value = 50, Shape = GaugeLiquidShape.Rectangle, WaveAmplitude = 0, IsWaveAnimationEnabled = false }), [(402, 220, "#D7259F")], [(402, 50, "#D7259F")]));
                cases.Add(("liquid-empty", Gauge(new LiquidFillGaugeChart { Value = 0, Shape = GaugeLiquidShape.Circle, WaveAmplitude = 0, IsWaveAnimationEnabled = false }), [], [(210, 220, "#D7259F")]));
                var sparkline = Micro(SparklineType.Line, [0, 100, double.NaN, 100, 0], 0, 100);
                cases.Add(("spark-line", sparkline, [(54.375, 140, "#D7259F"), (365.625, 140, "#D7259F")], [(210, 140, "#D7259F")]));
                cases.Add(("spark-area", Micro(SparklineType.Area, [-1, 1, double.NaN, 1, -1], -1, 1), [(90, 100, "#D7259F")], [(90, 220, "#D7259F"), (210, 100, "#D7259F")]));
                cases.Add(("spark-bar", Micro(SparklineType.Bar, [-50, 100, 0, double.NaN, 25], -100, 100), [(44, 200, "#D7259F"), (127, 60, "#D7259F")], [(44, 80, "#D7259F"), (293, 60, "#D7259F")]));
                cases.Add(("spark-winloss", Micro(SparklineType.WinLoss, [-2, 7, 0, double.NaN, -1], null, null), [(44, 220, "#D7259F"), (127, 60, "#19A798"), (210, 140, "#F2A33A")], [(210, 60, "#F2A33A"), (293, 80, "#19A798")]));
                foreach (var item in cases)
                {
                    window.Content = item.Chart; FlushControlTemplateWindow(window);
                    var image = Image(item.Name); var raster = CircularCapture(item.Chart, image);
                    object[] Read((double X, double Y, string Color)[] points, bool positive) => points.Select(point => { var count = Ink(raster, point); return (object)new { x = point.X, y = point.Y, color = point.Color, inkPixels = count, minimumPixels = positive ? 6 : 0, maximumPixels = positive ? (int?)null : 0 }; }).ToArray();
                    var positive = Read(item.Positive, true); var negative = Read(item.Negative, false);
                    var pixelPass = item.Positive.All(point => Ink(raster, point) >= 6) && item.Negative.All(point => Ink(raster, point) == 0);
                    Check(item.Name + "独立公式正负像素", pixelPass && item.Chart.Bounds.Width == 420 && item.Chart.Bounds.Height == 280);
                    using var stream = new MemoryStream(); var exported = Export(item.Chart, stream, 96); var exportImage = Image(item.Name + "-export-96"); File.WriteAllBytes(exportImage, stream.ToArray()); stream.Position = 0; using var decoded = new Bitmap(stream); var exportRaster = ReadShapePixels(decoded);
                    var changedPixels = CountControlTemplateExactChanges(raster, exportRaster, new Rect(0, 0, 420, 280)); var density = Density(stream.ToArray(), 96);
                    Check(item.Name + "96DPI导出原生像素完全一致", exported && changedPixels == 0 && density && decoded.PixelSize == new PixelSize(420, 280));
                    probes.Add(new { scenario = item.Name, image, exportImage, width = raster.Width, height = raster.Height, controlBounds = item.Chart.Bounds, positive, negative, pixelPass, exported, changedPixels, densityMatches = density });
                }
                foreach (var representative in new (string Name, ChartBase Chart)[] { ("circular-arc", circular), ("spark-line", sparkline) })
                {
                    window.Content = representative.Chart; FlushControlTemplateWindow(window); var baselineImage = Image(representative.Name); using var baselineBitmap = new Bitmap(baselineImage); var baseline = ReadShapePixels(baselineBitmap);
                    foreach (var dpi in new[] { 144d, 192d })
                    {
                        using var stream = new MemoryStream(); var exported = Export(representative.Chart, stream, dpi); var image = Image($"{representative.Name}-export-{dpi:0}"); File.WriteAllBytes(image, stream.ToArray()); stream.Position = 0; using var bitmap = new Bitmap(stream); var raster = ReadShapePixels(bitmap); var changedPixels = CountControlTemplateExactChanges(baseline, raster, new Rect(0, 0, 420, 280)); var density = Density(stream.ToArray(), dpi);
                        Check($"{representative.Name}{dpi:0}DPI尺寸密度和原生像素一致", exported && changedPixels == 0 && density && bitmap.PixelSize == new PixelSize(420, 280));
                        exports.Add(new { scenario = representative.Name, baselineImage, image, dpi, exported, changedPixels, densityMatches = density, width = raster.Width, height = raster.Height });
                    }
                }
                using (var stream = new MemoryStream()) { var exported = await circular.TryExportPngAsync(stream, new ChartExportOptions { PixelSize = new PixelSize(420, 280), Background = window.Background }); stream.Position = 0; using var bitmap = new Bitmap(stream); Check("仪表异步PNG输出真实尺寸", exported && bitmap.PixelSize == new PixelSize(420, 280)); }
                using (var stream = new MemoryStream()) { var exported = await sparkline.TryExportJpegAsync(stream, new ChartExportOptions { PixelSize = new PixelSize(420, 280), Background = window.Background, JpegQuality = 88 }); stream.Position = 0; using var bitmap = new Bitmap(stream); Check("微图异步JPEG输出真实尺寸", exported && bitmap.PixelSize == new PixelSize(420, 280)); }
                using (var stream = new MemoryStream()) { var canceled = false; try { await circular.TryExportPngAsync(stream, null, new CancellationToken(true)); } catch (OperationCanceledException) { canceled = true; } Check("仪表异步导出预取消不写入", canceled && stream.Length == 0); }
                using (var stream = new MemoryStream()) { var canceled = false; try { await sparkline.TryExportPngAsync(stream, null, new CancellationToken(true)); } catch (OperationCanceledException) { canceled = true; } Check("微图异步导出预取消不写入", canceled && stream.Length == 0); }
                _gaugeMicroEvidence.Add(new { theme = label, nativeWindowCreated = native, pageImage, changedPageImage, layouts, probes, exports, physicalInputValidated = false, performanceMeasured = false, oracle = new { registration = "gauge-sparkline-oracle-preregistered-v1.json", sha256 = "55187CDD0D16F32A240A63195757940BFE896BC9B62E714B21F2F524C381F079", explicitControlWidth = 420, explicitControlHeight = 280, pixelSquareDip = 6, colorChannelTolerance = 24, alphaMinimum = 240, minimumInk = 6 }, evidenceKind = "native-window-rendertargetbitmap-preregistered-independent-gauge-sparkline-business-oracles-and-public-contracts" });
                foreach (var item in cases) { if (item.Chart is SparklineChart micro) micro.ItemsSource = null; }
                T Gauge<T>(T gauge) where T : GaugeBase { gauge.Width = 420; gauge.Height = 280; gauge.Padding = default; gauge.Title = null; gauge.ShowValue = false; gauge.ShowScale = false; gauge.TrackThickness = 18; gauge.TrackBrush = Brushes.Transparent; if (gauge.ValueBrush == Brushes.DodgerBlue) gauge.ValueBrush = magenta; gauge.Minimum = 0; gauge.Maximum = 100; gauge.Background = window.Background; gauge.Foreground = window.Foreground; return gauge; }
                SparklineChart Micro(SparklineType type, double[] items, double? minimum, double? maximum) => new() { Width = 420, Height = 280, Padding = default, SparklineType = type, ItemsSource = items, Minimum = minimum, Maximum = maximum, Baseline = 0, StrokeThickness = 2, MarkerSize = 5, BarSpacing = .2, ShowMarkers = false, ShowMinMax = false, ShowFirstLast = false, ShowBaseline = false, LineBrush = magenta, AreaFill = magenta, BarBrush = magenta, WinBrush = Teal, LossBrush = magenta, ZeroBrush = Orange, Background = window.Background, Foreground = window.Foreground };
                bool Export(ChartBase chart, Stream stream, double dpi) { var options = new ChartExportOptions { PixelSize = new PixelSize(420, 280), Dpi = dpi, Background = window.Background }; return chart is GaugeBase gauge ? gauge.TryExportPng(stream, options) : ((SparklineChart)chart).TryExportPng(stream, options); }
            }
            finally { window.Close(); }
            void SaveWindow(string path) { using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)); bitmap.Render(window); bitmap.Save(path); }
        }
        _gaugeMicroExamples.IsExpanded = false;
        static int Ink(ShapeRaster raster, (double X, double Y, string Color) point)
        {
            var rectangle = TemplatePixelRect(new Rect(point.X - 3, point.Y - 3, 6, 6)); var color = Color.Parse(point.Color); var count = 0;
            for (var y = Math.Max(0, rectangle.Y); y < Math.Min(raster.Height, rectangle.Bottom); y++)
                for (var x = Math.Max(0, rectangle.X); x < Math.Min(raster.Width, rectangle.Right); x++)
                {
                    var offset = y * raster.Stride + x * 4;
                    if (raster.Pixels[offset + 3] >= 240 && Math.Abs(raster.Pixels[offset] - color.B) <= 24 && Math.Abs(raster.Pixels[offset + 1] - color.G) <= 24 && Math.Abs(raster.Pixels[offset + 2] - color.R) <= 24) count++;
                }
            return count;
        }
        static bool Density(byte[] bytes, double dpi) => TryReadCommonPngDensity(bytes, out var x, out var y, out var unit) && x == (uint)Math.Round(dpi / .0254) && x == y && unit == 1;
    }
}
