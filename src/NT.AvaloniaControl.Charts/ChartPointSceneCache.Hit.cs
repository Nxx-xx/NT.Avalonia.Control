using Avalonia;

namespace NT.AvaloniaControl.Charts;

internal static partial class ChartPointSceneCache
{
    private static long _hitNodeVisitCount, _hitCandidateCount;
    internal static long HitNodeVisitCount => Interlocked.Read(ref _hitNodeVisitCount);
    internal static long HitCandidateCount => Interlocked.Read(ref _hitCandidateCount);

    internal sealed partial class HitIndex
    {
        private int[]? _spatialOrder;
        private readonly List<HitNode> _hitNodes = new();
        private readonly record struct HitNode(double Left, double Top, double Right, double Bottom,
            int Start, int Count, int First, int Last, int MaximumPaintOrder, int EqualShapeEntry);

        // The existing view owns exact visible footprints. Only an ordinal
        // partition and bounds are added, without reading the source again.
        internal ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance)
        {
            lock (_hitNodes)
            {
                if (_spatialOrder is null)
                {
                    _spatialOrder = Enumerable.Range(0, Count).ToArray();
                    if (Count > 0) BuildHitNode(0, Count);
                }
            }
            var best = double.PositiveInfinity; var bestPaint = -1; var bestEntry = -1;
            long visited = 0, evaluated = 0;
            if (_hitNodes.Count > 0) Search(0);
            Interlocked.Add(ref _hitNodeVisitCount, visited); Interlocked.Add(ref _hitCandidateCount, evaluated);
            if (bestEntry < 0) return null;
            var winner = _entries[bestEntry]; var center = winner.Shape.Center;
            return new(winner.SourceIndex, new(Math.Clamp(center.X, context.PlotArea.Left, context.PlotArea.Right),
                Math.Clamp(center.Y, context.PlotArea.Top, context.PlotArea.Bottom)), best);

            void Search(int nodeIndex)
            {
                var node = _hitNodes[nodeIndex]; visited++;
                var lower = Lower(node);
                if (lower > tolerance * tolerance || lower > best || lower == best && node.MaximumPaintOrder <= bestPaint) return;
                if (node.EqualShapeEntry >= 0) { Evaluate(node.EqualShapeEntry); return; }
                if (node.First >= 0)
                {
                    var a = _hitNodes[node.First]; var b = _hitNodes[node.Last];
                    var da = Lower(a); var db = Lower(b);
                    var rightFirst = db < da || db == da && b.MaximumPaintOrder > a.MaximumPaintOrder;
                    Search(rightFirst ? node.Last : node.First); Search(rightFirst ? node.First : node.Last); return;
                }
                for (var cursor = node.Start + node.Count - 1; cursor >= node.Start; cursor--)
                {
                    var index = _spatialOrder![cursor];
                    if (best == 0 && _entries[index].PaintOrder <= bestPaint) continue;
                    Evaluate(index);
                }
            }
            void Evaluate(int index)
            {
                evaluated++;
                var entry = _entries[index]; var distance = double.PositiveInfinity;
                if (context.PointHitRegions is not { } regions) distance = entry.Shape.DistanceSquared(position, context.PlotArea);
                else foreach (var region in regions)
                    if (entry.Shape.Bounds.Intersects(region)) distance = Math.Min(distance, entry.Shape.DistanceSquared(position, region));
                if (!double.IsFinite(distance) || distance > tolerance * tolerance || distance > best ||
                    distance == best && entry.PaintOrder <= bestPaint) return;
                best = distance; bestPaint = entry.PaintOrder; bestEntry = index;
            }
            double Lower(HitNode node)
            {
                var guard = 1e-7 + Math.Max(Math.Max(Math.Abs(node.Left), Math.Abs(node.Right)), Math.Max(Math.Abs(node.Top), Math.Abs(node.Bottom))) * 1e-12;
                var dx = Math.Max(0, Math.Max(node.Left - guard - position.X, position.X - node.Right - guard));
                var dy = Math.Max(0, Math.Max(node.Top - guard - position.Y, position.Y - node.Bottom - guard));
                return dx * dx + dy * dy;
            }
        }

        private int BuildHitNode(int start, int count)
        {
            var left = double.PositiveInfinity; var top = double.PositiveInfinity;
            var right = double.NegativeInfinity; var bottom = double.NegativeInfinity;
            var maximum = -1; var maximumEntry = -1; var equal = true;
            var shape = _entries[_spatialOrder![start]].Shape;
            for (var cursor = start; cursor < start + count; cursor++)
            {
                var entryIndex = _spatialOrder[cursor]; var entry = _entries[entryIndex]; var bounds = entry.Shape.Bounds;
                left = Math.Min(left, bounds.Left); right = Math.Max(right, bounds.Right);
                top = Math.Min(top, bounds.Top); bottom = Math.Max(bottom, bounds.Bottom);
                if (entry.PaintOrder > maximum) { maximum = entry.PaintOrder; maximumEntry = entryIndex; }
                if (entry.Shape != shape) equal = false;
            }
            var nodeIndex = _hitNodes.Count;
            _hitNodes.Add(new(left, top, right, bottom, start, count, -1, -1, maximum, equal ? maximumEntry : -1));
            if (equal) return nodeIndex;
            if (count <= 16)
            {
                Array.Sort(_spatialOrder, start, count, Comparer<int>.Create((a, b) => _entries[a].PaintOrder.CompareTo(_entries[b].PaintOrder)));
                return nodeIndex;
            }
            var half = count / 2;
            SelectHit(start, start + count - 1, start + half, right / 2 - left / 2 >= bottom / 2 - top / 2);
            var first = BuildHitNode(start, half); var last = BuildHitNode(start + half, count - half);
            _hitNodes[nodeIndex] = new(left, top, right, bottom, start, count, first, last, maximum, -1);
            return nodeIndex;
        }

        private void SelectHit(int left, int right, int middle, bool horizontal)
        {
            var order = _spatialOrder!; var budget = 2 * (int)Math.Log2(right - left + 1) + 2;
            while (left < right)
            {
                if (--budget == 0) { Array.Sort(order, left, right - left + 1, Comparer<int>.Create(Compare)); return; }
                var pivot = order[left + (right - left) / 2]; var first = left; var last = right;
                while (first <= last)
                {
                    while (Compare(order[first], pivot) < 0) first++;
                    while (Compare(order[last], pivot) > 0) last--;
                    if (first > last) break;
                    (order[first], order[last]) = (order[last], order[first]); first++; last--;
                }
                if (middle <= last) right = last; else if (middle >= first) left = first; else return;
            }
            int Compare(int a, int b)
            {
                var first = _entries[a]; var last = _entries[b];
                var value = (horizontal ? first.Shape.Center.X : first.Shape.Center.Y).CompareTo(horizontal ? last.Shape.Center.X : last.Shape.Center.Y);
                return value != 0 ? value : first.PaintOrder.CompareTo(last.PaintOrder);
            }
        }
    }
}
