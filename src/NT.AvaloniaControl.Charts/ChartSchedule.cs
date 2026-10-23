namespace NT.AvaloniaControl.Charts;

public enum ScheduleDependencyType { FinishStart, StartStart, FinishFinish, StartFinish }
public enum ScheduleAmbiguousTime { Reject, Earlier, Later }
public enum ScheduleDiagnosticKind { MissingPredecessor, SelfDependency, DuplicateDependency, Cycle, ConstraintViolation }
public sealed record ScheduleDependency(object PredecessorId, ScheduleDependencyType Type = ScheduleDependencyType.FinishStart, TimeSpan Lag = default);
public sealed record ScheduleDiagnostic(ScheduleDiagnosticKind Kind, object TaskId, object? PredecessorId, string Message);
/// <summary>UTC-normalized task. Progress is a percentage from zero to one hundred; zero duration means a milestone.</summary>
public sealed record ScheduleTask(object Id, DateTimeOffset Start, DateTimeOffset End, double Progress = 0,
    string? Lane = null, string? Label = null, object? Source = null, IReadOnlyList<ScheduleDependency>? Dependencies = null);
public sealed record ScheduleEvent(object Id, DateTimeOffset Date, string? Label = null, double Value = 1, object? Source = null, string? Description = null);
/// <summary>A resource interval on a caller-defined finite numeric scale, matching the documented swimlane scale.</summary>
public sealed record ScheduleSpan(object Id, double Start, double End, string? Lane = null, string? Label = null, object? Source = null);
public sealed record ScheduleTaskEntry(int Index, ScheduleTask Task)
{ public int SourceIndex => Index; public object Id => Task.Id; public object Source => Task.Source ?? Task; public DateTimeOffset Start => Task.Start; public DateTimeOffset End => Task.End; public bool IsMilestone => Start == End; }
public sealed record ScheduleEventEntry(int Index, ScheduleEvent Event)
{ public int SourceIndex => Index; public object Id => Event.Id; public object Source => Event.Source ?? Event; public DateTimeOffset Date => Event.Date; }
public sealed record ScheduleSpanEntry(int Index, ScheduleSpan Span)
{ public int SourceIndex => Index; public object Id => Span.Id; public object Source => Span.Source ?? Span; }
public sealed record ScheduleVisibleTasks(IReadOnlyList<ScheduleTaskEntry> Entries, int VisitedNodes);
public sealed record ScheduleVisibleSpans(IReadOnlyList<ScheduleSpanEntry> Entries, int VisitedNodes);
public sealed record ScheduleLanePlacement(ScheduleSpanEntry Entry, int LaneIndex, string Lane, int Track, int TrackCount);
public sealed record ScheduleSwimlaneLayout(IReadOnlyList<ScheduleLanePlacement> Placements, IReadOnlyList<string> Lanes, double Minimum, double Maximum);
public sealed record ScheduleSpiralPoint(ScheduleEventEntry Entry, double Fraction, double Angle, double Radius, ChartPoint Position);
public sealed record ScheduleSpiralLayout(IReadOnlyList<ScheduleSpiralPoint> Points, DateTimeOffset Minimum, DateTimeOffset Maximum, double Turns, double InnerRadius);

