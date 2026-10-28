using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.Json;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

// An external diagnostic tool. It never loads the product into the collector process.
if (args.Length < 1) throw new ArgumentException("collect HOST OUTPUT MODE SECONDS [host options] | analyze TRACE HOST_EVENTS OUTPUT");
if (args[0] == "collect")
{
    var host = Path.GetFullPath(args[1]); var output = Path.GetFullPath(args[2]); var mode = args[3]; var seconds = int.Parse(args[4]);
    if (Directory.Exists(output)) throw new InvalidOperationException("Use a new diagnostic output directory.");
    Directory.CreateDirectory(output);
    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    start.ArgumentList.Add(host); start.ArgumentList.Add("--output"); start.ArgumentList.Add(Path.Combine(output, "host")); start.ArgumentList.Add("--mode"); start.ArgumentList.Add(mode);
    foreach (var argument in args.Skip(5)) start.ArgumentList.Add(argument);
    start.Environment["DOTNET_DefaultDiagnosticPortSuspend"] = "1";
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Native process failed to start.");
    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
    var client = new DiagnosticsClient(process.Id);
    // Runtime keyword GC + sampled allocations + JIT/loader + stacks; 1 ms managed sample profiler.
    var providers = new[] { new EventPipeProvider("Microsoft-Windows-DotNETRuntime", EventLevel.Verbose, 0x40000001 | 0x80000 | 0x10 | 0x8),
        new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", EventLevel.Informational) };
    EventPipeSession? session = null;
    try
    {
        for (var attempt = 0; ; attempt++)
        {
            try { session = client.StartEventPipeSession(providers, requestRundown: true, circularBufferMB: 256); break; }
            catch (ServerNotAvailableException) when (attempt < 100) { await Task.Delay(100); }
        }
        using var trace = File.Create(Path.Combine(output, "profile.nettrace"));
        var copying = session.EventStream.CopyToAsync(trace); client.ResumeRuntime();
        var exited = process.WaitForExitAsync(); var deadline = Task.Delay(TimeSpan.FromSeconds(seconds));
        await Task.WhenAny(exited, deadline);
        if (!process.HasExited) session.Stop();
        await copying.WaitAsync(TimeSpan.FromSeconds(45));
        await trace.FlushAsync();
        // A diagnostic capture can stop before the application. Let its own correctness checks finish.
        await exited.WaitAsync(TimeSpan.FromSeconds(750));
        await File.WriteAllTextAsync(Path.Combine(output, "stdout.log"), await stdout);
        await File.WriteAllTextAsync(Path.Combine(output, "stderr.log"), await stderr);
        await File.WriteAllTextAsync(Path.Combine(output, "capture.json"), JsonSerializer.Serialize(new { diagnosticOnly = true, process.Id, process.ExitCode, seconds, host,
            hostSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(host))), providers = providers.Select(p => new { p.Name, p.Keywords, p.EventLevel }),
            boundary = "EventPipe perturbs execution; samples are diagnostic attribution, never unprofiled performance acceptance." }, new JsonSerializerOptions { WriteIndented = true }));
        if (process.ExitCode != 0) throw new InvalidOperationException("Profiled host failed: " + await stderr);
    }
    finally { session?.Dispose(); if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
}
else if (args[0] == "analyze")
{
    var windows = ReadWindows(args[2]);
    var etlx = Path.ChangeExtension(args[1], ".etlx");
    if (!File.Exists(etlx)) TraceLog.CreateFromEventPipeDataFile(args[1], etlx, new TraceLogOptions());
    using var log = new TraceLog(etlx);
    var uiCandidates = new Dictionary<int, int>();
    foreach (var e in log.Events)
    {
        if (e.ProviderName != "Microsoft-DotNETCore-SampleProfiler") continue;
        var frames = Frames(e);
        if (frames.Any(s => s.Contains("TreeDataGridNativeBenchmarks.NativeRunner", StringComparison.Ordinal))
            && frames.Any(s => s.StartsWith("NT.AvaloniaControl.TreeDataGrid.", StringComparison.Ordinal)))
            uiCandidates[e.ThreadID] = uiCandidates.GetValueOrDefault(e.ThreadID) + 1;
    }
    if (uiCandidates.Count == 0) throw new InvalidOperationException("No sampled native UI runner stacks.");
    var uiThreadId = uiCandidates.MaxBy(pair => pair.Value).Key;
    var aggregates = new Dictionary<string, Totals>(); var samples = 0; var allocations = 0; var missingStacks = 0;
    foreach (var e in log.Events)
    {
        var cpu = e.ProviderName == "Microsoft-DotNETCore-SampleProfiler" && e.EventName == "Thread/Sample";
        var allocation = e is GCAllocationTickTraceData;
        if (!cpu && !allocation) continue;
        var stack = Frames(e);
        if (stack.Count == 0) missingStacks++;
        var time = e.TimeStamp.ToUniversalTime();
        var window = e.ThreadID == uiThreadId ? windows.LastOrDefault(w => w.Start <= time && w.End >= time) : null;
        var phase = window is null ? (e.ThreadID == uiThreadId ? "ui-outside-measurement" : "background") : window.Scenario + (window.Warmup ? "/warmup-ui" : "/measured-ui");
        var product = stack.FirstOrDefault(s => s.StartsWith("NT.AvaloniaControl.TreeDataGrid.", StringComparison.Ordinal));
        var owner = product ?? stack.FirstOrDefault(s => s.Contains("TreeDataGridNativeBenchmarks.", StringComparison.Ordinal)) ?? "framework/native/other";
        var key = phase + " | " + owner;
        if (!aggregates.TryGetValue(key, out var totals)) aggregates[key] = totals = new();
        if (cpu) { samples++; totals.CpuSamples++; }
        if (e is GCAllocationTickTraceData tick)
        {
            allocations++; totals.AllocationTicks++; var bytes = (long)tick.AllocationAmount64; totals.SampledAllocationBytes += bytes;
            totals.Types[tick.TypeName ?? "unknown"] = totals.Types.GetValueOrDefault(tick.TypeName ?? "unknown") + bytes;
        }
        if (totals.Stacks.Count < 4) totals.Stacks.Add(string.Join("\n", stack));
        foreach (var method in stack.Distinct())
        {
            if (!method.Contains("TreeDataGrid", StringComparison.Ordinal) && !method.StartsWith("Avalonia.", StringComparison.Ordinal)) continue;
            var existing = totals.Inclusive.GetValueOrDefault(method);
            totals.Inclusive[method] = (existing.Samples + (cpu ? 1 : 0), existing.Bytes + (e is GCAllocationTickTraceData a ? (long)a.AllocationAmount64 : 0));
        }
    }
    var report = new { diagnosticOnly = true, log.EventsLost, samples, allocations, missingStacks, uiThreadId, uiCandidates, uiWindows = windows.Count,
        boundary = "Managed sample counts are not elapsed CPU milliseconds. Allocation ticks estimate bytes by sampled type/stack, not exact object counts. Inclusive methods overlap. No native symbol-server resolution.",
        groups = aggregates.Select(pair => new { attribution = pair.Key, pair.Value.CpuSamples, pair.Value.AllocationTicks, pair.Value.SampledAllocationBytes,
            types = pair.Value.Types.OrderByDescending(x => x.Value).Take(15), cpuInclusive = pair.Value.Inclusive.OrderByDescending(x => x.Value.Samples).Take(20).Select(x => new { method = x.Key, samples = x.Value.Samples }),
            allocationInclusive = pair.Value.Inclusive.OrderByDescending(x => x.Value.Bytes).Take(20).Select(x => new { method = x.Key, sampledBytes = x.Value.Bytes }), stacks = pair.Value.Stacks }).OrderByDescending(x => x.SampledAllocationBytes) };
    File.WriteAllText(args[3], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"samples={samples}, allocation ticks={allocations}, lost={log.EventsLost}, stackless={missingStacks}");
    if (samples == 0 || allocations == 0 || log.EventsLost != 0) throw new InvalidOperationException("Missing profiling evidence or dropped events.");
}
else throw new ArgumentException("Unknown diagnostic mode.");

