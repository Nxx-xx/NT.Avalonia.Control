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
    private readonly List<TooltipTemplatePixelEvidence> _tooltipTemplateEvidence = [];

    private async Task RunTooltipTemplateSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        var chart = _tooltipReadingChart ?? throw new InvalidOperationException("提示卡示例尚未创建。");
        _tooltipReadingExamples!.IsExpanded = true;
        await SmokeFlushAsync();
        _tooltipReadingExamples.BringIntoView();
        await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!;
        var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var name = theme == ThemeVariant.Dark ? "暗色" : "亮色";
            _hostWindow.RequestedThemeVariant = theme;
            await SmokeFlushAsync();
            HoverTemplateChart(chart, _hostWindow, TemplateProject(chart, _readingResponses[1].X, _readingResponses[1].Y));
            var card = FindTemplateCard(chart);
            checks[$"提示模板{name}业务控件实际布局"] = card is { Bounds.Width: > 50, Bounds.Height: > 30 } &&
                card.DataContext is ChartTooltipContext { Hits.Count: 2 } &&
                card.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "BusinessTooltipId" && text.Text == "response-b");
            var image = Path.Combine(directory, stem + $"-tooltip-template-{(theme == ThemeVariant.Dark ? "dark" : "light")}.png");
            CaptureSmokeImage(image);
            VerifyTemplateNativePixels(checks, theme, directory, stem, image);
        }
        _tooltipReadingExamples.IsExpanded = false;
    }

    private void VerifyTemplateNativePixels(Dictionary<string, bool> checks, ThemeVariant theme, string directory, string stem, string pageImage)
    {
        var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
        var chart = CreateTemplatePixelChart(dark);
        var window = new Window { Title = "Charts · 提示模板与参考线像素检查", Width = 960, Height = 540, Content = chart, RequestedThemeVariant = theme };
        try
        {
            window.Show(_hostWindow); window.UpdateLayout();
            var native = window.TryGetPlatformHandle() is { Handle: not 0 };
            var anchor = TemplateProject(chart, 2, 37);
            var pointerRoute = HoverTemplateChart(chart, window, anchor);
            var card = FindTemplateCard(chart);
            var context = card?.DataContext as ChartTooltipContext;
            var identity = new TooltipIdentityEvidence(pointerRoute.EventPositionAfterRouting, context?.Anchor, pointerRoute, chart.PlotArea,
                native, context is not null, context?.Hits.Count ?? 0, ReferenceEquals(context?.Primary.Item, _readingResponses[1]),
                pointerRoute.EventPositionBeforeRouting == pointerRoute.EventPositionAfterRouting,
                context is not null && new Vector(context.Anchor.X - pointerRoute.EventPositionAfterRouting.X, context.Anchor.Y - pointerRoute.EventPositionAfterRouting.Y).Length < 1e-7,
                context?.Hits.Any(hit => ReferenceEquals(hit.Item, _readingVolumes[1])) == true,
                context?.Hits.Any(hit => ReferenceEquals(hit.Item, _readingVolumes[1]) && hit.Point == new ChartPoint(7, Math.Pow(10, 1.48))) == true,
                context?.Hits.Any(hit => ReferenceEquals(hit.Item, _readingVolumes[1]) && Equals(hit.StableId, "volume-b")) == true,
                context is null ? [] : context.Hits.Select(hit => new TooltipHitIdentity(hit.Series.Title, hit.Index, hit.Point, hit.Position,
                    hit.StableId?.ToString(), hit.StableId?.GetType().FullName, ReferenceEquals(hit.Item, _readingResponses[1]), ReferenceEquals(hit.Item, _readingVolumes[1]))).ToList());
            checks[$"提示模板{label}SharedY保留原始对象坐标和ID"] = identity.NativeWindow && identity.ContextPresent && identity.HitCount == 2 &&
                identity.PrimaryIsResponseRecord && identity.EventPositionStableDuringRouting && identity.AnchorMatchesRoutedPosition && identity.VolumePointMatchesOriginal && identity.VolumeStableIdMatches;
            checks[$"提示模板{label}逐点SharedY数学结果一致"] = TemplateSharedYMatchesOracle(chart, pointerRoute.EventPositionAfterRouting);
            var reading = chart.AxisReadings;
            checks[$"提示模板{label}多轴读数来自各自反算"] = reading.Count == 3 &&
                reading.Any(row => ReferenceEquals(row.Axis, chart.XAxis) && row.IsHorizontal && Math.Abs(row.Value - 2) < 1e-8 && row.Text == 2d.ToString("0.0")) &&
                reading.Any(row => ReferenceEquals(row.Axis, chart.YAxis) && !row.IsHorizontal && Math.Abs(row.Value - 37) < 1e-8) &&
                reading.Any(row => ReferenceEquals(row.Axis, chart.Axes[0]) && !row.IsHorizontal && Math.Abs(row.Value - Math.Pow(10, 1.48)) < 1e-7);
            chart.ShowCrosshairLabels = false; window.UpdateLayout();
            checks[$"提示模板{label}隐藏轴标签仍发布读数"] = chart.AxisReadings.Count == 3;
            var noLabels = TemplateChartRaster(chart);
            chart.ShowCrosshairLabels = true; window.UpdateLayout();
            var labels = TemplateChartRaster(chart);
            var tooltipBounds = FindTemplateCard(chart)?.GetVisualParent() is Control tooltipChrome ? TemplateControlBounds(tooltipChrome, chart) : default;
            var axisRegions = new[] { (Axis: chart.YAxis, Name: "Near-primary-Y"), (Axis: chart.Axes[0], Name: "Far-log-Y"), (Axis: chart.XAxis, Name: "Near-primary-X") }
                .Select(pair =>
                {
                    var bounds = chart.GetAxisLayout(pair.Axis)?.Bounds ?? default;
                    return new TooltipAxisLabelPixels(pair.Name, bounds, CountTemplatePixelChanges(noLabels, labels, bounds, tooltipBounds));
                }).ToList();
            checks[$"提示模板{label}轴读数标签实际改变像素"] = chart.AxisReadings.Count == 3 && axisRegions.Count == 3 && axisRegions.All(region => region.ChangedPixels >= 5);
            chart.ShowCrosshairLabels = false;

            var directions = new List<TooltipDirectionPixels>();
            foreach (var direction in new[] { ChartCrosshairDirection.Both, ChartCrosshairDirection.Horizontal, ChartCrosshairDirection.Vertical })
            {
                chart.CrosshairDirection = direction; chart.ShowCrosshair = false; window.UpdateLayout();
                var linesOffRaster = TemplateChartRaster(chart);
                var primary = chart.HoveredPoint ?? throw new InvalidOperationException("参考线像素检查缺少实际主命中。");
                var projected = TemplateProject(chart, primary.Point.X, primary.Point.Y, primary.Series.YAxisKey == "reading-volume");
                var positionMatchesProjection = Math.Abs(projected.X - primary.Position.X) < 1e-7 && Math.Abs(projected.Y - primary.Position.Y) < 1e-7;
                chart.ShowCrosshair = true; window.UpdateLayout();
                var raster = TemplateChartRaster(chart); var plot = chart.PlotArea; var ink = Color.Parse("#C615DA");
                var horizontalRegion = new Rect(Math.Ceiling(plot.Left) + 4, Math.Round(projected.Y) - 2,
                    Math.Max(0, Math.Floor(projected.X - plot.Left) - 22), 5);
                var verticalRegion = new Rect(Math.Round(projected.X) - 2, Math.Ceiling(projected.Y) + 20, 5,
                    Math.Max(0, Math.Floor(plot.Bottom - projected.Y) - 26));
                var horizontal = raster.Count(TemplatePixelRect(horizontalRegion), ink, 18);
                var vertical = raster.Count(TemplatePixelRect(verticalRegion), ink, 18);
                var changedHorizontal = CountTemplatePixelChanges(linesOffRaster, raster, horizontalRegion, tooltipBounds);
                var changedVertical = CountTemplatePixelChanges(linesOffRaster, raster, verticalRegion, tooltipBounds);
                directions.Add(new(direction.ToString(), primary.Position, projected, positionMatchesProjection, horizontalRegion, verticalRegion,
                    horizontal, vertical, changedHorizontal, changedVertical));
                checks[$"提示模板{label}{direction}参考线像素方向"] =
                    positionMatchesProjection && (direction == ChartCrosshairDirection.Vertical ? changedHorizontal == 0 : changedHorizontal >= 8) &&
                    (direction == ChartCrosshairDirection.Horizontal ? changedVertical == 0 : changedVertical >= 8);
            }
            chart.CrosshairDirection = ChartCrosshairDirection.Both; chart.ShowCrosshairLabels = true;
            HoverTemplateChart(chart, window, anchor);
            var pixels = TemplateChartRaster(chart);
            var badgeInk = pixels.Count(new PixelRect(0, 0, pixels.Width, pixels.Height), Color.Parse("#D7259F"), 4);
            var businessText = FindTemplateCard(chart)?.GetVisualDescendants().OfType<TextBlock>().SingleOrDefault(text => text.Name == "BusinessTooltipId" && text.Text == "response-b");
            var textBounds = businessText is null ? default : TemplateControlBounds(businessText, chart);
            var textColor = Color.Parse(dark ? "#FFE178" : "#8A3500");
            var nativeTextInk = pixels.Count(TemplatePixelRect(textBounds), textColor, 24);
            checks[$"提示模板{label}真实业务徽标非空像素"] = badgeInk >= 50 && businessText is { Bounds.Width: > 20, Bounds.Height: > 5 } && nativeTextInk >= 5;
            var pixelImage = Path.Combine(directory, stem + $"-tooltip-template-pixels-{(dark ? "dark" : "light")}.png");
            SaveTemplateChartImage(chart, pixelImage);
            var exportImage = Path.Combine(directory, stem + $"-tooltip-template-export-{(dark ? "dark" : "light")}.png");
            var exportEvidence = new List<TooltipExportPixels>(); ShapeRaster? baseline = null;
            foreach (var dpi in new[] { 96d, 144d, 192d })
            {
                using var stream = new MemoryStream();
                var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(960, 540), Dpi = dpi, Background = chart.Background });
                var bytes = stream.ToArray(); stream.Position = 0;
                using var decoded = new Bitmap(stream); var raster = ReadShapePixels(decoded);
                var ink = raster.Count(new PixelRect(0, 0, raster.Width, raster.Height), Color.Parse("#D7259F"), 4);
                var scale = Math.Min(960 / chart.Bounds.Width, 540 / chart.Bounds.Height);
                var exportTextBounds = new Rect((960 - chart.Bounds.Width * scale) / 2 + textBounds.X * scale,
                    (540 - chart.Bounds.Height * scale) / 2 + textBounds.Y * scale, textBounds.Width * scale, textBounds.Height * scale);
                var textInk = raster.Count(TemplatePixelRect(exportTextBounds), textColor, 24);
                var same = baseline is null || raster.Pixels.AsSpan().SequenceEqual(baseline.Pixels);
                var density = TryReadCommonPngDensity(bytes, out var x, out var y, out var unit) && x == (uint)Math.Round(dpi / .0254) && x == y && unit == 1;
                exportEvidence.Add(new(dpi, exported, raster.Width, raster.Height, ink, exportTextBounds, textInk, same, density));
                checks[$"提示模板{label}{dpi:0}DPI导出包含真实控件且像素不变"] = exported && raster.Width == 960 && raster.Height == 540 && ink >= 50 && textInk >= 5 && same && density;
                baseline ??= raster;
                if (dpi == 96) File.WriteAllBytes(exportImage, bytes);
            }
            var previousCard = FindTemplateCard(chart);
            chart.TooltipContentTemplate = null; window.UpdateLayout();
            checks[$"提示模板{label}切回默认提示卸载业务控件"] = previousCard is not null && FindTemplateCard(chart) is null && previousCard.GetVisualParent() is null && chart.HoveredPoints.Count == 2;
            chart.TooltipContentTemplate = _businessTooltipTemplate; window.UpdateLayout();
            chart.IsTooltipEnabled = false; window.UpdateLayout();
            checks[$"提示模板{label}关闭提示清理控件命中和读数"] = FindTemplateCard(chart) is null && chart.HoveredPoint is null && chart.HoveredPoints.Count == 0 && chart.AxisReadings.Count == 0;
            chart.IsTooltipEnabled = true; HoverTemplateChart(chart, window, anchor);
            var before = _readingResponses[1].Y;
            _readingResponses[1].Y = 46; window.UpdateLayout();
            HoverTemplateChart(chart, window, TemplateProject(chart, 2, 46));
            var updated = FindTemplateCard(chart)?.DataContext as ChartTooltipContext;
            checks[$"提示模板{label}业务属性更新后保留原对象新数值"] = updated is not null && updated.Hits.Any(hit => ReferenceEquals(hit.Item, _readingResponses[1]) && hit.Point.Y == 46) &&
                FindTemplateCard(chart)!.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "BusinessTooltipId" && text.Text == "response-b");
            _readingResponses[1].Y = before; window.UpdateLayout();
            HoverTemplateChart(chart, window, anchor);
            using var exitPointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            chart.RaiseEvent(new PointerEventArgs(InputElement.PointerExitedEvent, chart, exitPointer, window, new Point(-1, -1), 1, default, KeyModifiers.None));
            window.UpdateLayout();
            checks[$"提示模板{label}离开图表清理业务控件和读数"] = FindTemplateCard(chart) is null && chart.AxisReadings.Count == 0;
            _tooltipTemplateEvidence.Add(new(label, native, pageImage, pixelImage, exportImage, badgeInk, textColor.ToString(), textBounds, nativeTextInk, identity, axisRegions, directions, exportEvidence));
        }
        finally { window.Close(); chart.Series.Clear(); }
    }

    private CartesianChart CreateTemplatePixelChart(bool dark)
    {
        var chart = new CartesianChart
        {
            Background = Brush(dark ? "#111722" : "#F8FAFC"), Margin = new Thickness(12),
            XAxis = new NumericAxis { Title = "观察时间", Minimum = 0, Maximum = 10, LabelFormat = "0.0" },
            YAxis = new NumericAxis { Title = "响应", Minimum = 0, Maximum = 100, LabelFormat = "0.0" },
            TooltipMode = ChartTooltipMode.SharedY, ShowCrosshair = true, ShowCrosshairLabels = true,
            CrosshairBrush = Brush("#C615DA"), TooltipContentTemplate = _businessTooltipTemplate,
            TooltipForeground = Brush(dark ? "#FFE178" : "#8A3500"),
        };
        chart.Axes.Add(new LogarithmicAxis { Key = "reading-volume", Title = "任务量", Minimum = 1, Maximum = 10000, Position = ChartAxisPosition.Far, ShowGridLines = false, LabelFormat = "0.00" });
        chart.Series.Add(new LineSeries { Title = "响应时间", ItemsSource = _readingResponses, CategoryPath = nameof(TooltipBusinessRecord.X), ValuePath = nameof(TooltipBusinessRecord.Y), IdPath = nameof(TooltipBusinessRecord.Id), Stroke = Blue, MarkerSize = 9 });
        chart.Series.Add(new ScatterSeries { Title = "任务量", ItemsSource = _readingVolumes, CategoryPath = nameof(TooltipBusinessRecord.X), ValuePath = nameof(TooltipBusinessRecord.Y), IdPath = nameof(TooltipBusinessRecord.Id), YAxisKey = "reading-volume", Fill = Teal, Stroke = Teal, MarkerSize = 10 });
        return chart;
    }

    private bool TemplateSharedYMatchesOracle(CartesianChart chart, Point anchor)
    {
        if (chart.HoveredPoints.Count != 2) return false;
        foreach (var series in chart.Series)
        {
            var source = ReferenceEquals(series, chart.Series[0]) ? _readingResponses : _readingVolumes;
            var expected = source.Select((item, ordinal) => new { Item = item, Ordinal = ordinal, Screen = TemplateProject(chart, item.X, item.Y, !ReferenceEquals(series, chart.Series[0])) })
                .OrderBy(item => Math.Abs(item.Screen.Y - anchor.Y)).ThenBy(item => Math.Abs(item.Screen.X - anchor.X)).ThenByDescending(item => item.Ordinal).First();
            var hit = chart.HoveredPoints.SingleOrDefault(item => ReferenceEquals(item.Series, series));
            if (hit is null || !ReferenceEquals(hit.Item, expected.Item) || hit.Index != expected.Ordinal || hit.Point != new ChartPoint(expected.Item.X, expected.Item.Y)) return false;
        }
        return true;
    }

    private static Point TemplateProject(CartesianChart chart, double x, double y, bool logarithmic = false)
    { var plot = chart.PlotArea; return new(plot.Left + plot.Width * x / 10, plot.Bottom - plot.Height * (logarithmic ? Math.Log10(y) / 4 : y / 100)); }

    private static TooltipPointerRouteEvidence HoverTemplateChart(CartesianChart chart, Window window, Point point)
    {
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var target = chart.TranslatePoint(point, window) ?? throw new InvalidOperationException("提示卡数据点无法转换窗口坐标。");
        var routed = new PointerEventArgs(InputElement.PointerMovedEvent, chart, pointer, window, target, 1, default, KeyModifiers.None);
        var before = routed.GetPosition(chart);
        chart.RaiseEvent(routed);
        var after = routed.GetPosition(chart);
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        return new(point, target, before, after);
    }
    private static Border? FindTemplateCard(CartesianChart chart) => chart.GetVisualDescendants().OfType<Border>().SingleOrDefault(control => control.Name == "BusinessTooltipCard");
    private static ShapeRaster TemplateChartRaster(CartesianChart chart)
    { using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(chart.Bounds.Width), (int)Math.Ceiling(chart.Bounds.Height)), new Vector(96, 96)); bitmap.Render(chart); return ReadShapePixels(bitmap); }
    private static void SaveTemplateChartImage(CartesianChart chart, string path)
    { using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(chart.Bounds.Width), (int)Math.Ceiling(chart.Bounds.Height)), new Vector(96, 96)); bitmap.Render(chart); Directory.CreateDirectory(Path.GetDirectoryName(path)!); bitmap.Save(path); }

    private static Rect TemplateControlBounds(Control control, CartesianChart chart) => control.TranslatePoint(default, chart) is { } origin ? new Rect(origin, control.Bounds.Size) : default;
    private static PixelRect TemplatePixelRect(Rect bounds)
    {
        var x = (int)Math.Floor(bounds.X); var y = (int)Math.Floor(bounds.Y);
        return new(x, y, Math.Max(0, (int)Math.Ceiling(bounds.Right) - x), Math.Max(0, (int)Math.Ceiling(bounds.Bottom) - y));
    }
    private static int CountTemplatePixelChanges(ShapeRaster before, ShapeRaster after, Rect region, Rect exclude)
    {
        var count = 0; var box = TemplatePixelRect(region);
        if (before.Width != after.Width || before.Height != after.Height || before.Stride != after.Stride) return 0;
        for (var y = Math.Max(0, box.Y); y < Math.Min(after.Height, box.Bottom); y++)
            for (var x = Math.Max(0, box.X); x < Math.Min(after.Width, box.Right); x++)
            {
                if (exclude.Contains(new Point(x + .5, y + .5))) continue;
                var offset = y * after.Stride + x * 4;
                if (Math.Abs(before.Pixels[offset] - after.Pixels[offset]) > 12 || Math.Abs(before.Pixels[offset + 1] - after.Pixels[offset + 1]) > 12 ||
                    Math.Abs(before.Pixels[offset + 2] - after.Pixels[offset + 2]) > 12) count++;
            }
        return count;
    }

    private sealed record TooltipHitIdentity(string? SeriesTitle, int SourceIndex, ChartPoint Point, Point Position, string? StableId, string? StableIdType,
        bool IsExpectedResponseRecord, bool IsExpectedVolumeRecord);
    private sealed record TooltipPointerRouteEvidence(Point RequestedChartLocal, Point SuppliedWindowLocal, Point EventPositionBeforeRouting, Point EventPositionAfterRouting);
    private sealed record TooltipIdentityEvidence(Point RoutedAnchor, Point? ContextAnchor, TooltipPointerRouteEvidence PointerRoute, Rect PlotArea, bool NativeWindow, bool ContextPresent, int HitCount,
        bool PrimaryIsResponseRecord, bool EventPositionStableDuringRouting, bool AnchorMatchesRoutedPosition, bool VolumeItemMatchesOriginal, bool VolumePointMatchesOriginal, bool VolumeStableIdMatches, List<TooltipHitIdentity> Hits);
    private sealed record TooltipDirectionPixels(string Direction, Point ActualPrimaryPosition, Point IndependentlyProjectedPosition, bool PositionMatchesProjection,
        Rect HorizontalRegion, Rect VerticalRegion, int HorizontalInkPixels, int VerticalInkPixels, int HorizontalPixelsChangedFromLinesOff, int VerticalPixelsChangedFromLinesOff);
    private sealed record TooltipAxisLabelPixels(string AxisRole, Rect Region, int ChangedPixels);
    private sealed record TooltipExportPixels(double Dpi, bool Exported, int Width, int Height, int BadgeInkPixels, Rect BusinessTextRegion, int BusinessTextInkPixels, bool SameRasterAs96Dpi, bool DensityMatches);
    private sealed record TooltipTemplatePixelEvidence(string Theme, bool NativeWindowCreated, string PageImage, string PixelImage, string ExportImage, int BadgeInkPixels,
        string BusinessTextInkColor, Rect BusinessTextRegion, int BusinessTextInkPixels, TooltipIdentityEvidence Identity, List<TooltipAxisLabelPixels> AxisLabelRegions, List<TooltipDirectionPixels> Directions, List<TooltipExportPixels> Exports);
}
