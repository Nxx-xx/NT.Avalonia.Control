using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

// Captures inputs/results only. Mathematical acceptance runs in a separate Python process.
internal static class RelationsRegression
{
    private static readonly string[] Kinds = ["sankey", "alluvial", "chord", "network", "force", "arc", "flow", "process", "mindmap"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    private sealed record NodeInput(string Id, string Label, double Weight = 1, string? Group = null, double? X = null, double? Y = null, double Width = .12, double Height = .08, int? Stage = null, string Shape = "Rectangle");
    private sealed record EdgeInput(string Id, string SourceId, string TargetId, double Weight, string? Label = null);
    private sealed record Input(NodeInput[] Nodes, EdgeInput[] Edges);
    private static Input Fixture(string name) => name switch
    {
        "basic" => new([new("a", "A", 10, "origin", .15, .5, Stage: 0), new("b", "B", 6, "delivery", .5, .25, Stage: 1), new("c", "C", 4, "delivery", .5, .75, Stage: 1, Shape: "Diamond"), new("d", "D", 10, "delivery", .85, .5, Stage: 2)], [new("ab", "a", "b", 6, "six"), new("ac", "a", "c", 4), new("bd", "b", "d", 6), new("cd", "c", "d", 4)]),
        "empty" => new([], []),
        "zero" => new([new("a", "A", 0, Stage: 0), new("b", "B", 0, Stage: 1)], [new("ab", "a", "b", 0)]),
        "multigraph" => new([new("a", "A", 5, Stage: 0), new("b", "B", 5, Stage: 1), new("c", "C", 0, Stage: 2)], [new("ab1", "a", "b", 3), new("ab2", "a", "b", 2), new("ba", "b", "a", 1), new("bb", "b", "b", 2), new("bc", "b", "c", 0)]),
        "disconnected" => new([new("a", "A", 1, Stage: 0), new("b", "B", 2, Stage: 1), new("c", "C", 3, Stage: 2)], []),
        "fat-self" => new([new("a", "A", 4, X: .5, Y: .5, Stage: 0)], [new("aa", "a", "a", 4)]),
        "epsilon" => new([new("a", "A", double.Epsilon, X: .25, Y: .5, Stage: 0), new("b", "B", double.Epsilon, X: .75, Y: .5, Stage: 1)], [new("ab", "a", "b", double.Epsilon)]),
        "vertical" => new([new("a", "A", 4, X: .5, Y: .25, Stage: 0), new("b", "B", 4, X: .5, Y: .75, Stage: 1)], [new("ab", "a", "b", 4)]),
        "epsilon-parallel" => new([new("a", "A", double.Epsilon, X: .25, Y: .5, Stage: 0), new("b", "B", double.Epsilon, X: .75, Y: .5, Stage: 1)], [new("ab-1", "a", "b", double.Epsilon), new("ab-2", "a", "b", double.Epsilon * 2)]),
        "vertical-cycle" => new([new("a", "A", 4, X: .5, Y: .25, Stage: 0), new("b", "B", 4, X: .5, Y: .75, Stage: 1)], [new("ab", "a", "b", 4), new("ba", "b", "a", 2)]),
        "rectangle-gap" => new([new("a", "A", 4, X: .4, Y: .4, Stage: 0), new("b", "B", 4, X: .55, Y: .54, Stage: 1)], [new("ab", "a", "b", 4)]),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
    private static GraphSnapshot Snapshot(Input input, CancellationToken cancellationToken = default) => ChartRelations.Snapshot(
        input.Nodes.Select(n => new GraphNode(n.Id, n.Label, n, n.Group, Enum.Parse<RelationNodeShape>(n.Shape), n.X is { } x && n.Y is { } y ? new ChartPoint(x, y) : null, n.Weight, n.Width, n.Height, n.Stage)),
        input.Edges.Select(e => new GraphEdge(e.SourceId, e.TargetId, e.Weight, e.Label, e, e.Id)), cancellationToken);
    private static object Layout(string kind, GraphSnapshot graph, CancellationToken cancellationToken = default) => kind switch
    {
        "sankey" => ChartRelations.Sankey(graph, cancellationToken: cancellationToken),
        "alluvial" => ChartRelations.Sankey(graph, mode: RelationSankeyMode.Alluvial, cancellationToken: cancellationToken),
        "chord" => ChartRelations.Chord(graph, cancellationToken: cancellationToken),
        "network" => ChartRelations.Network(graph, RelationNetworkMode.Circular, cancellationToken: cancellationToken),
        "force" => ChartRelations.Network(graph, RelationNetworkMode.ForceDirected, cancellationToken: cancellationToken),
        "arc" => ChartRelations.Network(graph, RelationNetworkMode.Arc, cancellationToken: cancellationToken),
        "flow" => ChartRelations.Flow(graph, RelationFlowMode.Explicit, cancellationToken),
        "process" => ChartRelations.Flow(graph, RelationFlowMode.Process, cancellationToken),
        "mindmap" => ChartRelations.Flow(graph, RelationFlowMode.Mindmap, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public static int Run(string[] args)
    {
        var at = Array.IndexOf(args, "--output");
        var path = Path.GetFullPath(at >= 0 && at + 1 < args.Length ? args[at + 1] : "artifacts/charts-relations/raw.json");
        if (File.Exists(path)) { Console.Error.WriteLine("Choose a fresh evidence path."); return 1; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var report = new Report();
        try
        {
            foreach (var kind in Kinds)
                foreach (var fixture in new[] { "basic", "empty", "zero", "multigraph", "disconnected", "fat-self", "epsilon", "vertical", "epsilon-parallel", "vertical-cycle", "rectangle-gap" })
                {
                    var input = Fixture(fixture); var graph = Snapshot(input);
                    var row = Measure(kind + "-" + fixture, kind, input, new { iterations = 80, seed = 1, nodeWidth = .045, nodeGap = .025, crossingIterations = 6, paddingDegrees = 3 }, () => Layout(kind, graph));
                    row.Snapshot = graph;
                    if (row.ExceptionType is null) row.RepeatedResultIdentical = JsonSerializer.Serialize(row.Actual, JsonOptions) == JsonSerializer.Serialize(Layout(kind, graph), JsonOptions);
                    report.Cases.Add(row);
                }
            Invalid(report, "negative-node", "ArgumentOutOfRangeException", () => Snapshot(new([new("a", "A", -1)], [])));
            Invalid(report, "nonfinite-node", "ArgumentOutOfRangeException", () => Snapshot(new([new("a", "A", double.NaN)], [])));
            Invalid(report, "negative-edge", "ArgumentOutOfRangeException", () => Snapshot(new([new("a", "A")], [new("aa", "a", "a", -1)])));
            Invalid(report, "nonfinite-edge", "ArgumentOutOfRangeException", () => Snapshot(new([new("a", "A")], [new("aa", "a", "a", double.PositiveInfinity)])));
            Invalid(report, "missing-endpoint", "ArgumentException", () => Snapshot(new([new("a", "A")], [new("ab", "a", "b", 1)])));
            Invalid(report, "duplicate-node-id", "ArgumentException", () => Snapshot(new([new("a", "A"), new("a", "Another A")], [])));
            Invalid(report, "duplicate-edge-id", "ArgumentException", () => Snapshot(new([new("a", "A")], [new("same", "a", "a", 1), new("same", "a", "a", 1)])));
            Invalid(report, "node-budget", "ArgumentOutOfRangeException", () => Snapshot(new(Enumerable.Range(0, 2001).Select(i => new NodeInput("n" + i, "N")).ToArray(), [])));
            Invalid(report, "edge-budget", "ArgumentOutOfRangeException", () => Snapshot(new([new("a", "A")], Enumerable.Range(0, 10001).Select(i => new EdgeInput("e" + i, "a", "a", 1)).ToArray())));
            Invalid(report, "snapshot-cancelled", "OperationCanceledException", () => Snapshot(Fixture("empty"), new(true)));
            Invalid(report, "strict-conservation", "ArgumentException", () => ChartRelations.Sankey(Snapshot(new([new("a", "A"), new("b", "B"), new("c", "C")], [new("ab", "a", "b", 6), new("bc", "b", "c", 4)])), strictConservation: true));
            Invalid(report, "force-budget", "ArgumentOutOfRangeException", () => ChartRelations.Network(Snapshot(new(Enumerable.Range(0, 401).Select(i => new NodeInput("n" + i, "N")).ToArray(), [])), RelationNetworkMode.ForceDirected));
            Invalid(report, "force-iterations", "ArgumentOutOfRangeException", () => ChartRelations.Network(Snapshot(Fixture("basic")), iterations: 301));
            foreach (var kind in Kinds)
                Invalid(report, kind + "-cancelled", "OperationCanceledException", () => Layout(kind, Snapshot(Fixture("empty")), new(true)));
            Performance(report, "perf-sankey-1000-1997", "sankey", 1000);
            Performance(report, "perf-force-300-597", "force", 300);
            report.Passed = report.Cases.All(c => c.Kind == "invalid" ? c.ExceptionType is not null : c.ExceptionType is null && c.RepeatedResultIdentical == true) && report.PerformanceCases.All(c => c.ExceptionType is null);
        }
        catch (Exception e) { report.Error = e.ToString(); Console.Error.WriteLine(e); }
        report.FinishedUtc = DateTimeOffset.UtcNow;
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, report, JsonOptions);
        return report.Passed ? 0 : 1;
    }
    private static Observation Measure(string id, string kind, Input input, object parameters, Func<object> action)
    {
        var row = new Observation { Id = id, Kind = kind, Input = input, Parameters = parameters };
        var start = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
        try { row.Actual = action(); } catch (Exception e) { row.ExceptionType = e.GetType().Name; row.ExceptionMessage = e.Message; }
        row.ElapsedTicks = Stopwatch.GetTimestamp() - start; row.ThreadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return row;
    }
    private static void Invalid(Report report, string id, string expected, Func<object> action) => report.Cases.Add(Measure("invalid-" + id, "invalid", Fixture("empty"), new { expectedException = expected }, action));
    private static void Performance(Report report, string id, string kind, int count)
    {
        var nodes = Enumerable.Range(0, count).Select(i => new NodeInput("p" + i, "P" + i, Stage: i)).ToArray();
        var edges = Enumerable.Range(0, count - 1).Select(i => new EdgeInput("e" + i, "p" + i, "p" + (i + 1), 1)).Concat(Enumerable.Range(0, count - 2).Select(i => new EdgeInput("s" + i, "p" + i, "p" + (i + 2), 1))).ToArray();
        var input = new Input(nodes, edges);
        var row = Measure(id, kind, input, new { count, iterations = 80, seed = 1, nodeWidth = .045, nodeGap = .025, crossingIterations = 6, paddingDegrees = 3 }, () => Layout(kind, Snapshot(input)));
        row.InputSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input, JsonOptions)));
        report.PerformanceCases.Add(row);
    }
    private static object AssemblyRecord(Assembly assembly) => new { path = assembly.Location, bytes = new FileInfo(assembly.Location).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))), assembly = assembly.FullName };
    private sealed class Report
    {
        public string SchemaVersion { get; } = "charts-c602-relations-observations-v1";
        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow; public DateTimeOffset? FinishedUtc { get; set; } public bool Passed { get; set; } public string? Error { get; set; }
        public long StopwatchFrequency { get; } = Stopwatch.Frequency;
        public object Environment { get; } = new { os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), runtime = RuntimeInformation.FrameworkDescription, processorCount = System.Environment.ProcessorCount };
        public object[] AssemblyManifest { get; } = [AssemblyRecord(typeof(ChartRelations).Assembly), AssemblyRecord(typeof(RelationsRegression).Assembly)];
        public bool FormalSlaEvaluated { get; } = false; public List<Observation> Cases { get; } = []; public List<Observation> PerformanceCases { get; } = [];
    }
    private sealed class Observation
    {
        public required string Id { get; init; } public required string Kind { get; init; } public required Input Input { get; init; } public required object Parameters { get; init; }
        public object? Snapshot { get; set; } public object? Actual { get; set; } public string? ExceptionType { get; set; } public string? ExceptionMessage { get; set; } public bool? RepeatedResultIdentical { get; set; }
        public string? InputSha256 { get; set; } public long ElapsedTicks { get; set; } public long ThreadAllocatedBytes { get; set; }
    }
}

