using System.Diagnostics;
using NT.AvaloniaControl.Charts;

namespace Charts.NativeBenchmarks;

internal sealed record C7Options(string Output, string SourceRoot, string[] Scenarios,
    double WarmupSeconds, double MeasureSeconds, bool FirstUseOnly)
{
    internal static readonly string[] AllTracks = ["S10-L-exact", "S10-S-exact", "S10-B-exact", "S100-L-exact", "S1M-L-exact", "S100-L-minmax", "S1M-L-minmax"];
    internal const int RequestsPerSecond = 60;
    internal const double MaximumDrainSeconds = 15;
    internal static C7Options Parse(string[] args)
    {
        var flags = new HashSet<string>(["--c7-performance", "--c7-first-use-only"], StringComparer.Ordinal);
        var allowed = new HashSet<string>(["--output", "--source-root", "--c7-scenarios", "--c7-warmup-seconds", "--c7-measure-seconds", "--c7-renderer"], StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            if (!seen.Add(args[i])) throw new ArgumentException("Duplicate C7 option.");
            if (flags.Contains(args[i])) continue;
            if (!allowed.Contains(args[i]) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Unknown or missing C7 option.");
            values.Add(args[i], args[++i]);
        }
        string Required(string name) => values.TryGetValue(name, out var value) ? value : throw new ArgumentException(name + " is required.");
        double Duration(string name, double fallback, double minimum) => !values.TryGetValue(name, out var value) ? fallback :
            double.TryParse(value, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var duration) && double.IsFinite(duration) && duration >= minimum && duration <= 300 ? duration : throw new ArgumentException("Invalid C7 duration.");
        var output = Path.GetFullPath(Required("--output"));
        if (File.Exists(output) || Directory.Exists(output)) throw new IOException("C7 output must be a new directory.");
        var source = Path.GetFullPath(Required("--source-root"));
        if (!File.Exists(Path.Combine(source, "Directory.Build.props"))) throw new ArgumentException("Missing source root.");
        if (values.GetValueOrDefault("--c7-renderer", "software") != "software") throw new ArgumentException("This C7 protocol supports an explicit software renderer only.");
        var tracks = values.GetValueOrDefault("--c7-scenarios", "S10-L-exact,S10-S-exact,S10-B-exact,S100-L-minmax").Split(',', StringSplitOptions.TrimEntries);
        if (tracks.Length == 0 || tracks.Distinct().Count() != tracks.Length || tracks.Any(track => !AllTracks.Contains(track, StringComparer.Ordinal))) throw new ArgumentException("Unknown or duplicate C7 track.");
        var first = seen.Contains("--c7-first-use-only");
        if (first && tracks.Length != 1) throw new ArgumentException("First-use-only requires exactly one track per fresh process.");
        return new(output, source, tracks, Duration("--c7-warmup-seconds", 10, 0), Duration("--c7-measure-seconds", 30, .05), first);
    }
}

internal sealed class C7Report
{
    public string Schema { get; } = "charts-c7-performance/v1";
    public int ProcessId { get; } = Environment.ProcessId;
    public DateTime ProcessStartedUtc { get; } = Process.GetCurrentProcess().StartTime.ToUniversalTime();
    public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedUtc { get; set; }
    public long StopwatchFrequency { get; } = Stopwatch.Frequency;
    public required C7Options Options { get; init; }
    public string Status { get; set; } = "starting";
    public bool Passed { get; set; }
    public string? Error { get; set; }
    public object? NativeEnvironment { get; set; }
    public object? NativeTextBeforeUi { get; set; }
    public object? NativeTextAfterRealText { get; set; }
    public object? NativeBackendDiagnostics { get; set; }
    public object? Sources { get; set; }
    public object[] Assemblies { get; set; } = [];
    public List<C7SceneReport> Scenes { get; } = [];
    public Dictionary<string, string> SceneErrors { get; } = [];
    public string ReferenceStatus { get; } = "blocked-reference";
    public object? ReferenceMeasurements { get; } = null;
    public bool PresentMeasured { get; } = false;
    public bool FramePipelineMeasured { get; } = false;
    public bool HardwareGpuMeasured { get; } = false;
    public bool PhysicalInputMeasured { get; } = false;
    public object? Present { get; } = null;
    public object? FramePipeline { get; } = null;
    public string[] MeasurementBoundaries { get; } =
    [
        "controlledRaster measures RenderTargetBitmap.Render(chart) only, excluding bitmap allocation, CopyPixels, encoding, file writes and hashing. It is not native composition or present.",
        "firstUsableToControlledRaster starts before any source assignment to a newly constructed chart, includes attachment/layout/internal preparation, bitmap construction, full controlled raster render and first verified original hit. Dataset generation/object rows are separate.",
        "record measures chart.Render into DrawingGroup; primitiveCount includes axes/grid and all drawing leaves. Record is not raster render or composition.",
        "sourceApply/prepare/record/controlledRaster/hit are non-overlapping stage intervals. requestToControlledRaster includes scheduled queue wait only when explicitly recomputed from scheduled ticks; no nested intervals are summed.",
        "Memory observations are process endpoints and OS lifetime peak working set, not sampled peak private/native/GPU bytes. Reconstructed quality controls and managed pixel buffers can affect later observations.",
        "Fixed requested slots are never silently dropped. Slow work is drained for at most 15 seconds per phase, then the scene fails with pending slots retained in the schedule.",
        "The immutable C0 datasetManifest downsampling field describes its legacy generator defaults, not this track's runtime quality; runtimeQuality and actual samplingInfo declare exact versus MinMax.",
    ];
}

internal sealed class C7SceneReport
{
    public required string Id { get; init; }
    public required object DatasetManifest { get; init; }
    public required object RuntimeQuality { get; init; }
    public required C7Timing DatasetGeneration { get; init; }
    public List<object> DatasetFiles { get; } = [];
    public C7Operation? FirstUse { get; set; }
    public DateTimeOffset? FirstUseCompletedUtc { get; set; }
    public List<C7Operation> TraceSamples { get; } = [];
    public List<C7Query> HitWarmups { get; } = [];
    public List<C7Query> HitQueries { get; } = [];
    public List<C7Schedule> Schedules { get; } = [];
    public List<object> Artifacts { get; } = [];
    public List<object> MemoryEndpoints { get; } = [];
    public Dictionary<string, bool> Checks { get; } = [];
    public Dictionary<string, string> SectionErrors { get; } = [];
    public bool Passed { get; set; }
    public string? Error { get; set; }
}

internal sealed record C7Timing(long StartTicks, long EndTicks, long AllocatedBytes);
internal sealed class C7Operation
{
    public required string Phase { get; init; }
    public int Index { get; init; }
    public required string Action { get; init; }
    public int Revision { get; set; }
    public Dictionary<string, long> Ticks { get; } = [];
    public Dictionary<string, long> Allocations { get; } = [];
    public C7Query? Hit { get; set; }
    public C7Counts? Counts { get; set; }
    public object? MemoryAfter { get; set; }
    public bool Passed { get; set; }
    public string? Error { get; set; }
}

internal sealed record C7Counts(int InputCount, int ValidCount, int VisibleCount, int RenderedCount,
    int PrimitiveCount, ChartSamplingInfo[] SamplingInfo);
internal sealed record C7Query(int Index, string Phase, int Revision, int ProbeSeries, int ProbeIndex,
    double QueryX, double QueryY, int? ActualSeries, int? ActualIndex, int? ActualStableId,
    string? ActualXBits, string? ActualYBits, string? OriginalXBits, string? OriginalYBits,
    bool ReferenceMatches, bool Passed, C7Timing Timing, C7Timing? Maintenance,
    double[] Plot, double[] Range);
internal sealed class C7Schedule
{
    public required string Phase { get; init; }
    public int RequestedHz { get; } = C7Options.RequestsPerSecond;
    public int Planned { get; init; }
    public int Attempted { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Pending => Planned - Attempted;
    public long StartTicks { get; init; }
    public long PlannedEndTicks { get; init; }
    public long? ActualEndTicks { get; set; }
    public double MaximumDrainSeconds { get; } = C7Options.MaximumDrainSeconds;
    public bool Passed { get; set; }
}
