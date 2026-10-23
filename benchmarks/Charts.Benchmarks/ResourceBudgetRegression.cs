using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Themes;

namespace Charts.Benchmarks;

// Gates are fixed before measurement. These are local-cache work/allocation
// diagnostics, not a process/native/GPU byte ceiling or a frame/soak SLA.
internal static class ResourceBudgetRegression
{
    private const int Rows = 20_000, Warmups = 5, Samples = 20, ChurnRevisions = 40;
    private const long SmallBudget = 16_384, LargeBudget = 4 * 1024 * 1024;
    private const long CachedAllocationLimit = 2 * 1024 * 1024;
    private static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Assembly Product = typeof(CartesianChart).Assembly;

    public static int Run(string[] args)
    {
        var index = Array.IndexOf(args, "--output");
        var output = index >= 0 && index + 1 < args.Length ? args[index + 1] : "artifacts/charts-resource-budgets/raw.json";
        if (File.Exists(output)) { Console.Error.WriteLine("Choose a new evidence path."); return 1; }
        var report = new Report(); Program.WriteReport(output, report);
        try
        {
            AppBuilder.Configure<BenchmarkApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            report.MemorySamples.Add(Memory("after-app-setup"));
            report.Drawing = ExerciseDrawing();
            report.MemorySamples.Add(Memory("after-drawing-fixture"));
            report.Compaction = ExerciseCompaction();
            report.MemorySamples.Add(Memory("after-compaction-fixture"));
            report.Interaction = ExerciseInteraction();
            report.MemorySamples.Add(Memory("after-interaction-fixture"));
            report.Passed = report.Drawing.Passed && report.Compaction.Passed && report.Interaction.Passed;
        }
        catch (Exception error) { report.Error = error.ToString(); Console.Error.WriteLine(error); }
        report.FinishedUtc = DateTimeOffset.UtcNow; Program.WriteReport(output, report);
        return report.Passed ? 0 : 1;
    }

