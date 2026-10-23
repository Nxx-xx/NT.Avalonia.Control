using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _levelOfDetailEvidence = [];

    private async Task RunLevelOfDetailSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "dark" : "light";
            void Check(string name, bool value) => checks[$"LOD-{label}-{name}"] = value;
            string Image(string name) => Path.Combine(directory, stem + $"-lod-{label}-{name}.png");
            using var board = CreateLevelOfDetailBoard();
            var chart = board.Chart; var series = board.Series; var rows = board.GetRows();
            var window = new Window
            {
                Title = "Charts · 保真抽样与层级细节", Width = 1160, Height = 790, RequestedThemeVariant = theme,
                Background = Brush(dark ? "#111722" : "#F8FAFC"), Foreground = Brush(dark ? "#EAF0FB" : "#172238"),
                Content = new Border { Padding = new Thickness(20), Child = board.View },
            };
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window); board.Refresh();
                var native = window.TryGetPlatformHandle() is { Handle: not 0 };
                var overview = chart.GetSamplingInfo(series);
                Check("native-layout-and-explicit-opt-in", native && chart.PlotArea.Width > 700 && chart.PlotArea.Height > 200 && series.SamplingMode == ChartSamplingMode.MinMax && !series.ShowMarkers && series.MarkerSize == 0);
                Check("overview-reduces-original-vertices-and-keeps-gap-figures", overview.SourceCount == rows.Length && overview.IsApplied && overview.RenderedCount < overview.VisibleCount / 4 && overview.FigureCount == 2 && overview.RenderScale > 0);
                var overviewImage = Image("overview"); SaveWindow(overviewImage);
                var overviewChartImage = Image("overview-chart");
                var overviewRaster = CaptureControlTemplateRaster(chart, overviewChartImage);
                var overviewPlotArea = chart.PlotArea; var overviewRange = chart.VisibleRange;
                var peak = rows.Length / 3 + 1; var valley = rows.Length * 2 / 3 + 1;
                var peakPosition = Project(peak, rows[peak].Y); var valleyPosition = Project(valley, rows[valley].Y);
                var peakRegion = new Rect(peakPosition.X - 3, peakPosition.Y - 3, 6, 6); var valleyRegion = new Rect(valleyPosition.X - 3, valleyPosition.Y - 3, 6, 6);
                var peakInk = Ink(overviewRaster, peakPosition); var valleyInk = Ink(overviewRaster, valleyPosition);
                Check("overview-retains-single-sample-peak-and-valley-pixels", peakInk >= 4 && valleyInk >= 4);

                const int rawIndex = 23_457;
                var rawPosition = Project(rawIndex, rows[rawIndex].Y);
                var rawHit = chart.HitTestPoint(rawPosition, .1);
                Check("overview-hit-returns-original-non-extreme-record", rawHit is { Index: rawIndex } && ReferenceEquals(rawHit.Item, rows[rawIndex]) && Equals(rawHit.StableId, rows[rawIndex].Id) && rawHit.Point == new ChartPoint(rawIndex, rows[rawIndex].Y));
                var hoverRoute = HoverTemplateChart(chart, window, rawPosition);
                var hovered = chart.HoveredPoint; var tooltipIds = chart.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Name == "LevelOfDetailTooltipId").Select(text => text.Text).ToArray();
                // Native event positions are quantized by the platform. Hundreds
                // of source samples can occupy one pixel; use the actual routed
                // anchor and an independent full-source nearest-point oracle.
                var expectedHover = rows.Where(row => double.IsFinite(row.Y)).Select(row =>
                {
                    var position = Project(row.X, row.Y); var delta = position - hoverRoute.EventPositionAfterRouting;
                    return new { row.Index, Position = position, DistanceSquared = delta.X * delta.X + delta.Y * delta.Y };
                }).Where(row => chart.PlotArea.Contains(row.Position)).OrderBy(row => row.DistanceSquared).ThenByDescending(row => row.Index).First();
                Check("routed-tooltip-shows-original-business-id-and-index", hovered is not null && hovered.Index == expectedHover.Index && ReferenceEquals(hovered.Item, rows[expectedHover.Index]) && Equals(hovered.StableId, rows[expectedHover.Index].Id) && hovered.Point == new ChartPoint(rows[expectedHover.Index].X, rows[expectedHover.Index].Y) && tooltipIds.Contains(rows[expectedHover.Index].Id));
                var tooltipImage = Image("original-tooltip"); SaveWindow(tooltipImage);
                if (rawHit is not null) chart.SelectedPoints = [rawHit];
                Check("overview-selects-original-record", chart.SelectedPoint is { Index: rawIndex } selected && ReferenceEquals(selected.Item, rows[rawIndex]) && Equals(selected.StableId, rows[rawIndex].Id));
                var selectionRectangle = new Rect(rawPosition.X - 1.2, chart.PlotArea.Top, 2.4, chart.PlotArea.Height);
                var expected = rows.Where(row => double.IsFinite(row.Y) && selectionRectangle.Contains(Project(row.X, row.Y))).Select(row => row.Index).ToArray();
                var rectangle = chart.SelectRectangle(selectionRectangle);
                Check("overview-rectangle-includes-original-unsampled-points", expected.Length > 20 && rectangle.MatchedCount == expected.Length && rectangle.SelectedCount == expected.Length && !rectangle.IsTruncated && chart.SelectedPoints!.Select(hit => hit.Index).SequenceEqual(expected) && chart.SelectedPoints!.All(hit => ReferenceEquals(hit.Item, rows[hit.Index]) && Equals(hit.StableId, rows[hit.Index].Id)));
                chart.ClearSelection(); chart.IsTooltipEnabled = false; FlushControlTemplateWindow(window);

                board.PixelWidth.Value = 4; FlushControlTemplateWindow(window); board.Refresh(); var coarser = chart.GetSamplingInfo(series);
                Check("physical-pixel-bucket-control-updates-plan", series.SamplingPixelWidth == 4 && coarser.PixelWidth == 4 && coarser.IsApplied && coarser.PixelBucketCount < overview.PixelBucketCount && coarser.RenderedCount < overview.RenderedCount);
                board.PixelWidth.Value = 1; board.Enabled.IsChecked = false; FlushControlTemplateWindow(window); var disabled = chart.GetSamplingInfo(series);
                Check("explicit-disable-keeps-original-source", series.SamplingMode == ChartSamplingMode.None && !disabled.IsApplied && series.CreateSnapshot().Points.Count == rows.Length && ReferenceEquals(series.CreateSnapshot().Points[rawIndex].Item, rows[rawIndex]));
                var disabledImage = Image("disabled"); SaveWindow(disabledImage);
                board.Enabled.IsChecked = true; board.Detail(peak); FlushControlTemplateWindow(window); board.Refresh();
                var detail = chart.GetSamplingInfo(series);
                var peakHit = chart.HitTestPoint(Project(peak, 95), .1);
                Check("zoom-restores-original-detail-and-peak-identity", !detail.IsApplied && detail.VisibleCount < 100 && peakHit is { } && peakHit.Index == peak && ReferenceEquals(peakHit.Item, rows[peak]) && Equals(peakHit.StableId, rows[peak].Id));
                var detailImage = Image("peak-detail"); SaveWindow(detailImage);

                board.Detail(rows.Length / 2); FlushControlTemplateWindow(window); board.Refresh();
                var gapImage = Image("gap-detail"); SaveWindow(gapImage);
                var gapChartImage = Image("gap-chart"); var gapRaster = CaptureControlTemplateRaster(chart, gapChartImage);
                var gapX = Project(rows.Length / 2, 0).X;
                var gapRegion = new Rect(gapX - 5, chart.PlotArea.Top + 4, 10, chart.PlotArea.Height - 8);
                var gapInk = gapRaster.Count(TemplatePixelRect(gapRegion), Color.Parse("#4385F4"), 24);
                var gapInfo = chart.GetSamplingInfo(series);
                Check("zoom-gap-has-no-fabricated-connecting-ink", gapInk == 0 && series.CreateSnapshot().Points[rows.Length / 2].IsEmpty);

                var exports = new List<object>();
                foreach (var useSampling in new[] { false, true })
                {
                    using var stream = new MemoryStream();
                    var pixels = new PixelSize(2240, 780);
                    var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = pixels, Background = window.Background, UseSampling = useSampling });
                    var exportImage = Image($"gap-export-{(useSampling ? "sampled" : "original")}-2x"); File.WriteAllBytes(exportImage, stream.ToArray()); stream.Position = 0;
                    using var bitmap = new Bitmap(stream); var raster = ReadShapePixels(bitmap);
                    var scale = Math.Min(pixels.Width / chart.Bounds.Width, pixels.Height / chart.Bounds.Height);
                    var offset = new Vector((pixels.Width - chart.Bounds.Width * scale) / 2, (pixels.Height - chart.Bounds.Height * scale) / 2);
                    var region = new Rect(gapRegion.X * scale + offset.X, gapRegion.Y * scale + offset.Y, gapRegion.Width * scale, gapRegion.Height * scale);
                    var exportGap = raster.Count(TemplatePixelRect(region), Color.Parse("#4385F4"), 24);
                    Check($"gap-export-{(useSampling ? "sampled" : "original")}-2x-has-no-bridge", exported && bitmap.PixelSize == pixels && exportGap == 0);
                    exports.Add(new { scenario = "gap-detail", useSampling, exported, image = exportImage, bitmap.PixelSize, outputPixelsPerChartDip = scale, gapRegion = region, gapPixelRegion = TemplatePixelRect(region), gapInkPixels = exportGap });
                }
                foreach (var factor in new[] { 1, 2 })
                foreach (var useSampling in new[] { false, true })
                {
                    chart.ResetViewport(); FlushControlTemplateWindow(window);
                    using var stream = new MemoryStream();
                    var pixels = new PixelSize(1120 * factor, 390 * factor);
                    var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = pixels, Background = window.Background, UseSampling = useSampling });
                    var exportImage = Image($"export-{(useSampling ? "sampled" : "original")}-{factor}x"); File.WriteAllBytes(exportImage, stream.ToArray()); stream.Position = 0;
                    using var bitmap = new Bitmap(stream);
                    var raster = ReadShapePixels(bitmap);
                    var scale = Math.Min(pixels.Width / chart.Bounds.Width, pixels.Height / chart.Bounds.Height);
                    var offset = new Vector((pixels.Width - chart.Bounds.Width * scale) / 2, (pixels.Height - chart.Bounds.Height * scale) / 2);
                    Point ExportPoint(double x, double y) { var local = Project(x, y); return new(local.X * scale + offset.X, local.Y * scale + offset.Y); }
                    Rect ExportRegion(Point point) => new(point.X - 3 * scale, point.Y - 3 * scale, 6 * scale, 6 * scale);
                    var exportPeakRegion = ExportRegion(ExportPoint(peak, 95)); var exportValleyRegion = ExportRegion(ExportPoint(valley, -95));
                    var exportPeak = raster.Count(TemplatePixelRect(exportPeakRegion), Color.Parse("#4385F4"), 24); var exportValley = raster.Count(TemplatePixelRect(exportValleyRegion), Color.Parse("#4385F4"), 24);
                    Check($"export-{(useSampling ? "sampled" : "original")}-{factor}x-retains-extrema-pixels", exported && bitmap.PixelSize == pixels && exportPeak >= 4 && exportValley >= 4);
                    exports.Add(new { scenario = "overview", useSampling, exported, image = exportImage, bitmap.PixelSize, outputPixelsPerChartDip = scale, peakRegion = exportPeakRegion, valleyRegion = exportValleyRegion, peakPixelRegion = TemplatePixelRect(exportPeakRegion), valleyPixelRegion = TemplatePixelRect(exportValleyRegion), peakInkPixels = exportPeak, valleyInkPixels = exportValley });
                }
                // A half-size export has subpixel stroke coverage at an isolated
                // extremum. Use its original blue color after unpremultiplying
                // partially covered pixels; retain the opaque 1x/2x gates above.
                var originalBackground = chart.Background; chart.Background = Brushes.Transparent;
                try
                {
                    foreach (var scenario in new[] { "overview", "gap-detail" })
                    foreach (var useSampling in new[] { false, true })
                    {
                        if (scenario == "overview") chart.ResetViewport(); else board.Detail(rows.Length / 2);
                        FlushControlTemplateWindow(window);
                        using var stream = new MemoryStream(); var pixels = new PixelSize(560, 195);
                        var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = pixels, UseSampling = useSampling });
                        var exportImage = Image($"{scenario}-export-{(useSampling ? "sampled" : "original")}-0.5x-transparent"); File.WriteAllBytes(exportImage, stream.ToArray()); stream.Position = 0;
                        using var bitmap = new Bitmap(stream); var raster = ReadShapePixels(bitmap);
                        var scale = Math.Min(pixels.Width / chart.Bounds.Width, pixels.Height / chart.Bounds.Height);
                        var offset = new Vector((pixels.Width - chart.Bounds.Width * scale) / 2, (pixels.Height - chart.Bounds.Height * scale) / 2);
                        Point ExportPoint(double x, double y) { var local = Project(x, y); return new(local.X * scale + offset.X, local.Y * scale + offset.Y); }
                        Rect ExportRegion(Point point) => new(point.X - 3 * scale, point.Y - 3 * scale, 6 * scale, 6 * scale);
                        var exportPeakRegion = ExportRegion(ExportPoint(peak, 95)); var exportValleyRegion = ExportRegion(ExportPoint(valley, -95));
                        var localGapX = Project(rows.Length / 2, 0).X;
                        var exportGapRegion = new Rect((localGapX - 5) * scale + offset.X, (chart.PlotArea.Top + 4) * scale + offset.Y, 10 * scale, (chart.PlotArea.Height - 8) * scale);
                        var exportPeak = scenario == "overview" ? AntialiasedInk(raster, TemplatePixelRect(exportPeakRegion)) : 0;
                        var exportValley = scenario == "overview" ? AntialiasedInk(raster, TemplatePixelRect(exportValleyRegion)) : 0;
                        var exportGap = scenario == "gap-detail" ? AntialiasedInk(raster, TemplatePixelRect(exportGapRegion)) : 0;
                        var cornerAlpha = raster.Pixels[3];
                        Check($"{scenario}-export-{(useSampling ? "sampled" : "original")}-halfscale-transparent-quality", exported && bitmap.PixelSize == pixels && cornerAlpha == 0 &&
                            (scenario == "overview" ? exportPeak >= 1 && exportValley >= 1 : exportGap == 0));
                        exports.Add(new
                        {
                            scenario, useSampling, exported, image = exportImage, bitmap.PixelSize, outputPixelsPerChartDip = scale, transparentCanvas = true, cornerAlpha,
                            peakRegion = scenario == "overview" ? exportPeakRegion : (Rect?)null, valleyRegion = scenario == "overview" ? exportValleyRegion : (Rect?)null,
                            peakPixelRegion = scenario == "overview" ? TemplatePixelRect(exportPeakRegion) : (PixelRect?)null, valleyPixelRegion = scenario == "overview" ? TemplatePixelRect(exportValleyRegion) : (PixelRect?)null,
                            gapRegion = scenario == "gap-detail" ? exportGapRegion : (Rect?)null, gapPixelRegion = scenario == "gap-detail" ? TemplatePixelRect(exportGapRegion) : (PixelRect?)null,
                            peakInkPixels = exportPeak, valleyInkPixels = exportValley, gapInkPixels = exportGap,
                            pixelPredicate = new { straightPngRgb = true, premultipliedReadback = true, unpremultiplyBeforeColorTest = true, color = "#4385F4", colorChannelTolerance = 24, alphaMinimum = 16, minimumExtremumInk = 1, maximumGapInk = 0 },
                        });
                    }
                }
                finally { chart.Background = originalBackground; chart.ResetViewport(); FlushControlTemplateWindow(window); }
                var source = series.CreateSnapshot(); var afterExport = chart.GetSamplingInfo(series);
                Check("exports-do-not-replace-original-data-or-screen-plan", source.Points.Count == rows.Length && ReferenceEquals(source.Points[rawIndex].Item, rows[rawIndex]) && afterExport.RenderedCount == overview.RenderedCount && afterExport.PixelBucketCount == overview.PixelBucketCount);
                _levelOfDetailEvidence.Add(new
                {
                    theme = label, nativeWindowCreated = native, overviewImage, overviewChartImage, disabledImage, detailImage, gapImage, gapChartImage, tooltipImage, hoverRoute, tooltipIds, expectedHover,
                    overview, coarser, disabled, detail, gapInfo, afterExport, peakInk, valleyInk, gapInk, gapRegion, gapPixelRegion = TemplatePixelRect(gapRegion), peakRegion, valleyRegion,
                    peakPixelRegion = TemplatePixelRect(peakRegion), valleyPixelRegion = TemplatePixelRect(valleyRegion), overviewPlotArea, overviewRange,
                    pixelPredicate = new { straightPngRgb = true, color = "#4385F4", colorChannelTolerance = 24, alphaMinimum = 250, minimumExtremumInk = 4, maximumGapInk = 0 },
                    rawHit = rawHit is null ? null : new { rawHit.Index, rawHit.StableId, rawHit.Point },
                    hovered = hovered is null ? null : new { hovered.Index, hovered.StableId, hovered.Point },
                    rectangle = new { rectangle.MatchedCount, rectangle.SelectedCount, rectangle.IsTruncated, expectedIndices = expected }, exports,
                    originalSourceCount = source.Points.Count, physicalInputValidated = false, performanceMeasured = false,
                    evidenceKind = "native-window-rendertargetbitmap-single-sample-extrema-gap-and-original-business-identity",
                });

                Point Project(double x, double y)
                {
                    var range = chart.VisibleRange!.Value; var plot = chart.PlotArea;
                    return new(plot.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
                        plot.Bottom - (y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
                }
            }
            finally { window.Close(); }

            void SaveWindow(string path)
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
                bitmap.Render(window); bitmap.Save(path);
            }
        }
        static int Ink(ShapeRaster raster, Point point) => raster.Count(TemplatePixelRect(new Rect(point.X - 3, point.Y - 3, 6, 6)), Color.Parse("#4385F4"), 24);
        static int AntialiasedInk(ShapeRaster raster, PixelRect rectangle)
        {
            var color = Color.Parse("#4385F4"); var count = 0;
            for (var y = Math.Max(0, rectangle.Y); y < Math.Min(raster.Height, rectangle.Bottom); y++)
                for (var x = Math.Max(0, rectangle.X); x < Math.Min(raster.Width, rectangle.Right); x++)
                {
                    var index = y * raster.Stride + x * 4; var alpha = raster.Pixels[index + 3]; if (alpha < 16) continue;
                    if (Math.Abs(raster.Pixels[index] * 255d / alpha - color.B) <= 24 && Math.Abs(raster.Pixels[index + 1] * 255d / alpha - color.G) <= 24 && Math.Abs(raster.Pixels[index + 2] * 255d / alpha - color.R) <= 24) count++;
                }
            return count;
        }
    }
}


