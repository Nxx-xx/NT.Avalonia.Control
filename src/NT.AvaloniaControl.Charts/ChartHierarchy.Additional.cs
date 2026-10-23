namespace NT.AvaloniaControl.Charts;

/// <summary>LeafTotal uses the common hierarchy sum; Inclusive preserves branch self cost and requires each branch to cover its children.</summary>
public enum HierarchyStackValueMode { LeafTotal, Inclusive }
public enum HierarchyLinkStyle { Elbow, Straight }
public sealed record HierarchyRadialNode(HierarchyEntry Entry, ChartPoint Position, double Angle, double Radius);
public sealed class HierarchyRadialLayout
{
    internal HierarchyRadialLayout(HierarchyRadialNode[] nodes, HierarchyTreeEdge[] edges) { Nodes = Array.AsReadOnly(nodes); Edges = Array.AsReadOnly(edges); }
    public IReadOnlyList<HierarchyRadialNode> Nodes { get; }
    public IReadOnlyList<HierarchyTreeEdge> Edges { get; }
}
public sealed record HierarchyDendrogramNode(HierarchyEntry Entry, ChartPoint Position, double MergeHeight);
public sealed class HierarchyDendrogramLayout
{
    internal HierarchyDendrogramLayout(HierarchyDendrogramNode[] nodes, HierarchyTreeEdge[] edges, double maximumHeight)
    { Nodes = Array.AsReadOnly(nodes); Edges = Array.AsReadOnly(edges); MaximumHeight = maximumHeight; }
    public IReadOnlyList<HierarchyDendrogramNode> Nodes { get; }
    public IReadOnlyList<HierarchyTreeEdge> Edges { get; }
    public double MaximumHeight { get; }
}

public static partial class ChartHierarchy
{
    /// <summary>Source-ordered width partitions with one equal band per visible depth. Inclusive mode leaves branch self cost as unoccupied child width.</summary>
    public static HierarchyRectangleLayout Icicle(HierarchySnapshot snapshot, int rootIndex = -1,
        IReadOnlySet<int>? collapsedIndices = null, HierarchyStackValueMode valueMode = HierarchyStackValueMode.LeafTotal,
        CancellationToken cancellationToken = default) => StackedHierarchy(snapshot, rootIndex, collapsedIndices, valueMode, false, cancellationToken);

    /// <summary>The same cost partition as Icicle, with roots at the bottom and deeper calls above.</summary>
    public static HierarchyRectangleLayout Flame(HierarchySnapshot snapshot, int rootIndex = -1,
        IReadOnlySet<int>? collapsedIndices = null, HierarchyStackValueMode valueMode = HierarchyStackValueMode.Inclusive,
        CancellationToken cancellationToken = default) => StackedHierarchy(snapshot, rootIndex, collapsedIndices, valueMode, true, cancellationToken);

    private static HierarchyRectangleLayout StackedHierarchy(HierarchySnapshot snapshot, int rootIndex,
        IReadOnlySet<int>? collapsed, HierarchyStackValueMode mode, bool flame, CancellationToken token)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var visible = Scope(snapshot, rootIndex, collapsed, token);
        if (visible.Count == 0) return new([]);
        double Weight(int index) => mode == HierarchyStackValueMode.Inclusive ? snapshot.Nodes[index].Value : snapshot.Nodes[index].AggregateValue;
        // Validate the entire selected subtree, including collapsed descendants, before any geometry is published.
        if (mode == HierarchyStackValueMode.Inclusive)
            foreach (var index in Scope(snapshot, rootIndex, null, token))
            {
                token.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[index]; double sum = 0;
                foreach (var child in entry.Children)
                { sum += Weight(child); if (!double.IsFinite(sum)) throw new ArgumentOutOfRangeException(nameof(snapshot), "Inclusive child sum exceeds the finite range."); }
                if (sum > entry.Value) throw new ArgumentException("Inclusive branch cost must cover the sum of its children.", nameof(snapshot));
            }
        var baseDepth = rootIndex < 0 ? 0 : snapshot.Nodes[rootIndex].Depth;
        var levels = visible.Max(i => snapshot.Nodes[i].Depth - baseDepth) + 1; var band = 1d / levels;
        var rectangles = new HierarchyRectangle?[snapshot.Nodes.Count];
        var roots = ScopeRoots(snapshot, rootIndex); double total = 0;
        foreach (var index in roots) { total += Weight(index); if (!double.IsFinite(total)) throw new ArgumentOutOfRangeException(nameof(snapshot)); }
        Partition(roots, 0, 1, total);
        foreach (var index in visible)
        {
            token.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[index];
            if (!Terminal(entry, collapsed)) { var parent = rectangles[index]!; Partition(entry.Children, parent.X, parent.Width, Weight(index)); }
        }
        return new(visible.Select(i => rectangles[i]!).ToArray());

