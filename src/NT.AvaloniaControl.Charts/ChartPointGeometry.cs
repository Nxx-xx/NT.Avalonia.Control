using Avalonia;

namespace NT.AvaloniaControl.Charts;

internal readonly record struct ChartPointShape(Point Center, double Diameter, double StrokeThickness, ChartMarkerShape Shape)
{
    internal double Radius => Diameter / 2;
    internal double Extent => Radius * (Shape is ChartMarkerShape.Square or ChartMarkerShape.Cross ? Math.Sqrt(2) :
        Shape == ChartMarkerShape.Rectangle ? Math.Sqrt(1.36) : 1) + StrokeThickness / 2;
    internal Rect Bounds
    {
        get
        {
            var halfStroke = StrokeThickness / 2;
            if (Shape is ChartMarkerShape.Circle or ChartMarkerShape.Cross or ChartMarkerShape.HorizontalLine or ChartMarkerShape.VerticalLine)
            {
                var width = Shape == ChartMarkerShape.VerticalLine ? 0 : Radius;
                var height = Shape == ChartMarkerShape.HorizontalLine ? 0 : Radius;
                return new(Center.X - width - halfStroke, Center.Y - height - halfStroke, width * 2 + StrokeThickness, height * 2 + StrokeThickness);
            }
            Span<Point> points = stackalloc Point[5];
            var count = Vertices(points);
            var left = double.PositiveInfinity; var right = double.NegativeInfinity;
            var top = double.PositiveInfinity; var bottom = double.NegativeInfinity;
            for (var i = 0; i < count; i++)
            { left = Math.Min(left, points[i].X); right = Math.Max(right, points[i].X); top = Math.Min(top, points[i].Y); bottom = Math.Max(bottom, points[i].Y); }
            return new(left - halfStroke, top - halfStroke, right - left + StrokeThickness, bottom - top + StrokeThickness);
        }
    }

    // Distance is measured to the visible marker footprint, not to its center.
    // Alpha is deliberately not sampled: a transparent fill retains its geometry.
    internal double DistanceSquared(Point query, Rect plot)
    {
        if (Shape == ChartMarkerShape.Circle) return CircleDistanceSquared(query, plot);
        if (Shape is ChartMarkerShape.Cross or ChartMarkerShape.HorizontalLine or ChartMarkerShape.VerticalLine)
        {
            var distance = double.PositiveInfinity;
            if (Shape is ChartMarkerShape.Cross or ChartMarkerShape.HorizontalLine)
                distance = CapsuleDistanceSquared(query, Center + new Vector(-Radius, Shape == ChartMarkerShape.Cross ? -Radius : 0),
                    Center + new Vector(Radius, Shape == ChartMarkerShape.Cross ? Radius : 0), StrokeThickness / 2, plot);
            if (Shape is ChartMarkerShape.Cross or ChartMarkerShape.VerticalLine)
                distance = Math.Min(distance, CapsuleDistanceSquared(query, Center + new Vector(Shape == ChartMarkerShape.Cross ? Radius : 0, -Radius),
                    Center + new Vector(Shape == ChartMarkerShape.Cross ? -Radius : 0, Radius), StrokeThickness / 2, plot));
            return distance;
        }
        Span<Point> vertices = stackalloc Point[5];
        var count = Vertices(vertices);
        var polygon = vertices[..count];
        var nearest = NearestPolygonPoint(query, polygon, out var inside);
        var halfStroke = StrokeThickness / 2;
        var delta = query - nearest;
        var length = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        if (inside || length <= halfStroke)
        {
            if (plot.Contains(query)) return 0;
        }
        else
        {
            var fullNearest = nearest + delta * (halfStroke / length);
            if (plot.Contains(fullNearest)) return Squared(length - halfStroke);
        }

        // A convex filled marker with a round stroke is its polygon plus an edge
        // capsule at every side. If the full-shape nearest point is clipped away,
        // the nearest surviving point lies on one of the four plot boundaries.
        var best = double.PositiveInfinity;
        for (var edge = 0; edge < 4; edge++)
        {
            ConsiderPolygonSlice(query, plot, edge, polygon, ref best);
            if (halfStroke <= 0) continue;
            for (var i = 0; i < count; i++)
                ConsiderCapsuleSlice(query, plot, edge, polygon[i], polygon[(i + 1) % count], halfStroke, ref best);
        }
        return best;
    }

    private static Point NearestPolygonPoint(Point query, ReadOnlySpan<Point> polygon, out bool inside)
    {
        var nearest = default(Point);
        var best = double.PositiveInfinity;
        inside = false;
        var count = polygon.Length;
        for (var i = 0; i < count; i++)
        {
            var a = polygon[i]; var b = polygon[(i + 1) % count];
            var point = NearestSegmentPoint(query, a, b);
            var distance = Squared(query.X - point.X) + Squared(query.Y - point.Y);
            if (distance < best) { best = distance; nearest = point; }
            if ((a.Y > query.Y) != (b.Y > query.Y) && query.X < (b.X - a.X) * (query.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return nearest;
    }

    private double CircleDistanceSquared(Point query, Rect plot)
    {
        var centerPoint = Center;
        var radius = Radius + StrokeThickness / 2;
        var dx = query.X - Center.X; var dy = query.Y - Center.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= radius && plot.Contains(query)) return 0;
        var distance = double.PositiveInfinity;
        if (length > radius)
        {
            var nearest = new Point(Center.X + dx / length * radius, Center.Y + dy / length * radius);
            if (plot.Contains(nearest)) distance = Squared(length - radius);
        }
        ConsiderEdge(plot.Left, true); ConsiderEdge(plot.Right, true);
        ConsiderEdge(plot.Top, false); ConsiderEdge(plot.Bottom, false);
        return distance;

        void ConsiderEdge(double coordinate, bool vertical)
        {
            var offset = Math.Abs(coordinate - (vertical ? centerPoint.X : centerPoint.Y));
            if (offset > radius) return;
            var half = Math.Sqrt((radius - offset) * (radius + offset));
            var center = vertical ? centerPoint.Y : centerPoint.X;
            var minimum = Math.Max(vertical ? plot.Top : plot.Left, center - half);
            var maximum = Math.Min(vertical ? plot.Bottom : plot.Right, center + half);
            if (minimum > maximum) return;
            var along = Math.Clamp(vertical ? query.Y : query.X, minimum, maximum);
            var point = vertical ? new Point(coordinate, along) : new Point(along, coordinate);
            distance = Math.Min(distance, Squared(query.X - point.X) + Squared(query.Y - point.Y));
        }
    }

    private int Vertices(Span<Point> vertices)
    {
        if (Shape is ChartMarkerShape.Square or ChartMarkerShape.Rectangle)
        {
            var height = Radius * (Shape == ChartMarkerShape.Rectangle ? .6 : 1);
            vertices[0] = Center + new Vector(-Radius, -height); vertices[1] = Center + new Vector(Radius, -height);
            vertices[2] = Center + new Vector(Radius, height); vertices[3] = Center + new Vector(-Radius, height);
            return 4;
        }
        var count = Shape is ChartMarkerShape.Triangle or ChartMarkerShape.InvertedTriangle ? 3 : Shape == ChartMarkerShape.Pentagon ? 5 : 4;
        var angle = Shape == ChartMarkerShape.InvertedTriangle ? Math.PI / 2 : -Math.PI / 2;
        for (var i = 0; i < count; i++)
            vertices[i] = Center + new Vector(Math.Cos(angle + i * Math.Tau / count) * Radius, Math.Sin(angle + i * Math.Tau / count) * Radius);
        return count;
    }

    private static double CapsuleDistanceSquared(Point query, Point a, Point b, double radius, Rect plot)
    {
        var nearest = NearestSegmentPoint(query, a, b);
        var delta = query - nearest;
        var length = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        if (length <= radius)
        {
            if (plot.Contains(query)) return 0;
        }
        else
        {
            var fullNearest = nearest + delta * (radius / length);
            if (plot.Contains(fullNearest)) return Squared(length - radius);
        }
        var best = double.PositiveInfinity;
        for (var edge = 0; edge < 4; edge++) ConsiderCapsuleSlice(query, plot, edge, a, b, radius, ref best);
        return best;
    }

    private static void ConsiderCapsuleSlice(Point query, Rect plot, int edge, Point a, Point b, double radius, ref double best)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length > 0)
        {
            var perpendicular = new Vector(-dy / length * radius, dx / length * radius);
            Span<Point> rectangle = stackalloc Point[4] { a + perpendicular, b + perpendicular, b - perpendicular, a - perpendicular };
            ConsiderPolygonSlice(query, plot, edge, rectangle, ref best);
        }
        ConsiderCircleSlice(query, plot, edge, a, radius, ref best);
        ConsiderCircleSlice(query, plot, edge, b, radius, ref best);
    }

    private static void ConsiderPolygonSlice(Point query, Rect plot, int edge, ReadOnlySpan<Point> polygon, ref double best)
    {
        var vertical = edge < 2;
        var coordinate = Boundary(plot, edge);
        var minimum = double.PositiveInfinity; var maximum = double.NegativeInfinity;
        for (var i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length];
            var first = vertical ? a.X : a.Y; var last = vertical ? b.X : b.Y;
            var along = vertical ? a.Y : a.X;
            if (first == coordinate) { minimum = Math.Min(minimum, along); maximum = Math.Max(maximum, along); }
            if (first == last || coordinate < Math.Min(first, last) || coordinate > Math.Max(first, last)) continue;
            var fraction = (coordinate - first) / (last - first);
            along = along * (1 - fraction) + (vertical ? b.Y : b.X) * fraction;
            minimum = Math.Min(minimum, along); maximum = Math.Max(maximum, along);
        }
        ConsiderInterval(query, plot, edge, minimum, maximum, ref best);
    }

    private static void ConsiderCircleSlice(Point query, Rect plot, int edge, Point center, double radius, ref double best)
    {
        var vertical = edge < 2;
        var offset = Math.Abs(Boundary(plot, edge) - (vertical ? center.X : center.Y));
        if (offset > radius) return;
        var half = Math.Sqrt((radius - offset) * (radius + offset));
        var along = vertical ? center.Y : center.X;
        ConsiderInterval(query, plot, edge, along - half, along + half, ref best);
    }

    private static void ConsiderInterval(Point query, Rect plot, int edge, double minimum, double maximum, ref double best)
    {
        var vertical = edge < 2;
        minimum = Math.Max(minimum, vertical ? plot.Top : plot.Left);
        maximum = Math.Min(maximum, vertical ? plot.Bottom : plot.Right);
        if (minimum > maximum) return;
        var along = Math.Clamp(vertical ? query.Y : query.X, minimum, maximum);
        var point = vertical ? new Point(Boundary(plot, edge), along) : new Point(along, Boundary(plot, edge));
        best = Math.Min(best, Squared(query.X - point.X) + Squared(query.Y - point.Y));
    }

    private static double Boundary(Rect plot, int edge) => edge switch { 0 => plot.Left, 1 => plot.Right, 2 => plot.Top, _ => plot.Bottom };

    private static Point NearestSegmentPoint(Point query, Point a, Point b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var length = dx * dx + dy * dy;
        var fraction = length == 0 ? 0 : Math.Clamp(((query.X - a.X) * dx + (query.Y - a.Y) * dy) / length, 0, 1);
        return new(a.X + dx * fraction, a.Y + dy * fraction);
    }
    private static double Squared(double value) => value * value;
}

