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

// Counter gates describe these fixtures, not arbitrary overlap or a time SLA.
// Expected results come from exhaustive original rows, never indexed hit output.
internal static class EfficientHitRegression
{
    private const int Samples = 10;
    private const int CandidateLimit = 4096;
    private const int NodeLimit = 8192;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Assembly Product = typeof(CartesianChart).Assembly;
    private static readonly MethodInfo GetScene = typeof(CartesianChart).GetMethod("GetScene", Instance)!;
    private static readonly MethodInfo UpdateReadings = typeof(CartesianChart).GetMethod("UpdateReadings", Instance)!;
    private static readonly string[] ScenarioIds = ["ordered-line-1m", "same-x-vertical-100k", "coincident-100k", "unordered-scatter-100k", "bubble-paint-order-100k", "shared-x-duplicates-100k", "shared-y-duplicates-100k", "segment-ordered-100k", "segment-same-x-100k", "segment-unordered-gaps-20k", "multi-axis-minmax-100k", "grouped-same-x-bars-100k"];
    private static readonly string[] SharedCounters = [
        "ChartVisibilityIndex.BuildCount", "ChartVisibilityIndex.PointBuildCount", "ChartVisibilityIndex.HitQueryCount", "ChartVisibilityIndex.HitNodeVisitCount", "ChartVisibilityIndex.HitCandidateCount",
        "ChartSegmentIndex.BuildCount", "ChartSegmentIndex.QueryCount", "ChartSegmentIndex.NodeVisitCount", "ChartSegmentIndex.CandidateCount",
        "ChartComparisonLayout.BuildCount", "ChartComparisonLayout.QueryCount", "ChartComparisonLayout.VisitedCount", "ChartComparisonLayout.EvaluatedCount",
        "ChartPointSceneCache.PointOrderBuildCount", "ChartPointSceneCache.HitIndexBuildCount", "ChartPointSceneCache.HitNodeVisitCount", "ChartPointSceneCache.HitCandidateCount",
        "ChartDataIndex.BuildCount", "ChartReadingIndex.BuildCount", "ChartSamplingIndex.BuildCount", "ChartSamplingPlanner.PlanBuildCount"];
    private static readonly string[] LocalCounters = ["_preparedDataBuildCount", "_viewBuildCount", "_segmentCacheBuildCount", "_segmentProjectedVertexCount"];

    public static int Run(string[] args)
    {
        var option = Array.IndexOf(args, "--output");
        var output = option >= 0 && option + 1 < args.Length ? args[option + 1] : "artifacts/charts-efficient-hit/raw.json";
        if (File.Exists(output)) { Console.Error.WriteLine("Efficient-hit evidence already exists; use a new output path."); return 1; }
        var report = new HitReport();
        try
        {
            AppBuilder.Configure<BenchmarkApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            foreach (var id in ScenarioIds)
            {
                var scenario = Exercise(id); report.Scenarios.Add(scenario); Program.WriteReport(output, report);
                Console.WriteLine($"{id}: {(scenario.Passed ? "passed" : "failed")}; max {scenario.Queries.Max(query => query.CandidateEvents)} candidate events, {scenario.Queries.Max(query => query.NodeEvents)} nodes");
            }
            report.Passed = report.Scenarios.Count == ScenarioIds.Length && report.Scenarios.All(scenario => scenario.Passed);
        }
        catch (Exception exception) { report.Error = exception.ToString(); Console.Error.WriteLine(exception); }
        report.FinishedUtc = DateTimeOffset.UtcNow; Program.WriteReport(output, report); return report.Passed ? 0 : 1;
    }

