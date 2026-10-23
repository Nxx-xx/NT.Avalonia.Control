using Avalonia;

namespace NT.AvaloniaControl.Charts;

internal sealed partial class ChartVisibilityIndex
{
    // Ordinal is physical source storage, not SourceIndex. The caller resolves
    // only this winning record from its current metadata/style overlay.
    internal (int Ordinal, Point Position)? NearestReading(IChartProjection projection, Point position,
        bool nearestY, bool earliest)
    {
        Interlocked.Increment(ref _hitQueryCount);
        return Points.NearestReading(projection, position, nearestY, earliest);
    }

    private sealed partial class SpatialTree
    {
        internal (int Ordinal, Point Position)? NearestReading(IChartProjection projection, Point position,
            bool nearestY, bool earliest)
        {
            var bestOrdinal = -1; var bestMajor = double.PositiveInfinity; var bestMinor = double.PositiveInfinity;
            var bestPosition = default(Point); var plot = projection.PlotArea;
            long nodes = 0, candidates = 0;
            if (_nodes.Count > 0) Search(0);
            Interlocked.Add(ref _hitNodeVisitCount, nodes); Interlocked.Add(ref _hitCandidateCount, candidates);
            return bestOrdinal < 0 ? null : (bestOrdinal, bestPosition);

            void Search(int nodeIndex)
            {
                var node = _nodes[nodeIndex]; nodes++;
                var lower = LowerBound(node);
                if (double.IsPositiveInfinity(lower.Major) || lower.Major > bestMajor ||
                    lower.Major == bestMajor && lower.Minor > bestMinor ||
                    lower.Major == bestMajor && lower.Minor == bestMinor &&
                    (!earliest && node.MaximumOrdinal <= bestOrdinal || earliest && bestOrdinal == 0)) return;
                if (!earliest && node.MinimumX == node.MaximumX && node.MinimumY == node.MaximumY)
                {
                    // Every candidate has the same original coordinates and
                    // therefore the same visibility and exact distance key.
                    Consider(node.MaximumOrdinal); return;
                }
                if (node.Left >= 0)
                {
                    var a = _nodes[node.Left]; var b = _nodes[node.Right];
                    var first = LowerBound(a); var last = LowerBound(b);
                    var compare = first.Major.CompareTo(last.Major);
                    if (compare == 0) compare = first.Minor.CompareTo(last.Minor);
                    var rightFirst = compare > 0 || compare == 0 &&
                        (earliest ? b.MaximumOrdinal < a.MaximumOrdinal : b.MaximumOrdinal > a.MaximumOrdinal);
                    Search(rightFirst ? node.Right : node.Left); Search(rightFirst ? node.Left : node.Right); return;
                }
                for (var cursor = node.Start; cursor < node.Start + node.Count; cursor++) Consider(_ordinals[cursor]);
            }

            void Consider(int ordinal)
            {
                candidates++;
                var screen = projection.Project(_data.GetValue(ordinal).Point);
                if (!double.IsFinite(screen.X) || !double.IsFinite(screen.Y) || !plot.Contains(screen)) return;
                var x = Math.Abs(screen.X - position.X); var y = Math.Abs(screen.Y - position.Y);
                var major = nearestY ? y : x; var minor = nearestY ? x : y;
                if (major > bestMajor || major == bestMajor && (minor > bestMinor || minor == bestMinor &&
                    (earliest ? bestOrdinal >= 0 && ordinal > bestOrdinal : ordinal < bestOrdinal))) return;
                bestOrdinal = ordinal; bestMajor = major; bestMinor = minor; bestPosition = screen;
            }

            (double Major, double Minor) LowerBound(Node node)
            {
                var a = projection.Project(new(node.MinimumX, node.MinimumY));
                var b = projection.Project(new(node.MaximumX, node.MaximumY));
                // A straddled logarithmic domain cannot certify exclusion of
                // valid original interior points. Exact leaf checks decide.
                if (!double.IsFinite(a.X) || !double.IsFinite(a.Y) || !double.IsFinite(b.X) || !double.IsFinite(b.Y)) return (0, 0);
                var guard = 1e-7 + Math.Max(Math.Max(Math.Abs(a.X), Math.Abs(a.Y)), Math.Max(Math.Abs(b.X), Math.Abs(b.Y))) * 1e-12;
                // An exactly constant forward coordinate is already the exact
                // primary key for every point in this monotonic node. Padding
                // that coordinate would defeat secondary pruning for same-X/Y
                // groups when the pointer is off the shared coordinate.
                var xGuard = a.X == b.X ? 0 : guard; var yGuard = a.Y == b.Y ? 0 : guard;
                var left = Math.Max(plot.Left, Math.Min(a.X, b.X) - xGuard);
                var right = Math.Min(plot.Right, Math.Max(a.X, b.X) + xGuard);
                var top = Math.Max(plot.Top, Math.Min(a.Y, b.Y) - yGuard);
                var bottom = Math.Min(plot.Bottom, Math.Max(a.Y, b.Y) + yGuard);
                if (left > right || top > bottom) return (double.PositiveInfinity, double.PositiveInfinity);
                var dx = Math.Max(left - position.X, Math.Max(0, position.X - right));
                var dy = Math.Max(top - position.Y, Math.Max(0, position.Y - bottom));
                return nearestY ? (dy, dx) : (dx, dy);
            }
        }
    }
}
