namespace NT.AvaloniaControl.Charts;

public enum RelationNodeShape { Rectangle, RoundedRectangle, Ellipse, Diamond, Hexagon }
public enum RelationSankeyMode { Sankey, Alluvial }
public enum RelationNetworkMode { ForceDirected, Circular, Arc }
public enum RelationFlowMode { Layered, Explicit, Mindmap, Process }

/// <summary>An immutable original node. Position is a normalized center; dimensions apply to flowcharts.</summary>
public sealed class GraphNode
{
    public GraphNode(object id, string? label = null, object? source = null, string? group = null,
        RelationNodeShape shape = RelationNodeShape.Rectangle, ChartPoint? position = null,
        double weight = 1, double width = .12, double height = .08, int? stage = null)
    { Id = id; Label = label; Source = source; Group = group; Shape = shape; Position = position;
      Weight = weight; Width = width; Height = height; Stage = stage; }
    public object Id { get; }
    public string? Label { get; }
    public object? Source { get; }
    public string? Group { get; }
    public RelationNodeShape Shape { get; }
    public ChartPoint? Position { get; }
    public double? X => Position?.X;
    public double? Y => Position?.Y;
    public double Weight { get; }
    public double Width { get; }
    public double Height { get; }
    public int? Stage { get; }
}

/// <summary>An immutable directed original edge. Parallel edges and self loops remain distinct.</summary>
public sealed class GraphEdge
{
    public GraphEdge(object sourceId, object targetId, double weight = 1, string? label = null,
        object? source = null, object? id = null)
    { SourceId = sourceId; TargetId = targetId; Weight = weight; Label = label; Source = source; Id = id; }
    public object SourceId { get; }
    public object TargetId { get; }
    public double Weight { get; }
    public string? Label { get; }
    public object? Source { get; }
    public object? Id { get; }
}

public sealed class GraphNodeEntry
{
    internal GraphNodeEntry(int index, GraphNode node, double incoming, double outgoing)
    { Index = index; Node = node; IncomingWeight = incoming; OutgoingWeight = outgoing; }
    public int Index { get; }
    public int SourceIndex => Index;
    public GraphNode Node { get; }
    public object Id => Node.Id;
    public object Source => Node.Source ?? Node;
    public string? Label => Node.Label;
    public double IncomingWeight { get; }
    public double OutgoingWeight { get; }
}

public sealed class GraphEdgeEntry
{
    internal GraphEdgeEntry(int index, GraphEdge edge, int source, int target)
    { Index = index; Edge = edge; SourceNodeIndex = source; TargetNodeIndex = target; }
    public int Index { get; }
    public int SourceIndex => Index;
    public GraphEdge Edge { get; }
    public object Source => Edge.Source ?? Edge;
    public object? Id => Edge.Id;
    public string? Label => Edge.Label;
    public int SourceNodeIndex { get; }
    public int TargetNodeIndex { get; }
    public double Weight => Edge.Weight;
}

/// <summary>An owned graph snapshot with original enumeration indices and validated references.</summary>
public sealed class GraphSnapshot
{
    internal GraphSnapshot(GraphNodeEntry[] nodes, GraphEdgeEntry[] edges)
    { Nodes = Array.AsReadOnly(nodes); Edges = Array.AsReadOnly(edges); }
    public IReadOnlyList<GraphNodeEntry> Nodes { get; }
    public IReadOnlyList<GraphEdgeEntry> Edges { get; }
}

/// <summary>A normalized top-left rectangle; Center is its center in the same unit square.</summary>
public sealed record RelationNodeLayout(GraphNodeEntry Entry, double X, double Y, double Width, double Height)
{
    public ChartPoint Center => new(X + Width * .5, Y + Height * .5);
}

