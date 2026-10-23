using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>One annular path writer is used by the native renderer and command-recording tests.</summary>
internal static class GaugeGeometry
{
    internal static double NormalizeAngle(double angle) => angle % 360;
    internal static Point PointAt(Point center, double angle, double radius)
    {
        var radians = NormalizeAngle(angle) * Math.PI / 180;
        return center + new Vector(Math.Cos(radians) * radius, Math.Sin(radians) * radius);
    }
    internal static Geometry Annulus(Point center, double outer, double inner, double start, double sweep)
    {
        var geometry = new StreamGeometry(); using var path = geometry.Open();
        WriteAnnulus(new NativeSink(path), center, outer, inner, start, sweep); return geometry;
    }
    internal static void WriteAnnulus(ICircularGeometrySink path, Point center, double outer, double inner, double start, double sweep)
    {
        if (outer <= 0 || inner >= outer || sweep == 0 || !double.IsFinite(outer) || !double.IsFinite(inner) || !double.IsFinite(start) || !double.IsFinite(sweep)) return;
        inner = Math.Max(0, inner); sweep = Math.Clamp(sweep, -360, 360); start = NormalizeAngle(start);
        path.SetFillRule(FillRule.NonZero);
        path.BeginFigure(PointAt(center, start, outer), true); Arc(outer, start, sweep);
        if (Math.Abs(sweep) == 360)
        {
            path.EndFigure(true);
            if (inner > 0) { path.BeginFigure(PointAt(center, start, inner), true); Arc(inner, start, -sweep); path.EndFigure(true); }
        }
        else
        {
            if (inner > 0) { path.LineTo(PointAt(center, start + sweep, inner)); Arc(inner, start + sweep, -sweep); }
            else path.LineTo(center);
            path.EndFigure(true);
        }
        void Arc(double radius, double angle, double arc)
        {
            var direction = arc > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
            if (Math.Abs(arc) == 360)
            {
                path.ArcTo(PointAt(center, angle + arc / 2, radius), new Size(radius, radius), 0, false, direction);
                path.ArcTo(PointAt(center, angle + arc, radius), new Size(radius, radius), 0, false, direction);
            }
            else path.ArcTo(PointAt(center, angle + arc, radius), new Size(radius, radius), 0, Math.Abs(arc) > 180, direction);
        }
    }
    private sealed class NativeSink(StreamGeometryContext context) : ICircularGeometrySink
    {
        public void SetFillRule(FillRule rule) => context.SetFillRule(rule);
        public void BeginFigure(Point start, bool isFilled) => context.BeginFigure(start, isFilled);
        public void ArcTo(Point end, Size radii, double rotation, bool largeArc, SweepDirection direction) => context.ArcTo(end, radii, rotation, largeArc, direction);
        public void LineTo(Point end) => context.LineTo(end);
        public void EndFigure(bool close) => context.EndFigure(close);
    }
}
