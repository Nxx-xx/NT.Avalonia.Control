namespace NT.AvaloniaControl.Charts;

public static partial class ChartHierarchy
{
    /// <summary>Packs visible terminal nodes in source order using rotated tangent-envelope directions.</summary>
    public static HierarchyCircleLayout BubbleCloud(HierarchySnapshot snapshot, int rootIndex = -1,
        IReadOnlySet<int>? collapsedIndices = null, CancellationToken cancellationToken = default) =>
        FlatPack(snapshot, rootIndex, collapsedIndices, false, cancellationToken);

    /// <summary>Packs visible terminal nodes in stable descending value order. Circle areas share one value scale.</summary>
    public static HierarchyCircleLayout PackedBubble(HierarchySnapshot snapshot, int rootIndex = -1,
        IReadOnlySet<int>? collapsedIndices = null, CancellationToken cancellationToken = default) =>
        FlatPack(snapshot, rootIndex, collapsedIndices, true, cancellationToken);

    /// <summary>
    /// Packs children bottom-up inside enclosing parent circles. All terminal areas share one value scale;
    /// branch areas include layout whitespace and a six-percent radial inset, rather than encoding branch totals.
    /// </summary>
    public static HierarchyCircleLayout CirclePacking(HierarchySnapshot snapshot, int rootIndex = -1,
        IReadOnlySet<int>? collapsedIndices = null, CancellationToken cancellationToken = default)
    {
        var visible = Scope(snapshot, rootIndex, collapsedIndices, cancellationToken, true);
        if (visible.Count == 0) return new([]);
        var max = visible.Where(index => Terminal(snapshot.Nodes[index], collapsedIndices))
            .Max(index => snapshot.Nodes[index].AggregateValue);
        var radii = new double[snapshot.Nodes.Count]; var local = new ChartPoint[snapshot.Nodes.Count];
        for (var i = visible.Count - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[visible[i]];
            if (Terminal(entry, collapsedIndices)) radii[entry.Index] = Math.Sqrt(Share(entry.AggregateValue, max));
            else
            {
                var pack = Pack(entry.Children.Select(index => (Index: index, Radius: radii[index])).ToArray(), true, cancellationToken);
                foreach (var child in pack.Items) local[child.Index] = child.Center;
                radii[entry.Index] = pack.Radius * 1.06;
            }
        }
        var roots = ScopeRoots(snapshot, rootIndex);
        var top = Pack(roots.Select(index => (Index: index, Radius: radii[index])).ToArray(), true, cancellationToken);
        foreach (var item in top.Items) local[item.Index] = item.Center;
        var absolute = new ChartPoint[snapshot.Nodes.Count]; var result = new HierarchyCircle[visible.Count];
        var divisor = top.Radius == 0 ? 1 : 2 * top.Radius;
        for (var i = 0; i < visible.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var entry = snapshot.Nodes[visible[i]];
            var parent = entry.ParentIndex >= 0 && entry.Index != rootIndex ? absolute[entry.ParentIndex] : new ChartPoint(0, 0);
            absolute[entry.Index] = new(parent.X + local[entry.Index].X, parent.Y + local[entry.Index].Y);
            result[i] = new(entry, new(.5 + absolute[entry.Index].X / divisor, .5 + absolute[entry.Index].Y / divisor), radii[entry.Index] / divisor);
        }
        ValidateCirclePrecision(result, true, cancellationToken);
        return new(result);
    }

    private static HierarchyCircleLayout FlatPack(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int>? collapsed,
        bool ordered, CancellationToken token)
    {
        var visible = Scope(snapshot, rootIndex, collapsed, token, true);
        var terminals = visible.Where(index => Terminal(snapshot.Nodes[index], collapsed)).ToArray();
        if (terminals.Length == 0) return new([]);
        var max = terminals.Max(index => snapshot.Nodes[index].AggregateValue);
        var pack = Pack(terminals.Select(index => (Index: index, Radius: Math.Sqrt(Share(snapshot.Nodes[index].AggregateValue, max)))).ToArray(), ordered, token);
        var divisor = pack.Radius == 0 ? 1 : 2 * pack.Radius;
        var positions = pack.Items.ToDictionary(item => item.Index);
        var result = terminals.Select(index => {
            var item = positions[index];
            return new HierarchyCircle(snapshot.Nodes[index], new(.5 + item.Center.X / divisor, .5 + item.Center.Y / divisor), item.Radius / divisor);
        }).ToArray();
        ValidateCirclePrecision(result, false, token);
        return new(result);
    }

