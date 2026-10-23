namespace NT.AvaloniaControl.Charts;

public static partial class ChartRelations
{
    /// <summary>Shared-scale flow ribbons; SCC internal, self and backward links use outside return lanes.
    /// Alluvial honors explicit stages, with missing stages falling back to SCC layers. Sources and sinks
    /// are external boundaries; strict conservation checks only nodes with both positive input and output.</summary>
    public static RelationGraphLayout Sankey(GraphSnapshot snapshot, double nodeWidth = .045,
        double nodeGap = .025, bool strictConservation = false, int crossingIterations = 6,
        RelationSankeyMode mode = RelationSankeyMode.Sankey, CancellationToken cancellationToken = default)
    {
        SnapshotArgument(snapshot, cancellationToken); PositiveUnit(nodeWidth, nameof(nodeWidth));
        Nonnegative(nodeGap, nameof(nodeGap)); if (nodeGap > 1) throw new ArgumentOutOfRangeException(nameof(nodeGap));
        if (crossingIterations < 0 || crossingIterations > MaximumCrossingIterations) throw new ArgumentOutOfRangeException(nameof(crossingIterations));
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var imbalances = Imbalances(snapshot);
        if (strictConservation && imbalances.Length != 0) throw new ArgumentException("An interior node does not conserve flow.", nameof(snapshot));
        if (snapshot.Nodes.Count == 0) return new([], [], imbalances);
        var layers = Layers(snapshot, cancellationToken);
        if (mode == RelationSankeyMode.Alluvial)
        {
            for (var i = 0; i < layers.Length; i++) layers[i] = snapshot.Nodes[i].Node.Stage ?? layers[i];
            var ranks = layers.Distinct().Order().Select((stage, index) => (stage, index)).ToDictionary(p => p.stage, p => p.index);
            for (var i = 0; i < layers.Length; i++) layers[i] = ranks[layers[i]];
        }
        var columns = OrderedLayers(snapshot, layers, crossingIterations, cancellationToken);
        var maximum = LargestWeight(snapshot);
        var backWeight = 0d; var topWeight = 0d; var bottomWeight = 0d; var maximumBackWeight = 0d;
        foreach (var edge in snapshot.Edges)
            if (layers[edge.TargetNodeIndex] <= layers[edge.SourceNodeIndex] && maximum > 0)
            {
                var weight = edge.Weight / maximum; backWeight += weight;
                if (edge.Index % 2 == 0) topWeight += weight; else bottomWeight += weight;
                maximumBackWeight = Math.Max(maximumBackWeight, weight);
            }
        var capacities = snapshot.Nodes.Select(n => maximum > 0 ? Math.Max(n.IncomingWeight, n.OutgoingWeight) / maximum : n.Node.Weight).ToArray();
        if (maximum == 0)
        {
            var largestNode = capacities.Max();
            for (var i = 0; i < capacities.Length; i++) capacities[i] = largestNode > 0 ? capacities[i] / largestNode : 1;
        }
        var largestColumn = columns.Max(column => column.Count);
        var gap = Math.Min(nodeGap, .25 / Math.Max(1, largestColumn - 1));
        var zeroHeight = Math.Min(.015, .15 / largestColumn);
        var scale = double.PositiveInfinity;
        foreach (var column in columns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var total = column.Sum(i => capacities[i]); var zeros = column.Count(i => capacities[i] == 0);
            if (total + backWeight > 0) scale = Math.Min(scale, (.8 - gap * (column.Count - 1) - zeros * zeroHeight) / (total + backWeight));
        }
        if (!double.IsFinite(scale)) scale = 0;
        // A thick loop needs separate horizontal return corridors as well as vertical band space.
        // Limit only the common scale, preserving every edge ratio and its node/port width.
        if (maximumBackWeight > 0) scale = Math.Min(scale, .3 / maximumBackWeight);
        var topReserve = topWeight * scale; var bottomReserve = bottomWeight * scale;
        var maximumBackThickness = maximumBackWeight * scale;
        // Reserve the full widest return width plus half the node width in the X margin.
        // The extra .15 in the width bound leaves a positive gap between adjacent columns.
        var width = Math.Min(nodeWidth, (.65 - 2 * maximumBackThickness) / columns.Length);
        var margin = backWeight > 0 ? .1 + maximumBackThickness + width * .5 : .1;
        var nodes = new RelationNodeLayout[snapshot.Nodes.Count];
        for (var layer = 0; layer < columns.Length; layer++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var column = columns[layer];
            var height = column.Sum(i => capacities[i] > 0 ? capacities[i] * scale : zeroHeight) + gap * (column.Count - 1);
            var y = (.1 + topReserve + .9 - bottomReserve - height) * .5;
            var centerX = columns.Length == 1 ? .5 : margin + (1 - 2 * margin) * layer / (columns.Length - 1);
            foreach (var index in column)
            { var h = capacities[index] > 0 ? capacities[index] * scale : zeroHeight;
              nodes[index] = new(snapshot.Nodes[index], centerX - width * .5, y, width, h); y += h + gap; }
        }
        var incomingOffset = new double[nodes.Length]; var outgoingOffset = new double[nodes.Length];
        for (var i = 0; i < nodes.Length; i++)
        {
            incomingOffset[i] = Math.Max(0, (nodes[i].Height - (maximum > 0 ? snapshot.Nodes[i].IncomingWeight / maximum * scale : 0)) * .5);
            outgoingOffset[i] = Math.Max(0, (nodes[i].Height - (maximum > 0 ? snapshot.Nodes[i].OutgoingWeight / maximum * scale : 0)) * .5);
        }
        var edges = new RelationEdgeLayout[snapshot.Edges.Count]; var topOffset = 0d; var bottomOffset = 0d;
        foreach (var edge in snapshot.Edges)
        {
            cancellationToken.ThrowIfCancellationRequested(); var source = nodes[edge.SourceNodeIndex]; var target = nodes[edge.TargetNodeIndex];
            var thickness = maximum > 0 ? edge.Weight / maximum * scale : 0;
            var start = new ChartPoint(source.X + source.Width, source.Y + outgoingOffset[edge.SourceNodeIndex] + thickness * .5);
            var end = new ChartPoint(target.X, target.Y + incomingOffset[edge.TargetNodeIndex] + thickness * .5);
            outgoingOffset[edge.SourceNodeIndex] += thickness; incomingOffset[edge.TargetNodeIndex] += thickness;
            var back = layers[edge.TargetNodeIndex] <= layers[edge.SourceNodeIndex]; ChartPoint[] points;
            if (back)
            {
                // Pack full-width return ribbons in reserved bands; centerlines alone are insufficient
                // because a large cycle would otherwise be clipped and visually lose its weight.
                double lane;
                if (edge.Index % 2 == 0) { lane = .1 + topOffset + thickness * .5; topOffset += thickness; }
                else { lane = .9 - bottomOffset - thickness * .5; bottomOffset += thickness; }
                // Each vertical strip clears its node side by .025. Even the largest self loop
                // therefore has distinct left and right interiors instead of folding over itself.
                var right = start.X + thickness * .5 + .025;
                var left = end.X - thickness * .5 - .025;
                points = [start, new(right, start.Y), new(right, lane), new(left, lane), new(left, end.Y), end];
            }
            else
            { var middle = (start.X + end.X) * .5; points = Cubic(start, new(middle, start.Y), new(middle, end.Y), end); }
            edges[edge.Index] = new(edge, points, thickness, back);
        }
        return new(nodes, edges, imbalances, maximum > 0 ? scale / maximum : 0, crossingIterations);
    }