/// <summary>A sampled normalized centerline. Thickness is the full ribbon width, not half width.</summary>
public sealed class RelationEdgeLayout
{
    internal RelationEdgeLayout(GraphEdgeEntry entry, ChartPoint[] points, double thickness, bool isBackEdge)
    { Entry = entry; Points = Array.AsReadOnly(points); Thickness = thickness; IsBackEdge = isBackEdge; }
    public GraphEdgeEntry Entry { get; }
    public IReadOnlyList<ChartPoint> Points { get; }
    public double Thickness { get; }
    public bool IsBackEdge { get; }
}

public sealed record RelationFlowImbalance(GraphNodeEntry Node, double IncomingWeight, double OutgoingWeight)
{
    public double Delta => OutgoingWeight - IncomingWeight;
}
public sealed record RelationGroupLayout(string Group, double X, double Y, double Width, double Height);

public sealed class RelationGraphLayout
{
    internal RelationGraphLayout(RelationNodeLayout[] nodes, RelationEdgeLayout[] edges,
        RelationFlowImbalance[] imbalances, double scale = 0, int iterations = 0,
        int collisions = 0, bool converged = true, double displacement = 0, bool fallback = false)
    {
        Nodes = Array.AsReadOnly(nodes); Edges = Array.AsReadOnly(edges);
        Imbalances = Array.AsReadOnly(imbalances);
        IsWeightScaleSaturated = double.IsPositiveInfinity(scale);
        WeightScale = IsWeightScaleSaturated ? double.MaxValue : scale; Iterations = iterations;
        RemainingCollisionCount = collisions; IsConverged = converged;
        MaximumDisplacement = displacement; FallbackApplied = fallback;
        Groups = Array.AsReadOnly(nodes.Where(node => !string.IsNullOrEmpty(node.Entry.Node.Group))
            .GroupBy(node => node.Entry.Node.Group!, StringComparer.Ordinal).Select(group =>
            {
                var left = Math.Max(0, group.Min(n => n.X) - .015);
                var top = Math.Max(0, group.Min(n => n.Y) - .015);
                return new RelationGroupLayout(group.Key, left, top,
                    Math.Min(1, group.Max(n => n.X + n.Width) + .015) - left,
                    Math.Min(1, group.Max(n => n.Y + n.Height) + .015) - top);
            }).ToArray());
    }
    public IReadOnlyList<RelationNodeLayout> Nodes { get; }
    public IReadOnlyList<RelationEdgeLayout> Edges { get; }
    public IReadOnlyList<RelationGroupLayout> Groups { get; }
    public IReadOnlyList<RelationFlowImbalance> Imbalances { get; }
    /// <summary>Normalized ribbon width per original weight. Saturates at Double.MaxValue when the
    /// diagnostic ratio exceeds finite range; geometry always uses normalized weight ratios directly.</summary>
    public double WeightScale { get; }
    /// <summary>True only when WeightScale was saturated; multiplying that diagnostic by an original
    /// subnormal weight does not reconstruct ribbon width. Actual geometry remains unsaturated.</summary>
    public bool IsWeightScaleSaturated { get; }
    public int Iterations { get; }
    public int RemainingCollisionCount { get; }
    public bool IsConverged { get; }
    public double MaximumDisplacement { get; }
    public bool FallbackApplied { get; }
}

public sealed record RelationChordNode(GraphNodeEntry Entry, double StartAngle, double SweepAngle);
public sealed record RelationChordRibbon(GraphEdgeEntry Entry, double SourceStartAngle, double SourceSweepAngle,
    double TargetStartAngle, double TargetSweepAngle);
public sealed class RelationChordLayout
{
    internal RelationChordLayout(RelationChordNode[] nodes, RelationChordRibbon[] ribbons, double padding)
    { Nodes = Array.AsReadOnly(nodes); Ribbons = Array.AsReadOnly(ribbons); PaddingDegrees = padding; }
    public IReadOnlyList<RelationChordNode> Nodes { get; }
    public IReadOnlyList<RelationChordRibbon> Ribbons { get; }
    public double PaddingDegrees { get; }
}