    private static ScenarioReceipt Exercise(string id)
    {
        var count = id.EndsWith("1m", StringComparison.Ordinal) ? 1_000_000 : id.EndsWith("20k", StringComparison.Ordinal) ? 20_000 : 100_000;
        var segment = id.StartsWith("segment-", StringComparison.Ordinal);
        var shared = id.StartsWith("shared-", StringComparison.Ordinal);
        var bubble = id == "bubble-paint-order-100k";
        var bars = id == "grouped-same-x-bars-100k";
        var multi = id == "multi-axis-minmax-100k";
        var chart = new CartesianChart { Width = 1200, Height = 650, ShowLegend = false, IsTooltipEnabled = true,
            XAxis = new ChartAxis { Minimum = 0, Maximum = 10 }, YAxis = new ChartAxis { Minimum = 0, Maximum = 10 },
            SelectionMode = ChartSelectionMode.Multiple,
            TooltipMode = shared ? id.Contains("shared-x", StringComparison.Ordinal) ? ChartTooltipMode.SharedX : ChartTooltipMode.SharedY : segment ? ChartTooltipMode.NearestSegment : ChartTooltipMode.NearestPoint };
        if (multi) chart.Axes.Add(new ChartAxis { Key = "right", Position = ChartAxisPosition.Far, Minimum = 100, Maximum = 200, IsInversed = true });
        var sources = new List<CountedSource>(); var rows = new List<SourceRow[]>();
        for (var seriesIndex = 0; seriesIndex < (bars || multi ? 2 : 1); seriesIndex++)
        {
            var group = seriesIndex;
            var values = Enumerable.Range(0, count).Select(index => Row(id, index, count, group)).ToArray();
            var source = new CountedSource(values); sources.Add(source); rows.Add(values);
            ChartSeries series = bubble ? new BubbleSeries { SizePath = nameof(SourceRow.Size), SizeScale = ChartSizeScale.Diameter, SizeMinimum = 0, SizeMaximum = 10, MinimumSize = 4, MaximumSize = 12, DrawOrder = ChartPointDrawOrder.SizeDescending, MarkerStrokeThickness = 0, StrokeThickness = 0 }
                : bars ? new BarSeries { MarkerSize = 0, StrokeThickness = 0 }
                : id == "unordered-scatter-100k" ? new ScatterSeries { MarkerSize = 2, StrokeThickness = 0 }
                : new LineSeries { MarkerSize = 0, StrokeThickness = 1, SamplingMode = multi ? ChartSamplingMode.MinMax : ChartSamplingMode.None };
            series.Title = $"original-{seriesIndex}"; series.ItemsSource = source;
            series.CategoryPath = nameof(SourceRow.X); series.ValuePath = nameof(SourceRow.Y); series.IdPath = nameof(SourceRow.Id);
            if (multi && group == 1) series.YAxisKey = "right";
            chart.Series.Add(series);
        }
        var window = new Window { Width = 1200, Height = 650, Content = chart };
        try
        {
            var coldBefore = Counters(chart, sources); var coldStart = Stopwatch.GetTimestamp(); var coldBytes = GC.GetAllocatedBytesForCurrentThread();
            window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = chart.PlotArea; Render(chart); Dispatcher.UIThread.RunJobs();
            var coldTicks = Stopwatch.GetTimestamp() - coldStart; var coldAllocation = GC.GetAllocatedBytesForCurrentThread() - coldBytes;
            var coldAfter = Counters(chart, sources);
            var cold = new WorkReceipt(coldTicks, coldAllocation, coldBefore, coldAfter, Difference(coldBefore, coldAfter));
            var contexts = Contexts(chart);
            var queries = Enumerable.Range(0, Samples).Select(index => Query(id, index, contexts, rows)).ToArray();
            var firstBefore = Counters(chart, sources); var firstStart = Stopwatch.GetTimestamp(); var firstBytes = GC.GetAllocatedBytesForCurrentThread();
            _ = Probe(chart, queries[0], segment, shared);
            var firstTicks = Stopwatch.GetTimestamp() - firstStart; var firstAllocation = GC.GetAllocatedBytesForCurrentThread() - firstBytes;
            var firstAfter = Counters(chart, sources);
            var first = new WorkReceipt(firstTicks, firstAllocation, firstBefore, firstAfter, Difference(firstBefore, firstAfter));
            // First hover entry can invalidate a view through existing visual state.
            // Settle it and all lazy indices before the measured warm interval.
            foreach (var query in queries) { _ = Probe(chart, query, segment, shared); UpdateReadings.Invoke(chart, [query]); }
            Render(chart); Dispatcher.UIThread.RunJobs(); contexts = Contexts(chart);
            foreach (var query in queries) _ = Probe(chart, query, segment, shared);
            var checks = new Dictionary<string, bool>(StringComparer.Ordinal);
            var receipts = new List<QueryReceipt>();
            var queriedAxes = new HashSet<int>();
            var sampled = multi ? chart.Series.Select(series => chart.GetSamplingInfo(series)).ToArray() : [];
            var sampledOrdinals = multi ? contexts.Select(SampledOrdinals).ToArray() : [];
            for (var sequence = 0; sequence < queries.Length; sequence++)
            {
                var query = queries[sequence];
                var before = Counters(chart, sources); var started = Stopwatch.GetTimestamp(); var bytes = GC.GetAllocatedBytesForCurrentThread();
                var actual = Probe(chart, query, segment, shared);
                var ticks = Stopwatch.GetTimestamp() - started; var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                var after = Counters(chart, sources);
                var delta = Difference(before, after);
                // Everything below is excluded from the measured counter interval.
                var expectedSegment = segment ? SegmentOracle(contexts, rows, query) : null;
                var expected = segment ? expectedSegment is null ? [] : new[] { expectedSegment.Fraction <= .5 ? expectedSegment.Start : expectedSegment.End }
                    : shared ? SharedOracle(contexts, rows, query, id.Contains("shared-x", StringComparison.Ordinal))
                    : PointOracle(contexts, rows, query, bubble, bars) is { } point ? new[] { point } : [];
                var actualHits = actual.Segment is { } resultSegment ? new[] { resultSegment.Original } : actual.Points;
                foreach (var expectedHit in expected) queriedAxes.Add(expectedHit.Series);
                var identity = Identities(actualHits, expected, chart, rows);
                var segmentCorrect = !segment || SegmentMatches(actual.Segment, expectedSegment, chart, rows);
                UpdateReadings.Invoke(chart, [query]);
                var tooltip = Identities(chart.HoveredPoints, expected, chart, rows);
                var selection = false;
                if (actualHits.Length > 0)
                {
                    chart.SelectedPoint = actualHits[0];
                    selection = chart.SelectedPoint is { } selected && Identity(selected, expected[0], chart, rows) && chart.SelectedPoints?.Any(hit => Identity(hit, expected[0], chart, rows)) == true;
                    chart.ClearSelection();
                }
                var omitted = !multi || expected.Length > 0 && !sampledOrdinals[expected[0].Series].Contains(expected[0].Ordinal);
                var candidates = CandidateEvents(delta); var nodes = NodeEvents(delta);
                var work = new WorkReceipt(ticks, allocated, before, after, delta);
                receipts.Add(new(sequence, [query.X, query.Y], work, candidates, nodes,
                    expected.Select(ExpectReceipt).ToArray(), actualHits.Select(hit => HitReceipt(hit, chart, rows)).ToArray(),
                    expectedSegment is null ? null : new(expectedSegment.Start.Ordinal, expectedSegment.End.Ordinal, expectedSegment.Fraction, [expectedSegment.Position.X, expectedSegment.Position.Y], expectedSegment.DistanceSquared),
                    actual.Segment is null ? null : new(actual.Segment.Start.Index, actual.Segment.End.Index, actual.Segment.Fraction, [actual.Segment.Position.X, actual.Segment.Position.Y], actual.Segment.DistanceSquared), identity, segmentCorrect, tooltip, selection, omitted));
                checks[$"query-{sequence}:independentOriginalIdentity"] = identity;
                checks[$"query-{sequence}:originalSegmentEndpointsAndFraction"] = segmentCorrect;
                checks[$"query-{sequence}:tooltipOriginalIdentity"] = tooltip;
                checks[$"query-{sequence}:selectionOriginalIdentity"] = selection;
                checks[$"query-{sequence}:boundedCandidates"] = candidates <= CandidateLimit;
                checks[$"query-{sequence}:boundedNodes"] = nodes <= NodeLimit;
                checks[$"query-{sequence}:indexRetained"] = delta.Where(pair => pair.Key.EndsWith("BuildCount", StringComparison.Ordinal) || LocalCounters.Contains(pair.Key, StringComparer.Ordinal) && pair.Key != "_segmentProjectedVertexCount").All(pair => pair.Value == 0);
                checks[$"query-{sequence}:sourceRetained"] = delta["sourceEnumerations"] == 0 && delta["sourceVisitedItems"] == 0;
                checks[$"query-{sequence}:noFullProjectedSegmentCopy"] = !segment || delta["_segmentProjectedVertexCount"] <= CandidateLimit * 2L;
                checks[$"query-{sequence}:sampledOutOriginalQueryable"] = omitted;
            }
            checks["cold:sourceMappedOnce"] = cold.CounterDelta["sourceEnumerations"] == sources.Count && cold.CounterDelta["sourceVisitedItems"] == (long)count * sources.Count;
            checks["warm:indexMechanismObserved"] = receipts.All(receipt => receipt.CandidateEvents > 0 && receipt.NodeEvents > 0);
            checks["sampled:allViewsApplied"] = !multi || sampled.All(info => info.IsApplied);
            checks["multiAxis:bothOriginalAxesQueried"] = !multi || queriedAxes.SetEquals([0, 1]);
            return new(id, count, sources.Count, segment ? "nearest-segment" : shared ? chart.TooltipMode.ToString() : bubble ? "circle-footprint-paint-order" : bars ? "grouped-bar-body" : "nearest-original-point", rows.Select(SourceHash).ToArray(), contexts.Select(context => new ViewReceipt([context.PlotArea.Left, context.PlotArea.Top, context.PlotArea.Right, context.PlotArea.Bottom], context.GroupIndex, context.GroupCount, context.GroupWidth)).ToArray(), cold, first, sampled, receipts, checks, checks.Values.All(value => value));
        }
        finally { window.Content = null; window.Close(); Dispatcher.UIThread.RunJobs(); }
    }

