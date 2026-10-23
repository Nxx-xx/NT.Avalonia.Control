namespace NT.AvaloniaControl.Charts;

public static partial class ChartHierarchy
{
    /// <summary>Partitions every visible parent without padding. Ancestor rectangles overlap their descendant partitions.</summary>
    public static HierarchyRectangleLayout Treemap(HierarchySnapshot snapshot, int rootIndex = -1,
        IReadOnlySet<int>? collapsedIndices = null, TreemapLayoutMode mode = TreemapLayoutMode.Squarify,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var visible = Scope(snapshot, rootIndex, collapsedIndices, cancellationToken);
        var rectangles = new HierarchyRectangle?[snapshot.Nodes.Count];
        Partition(ScopeRoots(snapshot, rootIndex), 0, 0, 1, 1, 0);
        foreach (var index in visible)
        {
            cancellationToken.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[index];
            if (Terminal(entry, collapsedIndices)) continue;
            var parent = rectangles[index]!;
            Partition(entry.Children, parent.X, parent.Y, parent.Width, parent.Height, entry.Depth + 1);
        }
        return new(visible.Select(index => rectangles[index]!).ToArray());

        void Partition(IReadOnlyList<int> indices, double x, double y, double width, double height, int depth)
        {
            var total = Total(snapshot, indices);
            if (total == 0 || width == 0 || height == 0)
            {
                foreach (var index in indices) rectangles[index] = new(snapshot.Nodes[index], x, y, 0, 0);
                return;
            }
            var positive = new List<(int Index, double Area)>();
            foreach (var index in indices)
            {
                cancellationToken.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[index];
                if (entry.AggregateValue == 0) rectangles[index] = new(entry, x, y, 0, 0);
                else
                {
                    var area = width * height * Share(entry.AggregateValue, total);
                    if (area == 0) throw new ArgumentOutOfRangeException(nameof(snapshot), "A positive treemap area is below the representable range.");
                    positive.Add((index, area));
                }
            }
            if (mode == TreemapLayoutMode.Slice)
            {
                var horizontal = (depth & 1) == 0; double offset = 0;
                for (var i = 0; i < positive.Count; i++)
                {
                    var item = positive[i]; var dimension = horizontal ? width : height;
                    var amount = i == positive.Count - 1 ? Math.Max(0, dimension - offset) :
                        dimension * Share(snapshot.Nodes[item.Index].AggregateValue, total);
                    Set(item.Index, horizontal ? x + offset : x, horizontal ? y : y + offset,
                        horizontal ? amount : width, horizontal ? height : amount);
                    offset += amount;
                }
                return;
            }
            var cursor = 0; var rx = x; var ry = y; var rw = width; var rh = height;
            while (cursor < positive.Count)
            {
                cancellationToken.ThrowIfCancellationRequested(); var end = cursor + 1;
                var side = Math.Min(rw, rh); var rowArea = positive[cursor].Area;
                var min = rowArea; var max = rowArea; var best = Worst(rowArea, min, max, side);
                while (end < positive.Count)
                {
                    var next = positive[end].Area; var score = Worst(rowArea + next, Math.Min(min, next), Math.Max(max, next), side);
                    if (score > best) break;
                    rowArea += next; min = Math.Min(min, next); max = Math.Max(max, next); best = score; end++;
                }
                var vertical = rw >= rh;
                var thickness = end == positive.Count ? (vertical ? rw : rh) : rowArea / side;
                thickness = Math.Min(thickness, vertical ? rw : rh); double offset = 0;
                for (var i = cursor; i < end; i++)
                {
                    var amount = i == end - 1 ? Math.Max(0, side - offset) : positive[i].Area / thickness;
                    Set(positive[i].Index, vertical ? rx : rx + offset, vertical ? ry + offset : ry,
                        vertical ? thickness : amount, vertical ? amount : thickness);
                    offset += amount;
                }
                if (vertical) { rx += thickness; rw = Math.Max(0, rw - thickness); }
                else { ry += thickness; rh = Math.Max(0, rh - thickness); }
                cursor = end;
            }
        }
        void Set(int index, double x, double y, double width, double height)
        {
            if (!(width > 0) || !(height > 0) || !double.IsFinite(x + y + width + height))
                throw new ArgumentOutOfRangeException(nameof(snapshot), "A positive treemap rectangle cannot be represented.");
            rectangles[index] = new(snapshot.Nodes[index], x, y, width, height);
        }
    }

    private static double Worst(double area, double min, double max, double side)
    {
        if (side == 0 || area == 0) return double.PositiveInfinity;
        var thickness = area / side;
        return Math.Max((max / thickness) / thickness, thickness / (min / thickness));
    }