    // Weighted barycentric sweeps are O(iterations * (E + V log V)), never an all-pairs edge scan.
    private static List<int>[] OrderedLayers(GraphSnapshot graph, int[] layers, int iterations, CancellationToken token)
    {
        var columns = Enumerable.Range(0, layers.Length == 0 ? 0 : layers.Max() + 1).Select(_ => new List<int>()).ToArray();
        for (var i = 0; i < layers.Length; i++) columns[layers[i]].Add(i);
        var incoming = Enumerable.Range(0, layers.Length).Select(_ => new List<GraphEdgeEntry>()).ToArray();
        var outgoing = Enumerable.Range(0, layers.Length).Select(_ => new List<GraphEdgeEntry>()).ToArray();
        foreach (var edge in graph.Edges) { incoming[edge.TargetNodeIndex].Add(edge); outgoing[edge.SourceNodeIndex].Add(edge); }
        var rank = new double[layers.Length]; var largest = LargestWeight(graph);
        for (var sweep = 0; sweep < iterations; sweep++)
        {
            token.ThrowIfCancellationRequested(); var forward = sweep % 2 == 0;
            for (var j = 0; j < columns.Length; j++) for (var k = 0; k < columns[j].Count; k++) rank[columns[j][k]] = (k + .5) / columns[j].Count;
            for (var step = 0; step < columns.Length; step++)
            {
                token.ThrowIfCancellationRequested(); var column = columns[forward ? step : columns.Length - 1 - step];
                var scores = new Dictionary<int, double>();
                foreach (var node in column)
                {
                    var sum = 0d; var weight = 0d;
                    foreach (var edge in forward ? incoming[node] : outgoing[node])
                    {
                        var neighbor = forward ? edge.SourceNodeIndex : edge.TargetNodeIndex;
                        if (layers[neighbor] == layers[node]) continue;
                        var w = largest > 0 ? edge.Weight / largest : 1; sum += rank[neighbor] * w; weight += w;
                    }
                    scores[node] = weight > 0 ? sum / weight : rank[node];
                }
                column.Sort((a, b) => { var compared = scores[a].CompareTo(scores[b]); return compared != 0 ? compared : a.CompareTo(b); });
                for (var k = 0; k < column.Count; k++) rank[column[k]] = (k + .5) / column.Count;
            }
        }
        return columns;
    }
}
