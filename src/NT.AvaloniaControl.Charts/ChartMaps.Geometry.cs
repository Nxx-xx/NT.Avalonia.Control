namespace NT.AvaloniaControl.Charts;

public static partial class ChartMaps
{
    private static void ValidateRing(IReadOnlyList<MapCoordinate> ring, ref long work, CancellationToken token,
        MapTopologyRule topologyRule, ref int intersections)
    {
        if (ring.Count < 4 || ring[0] != ring[^1]) throw new ArgumentException("Map rings must be closed and have at least four coordinates.");
        var bounds = BoundsOf(ring.Select(p => new ChartPoint(p.X, p.Y)));
        if (bounds.MinX == bounds.MaxX || bounds.MinY == bounds.MaxY) throw new ArgumentException("A map ring must have nonzero area.");
        var normalized = ring.Select(p => bounds.Normalize(new(p.X, p.Y))).ToArray();
        double area = 0;
        for (var i = 0; i < normalized.Length - 1; i++)
        {
            if (ring[i] == ring[i + 1]) throw new ArgumentException("Consecutive duplicate map coordinates are not supported.");
            area += normalized[i].X * normalized[i + 1].Y - normalized[i + 1].X * normalized[i].Y;
        }
        if (area == 0) throw new ArgumentException("A map ring must have nonzero signed area.");
        var segments = ring.Count - 1;
        var pairs = (long)segments * (segments - 3) / 2;
        if (work + Math.Max(0, pairs) > MaximumWork) throw new ArgumentOutOfRangeException(nameof(ring), "Map intersection work budget exceeded.");
        work += Math.Max(0, pairs);
        for (var i = 0; i < segments; i++) for (var j = i + 2; j < segments; j++)
        {
            if (i == 0 && j == segments - 1) continue;
            if ((j & 255) == 0) token.ThrowIfCancellationRequested();
            if (SegmentsIntersect(normalized[i], normalized[i + 1], normalized[j], normalized[j + 1]))
            {
                if (topologyRule == MapTopologyRule.StrictSimple) throw new ArgumentException("Self-intersecting or self-touching map rings require the explicit EvenOdd topology rule.");
                intersections++;
            }
        }
        token.ThrowIfCancellationRequested();
    }

    private static double Cross(ChartPoint a, ChartPoint b, ChartPoint c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    private static bool OnSegment(ChartPoint a, ChartPoint b, ChartPoint p) => Cross(a, b, p) == 0
        && p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X) && p.Y >= Math.Min(a.Y, b.Y) && p.Y <= Math.Max(a.Y, b.Y);
    private static bool SegmentsIntersect(ChartPoint a, ChartPoint b, ChartPoint c, ChartPoint d)
    {
        var abC = Cross(a, b, c); var abD = Cross(a, b, d); var cdA = Cross(c, d, a); var cdB = Cross(c, d, b);
        return (abC > 0 && abD < 0 || abC < 0 && abD > 0) && (cdA > 0 && cdB < 0 || cdA < 0 && cdB > 0)
            || abC == 0 && OnSegment(a, b, c) || abD == 0 && OnSegment(a, b, d)
            || cdA == 0 && OnSegment(c, d, a) || cdB == 0 && OnSegment(c, d, b);
    }

