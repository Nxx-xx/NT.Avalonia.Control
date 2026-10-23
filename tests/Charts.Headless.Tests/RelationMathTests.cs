using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class RelationMathTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Equal(double expected, double actual, double tolerance = 1e-10) =>
        Assert.InRange(actual, expected - tolerance * Math.Max(1, Math.Abs(expected)), expected + tolerance * Math.Max(1, Math.Abs(expected)));
    private static GraphSnapshot Graph(IEnumerable<GraphEdge>? edges = null) => ChartRelations.Snapshot(
        [new("A"), new("B"), new("C")], edges ?? [new("A", "B", 3), new("A", "C", 1)], Token);

    [Fact]
    public void Snapshot_retains_original_node_edge_identity_indices_metadata_and_totals()
    {
        var nodeSource = new object(); var edgeSource = new object();
        var a = new GraphNode("A", "Alpha", nodeSource, "Group", RelationNodeShape.Diamond, new(.2, .3), 7, .16, .1, 2);
        var edge = new GraphEdge("A", "B", 4, "Original", edgeSource, "ab");
        var graph = ChartRelations.Snapshot([a, new("B")], [edge], Token);
        Assert.Same(a, graph.Nodes[0].Node); Assert.Same(nodeSource, graph.Nodes[0].Source);
        Assert.Same(edge, graph.Edges[0].Edge); Assert.Same(edgeSource, graph.Edges[0].Source);
        Assert.Equal([0, 1], graph.Nodes.Select(n => n.SourceIndex)); Assert.Equal(0, graph.Edges[0].SourceIndex);
        Assert.Equal("ab", graph.Edges[0].Id); Assert.Equal("Original", graph.Edges[0].Label);
        Assert.Equal("Alpha", graph.Nodes[0].Label); Assert.Equal("Group", a.Group); Assert.Equal(2, a.Stage);
        Assert.Equal(RelationNodeShape.Diamond, a.Shape); Equal(.2, a.X!.Value); Equal(.3, a.Y!.Value);
        Equal(.16, a.Width); Equal(.1, a.Height); Equal(7, a.Weight);
        Equal(4, graph.Nodes[0].OutgoingWeight); Equal(0, graph.Nodes[0].IncomingWeight);
        Equal(4, graph.Nodes[1].IncomingWeight); Equal(0, graph.Nodes[1].OutgoingWeight);
        Assert.Equal(0, graph.Edges[0].SourceNodeIndex); Assert.Equal(1, graph.Edges[0].TargetNodeIndex);
    }

    [Fact]
    public void Snapshot_and_layout_collections_are_owned_and_readonly()
    {
        var nodes = new List<GraphNode> { new("A"), new("B") }; var edges = new List<GraphEdge> { new("A", "B") };
        var graph = ChartRelations.Snapshot(nodes, edges, Token); nodes.Clear(); edges.Clear();
        Assert.Equal(2, graph.Nodes.Count); Assert.Single(graph.Edges);
        Assert.Throws<NotSupportedException>(() => ((IList<GraphNodeEntry>)graph.Nodes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<GraphEdgeEntry>)graph.Edges).Clear());
        var flow = ChartRelations.Sankey(graph, cancellationToken: Token);
        Assert.Throws<NotSupportedException>(() => ((IList<RelationNodeLayout>)flow.Nodes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ChartPoint>)flow.Edges[0].Points).Clear());
        var chord = ChartRelations.Chord(graph, cancellationToken: Token);
        Assert.Throws<NotSupportedException>(() => ((IList<RelationChordRibbon>)chord.Ribbons).Clear());
    }

    [Fact]
    public void Snapshot_accepts_parallel_edges_selfloops_and_cycles_without_merging_source_rows()
    {
        var graph = Graph([new("A", "B", 3), new("A", "B", 2), new("B", "A", 1), new("B", "B", 4)]);
        Assert.Equal([0, 1, 2, 3], graph.Edges.Select(e => e.SourceIndex));
        Equal(5, graph.Nodes[0].OutgoingWeight); Equal(1, graph.Nodes[0].IncomingWeight);
        Equal(9, graph.Nodes[1].IncomingWeight); Equal(5, graph.Nodes[1].OutgoingWeight);
        Assert.All(graph.Edges, e => Assert.Same(e.Edge, e.Source));
    }

    [Fact]
    public void Snapshot_rejects_duplicate_nodes_dangling_edges_and_duplicate_optional_edge_ids()
    {
        Assert.Throws<ArgumentException>(() => ChartRelations.Snapshot([new("A"), new("A")], [], Token));
        Assert.Throws<ArgumentException>(() => ChartRelations.Snapshot([new("A")], [new("A", "missing")], Token));
        Assert.Throws<ArgumentException>(() => ChartRelations.Snapshot([new("A")], [new("A", "A", id: 1), new("A", "A", id: 1)], Token));
        Assert.Equal(2, ChartRelations.Snapshot([new("A")], [new("A", "A"), new("A", "A")], Token).Edges.Count);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Snapshot_rejects_invalid_node_and_edge_weights(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Snapshot([new("A", weight: value)], [], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => Graph([new("A", "B", value)]));
    }

    [Fact]
    public void Snapshot_validates_shape_stage_coordinates_dimensions_and_aggregate_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Snapshot([new("A", shape: (RelationNodeShape)99)], [], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Snapshot([new("A", stage: -1)], [], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Snapshot([new("A", position: new(double.NaN, .5))], [], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Snapshot([new("A", position: new(1.1, .5))], [], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Snapshot([new("A", width: 0)], [], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Snapshot([new("A", height: 2)], [], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => Graph([new("A", "B", double.MaxValue), new("A", "C", double.MaxValue)]));
    }

    [Fact]
    public void Snapshot_rejects_count_budgets_before_unbounded_enumeration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Snapshot(
            Enumerable.Range(0, ChartRelations.MaximumNodeCount + 1).Select(i => new GraphNode(i)), [], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Snapshot([new("A")],
            Enumerable.Range(0, ChartRelations.MaximumEdgeCount + 1).Select(_ => new GraphEdge("A", "A")), Token));
    }

    [Fact]
    public void Cancellation_interrupts_input_enumeration_after_it_has_started()
    {
        using var cancellation = new CancellationTokenSource();
        IEnumerable<GraphNode> Nodes()
        { yield return new("A"); cancellation.Cancel(); yield return new("B"); }
        Assert.Throws<OperationCanceledException>(() => ChartRelations.Snapshot(Nodes(), [], cancellation.Token));
    }

    [Fact]
    public void All_layout_engines_observe_precancellation_including_empty_graphs()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var empty = ChartRelations.Snapshot([], [], Token);
        Assert.Throws<OperationCanceledException>(() => ChartRelations.Snapshot([], [], cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => ChartRelations.Sankey(empty, cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => ChartRelations.Network(empty, cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => ChartRelations.Chord(empty, cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => ChartRelations.Flow(empty, cancellationToken: cancellation.Token));
    }

    [Fact]
    public void Empty_graphs_produce_empty_layouts_for_all_engines()
    {
        var graph = ChartRelations.Snapshot([], [], Token);
        Assert.Empty(ChartRelations.Sankey(graph, cancellationToken: Token).Nodes);
        Assert.Empty(ChartRelations.Network(graph, cancellationToken: Token).Edges);
        Assert.Empty(ChartRelations.Chord(graph, cancellationToken: Token).Ribbons);
        Assert.Empty(ChartRelations.Flow(graph, cancellationToken: Token).Groups);
    }

    [Fact]
    public void Sankey_uses_one_weight_scale_and_nonoverlapping_ports_for_original_parallel_rows()
    {
        var graph = Graph(); var layout = ChartRelations.Sankey(graph, nodeGap: 0, cancellationToken: Token);
        Equal(3, layout.Edges[0].Thickness / layout.Edges[1].Thickness);
        Equal(layout.Edges[0].Thickness + layout.Edges[1].Thickness, layout.Nodes[0].Height);
        Equal(3 * layout.WeightScale, layout.Edges[0].Thickness);
        Equal(1 * layout.WeightScale, layout.Edges[1].Thickness);
        var first = layout.Edges[0]; var second = layout.Edges[1];
        Assert.True(first.Points[0].Y + first.Thickness * .5 <= second.Points[0].Y - second.Thickness * .5 + 1e-10);
        foreach (var edge in layout.Edges)
        {
            Assert.Same(graph.Edges[edge.Entry.Index], edge.Entry);
            var source = layout.Nodes[edge.Entry.SourceNodeIndex]; var target = layout.Nodes[edge.Entry.TargetNodeIndex];
            Equal(source.X + source.Width, edge.Points[0].X); Equal(target.X, edge.Points[^1].X);
            Assert.InRange(edge.Points[0].Y, source.Y, source.Y + source.Height);
            Assert.InRange(edge.Points[^1].Y, target.Y, target.Y + target.Height);
        }
        Inside(layout);
    }

    [Fact]
    public void Sankey_conservation_diagnostics_exempt_boundaries_but_strict_mode_rejects_interior_imbalance()
    {
        var graph = Graph([new("A", "B", 4), new("B", "C", 3)]);
        var layout = ChartRelations.Sankey(graph, cancellationToken: Token); var imbalance = Assert.Single(layout.Imbalances);
        Assert.Same(graph.Nodes[1], imbalance.Node); Equal(-1, imbalance.Delta);
        Assert.Throws<ArgumentException>(() => ChartRelations.Sankey(graph, strictConservation: true, cancellationToken: Token));
        Assert.Empty(ChartRelations.Sankey(Graph(), strictConservation: true, cancellationToken: Token).Imbalances);
    }

    [Fact]
    public void Sankey_SCC_layers_terminate_on_cycles_and_retain_selfloops_and_parallel_edges()
    {
        var graph = Graph([new("A", "B", 3), new("B", "A", 2), new("B", "B", 1), new("B", "C", 1), new("A", "B", 1)]);
        var layout = ChartRelations.Sankey(graph, cancellationToken: Token);
        Equal(layout.Nodes[0].Center.X, layout.Nodes[1].Center.X); Assert.True(layout.Nodes[2].Center.X > layout.Nodes[1].Center.X);
        Assert.Equal([true, true, true, false, true], layout.Edges.Select(e => e.IsBackEdge));
        Assert.Equal(5, layout.Edges.Count); Inside(layout);
    }

    [Fact]
    public void Alluvial_honors_explicit_sparse_stages_and_marks_backward_links()
    {
        var graph = ChartRelations.Snapshot([new("A", stage: int.MaxValue), new("B", stage: 0), new("C", stage: 100)],
            [new("A", "B", 1), new("B", "C", 2)], Token);
        var layout = ChartRelations.Sankey(graph, mode: RelationSankeyMode.Alluvial, cancellationToken: Token);
        Assert.True(layout.Nodes[1].Center.X < layout.Nodes[2].Center.X); Assert.True(layout.Nodes[2].Center.X < layout.Nodes[0].Center.X);
        Assert.True(layout.Edges[0].IsBackEdge); Assert.False(layout.Edges[1].IsBackEdge); Inside(layout);
    }

    [Fact]
    public void Sankey_barycentric_sweeps_remove_a_simple_crossing_and_preserve_original_indices()
    {
        var graph = ChartRelations.Snapshot([new("A"), new("B"), new("C"), new("D")], [new("A", "D"), new("B", "C")], Token);
        var baseline = ChartRelations.Sankey(graph, crossingIterations: 0, cancellationToken: Token);
        var ordered = ChartRelations.Sankey(graph, cancellationToken: Token);
        Assert.True(baseline.Nodes[2].Y < baseline.Nodes[3].Y); Assert.True(ordered.Nodes[3].Y < ordered.Nodes[2].Y);
        Assert.Equal([0, 1, 2, 3], ordered.Nodes.Select(n => n.Entry.Index));
    }

    [Fact]
    public void Sankey_zero_weights_and_extreme_finite_chain_are_finite()
    {
        var zero = ChartRelations.Sankey(Graph([new("A", "B", 0)]), cancellationToken: Token);
        Assert.All(zero.Edges, e => Equal(0, e.Thickness)); Inside(zero);
        var extreme = ChartRelations.Sankey(Graph([new("A", "B", 1e308), new("B", "C", 1e308)]), cancellationToken: Token);
        Equal(.8, extreme.Edges[0].Thickness); Equal(extreme.Edges[0].Thickness, extreme.Edges[1].Thickness); Inside(extreme);
    }

    [Fact]
    public void Sankey_thousand_layer_chain_clamps_widths_without_overlapping_nodes_or_recursion()
    {
        var graph = ChartRelations.Snapshot(Enumerable.Range(0, 1000).Select(i => new GraphNode(i)),
            Enumerable.Range(0, 999).Select(i => new GraphEdge(i, i + 1)), Token);
        var layout = ChartRelations.Sankey(graph, cancellationToken: Token); Assert.Equal(1000, layout.Nodes.Count);
        for (var i = 0; i < 999; i++) Assert.True(layout.Nodes[i].X + layout.Nodes[i].Width < layout.Nodes[i + 1].X);
        Inside(layout);
    }

    [Fact]
    public void Sankey_large_cycles_reserve_full_ribbon_bands_and_preserve_one_shared_scale()
    {
        var graph = ChartRelations.Snapshot([new("A"), new("B")], [new("A", "B", 4), new("B", "A", 4)], Token);
        var layout = ChartRelations.Sankey(graph, cancellationToken: Token);
        // Two unit-capacity nodes plus two unit return weights share the available .775 height.
        Equal(.775 / 4, layout.Edges[0].Thickness); Equal(.775 / 4, layout.Edges[1].Thickness);
        Equal(layout.Edges[0].Thickness, layout.Nodes[0].Height);
        Equal(4 * layout.WeightScale, layout.Edges[0].Thickness);
        FullReturnBandsInside(layout); Inside(layout);
    }

    [Fact]
    public void Sankey_parallel_return_lanes_pack_without_overlapping_their_full_widths()
    {
        var graph = ChartRelations.Snapshot([new("A"), new("B")],
            [new("A", "B", 2), new("A", "B", 4), new("B", "A", 6), new("B", "B", 1)], Token);
        var layout = ChartRelations.Sankey(graph, cancellationToken: Token);
        foreach (var parity in new[] { 0, 1 })
        {
            var lanes = layout.Edges.Where(edge => edge.IsBackEdge && edge.Entry.Index % 2 == parity)
                .Select(edge => (Low: edge.Points[2].Y - edge.Thickness / 2, High: edge.Points[2].Y + edge.Thickness / 2)).OrderBy(p => p.Low).ToArray();
            for (var i = 1; i < lanes.Length; i++) Assert.True(lanes[i - 1].High <= lanes[i].Low + 1e-10);
        }
        Equal(2, layout.Edges[1].Thickness / layout.Edges[0].Thickness);
        Equal(3, layout.Edges[2].Thickness / layout.Edges[0].Thickness);
        FullReturnBandsInside(layout);
    }

    [Fact]
    public void Alluvial_wide_nodes_leave_room_for_large_return_ribbons_and_separate_columns()
    {
        var graph = ChartRelations.Snapshot([new("A", stage: 0), new("B", stage: 1)],
            [new("A", "B", 4), new("B", "A", 4)], Token);
        var layout = ChartRelations.Sankey(graph, nodeWidth: .4, mode: RelationSankeyMode.Alluvial, cancellationToken: Token);
        Equal(.3, layout.Edges[1].Thickness); Assert.True(layout.Nodes[0].X + layout.Nodes[0].Width < layout.Nodes[1].X);
        FullReturnBandsInside(layout); Inside(layout);
    }

    [Fact]
    public void Sankey_single_large_selfloop_retains_its_full_band_inside_the_unit_square()
    {
        var graph = ChartRelations.Snapshot([new("A")], [new("A", "A", 1e308)], Token);
        var layout = ChartRelations.Sankey(graph, nodeWidth: 1, cancellationToken: Token);
        Equal(.3, layout.Edges[0].Thickness); Equal(.3, layout.Nodes[0].Height);
        var loop = layout.Edges[0]; var rightInterior = loop.Points[2].X - loop.Thickness * .5;
        var leftInterior = loop.Points[3].X + loop.Thickness * .5;
        Assert.True(leftInterior < rightInterior);
        Assert.True(leftInterior < layout.Nodes[0].X); Assert.True(rightInterior > layout.Nodes[0].X + layout.Nodes[0].Width);
        FullReturnBandsInside(layout); Inside(layout);
    }

    [Fact]
    public void Sankey_subnormal_weight_scale_is_explicitly_saturated_without_changing_geometry()
    {
        var normal = ChartRelations.Sankey(ChartRelations.Snapshot([new("A"), new("B")], [new("A", "B", 1)], Token), cancellationToken: Token);
        Assert.False(normal.IsWeightScaleSaturated);
        foreach (var tiny in new[] { double.Epsilon, 2 * double.Epsilon })
        {
            var layout = ChartRelations.Sankey(ChartRelations.Snapshot([new("A"), new("B")], [new("A", "B", tiny)], Token), cancellationToken: Token);
            Assert.True(layout.IsWeightScaleSaturated); Assert.Equal(double.MaxValue, layout.WeightScale);
            Assert.True(double.IsFinite(layout.WeightScale)); Equal(normal.Edges[0].Thickness, layout.Edges[0].Thickness);
            for (var i = 0; i < layout.Nodes.Count; i++)
            { Equal(normal.Nodes[i].X, layout.Nodes[i].X); Equal(normal.Nodes[i].Y, layout.Nodes[i].Y); Equal(normal.Nodes[i].Height, layout.Nodes[i].Height); }
            Assert.Equal(normal.Edges[0].Points, layout.Edges[0].Points); Inside(layout);
        }
        var zero = ChartRelations.Sankey(Graph([new("A", "B", 0)]), cancellationToken: Token);
        Assert.False(zero.IsWeightScaleSaturated); Equal(0, zero.WeightScale);
        Assert.False(ChartRelations.Sankey(Graph([new("A", "B", 1e308)]), cancellationToken: Token).IsWeightScaleSaturated);
    }

    [Fact]
    public void Chord_sector_mass_is_incoming_plus_outgoing_and_ports_match_original_edge_weights()
    {
        var graph = Graph([new("A", "B", 3), new("B", "C", 1)]);
        var layout = ChartRelations.Chord(graph, 0, Token);
        Equal(135, layout.Nodes[0].SweepAngle); Equal(180, layout.Nodes[1].SweepAngle); Equal(45, layout.Nodes[2].SweepAngle);
        Equal(135, layout.Ribbons[0].SourceSweepAngle); Equal(135, layout.Ribbons[0].TargetSweepAngle);
        Equal(45, layout.Ribbons[1].SourceSweepAngle); Equal(45, layout.Ribbons[1].TargetSweepAngle);
        Equal(360, layout.Nodes.Sum(n => n.SweepAngle));
        foreach (var ribbon in layout.Ribbons) Assert.Same(graph.Edges[ribbon.Entry.Index], ribbon.Entry);
    }

    [Fact]
    public void Chord_selfloop_has_two_distinct_half_sector_ports_and_parallel_ribbons_stay_distinct()
    {
        var graph = ChartRelations.Snapshot([new("A")], [new("A", "A", 5)], Token);
        var layout = ChartRelations.Chord(graph, 0, Token); var ribbon = Assert.Single(layout.Ribbons);
        Equal(360, layout.Nodes[0].SweepAngle); Equal(180, ribbon.SourceSweepAngle); Equal(180, ribbon.TargetSweepAngle);
        Equal(-90, ribbon.SourceStartAngle); Equal(90, ribbon.TargetStartAngle);
        var parallel = ChartRelations.Chord(Graph([new("A", "B", 2), new("A", "B", 3)]), cancellationToken: Token);
        Assert.Equal(2, parallel.Ribbons.Count); Assert.NotEqual(parallel.Ribbons[0].SourceStartAngle, parallel.Ribbons[1].SourceStartAngle);
    }

    [Fact]
    public void Chord_zero_graph_uses_node_weights_and_caps_padding_without_negative_sectors()
    {
        var graph = ChartRelations.Snapshot([new("A", weight: 3), new("B", weight: 1)], [new("A", "B", 0)], Token);
        var layout = ChartRelations.Chord(graph, 0, Token); Equal(270, layout.Nodes[0].SweepAngle); Equal(90, layout.Nodes[1].SweepAngle);
        Equal(0, layout.Ribbons[0].SourceSweepAngle);
        var dense = ChartRelations.Chord(Graph(), 180, Token); Equal(60, dense.PaddingDegrees);
        Equal(360, dense.Nodes.Sum(n => n.SweepAngle) + dense.Nodes.Count * dense.PaddingDegrees);
        Assert.All(dense.Nodes, n => Assert.True(n.SweepAngle >= 0));
    }

    [Theory]
    [InlineData(RelationNetworkMode.ForceDirected)]
    [InlineData(RelationNetworkMode.Circular)]
    [InlineData(RelationNetworkMode.Arc)]
    public void Network_modes_retain_every_original_node_and_edge_in_unit_square(RelationNetworkMode mode)
    {
        var graph = Graph([new("A", "B", 2), new("A", "B", 1), new("B", "A", 3), new("C", "C", 2)]);
        var layout = ChartRelations.Network(graph, mode, cancellationToken: Token); Inside(layout);
        Assert.Equal(3, layout.Nodes.Count); Assert.Equal(4, layout.Edges.Count);
        Assert.NotEqual(layout.Edges[0].Points[10], layout.Edges[1].Points[10]);
        Assert.Equal(3, layout.Edges.Take(3).Select(edge => edge.Points[12]).Distinct().Count());
        Assert.All(layout.Nodes, n => Assert.Same(graph.Nodes[n.Entry.Index], n.Entry));
        Assert.All(layout.Edges, e => Assert.Same(graph.Edges[e.Entry.Index], e.Entry));
    }

    [Fact]
    public void Circular_network_uses_equal_angle_centers_and_arc_uses_original_index_order()
    {
        var graph = Graph(); var circular = ChartRelations.Network(graph, RelationNetworkMode.Circular, cancellationToken: Token);
        Equal(.5, circular.Nodes[0].Center.X); Equal(.13, circular.Nodes[0].Center.Y);
        Equal(.5 + .37 * Math.Sqrt(3) * .5, circular.Nodes[1].Center.X); Equal(.685, circular.Nodes[1].Center.Y);
        var arc = ChartRelations.Network(graph, RelationNetworkMode.Arc, cancellationToken: Token);
        Equal(.1, arc.Nodes[0].Center.X); Equal(.5, arc.Nodes[1].Center.X); Equal(.9, arc.Nodes[2].Center.X);
        Assert.All(arc.Nodes, n => Equal(.68, n.Center.Y));
        Assert.True(arc.Edges[0].Points[12].Y < .68);
    }

    [Fact]
    public void Force_network_is_seed_deterministic_bounded_and_reports_actual_work()
    {
        var graph = ChartRelations.Snapshot(Enumerable.Range(0, 60).Select(i => new GraphNode(i)),
            Enumerable.Range(0, 59).Select(i => new GraphEdge(i, i + 1)), Token);
        var first = ChartRelations.Network(graph, iterations: 40, seed: 51, cancellationToken: Token);
        var again = ChartRelations.Network(graph, iterations: 40, seed: 51, cancellationToken: Token);
        Assert.Equal(first.Nodes, again.Nodes); Assert.Equal(first.MaximumDisplacement, again.MaximumDisplacement);
        Assert.InRange(first.Iterations, 1, 40); Assert.Equal(0, first.RemainingCollisionCount); Inside(first);
        Assert.All(first.Nodes, a => Assert.All(first.Nodes.Where(b => b.Entry.Index > a.Entry.Index), b =>
            Assert.True(a.X + a.Width <= b.X + 1e-9 || b.X + b.Width <= a.X + 1e-9 || a.Y + a.Height <= b.Y + 1e-9 || b.Y + b.Height <= a.Y + 1e-9)));
    }

    [Theory]
    [InlineData(RelationNetworkMode.ForceDirected)]
    [InlineData(RelationNetworkMode.Circular)]
    [InlineData(RelationNetworkMode.Arc)]
    public void Pinned_network_centers_are_preserved_and_caller_collisions_reported(RelationNetworkMode mode)
    {
        var graph = ChartRelations.Snapshot([new("A", position: new(.5, .5)), new("B", position: new(.5, .5))], [new("A", "B")], Token);
        var layout = ChartRelations.Network(graph, mode, cancellationToken: Token);
        Assert.All(layout.Nodes, node => { Equal(.5, node.Center.X); Equal(.5, node.Center.Y); });
        Assert.Equal(1, layout.RemainingCollisionCount); Assert.False(layout.IsConverged); Assert.False(layout.FallbackApplied);
    }

    [Fact]
    public void Network_rejects_force_and_iteration_budgets_but_larger_circular_graph_is_allowed()
    {
        var graph = ChartRelations.Snapshot(Enumerable.Range(0, 401).Select(i => new GraphNode(i)), [], Token);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Network(graph, cancellationToken: Token));
        var circular = ChartRelations.Network(graph, RelationNetworkMode.Circular, cancellationToken: Token);
        Assert.Equal(401, circular.Nodes.Count); Assert.Equal(0, circular.RemainingCollisionCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Network(Graph(), iterations: 301, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Network(Graph(), iterations: -1, cancellationToken: Token));
    }

    [Theory]
    [InlineData(RelationNetworkMode.ForceDirected)]
    [InlineData(RelationNetworkMode.Circular)]
    [InlineData(RelationNetworkMode.Arc)]
    public void Network_single_and_adjacent_subnormal_weights_preserve_unit_invariant_widths(RelationNetworkMode mode)
    {
        var normalSingle = ChartRelations.Network(Graph([new("A", "B", 1)]), mode, cancellationToken: Token);
        var normalPair = ChartRelations.Network(Graph([new("A", "B", 1), new("A", "C", 2)]), mode, cancellationToken: Token);
        foreach (var tiny in new[] { double.Epsilon, 2 * double.Epsilon })
        {
            var single = ChartRelations.Network(Graph([new("A", "B", tiny)]), mode, cancellationToken: Token);
            Equal(.006, single.Edges[0].Thickness); Equal(normalSingle.Edges[0].Thickness, single.Edges[0].Thickness);
            var pair = ChartRelations.Network(Graph([new("A", "B", tiny), new("A", "C", 2 * tiny)]), mode, cancellationToken: Token);
            for (var i = 0; i < pair.Edges.Count; i++)
            { Equal(normalPair.Edges[i].Thickness, pair.Edges[i].Thickness); Assert.Equal(normalPair.Edges[i].Points, pair.Edges[i].Points); }
        }
    }

    [Theory]
    [InlineData(RelationFlowMode.Layered)]
    [InlineData(RelationFlowMode.Process)]
    [InlineData(RelationFlowMode.Explicit)]
    [InlineData(RelationFlowMode.Mindmap)]
    public void Flow_single_and_adjacent_subnormal_weights_preserve_unit_invariant_widths(RelationFlowMode mode)
    {
        var normalSingle = ChartRelations.Flow(Graph([new("A", "B", 1)]), mode, Token);
        var normalPair = ChartRelations.Flow(Graph([new("A", "B", 1), new("A", "C", 2)]), mode, Token);
        foreach (var tiny in new[] { double.Epsilon, 2 * double.Epsilon })
        {
            var single = ChartRelations.Flow(Graph([new("A", "B", tiny)]), mode, Token);
            Equal(.006, single.Edges[0].Thickness); Equal(normalSingle.Edges[0].Thickness, single.Edges[0].Thickness);
            var pair = ChartRelations.Flow(Graph([new("A", "B", tiny), new("A", "C", 2 * tiny)]), mode, Token);
            for (var i = 0; i < pair.Edges.Count; i++)
            { Equal(normalPair.Edges[i].Thickness, pair.Edges[i].Thickness); Assert.Equal(normalPair.Edges[i].Points, pair.Edges[i].Points); }
        }
    }

    [Theory]
    [InlineData(RelationFlowMode.Layered)]
    [InlineData(RelationFlowMode.Process)]
    [InlineData(RelationFlowMode.Explicit)]
    [InlineData(RelationFlowMode.Mindmap)]
    public void Flow_modes_preserve_nodes_edges_groups_shapes_and_original_rows(RelationFlowMode mode)
    {
        var graph = ChartRelations.Snapshot([new("A", group: "g", shape: RelationNodeShape.Diamond),
            new("B", group: "g"), new("C", shape: RelationNodeShape.Ellipse)], [new("A", "B"), new("B", "C"), new("C", "A"), new("B", "B")], Token);
        var layout = ChartRelations.Flow(graph, mode, Token); Assert.Equal(3, layout.Nodes.Count); Assert.Equal(4, layout.Edges.Count);
        Assert.Equal(RelationNodeShape.Diamond, layout.Nodes[0].Entry.Node.Shape); var group = Assert.Single(layout.Groups); Assert.Equal("g", group.Group);
        foreach (var node in layout.Nodes.Take(2))
        { Assert.True(group.X <= node.X); Assert.True(group.Y <= node.Y); Assert.True(group.X + group.Width >= node.X + node.Width); Assert.True(group.Y + group.Height >= node.Y + node.Height); }
        Inside(layout);
    }

    [Fact]
    public void Explicit_flow_honors_centers_dimensions_and_preserves_caller_overlap()
    {
        var graph = ChartRelations.Snapshot([new("A", position: new(.3, .4), width: .2, height: .1), new("B", position: new(.3, .4), width: .2, height: .1)],
            [new("A", "B")], Token);
        var layout = ChartRelations.Flow(graph, RelationFlowMode.Explicit, Token);
        Assert.All(layout.Nodes, n => { Equal(.3, n.Center.X); Equal(.4, n.Center.Y); Equal(.2, n.Width); Equal(.1, n.Height); });
        Assert.Equal(1, layout.RemainingCollisionCount); Assert.False(layout.IsConverged);
        var invalid = ChartRelations.Snapshot([new("A", position: new(.01, .5), width: .2)], [], Token);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Flow(invalid, RelationFlowMode.Explicit, Token));
    }

    [Theory]
    [InlineData(.5, .2, .5, .8, false)]
    [InlineData(.5, .8, .5, .2, false)]
    [InlineData(.2, .5, .8, .5, true)]
    [InlineData(.8, .5, .2, .5, true)]
    [InlineData(.2, .2, .8, .35, true)]
    [InlineData(.8, .35, .2, .2, true)]
    [InlineData(.2, .2, .35, .8, false)]
    [InlineData(.35, .8, .2, .2, false)]
    public void Explicit_flow_ports_and_terminal_segments_follow_rectangle_separation_including_cycles(
        double ax, double ay, double bx, double by, bool horizontal)
    {
        foreach (var cycle in new[] { false, true })
        {
            var graph = ChartRelations.Snapshot([new("A", position: new(ax, ay)), new("B", position: new(bx, by))],
                cycle ? [new("A", "B"), new("B", "A")] : [new("A", "B")], Token);
            var layout = ChartRelations.Flow(graph, RelationFlowMode.Explicit, Token);
            foreach (var edge in layout.Edges)
            {
                Assert.Equal(cycle, edge.IsBackEdge);
                var source = layout.Nodes[edge.Entry.SourceNodeIndex]; var target = layout.Nodes[edge.Entry.TargetNodeIndex];
                var sign = horizontal ? Math.Sign(target.Center.X - source.Center.X) : Math.Sign(target.Center.Y - source.Center.Y);
                var start = edge.Points[0]; var next = edge.Points[1]; var previous = edge.Points[^2]; var end = edge.Points[^1];
                if (horizontal)
                {
                    Equal(source.Center.X + sign * source.Width / 2, start.X); Equal(source.Center.Y, start.Y);
                    Equal(target.Center.X - sign * target.Width / 2, end.X); Equal(target.Center.Y, end.Y);
                    Equal(start.Y, next.Y); Equal(previous.Y, end.Y);
                    Assert.True(sign * (next.X - start.X) > 0); Assert.True(sign * (end.X - previous.X) > 0);
                }
                else
                {
                    Equal(source.Center.X, start.X); Equal(source.Center.Y + sign * source.Height / 2, start.Y);
                    Equal(target.Center.X, end.X); Equal(target.Center.Y - sign * target.Height / 2, end.Y);
                    Equal(start.X, next.X); Equal(previous.X, end.X);
                    Assert.True(sign * (next.Y - start.Y) > 0); Assert.True(sign * (end.Y - previous.Y) > 0);
                }
            }
            Inside(layout); Assert.Equal(0, layout.RemainingCollisionCount);
        }
    }

    [Fact]
    public void Explicit_flow_parallel_offsets_remain_between_close_nonoverlapping_node_ports()
    {
        var graph = ChartRelations.Snapshot([new("A", position: new(.4, .5)), new("B", position: new(.53, .5))],
            Enumerable.Range(0, 40).Select(i => new GraphEdge("A", "B", id: i)), Token);
        var layout = ChartRelations.Flow(graph, RelationFlowMode.Explicit, Token);
        Assert.All(layout.Edges, edge =>
        { Assert.True(edge.Points[1].X > edge.Points[0].X); Assert.True(edge.Points[^1].X > edge.Points[^2].X); });
        Inside(layout);
    }

    [Fact]
    public void Process_and_missing_explicit_positions_use_same_predictable_automatic_layers()
    {
        var graph = ChartRelations.Snapshot([new("A"), new("B")], [new("A", "B")], Token);
        var process = ChartRelations.Flow(graph, RelationFlowMode.Process, Token);
        Equal(.1, process.Nodes[0].Center.X); Equal(.9, process.Nodes[1].Center.X);
        Assert.All(process.Nodes, n => Equal(.5, n.Center.Y));
        Assert.Equal(process.Nodes, ChartRelations.Flow(graph, RelationFlowMode.Explicit, Token).Nodes);
        Equal(process.Nodes[0].X + process.Nodes[0].Width, process.Edges[0].Points[0].X);
        Equal(process.Nodes[1].X, process.Edges[0].Points[^1].X);
    }

    [Fact]
    public void Mindmap_centers_root_and_alternates_branches_without_losing_crosslinks()
    {
        var graph = ChartRelations.Snapshot([new("root"), new("right"), new("left"), new("leaf")],
            [new("root", "right"), new("root", "left"), new("right", "leaf"), new("leaf", "root")], Token);
        var layout = ChartRelations.Flow(graph, RelationFlowMode.Mindmap, Token);
        Equal(.5, layout.Nodes[0].Center.X); Assert.True(layout.Nodes[1].Center.X > .5); Assert.True(layout.Nodes[2].Center.X < .5);
        Assert.True(layout.Nodes[3].Center.X > layout.Nodes[1].Center.X); Assert.Equal(4, layout.Edges.Count); Inside(layout);
    }

    [Fact]
    public void Engines_reject_invalid_modes_and_layout_options()
    {
        var graph = Graph();
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Sankey(graph, crossingIterations: 33, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Sankey(graph, nodeWidth: 0, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Sankey(graph, nodeGap: double.NaN, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Sankey(graph, mode: (RelationSankeyMode)99, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Chord(graph, -1, Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Network(graph, (RelationNetworkMode)99, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRelations.Flow(graph, (RelationFlowMode)99, Token));
    }

    private static void Inside(RelationGraphLayout layout)
    {
        Assert.All(layout.Nodes, node =>
        { Assert.InRange(node.X, -1e-10, 1); Assert.InRange(node.Y, -1e-10, 1); Assert.InRange(node.X + node.Width, 0, 1 + 1e-10); Assert.InRange(node.Y + node.Height, 0, 1 + 1e-10); });
        Assert.All(layout.Edges, edge =>
        { Assert.True(double.IsFinite(edge.Thickness)); Assert.True(edge.Thickness >= 0); Assert.All(edge.Points, p => { Assert.InRange(p.X, -1e-10, 1 + 1e-10); Assert.InRange(p.Y, -1e-10, 1 + 1e-10); }); });
    }

    private static void FullReturnBandsInside(RelationGraphLayout layout)
    {
        foreach (var edge in layout.Edges.Where(edge => edge.IsBackEdge))
        {
            if (edge.Entry.SourceNodeIndex == edge.Entry.TargetNodeIndex)
            {
                var node = layout.Nodes[edge.Entry.SourceNodeIndex];
                Assert.True(edge.Points[3].X + edge.Thickness * .5 <= node.X - .025 + 1e-10);
                Assert.True(edge.Points[2].X - edge.Thickness * .5 >= node.X + node.Width + .025 - 1e-10);
            }
            foreach (var point in edge.Points)
            {
                // A disk containing every possible unit-normal offset is a stronger condition than
                // the actual ribbon polygon, and covers either screen aspect ratio after projection.
                var half = edge.Thickness * .5;
                Assert.InRange(point.X - half, -1e-10, 1); Assert.InRange(point.X + half, 0, 1 + 1e-10);
                Assert.InRange(point.Y - half, -1e-10, 1); Assert.InRange(point.Y + half, 0, 1 + 1e-10);
            }
        }
    }
}
