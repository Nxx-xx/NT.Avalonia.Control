namespace NT.AvaloniaControl.Charts;

public static partial class ChartRelations
{
    /// <summary>A seeded bounded force layout, stable circle, or original-index arc diagram.
    /// Explicit positions are pinned. Remaining caller-imposed collisions are reported without moving pins.</summary>
    public static RelationGraphLayout Network(GraphSnapshot snapshot,
        RelationNetworkMode mode = RelationNetworkMode.ForceDirected, int iterations = 80, int seed = 1,
        CancellationToken cancellationToken = default)
    {
        SnapshotArgument(snapshot, cancellationToken);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (iterations < 0 || iterations > MaximumForceIterations) throw new ArgumentOutOfRangeException(nameof(iterations));
        if (mode == RelationNetworkMode.ForceDirected && snapshot.Nodes.Count > MaximumForceNodeCount)
            throw new ArgumentOutOfRangeException(nameof(snapshot), "Force layout node budget exceeded.");
        if (snapshot.Nodes.Count == 0) return new([], [], []);
        var count = snapshot.Nodes.Count; var size = Math.Min(.075, .72 / Math.Ceiling(Math.Sqrt(count)));
        if (mode == RelationNetworkMode.Circular && count > 1)
            size = Math.Min(size, .9 * 2 * .37 * Math.Sin(Math.PI / count) / Math.Sqrt(2));
        if (mode == RelationNetworkMode.Arc) size = Math.Min(size, .72 / count);
        var half = size * .5; var x = new double[count]; var y = new double[count]; var pinned = new bool[count];
        uint state = unchecked((uint)seed); if (state == 0) state = 0x9E3779B9;
        double RandomUnit() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state / (double)uint.MaxValue; }
        var phase = RandomUnit() * Math.PI * 2;
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var position = snapshot.Nodes[i].Node.Position;
            pinned[i] = position.HasValue;
            if (position is { } p)
            {
                if (p.X < half || p.X > 1 - half || p.Y < half || p.Y > 1 - half)
                    throw new ArgumentOutOfRangeException(nameof(snapshot), "Pinned network bounds leave the unit square.");
                x[i] = p.X; y[i] = p.Y;
            }
            else if (mode == RelationNetworkMode.Arc)
            { x[i] = count == 1 ? .5 : .1 + .8 * i / (count - 1); y[i] = .68; }
            else
            {
                var angle = (mode == RelationNetworkMode.Circular ? -Math.PI * .5 : phase) + i * Math.PI * 2 / count;
                var radius = count == 1 ? 0 : mode == RelationNetworkMode.Circular ? .37 : .14 + .24 * RandomUnit();
                x[i] = .5 + radius * Math.Cos(angle); y[i] = .5 + radius * Math.Sin(angle);
            }
        }
        var executed = 0; var displacement = 0d;
        if (mode == RelationNetworkMode.ForceDirected)
        {
            var maximum = LargestWeight(snapshot); var ideal = .65 / Math.Sqrt(count);
            var fx = new double[count]; var fy = new double[count];
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested(); Array.Clear(fx); Array.Clear(fy);
                for (var a = 0; a < count; a++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (var b = a + 1; b < count; b++)
                    {
                        var dx = x[a] - x[b]; var dy = y[a] - y[b]; var length = Math.Sqrt(dx * dx + dy * dy);
                        if (length < 1e-10) { dx = (a + b) % 2 == 0 ? .0001 : -.0001; dy = .0001; length = Math.Sqrt(dx * dx + dy * dy); }
                        var force = ideal * ideal / length; var ux = dx / length * force; var uy = dy / length * force;
                        fx[a] += ux; fy[a] += uy; fx[b] -= ux; fy[b] -= uy;
                    }
                }
                foreach (var edge in snapshot.Edges)
                {
                    var a = edge.SourceNodeIndex; var b = edge.TargetNodeIndex; if (a == b || edge.Weight == 0) continue;
                    var dx = x[b] - x[a]; var dy = y[b] - y[a]; var length = Math.Sqrt(dx * dx + dy * dy);
                    if (length == 0) continue;
                    var force = length * length / ideal * (.25 + .75 * (maximum > 0 ? edge.Weight / maximum : 0));
                    var ux = dx / length * force; var uy = dy / length * force;
                    fx[a] += ux; fy[a] += uy; fx[b] -= ux; fy[b] -= uy;
                }
                displacement = 0; var temperature = .045 * (1 - iteration / (double)Math.Max(1, iterations)) + .0001;
                for (var i = 0; i < count; i++)
                {
                    if (pinned[i]) continue;
                    fx[i] += (.5 - x[i]) * .08; fy[i] += (.5 - y[i]) * .08;
                    var length = Math.Sqrt(fx[i] * fx[i] + fy[i] * fy[i]); if (length == 0) continue;
                    var step = Math.Min(temperature, length * .18); var oldX = x[i]; var oldY = y[i];
                    x[i] = Math.Clamp(x[i] + fx[i] / length * step, half + .015, 1 - half - .015);
                    y[i] = Math.Clamp(y[i] + fy[i] / length * step, half + .015, 1 - half - .015);
                    displacement = Math.Max(displacement, Math.Sqrt(Math.Pow(x[i] - oldX, 2) + Math.Pow(y[i] - oldY, 2)));
                }
                executed++;
                if (displacement < .0001 && CountSquareCollisions(x, y, size) == 0) break;
            }
        }
        var beforeProjectionX = (double[])x.Clone(); var beforeProjectionY = (double[])y.Clone();
        // Deterministic bounded separation uses rectangle overlap, matching the public node rectangles.
        if (mode == RelationNetworkMode.ForceDirected)
            for (var pass = 0; pass < 64 && CountSquareCollisions(x, y, size) > 0; pass++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var a = 0; a < count; a++) for (var b = a + 1; b < count; b++)
                {
                    if (pinned[a] && pinned[b]) continue;
                    var dx = x[b] - x[a]; var dy = y[b] - y[a]; var ox = size + 1e-6 - Math.Abs(dx); var oy = size + 1e-6 - Math.Abs(dy);
                    if (ox <= 0 || oy <= 0) continue;
                    var moveX = ox <= oy; var amount = moveX ? ox : oy; var sign = (moveX ? dx : dy) >= 0 ? 1 : -1;
                    var shareA = pinned[a] ? 0 : pinned[b] ? 1 : .5; var shareB = pinned[b] ? 0 : pinned[a] ? 1 : .5;
                    if (moveX)
                    { if (!pinned[a]) x[a] = Math.Clamp(x[a] - sign * amount * shareA, half + .015, 1 - half - .015);
                      if (!pinned[b]) x[b] = Math.Clamp(x[b] + sign * amount * shareB, half + .015, 1 - half - .015); }
                    else
                    { if (!pinned[a]) y[a] = Math.Clamp(y[a] - sign * amount * shareA, half + .015, 1 - half - .015);
                      if (!pinned[b]) y[b] = Math.Clamp(y[b] + sign * amount * shareB, half + .015, 1 - half - .015); }
                }
            }
        var collisions = CountSquareCollisions(x, y, size); var fallback = false;
        if (mode == RelationNetworkMode.ForceDirected && collisions > 0 && pinned.All(p => !p))
        {
            // A documented last resort guarantees visible unpinned marks without unbounded force work.
            fallback = true; var columns = (int)Math.Ceiling(Math.Sqrt(count)); var rows = (int)Math.Ceiling(count / (double)columns);
            for (var i = 0; i < count; i++) { x[i] = (i % columns + .5) / columns; y[i] = (i / columns + .5) / rows; }
            collisions = CountSquareCollisions(x, y, size);
        }
        for (var i = 0; i < count; i++)
            displacement = Math.Max(displacement, Math.Sqrt(Math.Pow(x[i] - beforeProjectionX[i], 2) + Math.Pow(y[i] - beforeProjectionY[i], 2)));
        var nodes = snapshot.Nodes.Select((node, i) => new RelationNodeLayout(node, x[i] - half, y[i] - half, size, size)).ToArray();
        var edges = NetworkEdges(snapshot, nodes, mode, cancellationToken);
        return new(nodes, edges, [], iterations: executed, collisions: collisions,
            converged: collisions == 0 && !fallback && (mode != RelationNetworkMode.ForceDirected || displacement < .0005),
            displacement: displacement, fallback: fallback);
    }

    private static int CountSquareCollisions(double[] x, double[] y, double size)
    {
        var count = 0;
        for (var a = 0; a < x.Length; a++) for (var b = a + 1; b < x.Length; b++)
            if (Math.Abs(x[a] - x[b]) < size - 1e-10 && Math.Abs(y[a] - y[b]) < size - 1e-10) count++;
        return count;
    }

    private static RelationEdgeLayout[] NetworkEdges(GraphSnapshot graph, RelationNodeLayout[] nodes,
        RelationNetworkMode mode, CancellationToken token)
    {
        var edges = new RelationEdgeLayout[graph.Edges.Count]; var maximum = LargestWeight(graph);
        var parallel = new Dictionary<(int A, int B), int>();
        foreach (var edge in graph.Edges)
        {
            token.ThrowIfCancellationRequested(); var source = nodes[edge.SourceNodeIndex]; var target = nodes[edge.TargetNodeIndex];
            var a = source.Center; var b = target.Center; var key = (Math.Min(edge.SourceNodeIndex, edge.TargetNodeIndex), Math.Max(edge.SourceNodeIndex, edge.TargetNodeIndex));
            var ordinal = parallel.GetValueOrDefault(key); parallel[key] = ordinal + 1;
            ChartPoint[] points;
            if (edge.SourceNodeIndex == edge.TargetNodeIndex)
            {
                var radius = Math.Min(.055, .025 + ordinal * .004); var sign = a.Y > .5 ? -1 : 1;
                points = Cubic(new(a.X - source.Width * .3, a.Y + sign * source.Height * .35),
                    new(Math.Max(0, a.X - radius), Math.Clamp(a.Y + sign * radius * 2, .015, .985)),
                    new(Math.Min(1, a.X + radius), Math.Clamp(a.Y + sign * radius * 2, .015, .985)),
                    new(a.X + source.Width * .3, a.Y + sign * source.Height * .35));
            }
            else if (mode == RelationNetworkMode.Arc)
            {
                var lift = Math.Min(.5, Math.Abs(a.X - b.X) * .65 + .06 + ordinal * .02);
                points = Cubic(a, new(a.X, Math.Max(.025, a.Y - lift)), new(b.X, Math.Max(.025, b.Y - lift)), b);
            }
            else
            {
                var dx = b.X - a.X; var dy = b.Y - a.Y; var length = Math.Sqrt(dx * dx + dy * dy);
                var offset = ordinal == 0 ? 0 : Math.Min(.09, .02 * ((ordinal + 1) / 2)) * (ordinal % 2 == 0 ? -1 : 1);
                var orientation = edge.SourceNodeIndex <= edge.TargetNodeIndex ? 1 : -1;
                var ox = length > 0 ? -dy / length * offset * orientation : offset;
                var oy = length > 0 ? dx / length * offset * orientation : 0;
                points = Cubic(a, new(Math.Clamp(a.X + dx / 3 + ox, 0, 1), Math.Clamp(a.Y + dy / 3 + oy, 0, 1)),
                    new(Math.Clamp(a.X + dx * 2 / 3 + ox, 0, 1), Math.Clamp(a.Y + dy * 2 / 3 + oy, 0, 1)), b);
            }
            edges[edge.Index] = new(edge, points, maximum > 0 ? .0015 + .0045 * (edge.Weight / maximum) : .0015, edge.SourceNodeIndex == edge.TargetNodeIndex);
        }
        return edges;
    }
}
