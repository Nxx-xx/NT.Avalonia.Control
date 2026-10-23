using System.Collections;
using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

/// <summary>Inclusive raw-coordinate bounds. Infinite limits deliberately mean an unconstrained axis.</summary>
internal readonly record struct ChartVisibilityBounds(double MinimumX, double MaximumX, double MinimumY, double MaximumY)
{
    internal bool IsValid => !double.IsNaN(MinimumX) && !double.IsNaN(MaximumX) &&
        !double.IsNaN(MinimumY) && !double.IsNaN(MaximumY) && MinimumX <= MaximumX && MinimumY <= MaximumY;
    internal bool Contains(ChartPoint point) => point.X >= MinimumX && point.X <= MaximumX &&
        point.Y >= MinimumY && point.Y <= MaximumY;
    internal bool Contains(ChartSeriesBounds bounds) => MinimumX <= bounds.XMinimum && MaximumX >= bounds.XMaximum &&
        MinimumY <= bounds.YMinimum && MaximumY >= bounds.YMaximum;
    internal bool Intersects(double minimumX, double maximumX, double minimumY, double maximumY) =>
        maximumX >= MinimumX && minimumX <= MaximumX && maximumY >= MinimumY && minimumY <= MaximumY;
}

/// <summary>Source physical ordinals in their original order; never a reduced source snapshot.</summary>
internal readonly struct ChartVisibleOrdinals : IReadOnlyList<int>
{
    private readonly int[]? _ordinals;
    private readonly int _start;
    public int Count { get; }
    internal bool IsFull { get; }
    internal static ChartVisibleOrdinals All(int count) => new(0, count, count);
    internal static ChartVisibleOrdinals Range(int start, int count, int sourceCount) => new(start, count, sourceCount);
    internal static ChartVisibleOrdinals FromOwned(int[] ordinals, int sourceCount) =>
        ordinals.Length == sourceCount ? All(sourceCount) : new(ordinals);
    private ChartVisibleOrdinals(int start, int count, int sourceCount)
    { _start = start; Count = count; IsFull = start == 0 && count == sourceCount; }
    private ChartVisibleOrdinals(int[] ordinals) { _ordinals = ordinals; Count = ordinals.Length; }
    public int this[int index] => (uint)index < (uint)Count ? _ordinals is { } ordinals ? ordinals[index] : _start + index
        : throw new ArgumentOutOfRangeException(nameof(index));
    public Enumerator GetEnumerator() => new(this);
    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    internal struct Enumerator(ChartVisibleOrdinals values) : IEnumerator<int>
    {
        private int _index = -1;
        public int Current => values[_index];
        object IEnumerator.Current => Current;
        public bool MoveNext() => ++_index < values.Count;
        public void Reset() => _index = -1;
        public void Dispose() { }
    }
}

/// <summary>
/// Immutable-coordinate visibility indexes. Ordered X uses a binary range; a lazy spatial
/// hierarchy handles unordered points, narrow Y windows and edges crossing a viewport.
/// Results contain physical source ordinals, including both endpoints of crossing edges.
/// </summary>
internal sealed partial class ChartVisibilityIndex
{
    private const int DirectFilterLimit = 4096;
    private static readonly ConditionalWeakTable<ChartDataSnapshot, ChartVisibilityIndex> Cache = new();
    private static long _buildCount, _pointBuildCount, _segmentBuildCount, _queryCount, _visitedCount, _candidateCount;
    private readonly ChartDataSnapshot _data;
    private readonly ChartDataExtrema _extrema;
    private readonly bool _ordered, _descending;
    private readonly object _sync = new();
    private SpatialTree? _points, _segments;
    internal static long BuildCount => Interlocked.Read(ref _buildCount);
    internal static long PointBuildCount => Interlocked.Read(ref _pointBuildCount);
    internal static long SegmentBuildCount => Interlocked.Read(ref _segmentBuildCount);
    internal static long QueryCount => Interlocked.Read(ref _queryCount);
    internal static long VisitedCount => Interlocked.Read(ref _visitedCount);
    internal static long CandidateCount => Interlocked.Read(ref _candidateCount);
    internal static ChartVisibilityIndex For(ChartDataSnapshot data) =>
        Cache.GetValue(data.CoordinateCacheOwner, static owner => new(owner));
    private ChartVisibilityIndex(ChartDataSnapshot data)
    {
        _data = data; _extrema = ChartDataExtrema.For(data);
        _ordered = _extrema.Count == data.Count && (_extrema.AscendingX || _extrema.DescendingX);
        _descending = !_extrema.AscendingX;
        Interlocked.Increment(ref _buildCount);
    }