    private static SourceRow Row(string id, int index, int count, int group)
    {
        var fraction = (index + .5) / count;
        var x = 1 + fraction * 8; var y = 4 + Math.Sin(index * .071) * .2;
        if (id is "same-x-vertical-100k" or "shared-x-duplicates-100k" or "segment-same-x-100k") { x = 5; y = 1 + fraction * 8; }
        if (id == "shared-y-duplicates-100k") y = 5;
        if (id is "coincident-100k" or "bubble-paint-order-100k") { x = 5; y = 5; }
        if (id == "unordered-scatter-100k") { x = 1 + (index * 7919L % count + .5) / count * 8; y = 1 + (index * 15427L % count + .5) / count * 8; }
        if (id == "segment-unordered-gaps-20k") { x = 1 + ((index ^ 1) + .5) / count * 8; y = index % 997 == 500 ? double.NaN : 3 + fraction * 3; }
        if (id == "segment-ordered-100k") y = 2 + fraction * 6;
        if (id == "grouped-same-x-bars-100k") { x = 5; y = 2 + (index % 1024) / 1024d * 5; }
        if (id == "multi-axis-minmax-100k") y = group == 0 ? 2 + fraction * 2 : 160 + fraction * 20;
        return new($"original:{group}:{index}", index, x, y, 1 + index % 9);
    }