    private static DrawingReceipt ExerciseDrawing()
    {
        var source = Enumerable.Range(0, Rows).Select(i => new ChartPoint(i, Value(i))).ToArray();
        var brush = new SolidColorBrush(Colors.RoyalBlue);
        var series = new LineSeries { ItemsSource = source, Stroke = brush, MarkerSize = 0, SamplingMode = ChartSamplingMode.None };
        var chart = new CartesianChart
        {
            Width = 960, Height = 480, ShowLegend = false, ResourceCacheBudgetBytes = LargeBudget,
            XAxis = new NumericAxis { Minimum = 0, Maximum = Rows - 1 },
            YAxis = new NumericAxis { Minimum = -10, Maximum = 110 },
        };
        chart.Series.Add(series);
        using var host = new Host(chart);
        for (var i = 0; i < Warmups; i++) Render(chart);
        var samples = new List<Sample>();
        for (var i = 0; i < Samples; i++) samples.Add(Measure(chart, i, "cached"));
        var original = Fingerprint(Render(chart));
        var queryOrdinal = Rows / 2;
        var beforeHit = Probe(chart, series, source, queryOrdinal);
        chart.TrimResourceCaches(); var trimmed = Info(chart);
        var afterTrim = Fingerprint(Render(chart)); var afterTrimHit = Probe(chart, series, source, queryOrdinal);
        chart.ResourceCacheBudgetBytes = 0;
        for (var i = 0; i < Warmups; i++) Render(chart);
        for (var i = 0; i < Samples; i++) samples.Add(Measure(chart, i, "disabled"));
        var disabled = Fingerprint(Render(chart)); var afterDisabledHit = Probe(chart, series, source, queryOrdinal);
        var disabledInfo = Info(chart);
        // Mutable paint references must survive replay. Compare against the
        // same uncached draw rather than treating frozen brush snapshots as valid.
        brush.Color = Colors.OrangeRed; brush.Opacity = .61;
        var changedUncached = Fingerprint(Render(chart));
        chart.ResourceCacheBudgetBytes = LargeBudget;
        var changedCached = Fingerprint(Render(chart));
        var changedReplayed = Fingerprint(Render(chart));
        var churn = new List<ChurnReceipt>(); chart.ResourceCacheBudgetBytes = SmallBudget;
        for (var i = 0; i < ChurnRevisions; i++)
        {
            chart.Viewport = new ChartViewport(i * 13, i * 13 + 500, -10, 110);
            chart.XAxis.LabelFormat = "'" + new string((char)('a' + i % 20), 64 + i * 3) + "'0.00";
            Render(chart); var info = Info(chart);
            churn.Add(new(i, chart.VisibleRange, info, BudgetValid(info)));
        }
        var retainedBeforeDetach = Info(chart);
        host.Window.Content = null; Dispatcher.UIThread.RunJobs(); var detached = Info(chart);
        host.Window.Content = chart; host.Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Render(chart);
        var reattached = Info(chart);
        var cached = samples.Where(s => s.Mode == "cached").ToArray();
        var uncached = samples.Where(s => s.Mode == "disabled").ToArray();
        var checks = new Dictionary<string, bool>
        {
            ["cached-source-and-geometry-work-zero"] = cached.All(s => s.Delta["preparedData"] == 0 && s.Delta["views"] == 0 && s.Delta["seriesDrawings"] == 0 && s.Delta["axisTexts"] == 0 && s.Delta["axisPens"] == 0 && s.Delta["projectedPoints"] == 0),
            ["disabled-reprojects-original-geometry"] = uncached.All(s => s.Delta["projectedPoints"] > 0),
            ["all-receipt-budget-sums"] = samples.All(s => BudgetValid(s.Info)) && churn.All(s => s.Passed),
            ["cached-allocation-bounded"] = cached.All(s => s.AllocatedBytes <= CachedAllocationLimit),
            ["cached-allocation-at-most-half-disabled"] = cached.Sum(s => s.AllocatedBytes) * 2 <= uncached.Sum(s => s.AllocatedBytes),
            ["trim-releases-local-estimates"] = Number(trimmed, "EstimatedBytes") == 0 && Entries(trimmed) == 0,
            ["disabled-retains-no-local-entries"] = Number(disabledInfo, "EstimatedBytes") == 0 && Entries(disabledInfo) == 0,
            ["retained-trim-disabled-drawing-equivalent"] = original == afterTrim && original == disabled,
            ["live-brush-replay-equivalent"] = changedUncached == changedCached && changedCached == changedReplayed && changedCached != original,
            ["original-hit-through-trim-disabled"] = beforeHit.Passed && afterTrimHit.Passed && afterDisabledHit.Passed,
            ["churn-exercises-admission-rejection"] = Number(retainedBeforeDetach, "EvictionCount") + Number(retainedBeforeDetach, "BypassCount") > 0,
            ["detach-releases-local-estimates"] = Number(detached, "EstimatedBytes") == 0 && Entries(detached) == 0,
            ["reattach-respects-budget"] = BudgetValid(reattached),
        };
        return new(source.Length, samples, churn, original, afterTrim, disabled, changedUncached, changedCached, changedReplayed,
            beforeHit, afterTrimHit, afterDisabledHit, trimmed, disabledInfo, retainedBeforeDetach, detached, reattached, checks, checks.Values.All(v => v));
    }

