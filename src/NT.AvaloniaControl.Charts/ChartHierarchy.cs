namespace NT.AvaloniaControl.Charts;

/// <summary>An immutable hierarchy input. Only leaf values contribute to branch totals.</summary>
public sealed class HierarchyNode
{
    public HierarchyNode(double value = 0, IEnumerable<HierarchyNode>? children = null, string? label = null,
        object? source = null, object? id = null)
    {
        Value = value; Label = label; Source = source; Id = id;
        var owned = new List<HierarchyNode>();
        if (children is not null)
            foreach (var child in children)
            {
                ArgumentNullException.ThrowIfNull(child);
                if (owned.Count >= ChartHierarchy.MaximumCount)
                    throw new ArgumentOutOfRangeException(nameof(children), "Hierarchy child budget exceeded.");
                owned.Add(child);
            }
        Children = Array.AsReadOnly(owned.ToArray());
    }
    public double Value { get; }
    public string? Label { get; }
    public object? Source { get; }
    public object? Id { get; }
    public IReadOnlyList<HierarchyNode> Children { get; }
}

/// <summary>A validated preorder entry retaining its original sibling index and full tree path.</summary>
public sealed class HierarchyEntry
{
    internal HierarchyEntry(int index, int parentIndex, int sourceIndex, int[] path, HierarchyNode node,
        double aggregateValue, int[] children)
    {
        Index = index; ParentIndex = parentIndex; SourceIndex = sourceIndex; Path = Array.AsReadOnly(path);
        Node = node; AggregateValue = aggregateValue; Children = Array.AsReadOnly(children);
    }
    public int Index { get; }
    public int ParentIndex { get; }
    public int SourceIndex { get; }
    public int Depth => Path.Count - 1;
    public IReadOnlyList<int> Path { get; }
    public HierarchyNode Node { get; }
    public object? Source => Node.Source ?? Node;
    public object? Id => Node.Id;
    public string? Label => Node.Label;
    public double Value => Node.Value;
    public double AggregateValue { get; }
    public IReadOnlyList<int> Children { get; }
}

/// <summary>An owned validated forest; entry indices remain stable across layout, collapse and drill operations.</summary>
public sealed class HierarchySnapshot
{
    internal HierarchySnapshot(HierarchyEntry[] nodes, int[] roots)
    { Nodes = Array.AsReadOnly(nodes); Roots = Array.AsReadOnly(roots); }
    public IReadOnlyList<HierarchyEntry> Nodes { get; }
    public IReadOnlyList<int> Roots { get; }
}

public enum TreemapLayoutMode { Squarify, Slice }
public sealed record HierarchyRectangle(HierarchyEntry Entry, double X, double Y, double Width, double Height);
public sealed class HierarchyRectangleLayout
{
    internal HierarchyRectangleLayout(HierarchyRectangle[] rectangles) { Rectangles = Array.AsReadOnly(rectangles); }
    public IReadOnlyList<HierarchyRectangle> Rectangles { get; }
}
/// <summary>Angles are degrees clockwise from the positive X direction; center is (0.5,0.5).</summary>
public sealed record HierarchySector(HierarchyEntry Entry, double StartAngle, double SweepAngle,
    double InnerRadius, double OuterRadius);
public sealed class HierarchySectorLayout
{
    internal HierarchySectorLayout(HierarchySector[] sectors) { Sectors = Array.AsReadOnly(sectors); }
    public IReadOnlyList<HierarchySector> Sectors { get; }
}
public sealed record HierarchyTreeNode(HierarchyEntry Entry, double X, double Y, double Width, double Height);
public sealed record HierarchyTreeEdge(HierarchyEntry Parent, HierarchyEntry Child, ChartPoint Start, ChartPoint End);
public sealed class HierarchyTreeLayout
{
    internal HierarchyTreeLayout(HierarchyTreeNode[] nodes, HierarchyTreeEdge[] edges)
    { Nodes = Array.AsReadOnly(nodes); Edges = Array.AsReadOnly(edges); }
    public IReadOnlyList<HierarchyTreeNode> Nodes { get; }
    public IReadOnlyList<HierarchyTreeEdge> Edges { get; }
}
/// <summary>A normalized circle. Nested parent circles enclose their descendants and include packing whitespace.</summary>
public sealed record HierarchyCircle(HierarchyEntry Entry, ChartPoint Center, double Radius);
public sealed class HierarchyCircleLayout
{
    internal HierarchyCircleLayout(HierarchyCircle[] circles) { Circles = Array.AsReadOnly(circles); }
    public IReadOnlyList<HierarchyCircle> Circles { get; }
}

