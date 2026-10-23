using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class HierarchyChartTests
{
    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Every_family_preserves_original_nested_sources_and_drill_expand_depth_navigation(int kind)
    {
        var a = new Node("a", 6); var b = new Node("b", 4); var group = new Node("g", 999, a, b); var other = new Node("other", 3);
        var chart = Make(kind); chart.ItemsSource = new[] { group, other }; chart.IdPath = "Id"; chart.Gap = 0;
        using var host = new Host(chart); Assert.Null(chart.DataError); Assert.Equal(4, chart.Snapshot!.Nodes.Count);
        Assert.Equal(10, chart.Snapshot.Nodes[0].AggregateValue); Assert.Same(a, chart.Snapshot.Nodes[1].Source);
        var mark = Assert.Single(chart.GetMarks(), m => Equals(m.Key, "a")); Assert.Equal("a", mark.Members[0].StableId); Assert.Same(a, mark.Members[0].Item); Assert.Equal(0, mark.Members[0].SourceIndex);
        Assert.Equal("a", chart.HitTest(mark.Position)!.Mark.Key);
        chart.Collapse("g"); Assert.DoesNotContain(chart.GetMarks(), m => Equals(m.Key, "a")); Assert.False(chart.IsExpanded("g")); Assert.Contains(chart.GetMarks(), m => Equals(m.Key, "g"));
        chart.Expand("g"); Assert.Contains(chart.GetMarks(), m => Equals(m.Key, "b"));
        Assert.True(chart.DrillDown("g")); Assert.Equal("g", chart.DrillRootId); Assert.Equal(new[] { "g" }, chart.DrillPath.Select(n => n.Id)); Assert.DoesNotContain(chart.GetMarks(), m => Equals(m.Key, "other")); Assert.False(chart.DrillDown("a"));
        chart.MaximumVisibleDepth = 0; Assert.Single(chart.GetMarks()); chart.MaximumVisibleDepth = 64;
        Assert.True(chart.DrillUp()); Assert.Null(chart.DrillRootId); Assert.False(chart.DrillUp()); Assert.True(chart.DrillDown("g")); chart.ResetDrill(); Assert.Null(chart.DrillRootId);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Selection_follows_id_through_replacement_reorder_viewport_and_nested_changes(int kind)
    {
        var a = new Node("a", 6); var b = new Node("b", 4); var group = new Node("g", 0, a, b); var roots = new ObservableCollection<Node> { group };
        var chart = Make(kind); chart.ItemsSource = roots; chart.IdPath = "Id"; using var host = new Host(chart);
        chart.SelectedIndex = chart.GetMarks().Select((m, i) => (m, i)).Single(x => Equals(x.m.Key, "b")).i;
        group.Children.Move(1, 0); host.Drain(); Assert.Same(b, chart.SelectedPoint!.Members[0].Item); Assert.Equal(0, chart.SelectedPoint.Members[0].SourceIndex);
        var replacement = new Node("b", 9); group.Children[0] = replacement; host.Drain(); Assert.Same(replacement, chart.SelectedPoint!.Members[0].Item); Assert.Equal(9, chart.SelectedPoint.Mark.Value);
        chart.Zoom = 1.2; chart.PanX = .01; chart.PanY = -.01; host.Resize(520, 320); Assert.Same(replacement, chart.SelectedPoint!.Members[0].Item);
        replacement.Value = 7; host.Drain(); Assert.Equal(7, chart.SelectedPoint!.Mark.Value);
        group.Children.Remove(replacement); host.Drain(); Assert.Null(chart.SelectedPoint); Assert.Equal(-1, chart.SelectedIndex);
    }

    [AvaloniaFact]
    public void No_id_equal_records_are_distinct_reference_sources_and_typed_ids_keep_the_real_source()
    {
        var a = new EqualNode(5); var b = new EqualNode(5); var rows = new ObservableCollection<EqualNode> { a, b };
        var chart = new BubbleCloudChart { ItemsSource = rows }; using var host = new Host(chart); chart.SelectedIndex = 1;
        rows.Move(1, 0); host.Drain(); Assert.Same(b, chart.SelectedPoint!.Members[0].Item); Assert.Same(b, chart.SelectedPoint.Members[0].StableId); Assert.Equal(0, chart.SelectedIndex);
        var actual = new object(); chart.ItemsSource = new[] { new HierarchyNode(3, label: "typed", source: actual, id: "x") }; var mark = Assert.Single(chart.GetMarks()); Assert.Same(actual, mark.Members[0].Item); Assert.Equal("x", mark.Members[0].StableId);
        chart.SelectedIndex = 0; var newer = new object(); chart.ItemsSource = new[] { new HierarchyNode(7, source: newer, id: new string('x', 1)) }; Assert.Same(newer, chart.SelectedPoint!.Members[0].Item);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Invalid_nested_values_cycles_and_duplicate_ids_clear_and_recover_with_observers_released(int kind)
    {
        var leaf = new Node("a", -1); var root = new Node("g", 0, leaf); var chart = Make(kind); chart.ItemsSource = new[] { root }; chart.IdPath = "Id";
        using var host = new Host(chart); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); Assert.Null(chart.Snapshot); Assert.True(leaf.Observers > 0);
        leaf.Value = 3; host.Drain(); Assert.Null(chart.DataError); Assert.NotEmpty(chart.GetMarks());
        root.Children.Add(root); host.Drain(); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); root.Children.Remove(root); host.Drain(); Assert.Null(chart.DataError);
        root.Children.Add(new Node("a", 2)); host.Drain(); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); root.Children.RemoveAt(1); host.Drain(); Assert.Null(chart.DataError);
        host.Detach(); Assert.Equal(0, leaf.Observers); Assert.Equal(0, root.Observers); host.Attach(); Assert.True(leaf.Observers > 0);
    }

    [AvaloniaFact]
    public void Path_mapping_observes_nested_owners_and_labels_are_clipped_to_the_current_shape()
    {
        var leaf = new Mapped(new Payload(8), new ObservableCollection<Mapped>(), "leaf", "long label that must be clipped");
        var root = new Mapped(new Payload(999), new ObservableCollection<Mapped> { leaf }, "root", "branch");
        var chart = new TreemapChart { ItemsSource = new[] { root }, ValuePath = "Payload.Amount", ChildrenPath = "Parts", IdPath = "Key", LabelPath = "Caption" };
        using var host = new Host(chart); Assert.Equal(8, chart.Snapshot!.Nodes[0].AggregateValue); leaf.Payload.Amount = 12; host.Drain(); Assert.Equal(12, chart.Snapshot!.Nodes[0].AggregateValue);
        var group = Record(chart); Assert.NotEmpty(group.Children); root.Parts.Clear(); host.Drain(); Assert.Equal(999, chart.Snapshot!.Nodes[0].AggregateValue);
    }

    [AvaloniaFact]
    public void Sunburst_analytic_hits_respect_holes_arc_edges_and_zoom_in_headless()
    {
        var chart = new SunburstChart { ItemsSource = new[] { new HierarchyNode(1, label: "a", id: "a"), new HierarchyNode(1, label: "b", id: "b") }, InnerRadius = .2, SweepAngle = 180, Gap = 0, StartAngle = 0 };
        using var host = new Host(chart); Assert.Null(chart.HitTest(chart.PlotArea.Center)); Assert.Null(chart.HitTest(new Point(210, 40)));
        foreach (var mark in chart.GetMarks()) Assert.Equal(mark.Key, chart.HitTest(mark.Position)!.Mark.Key);
        chart.Zoom = 1.2; chart.PanX = -.02; foreach (var mark in chart.GetMarks()) if (chart.PlotArea.Contains(mark.Position)) Assert.Equal(mark.Key, chart.HitTest(mark.Position)!.Mark.Key);
    }

    [AvaloniaFact]
    public void Organization_orientation_changes_nodes_and_connector_gaps_remain_nonselectable()
    {
        var chart = new OrganizationChart { ItemsSource = new[] { new HierarchyNode(children: new[] { new HierarchyNode(1), new HierarchyNode(2) }) }, Gap = 0 };
        using var host = new Host(chart); var parent = chart.GetMarks()[0]; var child = chart.GetMarks()[1]; Assert.True(parent.Bounds.Bottom < child.Bounds.Top);
        Assert.Null(chart.HitTest(new Point(parent.Position.X, (parent.Bounds.Bottom + child.Bounds.Top) / 2)));
        chart.Orientation = Orientation.Horizontal; Assert.True(chart.GetMarks()[0].Bounds.Right < chart.GetMarks()[1].Bounds.Left); Assert.NotEmpty(Record(chart).Children);
    }

    [AvaloniaFact]
    public void Zero_and_empty_forests_have_no_invented_area_but_organization_can_display_zero_nodes()
    {
        foreach (var kind in new[] { 0, 1, 3, 4, 5 }) { var chart = Make(kind); chart.ItemsSource = new[] { new HierarchyNode(0) }; using var host = new Host(chart); Assert.Empty(chart.GetMarks()); Assert.Single(chart.Snapshot!.Nodes); chart.ItemsSource = Array.Empty<HierarchyNode>(); Assert.Empty(chart.GetMarks()); Assert.Null(chart.DataError); }
        var tree = new OrganizationChart { ItemsSource = new[] { new HierarchyNode(0) } }; using var treeHost = new Host(tree); Assert.Single(tree.GetMarks());
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task Each_family_retains_keyboard_raster_failure_and_cancellation_contracts(int kind)
    {
        var chart = Make(kind); chart.ItemsSource = new[] { new HierarchyNode(3, label: "leaf", id: "leaf") }; using var host = new Host(chart);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home }); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space }); Assert.Equal(0, chart.SelectedIndex);
        using var stream = new MemoryStream(); var failed = 0; chart.ExportFailed += (_, _) => failed++; Assert.False(chart.TryExportPng(stream, new ChartExportOptions { Dpi = 0 })); Assert.Equal(1, failed);
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.TryExportPngAsync(stream, cancellationToken: cancel.Token)); Assert.Equal(1, failed);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Assert.Equal(-1, chart.SelectedIndex);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Animation_delay_pause_data_and_view_frames_remain_hittable_and_targets_immutable(int kind)
    {
        var clock = new Clock(); var a = new Node("a", 4); var b = new Node("b", 8); var chart = Make(kind); chart.ItemsSource = new[] { a, b }; chart.IdPath = "Id";
        chart.AnimationTimeProvider = clock; chart.AnimationDuration = TimeSpan.FromSeconds(1); chart.AnimationDelay = TimeSpan.FromMilliseconds(100); chart.AnimationEasing = null; chart.AnimationTriggers = ChartAnimationTriggers.All;
        using var host = new Host(chart); Assert.Equal(ChartAnimationState.Delayed, chart.AnimationState); clock.Advance(600); chart.UpdateAnimationFrame(); var frame = chart.GetMarks(); Near(.5, chart.AnimationProgress); Assert.NotEmpty(frame);
        foreach (var mark in frame) Assert.Equal(mark.Key, chart.HitTest(mark.Position)!.Mark.Key);
        chart.PauseAnimations(); var widths = chart.GetMarks().Select(m => m.Bounds).ToArray(); clock.Advance(1000); chart.UpdateAnimationFrame(); Assert.Equal(widths, chart.GetMarks().Select(m => m.Bounds)); chart.ResumeAnimations(); clock.Advance(500); chart.UpdateAnimationFrame(); Assert.Equal(ChartAnimationState.Idle, chart.AnimationState);
        var old = chart.GetMarks().Select(m => m.Bounds).ToArray(); chart.Zoom = 1.5; _ = chart.GetMarks(); clock.Advance(600); chart.UpdateAnimationFrame(); Assert.Equal(ChartAnimationState.Running, chart.AnimationState); Assert.NotEqual(old, chart.GetMarks().Select(m => m.Bounds).ToArray());
        chart.CancelAnimations(); Assert.Equal(ChartAnimationState.Idle, chart.AnimationState); a.Value = 12;
        // A tree's numeric value alone leaves its geometry unchanged. A new child exercises its data-layout transition.
        if (kind == 2) a.Children.Add(new Node("child", 2));
        _ = chart.GetMarks(); clock.Advance(600); chart.UpdateAnimationFrame(); Assert.Equal(ChartAnimationState.Running, chart.AnimationState);
        chart.ReduceMotion = true; Assert.Equal(ChartAnimationState.Idle, chart.AnimationState); Assert.Equal(12, chart.Snapshot!.Nodes[0].Value); Assert.Equal(1, chart.AnimationProgress);
    }

    [AvaloniaFact]
    public void Drill_collapse_and_removed_nodes_animate_without_hitting_stale_sources()
    {
        var clock = new Clock(); var root = new Node("g", 0, new Node("a", 4), new Node("b", 6)); var rows = new ObservableCollection<Node> { root, new("other", 4) };
        var chart = new TreemapChart { ItemsSource = rows, IdPath = "Id", AnimationTimeProvider = clock, AnimationDuration = TimeSpan.FromSeconds(1), AnimationEasing = null, AnimationTriggers = ChartAnimationTriggers.Viewport | ChartAnimationTriggers.Data };
        using var host = new Host(chart); chart.Collapse("g"); _ = chart.GetMarks(); clock.Advance(500); chart.UpdateAnimationFrame(); Assert.Equal(ChartAnimationState.Running, chart.AnimationState); Assert.DoesNotContain(chart.HitTest(chart.GetMarks().First(m => Equals(m.Key, "g")).Position)!.Members, member => member.Item is Node { Id: "a" });
        chart.CancelAnimations(); chart.Expand("g"); chart.CancelAnimations(); Assert.True(chart.DrillDown("g")); _ = chart.GetMarks(); clock.Advance(500); chart.UpdateAnimationFrame(); Assert.Equal(ChartAnimationState.Running, chart.AnimationState); chart.CancelAnimations();
        rows.Remove(root); host.Drain(); chart.CancelAnimations(); Assert.Null(chart.DrillRootId); Assert.DoesNotContain(chart.GetMarks(), m => Equals(m.Key, "g")); Assert.Single(chart.GetMarks());
    }

    [AvaloniaFact]
    public void Removed_animated_nodes_are_excluded_from_keyboard_and_explicit_selection()
    {
        var clock = new Clock(); var rows = new ObservableCollection<Node> { new("a", 4), new("b", 8) };
        var chart = new PackedBubbleChart { ItemsSource = rows, IdPath = "Id", AnimationTimeProvider = clock, AnimationTriggers = ChartAnimationTriggers.Data, AnimationDuration = TimeSpan.FromSeconds(1), AnimationEasing = null };
        using var host = new Host(chart); rows.RemoveAt(1); _ = chart.GetMarks(); clock.Advance(500); chart.UpdateAnimationFrame(); Assert.Equal(ChartAnimationState.Running, chart.AnimationState); Assert.Single(chart.GetMarks());
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End }); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space }); Assert.Equal("a", chart.ActivePoint!.Mark.Key); Assert.Equal("a", chart.SelectedPoint!.Mark.Key);
        chart.SelectedIndex = 1; Assert.Null(chart.SelectedPoint); Assert.Equal(-1, chart.SelectedIndex); Assert.NotEmpty(Record(chart).Children);
    }

    [AvaloniaFact]
    public void Reentrant_nested_value_getter_cannot_overwrite_a_newer_source_revision()
    {
        var chart = new TreemapChart(); var child = new ReadCallbackNode(3); var old = new MixedBranch(new ObservableCollection<object> { child }); var latest = new Node("latest", 9);
        child.OnRead = () => chart.ItemsSource = new[] { latest }; chart.ItemsSource = new[] { old };
        using var host = new Host(chart); host.Drain(); Assert.Null(chart.DataError); Assert.Same(latest, Assert.Single(chart.Snapshot!.Nodes).Source); Assert.Same(latest, Assert.Single(chart.GetMarks()).Members[0].Item);
    }

    [AvaloniaFact]
    public void Reentrant_subscription_accessor_releases_superseded_sources_and_new_source_stays_observed()
    {
        var chart = new TreemapChart(); var child = new SubscribeNode(3); var latest = new Node("latest", 9);
        child.OnSubscribe = () => chart.ItemsSource = new[] { latest }; chart.ItemsSource = new[] { new MixedBranch(new ObservableCollection<object> { child }) };
        using var host = new Host(chart); host.Drain(); Assert.Equal(0, child.Observers); Assert.Same(latest, Assert.Single(chart.Snapshot!.Nodes).Source);
        latest.Value = 7; host.Drain(); Assert.Equal(7, Assert.Single(chart.GetMarks()).Value); host.Detach(); Assert.Equal(0, latest.Observers);
    }

    [AvaloniaFact]
    public void Narrow_sunburst_rings_with_gaps_never_invert_their_hit_band()
    {
        var root = new HierarchyNode(1, id: "leaf"); for (var i = 0; i < 64; i++) root = new HierarchyNode(children: new[] { root }, id: $"depth-{i}");
        var chart = new SunburstChart { ItemsSource = new[] { root } }; using var host = new Host(chart); var marks = chart.GetMarks(); Assert.Equal(65, marks.Count);
        foreach (var mark in marks) Assert.Equal(mark.Key, chart.HitTest(mark.Position)!.Mark.Key);
    }

    [AvaloniaFact]
    public void Organization_branch_boxes_render_labels_as_well_as_terminal_labels()
    {
        var chart = new OrganizationChart { ItemsSource = new[] { new HierarchyNode(children: new[] { new HierarchyNode(1, label: "A"), new HierarchyNode(2, label: "B") }, label: "CEO") }, ShowLabels = true };
        using var host = new Host(chart); Assert.Equal(3, Drawings(Record(chart)).OfType<GlyphRunDrawing>().Count());
    }

    [AvaloniaFact]
    public void Collapsed_state_releases_removed_source_subtrees_after_replacement()
    {
        var first = new Node("first", 0, new Node("a", 1)); var chart = new CirclePackingChart { ItemsSource = new[] { first } }; using var host = new Host(chart); chart.Collapse(first); _ = chart.GetMarks();
        chart.ItemsSource = new[] { new Node("next", 0, new Node("b", 2)) }; _ = chart.GetMarks(); chart.ItemsSource = new[] { first }; Assert.Equal(2, chart.GetMarks().Count); Assert.True(chart.IsExpanded(first));
    }

    [AvaloniaFact]
    public void Animation_notifications_coalesce_against_last_published_state_without_losing_running_transition()
    {
        var clock = new Clock(); var node = new Node("a", 4); var rows = new ObservableCollection<Node> { node, new("b", 7) };
        var chart = new TreemapChart { ItemsSource = rows, IdPath = "Id", AnimationTimeProvider = clock, AnimationTriggers = ChartAnimationTriggers.Data, AnimationDuration = TimeSpan.FromSeconds(1), AnimationEasing = null };
        using var host = new Host(chart); var states = new List<ChartAnimationState>(); chart.PropertyChanged += (_, e) => { if (e.Property == HierarchyChartBase.AnimationStateProperty) states.Add(chart.AnimationState); };
        node.Value = 8; _ = chart.GetMarks(); clock.Advance(500); chart.UpdateAnimationFrame(); Assert.Equal(ChartAnimationState.Running, chart.AnimationState); Dispatcher.UIThread.RunJobs(); Assert.Contains(ChartAnimationState.Running, states);
        chart.CancelAnimations(); Dispatcher.UIThread.RunJobs(); Assert.Equal(ChartAnimationState.Idle, states.Last());
    }

    [AvaloniaFact]
    public void Root_enumeration_stops_at_the_hierarchy_budget_before_generic_engineering_mapping()
    {
        var read = 0; IEnumerable<HierarchyNode> Endless() { while (true) { read++; yield return new HierarchyNode(1); } }
        var chart = new TreemapChart { ItemsSource = Endless() }; using var host = new Host(chart); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); Assert.InRange(read, ChartHierarchy.MaximumCount, ChartHierarchy.MaximumCount * 4 + 4);
        chart.ItemsSource = new[] { new HierarchyNode(1) }; Assert.Single(chart.GetMarks()); Assert.Null(chart.DataError);
    }

    [AvaloniaTheory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void Nonfinite_explicit_branch_values_are_rejected_even_when_children_have_valid_totals(double invalid)
    {
        var branch = new Node("g", invalid, new Node("a", 3)); var chart = new TreemapChart { ItemsSource = new[] { branch }, IdPath = "Id" };
        using var host = new Host(chart); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); branch.Value = 0; host.Drain(); Assert.Null(chart.DataError); Assert.Equal(3, chart.Snapshot!.Nodes[0].AggregateValue);
    }

    [AvaloniaFact]
    public void Keyboard_and_double_click_expose_drill_collapse_and_back_navigation()
    {
        var root = new HierarchyNode(children: new[] { new HierarchyNode(3, id: "leaf") }, label: "Root", id: "root");
        var chart = new OrganizationChart { ItemsSource = new[] { root } }; using var host = new Host(chart);
        host.DoubleClick(chart.GetMarks()[0].Position); Assert.Equal("root", chart.DrillRootId);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home });
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Subtract }); Assert.Single(chart.GetMarks()); Assert.False(chart.IsExpanded("root"));
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Add }); Assert.Equal(2, chart.GetMarks().Count);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Back }); Assert.Null(chart.DrillRootId);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Assert.Equal("root", chart.DrillRootId);
        chart.Zoom = 2; chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Assert.Null(chart.DrillRootId); Assert.Equal(1, chart.Zoom);
    }

    [AvaloniaFact]
    public void Nested_visual_spacing_exposes_treemap_ancestors_and_keeps_packed_children_inside_parents()
    {
        var root = new HierarchyNode(children: new[] { new HierarchyNode(children: new[] { new HierarchyNode(3, id: "leaf") }, id: "middle") }, id: "root");
        var chart = new TreemapChart { ItemsSource = new[] { root }, Gap = 3 }; using var host = new Host(chart); var marks = chart.GetMarks();
        Assert.Equal("root", chart.HitTest(new Point(marks[0].Bounds.Left + .5, marks[0].Bounds.Center.Y))!.Mark.Key); Assert.True(marks[0].Bounds.Contains(marks[1].Bounds)); Assert.True(marks[1].Bounds.Contains(marks[2].Bounds));
        var circles = new CirclePackingChart { ItemsSource = new[] { new HierarchyNode(children: Enumerable.Range(0, 12).Select(i => new HierarchyNode(i + 1, id: i)), id: "root") }, Gap = 4096 };
        using var other = new Host(circles); var ring = circles.GetMarks()[0]; foreach (var child in circles.GetMarks().Skip(1)) { var dx = child.Position.X - ring.Position.X; var dy = child.Position.Y - ring.Position.Y; var d = Math.Sqrt(dx * dx + dy * dy); Assert.True(d + child.Bounds.Width / 2 <= ring.Bounds.Width / 2 + 1e-8); }
    }

    private static HierarchyChartBase Make(int kind) => kind switch { 0 => new TreemapChart(), 1 => new SunburstChart(), 2 => new OrganizationChart(), 3 => new BubbleCloudChart(), 4 => new PackedBubbleChart(), _ => new CirclePackingChart() };
    private static DrawingGroup Record(EngineeringChartBase chart) { var group = new DrawingGroup(); using var context = group.Open(); chart.Render(context); return group; }
    private static IEnumerable<Drawing> Drawings(Drawing drawing) { yield return drawing; if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var nested in Drawings(child)) yield return nested; }
    private static void Near(double expected, double actual) => Assert.True(Math.Abs(expected - actual) < 1e-7, $"{expected:R} != {actual:R}");
    private sealed class Clock : TimeProvider { private long _ticks; public override long TimestampFrequency => 1000; public override long GetTimestamp() => _ticks; public void Advance(int milliseconds) => _ticks += milliseconds; }
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly EngineeringChartBase _chart;
        public Host(EngineeringChartBase chart) { _chart = chart; _window = new Window { Width = 420, Height = 280, Content = chart }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); _ = _chart.GetMarks(); }
        public void Resize(double width, double height) { _window.Width = width; _window.Height = height; Drain(); }
        public void Detach() { _window.Content = null; Drain(); }
        public void Attach() { _window.Content = _chart; Drain(); }
        public void DoubleClick(Point point) { _window.MouseDown(new Point(5, 5), MouseButton.Left); _window.MouseUp(new Point(5, 5), MouseButton.Left); for (var i = 0; i < 2; i++) { _window.MouseDown(point, MouseButton.Left); _window.MouseUp(point, MouseButton.Left); Drain(); } }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private sealed class Node(string id, double value, params Node[] children) : INotifyPropertyChanged
    {
        private double _value = value; private PropertyChangedEventHandler? _changed;
        public string Id { get; } = id; public string Label => Id; public ObservableCollection<Node> Children { get; } = new(children); public int Observers { get; private set; }
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }
    private sealed record EqualNode(double Value);
    private sealed record Mapped(Payload Payload, ObservableCollection<Mapped> Parts, string Key, string Caption);
    private sealed class Payload(double amount) : INotifyPropertyChanged { private double _amount = amount; public double Amount { get => _amount; set { _amount = value; PropertyChanged?.Invoke(this, new(nameof(Amount))); } } public event PropertyChangedEventHandler? PropertyChanged; }
    private sealed class MixedBranch(ObservableCollection<object> children) { public double Value => 0; public ObservableCollection<object> Children { get; } = children; }
    private sealed class ReadCallbackNode(double value) { public Action? OnRead { get; set; } public double Value { get { var callback = OnRead; OnRead = null; callback?.Invoke(); return value; } } }
    private sealed class SubscribeNode(double value) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed; public int Observers { get; private set; } public Action? OnSubscribe { get; set; } public double Value => value;
        public event PropertyChangedEventHandler? PropertyChanged { add { var callback = OnSubscribe; OnSubscribe = null; callback?.Invoke(); _changed += value; Observers++; } remove { _changed -= value; Observers--; } }
    }
}