internal static class ChartPointGeometry
{
    internal static bool TryCreate(ChartSeriesRenderContext context, int ordinal, out ChartPointShape shape)
        => TryCreate(context, context.Data.Points[ordinal], out shape);

    internal static bool TryCreate(ChartSeriesRenderContext context, ChartDataPoint point, out ChartPointShape shape)
    {
        shape = default;
        if (!context.Series.ShowMarkers || point.IsEmpty || !double.IsFinite(point.Point.X) || !double.IsFinite(point.Point.Y)) return false;
        var diameter = point.MarkerDiameter ?? ChartMarkerGeometry.Size(context.Series.MarkerSize);
        if (!double.IsFinite(diameter) || diameter <= 0) return false;
        var center = context.Projection.Project(point.Point);
        ChartVisibilityQuery.RecordProjected(1);
        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y)) return false;
        shape = new(center, diameter, ChartMarkerGeometry.StrokeThickness(context.Series), context.Series.MarkerShape);
        return true;
    }

    internal static double Radius(ChartSeriesRenderContext context, int ordinal) =>
        TryCreate(context, ordinal, out var shape) ? shape.Radius : 0;
    internal static double Extent(ChartSeriesRenderContext context, int ordinal) =>
        TryCreate(context, ordinal, out var shape) ? shape.Extent : 0;

    internal static IEnumerable<int> PointOrder(ChartSeriesRenderContext context)
        => ChartPointSceneCache.PointOrder(context);
}
