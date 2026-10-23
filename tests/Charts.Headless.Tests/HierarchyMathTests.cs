using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class HierarchyMathTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static HierarchySnapshot Forest() => ChartHierarchy.Snapshot([
        new(999, [new(2, label: "A1", id: "a1"), new(6, label: "A2", id: "a2")], "A", id: "a"),
        new(8, label: "B", id: "b")], Token);
    private static void Equal(double expected, double actual, double tolerance = 1e-11) =>
        Assert.InRange(actual, expected - tolerance * Math.Max(1, Math.Abs(expected)), expected + tolerance * Math.Max(1, Math.Abs(expected)));
    private static double Distance(ChartPoint a, ChartPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    [Fact]
    public void Snapshot_retains_preorder_sibling_paths_source_and_leaf_aggregation()
    {
        var source = new object(); var leaf = new HierarchyNode(3, source: source, id: "original");
        var snapshot = ChartHierarchy.Snapshot([new(100, [leaf, new(5)], "root"), new(7)], Token);
        Assert.Equal([0, 1, 2, 3], snapshot.Nodes.Select(entry => entry.Index));
        Assert.Equal([-1, 0, 0, -1], snapshot.Nodes.Select(entry => entry.ParentIndex));
        Assert.Equal([0, 0, 1, 1], snapshot.Nodes.Select(entry => entry.SourceIndex));
        Assert.Equal([0, 1, 1, 0], snapshot.Nodes.Select(entry => entry.Depth));
        Assert.Equal([0, 1], snapshot.Nodes[2].Path); Assert.Equal([0, 3], snapshot.Roots);
        Assert.Equal([1, 2], snapshot.Nodes[0].Children); Equal(8, snapshot.Nodes[0].AggregateValue);
        Assert.Same(source, snapshot.Nodes[1].Source); Assert.Equal("original", snapshot.Nodes[1].Id);
        Assert.Same(leaf, snapshot.Nodes[1].Node); Equal(100, snapshot.Nodes[0].Value);
    }

    [Fact]
    public void Node_snapshot_paths_and_layout_lists_are_owned_immutable_collections()
    {
        var children = new List<HierarchyNode> { new(2) }; var parent = new HierarchyNode(0, children);
        children.Clear(); Assert.Single(parent.Children);
        var roots = new List<HierarchyNode> { parent }; var snapshot = ChartHierarchy.Snapshot(roots, Token); roots.Clear();
        Assert.Equal(2, snapshot.Nodes.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<HierarchyNode>)parent.Children).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<int>)snapshot.Nodes[1].Path)[0] = 8);
        Assert.Throws<NotSupportedException>(() => ((IList<HierarchyEntry>)snapshot.Nodes).Clear());
        var layout = ChartHierarchy.Treemap(snapshot, cancellationToken: Token);
        Assert.Throws<NotSupportedException>(() => ((IList<HierarchyRectangle>)layout.Rectangles).Clear());
    }

    [Theory]
    [InlineData(TreemapLayoutMode.Slice)]
    [InlineData(TreemapLayoutMode.Squarify)]
    public void Treemap_areas_equal_leaf_proportions_and_children_partition_parent(TreemapLayoutMode mode)
    {
        var snapshot = Forest(); var layout = ChartHierarchy.Treemap(snapshot, mode: mode, cancellationToken: Token);
        var a = layout.Rectangles.Single(rect => Equals(rect.Entry.Id, "a"));
        var a1 = layout.Rectangles.Single(rect => Equals(rect.Entry.Id, "a1"));
        var a2 = layout.Rectangles.Single(rect => Equals(rect.Entry.Id, "a2"));
        var b = layout.Rectangles.Single(rect => Equals(rect.Entry.Id, "b"));
        Equal(.5, a.Width * a.Height); Equal(.125, a1.Width * a1.Height); Equal(.375, a2.Width * a2.Height); Equal(.5, b.Width * b.Height);
        Contained(a, a1); Contained(a, a2); Separate(a1, a2); Separate(a, b);
        Assert.Equal(snapshot.Nodes, layout.Rectangles.Select(rect => rect.Entry));
    }

    [Fact]
    public void Treemap_slice_has_predictable_alternating_partition_axes()
    {
        var layout = ChartHierarchy.Treemap(Forest(), mode: TreemapLayoutMode.Slice, cancellationToken: Token);
        var a = layout.Rectangles[0]; var a1 = layout.Rectangles[1]; var a2 = layout.Rectangles[2];
        Equal(.5, a.Width); Equal(1, a.Height); Equal(.5, a1.Width); Equal(.25, a1.Height);
        Equal(.25, a2.Y); Equal(.75, a2.Height);
    }

    [Fact]
    public void Treemap_squarify_gives_square_cells_for_four_equal_items()
    {
        var snapshot = ChartHierarchy.Snapshot(Enumerable.Range(0, 4).Select(_ => new HierarchyNode(1)), Token);
        var layout = ChartHierarchy.Treemap(snapshot, cancellationToken: Token);
        Assert.All(layout.Rectangles, rect => { Equal(.5, rect.Width); Equal(.5, rect.Height); });
        for (var i = 0; i < 4; i++) for (var j = i + 1; j < 4; j++) Separate(layout.Rectangles[i], layout.Rectangles[j]);
    }

    [Theory]
    [InlineData(TreemapLayoutMode.Slice)]
    [InlineData(TreemapLayoutMode.Squarify)]
    public void Treemap_drill_renormalizes_subtree_without_changing_original_identity(TreemapLayoutMode mode)
    {
        var snapshot = Forest(); var layout = ChartHierarchy.Treemap(snapshot, 0, mode: mode, cancellationToken: Token);
        Assert.Equal([0, 1, 2], layout.Rectangles.Select(rect => rect.Entry.Index));
        Equal(1, layout.Rectangles[0].Width * layout.Rectangles[0].Height);
        Equal(.25, layout.Rectangles[1].Width * layout.Rectangles[1].Height);
        Assert.Same(snapshot.Nodes[1], layout.Rectangles[1].Entry); Assert.Equal([0, 0], layout.Rectangles[1].Entry.Path);
    }

    [Theory]
    [InlineData(TreemapLayoutMode.Slice)]
    [InlineData(TreemapLayoutMode.Squarify)]
    public void Treemap_handles_unbalanced_hierarchy_without_sibling_overlap(TreemapLayoutMode mode)
    {
        var snapshot = ChartHierarchy.Snapshot([new(0, Enumerable.Range(1, 31).Select(i => new HierarchyNode(i * i))), new(777)], Token);
        var rectangles = ChartHierarchy.Treemap(snapshot, mode: mode, cancellationToken: Token).Rectangles;
        var children = rectangles.Where(rect => rect.Entry.ParentIndex == 0).ToArray();
        Equal(rectangles[0].Width * rectangles[0].Height, children.Sum(rect => rect.Width * rect.Height));
        foreach (var child in children) Contained(rectangles[0], child);
        for (var i = 0; i < children.Length; i++) for (var j = i + 1; j < children.Length; j++) Separate(children[i], children[j]);
    }

    [Fact]
    public void Sunburst_angles_are_leaf_weighted_and_equal_rings_share_parent_angles()
    {
        var snapshot = Forest(); var sectors = ChartHierarchy.Sunburst(snapshot, cancellationToken: Token).Sectors;
        Equal(-90, sectors[0].StartAngle); Equal(180, sectors[0].SweepAngle);
        Equal(45, sectors[1].SweepAngle); Equal(135, sectors[2].SweepAngle); Equal(90, sectors[3].StartAngle);
        Equal(0, sectors[0].InnerRadius); Equal(.25, sectors[0].OuterRadius);
        Equal(.25, sectors[1].InnerRadius); Equal(.5, sectors[1].OuterRadius);
        Equal(sectors[0].StartAngle, sectors[1].StartAngle);
        Equal(sectors[0].StartAngle + sectors[0].SweepAngle, sectors[2].StartAngle + sectors[2].SweepAngle);
    }

    [Fact]
    public void Sunburst_drill_and_configured_half_annulus_preserve_original_source_indices()
    {
        var snapshot = Forest(); var sectors = ChartHierarchy.Sunburst(snapshot, 0, innerRadius: .1, startAngle: 180, sweepAngle: 180, cancellationToken: Token).Sectors;
        Assert.Equal([0, 1, 2], sectors.Select(sector => sector.Entry.Index)); Equal(180, sectors[0].SweepAngle);
        Equal(.1, sectors[0].InnerRadius); Equal(.3, sectors[0].OuterRadius); Equal(.5, sectors[1].OuterRadius);
        Equal(45, sectors[1].SweepAngle); Equal(135, sectors[2].SweepAngle);
    }

    [Fact]
    public void Tree_centers_parents_over_terminals_and_edges_touch_node_boundaries()
    {
        var snapshot = Forest(); var layout = ChartHierarchy.Tree(snapshot, cancellationToken: Token);
        Assert.Equal(4, layout.Nodes.Count); Assert.Equal(2, layout.Edges.Count);
        var a = layout.Nodes[0]; var a1 = layout.Nodes[1]; var a2 = layout.Nodes[2];
        Equal((a1.X + a2.X + a1.Width) * .5, a.X + a.Width * .5);
        Assert.True(a.Y + a.Height < a1.Y);
        foreach (var edge in layout.Edges)
        {
            var parent = layout.Nodes.Single(node => node.Entry.Index == edge.Parent.Index);
            var child = layout.Nodes.Single(node => node.Entry.Index == edge.Child.Index);
            Equal(parent.Y + parent.Height, edge.Start.Y); Equal(child.Y, edge.End.Y);
            Assert.Same(snapshot.Nodes[edge.Child.Index], edge.Child);
        }
        Assert.All(layout.Nodes, node => { Assert.InRange(node.X, 0, 1); Assert.InRange(node.Y, 0, 1); Assert.InRange(node.X + node.Width, 0, 1); Assert.InRange(node.Y + node.Height, 0, 1); });
    }

    [Fact]
    public void Collapse_applies_to_all_layouts_and_keeps_aggregate_identity()
    {
        var snapshot = Forest(); var collapsed = new HashSet<int> { 0 };
        Assert.Equal([0, 3], ChartHierarchy.Treemap(snapshot, collapsedIndices: collapsed, cancellationToken: Token).Rectangles.Select(rect => rect.Entry.Index));
        var sectors = ChartHierarchy.Sunburst(snapshot, collapsedIndices: collapsed, cancellationToken: Token).Sectors;
        Assert.Equal([0, 3], sectors.Select(sector => sector.Entry.Index)); Equal(.5, sectors[0].OuterRadius);
        var tree = ChartHierarchy.Tree(snapshot, collapsedIndices: collapsed, cancellationToken: Token);
        Assert.Equal([0, 3], tree.Nodes.Select(node => node.Entry.Index)); Assert.Empty(tree.Edges);
        foreach (var layout in new[] {
            ChartHierarchy.BubbleCloud(snapshot, collapsedIndices: collapsed, cancellationToken: Token),
            ChartHierarchy.PackedBubble(snapshot, collapsedIndices: collapsed, cancellationToken: Token),
            ChartHierarchy.CirclePacking(snapshot, collapsedIndices: collapsed, cancellationToken: Token) })
        {
            Assert.Equal([0, 3], layout.Circles.Select(circle => circle.Entry.Index));
            Assert.Same(snapshot.Nodes[0], layout.Circles[0].Entry); Equal(8, layout.Circles[0].Entry.AggregateValue);
        }
        Assert.Equal(4, snapshot.Nodes.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Flat_packing_has_no_overlap_and_encodes_area_on_one_global_scale(bool ordered)
    {
        var source = Enumerable.Range(0, 80).Select(i => new HierarchyNode((i % 13 + 1) * (i % 13 + 1), id: i)).ToArray();
        var snapshot = ChartHierarchy.Snapshot(source, Token);
        var layout = ordered ? ChartHierarchy.PackedBubble(snapshot, cancellationToken: Token) : ChartHierarchy.BubbleCloud(snapshot, cancellationToken: Token);
        Assert.Equal(80, layout.Circles.Count); var scale = Math.Pow(layout.Circles[0].Radius, 2) / source[0].Value;
        foreach (var circle in layout.Circles)
        {
            Equal(scale * circle.Entry.AggregateValue, circle.Radius * circle.Radius);
            InsideSquare(circle); Assert.Same(snapshot.Nodes[circle.Entry.Index], circle.Entry);
        }
        for (var i = 0; i < layout.Circles.Count; i++)
            for (var j = i + 1; j < layout.Circles.Count; j++)
                Assert.True(Distance(layout.Circles[i].Center, layout.Circles[j].Center) + 1e-12 >= layout.Circles[i].Radius + layout.Circles[j].Radius);
        var again = ordered ? ChartHierarchy.PackedBubble(snapshot, cancellationToken: Token) : ChartHierarchy.BubbleCloud(snapshot, cancellationToken: Token);
        Assert.Equal(layout.Circles, again.Circles);
    }

    [Fact]
    public void Flat_packing_returns_terminals_and_drilled_paths_remain_original()
    {
        var snapshot = Forest(); var all = ChartHierarchy.PackedBubble(snapshot, cancellationToken: Token);
        Assert.Equal([1, 2, 3], all.Circles.Select(circle => circle.Entry.Index));
        var drilled = ChartHierarchy.BubbleCloud(snapshot, 0, cancellationToken: Token);
        Assert.Equal([1, 2], drilled.Circles.Select(circle => circle.Entry.Index));
        Equal(3, Math.Pow(drilled.Circles[1].Radius / drilled.Circles[0].Radius, 2));
        Assert.Equal([0, 1], drilled.Circles[1].Entry.Path);
    }

    [Fact]
    public void Hierarchical_circle_packing_encloses_every_child_and_separates_siblings()
    {
        var snapshot = ChartHierarchy.Snapshot([
            new(0, [new(0, [new(1), new(3), new(8)]), new(0, [new(5), new(2)]), new(9)]),
            new(6)], Token);
        var circles = ChartHierarchy.CirclePacking(snapshot, cancellationToken: Token).Circles;
        var byIndex = circles.ToDictionary(circle => circle.Entry.Index);
        var leaves = circles.Where(circle => circle.Entry.Children.Count == 0).ToArray();
        var scale = leaves[0].Radius * leaves[0].Radius / leaves[0].Entry.Value;
        foreach (var circle in circles)
        {
            InsideSquare(circle);
            if (circle.Entry.ParentIndex >= 0)
            {
                var parent = byIndex[circle.Entry.ParentIndex];
                Assert.True(Distance(parent.Center, circle.Center) + circle.Radius <= parent.Radius + 1e-12);
            }
        }
        foreach (var group in circles.GroupBy(circle => circle.Entry.ParentIndex))
        {
            var siblings = group.ToArray();
            for (var i = 0; i < siblings.Length; i++) for (var j = i + 1; j < siblings.Length; j++)
                Assert.True(Distance(siblings[i].Center, siblings[j].Center) + 1e-12 >= siblings[i].Radius + siblings[j].Radius);
        }
        foreach (var leaf in leaves) Equal(scale * leaf.Entry.Value, leaf.Radius * leaf.Radius);
        var drilled = ChartHierarchy.CirclePacking(snapshot, 1, cancellationToken: Token);
        Equal(.5, drilled.Circles[0].Center.X); Equal(.5, drilled.Circles[0].Center.Y); Equal(.5, drilled.Circles[0].Radius);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Empty_and_zero_sources_produce_finite_layouts(int kind)
    {
        var empty = ChartHierarchy.Snapshot([], Token); Assert.Equal(0, CountLayout(empty, kind));
        var zero = ChartHierarchy.Snapshot([new(0, [new(0), new(0)]), new(0)], Token);
        Assert.Equal(kind is 3 or 4 ? 3 : 4, CountLayout(zero, kind));
        Assert.All(ChartHierarchy.Treemap(zero, cancellationToken: Token).Rectangles, rect => Equal(0, rect.Width * rect.Height));
        Assert.All(ChartHierarchy.Sunburst(zero, cancellationToken: Token).Sectors, sector => Equal(0, sector.SweepAngle));
        Assert.All(ChartHierarchy.CirclePacking(zero, cancellationToken: Token).Circles, circle => { Equal(0, circle.Radius); Equal(.5, circle.Center.X); Equal(.5, circle.Center.Y); });
    }

    [Fact]
    public void Tiny_equal_weights_and_large_finite_weights_keep_proportions()
    {
        foreach (var weight in new[] { double.Epsilon, double.MaxValue / 4 })
        {
            var snapshot = ChartHierarchy.Snapshot([new(weight), new(weight)], Token);
            var rectangles = ChartHierarchy.Treemap(snapshot, cancellationToken: Token).Rectangles;
            Equal(.5, rectangles[0].Width * rectangles[0].Height);
            var circles = ChartHierarchy.PackedBubble(snapshot, cancellationToken: Token).Circles;
            Equal(circles[0].Radius, circles[1].Radius);
            Equal(180, ChartHierarchy.Sunburst(snapshot, cancellationToken: Token).Sectors[0].SweepAngle);
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void Invalid_values_are_rejected_even_on_nonleaf_nodes(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Snapshot([new(value, [new(1)])], Token));

    [Fact]
    public void Shared_node_instances_and_overflowing_branch_totals_are_rejected()
    {
        var shared = new HierarchyNode(1);
        Assert.Throws<ArgumentException>(() => ChartHierarchy.Snapshot([new(0, [shared]), new(0, [shared])], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Snapshot([new(0, [new(double.MaxValue), new(double.MaxValue)])], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Snapshot([new(double.MaxValue), new(double.MaxValue)], Token));
    }

    [Fact]
    public void Exact_budget_boundaries_remain_supported_and_deep_input_never_recurses()
    {
        var wide = ChartHierarchy.Snapshot(Enumerable.Range(0, ChartHierarchy.MaximumCount).Select(_ => new HierarchyNode(1)), Token);
        Assert.Equal(ChartHierarchy.MaximumCount, wide.Nodes.Count);
        HierarchyNode chain = new(1);
        for (var i = 0; i < ChartHierarchy.MaximumDepth; i++) chain = new(0, [chain]);
        var deep = ChartHierarchy.Snapshot([chain], Token); Assert.Equal(ChartHierarchy.MaximumDepth, deep.Nodes[^1].Depth);
        Assert.Equal(ChartHierarchy.MaximumDepth + 1, ChartHierarchy.Tree(deep, cancellationToken: Token).Nodes.Count);
        var packing = ChartHierarchy.Snapshot([new(0, Enumerable.Range(0, ChartHierarchy.MaximumPackingCount - 1).Select(_ => new HierarchyNode(1)))], Token);
        var collapsed = new HashSet<int> { 0 };
        Assert.Single(ChartHierarchy.PackedBubble(packing, collapsedIndices: collapsed, cancellationToken: Token).Circles);
    }

    [Fact]
    public void Enumeration_cancellation_stops_snapshot_before_publishing_partial_data()
    {
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var visited = 0;
        IEnumerable<HierarchyNode> Source()
        {
            visited++; yield return new(1);
            cancelled.Cancel(); visited++; yield return new(2);
            visited++; yield return new(3);
        }
        Assert.Throws<OperationCanceledException>(() => ChartHierarchy.Snapshot(Source(), cancelled.Token));
        Assert.Equal(2, visited);
    }

    [Fact]
    public void Count_depth_and_packing_budgets_include_hidden_descendants()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Snapshot(Enumerable.Range(0, ChartHierarchy.MaximumCount + 1).Select(_ => new HierarchyNode(1)), Token));
        HierarchyNode node = new(1);
        for (var i = 0; i <= ChartHierarchy.MaximumDepth; i++) node = new(0, [node]);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Snapshot([node], Token));
        var snapshot = ChartHierarchy.Snapshot([new(0, Enumerable.Range(0, ChartHierarchy.MaximumPackingCount).Select(_ => new HierarchyNode(1)))], Token);
        var collapsed = new HashSet<int> { 0 };
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.PackedBubble(snapshot, 0, collapsed, Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.CirclePacking(snapshot, 0, collapsed, Token));
        Assert.Equal(1, CountLayout(ChartHierarchy.Snapshot([new(1)], Token), 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Invalid_root_and_collapsed_indices_are_rejected_for_all_layouts(int kind)
    {
        var snapshot = Forest();
        Assert.Throws<ArgumentOutOfRangeException>(() => CountLayout(snapshot, kind, 99));
        Assert.Throws<ArgumentOutOfRangeException>(() => CountLayout(snapshot, kind, -2));
        Assert.Throws<ArgumentOutOfRangeException>(() => CountLayout(snapshot, kind, collapsed: new HashSet<int> { -1 }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Cancellation_is_honored_before_any_layout_work(int kind)
    {
        var snapshot = Forest(); using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => CountLayout(snapshot, kind, token: cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => ChartHierarchy.Snapshot([], cancelled.Token));
    }

    [Fact]
    public void Positive_weight_normalization_underflow_is_explicit_in_area_layouts()
    {
        var snapshot = ChartHierarchy.Snapshot([new(double.Epsilon), new(double.MaxValue)], Token);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Treemap(snapshot, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Sunburst(snapshot, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.PackedBubble(snapshot, cancellationToken: Token));
        Assert.Equal(2, ChartHierarchy.Tree(snapshot, cancellationToken: Token).Nodes.Count);
    }

    [Fact]
    public void Packing_rejects_tiny_disks_whose_distinct_normalized_centers_cannot_be_represented()
    {
        var snapshot = ChartHierarchy.Snapshot([new(1), new(double.Epsilon), new(double.Epsilon)], Token);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.PackedBubble(snapshot, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.CirclePacking(snapshot, cancellationToken: Token));
    }

    [Fact]
    public void Invalid_treemap_and_annulus_options_are_rejected()
    {
        var snapshot = Forest();
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Treemap(snapshot, mode: (TreemapLayoutMode)99, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Sunburst(snapshot, innerRadius: .5, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Sunburst(snapshot, innerRadius: -.01, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Sunburst(snapshot, startAngle: double.PositiveInfinity, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Sunburst(snapshot, sweepAngle: 0, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchy.Sunburst(snapshot, sweepAngle: 361, cancellationToken: Token));
    }

    private static int CountLayout(HierarchySnapshot snapshot, int kind, int root = -1, IReadOnlySet<int>? collapsed = null, CancellationToken? token = null)
    {
        var cancellation = token ?? Token;
        return kind switch
        {
            0 => ChartHierarchy.Treemap(snapshot, root, collapsed, cancellationToken: cancellation).Rectangles.Count,
            1 => ChartHierarchy.Sunburst(snapshot, root, collapsed, cancellationToken: cancellation).Sectors.Count,
            2 => ChartHierarchy.Tree(snapshot, root, collapsed, cancellation).Nodes.Count,
            3 => ChartHierarchy.BubbleCloud(snapshot, root, collapsed, cancellation).Circles.Count,
            4 => ChartHierarchy.PackedBubble(snapshot, root, collapsed, cancellation).Circles.Count,
            _ => ChartHierarchy.CirclePacking(snapshot, root, collapsed, cancellation).Circles.Count
        };
    }
    private static void Contained(HierarchyRectangle parent, HierarchyRectangle child)
    {
        Assert.True(child.X >= parent.X - 1e-12 && child.Y >= parent.Y - 1e-12);
        Assert.True(child.X + child.Width <= parent.X + parent.Width + 1e-12 && child.Y + child.Height <= parent.Y + parent.Height + 1e-12);
    }
    private static void Separate(HierarchyRectangle a, HierarchyRectangle b) =>
        Assert.True(a.X + a.Width <= b.X + 1e-12 || b.X + b.Width <= a.X + 1e-12 ||
                    a.Y + a.Height <= b.Y + 1e-12 || b.Y + b.Height <= a.Y + 1e-12);
    private static void InsideSquare(HierarchyCircle circle)
    {
        Assert.True(circle.Center.X - circle.Radius >= -1e-12 && circle.Center.Y - circle.Radius >= -1e-12);
        Assert.True(circle.Center.X + circle.Radius <= 1 + 1e-12 && circle.Center.Y + circle.Radius <= 1 + 1e-12);
    }
}
