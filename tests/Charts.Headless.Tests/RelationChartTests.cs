using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class RelationChartTests
{
    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void Each_catalog_mode_preserves_original_node_edge_sources_selection_and_viewport(int mode)
    {
        var nodeA = new object(); var nodeB = new object(); var edge = new object();
        var chart = Make(mode); chart.ItemsSource = new[] { new GraphNode("a", "Alpha", nodeA, position: new(.25, .5)), new GraphNode("b", "Beta", nodeB, position: new(.75, .5)) };
        chart.EdgesSource = new[] { new GraphEdge("a", "b", 5, "A to B", edge, "edge") };
        using var host = new Host(chart); Assert.Null(chart.DataError); Assert.Equal(2, chart.Snapshot!.Nodes.Count); Assert.Single(chart.Snapshot.Edges);
        Assert.Equal(3, chart.GetMarks().Count); Assert.True(chart.SelectNode("a")); Assert.Same(nodeA, chart.SelectedNode!.Source); Assert.Null(chart.SelectedEdge);
        Assert.True(chart.SelectEdge("edge")); Assert.Same(edge, chart.SelectedEdge!.Source); Assert.Null(chart.SelectedNode); Assert.Equal("edge", chart.SelectedPoint!.Members[0].StableId); Assert.Same(edge, chart.SelectedPoint.Members[0].Item);
        chart.Zoom = 1.2; chart.PanX = .01; host.Resize(540, 320); Assert.Same(edge, chart.SelectedEdge!.Source);
        foreach (var mark in chart.GetMarks().Where(m => ((RelationGlyphKey)m.Key!).Kind == RelationElementKind.Node))
            if (chart.PlotArea.Contains(mark.Position)) Assert.Equal(mark.Key, chart.HitTest(mark.Position)!.Mark.Key);
        chart.ItemsSource = new[] { new GraphNode("b", "newB", nodeB, position: new(.7, .5)), new GraphNode("a", "newA", nodeA, position: new(.3, .5)) };
        Assert.Same(edge, chart.SelectedEdge!.Source); Assert.Equal(0, chart.SelectedEdge.SourceIndex);
        chart.EdgesSource = Array.Empty<GraphEdge>(); Assert.Null(chart.SelectedPoint); Assert.Equal(-1, chart.SelectedIndex);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void Each_catalog_mode_retains_keyboard_and_export_cancellation(int mode)
    {
        var chart = Make(mode); chart.ItemsSource = new[] { new GraphNode("a"), new GraphNode("b") }; chart.EdgesSource = new[] { new GraphEdge("a", "b", id: "ab") };
        using var host = new Host(chart); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home }); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        Assert.Equal("ab", chart.SelectedEdge!.Id); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End }); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Assert.Equal("b", chart.SelectedNode!.Id);
        using var stream = new MemoryStream(); Assert.False(chart.TryExportPng(stream, new ChartExportOptions { Dpi = 0 }));
        chart.Zoom = 2; chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Assert.Equal(1, chart.Zoom); Assert.Equal(-1, chart.SelectedIndex);
    }

    [AvaloniaFact]
    public void Node_and_edge_ID_domains_are_separate_and_no_ID_edges_follow_reference_instead_of_record_equality()
    {
        var a = new EqualEdge("a", "b", 2); var b = new EqualEdge("a", "b", 2); var rows = new ObservableCollection<EqualEdge> { a, b };
        var chart = new NetworkChart { ItemsSource = new[] { new GraphNode("a"), new GraphNode("b") }, EdgesSource = rows };
        using var host = new Host(chart); var edgeKeys = chart.GetMarks().Where(m => m.Key is RelationGlyphKey { Kind: RelationElementKind.Edge }).Select(m => m.Key).ToArray(); Assert.NotEqual(edgeKeys[0], edgeKeys[1]); Assert.Equal(2, edgeKeys.Distinct().Count());
        Assert.True(chart.SelectEdge(b)); Assert.Same(b, chart.SelectedEdge!.Source); rows.Move(1, 0); host.Drain(); Assert.Same(b, chart.SelectedEdge!.Source); Assert.Equal(0, chart.SelectedEdge.SourceIndex);
        chart.EdgesSource = new[] { new GraphEdge("a", "b", id: "a") }; Assert.True(chart.SelectNode("a")); Assert.NotNull(chart.SelectedNode); Assert.True(chart.SelectEdge("a")); Assert.NotNull(chart.SelectedEdge); Assert.Null(chart.SelectedNode);
        chart.EdgesSource = new[] { new GraphEdge("a", "b", 3, id: "a") }; Assert.Equal(3, chart.SelectedEdge!.Weight);
    }

    [AvaloniaFact]
    public void Observable_edge_reorder_replacement_and_invalid_data_recovery_keep_real_sources()
    {
        var edge = new MutableEdge("ab", "a", "b", 2); var edges = new ObservableCollection<MutableEdge> { edge };
        var chart = new SankeyChart { ItemsSource = new[] { new GraphNode("a"), new GraphNode("b") }, EdgesSource = edges }; using var host = new Host(chart);
        Assert.True(edge.Observers > 0); chart.SelectEdge("ab"); edge.Weight = -1; host.Drain(); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); Assert.Null(chart.Snapshot);
        edge.Weight = 4; host.Drain(); Assert.Null(chart.DataError); Assert.Equal(4, chart.Snapshot!.Edges[0].Weight); Assert.Same(edge, chart.Snapshot.Edges[0].Source);
        edges[0] = new("ab", "a", "b", 8); host.Drain(); Assert.Equal(0, edge.Observers); Assert.Same(edges[0], chart.Snapshot!.Edges[0].Source);
        host.Detach(); Assert.Equal(0, edges[0].Observers); host.Attach(); Assert.True(edges[0].Observers > 0);
    }

    [AvaloniaFact]
    public void Mapped_node_shape_center_dimensions_stage_weight_and_nested_edge_payload_are_observed()
    {
        var payload = new MutablePayload(3); var chart = new FlowChart { Mode = RelationFlowMode.Explicit, ItemsSource = new[] { new MappedNode("a", .25, .4, .14, .1, 3, 4, RelationNodeShape.Diamond), new MappedNode("b", .75, .6, .2, .12, 7, 2, RelationNodeShape.Ellipse) },
            EdgesSource = new[] { new MappedEdge("ab", "a", "b", payload) }, XPath = "X", YPath = "Y", WeightPath = "Payload.Amount", NodeWeightPath = "Metric" };
        using var host = new Host(chart); Assert.Null(chart.DataError); Assert.Equal(4, chart.Snapshot!.Nodes[0].Node.Weight); Assert.Equal(3, chart.Snapshot.Nodes[0].Node.Stage);
        var layout = chart.GraphLayout!; Near(.25, layout.Nodes[0].Center.X); Near(.4, layout.Nodes[0].Center.Y); Near(.14, layout.Nodes[0].Width); Near(.1, layout.Nodes[0].Height);
        var diamond = chart.GetMarks().Single(m => m.Key is RelationGlyphKey { Kind: RelationElementKind.Node, Identity: "a" }); Assert.Null(chart.HitTest(diamond.Bounds.TopLeft + new Vector(1, 1)));
        payload.Amount = 9; host.Drain(); Assert.Equal(9, chart.Snapshot!.Edges[0].Weight); Assert.NotEmpty(Record(chart).Children);
    }

    [AvaloniaFact]
    public void Invalid_IDs_missing_targets_duplicate_edge_IDs_and_zero_edges_are_explicit()
    {
        var chart = new NetworkChart { ItemsSource = new[] { new { Label = "missing" } } }; using var host = new Host(chart); Assert.NotNull(chart.DataError); Assert.Empty(chart.GetMarks());
        chart.ItemsSource = new[] { new GraphNode("a"), new GraphNode("b") }; chart.EdgesSource = new[] { new GraphEdge("a", "missing") }; Assert.NotNull(chart.DataError);
        chart.EdgesSource = new[] { new GraphEdge("a", "b", id: "same"), new GraphEdge("b", "a", id: "same") }; Assert.NotNull(chart.DataError);
        chart.EdgesSource = new[] { new GraphEdge("a", "b", 0, id: "zero") }; Assert.Null(chart.DataError); Assert.Equal(2, chart.GetMarks().Count); Assert.Single(chart.Snapshot!.Edges); Assert.False(chart.SelectEdge("zero"));
    }

    [AvaloniaFact]
    public void Source_replacement_from_a_node_getter_never_publishes_the_old_snapshot()
    {
        var chart = new NetworkChart(); var node = new ReentrantNode(); node.Callback = () => chart.ItemsSource = new[] { new GraphNode("latest") }; chart.ItemsSource = new[] { node };
        using var host = new Host(chart); host.Drain(); Assert.Equal("latest", Assert.Single(chart.Snapshot!.Nodes).Id); Assert.Null(chart.DataError);
    }

    [AvaloniaFact]
    public void Subscription_accessor_reentry_releases_old_edge_and_observes_latest_source()
    {
        var chart = new NetworkChart { ItemsSource = new[] { new GraphNode("a"), new GraphNode("b") } }; var old = new MutableEdge("old", "a", "b", 2); var latest = new MutableEdge("latest", "a", "b", 3);
        old.OnSubscribe = () => chart.EdgesSource = new[] { latest }; chart.EdgesSource = new[] { old }; using var host = new Host(chart); host.Drain(); Assert.Equal(0, old.Observers); Assert.True(latest.Observers > 0); Assert.Equal("latest", Assert.Single(chart.Snapshot!.Edges).Id);
        latest.Weight = 7; host.Drain(); Assert.Equal(7, chart.Snapshot!.Edges[0].Weight); host.Detach(); Assert.Equal(0, latest.Observers);
    }

    [AvaloniaFact]
    public async Task Background_layout_runs_off_UI_thread_cancels_external_token_and_only_latest_generation_publishes()
    {
        using var chart = new BlockingChart { ItemsSource = new[] { new GraphNode("a"), new GraphNode("b") }, EdgesSource = new[] { new GraphEdge("a", "b") } }; using var host = new Host(chart);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var first = chart.LayoutAsync(cancellation.Token); await WaitUntil(() => chart.Started.IsSet); Assert.True(chart.IsLayoutRunning); Assert.Equal(RelationLayoutState.Running, chart.LayoutState); Assert.NotEqual(Environment.CurrentManagedThreadId, chart.WorkerThread);
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first); Assert.False(chart.IsLayoutRunning); Assert.Equal(RelationLayoutState.Canceled, chart.LayoutState);
        chart.ResetGate(); var stale = chart.LayoutAsync(TestContext.Current.CancellationToken); await WaitUntil(() => chart.Started.IsSet); chart.ItemsSource = new[] { new GraphNode("new") }; chart.EdgesSource = Array.Empty<GraphEdge>(); chart.Release.Set(); Assert.False(await stale); Assert.Equal("new", Assert.Single(chart.Snapshot!.Nodes).Id);
        chart.ResetGate(); chart.Release.Set(); Assert.True(await chart.LayoutAsync(TestContext.Current.CancellationToken)); Assert.Equal(RelationLayoutState.Completed, chart.LayoutState); Assert.False(chart.IsLayoutRunning);
    }

    [AvaloniaFact]
    public async Task Detach_and_dispose_cancel_active_layout_and_release_observers()
    {
        var edge = new MutableEdge("ab", "a", "b", 2); var chart = new BlockingChart { ItemsSource = new[] { new GraphNode("a"), new GraphNode("b") }, EdgesSource = new[] { edge } }; using var host = new Host(chart);
        var work = chart.LayoutAsync(TestContext.Current.CancellationToken); await WaitUntil(() => chart.Started.IsSet); host.Detach(); Assert.False(await work); Assert.Equal(0, edge.Observers);
        host.Attach(); chart.ResetGate(); work = chart.LayoutAsync(TestContext.Current.CancellationToken); await WaitUntil(() => chart.Started.IsSet); chart.Dispose(); Assert.False(await work); Assert.Equal(0, edge.Observers); Assert.Empty(chart.GetMarks()); await Assert.ThrowsAsync<ObjectDisposedException>(() => chart.LayoutAsync(TestContext.Current.CancellationToken));
    }

    [AvaloniaFact]
    public async Task Cancel_never_repeats_the_canceled_layout_on_the_UI_thread_and_fresh_data_reenables_layout()
    {
        using var chart = new BlockingChart { ItemsSource = new[] { new GraphNode("a") } };
        var work = chart.LayoutAsync(TestContext.Current.CancellationToken); await WaitUntil(() => chart.Started.IsSet); Assert.Equal(0, chart.SynchronousCalls);
        chart.CancelLayout(); Assert.False(await work); Assert.Empty(chart.GetMarks()); _ = Record(chart); Dispatcher.UIThread.RunJobs(); Assert.Equal(0, chart.SynchronousCalls); Assert.Equal(RelationLayoutState.Canceled, chart.LayoutState);
        chart.ItemsSource = new[] { new GraphNode("latest") }; Assert.Single(chart.GetMarks()); Assert.Equal(1, chart.SynchronousCalls);
    }

    [AvaloniaFact]
    public async Task Running_and_completed_notification_reentry_cannot_publish_or_report_a_stale_layout()
    {
        using var chart = new BlockingChart { ItemsSource = new[] { new GraphNode("initial") } }; var changeRunning = true;
        chart.PropertyChanged += (_, e) => { if (e.Property == RelationsChartBase.LayoutStateProperty && chart.LayoutState == RelationLayoutState.Running && changeRunning) { changeRunning = false; chart.ItemsSource = new[] { new GraphNode("running-new") }; } };
        Assert.False(await chart.LayoutAsync(TestContext.Current.CancellationToken)); Assert.False(chart.Started.IsSet); Assert.False(chart.IsLayoutRunning); Assert.Equal("running-new", Assert.Single(chart.Snapshot!.Nodes).Id);
        var changeCompleted = true; chart.ResetGate(); chart.Release.Set();
        chart.PropertyChanged += (_, e) => { if (e.Property == RelationsChartBase.LayoutStateProperty && chart.LayoutState == RelationLayoutState.Completed && changeCompleted) { changeCompleted = false; chart.ItemsSource = new[] { new GraphNode("completed-new") }; } };
        Assert.False(await chart.LayoutAsync(TestContext.Current.CancellationToken)); Assert.Equal("completed-new", Assert.Single(chart.Snapshot!.Nodes).Id); Assert.False(chart.IsLayoutRunning);
    }

    [AvaloniaFact]
    public void Source_node_values_remain_original_while_glyph_weight_exposes_derived_flow()
    {
        var chart = new SankeyChart { ItemsSource = new[] { new GraphNode("a", weight: 7), new GraphNode("b", weight: 9) }, EdgesSource = new[] { new GraphEdge("a", "b", 100, id: "ab") } };
        using var host = new Host(chart); chart.SelectNode("a"); var hit = chart.SelectedPoint!; Assert.Equal(7d, hit.Members[0].Value); Assert.Equal(7, hit.Members[0].Point.Y); Assert.Equal(100, hit.Mark.Value); Assert.Equal(100, hit.Members[0].Weight);
    }

    [AvaloniaFact]
    public void Mapped_edge_endpoints_accept_typed_node_original_sources_without_exposing_private_wrappers()
    {
        var a = new GraphNode("a"); var b = new GraphNode("b"); var edge = new ReferencedEdge("ab", a, b, 5);
        var chart = new NetworkChart { ItemsSource = new[] { a, b }, EdgesSource = new[] { edge } }; using var host = new Host(chart); Assert.Null(chart.DataError);
        Assert.Equal("a", chart.Snapshot!.Edges[0].Edge.SourceId); Assert.Equal("b", chart.Snapshot.Edges[0].Edge.TargetId); Assert.Same(edge, chart.Snapshot.Edges[0].Source);
        chart.SelectNode("a"); Assert.Same(a, chart.SelectedPoint!.Members[0].Item); Assert.Equal("a", chart.SelectedPoint.Members[0].StableId);
    }

    [AvaloniaTheory]
    [InlineData(1)] [InlineData(1.15)]
    public void Flow_arrow_wings_return_the_original_edge_and_hidden_arrows_leave_no_ghost_hit(double zoom)
    {
        var original = new object(); var chart = new FlowChart { Mode = RelationFlowMode.Explicit, ItemsSource = new[] { new GraphNode("a", position: new(.2, .5), width: .1, height: .1), new GraphNode("b", position: new(.8, .5), width: .1, height: .1) }, EdgesSource = new[] { new GraphEdge("a", "b", 3, source: original, id: "ab") }, Zoom = zoom };
        using var host = new Host(chart); var layout = chart.GraphLayout!; var end = layout.Edges[0].Points[^1]; var plot = chart.PlotArea;
        var tip = new Point(plot.Center.X + (plot.Left + end.X * plot.Width - plot.Center.X) * zoom, plot.Center.Y + (plot.Top + end.Y * plot.Height - plot.Center.Y) * zoom);
        var size = 7 * Math.Sqrt(zoom); var wing = tip + new Vector(-size * .75, size * .3);
        var hit = Assert.IsType<EngineeringPointHit>(chart.HitTest(wing)); Assert.Equal("ab", Assert.IsType<RelationGlyphKey>(hit.Mark.Key).Identity); Assert.Same(original, hit.Members[0].Item);
        chart.ShowArrows = false; Assert.Null(chart.HitTest(wing)); chart.ShowArrows = true; Assert.Same(original, chart.HitTest(wing)!.Members[0].Item);
        var node = chart.GetMarks().Single(m => m.Key is RelationGlyphKey { Kind: RelationElementKind.Node, Identity: "b" }); Assert.Equal(node.Key, chart.HitTest(node.Position)!.Mark.Key);
    }

    [AvaloniaTheory]
    [InlineData(420, 280)] [InlineData(280, 520)]
    public void Vertical_bidirectional_flow_cycle_arrows_enter_the_target_and_keep_original_edge_hits(int width, int height)
    {
        var down = new object(); var up = new object();
        var chart = new FlowChart { Mode = RelationFlowMode.Explicit, ItemsSource = new[] { new GraphNode("a", position: new(.5, .2), width: .12, height: .08), new GraphNode("b", position: new(.5, .8), width: .12, height: .08) },
            EdgesSource = new[] { new GraphEdge("a", "b", source: down, id: "down"), new GraphEdge("b", "a", source: up, id: "up") } };
        using var host = new Host(chart); host.Resize(width, height); var plot = chart.PlotArea;
        foreach (var edge in chart.GraphLayout!.Edges)
        {
            Assert.True(edge.IsBackEdge); var direction = Equals(edge.Entry.Id, "down") ? 1d : -1d;
            var first = edge.Points[1]; var start = edge.Points[0]; var previous = edge.Points[^2]; var end = edge.Points[^1];
            Near(.5, start.X); Near(.5, end.X); Assert.True((first.Y - start.Y) * direction > 0); Assert.True((end.Y - previous.Y) * direction > 0);
            Near(direction > 0 ? .24 : .76, start.Y); Near(direction > 0 ? .76 : .24, end.Y);
            var tip = new Point(plot.Left + end.X * plot.Width, plot.Top + end.Y * plot.Height);
            // Probe the exposed wing closer to the tip. In the shorter landscape plot,
            // the opposite edge's horizontal return body covers the arrow's wider base.
            var wing = tip + new Vector(7 * .2, -direction * 7 * .5);
            var hit = Assert.IsType<EngineeringPointHit>(chart.HitTest(wing)); Assert.Equal(edge.Entry.Id, Assert.IsType<RelationGlyphKey>(hit.Mark.Key).Identity); Assert.Same(edge.Entry.Source, hit.Members[0].Item); Assert.Equal(edge.Entry.SourceIndex, hit.Members[0].SourceIndex);
            Point? coveredWing = direction > 0 && width == 420 ? tip + new Vector(7 * .3, -7 * .75) : null;
            if (coveredWing is { } covered) { var topmost = chart.HitTest(covered)!; Assert.Equal("up", Assert.IsType<RelationGlyphKey>(topmost.Mark.Key).Identity); Assert.Same(up, topmost.Members[0].Item); }
            chart.ShowArrows = false; Assert.Null(chart.HitTest(wing)); chart.ShowArrows = true; Assert.Same(edge.Entry.Source, chart.HitTest(wing)!.Members[0].Item);
            Assert.True(chart.SelectEdge(edge.Entry.Id!)); Assert.Same(edge.Entry.Source, chart.SelectedEdge!.Source);
        }
    }

    private static async Task WaitUntil(Func<bool> predicate) { for (var i = 0; i < 500 && !predicate(); i++) await Task.Delay(2, TestContext.Current.CancellationToken); Assert.True(predicate()); }
    private static void Near(double expected, double actual) => Assert.InRange(Math.Abs(expected - actual), 0, 1e-9);
    private static DrawingGroup Record(EngineeringChartBase chart) { var drawing = new DrawingGroup(); using var context = drawing.Open(); chart.Render(context); return drawing; }
    private static RelationsChartBase Make(int mode) => mode switch { 0 => new SankeyChart(), 1 => new SankeyChart { Mode = RelationSankeyMode.Alluvial }, 2 => new NetworkChart { Mode = RelationNetworkMode.Circular }, 3 => new NetworkChart { Mode = RelationNetworkMode.ForceDirected }, 4 => new NetworkChart { Mode = RelationNetworkMode.Arc }, 5 => new ChordDiagramChart(), 6 => new FlowChart(), 7 => new FlowChart { Mode = RelationFlowMode.Explicit }, _ => new FlowChart { Mode = RelationFlowMode.Mindmap } };
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly RelationsChartBase _chart;
        public Host(RelationsChartBase chart) { _chart = chart; _window = new Window { Width = 420, Height = 280, Content = chart }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); _ = _chart.GetMarks(); }
        public void Resize(double width, double height) { _window.Width = width; _window.Height = height; Drain(); }
        public void Detach() { _window.Content = null; Drain(); }
        public void Attach() { _window.Content = _chart; Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private sealed record EqualEdge(string SourceId, string TargetId, double Weight);
    private sealed record ReferencedEdge(string Id, object SourceId, object TargetId, double Weight);
    private sealed record MappedNode(string Id, double X, double Y, double Width, double Height, int Stage, double Metric, RelationNodeShape Shape);
    private sealed record MappedEdge(string Id, string SourceId, string TargetId, MutablePayload Payload);
    private sealed class MutablePayload(double amount) : INotifyPropertyChanged { private double _amount = amount; public double Amount { get => _amount; set { _amount = value; PropertyChanged?.Invoke(this, new(nameof(Amount))); } } public event PropertyChangedEventHandler? PropertyChanged; }
    private sealed class ReentrantNode { public string Id => "old"; public Action? Callback { get; set; } public string Label { get { var callback = Callback; Callback = null; callback?.Invoke(); return "old"; } } }
    private sealed class MutableEdge(string id, string source, string target, double weight) : INotifyPropertyChanged
    {
        private double _weight = weight; private PropertyChangedEventHandler? _changed;
        public string Id => id; public string SourceId => source; public string TargetId => target; public Action? OnSubscribe { get; set; } public int Observers { get; private set; }
        public double Weight { get => _weight; set { _weight = value; _changed?.Invoke(this, new(nameof(Weight))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { var callback = OnSubscribe; OnSubscribe = null; callback?.Invoke(); _changed += value; Observers++; } remove { _changed -= value; Observers--; } }
    }
    private sealed class BlockingChart : RelationsChartBase
    {
        public ManualResetEventSlim Started { get; private set; } = new(false); public ManualResetEventSlim Release { get; private set; } = new(false); public int WorkerThread { get; private set; } public int SynchronousCalls { get; private set; }
        public void ResetGate() { Started.Dispose(); Release.Dispose(); Started = new(false); Release = new(false); }
        protected override object CalculateLayout(GraphSnapshot snapshot, CancellationToken cancellationToken) { SynchronousCalls++; return ChartRelations.Network(snapshot, RelationNetworkMode.Circular, cancellationToken: cancellationToken); }
        protected override Func<CancellationToken, object> CreateLayoutWorker(GraphSnapshot snapshot) => token => { WorkerThread = Environment.CurrentManagedThreadId; Started.Set(); Release.Wait(token); return ChartRelations.Network(snapshot, RelationNetworkMode.Circular, cancellationToken: token); };
    }
}
