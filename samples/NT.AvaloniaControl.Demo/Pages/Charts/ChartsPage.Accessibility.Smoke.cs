using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<AccessibilitySmokeEvidence> _accessibilityEvidence = [];

    private async Task RunAccessibilitySmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _accessibilityExamples!.IsExpanded = true; await SmokeFlushAsync();
        _accessibilityExamples.BringIntoView(); await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var token = dark ? "dark" : "light"; var label = dark ? "暗色" : "亮色";
            using var board = CreateAccessibilityBoard(false); var chart = board.Chart;
            var window = new Window { Title = "Charts · 辅助功能完整性", Width = 1160, Height = 840, RequestedThemeVariant = theme,
                Content = new Border { Padding = new Thickness(18), Child = board.View } };
            string Image(string suffix, string extension = "png") => Path.Combine(directory, stem + $"-accessibility-{token}-{suffix}.{extension}");
            void Check(string name, bool passed) => checks[$"辅助功能{label}{name}"] = passed;
            var keys = new List<AccessibilityKeyEvidence>(); var nodes = new List<AccessibilityNodeEvidence>(); var images = new List<AccessibilityImageEvidence>();
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                _ = chart.PlotArea; FlushControlTemplateWindow(window);
                var native = window.TryGetPlatformHandle() is { Handle: not 0 };
                Check("原生窗口及三系列多轴有效布局", native && chart.Bounds.Width > 700 && chart.PlotArea.Height > 160 && chart.Series.Count == 3 && chart.Axes.Count == 1);
                var root = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(chart)!;
                var rootChildren = root.GetChildren();
                Check("系列辅助节点及摘要完整计数", rootChildren.Count == 3 && chart.AccessibilitySummary.TotalCount == 10 && chart.AccessibilitySummary.ValidCount == 9 && chart.AccessibilitySummary.MissingCount == 1 &&
                    chart.AccessibilitySummary.Axes.Count >= 3 && board.Summary.Text == chart.AccessibilitySummaryText);
                var linePage = root.GetPointPage(board.Line, 0, 4);
                Check("按源索引分页保留业务身份并跳过缺失点", linePage.Count == 3 && linePage.Select(peer => peer.CurrentPoint?.Index).SequenceEqual(new int?[] { 0, 2, 3 }) &&
                    root.GetPointPeer(board.Line, 1) is null && linePage.All(peer => peer.CurrentPoint is { } hit && ReferenceEquals(hit.Item, board.Records[hit.Index]) && Equals(hit.StableId, board.Records[hit.Index].Id)));
                foreach (var series in chart.Series)
                    foreach (var point in root.GetPointPage(series, 0, 16)) nodes.Add(Node("initial", point));
                Check("点名称数值状态与原始数据一致", nodes.Count == 9 && nodes.All(node => node.Name.Contains(node.Series!, StringComparison.Ordinal) && node.Value.Length > 0 && node.Enabled && node.SourceIndex >= 0));
                var businessPeer = root.GetPointPeer(board.Line, 2)!; var businessIdentity = businessPeer.GetAutomationId();
                chart.SelectionMode = ChartSelectionMode.None; chart.Focus();
                KeyStep(Key.Right); Check("禁用选择仍可浏览首点", chart.CurrentPoint is { Index: 0 } first && ReferenceEquals(first.Item, board.Records[0]) && chart.SelectedPoint is null);
                KeyStep(Key.Right); Check("同系列方向键跳过缺失源行", chart.CurrentPoint is { Index: 2 } second && ReferenceEquals(second.Item, board.Records[2]) && Equals(second.StableId, "load-b"));
                KeyStep(Key.Left); Check("左方向键跳过缺失行回到前点", chart.CurrentPoint is { Index: 0 } && ReferenceEquals(chart.CurrentPoint.Series, board.Line));
                KeyStep(Key.Right); KeyStep(Key.PageDown); Check("PageDown在短系列边界停于末点", chart.CurrentPoint is { Index: 3 } && ReferenceEquals(chart.CurrentPoint.Series, board.Line));
                KeyStep(Key.PageUp); Check("PageUp在短系列边界停于首点", chart.CurrentPoint is { Index: 0 } && ReferenceEquals(chart.CurrentPoint.Series, board.Line));
                KeyStep(Key.End); Check("End浏览系列末点", ReferenceEquals(chart.CurrentPoint?.Series, board.Line) && chart.CurrentPoint?.Index == 3);
                KeyStep(Key.Home); Check("Home浏览系列首点", ReferenceEquals(chart.CurrentPoint?.Series, board.Line) && chart.CurrentPoint?.Index == 0);
                KeyStep(Key.Down); Check("下方向键切换到下一系列", ReferenceEquals(chart.CurrentPoint?.Series, board.Bars));
                KeyStep(Key.Down); Check("下方向键到次轴散点保留原值", ReferenceEquals(chart.CurrentPoint?.Series, board.Scatter) && chart.CurrentPoint is { Point.Y: >= 1000 and <= 2000 });
                KeyStep(Key.Up); Check("上方向键返回前一系列", ReferenceEquals(chart.CurrentPoint?.Series, board.Bars));
                chart.NavigateToPoint(board.Line, 2); FlushControlTemplateWindow(window);
                Check("公开定位点显示当前业务读数", chart.CurrentPoint is { Index: 2 } && board.Active.Text?.Contains("load-b", StringComparison.Ordinal) == true);
                nodes.Add(Node("focused", businessPeer));
                board.Records[2].Y = 78; _ = chart.PlotArea; FlushControlTemplateWindow(window);
                Check("业务属性变更刷新当前点与辅助节点", chart.CurrentPoint is { Point.Y: 78 } && businessPeer.CurrentPoint is { Point.Y: 78 } &&
                    businessPeer.GetAutomationId() == businessIdentity && ReferenceEquals(businessPeer.CurrentPoint.Item, board.Records[2]));
                nodes.Add(Node("after-value-update", businessPeer));
                board.Records[2].Y = 65; _ = chart.PlotArea; FlushControlTemplateWindow(window);
                chart.SelectionMode = ChartSelectionMode.Multiple;
                KeyStep(Key.Space); Check("Space选中当前点并提供选择语义", chart.SelectedPoint is { Index: 2 } && businessPeer.GetProvider<ISelectionItemProvider>() is { IsSelected: true });
                nodes.Add(Node("selected", businessPeer));
                chart.NavigateToPoint(board.Line, 3); KeyStep(Key.Space, KeyModifiers.Control);
                Check("CtrlSpace追加选择保留两点", chart.SelectedPoints?.Count == 2 && chart.SelectedPoints.All(hit => ReferenceEquals(hit.Series, board.Line)));
                KeyStep(Key.Space, KeyModifiers.Control); Check("CtrlSpace取消单点仍保留前一选择", chart.SelectedPoints?.Count == 1 && chart.SelectedPoint?.Index == 2);
                var invoked = 0; chart.PointInvokedCommand = new AccessibilityActionCommand(_ => invoked++);
                KeyStep(Key.Enter); Check("Enter调用当前点命令", invoked == 1);
                businessPeer.GetProvider<IInvokeProvider>()!.Invoke(); Check("辅助节点Invoke调用业务命令", invoked == 2);
                chart.PointInvokedCommand = null;
                chart.ClearSelection();
                EventHandler<ChartSelectionChangingEventArgs> cancelSelection = (_, args) => { if (args.CanCancel) args.Cancel = true; };
                chart.SelectionChanging += cancelSelection; businessPeer.GetProvider<ISelectionItemProvider>()!.Select(); FlushControlTemplateWindow(window);
                Check("点辅助选择尊重可取消通知", chart.SelectedPoint is null && businessPeer.GetProvider<ISelectionItemProvider>() is { IsSelected: false });
                chart.SelectionChanging -= cancelSelection;
                businessPeer.GetProvider<ISelectionItemProvider>()!.Select(); FlushControlTemplateWindow(window);
                KeyStep(Key.Escape); Check("Esc清当前读数但保留选择", chart.CurrentPoint is null && chart.SelectedPoint?.Index == 2);
                chart.ClearSelection(); chart.Viewport = new ChartViewport(10, 70, 0, 100); FlushControlTemplateWindow(window);
                KeyStep(Key.Home, KeyModifiers.Control); Check("CtrlHome复位图表视图", chart.Viewport is null);
                var originals = board.Line.ItemsSource;
                var replacements = new[] { new AccessibilityRecord("load-a", "新开始", 15, 25), new AccessibilityRecord("load-invalid", "缺失", 30, double.NaN),
                    new AccessibilityRecord("load-b", "新检查", 45, 72), new AccessibilityRecord("load-c", "新结束", 80, 40) };
                board.Line.ItemsSource = replacements; _ = chart.PlotArea; FlushControlTemplateWindow(window);
                Check("相同稳定编号替换来源后节点重新关联", businessPeer.GetAutomationId() == businessIdentity && businessPeer.CurrentPoint is { Index: 2, Point.Y: 72 } replacement && ReferenceEquals(replacement.Item, replacements[2]));
                nodes.Add(Node("after-source-replacement", businessPeer));
                board.Line.ItemsSource = originals; _ = chart.PlotArea; FlushControlTemplateWindow(window);
                Check("恢复来源后节点保留身份并返回原业务对象", businessPeer.GetAutomationId() == businessIdentity && ReferenceEquals(businessPeer.CurrentPoint?.Item, board.Records[2]));
                chart.ClearSelection(); KeyStep(Key.Escape); board.Options.Children.OfType<ComboBox>().Single().SelectedIndex = 0;
                chart.HighContrastMode = ChartHighContrastMode.None; FlushControlTemplateWindow(window);
                var normal = Capture("normal");
                board.Options.Children.OfType<ComboBox>().Single().SelectedIndex = dark ? 2 : 1;
                chart.HighContrastMode = dark ? ChartHighContrastMode.Dark : ChartHighContrastMode.Light; FlushControlTemplateWindow(window);
                var contrast = Capture("high-contrast");
                Check("显式高对比背景和前景拥有充分对比", contrast.ForegroundContrast >= 7 && contrast.Probes.All(probe => probe.BackgroundContrast >= 7));
                Check("高对比三系列实际绘制且具有形状差异", contrast.Probes.All(probe => probe.InkPixels >= 12) && board.Line.MarkerShape != board.Scatter.MarkerShape &&
                    board.Bars is BarSeries && chart.Series.All(series => series.Stroke is null && series.Fill is null));
                Check("高对比切换改变实际绘制并保留语义身份", CountControlTemplateExactChanges(normal.Raster!, contrast.Raster!, chart.PlotArea) > 200 && businessPeer.GetAutomationId() == businessIdentity);
                var page = Image("page");
                using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)))
                { bitmap.Render(window); bitmap.Save(page); }
                Check("完整面板说明和摘要均在窗口内布局", board.Summary.Bounds.Height > 10 && board.Active.Bounds.Height > 10 &&
                    board.View.Bounds.Height + 36 <= window.ClientSize.Height && board.View.Bounds.Width + 36 <= window.ClientSize.Width);
                AccessibilityImageEvidence? firstPng = null;
                foreach (var dpi in new[] { 96d, 144d, 192d })
                {
                    var png = await Export("png", dpi); firstPng ??= png;
                    Check($"{dpi:0}DPI异步PNG保留高对比像素和尺寸", png.Exported && png.Width == 1200 && png.Height == 700 && png.Probes.All(probe => probe.InkPixels >= 12) &&
                        CountControlTemplateExactChanges(firstPng.Raster!, png.Raster!, new Rect(0, 0, 1200, 700)) == 0);
                    var jpeg = await Export("jpeg", dpi);
                    Check($"{dpi:0}DPI异步JPEG保持几何与配色", jpeg.Exported && jpeg.Width == 1200 && jpeg.Height == 700 && jpeg.Probes.All(probe => probe.InkPixels >= 8) && jpeg.MeanRgbErrorToPng < 8);
                }
                Check("点辅助缓存受限且输入均使用路由事件", root.CachedPointPeerCount <= 256 && keys.Count >= 15 && keys.All(key => key.Handled));
                _accessibilityEvidence.Add(new(token, native, page, chart.Bounds.Size, chart.TranslatePoint(default, window) ?? default, window.RenderScaling, chart.PlotArea, chart.VisibleRange!.Value,
                    chart.AccessibilitySummaryText, chart.AccessibilitySummary.TotalCount, chart.AccessibilitySummary.ValidCount, chart.AccessibilitySummary.MissingCount,
                    root.CachedPointPeerCount, keys, nodes, images,
                    "Native Avalonia Window + RenderTargetBitmap; synthetic routed KeyDown and public point providers; native UIA/AT-SPI bridge and physical keyboard are not inferred."));

                void KeyStep(Key key, KeyModifiers modifiers = KeyModifiers.None)
                {
                    var before = PointState(chart.CurrentPoint); var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = key, KeyModifiers = modifiers };
                    chart.RaiseEvent(args); FlushControlTemplateWindow(window);
                    keys.Add(new(key.ToString(), modifiers.ToString(), args.Handled, before, PointState(chart.CurrentPoint), chart.SelectedPoints?.Select(PointState).ToArray() ?? []));
                }
                AccessibilityImageEvidence Capture(string scenario)
                {
                    var image = Image(scenario); var raster = CaptureControlTemplateRaster(chart, image);
                    var entry = ImageEntry(scenario, "render-target-bitmap", image, true, 96, raster, 1, default, 0); images.Add(entry); return entry;
                }
                async Task<AccessibilityImageEvidence> Export(string format, double dpi)
                {
                    var path = Image($"{format}-{dpi:0}", format == "jpeg" ? "jpg" : "png");
                    var options = new ChartExportOptions { PixelSize = new PixelSize(1200, 700), Dpi = dpi, Background = chart.Background, JpegQuality = 90 };
                    var success = format == "jpeg" ? await chart.TryExportJpegAsync(path, options) : await chart.TryExportPngAsync(path, options);
                    using var bitmap = new Bitmap(path); var raster = ReadShapePixels(bitmap);
                    var scale = Math.Min(1200 / chart.Bounds.Width, 700 / chart.Bounds.Height);
                    var offset = new Vector((1200 - chart.Bounds.Width * scale) / 2, (700 - chart.Bounds.Height * scale) / 2);
                    var error = format == "jpeg" ? FlattenedDifference(firstPng!.Raster!, raster) : 0;
                    var entry = ImageEntry($"{format}-{dpi:0}", format, path, success, dpi, raster, scale, offset, error); images.Add(entry); return entry;
                }
                AccessibilityImageEvidence ImageEntry(string scenario, string format, string path, bool exported, double dpi, ShapeRaster raster, double scale, Vector offset, double error)
                {
                    var background = (chart.Background as ISolidColorBrush)?.Color ?? (dark ? Colors.Black : Colors.White);
                    var foreground = (chart.Foreground as ISolidColorBrush)?.Color ?? (dark ? Colors.White : Colors.Black);
                    var probes = new List<AccessibilityProbeEvidence>();
                    AddProbe(board.Line, 2, 45, 65, 0, 100); AddProbe(board.Bars, 1, 60, 55, 0, 100); AddProbe(board.Scatter, 0, 20, 1850, 1000, 2000);
                    return new(scenario, format, path, exported, dpi, raster.Width, raster.Height, chart.HighContrastMode.ToString(), background.ToString(), foreground.ToString(),
                        Contrast(background, foreground), chart.PlotArea, chart.VisibleRange!.Value, scale, offset, probes, error, raster);
                    void AddProbe(ChartSeries series, int sourceIndex, double x, double y, double yMinimum, double yMaximum)
                    {
                        var plot = chart.PlotArea; var point = new Point((plot.Left + plot.Width * x / 100) * scale + offset.X,
                            (plot.Bottom - plot.Height * (y - yMinimum) / (yMaximum - yMinimum)) * scale + offset.Y);
                        var color = ((ISolidColorBrush)chart.GetSeriesBrush(series)).Color;
                        var radius = Math.Max(8, (int)Math.Ceiling(8 * scale)); var region = new PixelRect((int)Math.Round(point.X) - radius, (int)Math.Round(point.Y) - radius, 2 * radius + 1, 2 * radius + 1);
                        probes.Add(new(series.Title!, series.GetType().Name, sourceIndex, new ChartPoint(x, y), series.YAxisKey, yMinimum, yMaximum,
                            point, region, color.ToString(), Contrast(background, color), raster.Count(region, color, format == "jpeg" ? 48 : 16)));
                    }
                }
            }
            finally { window.Close(); }
        }
    }

    private static AccessibilityPointEvidence? PointState(ChartPointHit? point) => point is null ? null : new(point.Series.Title, point.Index, point.StableId?.ToString(), point.Point, point.Position);
    private static AccessibilityNodeEvidence Node(string scenario, ChartPointAutomationPeer point)
    {
        var hit = point.CurrentPoint;
        return new(scenario, point.GetAutomationId() ?? "", point.GetClassName(), point.GetAutomationControlType().ToString(), point.GetName(), point.GetHelpText() ?? "", point.GetItemStatus() ?? "",
            point.GetProvider<IValueProvider>()?.Value ?? "", point.GetProvider<ISelectionItemProvider>()?.IsSelected ?? false,
            point.HasKeyboardFocus(), point.IsEnabled(), point.IsOffscreen(), point.GetBoundingRectangle(), hit?.Series.Title, hit?.Index ?? -1, hit?.StableId?.ToString(), hit?.Point);
    }
    private static double Contrast(Color first, Color second)
    {
        static double Linear(byte channel) { var value = channel / 255d; return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
        static double Luminance(Color color) => .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        var a = Luminance(first); var b = Luminance(second); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
    private sealed class AccessibilityActionCommand(Action<object?> action) : System.Windows.Input.ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
    private sealed record AccessibilityPointEvidence(string? Series, int SourceIndex, string? StableId, ChartPoint Point, Point Position);
    private sealed record AccessibilityKeyEvidence(string Key, string Modifiers, bool Handled, AccessibilityPointEvidence? Before, AccessibilityPointEvidence? After, AccessibilityPointEvidence?[] Selection);
    private sealed record AccessibilityNodeEvidence(string Scenario, string AutomationId, string Class, string ControlType, string Name, string Help, string Status, string Value,
        bool Selected, bool Focused, bool Enabled, bool Offscreen, Rect Bounds, string? Series, int SourceIndex, string? StableId, ChartPoint? Point);
    private sealed record AccessibilityProbeEvidence(string Series, string SeriesType, int SourceIndex, ChartPoint Point, string? YAxisKey, double YMinimum, double YMaximum,
        Point ExpectedPixel, PixelRect Region, string Color, double BackgroundContrast, int InkPixels);
    private sealed record AccessibilityImageEvidence(string Scenario, string Format, string Image, bool Exported, double Dpi, int Width, int Height, string ContrastMode,
        string Background, string Foreground, double ForegroundContrast, Rect PlotArea, ChartViewport VisibleRange, double Scale, Vector Offset,
        List<AccessibilityProbeEvidence> Probes, double MeanRgbErrorToPng, [property: System.Text.Json.Serialization.JsonIgnore] ShapeRaster? Raster);
    private sealed record AccessibilitySmokeEvidence(string Theme, bool NativeWindowCreated, string PageImage, Size ChartSize, Point ChartOriginInWindow, double RenderScaling, Rect PlotArea, ChartViewport VisibleRange,
        string Summary, long TotalCount, long ValidCount, long MissingCount, int CachedPointPeers, List<AccessibilityKeyEvidence> Keys, List<AccessibilityNodeEvidence> Nodes,
        List<AccessibilityImageEvidence> Images, string Scope);
}
