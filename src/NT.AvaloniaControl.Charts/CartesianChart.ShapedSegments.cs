using Avalonia;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private static ChartPointHit CreateSegmentEndpoint(ChartSeries series, ChartDataPoint point, IChartProjection projection)
        => ChartPointHit.FromData(series, point, projection.Project(point.Point));

    private ChartSegmentHit? FindShapedSegment(ChartScene scene, SeriesSnapshot snapshot, Point query, SegmentQuery mode)
    {
        var context = snapshot.Context!;
        var cached = GetSegmentGeometry(scene, snapshot, linear: false);
        var gaps = cached.Gaps;
        if (InSegmentGap(gaps, query)) return null;
        var bestDistance = double.PositiveInfinity;
        var bestFraction = 0d; var bestPosition = default(Point);
        var bestPart = default(ChartLinePathPart); var found = false; double? bestBaseValue = null;
        var cuts = new List<double>(2 + gaps.Length * 2);
        foreach (var figure in cached.Figures!)
            for (var partIndex = 0; partIndex < figure.Upper.Count; partIndex++)
            {
                var part = figure.Upper[partIndex];
                var lower = partIndex < figure.Lower.Count ? (ChartLinePathPart?)figure.Lower[partIndex] : null;
                // Shape-preserving parts stay inside their endpoint coordinate
                // intervals; reject distant custom projections before flattening.
                if (Math.Max(part.Start.X, part.End.X) < scene.Plot.Left || Math.Min(part.Start.X, part.End.X) > scene.Plot.Right ||
                    Math.Max(part.Start.Y, part.End.Y) < scene.Plot.Top || Math.Min(part.Start.Y, part.End.Y) > scene.Plot.Bottom) continue;
                foreach (var edge in part.Flatten())
                {
                    double first, last;
                    if (part.IsCubic)
                    {
                        if (!ClipMonotoneCurve(part, edge.First, edge.Last, scene.Plot, out first, out last)) continue;
                    }
                    else
                    {
                        if (!ClipSegment(edge.Start, edge.End, scene.Plot, out var a, out var b)) continue;
                        first = edge.First + (edge.Last - edge.First) * a; last = edge.First + (edge.Last - edge.First) * b;
                    }
                    cuts.Clear(); cuts.Add(first); cuts.Add(last);
                    foreach (var gap in gaps)
                    {
                        AddBoundary(gap.Minimum, true); AddBoundary(gap.Maximum, false);
                        void AddBoundary(double boundary, bool preferLowCoordinate)
                        {
                            var start = part.At(first); var end = part.At(last);
                            var a = gap.Horizontal ? start.X : start.Y; var b = gap.Horizontal ? end.X : end.Y;
                            if (boundary <= Math.Min(a, b) || boundary >= Math.Max(a, b)) return;
                            var t = part.IsCubic ? CurveCoordinateT(part, first, last, gap.Horizontal, boundary, preferLowCoordinate) :
                                first + (last - first) * (boundary - a) / (b - a);
                            if (t > first && t < last) cuts.Add(t);
                        }
                    }
                    cuts.Sort();
                    for (var i = 0; i < cuts.Count; i++)
                    {
                        Consider(cuts[i], cuts[i]);
                        if (i + 1 < cuts.Count && cuts[i + 1] > cuts[i] &&
                            !InSegmentGap(gaps, part.At((cuts[i] + cuts[i + 1]) / 2)))
                            Consider(cuts[i], cuts[i + 1]);
                    }

                    void Consider(double low, double high)
                    {
                        var start = part.At(low); var end = part.At(high);
                        var dx = end.X - start.X; var dy = end.Y - start.Y;
                        var actualT = low;
                        if (mode is SegmentQuery.X or SegmentQuery.Y)
                        {
                            var horizontal = mode == SegmentQuery.X;
                            var a = horizontal ? start.X : start.Y; var b = horizontal ? end.X : end.Y;
                            var target = horizontal ? query.X : query.Y;
                            if (target < Math.Min(a, b) - 1e-9 || target > Math.Max(a, b) + 1e-9) return;
                            if (a != b)
                                actualT = part.IsCubic ? CurveCoordinateT(part, low, high, horizontal, target) :
                                    Math.Clamp(low + (high - low) * (target - a) / (b - a), low, high);
                            else actualT = NearestT();
                        }
                        else actualT = NearestT();
                        double NearestT()
                        {
                            if (!part.IsCubic)
                            {
                                var length = dx * dx + dy * dy;
                                return length == 0 ? low : low + (high - low) * Math.Clamp(((query.X - start.X) * dx + (query.Y - start.Y) * dy) / length, 0, 1);
                            }
                            var left = low; var right = high;
                            for (var iteration = 0; iteration < 40; iteration++)
                            {
                                var a = left + (right - left) / 3; var b = right - (right - left) / 3;
                                if (Distance(part.At(a)) <= Distance(part.At(b))) right = b; else left = a;
                            }
                            var middle = (left + right) / 2;
                            var best = Distance(part.At(low)) <= Distance(part.At(high)) ? low : high;
                            return Distance(part.At(middle)) < Distance(part.At(best)) ? middle : best;
                        }
                        var position = part.At(actualT);
                        if (!scene.Plot.Contains(position) || InSegmentGap(gaps, position)) return;
                        var distance = mode == SegmentQuery.X ? Square(position.Y - query.Y) : mode == SegmentQuery.Y ? Square(position.X - query.X) : Distance(position);
                        if (!double.IsFinite(distance) || distance >= bestDistance) return;
                        found = true; bestDistance = distance; bestPart = part; bestPosition = position;
                        bestFraction = part.StartFraction + (part.EndFraction - part.StartFraction) * actualT;
                        var baseValue = lower is { } lowerPart ? context.Projection.Unproject(lowerPart.At(actualT)).Y : double.NaN;
                        bestBaseValue = double.IsFinite(baseValue) ? baseValue : null;
                        double Distance(Point point) => Square(point.X - query.X) + Square(point.Y - query.Y);
                    }
                }
            }
        if (!found) return null;
        var firstPoint = CreateSegmentEndpoint(snapshot.Series, snapshot.Data.Points[bestPart.StartOrdinal], context.Projection);
        var lastPoint = CreateSegmentEndpoint(snapshot.Series, snapshot.Data.Points[bestPart.EndOrdinal], context.Projection);
        // Flat/duplicate-X steps can place an intermediate corner exactly on a
        // real endpoint. Preserve that endpoint's original, non-derived identity.
        if (bestPosition == firstPoint.Position) bestFraction = 0;
        else if (bestPosition == lastPoint.Position) bestFraction = 1;
        var point = bestFraction == 0 ? firstPoint.Point : bestFraction == 1 ? lastPoint.Point : context.Projection.Unproject(bestPosition);
        return IsValid(point) ? new(firstPoint, lastPoint, bestFraction, point, bestPosition, bestDistance) { BaseValue = bestBaseValue } : null;
    }

    private static double CurveCoordinateT(ChartLinePathPart part, double first, double last, bool horizontal,
        double target, bool? preferLowCoordinate = null)
    {
        var a = part.At(first); var b = part.At(last);
        var ascending = (horizontal ? a.X : a.Y) < (horizontal ? b.X : b.Y);
        var left = first; var right = last;
        for (var iteration = 0; iteration < 64; iteration++)
        {
            var middle = left / 2 + right / 2; var point = part.At(middle);
            if (((horizontal ? point.X : point.Y) < target) == ascending) left = middle; else right = middle;
        }
        return preferLowCoordinate is { } low ? low == ascending ? left : right : left / 2 + right / 2;
    }

    private static bool ClipMonotoneCurve(ChartLinePathPart part, double first, double last, Rect plot,
        out double visibleFirst, out double visibleLast)
    {
        var low = first; var high = last;
        bool Slab(bool horizontal, double minimum, double maximum)
        {
            var start = part.At(low); var end = part.At(high);
            var a = horizontal ? start.X : start.Y; var b = horizontal ? end.X : end.Y;
            if (Math.Max(a, b) < minimum || Math.Min(a, b) > maximum) return false;
            var originalLow = low; var originalHigh = high;
            if (a < minimum) low = CurveCoordinateT(part, originalLow, originalHigh, horizontal, minimum, false);
            else if (a > maximum) low = CurveCoordinateT(part, originalLow, originalHigh, horizontal, maximum, true);
            if (b < minimum) high = CurveCoordinateT(part, originalLow, originalHigh, horizontal, minimum, false);
            else if (b > maximum) high = CurveCoordinateT(part, originalLow, originalHigh, horizontal, maximum, true);
            return low <= high;
        }
        var visible = Slab(true, plot.Left, plot.Right) && Slab(false, plot.Top, plot.Bottom);
        visibleFirst = low; visibleLast = high;
        return visible;
    }

    private static double? LinearSegmentBaseValue(SeriesSnapshot snapshot, int first, int last, double fraction)
    {
        var a = snapshot.Data.GetBandLower(first); var b = snapshot.Data.GetBandLower(last);
        if (a is not { } lowerA || b is not { } lowerB) return null;
        var projection = snapshot.Context!.Projection;
        var pointA = projection.Project(snapshot.Data.GetValue(first).Point with { Y = lowerA });
        var pointB = projection.Project(snapshot.Data.GetValue(last).Point with { Y = lowerB });
        if ((!double.IsFinite(pointA.X) || !double.IsFinite(pointA.Y)) && lowerA == 0) pointA = new(projection.MapX(snapshot.Data.GetValue(first).Point.X), projection.YOrigin);
        if ((!double.IsFinite(pointB.X) || !double.IsFinite(pointB.Y)) && lowerB == 0) pointB = new(projection.MapX(snapshot.Data.GetValue(last).Point.X), projection.YOrigin);
        var value = projection.Unproject(InterpolateScreen(pointA, pointB, fraction)).Y;
        return double.IsFinite(value) ? value : null;
    }
}