    internal ChartVisibleOrdinals QueryPoints(ChartVisibilityBounds bounds)
    {
        long visited = 0;
        if (!bounds.IsValid || _extrema.Bounds is { } full && bounds.Contains(full)) return Complete(ChartVisibleOrdinals.All(_data.Count), visited);
        if (_extrema.Bounds is not { } extent || !Intersects(bounds, extent)) return Complete(default, visited);
        if (_ordered)
        {
            var (start, end) = OrderedRange(bounds, ref visited);
            if (start == end) return Complete(default, visited);
            if (bounds.MinimumY <= extent.YMinimum && bounds.MaximumY >= extent.YMaximum)
                return Complete(ChartVisibleOrdinals.Range(start, end - start, _data.Count), visited);
            if (end - start <= DirectFilterLimit)
            {
                var matches = new List<int>();
                for (var ordinal = start; ordinal < end; ordinal++)
                { visited++; if (bounds.Contains(_data.GetValue(ordinal).Point)) matches.Add(ordinal); }
                return Complete(ChartVisibleOrdinals.FromOwned(matches.ToArray(), _data.Count), visited);
            }
        }
        var candidates = new List<int>();
        Points.Query(bounds, candidates, ref visited);
        candidates.Sort();
        return Complete(ChartVisibleOrdinals.FromOwned(candidates.ToArray(), _data.Count), visited);
    }

    internal ChartVisibleOrdinals QuerySegments(ChartVisibilityBounds bounds, int neighborCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(neighborCount);
        long visited = 0;
        if (!bounds.IsValid || _extrema.Bounds is { } full && bounds.Contains(full)) return Complete(ChartVisibleOrdinals.All(_data.Count), visited);
        if (_extrema.Bounds is not { } extent || !Intersects(bounds, extent) || _data.Count < 2) return Complete(default, visited);
        var candidates = new List<int>();
        if (_ordered)
        {
            var (start, end) = OrderedRange(bounds, ref visited);
            // Edges with both endpoints outside still cross the X window. One
            // physical predecessor/successor retains those boundaries and duplicates.
            start = Math.Max(0, start - 1); end = (int)Math.Min(_data.Count, (long)end + 1);
            if (bounds.MinimumY <= extent.YMinimum && bounds.MaximumY >= extent.YMaximum)
            {
                start = Math.Max(0, start - neighborCount); end = (int)Math.Min(_data.Count, (long)end + neighborCount);
                return Complete(ChartVisibleOrdinals.Range(start, end - start, _data.Count), visited);
            }
            if (end - start <= DirectFilterLimit)
            {
                for (var ordinal = start; ordinal + 1 < end; ordinal++)
                { visited++; if (SegmentIntersects(ordinal, bounds)) AddEndpoints(candidates, ordinal, neighborCount); }
                return Complete(Unique(candidates), visited);
            }
        }
        var edges = new List<int>();
        Segments.Query(bounds, edges, ref visited);
        foreach (var edge in edges) AddEndpoints(candidates, edge, neighborCount);
        return Complete(Unique(candidates), visited);
    }

