namespace NT.AvaloniaControl.Charts;

public static partial class ChartRelations
{
    /// <summary>Directed ribbons allocate one source and one target port per original edge.
    /// Sector mass is incoming plus outgoing weight, including both ports of a self loop.
    /// If every edge is zero, node weights provide sectors; absent nonzero node weights sectors are equal.</summary>
    public static RelationChordLayout Chord(GraphSnapshot snapshot, double paddingDegrees = 3,
        CancellationToken cancellationToken = default)
    {
        SnapshotArgument(snapshot, cancellationToken); Nonnegative(paddingDegrees, nameof(paddingDegrees));
        if (paddingDegrees > 180) throw new ArgumentOutOfRangeException(nameof(paddingDegrees));
        var count = snapshot.Nodes.Count; if (count == 0) return new([], [], 0);
        var padding = Math.Min(paddingDegrees, 180d / count); var maximum = LargestWeight(snapshot);
        var masses = new double[count];
        foreach (var edge in snapshot.Edges)
        { cancellationToken.ThrowIfCancellationRequested(); var weight = maximum > 0 ? edge.Weight / maximum : 0;
          masses[edge.SourceNodeIndex] += weight; masses[edge.TargetNodeIndex] += weight; }
        if (maximum == 0)
        {
            var largest = snapshot.Nodes.Max(node => node.Node.Weight);
            for (var i = 0; i < count; i++) masses[i] = largest > 0 ? snapshot.Nodes[i].Node.Weight / largest : 1;
        }
        var total = masses.Sum(); var angleScale = total > 0 ? (360 - count * padding) / total : 0;
        var nodes = new RelationChordNode[count]; var angle = -90d; var offsets = new double[count];
        for (var i = 0; i < count; i++)
        { cancellationToken.ThrowIfCancellationRequested(); nodes[i] = new(snapshot.Nodes[i], angle, masses[i] * angleScale); offsets[i] = angle; angle += nodes[i].SweepAngle + padding; }
        var ribbons = new RelationChordRibbon[snapshot.Edges.Count];
        foreach (var edge in snapshot.Edges)
        {
            cancellationToken.ThrowIfCancellationRequested(); var sweep = maximum > 0 ? edge.Weight / maximum * angleScale : 0;
            var sourceStart = offsets[edge.SourceNodeIndex]; offsets[edge.SourceNodeIndex] += sweep;
            var targetStart = offsets[edge.TargetNodeIndex]; offsets[edge.TargetNodeIndex] += sweep;
            ribbons[edge.Index] = new(edge, sourceStart, sweep, targetStart, sweep);
        }
        return new(nodes, ribbons, padding);
    }

