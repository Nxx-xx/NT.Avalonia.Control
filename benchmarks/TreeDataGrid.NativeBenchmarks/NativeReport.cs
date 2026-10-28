using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace TreeDataGridNativeBenchmarks;

internal sealed record NativeOptions(string Output, string Mode, bool Pilot, int Samples, int Frames, int Warmup, int DurationSeconds, double CycleSeconds, bool RefreshSourceDuringSoak, bool BlockingGcReference, bool AutoHeightDuringSoak, string? AutomationClient, bool ShutdownAutomationProviders, string AutomationWorkload, string AutomationClientBackend)
{
    internal static NativeOptions Parse(string[] args)
    {
        string Value(string name, string fallback) { var index = Array.IndexOf(args, name); return index < 0 ? fallback : index + 1 < args.Length ? args[index + 1] : throw new ArgumentException(name + " needs a value."); }
        var pilot = args.Contains("--pilot", StringComparer.Ordinal);
        var result = new NativeOptions(Path.GetFullPath(Value("--output", "artifacts/treedatagrid-native/new-run")), Value("--mode", "performance"), pilot,
            int.Parse(Value("--samples", pilot ? "1" : "5")), int.Parse(Value("--frames", pilot ? "4" : "60")), int.Parse(Value("--warmup", pilot ? "2" : "30")),
            int.Parse(Value("--duration", pilot ? "10" : "600")), double.Parse(Value("--cycle-seconds", "2"), System.Globalization.CultureInfo.InvariantCulture), args.Contains("--refresh-source", StringComparer.Ordinal), args.Contains("--blocking-gc-reference", StringComparer.Ordinal), args.Contains("--auto-height-soak", StringComparer.Ordinal),
            args.Contains("--automation-client", StringComparer.Ordinal) ? Path.GetFullPath(Value("--automation-client", "")) : null,
            args.Contains("--shutdown-automation-providers", StringComparer.Ordinal), Value("--automation-workload", "both"), Value("--automation-client-backend", "explicit-com"));
        if (result.AutomationClientBackend is not ("explicit-com" or "managed-legacy") || result.AutomationClient is null && result.AutomationClientBackend != "explicit-com") throw new ArgumentException("Invalid automation client backend.");
        if (result.AutomationWorkload is not ("query" or "events" or "both") || result.AutomationClient is null && result.AutomationWorkload != "both") throw new ArgumentException("Invalid external automation workload.");
        if (result.ShutdownAutomationProviders && (result.AutomationClient is null || result.Mode != "soak")) throw new ArgumentException("Provider shutdown requires an external-client soak and runs only after the workload.");
        if (result.AutomationClient is { } client && !File.Exists(client)) throw new ArgumentException("Windows UIA client DLL is missing.");
        if (result.Mode is not ("performance" or "soak") || result.Samples < 1 || result.Frames < 1 || result.Warmup < 1 || result.DurationSeconds < 1 || result.CycleSeconds < .1
            || !pilot && (result.Samples < 5 || result.Frames < 30 || result.Warmup < 20 || result.DurationSeconds < 600)) throw new ArgumentException("Invalid native protocol; use --pilot for reduced diagnostics.");
        Directory.CreateDirectory(result.Output);
        if (File.Exists(Path.Combine(result.Output, "initial.json"))) throw new InvalidOperationException("Use a new output directory; native evidence is never overwritten.");
        return result;
    }
}