/// <summary>Deterministic, source-preserving hierarchy layouts in the normalized unit square.</summary>
public static partial class ChartHierarchy
{
    public const int MaximumCount = 10_000;
    /// <summary>Maximum zero-based entry depth; roots have depth zero.</summary>
    public const int MaximumDepth = 64;
    /// <summary>Maximum input nodes in the requested packing subtree, including collapsed descendants.</summary>
    public const int MaximumPackingCount = 2_000;

    private sealed class PendingEntry(HierarchyNode node, int parent, int sibling, int[] path)
    {
        public HierarchyNode Node { get; } = node;
        public int Parent { get; } = parent;
        public int Sibling { get; } = sibling;
        public int[] Path { get; } = path;
        public List<int> Children { get; } = [];
        public double Total { get; set; }
    }

    /// <summary>Validates and snapshots a forest iteratively. Shared node instances and cycles are rejected.</summary>
    public static HierarchySnapshot Snapshot(IEnumerable<HierarchyNode> source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); cancellationToken.ThrowIfCancellationRequested();
        var roots = new List<HierarchyNode>();
        foreach (var node in source)
        {
            cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(node);
            if (roots.Count >= MaximumCount) throw new ArgumentOutOfRangeException(nameof(source), "Hierarchy node budget exceeded.");
            roots.Add(node);
        }
        var work = new Stack<(HierarchyNode Node, int Parent, int Sibling, int[] Path)>();
        for (var i = roots.Count - 1; i >= 0; i--) work.Push((roots[i], -1, i, [i]));
        var entries = new List<PendingEntry>(); var rootIndices = new List<int>();
        var seen = new HashSet<HierarchyNode>(ReferenceEqualityComparer.Instance);
        while (work.TryPop(out var frame))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entries.Count >= MaximumCount) throw new ArgumentOutOfRangeException(nameof(source), "Hierarchy node budget exceeded.");
            if (frame.Path.Length - 1 > MaximumDepth) throw new ArgumentOutOfRangeException(nameof(source), "Hierarchy depth budget exceeded.");
            if (!seen.Add(frame.Node)) throw new ArgumentException("Hierarchy contains a cycle or a repeated node instance.", nameof(source));
            if (!double.IsFinite(frame.Node.Value) || frame.Node.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(source), "Hierarchy values must be finite and nonnegative.");
            var index = entries.Count;
            entries.Add(new(frame.Node, frame.Parent, frame.Sibling, frame.Path));
            if (frame.Parent < 0) rootIndices.Add(index); else entries[frame.Parent].Children.Add(index);
            if (entries.Count + work.Count + frame.Node.Children.Count > MaximumCount)
                throw new ArgumentOutOfRangeException(nameof(source), "Hierarchy node budget exceeded.");
            for (var i = frame.Node.Children.Count - 1; i >= 0; i--)
            {
                var path = new int[frame.Path.Length + 1]; frame.Path.CopyTo(path, 0); path[^1] = i;
                work.Push((frame.Node.Children[i], index, i, path));
            }
        }
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested(); var item = entries[i];
            if (item.Children.Count == 0) item.Total = item.Node.Value;
            else
            {
                double sum = 0, correction = 0;
                foreach (var child in item.Children)
                {
                    var adjusted = entries[child].Total - correction; var next = sum + adjusted;
                    correction = (next - sum) - adjusted; sum = next;
                    if (!double.IsFinite(sum)) throw new ArgumentOutOfRangeException(nameof(source), "Hierarchy aggregate exceeds the finite range.");
                }
                item.Total = sum;
            }
        }
        double forestTotal = 0, forestCorrection = 0;
        foreach (var index in rootIndices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var adjusted = entries[index].Total - forestCorrection; var next = forestTotal + adjusted;
            forestCorrection = (next - forestTotal) - adjusted; forestTotal = next;
            if (!double.IsFinite(forestTotal))
                throw new ArgumentOutOfRangeException(nameof(source), "Hierarchy forest aggregate exceeds the finite range.");
        }
        var result = new HierarchyEntry[entries.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var entry = entries[i];
            result[i] = new(i, entry.Parent, entry.Sibling, entry.Path, entry.Node, entry.Total, entry.Children.ToArray());
        }
        return new(result, rootIndices.ToArray());
    }

    private static List<int> Scope(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int>? collapsedIndices,
        CancellationToken token, bool packing = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot); token.ThrowIfCancellationRequested();
        if (rootIndex < -1 || rootIndex >= snapshot.Nodes.Count) throw new ArgumentOutOfRangeException(nameof(rootIndex));
        if (collapsedIndices is not null)
            foreach (var index in collapsedIndices)
            { token.ThrowIfCancellationRequested(); if ((uint)index >= (uint)snapshot.Nodes.Count) throw new ArgumentOutOfRangeException(nameof(collapsedIndices)); }
        var result = new List<int>(); var stack = new Stack<int>();
        if (rootIndex >= 0) stack.Push(rootIndex);
        else for (var i = snapshot.Roots.Count - 1; i >= 0; i--) stack.Push(snapshot.Roots[i]);
        var inputCount = 0;
        while (stack.TryPop(out var index))
        {
            token.ThrowIfCancellationRequested();
            if (packing && ++inputCount > MaximumPackingCount)
                throw new ArgumentOutOfRangeException(nameof(snapshot), "Packing subtree input budget exceeded (including collapsed descendants).");
            result.Add(index);
            var children = snapshot.Nodes[index].Children;
            for (var i = children.Count - 1; i >= 0; i--) stack.Push(children[i]);
        }
        if (collapsedIndices is null || collapsedIndices.Count == 0) return result;
        var visible = new List<int>(); var hiddenUntilDepth = int.MaxValue;
        foreach (var index in result)
        {
            var entry = snapshot.Nodes[index];
            if (entry.Depth > hiddenUntilDepth) continue;
            hiddenUntilDepth = int.MaxValue; visible.Add(index);
            if (collapsedIndices.Contains(index)) hiddenUntilDepth = entry.Depth;
        }
        return visible;
    }

    private static int[] ScopeRoots(HierarchySnapshot snapshot, int rootIndex) => rootIndex < 0 ? snapshot.Roots.ToArray() : [rootIndex];
    private static bool Terminal(HierarchyEntry entry, IReadOnlySet<int>? collapsed) => entry.Children.Count == 0 || collapsed?.Contains(entry.Index) == true;
    private static double Share(double value, double total)
    {
        if (value == 0 || total == 0) return 0;
        var share = value / total;
        if (share == 0) throw new ArgumentOutOfRangeException(nameof(value), "A positive normalized hierarchy weight is below the representable range.");
        return share;
    }
    private static double Total(HierarchySnapshot snapshot, IReadOnlyList<int> indices)
    {
        double result = 0, correction = 0;
        foreach (var index in indices)
        {
            var adjusted = snapshot.Nodes[index].AggregateValue - correction; var next = result + adjusted;
            correction = (next - result) - adjusted; result = next;
            if (!double.IsFinite(result)) throw new ArgumentOutOfRangeException(nameof(snapshot), "Forest aggregate exceeds the finite range.");
        }
        return result;
    }
}