    private static Point Query(string id, int sequence, ChartSeriesRenderContext[] contexts, List<SourceRow[]> rows)
    {
        var group = id is "grouped-same-x-bars-100k" or "multi-axis-minmax-100k" ? sequence % 2 : 0;
        var context = contexts[group];
        var sourceRows = rows[group];
        var ordinal = Math.Clamp(sourceRows.Length / 4 + 347 + sequence * 173, 1, sourceRows.Length - 2);
        if (id == "multi-axis-minmax-100k")
        {
            // Both source values are strictly monotonic. An original point
            // strictly inside one pixel bucket cannot be its first/last/min/max.
            // Choose that fixture point independently of the product sampler.
            var origin = context.PlotArea.Left - 6; // stroke=1, Headless scale=1
            long Bucket(int index) => (long)Math.Floor(context.Projection.MapX(sourceRows[index].X) - origin);
            while (Bucket(ordinal - 4) != Bucket(ordinal + 4)) ordinal++;
        }
        var row = sourceRows[ordinal];
        if (id is "coincident-100k" or "bubble-paint-order-100k") return context.Projection.Project(new(5, 5)) + (sequence % 2 == 0 ? default(Vector) : new Vector(3, 4));
        if (id == "grouped-same-x-bars-100k")
        {
            var point = context.Projection.Project(new(5, 1));
            var centerOffset = -context.GroupWidth / 2 + context.GroupWidth / context.GroupCount * (context.GroupIndex + .5);
            return new(point.X + centerOffset, point.Y);
        }
        if (id.StartsWith("segment-", StringComparison.Ordinal))
        {
            while (!Finite(rows[0][ordinal]) || !Finite(rows[0][ordinal + 1])) ordinal++;
            var a = context.Projection.Project(new(rows[0][ordinal].X, rows[0][ordinal].Y));
            var b = context.Projection.Project(new(rows[0][ordinal + 1].X, rows[0][ordinal + 1].Y));
            return new(Math.FusedMultiplyAdd(b.X - a.X, .37, a.X), Math.FusedMultiplyAdd(b.Y - a.Y, .37, a.Y));
        }
        return context.Projection.Project(new(row.X, row.Y));
    }

