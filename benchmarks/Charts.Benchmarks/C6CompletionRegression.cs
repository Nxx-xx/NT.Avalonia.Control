using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

// Records public numeric API inputs and outputs; the separate Python oracle decides acceptance.
internal static class C6CompletionRegression
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    private sealed class Capture
    {
        public required string Id { get; init; }
        public required string Kind { get; init; }
        public object? Input { get; init; }
        public object? Configuration { get; init; }
        public object? Actual { get; set; }
        public string? ExceptionType { get; set; }
        public string? Error { get; set; }
        public string? ExpectedExceptionType { get; init; }
        public double ElapsedMilliseconds { get; set; }
        public long AllocatedBytes { get; set; }
    }
    private static Capture Measure(string id, string kind, object? input, object? configuration, Func<object> action, string? expected = null)
    {
        var row = new Capture { Id = id, Kind = kind, Input = input, Configuration = configuration, ExpectedExceptionType = expected };
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var stamp = Stopwatch.GetTimestamp();
        try { row.Actual = action(); } catch (Exception error) { row.ExceptionType = error.GetType().Name; row.Error = error.Message; }
        row.ElapsedMilliseconds = Stopwatch.GetElapsedTime(stamp).TotalMilliseconds; row.AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - bytes; return row;
    }
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static object TaskInput(ScheduleTask x) => new { x.Id, startTicks = x.Start.UtcTicks, endTicks = x.End.UtcTicks, x.Progress, x.Lane, dependencies = x.Dependencies?.Select(d => new { d.PredecessorId, type = d.Type.ToString(), lagTicks = d.Lag.Ticks }).ToArray() ?? [] };
    private static object ScheduleResult(ScheduleSnapshot snapshot, (long Start, long End)[] queries) => new
    {
        tasks = snapshot.Tasks.Select(x => new { x.SourceIndex, x.Id, startTicks = x.Start.UtcTicks, endTicks = x.End.UtcTicks, x.IsMilestone, x.Task.Progress, x.Task.Lane }),
        minimumTicks = snapshot.Minimum?.UtcTicks, maximumTicks = snapshot.Maximum?.UtcTicks,
        diagnostics = snapshot.Diagnostics.Select(x => new { kind = x.Kind.ToString(), x.TaskId, x.PredecessorId }),
        queries = queries.Select(q => { var result = snapshot.QueryVisible(new(q.Start, TimeSpan.Zero), new(q.End, TimeSpan.Zero)); return new { startTicks = q.Start, endTicks = q.End, sourceIndices = result.Entries.Select(x => x.SourceIndex), result.VisitedNodes }; })
    };
    private static object Point(ChartPoint x) => new { x.X, x.Y };
    private static object HierarchyResult(HierarchySnapshot snapshot, object layout)
    {
        object Entry(HierarchyEntry e) => new { e.Index, e.ParentIndex, e.SourceIndex, e.Id, e.Depth, e.Value, e.AggregateValue, e.Path, e.Children };
        object? rectangles = layout is HierarchyRectangleLayout rectangleLayout ? rectangleLayout.Rectangles.Select(x => new { index = x.Entry.Index, x.X, x.Y, x.Width, x.Height }).ToArray() : null;
        object? nodes = layout switch
        {
            HierarchyRadialLayout r => r.Nodes.Select(x => new { index = x.Entry.Index, position = Point(x.Position), angle = (double?)x.Angle, radius = (double?)x.Radius, mergeHeight = (double?)null }).ToArray(),
            HierarchyDendrogramLayout d => d.Nodes.Select(x => new { index = x.Entry.Index, position = Point(x.Position), angle = (double?)null, radius = (double?)null, mergeHeight = (double?)x.MergeHeight }).ToArray(),
            _ => null
        };
        var edges = layout switch { HierarchyRadialLayout r => r.Edges, HierarchyDendrogramLayout d => d.Edges, _ => Array.Empty<HierarchyTreeEdge>() };
        return new { entries = snapshot.Nodes.Select(Entry).ToArray(), rectangles, nodes, edges = edges.Select(x => new { parent = x.Parent.Index, child = x.Child.Index, start = Point(x.Start), end = Point(x.End) }).ToArray(), maximumHeight = layout is HierarchyDendrogramLayout dendrogram ? (double?)dendrogram.MaximumHeight : null };
    }
    private static HierarchySnapshot Tree(double[] values, int[] parents)
    {
        var nodes = new HierarchyNode[values.Length];
        for (var i = values.Length - 1; i >= 0; i--) { var at = i; nodes[i] = new(values[i], Enumerable.Range(i + 1, values.Length - i - 1).Where(j => parents[j] == at).Select(j => nodes[j]).ToArray(), label: "n" + i, id: "n" + i); }
        return ChartHierarchy.Snapshot(Enumerable.Range(0, values.Length).Where(i => parents[i] < 0).Select(i => nodes[i]));
    }
    public static int Run(string[] args)
    {
        var at = Array.IndexOf(args, "--output"); var path = Path.GetFullPath(at >= 0 && at + 1 < args.Length ? args[at + 1] : "artifacts/charts-c6-completion/raw.json");
        if (File.Exists(path)) { Console.Error.WriteLine("Choose a fresh evidence path."); return 1; }
        var cases = new List<Capture>(); var performance = new List<Capture>(); var started = DateTimeOffset.UtcNow;
        void Add(string id, string kind, object? input, object? config, Func<object> action, string? expected = null) => cases.Add(Measure(id, kind, input, config, action, expected));
        try
        {
            var tasks = new[] { new ScheduleTask("t-a", Epoch, Epoch.AddDays(2), 25, "Alpha"), new ScheduleTask("t-b", Epoch.AddDays(1), Epoch.AddDays(3), 50, "Beta", Dependencies: [new("t-a", ScheduleDependencyType.StartStart, TimeSpan.FromDays(1))]), new ScheduleTask("t-m", Epoch.AddDays(4), Epoch.AddDays(4), 0, "Alpha") };
            var queries = new[] { (Epoch.UtcTicks, Epoch.AddDays(5).UtcTicks), (Epoch.AddDays(2).UtcTicks, Epoch.AddDays(2).UtcTicks), (Epoch.AddDays(3).UtcTicks + 1, Epoch.AddDays(4).UtcTicks - 1) };
            Add("schedule-basic", "schedule", tasks.Select(TaskInput).ToArray(), new { queries = queries.Select(q => new[] { q.Item1, q.Item2 }) }, () => ScheduleResult(ChartSchedule.Snapshot(tasks), queries));
            var precise = new[] { new ScheduleTask("late", Epoch.AddTicks(2), Epoch.AddTicks(3)), new ScheduleTask("early", Epoch.AddTicks(1), Epoch.AddTicks(2)), new ScheduleTask("milestone", Epoch.AddTicks(2), Epoch.AddTicks(2)) };
            var tickQueries = new[] { (Epoch.UtcTicks + 2, Epoch.UtcTicks + 2), (Epoch.UtcTicks + 3, Epoch.UtcTicks + 3) };
            Add("schedule-one-tick", "schedule", precise.Select(TaskInput).ToArray(), new { queries = tickQueries.Select(q => new[] { q.Item1, q.Item2 }) }, () => ScheduleResult(ChartSchedule.Snapshot(precise), tickQueries));
            foreach (var type in Enum.GetValues<ScheduleDependencyType>())
            {
                var input = new[] { new ScheduleTask("a", Epoch, Epoch.AddDays(2)), new ScheduleTask("b", Epoch.AddDays(1), Epoch.AddDays(3), Dependencies: [new("a", type, TimeSpan.FromDays(2))]) };
                Add("dependency-" + type, "schedule", input.Select(TaskInput).ToArray(), new { queries = Array.Empty<long[]>() }, () => ScheduleResult(ChartSchedule.Snapshot(input), []));
            }
            var cyclic = new[] { new ScheduleTask("a", Epoch, Epoch, Dependencies: [new("b"), new("b"), new("missing"), new("a")]), new ScheduleTask("b", Epoch, Epoch, Dependencies: [new("a")]) };
            Add("dependency-diagnostics", "schedule", cyclic.Select(TaskInput).ToArray(), new { queries = Array.Empty<long[]>() }, () => ScheduleResult(ChartSchedule.Snapshot(cyclic), []));
            Add("schedule-empty", "schedule", Array.Empty<object>(), new { queries = Array.Empty<long[]>() }, () => ScheduleResult(ChartSchedule.Snapshot([]), []));
            Add("schedule-invalid-progress", "invalid", new { progress = 101 }, null, () => ChartSchedule.Snapshot([new("a", Epoch, Epoch, 101)]), "ArgumentException");
            Add("schedule-invalid-order", "invalid", null, null, () => ChartSchedule.Snapshot([new("a", Epoch.AddTicks(1), Epoch)]), "ArgumentException");
            Add("schedule-duplicate", "invalid", null, null, () => ChartSchedule.Snapshot([tasks[0], tasks[0]]), "ArgumentException");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Add("schedule-cancel", "invalid", null, null, () => ChartSchedule.Snapshot(tasks, cancelled.Token), "OperationCanceledException");
            var zone = TimeZoneInfo.CreateCustomTimeZone("C6-Synthetic-Eastern", TimeSpan.FromHours(-5), "C6", "C6 standard", "C6 summer", [TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new(2000, 1, 1), new(2099, 12, 31), TimeSpan.FromHours(1), TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday), TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday))]);
            foreach (var item in new[] { ("winter", new DateTime(2026, 1, 15, 12, 0, 0), ScheduleAmbiguousTime.Reject, (string?)null), ("summer", new DateTime(2026, 6, 15, 12, 0, 0), ScheduleAmbiguousTime.Reject, (string?)null), ("gap", new DateTime(2026, 3, 8, 2, 30, 0), ScheduleAmbiguousTime.Reject, "ArgumentException"), ("earlier", new DateTime(2026, 11, 1, 1, 30, 0), ScheduleAmbiguousTime.Earlier, (string?)null), ("later", new DateTime(2026, 11, 1, 1, 30, 0), ScheduleAmbiguousTime.Later, (string?)null), ("ambiguous", new DateTime(2026, 11, 1, 1, 30, 0), ScheduleAmbiguousTime.Reject, "ArgumentException") })
                Add("wall-time-" + item.Item1, "wall-time", new { wallTime = item.Item2.ToString("O"), policy = item.Item3.ToString(), standardOffsetHours = -5, summerOffsetHours = -4 }, null, () => new { utcTicks = ChartSchedule.ResolveLocal(item.Item2, zone, item.Item3).UtcTicks }, item.Item4);
            var spans = new[] { new ScheduleSpan("s-a", 0, 4, "Alpha"), new ScheduleSpan("s-b", 2, 6, "Alpha"), new ScheduleSpan("s-c", 6, 8, "Alpha"), new ScheduleSpan("s-d", 1, 5, "Beta") };
            foreach (var input in new[] { spans, Array.Empty<ScheduleSpan>(), new[] { new ScheduleSpan("z", 0, 0, "A"), new ScheduleSpan("a", 0, 1, "A"), new ScheduleSpan("b", 1, 2, "A") } })
                Add("swimlane-" + cases.Count, "swimlane", input, null, () => { var s = ChartSchedule.Spans(input); var layout = ChartSchedule.Swimlanes(s); return new { layout.Lanes, layout.Minimum, layout.Maximum, placements = layout.Placements.Select(x => new { sourceIndex = x.Entry.Index, x.Entry.Id, x.LaneIndex, x.Lane, x.Track, x.TrackCount }), queries = new[] { (0d, 2d), (6d, 6d) }.Select(q => new { start = q.Item1, end = q.Item2, indices = s.QueryVisible(q.Item1, q.Item2).Entries.Select(x => x.Index) }) }; });
            var events = new[] { new ScheduleEvent("a", Epoch, Value: 1), new ScheduleEvent("b", Epoch.AddDays(2.5), Value: 2), new ScheduleEvent("c", Epoch.AddDays(5), Value: 3) };
            foreach (var period in new TimeSpan?[] { null, TimeSpan.FromDays(2.5) })
                Add("spiral-" + (period is null ? "turns" : "period"), "spiral", events.Select(x => new { x.Id, ticks = x.Date.UtcTicks, x.Value }), new { minimumTicks = Epoch.UtcTicks, maximumTicks = Epoch.AddDays(5).UtcTicks, turns = 1, innerRadius = .25, periodTicks = period?.Ticks }, () => { var r = ChartSchedule.Spiral(events, 1, .25, period, Epoch, Epoch.AddDays(5)); return new { r.Turns, r.InnerRadius, points = r.Points.Select(x => new { x.Entry.SourceIndex, x.Entry.Id, x.Fraction, x.Angle, x.Radius, position = Point(x.Position) }) }; });
            Add("spiral-invalid-turns", "invalid", new { turns = 65 }, null, () => ChartSchedule.Spiral(events, 65), "ArgumentOutOfRangeException");
            Operational(cases);
            var values = new[] { 8d, 6, 4, 2, 2 }; var parents = new[] { -1, 0, 1, 1, 0 };
            foreach (var family in new[] { "flame", "icicle", "indented", "radial", "dendrogram" })
                foreach (var state in new[] { "baseline", "collapsed", "drilled" })
                {
                    var root = state == "drilled" ? 1 : -1; var collapsed = state == "collapsed" ? new HashSet<int> { 1 } : [];
                    var config = new { rootIndex = root, collapsed = collapsed.ToArray(), indent = .1, startAngle = -90d, sweepAngle = 360d, inclusive = family == "flame", useMergeHeights = false };
                    Add(family + "-" + state, family, new { values, parents }, config, () => { var snapshot = Tree(values, parents); object layout = family switch { "flame" => ChartHierarchy.Flame(snapshot, root, collapsed), "icicle" => ChartHierarchy.Icicle(snapshot, root, collapsed), "indented" => ChartHierarchy.Indented(snapshot, root, collapsed, .1), "radial" => ChartHierarchy.RadialTree(snapshot, root, collapsed), _ => ChartHierarchy.Dendrogram(snapshot, root, collapsed) }; return HierarchyResult(snapshot, layout); });
                }
            Add("dendrogram-explicit", "dendrogram", new { values, parents }, new { rootIndex = -1, collapsed = Array.Empty<int>(), useMergeHeights = true }, () => { var s = Tree(values, parents); return HierarchyResult(s, ChartHierarchy.Dendrogram(s, useMergeHeights: true)); });
            Add("flame-invalid-inclusive", "invalid", new { values = new[] { 1d, 2, 2 }, parents = new[] { -1, 0, 0 } }, null, () => ChartHierarchy.Flame(Tree([1, 2, 2], [-1, 0, 0])), "ArgumentException");
            Add("dendrogram-invalid-height", "invalid", new { values = new[] { 1d, 2 }, parents = new[] { -1, 0 } }, null, () => ChartHierarchy.Dendrogram(Tree([1, 2], [-1, 0]), useMergeHeights: true), "ArgumentException");
            Add("hierarchy-cancel", "invalid", null, null, () => ChartHierarchy.Icicle(Tree(values, parents), cancellationToken: cancelled.Token), "OperationCanceledException");
            Maps(cases);
            var largeTasks = Enumerable.Range(0, 100000).Select(i => new ScheduleTask("s" + i, Epoch.AddTicks(i * 100L), Epoch.AddTicks(i * 100L + (i % 19) * 50L))).ToArray();
            var largeQueries = Enumerable.Range(0, 32).Select(i => (Epoch.UtcTicks + i * 300001L, Epoch.UtcTicks + i * 300001L + 777L)).ToArray();
            performance.Add(Measure("schedule-100000", "schedule-scale", new { count = 100000, epochTicks = Epoch.UtcTicks, startStep = 100, endPeriod = 19, endStep = 50, queryCount = 32, queryStep = 300001, queryWidth = 777 }, null, () => ScheduleResult(ChartSchedule.Snapshot(largeTasks), largeQueries)));
            var largeMatrix = Enumerable.Range(0, 20000).Select(i => new MatrixPoint("m" + i, i / 200, i % 200, (i * 17 % 997) - 400)).ToArray();
            performance.Add(Measure("matrix-20000", "matrix-scale", new { count = 20000, columns = 200, multiplier = 17, modulus = 997, shift = -400 }, null, () => ChartOperational.Matrix(largeMatrix)));
            var report = new { schemaVersion = "charts-c6-completion-numeric-v1", startedUtc = started, finishedUtc = DateTimeOffset.UtcNow, framework = RuntimeInformation.FrameworkDescription, operatingSystem = RuntimeInformation.OSDescription, productAssemblySha256 = FileSha(typeof(ChartSchedule).Assembly.Location), producerAssemblySha256 = FileSha(Assembly.GetExecutingAssembly().Location), cases, performance, producerCompleted = true, mathematicalAcceptanceEvaluated = false, formalSlaEvaluated = false };
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions)); Console.WriteLine($"Recorded {cases.Count} fixed cases and {performance.Count} bounded scale scenarios: {path}"); return 0;
        }
        catch (Exception error) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(new { producerCompleted = false, cases, performance, error = error.ToString() }, JsonOptions)); Console.Error.WriteLine(error); return 1; }
    }
    private static string FileSha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Operational(List<Capture> cases)
    {
        void Add(string id, string kind, object? input, object? config, Func<object> action, string? expected = null) => cases.Add(Measure(id, kind, input, config, action, expected));
        foreach (var input in new[] { new[] { new BulletPoint("a", 25, 75, 0, 100, [40, 70, 100]) }, new[] { new BulletPoint("a", -50, 200, 0, 100) }, Array.Empty<BulletPoint>() }) Add("bullet-" + cases.Count, "bullet", input, null, () => ChartOperational.Bullets(input));
        foreach (var input in new[] { new[] { new KpiPoint("a", 120, 100, [80, 100, 120]), new KpiPoint("b", 5, 0), new KpiPoint("c", -80, -100) }, Array.Empty<KpiPoint>() }) Add("kpi-" + cases.Count, "kpi", input, null, () => ChartOperational.Kpis(input));
        var dates = new[] { new CalendarPoint("a", new(2026, 1, 5), 1), new CalendarPoint("b", new(2026, 1, 8), 2), new CalendarPoint("c", new(2026, 1, 18), 4), new CalendarPoint("outside", new(2026, 2, 1), 8) };
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Sunday }) Add("calendar-" + day, "calendar", dates, new { firstDayOfWeek = (int)day, start = "2026-01-05", end = "2026-01-18" }, () => ChartOperational.Calendar(dates, day, new(2026, 1, 5), new(2026, 1, 18)));
        var matrix = new[] { new MatrixPoint("a", "A", "X", 1, "AX"), new MatrixPoint("b", "A", "Y", 2, "AY"), new MatrixPoint("c", "B", "X", 3, "BX"), new MatrixPoint("d", "B", "Y", 4, "BY") };
        Add("matrix-basic", "matrix", matrix, null, () => ChartOperational.Matrix(matrix));
        Add("matrix-duplicate", "invalid", null, null, () => ChartOperational.Matrix([matrix[0], matrix[0] with { Id = "other" }]), "ArgumentException");
        foreach (var weights in new[] { new[] { 1d, 3 }, new[] { 1d, 1, 1 }, new[] { 0d, 0 }, new[] { 1e308, 1e308 }, Array.Empty<double>() })
        { var input = weights.Select((x, i) => new WafflePoint("w" + i, x)).ToArray(); Add("waffle-" + cases.Count, "waffle", input, new { units = 16 }, () => ChartOperational.Waffle(input, 16)); }
        var parties = new[] { new ParliamentParty("a", 1), new ParliamentParty("b", 3) }; Add("parliament-basic", "parliament", parties, new { rows = 2, innerRadiusFactor = .5 }, () => ChartOperational.Parliament(parties, 2, .5));
        var ranks = new[] { new RankingPoint("a", "A", "P1", 10), new RankingPoint("b", "B", "P1", 20), new RankingPoint("c", "A", "P2", 30), new RankingPoint("d", "B", "P2", 15) };
        Add("bump-basic", "ranking", ranks, new { rankFromValues = true, slope = false }, () => ChartOperational.Ranking(ranks));
        Add("slope-basic", "ranking", ranks, new { rankFromValues = true, slope = true }, () => ChartOperational.Ranking(ranks, slope: true));
        var ties = new[] { new RankingPoint("a", "A", "P1", 9), new RankingPoint("b", "B", "P1", 9), new RankingPoint("c", "C", "P1", 3), new RankingPoint("d", "A", "P2", 1) };
        Add("bump-ties-missing", "ranking", ties, new { rankFromValues = true, slope = false }, () => ChartOperational.Ranking(ties));
        var words = new[] { new WordPoint("a", "Alpha", 4), new WordPoint("b", "Beta", 1), new WordPoint("zero", "Zero", 0) };
        foreach (var width in new[] { 300d, 70d, 1d }) Add("words-width-" + width, "words", words, new { width, height = 100, minimumFontSize = 12, maximumFontSize = 24, measure = "UTF16-length*font/2,font" }, () => ChartOperational.Words(words, width, 100, (s, f) => (s.Length * f / 2, f), 12, 24));
        var river = new[] { new ThemeRiverPoint("a0", "A", "X", 1), new ThemeRiverPoint("a1", "A", "Y", 2), new ThemeRiverPoint("a2", "A", "Z", 3), new ThemeRiverPoint("b0", "B", "X", 3), new ThemeRiverPoint("b1", "B", "Y", 2), new ThemeRiverPoint("b2", "B", "Z", 1) };
        foreach (var baseline in Enum.GetValues<OperationalRiverBaseline>()) Add("river-" + baseline, "river", river, new { baseline = (int)baseline }, () => ChartOperational.River(river, baseline));
        Add("river-missing-slot", "invalid", null, null, () => ChartOperational.River(river.Take(5)), "ArgumentException");
        Add("waffle-negative", "invalid", null, null, () => ChartOperational.Waffle([new("a", -1)]), "ArgumentOutOfRangeException");
        Add("calendar-duplicate", "invalid", null, null, () => ChartOperational.Calendar([dates[0], dates[0] with { Id = "b" }]), "ArgumentException");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); Add("operational-cancel", "invalid", null, null, () => ChartOperational.Waffle([new("a", 1)], cancellationToken: cancellation.Token), "OperationCanceledException");
    }
    private static void Maps(List<Capture> cases)
    {
        void Add(string id, string kind, object? input, object? config, Func<object> action, string? expected = null) => cases.Add(Measure(id, kind, input, config, action, expected));
        foreach (var projection in Enum.GetValues<MapProjection>())
        {
            var points = new[] { new MapObservation("a", new(-20, 10), -2), new MapObservation("b", new(0, 0), 0), new MapObservation("c", new(20, -10), 3) };
            Add("markers-" + projection, "map-markers", points.Select(x => new { x.Id, x = x.Position.X, y = x.Position.Y, x.Value }), new { projection = projection.ToString() }, () => ChartMaps.Markers(points, projection).Points.Select(x => new { x.Entry.SourceIndex, x.Entry.Id, x.Entry.Observation.Value, position = Point(x.Position) }).ToArray());
            var connections = new[] { new MapConnection("east", new(170, 10), new(-170, 20), 1), new MapConnection("west", new(-170, -10), new(170, -20), 2), new MapConnection("same-meridian", new(180, -10), new(-180, 10), 0) };
            Add("connections-" + projection, "map-connections", connections, new { projection = projection.ToString() }, () => ChartMaps.Connections(connections, projection).Connections.Select(x => new { x.Entry.SourceIndex, x.Entry.Id, x.Entry.Connection.Value, segments = x.Segments.Select(s => new { start = Point(s.Start), end = Point(s.End) }) }).ToArray());
            var pies = new[] { new MapPie("pie", new(-10, 10), [new(1, "A"), new(0, "Zero"), new(3, "B")]), new MapPie("empty", new(20, -10), [new(0, "Z")]) };
            Add("pies-" + projection, "map-pies", pies, new { projection = projection.ToString() }, () => ChartMaps.Pies(pies, projection).Slices.Select(x => new { x.Entry.SourceIndex, x.Entry.Id, x.SliceIndex, x.Slice.Value, x.StartAngle, x.SweepAngle, position = Point(x.Position) }).ToArray());
        }
        MapVector[] vectors = [new MapLineVector("line", new(-25, 15), new(-15, 15)), new MapPolylineVector("polyline", [new(-5, 12), new(0, 18), new(5, 12)]), new MapPolygonVector("polygon", new([new(15, 12), new(25, 12), new(20, 18), new(15, 12)])), new MapCircleVector("circle", new(-20, -12), 10), new MapArcVector("arc", new(15, -12), 12, 0, 180)];
        var vectorInput = vectors.Select(v => v switch { MapLineVector l => (object)new { kind = "line", l.Id, start = l.Start, end = l.End }, MapPolylineVector p => new { kind = "polyline", p.Id, p.Points }, MapPolygonVector p => new { kind = "polygon", p.Id, outer = p.Polygon.Outer, holes = p.Polygon.Holes }, MapCircleVector c => new { kind = "circle", c.Id, c.Center, c.Radius }, MapArcVector a => new { kind = "arc", a.Id, a.Center, a.Radius, a.StartAngle, a.SweepAngle }, _ => throw new InvalidOperationException() }).ToArray();
        Add("vectors-five", "map-vectors", vectorInput, new { projection = "Identity" }, () => ChartMaps.Vectors(vectors, MapProjection.Identity).Vectors.Select(x => new { x.Entry.SourceIndex, x.Entry.Id, type = x.Entry.Vector.GetType().Name, segments = x.Segments.Select(s => new { start = Point(s.Start), end = Point(s.End) }), polygons = x.Polygons.Select(p => new { outer = p.Outer.Select(Point), holes = p.Holes.Select(h => h.Select(Point)) }), center = x.Center is { } p ? Point(p) : null }).ToArray());
        Add("map-pies-negative", "invalid", null, null, () => ChartMaps.Pies([new("a", new(0, 0), [new(-1)])], MapProjection.Identity), "ArgumentException");
        Add("map-connections-negative", "invalid", null, null, () => ChartMaps.Connections([new("a", new(0, 0), new(1, 1), -1)], MapProjection.Identity), "ArgumentException");
        Add("map-vector-radius", "invalid", new { radius = 0 }, null, () => ChartMaps.Vectors([new MapCircleVector("a", new(0, 0), 0)], MapProjection.Identity), "ArgumentOutOfRangeException");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); Add("map-overlay-cancel", "invalid", null, null, () => ChartMaps.Markers([], MapProjection.Identity, cancellation.Token), "OperationCanceledException");
    }
}
