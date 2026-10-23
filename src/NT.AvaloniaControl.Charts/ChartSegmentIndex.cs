using System.Runtime.CompilerServices;
using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>
/// An immutable hierarchy over original adjacent finite endpoints. No projected
/// vertex array, reduced polyline or application metadata is retained here.
/// </summary>
internal sealed class ChartSegmentIndex
{
    private const int LeafSize = 16;
    private static readonly ConditionalWeakTable<ChartDataSnapshot, ChartSegmentIndex> Cache = new();
    private static long _buildCount, _queryCount, _nodeVisitCount, _candidateCount;
    private readonly ChartDataSnapshot _data;
    private readonly int[] _ordinals;
    private readonly List<Node> _nodes;
    private readonly record struct Node(double MinimumX, double MaximumX, double MinimumY, double MaximumY,
        int Start, int Count, int MinimumOrdinal, int Left, int Right);
    internal static long BuildCount => Interlocked.Read(ref _buildCount);
    internal static long QueryCount => Interlocked.Read(ref _queryCount);
    internal static long NodeVisitCount => Interlocked.Read(ref _nodeVisitCount);
    internal static long CandidateCount => Interlocked.Read(ref _candidateCount);
    internal int Count => _ordinals.Length;
    internal long EstimatedBytes => (long)_ordinals.Length * sizeof(int) + (long)_nodes.Capacity * 56;
    internal static ChartSegmentIndex For(ChartDataSnapshot data) =>
        Cache.GetValue(data.CoordinateCacheOwner, static owner => new(owner));

    private ChartSegmentIndex(ChartDataSnapshot data)
    {
        _data = data;
        var count = 0;
        for (var ordinal = 0; ordinal + 1 < data.Count; ordinal++) if (Valid(ordinal)) count++;
        _ordinals = new int[count];
        for (int ordinal = 0, cursor = 0; ordinal + 1 < data.Count; ordinal++)
            if (Valid(ordinal)) _ordinals[cursor++] = ordinal;
        // A balanced binary split needs at most this many nodes. Reserving a
        // slightly smaller approximate capacity would immediately double the
        // backing array for dense sources and keep that unused payload alive.
        var maximumLeaves = 1;
        while ((long)maximumLeaves * LeafSize < count) maximumLeaves *= 2;
        _nodes = new(maximumLeaves * 2 - 1);
        if (count > 0) Build(0, count);
        Interlocked.Increment(ref _buildCount);
    }