    private static ProbeResult Probe(CartesianChart chart, Point query, bool segment, bool shared)
    {
        if (segment) return new([], chart.HitTestSegment(query));
        if (shared) { UpdateReadings.Invoke(chart, [query]); return new(chart.HoveredPoints.ToArray(), null); }
        return new(chart.HitTestPoint(query) is { } hit ? [hit] : [], null);
    }

    private static ExpectedPoint? PointOracle(ChartSeriesRenderContext[] contexts, List<SourceRow[]> rows, Point query, bool bubble, bool bars)
    {
        ExpectedPoint? best = null; var bestPaint = -1;
        for (var series = 0; series < contexts.Length; series++)
        {
            var context = contexts[series];
            var order = bubble ? Enumerable.Range(0, rows[series].Length).OrderByDescending(index => 4 + .8 * rows[series][index].Size).ToArray() : Enumerable.Range(0, rows[series].Length).ToArray();
            for (var paint = 0; paint < order.Length; paint++)
            {
                var ordinal = order[paint]; var row = rows[series][ordinal]; if (!Finite(row)) continue;
                var position = context.Projection.Project(new(row.X, row.Y)); if (!Valid(position)) continue;
                double distance;
                if (bars)
                {
                    var width = context.GroupWidth / context.GroupCount; var bodyWidth = Math.Max(.5, width * .86);
                    var baseline = Math.Clamp(context.Projection.YOrigin, context.PlotArea.Top, context.PlotArea.Bottom);
                    var rectangle = new Rect(position.X - context.GroupWidth / 2 + context.GroupIndex * width + (width - bodyWidth) / 2,
                        Math.Min(position.Y, baseline), bodyWidth, Math.Abs(position.Y - baseline)).Intersect(context.PlotArea);
                    if (rectangle.Width <= 0 || rectangle.Height <= 0) continue;
                    var dx = Math.Max(rectangle.Left - query.X, Math.Max(0, query.X - rectangle.Right));
                    var dy = Math.Max(rectangle.Top - query.Y, Math.Max(0, query.Y - rectangle.Bottom));
                    distance = dx * dx + dy * dy;
                    position = new(rectangle.Center.X, Math.Clamp(position.Y, rectangle.Top, rectangle.Bottom));
                }
                else
                {
                    if (!context.PlotArea.Contains(position)) continue;
                    var dx = position.X - query.X; var dy = position.Y - query.Y;
                    distance = dx * dx + dy * dy;
                    if (bubble)
                    {
                        // Diameter mapping and visible circle footprint are
                        // independently computed from configured fixed domains.
                        var outside = Math.Max(0, Math.Sqrt(distance) - (4 + .8 * row.Size) / 2);
                        distance = outside * outside;
                    }
                }
                if (distance > 12 * 12 || best is not null && (distance > best.DistanceSquared || distance == best.DistanceSquared && series == best.Series && paint < bestPaint)) continue;
                best = new(series, ordinal, row.Id, row.X, row.Y, position, distance); bestPaint = paint;
            }
        }
        return best;
    }

    private static ExpectedPoint[] SharedOracle(ChartSeriesRenderContext[] contexts, List<SourceRow[]> rows, Point query, bool xFirst)
    {
        var hits = new List<ExpectedPoint>();
        for (var series = 0; series < contexts.Length; series++)
        {
            ExpectedPoint? best = null; var first = double.PositiveInfinity; var second = double.PositiveInfinity;
            for (var ordinal = 0; ordinal < rows[series].Length; ordinal++)
            {
                var row = rows[series][ordinal]; if (!Finite(row)) continue;
                var position = contexts[series].Projection.Project(new(row.X, row.Y)); if (!Valid(position) || !contexts[series].PlotArea.Contains(position)) continue;
                var dx = Math.Abs(position.X - query.X); var dy = Math.Abs(position.Y - query.Y); var a = xFirst ? dx : dy; var b = xFirst ? dy : dx;
                if (a > first || a == first && b > second) continue;
                first = a; second = b; best = new(series, ordinal, row.Id, row.X, row.Y, position, dx * dx + dy * dy);
            }
            if (best is not null) hits.Add(best);
        }
        return hits.ToArray();
    }

