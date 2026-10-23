using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ScheduleMathTests
{
    private static DateTimeOffset Day(int day) => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(day);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Task_snapshot_owns_dependency_arrays_normalizes_UTC_and_preserves_source_identity()
    {
        var dependency = new List<ScheduleDependency> { new("a") }; var source = new object();
        var a = new ScheduleTask("a", Day(0).ToOffset(TimeSpan.FromHours(8)), Day(1), Source: source);
        var b = new ScheduleTask("b", Day(1), Day(2), 35, Dependencies: dependency);
        var rows = new List<ScheduleTask> { b, a }; var snapshot = ChartSchedule.Snapshot(rows, Token); dependency.Clear(); rows.Clear();
        Assert.Equal(2, snapshot.Tasks.Count); Assert.Equal("b", snapshot.Tasks[0].Id); Assert.Same(b, snapshot.Tasks[0].Source); Assert.Same(source, snapshot.Tasks[1].Source); Assert.Equal(TimeSpan.Zero, snapshot.Tasks[1].Start.Offset);
        Assert.Single(snapshot.Tasks[0].Task.Dependencies!); Assert.Empty(snapshot.Diagnostics); Assert.Throws<NotSupportedException>(() => ((IList<ScheduleTaskEntry>)snapshot.Tasks).Clear());
    }

    [Theory]
    [InlineData(-1)] [InlineData(101)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void Invalid_progress_rejects_snapshot(double progress) => Assert.Throws<ArgumentException>(() => ChartSchedule.Snapshot([new("a", Day(0), Day(1), progress)], Token));

    [Fact]
    public void Invalid_ids_dates_numbers_and_text_reject_before_layout()
    {
        Assert.Throws<ArgumentException>(() => ChartSchedule.Snapshot([new("a", Day(2), Day(1))], Token));
        Assert.Throws<ArgumentException>(() => ChartSchedule.Snapshot([new("a", Day(0), Day(1)), new("a", Day(0), Day(1))], Token));
        Assert.Throws<ArgumentException>(() => ChartSchedule.Events([new(null!, Day(0))], Token));
        Assert.Throws<ArgumentException>(() => ChartSchedule.Events([new("a", Day(0), Value: -1)], Token));
        Assert.Throws<ArgumentException>(() => ChartSchedule.Spans([new("a", double.NaN, 1)], Token));
        Assert.Throws<ArgumentException>(() => ChartSchedule.Spans([new("a", 2, 1)], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartSchedule.Snapshot([new("a", Day(0), Day(1), Label: new string('x', ChartSchedule.MaximumLabelLength + 1))], Token));
    }

    [Fact]
    public void Task_interval_index_matches_bruteforce_and_keeps_crossing_boundary_and_exact_ticks()
    {
        var random = new Random(170); var rows = Enumerable.Range(0, 500).Select(i => { var start = random.Next(0, 100); return new ScheduleTask(i, Day(start), Day(start + random.Next(0, 30))); }).ToArray();
        var snapshot = ChartSchedule.Snapshot(rows, Token);
        for (var lo = 0; lo < 100; lo += 7)
        { var end = lo + 3; var expected = rows.Select((row, i) => (row, i)).Where(e => e.row.Start <= Day(end) && e.row.End >= Day(lo)).OrderBy(e => e.row.Start).ThenBy(e => e.i).Select(e => e.i); Assert.Equal(expected, snapshot.QueryVisible(Day(lo), Day(end), Token).Entries.Select(e => e.SourceIndex)); }
        var instant = Day(0).AddTicks(1); var exact = ChartSchedule.Snapshot([new("early", instant.AddTicks(-1), instant.AddTicks(-1)), new("exact", instant, instant), new("late", instant.AddTicks(1), instant.AddTicks(1))], Token);
        Assert.Equal("exact", Assert.Single(exact.QueryVisible(instant, instant, Token).Entries).Id);
    }

    [Fact]
    public void Task_index_sparse_query_is_sublinear_and_data_budget_is_not_a_performance_claim()
    {
        var snapshot = ChartSchedule.Snapshot(Enumerable.Range(0, 100_000).Select(i => new ScheduleTask(i, Day(0).AddMinutes(i * 2), Day(0).AddMinutes(i * 2 + 1))), Token);
        var query = snapshot.QueryVisible(Day(0).AddMinutes(100_000), Day(0).AddMinutes(100_001), Token); Assert.Single(query.Entries); Assert.InRange(query.VisitedNodes, 1, 100);
        var span = ChartSchedule.Spans(Enumerable.Range(0, 100_000).Select(i => new ScheduleSpan(i, i * 2, i * 2 + 1)), Token); var visible = span.QueryVisible(100_000, 100_001, Token); Assert.Single(visible.Entries); Assert.InRange(visible.VisitedNodes, 1, 100);
    }

    [Fact]
    public void Budget_and_cancellation_stop_unbounded_sources_and_queries()
    {
        var read = 0; IEnumerable<ScheduleTask> Infinite() { for (var i = 0; ; i++) { read++; yield return new(i, Day(0), Day(1)); } }
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartSchedule.Snapshot(Infinite(), Token)); Assert.Equal(ChartSchedule.MaximumItemCount + 1, read);
        using var canceled = new CancellationTokenSource(); canceled.Cancel(); Assert.ThrowsAny<OperationCanceledException>(() => ChartSchedule.Events([new("a", Day(0))], canceled.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => ChartSchedule.Snapshot([new("a", Day(0), Day(1))], canceled.Token));
        var snapshot = ChartSchedule.Snapshot([new("a", Day(0), Day(1))], Token); Assert.ThrowsAny<OperationCanceledException>(() => snapshot.QueryVisible(Day(0), Day(1), canceled.Token));
    }

    [Fact]
    public void Dependency_diagnostics_distinguish_missing_duplicate_self_constraints_and_only_actual_cycles()
    {
        var rows = new[] {
            new ScheduleTask("a", Day(0), Day(2), Dependencies: [new("b"), new("absent"), new("a"), new("b")]),
            new ScheduleTask("b", Day(0), Day(2), Dependencies: [new("a")]),
            new ScheduleTask("downstream", Day(3), Day(4), Dependencies: [new("b")]) };
        var diagnostics = ChartSchedule.Snapshot(rows, Token).Diagnostics;
        Assert.Contains(diagnostics, d => d.Kind == ScheduleDiagnosticKind.MissingPredecessor); Assert.Contains(diagnostics, d => d.Kind == ScheduleDiagnosticKind.DuplicateDependency); Assert.Contains(diagnostics, d => d.Kind == ScheduleDiagnosticKind.SelfDependency); Assert.Contains(diagnostics, d => d.Kind == ScheduleDiagnosticKind.ConstraintViolation);
        Assert.Equal(new[] { "a", "b" }, diagnostics.Where(d => d.Kind == ScheduleDiagnosticKind.Cycle).Select(d => d.TaskId).Cast<string>().Order());
    }

    [Theory]
    [InlineData(ScheduleDependencyType.FinishStart, 2, 3)] [InlineData(ScheduleDependencyType.StartStart, 0, 3)]
    [InlineData(ScheduleDependencyType.FinishFinish, 0, 2)] [InlineData(ScheduleDependencyType.StartFinish, 0, 0)]
    public void All_dependency_types_use_the_correct_anchors(ScheduleDependencyType type, int start, int end)
    { var snapshot = ChartSchedule.Snapshot([new("a", Day(0), Day(2)), new("b", Day(start), Day(end), Dependencies: [new("a", type)])], Token); Assert.Empty(snapshot.Diagnostics); }

    [Fact]
    public void Dependency_lag_overflow_and_very_long_chains_are_safe()
    {
        var extreme = ChartSchedule.Snapshot([new("a", DateTimeOffset.MinValue, DateTimeOffset.MaxValue), new("b", DateTimeOffset.MaxValue, DateTimeOffset.MaxValue, Dependencies: [new("a", Lag: TimeSpan.MaxValue)])], Token); Assert.Contains(extreme.Diagnostics, d => d.Kind == ScheduleDiagnosticKind.ConstraintViolation);
        var chain = ChartSchedule.Snapshot(Enumerable.Range(0, 10_000).Select(i => new ScheduleTask(i, Day(0).AddMinutes(i), Day(0).AddMinutes(i + 1), Dependencies: i == 0 ? [] : [new(i - 1)])), Token); Assert.Empty(chain.Diagnostics);
    }

    [Fact]
    public void Explicit_timezone_rules_handle_DST_gap_fold_and_reject_machine_local_kind()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Assert.Throws<ArgumentException>(() => ChartSchedule.ResolveLocal(new(2026, 3, 8, 2, 30, 0, DateTimeKind.Unspecified), zone));
        var fold = new DateTime(2026, 11, 1, 1, 30, 0, DateTimeKind.Unspecified); Assert.Throws<ArgumentException>(() => ChartSchedule.ResolveLocal(fold, zone));
        var earlier = ChartSchedule.ResolveLocal(fold, zone, ScheduleAmbiguousTime.Earlier); var later = ChartSchedule.ResolveLocal(fold, zone, ScheduleAmbiguousTime.Later); Assert.Equal(TimeSpan.FromHours(1), later - earlier); Assert.Equal(5, earlier.Hour); Assert.Equal(6, later.Hour);
        Assert.Throws<ArgumentException>(() => ChartSchedule.ResolveLocal(DateTime.SpecifyKind(fold, DateTimeKind.Local), zone));
    }

    [Fact]
    public void Swimlane_overlap_tracks_are_minimal_stable_and_preserve_first_seen_lane_order()
    {
        var rows = new[] { new ScheduleSpan("a", 0, 4, "Beta"), new ScheduleSpan("b", 1, 3, "Beta"), new ScheduleSpan("c", 3, 5, "Beta"), new ScheduleSpan("d", 4, 6, "Beta"), new ScheduleSpan("e", -1, 0, "Alpha") };
        var layout = ChartSchedule.Swimlanes(ChartSchedule.Spans(rows, Token), Token); Assert.Equal(new[] { "Beta", "Alpha" }, layout.Lanes); Assert.All(layout.Placements.Where(p => p.Lane == "Beta"), p => Assert.Equal(2, p.TrackCount));
        Assert.Equal(new[] { 0, 1, 1, 0 }, layout.Placements.Where(p => p.Lane == "Beta").Select(p => p.Track)); Assert.Same(rows[2], layout.Placements[2].Entry.Source);
    }

    [Fact]
    public void Event_sort_is_stable_with_original_indices_and_spiral_period_preserves_phase()
    {
        var original = new object(); var rows = new[] { new ScheduleEvent("last", Day(4)), new ScheduleEvent("first", Day(0), Source: original), new ScheduleEvent("same", Day(0)), new ScheduleEvent("half", Day(1)) };
        var entries = ChartSchedule.Events(rows, Token); Assert.Equal(new[] { 1, 2, 3, 0 }, entries.Select(e => e.SourceIndex)); Assert.Same(original, entries[0].Source);
        var spiral = ChartSchedule.Spiral(rows, period: TimeSpan.FromDays(2), minimum: Day(0), maximum: Day(4), cancellationToken: Token); Assert.Equal(2, spiral.Turns); var first = spiral.Points.Single(p => Equals(p.Entry.Id, "first")); var half = spiral.Points.Single(p => Equals(p.Entry.Id, "half")); Assert.InRange(Math.Abs((half.Angle - first.Angle) - Math.PI), 0, 1e-12);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartSchedule.Spiral(rows, period: TimeSpan.FromTicks(1), cancellationToken: Token));
    }
}
