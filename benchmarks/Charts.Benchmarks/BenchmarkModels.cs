using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

internal sealed class BenchmarkReport(BenchmarkOptions options)
{
    public string SchemaVersion { get; } = "charts-c0-headless-v1";
    public string RunId { get; } = options.RunId;
    public string Phase { get; } = options.Phase;
    public string Status { get; set; } = "running";
    public bool ProducerPassed { get; set; }
    public string PerformanceQualification { get; } = "pending-C4";
    public string ReferenceStatus { get; } = "blocked-reference";
    public string PresentStatus { get; } = "blocked-present";
    public object FrozenTargets { get; } = Fingerprint(Path.GetFullPath("docs/charts-performance-budgets.json"));
    public double? FramePipelineMs { get; } = null;
    public double? PresentMs { get; } = null;
    public double? SubmitMs { get; } = null;
    public string? Error { get; set; }
    public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedUtc { get; set; }
    public long StopwatchFrequency { get; } = Stopwatch.Frequency;
    public object Environment { get; } = new
    {
        executionOs = RuntimeInformation.OSDescription,
        processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
        framework = RuntimeInformation.FrameworkDescription,
        avaloniaVersion = typeof(Avalonia.Application).Assembly.GetName().Version?.ToString(),
        processorCount = System.Environment.ProcessorCount,
        gcServer = GCSettings.IsServerGC,
        backend = "Avalonia.Headless",
        rendering = "DrawingGroup render-record only; no GPU or compositor present",
        privateMeasurementKind = OperatingSystem.IsWindows() ? "Process.PrivateMemorySize64-private-commit" : "Process.PrivateMemorySize64-platform-definition",
        gpuBytes = (long?)null,
        gpuUnavailableReason = "Headless render-record host does not observe GPU allocations.",
        nativeBytes = (long?)null,
        nativeUnavailableReason = "No native allocator profiler attached; private minus managed is not reported as native.",
        fontQualification = "headless-test-font; real CJK/font hashes pending native harness",
    };
    public object Configuration { get; } = new
    {
        options.Quick, options.Samples, options.Warmups, options.HitQueries, options.SceneTimeoutSeconds,
        scenarios = options.Scenarios.Order(StringComparer.Ordinal).ToArray(),
        widthDip = 1200, heightDip = 650, dpi = 1.0, fontSizeDip = 12, strokeThicknessDip = 2,
        quality = "current-runtime-full-data; dense-line-markers-off; no-downsampling",
        budgetVersion = "charts-engineering-targets-v1",
        targetStatus = "frozen-targets-not-performance-acceptance",
        timeoutKind = "cooperative-between-operations; supervisor enforces process wall timeout",
    };
    public object Sources { get; } = new
    {
        harness = Fingerprint(typeof(BenchmarkReport).Assembly.Location),
        charts = Fingerprint(typeof(CartesianChart).Assembly.Location),
        themes = Fingerprint(typeof(NT.AvaloniaControl.Charts.Themes.ChartsTheme).Assembly.Location),
    };
    public string[] Boundaries { get; } =
    [
        "harnessSourceSnapshot is host ToArray preparation, not an isolated runtime-internal snapshot metric.",
        "sourceEnumeration is a nested instrumented IEnumerable consumption interval; scenePrepare includes it. Do not add nested timings.",
        "scenePrepare is the public PlotArea/VisibleRange getter including current internal snapshot/range/text preparation.",
        "renderRecord is chart.Render into DrawingGroup, not composition/render completion, framePipeline, actual present or FPS.",
        "firstUsableToRecord includes host snapshot, source assignment, scene preparation, record and query correctness; it is not firstUsableToRender/present.",
        "Quick mode is a prototype, not formal 5-process or long-soak acceptance. No absolute C4 budget is evaluated.",
    ];
    public List<ScenarioReport> Scenarios { get; } = [];

    private static object Fingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        return new { path = Path.GetFullPath(path), sha256 = Convert.ToHexString(SHA256.HashData(stream)) };
    }
}

internal sealed class ScenarioReport(DatasetDefinition definition)
{
    public string Id { get; } = definition.Id;
    public string Status { get; set; } = "running";
    public string? Error { get; set; }
    public string Qualification { get; } = definition.RealtimePoc ? "batch-replace-poc-only-not-rate-qualified" : "C0-diagnostic-only";
    public List<DatasetManifest> Datasets { get; } = [];
    public List<DatasetCreation> DatasetPreparation { get; } = [];
    public List<BenchmarkSample> Samples { get; } = [];
    public Dictionary<string, MetricSummary> Summary { get; set; } = [];
}

internal sealed record StageTiming(long StartTicks, long EndTicks, long AllocatedBytes)
{
    public double ElapsedMilliseconds => (EndTicks - StartTicks) * 1000.0 / Stopwatch.Frequency;
}
internal sealed record MetricSummary(int Count, double P50Milliseconds, double P95Milliseconds, double MaximumMilliseconds);
internal sealed record DatasetCreation(int Revision, StageTiming GenerateAndHash);
internal sealed record MemorySample(long WorkingSetBytes, long PrivateBytes, long PeakWorkingSetBytes,
    long ManagedHeapBytes, long TotalAllocatedBytes, long ProcessCpuTicks)
{
    public static MemorySample Capture()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        return new(process.WorkingSet64, process.PrivateMemorySize64, process.PeakWorkingSet64,
            GC.GetTotalMemory(forceFullCollection: false), GC.GetTotalAllocatedBytes(precise: false), process.TotalProcessorTime.Ticks);
    }
}
internal sealed record SourceEnumeration(int SeriesIndex, int EnumerationCount, int CompletedCount,
    long VisitedPointCount, long ElapsedTicks);
internal sealed record QueryReceipt(int QueryIndex, double ScreenX, double ScreenY, int ProbeSeriesIndex,
    int ProbeRawIndex, string ProbeXBits, string ProbeYBits, string OracleMode, int? ExpectedSeriesIndex,
    int? ExpectedRawIndex, int? HitSeriesIndex, int? HitRawIndex, string? ActualXBits, string? ActualYBits,
    string? SourceOracleXBits, string? SourceOracleYBits, bool OracleMatched, StageTiming Timing);
internal sealed record BenchmarkSample(int SampleIndex, bool IsWarmup, int DatasetRevision, string DatasetSha256,
    int InputPointCount, StageTiming HarnessSourceSnapshot, StageTiming SourceApply, StageTiming ScenePrepare,
    StageTiming CachedSceneGetter, StageTiming RenderRecord, StageTiming FirstUsableToRecord,
    SourceEnumeration[] SourceEnumerations, int PrimitiveCount, bool GeometryFinite,
    double[] PlotArea, double[] VisibleRange, MemorySample MemoryBefore, MemorySample MemoryAfter,
    QueryReceipt[] Queries);
