using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

internal static partial class StreamingRegression
{
    private const int ChartRevisionCount = 12;
    private const int ChartProjectionLimit = 8192;
    private const int ChartViewBuildLimit = 8;

    // Separate from the seconds-based data fixture: integer sample X makes the
    // 512-unit trailing window contain about 513 original rows in each series.
    private static ChartReceipt ExerciseRealChart()
    {
        var buffers = Enumerable.Range(0, SeriesCount).Select(_ => new ChartDataBuffer<Row>(CapacityPerSeries)).ToArray();
        var series = Enumerable.Range(0, SeriesCount).Select(index => new LineSeries
        {
            Title = $"live-{index}", ItemsSource = buffers[index], CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y),
            IdPath = nameof(Row.Id), MarkerSize = 0, SamplingMode = ChartSamplingMode.None
        }).ToArray();
        var chart = new CartesianChart
        {
            Width = 1200, Height = 650, ShowLegend = false, IsTooltipEnabled = true,
            XAxis = new NumericAxis { Minimum = 0, Maximum = CapacityPerSeries + ChartRevisionCount * SamplesPerSeriesPerRevision },
            YAxis = new NumericAxis { Minimum = -10, Maximum = 100 },
            FollowLatestData = true, FollowLatestWindow = 512, SelectionMode = ChartSelectionMode.Multiple
        };
        foreach (var item in series) chart.Series.Add(item);
        var initial = Enumerable.Range(0, SeriesCount).Select(index => Enumerable.Range(0, CapacityPerSeries).Select(sequence => CreateChartRow(index, sequence)).ToArray()).ToArray();
        var oracle = initial.Select(rows => new List<Row>(rows)).ToArray();
        var window = new Window { Width = 1200, Height = 650, Content = chart };
        try
        {
            var before = ChartCounters(chart, series); var started = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < SeriesCount; index++) buffers[index].AppendRange(initial[index]);
            window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = chart.PlotArea; RenderChart(chart); Dispatcher.UIThread.RunJobs();
            var cold = Work(started, allocated, before, ChartCounters(chart, series));
            var initialRange = chart.VisibleRange;
            var initialContexts = ChartContexts(chart);
            var survivor = initial[0][^1]; var evicted = initial[0][0];
            var survivorPosition = initialContexts[0].Projection.Project(new(survivor.ExpectedX, survivor.ExpectedY));
            var initialHit = chart.HitTestPoint(survivorPosition, .0001);
            var initialIdentity = SameHit(initialHit, survivor, CapacityPerSeries - 1, series[0]);
            // Prime the first lazy page identity lookup before warm revisions,
            // retaining its cold work separately instead of hiding it in a gate.
            before = ChartCounters(chart, series); started = Stopwatch.GetTimestamp(); allocated = GC.GetAllocatedBytesForCurrentThread();
            var evictedHit = new ChartPointHit(series[0], new(evicted.ExpectedX, evicted.ExpectedY), 0,
                initialContexts[0].Projection.Project(new(evicted.ExpectedX, evicted.ExpectedY))) { StableId = evicted.Id, Item = evicted };
            var survivorHit = new ChartPointHit(series[0], new(survivor.ExpectedX, survivor.ExpectedY), -1, survivorPosition) { StableId = survivor.Id, Item = survivor };
            chart.SelectedPoints = [evictedHit, survivorHit];
            RenderChart(chart); Dispatcher.UIThread.RunJobs();
            var selectionPrime = Work(started, allocated, before, ChartCounters(chart, series));
            var initialSelection = chart.SelectedPoints?.Select(hit => HitRaw(hit, Equals(hit.StableId, evicted.Id) ? evicted : survivor)).ToArray() ?? [];
            var activeInitialSelection = chart.SelectedPoints ?? [];
            var initialSelectionValid = initialSelection.Length == 2 && activeInitialSelection.Any(hit => ReferenceEquals(hit.Item, evicted)) && activeInitialSelection.Any(hit => ReferenceEquals(hit.Item, survivor));
            using var session = new ChartStreamingSession<Row>(values =>
            {
                using var deferred = chart.DeferDataUpdates();
                for (var index = 0; index < SeriesCount; index++)
                {
                    var group = index;
                    buffers[index].AppendRange(values.Where(row => row.Series == group));
                }
            }, IncrementalWorkLimit);
            var revisions = new List<ChartRevisionReceipt>(); var navigation = new List<NavigationReceipt>();
            ChartViewport? pausedRange = null;
            for (var revision = 0; revision < ChartRevisionCount; revision++)
            {
                var input = Enumerable.Range(0, SeriesCount).SelectMany(index => Enumerable.Range(0, SamplesPerSeriesPerRevision)
                    .Select(offset => CreateChartRow(index, CapacityPerSeries + revision * SamplesPerSeriesPerRevision + offset))).ToArray();
                before = ChartCounters(chart, series); started = Stopwatch.GetTimestamp(); allocated = GC.GetAllocatedBytesForCurrentThread();
                var statisticsBefore = session.Statistics;
                Drain(session.AppendAsync(input)); Drain(session.FlushAsync());
                _ = chart.PlotArea; RenderChart(chart); Dispatcher.UIThread.RunJobs();
                var after = ChartCounters(chart, series);
                var work = Work(started, allocated, before, after);
                var statisticsAfter = session.Statistics;
                for (var index = 0; index < SeriesCount; index++)
                {
                    oracle[index].AddRange(input.Where(row => row.Series == index)); oracle[index].RemoveRange(0, SamplesPerSeriesPerRevision);
                }
                var latest = CapacityPerSeries - 1d + (revision + 1) * SamplesPerSeriesPerRevision;
                var expectedRange = chart.FollowLatestData ? new ChartViewport(latest - 512, latest, -10, 100) : pausedRange!.Value;
                var actualRange = chart.VisibleRange;
                var selected = chart.SelectedPoints?.Select(hit => HitRaw(hit, survivor)).ToArray() ?? [];
                var selectedIdentity = chart.SelectedPoints is { Count: 1 } && SameHit(chart.SelectedPoints[0], survivor,
                    CapacityPerSeries - 1 - (revision + 1) * SamplesPerSeriesPerRevision, series[0]);
                var targetSeries = revision % SeriesCount;
                var targetSequence = chart.FollowLatestData ? (long)latest : (long)Math.Floor((expectedRange.XMinimum + expectedRange.XMaximum) / 2);
                var targetOrdinal = checked((int)(targetSequence - (revision + 1L) * SamplesPerSeriesPerRevision));
                var expected = oracle[targetSeries][targetOrdinal];
                var context = ChartContexts(chart)[targetSeries];
                var query = context.Projection.Project(new(expected.ExpectedX, expected.ExpectedY));
                var hit = chart.HitTestPoint(query, .0001);
                var hitIdentity = SameHit(hit, expected, targetOrdinal, series[targetSeries]);
                var workValid = work.Delta["bufferCopiedItems"] == IncrementalWorkLimit && work.Delta["mappedRows"] == IncrementalWorkLimit &&
                    work.Delta["bufferSummaryRows"] <= SummaryVisitLimit && work.Delta["chartProjectedPoints"] > 0 &&
                    work.Delta["chartProjectedPoints"] <= ChartProjectionLimit && work.Delta["viewBuilds"] <= ChartViewBuildLimit;
                var coalesced = work.Delta["deferredDataCommits"] == 1 && statisticsAfter.AppliedUiBatches == statisticsBefore.AppliedUiBatches + 1;
                var counts = buffers.All(buffer => buffer.Count == CapacityPerSeries) && buffers.Sum(buffer => buffer.EvictedCount) == (revision + 1L) * IncrementalWorkLimit;
                revisions.Add(new(revision, chart.FollowLatestData, expectedRange, actualRange, work, statisticsBefore, statisticsAfter, targetSeries,
                    Raw(targetOrdinal, expected.Id, expected, expected.ExpectedX, expected.ExpectedY, true), hit is null ? null : HitRaw(hit, expected), selected,
                    workValid, coalesced, counts, actualRange == expectedRange, hitIdentity, selectedIdentity));
                // Exercise explicit navigation/pause and later follow restoration
                // outside the source/render work interval; retain both results.
                if (revision == 3)
                {
                    var oldRange = chart.VisibleRange;
                    pausedRange = new(latest - 1024, latest - 512, -10, 100);
                    chart.Viewport = pausedRange; _ = chart.PlotArea; RenderChart(chart); Dispatcher.UIThread.RunJobs();
                    navigation.Add(new("explicit-viewport-pauses-follow", revision, oldRange, pausedRange, chart.VisibleRange, chart.FollowLatestData,
                        !chart.FollowLatestData && chart.VisibleRange == pausedRange));
                }
                if (revision == 6)
                {
                    var oldRange = chart.VisibleRange;
                    chart.FollowLatestData = true; _ = chart.PlotArea; RenderChart(chart); Dispatcher.UIThread.RunJobs();
                    var expectedFollow = new ChartViewport(latest - 512, latest, -10, 100);
                    navigation.Add(new("restore-follow-catches-up", revision, oldRange, expectedFollow, chart.VisibleRange, chart.FollowLatestData,
                        chart.FollowLatestData && chart.VisibleRange == expectedFollow));
                }
                Dispatcher.UIThread.RunJobs();
            }
            Drain(session.CompleteAsync());
            var finalSnapshots = series.Select(item => item.CreateSnapshot()).ToArray();
            var finalIdentities = Enumerable.Range(0, SeriesCount).Select(index => Compare(finalSnapshots[index], oracle[index])).ToArray();
            var checks = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["initial_original_hit_identity"] = initialIdentity,
                ["initial_surviving_and_soon_evicted_selections_are_both_active"] = initialSelectionValid,
                ["initial_follow_window_is_exactly_512_sample_units"] = initialRange == new ChartViewport(CapacityPerSeries - 1 - 512, CapacityPerSeries - 1, -10, 100),
                ["all_twelve_source_render_intervals_meet_declared_work_gates"] = revisions.All(value => value.WorkWithinGate),
                ["all_four_buffer_updates_have_one_deferred_chart_commit_and_one_ui_callback"] = revisions.All(value => value.CoalescedChartCommit),
                ["all_counts_and_explicit_evictions_balance"] = revisions.All(value => value.CountsValid),
                ["all_follow_and_paused_ranges_match_independent_expected_values"] = revisions.All(value => value.ViewRangeValid),
                ["every_hit_preserves_original_item_id_raw_point_and_current_ordinal"] = revisions.All(value => value.HitIdentity),
                ["surviving_selection_rebases_and_evicted_selection_is_removed"] = revisions.All(value => value.SelectionIdentity),
                ["explicit_navigation_pauses_and_restoring_follow_catches_up"] = navigation.Count == 2 && navigation.All(value => value.Passed),
                ["all_final_million_rows_match_full_independent_source_oracle"] = finalIdentities.All(value => value.Passed),
                ["all_6000_accepted_samples_are_committed_without_silent_drop"] = session.Statistics.AcceptedSamples == ChartRevisionCount * IncrementalWorkLimit &&
                    session.Statistics.CommittedSamples == ChartRevisionCount * IncrementalWorkLimit && session.Statistics.PendingSamples == 0 &&
                    session.Statistics.RejectedSamples == 0 && session.Statistics.CanceledSamples == 0 && session.Statistics.FaultedSamples == 0
            };
            return new("four-series-million-row-real-chart-follow-selection", ChartRevisionCount, 512, ChartProjectionLimit, ChartViewBuildLimit,
                cold, selectionPrime, initialRange, initialSelection, revisions, navigation, finalIdentities, session.Statistics, checks, checks.Values.All(value => value));
        }
        finally { window.Content = null; window.Close(); Dispatcher.UIThread.RunJobs(); }
    }

    private static Row CreateChartRow(int series, long sequence)
    {
        var phase = sequence % 1024; var triangle = phase < 512 ? phase : 1024 - phase;
        var noise = ((sequence * 1_103_515_245 + 12_345) & 511) - 255;
        return new(series, sequence, $"series-{series}/sample-{sequence}", sequence, (triangle * 32 + noise) / 256d + series * 3);
    }

    private static bool SameHit(ChartPointHit? hit, Row row, int ordinal, LineSeries series) => hit is not null && ReferenceEquals(hit.Series, series) &&
        hit.Index == ordinal && ReferenceEquals(hit.Item, row) && Equals(hit.StableId, row.Id) && (hit.RawPoint ?? hit.Point) == new ChartPoint(row.ExpectedX, row.ExpectedY);
    private static RawRow HitRaw(ChartPointHit hit, Row expected)
    {
        var point = hit.RawPoint ?? hit.Point;
        var row = hit.Item as Row;
        return Raw(hit.Index, hit.StableId?.ToString(), row, point.X, point.Y, ReferenceEquals(hit.Item, expected));
    }

    private static ChartSeriesRenderContext[] ChartContexts(CartesianChart chart)
    {
        var scene = typeof(CartesianChart).GetMethod("GetScene", Any)!.Invoke(chart, null)!;
        var snapshots = (Array)scene.GetType().GetProperty("Snapshots", Any)!.GetValue(scene)!;
        return snapshots.Cast<object>().Select(snapshot => (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context", Any)!.GetValue(snapshot)!).ToArray();
    }

    private static Dictionary<string, long> ChartCounters(CartesianChart chart, LineSeries[] series)
    {
        var counters = Counters(series);
        counters["bufferIdentityRows"] = Counter(Product.GetType("NT.AvaloniaControl.Charts.ChartDataBufferMetrics")!, "IdentityRowVisitCount", null);
        counters["chartProjectedPoints"] = Counter(Product.GetType("NT.AvaloniaControl.Charts.ChartVisibilityQuery")!, "ProjectedCount", null);
        counters["dataIndexBuilds"] = Counter(Product.GetType("NT.AvaloniaControl.Charts.ChartDataIndex")!, "BuildCount", null);
        counters["preparedDataBuilds"] = Counter(typeof(CartesianChart), "_preparedDataBuildCount", chart);
        counters["viewBuilds"] = Counter(typeof(CartesianChart), "_viewBuildCount", chart);
        counters["deferredDataCommits"] = Counter(typeof(CartesianChart), "_deferredDataCommitCount", chart);
        return counters;
    }

    private static void RenderChart(CartesianChart chart)
    {
        var group = new DrawingGroup(); using (var drawing = group.Open()) chart.Render(drawing); group.Children.Clear();
    }

    private sealed record ChartRevisionReceipt(int Revision, bool FollowLatestData, ChartViewport ExpectedRange, ChartViewport? ActualRange, WorkReceipt Work,
        ChartStreamingStatistics StatisticsBefore, ChartStreamingStatistics StatisticsAfter, int QuerySeries, RawRow ExpectedHit, RawRow? ActualHit, RawRow[] ActualSelection,
        bool WorkWithinGate, bool CoalescedChartCommit, bool CountsValid, bool ViewRangeValid, bool HitIdentity, bool SelectionIdentity);
    private sealed record NavigationReceipt(string Operation, int AfterRevision, ChartViewport? PreviousRange, ChartViewport? ExpectedRange, ChartViewport? ActualRange, bool FollowLatestData, bool Passed);
    private sealed record ChartReceipt(string Id, int RevisionCount, int FollowWindow, int MaximumProjectedPoints, int MaximumViewBuilds, WorkReceipt ColdSourceViewAndRender,
        WorkReceipt ColdSelectionIdentityPriming, ChartViewport? InitialRange, RawRow[] InitialSelection, List<ChartRevisionReceipt> Revisions, List<NavigationReceipt> Navigation,
        IdentityReceipt[] FinalSnapshots, ChartStreamingStatistics FinalStatistics, Dictionary<string, bool> Checks, bool Passed);
}