/// <summary>Source-preserving deterministic graph layouts, with explicit work and input budgets.</summary>
public static partial class ChartRelations
{
    public const int MaximumNodeCount = 2_000;
    public const int MaximumEdgeCount = 10_000;
    public const int MaximumForceNodeCount = 400;
    public const int MaximumForceIterations = 300;
    public const int MaximumCrossingIterations = 32;

    public static GraphSnapshot Snapshot(IEnumerable<GraphNode> nodes, IEnumerable<GraphEdge> edges,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nodes); ArgumentNullException.ThrowIfNull(edges);
        cancellationToken.ThrowIfCancellationRequested();
        var ownedNodes = new List<GraphNode>(); var indices = new Dictionary<object, int>();
        foreach (var node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(node);
            if (ownedNodes.Count >= MaximumNodeCount) throw new ArgumentOutOfRangeException(nameof(nodes), "Graph node budget exceeded.");
            ArgumentNullException.ThrowIfNull(node.Id); Nonnegative(node.Weight, nameof(node.Weight));
            PositiveUnit(node.Width, nameof(node.Width)); PositiveUnit(node.Height, nameof(node.Height));
            if (!Enum.IsDefined(node.Shape)) throw new ArgumentOutOfRangeException(nameof(nodes), "Unknown node shape.");
            if (node.Stage < 0) throw new ArgumentOutOfRangeException(nameof(nodes), "Stages must be nonnegative.");
            if (node.Position is { } position && (!double.IsFinite(position.X) || !double.IsFinite(position.Y)
                || position.X < 0 || position.X > 1 || position.Y < 0 || position.Y > 1))
                throw new ArgumentOutOfRangeException(nameof(nodes), "Node centers must be finite normalized coordinates.");
            if (!indices.TryAdd(node.Id, ownedNodes.Count)) throw new ArgumentException("Node IDs must be unique.", nameof(nodes));
            ownedNodes.Add(node);
        }
        var ownedEdges = new List<GraphEdgeEntry>(); var edgeIds = new HashSet<object>();
        var incoming = new double[ownedNodes.Count]; var outgoing = new double[ownedNodes.Count];
        foreach (var edge in edges)
        {
            cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(edge);
            if (ownedEdges.Count >= MaximumEdgeCount) throw new ArgumentOutOfRangeException(nameof(edges), "Graph edge budget exceeded.");
            Nonnegative(edge.Weight, nameof(edge.Weight));
            if (edge.Id is not null && !edgeIds.Add(edge.Id)) throw new ArgumentException("Non-null edge IDs must be unique.", nameof(edges));
            if (edge.SourceId is null || edge.TargetId is null || !indices.TryGetValue(edge.SourceId, out var source)
                || !indices.TryGetValue(edge.TargetId, out var target)) throw new ArgumentException("An edge references a missing node.", nameof(edges));
            incoming[target] = FiniteSum(incoming[target], edge.Weight);
            outgoing[source] = FiniteSum(outgoing[source], edge.Weight);
            ownedEdges.Add(new(ownedEdges.Count, edge, source, target));
        }
        return new(ownedNodes.Select((node, index) => new GraphNodeEntry(index, node, incoming[index], outgoing[index])).ToArray(), ownedEdges.ToArray());
    }

    private static double FiniteSum(double a, double b)
    { var sum = a + b; if (!double.IsFinite(sum)) throw new ArgumentOutOfRangeException(nameof(b), "Aggregate graph weight exceeds finite Double range."); return sum; }
    private static void Nonnegative(double value, string name)
    { if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name); }
    private static void PositiveUnit(double value, string name)
    { if (!double.IsFinite(value) || value <= 0 || value > 1) throw new ArgumentOutOfRangeException(name); }
    private static void SnapshotArgument(GraphSnapshot snapshot, CancellationToken token)
    { ArgumentNullException.ThrowIfNull(snapshot); token.ThrowIfCancellationRequested(); }
    private static double LargestWeight(GraphSnapshot graph) => graph.Edges.Count == 0 ? 0 : graph.Edges.Max(edge => edge.Weight);
    private static RelationFlowImbalance[] Imbalances(GraphSnapshot graph) => graph.Nodes
        .Where(node => node.IncomingWeight > 0 && node.OutgoingWeight > 0
            && Math.Abs(node.IncomingWeight - node.OutgoingWeight) / Math.Max(node.IncomingWeight, node.OutgoingWeight) > 1e-10)
        .Select(node => new RelationFlowImbalance(node, node.IncomingWeight, node.OutgoingWeight)).ToArray();

    private static ChartPoint[] Cubic(ChartPoint a, ChartPoint b, ChartPoint c, ChartPoint d)
    {
        var points = new ChartPoint[25];
        for (var i = 0; i < points.Length; i++)
        { var t = i / 24d; var s = 1 - t; points[i] = new(s * s * s * a.X + 3 * s * s * t * b.X + 3 * s * t * t * c.X + t * t * t * d.X,
            s * s * s * a.Y + 3 * s * s * t * b.Y + 3 * s * t * t * c.Y + t * t * t * d.Y); }
        return points;
    }

    // Iterative Kosaraju avoids recursion at the maximum graph depth.
    private static int[] Layers(GraphSnapshot graph, CancellationToken token)
    {
        var count = graph.Nodes.Count; var adjacency = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
        var reverse = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
        foreach (var edge in graph.Edges) { adjacency[edge.SourceNodeIndex].Add(edge.TargetNodeIndex); reverse[edge.TargetNodeIndex].Add(edge.SourceNodeIndex); }
        var seen = new bool[count]; var order = new List<int>(count);
        for (var root = 0; root < count; root++)
        {
            token.ThrowIfCancellationRequested(); if (seen[root]) continue;
            var stack = new Stack<(int Node, int Next)>(); seen[root] = true; stack.Push((root, 0));
            while (stack.TryPop(out var item))
            {
                token.ThrowIfCancellationRequested();
                if (item.Next >= adjacency[item.Node].Count) { order.Add(item.Node); continue; }
                stack.Push((item.Node, item.Next + 1)); var child = adjacency[item.Node][item.Next];
                if (!seen[child]) { seen[child] = true; stack.Push((child, 0)); }
            }
        }
        var component = Enumerable.Repeat(-1, count).ToArray(); var componentCount = 0;
        for (var i = order.Count - 1; i >= 0; i--)
        {
            token.ThrowIfCancellationRequested(); var root = order[i]; if (component[root] >= 0) continue;
            var stack = new Stack<int>(); stack.Push(root); component[root] = componentCount;
            while (stack.TryPop(out var node))
            { token.ThrowIfCancellationRequested(); foreach (var child in reverse[node]) if (component[child] < 0) { component[child] = componentCount; stack.Push(child); } }
            componentCount++;
        }
        var exits = Enumerable.Range(0, componentCount).Select(_ => new HashSet<int>()).ToArray(); var degree = new int[componentCount];
        foreach (var edge in graph.Edges)
        { var a = component[edge.SourceNodeIndex]; var b = component[edge.TargetNodeIndex]; if (a != b && exits[a].Add(b)) degree[b]++; }
        var levels = new int[componentCount]; var queue = new Queue<int>(Enumerable.Range(0, componentCount).Where(i => degree[i] == 0));
        while (queue.TryDequeue(out var node))
        { token.ThrowIfCancellationRequested(); foreach (var child in exits[node].Order()) { levels[child] = Math.Max(levels[child], levels[node] + 1); if (--degree[child] == 0) queue.Enqueue(child); } }
        return component.Select(c => levels[c]).ToArray();
    }
}