        void Partition(IReadOnlyList<int> indices, double x, double width, double parentWeight)
        {
            double cursor = 0;
            foreach (var index in indices)
            {
                token.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[index]; var weight = Weight(index);
                var w = weight == 0 || parentWeight == 0 ? 0 : width * Share(weight, parentWeight);
                if (weight > 0 && width > 0 && w == 0) throw new ArgumentOutOfRangeException(nameof(snapshot), "A positive stack width cannot be represented.");
                var depth = entry.Depth - baseDepth; var y = (flame ? levels - depth - 1 : depth) * band;
                rectangles[index] = new(entry, x + cursor, y, w, band); cursor += w;
            }
        }
    }

    /// <summary>Visible preorder rows in the unit square. Indentation is a fraction of total width and is capped to leave one row-width unit.</summary>
    public static HierarchyRectangleLayout Indented(HierarchySnapshot snapshot, int rootIndex = -1,
        IReadOnlySet<int>? collapsedIndices = null, double indent = .04, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(indent) || indent < 0 || indent > 1) throw new ArgumentOutOfRangeException(nameof(indent));
        var visible = Scope(snapshot, rootIndex, collapsedIndices, cancellationToken); if (visible.Count == 0) return new([]);
        var depth = rootIndex < 0 ? 0 : snapshot.Nodes[rootIndex].Depth; var height = 1d / visible.Count;
        return new(visible.Select((index, row) =>
        { cancellationToken.ThrowIfCancellationRequested(); var e = snapshot.Nodes[index]; var x = Math.Min(.9, (e.Depth - depth) * indent); return new HierarchyRectangle(e, x, row * height, 1 - x, height); }).ToArray());
    }

    /// <summary>Terminal nodes occupy source-ordered circular lanes. A single root is central; forest roots occupy the first nonzero radius. No clustering is inferred.</summary>
    public static HierarchyRadialLayout RadialTree(HierarchySnapshot snapshot, int rootIndex = -1,
        IReadOnlySet<int>? collapsedIndices = null, double startAngle = -90, double sweepAngle = 360,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(startAngle)) throw new ArgumentOutOfRangeException(nameof(startAngle));
        if (!double.IsFinite(sweepAngle) || sweepAngle <= 0 || sweepAngle > 360) throw new ArgumentOutOfRangeException(nameof(sweepAngle));
        var visible = Scope(snapshot, rootIndex, collapsedIndices, cancellationToken); if (visible.Count == 0) return new([], []);
        var (first, last, terminalCount) = TerminalLanes(snapshot, visible, collapsedIndices, cancellationToken);
        var baseDepth = rootIndex < 0 ? 0 : snapshot.Nodes[rootIndex].Depth; var maxDepth = visible.Max(i => snapshot.Nodes[i].Depth - baseDepth);
        var forest = ScopeRoots(snapshot, rootIndex).Length > 1; var denominator = Math.Max(1, maxDepth + (forest ? 1 : 0));
        var nodes = new List<HierarchyRadialNode>(); var edges = new List<HierarchyTreeEdge>(); var points = new ChartPoint?[snapshot.Nodes.Count];
        foreach (var index in visible)
        {
            cancellationToken.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[index];
            var angle = startAngle % 360 + (first[index] + last[index] + 1) / (2 * terminalCount) * sweepAngle;
            var radius = .44 * (entry.Depth - baseDepth + (forest ? 1 : 0)) / denominator;
            var radians = angle * Math.PI / 180; var point = new ChartPoint(.5 + radius * Math.Cos(radians), .5 + radius * Math.Sin(radians));
            nodes.Add(new(entry, point, angle, radius)); points[index] = point;
            if (entry.ParentIndex >= 0 && points[entry.ParentIndex] is { } parent) edges.Add(new(snapshot.Nodes[entry.ParentIndex], entry, parent, point));
        }
        return new(nodes.ToArray(), edges.ToArray());
    }

    /// <summary>Renders an existing cluster tree. Explicit merge heights are raw node values and must be nondecreasing toward each parent; false uses structural heights.</summary>
    public static HierarchyDendrogramLayout Dendrogram(HierarchySnapshot snapshot, int rootIndex = -1,
        IReadOnlySet<int>? collapsedIndices = null, bool useMergeHeights = false, CancellationToken cancellationToken = default)
    {
        var visible = Scope(snapshot, rootIndex, collapsedIndices, cancellationToken); if (visible.Count == 0) return new([], [], 0);
        var all = Scope(snapshot, rootIndex, null, cancellationToken); var heights = new double[snapshot.Nodes.Count];
        for (var i = all.Count - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[all[i]]; var childMaximum = entry.Children.Count == 0 ? 0 : entry.Children.Max(c => heights[c]);
            if (useMergeHeights && entry.Value < childMaximum) throw new ArgumentException("Merge heights must be nondecreasing toward parents.", nameof(snapshot));
            heights[entry.Index] = useMergeHeights ? entry.Value : entry.Children.Count == 0 ? 0 : childMaximum + 1;
        }
        var maximum = ScopeRoots(snapshot, rootIndex).Max(i => heights[i]);
        var (first, last, terminalCount) = TerminalLanes(snapshot, visible, collapsedIndices, cancellationToken);
        var nodes = new List<HierarchyDendrogramNode>(); var edges = new List<HierarchyTreeEdge>(); var points = new ChartPoint?[snapshot.Nodes.Count];
        foreach (var index in visible)
        {
            cancellationToken.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[index];
            var point = new ChartPoint(.05 + .9 * ((first[index] + last[index]) * .5 + .5) / terminalCount,
                maximum == 0 ? .5 : .05 + .9 * (1 - heights[index] / maximum));
            nodes.Add(new(entry, point, heights[index])); points[index] = point;
            if (entry.ParentIndex >= 0 && points[entry.ParentIndex] is { } parent) edges.Add(new(snapshot.Nodes[entry.ParentIndex], entry, parent, point));
        }
        return new(nodes.ToArray(), edges.ToArray(), maximum);
    }

    private static (double[] First, double[] Last, int Count) TerminalLanes(HierarchySnapshot snapshot, List<int> visible,
        IReadOnlySet<int>? collapsed, CancellationToken token)
    {
        var first = new double[snapshot.Nodes.Count]; var last = new double[snapshot.Nodes.Count]; var lane = 0;
        foreach (var index in visible) { token.ThrowIfCancellationRequested(); if (Terminal(snapshot.Nodes[index], collapsed)) first[index] = last[index] = lane++; }
        for (var i = visible.Count - 1; i >= 0; i--)
        { token.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[visible[i]]; if (!Terminal(entry, collapsed)) { first[entry.Index] = first[entry.Children[0]]; last[entry.Index] = last[entry.Children[^1]]; } }
        return (first, last, lane);
    }
}
