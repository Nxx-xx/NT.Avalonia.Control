using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ScheduleChartTests
{
    private static DateTimeOffset Day(int day) => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(day);

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Every_mode_preserves_business_sources_keys_hit_selection_keyboard_and_export_validation(int mode)
    {
        using var chart = Make(mode); using var host = new Host(chart); var marks = chart.GetMarks(); Assert.Null(chart.DataError); Assert.Equal(3, marks.Count);
        foreach (var mark in marks) { Assert.Single(mark.Members); Assert.NotNull(mark.Members[0].Item); Assert.Equal(mark.Key, mark.Members[0].StableId); Assert.True(double.IsFinite(mark.Position.X) && double.IsFinite(mark.Position.Y)); }
        var first = marks[1]; Assert.True(chart.SelectItem(first.Key!)); var source = chart.SelectedPoint!.Members[0].Item; Assert.NotNull(chart.HitTest(first.Position));
        chart.Zoom = 1.1; chart.PanX = .01; chart.PanY = 0; host.Resize(520, 360); Assert.Same(source, chart.SelectedPoint!.Members[0].Item);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End }); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space }); Assert.NotNull(chart.SelectedPoint);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Assert.Equal(1, chart.Zoom); Assert.Equal(-1, chart.SelectedIndex);
        using var output = new MemoryStream(); Assert.False(chart.TryExportPng(output, new ChartExportOptions { Dpi = 0 }));
    }

    [AvaloniaFact]
    public void Gantt_progress_milestones_and_all_dependency_anchor_types_are_nonfabricated()
    {
        var original = new object(); using var chart = new GanttChart { ItemsSource = new[] { new ScheduleTask("a", Day(0), Day(2), 50, Source: original), new ScheduleTask("b", Day(2), Day(4), 75, Dependencies: [new("a"), new("a", ScheduleDependencyType.StartStart), new("a", ScheduleDependencyType.FinishFinish), new("a", ScheduleDependencyType.StartFinish)]), new ScheduleTask("m", Day(5), Day(5)) } }; using var host = new Host(chart);
        Assert.Equal(3, chart.GetMarks().Count); Assert.Equal(4, chart.DependencyPaths.Count); Assert.Same(original, chart.GetMarks()[0].Members[0].Item); Assert.True(chart.Snapshot!.Tasks[2].IsMilestone);
        var group = new DrawingGroup(); using (var context = group.Open()) chart.Render(context); Assert.NotEmpty(group.Children); chart.ShowDependencies = false; Assert.Empty(chart.DependencyPaths); Assert.Equal(3, chart.GetMarks().Count);
        var milestone = chart.GetMarks()[2]; Assert.Null(chart.HitTest(new(milestone.Bounds.Left + .1, milestone.Bounds.Top + .1))); Assert.Equal("m", chart.HitTest(milestone.Position)!.Mark.Key);
    }

    [AvaloniaFact]
    public void Time_filter_keeps_cross_boundary_tasks_and_original_indices_without_copying_source_on_view_changes()
    {
        var source = new CountingRows([new ScheduleTask("cross", Day(0), Day(8)), new ScheduleTask("before", Day(0), Day(1)), new ScheduleTask("after", Day(8), Day(9)), new ScheduleTask("middle", Day(3), Day(4))]);
        using var chart = new GanttChart { ItemsSource = source, VisibleStart = Day(2), VisibleEnd = Day(5), RowHeight = 30 }; using var host = new Host(chart); Assert.Equal(new[] { "cross", "middle" }, chart.GetMarks().Select(m => (string)m.Key!)); Assert.Equal(new[] { 0, 3 }, chart.GetMarks().Select(m => m.Members[0].SourceIndex));
        var reads = source.Enumerations; chart.Zoom = 1.25; chart.PanX = .02; chart.PanY = .01; host.Resize(520, 320); _ = chart.GetMarks(); Assert.Equal(reads, source.Enumerations);
        chart.VisibleEnd = Day(1); Assert.NotNull(chart.DataError); Assert.Empty(chart.GetMarks()); chart.VisibleEnd = Day(5); Assert.Null(chart.DataError); Assert.NotEmpty(chart.GetMarks());
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void DTO_INPC_INCC_replace_detach_and_dispose_release_observers_and_recover_errors(int mode)
    {
        var a = new Row("a", Day(0), Day(2)); var b = new Row("b", Day(3), Day(4)); var rows = new ObservableCollection<Row> { a, b };
        using var chart = MakeEmpty(mode); chart.ItemsSource = rows; using var host = new Host(chart); Assert.Equal(2, chart.GetMarks().Count); Assert.True(a.Subscribers > 0); Assert.True(chart.SelectItem("a"));
        rows.Move(0, 1); Assert.Equal("a", chart.SelectedPoint!.Mark.Key); Assert.Equal(1, chart.SelectedPoint.Members[0].SourceIndex);
        if (mode == 0) a.Progress = -1; else if (mode == 1) a.StartNumber = double.NaN; else a.Value = -1;
        Assert.NotNull(chart.DataError); Assert.Empty(chart.GetMarks());
        if (mode == 0) a.Progress = 35; else if (mode == 1) a.StartNumber = 0; else a.Value = 2;
        Assert.Null(chart.DataError); Assert.Equal(2, chart.GetMarks().Count);
        a.Brush = "invalid-color"; Assert.NotNull(chart.DataError); a.Brush = "#2563EB"; Assert.Null(chart.DataError);
        rows.Remove(a); Assert.Single(chart.GetMarks()); Assert.Equal(0, a.Subscribers);
        chart.ItemsSource = new[] { a }; Assert.Single(chart.GetMarks()); Assert.Equal(0, b.Subscribers);
        host.Detach(); Assert.Equal(0, a.Subscribers); host.Attach(); Assert.True(a.Subscribers > 0); chart.Dispose(); Assert.Equal(0, a.Subscribers); Assert.Empty(chart.GetMarks());
    }

    [AvaloniaFact]
    public void Selected_index_binding_is_TwoWay_and_reorder_follows_ID_instead_of_new_ordinal()
    {
        var vm = new SelectionModel(); using var chart = Make(0); chart.Bind(EngineeringChartBase.SelectedIndexProperty, new Binding(nameof(SelectionModel.SelectedIndex)) { Source = vm }); using var host = new Host(chart);
        vm.SelectedIndex = 1; Assert.Equal(1, chart.SelectedIndex); Assert.Equal("b", chart.SelectedPoint!.Mark.Key); Assert.True(chart.SelectItem("m")); Assert.Equal(2, vm.SelectedIndex);
    }

    [AvaloniaFact]
    public void Nested_mapping_observes_replaced_payload_and_unsubscribes_former_payload()
    {
        var old = new ProgressData(25); var row = new NestedRow(old); using var chart = new GanttChart { ItemsSource = new[] { row }, ProgressPath = "Data.Progress" }; using var host = new Host(chart);
        Assert.Equal(25d, Assert.Single(chart.GetMarks()).Value); var replacement = new ProgressData(75); row.Data = replacement; Assert.Equal(75d, Assert.Single(chart.GetMarks()).Value); Assert.Equal(0, old.Subscribers); replacement.Progress = 50; Assert.Equal(50d, Assert.Single(chart.GetMarks()).Value); host.Detach(); Assert.Equal(0, replacement.Subscribers);
    }

    [AvaloniaFact]
    public void Mutable_dependency_collection_rebuilds_diagnostics_after_mutation_and_reattach()
    {
        var links = new ObservableCollection<ScheduleDependency>(); var row = new DependencyRow(links); using var chart = new GanttChart { ItemsSource = new object[] { new ScheduleTask("a", Day(0), Day(1)), row } }; using var host = new Host(chart);
        Assert.Empty(chart.Snapshot!.Diagnostics); links.Add(new("missing")); Assert.Contains(chart.Snapshot!.Diagnostics, d => d.Kind == ScheduleDiagnosticKind.MissingPredecessor); links.Clear(); links.Add(new("a")); Assert.Empty(chart.Snapshot!.Diagnostics); Assert.Single(chart.DependencyPaths);
        host.Detach(); links.Add(new("missing")); host.Attach(); Assert.Contains(chart.Snapshot!.Diagnostics, d => d.Kind == ScheduleDiagnosticKind.MissingPredecessor);
    }

    [AvaloniaFact]
    public void Swimlane_numeric_overlap_track_geometry_and_brush_paths_follow_the_original_rows()
    {
        using var chart = new SwimlaneChart { ItemsSource = new[] { new ScheduleSpan("a", 0, 4, "one"), new ScheduleSpan("b", 1, 3, "one"), new ScheduleSpan("c", 3, 5, "one") } }; using var host = new Host(chart); var marks = chart.GetMarks();
        Assert.Equal(3, marks.Count); Assert.True(marks[0].Bounds.Bottom < marks[1].Bounds.Top); Assert.Equal(marks[1].Bounds.Y, marks[2].Bounds.Y); Assert.All(chart.SwimlaneLayout!.Placements, p => Assert.Equal(2, p.TrackCount)); Assert.Null(chart.HitTest(new(marks[0].Bounds.Left + .1, marks[0].Bounds.Top + .1)));
        chart.Minimum = 2; chart.Maximum = 4; Assert.Equal(3, chart.GetMarks().Count); Assert.All(chart.GetMarks(), m => Assert.InRange(m.Members[0].SourceIndex, 0, 2));
        chart.Maximum = 1; Assert.NotNull(chart.DataError); chart.Maximum = 4; Assert.Null(chart.DataError);
    }

    [AvaloniaTheory]
    [InlineData(Orientation.Horizontal)] [InlineData(Orientation.Vertical)]
    public void Timeline_collision_labels_are_inside_plot_nonoverlapping_and_overflow_is_explicit(Orientation orientation)
    {
        using var chart = new EventTimelineChart { Orientation = orientation, ItemsSource = Enumerable.Range(0, 200).Select(i => new ScheduleEvent(i, Day(0), $"Event {i}")) }; using var host = new Host(chart);
        var labels = chart.LabelPlacements.Where(l => l.IsVisible).ToArray(); Assert.NotEmpty(labels); Assert.True(chart.HiddenLabelCount > 0); Assert.Equal(200, chart.GetMarks().Count);
        for (var i = 0; i < labels.Length; i++) { Assert.True(chart.PlotArea.Contains(labels[i].Bounds.TopLeft)); Assert.True(chart.PlotArea.Contains(labels[i].Bounds.BottomRight)); for (var j = i + 1; j < labels.Length; j++) Assert.False(labels[i].Bounds.Intersects(labels[j].Bounds)); }
        var first = labels[0].Bounds; chart.Orientation = orientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal; Assert.NotEqual(first, chart.LabelPlacements.First(l => l.IsVisible).Bounds);
    }

    [AvaloniaFact]
    public void Spiral_explicit_period_and_source_indices_remain_consistent_after_filter_view_and_reorder()
    {
        var rows = new ObservableCollection<ScheduleEvent> { new("later", Day(4)), new("earlier", Day(0)), new("middle", Day(2)) }; using var chart = new SpiralTimelineChart { ItemsSource = rows, Period = TimeSpan.FromDays(2) }; using var host = new Host(chart);
        Assert.Equal(new[] { 1, 2, 0 }, chart.GetMarks().Select(m => m.Members[0].SourceIndex)); Assert.Equal(2, chart.SpiralLayout!.Turns); Assert.True(chart.SelectItem("middle")); rows.Move(2, 0); Assert.Equal("middle", chart.SelectedPoint!.Mark.Key); Assert.Equal(0, chart.SelectedPoint.Members[0].SourceIndex);
        chart.VisibleStart = Day(1); chart.VisibleEnd = Day(3); Assert.Equal("middle", Assert.Single(chart.GetMarks()).Key); Assert.Equal(1, chart.SpiralLayout!.Turns);
        chart.InnerRadius = 4096; Assert.NotNull(chart.DataError); chart.InnerRadius = 30; Assert.Null(chart.DataError);
    }

    [AvaloniaFact]
    public void UTC_extremes_single_instants_and_numeric_extremes_produce_finite_geometry()
    {
        using var gantt = new GanttChart { ItemsSource = new[] { new ScheduleTask("all", DateTimeOffset.MinValue, DateTimeOffset.MaxValue), new ScheduleTask("max", DateTimeOffset.MaxValue, DateTimeOffset.MaxValue) } }; using var host = new Host(gantt); Assert.Null(gantt.DataError); Assert.All(gantt.GetMarks(), m => Assert.True(double.IsFinite(m.Position.X) && double.IsFinite(m.Bounds.Width)));
        using var swimlane = new SwimlaneChart { ItemsSource = new[] { new ScheduleSpan("all", -double.MaxValue, double.MaxValue) } }; using var numericHost = new Host(swimlane); Assert.Null(swimlane.DataError); var mark = Assert.Single(swimlane.GetMarks()); Assert.True(double.IsFinite(mark.Bounds.Width)); Assert.True(double.IsFinite(mark.Value));
    }

    [AvaloniaFact]
    public void Lane_filter_changes_only_visible_members_without_reindexing_original_sources()
    {
        using var chart = new GanttChart { ItemsSource = new[] { new ScheduleTask("a", Day(0), Day(3), Lane: "Design"), new ScheduleTask("b", Day(1), Day(4), Lane: "Build") } }; using var host = new Host(chart);
        chart.LaneFilter = "Build"; var mark = Assert.Single(chart.GetMarks()); Assert.Equal("b", mark.Key); Assert.Equal(1, mark.Members[0].SourceIndex); chart.LaneFilter = "missing"; Assert.Empty(chart.GetMarks()); chart.LaneFilter = null; Assert.Equal(2, chart.GetMarks().Count);
    }

    private static ScheduleChartBase Make(int mode)
    {
        if (mode == 0) return new GanttChart { ItemsSource = new[] { new ScheduleTask("a", Day(0), Day(2), 40), new ScheduleTask("b", Day(2), Day(4), 60), new ScheduleTask("m", Day(5), Day(5)) } };
        if (mode == 1) return new SwimlaneChart { ItemsSource = new[] { new ScheduleSpan("a", 0, 3, "one"), new ScheduleSpan("b", 1, 4, "one"), new ScheduleSpan("m", 3, 5, "two") } };
        ScheduleChartBase chart = mode == 2 ? new EventTimelineChart() : new SpiralTimelineChart(); chart.ItemsSource = new[] { new ScheduleEvent("a", Day(0)), new ScheduleEvent("b", Day(2)), new ScheduleEvent("m", Day(5)) }; return chart;
    }
    private static ScheduleChartBase MakeEmpty(int mode)
    { ScheduleChartBase chart = mode switch { 0 => new GanttChart(), 1 => new SwimlaneChart(), 2 => new EventTimelineChart(), _ => new SpiralTimelineChart() }; if (mode == 1) { chart.StartPath = nameof(Row.StartNumber); chart.EndPath = nameof(Row.EndNumber); } return chart; }
    private sealed class Host : IDisposable
    {
        private readonly ScheduleChartBase _chart; private readonly Window _window;
        public Host(ScheduleChartBase chart) { _chart = chart; _window = new() { Content = chart, Width = 420, Height = 280 }; _window.Show(); Resize(420, 280); }
        public void Resize(double width, double height) { _chart.Measure(new(width, height)); _chart.Arrange(new(0, 0, width, height)); _ = _chart.GetMarks(); Dispatcher.UIThread.RunJobs(); }
        public void Detach() { _window.Content = null; Dispatcher.UIThread.RunJobs(); }
        public void Attach() { _window.Content = _chart; Resize(420, 280); }
        public void Dispose() { _window.Content = null; _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private sealed class CountingRows(object[] rows) : IEnumerable { public int Enumerations { get; private set; } public IEnumerator GetEnumerator() { Enumerations++; return rows.GetEnumerator(); } }
    private sealed class Row(string id, DateTimeOffset start, DateTimeOffset end) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed; private double _progress = 25, _value = 1, _startNumber; private string? _brush;
        public string Id => id; public string Label => id; public string Lane => "lane"; public DateTimeOffset Start => start; public DateTimeOffset End => end; public DateTimeOffset Date => start; public double EndNumber => 4;
        public double Progress { get => _progress; set { _progress = value; _changed?.Invoke(this, new(nameof(Progress))); } }
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public double StartNumber { get => _startNumber; set { _startNumber = value; _changed?.Invoke(this, new(nameof(StartNumber))); } }
        public string? Brush { get => _brush; set { _brush = value; _changed?.Invoke(this, new(nameof(Brush))); } }
        public int Subscribers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { _changed += value; Subscribers++; } remove { _changed -= value; Subscribers--; } }
    }
    private sealed class SelectionModel : INotifyPropertyChanged
    { private int _selected = -1; public int SelectedIndex { get => _selected; set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(SelectedIndex))); } } public event PropertyChangedEventHandler? PropertyChanged; }
    private sealed class NestedRow(ProgressData data) : INotifyPropertyChanged
    { public string Id => "nested"; public DateTimeOffset Start => Day(0); public DateTimeOffset End => Day(1); private ProgressData _data = data; public ProgressData Data { get => _data; set { _data = value; PropertyChanged?.Invoke(this, new(nameof(Data))); } } public event PropertyChangedEventHandler? PropertyChanged; }
    private sealed class ProgressData(double progress) : INotifyPropertyChanged
    { private double _progress = progress; private PropertyChangedEventHandler? _changed; public int Subscribers { get; private set; } public double Progress { get => _progress; set { _progress = value; _changed?.Invoke(this, new(nameof(Progress))); } } public event PropertyChangedEventHandler? PropertyChanged { add { _changed += value; Subscribers++; } remove { _changed -= value; Subscribers--; } } }
    private sealed record DependencyRow(ObservableCollection<ScheduleDependency> Dependencies)
    { public string Id => "b"; public DateTimeOffset Start => Day(1); public DateTimeOffset End => Day(2); }
}
