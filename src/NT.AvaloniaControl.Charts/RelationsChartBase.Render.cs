using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public abstract partial class RelationsChartBase
{
    private sealed record PaintedRelation(RelationGlyphKey Key, Point[] Polygon, Point[] Arrow, Point[] Centerline, string? Label, RelationNodeShape Shape, Rect NodeBounds);
    private Point Project(ChartPoint point, Rect plot) => ApplyViewport(new(plot.Left + point.X * plot.Width, plot.Top + point.Y * plot.Height), plot);
    private IReadOnlyList<EngineeringMarkInfo> PaintLayout(Rect plot)
    {
        _painted.Clear(); _indices.Clear(); var marks = new List<EngineeringMarkInfo>();
        void Add(RelationGlyphKey key, object source, int index, double value, Point position, Point[] polygon, Point[] centerline, IBrush fill, string? label, RelationNodeShape shape = RelationNodeShape.Rectangle, Rect bounds = default, double? originalValue = null)
        {
            if (polygon.Length < 3) return;
            Geometry geometry = shape == RelationNodeShape.Ellipse && bounds.Width > 0 ? new EllipseGeometry(bounds) : new PolylineGeometry(polygon, true);
            var arrow = key.Kind == RelationElementKind.Edge && this is FlowChart { ShowArrows: true } ? ArrowPolygon(centerline, 7 * Math.Sqrt(Zoom)) : [];
            if (arrow.Length == 3) geometry = new CombinedGeometry(GeometryCombineMode.Union, geometry, new PolylineGeometry(arrow, true));
            var raw = originalValue ?? value;
            var member = new ChartDataPoint(index, key.Identity, source, label, new(index, raw), false) { Value = raw, Weight = value };
            var mark = new EngineeringMarkInfo(Array.AsReadOnly(new[] { member }), geometry, fill, position) { Key = key, Value = value, TooltipText = label ?? $"{key.Kind}: {key.Identity}", Stroke = Background ?? Brushes.White };
            _indices.TryAdd(new(key), marks.Count); marks.Add(mark); _keys.Add(mark, key); _painted.Add(mark, new(key, polygon, arrow, centerline, label, shape, bounds));
        }
        if (_graphLayout is { } graph)
        {
            foreach (var edge in graph.Edges)
            {
                if (edge.Entry.Weight <= 0) continue;
                var points = edge.Points.Select(p => Project(p, plot)).ToArray(); if (points.Length < 2) continue;
                Point[] polygon;
                if (this is SankeyChart)
                {
                    if (edge.Thickness <= 0) continue;
                    // Forward Sankey bands retain a vertical width at every sampled point;
                    // a perpendicular stroke would overshoot X for steep, densely spaced links.
                    // Return corridors use their reserved full-thickness orthogonal margins.
                    // Project the complete unit-square polygon for either viewport aspect.
                    var normalized = edge.Points.Select(p => new Point(p.X, p.Y)).ToArray();
                    var band = edge.IsBackEdge ? StrokePolygon(normalized, edge.Thickness, true) : VerticalBandPolygon(normalized, edge.Thickness);
                    polygon = band.Select(p => Project(new(p.X, p.Y), plot)).ToArray();
                }
                else polygon = StrokePolygon(points, Math.Max(2, edge.Thickness * Math.Min(plot.Width, plot.Height) * Zoom));
                Add(EdgeKey(edge.Entry), edge.Entry.Source, edge.Entry.SourceIndex, edge.Entry.Weight, points[points.Length / 2], polygon, points,
                    BrushAt(edge.Entry.SourceNodeIndex), edge.Entry.Edge.Label ?? $"{edge.Entry.Edge.SourceId} → {edge.Entry.Edge.TargetId}: {F(edge.Entry.Weight)}");
            }
            foreach (var node in graph.Nodes)
            {
                var a = Project(new(node.X, node.Y), plot); var b = Project(new(node.X + node.Width, node.Y + node.Height), plot);
                var rect = new Rect(a, b); var shape = node.Entry.Node.Shape;
                Add(NodeKey(node.Entry), node.Entry.Source, node.Entry.SourceIndex, Math.Max(node.Entry.IncomingWeight, node.Entry.OutgoingWeight), rect.Center,
                    ShapePolygon(rect, shape), [], BrushAt(node.Entry.Index), node.Entry.Node.Label ?? node.Entry.Id.ToString(), shape, rect, node.Entry.Node.Weight);
            }
        }
        if (_chordLayout is { } chord)
        {
            var circle = Square(plot);
            Point Radial(double angle, double radius)
            { var radians = angle * Math.PI / 180; return ApplyViewport(new(circle.Center.X + Math.Cos(radians) * radius * circle.Width / 2, circle.Center.Y + Math.Sin(radians) * radius * circle.Height / 2), plot); }
            var inner = this is ChordDiagramChart chart ? chart.InnerRadius : .82;
            foreach (var ribbon in chord.Ribbons)
            {
                if (ribbon.Entry.Weight <= 0 || ribbon.SourceSweepAngle <= 0 || ribbon.TargetSweepAngle <= 0) continue;
                var polygon = new List<Point>(); Arc(ribbon.SourceStartAngle, ribbon.SourceSweepAngle, inner, polygon);
                Curve(polygon[^1], Radial(ribbon.TargetStartAngle, inner), polygon); Arc(ribbon.TargetStartAngle, ribbon.TargetSweepAngle, inner, polygon);
                Curve(polygon[^1], Radial(ribbon.SourceStartAngle, inner), polygon);
                var a = Radial(ribbon.SourceStartAngle + ribbon.SourceSweepAngle / 2, inner); var b = Radial(ribbon.TargetStartAngle + ribbon.TargetSweepAngle / 2, inner);
                var center = ApplyViewport(circle.Center, plot); var position = new Point((a.X + 2 * center.X + b.X) / 4, (a.Y + 2 * center.Y + b.Y) / 4);
                Add(EdgeKey(ribbon.Entry), ribbon.Entry.Source, ribbon.Entry.SourceIndex, ribbon.Entry.Weight, position, polygon.ToArray(), [], BrushAt(ribbon.Entry.SourceNodeIndex), ribbon.Entry.Edge.Label ?? $"{ribbon.Entry.Edge.SourceId} → {ribbon.Entry.Edge.TargetId}: {F(ribbon.Entry.Weight)}");
            }
            foreach (var node in chord.Nodes)
            {
                if (node.SweepAngle <= 0) continue;
                var polygon = new List<Point>(); Arc(node.StartAngle, node.SweepAngle, 1, polygon); Arc(node.StartAngle + node.SweepAngle, -node.SweepAngle, inner, polygon);
                Add(NodeKey(node.Entry), node.Entry.Source, node.Entry.SourceIndex, Math.Max(node.Entry.IncomingWeight, node.Entry.OutgoingWeight), Radial(node.StartAngle + node.SweepAngle / 2, (inner + 1) / 2), polygon.ToArray(), [], BrushAt(node.Entry.Index), node.Entry.Node.Label ?? node.Entry.Id.ToString(), originalValue: node.Entry.Node.Weight);
            }
            void Arc(double start, double sweep, double radius, List<Point> points)
            { var count = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / 3)); for (var i = 0; i <= count; i++) points.Add(Radial(start + sweep * i / count, radius)); }
            void Curve(Point a, Point b, List<Point> points)
            { var c = ApplyViewport(circle.Center, plot); for (var i = 1; i <= 24; i++) { var t = i / 24d; var s = 1 - t; points.Add(new(s * s * a.X + 2 * s * t * c.X + t * t * b.X, s * s * a.Y + 2 * s * t * c.Y + t * t * b.Y)); } }
        }
        return marks.AsReadOnly();
    }
    private static Point[] ShapePolygon(Rect rect, RelationNodeShape shape) => shape switch
    {
        RelationNodeShape.Diamond => [new(rect.Center.X, rect.Top), new(rect.Right, rect.Center.Y), new(rect.Center.X, rect.Bottom), new(rect.Left, rect.Center.Y)],
        RelationNodeShape.Hexagon => [new(rect.Left + rect.Width / 4, rect.Top), new(rect.Right - rect.Width / 4, rect.Top), new(rect.Right, rect.Center.Y), new(rect.Right - rect.Width / 4, rect.Bottom), new(rect.Left + rect.Width / 4, rect.Bottom), new(rect.Left, rect.Center.Y)],
        RelationNodeShape.Ellipse => Enumerable.Range(0, 64).Select(i => new Point(rect.Center.X + Math.Cos(i * Math.PI / 32) * rect.Width / 2, rect.Center.Y + Math.Sin(i * Math.PI / 32) * rect.Height / 2)).ToArray(),
        RelationNodeShape.RoundedRectangle => Rounded(rect),
        _ => [rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft],
    };
    private static Point[] Rounded(Rect rect)
    {
        var radius = Math.Min(rect.Width, rect.Height) * .2; var result = new List<Point>();
        foreach (var (x, y, start) in new[] { (rect.Right - radius, rect.Top + radius, -90d), (rect.Right - radius, rect.Bottom - radius, 0d), (rect.Left + radius, rect.Bottom - radius, 90d), (rect.Left + radius, rect.Top + radius, 180d) })
            for (var i = 0; i <= 6; i++) { var angle = (start + 90 * i / 6) * Math.PI / 180; result.Add(new(x + Math.Cos(angle) * radius, y + Math.Sin(angle) * radius)); }
        return result.ToArray();
    }
    private static Point[] StrokePolygon(Point[] points, double thickness, bool horizontalPorts = false)
    {
        var left = new Point[points.Length]; var right = new Point[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            var a = points[Math.Max(0, i - 1)]; var b = points[Math.Min(points.Length - 1, i + 1)]; var dx = b.X - a.X; var dy = b.Y - a.Y; var length = Math.Sqrt(dx * dx + dy * dy);
            var nx = length > 1e-12 ? -dy / length * thickness / 2 : thickness / 2; var ny = length > 1e-12 ? dx / length * thickness / 2 : 0;
            if (horizontalPorts && (i == 0 || i == points.Length - 1)) { nx = 0; ny = thickness / 2; }
            left[i] = new(points[i].X + nx, points[i].Y + ny); right[i] = new(points[i].X - nx, points[i].Y - ny);
        }
        return left.Concat(right.Reverse()).ToArray();
    }
    private static Point[] VerticalBandPolygon(Point[] points, double thickness) =>
        points.Select(p => new Point(p.X, p.Y + thickness / 2))
            .Concat(points.Reverse().Select(p => new Point(p.X, p.Y - thickness / 2))).ToArray();
    private static Point[] ArrowPolygon(Point[] points, double size)
    {
        if (points.Length < 2) return [];
        var b = points[^1];
        for (var i = points.Length - 2; i >= 0; i--)
        {
            var a = points[i]; var dx = b.X - a.X; var dy = b.Y - a.Y; var length = Math.Sqrt(dx * dx + dy * dy); if (length <= 1e-9) continue;
            var ux = dx / length; var uy = dy / length;
            return [b, new(b.X - ux * size - uy * size / 2, b.Y - uy * size + ux * size / 2), new(b.X - ux * size + uy * size / 2, b.Y - uy * size - ux * size / 2)];
        }
        return [];
    }
    protected override bool ContainsMark(EngineeringMarkInfo mark, Point point)
    {
        if (!_painted.TryGetValue(mark, out var item)) return false;
        if (item.Shape == RelationNodeShape.Ellipse && item.NodeBounds.Width > 0)
        { var x = (point.X - item.NodeBounds.Center.X) / (item.NodeBounds.Width / 2); var y = (point.Y - item.NodeBounds.Center.Y) / (item.NodeBounds.Height / 2); return x * x + y * y <= 1; }
        return PolygonContains(item.Polygon, point) || item.Arrow.Length == 3 && PolygonContains(item.Arrow, point);
    }
    private static bool PolygonContains(Point[] polygon, Point point)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var a = polygon[j]; var b = polygon[i]; var cross = (point.X - a.X) * (b.Y - a.Y) - (point.Y - a.Y) * (b.X - a.X);
            if (Math.Abs(cross) < 1e-7 && point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) && point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y)) return true;
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    {
        using var clip = context.PushClip(plot);
        if (ShowGroups && _graphLayout is { } graph)
            foreach (var group in graph.Nodes.Where(n => !string.IsNullOrEmpty(n.Entry.Node.Group)).GroupBy(n => n.Entry.Node.Group))
            {
                var nodes = group.ToArray(); var left = nodes.Min(n => n.X); var right = nodes.Max(n => n.X + n.Width); var top = nodes.Min(n => n.Y); var bottom = nodes.Max(n => n.Y + n.Height);
                var rect = new Rect(Project(new(left, top), plot), Project(new(right, bottom), plot)).Inflate(5);
                var visible = rect.Intersect(plot); if (visible.Width <= 0 || visible.Height <= 0) continue;
                context.DrawRectangle(null, new Pen(Foreground ?? Brushes.Gray, 1) { DashStyle = DashStyle.Dash }, rect);
                DrawGroupLabel(context, group.Key!, rect, visible, plot);
            }
        foreach (var (mark, item) in _painted)
        {
            if (!UsesDataLabelTemplate && item.Key.Kind == RelationElementKind.Node && ShowLabels && item.Label is { } nodeLabel)
            {
                var maxWidth = item.NodeBounds.Width > 0 ? Math.Max(1, item.NodeBounds.Width - 4) : Math.Max(16, mark.Bounds.Width);
                using var nodeClip = context.PushGeometryClip(mark.Geometry); DrawLabel(context, nodeLabel, mark.Position, maxWidth);
            }
            if (item.Key.Kind != RelationElementKind.Edge) continue;
            if (!UsesDataLabelTemplate && ShowEdgeLabels && item.Label is { } edgeLabel) DrawLabel(context, edgeLabel, mark.Position, 120);
        }
    }
    private void DrawGroupLabel(DrawingContext context, string text, Rect group, Rect visible, Rect plot)
    {
        const double inset = 2;
        var width = visible.Width - inset * 2;
        // Do not pin labels for offscreen groups or squeeze a partial line into a tiny
        // visible fragment. Narrow but usable group headers trim with an ellipsis.
        if (string.IsNullOrEmpty(text) || width < FontSize) return;
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight), FontSize, Foreground ?? Brushes.Gray)
        { MaxTextWidth = width, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
        var height = Math.Max(formatted.Height, formatted.Extent);
        if (visible.Height < height + inset * 2 || plot.Height < height + inset * 2) return;
        var x = Math.Clamp(group.Center.X - formatted.Width / 2, visible.Left + inset, visible.Right - inset - formatted.Width);
        var y = Math.Clamp(group.Top - height - inset, plot.Top + inset, plot.Bottom - inset - height);
        context.DrawText(formatted, new(x, y));
    }
}
