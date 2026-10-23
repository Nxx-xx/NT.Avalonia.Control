using System.Buffers.Binary;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<CommonExportEvidence> _commonExportEvidence = [];

    private async Task RunCommonFeaturesSmokeAsync(Dictionary<string, bool> checks, string image, string darkImage, string exportImage, string darkExportImage)
    {
        if (_commonExamples is null) throw new InvalidOperationException("C2 组合示例尚未创建。");
        _commonExamples.IsExpanded = true;
        try
        {
            await SmokeFlushAsync(); _commonExamples.BringIntoView(); await SmokeFlushAsync();
            var sales = _commonCharts[0]; var monitor = _commonCharts[1];
            checks["C2两张组合图已布局并包含三种系列"] = _commonCharts.Count == 2 && _commonCharts.All(chart => chart.PlotArea.Width > 150 && chart.PlotArea.Height > 150) &&
                sales.Series.Any(series => series is LineSeries) && sales.Series.Any(series => series is BarSeries) && sales.Series.Any(series => series is ScatterSeries);

            var first = sales.Series[0]; var brush = sales.GetSeriesBrush(first); var identity = first.CreateSnapshot().Points[0].StableId;
            var legend = sales.LegendItems.Single(item => ReferenceEquals(item.Series, first));
            RouteCommonClick(sales, legend.Bounds.Center);
            var hidden = !first.IsVisible && sales.LegendItems.Any(item => ReferenceEquals(item.Series, first) && !item.IsVisible);
            RouteCommonClick(sales, sales.LegendItems.Single(item => ReferenceEquals(item.Series, first)).Bounds.Center);
            checks["C2图例路由点击显隐并保留颜色和原始身份"] = hidden && first.IsVisible && Equals(brush, sales.GetSeriesBrush(first)) && Equals(identity, first.CreateSnapshot().Points[0].StableId);
            var bottomPlot = sales.PlotArea;
            sales.LegendPosition = ChartLegendPosition.Right; sales.LegendOrientation = Orientation.Vertical;
            checks["C2图例位置与排列参与实际布局"] = sales.PlotArea.Right < bottomPlot.Right && sales.LegendItems.All(item => item.Bounds.Left >= sales.PlotArea.Right);
            sales.LegendPosition = ChartLegendPosition.Bottom; sales.LegendOrientation = Orientation.Horizontal;
            RunLegendLayoutSmoke(checks, sales, image);

            var labels = CommonChartHash(sales); first.ShowDataLabels = false;
            checks["C2数据标签改变实际渲染"] = labels != CommonChartHash(sales); first.ShowDataLabels = true;
            var scatter = sales.Series.OfType<ScatterSeries>().Single(); var marker = CommonChartHash(sales);
            scatter.MarkerShape = ChartMarkerShape.Cross;
            checks["C2不同标记形状改变实际渲染"] = marker != CommonChartHash(sales); scatter.MarkerShape = ChartMarkerShape.Triangle;
            var annotated = CommonChartHash(monitor); monitor.Annotations[1].IsVisible = false;
            checks["C2数据与像素注释实际参与绘制"] = monitor.Annotations.Count == 3 && annotated != CommonChartHash(monitor); monitor.Annotations[1].IsVisible = true;

            var plot = monitor.PlotArea;
            monitor.SelectRectangle(new Rect(plot.Left + plot.Width * .5, plot.Top, plot.Width * .4, plot.Height));
            checks["C2矩形选择保留原始数值"] = CommonSelection(monitor).Count >= 6 && CommonSelection(monitor).All(hit => hit.Point.X >= 5 && hit.Point.X <= 9 && hit.Item is not null && hit.StableId is not null);
            monitor.ClearSelection(); monitor.Focus(); RouteCommonKey(monitor, Key.Right);
            var selected = CommonSelection(monitor).FirstOrDefault(); RouteCommonKey(monitor, Key.Right);
            checks["C2路由方向键可浏览原始数据点"] = selected is not null && CommonSelection(monitor).Count == 1 &&
                (CommonSelection(monitor)[0].Index != selected.Index || !ReferenceEquals(CommonSelection(monitor)[0].Series, selected.Series));
            monitor.Viewport = new ChartViewport(2, 8, 20, 80); RouteCommonKey(monitor, Key.Home);
            checks["C2路由Home恢复视图"] = monitor.Viewport is null;
            var beforeEscape = CommonSelection(monitor).Count;
            using (var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true)) MoveCommonPointer(monitor, pointer);
            var hadReadings = monitor.HoveredPoints.Count > 0;
            RouteCommonKey(monitor, Key.Escape);
            checks["C2路由Esc取消读数并保留选择"] = hadReadings && monitor.HoveredPoints.Count == 0 && CommonSelection(monitor).Count == beforeEscape;
            RunSelectionChangingSmoke(checks, monitor);

            monitor.SelectRectangle(new Rect(monitor.PlotArea.Left + monitor.PlotArea.Width * .5, monitor.PlotArea.Top, monitor.PlotArea.Width * .3, monitor.PlotArea.Height));
            var hover = CaptureCommonHoverImage(monitor, image);
            checks["C2共享悬停返回同X的三组原始数据"] = hover.Hits;
            checks["C2共享提示与十字线改变实际渲染"] = hover.Changed && monitor.ShowCrosshair && monitor.TooltipFormatter?.Invoke(monitor.HoveredPoints)?.Contains("接口 A", StringComparison.Ordinal) == true;
            checks["C2亮色PNG导出包含实际图形"] = VerifyCommonExport(monitor, exportImage, false);
            _hostWindow.RequestedThemeVariant = ThemeVariant.Dark; await SmokeFlushAsync();
            var darkHover = CaptureCommonHoverImage(monitor, darkImage);
            checks["C2暗色共享提示与十字线可渲染"] = darkHover.Hits && darkHover.Changed;
            checks["C2暗色PNG导出包含实际图形"] = VerifyCommonExport(monitor, darkExportImage, true);
        }
        finally
        {
            foreach (var chart in _commonCharts) { chart.ClearSelection(); chart.ResetViewport(); }
            _hostWindow.RequestedThemeVariant = ThemeVariant.Light; _commonExamples.IsExpanded = false; _scroll.Offset = default; await SmokeFlushAsync();
        }
    }

    private (bool Hits, bool Changed) CaptureCommonHoverImage(CartesianChart chart, string path)
    {
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var outside = chart.TranslatePoint(new Point(-1, -1), _hostWindow) ?? throw new InvalidOperationException("C2 图表无法转换为窗口坐标。");
        chart.RaiseEvent(new PointerEventArgs(InputElement.PointerExitedEvent, chart, pointer, _hostWindow, outside, 0, default, KeyModifiers.None));
        var before = CommonChartHash(chart); MoveCommonPointer(chart, pointer);
        var hits = chart.HoveredPoints.Count == 3 && chart.HoveredPoints.All(hit => hit.Point.X == 5 && hit.StableId is not null) && chart.HoveredPoints.Select(hit => hit.Series).Distinct().Count() == 3;
        var changed = before != CommonChartHash(chart); CaptureSmokeImage(path); return (hits, changed);
    }

    private void MoveCommonPointer(CartesianChart chart, Pointer pointer)
    {
        var range = chart.VisibleRange!.Value; var plot = chart.PlotArea; var value = _commonResponse[5];
        var point = new Point(plot.Left + (value.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
            plot.Bottom - (value.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
        var windowPoint = chart.TranslatePoint(point, _hostWindow) ?? throw new InvalidOperationException("C2 读数点无法转换为窗口坐标。");
        chart.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, chart, pointer, _hostWindow, windowPoint, 1, default, KeyModifiers.None));
    }

    private bool VerifyCommonExport(CartesianChart chart, string path, bool dark)
    {
        var saved = chart.TryExportPng(path, new ChartExportOptions { PixelSize = new PixelSize(960, 540), Dpi = 144, Background = Brush(dark ? "#182232" : "#FFFFFF") });
        if (!saved || !File.Exists(path)) return false;
        using var bitmap = new Bitmap(path);
        var bytes = File.ReadAllBytes(path);
        var hasDensity = TryReadCommonPngDensity(bytes, out var xDensity, out var yDensity, out var densityUnit);
        var pixels = VerifyCommonExportPixels(chart, bytes, dark);
        _commonExportEvidence.Add(new(dark ? "dark" : "light", path, bitmap.PixelSize.Width, bitmap.PixelSize.Height, 144,
            hasDensity ? xDensity : null, hasDensity ? yDensity : null, hasDensity ? densityUnit : null,
            bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), pixels));
        return bitmap.PixelSize == new PixelSize(960, 540) && bytes.Length > 1000 && hasDensity && densityUnit == 1 && xDensity == 5669 && yDensity == 5669 &&
            pixels.DpiInvariant && pixels.PlotFitsCanvas && pixels.BottomAxisInkPixels > 0 && pixels.LeftAxisInkPixels > 0 && pixels.LeftBoundaryInkPixels > 0 &&
            pixels.SeriesEndpoints.Count == 6 && pixels.LegendItems.Count == 3 &&
            pixels.SeriesEndpoints.All(endpoint => endpoint.InkPixels > 0) && pixels.LegendItems.All(item => item.SymbolInkPixels > 0 && item.TextInkPixels > 0);
    }

    private static bool TryReadCommonPngDensity(ReadOnlySpan<byte> bytes, out uint xDensity, out uint yDensity, out byte densityUnit)
    {
        xDensity = yDensity = 0; densityUnit = 0;
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!bytes.StartsWith(signature)) return false;
        for (var offset = 8; bytes.Length - offset >= 12;)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (length > int.MaxValue || length > bytes.Length - offset - 12) return false;
            if (bytes.Slice(offset + 4, 4).SequenceEqual("pHYs"u8))
            {
                if (length != 9) return false;
                xDensity = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 8, 4));
                yDensity = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 12, 4));
                densityUnit = bytes[offset + 16];
                return true;
            }
            offset += (int)length + 12;
        }
        return false;
    }

    private void RouteCommonClick(CartesianChart chart, Point position)
    {
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var point = chart.TranslatePoint(position, _hostWindow) ?? throw new InvalidOperationException("C2 图例无法转换为窗口坐标。");
        chart.RaiseEvent(new PointerPressedEventArgs(chart, pointer, _hostWindow, point, 0, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1) { RoutedEvent = InputElement.PointerPressedEvent });
        chart.RaiseEvent(new PointerReleasedEventArgs(chart, pointer, _hostWindow, point, 1, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left) { RoutedEvent = InputElement.PointerReleasedEvent });
        _hostWindow.UpdateLayout();
    }

    private static void RouteCommonKey(CartesianChart chart, Key key) => chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = key });
    private static string CommonChartHash(CartesianChart chart)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(chart.Bounds.Width), (int)Math.Ceiling(chart.Bounds.Height)), new Vector(96, 96));
        bitmap.Render(chart); using var stream = new MemoryStream(); bitmap.Save(stream); return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private sealed record CommonExportEvidence(string Theme, string Path, int Width, int Height, double RequestedDpi, uint? PixelsPerMeterX, uint? PixelsPerMeterY, byte? DensityUnit, int Bytes, string Sha256, CommonExportPixelsEvidence Pixels);
}