    /// <summary>Automatic SCC process layers, exact caller-centered flow rectangles, or a deterministic
    /// bilateral spanning-forest mindmap. Explicit overlaps remain visible in RemainingCollisionCount.</summary>
    public static RelationGraphLayout Flow(GraphSnapshot snapshot, RelationFlowMode mode = RelationFlowMode.Layered,
        CancellationToken cancellationToken = default)
    {
        SnapshotArgument(snapshot, cancellationToken); if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (snapshot.Nodes.Count == 0) return new([], [], []);
        var layers = Layers(snapshot, cancellationToken);
        var columns = OrderedLayers(snapshot, layers, 6, cancellationToken); var largest = columns.Max(column => column.Count);
        var nodes = new RelationNodeLayout[snapshot.Nodes.Count];
        for (var layer = 0; layer < columns.Length; layer++)
        {
            var column = columns[layer];
            for (var rank = 0; rank < column.Count; rank++)
            {
                cancellationToken.ThrowIfCancellationRequested(); var index = column[rank]; var entry = snapshot.Nodes[index];
                var width = Math.Min(entry.Node.Width, columns.Length == 1 ? .8 : Math.Min(.16, .6 / (columns.Length - 1)));
                var height = Math.Min(entry.Node.Height, Math.Min(.16, .65 / largest));
                var center = new ChartPoint(columns.Length == 1 ? .5 : .1 + .8 * layer / (columns.Length - 1),
                    column.Count == 1 ? .5 : .1 + .8 * rank / (column.Count - 1));
                if (mode == RelationFlowMode.Explicit && entry.Node.Position is { } position)
                {
                    width = entry.Node.Width; height = entry.Node.Height; center = position;
                    if (position.X - width * .5 < 0 || position.X + width * .5 > 1 || position.Y - height * .5 < 0 || position.Y + height * .5 > 1)
                        throw new ArgumentOutOfRangeException(nameof(snapshot), "Explicit flow node bounds leave the unit square.");
                }
                nodes[index] = new(entry, center.X - width * .5, center.Y - height * .5, width, height);
            }
        }
        if (mode == RelationFlowMode.Mindmap) nodes = MindmapNodes(snapshot, cancellationToken);
        var edges = FlowEdges(snapshot, nodes, layers, mode, cancellationToken);
        var collisions = 0;
        for (var a = 0; a < nodes.Length; a++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var b = a + 1; b < nodes.Length; b++)
                if (nodes[a].X < nodes[b].X + nodes[b].Width - 1e-10 && nodes[b].X < nodes[a].X + nodes[a].Width - 1e-10
                    && nodes[a].Y < nodes[b].Y + nodes[b].Height - 1e-10 && nodes[b].Y < nodes[a].Y + nodes[a].Height - 1e-10) collisions++;
        }
        return new(nodes, edges, [], collisions: collisions, converged: collisions == 0);
    }

    private static RelationNodeLayout[] MindmapNodes(GraphSnapshot snapshot, CancellationToken token)
    {
        var count = snapshot.Nodes.Count; var children = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
        var adjacency = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray(); var indegree = new int[count];
        foreach (var edge in snapshot.Edges) { adjacency[edge.SourceNodeIndex].Add(edge.TargetNodeIndex); indegree[edge.TargetNodeIndex]++; }
        var parents = Enumerable.Repeat(-2, count).ToArray(); var depths = new int[count]; var sides = new int[count];
        var roots = new List<int>(); var visit = new List<int>();
        foreach (var root in Enumerable.Range(0, count).Where(i => indegree[i] == 0).Concat(Enumerable.Range(0, count)))
        {
            token.ThrowIfCancellationRequested(); if (parents[root] != -2) continue;
            parents[root] = -1; roots.Add(root); var queue = new Queue<int>(); queue.Enqueue(root);
            while (queue.TryDequeue(out var node))
            {
                token.ThrowIfCancellationRequested(); visit.Add(node);
                foreach (var child in adjacency[node])
                    if (parents[child] == -2)
                    {
                        parents[child] = node; depths[child] = depths[node] + 1;
                        sides[child] = parents[node] < 0 ? children[node].Count % 2 == 0 ? 1 : -1 : sides[node];
                        children[node].Add(child); queue.Enqueue(child);
                    }
            }
        }
        var starts = new int[count]; var ends = new int[count]; var leaf = 0;
        foreach (var root in roots)
        {
            var stack = new Stack<(int Node, bool Complete)>(); stack.Push((root, false));
            while (stack.TryPop(out var frame))
            {
                token.ThrowIfCancellationRequested();
                if (frame.Complete)
                { starts[frame.Node] = starts[children[frame.Node][0]]; ends[frame.Node] = ends[children[frame.Node][^1]]; }
                else if (children[frame.Node].Count == 0) { starts[frame.Node] = leaf; ends[frame.Node] = leaf; leaf++; }
                else
                { stack.Push((frame.Node, true)); for (var j = children[frame.Node].Count - 1; j >= 0; j--) stack.Push((children[frame.Node][j], false)); }
            }
        }
        var maximumDepth = Math.Max(1, depths.Max()); var result = new RelationNodeLayout[count];
        for (var i = 0; i < count; i++)
        {
            var centerX = .5 + .4 * depths[i] / maximumDepth * sides[i];
            var centerY = leaf == 1 ? .5 : .1 + .8 * (starts[i] + ends[i]) * .5 / (leaf - 1);
            var width = Math.Min(snapshot.Nodes[i].Node.Width, Math.Min(.16, .3 / maximumDepth));
            var height = Math.Min(snapshot.Nodes[i].Node.Height, Math.Min(.16, .65 / leaf));
            result[i] = new(snapshot.Nodes[i], centerX - width * .5, centerY - height * .5, width, height);
        }
        return result;
    }

    private static RelationEdgeLayout[] FlowEdges(GraphSnapshot snapshot, RelationNodeLayout[] nodes,
        int[] layers, RelationFlowMode mode, CancellationToken token)
    {
        var result = new RelationEdgeLayout[snapshot.Edges.Count]; var maximum = LargestWeight(snapshot);
        var parallel = new Dictionary<(int A, int B), int>();
        foreach (var edge in snapshot.Edges)
        {
            token.ThrowIfCancellationRequested(); var source = nodes[edge.SourceNodeIndex]; var target = nodes[edge.TargetNodeIndex];
            var a = source.Center; var b = target.Center;
            var horizontalGap = Math.Max(target.X - (source.X + source.Width), source.X - (target.X + target.Width));
            var verticalGap = Math.Max(target.Y - (source.Y + source.Height), source.Y - (target.Y + target.Height));
            var horizontal = horizontalGap > 0 ? verticalGap <= 0 || horizontalGap >= verticalGap : verticalGap <= 0;
            var direction = horizontal ? b.X >= a.X ? 1 : -1 : b.Y >= a.Y ? 1 : -1;
            var start = horizontal ? new ChartPoint(a.X + direction * source.Width * .5, a.Y)
                : new ChartPoint(a.X, a.Y + direction * source.Height * .5);
            var end = horizontal ? new ChartPoint(b.X - direction * target.Width * .5, b.Y)
                : new ChartPoint(b.X, b.Y - direction * target.Height * .5);
            var key = (edge.SourceNodeIndex, edge.TargetNodeIndex); var ordinal = parallel.GetValueOrDefault(key); parallel[key] = ordinal + 1;
            var back = edge.SourceNodeIndex == edge.TargetNodeIndex || (mode != RelationFlowMode.Mindmap && layers[edge.TargetNodeIndex] <= layers[edge.SourceNodeIndex]);
            ChartPoint[] points;
            if (edge.SourceNodeIndex == edge.TargetNodeIndex)
            {
                var lane = Math.Max(.015, source.Y - .04 - .005 * Math.Min(ordinal, 5));
                points = [new(source.X + source.Width, a.Y), new(Math.Min(.985, source.X + source.Width + .025), a.Y),
                    new(Math.Min(.985, source.X + source.Width + .025), lane), new(a.X, lane), new(a.X, source.Y)];
            }
            else if (back)
            {
                var lane = edge.Index % 2 == 0 ? .025 : .975;
                var available = horizontal ? horizontalGap : verticalGap;
                var lead = available > 0 ? Math.Min(.025, available * .25) : .025;
                if (horizontal)
                {
                    var sourceLead = Math.Clamp(start.X + direction * lead, 0, 1);
                    var targetLead = Math.Clamp(end.X - direction * lead, 0, 1);
                    points = [start, new(sourceLead, start.Y), new(sourceLead, lane), new(targetLead, lane), new(targetLead, end.Y), end];
                }
                else
                {
                    var sourceLead = Math.Clamp(start.Y + direction * lead, 0, 1);
                    var targetLead = Math.Clamp(end.Y - direction * lead, 0, 1);
                    points = [start, new(start.X, sourceLead), new(lane, sourceLead), new(lane, targetLead), new(end.X, targetLead), end];
                }
            }
            else
            {
                var startAxis = horizontal ? start.X : start.Y; var endAxis = horizontal ? end.X : end.Y;
                var available = horizontal ? horizontalGap : verticalGap;
                var offset = (ordinal % 2 == 0 ? 1 : -1) * .01 * ((ordinal + 1) / 2);
                var middle = (startAxis + endAxis) * .5 + offset;
                var axisSpan = Math.Abs(endAxis - startAxis);
                middle = available > 0 ? Math.Clamp(middle, Math.Min(startAxis, endAxis) + axisSpan * .1,
                    Math.Max(startAxis, endAxis) - axisSpan * .1) : Math.Clamp(middle, .015, .985);
                if (horizontal)
                    points = mode == RelationFlowMode.Mindmap ? Cubic(start, new(middle, start.Y), new(middle, end.Y), end)
                        : [start, new(middle, start.Y), new(middle, end.Y), end];
                else
                    points = mode == RelationFlowMode.Mindmap ? Cubic(start, new(start.X, middle), new(end.X, middle), end)
                        : [start, new(start.X, middle), new(end.X, middle), end];
            }
            result[edge.Index] = new(edge, points, maximum > 0 ? .0015 + .0045 * (edge.Weight / maximum) : .0015, back);
        }
        return result;
    }
}
