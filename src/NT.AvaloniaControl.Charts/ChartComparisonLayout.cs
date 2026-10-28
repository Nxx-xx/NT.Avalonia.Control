using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Layout;

namespace NT.AvaloniaControl.Charts;

/// <summary>Original-ordinal bar geometry shared by painting and interaction for one immutable view.</summary>
internal sealed class ChartComparisonLayout
{
    private static readonly ConditionalWeakTable<ChartSeriesRenderContext, Cache> Layouts = new();
    private static long _buildCount, _queryCount, _visitedCount, _evaluatedCount;
    private sealed class Cache { internal readonly object Sync = new(); internal ChartComparisonLayout? Layout; }
    private readonly record struct Signature(ChartComparisonKind? Kind, Orientation Orientation, double Baseline,
        double? BarWidth, double WidthRatio, bool ShowMarkers, double Radius, double HitRadius, double StrokeThickness);
    internal readonly record struct Entry(int Ordinal, ChartComparisonShape Shape);
    private readonly record struct Bounds(double Left, double Top, double Right, double Bottom)
    {
        internal static Bounds From(Rect value)
        {
            // Segment clipping and closest-point arithmetic may differ at their
            // last ULP. Broad bounds deliberately cover that screen-space error;
            // the unchanged exact primitive test still decides every hit.
            var guard = 1e-7 + Math.Max(Math.Max(Math.Abs(value.Left), Math.Abs(value.Right)),
                Math.Max(Math.Abs(value.Top), Math.Abs(value.Bottom))) * 1e-12;
            return new(value.Left - guard, value.Top - guard, value.Right + guard, value.Bottom + guard);
        }
        internal double DistanceSquared(Point position)
        {
            var dx = Math.Max(Left - position.X, Math.Max(0, position.X - Right));
            var dy = Math.Max(Top - position.Y, Math.Max(0, position.Y - Bottom));
            return dx * dx + dy * dy;
        }
        internal Bounds Union(Bounds other) => new(Math.Min(Left, other.Left), Math.Min(Top, other.Top), Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom));
    }
    private readonly record struct Node(Bounds Bounds, int Start, int Count, int Left, int Right, int MaximumOrdinal, int EqualShapeEntry);
    private readonly ChartSeriesRenderContext _context;
    private readonly Signature _signature;
    private readonly Entry[] _entries;
    private int[] _order = [];
    private Bounds[] _hitBounds = [];
    private readonly List<Node> _nodes = [];
    private readonly object _hitSync = new();
    private bool _hitReady;
    private readonly bool _rectangle, _stem, _variance;
    internal static long BuildCount => Interlocked.Read(ref _buildCount);
    internal static long QueryCount => Interlocked.Read(ref _queryCount);
    internal static long VisitedCount => Interlocked.Read(ref _visitedCount);
    internal static long EvaluatedCount => Interlocked.Read(ref _evaluatedCount);
    internal ReadOnlySpan<Entry> Entries => _entries;

    internal static ChartComparisonLayout For(ChartSeriesRenderContext context, ChartComparisonKind? kind)
    {
        var options = context.Series as IChartComparisonSeries;
        var signature = new Signature(kind, options?.Orientation ?? Orientation.Vertical, options?.Baseline ?? 0,
            options?.BarWidth, options?.WidthRatio ?? .86, context.Series.ShowMarkers,
            ChartMarkerGeometry.Radius(context.Series), ChartMarkerGeometry.HitRadius(context.Series), context.Series.StrokeThickness);
        // Extension projections may mutate behind their interface. Retain their
        // established fresh exhaustive projection instead of caching stale boxes.
        if (context.Projection is not CartesianChart.CartesianProjection { HasStableTransform: true }) return new(context, signature);
        var cache = Layouts.GetValue(context, static _ => new());
        lock (cache.Sync)
        {
            if (cache.Layout is { } current && current._signature == signature) return current;
            return cache.Layout = new(context, signature);
        }
    }

    private ChartComparisonLayout(ChartSeriesRenderContext context, Signature signature)
    {
        _context = context; _signature = signature;
        _rectangle = signature.Kind is null or ChartComparisonKind.Bar or ChartComparisonKind.Range or ChartComparisonKind.Waterfall or ChartComparisonKind.Pareto;
        _stem = signature.Kind is ChartComparisonKind.Lollipop or ChartComparisonKind.Variance;
        _variance = signature.Kind == ChartComparisonKind.Variance;
        var candidates = signature.Kind is null ? LegacyCandidates(context) : ChartComparisonGeometry.VisibleCandidates(context);
        var entries = candidates.Count == 0 ? Array.Empty<Entry>() : new Entry[candidates.Count];
        var count = 0;
        foreach (var ordinal in candidates)
        {
            var valid = signature.Kind is null ? TryCreateLegacy(context, ordinal, out var shape)
                : ChartComparisonGeometry.TryCreate(context, ordinal, out shape);
            if (valid) entries[count++] = new(ordinal, shape);
        }
        if (count != entries.Length) Array.Resize(ref entries, count);
        _entries = entries;
        Interlocked.Increment(ref _buildCount);
    }

    private void EnsureHitIndex()
    {
        lock (_hitSync)
        {
            if (_hitReady) return;
            _hitBounds = new Bounds[_entries.Length];
            var order = new List<int>(_entries.Length);
            for (var index = 0; index < _entries.Length; index++)
                if (TryHitBounds(_entries[index].Shape, out var bounds)) { _hitBounds[index] = bounds; order.Add(index); }
            _order = order.ToArray();
            if (_order.Length > 0) Build(0, _order.Length);
            _hitReady = true;
        }
    }

    // Keep the established left-associative rectangle arithmetic. Callers may
    // supply the original fast linear projection without changing native paint.
    internal static Rect LegacyRectangle(ChartSeriesRenderContext context, Point screen)
    {
        var width = context.GroupWidth / Math.Max(1, context.GroupCount);
        var barWidth = Math.Max(.5, width * .86);
        var baseline = context.Projection.YOrigin;
        if (!double.IsFinite(baseline)) baseline = context.PlotArea.Bottom;
        baseline = Math.Clamp(baseline, context.PlotArea.Top, context.PlotArea.Bottom);
        return new(screen.X - context.GroupWidth / 2 + context.GroupIndex * width + (width - barWidth) / 2,
            Math.Min(screen.Y, baseline), barWidth, Math.Abs(screen.Y - baseline));
    }

    private static bool TryCreateLegacy(ChartSeriesRenderContext context, int ordinal, out ChartComparisonShape shape)
    {
        shape = default;
        var point = context.Data.GetValue(ordinal).Point;
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) return false;
        // Retained/native bars without markers use this exact fast projection.
        // With markers the established body and endpoint use general Project.
        var screen = ChartMarkerGeometry.Radius(context.Series) <= 0 &&
            context.Projection is CartesianChart.CartesianProjection projection &&
            projection.TryGetLinearProjection(context.Data, out var linear) ? linear.Project(point) : context.Projection.Project(point);
        ChartVisibilityQuery.RecordProjected(1);
        if (!ChartComparisonGeometry.Valid(screen)) return false;
        var rectangle = LegacyRectangle(context, screen);
        if (!Finite(rectangle)) return false;
        shape = new(rectangle, new(rectangle.Center.X, context.Projection.YOrigin), new(rectangle.Center.X, screen.Y), false);
        return true;
    }

    private static ChartVisibleOrdinals LegacyCandidates(ChartSeriesRenderContext context)
    {
        var width = context.GroupWidth / Math.Max(1, context.GroupCount);
        var barWidth = Math.Max(.5, width * .86);
        var offset = -context.GroupWidth / 2 + context.GroupIndex * width + (width - barWidth) / 2;
        var extent = Math.Max(Math.Abs(offset), Math.Abs(offset + barWidth)) + ChartMarkerGeometry.HitRadius(context.Series) + 1;
        return ChartVisibilityQuery.Points(context, inflation: extent, xOnly: true);
    }

    private bool TryHitBounds(ChartComparisonShape shape, out Bounds bounds)
    {
        var union = default(Bounds); var any = false;
        if (_rectangle)
        {
            var body = shape.Bounds.Intersect(_context.PlotArea);
            if (body.Width > 0 && body.Height > 0) Add(body);
        }
        if (_stem && _signature.StrokeThickness > 0)
        {
            var a = shape.Start; var b = shape.End;
            if (ChartComparisonGeometry.ClipSegment(_context.PlotArea, ref a, ref b))
                Add(new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)));
        }
        if (_signature.ShowMarkers && _signature.Radius > 0)
        {
            var markerPlot = _context.PlotArea.Inflate(_signature.HitRadius);
            if (markerPlot.Contains(shape.End)) Add(new(shape.End, default(Size)));
            if (_variance && markerPlot.Contains(shape.Start)) Add(new(shape.Start, default(Size)));
        }
        bounds = union; return any;

        void Add(Rect value) { var box = Bounds.From(value); union = any ? union.Union(box) : box; any = true; }
        // Explicit min/max retains zero-size marker/stem bounds and avoids
        // narrowing an edge through Rect.Width/Right subtraction/addition.
    }

    internal ChartSeriesHit? HitTest(Point position, double tolerance)
    {
        Interlocked.Increment(ref _queryCount);
        if (!ChartComparisonGeometry.Valid(position) || !double.IsFinite(tolerance) || tolerance < 0) return null;
        EnsureHitIndex();
        if (_nodes.Count == 0) return null;
        var maximum = Math.Max(tolerance, Math.Max(_signature.HitRadius, _stem ? _signature.StrokeThickness / 2 : 0));
        var bestDistance = double.PositiveInfinity; var bestOrdinal = -1; var bestPosition = default(Point);
        long visited = 0, evaluated = 0;
        Search(0);
        Interlocked.Add(ref _visitedCount, visited); Interlocked.Add(ref _evaluatedCount, evaluated);
        return bestOrdinal < 0 ? null : new(_context.Data.GetValue(bestOrdinal).SourceIndex, bestPosition, bestDistance);

        void Search(int nodeIndex)
        {
            var node = _nodes[nodeIndex]; visited++;
            var lower = node.Bounds.DistanceSquared(position);
            if (lower > maximum * maximum || lower > bestDistance || lower == bestDistance && node.MaximumOrdinal <= bestOrdinal) return;
            if (node.EqualShapeEntry >= 0)
            {
                // Identical primitives have identical exact distance and anchor.
                // This also resolves an off-center coincident marker cluster in
                // one test, where conservative padded bounds cannot prove a tie.
                Test(node.EqualShapeEntry); return;
            }
            if (node.Left >= 0)
            {
                var left = _nodes[node.Left]; var right = _nodes[node.Right];
                var a = left.Bounds.DistanceSquared(position);
                var b = right.Bounds.DistanceSquared(position);
                var rightFirst = b < a || b == a && right.MaximumOrdinal > left.MaximumOrdinal;
                Search(rightFirst ? node.Right : node.Left); Search(rightFirst ? node.Left : node.Right); return;
            }
            // Later physical rows win exact ties. Visiting them first allows an
            // overlapping zero-distance bar to prune every earlier equal body.
            for (var cursor = node.Start + node.Count - 1; cursor >= node.Start; cursor--)
            {
                var index = _order[cursor]; var entry = _entries[index];
                var bound = _hitBounds[index].DistanceSquared(position);
                if (bound > bestDistance || bound == bestDistance && entry.Ordinal <= bestOrdinal) continue;
                Test(index);
            }
        }
        void Test(int index)
        {
            var entry = _entries[index]; evaluated++;
            Evaluate(entry.Shape, position, tolerance, out var distance, out var anchor);
            if (!double.IsFinite(distance) || distance > bestDistance || distance == bestDistance && entry.Ordinal <= bestOrdinal) return;
            bestDistance = distance; bestOrdinal = entry.Ordinal; bestPosition = anchor;
        }
    }

    private void Evaluate(ChartComparisonShape shape, Point position, double tolerance, out double distance, out Point anchor)
    {
        var best = double.PositiveInfinity; var selected = shape.End;
        if (_rectangle)
        {
            var rectangle = shape.Bounds.Intersect(_context.PlotArea);
            if (rectangle.Width > 0 && rectangle.Height > 0)
            {
                var body = ChartComparisonGeometry.RectangleDistanceSquared(rectangle, position);
                if (body <= tolerance * tolerance)
                {
                    best = body;
                    selected = shape.Horizontal ? new(Math.Clamp(shape.End.X, rectangle.Left, rectangle.Right), rectangle.Center.Y)
                        : new(rectangle.Center.X, Math.Clamp(shape.End.Y, rectangle.Top, rectangle.Bottom));
                }
            }
        }
        if (_stem && _signature.StrokeThickness > 0)
        {
            var a = shape.Start; var b = shape.End;
            if (ChartComparisonGeometry.ClipSegment(_context.PlotArea, ref a, ref b))
            {
                var stem = ChartComparisonGeometry.SegmentDistanceSquared(position, a, b, out var nearest);
                var extent = Math.Max(tolerance, _signature.StrokeThickness / 2);
                if (stem <= extent * extent && stem < best) { best = stem; selected = nearest; }
            }
        }
        if (_signature.ShowMarkers && _signature.Radius > 0)
        {
            Marker(shape.End); if (_variance) Marker(shape.Start);
        }
        distance = best; anchor = selected;
        void Marker(Point center)
        {
            if (!_context.PlotArea.Inflate(_signature.HitRadius).Contains(center)) return;
            var delta = center - position; var candidate = delta.X * delta.X + delta.Y * delta.Y;
            var radius = Math.Max(tolerance, _signature.HitRadius);
            if (candidate <= radius * radius && candidate < best) { best = candidate; selected = center; }
        }
    }

    private int Build(int start, int count)
    {
        var bounds = _hitBounds[_order[start]]; var maximum = -1; var maximumEntry = -1;
        var identical = true; var firstShape = _entries[_order[start]].Shape;
        for (var cursor = start; cursor < start + count; cursor++)
        {
            var index = _order[cursor]; var entry = _entries[index]; bounds = bounds.Union(_hitBounds[index]);
            if (entry.Ordinal > maximum) { maximum = entry.Ordinal; maximumEntry = index; }
            if (identical && entry.Shape != firstShape) identical = false;
        }
        var node = _nodes.Count; _nodes.Add(new(bounds, start, count, -1, -1, maximum, identical ? maximumEntry : -1));
        if (identical) return node;
        if (count <= 16)
        {
            Array.Sort(_order, start, count, Comparer<int>.Create((a, b) => _entries[a].Ordinal.CompareTo(_entries[b].Ordinal)));
            return node;
        }
        var half = count / 2;
        Select(start, start + count - 1, start + half, bounds.Right / 2 - bounds.Left / 2 >= bounds.Bottom / 2 - bounds.Top / 2);
        var left = Build(start, half); var right = Build(start + half, count - half);
        _nodes[node] = new(bounds, start, count, left, right, maximum, -1); return node;
    }

    private void Select(int left, int right, int middle, bool horizontal)
    {
        var budget = 2 * (int)Math.Log2(right - left + 1) + 2;
        while (left < right)
        {
            if (--budget == 0) { Array.Sort(_order, left, right - left + 1, Comparer<int>.Create(Compare)); return; }
            var pivot = _order[left + (right - left) / 2]; var first = left; var last = right;
            while (first <= last)
            {
                while (Compare(_order[first], pivot) < 0) first++;
                while (Compare(_order[last], pivot) > 0) last--;
                if (first > last) break;
                (_order[first], _order[last]) = (_order[last], _order[first]); first++; last--;
            }
            if (middle <= last) right = last; else if (middle >= first) left = first; else return;
        }
        int Compare(int a, int b)
        {
            var x = _hitBounds[a]; var y = _hitBounds[b];
            var first = horizontal ? x.Left / 2 + x.Right / 2 : x.Top / 2 + x.Bottom / 2;
            var second = horizontal ? y.Left / 2 + y.Right / 2 : y.Top / 2 + y.Bottom / 2;
            var compare = first.CompareTo(second); return compare != 0 ? compare : _entries[a].Ordinal.CompareTo(_entries[b].Ordinal);
        }
    }

    private static bool Finite(Rect rectangle) => double.IsFinite(rectangle.X) && double.IsFinite(rectangle.Y) &&
        double.IsFinite(rectangle.Right) && double.IsFinite(rectangle.Bottom);
}
