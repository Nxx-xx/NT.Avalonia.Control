using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<ControlTemplateSmokeEvidence> _controlTemplateEvidence = [];

    private async Task RunControlTemplateSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _controlTemplateExamples!.IsExpanded = true;
        await SmokeFlushAsync(); _controlTemplateExamples.BringIntoView(); await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
            _hostWindow.RequestedThemeVariant = theme; await SmokeFlushAsync();
            checks[$"控件模板{label}页面展示实际业务标签"] = _controlTemplateChart!.GetVisualDescendants().OfType<Border>().Any(control => control.Name == "ControlDataLabelCard" &&
                control.DataContext is ChartDataLabelContext context && ReferenceEquals(context.Data.Item, _controlTemplateRecords[0]) && control.Bounds.Width > 20);
            var pageImage = Path.Combine(directory, stem + $"-control-templates-{(dark ? "dark" : "light")}.png");
            CaptureSmokeImage(pageImage);
            await VerifyControlTemplateNative(checks, theme, directory, stem, pageImage);
        }
        _controlTemplateExamples.IsExpanded = false;
    }

    private async Task VerifyControlTemplateNative(Dictionary<string, bool> checks, ThemeVariant theme, string directory, string stem, string pageImage)
    {
        var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
        var ink = Color.Parse(dark ? "#FFB4E1" : "#962775");
        var records = new ObservableCollection<ControlTemplateRecord>
        { new("template-primary", "直营订单", 1, 37), new("template-second", "会员订单", 3, 62), new("template-third", "到店订单", 5, 45) };
        var chart = new CartesianChart { Margin = new Thickness(12), Background = Brush(dark ? "#111722" : "#F8FAFC"), Foreground = Brush(dark ? "#EAF0FB" : "#172238"), IsZoomEnabled = true, IsPanEnabled = true };
        ConfigureControlTemplateChart(chart, records, new SolidColorBrush(ink));
        var window = new Window { Title = "Charts · 图例与标签模板像素检查", Width = 960, Height = 540, RequestedThemeVariant = theme, Content = chart };
        try
        {
            window.Show(_hostWindow); FlushControlTemplateWindow(window);
            await Task.Delay(80);
            FlushControlTemplateWindow(window);
            Border? PrimaryCard() => chart.GetVisualDescendants().OfType<Border>().FirstOrDefault(control => control.Name == "ControlDataLabelCard" &&
                control.DataContext is ChartDataLabelContext context && ReferenceEquals(context.Data.Item, records[0]));
            TextBlock? Title(ChartSeries series) => chart.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "ControlLegendCard" &&
                control.DataContext is ChartLegendItem context && ReferenceEquals(context.Series, series))
                .SelectMany(control => control.GetVisualDescendants().OfType<TextBlock>()).SingleOrDefault(text => text.Name == "ControlLegendTitle");
            var native = window.TryGetPlatformHandle() is { Handle: not 0 };
            var primary = PrimaryCard(); var context = primary?.DataContext as ChartDataLabelContext;
            checks[$"控件模板{label}原生业务标签保留原对象坐标ID"] = native && primary is { Bounds.Width: > 20, Bounds.Height: > 15 } && context is not null &&
                ReferenceEquals(context.Series, chart.Series[0]) && ReferenceEquals(context.Data.Item, records[0]) && context.Data.SourceIndex == 0 &&
                Equals(context.Data.StableId, "template-primary") && context.Data.Point == new ChartPoint(1, 37) && context.Text == "37";
            var id = primary?.GetVisualDescendants().OfType<TextBlock>().SingleOrDefault(text => text.Name == "ControlDataLabelId" && text.Text == "template-primary");
            var idBounds = id is null ? default : TemplateControlBounds(id, chart);
            var firstTitle = Title(chart.Series[0]); var firstBounds = firstTitle is null ? default : TemplateControlBounds(firstTitle, chart);
            var initialImage = Path.Combine(directory, stem + $"-control-template-initial-{(dark ? "dark" : "light")}.png");
            var initialRaster = CaptureControlTemplateRaster(chart, initialImage);
            var firstForeground = firstTitle?.Foreground?.ToString();
            var firstOpacity = firstTitle?.Opacity;
            var firstParents = firstTitle?.GetVisualAncestors().Select(visual => new ControlTemplateAncestorEvidence(visual.GetType().Name,
                (visual as Control)?.Name, visual.Bounds, visual.IsVisible, visual.Opacity)).ToList() ?? [];
            var idPixels = initialRaster.Count(TemplatePixelRect(idBounds), ink, 24);
            var firstPixels = initialRaster.Count(TemplatePixelRect(firstBounds), ink, 24);
            checks[$"控件模板{label}业务编号文字有非空像素"] = id is { Bounds.Width: > 20 } && chart.PlotArea.Contains(idBounds) && idPixels >= 5;
            var withoutGlyphImage = Path.Combine(directory, stem + $"-control-template-without-first-glyph-{(dark ? "dark" : "light")}.png");
            var restoredGlyphImage = Path.Combine(directory, stem + $"-control-template-restored-first-glyph-{(dark ? "dark" : "light")}.png");
            var initialView = chart.Viewport; var initialPlot = chart.PlotArea;
            ShapeRaster withoutGlyph;
            try
            {
                if (firstTitle is not null) firstTitle.Opacity = 0;
                FlushControlTemplateWindow(window);
                withoutGlyph = CaptureControlTemplateRaster(chart, withoutGlyphImage);
            }
            finally
            {
                if (firstTitle is not null && firstOpacity is { } opacity) firstTitle.Opacity = opacity;
                FlushControlTemplateWindow(window);
            }
            var restoredGlyph = CaptureControlTemplateRaster(chart, restoredGlyphImage);
            var glyphChanged = CountControlTemplateExactChanges(initialRaster, withoutGlyph, firstBounds);
            var restoreChanged = CountControlTemplateExactChanges(initialRaster, restoredGlyph, firstBounds);
            var geometryUnchanged = firstTitle is not null && TemplateControlBounds(firstTitle, chart) == firstBounds && chart.PlotArea == initialPlot && chart.Viewport == initialView;
            checks[$"控件模板{label}图例首项文字有非空像素"] = firstTitle is { Text: "直营订单", Bounds.Width: > 20 } &&
                firstTitle.Foreground is ISolidColorBrush firstBrush && firstBrush.Color == ink && new Rect(chart.Bounds.Size).Contains(firstBounds) &&
                geometryUnchanged && glyphChanged >= 5 && restoreChanged == 0;
            var scroll = chart.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "PART_ChartLegendScrollViewer");
            var startOffset = scroll.Offset; var beforeViewport = chart.Viewport;
            var scrollBounds = TemplateControlBounds(scroll, chart);
            var wheel = RouteControlTemplateWheel(chart, window, scrollBounds.Center, -100);
            checks[$"控件模板{label}合成滚轮滚动真实图例而不缩放"] = native && wheel.Handled && scroll.Extent.Height > scroll.Viewport.Height &&
                scroll.Viewport.Height > 0 && scroll.Viewport.Height <= 205.01 && scroll.Offset.Y > startOffset.Y && chart.Viewport == beforeViewport;
            var last = chart.Series[^1]; var tail = Title(last);
            var tailBounds = tail is null ? default : TemplateControlBounds(tail, chart);
            var tailRaster = TemplateChartRaster(chart); var tailPixels = tailRaster.Count(TemplatePixelRect(tailBounds), ink, 24);
            var tailItem = chart.LegendItems.FirstOrDefault(item => ReferenceEquals(item.Series, last));
            checks[$"控件模板{label}滚动末项文字与可见命中区域一致"] = tail is { Text: "候选渠道 16", Bounds.Width: > 20 } && tailPixels >= 5 &&
                tailItem is not null && tailItem.Bounds.Height > 5 && scrollBounds.Intersects(tailBounds) && new Rect(chart.Bounds.Size).Contains(tailItem.Bounds);
            var visibleCount = chart.Series.Count(series => series.IsVisible);
            if (tailItem is not null) RouteControlTemplateClick(chart, window, tailItem.Bounds.Center);
            checks[$"控件模板{label}末项合成点击切换正确系列"] = last.IsVisible && chart.Series.Count(series => series.IsVisible) == visibleCount + 1 &&
                chart.Series.Take(chart.Series.Count - 1).Count(series => series.IsVisible) == visibleCount;
            chart.ToggleSeriesVisibility(last); FlushControlTemplateWindow(window);
            records[0].Value = 54; FlushControlTemplateWindow(window); primary = PrimaryCard();
            checks[$"控件模板{label}业务INPC更新同对象标签和坐标"] = primary is not null && primary.DataContext is ChartDataLabelContext updated && ReferenceEquals(updated.Data.Item, records[0]) && updated.Data.Point == new ChartPoint(1, 54) &&
                primary.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "ControlDataLabelValue" && text.Text == "54 单");
            chart.Series[0].DataLabelFormatter = _ => null; FlushControlTemplateWindow(window);
            checks[$"控件模板{label}空格式文本仍构建业务模板"] = PrimaryCard() is { Bounds.Width: > 20 } && PrimaryCard()!.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "ControlDataLabelId" && text.Text == "template-primary");
            chart.Series[0].DataLabelFormatter = null; records[0].Value = 37; FlushControlTemplateWindow(window);
            var legendTemplate = chart.LegendItemTemplate; var labelTemplate = chart.Series[0].DataLabelTemplate;
            var previousLabel = PrimaryCard(); var previousLegend = Title(last);
            chart.Series[0].DataLabelTemplate = null; chart.LegendItemTemplate = null; FlushControlTemplateWindow(window);
            var removed = previousLabel is not null && previousLabel.GetVisualParent() is null && previousLegend is not null && !previousLegend.GetVisualAncestors().Contains(chart) &&
                !chart.GetVisualDescendants().OfType<Border>().Any(control => control.Name is "ControlDataLabelCard" or "ControlLegendCard");
            chart.Series[0].DataLabelTemplate = labelTemplate; chart.LegendItemTemplate = legendTemplate; FlushControlTemplateWindow(window);
            scroll = chart.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "PART_ChartLegendScrollViewer");
            scroll.Offset = new Vector(0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)); FlushControlTemplateWindow(window);
            checks[$"控件模板{label}切回默认卸载并恢复真实模板"] = removed && PrimaryCard() is { Bounds.Width: > 20 } && Title(last) is { Bounds.Width: > 20 };
            primary = PrimaryCard(); id = primary?.GetVisualDescendants().OfType<TextBlock>().SingleOrDefault(text => text.Name == "ControlDataLabelId");
            tail = Title(last); idBounds = id is null ? default : TemplateControlBounds(id, chart); tailBounds = tail is null ? default : TemplateControlBounds(tail, chart);
            var image = Path.Combine(directory, stem + $"-control-template-pixels-{(dark ? "dark" : "light")}.png");
            SaveTemplateChartImage(chart, image);
            var exportImage = Path.Combine(directory, stem + $"-control-template-export-{(dark ? "dark" : "light")}.png");
            var exports = new List<ControlTemplateExportEvidence>(); ShapeRaster? baseline = null;
            foreach (var dpi in new[] { 96d, 144d, 192d })
            {
                using var stream = new MemoryStream();
                var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(960, 540), Dpi = dpi, Background = chart.Background });
                var bytes = stream.ToArray(); stream.Position = 0;
                using var bitmap = new Bitmap(stream); var raster = ReadShapePixels(bitmap);
                var scale = Math.Min(960 / chart.Bounds.Width, 540 / chart.Bounds.Height);
                Rect ExportRegion(Rect bounds) => new((960 - chart.Bounds.Width * scale) / 2 + bounds.X * scale,
                    (540 - chart.Bounds.Height * scale) / 2 + bounds.Y * scale, bounds.Width * scale, bounds.Height * scale);
                var idRegion = ExportRegion(idBounds); var tailRegion = ExportRegion(tailBounds);
                var idInk = raster.Count(TemplatePixelRect(idRegion), ink, 24); var tailInk = raster.Count(TemplatePixelRect(tailRegion), ink, 24);
                var same = baseline is null || baseline.Pixels.AsSpan().SequenceEqual(raster.Pixels);
                var density = TryReadCommonPngDensity(bytes, out var x, out var y, out var unit) && x == (uint)Math.Round(dpi / .0254) && y == x && unit == 1;
                checks[$"控件模板{label}{dpi:0}DPI导出包含编号与末项且像素不变"] = exported && raster.Width == 960 && raster.Height == 540 && idInk >= 5 && tailInk >= 5 && same && density;
                exports.Add(new(dpi, exported, raster.Width, raster.Height, idRegion, idInk, tailRegion, tailInk, same, density));
                baseline ??= raster; if (dpi == 96) File.WriteAllBytes(exportImage, bytes);
            }
            _controlTemplateEvidence.Add(new(label, native, pageImage, image, exportImage, ink.ToString(), idBounds, idPixels, firstBounds, firstPixels,
                tailBounds, tailPixels, startOffset, scroll.Offset, scroll.Extent, scroll.Viewport, wheel.RoutedChartPosition, wheel.Handled, exports,
                initialImage, firstForeground, firstOpacity, firstParents, 24, glyphChanged, restoreChanged, restoreChanged == 0, geometryUnchanged, withoutGlyphImage, restoredGlyphImage));
        }
        finally { window.Close(); chart.Series.Clear(); }
    }

    private static void FlushControlTemplateWindow(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }

    private static ShapeRaster CaptureControlTemplateRaster(CartesianChart chart, string image)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(chart.Bounds.Width), (int)Math.Ceiling(chart.Bounds.Height)), new Vector(96, 96));
        bitmap.Render(chart);
        var raster = ReadShapePixels(bitmap);
        Directory.CreateDirectory(Path.GetDirectoryName(image)!);
        bitmap.Save(image);
        return raster;
    }

    private static int CountControlTemplateExactChanges(ShapeRaster before, ShapeRaster after, Rect bounds)
    {
        if (before.Width != after.Width || before.Height != after.Height || before.Stride != after.Stride) return -1;
        var region = TemplatePixelRect(bounds); var changed = 0;
        for (var y = Math.Max(0, region.Y); y < Math.Min(before.Height, region.Bottom); y++)
            for (var x = Math.Max(0, region.X); x < Math.Min(before.Width, region.Right); x++)
            {
                var index = y * before.Stride + x * 4;
                if (!before.Pixels.AsSpan(index, 4).SequenceEqual(after.Pixels.AsSpan(index, 4))) changed++;
            }
        return changed;
    }

    private static ControlTemplateWheelEvidence RouteControlTemplateWheel(CartesianChart chart, Window window, Point local, double delta)
    {
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var position = chart.TranslatePoint(local, window) ?? throw new InvalidOperationException("图例模板未连接到原生窗口。");
        var args = new PointerWheelEventArgs(chart, pointer, window, position, 1, default, KeyModifiers.None, new Vector(0, delta)) { RoutedEvent = InputElement.PointerWheelChangedEvent };
        var routedPosition = args.GetPosition(chart); chart.RaiseEvent(args); FlushControlTemplateWindow(window);
        return new(routedPosition, args.Handled);
    }

    private static void RouteControlTemplateClick(CartesianChart chart, Window window, Point local)
    {
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var position = chart.TranslatePoint(local, window) ?? throw new InvalidOperationException("图例模板未连接到原生窗口。");
        chart.RaiseEvent(new PointerPressedEventArgs(chart, pointer, window, position, 1, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1) { RoutedEvent = InputElement.PointerPressedEvent });
        chart.RaiseEvent(new PointerReleasedEventArgs(chart, pointer, window, position, 2, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left) { RoutedEvent = InputElement.PointerReleasedEvent });
        FlushControlTemplateWindow(window);
    }

    private sealed record ControlTemplateWheelEvidence(Point RoutedChartPosition, bool Handled);
    private sealed record ControlTemplateAncestorEvidence(string Type, string? Name, Rect Bounds, bool IsVisible, double Opacity);
    private sealed record ControlTemplateExportEvidence(double Dpi, bool Exported, int Width, int Height, Rect BusinessIdRegion, int BusinessIdInkPixels, Rect LastLegendRegion, int LastLegendInkPixels, bool SameRasterAs96Dpi, bool DensityMatches);
    private sealed record ControlTemplateSmokeEvidence(string Theme, bool NativeWindowCreated, string PageImage, string PixelImage, string ExportImage, string TextInkColor,
        Rect BusinessIdRegion, int BusinessIdInkPixels, Rect FirstLegendRegion, int FirstLegendInkPixels, Rect LastLegendRegion, int LastLegendInkPixels,
        Vector InitialLegendOffset, Vector FinalLegendOffset, Size LegendExtent, Size LegendViewport, Point RoutedWheelPosition, bool RoutedWheelHandled, List<ControlTemplateExportEvidence> Exports,
        string InitialImage, string? FirstTextForeground, double? FirstTextOpacity, List<ControlTemplateAncestorEvidence> FirstTextAncestors,
        int FirstLegendNearColorTolerance, int FirstLegendGlyphChangedPixels, int FirstLegendRestoredChangedPixels, bool FirstLegendRestoreSame, bool FirstLegendGeometryUnchanged,
        string WithoutGlyphImage, string RestoredGlyphImage);
}