static List<UiWindow> ReadWindows(string path)
{
    var result = new List<UiWindow>();
    using var initial = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "initial.json")));
    var frequency = initial.RootElement.GetProperty("stopwatchFrequency").GetInt64();
    foreach (var line in File.ReadLines(path))
    {
        using var doc = JsonDocument.Parse(line); var root = doc.RootElement;
        if (root.GetProperty("kind").GetString() != "completed") continue;
        var value = root.GetProperty("value"); var anchor = root.GetProperty("utc").GetDateTimeOffset().UtcDateTime; var anchorTicks = root.GetProperty("ticks").GetInt64();
        DateTime At(string key) => anchor.AddSeconds((value.GetProperty(key).GetInt64() - anchorTicks) / (double)frequency);
        result.Add(new(At("StartTicks"), At("LayoutEndTicks"), value.GetProperty("Scenario").GetString()!, value.GetProperty("Warmup").GetBoolean()));
    }
    return result;
}

static List<string> Frames(TraceEvent e)
{
    var stack = new List<string>();
    for (var frame = e.CallStack(); frame is not null; frame = frame.Caller)
        stack.Add(frame.CodeAddress.FullMethodName is { Length: > 0 } method ? method : frame.CodeAddress.ModuleName + "!0x" + frame.CodeAddress.Address.ToString("X"));
    return stack;
}

sealed record UiWindow(DateTime Start, DateTime End, string Scenario, bool Warmup);
sealed class Totals
{
    internal long CpuSamples, AllocationTicks, SampledAllocationBytes;
    internal Dictionary<string, long> Types = [];
    internal Dictionary<string, (long Samples, long Bytes)> Inclusive = [];
    internal HashSet<string> Stacks = [];
}
