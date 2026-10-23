using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal sealed class ChartFinancialSeriesRenderer : IChartSeriesRenderer
{
    internal static readonly ChartFinancialSeriesRenderer Instance = new();
    private readonly ConditionalWeakTable<ChartSeriesRenderContext, Layout> _layouts = new();
    private readonly record struct Segment(Point Start, Point End);
    private sealed record Glyph(int Ordinal, Rect? Body, Point Anchor, Segment[] Lines, Rect[] Circles, IBrush Brush, double Thickness);
    private sealed record Layout(Glyph[] Glyphs);
    public bool IncludesZero => false;
    public bool UsesCategorySlots => false;
    public ChartLegendSymbol LegendSymbol => ChartLegendSymbol.Rectangle;

    public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data)
    {
        ChartSeriesBounds? result = null;
        foreach (var item in data.Points)
        {
            if (item.IsEmpty || item.Financial is not { } sample) continue;
            var lower = series is FinancialVolumeSeries ? 0 : sample.Low;
            var upper = series is FinancialVolumeSeries ? item.Point.Y : sample.High;
            if (series is not FinancialVolumeSeries && sample.Kind == ChartFinancialTransformKind.PointAndFigure && sample.BoxSize is { } box)
            { lower -= box / 2; upper += box / 2; }
            if (!double.IsFinite(lower) || !double.IsFinite(upper)) continue;
            var bounds = new ChartSeriesBounds(item.Point.X - .5, item.Point.X + .5, lower, upper);
            result = result is { } old ? new(Math.Min(old.XMinimum, bounds.XMinimum), Math.Max(old.XMaximum, bounds.XMaximum),
                Math.Min(old.YMinimum, lower), Math.Max(old.YMaximum, upper)) : bounds;
        }
        return result;
    }

    public void Render(DrawingContext drawing, ChartSeriesRenderContext context)
    {
        foreach (var glyph in For(context).Glyphs)
        {
            var pen = new Pen(context.Series.Stroke ?? glyph.Brush, glyph.Thickness, lineCap: PenLineCap.Flat);
            if (glyph.Body is { } body) drawing.DrawGeometry(context.Series.Fill ?? glyph.Brush, glyph.Thickness > 0 ? pen : null, new RectangleGeometry(body));
            if (glyph.Thickness > 0)
            {
                foreach (var line in glyph.Lines) drawing.DrawLine(pen, line.Start, line.End);
                foreach (var circle in glyph.Circles) drawing.DrawGeometry(null, pen, new EllipseGeometry(circle));
            }
        }
    }

    internal ChartRetainedSeriesDrawing CreateRetainedDrawing(ChartSeriesRenderContext context)
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open()) Render(drawing, context);
        return new ChartRetainedSeriesDrawing.Recorded(group, 512 + For(context).Glyphs.Sum(glyph => 256L + glyph.Lines.LongLength * 64 + glyph.Circles.LongLength * 128));
    }

    public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance < 0 || !context.PlotArea.Contains(position)) return null;
        var best = double.PositiveInfinity; Glyph? winner = null; var anchor = default(Point);
        foreach (var glyph in For(context).Glyphs)
        {
            var distance = double.PositiveInfinity; var closest = glyph.Anchor;
            if (glyph.Body is { } body)
            {
                body = body.Intersect(context.PlotArea);
                if (body.Width > 0 && body.Height > 0)
                {
                    closest = new(Math.Clamp(position.X, body.Left, body.Right), Math.Clamp(position.Y, body.Top, body.Bottom));
                    distance = Square(position - closest);
                    if (distance > tolerance * tolerance) distance = double.PositiveInfinity;
                }
            }
            if (glyph.Thickness > 0)
            {
                var limit = tolerance + glyph.Thickness / 2;
                foreach (var line in glyph.Lines)
                {
                    var candidate = Closest(position, line.Start, line.End);
                    var squared = Square(position - candidate);
                    if (context.PlotArea.Contains(candidate) && squared <= limit * limit && squared < distance) { distance = squared; closest = candidate; }
                }
                foreach (var circle in glyph.Circles)
                {
                    var dx = position.X - circle.Center.X; var dy = position.Y - circle.Center.Y;
                    var theta = Math.Atan2(dy / Math.Max(.001, circle.Height / 2), dx / Math.Max(.001, circle.Width / 2));
                    var candidate = circle.Center + new Vector(Math.Cos(theta) * circle.Width / 2, Math.Sin(theta) * circle.Height / 2);
                    var squared = Square(position - candidate);
                    if (context.PlotArea.Contains(candidate) && squared <= limit * limit && squared < distance) { distance = squared; closest = candidate; }
                }
            }
            if (distance < best || double.IsFinite(distance) && distance == best && glyph.Ordinal > (winner?.Ordinal ?? -1))
            { best = distance; winner = glyph; anchor = closest; }
        }
        return winner is null ? null : new(context.Data.Points[winner.Ordinal].SourceIndex, anchor, best);
    }

    internal bool TryPlacement(ChartSeriesRenderContext context, int ordinal, out Point anchor, out Rect body)
    {
        foreach (var glyph in For(context).Glyphs)
            if (glyph.Ordinal == ordinal) { anchor = glyph.Anchor; body = glyph.Body ?? default; return true; }
        anchor = default; body = default; return false;
    }
    private Layout For(ChartSeriesRenderContext context) => _layouts.GetValue(context, Build);
    private static Layout Build(ChartSeriesRenderContext context)
    {
        var options = (FinancialSeries)context.Series;
        var glyphs = new List<Glyph>();
        ChartFinancialSample? previous = null;
        var previousX = double.NaN;
        for (var ordinal = 0; ordinal < context.Data.Count; ordinal++)
        {
            var item = context.Data.Points[ordinal];
            if (item.IsEmpty || item.Financial is not { } sample) { previous = null; continue; }
            var x = context.Projection.MapX(item.Point.X);
            var slotWidth = Math.Abs(context.Projection.MapX(item.Point.X + .5) - context.Projection.MapX(item.Point.X - .5));
            var half = Math.Min(100000, Math.Max(.75, slotWidth * options.WidthRatio)) / 2;
            var lines = new List<Segment>(); var circles = new List<Rect>(); Rect? body = null;
            Point Map(double value) => new(x, context.Projection.MapY(value));
            void Add(Point first, Point last) { if (Valid(first) && Valid(last)) lines.Add(new(first, last)); }
            var brush = sample.Direction > 0 ? options.RisingBrush : sample.Direction < 0 ? options.FallingBrush : options.NeutralBrush;
            brush ??= context.Brush;
            var thickness = options.StrokeThickness;
            var anchor = Map(item.Point.Y);
            if (!Valid(anchor) || !double.IsFinite(half)) { previous = null; continue; }
            if (options is FinancialVolumeSeries)
            {
                var baseline = context.Projection.YOrigin;
                if (!double.IsFinite(baseline)) baseline = context.PlotArea.Bottom;
                body = new(x - half, Math.Min(baseline, anchor.Y), half * 2, Math.Max(.75, Math.Abs(baseline - anchor.Y)));
            }
            else
            {
                var style = sample.Kind switch { ChartFinancialTransformKind.Renko => ChartFinancialStyle.Renko,
                    ChartFinancialTransformKind.Kagi => ChartFinancialStyle.Kagi, ChartFinancialTransformKind.PointAndFigure => ChartFinancialStyle.PointAndFigure, _ => options.Style };
                var open = Map(sample.Open); var close = Map(sample.Close); var high = Map(sample.High); var low = Map(sample.Low);
                if (style == ChartFinancialStyle.Kagi)
                {
                    thickness *= sample.IsThick ? 2.5 : 1;
                    Add(open, close);
                    if (!sample.IsSegmentStart && previous is { } last)
                        Add(new(previousX, context.Projection.MapY(last.Close)), open);
                }
                else if (style == ChartFinancialStyle.PointAndFigure)
                {
                    var box = sample.BoxSize ?? options.BoxSize;
                    for (var index = 0; index < sample.BoxCount; index++)
                    {
                        var center = sample.Open + sample.Direction * (index + 1) * box;
                        var a = Map(center - box / 2);
                        var b = Map(center + box / 2);
                        if (!Valid(a) || !Valid(b)) continue;
                        var rect = new Rect(x - half, Math.Min(a.Y, b.Y), half * 2, Math.Max(.75, Math.Abs(a.Y - b.Y)));
                        if (sample.Direction > 0) { Add(rect.TopLeft, rect.BottomRight); Add(rect.TopRight, rect.BottomLeft); }
                        else circles.Add(rect);
                    }
                }
                else if (style == ChartFinancialStyle.Ohlc)
                {
                    Add(high, low); Add(open - new Vector(half, 0), open); Add(close, close + new Vector(half, 0));
                }
                else if (style == ChartFinancialStyle.HighLow) Add(high, low);
                else
                {
                    if (style != ChartFinancialStyle.Renko) Add(high, low);
                    if (Valid(open) && Valid(close))
                        body = new(x - half, Math.Min(open.Y, close.Y) - (open.Y == close.Y ? .5 : 0), half * 2, Math.Max(1, Math.Abs(open.Y - close.Y)));
                }
            }
            glyphs.Add(new(ordinal, body, anchor, lines.ToArray(), circles.ToArray(), brush, thickness));
            previous = sample;
            previousX = x;
        }
        return new(glyphs.ToArray());
    }
    private static bool Valid(Point value) => double.IsFinite(value.X) && double.IsFinite(value.Y);
    private static double Square(Vector value) => value.X * value.X + value.Y * value.Y;
    private static Point Closest(Point point, Point first, Point last)
    {
        var delta = last - first; var length = Square(delta);
        var relative = point - first;
        var fraction = length > 0 ? Math.Clamp((relative.X * delta.X + relative.Y * delta.Y) / length, 0, 1) : 0;
        return first + delta * fraction;
    }
    internal static IEnumerable<double> AxisValues(ChartSeries series, ChartDataSnapshot data, bool horizontal)
    {
        if (horizontal || series is FinancialVolumeSeries) yield break;
        foreach (var item in data.Points)
            if (!item.IsEmpty && item.Financial is { } sample)
            { yield return sample.Low; yield return sample.Open; yield return sample.Close; yield return sample.High; }
    }
}