    private SpatialTree Points
    {
        get { lock (_sync) { if (_points is null) { _points = new(_data, false); Interlocked.Increment(ref _pointBuildCount); } return _points; } }
    }
    private SpatialTree Segments
    {
        get { lock (_sync) { if (_segments is null) { _segments = new(_data, true); Interlocked.Increment(ref _segmentBuildCount); } return _segments; } }
    }
    private static bool Intersects(ChartVisibilityBounds bounds, ChartSeriesBounds extent) =>
        bounds.Intersects(extent.XMinimum, extent.XMaximum, extent.YMinimum, extent.YMaximum);
    private ChartVisibleOrdinals Complete(ChartVisibleOrdinals result, long visited)
    {
        Interlocked.Increment(ref _queryCount); Interlocked.Add(ref _visitedCount, visited); Interlocked.Add(ref _candidateCount, result.Count);
        return result;
    }
    private (int Start, int End) OrderedRange(ChartVisibilityBounds bounds, ref long visited)
    {
        var visits = 0;
        int Bound(double value, bool upper)
        {
            var low = 0; var high = _data.Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                var ordinal = _descending ? _data.Count - 1 - middle : middle;
                var x = _data.GetValue(ordinal).Point.X; visits++;
                if (x < value || upper && x == value) low = middle + 1; else high = middle;
            }
            return low;
        }
        var start = Bound(bounds.MinimumX, false); var end = Bound(bounds.MaximumX, true);
        visited += visits;
        return _descending ? (_data.Count - end, _data.Count - start) : (start, end);
    }
    private bool SegmentIntersects(int ordinal, ChartVisibilityBounds bounds)
    {
        var a = _data.GetValue(ordinal).Point; var b = _data.GetValue(ordinal + 1).Point;
        return Finite(a) && Finite(b) && bounds.Intersects(Math.Min(a.X, b.X), Math.Max(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y));
    }
    private void AddEndpoints(List<int> candidates, int edge, int neighbors)
    {
        var start = (int)Math.Max(0L, (long)edge - neighbors);
        var end = (int)Math.Min(_data.Count - 1L, (long)edge + 1 + neighbors);
        for (var ordinal = start; ordinal <= end; ordinal++) candidates.Add(ordinal);
    }
    private ChartVisibleOrdinals Unique(List<int> candidates)
    {
        if (candidates.Count == 0) return default;
        candidates.Sort();
        var count = 1;
        for (var index = 1; index < candidates.Count; index++)
            if (candidates[index] != candidates[count - 1]) candidates[count++] = candidates[index];
        if (count == _data.Count) return ChartVisibleOrdinals.All(count);
        var result = new int[count]; candidates.CopyTo(0, result, 0, count);
        return ChartVisibleOrdinals.FromOwned(result, _data.Count);
    }
    private static bool Finite(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    /// <summary>Balanced spatial partition, retaining only source ordinals and conservative node AABBs.</summary>
    private sealed partial class SpatialTree
    {
        private const int LeafSize = 32;
        private readonly ChartDataSnapshot _data;
        private readonly bool _segments;
        private readonly int[] _ordinals;
        private readonly List<Node> _nodes;
        private readonly record struct Node(double MinimumX, double MaximumX, double MinimumY, double MaximumY,
            int Start, int Count, int Left, int Right, int MaximumOrdinal);
        internal SpatialTree(ChartDataSnapshot data, bool segments)
        {
            _data = data; _segments = segments;
            var limit = Math.Max(0, data.Count - (segments ? 1 : 0));
            var count = 0;
            for (var ordinal = 0; ordinal < limit; ordinal++) if (Valid(ordinal)) count++;
            _ordinals = new int[count];
            for (int ordinal = 0, cursor = 0; ordinal < limit; ordinal++) if (Valid(ordinal)) _ordinals[cursor++] = ordinal;
            _nodes = new(Math.Max(1, count / 16));
            if (count > 0) Build(0, count);
        }
        private bool Valid(int ordinal) => Finite(_data.GetValue(ordinal).Point) && (!_segments || Finite(_data.GetValue(ordinal + 1).Point));
        private int Build(int start, int count)
        {
            var minimumX = double.PositiveInfinity; var maximumX = double.NegativeInfinity;
            var minimumY = double.PositiveInfinity; var maximumY = double.NegativeInfinity;
            var maximumOrdinal = -1;
            for (var cursor = start; cursor < start + count; cursor++)
            {
                var ordinal = _ordinals[cursor]; var a = _data.GetValue(ordinal).Point;
                maximumOrdinal = Math.Max(maximumOrdinal, ordinal);
                minimumX = Math.Min(minimumX, a.X); maximumX = Math.Max(maximumX, a.X);
                minimumY = Math.Min(minimumY, a.Y); maximumY = Math.Max(maximumY, a.Y);
                if (_segments)
                {
                    var b = _data.GetValue(ordinal + 1).Point;
                    minimumX = Math.Min(minimumX, b.X); maximumX = Math.Max(maximumX, b.X);
                    minimumY = Math.Min(minimumY, b.Y); maximumY = Math.Max(maximumY, b.Y);
                }
            }
            var node = _nodes.Count;
            _nodes.Add(new(minimumX, maximumX, minimumY, maximumY, start, count, -1, -1, maximumOrdinal));
            if (count > LeafSize)
            {
                var horizontal = maximumX / 2 - minimumX / 2 >= maximumY / 2 - minimumY / 2;
                var half = count / 2;
                Select(start, start + count - 1, start + half, horizontal);
                var left = Build(start, half); var right = Build(start + half, count - half);
                _nodes[node] = new(minimumX, maximumX, minimumY, maximumY, start, count, left, right, maximumOrdinal);
            }
            return node;
        }
        private double Key(int ordinal, bool horizontal)
        {
            var a = _data.GetValue(ordinal).Point;
            var first = horizontal ? a.X : a.Y;
            if (!_segments) return first;
            var b = _data.GetValue(ordinal + 1).Point;
            // Dividing before addition preserves finite extrema near Double.MaxValue.
            return first / 2 + (horizontal ? b.X : b.Y) / 2;
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
        internal void Query(ChartVisibilityBounds bounds, List<int> result, ref long visited)
        { if (_nodes.Count > 0) QueryNode(0, bounds, result, ref visited); }
        private void QueryNode(int index, ChartVisibilityBounds bounds, List<int> result, ref long visited)
        {
            var node = _nodes[index]; visited++;
            if (!bounds.Intersects(node.MinimumX, node.MaximumX, node.MinimumY, node.MaximumY)) return;
            if (node.Left >= 0)
            { QueryNode(node.Left, bounds, result, ref visited); QueryNode(node.Right, bounds, result, ref visited); return; }
            for (var cursor = node.Start; cursor < node.Start + node.Count; cursor++)
            {
                var ordinal = _ordinals[cursor]; visited++;
                var a = _data.GetValue(ordinal).Point;
                if (!_segments) { if (bounds.Contains(a)) result.Add(ordinal); }
                else
                {
                    var b = _data.GetValue(ordinal + 1).Point;
                    if (bounds.Intersects(Math.Min(a.X, b.X), Math.Max(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y))) result.Add(ordinal);
                }
            }
        }
    }
}
