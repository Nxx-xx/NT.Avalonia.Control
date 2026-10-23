using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Themes;

namespace Charts.Benchmarks;

// Fixed mechanism gates, not a UI frame-time, actual-memory or presentation SLA.
internal static class MultiChartLayoutRegression
{
    private const int ChartCount = 4, Rows = 2048, StormRequests = 1000;
    private const long CacheBudget = 128 * 1024, PayloadBudget = 32 * 1024 * 1024;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type Geometry = typeof(CartesianChart).Assembly.GetType("NT.AvaloniaControl.Charts.ChartLineGeometry", true)!;

    public static int Run(string[] args)
    {
        var at = Array.IndexOf(args, "--output");
        var output = at >= 0 && at + 1 < args.Length ? args[at + 1] : "artifacts/charts-multichart-layout/raw.json";
        if (File.Exists(output)) { Console.Error.WriteLine("Choose a new evidence path."); return 1; }
        var report = new Report(); Program.WriteReport(output, report);
        try
        {
            AppBuilder.Configure<BenchmarkApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            Exercise(report);
            report.Passed = report.Checks.Count > 0 && report.Checks.Values.All(value => value);
        }
        catch (Exception exception) { report.Error = exception.ToString(); Console.Error.WriteLine(exception); }
        report.FinishedUtc = DateTimeOffset.UtcNow; Program.WriteReport(output, report);
        return report.Passed ? 0 : 1;
    }

