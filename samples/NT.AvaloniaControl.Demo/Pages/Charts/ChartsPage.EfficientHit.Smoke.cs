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
    private readonly List<object> _efficientHitEvidence = [];
    private async Task RunEfficientHitSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var label = theme == ThemeVariant.Dark ? "dark" : "light";
            void Check(string name, bool value) => checks[$"HIT-{label}-{name}"] = value;
            string Image(string name) => Path.Combine(Path.GetDirectoryName(baseImage)!, Path.GetFileNameWithoutExtension(baseImage) + $"-hit-{label}-{name}.png");
            using var board = CreateEfficientHitBoard();
            var window = new Window { Title = "Charts · 高效命中与原始数据", Width = 1160, Height = 860, RequestedThemeVariant = theme,
                Background = Brush(label == "dark" ? "#111722" : "#F8FAFC"), Foreground = Brush(label == "dark" ? "#EAF0FB" : "#172238"),
                Content = new Border { Padding = new Thickness(20), Child = board.View } };
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window); board.Refresh();
                var points = board.Points; var dense = board.Dense; var bars = board.Bars; var rows = board.GetRows();
                Check("native-layout", window.TryGetPlatformHandle() is { Handle: not 0 } && points.PlotArea.Width > 250 && dense.PlotArea.Width > 250 && bars.PlotArea.Width > 700);
                var overlapping = points.HitTestPoint(Project(points, 2, 2), .001);
                Check("overlap-keeps-later-series-and-later-original-record", Identity(overlapping, board.Last, 1, board.LastRows[1]));
                var sameX = points.HitTestPoint(Project(points, 5, 6.9), 10);
                Check("same-x-uses-screen-distance-and-original-item", Identity(sameX, board.Last, 3, board.LastRows[3]));
                var right = points.HitTestPoint(Project(points, 8, 800, 0, 1000), .001);
                Check("independent-right-axis-keeps-unscaled-original-value", Identity(right, board.AxisSeries, 0, board.AxisRows[0]) && right!.Point.Y == 800);
                var overviewImage = Image("overview"); Save(overviewImage);
                var pointImage = Image("point-chart"); var raster = CaptureControlTemplateRaster(points, pointImage);
                var overlapRegion = Around(Project(points, 2, 2)); var rightRegion = Around(Project(points, 8, 800, 0, 1000));
                var overlapInk = raster.Count(TemplatePixelRect(overlapRegion), Color.Parse("#19A798"), 24);
                var rightInk = raster.Count(TemplatePixelRect(rightRegion), Color.Parse("#F2A33A"), 24);
                Check("point-and-multi-axis-original-markers-visible", overlapInk >= 20 && rightInk >= 20);
                var pointRoute = HoverTemplateChart(points, window, Project(points, 2, 2));
                var pointExpected = Nearest(points, pointRoute.EventPositionAfterRouting, [ (board.First, board.FirstRows, 0, 10), (board.Last, board.LastRows, 0, 10), (board.AxisSeries, board.AxisRows, 0, 1000) ]);
                var pointIds = points.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "EfficientHitTooltipId").Select(t => t.Text).ToArray();
                Check("routed-overlap-tooltip-matches-independent-original-scan", Identity(points.HoveredPoint, pointExpected.Series, pointExpected.Index, pointExpected.Row) && pointIds.Contains(pointExpected.Row.Id));
                points.SelectedPoint = overlapping;
                Check("overlap-selection-keeps-original-item", Identity(points.SelectedPoint, board.Last, 1, board.LastRows[1]));
                var tooltipImage = Image("point-tooltip-selection"); Save(tooltipImage);
                var info = dense.GetSamplingInfo(board.Line); var overviewDensePlot = dense.PlotArea; var overviewDenseRange = dense.VisibleRange;
                var rawIndex = 12345; var guard = 5 * board.Line.StrokeThickness + Math.Max(1, 1 / info.RenderScale);
                int Bucket(int i) => (int)Math.Floor((Project(dense, rows[i].X, rows[i].Y).X - dense.PlotArea.Left + guard) * info.RenderScale / info.PixelWidth);
                while (Bucket(rawIndex - 1) != Bucket(rawIndex) || Bucket(rawIndex + 1) != Bucket(rawIndex)) rawIndex++;
                // All Y values strictly increase. A sample with both physical
                // neighbors in its bucket is neither first/last nor min/max.
                var omittedProof = Bucket(rawIndex - 1) == Bucket(rawIndex) && Bucket(rawIndex + 1) == Bucket(rawIndex) && rows[rawIndex - 1].Y < rows[rawIndex].Y && rows[rawIndex + 1].Y > rows[rawIndex].Y;
                var originalBucket = Bucket(rawIndex);
                var raw = dense.HitTestPoint(Project(dense, rows[rawIndex].X, rows[rawIndex].Y), .001);
                Check("minmax-omits-proven-interior-original-point", info.IsApplied && info.RenderedCount < rows.Length / 10 && omittedProof);
                Check("omitted-point-hit-keeps-original-index-id-item", Identity(raw, board.Line, rawIndex, rows[rawIndex]));
                dense.SelectedPoint = raw;
                Check("omitted-point-selection-keeps-original-item", Identity(dense.SelectedPoint, board.Line, rawIndex, rows[rawIndex]));
                var denseRoute = HoverTemplateChart(dense, window, Project(dense, rows[rawIndex].X, rows[rawIndex].Y));
                var denseExpected = Nearest(dense, denseRoute.EventPositionAfterRouting, [ (board.Line, rows, 0, 100) ]);
                var denseHovered = dense.HoveredPoint;
                var denseIds = dense.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "EfficientHitTooltipId").Select(t => t.Text).ToArray();
                Check("sampled-tooltip-matches-full-original-source-scan", Identity(dense.HoveredPoint, board.Line, denseExpected.Index, denseExpected.Row) && denseIds.Contains(denseExpected.Row.Id));
                var sampledImage = Image("sampled-original-tooltip"); Save(sampledImage);
                var rawPosition = Project(dense, rows[rawIndex].X, rows[rawIndex].Y); var rect = new Rect(rawPosition.X - 1, dense.PlotArea.Top, 2, dense.PlotArea.Height);
                var expectedRectangle = rows.Select((row, index) => (row, index)).Where(item => rect.Contains(Project(dense, item.row.X, item.row.Y))).Select(item => item.index).ToArray();
                var rectangle = dense.SelectRectangle(rect);
                var actualRectangleIndices = dense.SelectedPoints!.Select(hit => hit.Index).ToArray();
                Check("rectangle-includes-unsampled-original-records", expectedRectangle.Length > 10 && rectangle.MatchedCount == expectedRectangle.Length && !rectangle.IsTruncated && dense.SelectedPoints!.Select(hit => hit.Index).SequenceEqual(expectedRectangle) && dense.SelectedPoints!.All(hit => Identity(hit, board.Line, hit.Index, rows[hit.Index])));
                dense.ClearSelection(); dense.Viewport = new ChartViewport(rawIndex - 20, rawIndex + 20, 60, 63); FlushControlTemplateWindow(window);
                var a = Project(dense, rows[rawIndex].X, rows[rawIndex].Y); var b = Project(dense, rows[rawIndex + 1].X, rows[rawIndex + 1].Y); var segmentAnchor = a + (b - a) * .37;
                var segment = dense.HitTestSegment(segmentAnchor, .001);
                Check("segment-keeps-adjacent-original-endpoints", segment is not null && Identity(segment.Start, board.Line, rawIndex, rows[rawIndex]) && Identity(segment.End, board.Line, rawIndex + 1, rows[rawIndex + 1]) && Math.Abs(segment.Fraction - .37) < 1e-6);
                Check("view-change-restores-full-detail-and-original-hit", !dense.GetSamplingInfo(board.Line).IsApplied && Identity(dense.HitTestPoint(a, .001), board.Line, rawIndex, rows[rawIndex]));
                var detailImage = Image("segment-detail"); Save(detailImage);
                var replacement = EfficientHitData(rows.Length, "replacement-"); board.Line.ItemsSource = replacement; FlushControlTemplateWindow(window);
                var replaced = dense.HitTestPoint(a, .001);
                Check("same-coordinate-source-replacement-invalidates-item-identity", Identity(replaced, board.Line, rawIndex, replacement[rawIndex]) && !ReferenceEquals(replaced!.Item, rows[rawIndex]));
                var groupWidth = Math.Max(1, Math.Min(120, bars.PlotArea.Width / 8 * .72)); var barProbes = new List<object>();
                foreach (var (series, source, group) in new[] { (board.Bar, board.BarRows, 0), (board.OtherBar, board.OtherBarRows, 1) })
                {
                    const int index = 3; var center = Project(bars, source[index].X, source[index].Y / 2); center = new Point(center.X + (group == 0 ? -1 : 1) * groupWidth / 4, center.Y);
                    var barHit = bars.HitTestPoint(center, 0);
                    Check($"shared-bar-layout-group-{group}-keeps-original-item", Identity(barHit, series, index, source[index]));
                    barProbes.Add(new { group, center, expectedIndex = index, expectedId = source[index].Id, actualIndex = barHit?.Index, actualId = barHit?.StableId });
                }
                _efficientHitEvidence.Add(new
                {
                    theme = label, overviewImage, pointImage, tooltipImage, sampledImage, detailImage, pointsPlotArea = points.PlotArea, pointsVisibleRange = points.VisibleRange, overviewDensePlot, overviewDenseRange,
                    overlap = HitEvidence(overlapping), sameX = HitEvidence(sameX), rightAxis = HitEvidence(right), raw = HitEvidence(raw), denseHovered = HitEvidence(denseHovered), replacement = HitEvidence(replaced), pointRoute, pointExpectedIndex = pointExpected.Index, pointExpectedId = pointExpected.Row.Id, pointIds,
                    denseRoute, denseExpectedIndex = denseExpected.Index, denseExpectedId = denseExpected.Row.Id, denseIds, info, rawIndex, originalId = rows[rawIndex].Id, omittedProof,
                    originalBucket, rectangleIndices = actualRectangleIndices, expectedRectangleIndices = expectedRectangle, barProbes,
                    segment = segment is null ? null : new { startIndex = segment.Start.Index, endIndex = segment.End.Index, startId = segment.Start.StableId, endId = segment.End.StableId, segment.Fraction },
                    overlapRegion, overlapPixelRegion = TemplatePixelRect(overlapRegion), overlapInk, overlapColor = "#19A798", rightRegion, rightPixelRegion = TemplatePixelRect(rightRegion), rightInk, rightColor = "#F2A33A",
                    colorChannelTolerance = 24, alphaMinimum = 250, minimumMarkerInk = 20, physicalInputValidated = false, performanceMeasured = false,
                    oracle = "full original business arrays with independent linear axis projection, screen distance, later physical/series tie rule; no tested hit used as its own expected record",
                });
            }
            finally { window.Close(); }
            void Save(string path) { using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)); bitmap.Render(window); bitmap.Save(path); }
        }
        static object? HitEvidence(ChartPointHit? hit) => hit is null ? null : new { hit.Index, hit.StableId, hit.Point, hit.RawPoint };
        static Rect Around(Point point) => new(point.X - 7, point.Y - 7, 14, 14);
        static bool Identity(ChartPointHit? hit, ChartSeries series, int index, EfficientHitRow row) => hit is not null && ReferenceEquals(hit.Series, series) && hit.Index == index && ReferenceEquals(hit.Item, row) && Equals(hit.StableId, row.Id) && hit.Point == new ChartPoint(row.X, row.Y) && hit.RawPoint is null;
        static Point Project(CartesianChart chart, double x, double y, double? ymin = null, double? ymax = null)
        {
            var range = chart.VisibleRange!.Value; var plot = chart.PlotArea;
            return new(plot.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width, plot.Bottom - (y - (ymin ?? range.YMinimum)) / ((ymax ?? range.YMaximum) - (ymin ?? range.YMinimum)) * plot.Height);
        }
        static (ChartSeries Series, int Index, EfficientHitRow Row) Nearest(CartesianChart chart, Point anchor, (ChartSeries Series, EfficientHitRow[] Rows, double Minimum, double Maximum)[] sources)
        {
            var best = double.PositiveInfinity; (ChartSeries Series, int Index, EfficientHitRow Row) answer = default;
            foreach (var (series, source, minimum, maximum) in sources)
                for (var i = 0; i < source.Length; i++)
                {
                    var position = Project(chart, source[i].X, source[i].Y, minimum, maximum); if (!chart.PlotArea.Contains(position)) continue;
                    var delta = position - anchor; var distance = delta.X * delta.X + delta.Y * delta.Y;
                    if (distance <= best) { best = distance; answer = (series, i, source[i]); }
                }
            return answer;
        }
    }
}