    /// <summary>Boundary-inclusive even/odd ring test, in the same projected coordinates as the ring.</summary>
    public static bool Contains(IReadOnlyList<ChartPoint> ring, ChartPoint point) => ContainsRing(ring, point, true);
    private static bool ContainsRing(IReadOnlyList<ChartPoint> ring, ChartPoint point, bool roundedBoundary)
    {
        ArgumentNullException.ThrowIfNull(ring); if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) return false;
        var bounds = BoundsOf(ring); var p = bounds.Normalize(point); var inside = false;
        var errorX = BoundaryCoordinateError(bounds.MinX, bounds.MaxX);
        var errorY = BoundaryCoordinateError(bounds.MinY, bounds.MaxY);
        for (var i = 0; i < ring.Count - 1; i++)
        {
            var a = bounds.Normalize(ring[i]); var b = bounds.Normalize(ring[i + 1]);
            if (roundedBoundary ? OnBoundarySegment(a, b, p, errorX, errorY) : OnSegment(a, b, p)) return true;
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    // Geographic projection and the scale-safe normalization each round represented coordinates.
    // Bound that uncertainty in normalized units using the axis' scaled span, rather than a fixed
    // world-coordinate epsilon. Eight machine epsilons cover the bounded division/subtraction
    // operations; short spans far from the origin consequently retain their source ULP scale.
    private static double BoundaryCoordinateError(double minimum, double maximum)
    {
        if (minimum == maximum) return 0;
        var scale = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
        var scaledSpan = maximum / scale - minimum / scale;
        return 8 * 2.2204460492503131e-16 / scaledSpan;
    }
    private static bool OnBoundarySegment(ChartPoint a, ChartPoint b, ChartPoint p, double errorX, double errorY)
    {
        if (p.X < Math.Min(a.X, b.X) - errorX || p.X > Math.Max(a.X, b.X) + errorX
            || p.Y < Math.Min(a.Y, b.Y) - errorY || p.Y > Math.Max(a.Y, b.Y) + errorY) return false;
        var dx = b.X - a.X; var dy = b.Y - a.Y; var px = p.X - a.X; var py = p.Y - a.Y;
        var left = dx * py; var right = dy * px;
        var uncertainty = 2 * (Math.Abs(dx) * errorY + Math.Abs(dy) * errorX
            + Math.Abs(px) * errorY + Math.Abs(py) * errorX)
            + 8 * 2.2204460492503131e-16 * (Math.Abs(left) + Math.Abs(right));
        return Math.Abs(left - right) <= uncertainty;
    }

    /// <summary>Tests outer fill excluding hole interiors and boundaries.</summary>
    public static bool Contains(MapProjectedPolygon polygon, ChartPoint point) => Contains(polygon.Outer, point)
        && !polygon.Holes.Any(hole => Contains(hole, point));

    public static MapProjectedGeometry Project(MapGeometrySnapshot snapshot, MapProjection projection,
        CancellationToken cancellationToken = default, MapTopologyRule topologyRule = MapTopologyRule.StrictSimple)
    {
        ArgumentNullException.ThrowIfNull(snapshot); CheckProjection(projection); cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(topologyRule)) throw new ArgumentOutOfRangeException(nameof(topologyRule));
        var result = new List<MapProjectedFeature>(); var clamped = 0; var generated = 0; var intersections = 0; long work = 0;
        foreach (var feature in snapshot.Features)
        {
            cancellationToken.ThrowIfCancellationRequested(); var polygons = new List<MapProjectedPolygon>(); var projectedOriginals = new List<ChartPoint>();
            foreach (var polygon in feature.Feature.Polygons)
            {
                // Validate geographic topology before Mercator's documented latitude clamp collapses
                // legal polar edges onto its finite world boundary.
                var topologyProjection = projection == MapProjection.WebMercator ? MapProjection.Equirectangular : projection;
                var ignored = 0;
                var topologyOuter = ProjectRing(polygon.Outer, topologyProjection, null, ref ignored);
                var topologyCenter = projection == MapProjection.Identity ? 0 : (topologyOuter.Min(p => p.X) + topologyOuter.Max(p => p.X)) / 2;
                var topologyHoles = polygon.Holes.Select(hole => ProjectRing(hole, topologyProjection, topologyCenter, ref ignored)).ToArray();
                ValidateRing(topologyOuter.Select(p => new MapCoordinate(p.X, p.Y)).ToArray(), ref work, cancellationToken, topologyRule, ref intersections);
                ValidateHoles(topologyOuter, topologyHoles, ref work, cancellationToken, topologyRule, ref intersections);
                var outer = projection == topologyProjection ? topologyOuter : ProjectRing(polygon.Outer, projection, null, ref clamped);
                projectedOriginals.AddRange(outer);
                var centerX = projection == MapProjection.Identity ? 0 : (outer.Min(p => p.X) + outer.Max(p => p.X)) / 2;
                var holes = projection == topologyProjection ? topologyHoles : polygon.Holes.Select(hole => ProjectRing(hole, projection, centerX, ref clamped)).ToArray();
                if (projection == MapProjection.Identity)
                {
                    generated = checked(generated + outer.Length + holes.Sum(h => h.Length));
                    polygons.Add(new(outer, holes.Select(h => (IReadOnlyList<ChartPoint>)Array.AsReadOnly(h)).ToArray()));
                }
                else
                {
                    var min = outer.Min(p => p.X); var max = outer.Max(p => p.X);
                    if (max - min > 1 + 1e-12) throw new ArgumentException("A map ring cannot wrap across more than one world; pre-cut the geometry.");
                    var first = (int)Math.Floor(min); var last = (int)Math.Ceiling(max) - 1;
                    for (var strip = first; strip <= last; strip++)
                    {
                        var clipped = ClipStrip(outer, strip, strip + 1);
                        if (clipped.Length == 0) continue;
                        var clippedHoles = new List<IReadOnlyList<ChartPoint>>();
                        foreach (var hole in holes)
                        {
                            var clippedHole = ClipStrip(hole, strip, strip + 1);
                            if (clippedHole.Length > 0) clippedHoles.Add(Array.AsReadOnly(clippedHole.Select(p => new ChartPoint(Math.Clamp(p.X - strip, 0, 1), p.Y)).ToArray()));
                        }
                        generated = checked(generated + clipped.Length + clippedHoles.Sum(h => h.Count));
                        if (generated > MaximumVertexCount * 3) throw new ArgumentOutOfRangeException(nameof(snapshot), "Projected map vertex budget exceeded.");
                        polygons.Add(new(clipped.Select(p => new ChartPoint(Math.Clamp(p.X - strip, 0, 1), p.Y)).ToArray(), clippedHoles.ToArray()));
                    }
                }
            }
            result.Add(new(feature, polygons.ToArray(), polygons.Count == 0 ? BoundsOf(projectedOriginals) : BoundsOf(polygons.SelectMany(p => p.Outer))));
        }
        return new(result.ToArray(), BoundsOf(result.SelectMany(f => f.Polygons).SelectMany(p => p.Outer)), clamped, intersections);
    }

