using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _scatterBubbleEvidence = [];

    private async Task RunScatterBubbleSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _scatterBubbleExamples!.IsExpanded = true; await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
            string Image(string scenario) => Path.Combine(directory, stem + $"-scatter-bubble-{(dark ? "dark" : "light")}-{scenario}.png");
            void Check(string name, bool value) => checks[$"散点气泡{label}{name}"] = value;
            using var board = CreateScatterBubbleBoard(false);
            var window = new Window { Title = "Charts · 散点与气泡", Width = 1160, Height = 1100, RequestedThemeVariant = theme,
                Background = Brush(dark ? "#111722" : "#F8FAFC"), Foreground = Brush(dark ? "#EAF0FB" : "#172238"), Content = new Border { Padding = new Thickness(16), Child = board.View } };
            CartesianChart? chart = null; var probes = new List<object>(); var exports = new List<object>();
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                var native = window.TryGetPlatformHandle() is { Handle: not 0 };
                var boardLayouts = board.Charts.Select(value => new { value.Name, value.Bounds, value.PlotArea }).ToArray();
                Check("六图原生布局与真实业务源", native && board.Charts.All(value => value.Bounds.Width > 400 && value.PlotArea.Height > 100) && ReferenceEquals(board.Scatter.CreateSnapshot().Points[0].Item, board.Records[0]));
                var pageImage = Image("page"); SaveWindow(pageImage);
                board.SizeScale.SelectedItem = ChartSizeScale.Diameter; board.DrawOrder.SelectedItem = ChartPointDrawOrder.SizeAscending; board.Labels.IsChecked = true; board.ShowLine.IsChecked = false;
                FlushControlTemplateWindow(window);
                Check("用户尺度排序标签连线控制生效", board.Scatter.SizeScale == ChartSizeScale.Diameter && board.Area.SizeScale == ChartSizeScale.Diameter && board.Overlap.DrawOrder == ChartPointDrawOrder.SizeAscending && !board.Line.ShowLine && board.Charts.SelectMany(value => value.Series).All(value => value.ShowDataLabels));
                var changedPageImage = Image("page-diameter-overlap"); SaveWindow(changedPageImage);
                chart = new CartesianChart { Background = window.Background, Foreground = window.Foreground, Margin = new Thickness(12), ShowLegend = false, ShowGridLines = false,
                    IsZoomEnabled = true, IsPanEnabled = true, SelectionMode = ChartSelectionMode.Multiple, IsKeyboardNavigationEnabled = true,
                    XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
                window.Content = chart;
                var records = new[] { new ScatterBubbleRecord("oracle-a", 2, 25, 0, 0), new ScatterBubbleRecord("oracle-b", 5, 55, 50, 50), new ScatterBubbleRecord("oracle-c", 8, 75, 100, 100) };
                var scatter = new ScatterSeries { ItemsSource = records, CategoryPath = nameof(ScatterBubbleRecord.X), ValuePath = nameof(ScatterBubbleRecord.Y), IdPath = nameof(ScatterBubbleRecord.Id), SizePath = nameof(ScatterBubbleRecord.Size), MinimumSize = 20, MaximumSize = 60, SizeMinimum = 0, SizeMaximum = 100, SizeScale = ChartSizeScale.Area, Fill = Blue, MarkerStrokeThickness = 0 };
                chart.Series.Add(scatter); FlushControlTemplateWindow(window);
                Check("固定业务坐标轴范围独立", chart.VisibleRange is { XMinimum: 0, XMaximum: 10, YMinimum: 0, YMaximum: 100 });
                for (var index = 0; index < records.Length; index++)
                {
                    var item = records[index]; var diameter = Math.Sqrt(400 + item.Size / 100 * 3200);
                    Probe("scatter-size-" + Suffix(index), new(item.X, item.Y), new(item.X, item.Y), Color.Parse("#4385F4"), new(diameter / 2 - 5, 0), new(diameter / 2 + 7, 0), diameter);
                }
                scatter.ColorPath = nameof(ScatterBubbleRecord.Color); scatter.ColorMinimum = 0; scatter.ColorMaximum = 100; scatter.MinimumColor = Colors.Blue; scatter.MaximumColor = Colors.Red;
                FlushControlTemplateWindow(window);
                var colors = new[] { Colors.Blue, Color.FromRgb(128, 0, 128), Colors.Red };
                for (var index = 0; index < records.Length; index++)
                {
                    var item = records[index]; var diameter = Math.Sqrt(400 + item.Size / 100 * 3200);
                    Probe("scatter-color-" + Suffix(index), new(item.X, item.Y), new(item.X, item.Y), colors[index], default, new(diameter / 2 + 7, 0), diameter);
                }
                var root = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(chart)!;
                var encoded = root.GetPointPeer(scatter, 1)?.CurrentPoint;
                Check("编码命中保留原XY源索引身份及原值", encoded is { Index: 1, Point.X: 5, Point.Y: 55, SizeValue: 50 } && Equals(encoded.ColorValue, 50d) && Near(encoded.MarkerDiameter, Math.Sqrt(2000)) && ReferenceEquals(encoded.Item, records[1]) && Equals(encoded.StableId, "oracle-b"));
                if (encoded is not null) chart.SelectedPoints = new[] { encoded }; FlushControlTemplateWindow(window);
                Check("选择保留编码元数据与原始对象", chart.SelectedPoint is { SizeValue: 50 } selected && ReferenceEquals(selected.Item, records[1]) && Near(selected.MarkerDiameter, Math.Sqrt(2000)));
                chart.ClearSelection(); chart.Series.Clear();
                var line = new ScatterLineSeries { Stroke = Teal, Fill = Teal, StrokeThickness = 3, MarkerSize = 0, ItemsSource = new ChartPoint[] { new(1, 20), new(7, 80), new(3, 60), new(4, double.NaN), new(7, 20), new(9, 40) } };
                chart.Series.Add(line); FlushControlTemplateWindow(window);
                Probe("scatter-line-source-order", new(4, 50), new(4, 75), Color.Parse("#19A798"), default, default, null, true);
                Probe("scatter-line-gap", new(8, 30), new(5, 40), Color.Parse("#19A798"), default, default, null, true);
                var segment = chart.HitTestSegment(Project(new(4, 50)), 3);
                Check("ScatterLine线段插值沿真实源顺序", segment is { Start.Index: 0, End.Index: 1 } && Near(segment.Point.X, 4) && Near(segment.Point.Y, 50) && Near(segment.Fraction, .5));
                chart.TrackballMode = ChartTrackballMode.Vertical; chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
                HoverTemplateChart(chart, window, Project(new(4, 50))); FlushControlTemplateWindow(window);
                Check("ScatterLine完整Trackball返回真实两端点", chart.TrackballReadings.Any(reading => ReferenceEquals(reading.Series, line) && reading.IsInterpolated && reading.Segment is { Start.Index: 0, End.Index: 1 }));
                chart.TrackballMode = ChartTrackballMode.None; chart.IsTooltipEnabled = false; FlushControlTemplateWindow(window);
                line.ShowLine = false; FlushControlTemplateWindow(window);
                Check("ScatterLine关闭连线停止线段命中", chart.HitTestSegment(Project(new(4, 50)), 3) is null);
                chart.Series.Clear();
                var bubble = new BubbleSeries { ItemsSource = records, CategoryPath = nameof(ScatterBubbleRecord.X), ValuePath = nameof(ScatterBubbleRecord.Y), IdPath = nameof(ScatterBubbleRecord.Id), WeightPath = nameof(ScatterBubbleRecord.Size), MinimumSize = 20, MaximumSize = 60, SizeMinimum = 0, SizeMaximum = 100, Fill = Blue, MarkerStrokeThickness = 0 };
                chart.Series.Add(bubble); FlushControlTemplateWindow(window);
                for (var index = 0; index < records.Length; index++)
                {
                    var item = records[index]; var diameter = Math.Sqrt(400 + item.Size / 100 * 3200);
                    Probe("bubble-area-" + Suffix(index), new(item.X, item.Y), new(item.X, item.Y), Color.Parse("#4385F4"), new(diameter / 2 - 5, 0), new(diameter / 2 + 7, 0), diameter);
                }
                var sourceIdentity = bubble.CreateSnapshot().Points[1];
                Check("气泡WeightPath兼容并保持真实XY", sourceIdentity.Point == new ChartPoint(5, 55) && sourceIdentity.Weight == 50 && ReferenceEquals(sourceIdentity.Item, records[1]) && Equals(sourceIdentity.StableId, "oracle-b"));
                var fullImage = Image("bubble-area-full"); var baseline = CaptureControlTemplateRaster(chart, fullImage);
                foreach (var dpi in new[] { 96d, 144d, 192d })
                {
                    using var stream = new MemoryStream(); var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(baseline.Width, baseline.Height), Dpi = dpi, Background = chart.Background });
                    var exportImage = Image($"bubble-area-export-{dpi:0}"); File.WriteAllBytes(exportImage, stream.ToArray()); stream.Position = 0;
                    using var bitmap = new Bitmap(stream); var raster = ReadShapePixels(bitmap); var changed = CountControlTemplateExactChanges(baseline, raster, chart.PlotArea);
                    var density = TryReadCommonPngDensity(stream.ToArray(), out var dx, out var dy, out var unit) && dx == (uint)Math.Round(dpi / .0254) && dx == dy && unit == 1;
                    Check($"气泡{dpi:0}DPI导出保持编码像素与密度", exported && changed == 0 && density && raster.Width == baseline.Width && raster.Height == baseline.Height);
                    exports.Add(new { dpi, baselineImage = fullImage, image = exportImage, exported, width = raster.Width, height = raster.Height, plotArea = chart.PlotArea, visibleRange = chart.VisibleRange, changedPixels = changed, densityMatches = density });
                }
                bubble.SizeScale = ChartSizeScale.Diameter; FlushControlTemplateWindow(window);
                Probe("bubble-diameter-mid", new(5, 55), new(5, 55), Color.Parse("#4385F4"), new(15, 0), new(27, 0), 40);
                var originalRange = chart.VisibleRange; chart.Viewport = new ChartViewport(1, 9, 10, 90); FlushControlTemplateWindow(window);
                var zoomHit = chart.HitTestPoint(Project(new(5, 55)), 0);
                Check("气泡缩放保持DIP直径与原始身份", chart.VisibleRange != originalRange && zoomHit is { Index: 1, SizeValue: 50, MarkerDiameter: 40 } && ReferenceEquals(zoomHit.Item, records[1]));
                chart.ResetViewport(); chart.Series.Clear();
                var overlapRows = new[] { new ScatterBubbleDirectRecord("earlier-blue", 5, 50, 100, "#4385F4"), new ScatterBubbleDirectRecord("later-teal", 5, 50, 25, "#19A798") };
                bubble = new BubbleSeries { ItemsSource = overlapRows, CategoryPath = nameof(ScatterBubbleDirectRecord.X), ValuePath = nameof(ScatterBubbleDirectRecord.Y), IdPath = nameof(ScatterBubbleDirectRecord.Id), SizePath = nameof(ScatterBubbleDirectRecord.Size), ColorPath = nameof(ScatterBubbleDirectRecord.Color), MinimumSize = 20, MaximumSize = 60, SizeMinimum = 0, SizeMaximum = 100, MarkerStrokeThickness = 0 };
                chart.Series.Add(bubble); FlushControlTemplateWindow(window);
                Probe("bubble-overlap-top", new(5, 50), new(5, 50), Color.Parse("#19A798"), default, default, null, false, Color.Parse("#4385F4"));
                Check("重叠填充内部同距按后绘制源项命中", chart.HitTestPoint(Project(new(5, 50)), 0) is { Index: 1 } sourceTop && ReferenceEquals(sourceTop.Item, overlapRows[1]));
                bubble.DrawOrder = ChartPointDrawOrder.SizeAscending; FlushControlTemplateWindow(window);
                Check("大小升序改变顶层命中但不改变源身份", chart.HitTestPoint(Project(new(5, 50)), 0) is { Index: 0 } sizeTop && ReferenceEquals(sizeTop.Item, overlapRows[0]) && bubble.CreateSnapshot().Points[0].SourceIndex == 0);
                chart.Series.Clear();
                var clipRows = new[] { new ScatterBubbleRecord("outside-center", -.1, 50, 100, 0) };
                bubble = new BubbleSeries { ItemsSource = clipRows, CategoryPath = nameof(ScatterBubbleRecord.X), ValuePath = nameof(ScatterBubbleRecord.Y), SizePath = nameof(ScatterBubbleRecord.Size), MinimumSize = 20, MaximumSize = 60, SizeMinimum = 0, SizeMaximum = 100, Fill = Blue, MarkerStrokeThickness = 0 };
                chart.Series.Add(bubble); FlushControlTemplateWindow(window);
                Probe("bubble-outside-center-clip", new(.1, 50), new(.5, 70), Color.Parse("#4385F4"), default, default, 60);
                Check("中心在视口外仍可命中可见气泡部分", chart.HitTestPoint(Project(new(.1, 50)), 0) is { Index: 0, Point.X: -.1 } clipped && ReferenceEquals(clipped.Item, clipRows[0]));
                _scatterBubbleEvidence.Add(new { theme = label, nativeWindowCreated = native, pageImage, changedPageImage, boardLayouts, probes, exports, pixelSquareDip = 6, colorChannelTolerance = 24,
                    fixedAxes = new { xMinimum = 0, xMaximum = 10, yMinimum = 0, yMaximum = 100 }, sourceIdentityExact = true, derivedMetadataAbsoluteTolerance = 1e-9, physicalInputValidated = false, performanceMeasured = false,
                    evidenceKind = "native-window-rendertargetbitmap-preregistered-business-algebra-positive-negative-pixels-and-public-contracts" });
            }
            finally { window.Close(); chart?.Series.Clear(); }

            void SaveWindow(string path) { using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)); bitmap.Render(window); bitmap.Save(path); }
            Point Project(ChartPoint point) { var plot = chart!.PlotArea; var range = chart.VisibleRange!.Value; return new(plot.Left + (point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width, plot.Bottom - (point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height); }
            void Probe(string scenario, ChartPoint positive, ChartPoint negative, Color color, Point positiveOffsetDip, Point negativeOffsetDip, double? expectedDiameter, bool stroke = false, Color? negativeColor = null)
            {
                var image = Image(scenario); var raster = CaptureControlTemplateRaster(chart!, image);
                var p = Project(positive); var n = Project(negative); var positivePixel = new Point(p.X + positiveOffsetDip.X, p.Y + positiveOffsetDip.Y); var negativePixel = new Point(n.X + negativeOffsetDip.X, n.Y + negativeOffsetDip.Y);
                int Ink(Point point, Color brush) => raster.Count(TemplatePixelRect(new Rect(point.X - 3, point.Y - 3, 6, 6)), brush, 24);
                var positiveInk = Ink(positivePixel, color); var negativeInk = Ink(negativePixel, negativeColor ?? color); var minimumInk = stroke ? 4 : 10;
                Check(scenario + "独立数学坐标正负像素", positiveInk >= minimumInk && negativeInk == 0);
                probes.Add(new { scenario, image, width = raster.Width, height = raster.Height, plotArea = chart!.PlotArea, visibleRange = chart.VisibleRange, positive, negative, positiveOffsetDip, negativeOffsetDip, expectedDiameter,
                    color = color.ToString(), negativeColor = (negativeColor ?? color).ToString(), positivePixel, negativePixel, positiveInkPixels = positiveInk, negativeInkPixels = negativeInk, minimumInk, pixelSquareDip = 6, colorChannelTolerance = 24 });
            }
        }
        _scatterBubbleExamples.IsExpanded = false;
        static string Suffix(int index) => index == 0 ? "low" : index == 1 ? "mid" : "high";
        static bool Near(double? value, double expected) => value is { } number && double.IsFinite(number) && Math.Abs(number - expected) <= 1e-9;
    }
}
