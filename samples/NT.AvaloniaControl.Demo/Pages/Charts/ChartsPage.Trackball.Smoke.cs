using System.Collections.ObjectModel;
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
    private readonly List<TrackballSmokeEvidence> _trackballEvidence = [];

    private async Task RunTrackballSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _trackballExamples!.IsExpanded = true;
        await SmokeFlushAsync(); _trackballExamples.BringIntoView(); await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
            _hostWindow.RequestedThemeVariant = theme; await SmokeFlushAsync();
            HoverTemplateChart(_trackballChart!, _hostWindow, TrackballProject(_trackballChart!, 3, 40));
            checks[$"Trackball{label}页面显示真实追踪业务卡"] = _trackballChart!.TrackballReadings.Count == 2 && TrackballCards(_trackballChart).Any(card => card.Bounds.Width > 30 && card.DataContext is ChartTooltipContext { Readings.Count: 2 });
            var pageImage = Path.Combine(directory, stem + $"-trackball-{(dark ? "dark" : "light")}.png");
            CaptureSmokeImage(pageImage);
            await VerifyTrackballNativeAsync(checks, theme, directory, stem, pageImage);
        }
        _trackballExamples.IsExpanded = false;
    }

    private async Task VerifyTrackballNativeAsync(Dictionary<string, bool> checks, ThemeVariant theme, string directory, string stem, string pageImage)
    {
        var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
        var primary = new ObservableCollection<TrackballBusinessRecord> { new("track-a0", "09:00", 1, 20), new("track-a1", "09:40", 5, 60), new("track-a2", "10:20", 9, 35) };
        var secondary = new ObservableCollection<TrackballBusinessRecord> { new("track-b0", "批次 A", 0, 80), new("track-b1", "批次 B", 4, 40), new("track-b2", "批次 C", 8, 70) };
        var chart = new CartesianChart { Margin = new Thickness(12), Background = Brush(dark ? "#111722" : "#F8FAFC"), Foreground = Brush(dark ? "#EAF0FB" : "#172238"),
            TooltipBackground = Brush(dark ? "#202B40" : "#FFFFFF"), TooltipForeground = Brush(dark ? "#FFE178" : "#8A3500"),
            TrackballLineStroke = Brush("#D7259F"), TrackballLineStrokeThickness = 1.5,
            TrackballMarkerStroke = Brush("#D7259F"), TrackballMarkerFill = Brushes.Transparent, TrackballMarkerSize = 14, TrackballMarkerStrokeThickness = 2 };
        ConfigureTrackballChart(chart, primary, secondary);
        var template = CreateTrackballBusinessTemplate(chart.TooltipForeground); chart.TrackballContentTemplate = template; chart.TooltipContentTemplate = template;
        var window = new Window { Title = "Charts · Trackball 与线段端点原生检查", Width = 1120, Height = 720, RequestedThemeVariant = theme, Content = chart };
        try
        {
            window.Show(_hostWindow); FlushControlTemplateWindow(window); await Task.Delay(80); FlushControlTemplateWindow(window);
            var native = window.TryGetPlatformHandle() is { Handle: not 0 };
            var hit = chart.HitTestSegment(TrackballProject(chart, 3, 40), 8);
            checks[$"Trackball{label}公开线段命中数学与真实端点一致"] = native && hit is not null && ReferenceEquals(hit.Series, chart.Series[0]) &&
                ReferenceEquals(hit.Start.Item, primary[0]) && ReferenceEquals(hit.End.Item, primary[1]) && hit.Start.Point == new ChartPoint(1, 20) && hit.End.Point == new ChartPoint(5, 60) &&
                hit.Start.Index == 0 && hit.End.Index == 1 && Equals(hit.Start.StableId, "track-a0") && Equals(hit.End.StableId, "track-a1") &&
                Math.Abs(hit.Fraction - .5) < 1e-8 && NearTrackballPoint(hit.Point, new ChartPoint(3, 40));
            var modes = new List<TrackballModeEvidence>();
            foreach (var direction in new[] { ChartTrackballMode.Vertical, ChartTrackballMode.Horizontal })
                foreach (var display in new[] { ChartTrackballDisplayMode.GroupAllPoints, ChartTrackballDisplayMode.FloatAllPoints })
                    foreach (var snap in new[] { ChartTrackballSnapMode.NearestPoint, ChartTrackballSnapMode.LinearInterpolation })
                    {
                        chart.TrackballMode = direction; chart.TrackballDisplayMode = display; chart.TrackballSnapMode = snap;
                        FlushControlTemplateWindow(window);
                        var route = HoverTemplateChart(chart, window, TrackballProject(chart, snap == ChartTrackballSnapMode.NearestPoint ? 2.5 : 3, snap == ChartTrackballSnapMode.NearestPoint ? 38 : 40));
                        var valid = TrackballMatchesMath(chart, primary, secondary, route.EventPositionAfterRouting);
                        var cards = TrackballCards(chart);
                        var cardCount = display == ChartTrackballDisplayMode.GroupAllPoints ? 1 : 2;
                        var contexts = cards.Length == cardCount && cards.All(card => card.Bounds.Width > 30 && card.Bounds.Height > 20 &&
                            card.DataContext is ChartTooltipContext context && context.Readings.Count == (cardCount == 1 ? 2 : 1) && context.Hits.All(item => item.Item is TrackballBusinessRecord record && item.Point == new ChartPoint(record.X, record.Y)));
                        checks[$"Trackball{label}{direction}{display}{snap}数学身份与真实卡布局"] = !chart.IsTooltipEnabled && valid && contexts;
                        var lineChanges = -1;
                        if (snap == ChartTrackballSnapMode.LinearInterpolation)
                        {
                            chart.ShowTrackballLine = false; FlushControlTemplateWindow(window); var off = TemplateChartRaster(chart);
                            chart.ShowTrackballLine = true; FlushControlTemplateWindow(window); var on = TemplateChartRaster(chart);
                            var plot = chart.PlotArea; var anchor = chart.TrackballReadings.First().Position;
                            var stripe = direction == ChartTrackballMode.Vertical ? new Rect(Math.Floor(anchor.X) - 3, plot.Top + 3, 7, Math.Max(0, plot.Height - 6)) : new Rect(plot.Left + 3, Math.Floor(anchor.Y) - 3, Math.Max(0, plot.Width - 6), 7);
                            lineChanges = TrackballPixelChanges(off, on, stripe, cards.Select(card => TrackballCardBounds(card, chart)).ToArray());
                        }
                        modes.Add(new(direction.ToString(), display.ToString(), snap.ToString(), route.EventPositionAfterRouting, valid, cards.Length, contexts, lineChanges,
                            chart.TrackballReadings.Select(reading => new TrackballReadingEvidence(reading.Original.Series.Title, reading.Original.StableId?.ToString(), reading.Original.Point,
                                reading.Point, reading.Position, reading.IsInterpolated, reading.Segment?.Start.StableId?.ToString(), reading.Segment?.End.StableId?.ToString(), reading.Segment?.Fraction)).ToList()));
                    }
            checks[$"Trackball{label}横纵线在两种显示模式有正负像素证据"] = modes.Where(mode => mode.Snap == nameof(ChartTrackballSnapMode.LinearInterpolation)).All(mode => mode.LineChangedPixels >= 8);

            chart.TrackballMode = ChartTrackballMode.None; chart.IsTooltipEnabled = true;
            foreach (var mode in new[] { ChartTooltipMode.NearestSegment, ChartTooltipMode.InterpolateX, ChartTooltipMode.InterpolateY })
            {
                chart.TooltipMode = mode; FlushControlTemplateWindow(window);
                var route = HoverTemplateChart(chart, window, TrackballProject(chart, 3, 40));
                var segment = chart.HoveredSegments.FirstOrDefault(value => ReferenceEquals(value.Series, chart.Series[0]));
                var query = TrackballUnproject(chart, route.EventPositionAfterRouting);
                var fraction = mode == ChartTooltipMode.InterpolateY ? (query.Y - 20) / 40 : (query.X - 1) / 4;
                var start = TrackballProject(chart, 1, 20); var end = TrackballProject(chart, 5, 60); var dx = end.X - start.X; var dy = end.Y - start.Y;
                if (mode == ChartTooltipMode.NearestSegment) fraction = Math.Clamp(((route.EventPositionAfterRouting.X - start.X) * dx + (route.EventPositionAfterRouting.Y - start.Y) * dy) / (dx * dx + dy * dy), 0, 1);
                checks[$"Trackball{label}关闭追踪后{mode}保留端点及估算值"] = chart.TrackballMode == ChartTrackballMode.None &&
                    !chart.GetVisualDescendants().OfType<Control>().Any(control => control.Name == "PART_ChartTrackballOverlay") &&
                    segment is not null && ReferenceEquals(segment.Start.Item, primary[0]) && ReferenceEquals(segment.End.Item, primary[1]) &&
                    Math.Abs(segment.Fraction - fraction) < 1e-8 && NearTrackballPoint(segment.Point, new ChartPoint(1 + 4 * fraction, 20 + 40 * fraction));
            }
            chart.IsTooltipEnabled = false; chart.TrackballMode = ChartTrackballMode.Vertical; chart.TrackballDisplayMode = ChartTrackballDisplayMode.GroupAllPoints; chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
            FlushControlTemplateWindow(window); HoverTemplateChart(chart, window, TrackballProject(chart, 3, 40));
            var cardsNow = TrackballCards(chart);
            var markerOn = TemplateChartRaster(chart); chart.ShowTrackballMarkers = false; FlushControlTemplateWindow(window); var markerOff = TemplateChartRaster(chart);
            var markerChanges = chart.TrackballReadings.Select(reading => TrackballPixelChanges(markerOff, markerOn,
                new Rect(reading.Position.X - 11, reading.Position.Y - 11, 22, 22), cardsNow.Select(card => TrackballCardBounds(card, chart)).ToArray())).ToArray();
            chart.ShowTrackballMarkers = true; FlushControlTemplateWindow(window);
            checks[$"Trackball{label}每个派生位置都有真实标记墨迹"] = markerChanges.Length == 2 && markerChanges.All(count => count >= 5);
            var left = TrackballEndpointText(chart, "TrackballLeftId", "track-a0"); var right = TrackballEndpointText(chart, "TrackballRightId", "track-a1");
            var leftBounds = left is null ? default : TemplateControlBounds(left, chart); var rightBounds = right is null ? default : TemplateControlBounds(right, chart);
            var image = Path.Combine(directory, stem + $"-trackball-pixels-{(dark ? "dark" : "light")}.png");
            var onGlyph = CaptureControlTemplateRaster(chart, image);
            var withoutImage = Path.Combine(directory, stem + $"-trackball-without-endpoints-{(dark ? "dark" : "light")}.png");
            var previousLeftOpacity = left?.Opacity ?? 1; var previousRightOpacity = right?.Opacity ?? 1;
            if (left is not null) left.Opacity = 0; if (right is not null) right.Opacity = 0; FlushControlTemplateWindow(window);
            var offGlyph = CaptureControlTemplateRaster(chart, withoutImage);
            if (left is not null) left.Opacity = previousLeftOpacity; if (right is not null) right.Opacity = previousRightOpacity; FlushControlTemplateWindow(window);
            var restored = TemplateChartRaster(chart);
            var leftChanges = CountControlTemplateExactChanges(onGlyph, offGlyph, leftBounds); var rightChanges = CountControlTemplateExactChanges(onGlyph, offGlyph, rightBounds);
            var restoreSame = CountControlTemplateExactChanges(onGlyph, restored, leftBounds) == 0 && CountControlTemplateExactChanges(onGlyph, restored, rightBounds) == 0;
            checks[$"Trackball{label}两端业务编号实际绘制且恢复完全一致"] = left is { Bounds.Width: > 20 } && right is { Bounds.Width: > 20 } && leftChanges >= 5 && rightChanges >= 5 && restoreSame;
            var exportImage = Path.Combine(directory, stem + $"-trackball-export-{(dark ? "dark" : "light")}.png");
            var exports = new List<TrackballExportEvidence>(); ShapeRaster? baseline = null;
            foreach (var dpi in new[] { 96d, 144d, 192d })
            {
                var positive = ExportTrackballRaster(chart, dpi); var scale = Math.Min(960 / chart.Bounds.Width, 540 / chart.Bounds.Height);
                Rect ExportBounds(Rect bounds) => new((960 - chart.Bounds.Width * scale) / 2 + bounds.X * scale, (540 - chart.Bounds.Height * scale) / 2 + bounds.Y * scale, bounds.Width * scale, bounds.Height * scale);
                if (left is not null) left.Opacity = 0; if (right is not null) right.Opacity = 0; FlushControlTemplateWindow(window);
                var negative = ExportTrackballRaster(chart, dpi);
                if (left is not null) left.Opacity = previousLeftOpacity; if (right is not null) right.Opacity = previousRightOpacity; FlushControlTemplateWindow(window);
                var restoredExport = ExportTrackballRaster(chart, dpi);
                var leftInk = CountControlTemplateExactChanges(positive.Raster, negative.Raster, ExportBounds(leftBounds));
                var rightInk = CountControlTemplateExactChanges(positive.Raster, negative.Raster, ExportBounds(rightBounds));
                var same = baseline is null || baseline.Pixels.AsSpan().SequenceEqual(positive.Raster.Pixels);
                var restoredSame = positive.Raster.Pixels.AsSpan().SequenceEqual(restoredExport.Raster.Pixels);
                var density = TryReadCommonPngDensity(positive.Bytes, out var x, out var y, out var unit) && x == (uint)Math.Round(dpi / .0254) && y == x && unit == 1;
                var badgePixels = positive.Raster.Count(new PixelRect(0, 0, positive.Raster.Width, positive.Raster.Height), Color.Parse("#D7259F"), 4);
                checks[$"Trackball{label}{dpi:0}DPI导出端点字形非空且像素一致"] = positive.Exported && negative.Exported && restoredExport.Exported && positive.Raster.Width == 960 && positive.Raster.Height == 540 &&
                    leftInk >= 5 && rightInk >= 5 && badgePixels >= 50 && same && restoredSame && density;
                exports.Add(new(dpi, positive.Exported, positive.Raster.Width, positive.Raster.Height, ExportBounds(leftBounds), leftInk, ExportBounds(rightBounds), rightInk, badgePixels, same, restoredSame, density));
                baseline ??= positive.Raster; if (dpi == 96) File.WriteAllBytes(exportImage, positive.Bytes);
            }
            primary[1].Y = 80; FlushControlTemplateWindow(window); var updateRoute = HoverTemplateChart(chart, window, TrackballProject(chart, 3, 40));
            var updated = chart.TrackballReadings.FirstOrDefault(reading => ReferenceEquals(reading.Original.Series, chart.Series[0])); var updatedQuery = TrackballUnproject(chart, updateRoute.EventPositionAfterRouting);
            checks[$"Trackball{label}端点INPC更新估算但保持真实身份"] = updated?.Segment is { } updatedSegment && ReferenceEquals(updatedSegment.Start.Item, primary[0]) && ReferenceEquals(updatedSegment.End.Item, primary[1]) &&
                updatedSegment.End.Point.Y == 80 && Math.Abs(updated.Point.Y - (20 + 15 * (updatedQuery.X - 1))) < 1e-8;
            primary[1].Y = 60; chart.TrackballFormatter = _ => null; FlushControlTemplateWindow(window); HoverTemplateChart(chart, window, TrackballProject(chart, 3, 40));
            checks[$"Trackball{label}空formatter仍构建端点业务模板"] = TrackballCards(chart) is [{ Bounds.Width: > 30 }] && TrackballEndpointText(chart, "TrackballLeftId", "track-a0") is not null && chart.TrackballReadings.Count == 2;
            chart.TrackballFormatter = null; var previousCards = TrackballCards(chart); chart.TrackballContentTemplate = null; FlushControlTemplateWindow(window);
            var removed = TrackballCards(chart).Length == 0 && previousCards.All(card => !card.GetVisualAncestors().Contains(chart));
            chart.TrackballContentTemplate = template; FlushControlTemplateWindow(window); HoverTemplateChart(chart, window, TrackballProject(chart, 3, 40));
            checks[$"Trackball{label}模板卸载并恢复真实端点卡"] = removed && TrackballCards(chart) is [{ Bounds.Width: > 30 }] && chart.TrackballReadings.Count == 2;
            using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            chart.RaiseEvent(new PointerEventArgs(InputElement.PointerExitedEvent, chart, pointer, window, new Point(-1, -1), 1, default, KeyModifiers.None)); FlushControlTemplateWindow(window);
            checks[$"Trackball{label}离开清理读数线段和业务卡"] = chart.TrackballReadings.Count == 0 && chart.HoveredSegments.Count == 0 && chart.PrimarySegment is null && TrackballCards(chart).Length == 0;
            _trackballEvidence.Add(new(label, native, pageImage, image, withoutImage, exportImage, leftBounds, leftChanges, rightBounds, rightChanges, restoreSame, markerChanges, modes, exports));
        }
        finally { window.Close(); chart.Series.Clear(); }
    }

    private static bool TrackballMatchesMath(CartesianChart chart, ObservableCollection<TrackballBusinessRecord> primary, ObservableCollection<TrackballBusinessRecord> secondary, Point pointer)
    {
        if (chart.TrackballReadings.Count != 2) return false;
        var horizontal = chart.TrackballMode == ChartTrackballMode.Horizontal; var query = TrackballUnproject(chart, pointer);
        foreach (var reading in chart.TrackballReadings)
        {
            var source = ReferenceEquals(reading.Original.Series, chart.Series[0]) ? primary : secondary;
            if (reading.Original.Item is not TrackballBusinessRecord original || !source.Any(item => ReferenceEquals(item, original)) || reading.Original.Point != new ChartPoint(original.X, original.Y)) return false;
            if (chart.TrackballSnapMode == ChartTrackballSnapMode.NearestPoint)
            {
                var expected = source.Select((item, ordinal) => new { Item = item, Ordinal = ordinal, Screen = TrackballProject(chart, item.X, item.Y) })
                    .OrderBy(item => horizontal ? Math.Abs(item.Screen.Y - pointer.Y) : Math.Abs(item.Screen.X - pointer.X))
                    .ThenBy(item => horizontal ? Math.Abs(item.Screen.X - pointer.X) : Math.Abs(item.Screen.Y - pointer.Y)).ThenByDescending(item => item.Ordinal).First();
                if (reading.Segment is not null || reading.IsInterpolated || !ReferenceEquals(reading.Original.Item, expected.Item) || reading.Point != reading.Original.Point) return false;
                continue;
            }
            var candidates = new List<(ChartPoint Point, int Start, double Fraction)>();
            for (var index = 0; index < source.Count - 1; index++)
            {
                var start = source[index]; var end = source[index + 1]; var from = horizontal ? start.Y : start.X; var to = horizontal ? end.Y : end.X; var value = horizontal ? query.Y : query.X;
                if (from == to || value < Math.Min(from, to) || value > Math.Max(from, to)) continue;
                var fraction = (value - from) / (to - from); candidates.Add((new(start.X + (end.X - start.X) * fraction, start.Y + (end.Y - start.Y) * fraction), index, fraction));
            }
            if (candidates.Count == 0 || reading.Segment is not { } segment) return false;
            var closest = candidates.OrderBy(candidate => { var position = TrackballProject(chart, candidate.Point.X, candidate.Point.Y); return horizontal ? Math.Abs(position.X - pointer.X) : Math.Abs(position.Y - pointer.Y); }).First();
            if (!NearTrackballPoint(reading.Point, closest.Point) || !candidates.Any(candidate => ReferenceEquals(segment.Start.Item, source[candidate.Start]) && ReferenceEquals(segment.End.Item, source[candidate.Start + 1]) && Math.Abs(segment.Fraction - candidate.Fraction) < 1e-8)) return false;
        }
        return true;
    }

    private static Point TrackballProject(CartesianChart chart, double x, double y) => new(chart.PlotArea.Left + chart.PlotArea.Width * x / 10, chart.PlotArea.Bottom - chart.PlotArea.Height * y / 100);
    private static ChartPoint TrackballUnproject(CartesianChart chart, Point position) => new((position.X - chart.PlotArea.Left) * 10 / chart.PlotArea.Width, (chart.PlotArea.Bottom - position.Y) * 100 / chart.PlotArea.Height);
    private static bool NearTrackballPoint(ChartPoint left, ChartPoint right) => Math.Abs(left.X - right.X) < 1e-8 && Math.Abs(left.Y - right.Y) < 1e-8;
    private static Border[] TrackballCards(CartesianChart chart) => chart.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "TrackballBusinessCard").ToArray();
    private static TextBlock? TrackballEndpointText(CartesianChart chart, string name, string value) => chart.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(text => text.Name == name && text.Text == value);
    private static Rect TrackballCardBounds(Border card, CartesianChart chart) => card.GetVisualParent() is Control parent ? TemplateControlBounds(parent, chart) : TemplateControlBounds(card, chart);
    private static int TrackballPixelChanges(ShapeRaster before, ShapeRaster after, Rect bounds, Rect[] exclusions)
    {
        if (before.Width != after.Width || before.Height != after.Height || before.Stride != after.Stride) return -1;
        var region = TemplatePixelRect(bounds); var changed = 0;
        for (var y = Math.Max(0, region.Y); y < Math.Min(before.Height, region.Bottom); y++)
            for (var x = Math.Max(0, region.X); x < Math.Min(before.Width, region.Right); x++)
            {
                if (exclusions.Any(exclude => exclude.Contains(new Point(x + .5, y + .5)))) continue;
                var index = y * before.Stride + x * 4; if (!before.Pixels.AsSpan(index, 4).SequenceEqual(after.Pixels.AsSpan(index, 4))) changed++;
            }
        return changed;
    }
    private static (bool Exported, byte[] Bytes, ShapeRaster Raster) ExportTrackballRaster(CartesianChart chart, double dpi)
    {
        using var stream = new MemoryStream(); var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(960, 540), Dpi = dpi, Background = chart.Background });
        var bytes = stream.ToArray(); stream.Position = 0; using var bitmap = new Bitmap(stream); return (exported, bytes, ReadShapePixels(bitmap));
    }
    private sealed record TrackballReadingEvidence(string? Series, string? OriginalId, ChartPoint OriginalPoint, ChartPoint ReadingPoint, Point Position, bool IsInterpolated, string? StartId, string? EndId, double? Fraction);
    private sealed record TrackballModeEvidence(string Direction, string Display, string Snap, Point RoutedPointer, bool MatchesMath, int CardCount, bool TemplateContextsMatch, int LineChangedPixels, List<TrackballReadingEvidence> Readings);
    private sealed record TrackballExportEvidence(double Dpi, bool Exported, int Width, int Height, Rect StartIdRegion, int StartGlyphChangedPixels, Rect EndIdRegion, int EndGlyphChangedPixels, int BadgeInkPixels, bool SameRasterAs96Dpi, bool RestoreSame, bool DensityMatches);
    private sealed record TrackballSmokeEvidence(string Theme, bool NativeWindowCreated, string PageImage, string PixelImage, string WithoutEndpointsImage, string ExportImage,
        Rect StartIdRegion, int StartGlyphChangedPixels, Rect EndIdRegion, int EndGlyphChangedPixels, bool RestoredGlyphPixelsSame, int[] MarkerChangedPixels, List<TrackballModeEvidence> Modes, List<TrackballExportEvidence> Exports);
}
