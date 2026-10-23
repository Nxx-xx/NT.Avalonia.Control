using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

// Records original inputs and product results. Mathematical acceptance is a separate Python process.
internal static class HierarchyRegression
{
    public static int Run(string[] args)
    {
        var at = Array.IndexOf(args, "--output");
        var output = Path.GetFullPath(at >= 0 && at + 1 < args.Length ? args[at + 1] : "artifacts/charts-hierarchy/raw.json");
        if (File.Exists(output)) { Console.Error.WriteLine("Choose a fresh evidence path."); return 1; }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var report = new Report();
        try
        {
            foreach (var family in Families)
            {
                foreach (var fixture in new[] { "basic", "flat", "empty", "zero", "deep" })
                    Observe(report, family + "-" + fixture, family, Fixture(fixture));
                Observe(report, family + "-drill", family, Fixture("basic"), root: 0);
                Observe(report, family + "-collapse", family, Fixture("basic"), collapsed: [0]);
            }
            Observe(report, "treemap-slice", "treemap", Fixture("basic"), slice: true);
            Invalid(report, "negative", "ArgumentOutOfRangeException", () => ChartHierarchy.Snapshot([new HierarchyNode(-1)]));
            Invalid(report, "nonfinite", "ArgumentOutOfRangeException", () => ChartHierarchy.Snapshot([new HierarchyNode(double.NaN)]));
            var repeated = new HierarchyNode(1);
            Invalid(report, "repeated-node", "ArgumentException", () => ChartHierarchy.Snapshot([repeated, repeated]));
            Invalid(report, "snapshot-cancelled", "OperationCanceledException", () => ChartHierarchy.Snapshot([], cancellationToken: new(true)));
            foreach (var family in Families)
                Invalid(report, family + "-cancelled", "OperationCanceledException", () => Layout(family, [], cancellationToken: new(true)));
            var tooMany = Enumerable.Range(0, 10001).Select(_ => new HierarchyNode(1)).ToArray();
            Invalid(report, "node-budget", "ArgumentOutOfRangeException", () => ChartHierarchy.Snapshot(tooMany));
            var tooDeep = new HierarchyNode(1);
            for (var i = 0; i < 65; i++) tooDeep = new HierarchyNode(0, [tooDeep]);
            Invalid(report, "depth-budget", "ArgumentOutOfRangeException", () => ChartHierarchy.Snapshot([tooDeep]));
            var tooPacked = Enumerable.Range(0, 2001).Select(_ => new HierarchyNode(1)).ToArray();
            Invalid(report, "packing-budget", "ArgumentOutOfRangeException", () => ChartHierarchy.PackedBubble(ChartHierarchy.Snapshot(tooPacked)));
            Invalid(report, "treemap-root", "ArgumentOutOfRangeException", () => ChartHierarchy.Treemap(ChartHierarchy.Snapshot([]), rootIndex: 0));
            Performance(report, "perf-treemap-10000", "treemap", 10000, 997);
            Performance(report, "perf-packed-1000", "packed-bubble", 1000, 97);
            report.Passed = report.Cases.Count == 57 && report.Cases.All(c => c.Kind == "invalid" ? c.ExceptionType is not null : c.ExceptionType is null && c.RepeatedResultIdentical == true) && report.PerformanceCases.Count == 2 && report.PerformanceCases.All(c => c.ExceptionType is null);
        }
        catch (Exception e) { report.Error = e.ToString(); Console.Error.WriteLine(e); }
        report.FinishedUtc = DateTimeOffset.UtcNow;
        using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, report, JsonOptions);
        return report.Passed ? 0 : 1;
    }
    private static readonly string[] Families = ["treemap", "sunburst", "organization", "bubble-cloud", "packed-bubble", "circle-packing"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    private sealed record Input(string Id, int Parent, double Value, string Label);
    private static Input[] Fixture(string id) => id switch
    {
        "basic" => [new("north", -1, 999, "North"), new("north-a", 0, 6, "A"), new("north-b", 0, 3, "B"), new("south", -1, 222, "South"), new("south-c", 3, 2, "C"), new("south-d", 3, 1, "D")],
        "flat" => [new("a", -1, 9, "A"), new("b", -1, 4, "B"), new("c", -1, 1, "C")],
        "zero" => [new("zero-root", -1, 400, "Zero"), new("zero-a", 0, 0, "Zero A"), new("zero-b", 0, 0, "Zero B")],
        "deep" => Enumerable.Range(0, 12).Select(i => new Input($"d{i}", i - 1, 1, $"D{i}")).ToArray(),
        "empty" => [],
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    private static HierarchyNode[] Nodes(Input[] rows)
    {
        var nodes = new HierarchyNode[rows.Length];
        var children = Enumerable.Range(0, rows.Length).Select(_ => new List<int>()).ToArray();
        for (var i = 0; i < rows.Length; i++) if (rows[i].Parent >= 0) children[rows[i].Parent].Add(i);
        for (var i = rows.Length - 1; i >= 0; i--)
        {
            var row = rows[i];
            nodes[i] = new HierarchyNode(row.Value, children[i].Select(j => nodes[j]), row.Label, row, row.Id);
        }
        return Enumerable.Range(0, rows.Length).Where(i => rows[i].Parent == -1).Select(i => nodes[i]).ToArray();
    }
    private static object Layout(string family, Input[] rows, int root = -1, int[]? collapsed = null, bool slice = false, CancellationToken cancellationToken = default)
    {
        var snapshot = ChartHierarchy.Snapshot(Nodes(rows));
        var collapsedSet = collapsed?.ToHashSet();
        return family switch
        {
            "treemap" => ChartHierarchy.Treemap(snapshot, root, collapsedSet, slice ? TreemapLayoutMode.Slice : TreemapLayoutMode.Squarify, cancellationToken),
            "sunburst" => ChartHierarchy.Sunburst(snapshot, root, collapsedSet, cancellationToken: cancellationToken),
            "organization" => ChartHierarchy.Tree(snapshot, root, collapsedSet, cancellationToken),
            "bubble-cloud" => ChartHierarchy.BubbleCloud(snapshot, root, collapsedSet, cancellationToken),
            "packed-bubble" => ChartHierarchy.PackedBubble(snapshot, root, collapsedSet, cancellationToken),
            "circle-packing" => ChartHierarchy.CirclePacking(snapshot, root, collapsedSet, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
    }
    private static void Observe(Report report, string id, string family, Input[] rows, int root = -1, int[]? collapsed = null, bool slice = false)
    {
        var observation = Measure(id, family, rows, new { rootIndex = root, collapsedIndices = collapsed ?? [], treemapMode = slice ? "Slice" : "Squarify" }, () => Layout(family, rows, root, collapsed, slice));
        if (observation.ExceptionType is null) observation.RepeatedResultIdentical = JsonSerializer.Serialize(observation.Actual, JsonOptions) == JsonSerializer.Serialize(Layout(family, rows, root, collapsed, slice), JsonOptions);
        report.Cases.Add(observation);
    }
    private static Observation Measure(string id, string family, Input[] rows, object parameters, Func<object> action)
    {
        var result = new Observation { Id = id, Kind = family, Input = rows, Parameters = parameters };
        var start = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
        try { result.Actual = action(); } catch (Exception e) { result.ExceptionType = e.GetType().Name; result.ExceptionMessage = e.Message; }
        result.ElapsedTicks = Stopwatch.GetTimestamp() - start; result.ThreadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return result;
    }
    private static void Invalid(Report report, string id, string expected, Func<object> action) => report.Cases.Add(Measure("invalid-" + id, "invalid", [], new { expectedException = expected }, action));
    private static void Performance(Report report, string id, string family, int count, int modulus)
    {
        var rows = Enumerable.Range(0, count).Select(i => new Input($"p{i}", -1, 1 + (i * 37) % modulus, $"P{i}")).ToArray();
        var source = Nodes(rows);
        var observation = Measure(id, family, [], new { rootIndex = -1, collapsedIndices = Array.Empty<int>(), treemapMode = "Squarify", count, modulus }, () =>
        {
            var snapshot = ChartHierarchy.Snapshot(source);
            return family == "treemap" ? (object)ChartHierarchy.Treemap(snapshot) : ChartHierarchy.PackedBubble(snapshot);
        });
        observation.InputCount = count; observation.InputFormula = $"value=1+(i*37)%{modulus}";
        using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes); foreach (var row in rows) writer.Write(row.Value);
        observation.InputSha256 = Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length))));
        report.PerformanceCases.Add(observation);
    }
    private static object AssemblyRecord(Assembly assembly) => new { path = assembly.Location, bytes = new FileInfo(assembly.Location).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))), assembly = assembly.FullName };
    private sealed class Report
    {
        public string SchemaVersion { get; } = "charts-c601-hierarchy-observations-v1";
        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow; public DateTimeOffset? FinishedUtc { get; set; } public bool Passed { get; set; } public string? Error { get; set; }
        public long StopwatchFrequency { get; } = Stopwatch.Frequency;
        public object Environment { get; } = new { os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), runtime = RuntimeInformation.FrameworkDescription, processorCount = System.Environment.ProcessorCount };
        public object[] AssemblyManifest { get; } = [AssemblyRecord(typeof(ChartHierarchy).Assembly), AssemblyRecord(typeof(HierarchyRegression).Assembly)];
        public bool FormalSlaEvaluated { get; } = false; public List<Observation> Cases { get; } = []; public List<Observation> PerformanceCases { get; } = [];
    }
    private sealed class Observation
    {
        public required string Id { get; init; } public required string Kind { get; init; } public required Input[] Input { get; init; } public required object Parameters { get; init; }
        public object? Actual { get; set; } public string? ExceptionType { get; set; } public string? ExceptionMessage { get; set; } public bool? RepeatedResultIdentical { get; set; }
        public string? InputFormula { get; set; } public int? InputCount { get; set; } public string? InputSha256 { get; set; } public long ElapsedTicks { get; set; } public long ThreadAllocatedBytes { get; set; }
    }
}
