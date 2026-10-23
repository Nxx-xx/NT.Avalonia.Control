using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace Charts.TraceAnalysis;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Notes =
    [
        "This is a diagnostic run with tracing enabled. Its timings are ineligible for untraced performance comparisons.",
        "GCAllocationTick is sampled. Received event counts are not object counts. AllocationAmount64/AllocationAmount represents approximate allocation intervals attributed to the sampled type and stack, not exact per-type bytes.",
        "ObjectSize is the size of the sampled object only. Summing it does not give total heap allocation or retained/live memory.",
        "Managed ThreadSample counts estimate managed thread activity. They are not kernel CPU samples, exact CPU duration, native/GPU time, or measured UI present latency. External samples are reported separately.",
        "TraceLog.SampleProfileInterval is converter metadata, not a validated measured EventPipe sampling cadence. No CPU time is calculated by multiplying counts by that interval.",
        "Inclusive frame rows overlap: one sample contributes to multiple frames. Do not sum frame rows.",
        "The trace covers process startup, fixture generation, warmups, measured samples and shutdown. No stage-specific attribution is asserted because the frozen host emits no phase markers.",
        "Local managed rundown names are used; no remote native-symbol download or source-line resolution is attempted. Unknown frames and missing stacks are retained and counted.",
        "Event loss and conversion truncation are reported. Zero reported loss is not proof that every object allocation or every CPU instruction was observed."
    ];

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            Console.WriteLine("Charts.TraceAnalysis --input <trace.nettrace> --output <analysis.json> [--top 40]");
            return 0;
        }
        try { Analyze(args); return 0; }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }

    private static void Analyze(string[] args)
    {
        string Required(string key)
        {
            var index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : throw new ArgumentException($"Missing {key}.");
        }
        var input = Path.GetFullPath(Required("--input"));
        var output = Path.GetFullPath(Required("--output"));
        var top = args.Contains("--top", StringComparer.Ordinal) ? int.Parse(Required("--top"), System.Globalization.CultureInfo.InvariantCulture) : 40;
        if (top is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(args), "--top must be between 1 and 200.");
        if (!File.Exists(input)) throw new FileNotFoundException("Trace does not exist.", input);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var etlx = Path.ChangeExtension(output, ".etlx");
        if (string.Equals(input, etlx, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Input must not be the derived ETLX file.");
        var losses = new List<object>();
        var conversionLog = Path.ChangeExtension(output, ".conversion.txt");
        using (var conversion = File.CreateText(conversionLog))
        {
            var options = new TraceLogOptions
            {
                KeepAllEvents = true,
                LocalSymbolsOnly = true,
                ShouldResolveSymbols = _ => false,
                ConversionLog = conversion,
                OnLostEvents = (truncated, lost, total) => losses.Add(new { truncated, eventsLost = lost, totalEvents = total }),
            };
            TraceLog.CreateFromEventPipeDataFile(input, etlx, options);
        }

        var allocations = new Dictionary<string, Counter>(StringComparer.Ordinal);
        var allocationStacks = new Dictionary<string, Counter>(StringComparer.Ordinal);
        var allocationFrames = new Dictionary<string, Counter>(StringComparer.Ordinal);
        var managedStacks = new Dictionary<string, Counter>(StringComparer.Ordinal);
        var managedFrames = new Dictionary<string, Counter>(StringComparer.Ordinal);
        var externalStacks = new Dictionary<string, Counter>(StringComparer.Ordinal);
        var eventKinds = new Dictionary<string, long>(StringComparer.Ordinal);
        var sampleKinds = new Dictionary<string, long>(StringComparer.Ordinal);
        var versions = new Dictionary<int, long>();
        var processIds = new HashSet<int>();
        var allFrames = new HashSet<string>(StringComparer.Ordinal);
        var unresolvedFrames = new HashSet<string>(StringComparer.Ordinal);
        long stacksTruncatedAt128Frames = 0;
        long receivedEvents = 0, allocationTicks = 0, allocationMissingStacks = 0, managedSamples = 0, managedMissingStacks = 0;
        long totalReportedBytes = 0, sampledObjectBytes = 0, amount64Events = 0, amount32Events = 0;
        double firstAllocationMs = double.PositiveInfinity, lastAllocationMs = 0;
        using var log = new TraceLog(etlx);
        using var source = log.Events.GetSource();

        string[] Stack(TraceEvent data)
        {
            var result = new List<string>();
            var stack = log.GetCallStackForEvent(data);
            for (var depth = 0; stack is not null && depth < 128; depth++, stack = stack.Caller)
            {
                var address = stack.CodeAddress;
                var method = address.FullMethodName;
                var unresolved = string.IsNullOrEmpty(method);
                var frame = unresolved ? $"{address.ModuleName ?? "unknown"}!0x{address.Address:x}" : $"{address.ModuleName}!{method}";
                result.Add(frame); allFrames.Add(frame);
                if (unresolved) unresolvedFrames.Add(frame);
            }
            if (stack is not null) stacksTruncatedAt128Frames++;
            return result.Count == 0 ? ["[missing stack]"] : result.ToArray();
        }

        source.AllEvents += data =>
        {
            receivedEvents++;
            processIds.Add(data.ProcessID);
            Increment(eventKinds, $"{data.ProviderName}/{data.EventName}");
        };
        source.Clr.GCAllocationTick += data =>
        {
            allocationTicks++;
            Increment(versions, data.Version);
            var bytes = data.Version >= 2 ? data.AllocationAmount64 : Math.Max(0, data.AllocationAmount);
            if (data.Version >= 2) amount64Events++; else amount32Events++;
            bytes = Math.Max(0, bytes);
            totalReportedBytes += bytes;
            var objectBytes = data.Version >= 4 ? Math.Max(0, data.ObjectSize) : 0;
            sampledObjectBytes += objectBytes;
            firstAllocationMs = Math.Min(firstAllocationMs, data.TimeStampRelativeMSec);
            lastAllocationMs = Math.Max(lastAllocationMs, data.TimeStampRelativeMSec);
            var type = $"{data.TypeName ?? "[unknown type]"} ({data.AllocationKind})";
            Add(allocations, type, bytes, objectBytes);
            var frames = Stack(data);
            if (frames[0] == "[missing stack]") allocationMissingStacks++;
            Add(allocationStacks, type + "\n" + string.Join('\n', frames), bytes, objectBytes);
            foreach (var frame in frames.Distinct(StringComparer.Ordinal)) Add(allocationFrames, frame, bytes, objectBytes);
        };
        var profiler = new SampleProfilerTraceEventParser(source);
        profiler.ThreadSample += data =>
        {
            Increment(sampleKinds, data.Type.ToString());
            var frames = Stack(data);
            if (data.Type == ClrThreadSampleType.Managed)
            {
                managedSamples++;
                if (frames[0] == "[missing stack]") managedMissingStacks++;
                Add(managedStacks, string.Join('\n', frames));
                foreach (var frame in frames.Distinct(StringComparer.Ordinal)) Add(managedFrames, frame);
            }
            else Add(externalStacks, data.Type + "\n" + string.Join('\n', frames));
        };
        source.Process();

        var capture = Path.Combine(Path.GetDirectoryName(input)!, "capture.json");
        var traceEventAssembly = typeof(TraceLog).Assembly;
        var analyzerAssembly = typeof(Program).Assembly;
        var report = new
        {
            schemaVersion = 1,
            status = log.EventsLost == 0 && losses.Count == 0 ? "analyzed-no-reported-loss" : "analyzed-with-loss-or-truncation",
            generatedUtc = DateTimeOffset.UtcNow,
            input = FileRecord(input),
            captureManifest = File.Exists(capture) ? FileRecord(capture) : null,
            analyzer = new
            {
                assembly = FileRecord(analyzerAssembly.Location),
                informationalVersion = analyzerAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                traceEventPackageVersion = "3.2.6",
                traceEventAssembly = FileRecord(traceEventAssembly.Location),
                traceEventAssemblyVersion = traceEventAssembly.GetName().Version?.ToString(),
                runtime = RuntimeInformation.FrameworkDescription,
                os = RuntimeInformation.OSDescription,
                command = Environment.GetCommandLineArgs(),
                workingDirectory = Environment.CurrentDirectory,
                sources = AnalyzerSources(),
            },
            trace = new
            {
                log.SessionStartTime, log.SessionEndTime, durationMilliseconds = log.SessionDuration.TotalMilliseconds,
                log.EventCount, receivedEvents, log.EventsLost, lostEventCallbacks = losses,
                log.HasCallStacks, traceLogNominalSampleProfileIntervalMilliseconds = log.SampleProfileInterval.TotalMilliseconds,
                uniqueObservedFrames = allFrames.Count, unresolvedFrames = unresolvedFrames.Order().ToArray(), stacksTruncatedAt128Frames,
                processIds = processIds.Order().ToArray(),
                eventKinds = eventKinds.OrderByDescending(pair => pair.Value).ToDictionary(),
            },
            allocation = new
            {
                receivedTickEvents = allocationTicks,
                reportedApproximateAllocationBytes = totalReportedBytes,
                sampledObjectBytes,
                allocationMissingStacks,
                amount64Events, amount32Events,
                eventVersions = versions,
                firstAllocationMilliseconds = allocationTicks == 0 ? (double?)null : firstAllocationMs,
                lastAllocationMilliseconds = lastAllocationMs,
                types = Rows(allocations, top, true),
                stacks = Rows(allocationStacks, top, true),
                inclusiveFrames = Rows(allocationFrames, top, true),
            },
            threadSamples = new
            {
                kinds = sampleKinds, managedSamples, managedMissingStacks,
                managedStacks = Rows(managedStacks, top, false),
                managedInclusiveFrames = Rows(managedFrames, top, false),
                externalOrErrorStacks = Rows(externalStacks, top, false),
            },
            notes = Notes,
        };
        File.WriteAllText(output, JsonSerializer.Serialize(report, JsonOptions));
        var markdown = new StringBuilder("# Charts EventPipe diagnostic\n\n");
        markdown.AppendLine($"Input: `{input}`  \nTrace SHA256: `{Hash(input)}`  \nReported lost events: {log.EventsLost}; conversion loss/truncation callbacks: {losses.Count}.\n");
        markdown.AppendLine($"Allocation tick events: {allocationTicks:N0}; approximate allocation intervals represented: {totalReportedBytes:N0} bytes. Managed thread samples: {managedSamples:N0}.\n");
        AppendTable(markdown, "Allocation samples by type (approximate attributed bytes)", allocations, Math.Min(top, 20), true);
        AppendTable(markdown, "Allocation inclusive frames (overlapping rows)", allocationFrames, Math.Min(top, 20), true);
        AppendTable(markdown, "Managed thread inclusive frames (sample counts)", managedFrames, Math.Min(top, 20), false);
        foreach (var note in Notes) markdown.AppendLine($"- {note}");
        File.WriteAllText(Path.ChangeExtension(output, ".md"), markdown.ToString());
        Console.WriteLine($"Allocation ticks={allocationTicks}; approximate represented bytes={totalReportedBytes}; managed samples={managedSamples}; reported lost events={log.EventsLost}; loss callbacks={losses.Count}");
        Console.WriteLine(output);
    }

    private sealed class Counter
    {
        public long Events { get; set; }
        public long ApproximateBytes { get; set; }
        public long SampledObjectBytes { get; set; }
    }
    private sealed record Row(string Name, long Events, long ApproximateBytes, long SampledObjectBytes);
    private static void Add(Dictionary<string, Counter> counters, string key, long bytes = 0, long objectBytes = 0)
    {
        if (!counters.TryGetValue(key, out var counter)) counters[key] = counter = new Counter();
        counter.Events++; counter.ApproximateBytes += bytes; counter.SampledObjectBytes += objectBytes;
    }
    private static void Increment<T>(Dictionary<T, long> counters, T key) where T : notnull => counters[key] = counters.GetValueOrDefault(key) + 1;
    private static Row[] Rows(Dictionary<string, Counter> counters, int count, bool bytes) =>
        counters.OrderByDescending(pair => bytes ? pair.Value.ApproximateBytes : pair.Value.Events).Take(count)
            .Select(pair => new Row(pair.Key, pair.Value.Events, pair.Value.ApproximateBytes, pair.Value.SampledObjectBytes)).ToArray();
    private static void AppendTable(StringBuilder text, string title, Dictionary<string, Counter> counters, int count, bool bytes)
    {
        text.AppendLine($"## {title}\n\n| Name | Received events | Approximate attributed bytes |\n| --- | ---: | ---: |");
        foreach (var row in Rows(counters, count, bytes)) text.AppendLine($"| {row.Name.Replace("|", "\\|", StringComparison.Ordinal)} | {row.Events} | {(bytes ? row.ApproximateBytes.ToString(System.Globalization.CultureInfo.InvariantCulture) : "n/a")} |");
        text.AppendLine();
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static object FileRecord(string path) => new { path = Path.GetFullPath(path), bytes = new FileInfo(path).Length, sha256 = Hash(path) };
    private static object[] AnalyzerSources() => new[] { "Program.cs", "Charts.TraceAnalysis.csproj", "packages.lock.json", "Capture.ps1" }
        .Select(name => Path.Combine("benchmarks", "Charts.TraceAnalysis", name)).Where(File.Exists).Select(FileRecord).ToArray();
}
