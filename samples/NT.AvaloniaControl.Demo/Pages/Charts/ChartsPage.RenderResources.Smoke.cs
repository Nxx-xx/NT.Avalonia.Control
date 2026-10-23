using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _renderResourceEvidence = [];
    private async Task RunRenderResourceSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var label = theme == ThemeVariant.Dark ? "dark" : "light";
            void Check(string name, bool value) => checks[$"RESOURCE-{label}-{name}"] = value;
            string Image(string name) => Path.Combine(Path.GetDirectoryName(baseImage)!, Path.GetFileNameWithoutExtension(baseImage) + $"-resource-{label}-{name}.png");
            using var board = CreateRenderResourceBoard(); var chart = board.Chart; chart.IsTooltipEnabled = false;
            var window = new Window { Title = "Charts · 绘图与资源预算", Width = 1160, Height = 860, RequestedThemeVariant = theme,
                Background = Brush(label == "dark" ? "#111722" : "#F8FAFC"), Foreground = Brush(label == "dark" ? "#EAF0FB" : "#172238"),
                Content = new Border { Padding = new Thickness(20), Child = board.View } };
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                var expected = board.Buffers.Select(source => source.ToArray()).ToArray();
                var oldSnapshots = chart.Series.Select(series => series.CreateSnapshot()).ToArray();
                Check("native-three-series-layout", window.TryGetPlatformHandle() is { Handle: not 0 } && chart.PlotArea.Width > 700 && chart.Series.Count == 3);
                Check("independent-business-formula", expected.SelectMany((rows, s) => rows.Select((row, q) => row.Id == $"resource-{s}-sample-{q}" && row.X == q && row.Y == 10 + s * 28 + q % 32 * .8)).All(value => value));
                var pagesBefore = board.Buffers.Select(source => source.StoragePageCount).ToArray();
                Check("intentional-single-row-page-fragmentation", pagesBefore.SequenceEqual(new[] { 256, 256, 256 }));
                var selected = chart.HitTestPoint(Project(170, expected[0][170].Y), .001); chart.SelectedPoint = selected; FlushControlTemplateWindow(window);
                Check("initial-selection-original-identity", Original(selected, 0, 170));
                var cachedImage = Image("cached"); var cachedRaster = CaptureControlTemplateRaster(chart, cachedImage); var warm = chart.GetResourceCacheInfo();
                Check("warm-budget-model-and-scope", warm.BudgetBytes == 128 * 1024 && warm.EstimatedBytes > 0 && warm.EstimatedBytes <= warm.BudgetBytes && warm.ModelVersion == 1 && !warm.NativeMemoryIncluded && !warm.SharedIndexCachesIncluded);
                Check("warm-five-layer-byte-total", warm.EstimatedBytes == warm.SeriesDrawingBytes + warm.AxisTextBytes + warm.InteractionTextBytes + warm.AxisPenBytes + warm.SegmentGeometryBytes);
                Warm(); var repeated = chart.GetResourceCacheInfo();
                Check("unchanged-redraw-reuses-budgeted-resources", repeated.CacheHits > warm.CacheHits && repeated.EstimatedBytes <= repeated.BudgetBytes);
                chart.TrimResourceCaches(); var trimmed = chart.GetResourceCacheInfo();
                Check("trim-immediately-releases-accounted-bytes", trimmed.EstimatedBytes == 0 && trimmed.SeriesDrawingCount + trimmed.AxisTextCount + trimmed.InteractionTextCount + trimmed.AxisPenCount + trimmed.SegmentGeometryCount == 0 && trimmed.TrimCount > repeated.TrimCount);
                var rebuiltImage = Image("trim-rebuilt"); var rebuiltRaster = CaptureControlTemplateRaster(chart, rebuiltImage); var rebuilt = chart.GetResourceCacheInfo();
                Check("trim-rebuild-respects-budget", rebuilt.EstimatedBytes > 0 && rebuilt.EstimatedBytes <= rebuilt.BudgetBytes);
                var rebuiltChanged = Changes(cachedRaster, rebuiltRaster); Check("trim-rebuild-raster-exact", rebuiltChanged == 0);
                chart.ResourceCacheBudgetBytes = 4096; var lowered = chart.GetResourceCacheInfo();
                Check("lowered-budget-immediately-bounded", lowered.BudgetBytes == 4096 && lowered.EstimatedBytes <= 4096);
                var smallImage = Image("small-budget"); var smallRaster = CaptureControlTemplateRaster(chart, smallImage); var small = chart.GetResourceCacheInfo();
                Check("small-budget-after-draw-is-bounded", small.EstimatedBytes <= 4096 && small.EstimatedBytes == small.SeriesDrawingBytes + small.AxisTextBytes + small.InteractionTextBytes + small.AxisPenBytes + small.SegmentGeometryBytes);
                var smallChanged = Changes(cachedRaster, smallRaster); Check("small-budget-raster-exact", smallChanged == 0);
                chart.ResourceCacheBudgetBytes = 0; var disabledBefore = chart.GetResourceCacheInfo();
                Check("zero-budget-clears-local-resources-immediately", disabledBefore.BudgetBytes == 0 && disabledBefore.EstimatedBytes == 0);
                var disabledImage = Image("disabled"); var disabledRaster = CaptureControlTemplateRaster(chart, disabledImage); var disabled = chart.GetResourceCacheInfo();
                Check("zero-budget-draw-retains-no-accounted-resource", disabled.EstimatedBytes == 0 && disabled.SeriesDrawingCount + disabled.AxisTextCount + disabled.InteractionTextCount + disabled.AxisPenCount + disabled.SegmentGeometryCount == 0 && disabled.BypassCount > disabledBefore.BypassCount);
                var disabledChanged = Changes(cachedRaster, disabledRaster); Check("disabled-raster-exact", disabledChanged == 0);
                Check("budgets-and-trim-keep-original-source-and-selection", CurrentRows() && Original(chart.SelectedPoint, 0, 170));
                chart.ResourceCacheBudgetBytes = 128 * 1024;
                board.MutableBrush.Color = Color.Parse("#9A60D0");
                var mutatedImage = Image("mutable-brush"); var mutatedRaster = CaptureControlTemplateRaster(chart, mutatedImage); var mutated = chart.GetResourceCacheInfo();
                var regions = new List<object>();
                foreach (var (state, raster, colors) in new[]
                {
                    ("cached", cachedRaster, new[] { "#4385F4", "#19A798", "#F2A33A" }),
                    ("mutable", mutatedRaster, new[] { "#9A60D0", "#19A798", "#F2A33A" }),
                })
                    for (var s = 0; s < 3; s++)
                    {
                        var center = Project(190, 10 + s * 28 + 190 % 32 * .8); var region = new Rect(center.X - 7, center.Y - 7, 14, 14);
                        var ink = raster.Count(TemplatePixelRect(region), Color.Parse(colors[s]), 24);
                        Check($"{state}-series-{s}-business-color-visible", ink >= 10);
                        regions.Add(new { state, series = s, sequence = 190, color = colors[s], region, pixelRegion = TemplatePixelRect(region), ink });
                    }
                var mutableCenter = Project(190, 10 + 190 % 32 * .8); var mutableRegion = new Rect(mutableCenter.X - 7, mutableCenter.Y - 7, 14, 14);
                var staleBlueInk = mutatedRaster.Count(TemplatePixelRect(mutableRegion), Color.Parse("#4385F4"), 24);
                Check("mutable-brush-does-not-replay-stale-blue", staleBlueInk == 0 && ReferenceEquals(chart.Series[0].Stroke, board.MutableBrush));
                var versionsBefore = board.Buffers.Select(source => source.Version).ToArray(); var results = new bool[3];
                using (chart.DeferDataUpdates()) for (var s = 0; s < 3; s++) results[s] = board.Buffers[s].Compact();
                FlushControlTemplateWindow(window);
                var pagesAfter = board.Buffers.Select(source => source.StoragePageCount).ToArray();
                Check("explicit-compact-packs-all-three-buffers", results.All(value => value) && pagesAfter.SequenceEqual(new[] { 1, 1, 1 }));
                Check("compact-publishes-one-buffer-version", board.Buffers.Select((source, s) => source.Version == versionsBefore[s] + 1).All(value => value));
                Check("compact-current-original-order-id-and-item", CurrentRows());
                Check("compact-keeps-old-snapshot-original-records", oldSnapshots.Select((snapshot, s) => snapshot.Points.Count == 256 && snapshot.Points.Select((point, q) => point.SourceIndex == q && ReferenceEquals(point.Item, expected[s][q]) && Equals(point.StableId, $"resource-{s}-sample-{q}") && point.Point == new ChartPoint(q, 10 + s * 28 + q % 32 * .8)).All(value => value)).All(value => value));
                var hit = chart.HitTestPoint(Project(170, expected[0][170].Y), .001);
                Check("compact-selection-and-hit-keep-original-identity", Original(chart.SelectedPoint, 0, 170) && Original(hit, 0, 170));
                var compactedImage = Image("compacted"); var compactedRaster = CaptureControlTemplateRaster(chart, compactedImage);
                var compactedChanged = Changes(mutatedRaster, compactedRaster); Check("compact-raster-exact", compactedChanged == 0);
                var packedVersions = board.Buffers.Select(source => source.Version).ToArray();
                Check("already-packed-compact-is-no-op", board.Buffers.All(source => !source.Compact()) && board.Buffers.Select((source, s) => source.Version == packedVersions[s]).All(value => value));
                board.Refresh(); var overviewImage = Image("overview"); SaveWindow(overviewImage);
                chart.TrimResourceCaches(); var final = chart.GetResourceCacheInfo();
                Check("final-trim-releases-all-accounted-layers", final.EstimatedBytes == 0 && final.SeriesDrawingCount + final.AxisTextCount + final.InteractionTextCount + final.AxisPenCount + final.SegmentGeometryCount == 0);
                _renderResourceEvidence.Add(new
                {
                    theme = label, cachedImage, rebuiltImage, smallImage, disabledImage, mutatedImage, compactedImage, overviewImage,
                    plot = chart.PlotArea, range = chart.VisibleRange, captureSequence = 190, selectedSequence = 170,
                    warm, repeated, trimmed, rebuilt, lowered, small, disabledBefore, disabled, mutated, final,
                    pagesBefore, pagesAfter, compactResults = results, versionsBefore, versionsAfter = board.Buffers.Select(source => source.Version).ToArray(),
                    comparisons = new { rebuiltChanged, smallChanged, disabledChanged, compactedChanged }, regions, staleBlueInk,
                    currentOriginalRows = chart.Series.Select((series, s) => series.CreateSnapshot().Points.Select(point => new { series = s, index = point.SourceIndex, id = point.StableId, x = point.Point.X, y = point.Point.Y, itemId = (point.Item as RenderResourceRow)?.Id }).ToArray()).ToArray(),
                    initialSnapshotRows = oldSnapshots.Select((snapshot, s) => snapshot.Points.Select(point => new { series = s, index = point.SourceIndex, id = point.StableId, x = point.Point.X, y = point.Point.Y, itemId = (point.Item as RenderResourceRow)?.Id }).ToArray()).ToArray(),
                    hit = hit is null ? null : new { hit.Index, hit.StableId, hit.Point },
                    colorChannelTolerance = 24, alphaMinimum = 250, minimumInkPerSeries = 10,
                    physicalInputValidated = false, performanceMeasured = false, totalMemoryQualified = false,
                });
                bool CurrentRows() => chart.Series.Select((series, s) => board.Buffers[s].Count == 256 && series.CreateSnapshot().Points.Select((point, q) => point.SourceIndex == q && ReferenceEquals(point.Item, expected[s][q]) && Equals(point.StableId, expected[s][q].Id) && point.Point == new ChartPoint(expected[s][q].X, expected[s][q].Y)).All(value => value)).All(value => value);
                bool Original(ChartPointHit? candidate, int s, int q) => candidate is not null && ReferenceEquals(candidate.Series, chart.Series[s]) && candidate.Index == q && ReferenceEquals(candidate.Item, expected[s][q]) && Equals(candidate.StableId, expected[s][q].Id) && candidate.Point == new ChartPoint(expected[s][q].X, expected[s][q].Y);
                Point Project(double x, double y) { var range = chart.VisibleRange!.Value; var plot = chart.PlotArea; return new(plot.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width, plot.Bottom - (y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height); }
                void Warm() { var drawing = new DrawingGroup(); using var context = drawing.Open(); chart.Render(context); }
                int Changes(ShapeRaster a, ShapeRaster b) => CountControlTemplateExactChanges(a, b, new Rect(0, 0, chart.Bounds.Width, chart.Bounds.Height));
            }
            finally { window.Close(); }
            void SaveWindow(string path) { using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)); bitmap.Render(window); bitmap.Save(path); }
        }
    }
}