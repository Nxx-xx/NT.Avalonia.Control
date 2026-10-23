using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _incrementalStreamingEvidence = [];
    private async Task RunIncrementalStreamingSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var label = theme == ThemeVariant.Dark ? "dark" : "light";
            void Check(string name, bool value) => checks[$"STREAM-{label}-{name}"] = value;
            string Image(string name) => Path.Combine(Path.GetDirectoryName(baseImage)!, Path.GetFileNameWithoutExtension(baseImage) + $"-stream-{label}-{name}.png");
            using var board = CreateIncrementalStreamingBoard(); var chart = board.Chart;
            var window = new Window { Title = "Charts · 增量与实时调度", Width = 1160, Height = 860, RequestedThemeVariant = theme,
                Background = Brush(label == "dark" ? "#111722" : "#F8FAFC"), Foreground = Brush(label == "dark" ? "#EAF0FB" : "#172238"),
                Content = new Border { Padding = new Thickness(20), Child = board.View } };
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                var expected = board.Buffers.Select(buffer => buffer.ToList()).ToArray();
                var initialRows = expected.Select(rows => rows.ToArray()).ToArray();
                var originalSnapshots = chart.Series.Select(series => series.CreateSnapshot()).ToArray();
                Check("native-four-series-layout", window.TryGetPlatformHandle() is { Handle: not 0 } && chart.PlotArea.Width > 700 && chart.Series.Count == 4);
                Check("initial-independent-business-formula", expected.SelectMany((rows, s) => rows.Select((row, i) => row.Id == $"stream-{s}-sample-{i}" && row.X == i && row.Y == 10 + s * 22 + i % 16 * .55)).All(ok => ok));
                Check("initial-follow-window", chart.FollowLatestData && chart.VisibleRange is { } initial && Math.Abs(initial.XMaximum - 255) < 1e-8 && Math.Abs(initial.XMaximum - initial.XMinimum - 128) < 1e-8);
                var selected = chart.HitTestPoint(Project(chart, 200, expected[0][200].Y), .001); chart.SelectedPoint = selected;
                Check("initial-selection-original-record", Original(selected, chart.Series[0], 200, expected[0][200]));
                var oldSelected = expected[0][200];
                board.Session.Pause(); var before = board.Buffers.Select(buffer => buffer.Count).ToArray();
                var first = IncrementalStreamingBoard.Batch(256); var second = IncrementalStreamingBoard.Batch(320); var rejected = IncrementalStreamingBoard.Batch(384);
                Check("paused-queue-accepts-first-two-batches", board.Session.TryAppend(first) && board.Session.TryAppend(second));
                Check("paused-queue-explicitly-rejects-over-capacity", !board.Session.TryAppend(rejected));
                var paused = board.Session.Statistics;
                Check("paused-queue-has-finite-bound-and-no-commits", board.Session.IsPaused && paused.PendingBatches == 2 && paused.PendingSamples == 512 && paused.CommittedSamples == 0 && board.Buffers.Select(buffer => buffer.Count).SequenceEqual(before));
                using (var cancellation = new CancellationTokenSource())
                {
                    var waiting = board.Session.AppendAsync(rejected, cancellation.Token); cancellation.Cancel();
                    var canceled = false; try { await waiting; } catch (OperationCanceledException) { canceled = true; }
                    Check("backpressured-writer-cancels-without-accepting", canceled && board.Session.Statistics.AcceptedSamples == 512 && board.Session.Statistics.PendingBatches == 2);
                }
                board.Refresh(); var pausedImage = Image("paused-queue"); Save(pausedImage);
                foreach (var batch in new[] { first, second }) UpdateOracle(batch);
                board.Session.Resume(); await board.Session.FlushAsync(); FlushControlTemplateWindow(window); board.Refresh();
                Check("resume-drains-original-batches-in-order", CompleteOriginalOrder());
                Check("resume-clears-pending-and-keeps-bound", board.Session.Statistics is { PendingBatches: 0, PendingSamples: 0, CommittedSamples: 512 } && board.Session.Statistics.MaximumObservedPendingBatches <= 2);
                Check("paused-batches-coalesce-into-one-ui-commit", board.Session.Statistics is { AppliedUiBatches: 1, CoalescedBatches: 1 });
                chart.FollowLatestData = false; chart.Viewport = new ChartViewport(150, 250, 0, 100); FlushControlTemplateWindow(window);
                var history = chart.Viewport;
                var uiThreadId = Environment.CurrentManagedThreadId; var producerThreadIds = new List<int>();
                foreach (var start in new[] { 384, 448, 512 })
                {
                    var batch = IncrementalStreamingBoard.Batch(start); UpdateOracle(batch);
                    producerThreadIds.Add(await Task.Run(async () => { var thread = Environment.CurrentManagedThreadId; await board.Session.AppendAsync(batch); return thread; }));
                    await board.Session.FlushAsync();
                }
                FlushControlTemplateWindow(window);
                Check("background-producer-commits-on-native-ui-thread", producerThreadIds.All(thread => thread != uiThreadId) && board.LastApplyThreadId == uiThreadId);
                Check("sliding-window-oldest-eviction-and-full-original-order", CompleteOriginalOrder() && board.Buffers.All(buffer => buffer.Count == 512 && buffer.EvictedCount == 64));
                Check("immutable-previous-snapshots-survive-window-shift", originalSnapshots.Select((snapshot, s) => snapshot.Points.Count == 256 && snapshot.Points.Select((point, i) => point.SourceIndex == i && ReferenceEquals(point.Item, initialRows[s][i]) && point.StableId?.Equals($"stream-{s}-sample-{i}") == true && point.Point == new ChartPoint(i, 10 + s * 22 + i % 16 * .55)).All(value => value)).All(value => value));
                Check("follow-off-keeps-requested-history-during-append", !chart.FollowLatestData && chart.Viewport == history);
                Check("selection-reindexes-surviving-original-business-item", chart.SelectedPoint is { Index: 136 } survived && ReferenceEquals(survived.Item, oldSelected) && Equals(survived.StableId, oldSelected.Id));
                using (chart.DeferDataUpdates())
                    for (var s = 0; s < 4; s++)
                    {
                        board.Buffers[s].RemoveRange(0, 16); expected[s].RemoveRange(0, 16);
                        var replacements = expected[s].Take(8).Select(row => row with { Id = "replacement-" + row.Id, Y = row.Y + 3 }).ToArray();
                        board.Buffers[s].ReplaceRange(0, 8, replacements); expected[s].RemoveRange(0, 8); expected[s].InsertRange(0, replacements);
                    }
                FlushControlTemplateWindow(window);
                Check("batch-remove-replace-retains-full-original-index-id-item", CompleteOriginalOrder() && expected.All(rows => rows.Count == 496 && rows[0].Id.StartsWith("replacement-", StringComparison.Ordinal)));
                chart.FollowLatestData = true; FlushControlTemplateWindow(window); board.Refresh();
                Check("follow-resume-jumps-to-latest-independent-of-stream-pause", chart.VisibleRange is { } latest && Math.Abs(latest.XMaximum - 575) < 1e-8 && Math.Abs(latest.XMaximum - latest.XMinimum - 128) < 1e-8);
                const int captureOrdinal = 511; var captureIndex = expected[0].FindIndex(row => row.X == captureOrdinal);
                var hit = chart.HitTestPoint(Project(chart, captureOrdinal, expected[0][captureIndex].Y), .001);
                Check("shifted-window-hit-has-current-index-and-original-item", Original(hit, chart.Series[0], captureIndex, expected[0][captureIndex]));
                var routed = HoverTemplateChart(chart, window, Project(chart, captureOrdinal, expected[0][captureIndex].Y));
                var tooltip = chart.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Name == "StreamingTooltipId").Select(text => text.Text).ToArray();
                var nearest = Nearest(chart, routed.EventPositionAfterRouting, expected);
                Check("routed-tooltip-uses-independent-full-business-array", Original(chart.HoveredPoint, chart.Series[nearest.Stream], nearest.Index, expected[nearest.Stream][nearest.Index]) && tooltip.Contains(expected[nearest.Stream][nearest.Index].Id));
                var resumedImage = Image("resumed-follow"); Save(resumedImage);
                chart.IsTooltipEnabled = false; FlushControlTemplateWindow(window);
                var chartImage = Image("four-stream-chart"); var raster = CaptureControlTemplateRaster(chart, chartImage);
                var colors = new[] { "#4385F4", "#19A798", "#F2A33A", "#D760A5" }; var regions = new List<object>();
                for (var s = 0; s < 4; s++)
                {
                    var point = Project(chart, captureOrdinal, 10 + s * 22 + captureOrdinal % 16 * .55); var region = new Rect(point.X - 7, point.Y - 7, 14, 14);
                    var ink = raster.Count(TemplatePixelRect(region), Avalonia.Media.Color.Parse(colors[s]), 24);
                    Check($"stream-{s}-original-signal-visible", ink >= 10); regions.Add(new { stream = s, color = colors[s], region, pixelRegion = TemplatePixelRect(region), ink });
                }
                var renderedPlot = chart.PlotArea; var renderedRange = chart.VisibleRange; var resumedStats = board.Session.Statistics;
                board.Session.Pause(); Check("cancel-fixture-accepted-within-bound", board.Session.TryAppend(IncrementalStreamingBoard.Batch(576)) && board.Session.TryAppend(IncrementalStreamingBoard.Batch(640)));
                var draining = board.Session.FlushAsync(); board.Session.Cancel(); var flushCanceled = false;
                try { await draining; } catch (OperationCanceledException) { flushCanceled = true; }
                FlushControlTemplateWindow(window); board.Refresh();
                Check("cancel-releases-pending-and-cancels-flush-without-commit", flushCanceled && board.Session.Statistics is { PendingBatches: 0, PendingSamples: 0, CanceledSamples: 512 } && CompleteOriginalOrder());
                Check("completed-session-refuses-new-batches", board.Session.IsCompleted && !board.Session.TryAppend(IncrementalStreamingBoard.Batch(704)));
                _incrementalStreamingEvidence.Add(new
                {
                    theme = label, pausedImage, resumedImage, chartImage, plot = renderedPlot, range = renderedRange, captureOrdinal, regions,
                    initialCount = 256, retainedCount = expected[0].Count, latestX = 575, paused, resumedStats, finalStats = board.Session.Statistics,
                    uiThreadId, producerThreadIds, applyThreadId = board.LastApplyThreadId,
                    currentOriginalRows = chart.Series.Select((series, s) => series.CreateSnapshot().Points.Select(point => new { stream = s, index = point.SourceIndex, id = point.StableId, x = point.Point.X, y = point.Point.Y, itemId = (point.Item as StreamingRow)?.Id }).ToArray()).ToArray(),
                    initialSnapshotRows = originalSnapshots.Select((snapshot, s) => snapshot.Points.Select(point => new { stream = s, index = point.SourceIndex, id = point.StableId, x = point.Point.X, y = point.Point.Y, itemId = (point.Item as StreamingRow)?.Id }).ToArray()).ToArray(),
                    hit = hit is null ? null : new { hit.Index, hit.StableId, hit.Point }, routed, tooltip, expectedTooltipStream = nearest.Stream, expectedTooltipIndex = nearest.Index,
                    colorChannelTolerance = 24, alphaMinimum = 250, minimumInkPerStream = 10, physicalInputValidated = false, performanceMeasured = false,
                });
                void UpdateOracle(StreamingSample[] batch)
                {
                    foreach (var item in batch) expected[item.Stream].Add(item.Row);
                    foreach (var rows in expected) if (rows.Count > 512) rows.RemoveRange(0, rows.Count - 512);
                }
                bool CompleteOriginalOrder() => expected.Select((rows, s) => rows.Count == board.Buffers[s].Count && rows.Select((row, i) => ReferenceEquals(row, board.Buffers[s][i])).All(value => value) && chart.Series[s].CreateSnapshot().Points.Select((point, i) => point.SourceIndex == i && ReferenceEquals(point.Item, rows[i]) && Equals(point.StableId, rows[i].Id) && point.Point == new ChartPoint(rows[i].X, rows[i].Y)).All(value => value)).All(value => value);
            }
            finally { window.Close(); }
            void Save(string path) { using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)); bitmap.Render(window); bitmap.Save(path); }
        }
        static bool Original(ChartPointHit? hit, ChartSeries series, int index, StreamingRow row) => hit is not null && ReferenceEquals(hit.Series, series) && hit.Index == index && ReferenceEquals(hit.Item, row) && Equals(hit.StableId, row.Id) && hit.Point == new ChartPoint(row.X, row.Y);
        static Point Project(CartesianChart chart, double x, double y) { var range = chart.VisibleRange!.Value; var plot = chart.PlotArea; return new(plot.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width, plot.Bottom - (y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height); }
        static (int Stream, int Index) Nearest(CartesianChart chart, Point anchor, List<StreamingRow>[] sources)
        {
            var best = double.PositiveInfinity; var answer = (Stream: 0, Index: 0);
            for (var s = 0; s < sources.Length; s++) for (var i = 0; i < sources[s].Count; i++) { var row = sources[s][i]; var point = Project(chart, row.X, row.Y); if (!chart.PlotArea.Contains(point)) continue; var delta = point - anchor; var distance = delta.X * delta.X + delta.Y * delta.Y; if (distance <= best) { best = distance; answer = (s, i); } }
            return answer;
        }
    }
}