    private static ChartPoint[] ProjectRing(IReadOnlyList<MapCoordinate> ring, MapProjection projection, double? centerX, ref int clamped)
    {
        var points = new ChartPoint[ring.Count]; var previousLongitude = 0d;
        for (var i = 0; i < ring.Count; i++)
        {
            var point = ring[i]; CheckCoordinate(point, projection);
            if (projection == MapProjection.WebMercator && Math.Abs(point.Y) > MercatorLatitudeLimit) clamped++;
            var p = ProjectUnchecked(point, projection);
            if (projection != MapProjection.Identity)
            {
                var longitude = point.X;
                if (i > 0)
                {
                    var rawDelta = point.X - ring[i - 1].X;
                    if (Math.Abs(point.Y) == 90 && point.Y == ring[i - 1].Y && Math.Abs(rawDelta) == 360)
                        longitude = previousLongitude + rawDelta; // Explicit polar closure spans the whole pole.
                    else
                    {
                        while (longitude - previousLongitude > 180) longitude -= 360;
                        while (longitude - previousLongitude < -180) longitude += 360;
                    }
                }
                previousLongitude = longitude; p = new((longitude + 180) / 360, p.Y);
            }
            points[i] = p;
        }
        if (projection != MapProjection.Identity)
        {
            if (Math.Abs(points[0].X - points[^1].X) > 1e-12) throw new ArgumentException("Map rings that wind around the globe require pre-cut geometry.");
            points[^1] = points[0];
            if (centerX is { } center)
            {
                var shift = Math.Round(center - (points.Min(p => p.X) + points.Max(p => p.X)) / 2);
                if (shift != 0) for (var i = 0; i < points.Length; i++) points[i] = new(points[i].X + shift, points[i].Y);
            }
        }
        var clean = new List<ChartPoint>();
        foreach (var point in points) if (clean.Count == 0 || clean[^1] != point) clean.Add(point);
        if (clean.Count > 0 && clean[0] != clean[^1]) clean.Add(clean[0]);
        return clean.ToArray();
    }

