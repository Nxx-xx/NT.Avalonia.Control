using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal readonly record struct PictorialBarSymbol(Geometry Geometry, Rect Clip, Rect Target, Geometry Source, Matrix Normalize);

internal sealed partial class PictorialBarRenderer : IChartSeriesRenderer
{
    internal static readonly PictorialBarRenderer Instance = new();
    public bool IncludesZero => false;
    public bool UsesCategorySlots => true;
    public ChartLegendSymbol LegendSymbol => ChartLegendSymbol.Rectangle;
    public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data) =>
        ChartComparisonSeriesRenderer.Bar.GetBounds(series, data);

    public void Render(DrawingContext drawing, ChartSeriesRenderContext context)
    {
        if (context.Series is not PictorialBarSeries series) return;
        var fill = series.Fill ?? context.Brush;
        foreach (var ordinal in ChartComparisonGeometry.VisibleCandidates(context))
            foreach (var symbol in Symbols(context, ordinal))
            {
                using var clip = drawing.PushClip(symbol.Clip);
                drawing.DrawGeometry(fill, null, symbol.Geometry);
            }
    }

    public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance)
    {
        // Geometry holes and repeat gaps remain transparent even when the chart asks for a
        // generous point tolerance. This is a filled silhouette, rather than a point marker.
        if (!context.PlotArea.Contains(position) || context.PrimaryBreakClip is { } clip && !Contains(clip, position)) return null;
        var bestOrdinal = -1;
        foreach (var ordinal in ChartComparisonGeometry.HitCandidates(context, position, 0))
        {
            if (ordinal <= bestOrdinal) continue;
            foreach (var symbol in Symbols(context, ordinal))
                if (symbol.Clip.Contains(position) && symbol.Normalize.TryInvert(out var inverse) &&
                    Contains(symbol.Source, inverse.Transform(position)))
                { bestOrdinal = ordinal; break; }
        }
        return bestOrdinal < 0 ? null : new(context.Data.GetValue(bestOrdinal).SourceIndex, position, 0);
    }

    internal static IEnumerable<PictorialBarSymbol> Symbols(ChartSeriesRenderContext context, int ordinal)
    {
        if (context.Series is not PictorialBarSeries series ||
            !ChartComparisonGeometry.TryCreate(context, ordinal, out var shape) || !shape.HasBody) yield break;
        var clip = shape.Bounds.Intersect(context.PlotArea);
        if (!Valid(clip)) yield break;
        var source = series.Geometry ?? new RectangleGeometry(new Rect(0, 0, 1, 1));
        var sourceBounds = source.Bounds;
        if (!Valid(sourceBounds)) yield break;
        if (series.RenderMode == PictorialBarRenderMode.Stretch)
        {
            yield return Create(source, sourceBounds, shape.Bounds, clip);
            yield break;
        }
        var horizontal = shape.Horizontal;
        var origin = horizontal ? shape.Start.X : shape.Start.Y;
        var endpoint = horizontal ? shape.End.X : shape.End.Y;
        var direction = endpoint >= origin ? 1d : -1d;
        var extent = Math.Abs(endpoint - origin);
        var size = series.SymbolSize;
        var step = size + series.SymbolSpacing;
        var visibleFirst = direction > 0 ? (horizontal ? clip.Left : clip.Top) - origin : origin - (horizontal ? clip.Right : clip.Bottom);
        var visibleLast = direction > 0 ? (horizontal ? clip.Right : clip.Bottom) - origin : origin - (horizontal ? clip.Left : clip.Top);
        var first = series.RenderMode == PictorialBarRenderMode.Clip ? 0 : Math.Max(0, (long)Math.Floor((visibleFirst - size) / step) + 1);
        var last = series.RenderMode == PictorialBarRenderMode.Clip ? 0 : (long)Math.Min(Math.Ceiling(extent / step) - 1, Math.Floor(visibleLast / step));
        for (var index = first; index <= last; index++)
        {
            var a = origin + direction * (index * step);
            var b = a + direction * size;
            var target = horizontal
                ? new Rect(Math.Min(a, b), shape.Bounds.Top, size, shape.Bounds.Height)
                : new Rect(shape.Bounds.Left, Math.Min(a, b), shape.Bounds.Width, size);
            var symbolClip = clip.Intersect(target);
            if (Valid(symbolClip)) yield return Create(source, sourceBounds, target, symbolClip);
        }
    }

    private static PictorialBarSymbol Create(Geometry source, Rect bounds, Rect target, Rect clip)
    {
        var geometry = source.Clone();
        var sx = target.Width / bounds.Width;
        var sy = target.Height / bounds.Height;
        // Bounds already include the source transform. Compose it before normalization;
        // replacing it outright would lose user rotations, translation and nested holes.
        var normalize = new Matrix(sx, 0, 0, sy, target.X - bounds.X * sx, target.Y - bounds.Y * sy);
        geometry.Transform = new MatrixTransform((source.Transform?.Value ?? Matrix.Identity) * normalize);
        return new(geometry, clip, target, source, normalize);
    }

    private static bool Valid(Rect value) => double.IsFinite(value.X) && double.IsFinite(value.Y) &&
        double.IsFinite(value.Width) && double.IsFinite(value.Height) && value.Width > 0 && value.Height > 0;

    internal static bool Contains(Geometry geometry, Point position)
    {
        // Exact primitive tests also work on Avalonia.Headless, whose platform geometry
        // containment is intentionally a stub. Arbitrary paths retain native fill rules.
        var local = position;
        if (geometry.Transform is { } transform)
        {
            if (!transform.Value.TryInvert(out var inverse)) return false;
            local = inverse.Transform(position);
        }
        return geometry switch
        {
            RectangleGeometry rectangle when rectangle.RadiusX == 0 && rectangle.RadiusY == 0 => rectangle.Rect.Contains(local),
            EllipseGeometry ellipse => EllipseContains(Valid(ellipse.Rect) ? ellipse.Rect :
                new Rect(ellipse.Center.X - ellipse.RadiusX, ellipse.Center.Y - ellipse.RadiusY, ellipse.RadiusX * 2, ellipse.RadiusY * 2), local),
            PolylineGeometry polygon => polygon.IsFilled && PolygonContains(polygon.Points.ToArray(), polygon.FillRule, local),
            PathGeometry path when TryPolygonContains(path, local, out var inside) => inside,
            CombinedGeometry combined => combined.GeometryCombineMode switch
            {
                GeometryCombineMode.Union => Child(combined.Geometry1) || Child(combined.Geometry2),
                GeometryCombineMode.Intersect => Child(combined.Geometry1) && Child(combined.Geometry2),
                GeometryCombineMode.Exclude => Child(combined.Geometry1) && !Child(combined.Geometry2),
                GeometryCombineMode.Xor => Child(combined.Geometry1) != Child(combined.Geometry2),
                _ => false,
            },
            GeometryGroup group => group.FillRule == FillRule.EvenOdd
                ? group.Children.Count(child => Contains(child, local)) % 2 != 0
                : geometry.FillContains(position),
            _ => geometry.FillContains(position),
        };
        bool Child(Geometry? child) => child is not null && Contains(child, local);
    }

    private static bool TryPolygonContains(PathGeometry geometry, Point point, out bool inside)
    {
        inside = false;
        var winding = 0;
        if (geometry.Figures is null) return true;
        foreach (var figure in geometry.Figures)
        {
            if (!figure.IsFilled) continue;
            var vertices = new List<Point> { figure.StartPoint };
            if (figure.Segments is not null)
                foreach (var segment in figure.Segments)
                    switch (segment)
                    {
                        case LineSegment line: vertices.Add(line.Point); break;
                        case PolyLineSegment lines: vertices.AddRange(lines.Points); break;
                        default: return false;
                    }
            if (vertices.Count < 3) continue;
            for (var i = 0; i < vertices.Count; i++)
            {
                var a = vertices[i]; var b = vertices[(i + 1) % vertices.Count];
                var side = (b.X - a.X) * (point.Y - a.Y) - (point.X - a.X) * (b.Y - a.Y);
                if (side == 0 && point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) &&
                    point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y)) { inside = true; return true; }
                if (a.Y <= point.Y && b.Y > point.Y && side > 0) winding++;
                else if (a.Y > point.Y && b.Y <= point.Y && side < 0) winding--;
            }
        }
        inside = geometry.FillRule == FillRule.EvenOdd ? (winding & 1) != 0 : winding != 0;
        return true;
    }

    private static bool PolygonContains(IReadOnlyList<Point> vertices, FillRule rule, Point point)
    {
        if (vertices.Count < 3) return false;
        var winding = 0;
        for (var i = 0; i < vertices.Count; i++)
        {
            var a = vertices[i]; var b = vertices[(i + 1) % vertices.Count];
            var side = (b.X - a.X) * (point.Y - a.Y) - (point.X - a.X) * (b.Y - a.Y);
            if (side == 0 && point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) &&
                point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y)) return true;
            if (a.Y <= point.Y && b.Y > point.Y && side > 0) winding++;
            else if (a.Y > point.Y && b.Y <= point.Y && side < 0) winding--;
        }
        return rule == FillRule.EvenOdd ? (winding & 1) != 0 : winding != 0;
    }

    private static bool EllipseContains(Rect rect, Point point)
    {
        if (!Valid(rect)) return false;
        var x = (point.X - rect.Center.X) / (rect.Width / 2);
        var y = (point.Y - rect.Center.Y) / (rect.Height / 2);
        return x * x + y * y <= 1;
    }
}
