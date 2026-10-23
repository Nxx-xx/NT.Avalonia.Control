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

// The raw source oracle deliberately does not call the sampling implementation.
internal static class LevelOfDetailRegression
{
    private const int Samples = 10;
    private static readonly Assembly ProductAssembly = typeof(CartesianChart).Assembly;
    private static readonly MethodInfo GetScene = typeof(CartesianChart).GetMethod("GetScene", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo UpdateReadings = typeof(CartesianChart).GetMethod("UpdateReadings", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly string[] CounterNames = ["BuildCount", "QueryCount", "VisitedCount", "OutputCount"];

    public static int Run(string[] args)
    {
        var option = Array.IndexOf(args, "--output");
        var output = option >= 0 && option + 1 < args.Length ? args[option + 1] : "artifacts/charts-level-of-detail/raw.json";
        if (File.Exists(output)) { Console.Error.WriteLine("LOD evidence already exists; use a new output path."); return 1; }
        var report = new LodReport();
        try
        {
            AppBuilder.Configure<BenchmarkApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            foreach (var id in new[] { "ordered-line-1m", "descending-line-100k", "gaps-line-100k", "duplicate-x-100k", "log-inverse-100k", "multi-axis-100k", "four-pixel-100k", "unordered-fallback-100k", "smooth-fallback-10k", "markers-fallback-10k" })
            {
                var result = Exercise(id);
                report.Scenarios.Add(result);
                Program.WriteReport(output, report);
                Console.WriteLine($"{id}: {(result.Passed ? "passed" : "failed")}; {result.ViewportSamples.Min(sample => sample.RenderedCount)}–{result.ViewportSamples.Max(sample => sample.RenderedCount)} rendered original points");
            }
            report.Passed = report.Scenarios.Count == 10 && report.Scenarios.All(scenario => scenario.Passed);
        }
        catch (Exception exception) { report.Error = exception.ToString(); Console.Error.WriteLine(exception); }
        report.FinishedUtc = DateTimeOffset.UtcNow;
        Program.WriteReport(output, report);
        return report.Passed ? 0 : 1;
    }

    private static ScenarioReceipt Exercise(string id)
    {
        var count = id.EndsWith("1m", StringComparison.Ordinal) ? 1_000_000 : id.EndsWith("100k", StringComparison.Ordinal) ? 100_000 : 10_000;
        var supported = !id.Contains("fallback", StringComparison.Ordinal);
        var rows = Enumerable.Range(0, count).Select(index =>
        {
            var x = id == "descending-line-100k" ? count - index : id == "duplicate-x-100k" ? index / 4d - index % 4 / 4d + 1 : id == "unordered-fallback-100k" ? (index ^ 1) + 1d : index + 1d;
            var y = 5 + Math.Sin(index * .00019) + Math.Sin(index * .071) * .35 + (index % 7919 == 0 ? 3 : index % 7907 == 0 ? -3 : 0);
            if (id == "gaps-line-100k" && index % 12000 == 6000) y = double.NaN;
            return new SourceRow(index, x, y);
        }).ToArray();
        var source = new CountedSource(rows);
        var series = new LineSeries { ItemsSource = source, CategoryPath = nameof(SourceRow.X), ValuePath = nameof(SourceRow.Y), IdPath = nameof(SourceRow.Id), MarkerSize = id == "markers-fallback-10k" ? 3 : 0, Interpolation = id == "smooth-fallback-10k" ? ChartLineInterpolation.Smooth : ChartLineInterpolation.Linear, SamplingMode = ChartSamplingMode.MinMax, SamplingPixelWidth = id == "four-pixel-100k" ? 4 : 1 };
        var chart = new CartesianChart { Width = 1200, Height = 650, ShowLegend = false, IsTooltipEnabled = true, SelectionMode = ChartSelectionMode.Multiple, MaxSelectionCount = 100_000, XAxis = id == "log-inverse-100k" ? new LogarithmicAxis { Minimum = 1, Maximum = count + 1, IsInversed = true } : new ChartAxis { Minimum = 0, Maximum = count + 1 }, YAxis = new ChartAxis { Minimum = 0, Maximum = 10, IsInversed = id == "log-inverse-100k" } };
        if (id == "multi-axis-100k") { chart.Axes.Add(new ChartAxis { Key = "right", Position = ChartAxisPosition.Far, Minimum = 0, Maximum = 10, IsInversed = true }); series.YAxisKey = "right"; }
        chart.Series.Add(series);
        ChartViewport View(int step)
        {
            var maximum = id == "duplicate-x-100k" ? count / 4d : count;
            var minimum = maximum * (.02 + step * .003) + 1;
            return new(minimum, maximum * (.92 + step * .003), 0, 10);
        }
        chart.Viewport = View(0);
        var coldBefore = Counters(source); var coldStart = Stopwatch.GetTimestamp(); var coldBytes = GC.GetAllocatedBytesForCurrentThread();
        var window = new Window { Width = 1200, Height = 650, Content = chart };
        try
        {
            window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = chart.PlotArea; Render(chart); Dispatcher.UIThread.RunJobs();
            var coldAfter = Counters(source);
            var cold = new WorkReceipt(Stopwatch.GetTimestamp() - coldStart, GC.GetAllocatedBytesForCurrentThread() - coldBytes, coldBefore, coldAfter, Difference(coldBefore, coldAfter));
            var hierarchy = supported ? Hierarchy(Context(chart)) : null;
            var checks = new Dictionary<string, bool>(StringComparer.Ordinal); var samples = new List<ViewportReceipt>();
            for (var step = 1; step <= Samples; step++)
            {
                var before = Counters(source); var start = Stopwatch.GetTimestamp(); var bytes = GC.GetAllocatedBytesForCurrentThread();
                chart.Viewport = View(step); _ = chart.PlotArea; Render(chart); Dispatcher.UIThread.RunJobs();
                var after = Counters(source); var elapsed = Stopwatch.GetTimestamp() - start; var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes; var delta = Difference(before, after);
                // Capture measured work before the independent full source oracle or identity queries.
                var context = Context(chart); var info = chart.GetSamplingInfo(series);
                var oracle = supported ? QueryOracle(context, rows, info.RenderScale, series.SamplingPixelWidth) : null;
                var emitted = oracle?.ActualOrdinals.ToHashSet() ?? [];
                var original = rows.Where(row => double.IsFinite(row.Y) && context.PlotArea.Contains(context.Projection.Project(new(row.X, row.Y)))).ToArray();
                var omitted = original.Where(row => !emitted.Contains(row.Id)).ToArray();
                var expected = omitted.Length > 0 ? omitted[omitted.Length / 2] : original[original.Length / 2];
                var position = context.Projection.Project(new(expected.X, expected.Y));
                var hit = chart.HitTestPoint(position, .0000001);
                UpdateReadings.Invoke(chart, [position]); chart.SelectedPoint = hit;
                var identity = Identity(hit, expected) && chart.HoveredPoints.Any(point => Identity(point, expected)) && Identity(chart.SelectedPoint, expected);
                var rectangle = new Rect(position.X - .4, context.PlotArea.Top, .8, context.PlotArea.Height).Intersect(context.PlotArea);
                var expectedSet = rows.Where(row => double.IsFinite(row.Y) && rectangle.Contains(context.Projection.Project(new(row.X, row.Y)))).Select(row => row.Id).ToArray();
                chart.ClearSelection(); chart.SelectRectangle(rectangle);
                var selected = chart.SelectedPoints?.Select(point => point.Index).ToArray() ?? [];
                var rectangleIdentity = selected.SequenceEqual(expectedSet) && (chart.SelectedPoints?.All(point => Identity(point, rows[point.Index])) ?? false);
                var receipt = new ViewportReceipt(step, chart.VisibleRange!.Value, [context.PlotArea.Left, context.PlotArea.Top, context.PlotArea.Right, context.PlotArea.Bottom], elapsed, allocated, before, after, delta, info.IsApplied, info.SourceCount, info.VisibleCount, info.RenderedCount, info.FigureCount, info.PixelBucketCount, info.RenderScale, info.PixelWidth, info.FallbackReason, expected.Id, !emitted.Contains(expected.Id), hit is null ? null : new(hit.Index, hit.StableId?.ToString(), (hit.Item as SourceRow)?.Id, (hit.RawPoint ?? hit.Point).X, (hit.RawPoint ?? hit.Point).Y, ReferenceEquals(hit.Item, expected)), identity, [rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom], expectedSet, selected, rectangleIdentity, oracle);
                samples.Add(receipt); chart.ClearSelection();
                checks[$"view-{step}:sourceRetained"] = delta["sourceEnumerations"] == 0 && delta["sourceVisitedItems"] == 0;
                checks[$"view-{step}:indexRetained"] = delta["BuildCount"] == 0;
                checks[$"view-{step}:originalHitTooltipSelection"] = identity;
                checks[$"view-{step}:originalRectangleSet"] = rectangleIdentity;
                checks[$"view-{step}:appliedOrExplained"] = supported ? info.IsApplied && string.IsNullOrEmpty(info.FallbackReason) : !info.IsApplied && !string.IsNullOrEmpty(info.FallbackReason);
                if (supported)
                {
                    checks[$"view-{step}:independentRawMinMaxAndGaps"] = oracle!.Passed;
                    checks[$"view-{step}:reducedDenseGeometry"] = info.RenderedCount < info.VisibleCount / 2;
                    checks[$"view-{step}:outputPixelBudget"] = info.RenderedCount <= 4L * oracle.ActualBucketCount + 2L * oracle.FigureCount;
                    checks[$"view-{step}:boundedHierarchyTraversal"] = delta["VisitedCount"] > 0 && delta["VisitedCount"] < count / 2;
                    checks[$"view-{step}:omittedPointRemainsQueryable"] = !emitted.Contains(expected.Id);
                }
                Dispatcher.UIThread.RunJobs();
            }
            checks["cold:sourceMappedExactlyOnce"] = cold.CounterDelta["sourceEnumerations"] == 1 && cold.CounterDelta["sourceVisitedItems"] == count;
            checks["cold:samplingIndexBuiltIfSupported"] = !supported || cold.CounterDelta["BuildCount"] > 0;
            return new(id, count, supported, hierarchy?.Bytes, hierarchy?.Runs, cold, samples, checks, checks.Values.All(value => value));
        }
        finally { window.Content = null; window.Close(); Dispatcher.UIThread.RunJobs(); }
    }

    private static OracleReceipt QueryOracle(ChartSeriesRenderContext context, SourceRow[] rows, double scale, int pixelWidth)
    {
        var expected = new List<int>(); var starts = new List<bool>(); var visibleCount = 0; var inputCount = 0; var bucketCount = 0; var figures = 0;
        // The raw line uses a bounded numeric adapter where available; preserve
        // that original arithmetic independently of the sampling planner.
        var mapX = OriginalLineMapX(context, out var mappingKind);
        var margin = 5 * (double.IsFinite(context.Series.StrokeThickness) ? Math.Clamp(context.Series.StrokeThickness, 0, 100) : 0) + Math.Max(1, 1 / scale);
        var guarded = context.PlotArea.Inflate(margin);
        var left = guarded.Left; var right = guarded.Right;
        for (var begin = 0; begin < rows.Length;)
        {
            while (begin < rows.Length && !Finite(rows[begin])) begin++;
            if (begin == rows.Length) break;
            var end = begin + 1; while (end < rows.Length && Finite(rows[end])) end++;
            var buckets = new Dictionary<long, List<int>>(); var first = -1; var last = -1;
            for (var index = begin; index < end; index++)
            {
                var x = mapX(rows[index].X);
                if (x < left || x > right) continue;
                if (first < 0) first = index; last = index; visibleCount++;
                var bucket = (long)Math.Floor((x - left) * scale / pixelWidth);
                if (!buckets.TryGetValue(bucket, out var indices)) buckets.Add(bucket, indices = []);
                indices.Add(index);
            }
            var ordinals = new SortedSet<int>();
            if (first >= 0)
            {
                inputCount += last - first + 1; if (first > begin) { ordinals.Add(first - 1); inputCount++; } if (last + 1 < end) { ordinals.Add(last + 1); inputCount++; }
                foreach (var bucket in buckets.Values)
                {
                    ordinals.Add(bucket[0]); ordinals.Add(bucket[^1]);
                    var minimum = bucket[0]; var maximum = bucket[0];
                    foreach (var index in bucket) { if (rows[index].Y < rows[minimum].Y) minimum = index; if (rows[index].Y > rows[maximum].Y) maximum = index; }
                    ordinals.Add(minimum); ordinals.Add(maximum);
                }
            }
            else
            {
                for (var index = begin + 1; index < end; index++)
                {
                    var a = mapX(rows[index - 1].X); var b = mapX(rows[index].X);
                    if (Math.Min(a, b) <= right && Math.Max(a, b) >= left) { ordinals.Add(index - 1); ordinals.Add(index); }
                }
                inputCount += ordinals.Count;
            }
            if (ordinals.Count > 0) { var start = true; foreach (var ordinal in ordinals) { expected.Add(ordinal); starts.Add(start); start = false; } figures++; }
            bucketCount += buckets.Count; begin = end;
        }
        var type = ProductAssembly.GetType("NT.AvaloniaControl.Charts.ChartSamplingIndex")!;
        var flags = BindingFlags.Public | BindingFlags.NonPublic;
        var indexObject = type.GetMethod("For", flags | BindingFlags.Static)!.Invoke(null, [context.Data, CancellationToken.None])!;
        object?[] arguments = [mapX, left, right, scale, pixelWidth, null, CancellationToken.None];
        var queried = (bool)type.GetMethod("TryQuery", flags | BindingFlags.Instance)!.Invoke(indexObject, arguments)!;
        var sampling = arguments[5]!; var sampleType = sampling.GetType();
        int Integer(string name) => (int)sampleType.GetProperty(name, flags | BindingFlags.Instance)!.GetValue(sampling)!;
        var item = sampleType.GetProperty("Item", flags | BindingFlags.Instance)!; var startsMethod = sampleType.GetMethod("StartsFigure", flags | BindingFlags.Instance)!;
        var actual = Enumerable.Range(0, Integer("Count")).Select(i => (int)item.GetValue(sampling, [i])!).ToArray();
        var actualStarts = Enumerable.Range(0, actual.Length).Select(i => (bool)startsMethod.Invoke(sampling, [i])!).ToArray();
        var exact = expected.SequenceEqual(actual) && starts.SequenceEqual(actualStarts);
        var counts = visibleCount == Integer("RawVisibleCount") && inputCount == Integer("InputCount") && bucketCount == Integer("BucketCount") && figures == Integer("FigureCount");
        return new(expected.Count, actual.Length, visibleCount, inputCount, bucketCount, Integer("BucketCount"), figures, Integer("BucketBudget"), [left, right], margin, mappingKind, IndexHash(expected), IndexHash(actual), actual, actualStarts, queried, exact, counts, queried && exact && counts);
    }

    private static Func<double, double> OriginalLineMapX(ChartSeriesRenderContext context, out string mappingKind)
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (var methodName in new[] { "TryGetLinearLineProjection", "TryGetGappedLinearProjection" })
        {
            var method = context.Projection.GetType().GetMethod(methodName, flags);
            if (method is null) continue;
            object?[] arguments = [context.Data, null];
            if (!(bool)method.Invoke(context.Projection, arguments)!) continue;
            var boxedProjection = arguments[1]!;
            var project = boxedProjection.GetType().GetMethod("Project", flags)!.CreateDelegate<Func<ChartPoint, Point>>(boxedProjection);
            mappingKind = methodName;
            return x => project(new(x, 0)).X;
        }
        mappingKind = "GeneralProjectionMapX";
        return context.Projection.MapX;
    }

    private static bool Finite(SourceRow row) => double.IsFinite(row.X) && double.IsFinite(row.Y);
    private static bool Identity(ChartPointHit? hit, SourceRow expected) => hit is not null && hit.Index == expected.Id && ReferenceEquals(hit.Item, expected) && Equals(hit.StableId, expected.Id) && (hit.RawPoint ?? hit.Point) == new ChartPoint(expected.X, expected.Y);
    private static string IndexHash(IEnumerable<int> indices)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); Span<byte> bytes = stackalloc byte[4];
        foreach (var index in indices) { System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes, index); hash.AppendData(bytes); }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static ChartSeriesRenderContext Context(CartesianChart chart)
    {
        var scene = GetScene.Invoke(chart, null)!; var snapshots = (Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!;
        var snapshot = snapshots.GetValue(0)!; return (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context")!.GetValue(snapshot)!;
    }
    private static void Render(CartesianChart chart) { var group = new DrawingGroup(); using (var drawing = group.Open()) chart.Render(drawing); group.Children.Clear(); }
    private static Dictionary<string, long> Counters(CountedSource source)
    {
        var type = ProductAssembly.GetType("NT.AvaloniaControl.Charts.ChartSamplingIndex")!; var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var result = CounterNames.ToDictionary(name => name, name => Convert.ToInt64(type.GetProperty(name, flags)!.GetValue(null)), StringComparer.Ordinal);
        result["sourceEnumerations"] = source.Enumerations; result["sourceVisitedItems"] = source.VisitedItems; return result;
    }
    private static (long Bytes, int Runs)? Hierarchy(ChartSeriesRenderContext context)
    {
        var type = ProductAssembly.GetType("NT.AvaloniaControl.Charts.ChartSamplingIndex")!;
        var flags = BindingFlags.Public | BindingFlags.NonPublic;
        var index = type.GetMethod("For", flags | BindingFlags.Static)!.Invoke(null, [context.Data, CancellationToken.None])!;
        return ((long)type.GetProperty("EstimatedBytes", flags | BindingFlags.Instance)!.GetValue(index)!, (int)type.GetProperty("RunCount", flags | BindingFlags.Instance)!.GetValue(index)!);
    }
    private static Dictionary<string, long> Difference(Dictionary<string, long> before, Dictionary<string, long> after) => before.ToDictionary(item => item.Key, item => after[item.Key] - item.Value, StringComparer.Ordinal);
    private static object AssemblyRecord(Assembly assembly) => new { path = assembly.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) };
    private sealed class CountedSource(SourceRow[] rows) : IEnumerable<SourceRow>
    {
        public long Enumerations { get; private set; } public long VisitedItems { get; private set; }
        public IEnumerator<SourceRow> GetEnumerator() { Enumerations++; foreach (var row in rows) { VisitedItems++; yield return row; } }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed record SourceRow(int Id, double X, double Y);
    private sealed class LodReport
    {
        public string SchemaVersion { get; } = "charts-level-of-detail-v1";
        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow; public DateTimeOffset? FinishedUtc { get; set; }
        public bool Passed { get; set; } public string? Error { get; set; } public int SamplesPerScenario { get; } = Samples;
        public long StopwatchFrequency { get; } = Stopwatch.Frequency; public string Runtime { get; } = RuntimeInformation.FrameworkDescription; public string Os { get; } = RuntimeInformation.OSDescription;
        public object[] Assemblies { get; } = [AssemblyRecord(typeof(CartesianChart).Assembly), AssemblyRecord(typeof(ChartsTheme).Assembly), AssemblyRecord(typeof(LevelOfDetailRegression).Assembly)];
        public List<ScenarioReceipt> Scenarios { get; } = []; public bool PerformanceMeasured { get; } = true; public bool FormalSlaEvaluated { get; } = false; public bool MemoryLongStabilityQualified { get; } = false; public bool ActualPresentMeasured { get; } = false;
        public string Scope { get; } = "Explicit MinMax linear LineSeries, attached Headless. Cold source/index construction separated from ten warm dense viewport changes. Work counters and render timing/allocation captured before full raw per-pixel min/max/gap oracle, emitted ordinal inspection and original point/Tooltip/selection checks. Conservative unsupported cases must explain fallback. Pixel envelope mechanism is checked independently; no bitmap equivalence, physical input, actual backend/present, FPS, formal latency or long-run memory qualification.";
    }
    private sealed record WorkReceipt(long ElapsedTicks, long AllocatedBytes, Dictionary<string, long> CounterBefore, Dictionary<string, long> CounterAfter, Dictionary<string, long> CounterDelta);
    private sealed record OracleReceipt(int ExpectedCount, int ActualCount, int RawVisibleCount, int InputCount, int ExpectedBucketCount, int ActualBucketCount, int FigureCount, int BucketBudget, double[] GuardedXBounds, double GuardMargin, string OriginalMappingKind, string ExpectedOrdinalsSha256, string ActualOrdinalsSha256, int[] ActualOrdinals, bool[] ActualStartsFigure, bool QuerySucceeded, bool ExactOrdinalsAndGaps, bool CountsMatch, bool Passed);
    private sealed record HitReceipt(int Index, string? StableId, int? ItemId, double RawX, double RawY, bool OriginalItemReference);
    private sealed record ViewportReceipt(int Sequence, ChartViewport EffectiveViewport, double[] PlotBounds, long ElapsedTicks, long AllocatedBytes, Dictionary<string, long> CounterBefore, Dictionary<string, long> CounterAfter, Dictionary<string, long> CounterDelta, bool IsApplied, int SourceCount, int VisibleCount, int RenderedCount, int FigureCount, int PixelBucketCount, double RenderScale, int PixelWidth, string? FallbackReason, int ProbedOriginalOrdinal, bool ProbedPointOmittedFromDrawing, HitReceipt? PointHit, bool HitTooltipSelectionIdentity, double[] RectangleBounds, int[] ExpectedRectangleIndices, int[] ActualRectangleIndices, bool RectangleOriginalIdentitySet, OracleReceipt? RawPixelOracle);
    private sealed record ScenarioReceipt(string Id, int SourceCount, bool SamplingSupported, long? HierarchyOwnedArrayBytes, int? HierarchyRunCount, WorkReceipt Cold, List<ViewportReceipt> ViewportSamples, Dictionary<string, bool> Checks, bool Passed);
}