internal sealed class NativeReport : IDisposable
{
    internal const string Protocol = "treedatagrid-native-render-v2";
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly StreamWriter _events;
    internal NativeOptions Options { get; }
    internal Dictionary<string, object?> Environment { get; } = [];
    internal Dictionary<string, bool> Checks { get; } = [];
    internal List<object> Scenarios { get; } = [];
    internal List<object> Memory { get; } = [];
    internal object? Retention { get; set; }
    internal object? Drift { get; set; }
    internal ResourceReading? BaselineMemory { get; set; }
    internal object? MemoryTelemetry { get; set; }
    internal object? AutomationRetirement { get; set; }
    internal double? MeasuredSoakSeconds { get; set; }
    internal int Cycles { get; set; }
    internal string? Error { get; set; }
    internal DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
    internal int Requests, Completed;
    internal NativeReport(NativeOptions options)
    {
        Options = options;
        _events = new StreamWriter(Path.Combine(options.Output, "events.jsonl"), false) { AutoFlush = true };
        File.WriteAllText(Path.Combine(options.Output, "initial.json"), JsonSerializer.Serialize(new { schemaVersion = 2, protocol = Protocol, options, startedUtc = StartedUtc,
            status = "started", runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, stopwatchFrequency = Stopwatch.Frequency }, Json));
    }
    internal void Event(string kind, object value) => _events.WriteLine(JsonSerializer.Serialize(new { kind, utc = DateTimeOffset.UtcNow, ticks = Stopwatch.GetTimestamp(), value }));
    internal void Save()
    {
        var passed = Error is null && Checks.Count > 0 && Checks.Values.All(value => value) && Requests > 0 && Requests == Completed;
        File.WriteAllText(Path.Combine(Options.Output, "report.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 2, protocol = Protocol, evidenceKind = "NativeMeasured", passed, pilot = Options.Pilot, options = Options,
            startedUtc = StartedUtc, completedUtc = DateTimeOffset.UtcNow, environment = Environment, checks = Checks,
            requests = Requests, completed = Completed, scenarios = Scenarios, memory = Memory, baselineMemory = BaselineMemory, memoryTelemetry = MemoryTelemetry,
            retention = Retention, drift = Drift, measuredSoakSeconds = MeasuredSoakSeconds, cycles = Cycles, error = Error, automationRetirement = AutomationRetirement,
            boundary = "Stopwatch work start immediately before UI mutation through public CompositionBatch.Rendered after a correlated Control.Render. Includes witness/batch/dispatch overhead; serial observations, not physical screen FPS or GPU timings. UI allocation spans only synchronous UI mutation/layout; process allocation also includes concurrent renderer work.",
            nativePresentTicks = (long?)null, gpuTicks = (long?)null, physicalInputLatencyMs = (double?)null,
            memoryBoundary = "Fresh soak process, ten warmup cycles and pre-baseline full GC. No forced GC in timed soak. Raw GetTotalMemory is a current estimate; negative values are preserved but usable managedBytes is null. gcSnapshotKind identifies the required Any or FullBlocking reference; FullBlocking can be older. lastAnyGc is separately preserved with consistency errors and is never replaced. GCMemoryInfo fields describe the selected GC, not current heap/private memory peaks. GC counts bracket sampling. Final drain/12-second settle/full GC is outside the soak. Investigation guards are not a product SLA."
        }, Json));
    }
    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    internal static object Distribution(IEnumerable<double> values)
    {
        var data = values.Order().ToArray();
        double At(double percentile) => data[Math.Clamp((int)Math.Ceiling(data.Length * percentile) - 1, 0, data.Length - 1)];
        return new { count = data.Length, min = data[0], p50 = At(.5), p95 = At(.95), p99 = At(.99), max = data[^1] };
    }
    public void Dispose() => _events.Dispose();
}

internal sealed class RetiredTracker
{
    private sealed class Seen;
    private readonly ConditionalWeakTable<object, Seen> _seen = new();
    private readonly List<(string Kind, WeakReference<object> Reference)> _pending = [];
    private readonly Dictionary<string, long> _tracked = [], _collected = [];
    internal void Track(string kind, object value)
    {
        if (_seen.TryGetValue(value, out _)) return;
        _seen.Add(value, new()); _pending.Add((kind, new(value))); _tracked[kind] = _tracked.GetValueOrDefault(kind) + 1;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal object[] Snapshot()
    {
        _pending.RemoveAll(item =>
        {
            if (item.Reference.TryGetTarget(out _)) return false;
            _collected[item.Kind] = _collected.GetValueOrDefault(item.Kind) + 1; return true;
        });
        return _tracked.Select(pair => (object)new { kind = pair.Key, tracked = pair.Value, collected = _collected.GetValueOrDefault(pair.Key), retained = pair.Value - _collected.GetValueOrDefault(pair.Key) }).ToArray();
    }
    internal long Outstanding => _tracked.Values.Sum() - _collected.Values.Sum();
}
