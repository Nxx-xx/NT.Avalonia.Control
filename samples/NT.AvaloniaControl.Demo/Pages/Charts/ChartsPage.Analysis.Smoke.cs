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
    private readonly List<object> _analysisEvidence = [];
    private async Task RunAnalysisSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _analysisExamples!.IsExpanded = true; await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
            string Image(string scenario) => Path.Combine(directory, stem + $"-analysis-{(dark ? "dark" : "light")}-{scenario}.png");
            void Check(string name, bool value) => checks[$"基础分析{label}{name}"] = value;
            using var board = CreateAnalysisBoard();
            var window = new Window { Title = "Charts · 基础分析图", Width = 1160, Height = 1480, RequestedThemeVariant = theme,
                Background = Brush(dark ? "#111722" : "#F8FAFC"), Foreground = Brush(dark ? "#EAF0FB" : "#172238"), Content = new Viewbox { Stretch = Stretch.Uniform, Child = new Border { Width = 1160, Padding = new Thickness(16), Child = board.View } } };
            var probes = new List<object>(); var exports = new List<object>();
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                var native = window.TryGetPlatformHandle() is { Handle: not 0 };
                var layouts = board.Analysis.Cast<Control>().Concat(board.Cartesian).Select(chart => new { chart.Name, chart.Bounds }).ToArray();
                Check("七图原生布局和共享业务源", native && board.Analysis.All(chart => chart.Bounds.Width > 400 && chart.PlotArea.Height > 100) && board.Cartesian.All(chart => chart.Bounds.Width > 400 && chart.PlotArea.Height > 100) && ReferenceEquals(board.Analysis[0].ItemsSource, board.Rows));
                Check("象形柱首尾整个宽度位于绘图区", board.Pictorial.Select((series, index) =>
                {
                    var chart = board.Cartesian[index]; var peer = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(chart)!;
                    var first = peer.GetPointPeer(series, 0)?.CurrentPoint; var last = peer.GetPointPeer(series, board.Symbols.Count - 1)?.CurrentPoint;
                    return chart.XAxis is CategoryAxis { PlotMode: CategoryPlotMode.BetweenTicks } && series.BarWidth is { } width && first is not null && last is not null && first.Position.X - width / 2 >= chart.PlotArea.Left && last.Position.X + width / 2 <= chart.PlotArea.Right;
                }).All(value => value));
                var funnelBoard = (FunnelChart)board.Analysis[0]; var heatBoard = (HeatmapChart)board.Analysis[2];
                var selected = funnelBoard.GetMarks().Single(mark => mark.Data.SourceIndex == 1); var hit = funnelBoard.HitTest(selected.LabelPosition);
                Check("原生漏斗命中保留业务项和ID", hit is { Index: 1, Point.Y: 85 } && ReferenceEquals(hit.Item, board.Rows[1]) && Equals(hit.StableId, "stage-b"));
                funnelBoard.SelectedIndex = 1; FlushControlTemplateWindow(window);
                Check("原生选择解析实际业务项", funnelBoard.SelectedPoint is { Index: 1 } value && ReferenceEquals(value.Item, board.Rows[1]));
                Check("矩阵区分类型键与显式缺失及无组合", heatBoard.RowKeys.Count == 2 && heatBoard.RowKeys[0] is int and 1 && heatBoard.RowKeys[1] is string and "1" && heatBoard.GetMarks().Count == 5 && heatBoard.GetMarks().Single(mark => mark.Data.SourceIndex == 2).Data.IsEmpty);
                var pageImage = Image("page"); SaveWindow(pageImage);
                board.Labels.IsChecked = false; board.Selection.IsChecked = false; board.Gap.Value = 12; board.Neck.Value = .5; board.UpdateData(); FlushControlTemplateWindow(window);
                Check("用户标签选择间距颈宽控制生效", board.Analysis.All(chart => !chart.ShowLabels && !chart.ShowValues && !chart.IsSelectionEnabled) && funnelBoard.SegmentGap == 12 && funnelBoard.NeckWidth == .5 && ((PyramidChart)board.Analysis[1]).SegmentGap == 12 && heatBoard.CellGap == 12 && board.Cartesian.Take(3).All(chart => chart.SelectionMode == ChartSelectionMode.None));
                Check("实际业务INPC和河流重算生效", funnelBoard.GetMarks().Single(mark => mark.Data.SourceIndex == 1).Data.Point.Y == 105 && heatBoard.GetMarks().Single(mark => mark.Data.SourceIndex == 1).Data.Point.Y == 95 && board.Pictorial.All(series => series.CreateSnapshot().Points[1].Point.Y == -25) && board.River[1].CreateSnapshot().Points[0].Point.Y == 25 && board.River[0].CreateSnapshot().Points[0].Point.Y == 27.5);
                var changedPageImage = Image("page-changed"); SaveWindow(changedPageImage);
                const string m = "#D7259F", t = "#19A798", low = "#2040A0", mid = "#807060", high = "#E0A020", missing = "#F2A33A";
                var magenta = Brush(m);
                var cases = new List<(string Name, ChartBase Chart, (double X, double Y, string Color)[] Positive, (double X, double Y, string Color)[] Negative)>();
                var firstFunnel = Funnel([100, 50, 25], .2, 0);
                cases.Add(("funnel-widths", firstFunnel, [(210, 40, m), (210, 140, m)], [(40, 220, m)]));
                cases.Add(("funnel-gap", Funnel([100, 50, 25], .2, 12), [(210, 40, m)], [(210, 280d / 3, m)]));
                cases.Add(("funnel-neck", Funnel([1], .5, 0), [(120, 260, m)], [(50, 260, m)]));
                cases.Add(("pyramid-area", Pyramid([1, 3, 5]), [(210, 30, m), (300, 220, m)], [(20, 140, m)]));
                cases.Add(("pyramid-skip", Pyramid([0, -1, 1, double.NaN, 3]), [(210, 70, m), (100, 240, m)], [(20, 100, m)]));
                cases.Add(("heatmap-colors", Heat([new("a", "R", "A", 0), new("b", "R", "B", 50), new("c", "R", "C", 100)]), [(70, 140, low), (210, 140, mid), (350, 140, high)], [(140, 140, low), (140, 140, mid)]));
                cases.Add(("heatmap-missing", Heat([new("a", "R1", "C1", 0), new("b", "R1", "C2", double.NaN), new("c", "R2", "C1", 100)], 0), [(105, 70, low), (315, 70, missing), (105, 210, high)], [(315, 210, low), (315, 210, high), (315, 210, missing)]));
                cases.Add(("heatmap-typed", Heat([new("a", 1, true, 0), new("b", "1", true, 100)], 0), [(210, 70, low), (210, 210, high)], [(210, 70, high), (210, 210, low)]));
                cases.Add(("pict-stretch", Pict(PictorialBarRenderMode.Stretch), [(107.5, 100, m)], [(107.5, 180, m)]));
                cases.Add(("pict-stretch-negative", Pict(PictorialBarRenderMode.Stretch, negative: true), [(306.5, 190, m)], [(306.5, 100, m)]));
                cases.Add(("pict-hole", Pict(PictorialBarRenderMode.Stretch, hole: true), [(93, 100, m)], [(107.5, 103, m)]));
                cases.Add(("pict-clip", Pict(PictorialBarRenderMode.Clip), [(107.5, 130, m)], [(107.5, 80, m)]));
                cases.Add(("pict-clip-negative", Pict(PictorialBarRenderMode.Clip, negative: true), [(306.5, 160, m)], [(306.5, 210, m)]));
                var repeated = Pict(PictorialBarRenderMode.Repeat);
                cases.Add(("pict-repeat", repeated, [(107.5, 130, m), (107.5, 98, m), (107.5, 70, m)], [(107.5, 114, m), (107.5, 60, m)]));
                cases.Add(("pict-repeat-negative", Pict(PictorialBarRenderMode.Repeat, negative: true), [(306.5, 154, m), (306.5, 186, m), (306.5, 214, m)], [(306.5, 170, m), (306.5, 226, m)]));
                cases.Add(("pict-repeat-hole", Pict(PictorialBarRenderMode.Repeat, hole: true), [(93, 130, m)], [(107.5, 130, m), (107.5, 114, m)]));
                cases.Add(("river-centered", River(20, 40), [(207, 194, m), (207, 116, t)], [(207, 246, m), (207, 246, t)]));
                cases.Add(("river-updated", River(40, 20), [(207, 142, m), (207, 90, t)], [(207, 246, m), (207, 246, t)]));
                foreach (var item in cases)
                {
                    window.Content = item.Chart; FlushControlTemplateWindow(window);
                    if (item.Chart is CartesianChart cart) Check(item.Name + "固定隐藏坐标轴绘图区", cart.PlotArea == new Rect(8, 12, 398, 260));
                    var image = Image(item.Name); var raster = CircularCapture(item.Chart, image);
                    object[] Read((double X, double Y, string Color)[] points, bool positive) => points.Select(point => { var count = Ink(raster, point); return (object)new { x = point.X, y = point.Y, color = point.Color, inkPixels = count, minimumPixels = positive ? 6 : 0, maximumPixels = positive ? (int?)null : 0 }; }).ToArray();
                    var positive = Read(item.Positive, true); var negative = Read(item.Negative, false);
                    var pixelPass = item.Positive.All(point => Ink(raster, point) >= 6) && item.Negative.All(point => Ink(raster, point) == 0);
                    Check(item.Name + "独立公式正负像素", pixelPass && item.Chart.Bounds.Width == 420 && item.Chart.Bounds.Height == 280);
                    if (item.Chart is AnalysisChartBase analytic)
                    {
                        var anchor = item.Positive[0]; var real = analytic.HitTest(new(anchor.X, anchor.Y));
                        Check(item.Name + "正向实际图形可命中", real is not null && ReferenceEquals(real.Series, analytic.Series));
                    }
                    if (item.Name is "pict-hole" or "pict-repeat-hole") Check(item.Name + "孔洞和间隙不命中", ((CartesianChart)item.Chart).HitTestPoint(new(107.5, item.Name == "pict-hole" ? 103 : 130), 0) is null);
                    if (item.Name == "pict-repeat") Check("重复末段和间隙命中真实轮廓", repeated.HitTestPoint(new(107.5, 70), 0) is not null && repeated.HitTestPoint(new(107.5, 114), 0) is null);
                    using var stream = new MemoryStream(); var exported = Export(item.Chart, stream, 96); var exportImage = Image(item.Name + "-export-96"); File.WriteAllBytes(exportImage, stream.ToArray()); stream.Position = 0; using var decoded = new Bitmap(stream); var exportRaster = ReadShapePixels(decoded);
                    var changedPixels = CountControlTemplateExactChanges(raster, exportRaster, new Rect(0, 0, 420, 280)); var density = Density(stream.ToArray(), 96);
                    Check(item.Name + "96DPI导出原生像素完全一致", exported && changedPixels == 0 && density && decoded.PixelSize == new PixelSize(420, 280));
                    probes.Add(new { scenario = item.Name, image, exportImage, width = raster.Width, height = raster.Height, controlBounds = item.Chart.Bounds, positive, negative, pixelPass, exported, changedPixels, densityMatches = density });
                }
                foreach (var representative in new (string Name, ChartBase Chart)[] { ("funnel-widths", firstFunnel), ("pict-repeat", repeated) })
                {
                    window.Content = representative.Chart; FlushControlTemplateWindow(window); var baselineImage = Image(representative.Name); using var baselineBitmap = new Bitmap(baselineImage); var baseline = ReadShapePixels(baselineBitmap);
                    foreach (var dpi in new[] { 144d, 192d })
                    {
                        using var stream = new MemoryStream(); var exported = Export(representative.Chart, stream, dpi); var image = Image($"{representative.Name}-export-{dpi:0}"); File.WriteAllBytes(image, stream.ToArray()); stream.Position = 0; using var bitmap = new Bitmap(stream); var raster = ReadShapePixels(bitmap); var changedPixels = CountControlTemplateExactChanges(baseline, raster, new Rect(0, 0, 420, 280)); var density = Density(stream.ToArray(), dpi);
                        Check($"{representative.Name}{dpi:0}DPI尺寸密度和原生像素一致", exported && changedPixels == 0 && density && bitmap.PixelSize == new PixelSize(420, 280));
                        exports.Add(new { scenario = representative.Name, baselineImage, image, dpi, exported, changedPixels, densityMatches = density, width = raster.Width, height = raster.Height });
                    }
                }
                using (var stream = new MemoryStream()) { var exported = await firstFunnel.TryExportJpegAsync(stream, new ChartExportOptions { PixelSize = new PixelSize(420, 280), Background = window.Background, JpegQuality = 88 }); stream.Position = 0; using var bitmap = new Bitmap(stream); Check("分析图异步JPEG真实尺寸", exported && bitmap.PixelSize == new PixelSize(420, 280)); }
                using (var stream = new MemoryStream()) { var canceled = false; try { await firstFunnel.TryExportPngAsync(stream, null, new CancellationToken(true)); } catch (OperationCanceledException) { canceled = true; } Check("分析图异步预取消不写入", canceled && stream.Length == 0); }
                _analysisEvidence.Add(new { theme = label, nativeWindowCreated = native, pageImage, changedPageImage, layouts, probes, exports, physicalInputValidated = false, performanceMeasured = false, oracle = new { registration = "analysis-oracle-preregistered-v1.json", sha256 = "28248043908BE341459FE7FD9C15F388694C16173F20BAE4AE24F130B6EC9A4F", explicitControlWidth = 420, explicitControlHeight = 280, pixelSquareDip = 6, colorChannelTolerance = 24, alphaMinimum = 240, minimumInk = 6 }, evidenceKind = "native-window-rendertargetbitmap-preregistered-independent-analysis-business-oracles-and-public-contracts" });
                foreach (var item in cases) { if (item.Chart is AnalysisChartBase chart) chart.ItemsSource = null; else ((CartesianChart)item.Chart).Series.Clear(); }

                T Analysis<T>(T chart) where T : AnalysisChartBase { chart.Width = 420; chart.Height = 280; chart.Padding = default; chart.Title = null; chart.ShowLabels = chart.ShowValues = chart.IsTooltipEnabled = chart.IsSelectionEnabled = false; chart.Palette = [magenta]; chart.Background = window.Background; chart.Foreground = window.Foreground; return chart; }
                FunnelChart Funnel(double[] values, double neck, double gap) => Analysis(new FunnelChart { ItemsSource = values, NeckWidth = neck, SegmentGap = gap });
                PyramidChart Pyramid(double[] values) => Analysis(new PyramidChart { ItemsSource = values, SegmentGap = 0 });
                HeatmapChart Heat(AnalysisDemoCell[] values, double gap = 8) => Analysis(new HeatmapChart { ItemsSource = values, RowPath = "Row", ColumnPath = "Column", ValuePath = "Value", IdPath = "Id", Minimum = 0, Maximum = 100, CellGap = gap, LowColor = Color.Parse(low), HighColor = Color.Parse(high), MissingBrush = Brush(missing), ShowAxes = false, ShowColorScale = false });
                CartesianChart Cartesian(double xMaximum, double yMinimum, double yMaximum) => new() { Width = 420, Height = 280, Padding = default, Background = window.Background, Foreground = window.Foreground, ShowLegend = false, ShowGridLines = false, IsTooltipEnabled = false, SelectionMode = ChartSelectionMode.None, XAxis = new NumericAxis { Minimum = 0, Maximum = xMaximum, IsVisible = false }, YAxis = new NumericAxis { Minimum = yMinimum, Maximum = yMaximum, IsVisible = false } };
                CartesianChart Pict(PictorialBarRenderMode mode, bool negative = false, bool hole = false)
                {
                    var chart = Cartesian(4, -100, 100); Geometry geometry = new RectangleGeometry(new Rect(0, 0, 1, 1)); if (hole) geometry = new CombinedGeometry(GeometryCombineMode.Exclude, geometry, new RectangleGeometry(new Rect(.25, .25, .5, .5)));
                    chart.Series.Add(new PictorialBarSeries { ItemsSource = new ChartPoint[] { new(1, 60), new(3, -60) }, RenderMode = mode, Geometry = geometry, BarWidth = 40, Fill = magenta, StrokeThickness = 0, SymbolSize = mode == PictorialBarRenderMode.Clip ? 40 : 24, SymbolSpacing = 8 }); return chart;
                }
                CartesianChart River(double first, double second)
                {
                    var chart = Cartesian(2, 0, 100); var areas = new[] { new AreaSeries { Fill = Brushes.Transparent, Stroke = Brushes.Transparent, ShowInLegend = false }, new AreaSeries { Fill = magenta, Stroke = magenta }, new AreaSeries { Fill = Brush(t), Stroke = Brush(t) } };
                    foreach (var series in areas) { series.StackMode = ChartStackMode.Stacked; series.StackGroup = "native-analysis-river"; series.StrokeThickness = 0; series.ShowMarkers = false; chart.Series.Add(series); } SetAnalysisRiver(areas, [first, first, first], [second, second, second]); return chart;
                }
                bool Export(ChartBase chart, Stream stream, double dpi) { var options = new ChartExportOptions { PixelSize = new PixelSize(420, 280), Dpi = dpi, Background = window.Background }; return chart is AnalysisChartBase analysis ? analysis.TryExportPng(stream, options) : ((CartesianChart)chart).TryExportPng(stream, options); }
            }
            finally { window.Close(); }
            void SaveWindow(string path) { using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)); bitmap.Render(window); bitmap.Save(path); }
        }
        _analysisExamples.IsExpanded = false;
        static int Ink(ShapeRaster raster, (double X, double Y, string Color) point)
        {
            var rectangle = TemplatePixelRect(new Rect(point.X - 3, point.Y - 3, 6, 6)); var color = Color.Parse(point.Color); var count = 0;
            for (var y = Math.Max(0, rectangle.Y); y < Math.Min(raster.Height, rectangle.Bottom); y++) for (var x = Math.Max(0, rectangle.X); x < Math.Min(raster.Width, rectangle.Right); x++)
            { var offset = y * raster.Stride + x * 4; if (raster.Pixels[offset + 3] >= 240 && Math.Abs(raster.Pixels[offset] - color.B) <= 24 && Math.Abs(raster.Pixels[offset + 1] - color.G) <= 24 && Math.Abs(raster.Pixels[offset + 2] - color.R) <= 24) count++; }
            return count;
        }
        static bool Density(byte[] bytes, double dpi) => TryReadCommonPngDensity(bytes, out var x, out var y, out var unit) && x == (uint)Math.Round(dpi / .0254) && x == y && unit == 1;
    }
}