    /// <summary>Allocates branch angles by leaf totals, with equal-width rings for visible relative depths.</summary>
    public static HierarchySectorLayout Sunburst(HierarchySnapshot snapshot, int rootIndex = -1,
        IReadOnlySet<int>? collapsedIndices = null, double innerRadius = 0, double startAngle = -90,
        double sweepAngle = 360, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(innerRadius) || innerRadius < 0 || innerRadius >= .5) throw new ArgumentOutOfRangeException(nameof(innerRadius));
        if (!double.IsFinite(startAngle)) throw new ArgumentOutOfRangeException(nameof(startAngle));
        if (!double.IsFinite(sweepAngle) || sweepAngle <= 0 || sweepAngle > 360) throw new ArgumentOutOfRangeException(nameof(sweepAngle));
        var visible = Scope(snapshot, rootIndex, collapsedIndices, cancellationToken);
        if (visible.Count == 0) return new([]);
        var baseDepth = rootIndex < 0 ? 0 : snapshot.Nodes[rootIndex].Depth;
        var levels = visible.Max(index => snapshot.Nodes[index].Depth - baseDepth) + 1;
        var ring = (.5 - innerRadius) / levels;
        var sectors = new HierarchySector?[snapshot.Nodes.Count];
        Partition(ScopeRoots(snapshot, rootIndex), startAngle % 360, sweepAngle);
        foreach (var index in visible)
        {
            cancellationToken.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[index];
            if (!Terminal(entry, collapsedIndices)) Partition(entry.Children, sectors[index]!.StartAngle, sectors[index]!.SweepAngle);
        }
        return new(visible.Select(index => sectors[index]!).ToArray());

        void Partition(IReadOnlyList<int> indices, double angle, double sweep)
        {
            var total = Total(snapshot, indices); double offset = 0;
            var lastPositive = -1;
            for (var i = 0; i < indices.Count; i++) if (snapshot.Nodes[indices[i]].AggregateValue > 0) lastPositive = i;
            for (var i = 0; i < indices.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[indices[i]];
                var amount = entry.AggregateValue == 0 || total == 0 ? 0 : i == lastPositive ? Math.Max(0, sweep - offset) : sweep * Share(entry.AggregateValue, total);
                if (entry.AggregateValue > 0 && sweep > 0 && amount == 0)
                    throw new ArgumentOutOfRangeException(nameof(snapshot), "A positive sunburst angle cannot be represented.");
                var depth = entry.Depth - baseDepth;
                sectors[entry.Index] = new(entry, angle + offset, amount, innerRadius + depth * ring, innerRadius + (depth + 1) * ring);
                offset += amount;
            }
        }
    }

    /// <summary>Places a forest top-to-bottom. Terminal nodes occupy stable source-ordered lanes; parents center over their terminal descendants.</summary>
    public static HierarchyTreeLayout Tree(HierarchySnapshot snapshot, int rootIndex = -1,
        IReadOnlySet<int>? collapsedIndices = null, CancellationToken cancellationToken = default)
    {
        var visible = Scope(snapshot, rootIndex, collapsedIndices, cancellationToken);
        if (visible.Count == 0) return new([], []);
        var baseDepth = rootIndex < 0 ? 0 : snapshot.Nodes[rootIndex].Depth;
        var levels = visible.Max(index => snapshot.Nodes[index].Depth - baseDepth) + 1;
        var terminalCount = visible.Count(index => Terminal(snapshot.Nodes[index], collapsedIndices));
        var first = new double[snapshot.Nodes.Count]; var last = new double[snapshot.Nodes.Count]; var lane = 0;
        foreach (var index in visible)
            if (Terminal(snapshot.Nodes[index], collapsedIndices)) first[index] = last[index] = lane++;
        for (var i = visible.Count - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[visible[i]];
            if (!Terminal(entry, collapsedIndices)) { first[entry.Index] = first[entry.Children[0]]; last[entry.Index] = last[entry.Children[^1]]; }
        }
        var width = .8 / terminalCount; var height = .6 / levels;
        var nodes = new HierarchyTreeNode[visible.Count]; var edges = new List<HierarchyTreeEdge>();
        var byIndex = new HierarchyTreeNode?[snapshot.Nodes.Count];
        for (var i = 0; i < visible.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[visible[i]];
            var x = ((first[entry.Index] + last[entry.Index]) * .5 + .5) / terminalCount;
            var y = (entry.Depth - baseDepth + .5) / levels;
            var node = new HierarchyTreeNode(entry, x - width * .5, y - height * .5, width, height);
            nodes[i] = node; byIndex[entry.Index] = node;
            if (entry.ParentIndex >= 0 && byIndex[entry.ParentIndex] is { } parent)
                edges.Add(new(parent.Entry, entry, new(parent.X + parent.Width * .5, parent.Y + parent.Height),
                    new(node.X + node.Width * .5, node.Y)));
        }
        return new(nodes, edges.ToArray());
    }
}
