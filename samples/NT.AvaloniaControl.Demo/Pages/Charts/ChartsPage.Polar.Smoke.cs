using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _polarEvidence = [];

    private async Task RunPolarSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _polarExamples!.IsExpanded = true; await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
            string Image(string scenario) => Path.Combine(directory, stem + $"-polar-{(dark ? "dark" : "light")}-{scenario}.png");
            void Check(string name, bool value) => checks[$"极坐标雷达{label}{name}"] = value;
            using var board = CreatePolarBoard();
            var window = new Window { Title = "Charts · 极坐标与雷达", Width = 1160, Height = 1480, RequestedThemeVariant = theme,
                Background = Brush(dark ? "#111722" : "#F8FAFC"), Foreground = Brush(dark ? "#EAF0FB" : "#172238"), Content = new Viewbox { Stretch = Stretch.Uniform, Child = new Border { Width = 1160, Padding = new Thickness(16), Child = board.View } } };
            PolarChart? chart = null; var probes = new List<object>(); var axisProbes = new List<object>(); var exports = new List<object>();
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                var native = window.TryGetPlatformHandle() is { Handle: not 0 };
                var boardLayouts = board.Charts.Select(value => new { value.Name, value.Bounds, value.PlotArea }).ToArray();
                Check("八图原生布局和真实业务源", native && board.Charts.All(value => value.Bounds.Width > 400 && value.Bounds.Height >= 230 && value.PlotArea.Width > 100) && ReferenceEquals(board.Line.ItemsSource, board.Records));
                var pageImage = Image("page"); SaveWindow(pageImage);
                board.Unit.SelectedItem = ChartAngleUnit.Radians; board.GridShape.SelectedItem = PolarGridShape.Polygon; board.Clockwise.IsChecked = false; board.Closed.IsChecked = false; board.Labels.IsChecked = true; board.MixedReference.IsChecked = true; board.UpdateData(); FlushControlTemplateWindow(window);
                Check("用户角度网格方向闭合标签和数据控制生效", board.Charts.All(value => value.AngleUnit == ChartAngleUnit.Radians && value.GridShape == PolarGridShape.Polygon && !value.Clockwise && value.ShowDataLabels) && !board.Line.IsClosed && !board.Radar.IsClosed && board.Area.CreateSnapshot().Points[0].Point.Y == 40);
                Check("异构叠加控制保留两类原始数据", board.Charts[5].Series.OfType<PolarLineSeries>().Single().CreateSnapshot().Points[1].Point.X == Math.PI / 2 && board.Area.CreateSnapshot().Points[1].Point.Y == 85);
                var changedPageImage = Image("page-radians-polygon"); SaveWindow(changedPageImage);
                chart = new PolarChart { Width = 700, Height = 520, Background = window.Background, Foreground = window.Foreground, Padding = default, ShowLegend = false, ShowAxisLabels = false, ShowDataLabels = false,
                    IsTooltipEnabled = false, SelectionMode = ChartSelectionMode.None, ShowGridLines = false, AxisBrush = Brushes.Transparent,
                    AngularAxis = Axis(0, 360, 90), RadialAxis = Axis(0, 100, 25) };
                window.Content = chart; FlushControlTemplateWindow(window);
                Check("显式700乘520真实布局和独立公式", Near(chart.Bounds.Width, 700) && Near(chart.Bounds.Height, 520) && Near(chart.PlotArea.X, 8) && Near(chart.PlotArea.Y, 8) && Near(chart.PlotArea.Width, 684) && Near(chart.PlotArea.Height, 504));
                var numeric = new[] { new ChartPoint(0, 100), new ChartPoint(90, 100), new ChartPoint(180, 100), new ChartPoint(270, 100) };
                var line = new PolarLineSeries { ItemsSource = numeric, Stroke = Blue, StrokeThickness = 4, ShowMarkers = false, IsClosed = true };
                SetSeries(line); Probe("closed-square-line", "#4385F4", 225, Math.Sqrt(.5), 225, .4);
                line.IsClosed = false; FlushControlTemplateWindow(window); Probe("open-square-line", "#4385F4", 45, Math.Sqrt(.5), 225, Math.Sqrt(.5));
                line.ItemsSource = new ChartPoint[] { new(0, 100), new(90, double.NaN), new(180, 100), new(270, 100) }; FlushControlTemplateWindow(window);
                Probe("gap-line", "#4385F4", 135, Math.Sqrt(.5), -45, Math.Sqrt(.5));
                var scatter = new PolarScatterSeries { ItemsSource = numeric, Fill = Blue, MarkerSize = 16 };
                SetSeries(scatter); Probe("scatter-cardinals", "#4385F4", -90, 1, 45, Math.Sqrt(.5));
                var filled = new PolarFilledSeries { ItemsSource = numeric, Fill = Blue, FillOpacity = 1, StrokeThickness = 0, ShowMarkers = false };
                SetSeries(filled); Probe("filled-diamond", "#4385F4", 45, .3, 45, .9);
                var categories = new[] { new PolarRecord("A", "A", 0, 100, "#19A798"), new("B", "B", 90, 100, "#19A798"), new("C", "C", 180, 100, "#19A798"), new("D", "D", 270, 100, "#19A798") };
                var radar = Category(new RadarSeries { Fill = Teal, FillOpacity = 1, StrokeThickness = 0, ShowMarkers = false }, categories);
                SetSeries(radar); chart.GridShape = PolarGridShape.Polygon; FlushControlTemplateWindow(window); Probe("radar-polygon", "#19A798", 45, .3, 45, .9);
                var radial = Category(new RadialLineSeries { Stroke = Orange, StrokeThickness = 4, ShowMarkers = false }, categories);
                SetSeries(radial); chart.GridShape = PolarGridShape.Circle; FlushControlTemplateWindow(window); Probe("radial-line", "#F2A33A", 225, Math.Sqrt(.5), 225, .4);
                chart.AngleUnit = ChartAngleUnit.Radians; chart.AngularAxis.Maximum = 2 * Math.PI; chart.AngularAxis.Interval = Math.PI / 2;
                line.ItemsSource = numeric.Select(value => new ChartPoint(value.X * Math.PI / 180, value.Y)).ToArray(); line.IsClosed = true; SetSeries(line); Probe("radians-line", "#4385F4", 225, Math.Sqrt(.5), 225, .4);
                chart.AngleUnit = ChartAngleUnit.Degrees; chart.AngularAxis.Maximum = 360; chart.AngularAxis.Interval = 90; chart.Clockwise = false;
                line.ItemsSource = new ChartPoint[] { new(0, 100), new(90, 100) }; line.IsClosed = false; SetSeries(line); Probe("counterclockwise-line", "#4385F4", -135, Math.Sqrt(.5), -45, Math.Sqrt(.5));
                chart.Clockwise = true; chart.SweepAngle = 180; line.ItemsSource = new ChartPoint[] { new(0, 100), new(180, 100) }; SetSeries(line); Probe("partial-line", "#4385F4", -45, Math.Sqrt(.5), 135, Math.Sqrt(.5));
                chart.SweepAngle = 360;
                var areaRecords = categories.Select((value, index) => value with { Value = new[] { 100d, 25, 64, 0 }[index], Color = "#4385F4" }).ToArray();
                var area = Category(new PolarAreaSeries { Fill = Blue, FillOpacity = 1, StrokeThickness = 0, SectorGap = 0, InnerRadius = 0 }, areaRecords);
                SetSeries(area); Probe("polar-area-radius", "#4385F4", 45, .4, 45, .6, (225, .2));
                var rose = Category(new NightingaleRoseSeries { Fill = Teal, FillOpacity = 1, StrokeThickness = 0, SectorGap = 0, InnerRadius = 0 }, areaRecords.Select(row => row with { Color = "#19A798" }).ToArray());
                SetSeries(rose); Probe("nightingale-rose-radius", "#19A798", 45, .2, 45, .4, (225, .2));
                var barRecords = categories.Select((value, index) => value with { Value = new[] { 25d, 50, 75, 100 }[index], Color = "#F2A33A" }).ToArray();
                var bars = Category(new RadialBarSeries { Fill = Orange, FillOpacity = 1, StrokeThickness = 0, SectorGap = 0, InnerRadius = .2 }, barRecords);
                SetSeries(bars); Probe("radial-bar-lanes", "#F2A33A", -45, .3, 135, .3, (0, .1));
                Check("三种扇区编码保留原始数值和源身份", area.CreateSnapshot().Points[1].Point.Y == 25 && rose.CreateSnapshot().Points[2].Point.Y == 64 && chart.GetSectors().Single(value => value.Data.SourceIndex == 0) is { Data.Point.Y: 25 } bar && Equals(bar.Data.StableId, "A") && ReferenceEquals(bar.Data.Item, barRecords[0]));
                var transparent = Category(new RadialLineSeries { Stroke = Brushes.Transparent, Fill = Brushes.Transparent, StrokeThickness = 0, ShowMarkers = false, FillOpacity = 0 }, categories);
                SetSeries(transparent); chart.ShowGridLines = true; chart.GridShape = PolarGridShape.Circle;
                chart.GridLineBrush = Brush("#9BA5B1"); chart.GridLineStrokeThickness = 2; chart.GridLevels = 4; FlushControlTemplateWindow(window);
                Probe("circle-grid", "#9BA5B1", 45, .5, 45, Math.Sqrt(.125));
                chart.GridShape = PolarGridShape.Polygon; FlushControlTemplateWindow(window); Probe("polygon-grid", "#9BA5B1", 45, Math.Sqrt(.125), 45, .5);
                chart.GridShape = PolarGridShape.Circle; chart.GridLineBrush = Brush("#D7259F"); chart.AxisBrush = null; chart.RadialAxis.Interval = 25;
                area.InnerRadius = .15; area.FillOpacity = 0; area.ShowMarkers = false; SetSeries(area);
                Probe("area-inner-axis", "#D7259F", 22.5, Math.Sqrt(.266875), 22.5, .25, isAxis: true);
                rose.InnerRadius = .15; rose.FillOpacity = 0; rose.ShowMarkers = false; SetSeries(rose);
                Probe("rose-inner-axis", "#D7259F", 22.5, .3625, 22.5, .25, isAxis: true);
                bars.ItemsSource = barRecords.Take(3).Select((row, index) => row with { Value = new[] { 25d, 50, 75 }[index] }).ToArray(); bars.FillOpacity = 0; bars.ShowMarkers = false; chart.RadialAxis.Interval = 20; SetSeries(bars);
                Probe("radial-bar-value-axis", "#D7259F", -18, .5, 30, .5, isAxis: true);
                chart.RadialAxis.Interval = 25;
                chart.ShowGridLines = false; chart.GridShape = PolarGridShape.Circle;
                var source = PolarRecords(); scatter = new PolarScatterSeries { ItemsSource = source, AnglePath = nameof(PolarRecord.Angle), RadiusPath = nameof(PolarRecord.Value), IdPath = nameof(PolarRecord.Id), ColorPath = nameof(PolarRecord.Color), MarkerSize = 18 };
                SetSeries(scatter); chart.SelectionMode = ChartSelectionMode.Multiple; chart.IsTooltipEnabled = true;
                var candidate = chart.GetPoints().Single(value => value.Data.SourceIndex == 1);
                void Cancel(object? sender, ChartSelectionChangingEventArgs args) { if (args.CanCancel) args.Cancel = true; }
                chart.SelectionChanging += Cancel; SelectionRangeClick(chart, window, candidate.Position); chart.SelectionChanging -= Cancel;
                Check("选择前置取消拦截路由指针", chart.SelectedPoints.Count == 0);
                SelectionRangeClick(chart, window, candidate.Position); FlushControlTemplateWindow(window);
                Check("指针选择保留原始角半径对象和业务ID", chart.SelectedPoint is { Index: 1, Point.X: 90, Point.Y: 50 } selected && ReferenceEquals(selected.Item, source[1]) && Equals(selected.StableId, "polar-b"));
                var replaced = source.Select((value, index) => index == 1 ? value with { Value = 75 } : value).ToArray(); scatter.ItemsSource = replaced; _ = chart.GetPoints(); FlushControlTemplateWindow(window);
                Check("换源选择按稳定ID跟随新对象", chart.SelectedPoint is { Index: 1, Point.Y: 75 } replacedHit && ReferenceEquals(replacedHit.Item, replaced[1]) && Equals(replacedHit.StableId, "polar-b"));
                chart.ClearSelection(); chart.SelectionMode = ChartSelectionMode.None; chart.IsTooltipEnabled = false; filled.ItemsSource = numeric; SetSeries(filled);
                var baselineImage = Image("export-baseline"); var baseline = CircularCapture(chart, baselineImage);
                foreach (var dpi in new[] { 96d, 144d, 192d })
                {
                    using var stream = new MemoryStream(); var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(700, 520), Dpi = dpi, Background = chart.Background });
                    var image = Image($"export-{dpi:0}"); File.WriteAllBytes(image, stream.ToArray()); stream.Position = 0; using var bitmap = new Bitmap(stream); var raster = ReadShapePixels(bitmap);
                    var changed = CountControlTemplateExactChanges(baseline, raster, new Rect(chart.Bounds.Size)); var density = TryReadCommonPngDensity(stream.ToArray(), out var dx, out var dy, out var unit) && dx == (uint)Math.Round(dpi / .0254) && dx == dy && unit == 1;
                    Check($"{dpi:0}DPI导出尺寸密度和当前画面像素一致", exported && density && changed == 0 && raster.Width == 700 && raster.Height == 520);
                    exports.Add(new { dpi, baselineImage, image, exported, width = raster.Width, height = raster.Height, controlBounds = chart.Bounds, changedPixels = changed, densityMatches = density });
                }
                using (var png = new MemoryStream()) { var saved = await chart.TryExportPngAsync(png, new ChartExportOptions { PixelSize = new PixelSize(700, 520), Background = chart.Background }); png.Position = 0; using var bitmap = new Bitmap(png); Check("异步PNG输出真实可解码尺寸", saved && bitmap.PixelSize == new PixelSize(700, 520)); }
                using (var jpeg = new MemoryStream()) { var saved = await chart.TryExportJpegAsync(jpeg, new ChartExportOptions { PixelSize = new PixelSize(700, 520), Background = chart.Background, JpegQuality = 88 }); jpeg.Position = 0; using var bitmap = new Bitmap(jpeg); Check("异步JPEG输出真实可解码尺寸", saved && bitmap.PixelSize == new PixelSize(700, 520)); }
                using (var canceled = new MemoryStream()) { var raised = false; try { await chart.TryExportPngAsync(canceled, null, new CancellationToken(true)); } catch (OperationCanceledException) { raised = true; } Check("预取消异步导出不写入", raised && canceled.Length == 0); }
                _polarEvidence.Add(new { theme = label, nativeWindowCreated = native, pageImage, changedPageImage, boardLayouts, probes, axisProbes, exports, physicalInputValidated = false, performanceMeasured = false,
                    oracle = new { explicitControlWidth = 700, explicitControlHeight = 520, centerX = 350, centerY = 260, radius = 252, insetDip = 8, angularMinimum = 0, angularMaximum = 360, radialMinimum = 0, radialMaximum = 100, startAngle = -90, sweepAngle = 360, clockwise = true, background = dark ? "#111722" : "#F8FAFC", pixelSquareDip = 6, colorChannelTolerance = 24, minimumInk = 10 }, evidenceKind = "native-window-rendertargetbitmap-preregistered-independent-polar-radar-business-oracles-and-public-contracts" });
            }
            finally { window.Close(); chart?.Series.Clear(); }

            void SaveWindow(string path) { using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)); bitmap.Render(window); bitmap.Save(path); }
            void SetSeries(PolarSeries series) { chart!.Series.Clear(); chart.Series.Add(series); FlushControlTemplateWindow(window); }
            void Probe(string scenario, string colorText, double positiveAngle, double positiveRadius, double negativeAngle, double negativeRadius, (double Angle, double Radius)? extraNegative = null, bool isAxis = false)
            {
                var image = Image(scenario); var raster = CircularCapture(chart!, image); var color = Color.Parse(colorText);
                var minimumInk = isAxis || scenario is "circle-grid" or "polygon-grid" ? 6 : 10;
                var positive = Project(positiveAngle, positiveRadius); var negative = Project(negativeAngle, negativeRadius); var positiveInk = Ink(raster, positive, color); var negativeInk = Ink(raster, negative, color);
                Point? extraPixel = extraNegative is { } extra ? Project(extra.Angle, extra.Radius) : null; var extraInk = extraPixel is { } pixel ? Ink(raster, pixel, color) : 0;
                Check(scenario + "独立角度半径正负像素", positiveInk >= minimumInk && negativeInk == 0 && extraInk == 0 && Near(chart!.Bounds.Width, 700) && Near(chart.Bounds.Height, 520));
                (isAxis ? axisProbes : probes).Add(new { scenario, image, width = raster.Width, height = raster.Height, controlBounds = chart!.Bounds, color = colorText, positiveAngle, positiveRadius, negativeAngle, negativeRadius, positivePixel = positive, negativePixel = negative, extraNegativePixel = extraPixel, positiveInkPixels = positiveInk, negativeInkPixels = negativeInk, extraNegativeInkPixels = extraInk, minimumInk, pixelSquareDip = 6, colorChannelTolerance = 24 });
            }
        }
        _polarExamples.IsExpanded = false;
        static NumericAxis Axis(double minimum, double maximum, double interval) => new() { Minimum = minimum, Maximum = maximum, Interval = interval, ShowGridLines = false, ShowLabels = false };
        static T Category<T>(T series, PolarRecord[] rows) where T : PolarSeries { series.ItemsSource = rows; series.CategoryPath = nameof(PolarRecord.Label); series.ValuePath = nameof(PolarRecord.Value); series.IdPath = nameof(PolarRecord.Id); return series; }
        static Point Project(double angle, double radius) { var radians = angle * Math.PI / 180; return new(350 + Math.Cos(radians) * 252 * radius, 260 + Math.Sin(radians) * 252 * radius); }
        static int Ink(ShapeRaster raster, Point point, Color color) => raster.Count(TemplatePixelRect(new Rect(point.X - 3, point.Y - 3, 6, 6)), color, 24);
        static bool Near(double actual, double expected) => double.IsFinite(actual) && Math.Abs(actual - expected) < 1e-8;
    }
}