/// <summary>Immutable task data with an augmented interval index. View queries retain tasks crossing either boundary.</summary>
public sealed class ScheduleSnapshot
{
    private readonly ScheduleIntervalIndex<ScheduleTaskEntry, long> _index;
    internal ScheduleSnapshot(IReadOnlyList<ScheduleTaskEntry> tasks, IReadOnlyList<ScheduleDiagnostic> diagnostics, CancellationToken token)
    { Tasks = tasks; Diagnostics = diagnostics; Minimum = tasks.Count == 0 ? null : tasks.Min(e => e.Start); Maximum = tasks.Count == 0 ? null : tasks.Max(e => e.End); _index = new(tasks, e => e.Start.UtcTicks, e => e.End.UtcTicks, token); }
    public IReadOnlyList<ScheduleTaskEntry> Tasks { get; }
    public IReadOnlyList<ScheduleDiagnostic> Diagnostics { get; }
    public DateTimeOffset? Minimum { get; }
    public DateTimeOffset? Maximum { get; }
    public ScheduleVisibleTasks QueryVisible(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    { if (end < start) throw new ArgumentException("Visible end must not precede start."); var result = _index.Query(start.UtcTicks, end.UtcTicks, cancellationToken); return new(result.Items, result.Visited); }
}
public sealed class ScheduleSpanSnapshot
{
    private readonly ScheduleIntervalIndex<ScheduleSpanEntry, double> _index;
    internal ScheduleSpanSnapshot(IReadOnlyList<ScheduleSpanEntry> spans, CancellationToken token)
    { Spans = spans; _index = new(spans, e => e.Span.Start, e => e.Span.End, token); }
    public IReadOnlyList<ScheduleSpanEntry> Spans { get; }
    public ScheduleVisibleSpans QueryVisible(double start, double end, CancellationToken cancellationToken = default)
    { if (!double.IsFinite(start) || !double.IsFinite(end) || end < start) throw new ArgumentException("A visible span needs finite ordered endpoints."); var result = _index.Query(start, end, cancellationToken); return new(result.Items, result.Visited); }
}

public static partial class ChartSchedule
{
    public const int MaximumItemCount = 100_000;
    public const int MaximumDependencyCount = 200_000;
    public const int MaximumLabelLength = 2048;
    /// <summary>Creates a bounded immutable snapshot. Dependency problems are explicit diagnostics; invalid task values reject the snapshot.</summary>
    public static ScheduleSnapshot Snapshot(IEnumerable<ScheduleTask> tasks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks); var entries = new List<ScheduleTaskEntry>(); var ids = new HashSet<object>(); var dependencies = 0;
        foreach (var task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested(); Limit(entries.Count);
            if (task is null || task.Id is null || !ids.Add(task.Id)) throw new ArgumentException("Tasks need nonnull unique IDs.", nameof(tasks));
            if (task.End < task.Start || !double.IsFinite(task.Progress) || task.Progress is < 0 or > 100) throw new ArgumentException("Task intervals must be ordered and progress must be in [0,100].", nameof(tasks));
            Text(task.Label); Text(task.Lane);
            var copy = new List<ScheduleDependency>();
            if (task.Dependencies is { } links) foreach (var link in links)
            {
                cancellationToken.ThrowIfCancellationRequested(); if (++dependencies > MaximumDependencyCount) throw new ArgumentOutOfRangeException(nameof(tasks), "Dependency budget exceeded.");
                if (link is null || link.PredecessorId is null || !Enum.IsDefined(link.Type)) throw new ArgumentException("Dependencies need an ID and a supported type.", nameof(tasks)); copy.Add(link);
            }
            entries.Add(new(entries.Count, task with { Start = task.Start.ToUniversalTime(), End = task.End.ToUniversalTime(), Source = task.Source ?? task, Dependencies = copy.AsReadOnly() }));
        }
        return new(entries.AsReadOnly(), Diagnose(entries, cancellationToken), cancellationToken);
    }
    public static IReadOnlyList<ScheduleEventEntry> Events(IEnumerable<ScheduleEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events); var result = new List<ScheduleEventEntry>(); var ids = new HashSet<object>();
        foreach (var item in events)
        { cancellationToken.ThrowIfCancellationRequested(); Limit(result.Count); if (item is null || item.Id is null || !ids.Add(item.Id) || !double.IsFinite(item.Value) || item.Value < 0) throw new ArgumentException("Events need unique IDs and finite nonnegative values.", nameof(events)); Text(item.Label); Text(item.Description); result.Add(new(result.Count, item with { Date = item.Date.ToUniversalTime(), Source = item.Source ?? item })); }
        return Array.AsReadOnly(result.OrderBy(e => e.Date).ThenBy(e => e.Index).ToArray());
    }
    public static ScheduleSpanSnapshot Spans(IEnumerable<ScheduleSpan> spans, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spans); var result = new List<ScheduleSpanEntry>(); var ids = new HashSet<object>();
        foreach (var item in spans)
        { cancellationToken.ThrowIfCancellationRequested(); Limit(result.Count); if (item is null || item.Id is null || !ids.Add(item.Id) || !double.IsFinite(item.Start) || !double.IsFinite(item.End) || item.End < item.Start) throw new ArgumentException("Spans need unique IDs and finite ordered endpoints.", nameof(spans)); Text(item.Label); Text(item.Lane); result.Add(new(result.Count, item)); }
        return new(result.AsReadOnly(), cancellationToken);
    }
    public static DateTimeOffset ResolveLocal(DateTime local, TimeZoneInfo timeZone, ScheduleAmbiguousTime ambiguous = ScheduleAmbiguousTime.Reject)
    {
        ArgumentNullException.ThrowIfNull(timeZone); if (!Enum.IsDefined(ambiguous)) throw new ArgumentOutOfRangeException(nameof(ambiguous));
        if (local.Kind == DateTimeKind.Utc) return new(local, TimeSpan.Zero);
        if (local.Kind == DateTimeKind.Local) throw new ArgumentException("Use DateTimeOffset, UTC, or an explicitly unspecified wall time; machine-local DateTime is not deterministic.", nameof(local));
        if (timeZone.IsInvalidTime(local)) throw new ArgumentException("The local wall time falls in a daylight-saving gap.", nameof(local));
        var offset = timeZone.GetUtcOffset(local);
        if (timeZone.IsAmbiguousTime(local))
        { if (ambiguous == ScheduleAmbiguousTime.Reject) throw new ArgumentException("The local wall time is ambiguous; choose Earlier or Later.", nameof(local)); var offsets = timeZone.GetAmbiguousTimeOffsets(local); offset = ambiguous == ScheduleAmbiguousTime.Earlier ? offsets.Max() : offsets.Min(); }
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
    internal static void Limit(int current) { if (current >= MaximumItemCount) throw new ArgumentOutOfRangeException("items", "Scheduling item budget exceeded."); }
    private static void Text(string? value) { if (value?.Length > MaximumLabelLength) throw new ArgumentOutOfRangeException(nameof(value), "Scheduling text budget exceeded."); }
}

