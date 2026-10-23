using Avalonia;
using Avalonia.Controls;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal List<object> IncrementalStreamingBindingDiagnostics { get; } = [];
    internal void VerifyIncrementalStreamingBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("IncrementalStreamingExamples")!; expander.IsExpanded = true; FlushBindings();
        var chart = this.FindControl<CartesianChart>("UsageIncrementalStreamingChart")!; var source = Model.StreamingRecords; var series = chart.Series.Single();
        void Check(string name, bool value) => checks["streaming-compiled-" + name] = value;
        var initialRows = source.ToArray(); var oldSnapshot = series.CreateSnapshot();
        Check("bounded-buffer-and-mapping-are-bound", ReferenceEquals(series.ItemsSource, source) && source.Capacity == 256 && oldSnapshot.Points.Count == 128 && series.CategoryPath == "X" && series.ValuePath == "Y" && series.IdPath == "Id");
        Check("follow-and-window-are-bound", chart.FollowLatestData && chart.FollowLatestWindow == 64 && chart.VisibleRange is { XMaximum: 127, XMinimum: 63 });
        var expected = initialRows.ToList(); var batch = ChartUsageViewModel.StreamingRows(128, 160); expected.AddRange(batch); expected.RemoveRange(0, 32); source.AppendRange(batch); FlushBindings();
        Check("append-window-evicts-oldest-original-records", source.Count == 256 && source.EvictedCount == 32 && source.Select((row, i) => ReferenceEquals(row, expected[i])).All(value => value));
        Check("old-projection-is-immutable-after-eviction", oldSnapshot.Points.Count == 128 && oldSnapshot.Points.Select((point, i) => point.SourceIndex == i && ReferenceEquals(point.Item, initialRows[i]) && Equals(point.StableId, initialRows[i].Id)).All(value => value));
        const int ordinal = 255; var index = expected.FindIndex(row => row.X == ordinal); var row = expected[index]; var hit = chart.HitTestPoint(Project(chart, row), .001);
        Check("window-hit-has-current-source-index-id-and-item", hit is not null && hit.Index == index && ReferenceEquals(hit.Item, row) && Equals(hit.StableId, row.Id));
        chart.SetCurrentValue(CartesianChart.SelectedPointProperty, hit); FlushBindings();
        Check("original-selection-is-two-way-bound", ReferenceEquals(Model.StreamingSelectedPoint?.Item, row) && Model.StreamingSelectionText.Contains(row.Id, StringComparison.Ordinal));
        Model.StreamingFollowLatest = false; FlushBindings(); chart.SetCurrentValue(CartesianChart.ViewportProperty, new ChartViewport(100, 180, 0, 80)); FlushBindings();
        Check("follow-and-history-viewport-are-two-way-bound", !chart.FollowLatestData && Model.StreamingViewport == chart.Viewport);
        using (chart.DeferDataUpdates()) { source.RemoveRange(0, 16); expected.RemoveRange(0, 16); var replacements = expected.Take(8).Select(item => item with { Id = "replaced-" + item.Id, Y = item.Y + 2 }).ToArray(); source.ReplaceRange(0, 8, replacements); expected.RemoveRange(0, 8); expected.InsertRange(0, replacements); }
        FlushBindings(); var current = series.CreateSnapshot();
        Check("remove-replace-map-exact-original-records", current.Points.Count == expected.Count && current.Points.Select((point, i) => point.SourceIndex == i && ReferenceEquals(point.Item, expected[i]) && Equals(point.StableId, expected[i].Id) && point.Point == new ChartPoint(expected[i].X, expected[i].Y)).All(value => value));
        Check("follow-off-preserves-requested-history", chart.Viewport is { XMinimum: 100, XMaximum: 180 } && !chart.FollowLatestData);
        Model.StreamingFollowLatest = true; FlushBindings();
        Check("follow-resumes-latest-after-mutations", chart.FollowLatestData && chart.VisibleRange is { XMaximum: 287, XMinimum: 223 });
        IncrementalStreamingBindingDiagnostics.Add(new { source.Capacity, source.Count, source.EvictedCount, oldSnapshotCount = oldSnapshot.Points.Count, currentSnapshotCount = current.Points.Count, hitIndex = hit?.Index, hitId = hit?.StableId, followRange = chart.VisibleRange, physicalInputValidated = false, performanceMeasured = false });
        chart.ClearSelection(); expander.IsExpanded = false; FlushBindings();
        static Point Project(CartesianChart chart, LevelOfDetailUsageRecord row) { var range = chart.VisibleRange!.Value; var plot = chart.PlotArea; return new(plot.Left + (row.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width, plot.Bottom - (row.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height); }
    }

    internal async Task<Dictionary<string, bool>> VerifyIncrementalStreamingSessionAsync()
    {
        var checks = new Dictionary<string, bool>(); var applied = new List<int[]>();
        using var session = new ChartStreamingSession<int>(batch => applied.Add(batch.ToArray()), maximumBatchSize: 8, maximumPendingBatches: 2);
        session.Pause(); checks["streaming-session-paused-accepts-two-batches"] = session.TryAppend(new[] { 1, 2, 3 }) && session.TryAppend(new[] { 4, 5 });
        checks["streaming-session-full-try-is-explicit-rejection"] = !session.TryAppend(new[] { 6 });
        checks["streaming-session-paused-finite-queue-no-commit"] = session.Statistics is { PendingBatches: 2, PendingSamples: 5, CommittedSamples: 0 } && applied.Count == 0;
        using (var cancellation = new CancellationTokenSource()) { var waiting = session.AppendAsync(new[] { 6 }, cancellation.Token); cancellation.Cancel(); var canceled = false; try { await waiting; } catch (OperationCanceledException) { canceled = true; } checks["streaming-session-waiting-writer-cancels-without-accepting"] = canceled && session.Statistics.AcceptedSamples == 5; }
        session.Resume(); await session.FlushAsync();
        checks["streaming-session-flush-commits-in-original-order"] = applied.SelectMany(batch => batch).SequenceEqual(new[] { 1, 2, 3, 4, 5 }) && session.Statistics is { PendingBatches: 0, PendingSamples: 0, CommittedSamples: 5 };
        checks["streaming-session-coalesced-ui-batches-remain-bounded"] = session.Statistics.MaximumObservedPendingBatches <= 2 && session.Statistics.AppliedUiBatches is >= 1 and <= 2;
        await session.CompleteAsync(); checks["streaming-session-complete-stops-acceptance"] = session.IsCompleted && !session.TryAppend(new[] { 7 });
        IncrementalStreamingBindingDiagnostics.Add(new { kind = "session", session.Statistics, applied = applied.SelectMany(batch => batch).ToArray(), physicalInputValidated = false, performanceMeasured = false });
        return checks;
    }
}
