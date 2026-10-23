using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal sealed record PolarPathInfo(IReadOnlyList<PolarPointInfo> Points, bool IsClosed);
internal sealed record PolarSeriesScene(PolarSeries Series, IReadOnlyList<PolarPointInfo> Points,
    IReadOnlyList<PolarSectorInfo> Sectors, IReadOnlyList<PolarPathInfo> Paths);
internal sealed record PolarScene(Point Center, double Radius, double RadiusMinimum, double RadiusMaximum,
    IReadOnlyList<object?> Categories, IReadOnlyList<PolarSeriesScene> Series)
{
    public IReadOnlyList<PolarPointInfo> Points { get; } = Array.AsReadOnly(Series.SelectMany(s => s.Points).ToArray());
    public IReadOnlyList<PolarSectorInfo> Sectors { get; } = Array.AsReadOnly(Series.SelectMany(s => s.Sectors).ToArray());
}

/// <summary>One source-preserving projection shared by native paint, selection, tooltip and export.</summary>
internal static class PolarChartGeometry
{
    internal static PolarScene Build(IReadOnlyList<(PolarSeries Series, ChartDataSnapshot Data)> sources,
        Point center, double radius, double angleMinimum = 0, double angleMaximum = 360,
        double radiusMinimum = 0, double? radiusMaximum = null, double startAngle = -90,
        double sweepAngle = 360, bool clockwise = true, ChartAngleUnit angleUnit = ChartAngleUnit.Degrees,
        bool wrapAngles = true, bool angleInversed = false, bool radiusInversed = false)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (!Finite(center) || !double.IsFinite(radius) || radius < 0 || !double.IsFinite(radius * 2) ||
            !double.IsFinite(center.X - radius) || !double.IsFinite(center.X + radius) ||
            !double.IsFinite(center.Y - radius) || !double.IsFinite(center.Y + radius))
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!double.IsFinite(angleMinimum) || !double.IsFinite(angleMaximum) || angleMaximum <= angleMinimum)
            throw new ArgumentOutOfRangeException(nameof(angleMaximum));
        if (!Enum.IsDefined(angleUnit)) throw new ArgumentOutOfRangeException(nameof(angleUnit));
        if (!double.IsFinite(radiusMinimum) || radiusMinimum < 0 ||
            radiusMaximum is { } max && (!double.IsFinite(max) || max <= radiusMinimum))
            throw new ArgumentOutOfRangeException(nameof(radiusMaximum));
        if (!double.IsFinite(startAngle) || !double.IsFinite(sweepAngle) || sweepAngle < 0 || sweepAngle > 360)
            throw new ArgumentOutOfRangeException(nameof(sweepAngle));
        var categories = new List<object?>();
        var autoMaximum = radiusMinimum;
        foreach (var (series, data) in sources)
        {
            ArgumentNullException.ThrowIfNull(series); ArgumentNullException.ThrowIfNull(data);
            foreach (var point in data.Points)
            {
                if (series.IsCategorical && CategoryIndex(categories, point.Category) < 0) categories.Add(point.Category);
                if (!point.IsEmpty && double.IsFinite(point.Point.Y) && point.Point.Y >= 0)
                    autoMaximum = Math.Max(autoMaximum, point.Point.Y);
            }
            if (series is RadarSeries { MaxValue: { } radarMaximum }) autoMaximum = Math.Max(autoMaximum, radarMaximum);
        }
        var maximum = radiusMaximum ?? (autoMaximum > radiusMinimum ? autoMaximum : radiusMinimum < double.MaxValue ? Math.Max(1, Math.BitIncrement(radiusMinimum)) : double.MaxValue);
        if (maximum <= radiusMinimum) throw new ArgumentOutOfRangeException(nameof(radiusMinimum));
        var direction = clockwise ? 1d : -1d;
        var start = NormalizeAngle(startAngle);
        var results = new List<PolarSeriesScene>();
        foreach (var (series, data) in sources)
        {
            var points = new List<PolarPointInfo>(data.Count);
            var sectors = new List<PolarSectorInfo>();
            var localMaximum = series is RadarSeries { MaxValue: { } radarMaximum } ? radarMaximum : maximum;
            foreach (var item in data.Points)
            {
                var category = series.IsCategorical ? CategoryIndex(categories, item.Category) : -1;
                var angleFraction = series.IsCategorical ? (double)category / Math.Max(1, categories.Count) : Fraction(item.Point.X, angleMinimum, angleMaximum);
                if (!series.IsCategorical && wrapAngles && double.IsFinite(angleFraction) && (angleFraction < 0 || angleFraction > 1))
                    angleFraction -= Math.Floor(angleFraction);
                var radialFraction = localMaximum > radiusMinimum ? Fraction(item.Point.Y, radiusMinimum, localMaximum) : double.NaN;
                var valid = !item.IsEmpty && double.IsFinite(item.Point.Y) && item.Point.Y >= 0 &&
                    double.IsFinite(angleFraction) && angleFraction >= 0 && angleFraction <= 1 &&
                    double.IsFinite(radialFraction) && radialFraction >= 0 && radialFraction <= 1;
                if (angleInversed) angleFraction = 1 - angleFraction;
                if (radiusInversed) radialFraction = 1 - radialFraction;
                var angle = start + direction * sweepAngle * angleFraction;
                var inner = radius * series.InnerRadius;
                var projectedRadius = inner + (radius - inner) * radialFraction;
                var point = new PolarPointInfo(series, item, angle, projectedRadius,
                    valid ? PointAt(center, angle, projectedRadius) : center)
                { IsVisible = valid && radius > 0 && sweepAngle > 0, Fill = DirectBrush(item.ColorValue) };
                points.Add(point);
                if (series is not (PolarAreaSeries or NightingaleRoseSeries or RadialBarSeries)) continue;

                var sectorDirection = direction * (angleInversed ? -1 : 1);
                var sectorStart = angle;
                var sectorSweep = sectorDirection * sweepAngle / Math.Max(1, categories.Count);
                var outer = radius;
                if (series is RadialBarSeries)
                {
                    var laneWidth = (radius - inner) / Math.Max(1, categories.Count);
                    outer = inner + (category + .96) * laneWidth;
                    inner += (category + .04) * laneWidth;
                    sectorStart = start + (angleInversed ? direction * sweepAngle : 0);
                    sectorSweep = sectorDirection * sweepAngle * radialFraction;
                }
                else
                {
                    // sqrt applies to annular area, retaining the configured central hole.
                    outer = series is PolarAreaSeries ? radius * Math.Sqrt(series.InnerRadius * series.InnerRadius +
                        (1 - series.InnerRadius * series.InnerRadius) * radialFraction) : projectedRadius;
                }
                var gap = Math.Min(Math.Abs(sectorSweep), series.SectorGap);
                sectorStart += sectorDirection * gap / 2;
                sectorSweep -= sectorDirection * gap;
                sectors.Add(new PolarSectorInfo(series, item, center, inner, outer, sectorStart, sectorSweep)
                {
                    IsVisible = valid && item.Point.Y > 0 && radius > 0 && outer > inner && Math.Abs(sectorSweep) > 0,
                    Fill = point.Fill,
                });
            }
            var paths = new List<PolarPathInfo>();
            if (series is not (PolarScatterSeries or PolarAreaSeries or NightingaleRoseSeries or RadialBarSeries))
            {
                var ordered = series.IsCategorical ? points.OrderBy(p => CategoryIndex(categories, p.Data.Category)).ToArray() : points.ToArray();
                var hasGap = ordered.Any(p => !p.IsVisible);
                if (series.IsCategorical)
                    hasGap |= Enumerable.Range(0, categories.Count).Any(i => !ordered.Any(p => CategoryIndex(categories, p.Data.Category) == i && p.IsVisible));
                var segment = new List<PolarPointInfo>(); var previousCategory = -1;
                foreach (var point in ordered)
                {
                    var category = series.IsCategorical ? CategoryIndex(categories, point.Data.Category) : -1;
                    if (!point.IsVisible || series.IsCategorical && previousCategory >= 0 && category > previousCategory + 1) Flush();
                    if (point.IsVisible) segment.Add(point);
                    previousCategory = category;
                }
                Flush();
                void Flush()
                {
                    if (segment.Count > 0) paths.Add(new PolarPathInfo(Array.AsReadOnly(segment.ToArray()), series.IsClosed && !hasGap && segment.Count >= 3));
                    segment.Clear();
                }
            }
            results.Add(new PolarSeriesScene(series,
                series is PolarAreaSeries or NightingaleRoseSeries or RadialBarSeries ? Array.Empty<PolarPointInfo>() : Array.AsReadOnly(points.ToArray()),
                Array.AsReadOnly(sectors.ToArray()), Array.AsReadOnly(paths.ToArray())));
        }
        return new PolarScene(center, radius, radiusMinimum, maximum, Array.AsReadOnly(categories.ToArray()), Array.AsReadOnly(results.ToArray()));
    }

    internal static Geometry CreateLineGeometry(PolarSeriesScene scene) => CreatePathGeometry(scene, false);
    internal static Geometry CreateFillGeometry(PolarSeriesScene scene, Point center) => CreatePathGeometry(scene, true);

    private static Geometry CreatePathGeometry(PolarSeriesScene scene, bool filled)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        WritePathGeometry(scene, filled, new NativeGeometrySink(context));
        return geometry;
    }

    internal static void WritePathGeometry(PolarSeriesScene scene, bool filled, ICircularGeometrySink context)
    {
        context.SetFillRule(FillRule.NonZero);
        foreach (var path in scene.Paths)
        {
            if (path.Points.Count < (filled ? 3 : 2) || filled && !path.IsClosed) continue;
            context.BeginFigure(path.Points[0].Position, filled);
            foreach (var point in path.Points.Skip(1)) context.LineTo(point.Position);
            context.EndFigure(path.IsClosed);
        }
    }

    internal static Geometry CreateGeometry(PolarSectorInfo sector)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        WriteGeometry(sector, new NativeGeometrySink(context));
        return geometry;
    }

    internal static void WriteGeometry(PolarSectorInfo sector, ICircularGeometrySink path)
    {
        if (!Drawable(sector)) return;
        path.SetFillRule(FillRule.NonZero);
        path.BeginFigure(PointAt(sector.Center, sector.StartAngle, sector.OuterRadius), true);
        Arc(sector.OuterRadius, sector.StartAngle, sector.SweepAngle);
        if (Math.Abs(sector.SweepAngle) >= 360)
        {
            path.EndFigure(true);
            if (sector.InnerRadius > 0)
            {
                path.BeginFigure(PointAt(sector.Center, sector.StartAngle, sector.InnerRadius), true);
                Arc(sector.InnerRadius, sector.StartAngle, -sector.SweepAngle);
                path.EndFigure(true);
            }
            return;
        }
        if (sector.InnerRadius > 0)
        {
            path.LineTo(PointAt(sector.Center, sector.StartAngle + sector.SweepAngle, sector.InnerRadius));
            Arc(sector.InnerRadius, sector.StartAngle + sector.SweepAngle, -sector.SweepAngle);
        }
        else path.LineTo(sector.Center);
        path.EndFigure(true);

        void Arc(double arcRadius, double from, double sweep)
        {
            var direction = sweep >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
            if (Math.Abs(sweep) >= 360)
            {
                path.ArcTo(PointAt(sector.Center, from + sweep / 2, arcRadius), new Size(arcRadius, arcRadius), 0, false, direction);
                path.ArcTo(PointAt(sector.Center, from + sweep, arcRadius), new Size(arcRadius, arcRadius), 0, false, direction);
            }
            else path.ArcTo(PointAt(sector.Center, from + sweep, arcRadius), new Size(arcRadius, arcRadius), 0, Math.Abs(sweep) > 180, direction);
        }
    }

    internal static bool Contains(PolarSectorInfo sector, Point point)
    {
        if (!Drawable(sector) || !Finite(point)) return false;
        var vector = point - sector.Center;
        var length = Length(vector);
        if (length < sector.InnerRadius && !SameBoundary(length, sector.InnerRadius) ||
            length > sector.OuterRadius && !SameBoundary(length, sector.OuterRadius)) return false;
        return length == 0 ? sector.InnerRadius == 0 : ContainsAngle(sector, Math.Atan2(vector.Y, vector.X) * 180 / Math.PI) ||
            OnRay(vector, sector.StartAngle) || OnRay(vector, sector.StartAngle + sector.SweepAngle);
    }

    internal static double DistanceSquared(PolarSectorInfo sector, Point point)
    {
        if (!Drawable(sector) || !Finite(point)) return double.PositiveInfinity;
        if (Contains(sector, point)) return 0;
        var vector = point - sector.Center;
        var length = Length(vector);
        if (ContainsAngle(sector, Math.Atan2(vector.Y, vector.X) * 180 / Math.PI))
        {
            var distance = length < sector.InnerRadius ? sector.InnerRadius - length : Math.Max(0, length - sector.OuterRadius);
            return distance * distance;
        }
        return Math.Min(SegmentDistanceSquared(point, PointAt(sector.Center, sector.StartAngle, sector.InnerRadius), PointAt(sector.Center, sector.StartAngle, sector.OuterRadius)),
            SegmentDistanceSquared(point, PointAt(sector.Center, sector.StartAngle + sector.SweepAngle, sector.InnerRadius), PointAt(sector.Center, sector.StartAngle + sector.SweepAngle, sector.OuterRadius)));
    }

    internal static double SegmentDistanceSquared(Point point, Point first, Point last)
    {
        var vector = last - first; var relative = point - first;
        var length = vector.X * vector.X + vector.Y * vector.Y;
        var fraction = length == 0 ? 0 : Math.Clamp((relative.X * vector.X + relative.Y * vector.Y) / length, 0, 1);
        var dx = relative.X - fraction * vector.X; var dy = relative.Y - fraction * vector.Y;
        return dx * dx + dy * dy;
    }
    internal static Point PointAt(Point center, double angle, double radius)
    {
        var radians = NormalizeAngle(angle) * Math.PI / 180;
        return new Point(center.X + Math.Cos(radians) * radius, center.Y + Math.Sin(radians) * radius);
    }
    internal static double NormalizeAngle(double angle)
    {
        var normalized = angle % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
    private static int CategoryIndex(IReadOnlyList<object?> categories, object? category)
    {
        for (var i = 0; i < categories.Count; i++) if (Equals(categories[i], category)) return i;
        return -1;
    }
    private static IBrush? DirectBrush(object? value) => value switch
    { IBrush brush => brush, Color color => new SolidColorBrush(color), string text when Color.TryParse(text, out var color) => new SolidColorBrush(color), _ => null };
    private static double Fraction(double value, double minimum, double maximum)
    {
        var span = maximum - minimum;
        return double.IsFinite(span) ? (value - minimum) / span : (value / 2 - minimum / 2) / (maximum / 2 - minimum / 2);
    }
    private static bool Drawable(PolarSectorInfo sector) => sector.IsVisible && Finite(sector.Center) &&
        double.IsFinite(sector.StartAngle) && double.IsFinite(sector.SweepAngle) && Math.Abs(sector.SweepAngle) > 0 && Math.Abs(sector.SweepAngle) <= 360 &&
        double.IsFinite(sector.InnerRadius) && double.IsFinite(sector.OuterRadius) && sector.InnerRadius >= 0 && sector.OuterRadius > sector.InnerRadius;
    private static bool ContainsAngle(PolarSectorInfo sector, double angle) => Math.Abs(sector.SweepAngle) >= 360 ||
        NormalizeAngle(sector.SweepAngle >= 0 ? angle - NormalizeAngle(sector.StartAngle) : NormalizeAngle(sector.StartAngle) - angle) <= Math.Abs(sector.SweepAngle);
    private static bool Finite(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static bool SameBoundary(double first, double second) => double.IsFinite(first) && double.IsFinite(second) &&
        Math.Abs(first - second) <= Math.Max(Math.Abs(first), Math.Abs(second)) * 1.7763568394002505e-15;
    private static bool OnRay(Vector vector, double angle)
    {
        var unit = PointAt(default, angle, 1);
        var first = vector.X * unit.Y; var second = vector.Y * unit.X;
        return vector.X * unit.X + vector.Y * unit.Y >= 0 && Math.Abs(first - second) <= (Math.Abs(first) + Math.Abs(second)) * 1.7763568394002505e-15;
    }
    private static double Length(Vector vector)
    {
        var scale = Math.Max(Math.Abs(vector.X), Math.Abs(vector.Y));
        return double.IsPositiveInfinity(scale) ? scale : scale == 0 ? 0 : scale * Math.Sqrt(Math.Pow(vector.X / scale, 2) + Math.Pow(vector.Y / scale, 2));
    }
    private sealed class NativeGeometrySink(StreamGeometryContext context) : ICircularGeometrySink
    {
        public void SetFillRule(FillRule rule) => context.SetFillRule(rule);
        public void BeginFigure(Point start, bool isFilled) => context.BeginFigure(start, isFilled);
        public void ArcTo(Point end, Size radii, double rotation, bool largeArc, SweepDirection direction) => context.ArcTo(end, radii, rotation, largeArc, direction);
        public void LineTo(Point end) => context.LineTo(end);
        public void EndFigure(bool close) => context.EndFigure(close);
    }
}