internal sealed class ScheduleIntervalIndex<T, TValue> where TValue : struct, IComparable<TValue>
{
    private readonly T[] _items; private readonly TValue[] _starts, _ends, _maxEnd;
    public ScheduleIntervalIndex(IEnumerable<T> items, Func<T, TValue> start, Func<T, TValue> end, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); _items = items.Select((item, i) => (item, i)).OrderBy(e => start(e.item)).ThenBy(e => e.i).Select(e => e.item).ToArray();
        _starts = new TValue[_items.Length]; _ends = new TValue[_items.Length]; _maxEnd = new TValue[_items.Length];
        for (var i = 0; i < _items.Length; i++) { token.ThrowIfCancellationRequested(); _starts[i] = start(_items[i]); _ends[i] = end(_items[i]); }
        Build(0, _items.Length - 1);
        TValue? Build(int lo, int hi) { if (lo > hi) return null; var m = lo + (hi - lo) / 2; var value = _ends[m]; if (Build(lo, m - 1) is { } left && left.CompareTo(value) > 0) value = left; if (Build(m + 1, hi) is { } right && right.CompareTo(value) > 0) value = right; return _maxEnd[m] = value; }
    }
    public (IReadOnlyList<T> Items, int Visited) Query(TValue start, TValue end, CancellationToken token)
    {
        var result = new List<T>(); var visited = 0; token.ThrowIfCancellationRequested(); Search(0, _items.Length - 1); return (result.AsReadOnly(), visited);
        void Search(int lo, int hi)
        { if (lo > hi) return; token.ThrowIfCancellationRequested(); var m = lo + (hi - lo) / 2; visited++; if (_maxEnd[m].CompareTo(start) < 0) return; Search(lo, m - 1); if (_starts[m].CompareTo(end) > 0) return; if (_ends[m].CompareTo(start) >= 0) result.Add(_items[m]); Search(m + 1, hi); }
    }
}