    private static void ValidateCirclePrecision(HierarchyCircle[] circles, bool hierarchical, CancellationToken token)
    {
        // Absolute epsilon would silently accept coincident subnormal disks. Use radius-relative
        // error instead and report input whose normalized centers cannot represent a valid pack.
        IEnumerable<HierarchyCircle[]> groups = hierarchical
            ? circles.GroupBy(circle => circle.Entry.ParentIndex).Select(group => group.ToArray()) : new[] { circles };
        foreach (var group in groups)
            for (var i = 0; i < group.Length; i++)
            {
                token.ThrowIfCancellationRequested(); var a = group[i]; if (a.Radius == 0) continue;
                for (var j = i + 1; j < group.Length; j++)
                {
                    if ((j & 255) == 0) token.ThrowIfCancellationRequested(); var b = group[j]; if (b.Radius == 0) continue;
                    var dx = a.Center.X - b.Center.X; var dy = a.Center.Y - b.Center.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance + Math.Max(a.Radius, b.Radius) * 2e-13 < a.Radius + b.Radius)
                        throw new ArgumentOutOfRangeException(nameof(circles), "Normalized circle positions cannot represent a nonoverlapping pack at this value range.");
                }
            }
    }

    private sealed record PackedItem(int Index, ChartPoint Center, double Radius);
    private sealed record Packing(PackedItem[] Items, double Radius);

    /// <summary>
    /// A bounded radial tangent-envelope pack. Each tested ray places the new disk beyond the forbidden
    /// ray intervals of every existing disk. The best enclosure of 16 directions is selected; this is
    /// a deterministic nonoverlapping heuristic, not an optimal-density solver. At most 16*n*(n-1)/2
    /// pair constraints are evaluated, with cancellation inside the pair loop.
    /// </summary>
    private static Packing Pack((int Index, double Radius)[] input, bool ordered, CancellationToken token)
    {
        var sequence = ordered ? input.OrderByDescending(item => item.Radius).ThenBy(item => item.Index).ToArray() : input;
        var placed = new List<PackedItem>(); var zero = new List<PackedItem>(); double extent = 0;
        foreach (var item in sequence)
        {
            token.ThrowIfCancellationRequested();
            if (item.Radius == 0) { zero.Add(new(item.Index, new(0, 0), 0)); continue; }
            if (placed.Count == 0) { placed.Add(new(item.Index, new(0, 0), item.Radius)); extent = item.Radius; continue; }
            var best = new ChartPoint(0, 0); var bestExtent = double.PositiveInfinity;
            var rotation = ordered ? 0 : placed.Count * 2.39996322972865332;
            for (var ray = 0; ray < 16; ray++)
            {
                token.ThrowIfCancellationRequested(); var angle = rotation + ray * (Math.PI / 8);
                var ux = Math.Cos(angle); var uy = Math.Sin(angle); double t = 0;
                for (var j = 0; j < placed.Count; j++)
                {
                    if ((j & 255) == 0) token.ThrowIfCancellationRequested(); var previous = placed[j];
                    var projection = previous.Center.X * ux + previous.Center.Y * uy;
                    var perpendicular = previous.Center.X * uy - previous.Center.Y * ux;
                    // A small outward margin avoids floating-point overlap at tangencies after normalization.
                    var exclusion = (previous.Radius + item.Radius) * (1 + 8e-15);
                    if (Math.Abs(perpendicular) >= exclusion) continue;
                    var reach = Math.Sqrt(Math.Max(0, (exclusion - Math.Abs(perpendicular)) * (exclusion + Math.Abs(perpendicular))));
                    t = Math.Max(t, projection + reach);
                }
                t = Math.Max(0, t); var candidateExtent = Math.Max(extent, t + item.Radius);
                if (candidateExtent < bestExtent)
                { bestExtent = candidateExtent; best = new(t * ux, t * uy); }
            }
            placed.Add(new(item.Index, best, item.Radius)); extent = bestExtent;
        }
        if (placed.Count == 0) return new(zero.ToArray(), 0);
        // Recenter by disk bounds, then compute a guaranteed containing disk around the new origin.
        var minX = placed.Min(item => item.Center.X - item.Radius); var maxX = placed.Max(item => item.Center.X + item.Radius);
        var minY = placed.Min(item => item.Center.Y - item.Radius); var maxY = placed.Max(item => item.Center.Y + item.Radius);
        var center = new ChartPoint((minX + maxX) * .5, (minY + maxY) * .5); var result = new List<PackedItem>();
        double enclosing = 0;
        foreach (var item in placed)
        {
            token.ThrowIfCancellationRequested(); var position = new ChartPoint(item.Center.X - center.X, item.Center.Y - center.Y);
            enclosing = Math.Max(enclosing, Math.Sqrt(position.X * position.X + position.Y * position.Y) + item.Radius);
            result.Add(item with { Center = position });
        }
        // Empty nodes remain at the containing center and never reserve area.
        result.AddRange(zero); return new(result.ToArray(), enclosing * (1 + 4e-15));
    }
}
