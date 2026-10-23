using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Themes;

namespace Charts.Benchmarks;

// The gates are declared before any measurements. They describe bounded work in
// these fixtures, not paced throughput, a frame budget, a soak or arbitrary data.
internal static partial class StreamingRegression
{
    private const int SeriesCount = 4;
    private const int CapacityPerSeries = 250_000;
    private const int RevisionCount = 120;
    private const int SamplesPerSeriesPerRevision = 125;
    private const int IncrementalWorkLimit = SeriesCount * SamplesPerSeriesPerRevision;
    private const int SummaryVisitLimit = IncrementalWorkLimit + SeriesCount * 512;
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly Assembly Product = typeof(ChartSeries).Assembly;

    public static int Run(string[] args)
    {
        var option = Array.IndexOf(args, "--output");
        var output = option >= 0 && option + 1 < args.Length ? args[option + 1] : "artifacts/charts-streaming/raw.json";
        if (File.Exists(output)) { Console.Error.WriteLine("Streaming evidence already exists; use a new output path."); return 1; }
        var report = new StreamingReport();
        Program.WriteReport(output, report);
        try
        {
            AppBuilder.Configure<BenchmarkApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            report.Scenarios.Add(ExerciseIncrementalWindow());
            report.Scheduler = ExerciseBoundedScheduler();
            report.Mutations = ExerciseMutations();
            report.Chart = ExerciseRealChart();
            Program.WriteReport(output, report);
            report.Passed = report.Scenarios.Count > 0 && report.Scenarios.All(scenario => scenario.Passed) && report.Scheduler.Passed && report.Mutations.Passed && report.Chart.Passed;
        }
        catch (Exception exception) { report.Error = exception.ToString(); Console.Error.WriteLine(exception); }
        report.FinishedUtc = DateTimeOffset.UtcNow;
        Program.WriteReport(output, report);
        return report.Passed ? 0 : 1;
    }