    private static void Exercise(Report report)
    {
        var savedCache = ChartWorkScheduler.ResourceCacheBudgetBytes;
        var savedPayload = ChartWorkScheduler.BackgroundPayloadBudgetBytes;
        var savedJobs = ChartWorkScheduler.MaximumBackgroundJobs;
        var charts = Enumerable.Range(0, ChartCount).Select(CreateChart).ToArray();
        var panel = new StackPanel(); foreach (var chart in charts) panel.Children.Add(chart);
        var window = new Window { Width = 720, Height = 1000, Content = panel };
        try
        {
            ChartWorkScheduler.ResourceCacheBudgetBytes = CacheBudget;
            ChartWorkScheduler.BackgroundPayloadBudgetBytes = PayloadBudget;
            ChartWorkScheduler.MaximumBackgroundJobs = ChartCount;
            window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            foreach (var chart in charts) Render(chart);
            var sync = charts.Select(Paths).ToArray();
            report.SynchronousPaths = sync;
            report.BudgetSamples.Add(ChartWorkScheduler.GetBudgetInfo());

            var before = charts.Select(chart => chart.GetWorkSchedulingInfo()).ToArray();
            for (var i = 0; i < StormRequests; i++) charts[0].RequestRender();
            foreach (var chart in charts.Skip(1)) chart.RequestRender();
            report.SchedulingBefore = before;
            report.SchedulingQueued = charts.Select(chart => chart.GetWorkSchedulingInfo()).ToArray();
            report.BudgetSamples.Add(ChartWorkScheduler.GetBudgetInfo());
            Dispatcher.UIThread.RunJobs();
            report.SchedulingAfter = charts.Select(chart => chart.GetWorkSchedulingInfo()).ToArray();
            report.Checks["four-queued-charts-all-receive-one-dispatch"] = charts.Select((chart, i) =>
                chart.GetWorkSchedulingInfo().RefreshDispatches == before[i].RefreshDispatches + 1).All(value => value);
            report.Checks["one-thousand-storm-requests-coalesce"] =
                report.SchedulingQueued[0].CoalescedRefreshes - before[0].CoalescedRefreshes == StormRequests - 1;

            var uiThread = Environment.CurrentManagedThreadId;
            var ticks = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
            var work = charts.Select(chart => { chart.UseBackgroundLayout = true; return chart.PrepareBackgroundLayoutAsync(); }).ToArray();
            report.CaptureThreadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            report.CaptureElapsedTicks = Stopwatch.GetTimestamp() - ticks;
            report.BudgetSamples.Add(ChartWorkScheduler.GetBudgetInfo());
            Pump(work, report);
            report.BackgroundInfos = charts.Select(chart => chart.GetBackgroundLayoutInfo()).ToArray();
            var background = charts.Select(Paths).ToArray(); report.BackgroundPaths = background;
            report.Checks["every-current-background-job-published"] = work.All(task => task.IsCompletedSuccessfully && task.Result) &&
                report.BackgroundInfos.All(info => !info.IsBusy && info.CompletedCount > 0);
            report.Checks["actual-numeric-worker-is-not-ui-thread"] = report.BackgroundInfos.All(info => info.LastWorkerThreadId != 0 && info.LastWorkerThreadId != uiThread);
            report.Checks["all-original-path-parts-identical-to-synchronous"] = sync.Zip(background).All(pair => pair.First == pair.Second) && sync.All(value => value.PartCount > 0);
            report.Hits = charts.Select(Probe).ToArray();
            report.Checks["original-data-hit-identity"] = report.Hits.All(hit => hit.Passed);

            // Retarget without pumping: all obsolete captures are canceled before any UI publication.
            var beforeRetarget = charts[0].GetBackgroundLayoutInfo();
            var obsolete = new List<Task<bool>>();
            for (var revision = 1; revision <= 3; revision++)
            {
                charts[0].Viewport = new ChartViewport(revision * 100, revision * 100 + 1200, -5, 65);
                obsolete.Add(charts[0].PrepareBackgroundLayoutAsync());
            }
            Pump(obsolete, report);
            report.RetargetInfo = charts[0].GetBackgroundLayoutInfo();
            report.RetargetResults = obsolete.Select(task => task.IsCompletedSuccessfully && task.Result).ToArray();
            var latest = Paths(charts[0]);
            charts[0].UseBackgroundLayout = false; var latestSync = Paths(charts[0]);
            report.Checks["latest-retarget-published-obsolete-completions-rejected"] =
                !report.RetargetResults[0] && !report.RetargetResults[1] && report.RetargetResults[2] &&
                report.RetargetInfo.CanceledCount >= beforeRetarget.CanceledCount + 2 && latest == latestSync;

            panel.IsVisible = false; Dispatcher.UIThread.RunJobs();
            var hiddenDispatches = charts.Select(chart => chart.GetWorkSchedulingInfo().RefreshDispatches).ToArray();
            var hiddenPrepares = charts.Select(chart => Counter(chart, "_preparedDataBuildCount")).ToArray();
            foreach (var chart in charts)
            {
                chart.Series[0].ItemsSource = Source(7);
                for (var i = 0; i < StormRequests; i++) chart.RequestRender();
            }
            Dispatcher.UIThread.RunJobs();
            report.HiddenScheduling = charts.Select(chart => chart.GetWorkSchedulingInfo()).ToArray();
            report.HiddenBudget = ChartWorkScheduler.GetBudgetInfo();
            report.Checks["hidden-ancestor-stops-automatic-refresh-and-data-prepare"] = charts.Select((chart, i) =>
                !chart.GetWorkSchedulingInfo().IsActive && chart.GetWorkSchedulingInfo().RefreshDispatches == hiddenDispatches[i] &&
                Counter(chart, "_preparedDataBuildCount") == hiddenPrepares[i]).All(value => value);
            report.Checks["hidden-ancestor-releases-all-registered-local-cache-estimates"] = report.HiddenBudget.EstimatedResourceCacheBytes == 0 &&
                report.HiddenBudget.ResourceEntryCount == 0 && report.HiddenBudget.PendingRefreshCount == 0;

            panel.IsVisible = true; window.UpdateLayout();
            var shown = charts.Skip(1).Select(chart => chart.PrepareBackgroundLayoutAsync()).ToArray(); Pump(shown, report);
            foreach (var chart in charts) Render(chart);
            report.Checks["show-uses-latest-source"] = charts.All(chart => chart.Series[0].CreateSnapshot().Points[20].Point == SourcePoint(20, 7));
            report.Checks["shown-current-workers-publish"] = shown.All(task => task.IsCompletedSuccessfully && task.Result);
            report.BudgetSamples.Add(ChartWorkScheduler.GetBudgetInfo());

            ChartWorkScheduler.ResourceCacheBudgetBytes = 4096;
            report.ReducedCacheBudget = ChartWorkScheduler.GetBudgetInfo();
            report.Checks["lower-global-cache-budget-enforced-immediately"] = report.ReducedCacheBudget.EstimatedResourceCacheBytes <= 4096;
            ChartWorkScheduler.TrimResourceCaches(); report.TrimmedBudget = ChartWorkScheduler.GetBudgetInfo();
            report.Checks["process-trim-clears-all-chart-local-resource-ledgers"] = report.TrimmedBudget.EstimatedResourceCacheBytes == 0 &&
                charts.All(chart => chart.GetResourceCacheInfo().EstimatedBytes == 0);

            var captured = charts[1].GetBackgroundLayoutInfo().CapturedVertexCount;
            ChartWorkScheduler.BackgroundPayloadBudgetBytes = 0;
            charts[1].Viewport = new ChartViewport(0, 1000, -5, 65);
            var rejected = charts[1].PrepareBackgroundLayoutAsync(); Pump([rejected], report);
            var fallback = charts[1].GetBackgroundLayoutInfo(); report.AdmissionFallback = fallback;
            report.Checks["zero-payload-budget-falls-back-before-numeric-capture"] = !rejected.Result && fallback.CapturedVertexCount == captured && fallback.FallbackCount > 0;
            report.Checks["fallback-still-has-business-path-parts"] = Paths(charts[1]).PartCount > 0;

            // Explicit detach after all jobs are drained must release scheduler registrations and local cache tokens.
            window.Content = null; Dispatcher.UIThread.RunJobs();
            report.DetachedBudget = ChartWorkScheduler.GetBudgetInfo();
            report.Checks["detach-releases-queues-reservations-and-caches"] = report.DetachedBudget.PendingRefreshCount == 0 &&
                report.DetachedBudget.ReservedBackgroundJobs == 0 && report.DetachedBudget.EstimatedBackgroundPayloadBytes == 0 &&
                report.DetachedBudget.EstimatedResourceCacheBytes == 0 && charts.All(chart => !chart.GetWorkSchedulingInfo().IsRegistered);
            report.Checks["all-fixed-budget-observations-valid"] = report.BudgetSamples.All(info =>
                info.EstimatedResourceCacheBytes >= 0 && info.EstimatedResourceCacheBytes <= info.ResourceCacheBudgetBytes &&
                info.EstimatedBackgroundPayloadBytes >= 0 && info.EstimatedBackgroundPayloadBytes <= info.BackgroundPayloadBudgetBytes &&
                info.ReservedBackgroundJobs <= info.MaximumBackgroundJobs && info.RunningBackgroundJobs <= 2);
        }
        finally
        {
            window.Close(); Dispatcher.UIThread.RunJobs();
            ChartWorkScheduler.ResourceCacheBudgetBytes = savedCache;
            ChartWorkScheduler.BackgroundPayloadBudgetBytes = savedPayload;
            ChartWorkScheduler.MaximumBackgroundJobs = savedJobs;
        }
    }

