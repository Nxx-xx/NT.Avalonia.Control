using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal interface ICircularGeometrySink
{
    void SetFillRule(FillRule rule);
    void BeginFigure(Point start, bool isFilled);
    void ArcTo(Point end, Size radii, double rotation, bool largeArc, SweepDirection direction);
    void LineTo(Point end);
    void EndFigure(bool close);
}

/// <summary>The same annular sectors drive native drawing, source identity and hit testing.</summary>
internal static class CircularChartGeometry
{
    internal static IReadOnlyList<CircularSliceInfo> Build(CircularSeries series, ChartDataSnapshot data,
        Point center, double radius, Func<ChartDataPoint, double>? additionalExplode = null)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(data);
        if (!Finite(center) || !double.IsFinite(radius) || radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "The layout center and radius must be finite, with a nonnegative radius.");
        if (!double.IsFinite(radius * 2) || !double.IsFinite(center.X - radius) || !double.IsFinite(center.X + radius) ||
            !double.IsFinite(center.Y - radius) || !double.IsFinite(center.Y + radius))
            throw new ArgumentOutOfRangeException(nameof(radius), "The circular layout bounds must be representable by finite coordinates.");

        // Scaling before addition supports [MaxValue, MaxValue] and subnormal-only
        // sources. A tiny unrepresentable fraction is never replaced by a fake minimum share.
        var contributions = new List<(ChartDataPoint Data, double Value)>();
        var maximum = 0d;
        for (var index = 0; index < data.Count; index++)
        {
            var point = data.Points[index];
            var value = point.Point.Y;
            if (point.IsEmpty || !double.IsFinite(value) || value == 0) continue;
            if (value < 0)
            {
                if (series.NegativeValueMode == ChartCircularNegativeValueMode.Throw)
                    throw new InvalidOperationException($"Circular source index {point.SourceIndex} contains a negative value.");
                if (series.NegativeValueMode == ChartCircularNegativeValueMode.Ignore) continue;
                value = Math.Abs(value);
            }
            maximum = Math.Max(maximum, value);
            contributions.Add((point, value));
        }
        if (contributions.Count == 0) return [];
        var total = 0d; var totalCorrection = 0d;
        foreach (var item in contributions) Add(item.Value / maximum, ref total, ref totalCorrection);
        var result = new CircularSliceInfo[contributions.Count];
        var direction = series.Clockwise ? 1d : -1d;
        var start = NormalizeAngle(series.StartAngle);
        var cumulative = 0d; var cumulativeCorrection = 0d; var previousAngle = 0d;
        var outer = radius * series.Radius;
        var inner = outer * series.InnerRadius;
        for (var index = 0; index < contributions.Count; index++)
        {
            var (point, value) = contributions[index];
            var scaled = value / maximum;
            var fraction = scaled / total;
            Add(scaled, ref cumulative, ref cumulativeCorrection);
            // Exactly close the final allocated range. This only compensates summation
            // rounding, and does not enlarge a tiny sector to a configured minimum.
            var endAngle = index == contributions.Count - 1 ? series.SweepAngle :
                Math.Clamp(cumulative / total * series.SweepAngle, previousAngle, series.SweepAngle);
            var rawSweep = Math.Max(0, endAngle - previousAngle);
            var gap = Math.Min(rawSweep, series.SliceGap);
            var rawStart = start + direction * previousAngle;
            var paintedStart = rawStart + direction * gap / 2;
            var paintedSweep = rawSweep - gap;
            var offset = point.SourceIndex == series.ExplodedIndex ? series.ExplodeOffset : 0;
            if (additionalExplode is not null)
            {
                var additional = additionalExplode(point);
                if (!double.IsFinite(additional) || additional < 0)
                    throw new InvalidOperationException("An additional circular explosion offset must be finite and nonnegative.");
                offset += additional;
            }
            var middle = rawStart + direction * rawSweep / 2;
            var shiftedCenter = offset == 0 ? center : Offset(center, middle, offset);
            var visible = fraction > 0 && outer > inner && paintedSweep > 0 &&
                (series.SmallSliceMode != ChartCircularSmallSliceMode.Hide || rawSweep >= series.MinimumSliceAngle);
            result[index] = new CircularSliceInfo(series, point, fraction, paintedStart,
                direction * paintedSweep, shiftedCenter, inner, outer)
            {
                RawStartAngle = rawStart,
                RawSweepAngle = direction * rawSweep,
                IsVisible = visible,
            };
            previousAngle = endAngle;
        }
        return Array.AsReadOnly(result);
    }

    internal static Point Anchor(CircularSliceInfo slice) => PointAt(slice,
        slice.StartAngle + slice.SweepAngle / 2, slice.InnerRadius / 2 + slice.OuterRadius / 2);

    internal static Point PointAt(CircularSliceInfo slice, double angle, double radius) => Offset(slice.Center, angle, radius);

    internal static Rect Bounds(CircularSliceInfo slice)
    {
        if (!Drawable(slice)) return new Rect(slice.Center, default(Size));
        if (Math.Abs(slice.SweepAngle) >= 360)
            return new Rect(slice.Center.X - slice.OuterRadius, slice.Center.Y - slice.OuterRadius,
                slice.OuterRadius * 2, slice.OuterRadius * 2);
        var first = PointAt(slice, slice.StartAngle, slice.OuterRadius);
        var last = PointAt(slice, slice.StartAngle + slice.SweepAngle, slice.OuterRadius);
        var left = Math.Min(first.X, last.X); var right = Math.Max(first.X, last.X);
        var top = Math.Min(first.Y, last.Y); var bottom = Math.Max(first.Y, last.Y);
        Include(PointAt(slice, slice.StartAngle, slice.InnerRadius));
        Include(PointAt(slice, slice.StartAngle + slice.SweepAngle, slice.InnerRadius));
        foreach (var angle in new[] { 0d, 90d, 180d, 270d })
            if (ContainsAngle(slice, angle)) Include(PointAt(slice, angle, slice.OuterRadius));
        return new Rect(left, top, right - left, bottom - top);

        void Include(Point point)
        { left = Math.Min(left, point.X); right = Math.Max(right, point.X); top = Math.Min(top, point.Y); bottom = Math.Max(bottom, point.Y); }
    }

    internal static bool Contains(CircularSliceInfo slice, Point position)
    {
        if (slice.IsExiting || !Drawable(slice) || !Finite(position)) return false;
        var vector = position - slice.Center;
        if (!double.IsFinite(vector.X) || !double.IsFinite(vector.Y)) return false;
        var radius = Length(vector);
        if (radius < slice.InnerRadius && !SameBoundary(radius, slice.InnerRadius) ||
            radius > slice.OuterRadius && !SameBoundary(radius, slice.OuterRadius)) return false;
        if (radius == 0) return slice.InnerRadius == 0;
        return ContainsAngle(slice, Math.Atan2(vector.Y, vector.X) * 180 / Math.PI) ||
            OnRay(vector, slice.StartAngle) || OnRay(vector, slice.StartAngle + slice.SweepAngle);
    }

    /// <summary>Exact filled annular-sector distance, including radial ends. Stroke and hit tolerance are caller policies.</summary>
    internal static double DistanceSquared(CircularSliceInfo slice, Point position)
    {
        if (slice.IsExiting || !Drawable(slice) || !Finite(position)) return double.PositiveInfinity;
        if (Contains(slice, position)) return 0;
        var vector = position - slice.Center;
        var length = Length(vector);
        var angle = Math.Atan2(vector.Y, vector.X) * 180 / Math.PI;
        if (ContainsAngle(slice, angle))
        {
            var distance = length < slice.InnerRadius ? slice.InnerRadius - length : Math.Max(0, length - slice.OuterRadius);
            return distance * distance;
        }
        return Math.Min(SegmentDistanceSquared(position, PointAt(slice, slice.StartAngle, slice.InnerRadius),
                PointAt(slice, slice.StartAngle, slice.OuterRadius)),
            SegmentDistanceSquared(position, PointAt(slice, slice.StartAngle + slice.SweepAngle, slice.InnerRadius),
                PointAt(slice, slice.StartAngle + slice.SweepAngle, slice.OuterRadius)));
    }

    internal static Geometry CreateGeometry(CircularSliceInfo slice)
    {
        var geometry = new StreamGeometry();
        if (!Drawable(slice)) return geometry;
        using var path = geometry.Open();
        WriteGeometry(slice, new NativeGeometrySink(path));
        return geometry;
    }

    // A single production path writer lets tests record actual arc commands without
    // treating the Headless backend's endpoint-only ArcTo stub as a curve renderer.
    internal static void WriteGeometry(CircularSliceInfo slice, ICircularGeometrySink path)
    {
        if (!Drawable(slice)) return;
        path.SetFillRule(FillRule.NonZero);
        var end = slice.StartAngle + slice.SweepAngle;
        path.BeginFigure(PointAt(slice, slice.StartAngle, slice.OuterRadius), true);
        Arc(path, slice, slice.OuterRadius, slice.StartAngle, slice.SweepAngle);
        if (Math.Abs(slice.SweepAngle) >= 360)
        {
            path.EndFigure(true);
            if (slice.InnerRadius > 0)
            {
                path.BeginFigure(PointAt(slice, slice.StartAngle, slice.InnerRadius), true);
                Arc(path, slice, slice.InnerRadius, slice.StartAngle, -slice.SweepAngle);
                path.EndFigure(true);
            }
            return;
        }
        if (slice.InnerRadius > 0)
        {
            path.LineTo(PointAt(slice, end, slice.InnerRadius));
            Arc(path, slice, slice.InnerRadius, end, -slice.SweepAngle);
        }
        else if (Math.Abs(slice.SweepAngle) < 360) path.LineTo(slice.Center);
        path.EndFigure(true);
    }

    internal static double NormalizeAngle(double angle)
    {
        var normalized = angle % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static bool Drawable(CircularSliceInfo slice) => slice.IsVisible && Finite(slice.Center) &&
        double.IsFinite(slice.StartAngle) && double.IsFinite(slice.SweepAngle) && Math.Abs(slice.SweepAngle) > 0 &&
        Math.Abs(slice.SweepAngle) <= 360 && double.IsFinite(slice.OuterRadius) && double.IsFinite(slice.InnerRadius) &&
        slice.InnerRadius >= 0 && slice.OuterRadius > slice.InnerRadius;

    private static bool ContainsAngle(CircularSliceInfo slice, double angle)
    {
        if (Math.Abs(slice.SweepAngle) >= 360) return true;
        var progress = NormalizeAngle(slice.SweepAngle >= 0 ? angle - NormalizeAngle(slice.StartAngle) : NormalizeAngle(slice.StartAngle) - angle);
        return progress <= Math.Abs(slice.SweepAngle);
    }

    private static void Arc(ICircularGeometrySink path, CircularSliceInfo slice, double radius, double start, double sweep)
    {
        var direction = sweep >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
        // A single SVG-style arc cannot represent a full circle with coincident endpoints.
        if (Math.Abs(sweep) >= 360)
        {
            path.ArcTo(PointAt(slice, start + sweep / 2, radius), new Size(radius, radius), 0, false, direction);
            path.ArcTo(PointAt(slice, start + sweep, radius), new Size(radius, radius), 0, false, direction);
        }
        else path.ArcTo(PointAt(slice, start + sweep, radius), new Size(radius, radius), 0, Math.Abs(sweep) > 180, direction);
    }

    private static Point Offset(Point center, double angle, double radius)
    {
        var normalized = NormalizeAngle(angle);
        // Cardinal rays are exact, keeping shared radial boundaries and top-half
        // bounds identical in rendering, label anchors and analytic hit testing.
        return normalized switch
        {
            0 => new Point(center.X + radius, center.Y),
            90 => new Point(center.X, center.Y + radius),
            180 => new Point(center.X - radius, center.Y),
            270 => new Point(center.X, center.Y - radius),
            _ => new Point(center.X + Math.Cos(normalized * Math.PI / 180) * radius,
                center.Y + Math.Sin(normalized * Math.PI / 180) * radius),
        };
    }

    private static void Add(double value, ref double total, ref double correction)
    {
        var corrected = value - correction;
        var next = total + corrected;
        correction = next - total - corrected;
        total = next;
    }

    private static double SegmentDistanceSquared(Point point, Point first, Point last)
    {
        var vector = last - first; var relative = point - first;
        var length = vector.X * vector.X + vector.Y * vector.Y;
        var fraction = length == 0 ? 0 : Math.Clamp((relative.X * vector.X + relative.Y * vector.Y) / length, 0, 1);
        var dx = relative.X - fraction * vector.X; var dy = relative.Y - fraction * vector.Y;
        return dx * dx + dy * dy;
    }

    private static bool OnRay(Vector vector, double angle)
    {
        var unit = Offset(default, angle, 1);
        var first = vector.X * unit.Y; var second = vector.Y * unit.X;
        // Product-rounding tolerance follows the two actual cross-product terms,
        // rather than a fixed angular epsilon that could enlarge tiny sectors.
        return vector.X * unit.X + vector.Y * unit.Y >= 0 &&
            Math.Abs(first - second) <= (Math.Abs(first) + Math.Abs(second)) * 1.7763568394002505e-15;
    }

    private static bool SameBoundary(double first, double second) =>
        double.IsFinite(first) && double.IsFinite(second) &&
        Math.Abs(first - second) <= Math.Max(Math.Abs(first), Math.Abs(second)) * 1.7763568394002505e-15;

    private static double Length(Vector vector)
    {
        var scale = Math.Max(Math.Abs(vector.X), Math.Abs(vector.Y));
        if (double.IsPositiveInfinity(scale)) return double.PositiveInfinity;
        return scale == 0 ? 0 : scale * Math.Sqrt(Math.Pow(vector.X / scale, 2) + Math.Pow(vector.Y / scale, 2));
    }
    private static bool Finite(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    private sealed class NativeGeometrySink(StreamGeometryContext context) : ICircularGeometrySink
    {
        public void SetFillRule(FillRule rule) => context.SetFillRule(rule);
        public void BeginFigure(Point start, bool isFilled) => context.BeginFigure(start, isFilled);
        public void ArcTo(Point end, Size radii, double rotation, bool largeArc, SweepDirection direction) => context.ArcTo(end, radii, rotation, largeArc, direction);
        public void LineTo(Point end) => context.LineTo(end);
        public void EndFigure(bool close) => context.EndFigure(close);
    }
}