    private static ScenarioReceipt ExerciseIncrementalWindow()
    {
        var buffers = Enumerable.Range(0, SeriesCount).Select(_ => new ChartDataBuffer<Row>(CapacityPerSeries)).ToArray();
        var series = Enumerable.Range(0, SeriesCount).Select(index => new LineSeries
        {
            Title = $"stream-{index}", ItemsSource = buffers[index], CategoryPath = nameof(Row.X),
            ValuePath = nameof(Row.Y), IdPath = nameof(Row.Id), MarkerSize = 0
        }).ToArray();
        EventHandler owner = (_, _) => { };
        foreach (var item in series) item.Changed += owner;
        try
        {
            var initial = Enumerable.Range(0, SeriesCount).Select(index => Enumerable.Range(0, CapacityPerSeries).Select(sequence => CreateRow(index, sequence)).ToArray()).ToArray();
            var before = Counters(series); var started = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < SeriesCount; index++) buffers[index].AppendRange(initial[index]);
            var oldSnapshots = series.Select(item => item.CreateSnapshot()).ToArray();
            var initialActualSummaries = oldSnapshots.Select(ReadExtrema).ToArray();
            var cold = Work(started, allocated, before, Counters(series));
            var initialExpectedSummaries = initial.Select(ExpectedSummary).ToArray();
            var oracle = initial.Select(rows => new List<Row>(rows)).ToArray();
            var samples = new List<RevisionReceipt>(); var snapshots = oldSnapshots;
            var checks = new Dictionary<string, bool>(StringComparer.Ordinal);
            var previousBufferVersions = buffers.Select(buffer => buffer.Version).ToArray();
            var previousSourceVersions = series.Select(item => item.SourceVersion).ToArray();
            using var session = new ChartStreamingSession<Row>(values =>
            {
                for (var index = 0; index < SeriesCount; index++)
                {
                    var group = index;
                    buffers[index].AppendRange(values.Where(row => row.Series == group));
                }
            }, SeriesCount * SamplesPerSeriesPerRevision);
            for (var revision = 0; revision < RevisionCount; revision++)
            {
                var next = Enumerable.Range(0, SeriesCount).Select(index => Enumerable.Range(0, SamplesPerSeriesPerRevision)
                    .Select(offset => CreateRow(index, CapacityPerSeries + revision * SamplesPerSeriesPerRevision + offset)).ToArray()).ToArray();
                var input = next.SelectMany(rows => rows).ToArray();
                before = Counters(series); started = Stopwatch.GetTimestamp(); allocated = GC.GetAllocatedBytesForCurrentThread();
                Drain(session.AppendAsync(input)); Drain(session.FlushAsync());
                var committedCounters = Counters(series);
                var commitWork = Work(started, allocated, before, committedCounters);
                var mappingStarted = Stopwatch.GetTimestamp(); var mappingAllocated = GC.GetAllocatedBytesForCurrentThread();
                snapshots = series.Select(item => item.CreateSnapshot()).ToArray();
                var afterMapping = Counters(series);
                var mappingWork = Work(mappingStarted, mappingAllocated, committedCounters, afterMapping);
                var extremaStarted = Stopwatch.GetTimestamp(); var extremaAllocated = GC.GetAllocatedBytesForCurrentThread();
                var actualSummaries = snapshots.Select(ReadExtrema).ToArray();
                var afterExtrema = Counters(series);
                var extremaWork = Work(extremaStarted, extremaAllocated, afterMapping, afterExtrema);
                var work = Work(started, allocated, before, afterExtrema);
                var versionsValid = true;
                for (var index = 0; index < SeriesCount; index++)
                {
                    oracle[index].AddRange(next[index]); oracle[index].RemoveRange(0, SamplesPerSeriesPerRevision);
                    versionsValid &= buffers[index].Version == previousBufferVersions[index] + 1 && series[index].SourceVersion > previousSourceVersions[index] && snapshots[index].Version == series[index].SourceVersion;
                    previousBufferVersions[index] = buffers[index].Version; previousSourceVersions[index] = series[index].SourceVersion;
                }
                var expectedSummaries = oracle.Select(ExpectedSummary).ToArray();
                var incremental = work.Delta["bufferCopiedItems"] == IncrementalWorkLimit && work.Delta["mappedRows"] == IncrementalWorkLimit &&
                    mappingWork.Delta["sourceCategoryGetterReads"] == IncrementalWorkLimit && mappingWork.Delta["sourceValueGetterReads"] == IncrementalWorkLimit &&
                    work.Delta["sourceCategoryGetterReads"] <= IncrementalWorkLimit * 3 && work.Delta["sourceValueGetterReads"] <= IncrementalWorkLimit * 3;
                var retained = snapshots.Sum(snapshot => snapshot.Points.Count) == SeriesCount * CapacityPerSeries;
                var noSilentLoss = buffers.Sum(buffer => buffer.EvictedCount) == (long)(revision + 1) * SeriesCount * SamplesPerSeriesPerRevision;
                var summaryWorkValid = extremaWork.Delta["bufferSummaryRows"] <= SummaryVisitLimit && actualSummaries.SequenceEqual(expectedSummaries);
                samples.Add(new(revision, 50, SeriesCount * SamplesPerSeriesPerRevision, work, commitWork, mappingWork, extremaWork,
                    expectedSummaries, actualSummaries, summaryWorkValid, versionsValid, incremental, retained, noSilentLoss));
            }
            Drain(session.CompleteAsync());
            var current = Enumerable.Range(0, SeriesCount).Select(index => Compare(snapshots[index], oracle[index])).ToArray();
            var historical = Enumerable.Range(0, SeriesCount).Select(index => Compare(oldSnapshots[index], initial[index])).ToArray();
            checks["all_120_revisions_retain_one_million_rows"] = samples.All(sample => sample.RetainedCountCorrect);
            checks["all_revisions_copy_and_map_exactly_500_new_rows_with_bounded_observer_getters"] = samples.All(sample => sample.IncrementalWorkWithinGate);
            checks["all_source_versions_advance_and_snapshot_versions_match"] = samples.All(sample => sample.VersionsValid);
            checks["every_eviction_is_explicitly_counted"] = samples.All(sample => sample.NoSilentLoss);
            checks["all_page_summaries_match_full_source_oracle_within_2548_row_visit_gate"] = samples.All(sample => sample.SummaryWorkWithinGate);
            checks["initial_summaries_match_full_source_oracle"] = initialActualSummaries.SequenceEqual(initialExpectedSummaries);
            checks["all_one_million_final_rows_match_independent_item_id_index_coordinates"] = current.All(receipt => receipt.Passed);
            checks["all_one_million_original_snapshot_rows_remain_unchanged"] = historical.All(receipt => receipt.Passed);
            var produced = (long)RevisionCount * SeriesCount * SamplesPerSeriesPerRevision;
            var statistics = session.Statistics;
            var ledger = new { initial = SeriesCount * CapacityPerSeries, produced, accepted = statistics.AcceptedSamples, committed = statistics.CommittedSamples,
                retained = buffers.Sum(buffer => buffer.Count), evicted = buffers.Sum(buffer => buffer.EvictedCount), rejected = 0, canceled = 0,
                pending = 0, commitBoundary = "UI callback applies source buffers; no chart render/present is implied", statistics,
                invariant = SeriesCount * CapacityPerSeries + produced == buffers.Sum(buffer => (long)buffer.Count + buffer.EvictedCount) };
            checks["initial_plus_committed_equals_retained_plus_evicted"] = ledger.invariant;
            checks["single_session_accepts_and_commits_every_sample_without_rejection_or_cancellation"] =
                statistics.AcceptedSamples == produced && statistics.CommittedSamples == produced && statistics.CanceledSamples == 0 && statistics.FaultedSamples == 0 && statistics.RejectedSamples == 0 && statistics.PendingSamples == 0;
            return new("four-series-million-row-structural-window", cold, initialExpectedSummaries, initialActualSummaries,
                samples, current, historical, ledger, checks, checks.Values.All(value => value));
        }
        finally { foreach (var item in series) item.Changed -= owner; }
    }

    private static SchedulerReceipt ExerciseBoundedScheduler()
    {
        var applied = new List<int>(); var callbackInputs = new List<int[]>();
        using var session = new ChartStreamingSession<int>(values => { callbackInputs.Add(values.ToArray()); applied.AddRange(values); }, 2, 2);
        session.Pause();
        var first = session.TryAppend(new[] { 1, 2 }); var second = session.TryAppend(new[] { 3, 4 });
        var rejected = session.TryAppend(new[] { 5, 6 });
        var waiting = session.AppendAsync(new[] { 7, 8 });
        var paused = session.Statistics; var waitingBeforeResume = !waiting.IsCompleted;
        Dispatcher.UIThread.RunJobs(); var appliedWhilePaused = applied.ToArray();
        session.Resume(); Drain(waiting); Drain(session.CompleteAsync());
        var completed = session.Statistics;
        var canceledApplied = new List<int>();
        using var canceled = new ChartStreamingSession<int>(values => canceledApplied.AddRange(values), 2, 2);
        canceled.Pause(); var canceledFirst = canceled.TryAppend(new[] { 10, 11 }); var canceledSecond = canceled.TryAppend(new[] { 12, 13 });
        var beforeCancel = canceled.Statistics; canceled.Cancel(); Dispatcher.UIThread.RunJobs();
        var cancellationObserved = false;
        try { canceled.Completion.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancellationObserved = true; }
        var afterCancel = canceled.Statistics;
        var checks = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["two_batches_are_accepted_and_third_is_explicitly_rejected"] = first && second && !rejected,
            ["one_waiting_writer_does_not_expand_accepted_queue"] = waitingBeforeResume && paused.WaitingWriters == 1 && paused.PendingBatches == 2 && paused.AcceptedSamples == 4,
            ["pause_applies_no_samples"] = appliedWhilePaused.Length == 0,
            ["all_accepted_samples_commit_in_order"] = applied.SequenceEqual(new[] { 1, 2, 3, 4, 7, 8 }),
            ["two_initial_batches_coalesce_into_one_ui_callback"] = callbackInputs.Count == 2 && callbackInputs[0].SequenceEqual(new[] { 1, 2, 3, 4 }) && completed.CoalescedBatches >= 1,
            ["pending_bound_includes_inflight_batches"] = completed.MaximumObservedPendingBatches <= 2 && completed.PendingBatches == 0,
            ["accepted_committed_pending_canceled_faulted_ledger_balances"] = completed.AcceptedSamples == completed.CommittedSamples + completed.PendingSamples + completed.CanceledSamples + completed.FaultedSamples,
            ["produced_equals_accepted_plus_explicit_rejection"] = completed.AcceptedSamples + completed.RejectedSamples == 8 && completed.RejectedSamples == 2,
            ["cancel_reports_accepted_pending_samples_and_late_callback_applies_nothing"] = canceledFirst && canceledSecond && cancellationObserved && afterCancel.AcceptedSamples == 4 && afterCancel.CanceledSamples == 4 && afterCancel.CommittedSamples == 0 && afterCancel.PendingSamples == 0 && canceledApplied.Count == 0
        };
        return new("bounded-pause-backpressure-coalescing-cancel", [1, 2, 3, 4, 5, 6, 7, 8], [1, 2, 3, 4, 7, 8], applied.ToArray(), callbackInputs,
            paused, completed, beforeCancel, afterCancel, checks, checks.Values.All(value => value));
    }

    private static MutationReceipt ExerciseMutations()
    {
        var buffer = new ChartDataBuffer<Row>(12);
        var series = new LineSeries { ItemsSource = buffer, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y), IdPath = nameof(Row.Id) };
        EventHandler owner = (_, _) => { }; series.Changed += owner;
        try
        {
            var oracle = new List<Row>(); var retainedSnapshots = new List<(ChartDataSnapshot Snapshot, Row[] Rows)>();
            var steps = new List<MutationStep>(); long evicted = 0;
            void Step(string operation, int index, int removeCount, int firstSequence, int inputCount, Action<Row[]> mutate)
            {
                var input = Enumerable.Range(firstSequence, inputCount).Select(sequence => CreateRow(0, sequence)).ToArray();
                var beforeBufferVersion = buffer.Version; var beforeSourceVersion = series.SourceVersion;
                var before = Counters([series]); var started = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
                mutate(input); var snapshot = series.CreateSnapshot();
                var work = Work(started, allocated, before, Counters([series]));
                if (operation == "clear") oracle.Clear();
                else
                {
                    if (removeCount > 0) oracle.RemoveRange(index, removeCount);
                    if (input.Length > 0) oracle.InsertRange(index, input);
                    if (oracle.Count > 12) { var excess = oracle.Count - 12; oracle.RemoveRange(0, excess); evicted += excess; }
                }
                var expectedRows = oracle.Select((row, ordinal) => Raw(ordinal, row.Id, row, row.X, row.Y, true)).ToArray();
                var actualRows = snapshot.Points.Select((point, ordinal) => Raw(point.SourceIndex, point.StableId?.ToString(), point.Item as Row,
                    point.Point.X, point.Point.Y, ordinal < oracle.Count && ReferenceEquals(point.Item, oracle[ordinal]))).ToArray();
                var identity = Compare(snapshot, oracle);
                var versions = buffer.Version == beforeBufferVersion + 1 && series.SourceVersion > beforeSourceVersion && snapshot.Version == series.SourceVersion;
                steps.Add(new(operation, index, removeCount, input.Select(row => row.Sequence).ToArray(), beforeBufferVersion, buffer.Version,
                    beforeSourceVersion, series.SourceVersion, buffer.EvictedCount, evicted, work, expectedRows, actualRows, identity, versions));
                retainedSnapshots.Add((snapshot, oracle.ToArray()));
            }
            Step("append", 0, 0, 0, 8, input => buffer.AppendRange(input));
            Step("append", 8, 0, 8, 8, input => buffer.AppendRange(input));
            Step("insert", 2, 0, 100, 2, input => buffer.InsertRange(2, input));
            Step("replace", 4, 3, 200, 2, input => buffer.ReplaceRange(4, 3, input));
            Step("remove", 1, 4, 0, 0, _ => buffer.RemoveRange(1, 4));
            Step("append", 7, 0, 300, 20, input => buffer.AppendRange(input));
            Step("clear", 0, 0, 0, 0, _ => buffer.Clear());
            var oldSnapshots = retainedSnapshots.Select(value => Compare(value.Snapshot, value.Rows)).ToArray();
            var checks = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["all_mutations_match_full_independent_item_id_index_and_coordinate_oracle"] = steps.All(step => step.Identity.Passed),
                ["all_mutations_advance_source_and_buffer_versions"] = steps.All(step => step.VersionsValid),
                ["all_capacity_evictions_match_list_oracle"] = steps.All(step => step.ActualEvictedCount == step.ExpectedEvictedCount),
                ["all_retained_old_snapshots_survive_later_insert_replace_remove_clear"] = oldSnapshots.All(receipt => receipt.Passed)
            };
            return new("insert-remove-replace-clear-original-identity", 12, steps, oldSnapshots, checks, checks.Values.All(value => value));
        }
        finally { series.Changed -= owner; }
    }

    private static RawRow Raw(int sourceIndex, string? id, Row? item, double x, double y, bool sameReference) =>
        new(sourceIndex, id, item?.Series, item?.Sequence, BitConverter.DoubleToInt64Bits(x).ToString("X16"), BitConverter.DoubleToInt64Bits(y).ToString("X16"), sameReference);

    private static SummaryReceipt ReadExtrema(ChartDataSnapshot snapshot)
    {
        var type = Product.GetType("NT.AvaloniaControl.Charts.ChartDataExtrema")!;
        var value = type.GetMethod("For", Any)!.Invoke(null, [snapshot])!;
        return new(Convert.ToInt32(type.GetProperty("SourceCount", Any)!.GetValue(value)),
            Convert.ToInt32(type.GetProperty("Count", Any)!.GetValue(value)),
            (ChartSeriesBounds?)type.GetProperty("Bounds", Any)!.GetValue(value),
            (bool)type.GetProperty("AscendingX", Any)!.GetValue(value)!, (bool)type.GetProperty("DescendingX", Any)!.GetValue(value)!,
            (bool)type.GetProperty("AscendingY", Any)!.GetValue(value)!, (bool)type.GetProperty("DescendingY", Any)!.GetValue(value)!);
    }

    private static SummaryReceipt ExpectedSummary(IReadOnlyList<Row> rows)
    {
        var minX = double.PositiveInfinity; var maxX = double.NegativeInfinity; var minY = double.PositiveInfinity; var maxY = double.NegativeInfinity;
        var ascendingX = true; var descendingX = true; var ascendingY = true; var descendingY = true;
        var previousX = 0d; var previousY = 0d;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index]; var x = row.ExpectedX; var y = row.ExpectedY;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            if (index > 0) { ascendingX &= x >= previousX; descendingX &= x <= previousX; ascendingY &= y >= previousY; descendingY &= y <= previousY; }
            previousX = x; previousY = y;
        }
        return new(rows.Count, rows.Count, rows.Count == 0 ? null : new(minX, maxX, minY, maxY), ascendingX, descendingX, ascendingY, descendingY);
    }

    private static void Drain(Task task)
    {
        var timeout = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            if (timeout.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Controlled streaming fixture did not reach its expected boundary.");
            Thread.Yield();
        }
        task.GetAwaiter().GetResult();
    }

    private static Row CreateRow(int series, long sequence)
    {
        var phase = sequence % 1024; var triangle = phase < 512 ? phase : 1024 - phase;
        var noise = ((sequence * 1_103_515_245 + 12_345) & 511) - 255;
        return new(series, sequence, $"series-{series}/sample-{sequence}", sequence / 2500d, (triangle * 32 + noise) / 256d + series * 3);
    }

    private static IdentityReceipt Compare(ChartDataSnapshot snapshot, IReadOnlyList<Row> expected)
    {
        using var expectedHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var actualHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var valid = snapshot.Points.Count == expected.Count; var mismatch = -1;
        for (var ordinal = 0; ordinal < Math.Min(expected.Count, snapshot.Points.Count); ordinal++)
        {
            var row = expected[ordinal]; var point = snapshot.Points[ordinal];
            var same = ReferenceEquals(point.Item, row) && Equals(point.StableId, row.Id) && point.SourceIndex == ordinal &&
                point.Point == new ChartPoint(row.X, row.Y) && !point.IsEmpty;
            if (!same && mismatch < 0) mismatch = ordinal;
            valid &= same;
            Hash(expectedHash, ordinal, row.Id, row.X, row.Y);
            Hash(actualHash, point.SourceIndex, point.StableId?.ToString() ?? "<null>", point.Point.X, point.Point.Y);
        }
        var expectedSha = Convert.ToHexString(expectedHash.GetHashAndReset()); var actualSha = Convert.ToHexString(actualHash.GetHashAndReset());
        return new(expected.Count, snapshot.Points.Count, snapshot.Version, mismatch, expectedSha, actualSha, valid && expectedSha == actualSha);
    }

    private static void Hash(IncrementalHash hash, int ordinal, string id, double x, double y)
    {
        Span<byte> bytes = stackalloc byte[20];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, ordinal);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[4..], BitConverter.DoubleToInt64Bits(x));
        BinaryPrimitives.WriteInt64LittleEndian(bytes[12..], BitConverter.DoubleToInt64Bits(y));
        hash.AppendData(bytes); hash.AppendData(Encoding.UTF8.GetBytes(id)); hash.AppendData([0]);
    }

    private static Dictionary<string, long> Counters(LineSeries[] series)
    {
        var type = Product.GetType("NT.AvaloniaControl.Charts.ChartDataBufferMetrics")!;
        return new(StringComparer.Ordinal)
        {
            ["bufferCopiedItems"] = Counter(type, "CopiedItemCount", null),
            ["bufferCreatedPages"] = Counter(type, "CreatedPageCount", null),
            ["bufferDirectoryEntries"] = Counter(type, "DirectoryEntryCount", null),
            ["bufferSummaryRows"] = Counter(type, "SummaryRowVisitCount", null),
            ["extremaBuilds"] = Counter(Product.GetType("NT.AvaloniaControl.Charts.ChartDataExtrema")!, "BuildCount", null),
            ["mappedRows"] = Counter(typeof(ChartSeries), "BufferedMappedRowCount", null),
            ["sourceCategoryGetterReads"] = Interlocked.Read(ref Row.CategoryGetterReads),
            ["sourceValueGetterReads"] = Interlocked.Read(ref Row.ValueGetterReads),
            ["sourceIdGetterReads"] = Interlocked.Read(ref Row.IdGetterReads)
        };
    }

    private static long Counter(Type type, string name, object? owner) => Convert.ToInt64(type.GetProperty(name, Any)?.GetValue(owner) ?? type.GetField(name, Any)?.GetValue(owner) ?? throw new MissingMemberException(type.FullName, name));
    private static WorkReceipt Work(long started, long allocated, Dictionary<string, long> before, Dictionary<string, long> after) => new(
        Stopwatch.GetTimestamp() - started, GC.GetAllocatedBytesForCurrentThread() - allocated, before, after,
        before.ToDictionary(pair => pair.Key, pair => after[pair.Key] - pair.Value, StringComparer.Ordinal));
    private static object AssemblyRecord(Assembly assembly) => new { path = assembly.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) };
    private sealed class Row(int series, long sequence, string id, double x, double y)
    {
        internal static long CategoryGetterReads, ValueGetterReads, IdGetterReads;
        public int Series { get; } = series;
        public long Sequence { get; } = sequence;
        public string Id { get { Interlocked.Increment(ref IdGetterReads); return id; } }
        public double X { get { Interlocked.Increment(ref CategoryGetterReads); return x; } }
        public double Y { get { Interlocked.Increment(ref ValueGetterReads); return y; } }
        internal double ExpectedX => x;
        internal double ExpectedY => y;
    }
    private sealed record WorkReceipt(long ElapsedTicks, long AllocatedBytes, Dictionary<string, long> Before, Dictionary<string, long> After, Dictionary<string, long> Delta);
    private sealed record SummaryReceipt(int SourceCount, int ValidCount, ChartSeriesBounds? Bounds, bool AscendingX, bool DescendingX, bool AscendingY, bool DescendingY);
    private sealed record RevisionReceipt(int Revision, int VirtualBatchMilliseconds, int InputSamples, WorkReceipt Work, WorkReceipt UiSourceCommitWork, WorkReceipt MappingWork,
        WorkReceipt ExtremaWork, SummaryReceipt[] ExpectedSummaries, SummaryReceipt[] ActualSummaries, bool SummaryWorkWithinGate,
        bool VersionsValid, bool IncrementalWorkWithinGate, bool RetainedCountCorrect, bool NoSilentLoss);
    private sealed record IdentityReceipt(int ExpectedCount, int ActualCount, long SourceVersion, int FirstMismatchingOrdinal, string ExpectedSha256, string ActualSha256, bool Passed);
    private sealed record ScenarioReceipt(string Id, WorkReceipt ColdSourceMapping, SummaryReceipt[] InitialExpectedSummaries, SummaryReceipt[] InitialActualSummaries,
        List<RevisionReceipt> Revisions, IdentityReceipt[] FinalSnapshots, IdentityReceipt[] InitialSnapshotsAfterUpdates, object SampleLedger, Dictionary<string, bool> Checks, bool Passed);
    private sealed record SchedulerReceipt(string Id, int[] ProducedSamples, int[] ExpectedCommittedSamples, int[] ActualCommittedSamples, List<int[]> ActualCallbackInputs,
        ChartStreamingStatistics PausedStatistics, ChartStreamingStatistics CompletedStatistics, ChartStreamingStatistics BeforeCancelStatistics, ChartStreamingStatistics AfterCancelStatistics, Dictionary<string, bool> Checks, bool Passed);
    private sealed record RawRow(int SourceIndex, string? StableId, int? ItemSeries, long? ItemSequence, string XBits, string YBits, bool OriginalItemReference);
    private sealed record MutationStep(string Operation, int Index, int RemoveCount, long[] InputSequences, long BufferVersionBefore, long BufferVersionAfter,
        long SourceVersionBefore, long SourceVersionAfter, long ActualEvictedCount, long ExpectedEvictedCount, WorkReceipt Work, RawRow[] ExpectedRows, RawRow[] ActualRows, IdentityReceipt Identity, bool VersionsValid);
    private sealed record MutationReceipt(string Id, int Capacity, List<MutationStep> Steps, IdentityReceipt[] OldSnapshotsAfterAllMutations, Dictionary<string, bool> Checks, bool Passed);
    private sealed class StreamingReport
    {
        public string SchemaVersion { get; } = "charts-streaming-v1";
        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FinishedUtc { get; set; }
        public bool Passed { get; set; }
        public string? Error { get; set; }
        public long StopwatchFrequency { get; } = Stopwatch.Frequency;
        public string Runtime { get; } = RuntimeInformation.FrameworkDescription;
        public string Os { get; } = RuntimeInformation.OSDescription;
        public int ExpectedIncrementalCopiedMappedAndGetterRowsPerRevision { get; } = IncrementalWorkLimit;
        public int MaximumTotalSourceGetterReadsPerRevision { get; } = IncrementalWorkLimit * 3;
        public int MaximumSummaryRowVisitsPerRevision { get; } = SummaryVisitLimit;
        public object FixtureDefinition { get; } = new
        {
            generatorVersion = "charts-c405-integer-triangle-v1", seriesCount = SeriesCount, capacityPerSeries = CapacityPerSeries,
            revisions = RevisionCount, samplesPerSeriesPerRevision = SamplesPerSeriesPerRevision, virtualBatchMilliseconds = 50,
            initialFirstSequence = 0, initialLastSequence = CapacityPerSeries - 1,
            finalFirstSequence = RevisionCount * SamplesPerSeriesPerRevision,
            finalLastSequence = CapacityPerSeries + RevisionCount * SamplesPerSeriesPerRevision - 1,
            hashLayout = "For each ordinal: LE Int32 sourceIndex + LE IEEE754 Int64 X + LE IEEE754 Int64 Y + UTF8 stableId + NUL",
            seriesMapping = new { categoryPath = nameof(Row.X), valuePath = nameof(Row.Y), idPath = nameof(Row.Id) },
            attachedChart = new { revisions = ChartRevisionCount, xFormula = "q (integer sample ordinal)", followWindow = 512,
                finalFirstSequence = ChartRevisionCount * SamplesPerSeriesPerRevision,
                finalLastSequence = CapacityPerSeries + ChartRevisionCount * SamplesPerSeriesPerRevision - 1,
                maximumProjectedPoints = ChartProjectionLimit, maximumViewBuilds = ChartViewBuildLimit, samplingMode = "None" }
        };
        public object[] Assemblies { get; } = [AssemblyRecord(typeof(CartesianChart).Assembly), AssemblyRecord(typeof(ChartsTheme).Assembly), AssemblyRecord(typeof(StreamingRegression).Assembly)];
        public List<ScenarioReceipt> Scenarios { get; } = [];
        public SchedulerReceipt? Scheduler { get; set; }
        public MutationReceipt? Mutations { get; set; }
        public ChartReceipt? Chart { get; set; }
        public bool FormalSlaEvaluated { get; } = false;
        public bool PacedThroughputQualified { get; } = false;
        public bool MemoryLongStabilityQualified { get; } = false;
        public bool ActualPresentMeasured { get; } = false;
        public string Scope { get; } = "Preregistered short accelerated diagnostics. Four 250,000-row series accept 120 revisions of 500 total rows through one session. Separate UI source commit, eager mapping and extrema intervals require exactly 500 buffer copies/mapped rows and mapping X/Y getter reads, at most 1500 whole-interval X/Y getters including observer traversal, and at most 2548 summary row visits. Directory/summary merges may scale with page count; buffer-copy counts exclude bounded session arrays. Full original-row and summary oracles run outside intervals, retaining actual/expected hashes, summaries and small mutation rows. A controlled paused fixture records bounded queues, one waiting writer, explicit rejection, order, coalescing and cancellation. A separate attached Headless four-series chart records twelve source/DrawGroup revisions with numeric axes, raw linear unsampled lines and a 512 integer-X trailing window; work gates are 500 copies/maps, 2548 summary visits, 1..8192 projected points and <=8 view builds. Initial lazy selection indexing is separate cold work; hits, surviving/evicted selection and pause/restore ranges retain raw independent expectations. Accepted/committed mean copied input/successful UI source callback; neither is rendered/presented. No compositor/present, actual 10,000 samples/s pacing, formal latency/bytes, 30-minute/8-hour soaking, physical interaction or official-reference qualification is inferred. Complex domains/series and MinMax source rebuilds remain separate scope.";
    }
}
