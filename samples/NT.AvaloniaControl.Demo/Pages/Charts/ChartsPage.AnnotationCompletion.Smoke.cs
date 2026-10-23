using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<AnnotationCompletionEvidence> _annotationCompletionEvidence = [];

    private async Task RunAnnotationCompletionSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _annotationCompletionExamples!.IsExpanded = true;
        await SmokeFlushAsync(); _annotationCompletionExamples.BringIntoView(); await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色"; var token = dark ? "dark" : "light";
            using var board = CreateAnnotationCompletionBoard(false);
            var window = new Window { Title = "Charts · 标注与参考元素完整性", Width = 1160, Height = 760, RequestedThemeVariant = theme,
                Content = new Border { Padding = new Thickness(18), Child = board.View } };
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                window.Background = Brush(dark ? "#111722" : "#F8FAFC"); window.Foreground = Brush(dark ? "#EAF0FB" : "#172238");
                ((Border)window.Content!).Background = window.Background;
                var chart = board.Chart; chart.Background = window.Background; chart.Foreground = window.Foreground;
                FlushControlTemplateWindow(window);
                void Check(string name, bool passed) => checks[$"标注参考完整性{label}{name}"] = passed;
                string Image(string suffix) => Path.Combine(directory, stem + $"-annotation-{token}-{suffix}.png");
                var projections = new List<AnnotationProjectionEvidence>();
                var changes = new List<AnnotationPixelChangeEvidence>();
                var exportRecords = new List<AnnotationExportEvidence>();
                var native = window.TryGetPlatformHandle() is { Handle: not 0 };
                var beforePath = Image("before"); var before = CaptureControlTemplateRaster(chart, beforePath); var fullPlot = chart.PlotArea;
                var initialContext = board.LastContext();
                Check("原生窗口与仅标注使用的额外轴布局", native && chart.Bounds.Width > 700 && chart.PlotArea.Height > 150 &&
                    chart.Series.Count == 1 && chart.Series.All(series => series.XAxisKey is null && series.YAxisKey is null) && initialContext is not null);
                AddProjection("independent-broken-x-inverse-log-y", 30, 100);
                Check("独立反向对数与断轴投影匹配数学公式", ProjectionNear(projections[^1]));
                var template = chart.GetVisualDescendants().OfType<Grid>().SingleOrDefault(control => control.Name == "AnnotationCompletionTemplateRoot");
                var badge = chart.GetVisualDescendants().OfType<Border>().SingleOrDefault(control => control.Name == "AnnotationCompletionBadge");
                var message = chart.GetVisualDescendants().OfType<TextBlock>().SingleOrDefault(control => control.Name == "AnnotationCompletionMessage");
                var templateContext = template?.DataContext as ChartPlotAreaContext;
                var namedAvailable = templateContext is not null && templateContext.TryGetProjection(board.ReferenceX.Key, board.ReferenceY.Key, out _);
                var templatePoint = templateContext is not null && templateContext.TryGetProjection(board.ReferenceX.Key, board.ReferenceY.Key, out var namedProjection) ? namedProjection.Project(new ChartPoint(30, 100)) : new Point(double.NaN, double.NaN);
                var expectedLocal = AnnotationExpected(board, 30, 100) - fullPlot.TopLeft;
                var initialBadgeBounds = badge is null ? default : TemplateControlBounds(badge, chart);
                Check("绘图区模板绑定对象并限制在绘图区内", template is { Bounds.Width: > 100 } && badge is { Bounds.Width: > 40 } &&
                    message?.Text == board.Note.Message && templateContext is not null && ReferenceEquals(templateContext.Content, board.Note) &&
                    fullPlot.Contains(initialBadgeBounds) && template.GetVisualAncestors().OfType<InputElement>().Any(element => !element.IsHitTestVisible));
                Check("模板命名轴投影使用绘图区局部坐标", namedAvailable && PointNear(templatePoint, expectedLocal));
                board.Note.Message = "同对象属性已更新"; FlushControlTemplateWindow(window);
                Check("同业务对象属性通知刷新真实模板", message?.Text == "同对象属性已更新" && ReferenceEquals(template,
                    chart.GetVisualDescendants().OfType<Grid>().Single(control => control.Name == "AnnotationCompletionTemplateRoot")));
                board.Note.Message = "参考元素已就绪"; FlushControlTemplateWindow(window);
                var inkRaster = CaptureControlTemplateRaster(chart, Image("on"));
                Check("三形状标签和像素提示均有可见墨迹", inkRaster.Count(TemplatePixelRect(fullPlot), Color.Parse("#4385F4"), 24) >= 20 &&
                    inkRaster.Count(TemplatePixelRect(fullPlot), Color.Parse("#19A798"), 24) >= 20 && inkRaster.Count(TemplatePixelRect(fullPlot), Color.Parse("#F2A33A"), 24) >= 20 &&
                    inkRaster.Count(TemplatePixelRect(AnnotationFixedInterior(chart)), Color.Parse("#CB11A5"), 3) > 100);
                board.Rectangle.CornerRadius = 0; FlushControlTemplateWindow(window);
                var squarePath = Image("square-corner"); var square = CaptureControlTemplateRaster(chart, squarePath);
                board.Rectangle.CornerRadius = 16; FlushControlTemplateWindow(window);
                var roundedRestorePath = Image("rounded-restored"); var roundedRestore = CaptureControlTemplateRaster(chart, roundedRestorePath);
                var cornerChanges = CountControlTemplateExactChanges(inkRaster, square, fullPlot);
                var cornerRestored = CountControlTemplateExactChanges(inkRaster, roundedRestore, fullPlot);
                changes.Add(new("rounded-corner", Image("on"), squarePath, roundedRestorePath, fullPlot, cornerChanges, cornerRestored));
                Check("圆角改变实际像素且恢复精确", cornerChanges > 10 && cornerRestored == 0);
                var rectangleFirst = AnnotationExpected(board, board.Rectangle.X1, board.Rectangle.Y1);
                var rectangleLast = AnnotationExpected(board, board.Rectangle.X2, board.Rectangle.Y2);
                var rectangleBounds = new Rect(Math.Min(rectangleFirst.X, rectangleLast.X), Math.Min(rectangleFirst.Y, rectangleLast.Y),
                    Math.Abs(rectangleLast.X - rectangleFirst.X), Math.Abs(rectangleLast.Y - rectangleFirst.Y));
                var cornerRegion = new Rect(rectangleBounds.Left + .5, rectangleBounds.Top + .5, 2, 2);
                board.Rectangle.IsVisible = false; FlushControlTemplateWindow(window);
                var noRectanglePath = Image("rectangle-off"); var noRectangle = CaptureControlTemplateRaster(chart, noRectanglePath);
                board.Rectangle.IsVisible = true; FlushControlTemplateWindow(window);
                var cornerCutoutChanges = CountControlTemplateExactChanges(inkRaster, square, cornerRegion);
                var cornerEmptyChanges = CountControlTemplateExactChanges(inkRaster, noRectangle, cornerRegion);
                changes.Add(new("rounded-corner-cutout", Image("on"), squarePath, roundedRestorePath, cornerRegion, cornerCutoutChanges,
                    CountControlTemplateExactChanges(inkRaster, roundedRestore, cornerRegion), noRectanglePath, cornerEmptyChanges));
                Check("圆角角落保留底图而直角覆盖", cornerCutoutChanges >= 3 && cornerEmptyChanges == 0);
                foreach (var (shape, name) in new (ChartShapeAnnotation, string)[] { (board.Rectangle, "rectangle"), (board.Ellipse, "ellipse"), (board.Arrow, "arrow") })
                {
                    var originalLabel = shape.Label; shape.Label = null; FlushControlTemplateWindow(window);
                    var offLabelPath = Image(name + "-label-off"); var offLabel = CaptureControlTemplateRaster(chart, offLabelPath);
                    shape.Label = originalLabel; FlushControlTemplateWindow(window);
                    var restoredLabelPath = Image(name + "-label-restored"); var restoredLabel = CaptureControlTemplateRaster(chart, restoredLabelPath);
                    var delta = CountControlTemplateExactChanges(inkRaster, offLabel, fullPlot); var restoreDelta = CountControlTemplateExactChanges(inkRaster, restoredLabel, fullPlot);
                    changes.Add(new(name + "-label", Image("on"), offLabelPath, restoredLabelPath, fullPlot, delta, restoreDelta));
                    Check(name + "独立标签有字形且恢复精确", delta >= 5 && restoreDelta == 0);
                }
                // Center-radius mode must project the two data-domain extrema, including
                // logarithmic scaling, rather than using constant screen-space radii.
                var ellipseMinimum = AnnotationExpected(board, board.Ellipse.X - board.Ellipse.RadiusX, board.Ellipse.Y - board.Ellipse.RadiusY);
                var ellipseMaximum = AnnotationExpected(board, board.Ellipse.X + board.Ellipse.RadiusX, board.Ellipse.Y + board.Ellipse.RadiusY);
                var projectedCenter = AnnotationExpected(board, board.Ellipse.X, board.Ellipse.Y);
                var screenCenter = new Point((ellipseMinimum.X + ellipseMaximum.X) / 2, (ellipseMinimum.Y + ellipseMaximum.Y) / 2);
                board.Ellipse.X1 = 63; board.Ellipse.X2 = 83; board.Ellipse.Y1 = 70; board.Ellipse.Y2 = 370; board.Ellipse.GeometryMode = ChartEllipseGeometryMode.EndpointBounds;
                FlushControlTemplateWindow(window); var endpointsPath = Image("ellipse-endpoints"); var endpoints = CaptureControlTemplateRaster(chart, endpointsPath);
                board.Ellipse.GeometryMode = ChartEllipseGeometryMode.CenterRadius; FlushControlTemplateWindow(window);
                var ellipseRestoredPath = Image("ellipse-radius-restored"); var radiusRestore = CaptureControlTemplateRaster(chart, ellipseRestoredPath);
                Check("中心数据半径等价于投影两侧界限", CountControlTemplateExactChanges(inkRaster, endpoints, fullPlot) == 0 &&
                    CountControlTemplateExactChanges(inkRaster, radiusRestore, fullPlot) == 0 && Math.Abs(projectedCenter.Y - screenCenter.Y) > 1);
                board.ReferenceY.IsInversed = false; FlushControlTemplateWindow(window);
                CaptureControlTemplateRaster(chart, Image("normal-log")); AddProjection("normal-log-y", 30, 100);
                Check("反向开关后命名轴仍匹配独立投影", ProjectionNear(projections[^1]));
                board.ReferenceY.IsInversed = true; FlushControlTemplateWindow(window);
                var initialTemplate = template;
                chart.Viewport = new ChartViewport(2, 8, 20, 80);
                board.ReferenceX.ViewMinimum = 10; board.ReferenceX.ViewMaximum = 90;
                board.ReferenceY.ViewMinimum = 3; board.ReferenceY.ViewMaximum = 900;
                FlushControlTemplateWindow(window);
                var zoomPath = Image("zoom"); var zoom = CaptureControlTemplateRaster(chart, zoomPath); AddProjection("independent-axis-viewports", 30, 100);
                var zoomContext = template?.DataContext as ChartPlotAreaContext;
                var zoomNamed = zoomContext is not null && zoomContext.TryGetVisibleRange(board.ReferenceX.Key, board.ReferenceY.Key, out var namedRange) &&
                    Math.Abs(namedRange.XMinimum - 10) < 1e-7 && Math.Abs(namedRange.XMaximum - 90) < 1e-7 && Math.Abs(namedRange.YMinimum - 3) < 1e-7 && Math.Abs(namedRange.YMaximum - 900) < 1e-7;
                Check("多轴缩放保持各自原始数据范围", HasVisibleRange(chart, new ChartViewport(2, 8, 20, 80)) && ProjectionNear(projections[^1]) && zoomNamed);
                Check("缩放复用模板并更新上下文", ReferenceEquals(initialTemplate, chart.GetVisualDescendants().OfType<Grid>().Single(control => control.Name == "AnnotationCompletionTemplateRoot")) &&
                    zoomContext is not null && !ReferenceEquals(templateContext, zoomContext) && zoomContext.VisibleRange == chart.VisibleRange);
                Check("数据标注移动而像素提示保持DIP尺寸", CountControlTemplateExactChanges(inkRaster, zoom, fullPlot) > 100 &&
                    zoom.Count(TemplatePixelRect(AnnotationFixedInterior(chart)), Color.Parse("#CB11A5"), 3) > 100 &&
                    Math.Abs((board.FixedRectangle.X2 - board.FixedRectangle.X1) - 154) < 1e-9);
                chart.ResetViewport(); FlushControlTemplateWindow(window);
                var resetPath = Image("view-restored"); var reset = CaptureControlTemplateRaster(chart, resetPath);
                Check("恢复多轴视图后全图像素精确恢复", CountControlTemplateExactChanges(inkRaster, reset, new Rect(chart.Bounds.Size)) == 0);
                var previousTemplate = template;
                chart.PlotAreaContentTemplate = null; chart.PlotAreaContent = null; FlushControlTemplateWindow(window);
                var removed = previousTemplate is not null && previousTemplate.GetVisualParent() is null && !chart.GetVisualDescendants().Any(item => item.Name == "AnnotationCompletionTemplateRoot");
                chart.PlotAreaContentTemplate = board.ContentTemplate; chart.PlotAreaContent = board.Note; FlushControlTemplateWindow(window);
                Check("更换模板卸载旧内容并可恢复", removed && chart.GetVisualDescendants().OfType<TextBlock>().Any(item => item.Name == "AnnotationCompletionMessage" && item.Text == board.Note.Message));
                // Retain positive, negative and restored samples at each declared export
                // density. Density changes metadata, while the fixed PixelSize preserves pixels.
                ShapeRaster? baselineExport = null;
                foreach (var dpi in new[] { 96d, 144d, 192d })
                {
                    var on = Export("on", dpi);
                    foreach (var annotation in chart.Annotations.Where(item => !ReferenceEquals(item, board.Probe))) annotation.IsVisible = false;
                    chart.PlotAreaContent = null; FlushControlTemplateWindow(window);
                    var off = Export("off", dpi);
                    foreach (var annotation in chart.Annotations.Where(item => !ReferenceEquals(item, board.Probe))) annotation.IsVisible = true;
                    chart.PlotAreaContent = board.Note; FlushControlTemplateWindow(window);
                    var restored = Export("restored", dpi);
                    var all = new Rect(0, 0, on.Raster.Width, on.Raster.Height);
                    var diff = CountControlTemplateExactChanges(on.Raster, off.Raster, all); var restoreDiff = CountControlTemplateExactChanges(on.Raster, restored.Raster, all);
                    var sameDpi = baselineExport is null || baselineExport.Pixels.AsSpan().SequenceEqual(on.Raster.Pixels);
                    var scale = Math.Min(1000 / chart.Bounds.Width, 560 / chart.Bounds.Height);
                    var contentBounds = chart.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "AnnotationCompletionBadge");
                    var badgeBounds = TemplateControlBounds(contentBounds, chart);
                    Rect ExportRect(Rect bounds) => new((1000 - chart.Bounds.Width * scale) / 2 + bounds.X * scale,
                        (560 - chart.Bounds.Height * scale) / 2 + bounds.Y * scale, bounds.Width * scale, bounds.Height * scale);
                    var badgeExportRegion = ExportRect(badgeBounds); var pixelRegion = ExportRect(AnnotationFixedInterior(chart));
                    var templateInk = on.Raster.Count(TemplatePixelRect(badgeExportRegion), Color.Parse("#7B2688"), 3);
                    var pixelInk = on.Raster.Count(TemplatePixelRect(pixelRegion), Color.Parse("#CB11A5"), 3);
                    Check($"{dpi:0}DPI导出保留标注模板且正负恢复一致", on.Exported && off.Exported && restored.Exported && on.Density && off.Density && restored.Density &&
                        on.Raster.Width == 1000 && on.Raster.Height == 560 && diff > 100 && restoreDiff == 0 && sameDpi && templateInk > 50 && pixelInk > 50);
                    exportRecords.Add(new(dpi, on.Path, off.Path, restored.Path, on.Exported, off.Exported, restored.Exported, on.Density, off.Density, restored.Density,
                        on.Raster.Width, on.Raster.Height, diff, restoreDiff, sameDpi, badgeExportRegion, templateInk, pixelRegion, pixelInk));
                    baselineExport ??= on.Raster;
                }
                var offPath = Image("off");
                foreach (var annotation in chart.Annotations.Where(item => !ReferenceEquals(item, board.Probe))) annotation.IsVisible = false;
                chart.PlotAreaContent = null; FlushControlTemplateWindow(window); var offScreen = CaptureControlTemplateRaster(chart, offPath);
                foreach (var annotation in chart.Annotations.Where(item => !ReferenceEquals(item, board.Probe))) annotation.IsVisible = true;
                chart.PlotAreaContent = board.Note; FlushControlTemplateWindow(window);
                var restoredPath = Image("restored"); var restoredScreen = CaptureControlTemplateRaster(chart, restoredPath);
                var positiveDelta = CountControlTemplateExactChanges(inkRaster, offScreen, fullPlot); var restoreDeltaScreen = CountControlTemplateExactChanges(inkRaster, restoredScreen, fullPlot);
                Check("屏幕标注模板正负样本差异且恢复精确", positiveDelta > 100 && restoreDeltaScreen == 0);
                changes.Add(new("all-reference-elements-and-template", Image("on"), offPath, restoredPath, fullPlot, positiveDelta, restoreDeltaScreen));
                var pagePath = Image("page");
                using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96))) { bitmap.Render(window); bitmap.Save(pagePath); }
                _annotationCompletionEvidence.Add(new(token, native, pagePath, beforePath, Image("on"), offPath, restoredPath, zoomPath, resetPath,
                    chart.Bounds, fullPlot, chart.Series.Count, board.ReferenceX.Key!, board.ReferenceY.Key!, projections, templatePoint, expectedLocal,
                    initialBadgeBounds, ellipseMinimum, ellipseMaximum, projectedCenter, screenCenter, changes, exportRecords,
                    "Native Win32 or WSLg window; public chart APIs, routed visuals and RenderTargetBitmap/PNG checks; desktop presentation and physical devices are not instrumented."));

                void AddProjection(string scenario, double x, double y)
                {
                    var actual = board.LastContext()?.Project(x, y) ?? new Point(double.NaN, double.NaN);
                    projections.Add(new(scenario, chart.PlotArea, x, y, AnnotationExpected(board, x, y), actual,
                        board.ReferenceX.ViewMinimum ?? board.ReferenceX.Minimum!.Value, board.ReferenceX.ViewMaximum ?? board.ReferenceX.Maximum!.Value,
                        board.ReferenceY.ViewMinimum ?? board.ReferenceY.Minimum!.Value, board.ReferenceY.ViewMaximum ?? board.ReferenceY.Maximum!.Value, board.ReferenceY.IsInversed));
                }
                (string Path, bool Exported, bool Density, ShapeRaster Raster) Export(string state, double dpi)
                {
                    var path = Image($"export-{dpi:0}-{state}"); using var stream = new MemoryStream();
                    var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(1000, 560), Dpi = dpi, Background = chart.Background });
                    var bytes = stream.ToArray(); File.WriteAllBytes(path, bytes); stream.Position = 0; using var bitmap = new Bitmap(stream);
                    var density = TryReadCommonPngDensity(bytes, out var x, out var y, out var unit) && x == (uint)Math.Round(dpi / .0254) && x == y && unit == 1;
                    return (path, exported, density, ReadShapePixels(bitmap));
                }
            }
            finally { window.Close(); }
        }
    }

    private static Point AnnotationExpected(AnnotationCompletionBoard board, double x, double y)
    {
        // Independent arithmetic oracle: the reference X break removes 80% of
        // [40, 60]; Y is base-10 logarithmic with its own raw viewport bounds.
        static double Compress(double value) => value <= 40 ? value : value < 60 ? 40 + (value - 40) * .2 : value - 16;
        var xMin = board.ReferenceX.ViewMinimum ?? 0; var xMax = board.ReferenceX.ViewMaximum ?? 100;
        var yMin = board.ReferenceY.ViewMinimum ?? 1; var yMax = board.ReferenceY.ViewMaximum ?? 1000;
        var nx = (Compress(x) - Compress(xMin)) / (Compress(xMax) - Compress(xMin));
        var ny = (Math.Log10(y) - Math.Log10(yMin)) / (Math.Log10(yMax) - Math.Log10(yMin));
        if (board.ReferenceX.IsInversed) nx = 1 - nx;
        if (!board.ReferenceY.IsInversed) ny = 1 - ny;
        return new Point(board.Chart.PlotArea.Left + board.Chart.PlotArea.Width * nx, board.Chart.PlotArea.Top + board.Chart.PlotArea.Height * ny);
    }
    private static Rect AnnotationFixedInterior(CartesianChart chart) => new(chart.PlotArea.Left + 27, chart.PlotArea.Top + 17, 130, 16);
    private static bool PointNear(Point actual, Point expected) => double.IsFinite(actual.X) && double.IsFinite(actual.Y) && Math.Abs(actual.X - expected.X) < 1e-7 && Math.Abs(actual.Y - expected.Y) < 1e-7;
    private static bool ProjectionNear(AnnotationProjectionEvidence evidence) => PointNear(evidence.Actual, evidence.Expected);
    private sealed record AnnotationProjectionEvidence(string Scenario, Rect PlotArea, double X, double Y, Point Expected, Point Actual,
        double XMinimum, double XMaximum, double YMinimum, double YMaximum, bool YInversed);
    private sealed record AnnotationPixelChangeEvidence(string Feature, string OnImage, string OffImage, string RestoredImage, Rect Region, int ChangedPixels, int RestoredChangedPixels, string? NoFeatureImage = null, int? NoFeatureChangedPixels = null);
    private sealed record AnnotationExportEvidence(double Dpi, string OnImage, string OffImage, string RestoredImage,
        bool OnExported, bool OffExported, bool RestoredExported, bool OnDensityMatches, bool OffDensityMatches, bool RestoredDensityMatches,
        int Width, int Height, int ChangedPixels, int RestoredChangedPixels, bool SameRasterAs96Dpi,
        Rect TemplateRegion, int TemplateInkPixels, Rect PixelAnnotationRegion, int PixelAnnotationInkPixels);
    private sealed record AnnotationCompletionEvidence(string Theme, bool NativeWindowCreated, string PageImage, string BeforeImage, string OnImage, string OffImage,
        string RestoredImage, string ZoomImage, string ViewRestoredImage, Rect ChartBounds, Rect PlotArea, int SeriesCount, string XAxisKey, string YAxisKey,
        List<AnnotationProjectionEvidence> Projections, Point TemplateProjection, Point ExpectedTemplateProjection, Rect TemplateBadgeBounds,
        Point EllipseMinimum, Point EllipseMaximum, Point ProjectedDataCenter, Point ScreenBoundsCenter,
        List<AnnotationPixelChangeEvidence> PixelChanges, List<AnnotationExportEvidence> Exports, string ValidationScope);
}
