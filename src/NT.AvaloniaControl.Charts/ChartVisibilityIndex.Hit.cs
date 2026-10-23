using Avalonia;

namespace NT.AvaloniaControl.Charts;

internal sealed partial class ChartVisibilityIndex
{
    private static long _hitQueryCount, _hitNodeVisitCount, _hitCandidateCount;
    internal static long HitQueryCount => Interlocked.Read(ref _hitQueryCount);
    internal static long HitNodeVisitCount => Interlocked.Read(ref _hitNodeVisitCount);
    internal static long HitCandidateCount => Interlocked.Read(ref _hitCandidateCount);

    // Reuse the original-coordinate visibility tree. A viewport never creates an
    // N-point screen copy; exact distances still use the original forward map.
    internal ChartSeriesHit? HitTestPoints(IChartProjection projection, Rect plot, Point position, double radius)
    {
        Interlocked.Increment(ref _hitQueryCount);
        return Points.HitTestPoints(projection, plot, position, radius);
    }

    private sealed partial class SpatialTree
    {
        internal ChartSeriesHit? HitTestPoints(IChartProjection projection, Rect plot, Point position, double radius)
        {
            var bestOrdinal = -1; var bestDistance = double.PositiveInfinity; var bestPosition = default(Point);
            var limit = radius * radius;
            long nodes = 0, candidates = 0;
            if (_nodes.Count > 0) Search(0);
            Interlocked.Add(ref _hitNodeVisitCount, nodes);
            Interlocked.Add(ref _hitCandidateCount, candidates);
            return bestOrdinal < 0 ? null : new(_data.GetValue(bestOrdinal).SourceIndex, bestPosition, bestDistance);

            void Search(int nodeIndex)
            {
                var node = _nodes[nodeIndex]; nodes++;
                var lower = LowerBound(node);
                if (lower > limit || lower > bestDistance || lower == bestDistance && node.MaximumOrdinal <= bestOrdinal) return;
                if (node.MinimumX == node.MaximumX && node.MinimumY == node.MaximumY)
                {
                    Evaluate(node.MaximumOrdinal);
                    return;
                }
                if (node.Left >= 0)
                {
                    var a = _nodes[node.Left]; var b = _nodes[node.Right];
                    var first = LowerBound(a); var last = LowerBound(b);
                    var rightFirst = last < first || last == first && b.MaximumOrdinal > a.MaximumOrdinal;
                    Search(rightFirst ? node.Right : node.Left);
                    Search(rightFirst ? node.Left : node.Right);
                    return;
                }
                for (var cursor = node.Start + node.Count - 1; cursor >= node.Start; cursor--)
                {
                    var ordinal = _ordinals[cursor];
                    if (bestDistance == 0 && ordinal <= bestOrdinal) continue;
                    Evaluate(ordinal);
                }
            }

            void Evaluate(int ordinal)
            {
                candidates++;
                var screen = projection.Project(_data.GetValue(ordinal).Point);
                if (!double.IsFinite(screen.X) || !double.IsFinite(screen.Y) || !plot.Contains(screen)) return;
                var delta = screen - position;
                var distance = delta.X * delta.X + delta.Y * delta.Y;
                if (distance > limit || distance > bestDistance || distance == bestDistance && ordinal <= bestOrdinal) return;
                bestOrdinal = ordinal; bestDistance = distance; bestPosition = screen;
            }

            double LowerBound(Node node)
            {
                var x0 = projection.MapX(node.MinimumX); var x1 = projection.MapX(node.MaximumX);
                var y0 = projection.MapY(node.MinimumY); var y1 = projection.MapY(node.MaximumY);
                // Invalid logarithmic endpoints do not prove that the whole node
                // is invalid. Descend and check each original point instead.
                if (!double.IsFinite(x0) || !double.IsFinite(x1) || !double.IsFinite(y0) || !double.IsFinite(y1)) return 0;
                x0 = Math.Clamp(x0, -1_000_000, 1_000_000); x1 = Math.Clamp(x1, -1_000_000, 1_000_000);
                y0 = Math.Clamp(y0, -1_000_000, 1_000_000); y1 = Math.Clamp(y1, -1_000_000, 1_000_000);
                var left = Math.Min(x0, x1); var right = Math.Max(x0, x1);
                var top = Math.Min(y0, y1); var bottom = Math.Max(y0, y1);
                // Candidate padding protects boundary ULPs only. Final distances
                // and exact tie order are never widened by this guard.
                var guard = 1e-7 + Math.Max(Math.Max(Math.Abs(left), Math.Abs(right)), Math.Max(Math.Abs(top), Math.Abs(bottom))) * 1e-12;
                if (right + guard < plot.Left || left - guard > plot.Right || bottom + guard < plot.Top || top - guard > plot.Bottom) return double.PositiveInfinity;
                var dx = Math.Max(left - guard - position.X, Math.Max(0, position.X - right - guard));
                var dy = Math.Max(top - guard - position.Y, Math.Max(0, position.Y - bottom - guard));
                return dx * dx + dy * dy;
            }
        }
    }
}
