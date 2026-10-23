using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal enum ChartStatisticalKind { Box, Error, Histogram, Density }

internal sealed class ChartStatisticalSeriesRenderer(ChartStatisticalKind kind) : IChartSeriesRenderer
{
    internal static readonly ChartStatisticalSeriesRenderer Box = new(ChartStatisticalKind.Box);
    internal static readonly ChartStatisticalSeriesRenderer Error = new(ChartStatisticalKind.Error);
    internal static readonly ChartStatisticalSeriesRenderer Histogram = new(ChartStatisticalKind.Histogram);
    internal static readonly ChartStatisticalSeriesRenderer Density = new(ChartStatisticalKind.Density);
    private readonly ConditionalWeakTable<ChartSeriesRenderContext, Layout> _layouts = new();
    private sealed record Glyph(int Ordinal, Rect? Body, Point Anchor, Segment[] Lines, Point[] Marks);
    private readonly record struct Segment(Point Start, Point End);
    private sealed record Layout(Glyph[] Glyphs);
    public bool IncludesZero => kind is ChartStatisticalKind.Histogram or ChartStatisticalKind.Density;
    public bool UsesCategorySlots => kind is ChartStatisticalKind.Box or ChartStatisticalKind.Error;
    public ChartLegendSymbol LegendSymbol => kind is ChartStatisticalKind.Box or ChartStatisticalKind.Histogram ? ChartLegendSymbol.Rectangle : ChartLegendSymbol.Line;

