using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public partial class ShapeMap
{
    private delegate void AddOverlayMark(MapGlyphKey key, IReadOnlyList<ChartDataPoint> members, Geometry geometry, Point position,
        double value, IBrush? fill, IBrush? stroke, string text, PaintedMapMark primitive);

    private void BuildOverlayMarks(MapLayerSnapshot state, Rect plot, IReadOnlyDictionary<object, (double Value, IBrush? Fill)> original, AddOverlayMark add)
    {
        var layer = state.Layer;
        if (layer is MarkerLayer marker && state.Points is { } points)
            foreach (var point in points.Points)
            {
                var entry = point.Entry; var raw = original.GetValueOrDefault(entry.Id); var p = ProjectPoint(point.Position, plot); var size = marker.MarkerSize * Zoom;
                var fill = WithOpacity(raw.Fill ?? layer.Fill, layer.Opacity); var bounds = new Rect(p.X - size / 2, p.Y - size / 2, size, size);
                Geometry geometry; PaintedMapMark primitive;
                if (marker.MarkerType == MapMarkerType.Circle) { geometry = new EllipseGeometry(bounds); primitive = new([], bounds); }
                else
                {
                    Point[] ring = marker.MarkerType switch
                    {
                        MapMarkerType.Diamond => [new(p.X, bounds.Top), new(bounds.Right, p.Y), new(p.X, bounds.Bottom), new(bounds.Left, p.Y)],
                        MapMarkerType.Triangle => [new(p.X, bounds.Top), bounds.BottomRight, bounds.BottomLeft],
                        MapMarkerType.Pin => Enumerable.Range(0, 25).Select(i => { var angle = (135 + 270d * i / 24) * Math.PI / 180; return new Point(p.X + Math.Cos(angle) * size * .35, p.Y - size * .15 + Math.Sin(angle) * size * .35); }).Append(new Point(p.X, bounds.Bottom)).ToArray(),
                        _ => [bounds.TopLeft, bounds.TopRight, bounds.BottomRight, bounds.BottomLeft],
                    };
                    var polygon = new PaintedPolygon(ring, []); geometry = PolygonGeometry([polygon]); primitive = new([polygon], null);
                }
                var member = new ChartDataPoint(entry.SourceIndex, entry.Id, entry.Source, entry.Label, new(entry.Observation.Position.X, entry.Observation.Position.Y), false)
                { Value = entry.Value, MarkerDiameter = size, PointFill = fill };
                add(new(layer, MapElementKind.Marker, entry.Id, !ReferenceEquals(entry.Id, entry.Source)), Array.AsReadOnly(new[] { member }), geometry, p, entry.Value, fill,
                    WithOpacity(layer.Stroke, layer.Opacity), $"{entry.Label ?? entry.Id.ToString()}: {F(entry.Value)}", primitive);
            }
        if (layer is LineLayer line && state.Connections is { } connections)
        {
            if (line.MaximumThickness < line.MinimumThickness) throw new ArgumentException("Maximum line thickness must cover its minimum.");
            var maximum = connections.Connections.Count == 0 ? 0 : connections.Connections.Max(c => c.Entry.Connection.Value);
            foreach (var connection in connections.Connections)
            {
                var entry = connection.Entry; var value = entry.Connection.Value;
                var thickness = (maximum == 0 ? line.MinimumThickness : line.MinimumThickness + (line.MaximumThickness - line.MinimumThickness) * (value / maximum)) * Zoom;
                var segments = new List<(Point Start, Point End)>();
                foreach (var segment in connection.Segments)
                {
                    var a = ProjectPoint(segment.Start, plot); var b = ProjectPoint(segment.End, plot);
                    if (!line.IsCurved) segments.Add((a, b));
                    else
                    {
                        var control = new Point((a.X + b.X) / 2 + (b.Y - a.Y) * .2, (a.Y + b.Y) / 2 - (b.X - a.X) * .2); var previous = a;
                        for (var i = 1; i <= 128; i++) { var t = i / 128d; var p = new Point((1 - t) * (1 - t) * a.X + 2 * (1 - t) * t * control.X + t * t * b.X, (1 - t) * (1 - t) * a.Y + 2 * (1 - t) * t * control.Y + t * t * b.Y); segments.Add((previous, p)); previous = p; }
                    }
                }
                var position = segments[segments.Count / 2].Start; var fill = WithOpacity(original.GetValueOrDefault(entry.Id).Fill ?? line.Stroke ?? line.Fill, line.Opacity);
                var member = new ChartDataPoint(entry.SourceIndex, entry.Id, entry.Source, entry.Connection.Label, new(entry.Connection.Start.X, entry.Connection.Start.Y), false) { Value = value, Weight = value, PointFill = fill };
                var ends = line.ShowEndpoints ? connection.Segments.SelectMany(s => new[] { ProjectPoint(s.Start, plot), ProjectPoint(s.End, plot) }).ToArray() : [];
                add(new(layer, MapElementKind.Connection, entry.Id, !ReferenceEquals(entry.Id, entry.Source)), Array.AsReadOnly(new[] { member }), StrokeGeometry(segments, thickness / 2, ends, thickness), position, value, fill, null,
                    $"{entry.Connection.Label ?? entry.Id.ToString()}: {F(value)}", new([], null, segments.ToArray(), thickness / 2, null, ends, thickness));
            }
        }
        if (layer is VectorLayer vectors && state.Vectors is { } projected)
            foreach (var vector in projected.Vectors)
            {
                var entry = vector.Entry; var segments = vector.Segments.Select(s => (Start: ProjectPoint(s.Start, plot), End: ProjectPoint(s.End, plot))).ToList();
                var radius = vectors.StrokeThickness * Zoom / 2; var fill = WithOpacity(vectors.Stroke ?? vectors.Fill, vectors.Opacity); Geometry geometry; Point position; PaintedMapMark primitive;
                if (vector.Polygons.Count > 0)
                {
                    var polygons = vector.Polygons.Select(p => new PaintedPolygon(p.Outer.Select(v => ProjectPoint(v, plot)).ToArray(), p.Holes.Select(h => h.Select(v => ProjectPoint(v, plot)).ToArray()).ToArray())).ToArray();
                    geometry = PolygonGeometry(polygons); position = LabelPoint(polygons); primitive = new(polygons, null); fill = WithOpacity(vectors.Fill ?? vectors.Stroke, vectors.Opacity);
                }
                else if (vector.Entry.Vector is MapCircleVector circle && vector.Center is { } center)
                {
                    position = ProjectPoint(center, plot); var r = circle.Radius * Zoom; var bounds = new Rect(position.X - r, position.Y - r, r * 2, r * 2);
                    geometry = new EllipseGeometry(bounds); primitive = new([], bounds); fill = WithOpacity(vectors.Fill ?? vectors.Stroke, vectors.Opacity);
                }
                else
                {
                    if (entry.Vector is MapArcVector arc && vector.Center is { } arcCenter)
                    {
                        var centerPoint = ProjectPoint(arcCenter, plot); var r = arc.Radius * Zoom; Point At(int i) { var angle = (arc.StartAngle % 360 + arc.SweepAngle * i / 128) * Math.PI / 180; return new(centerPoint.X + r * Math.Cos(angle), centerPoint.Y + r * Math.Sin(angle)); }
                        for (var i = 0; i < 128; i++) segments.Add((At(i), At(i + 1)));
                    }
                    if (segments.Count == 0) continue; position = segments[segments.Count / 2].Start; geometry = StrokeGeometry(segments, radius); primitive = new([], null, segments.ToArray(), radius);
                }
                var member = new ChartDataPoint(entry.SourceIndex, entry.Id, entry.Source, entry.Vector.Label, default, false) { PointFill = fill };
                add(new(layer, MapElementKind.Vector, entry.Id, !ReferenceEquals(entry.Id, entry.Source)), Array.AsReadOnly(new[] { member }), geometry, position, 0, fill, null, entry.Vector.Label ?? entry.Id.ToString() ?? "Vector", primitive);
            }
        if (layer is PieLayer pieLayer && state.Pies is { } pies)
            foreach (var slice in pies.Slices)
            {
                var center = ProjectPoint(slice.Position, plot); var radius = pieLayer.Radius * Zoom; var angle = (slice.StartAngle + slice.SweepAngle / 2) * Math.PI / 180;
                var position = new Point(center.X + Math.Cos(angle) * radius * .6, center.Y + Math.Sin(angle) * radius * .6); var fill = WithOpacity(BrushAt(slice.SliceIndex), layer.Opacity);
                var member = new ChartDataPoint(slice.Entry.SourceIndex, slice.Entry.Id, slice.Entry.Source, slice.Slice.Label ?? slice.Entry.Pie.Label, new(slice.Entry.Pie.Position.X, slice.Entry.Pie.Position.Y), false) { Value = slice.Slice.Value, Weight = slice.Slice.Value, PointFill = fill };
                add(new(layer, MapElementKind.PieSlice, (slice.Entry.Id, slice.SliceIndex), true), Array.AsReadOnly(new[] { member }), SectorGeometry(center, radius, slice.StartAngle, slice.SweepAngle), position,
                    slice.Slice.Value, fill, WithOpacity(layer.Stroke, layer.Opacity), $"{slice.Entry.Pie.Label ?? slice.Entry.Id.ToString()} / {slice.Slice.Label ?? slice.SliceIndex.ToString()}: {F(slice.Slice.Value)}", new([], null, Sector: new(center, radius, slice.StartAngle, slice.SweepAngle)));
            }
    }

    private static double SegmentDistance(Point p, Point a, Point b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y; var length = dx * dx + dy * dy;
        var t = length == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length, 0, 1);
        var x = p.X - a.X - t * dx; var y = p.Y - a.Y - t * dy; return Math.Sqrt(x * x + y * y);
    }

    private static Geometry StrokeGeometry(IEnumerable<(Point Start, Point End)> segments, double radius, Point[]? endpoints = null, double endpointRadius = 0)
    {
        var geometry = new StreamGeometry(); using var path = geometry.Open(); path.SetFillRule(FillRule.NonZero);
        foreach (var (a, b) in segments)
        {
            var dx = b.X - a.X; var dy = b.Y - a.Y; var length = Math.Sqrt(dx * dx + dy * dy);
            if (length == 0) { Disk(a, radius); continue; }
            var nx = -dy / length * radius; var ny = dx / length * radius;
            path.BeginFigure(new(a.X - nx, a.Y - ny), true); path.LineTo(new(b.X - nx, b.Y - ny));
            path.ArcTo(new(b.X + nx, b.Y + ny), new(radius, radius), 0, false, SweepDirection.Clockwise); path.LineTo(new(a.X + nx, a.Y + ny));
            path.ArcTo(new(a.X - nx, a.Y - ny), new(radius, radius), 0, false, SweepDirection.Clockwise); path.EndFigure(true);
        }
        if (endpoints is not null) foreach (var p in endpoints) Disk(p, endpointRadius);
        return geometry;
        void Disk(Point center, double r)
        {
            path.BeginFigure(new(center.X, center.Y - r), true); path.ArcTo(new(center.X, center.Y + r), new(r, r), 0, false, SweepDirection.Clockwise);
            path.ArcTo(new(center.X, center.Y - r), new(r, r), 0, false, SweepDirection.Clockwise); path.EndFigure(true);
        }
    }

    private static Geometry SectorGeometry(Point center, double radius, double start, double sweep)
    {
        if (sweep >= 360) return new EllipseGeometry(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
        Point At(double angle) => new(center.X + Math.Cos(angle * Math.PI / 180) * radius, center.Y + Math.Sin(angle * Math.PI / 180) * radius);
        var geometry = new StreamGeometry(); using var path = geometry.Open(); path.BeginFigure(center, true); path.LineTo(At(start));
        var pieces = Math.Max(1, (int)Math.Ceiling(sweep / 180)); for (var i = 1; i <= pieces; i++) path.ArcTo(At(start + sweep * i / pieces), new(radius, radius), 0, false, SweepDirection.Clockwise);
        path.LineTo(center); path.EndFigure(true); return geometry;
    }
}