    private static void ValidateHoles(ChartPoint[] outer, ChartPoint[][] holes, ref long work, CancellationToken token,
        MapTopologyRule topologyRule, ref int intersections)
    {
        var bounds = BoundsOf(outer); var normOuter = outer.Select(bounds.Normalize).ToArray();
        var normalized = holes.Select(h => h.Select(bounds.Normalize).ToArray()).ToArray();
        for (var i = 0; i < holes.Length; i++)
        {
            ValidateRing(holes[i].Select(p => new MapCoordinate(p.X, p.Y)).ToArray(), ref work, token, topologyRule, ref intersections);
            if (!ContainsRing(outer, holes[i][0], false)) throw new ArgumentException("A map hole must lie strictly inside its outer ring.");
            EnsureDisjoint(normOuter, normalized[i], ref work, token);
            for (var j = 0; j < i; j++)
            {
                EnsureDisjoint(normalized[j], normalized[i], ref work, token);
                if (ContainsRing(holes[j], holes[i][0], false) || ContainsRing(holes[i], holes[j][0], false)) throw new ArgumentException("Map holes cannot overlap or nest.");
            }
        }
    }
    private static void EnsureDisjoint(ChartPoint[] a, ChartPoint[] b, ref long work, CancellationToken token)
    {
        var pairs = (long)(a.Length - 1) * (b.Length - 1);
        if (work + pairs > MaximumWork) throw new ArgumentOutOfRangeException(nameof(a), "Map intersection work budget exceeded."); work += pairs;
        for (var i = 0; i < a.Length - 1; i++) for (var j = 0; j < b.Length - 1; j++)
        {
            if ((j & 255) == 0) token.ThrowIfCancellationRequested();
            if (SegmentsIntersect(a[i], a[i + 1], b[j], b[j + 1])) throw new ArgumentException("Map holes cannot touch or cross another ring.");
        }
    }

    private static ChartPoint[] ClipStrip(ChartPoint[] ring, double left, double right)
    {
        var points = ring.Take(ring.Length - 1).ToList();
        points = Clip(points, left, true); points = Clip(points, right, false);
        for (var i = points.Count - 1; i > 0; i--) if (points[i] == points[i - 1]) points.RemoveAt(i);
        if (points.Count > 1 && points[0] == points[^1]) points.RemoveAt(points.Count - 1);
        if (points.Count < 3) return [];
        var bounds = BoundsOf(points);
        if (bounds.MinX == bounds.MaxX || bounds.MinY == bounds.MaxY) return [];
        double area = 0;
        for (var i = 0; i < points.Count; i++)
        { var a = bounds.Normalize(points[i]); var b = bounds.Normalize(points[(i + 1) % points.Count]); area += a.X * b.Y - b.X * a.Y; }
        if (area == 0) return [];
        points.Add(points[0]); return points.ToArray();
    }
    private static List<ChartPoint> Clip(List<ChartPoint> input, double boundary, bool keepRight)
    {
        var output = new List<ChartPoint>(); if (input.Count == 0) return output; var a = input[^1];
        foreach (var b in input)
        {
            var insideA = keepRight ? a.X >= boundary : a.X <= boundary;
            var insideB = keepRight ? b.X >= boundary : b.X <= boundary;
            if (insideA != insideB)
            {
                var t = (boundary - a.X) / (b.X - a.X); output.Add(new(boundary, a.Y + (b.Y - a.Y) * t));
            }
            if (insideB) output.Add(b); a = b;
        }
        return output;
    }
}
