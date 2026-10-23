using System.Collections;
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

// Work-count regression. Timings are recorded without a latency or allocation budget.
internal static class VisibleClippingRegression
{
    private const int Samples = 10;
    private static readonly Assembly ProductAssembly = typeof(CartesianChart).Assembly;
    private static readonly MethodInfo GetScene = typeof(CartesianChart).GetMethod("GetScene", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo UpdateReadings = typeof(CartesianChart).GetMethod("UpdateReadings", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly string[] CounterNames = ["ChartVisibilityIndex.BuildCount", "ChartVisibilityIndex.PointBuildCount", "ChartVisibilityIndex.SegmentBuildCount", "ChartVisibilityIndex.QueryCount", "ChartVisibilityIndex.VisitedCount", "ChartVisibilityIndex.CandidateCount", "ChartVisibilityQuery.PointQueryCount", "ChartVisibilityQuery.SegmentQueryCount", "ChartVisibilityQuery.FallbackCount", "ChartVisibilityQuery.ProjectedCount"];

    public static int Run(string[] args)
    {
        var option = Array.IndexOf(args, "--output");
        var output = option >= 0 && option + 1 < args.Length ? args[option + 1] : "artifacts/charts-visible-clipping/raw.json";
        if (File.Exists(output)) { Console.Error.WriteLine("Visibility evidence already exists; use a new output path."); return 1; }
        var report = new VisibilityReport();
        try
        {
            AppBuilder.Configure<BenchmarkApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            foreach (var id in new[] { "ordered-line-1m", "unordered-line-100k", "scatter-2d-100k", "scatter-footprint-10k", "gaps-duplicates-10k", "log-inverse-10k", "smooth-line-10k", "range-area-10k", "multi-axis-10k" })
            {
                var result = Exercise(id);
                report.Scenarios.Add(result);
                Program.WriteReport(output, report);
                Console.WriteLine($"{id}: {(result.Passed ? "passed" : "failed")}; {result.ViewportSamples.Sum(x => x.CounterDelta["ChartVisibilityQuery.ProjectedCount"])} projected points across {Samples} view changes");
            }
            report.Passed = report.Scenarios.Count == 9 && report.Scenarios.All(x => x.Passed);
        }
        catch (Exception exception) { report.Error = exception.ToString(); Console.Error.WriteLine(exception); }
        report.FinishedUtc = DateTimeOffset.UtcNow;
        Program.WriteReport(output, report);
        return report.Passed ? 0 : 1;
    }

    private static ScenarioReceipt Exercise(string id)
    {
        var count = id == "ordered-line-1m" ? 1_000_000 : id.EndsWith("100k", StringComparison.Ordinal) ? 100_000 : 10_000;
        var isScatter = id.StartsWith("scatter-", StringComparison.Ordinal);
        var columns = id == "scatter-2d-100k" ? 1000 : 100;
        var rows = Enumerable.Range(0, count).Select(index =>
        {
            var x = isScatter ? index % columns : id == "unordered-line-100k" ? index ^ 1 : id == "gaps-duplicates-10k" ? (double)(index / 2) : index + 1d;
            var y = isScatter ? index / columns : 5 + Math.Sin(index * .037) + (id == "gaps-duplicates-10k" ? index % 2 * .2 : 0);
            if (id == "gaps-duplicates-10k" && index % 97 == 0) y = double.NaN;
            return new SourceRow(index, x, y, double.IsFinite(y) ? y - 1 : y, double.IsFinite(y) ? y + 1 : y);
        }).ToArray();
        var source = new CountedSource(rows);
        var chart = new CartesianChart { Width = 1200, Height = 650, ShowLegend = false, IsTooltipEnabled = true, SelectionMode = ChartSelectionMode.Multiple };
        chart.XAxis = id == "log-inverse-10k" ? new LogarithmicAxis { Minimum = 1, Maximum = count + 1, IsInversed = true } : new ChartAxis { Minimum = 0, Maximum = isScatter ? columns : count + 1 };
        chart.YAxis = new ChartAxis { Minimum = 0, Maximum = isScatter ? count / columns : 10, IsInversed = id == "log-inverse-10k" };
        ChartSeries series = id switch
        {
            "scatter-2d-100k" => new ScatterSeries { MarkerSize = 4 },
            "scatter-footprint-10k" => new ScatterSeries { MarkerSize = 36 },
            "smooth-line-10k" => new LineSeries { Interpolation = ChartLineInterpolation.Smooth, MarkerSize = 0 },
            "range-area-10k" => new RangeAreaSeries { MarkerSize = 0, LowPath = nameof(SourceRow.Low), HighPath = nameof(SourceRow.High) },
            _ => new LineSeries { MarkerSize = 0 }
        };
        series.CategoryPath = nameof(SourceRow.X); series.ValuePath = nameof(SourceRow.Y); series.IdPath = nameof(SourceRow.Id); series.ItemsSource = source;
        if (id == "multi-axis-10k")
        {
            chart.Axes.Add(new ChartAxis { Key = "right", Position = ChartAxisPosition.Far, Minimum = 0, Maximum = 10, IsInversed = true });
            series.YAxisKey = "right";
        }
        chart.Series.Add(series);
        ChartViewport View(int step)
        {
            var x = isScatter ? columns / 2d - 5 + step * .1 : id == "gaps-duplicates-10k" ? count / 4d - 10 + step : count / 2d - 50 + step * 3;
            var width = isScatter ? 10 : id == "gaps-duplicates-10k" ? 20 : 100;
            return new(x, x + width, isScatter ? count / columns / 2d - 5 : 0, isScatter ? count / columns / 2d + 5 : 10);
        }
        chart.Viewport = View(0);
        var coldBefore = Counters(source);
        var coldStart = Stopwatch.GetTimestamp(); var coldAllocation = GC.GetAllocatedBytesForCurrentThread();
        var window = new Window { Width = 1200, Height = 650, Content = chart };
        try
        {
            window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            _ = chart.PlotArea; Render(chart); Dispatcher.UIThread.RunJobs();
            var coldAfter = Counters(source);
            var cold = new WorkReceipt(Stopwatch.GetTimestamp() - coldStart, GC.GetAllocatedBytesForCurrentThread() - coldAllocation, coldBefore, coldAfter, Difference(coldBefore, coldAfter));
            var samples = new List<ViewportReceipt>();
            var checks = new Dictionary<string, bool>(StringComparer.Ordinal);
            for (var step = 1; step <= Samples; step++)
            {
                var before = Counters(source); var start = Stopwatch.GetTimestamp(); var bytes = GC.GetAllocatedBytesForCurrentThread();
                chart.Viewport = View(step); _ = chart.PlotArea; Render(chart); Dispatcher.UIThread.RunJobs();
                var after = Counters(source); var delta = Difference(before, after);
                var projection = Context(chart).Projection;
                // Independent original-source oracle, deliberately outside measured work.
                var visible = rows.Where(row => double.IsFinite(row.Y) && projection.PlotArea.Contains(projection.Project(new(row.X, id == "range-area-10k" ? row.High : row.Y)))).Select(row => row.Id).ToArray();
                var expectedId = visible.Length > 0 ? visible[visible.Length / 2] : -1;
                var expected = expectedId >= 0 ? rows[expectedId] : null;
                var hit = expected is null ? null : chart.HitTestPoint(projection.Project(new(expected.X, id == "range-area-10k" ? expected.High : expected.Y)), .0001);
                var identity = expected is not null && Identity(hit, expected);
                HitIdentityReceipt[] tooltipHits = []; HitIdentityReceipt? selectedHit = null;
                if (expected is not null)
                {
                    UpdateReadings.Invoke(chart, [projection.Project(new(expected.X, id == "range-area-10k" ? expected.High : expected.Y))]);
                    identity &= chart.HoveredPoints.Any(point => Identity(point, expected));
                    chart.SelectedPoint = hit;
                    identity &= Identity(chart.SelectedPoint, expected);
                    tooltipHits = chart.HoveredPoints.Select(point => HitIdentity(point, rows)).ToArray();
                    selectedHit = chart.SelectedPoint is { } selected ? HitIdentity(selected, rows) : null;
                }
                chart.ClearSelection();
                chart.SelectRectangle(chart.PlotArea);
                var selectedIds = chart.SelectedPoints?.Select(point => point.Index).Order().ToArray() ?? [];
                var rectangleIdentity = selectedIds.SequenceEqual(visible.Order()) && (chart.SelectedPoints?.All(point => Identity(point, rows[point.Index])) ?? visible.Length == 0);
                chart.ClearSelection();
                var queryOracle = QueryOracle(Context(chart), rows, isScatter);
                var receipt = new ViewportReceipt(step, chart.VisibleRange!.Value, Stopwatch.GetTimestamp() - start, GC.GetAllocatedBytesForCurrentThread() - bytes, before, after, delta, visible.Length, expectedId, hit?.Index, hit?.StableId?.ToString(), hit is { } actualHit ? HitIdentity(actualHit, rows) : null, tooltipHits, selectedHit, identity, selectedIds.Length, selectedIds, rectangleIdentity, queryOracle);
                samples.Add(receipt);
                checks[$"view-{step}:sourceRetained"] = delta["sourceEnumerations"] == 0 && delta["sourceVisitedItems"] == 0;
                checks[$"view-{step}:indicesRetained"] = delta["ChartVisibilityIndex.BuildCount"] == 0;
                checks[$"view-{step}:visibilityQueried"] = delta["ChartVisibilityIndex.QueryCount"] > 0;
                checks[$"view-{step}:noFallback"] = delta["ChartVisibilityQuery.FallbackCount"] == 0;
                checks[$"view-{step}:boundedProjection"] = delta["ChartVisibilityQuery.ProjectedCount"] > 0 && delta["ChartVisibilityQuery.ProjectedCount"] < count / 4;
                checks[$"view-{step}:boundedTraversal"] = delta["ChartVisibilityIndex.VisitedCount"] > 0 && delta["ChartVisibilityIndex.VisitedCount"] < count / 4;
                checks[$"view-{step}:originalHitAndTooltipAndSelectionIdentity"] = identity;
                checks[$"view-{step}:rectangleOriginalIdentitySet"] = rectangleIdentity;
                checks[$"view-{step}:independentRawCandidateOracle"] = queryOracle.Passed;
                Dispatcher.UIThread.RunJobs();
            }
            checks["cold:sourceMappedExactlyOnce"] = cold.CounterDelta["sourceEnumerations"] == 1 && cold.CounterDelta["sourceVisitedItems"] == count;
            checks["cold:visibilityIndexBuilt"] = cold.CounterDelta["ChartVisibilityIndex.BuildCount"] > 0;
            return new(id, count, cold, samples, checks, checks.Values.All(value => value));
        }
        finally { window.Content = null; window.Close(); Dispatcher.UIThread.RunJobs(); }
    }

    private static bool Identity(ChartPointHit? hit, SourceRow expected) => hit is not null && hit.Index == expected.Id && ReferenceEquals(hit.Item, expected) && Equals(hit.StableId, expected.Id) && (hit.RawPoint ?? hit.Point) == new ChartPoint(expected.X, hit.Series is RangeAreaSeries ? Math.Max(expected.High, expected.Low) : expected.Y);
    private static HitIdentityReceipt HitIdentity(ChartPointHit hit, SourceRow[] rows)
    {
        var raw = hit.RawPoint ?? hit.Point;
        return new(hit.Index, hit.StableId?.ToString(), (hit.Item as SourceRow)?.Id, raw.X, raw.Y, (uint)hit.Index < (uint)rows.Length && ReferenceEquals(hit.Item, rows[hit.Index]));
    }
    private static QueryOracleReceipt QueryOracle(ChartSeriesRenderContext context, SourceRow[] rows, bool points)
    {
        var a = context.Projection.Unproject(context.PlotArea.TopLeft); var b = context.Projection.Unproject(context.PlotArea.BottomRight);
        var minimumX = Math.Min(a.X, b.X); var maximumX = Math.Max(a.X, b.X);
        var minimumY = Math.Min(a.Y, b.Y); var maximumY = Math.Max(a.Y, b.Y);
        var expected = new SortedSet<int>();
        bool Finite(SourceRow row) => double.IsFinite(row.X) && double.IsFinite(row.Y);
        if (points)
        {
            foreach (var row in rows) if (Finite(row) && row.X >= minimumX && row.X <= maximumX && row.Y >= minimumY && row.Y <= maximumY) expected.Add(row.Id);
        }
        else
        {
            for (var index = 1; index < rows.Length; index++)
            {
                var first = rows[index - 1]; var second = rows[index];
                if (Finite(first) && Finite(second) && Math.Min(first.X, second.X) <= maximumX && Math.Max(first.X, second.X) >= minimumX && Math.Min(first.Y, second.Y) <= maximumY && Math.Max(first.Y, second.Y) >= minimumY)
                { expected.Add(index - 1); expected.Add(index); }
            }
        }
        var indexType = ProductAssembly.GetType("NT.AvaloniaControl.Charts.ChartVisibilityIndex")!;
        var boundsType = ProductAssembly.GetType("NT.AvaloniaControl.Charts.ChartVisibilityBounds")!;
        var bounds = Activator.CreateInstance(boundsType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, [minimumX, maximumX, minimumY, maximumY], null)!;
        var indexObject = indexType.GetMethod("For", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [context.Data]);
        var query = indexType.GetMethod(points ? "QueryPoints" : "QuerySegments", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        var queried = (IEnumerable)query.Invoke(indexObject, points ? [bounds] : [bounds, 0])!;
        var actual = queried.Cast<int>().ToArray();
        // Ordered ranges may conservatively retain boundary neighbors; endpoint omission is never allowed.
        var sortedUnique = actual.SequenceEqual(actual.Distinct().Order());
        var originalOrdinals = actual.All(ordinal => ordinal >= 0 && ordinal < rows.Length);
        var complete = expected.IsSubsetOf(actual);
        var exact = !points || expected.SetEquals(actual);
        return new(points ? "points" : "segments", new(minimumX, maximumX, minimumY, maximumY), expected.Count, actual.Length, IndexHash(expected), IndexHash(actual), sortedUnique, originalOrdinals, complete, exact, sortedUnique && originalOrdinals && complete && exact);
    }
    private static string IndexHash(IEnumerable<int> indices)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> bytes = stackalloc byte[4];
        foreach (var index in indices) { System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes, index); hash.AppendData(bytes); }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static ChartSeriesRenderContext Context(CartesianChart chart)
    {
        var scene = GetScene.Invoke(chart, null)!;
        var snapshots = (Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!;
        var snapshot = snapshots.GetValue(0)!;
        return (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context")!.GetValue(snapshot)!;
    }
    private static void Render(CartesianChart chart)
    {
        var group = new DrawingGroup(); using (var drawing = group.Open()) chart.Render(drawing); group.Children.Clear();
    }
    private static Dictionary<string, long> Counters(CountedSource source)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var name in CounterNames)
        {
            var parts = name.Split('.'); var type = ProductAssembly.GetType("NT.AvaloniaControl.Charts." + parts[0])!;
            var field = type.GetField(parts[1], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var property = type.GetProperty(parts[1], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            result[name] = Convert.ToInt64(field is not null ? field.GetValue(null) : property?.GetValue(null) ?? throw new MissingMemberException(name), System.Globalization.CultureInfo.InvariantCulture);
        }
        result["sourceEnumerations"] = source.Enumerations; result["sourceVisitedItems"] = source.VisitedItems;
        return result;
    }
    private static Dictionary<string, long> Difference(Dictionary<string, long> before, Dictionary<string, long> after) => before.ToDictionary(item => item.Key, item => after[item.Key] - item.Value, StringComparer.Ordinal);
    private static object AssemblyRecord(Assembly assembly) => new { path = assembly.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) };
    private sealed class CountedSource(SourceRow[] rows) : IEnumerable<SourceRow>
    {
        public long Enumerations { get; private set; }
        public long VisitedItems { get; private set; }
        public IEnumerator<SourceRow> GetEnumerator() { Enumerations++; foreach (var row in rows) { VisitedItems++; yield return row; } }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed record SourceRow(int Id, double X, double Y, double Low, double High);
    private sealed class VisibilityReport
    {
        public string SchemaVersion { get; } = "charts-visible-clipping-v1";
        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FinishedUtc { get; set; }
        public bool Passed { get; set; }
        public string? Error { get; set; }
        public int SamplesPerScenario { get; } = Samples;
        public long StopwatchFrequency { get; } = Stopwatch.Frequency;
        public string Runtime { get; } = RuntimeInformation.FrameworkDescription;
        public string Os { get; } = RuntimeInformation.OSDescription;
        public object[] Assemblies { get; } = [AssemblyRecord(typeof(CartesianChart).Assembly), AssemblyRecord(typeof(ChartsTheme).Assembly), AssemblyRecord(typeof(VisibleClippingRegression).Assembly)];
        public List<ScenarioReceipt> Scenarios { get; } = [];
        public bool PerformanceMeasured { get; } = true;
        public bool FormalSlaEvaluated { get; } = false;
        public bool MemoryLongStabilityQualified { get; } = false;
        public bool ActualPresentMeasured { get; } = false;
        public string Scope { get; } = "Attached Headless CartesianChart; cold mapped source/index separately, then ten narrow viewport changes per scenario. Counter deltas cover scene preparation and DrawingGroup Render. Independent full-source screen oracle, Tooltip/selection queries and rectangle selections occur after counter capture and are excluded from work gates. Timing/allocation cover the entire sample including the independent oracle and identity checks and are diagnostic only. No present/FPS/physical input or formal latency/memory SLA.";
    }
    private sealed record WorkReceipt(long ElapsedTicks, long AllocatedBytes, Dictionary<string, long> CounterBefore, Dictionary<string, long> CounterAfter, Dictionary<string, long> CounterDelta);
    private sealed record QueryOracleReceipt(string Kind, ChartSeriesBounds Bounds, int ExpectedCount, int ActualCount, string ExpectedOrdinalsSha256, string ActualOrdinalsSha256, bool SortedUnique, bool OriginalOrdinals, bool Complete, bool ExactPointSet, bool Passed);
    private sealed record HitIdentityReceipt(int Index, string? StableId, int? ItemId, double RawX, double RawY, bool OriginalItemReference);
    private sealed record ViewportReceipt(int Sequence, ChartViewport EffectiveViewport, long ElapsedTicks, long AllocatedBytes, Dictionary<string, long> CounterBefore, Dictionary<string, long> CounterAfter, Dictionary<string, long> CounterDelta, int OracleVisibleCount, int OraclePointId, int? ActualHitIndex, string? ActualStableId, HitIdentityReceipt? PointHit, HitIdentityReceipt[] TooltipHits, HitIdentityReceipt? SelectedHit, bool HitTooltipSelectionIdentity, int RectangleSelectedCount, int[] RectangleSelectedIndices, bool RectangleOriginalIdentitySet, QueryOracleReceipt RawCandidateOracle);
    private sealed record ScenarioReceipt(string Id, int SourceCount, WorkReceipt Cold, List<ViewportReceipt> ViewportSamples, Dictionary<string, bool> Checks, bool Passed);
}