    private static CompactionReceipt ExerciseCompaction()
    {
        const int count = 2048;
        var source = new ChartDataBuffer<Row>(count);
        var rows = Enumerable.Range(0, count).Select(i => new Row(i)).ToArray();
        foreach (var row in rows) source.AppendRange([row]);
        var series = new LineSeries { ItemsSource = source, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y), IdPath = nameof(Row.Id) };
        EventHandler owner = (_, _) => { }; series.Changed += owner;
        try
        {
            var old = series.CreateSnapshot(); var before = source.StoragePageCount;
            var reads = Row.Reads; var version = source.Version; var sourceVersion = series.SourceVersion;
            var metricsBefore = CompactionMetrics();
            var sw = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
            var compacted = source.Compact(); var current = series.CreateSnapshot();
            var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated; var ticks = Stopwatch.GetTimestamp() - sw;
            var metricsAfter = CompactionMetrics();
            var afterReads = Row.Reads; var after = source.StoragePageCount;
            var actual = Enumerable.Range(0, count).Select(i => Raw(current.Points[i], rows[i])).ToArray();
            var historical = Enumerable.Range(0, count).Select(i => Raw(old.Points[i], rows[i])).ToArray();
            var valid = actual.Select((r, i) => Same(r, i)).All(v => v) && historical.Select((r, i) => Same(r, i)).All(v => v);
            var oldVersion = source.Version; var second = source.Compact();
            var checks = new Dictionary<string, bool>
            {
                ["physical-pages-2048-to-8"] = before == count && after == 8,
                ["one-source-version"] = compacted && source.Version == version + 1 && series.SourceVersion == sourceVersion + 1 && old.Version == sourceVersion && current.Version == series.SourceVersion,
                ["mapping-transfer-no-getter-reads"] = afterReads == reads,
                ["copies-retained-payload-and-mapping-once"] = metricsAfter["bufferCopiedItems"] - metricsBefore["bufferCopiedItems"] == count && metricsAfter["compactedItems"] - metricsBefore["compactedItems"] == count && metricsAfter["mappedCompactionCopies"] - metricsBefore["mappedCompactionCopies"] == count && metricsAfter["mappedRows"] == metricsBefore["mappedRows"],
                ["full-original-and-historical-records"] = valid,
                ["second-compact-no-op"] = !second && source.Version == oldVersion,
                ["explicit-compaction-not-eviction-or-removal"] = source.EvictedCount == 0 && source.RemovedCount == 0,
            };
            return new(count, before, after, compacted, afterReads - reads, reads, afterReads, version, oldVersion, source.Version,
                sourceVersion, series.SourceVersion, old.Version, current.Version, source.EvictedCount, source.RemovedCount,
                metricsBefore, metricsAfter, ticks, bytes, actual, historical, checks, checks.Values.All(v => v));
        }
        finally { series.Changed -= owner; }
    }

    private static InteractionReceipt ExerciseInteraction()
    {
        var source = Enumerable.Range(0, Rows).Select(i => new ChartPoint(i, Value(i))).ToArray();
        var series = new LineSeries { ItemsSource = source, MarkerSize = 0 };
        var chart = new CartesianChart { Width = 960, Height = 480, ShowLegend = false, ResourceCacheBudgetBytes = LargeBudget,
            XAxis = new NumericAxis { Minimum = 0, Maximum = Rows - 1 }, YAxis = new NumericAxis { Minimum = -10, Maximum = 110 } };
        chart.Series.Add(series); using var host = new Host(chart); Render(chart);
        var queries = new List<SegmentReceipt>(); var milestones = new List<Dictionary<string, object?>>();
        var memoBefore = Field(chart, "_segmentMemoEvictionCount");
        for (var i = 0; i < 3000; i++)
        {
            queries.Add(SegmentProbe(chart, i * 3));
            if ((i + 1) % 100 == 0) milestones.Add(Info(chart));
        }
        var memoAfter = Field(chart, "_segmentMemoEvictionCount");
        DrawInteractionLiteral(chart, "resource-budget-tooltip"); var filled = Info(chart);
        chart.TrimResourceCaches(); var trimmed = Info(chart);
        Render(chart); var afterTrim = SegmentProbe(chart, 8997); DrawInteractionLiteral(chart, "resource-budget-tooltip"); var rebuilt = Info(chart);
        chart.ResourceCacheBudgetBytes = SmallBudget;
        var beforeOversized = Info(chart); DrawInteractionLiteral(chart, new string('x', 4096)); var oversized = Info(chart);
        chart.ResourceCacheBudgetBytes = 0; Render(chart); var afterZero = SegmentProbe(chart, 8997);
        DrawInteractionLiteral(chart, "resource-budget-tooltip"); var zero = Info(chart);
        var checks = new Dictionary<string, bool>
        {
            ["all-3000-original-segment-pairs"] = queries.All(q => q.Passed),
            ["memo-cap-at-every-milestone"] = milestones.All(i => Number(i, "SegmentProjectionCount") <= 4096 && BudgetValid(i)),
            ["memo-really-overflows-and-evicts"] = memoAfter > memoBefore && Number(filled, "SegmentProjectionCount") > 0,
            ["all-five-layers-really-populated"] = new[] { "SeriesDrawingCount", "AxisTextCount", "InteractionTextCount", "AxisPenCount", "SegmentGeometryCount" }.All(k => Number(filled, k) > 0),
            ["filled-and-rebuilt-budget-valid"] = BudgetValid(filled) && BudgetValid(rebuilt) && BudgetValid(oversized),
            ["oversized-interaction-text-bypasses"] = Number(oversized, "BypassCount") > Number(beforeOversized, "BypassCount") && Number(oversized, "InteractionTextCount") == Number(beforeOversized, "InteractionTextCount"),
            ["trim-and-zero-clear-five-layers"] = Entries(trimmed) == 0 && Number(trimmed, "EstimatedBytes") == 0 && Entries(zero) == 0 && Number(zero, "EstimatedBytes") == 0,
            ["trim-and-zero-keep-original-segment"] = afterTrim.Passed && afterZero.Passed,
        };
        return new("Private interaction text helper with deterministic literal; actual routed Tooltip is separately verified by native Demo.", queries, milestones,
            memoBefore, memoAfter, filled, trimmed, rebuilt, beforeOversized, oversized, zero, afterTrim, afterZero, checks, checks.Values.All(v => v));
    }
    private static SegmentReceipt SegmentProbe(CartesianChart chart, int start)
    {
        var range = chart.VisibleRange!.Value; var plot = chart.PlotArea; var x = start + .5; var y = (Value(start) + Value(start + 1)) / 2;
        var query = new Point(plot.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
            plot.Bottom - (y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
        var hit = chart.HitTestSegment(query, .0001);
        return new(start, hit?.Start.Index, hit?.End.Index, hit?.Start.Point, hit?.End.Point,
            hit is not null && hit.Start.Index == start && hit.End.Index == start + 1 && hit.Start.Point == new ChartPoint(start, Value(start)) && hit.End.Point == new ChartPoint(start + 1, Value(start + 1)));
    }
    private static void DrawInteractionLiteral(CartesianChart chart, string text)
    {
        var formatted = typeof(CartesianChart).GetMethod("CachedInteractionText", Any)!.Invoke(chart,
            [text, System.Globalization.CultureInfo.InvariantCulture, new Typeface(chart.FontFamily), chart.FontSize, Brushes.Black])!;
        var group = new DrawingGroup(); using var dc = group.Open();
        typeof(CartesianChart).GetMethod("DrawInteractionText", Any)!.Invoke(chart, [dc, formatted, new Point(10, 10)]);
    }

    private static bool Same(RawRow row, int i) => row.SourceIndex == i && row.Id == $"row-{i}" && row.X == i && row.Y == Value(i) && row.SameItem;
    private static RawRow Raw(ChartDataPoint point, Row expected) => new(point.SourceIndex, point.StableId?.ToString(), point.Point.X, point.Point.Y, ReferenceEquals(point.Item, expected));
    private sealed class Row(int i)
    {
        internal static long Reads;
        public string Id { get { Reads++; return $"row-{i}"; } }
        public double X { get { Reads++; return i; } }
        public double Y { get { Reads++; return Value(i); } }
    }
    private static double Value(int i) => (i % 97) * .875 + (i % 13) * .125;

    private static MemoryReceipt Memory(string phase)
    {
        var gc = GC.GetGCMemoryInfo(); using var process = Process.GetCurrentProcess(); process.Refresh();
        long? Read(Func<long> read) { try { return read(); } catch (Exception error) when (error is PlatformNotSupportedException or InvalidOperationException or System.ComponentModel.Win32Exception) { return null; } }
        return new(phase, GC.GetTotalMemory(false), gc.HeapSizeBytes, gc.FragmentedBytes, gc.TotalCommittedBytes,
            GC.GetTotalAllocatedBytes(), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
            Read(() => process.PrivateMemorySize64), Read(() => process.WorkingSet64), Read(() => process.PeakWorkingSet64));
    }

    private static Sample Measure(CartesianChart chart, int ordinal, string mode)
    {
        var before = Counters(chart); var start = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
        Render(chart); var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated; var ticks = Stopwatch.GetTimestamp() - start;
        var after = Counters(chart);
        return new(ordinal, mode, ticks, bytes, before, after, after.ToDictionary(p => p.Key, p => p.Value - before[p.Key]), Info(chart));
    }
    private static Dictionary<string, long> Counters(CartesianChart chart) => new()
    {
        ["preparedData"] = Field(chart, "_preparedDataBuildCount"), ["views"] = Field(chart, "_viewBuildCount"),
        ["seriesDrawings"] = Field(chart, "_seriesDrawingBuildCount"), ["axisTexts"] = Field(chart, "_axisTextBuildCount"),
        ["axisPens"] = Field(chart, "_axisPenBuildCount"),
        ["projectedPoints"] = (long)Product.GetType("NT.AvaloniaControl.Charts.ChartVisibilityQuery")!.GetProperty("ProjectedCount", Any)!.GetValue(null)!,
    };
    private static long Field(object owner, string name) => (long)owner.GetType().GetField(name, Any)!.GetValue(owner)!;
    private static Dictionary<string, long> CompactionMetrics()
    {
        var metrics = Product.GetType("NT.AvaloniaControl.Charts.ChartDataBufferMetrics")!;
        long Read(string name) => (long)metrics.GetField(name, Any)!.GetValue(null)!;
        return new() { ["bufferCopiedItems"] = Read("CopiedItemCount"), ["compactedItems"] = Read("CompactedItemCount"),
            ["mappedCompactionCopies"] = Read("MappedCompactionCopyCount"),
            ["mappedRows"] = (long)typeof(ChartSeries).GetField("BufferedMappedRowCount", Any)!.GetValue(null)! };
    }
    // Preserve all public diagnostic fields in raw JSON without binding the
    // independent reader to reflection ordering or a producer-written summary.
    private static Dictionary<string, object?> Info(CartesianChart chart)
    { var info = chart.GetResourceCacheInfo(); return info.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(info)); }
    private static long Number(Dictionary<string, object?> info, string name) => Convert.ToInt64(info[name], System.Globalization.CultureInfo.InvariantCulture);
    private static long Entries(Dictionary<string, object?> info) => Number(info, "SeriesDrawingCount") + Number(info, "AxisTextCount") + Number(info, "InteractionTextCount") + Number(info, "AxisPenCount") + Number(info, "SegmentGeometryCount");
    private static bool BudgetValid(Dictionary<string, object?> info)
    {
        var sum = new[] { "SeriesDrawingBytes", "AxisTextBytes", "InteractionTextBytes", "AxisPenBytes", "SegmentGeometryBytes" }.Sum(k => Number(info, k));
        return sum == Number(info, "EstimatedBytes") && sum >= 0 && sum <= Number(info, "BudgetBytes") && Entries(info) >= 0 && info["NativeMemoryIncluded"] is false && info["SharedIndexCachesIncluded"] is false;
    }
    private static DrawingGroup Render(CartesianChart chart)
    { _ = chart.PlotArea; var group = new DrawingGroup(); using (var dc = group.Open()) chart.Render(dc); return group; }
    private static HitReceipt Probe(CartesianChart chart, ChartSeries series, ChartPoint[] source, int ordinal)
    {
        var scene = typeof(CartesianChart).GetMethod("GetScene", Any)!.Invoke(chart, null)!;
        var snapshots = (Array)scene.GetType().GetProperty("Snapshots", Any)!.GetValue(scene)!;
        var context = snapshots.Cast<object>().Select(s => (ChartSeriesRenderContext)s.GetType().GetProperty("Context", Any)!.GetValue(s)!).Single(c => ReferenceEquals(c.Series, series));
        var position = context.Projection.Project(source[ordinal]); var hit = chart.HitTestPoint(position, .0001);
        return new(ordinal, hit?.Index, hit?.Point, hit is not null && ReferenceEquals(hit.Series, series) && hit.Index == ordinal && hit.Point == source[ordinal]);
    }

    // Serialize the vector primitive tree to compare actual cached/uncached
    // geometry, paint, clip and glyph content. Native PNG oracles are separate.
    private static string Fingerprint(Drawing drawing)
    {
        var text = new System.Text.StringBuilder();
        void Walk(Drawing current, Matrix transform, double opacity, string clips)
        {
            switch (current)
            {
                case DrawingGroup group:
                    var combined = (group.Transform?.Value ?? Matrix.Identity) * transform;
                    var nextClips = group.ClipGeometry is { } clip ? clips + "/" + clip.Bounds.TransformToAABB(combined) : clips;
                    foreach (var child in group.Children) Walk(child, combined, opacity * group.Opacity, nextClips);
                    break;
                case GeometryDrawing shape:
                    text.Append("S|").Append(transform).Append('|').Append(opacity).Append('|').Append(clips).Append('|').Append(shape.Geometry?.ToString()).Append('|').Append(shape.Geometry?.Bounds).Append('|').Append(Paint(shape.Brush)).Append('|').Append(Paint(shape.Pen?.Brush)).Append('|').Append(shape.Pen?.Thickness).AppendLine(); break;
                case GlyphRunDrawing glyph:
                    text.Append("T|").Append(transform).Append('|').Append(opacity).Append('|').Append(clips).Append('|').Append(Paint(glyph.Foreground)).Append('|').Append(glyph.GlyphRun?.BaselineOrigin).Append('|').Append(glyph.GetBounds()).Append('|');
                    if (glyph.GlyphRun is { } run) foreach (var glyphInfo in run.GlyphInfos) text.Append(glyphInfo.GlyphIndex).Append(':').Append(glyphInfo.GlyphAdvance).Append(',');
                    text.AppendLine(); break;
                default: text.Append(current.GetType().FullName).Append('|').Append(current.GetBounds()).AppendLine(); break;
            }
        }
        Walk(drawing, Matrix.Identity, 1, ""); return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString())));
    }
    private static string Paint(IBrush? brush) => brush is ISolidColorBrush solid ? $"{solid.Color}/{solid.Opacity}/{solid.Transform?.Value}/{solid.TransformOrigin}" : brush?.ToString() ?? "null";
    private sealed class Host : IDisposable
    {
        internal Window Window { get; }
        internal Host(CartesianChart chart) { Window = new Window { Width = chart.Width, Height = chart.Height, Content = chart }; Window.Show(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        public void Dispose() => Window.Close();
    }
    private sealed record Sample(int Ordinal, string Mode, long ElapsedTicks, long AllocatedBytes, Dictionary<string, long> Before, Dictionary<string, long> After, Dictionary<string, long> Delta, Dictionary<string, object?> Info);
    private sealed record ChurnReceipt(int Revision, ChartViewport? Range, Dictionary<string, object?> Info, bool Passed);
    private sealed record HitReceipt(int ExpectedIndex, int? ActualIndex, ChartPoint? ActualPoint, bool Passed);
    private sealed record RawRow(int SourceIndex, string? Id, double X, double Y, bool SameItem);
    private sealed record MemoryReceipt(string Phase, long ManagedLiveEstimateBytes, long LastGcHeapSizeBytes, long LastGcFragmentedBytes,
        long LastGcCommittedBytes, long TotalAllocatedBytes, int Gen0Collections, int Gen1Collections, int Gen2Collections,
        long? ProcessPrivateBytes, long? WorkingSetBytes, long? OsPeakWorkingSetBytes)
    {
        public long? NativeBytes => null;
        public long? GpuBytes => null;
        public string Scope => "Short process snapshots without forced GC; GC heap values describe the latest collection, OS peak is process-lifetime, not this fixture's exact peak. Private-minus-managed is not native memory. Unsupported OS counters are null.";
    }
    private sealed record SegmentReceipt(int ExpectedStart, int? ActualStart, int? ActualEnd, ChartPoint? StartPoint, ChartPoint? EndPoint, bool Passed);
    private sealed record InteractionReceipt(string TextRouteScope, List<SegmentReceipt> Queries, List<Dictionary<string, object?>> Milestones,
        long MemoEvictionsBefore, long MemoEvictionsAfter, Dictionary<string, object?> Filled, Dictionary<string, object?> Trimmed,
        Dictionary<string, object?> Rebuilt, Dictionary<string, object?> BeforeOversized, Dictionary<string, object?> Oversized, Dictionary<string, object?> Zero,
        SegmentReceipt AfterTrim, SegmentReceipt AfterZero, Dictionary<string, bool> Checks, bool Passed);
    private sealed record DrawingReceipt(int SourceCount, List<Sample> Samples, List<ChurnReceipt> Churn, string Original, string AfterTrim, string Disabled,
        string ChangedUncached, string ChangedCached, string ChangedReplayed, HitReceipt BeforeHit, HitReceipt AfterTrimHit, HitReceipt AfterDisabledHit,
        Dictionary<string, object?> Trimmed, Dictionary<string, object?> DisabledInfo, Dictionary<string, object?> BeforeDetach, Dictionary<string, object?> Detached,
        Dictionary<string, object?> Reattached, Dictionary<string, bool> Checks, bool Passed);
    private sealed record CompactionReceipt(int Count, int BeforePages, int AfterPages, bool Compacted, long GetterReads, long GetterReadsBefore, long GetterReadsAfter,
        long VersionBefore, long VersionAfter, long SecondCompactVersionAfter, long SourceVersionBefore, long SourceVersionAfter,
        long HistoricalSnapshotVersion, long CurrentSnapshotVersion, long EvictedCount, long RemovedCount,
        Dictionary<string, long> MetricsBefore, Dictionary<string, long> MetricsAfter, long ElapsedTicks, long AllocatedBytes,
        RawRow[] Actual, RawRow[] Historical, Dictionary<string, bool> Checks, bool Passed);
    private sealed class Report
    {
        public string SchemaVersion { get; } = "charts-resource-budgets-v1";
        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FinishedUtc { get; set; }
        public string Os { get; } = RuntimeInformation.OSDescription;
        public long TimestampFrequency { get; } = Stopwatch.Frequency;
        public int SourceRows { get; } = Rows;
        public int WarmupsPerMode { get; } = Warmups;
        public int SamplesPerMode { get; } = Samples;
        public int ChurnCount { get; } = ChurnRevisions;
        public long SmallBudgetBytes { get; } = SmallBudget;
        public long LargeBudgetBytes { get; } = LargeBudget;
        public long CachedAllocationMaximumBytes { get; } = CachedAllocationLimit;
        public double CachedAllocationMaximumRatio { get; } = .5;
        public bool NativeMemoryIncluded { get; } = false;
        public bool SharedIndexCachesIncluded { get; } = false;
        public string DrawingFingerprintScope { get; } = "Ordered leaves, effective transform/clip/opacity, paints, glyphs, geometry.ToString and bounds; opaque path commands are not independently decoded. Native PNG checks are separate.";
        public bool ProcessPeakOrLongSoakQualified { get; } = false;
        public bool FormalSlaEvaluated { get; } = false;
        public bool CompositorPresentMeasured { get; } = false;
        public object[] Assemblies { get; } = new[] { typeof(CartesianChart).Assembly, typeof(ChartsTheme).Assembly, typeof(ResourceBudgetRegression).Assembly }.Select(a =>
            (object)new { Path = a.Location, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) }).ToArray();
        public List<MemoryReceipt> MemorySamples { get; } = [];
        public DrawingReceipt? Drawing { get; set; }
        public CompactionReceipt? Compaction { get; set; }
        public InteractionReceipt? Interaction { get; set; }
        public bool Passed { get; set; }
        public string? Error { get; set; }
    }
}