    public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data)
    {
        ChartSeriesBounds? bounds = null;
        var horizontal = series is StatisticalSeries { Orientation: Orientation.Horizontal };
        foreach (var item in data.Points)
        {
            if (item.IsEmpty || !Valid(item.Point)) continue;
            var x = item.Point.X; var y = item.Point.Y;
            double minimum = horizontal ? x : y, maximum = minimum;
            if (kind == ChartStatisticalKind.Box && item.BoxPlot is { } summary)
            {
                var shift = minimum - summary.Median;
                minimum = summary.Minimum + shift; maximum = summary.Maximum + shift;
            }
            else if (kind == ChartStatisticalKind.Error)
            {
                var rawCenter = (item.RawPoint ?? (horizontal ? new(item.Point.Y, item.Point.X) : item.Point)).Y;
                var shift = minimum - rawCenter;
                minimum = (item.Low ?? minimum) + shift; maximum = (item.High ?? maximum) + shift;
            }
            if (kind == ChartStatisticalKind.Histogram)
            {
                if (item.Low is not { } lower || item.High is not { } upper) continue;
                Include(horizontal ? new(0, y) : new(x, 0));
                Include(horizontal ? new(x, lower) : new(lower, y));
                Include(horizontal ? new(x, upper) : new(upper, y));
            }
            else
            {
                Include(horizontal ? new(minimum, y) : new(x, minimum));
                Include(horizontal ? new(maximum, y) : new(x, maximum));
                if (kind == ChartStatisticalKind.Density) Include(new(x, 0));
            }
        }
        return bounds;

        void Include(ChartPoint value)
        {
            if (!Valid(value)) return;
            bounds = bounds is { } previous ? new(Math.Min(previous.XMinimum, value.X), Math.Max(previous.XMaximum, value.X),
                Math.Min(previous.YMinimum, value.Y), Math.Max(previous.YMaximum, value.Y)) : new(value.X, value.X, value.Y, value.Y);
        }
    }

    public void Render(DrawingContext drawing, ChartSeriesRenderContext context)
    {
        if (kind == ChartStatisticalKind.Density) { RenderDensity(drawing, context); return; }
        var series = context.Series;
        var pen = series.StrokeThickness > 0 ? new Pen(series.Stroke ?? context.Brush, series.StrokeThickness,
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round) : null;
        var fill = series.Fill ?? context.Brush;
        var markerRadius = Math.Max(2, series.MarkerSize / 2);
        foreach (var glyph in For(context).Glyphs)
        {
            if (glyph.Body is { } body) drawing.DrawGeometry(kind == ChartStatisticalKind.Box ? series.Fill : fill, pen, new RectangleGeometry(body));
            if (pen is not null && glyph.Lines.Length > 0)
            {
                var geometry = new StreamGeometry();
                using (var path = geometry.Open())
                    foreach (var line in glyph.Lines) { path.BeginFigure(line.Start, false); path.LineTo(line.End); path.EndFigure(false); }
                drawing.DrawGeometry(null, pen, geometry);
            }
            foreach (var marker in glyph.Marks)
                drawing.DrawGeometry(series.MarkerFill ?? fill, pen, new EllipseGeometry(new Rect(marker.X - markerRadius, marker.Y - markerRadius, markerRadius * 2, markerRadius * 2)));
        }
    }

    internal ChartRetainedSeriesDrawing CreateRetainedDrawing(ChartSeriesRenderContext context)
    {
        var group = new DrawingGroup();
        using (var recording = group.Open()) Render(recording, context);
        var bytes = kind == ChartStatisticalKind.Density ? 512L + context.Data.Count * 64L :
            For(context).Glyphs.Sum(glyph => 256L + glyph.Lines.LongLength * 64 + glyph.Marks.LongLength * 128);
        return new ChartRetainedSeriesDrawing.Recorded(group, bytes);
    }

    public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance < 0) return null;
        if (kind == ChartStatisticalKind.Density) return HitDensity(context, position, tolerance);
        var best = double.PositiveInfinity; var ordinal = -1; var anchor = default(Point);
        var lineTolerance = tolerance + context.Series.StrokeThickness / 2;
        foreach (var glyph in For(context).Glyphs)
        {
            var distance = double.PositiveInfinity; var closest = glyph.Anchor;
            if (glyph.Body is { } body)
            {
                body = body.Intersect(context.PlotArea);
                if (body.Width > 0 && body.Height > 0)
                {
                    closest = new(Math.Clamp(position.X, body.Left, body.Right), Math.Clamp(position.Y, body.Top, body.Bottom));
                    distance = Squared(position - closest);
                    if (distance > tolerance * tolerance) distance = double.PositiveInfinity;
                }
            }
            if (context.Series.StrokeThickness > 0)
                foreach (var line in glyph.Lines)
                {
                    if (!ClipSegment(context.PlotArea, line.Start, line.End, out var first, out var last)) continue;
                    var candidate = Closest(position, first, last);
                    var squared = Squared(position - candidate);
                    if (context.PlotArea.Contains(candidate) && squared <= lineTolerance * lineTolerance && squared < distance)
                    { distance = squared; closest = candidate; }
                }
            foreach (var marker in glyph.Marks)
            {
                var radius = Math.Max(2, context.Series.MarkerSize / 2) + context.Series.StrokeThickness / 2;
                var squared = Squared(position - marker);
                if (context.PlotArea.Contains(marker) && squared <= (tolerance + radius) * (tolerance + radius) && squared < distance)
                { distance = squared; closest = marker; }
            }
            if (distance < best || double.IsFinite(distance) && distance == best && glyph.Ordinal > ordinal)
            { best = distance; ordinal = glyph.Ordinal; anchor = closest; }
        }
        return ordinal < 0 ? null : new(context.Data.Points[ordinal].SourceIndex, anchor, best);
    }

    internal bool TryPlacement(ChartSeriesRenderContext context, int ordinal, out Point anchor, out Rect body)
    {
        if (kind == ChartStatisticalKind.Density)
        {
            anchor = context.Projection.Project(context.Data.Points[ordinal].Point); body = default;
            return Valid(anchor);
        }
        foreach (var glyph in For(context).Glyphs)
            if (glyph.Ordinal == ordinal) { anchor = glyph.Anchor; body = glyph.Body ?? default; return true; }
        anchor = default; body = default; return false;
    }

    private Layout For(ChartSeriesRenderContext context) => _layouts.GetValue(context, Build);

    private Layout Build(ChartSeriesRenderContext context)
    {
        var glyphs = new List<Glyph>();
        var horizontal = context.Series is StatisticalSeries { Orientation: Orientation.Horizontal };
        var count = Math.Max(1, context.GroupCount);
        var slot = context.GroupWidth / count;
        var offset = UsesCategorySlots ? -context.GroupWidth / 2 + slot * (context.GroupIndex + .5) : 0;
        var options = (StatisticalSeries)context.Series;
        var width = options.BarWidth ?? Math.Max(.5, slot * options.WidthRatio);
        var halfWidth = Math.Min(100_000, width) / 2;
        for (var ordinal = 0; ordinal < context.Data.Count; ordinal++)
        {
            var item = context.Data.Points[ordinal];
            if (item.IsEmpty || !Valid(item.Point)) continue;
            var category = horizontal ? item.Point.Y : item.Point.X;
            var center = horizontal ? item.Point.X : item.Point.Y;
            var lines = new List<Segment>(); var marks = new List<Point>(); Rect? body = null;
            Point Map(double value) => horizontal ? new(context.Projection.MapX(value), context.Projection.MapY(category) + offset)
                : new(context.Projection.MapX(category) + offset, context.Projection.MapY(value));
            void Cross(double value, double half)
            {
                var point = Map(value);
                var delta = horizontal ? new Vector(0, half) : new Vector(half, 0);
                if (Valid(point)) lines.Add(new(point - delta, point + delta));
            }
            void Stem(double a, double b) { var first = Map(a); var last = Map(b); if (Valid(first) && Valid(last)) lines.Add(new(first, last)); }
            if (kind == ChartStatisticalKind.Box && item.BoxPlot is { } summary)
            {
                var shift = center - summary.Median;
                var lower = Map(summary.LowerQuartile + shift); var upper = Map(summary.UpperQuartile + shift);
                if (Valid(lower) && Valid(upper)) body = horizontal ?
                    new Rect(Math.Min(lower.X, upper.X), lower.Y - halfWidth, Math.Max(.75, Math.Abs(upper.X - lower.X)), halfWidth * 2) :
                    new Rect(lower.X - halfWidth, Math.Min(lower.Y, upper.Y), halfWidth * 2, Math.Max(.75, Math.Abs(upper.Y - lower.Y)));
                Stem(summary.LowerWhisker + shift, summary.LowerQuartile + shift);
                Stem(summary.UpperQuartile + shift, summary.UpperWhisker + shift);
                Cross(summary.LowerWhisker + shift, halfWidth * .55); Cross(summary.UpperWhisker + shift, halfWidth * .55);
                Cross(center, halfWidth);
                if (context.Series is BoxPlotSeries box)
                {
                    if (box.ShowOutliers) foreach (var outlier in summary.Outliers) { var marker = Map(outlier.Value + shift); if (Valid(marker)) marks.Add(marker); }
                    if (box.ShowMean) { var marker = Map(summary.Mean + shift); if (Valid(marker)) marks.Add(marker); }
                }
            }
            else if (kind == ChartStatisticalKind.Error && item.Low is { } low && item.High is { } high)
            {
                var rawCenter = (item.RawPoint ?? (horizontal ? new(item.Point.Y, item.Point.X) : item.Point)).Y;
                var shift = center - rawCenter;
                Stem(low + shift, high + shift);
                var cap = ((ErrorBarSeries)context.Series).CapSize / 2;
                Cross(low + shift, cap); Cross(high + shift, cap);
                if (context.Series.ShowMarkers && context.Series.MarkerSize > 0) { var marker = Map(center); if (Valid(marker)) marks.Add(marker); }
            }
            else if (kind == ChartStatisticalKind.Histogram && item.Low is { } binLow && item.High is { } binHigh)
            {
                var first = horizontal ? context.Projection.MapY(binLow) : context.Projection.MapX(binLow);
                var last = horizontal ? context.Projection.MapY(binHigh) : context.Projection.MapX(binHigh);
                var baseline = context.Projection.Project(horizontal ? new(0, category) : new(category, 0));
                var top = Map(center);
                if (!Valid(baseline)) baseline = horizontal ? new(context.PlotArea.Left, top.Y) : new(top.X, context.Projection.YOrigin);
                if (double.IsFinite(first) && double.IsFinite(last) && Valid(top) && Valid(baseline))
                {
                    var gap = Math.Abs(last - first) * ((HistogramSeries)context.Series).BinGapRatio;
                    body = horizontal ? new(Math.Min(top.X, baseline.X), Math.Min(first, last) + gap / 2, Math.Max(.75, Math.Abs(top.X - baseline.X)), Math.Max(.5, Math.Abs(last - first) - gap)) :
                        new(Math.Min(first, last) + gap / 2, Math.Min(top.Y, baseline.Y), Math.Max(.5, Math.Abs(last - first) - gap), Math.Max(.75, Math.Abs(top.Y - baseline.Y)));
                }
            }
            var anchor = Map(center);
            if (Valid(anchor) && (body is not null || lines.Count > 0 || marks.Count > 0)) glyphs.Add(new(ordinal, body, anchor, lines.ToArray(), marks.ToArray()));
        }
        return new(glyphs.ToArray());
    }

    private static void RenderDensity(DrawingContext drawing, ChartSeriesRenderContext context)
    {
        var series = (DensitySeries)context.Series;
        var geometry = new StreamGeometry();
        var fill = new StreamGeometry();
        using (var linePath = geometry.Open())
        using (var fillPath = fill.Open())
        {
            var connected = false; var first = default(Point); var last = default(Point);
            var origin = context.Projection.YOrigin;
            void Finish()
            {
                if (!connected) return;
                linePath.EndFigure(false);
                if (double.IsFinite(origin)) { fillPath.LineTo(new(last.X, origin)); fillPath.LineTo(new(first.X, origin)); fillPath.EndFigure(true); }
                connected = false;
            }
            foreach (var item in context.Data.Points)
            {
                var screen = context.Projection.Project(item.Point);
                if (item.IsEmpty || !Valid(item.Point) || !Valid(screen)) { Finish(); continue; }
                if (!connected)
                {
                    first = screen; linePath.BeginFigure(screen, false);
                    if (double.IsFinite(origin)) fillPath.BeginFigure(screen, true);
                    connected = true;
                }
                else { linePath.LineTo(screen); if (double.IsFinite(origin)) fillPath.LineTo(screen); }
                last = screen;
            }
            Finish();
        }
        if (series.FillUnderCurve) drawing.DrawGeometry(series.Fill ?? context.Brush, null, fill);
        if (series.StrokeThickness > 0) drawing.DrawGeometry(null, new Pen(series.Stroke ?? context.Brush, series.StrokeThickness), geometry);
    }

    private static ChartSeriesHit? HitDensity(ChartSeriesRenderContext context, Point position, double tolerance)
    {
        var best = double.PositiveInfinity; var ordinal = -1; var anchor = default(Point);
        Point? previous = null; var previousOrdinal = -1;
        for (var index = 0; index < context.Data.Count; index++)
        {
            var item = context.Data.Points[index]; var screen = context.Projection.Project(item.Point);
            if (item.IsEmpty || !Valid(item.Point) || !Valid(screen)) { previous = null; continue; }
            var closest = screen;
            if (previous is { } before && context.Series.StrokeThickness > 0 &&
                ClipSegment(context.PlotArea, before, screen, out var first, out var last)) closest = Closest(position, first, last);
            var distance = Squared(position - closest);
            var threshold = tolerance + context.Series.StrokeThickness / 2;
            if (context.PlotArea.Contains(closest) && distance <= threshold * threshold && distance <= best)
            {
                var nearer = previous is { } old && Squared(position - old) < Squared(position - screen) ? previousOrdinal : index;
                ordinal = nearer; anchor = closest; best = distance;
            }
            previous = screen; previousOrdinal = index;
        }
        return ordinal < 0 ? null : new(context.Data.Points[ordinal].SourceIndex, anchor, best);
    }

    private static Point Closest(Point point, Point a, Point b)
    {
        var delta = b - a; var length = Squared(delta);
        if (!(length > 0) || !double.IsFinite(length)) return a;
        var ratio = Math.Clamp(((point.X - a.X) * delta.X + (point.Y - a.Y) * delta.Y) / length, 0, 1);
        return a + delta * ratio;
    }

    // Clip before squaring distances. Third-party projections may expose finite
    // coordinates much larger than the built-in projection's saturation bound.
    private static bool ClipSegment(Rect plot, Point a, Point b, out Point first, out Point last)
    {
        first = a; last = b;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var codeA = Code(first); var codeB = Code(last);
            if ((codeA | codeB) == 0) return true;
            if ((codeA & codeB) != 0) return false;
            var code = codeA != 0 ? codeA : codeB;
            Point clipped;
            if ((code & 8) != 0) clipped = new(At(first.X, last.X, first.Y, last.Y, plot.Bottom), plot.Bottom);
            else if ((code & 4) != 0) clipped = new(At(first.X, last.X, first.Y, last.Y, plot.Top), plot.Top);
            else if ((code & 2) != 0) clipped = new(plot.Right, At(first.Y, last.Y, first.X, last.X, plot.Right));
            else clipped = new(plot.Left, At(first.Y, last.Y, first.X, last.X, plot.Left));
            if (!Valid(clipped)) return false;
            if (code == codeA) first = clipped; else last = clipped;
        }
        return false;

        int Code(Point point) => (point.X < plot.Left ? 1 : point.X > plot.Right ? 2 : 0) |
            (point.Y < plot.Top ? 4 : point.Y > plot.Bottom ? 8 : 0);
        static double At(double a, double b, double axisA, double axisB, double target)
        {
            var scale = Math.Max(Math.Abs(axisA), Math.Max(Math.Abs(axisB), Math.Abs(target)));
            var ratio = scale == 0 ? 0 : (target / scale - axisA / scale) / (axisB / scale - axisA / scale);
            return a * (1 - ratio) + b * ratio;
        }
    }
    private static double Squared(Vector delta) => delta.X * delta.X + delta.Y * delta.Y;
    private static bool Valid(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static bool Valid(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    internal static IEnumerable<double> AxisValues(ChartSeries series, ChartDataSnapshot data, bool horizontal)
    {
        var sideways = series is StatisticalSeries { Orientation: Orientation.Horizontal };
        foreach (var point in data.Points)
        {
            if (point.IsEmpty) continue;
            if (series is HistogramSeries && horizontal != sideways)
            {
                if (point.Low is { } low) yield return low;
                if (point.High is { } high) yield return high;
            }
            else if (horizontal == sideways)
            {
                if (point.BoxPlot is { } box)
                {
                    yield return box.Minimum; yield return box.LowerWhisker; yield return box.LowerQuartile;
                    yield return box.Median; yield return box.UpperQuartile; yield return box.UpperWhisker; yield return box.Maximum;
                    foreach (var outlier in box.Outliers) yield return outlier.Value;
                }
                else if (series is ErrorBarSeries)
                {
                    if (point.Low is { } low) yield return low;
                    if (point.High is { } high) yield return high;
                }
            }
        }
    }
}