    private bool Valid(int ordinal) => Finite(_data.GetValue(ordinal).Point) && Finite(_data.GetValue(ordinal + 1).Point);
    private static bool Finite(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private int Build(int start, int count)
    {
        var minimumX = double.PositiveInfinity; var maximumX = double.NegativeInfinity;
        var minimumY = double.PositiveInfinity; var maximumY = double.NegativeInfinity;
        var minimumOrdinal = int.MaxValue;
        for (var cursor = start; cursor < start + count; cursor++)
        {
            var ordinal = _ordinals[cursor];
            var a = _data.GetValue(ordinal).Point; var b = _data.GetValue(ordinal + 1).Point;
            minimumX = Math.Min(minimumX, Math.Min(a.X, b.X)); maximumX = Math.Max(maximumX, Math.Max(a.X, b.X));
            minimumY = Math.Min(minimumY, Math.Min(a.Y, b.Y)); maximumY = Math.Max(maximumY, Math.Max(a.Y, b.Y));
            minimumOrdinal = Math.Min(minimumOrdinal, ordinal);
        }
        var index = _nodes.Count;
        _nodes.Add(new(minimumX, maximumX, minimumY, maximumY, start, count, minimumOrdinal, -1, -1));
        if (count > LeafSize)
        {
            var horizontal = maximumX / 2 - minimumX / 2 >= maximumY / 2 - minimumY / 2;
            var half = count / 2;
            Select(start, start + count - 1, start + half, horizontal);
            var left = Build(start, half); var right = Build(start + half, count - half);
            _nodes[index] = new(minimumX, maximumX, minimumY, maximumY, start, count, minimumOrdinal, left, right);
        }
        return index;
    }
    private double Key(int ordinal, bool horizontal)
    {
        var a = _data.GetValue(ordinal).Point; var b = _data.GetValue(ordinal + 1).Point;
        return horizontal ? a.X / 2 + b.X / 2 : a.Y / 2 + b.Y / 2;
    }
    private int Compare(int a, int b, bool horizontal)
    { var comparison = Key(a, horizontal).CompareTo(Key(b, horizontal)); return comparison != 0 ? comparison : a.CompareTo(b); }
    private void Select(int left, int right, int middle, bool horizontal)
    {
        var budget = 2 * (int)Math.Log2(right - left + 1) + 2;
        while (left < right)
        {
            if (--budget == 0)
            { Array.Sort(_ordinals, left, right - left + 1, Comparer<int>.Create((a, b) => Compare(a, b, horizontal))); return; }
            var pivot = _ordinals[left + (right - left) / 2];
            var first = left; var last = right;
            while (first <= last)
            {
                while (Compare(_ordinals[first], pivot, horizontal) < 0) first++;
                while (Compare(_ordinals[last], pivot, horizontal) > 0) last--;
                if (first > last) break;
                (_ordinals[first], _ordinals[last]) = (_ordinals[last], _ordinals[first]); first++; last--;
            }
            if (middle <= last) right = last; else if (middle >= first) left = first; else return;
        }
    }

    // The visitor returns its best (distance, original physical ordinal). Bounds
    // only exclude candidates; original projection and clipping decide the hit.
    internal void Search(IChartProjection projection, Rect plot, Point query, bool xOnly, bool yOnly,
        Func<int, (double Distance, int Ordinal)> visit)
    {
        long nodes = 0, candidates = 0;
        var best = (Distance: double.PositiveInfinity, Ordinal: int.MaxValue);
        if (_nodes.Count > 0) SearchNode(0);
        Interlocked.Increment(ref _queryCount); Interlocked.Add(ref _nodeVisitCount, nodes);
        Interlocked.Add(ref _candidateCount, candidates);

        void SearchNode(int index)
        {
            var node = _nodes[index]; nodes++;
            var bound = Bound(node);
            if (double.IsPositiveInfinity(bound) || bound > best.Distance || bound == best.Distance && node.MinimumOrdinal >= best.Ordinal) return;
            if (node.Left < 0)
            {
                for (var cursor = node.Start; cursor < node.Start + node.Count; cursor++)
                { candidates++; best = visit(_ordinals[cursor]); }
                return;
            }
            var left = Bound(_nodes[node.Left]); var right = Bound(_nodes[node.Right]);
            if (left < right || left == right && _nodes[node.Left].MinimumOrdinal < _nodes[node.Right].MinimumOrdinal)
            { SearchNode(node.Left); SearchNode(node.Right); }
            else { SearchNode(node.Right); SearchNode(node.Left); }
        }
        double Bound(Node node)
        {
            var a = projection.Project(new(node.MinimumX, node.MinimumY));
            var b = projection.Project(new(node.MaximumX, node.MaximumY));
            // Invalid-domain extrema cannot certify exclusion of valid interior
            // coordinates (for example, a tree straddling a logarithmic zero).
            if (!double.IsFinite(a.X) || !double.IsFinite(a.Y) || !double.IsFinite(b.X) || !double.IsFinite(b.Y)) return 0;
            var guard = 1e-7 + Math.Max(Math.Max(Math.Abs(a.X), Math.Abs(a.Y)), Math.Max(Math.Abs(b.X), Math.Abs(b.Y))) * 1e-12;
            var left = Math.Max(plot.Left, Math.Min(a.X, b.X) - guard);
            var right = Math.Min(plot.Right, Math.Max(a.X, b.X) + guard);
            var top = Math.Max(plot.Top, Math.Min(a.Y, b.Y) - guard);
            var bottom = Math.Min(plot.Bottom, Math.Max(a.Y, b.Y) + guard);
            if (left > right || top > bottom) return double.PositiveInfinity;
            if (xOnly && (query.X < left || query.X > right) || yOnly && (query.Y < top || query.Y > bottom)) return double.PositiveInfinity;
            var dx = query.X < left ? left - query.X : query.X > right ? query.X - right : 0;
            var dy = query.Y < top ? top - query.Y : query.Y > bottom ? query.Y - bottom : 0;
            return xOnly ? dy * dy : yOnly ? dx * dx : dx * dx + dy * dy;
        }
    }
}
