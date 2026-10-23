namespace NT.AvaloniaControl.Charts;

public static partial class ChartSchedule
{
    private static IReadOnlyList<ScheduleDiagnostic> Diagnose(IReadOnlyList<ScheduleTaskEntry> entries, CancellationToken token)
    {
        var diagnostics = new List<ScheduleDiagnostic>(); var byId = entries.ToDictionary(e => e.Id, e => e.Index);
        var forward = Enumerable.Range(0, entries.Count).Select(_ => new List<int>()).ToArray(); var reverse = Enumerable.Range(0, entries.Count).Select(_ => new List<int>()).ToArray();
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested(); var seen = new HashSet<(object, ScheduleDependencyType)>();
            foreach (var dependency in entry.Task.Dependencies!)
            {
                token.ThrowIfCancellationRequested();
                void Error(ScheduleDiagnosticKind kind, string message) => diagnostics.Add(new(kind, entry.Id, dependency.PredecessorId, message));
                if (!seen.Add((dependency.PredecessorId, dependency.Type))) { Error(ScheduleDiagnosticKind.DuplicateDependency, "Repeated predecessor/type dependency."); continue; }
                if (!byId.TryGetValue(dependency.PredecessorId, out var predecessor)) { Error(ScheduleDiagnosticKind.MissingPredecessor, "Predecessor is absent from this immutable snapshot."); continue; }
                if (predecessor == entry.Index) { Error(ScheduleDiagnosticKind.SelfDependency, "A task cannot depend on itself."); continue; }
                forward[predecessor].Add(entry.Index); reverse[entry.Index].Add(predecessor);
                var before = entries[predecessor];
                var anchor = dependency.Type is ScheduleDependencyType.FinishStart or ScheduleDependencyType.FinishFinish ? before.End : before.Start;
                var after = dependency.Type is ScheduleDependencyType.FinishStart or ScheduleDependencyType.StartStart ? entry.Start : entry.End;
                // Decimal prevents lag arithmetic from overflowing at the DateTimeOffset extremes.
                if ((decimal)after.UtcTicks < (decimal)anchor.UtcTicks + dependency.Lag.Ticks) Error(ScheduleDiagnosticKind.ConstraintViolation, "Task timing does not satisfy the predecessor anchor and lag.");
            }
        }
        // Iterative Kosaraju avoids recursion depth failures on long dependency chains.
        var visited = new bool[entries.Count]; var order = new List<int>(entries.Count);
        for (var root = 0; root < entries.Count; root++)
        {
            if (visited[root]) continue; var stack = new Stack<(int Node, int Next)>(); visited[root] = true; stack.Push((root, 0));
            while (stack.Count > 0)
            { token.ThrowIfCancellationRequested(); var frame = stack.Pop(); if (frame.Next == forward[frame.Node].Count) { order.Add(frame.Node); continue; } stack.Push((frame.Node, frame.Next + 1)); var next = forward[frame.Node][frame.Next]; if (!visited[next]) { visited[next] = true; stack.Push((next, 0)); } }
        }
        Array.Clear(visited);
        for (var i = order.Count - 1; i >= 0; i--)
        {
            var root = order[i]; if (visited[root]) continue; var component = new List<int>(); var stack = new Stack<int>(); stack.Push(root); visited[root] = true;
            while (stack.Count > 0) { token.ThrowIfCancellationRequested(); var node = stack.Pop(); component.Add(node); foreach (var next in reverse[node]) if (!visited[next]) { visited[next] = true; stack.Push(next); } }
            if (component.Count > 1) foreach (var node in component.Order()) diagnostics.Add(new(ScheduleDiagnosticKind.Cycle, entries[node].Id, null, "Task belongs to a dependency cycle; no automatic rescheduling is performed."));
        }
        return diagnostics.AsReadOnly();
    }

    /// <summary>Stable first-seen lane order and minimum nonoverlapping tracks. Touching endpoints may share a track.</summary>
    public static ScheduleSwimlaneLayout Swimlanes(ScheduleSpanSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot); var lanes = new List<string>(); var groups = new Dictionary<string, List<ScheduleSpanEntry>>(StringComparer.Ordinal);
        foreach (var entry in snapshot.Spans) { cancellationToken.ThrowIfCancellationRequested(); var lane = entry.Span.Lane ?? ""; if (!groups.TryGetValue(lane, out var group)) { lanes.Add(lane); groups.Add(lane, group = []); } group.Add(entry); }
        var result = new List<ScheduleLanePlacement>();
        for (var laneIndex = 0; laneIndex < lanes.Count; laneIndex++)
        {
            var lane = lanes[laneIndex]; var active = new PriorityQueue<int, (double End, int Track)>(); var free = new PriorityQueue<int, int>(); var placed = new List<(ScheduleSpanEntry Entry, int Track)>(); var count = 0;
            foreach (var entry in groups[lane].OrderBy(e => e.Span.Start).ThenBy(e => e.Index))
            {
                cancellationToken.ThrowIfCancellationRequested(); while (active.TryPeek(out var track, out var priority) && priority.End <= entry.Span.Start) { active.Dequeue(); free.Enqueue(track, track); }
                var selected = free.Count > 0 ? free.Dequeue() : count++; placed.Add((entry, selected)); active.Enqueue(selected, (entry.Span.End, selected));
            }
            foreach (var (entry, track) in placed) result.Add(new(entry, laneIndex, lane, track, count));
        }
        return new(result.AsReadOnly(), lanes.AsReadOnly(), snapshot.Spans.Count == 0 ? 0 : snapshot.Spans.Min(e => e.Span.Start), snapshot.Spans.Count == 0 ? 1 : snapshot.Spans.Max(e => e.Span.End));
    }

    /// <summary>Normalized Archimedean spiral. A full cycle is one turn; period can override the fit-to-range Turns setting.</summary>
    public static ScheduleSpiralLayout Spiral(IEnumerable<ScheduleEvent> events, double turns = 3, double innerRadius = .15,
        TimeSpan? period = null, DateTimeOffset? minimum = null, DateTimeOffset? maximum = null, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(turns) || turns <= 0 || turns > 64 || !double.IsFinite(innerRadius) || innerRadius is < 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(turns), "Turns must be in (0,64] and normalized inner radius in [0,1).");
        if (period is { Ticks: <= 0 }) throw new ArgumentOutOfRangeException(nameof(period));
        var entries = Events(events, cancellationToken); var min = (minimum ?? entries.FirstOrDefault()?.Date ?? DateTimeOffset.UnixEpoch).ToUniversalTime(); var max = (maximum ?? entries.LastOrDefault()?.Date ?? new DateTimeOffset(Math.Min(DateTimeOffset.MaxValue.UtcTicks, min.UtcTicks + TimeSpan.TicksPerDay), TimeSpan.Zero)).ToUniversalTime();
        if (max < min) throw new ArgumentException("Spiral bounds must be ordered.");
        var span = max.UtcTicks - min.UtcTicks; var effectiveTurns = period is { } cycle ? span / (double)cycle.Ticks : turns;
        if (effectiveTurns > 64) throw new ArgumentOutOfRangeException(nameof(period), "The requested period exceeds the 64-turn geometry budget.");
        var points = new List<ScheduleSpiralPoint>();
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested(); if (entry.Date < min || entry.Date > max) continue;
            var fraction = span == 0 ? .5 : (entry.Date.UtcTicks - min.UtcTicks) / (double)span; var angle = -Math.PI / 2 + fraction * effectiveTurns * Math.Tau; var radius = innerRadius + (1 - innerRadius) * fraction;
            points.Add(new(entry, fraction, angle, radius, new(radius * Math.Cos(angle), radius * Math.Sin(angle))));
        }
        return new(points.AsReadOnly(), min, max, effectiveTurns, innerRadius);
    }
}