    private static ExpectedSegment? SegmentOracle(ChartSeriesRenderContext[] contexts, List<SourceRow[]> rows, Point query)
    {
        ExpectedSegment? best = null;
        for (var series = 0; series < contexts.Length; series++)
        {
            var projection = contexts[series].Projection; var plot = contexts[series].PlotArea;
            for (var ordinal = 0; ordinal + 1 < rows[series].Length; ordinal++)
            {
                var a = rows[series][ordinal]; var b = rows[series][ordinal + 1]; if (!Finite(a) || !Finite(b)) continue;
                var start = projection.Project(new(a.X, a.Y)); var end = projection.Project(new(b.X, b.Y));
                if (!Valid(start) || !Valid(end) || !Clip(start, end, plot, out var low, out var high)) continue;
                var dx = end.X - start.X; var dy = end.Y - start.Y; var length = dx * dx + dy * dy;
                var fraction = length == 0 ? low : Math.Clamp(((query.X - start.X) * dx + (query.Y - start.Y) * dy) / length, low, high);
                var position = fraction == 0 ? start : fraction == 1 ? end : new Point(Math.FusedMultiplyAdd(dx, fraction, start.X), Math.FusedMultiplyAdd(dy, fraction, start.Y));
                var qx = position.X - query.X; var qy = position.Y - query.Y; var distance = qx * qx + qy * qy;
                if (!double.IsFinite(distance) || distance > 12 * 12 || best is not null && distance >= best.DistanceSquared) continue;
                best = new(new(series, ordinal, a.Id, a.X, a.Y, start, 0), new(series, ordinal + 1, b.Id, b.X, b.Y, end, 0), fraction, position, distance);
            }
        }
        return best;
    }

    private static bool Clip(Point a, Point b, Rect plot, out double low, out double high)
    {
        var first = 0d; var last = 1d;
        bool Axis(double start, double end, double minimum, double maximum)
        {
            var delta = end - start; if (delta == 0) return start >= minimum && start <= maximum;
            var x = (minimum - start) / delta; var y = (maximum - start) / delta; if (x > y) (x, y) = (y, x);
            first = Math.Max(first, x); last = Math.Min(last, y); return first <= last;
        }
        var visible = Axis(a.X, b.X, plot.Left, plot.Right) && Axis(a.Y, b.Y, plot.Top, plot.Bottom);
        low = first; high = last; return visible && low <= 1 && high >= 0;
    }

