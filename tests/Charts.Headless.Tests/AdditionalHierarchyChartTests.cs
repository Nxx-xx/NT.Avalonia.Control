using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class AdditionalHierarchyChartTests
{
    private static HierarchySnapshot CostTree() => ChartHierarchy.Snapshot([new HierarchyNode(10,
        [new HierarchyNode(6, [new HierarchyNode(6, id: "a")], id: "left"), new HierarchyNode(2, id: "b")], id: "root")], cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public void Inclusive_flame_keeps_parent_self_cost_and_bottom_up_direction()
    {
        var tree = CostTree(); var layout = ChartHierarchy.Flame(tree, cancellationToken: TestContext.Current.CancellationToken); var root = layout.Rectangles[0]; var left = layout.Rectangles[1]; var leaf = layout.Rectangles[2]; var b = layout.Rectangles[3];
        Near(1, root.Width); Near(2d / 3, root.Y); Near(.6, left.Width); Near(.2, b.Width); Near(.6, b.X); Near(0, leaf.Y);
        Assert.Same(tree.Nodes[2], leaf.Entry); Assert.True(left.Y < root.Y);
    }

    [Fact]
    public void Leaf_total_icicle_partitions_only_terminal_cost_and_transposes_no_original_data()
    {
        var tree = CostTree(); var layout = ChartHierarchy.Icicle(tree, cancellationToken: TestContext.Current.CancellationToken); Near(0, layout.Rectangles[0].Y); Near(.75, layout.Rectangles[1].Width); Near(.25, layout.Rectangles[3].Width);
        Near(1d / 3, layout.Rectangles[1].Y); Near(2d / 3, layout.Rectangles[2].Y); Assert.Equal(10, tree.Nodes[0].Value); Assert.Equal(8, tree.Nodes[0].AggregateValue);
    }

    [Fact]
    public void Inclusive_cost_rejects_inconsistent_hidden_descendants_and_extreme_unrepresentable_width()
    {
        var invalid = ChartHierarchy.Snapshot([new HierarchyNode(5, [new HierarchyNode(6)])], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentException>(() => ChartHierarchy.Flame(invalid, collapsedIndices: new HashSet<int> { 0 }, cancellationToken: TestContext.Current.CancellationToken));
        var huge = ChartHierarchy.Snapshot([new HierarchyNode(double.MaxValue), new HierarchyNode(double.Epsilon)], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Flame(huge, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Icicle(CostTree(), valueMode: (HierarchyStackValueMode)99, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Indented_preorder_rows_retain_original_indices_and_collapse_changes_only_visible_rows()
    {
        var tree = CostTree(); var layout = ChartHierarchy.Indented(tree, indent: .1, cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(new[] { 0, 1, 2, 3 }, layout.Rectangles.Select(r => r.Entry.Index));
        Near(.2, layout.Rectangles[2].X); Near(.5, layout.Rectangles[2].Y); Near(.8, layout.Rectangles[2].Width);
        var collapsed = ChartHierarchy.Indented(tree, collapsedIndices: new HashSet<int> { 1 }, cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(new[] { 0, 1, 3 }, collapsed.Rectangles.Select(r => r.Entry.Index)); Near(1d / 3, collapsed.Rectangles[1].Height);
        Assert.Equal(new[] { 1, 2 }, ChartHierarchy.Indented(tree, rootIndex: 1, cancellationToken: TestContext.Current.CancellationToken).Rectangles.Select(r => r.Entry.Index));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Indented(tree, indent: double.NaN, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Dendrogram_explicit_merge_heights_have_known_positions_and_preserve_leaf_order()
    {
        var tree = ChartHierarchy.Snapshot([new HierarchyNode(10, [new HierarchyNode(4, [new HierarchyNode(0, id: "a"), new HierarchyNode(0, id: "b")]), new HierarchyNode(0, id: "c")])], cancellationToken: TestContext.Current.CancellationToken);
        var layout = ChartHierarchy.Dendrogram(tree, useMergeHeights: true, cancellationToken: TestContext.Current.CancellationToken); Near(10, layout.MaximumHeight); Near(.05, layout.Nodes[0].Position.Y); Near(.59, layout.Nodes[1].Position.Y);
        Near(.2, layout.Nodes[2].Position.X); Near(.5, layout.Nodes[3].Position.X); Near(.8, layout.Nodes[4].Position.X); Near(.95, layout.Nodes[4].Position.Y);
        Assert.Equal(4, layout.Edges.Count); Assert.Same(tree.Nodes[3], layout.Nodes[3].Entry); Assert.Equal(4, layout.Nodes[1].MergeHeight);
        var collapsed = ChartHierarchy.Dendrogram(tree, collapsedIndices: new HashSet<int> { 1 }, useMergeHeights: true, cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(3, collapsed.Nodes.Count); Near(.59, collapsed.Nodes[1].Position.Y);
    }

    [Fact]
    public void Dendrogram_structural_heights_need_no_cost_and_explicit_inversions_are_rejected()
    {
        var tree = ChartHierarchy.Snapshot([new HierarchyNode(0, [new HierarchyNode(0, [new HierarchyNode(0)])])], cancellationToken: TestContext.Current.CancellationToken);
        var structural = ChartHierarchy.Dendrogram(tree, cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(new double[] { 2, 1, 0 }, structural.Nodes.Select(n => n.MergeHeight)); Near(2, structural.MaximumHeight);
        var invalid = ChartHierarchy.Snapshot([new HierarchyNode(2, [new HierarchyNode(3)])], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentException>(() => ChartHierarchy.Dendrogram(invalid, useMergeHeights: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartHierarchy.Dendrogram(invalid, collapsedIndices: new HashSet<int> { 0 }, useMergeHeights: true, cancellationToken: TestContext.Current.CancellationToken));
        var huge = ChartHierarchy.Dendrogram(ChartHierarchy.Snapshot([new HierarchyNode(double.MaxValue, [new HierarchyNode(0)])], cancellationToken: TestContext.Current.CancellationToken), useMergeHeights: true, cancellationToken: TestContext.Current.CancellationToken); Assert.All(huge.Nodes, n => Assert.True(double.IsFinite(n.Position.X + n.Position.Y)));
    }

    [Fact]
    public void Radial_root_is_central_and_original_terminal_lanes_define_angle_and_depth_radius()
    {
        var layout = ChartHierarchy.RadialTree(CostTree(), cancellationToken: TestContext.Current.CancellationToken); Near(.5, layout.Nodes[0].Position.X); Near(.5, layout.Nodes[0].Position.Y); Near(0, layout.Nodes[0].Radius);
        Near(.22, layout.Nodes[1].Radius); Near(.44, layout.Nodes[2].Radius); Near(0, layout.Nodes[2].Angle); Near(180, layout.Nodes[3].Angle);
        Near(.94, layout.Nodes[2].Position.X); Near(.5, layout.Nodes[2].Position.Y); Assert.Equal(3, layout.Edges.Count);
        var collapsed = ChartHierarchy.RadialTree(CostTree(), collapsedIndices: new HashSet<int> { 1 }, cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(new[] { 0, 1, 3 }, collapsed.Nodes.Select(n => n.Entry.Index));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.RadialTree(CostTree(), sweepAngle: 0, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Radial_forest_does_not_fabricate_a_selectable_root_or_overlap_original_roots()
    {
        var tree = ChartHierarchy.Snapshot([new HierarchyNode(0, id: "a"), new HierarchyNode(0, id: "b")], cancellationToken: TestContext.Current.CancellationToken); var layout = ChartHierarchy.RadialTree(tree, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, layout.Nodes.Count); Assert.Empty(layout.Edges); Assert.NotEqual(layout.Nodes[0].Position, layout.Nodes[1].Position); Assert.All(layout.Nodes, n => Near(.44, n.Radius));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Every_layout_is_empty_safe_cancellable_and_rejects_invalid_scope(int kind)
    {
        var empty = ChartHierarchy.Snapshot(Array.Empty<HierarchyNode>(), cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(0, Count(kind, empty, TestContext.Current.CancellationToken));
        using var cts = new CancellationTokenSource(); cts.Cancel(); Assert.Throws<OperationCanceledException>(() => Count(kind, CostTree(), cts.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => Count(kind, CostTree(), TestContext.Current.CancellationToken, root: 99));
    }

    [Fact]
    public void Additional_layouts_are_deterministic_for_a_large_bounded_forest()
    {
        var tree = ChartHierarchy.Snapshot(Enumerable.Range(0, 100).Select(i => new HierarchyNode(10, Enumerable.Range(0, 10).Select(j => new HierarchyNode(1, id: $"{i}:{j}")), id: i)), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1100, tree.Nodes.Count); var a = ChartHierarchy.RadialTree(tree, cancellationToken: TestContext.Current.CancellationToken); var b = ChartHierarchy.RadialTree(tree, cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(a.Nodes, b.Nodes); Assert.Equal(1000, a.Edges.Count);
        Assert.Equal(1100, ChartHierarchy.Flame(tree, cancellationToken: TestContext.Current.CancellationToken).Rectangles.Count); Assert.Equal(1100, ChartHierarchy.Icicle(tree, cancellationToken: TestContext.Current.CancellationToken).Rectangles.Count); Assert.Equal(1100, ChartHierarchy.Indented(tree, cancellationToken: TestContext.Current.CancellationToken).Rectangles.Count); Assert.Equal(1100, ChartHierarchy.Dendrogram(tree, cancellationToken: TestContext.Current.CancellationToken).Nodes.Count);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Every_control_keeps_nested_original_identity_hit_selection_navigation_and_source_updates(int kind)
    {
        var a = new Node("a", 6); var b = new Node("b", 2); var root = new Node("root", 10, a, b); var chart = Make(kind); chart.ItemsSource = new[] { root }; chart.IdPath = "Id";
        using var host = new Host(chart); Assert.Null(chart.DataError); Assert.Equal(3, chart.GetMarks().Count);
        var mark = chart.GetMarks().Single(m => Equals(m.Key, "b")); Assert.Same(b, mark.Members[0].Item); Assert.Equal("b", chart.HitTest(mark.Position)!.Mark.Key);
        chart.SelectedIndex = chart.GetMarks().Select((m, i) => (m, i)).Single(p => Equals(p.m.Key, "b")).i;
        root.Children.Move(1, 0); host.Drain(); Assert.Same(b, chart.SelectedPoint!.Members[0].Item); Assert.Equal(0, chart.SelectedPoint.Members[0].SourceIndex);
        b.Value = 3; host.Drain(); Assert.Same(b, chart.SelectedPoint!.Members[0].Item); chart.Zoom = 1.2; chart.PanX = .02; _ = chart.GetMarks(); Assert.Same(b, chart.SelectedPoint!.Members[0].Item);
        chart.Collapse("root"); Assert.Single(chart.GetMarks()); chart.Expand("root"); Assert.Equal(3, chart.GetMarks().Count); Assert.True(chart.DrillDown("root")); Assert.True(chart.DrillUp());
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home }); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space }); Assert.NotNull(chart.SelectedPoint);
        Assert.NotEmpty(Record(chart).Children); host.Detach(); Assert.Equal(0, root.Subscribers); Assert.Equal(0, a.Subscribers); host.Attach(); Assert.True(root.Subscribers > 0);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task Every_control_retains_export_validation_cancellation_and_invalid_data_recovery(int kind)
    {
        var chart = Make(kind); if (chart is DendrogramChart dendrogram) dendrogram.DistancePath = "Value"; var row = new Node("root", 10, new Node("leaf", 5)); chart.ItemsSource = new[] { row }; chart.IdPath = "Id"; using var host = new Host(chart);
        using var stream = new MemoryStream(); var failures = 0; chart.ExportFailed += (_, _) => failures++; Assert.False(chart.TryExportPng(stream, new ChartExportOptions { Dpi = 0 })); Assert.Equal(1, failures);
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.TryExportPngAsync(stream, cancellationToken: cancel.Token)); Assert.Equal(1, failures);
        row.Value = double.NaN; host.Drain(); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); row.Value = 10; host.Drain(); Assert.NotEmpty(chart.GetMarks()); Assert.Null(chart.DataError);
    }

    [AvaloniaFact]
    public void Unweighted_tree_sources_can_omit_values_and_existing_weighted_contract_stays_strict()
    {
        var rows = new[] { new BareNode("Root", [new BareNode("Leaf", [])]) };
        foreach (var chart in new HierarchyChartBase[] { new DendrogramChart(), new IndentedTreeChart(), new RadialTreeChart() })
        { chart.ItemsSource = rows; using var host = new Host(chart); Assert.Null(chart.DataError); Assert.Equal(2, chart.GetMarks().Count); }
        var treemap = new TreemapChart { ItemsSource = rows }; using var weighted = new Host(treemap); Assert.NotNull(treemap.DataError); Assert.Empty(treemap.GetMarks());
        treemap.ItemsSource = new[] { new HierarchyNode(999, [new HierarchyNode(3, id: "a"), new HierarchyNode(2, id: "b")], id: "root") }; var root = treemap.GetMarks()[0]; Assert.Equal(5, root.Value); Assert.Equal(5, root.Members[0].Weight); Assert.Contains("depth 0; path 0", root.TooltipText!);
    }

    [AvaloniaFact]
    public void Dendrogram_distance_path_tracks_nested_notifications_and_rejects_inverted_heights()
    {
        var leaf = new Node("leaf", 0); var root = new Node("root", 10, leaf); var chart = new DendrogramChart { ItemsSource = new[] { root }, IdPath = "Id", DistancePath = "Value" };
        using var host = new Host(chart); Assert.Equal(10, chart.GetMarks()[0].Value); Assert.Contains("merge height 10", chart.GetMarks()[0].TooltipText!);
        root.Value = 20; host.Drain(); Assert.Equal(20, chart.Layout!.MaximumHeight); leaf.Value = 30; host.Drain(); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError);
        leaf.Value = 0; host.Drain(); Assert.Equal(20, chart.GetMarks()[0].Value); chart.DistancePath = null; Assert.Equal(1, chart.Layout!.MaximumHeight); Assert.Equal(1, chart.GetMarks()[0].Value);
        chart.Orientation = Orientation.Vertical; chart.LinkStyle = HierarchyLinkStyle.Straight; Assert.True(chart.GetMarks()[0].Position.Y < chart.GetMarks()[1].Position.Y); Assert.NotEmpty(Record(chart).Children);
    }

    [AvaloniaFact]
    public void Icicle_orientation_and_flame_inclusive_tooltips_use_the_displayed_cost()
    {
        var rows = new[] { new HierarchyNode(10, [new HierarchyNode(6, id: "a"), new HierarchyNode(2, id: "b")], id: "root") };
        var icicle = new IcicleChart { ItemsSource = rows, Gap = 0 }; using var host = new Host(icicle); Assert.True(icicle.GetMarks()[0].Position.Y < icicle.GetMarks()[1].Position.Y);
        icicle.Orientation = Orientation.Horizontal; Assert.True(icicle.GetMarks()[0].Position.X < icicle.GetMarks()[1].Position.X);
        var flame = new FlameGraph { ItemsSource = rows }; using var flameHost = new Host(flame); Assert.Equal(10, flame.GetMarks()[0].Value); Assert.Equal(10, flame.GetMarks()[0].Members[0].Weight); Assert.Contains("self 2", flame.GetMarks()[0].TooltipText!);
    }

    [AvaloniaFact]
    public void Indented_left_right_keys_expand_and_collapse_the_focused_original_branch()
    {
        var chart = new IndentedTreeChart { ItemsSource = new[] { new BareNode("Root", [new BareNode("Leaf", [])]) } }; using var host = new Host(chart);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home }); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left }); Assert.Single(chart.GetMarks());
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right }); Assert.Equal(2, chart.GetMarks().Count);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Additional_family_animation_retains_hittable_source_identities_and_reduced_motion(int kind)
    {
        var clock = new Clock(); var chart = Make(kind); chart.ItemsSource = new[] { new Node("root", 10, new Node("a", 6), new Node("b", 2)) }; chart.IdPath = "Id";
        chart.AnimationTimeProvider = clock; chart.AnimationDuration = TimeSpan.FromSeconds(1); chart.AnimationTriggers = ChartAnimationTriggers.All; chart.AnimationEasing = null;
        using var host = new Host(chart); clock.Advance(500); chart.UpdateAnimationFrame(); Assert.Equal(ChartAnimationState.Running, chart.AnimationState);
        foreach (var mark in chart.GetMarks()) Assert.Equal(mark.Key, chart.HitTest(mark.Position)!.Mark.Key);
        chart.PauseAnimations(); var before = chart.GetMarks().Select(m => m.Bounds).ToArray(); clock.Advance(500); chart.UpdateAnimationFrame(); Assert.Equal(before, chart.GetMarks().Select(m => m.Bounds));
        chart.ReduceMotion = true; Assert.Equal(ChartAnimationState.Idle, chart.AnimationState); Assert.Equal(1, chart.AnimationProgress); Assert.Equal(3, chart.GetMarks().Count);
    }

    private static int Count(int kind, HierarchySnapshot tree, CancellationToken token = default, int root = -1) => kind switch
    { 0 => ChartHierarchy.Flame(tree, root, cancellationToken: token).Rectangles.Count, 1 => ChartHierarchy.Icicle(tree, root, cancellationToken: token).Rectangles.Count, 2 => ChartHierarchy.Indented(tree, root, cancellationToken: token).Rectangles.Count, 3 => ChartHierarchy.Dendrogram(tree, root, cancellationToken: token).Nodes.Count, _ => ChartHierarchy.RadialTree(tree, root, cancellationToken: token).Nodes.Count };
    private static HierarchyChartBase Make(int kind) => kind switch { 0 => new FlameGraph(), 1 => new IcicleChart(), 2 => new IndentedTreeChart(), 3 => new DendrogramChart(), _ => new RadialTreeChart() };
    private static void Near(double expected, double actual) => Assert.True(Math.Abs(expected - actual) <= 1e-9, $"{expected:R} != {actual:R}");
    private static DrawingGroup Record(EngineeringChartBase chart) { var drawing = new DrawingGroup(); using var context = drawing.Open(); chart.Render(context); return drawing; }
    private sealed class BareNode(string label, IReadOnlyList<BareNode> children) { public string Label { get; } = label; public IReadOnlyList<BareNode> Children { get; } = children; }
    private sealed class Node(string id, double value, params Node[] children) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed; private double _value = value; public string Id { get; } = id; public string Label => Id; public int Subscribers { get; private set; }
        public ObservableCollection<Node> Children { get; } = new(children); public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Subscribers++; _changed += value; } remove { Subscribers--; _changed -= value; } }
    }
    private sealed class Clock : TimeProvider { private long _stamp; public override long TimestampFrequency => 1000; public override long GetTimestamp() => _stamp; public void Advance(int ms) => _stamp += ms; }
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly EngineeringChartBase _chart;
        public Host(EngineeringChartBase chart) { _chart = chart; _window = new Window { Width = 420, Height = 280, Content = chart }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); _ = _chart.GetMarks(); }
        public void Detach() { _window.Content = null; Drain(); } public void Attach() { _window.Content = _chart; Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