    private static CartesianChart CreateChart(int index)
    {
        var chart = new CartesianChart { Width = 700, Height = 240, ShowLegend = false, ResourceCacheBudgetBytes = 128 * 1024,
            XAxis = new NumericAxis { Minimum = 0, Maximum = Rows - 1 }, YAxis = new NumericAxis { Minimum = -5, Maximum = 65 } };
        if (index % 2 == 0)
            chart.Series.Add(new LineSeries { ItemsSource = Source(0), Interpolation = ChartLineInterpolation.Smooth, MarkerSize = 0, SamplingMode = ChartSamplingMode.None });
        else
        {
            chart.Series.Add(new AreaSeries { ItemsSource = Source(0), Interpolation = ChartLineInterpolation.Smooth, StackMode = ChartStackMode.Stacked, MarkerSize = 0 });
            chart.Series.Add(new AreaSeries { ItemsSource = Source(1), Interpolation = ChartLineInterpolation.Smooth, StackMode = ChartStackMode.Stacked, MarkerSize = 0 });
        }
        return chart;
    }
    private static ChartPoint[] Source(int phase) => Enumerable.Range(0, Rows).Select(i => SourcePoint(i, phase)).ToArray();
    private static ChartPoint SourcePoint(int i, int phase) => new(i, i > 0 && i % 127 == 0 ? double.NaN : 20 + Math.Sin((i + phase * 11) / 17d) * 8 + phase * .125);
    private static long Counter(CartesianChart chart, string field) => (long)typeof(CartesianChart).GetField(field, Any)!.GetValue(chart)!;
    private static void Render(CartesianChart chart) { _ = chart.PlotArea; var drawing = new DrawingGroup(); using var dc = drawing.Open(); chart.Render(dc); }
    private static ChartSeriesRenderContext[] Contexts(CartesianChart chart)
    {
        _ = chart.PlotArea;
        var scene = typeof(CartesianChart).GetMethod("GetScene", Any)!.Invoke(chart, null)!;
        var snapshots = (Array)scene.GetType().GetProperty("Snapshots", Any)!.GetValue(scene)!;
        return snapshots.Cast<object>().Select(snapshot => (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context", Any)!.GetValue(snapshot)!).ToArray();
    }
    private static PathReceipt Paths(CartesianChart chart)
    {
        var text = new StringBuilder(); var count = 0;
        foreach (var context in Contexts(chart))
        {
            text.Append("series;");
            var figures = (System.Collections.IEnumerable)Geometry.GetMethod("Build", Any)!.Invoke(null, [context])!;
            foreach (var figure in figures)
            {
                text.Append("figure;");
                foreach (var side in new[] { "Upper", "Lower" })
                {
                    text.Append(side).Append(';');
                    var parts = (System.Collections.IEnumerable)figure.GetType().GetProperty(side, Any)!.GetValue(figure)!;
                    foreach (var part in parts)
                    {
                        foreach (var field in new[] { "StartOrdinal", "EndOrdinal", "Start", "Control1", "Control2", "End", "IsCubic", "StartFraction", "EndFraction" })
                        {
                            var value = part.GetType().GetProperty(field, Any)!.GetValue(part);
                            if (value is Point point) text.Append(Bits(point.X)).Append(',').Append(Bits(point.Y));
                            else if (value is double scalar) text.Append(Bits(scalar));
                            else text.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                            text.Append('|');
                        }
                        text.Append(';'); count++;
                    }
                }
            }
        }
        return new(count, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))));
    }
    private static string Bits(double value) => BitConverter.DoubleToInt64Bits(value).ToString("X16", CultureInfo.InvariantCulture);
    private static HitReceipt Probe(CartesianChart chart)
    {
        const int ordinal = 200;
        var context = Contexts(chart)[0]; var expected = context.Data.Points[ordinal];
        var hit = chart.HitTestPoint(context.Projection.Project(expected.Point), .0001);
        return new(ordinal, hit?.Index, expected.Point, hit?.Point, hit?.StableId?.ToString(),
            hit is not null && ReferenceEquals(hit.Series, context.Series) && hit.Index == ordinal && hit.Point == expected.Point && Equals(hit.StableId, expected.StableId));
    }
    private static void Pump(IEnumerable<Task<bool>> tasks, Report report)
    {
        var all = Task.WhenAll(tasks); var deadline = Stopwatch.StartNew();
        while (!all.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(30))
        { Dispatcher.UIThread.RunJobs(); report.BudgetSamples.Add(ChartWorkScheduler.GetBudgetInfo()); Thread.Sleep(1); }
        Dispatcher.UIThread.RunJobs();
        if (!all.IsCompleted) throw new TimeoutException("Background mechanism fixture did not settle in 30 seconds.");
        all.GetAwaiter().GetResult();
        // Worker completion continuations retain payload until UI publication/final release has drained.
        while (ChartWorkScheduler.GetBudgetInfo().ReservedBackgroundJobs > 0 && deadline.Elapsed < TimeSpan.FromSeconds(30))
        { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
    }

    private sealed record PathReceipt(int PartCount, string Sha256);
    private sealed record HitReceipt(int ExpectedIndex, int? ActualIndex, ChartPoint ExpectedPoint, ChartPoint? ActualPoint, string? StableId, bool Passed);
    private sealed class Report
    {
        public string SchemaVersion => "charts-multichart-background-layout-v1";
        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FinishedUtc { get; set; }
        public string Os => RuntimeInformation.OSDescription;
        public long TimestampFrequency => Stopwatch.Frequency;
        public int SourceRowsPerSeries => Rows;
        public int Charts => ChartCount;
        public int RepeatedRequests => StormRequests;
        public string Scope => "Headless logical scheduling/admission and exact ordered IEEE754 numeric path parts including source ordinals. Capture allocation/ticks are observations, not limits. No rasterization, present, native/GPU or whole-process/long-soak memory qualification.";
        public bool PerformanceQualified => false;
        public bool TotalMemoryQualified => false;
        public object[] Assemblies => new[] { typeof(CartesianChart).Assembly, typeof(ChartsTheme).Assembly, typeof(MultiChartLayoutRegression).Assembly }
            .Select(assembly => (object)new { Path = assembly.Location, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) }).ToArray();
        public long CaptureThreadAllocatedBytes { get; set; }
        public long CaptureElapsedTicks { get; set; }
        public ChartSchedulingInfo[] SchedulingBefore { get; set; } = [];
        public ChartSchedulingInfo[] SchedulingQueued { get; set; } = [];
        public ChartSchedulingInfo[] SchedulingAfter { get; set; } = [];
        public ChartSchedulingInfo[] HiddenScheduling { get; set; } = [];
        public ChartBackgroundLayoutInfo[] BackgroundInfos { get; set; } = [];
        public PathReceipt[] SynchronousPaths { get; set; } = [];
        public PathReceipt[] BackgroundPaths { get; set; } = [];
        public HitReceipt[] Hits { get; set; } = [];
        public ChartBackgroundLayoutInfo? RetargetInfo { get; set; }
        public bool[] RetargetResults { get; set; } = [];
        public ChartBackgroundLayoutInfo? AdmissionFallback { get; set; }
        public ChartWorkBudgetInfo? HiddenBudget { get; set; }
        public ChartWorkBudgetInfo? ReducedCacheBudget { get; set; }
        public ChartWorkBudgetInfo? TrimmedBudget { get; set; }
        public ChartWorkBudgetInfo? DetachedBudget { get; set; }
        public List<ChartWorkBudgetInfo> BudgetSamples { get; } = [];
        public Dictionary<string, bool> Checks { get; } = [];
        public bool Passed { get; set; }
        public string? Error { get; set; }
    }
}