    private static bool Identities(IReadOnlyList<ChartPointHit> actual, ExpectedPoint[] expected, CartesianChart chart, List<SourceRow[]> rows) =>
        actual.Count == expected.Length && actual.Select((hit, index) => Identity(hit, expected[index], chart, rows)).All(value => value);
    private static bool Identity(ChartPointHit hit, ExpectedPoint expected, CartesianChart chart, List<SourceRow[]> rows) =>
        ReferenceEquals(hit.Series, chart.Series[expected.Series]) && hit.Index == expected.Ordinal && Equals(hit.StableId, expected.Id) &&
        ReferenceEquals(hit.Item, rows[expected.Series][expected.Ordinal]) && (hit.RawPoint ?? hit.Point) == new ChartPoint(expected.X, expected.Y);
    private static bool SegmentMatches(ChartSegmentHit? actual, ExpectedSegment? expected, CartesianChart chart, List<SourceRow[]> rows) =>
        actual is null ? expected is null : expected is not null && Identity(actual.Start, expected.Start, chart, rows) && Identity(actual.End, expected.End, chart, rows) &&
        Math.Abs(actual.Fraction - expected.Fraction) < 1e-10 && PointDistance(actual.Position, expected.Position) < 1e-7 && Math.Abs(actual.DistanceSquared - expected.DistanceSquared) < 1e-7;
    private static double PointDistance(Point first, Point last) => Math.Sqrt((first.X - last.X) * (first.X - last.X) + (first.Y - last.Y) * (first.Y - last.Y));
    private static bool Finite(SourceRow row) => double.IsFinite(row.X) && double.IsFinite(row.Y);
    private static bool Valid(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static ChartSeriesRenderContext[] Contexts(CartesianChart chart)
    {
        var scene = GetScene.Invoke(chart, null)!; var snapshots = (Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!;
        return snapshots.Cast<object>().Select(snapshot => (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context")!.GetValue(snapshot)!).ToArray();
    }
    private static HashSet<int> SampledOrdinals(ChartSeriesRenderContext context)
    {
        var planner = Product.GetType("NT.AvaloniaControl.Charts.ChartSamplingPlanner")!;
        var plan = planner.GetMethod("For", Static)!.Invoke(null, [context, CancellationToken.None])!;
        var sampling = plan.GetType().GetProperty("Sampling", Instance)!.GetValue(plan);
        return sampling is IEnumerable<int> ordinals ? ordinals.ToHashSet() : [];
    }
    private static void Render(CartesianChart chart) { var group = new DrawingGroup(); using (var drawing = group.Open()) chart.Render(drawing); group.Children.Clear(); }
    private static Dictionary<string, long> Counters(CartesianChart chart, List<CountedSource> sources)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var fullName in SharedCounters)
        {
            var separator = fullName.IndexOf('.'); var type = Product.GetType("NT.AvaloniaControl.Charts." + fullName[..separator])!;
            var property = type.GetProperty(fullName[(separator + 1)..], Static) ?? throw new MissingMemberException(type.FullName, fullName);
            result[fullName] = Convert.ToInt64(property.GetValue(null));
        }
        foreach (var name in LocalCounters) result[name] = Convert.ToInt64(typeof(CartesianChart).GetField(name, Instance)!.GetValue(chart));
        result["sourceEnumerations"] = sources.Sum(source => source.Enumerations); result["sourceVisitedItems"] = sources.Sum(source => source.VisitedItems);
        return result;
    }
    private static long CandidateEvents(Dictionary<string, long> delta) => delta["ChartVisibilityIndex.HitCandidateCount"] + delta["ChartSegmentIndex.CandidateCount"] + delta["ChartComparisonLayout.EvaluatedCount"] + delta["ChartPointSceneCache.HitCandidateCount"];
    private static long NodeEvents(Dictionary<string, long> delta) => delta["ChartVisibilityIndex.HitNodeVisitCount"] + delta["ChartSegmentIndex.NodeVisitCount"] + delta["ChartComparisonLayout.VisitedCount"] + delta["ChartPointSceneCache.HitNodeVisitCount"];
    private static Dictionary<string, long> Difference(Dictionary<string, long> before, Dictionary<string, long> after) => before.ToDictionary(pair => pair.Key, pair => after[pair.Key] - pair.Value, StringComparer.Ordinal);
    private static object ExpectReceipt(ExpectedPoint expected) => new { expected.Series, expected.Ordinal, expected.Id, rawPoint = new[] { expected.X, expected.Y }, position = new[] { expected.Position.X, expected.Position.Y }, expected.DistanceSquared };
    private static object HitReceipt(ChartPointHit hit, CartesianChart chart, List<SourceRow[]> rows)
    {
        var series = chart.Series.IndexOf(hit.Series); var raw = hit.RawPoint ?? hit.Point;
        return new { series, index = hit.Index, stableId = hit.StableId?.ToString(), itemId = (hit.Item as SourceRow)?.Id, rawPoint = new[] { raw.X, raw.Y }, position = new[] { hit.Position.X, hit.Position.Y }, originalItemReference = series >= 0 && hit.Index >= 0 && hit.Index < rows[series].Length && ReferenceEquals(hit.Item, rows[series][hit.Index]) };
    }
    private static string SourceHash(SourceRow[] rows)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); Span<byte> bytes = stackalloc byte[24];
        foreach (var row in rows)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverter.DoubleToInt64Bits(row.X));
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes[8..], BitConverter.DoubleToInt64Bits(row.Y));
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes[16..], BitConverter.DoubleToInt64Bits(row.Size)); hash.AppendData(bytes);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static object AssemblyRecord(Assembly assembly) => new { path = assembly.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) };
    private sealed record SourceRow(string Id, int Ordinal, double X, double Y, double Size);
    private sealed class CountedSource(SourceRow[] rows) : IEnumerable<SourceRow>
    {
        public long Enumerations { get; private set; } public long VisitedItems { get; private set; }
        public IEnumerator<SourceRow> GetEnumerator() { Enumerations++; foreach (var row in rows) { VisitedItems++; yield return row; } }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed record ProbeResult(ChartPointHit[] Points, ChartSegmentHit? Segment);
    private sealed record ExpectedPoint(int Series, int Ordinal, string Id, double X, double Y, Point Position, double DistanceSquared);
    private sealed record ExpectedSegment(ExpectedPoint Start, ExpectedPoint End, double Fraction, Point Position, double DistanceSquared);
    private sealed record WorkReceipt(long ElapsedTicks, long AllocatedBytes, Dictionary<string, long> CounterBefore, Dictionary<string, long> CounterAfter, Dictionary<string, long> CounterDelta);
    private sealed record SegmentReceipt(int StartOrdinal, int EndOrdinal, double Fraction, double[] Position, double DistanceSquared);
    private sealed record ViewReceipt(double[] PlotBounds, int GroupIndex, int GroupCount, double GroupWidth);
    private sealed record QueryReceipt(int Sequence, double[] QueryPosition, WorkReceipt Work, long CandidateEvents, long NodeEvents, object[] ExpectedHits, object[] ActualHits, SegmentReceipt? ExpectedSegment, SegmentReceipt? ActualSegment, bool OriginalIdentity, bool OriginalSegmentEndpointsAndFraction, bool TooltipOriginalIdentity, bool SelectionOriginalIdentity, bool SampledOutOriginalQueryable);
    private sealed record ScenarioReceipt(string Id, int SourceCountPerSeries, int SeriesCount, string QueryKind, string[] SourceCoordinateSizeSha256, ViewReceipt[] Views, WorkReceipt ColdSourceAndView, WorkReceipt FirstQuery, ChartSamplingInfo[] Sampling, List<QueryReceipt> Queries, Dictionary<string, bool> Checks, bool Passed);
    private sealed class HitReport
    {
        public string SchemaVersion { get; } = "charts-efficient-hit-v1";
        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow; public DateTimeOffset? FinishedUtc { get; set; }
        public bool Passed { get; set; } public string? Error { get; set; } public int SamplesPerScenario { get; } = Samples;
        public int WarmCandidateEventLimit { get; } = CandidateLimit; public int WarmNodeEventLimit { get; } = NodeLimit;
        public string[] FixtureIds { get; } = ScenarioIds;
        public long StopwatchFrequency { get; } = Stopwatch.Frequency; public string Runtime { get; } = RuntimeInformation.FrameworkDescription; public string Os { get; } = RuntimeInformation.OSDescription;
        public object[] Assemblies { get; } = [AssemblyRecord(typeof(CartesianChart).Assembly), AssemblyRecord(typeof(ChartsTheme).Assembly), AssemblyRecord(typeof(EfficientHitRegression).Assembly)];
        public List<ScenarioReceipt> Scenarios { get; } = [];
        public bool PerformanceMeasured { get; } = true; public bool FormalSlaEvaluated { get; } = false; public bool MemoryLongStabilityQualified { get; } = false; public bool ActualPresentMeasured { get; } = false;
        public string Scope { get; } = "Attached Headless original-data point/shared-axis/linear-segment/bar/circle-footprint queries. Twelve predefined dense and degenerate fixtures; ten fully warmed repeated-position probes each. ItemsSource setup is outside the cold interval and may already enumerate/map; cold Show/layout/render and first lazy query are recorded separately. Every measured query position is primed before measurement, including sparse segment endpoint caches. Multi-axis MinMax probes alternate both source series and require sampled-out original winners on both axes. Measured work ends before exhaustive raw-row projection oracles, tooltip/selection identity checks and sampled-ordinal inspection. Operation-count gates are fixture-specific (4096 candidate/evaluation events and 8192 nodes per query), not arbitrary-data complexity, new-cursor/first-usable latency or a wall-time/byte SLA. No compositor/present, physical input, native accessibility, long-run memory or official-reference equivalence qualification.";
    }
}
