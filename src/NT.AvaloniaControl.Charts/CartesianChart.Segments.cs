using Avalonia;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    /// <summary>
    /// Finds the nearest visible path segment of a built-in line or area series in
    /// chart-local DIPs. Null tolerance uses 12 DIPs; invalid tolerance returns null.
    /// Gaps and break interiors are excluded. Equal distances select the earliest
    /// series and earliest physical segment. No derived value is an application item.
    /// </summary>
    public ChartSegmentHit? HitTestSegment(Point position, double? tolerance = null)
    {
        VerifyAccess();
        var limit = tolerance ?? 12;
        if (!double.IsFinite(limit) || limit < 0) return null;
        var scene = GetScene();
        if (scene is null || !scene.Plot.Contains(position)) return null;
        return NearestSceneSegment(scene, position, limit);
    }

    private ChartSegmentHit? NearestSceneSegment(ChartScene scene, Point position, double tolerance)
    {
        ChartSegmentHit? best = null;
        foreach (var snapshot in scene.Snapshots)
        {
            var hit = FindSegment(scene, snapshot, position, SegmentQuery.Nearest);
            if (hit is not null && hit.DistanceSquared <= tolerance * tolerance &&
                (best is null || hit.DistanceSquared < best.DistanceSquared)) best = hit;
        }
        return best;
    }

    private enum SegmentQuery { Nearest, X, Y }
    private readonly record struct SegmentGap(bool Horizontal, double Minimum, double Maximum)
    {
        public bool Contains(Point point)
        { var value = Horizontal ? point.X : point.Y; return value > Minimum && value < Maximum; }
    }

    private ChartSegmentHit? FindSegment(ChartScene scene, SeriesSnapshot snapshot, Point query, SegmentQuery mode)
    {
        // Custom renderers do not promise that their legend's symbol describes
        // their geometry. Index only the built-in immutable view's vertices.
        if (!ChartLineGeometry.IsBuiltIn(snapshot.Series)) return null;
        if (snapshot.Series is not LineSeries { Interpolation: ChartLineInterpolation.Linear } and not ScatterLineSeries)
            return FindShapedSegment(scene, snapshot, query, mode);
        var data = snapshot.Data;
        var projection = snapshot.Context!.Projection;
        var cached = GetSegmentGeometry(scene, snapshot, linear: true);
        var gaps = cached.Gaps;
        if (InSegmentGap(gaps, query)) return null;
        var cuts = new List<double>(2 + gaps.Length * 2);
        var bestDistance = double.PositiveInfinity;
        var bestFraction = 0d; var bestStart = -1; var bestPosition = default(Point);
        if (cached.Index is { } index)
            index.Search(projection, scene.Plot, query, mode == SegmentQuery.X, mode == SegmentQuery.Y, Visit);
        else
            for (var ordinal = 0; ordinal + 1 < data.Count; ordinal++) Visit(ordinal);
        if (bestStart < 0) return null;
        var firstData = data.Points[bestStart]; var lastData = data.Points[bestStart + 1];
        var firstPoint = ChartPointHit.FromData(snapshot.Series, firstData, SegmentVertex(snapshot, cached, bestStart));
        var lastPoint = ChartPointHit.FromData(snapshot.Series, lastData, SegmentVertex(snapshot, cached, bestStart + 1));
        var point = bestFraction == 0 ? firstPoint.Point : bestFraction == 1 ? lastPoint.Point : projection.Unproject(bestPosition);
        if (!IsValid(point)) return null;
        return new(firstPoint, lastPoint, bestFraction, point, bestPosition, bestDistance)
        { BaseValue = LinearSegmentBaseValue(snapshot, bestStart, bestStart + 1, bestFraction) };

        (double Distance, int Ordinal) Visit(int ordinal)
        {
            var start = SegmentVertex(snapshot, cached, ordinal);
            var end = SegmentVertex(snapshot, cached, ordinal + 1);
            if (!double.IsFinite(start.X) || !double.IsFinite(start.Y) || !double.IsFinite(end.X) || !double.IsFinite(end.Y) ||
                !ClipSegment(start, end, scene.Plot, out var first, out var last)) return Best();
            cuts.Clear(); cuts.Add(first); cuts.Add(last);
            foreach (var gap in gaps)
            {
                var a = gap.Horizontal ? start.X : start.Y;
                var difference = (gap.Horizontal ? end.X : end.Y) - a;
                if (difference == 0) continue;
                var low = (gap.Minimum - a) / difference; var high = (gap.Maximum - a) / difference;
                if (low > first && low < last) cuts.Add(low);
                if (high > first && high < last) cuts.Add(high);
            }
            cuts.Sort();
            for (var cut = 0; cut < cuts.Count; cut++)
            {
                // Preserve isolated visible endpoints too, including a break edge.
                Consider(cuts[cut], cuts[cut]);
                if (cut + 1 < cuts.Count && cuts[cut + 1] > cuts[cut])
                {
                    var middle = InterpolateScreen(start, end, (cuts[cut] + cuts[cut + 1]) / 2);
                    if (!InSegmentGap(gaps, middle)) Consider(cuts[cut], cuts[cut + 1]);
                }
            }
            return Best();

            void Consider(double low, double high)
            {
                var dx = end.X - start.X; var dy = end.Y - start.Y;
                double fraction;
                if (mode == SegmentQuery.X && dx != 0) fraction = (query.X - start.X) / dx;
                else if (mode == SegmentQuery.Y && dy != 0) fraction = (query.Y - start.Y) / dy;
                else
                {
                    if (mode == SegmentQuery.X && query.X != start.X || mode == SegmentQuery.Y && query.Y != start.Y) return;
                    var lengthSquared = dx * dx + dy * dy;
                    fraction = lengthSquared == 0 ? low : Math.Clamp(((query.X - start.X) * dx + (query.Y - start.Y) * dy) / lengthSquared, low, high);
                }
                // Fractions refer to the full original segment, not a clipped or sampled piece.
                if (!double.IsFinite(fraction) || fraction < low || fraction > high) return;
                var position = InterpolateScreen(start, end, fraction);
                if (InSegmentGap(gaps, position)) return;
                var distance = mode == SegmentQuery.X ? Square(position.Y - query.Y) : mode == SegmentQuery.Y ? Square(position.X - query.X) :
                    Square(position.X - query.X) + Square(position.Y - query.Y);
                // A spatial traversal need not visit physical ordinals in source
                // order. Restore the established original-segment tie rule here.
                if (!double.IsFinite(distance) || distance > bestDistance || distance == bestDistance && ordinal >= bestStart) return;
                bestDistance = distance; bestFraction = fraction; bestStart = ordinal; bestPosition = position;
            }
        }
        (double Distance, int Ordinal) Best() => (bestDistance, bestStart < 0 ? int.MaxValue : bestStart);
    }

    private static double Square(double value) => value * value;
    private static bool InSegmentGap(SegmentGap[] gaps, Point point)
    { foreach (var gap in gaps) if (gap.Contains(point)) return true; return false; }
    private static Point InterpolateScreen(Point start, Point end, double fraction) => fraction == 0 ? start : fraction == 1 ? end :
        new(Math.FusedMultiplyAdd(end.X - start.X, fraction, start.X), Math.FusedMultiplyAdd(end.Y - start.Y, fraction, start.Y));

    private static bool ClipSegment(Point start, Point end, Rect plot, out double first, out double last)
    {
        var low = 0d; var high = 1d;
        bool Slab(double a, double b, double minimum, double maximum)
        {
            var difference = b - a;
            if (difference == 0) return a >= minimum && a <= maximum;
            var x = (minimum - a) / difference; var y = (maximum - a) / difference;
            if (x > y) (x, y) = (y, x);
            low = Math.Max(low, x); high = Math.Min(high, y);
            return low <= high;
        }
        var visible = Slab(start.X, end.X, plot.Left, plot.Right) && Slab(start.Y, end.Y, plot.Top, plot.Bottom);
        first = low; last = high;
        return visible && low <= 1 && high >= 0;
    }
}
