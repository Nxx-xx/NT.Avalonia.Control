using Avalonia;
using Avalonia.Layout;

namespace NT.AvaloniaControl.Charts;

/// <summary>One category's geometry, shared by rendering, hit testing and point overlays.</summary>
internal readonly record struct ChartComparisonShape(Rect Bounds, Point Start, Point End, bool Horizontal)
{
    internal bool HasBody => Bounds.Width > 0 && Bounds.Height > 0;
}

internal static class ChartComparisonGeometry
{
    internal static ChartVisibleOrdinals VisibleCandidates(ChartSeriesRenderContext context, bool connectors = false)
    {
        if (context.Series is not IChartComparisonSeries options) return ChartVisibleOrdinals.All(context.Data.Count);
        var horizontal = options.Orientation == Orientation.Horizontal;
        var group = double.IsFinite(context.GroupWidth) ? Math.Abs(context.GroupWidth) : 0;
        var width = options.BarWidth ?? Math.Max(.5, group / Math.Max(1, context.GroupCount) * options.WidthRatio);
        var extent = group / 2 + width / 2 + Math.Max(ChartMarkerGeometry.HitRadius(context.Series), context.Series.StrokeThickness / 2) + 1;
        // Value endpoints can both be outside the plot while a bar or stem spans
        // it. Culling is therefore restricted to the category dimension.
        var points = ChartVisibilityQuery.Points(context, inflation: extent, xOnly: !horizontal, yOnly: horizontal);
        if (!connectors || points.IsFull) return points;
        var edges = ChartVisibilityQuery.Segments(context, inflation: extent, xOnly: !horizontal, yOnly: horizontal);
        return ChartVisibleOrdinals.FromOwned(points.Concat(edges).Distinct().Order().ToArray(), context.Data.Count);
    }
    // Category slots bound bodies, stems and markers even when their value range
    // spans the entire plot. Query the existing immutable-coordinate index rather
    // than projecting every source row for each tooltip move.
    internal static IEnumerable<int> HitCandidates(ChartSeriesRenderContext context, Point position, double tolerance)
    {
        if (context.Series is not IChartComparisonSeries options ||
            context.Projection is not CartesianChart.CartesianProjection projection ||
            (options.Orientation == Orientation.Horizontal ? !projection.HasMonotonicY : !projection.HasMonotonicX))
        {
            for (var ordinal = 0; ordinal < context.Data.Count; ordinal++) yield return ordinal;
            yield break;
        }
        var horizontal = options.Orientation == Orientation.Horizontal;
        var group = double.IsFinite(context.GroupWidth) ? Math.Abs(context.GroupWidth) : 0;
        var width = options.BarWidth ?? Math.Max(.5, group / Math.Max(1, context.GroupCount) * options.WidthRatio);
        var extent = group / 2 + width / 2 + Math.Max(tolerance,
            Math.Max(ChartMarkerGeometry.HitRadius(context.Series), context.Series.StrokeThickness / 2));
        var center = horizontal ? position.Y : position.X;
        if (horizontal)
        {
            var index = ChartReadingIndex.For(context.Data);
            var (start, end, reverse) = index.ScreenRange(context.Projection, center - extent, center + extent);
            for (var cursor = start; cursor < end; cursor++)
                yield return index.Ordinal(reverse ? index.Count - 1 - cursor : cursor);
        }
        else
        {
            var index = ChartDataIndex.For(context.Data);
            var (start, end, reverse) = index.ScreenRange(context.Projection, center - extent, center + extent);
            for (var cursor = start; cursor < end; cursor++)
                yield return index.Ordinal(reverse ? index.Count - 1 - cursor : cursor);
        }
    }

    internal static bool TryCreate(ChartSeriesRenderContext context, int ordinal, out ChartComparisonShape shape)
    {
        shape = default;
        if (context.Series is not IChartComparisonSeries options || ordinal < 0 || ordinal >= context.Data.Count) return false;
        var value = context.Data.GetValue(ordinal);
        if (value.IsEmpty || !Valid(value.Point)) return false;
        var horizontal = options.Orientation == Orientation.Horizontal;
        var screen = context.Projection.Project(value.Point);
        ChartVisibilityQuery.RecordProjected(1);
        if (!Valid(screen)) return false;
        var baseline = context.Data.GetBandLower(ordinal) ?? options.Baseline;
        if (context.Data.GetBandLower(ordinal) is null && context.Series is RangeBarSeries or VarianceSeries)
            baseline = context.Data.Points[ordinal].Low ?? double.NaN;
        var start = horizontal ? context.Projection.MapX(baseline) : context.Projection.MapY(baseline);
        if (context.Series is DotSeries && !double.IsFinite(start)) start = horizontal ? screen.X : screen.Y;
        // A zero baseline may lie outside a logarithmic domain. A range endpoint
        // has its own numeric meaning and must not be silently replaced instead.
        if (!double.IsFinite(start) && baseline == 0 && context.Series is not RangeBarSeries and not VarianceSeries)
            start = Origin(context.Projection, horizontal);
        if (!double.IsFinite(start)) return false;
        var groupWidth = double.IsFinite(context.GroupWidth) ? Math.Abs(context.GroupWidth) : 0;
        var slot = groupWidth / Math.Max(1, context.GroupCount);
        var width = options.BarWidth ?? Math.Max(.5, slot * options.WidthRatio);
        var center = (horizontal ? screen.Y : screen.X) - groupWidth / 2 + context.GroupIndex * slot + slot / 2;
        if (!double.IsFinite(center) || !double.IsFinite(width)) return false;
        var end = horizontal ? screen.X : screen.Y;
        var a = horizontal ? new Point(start, center) : new Point(center, start);
        var b = horizontal ? new Point(end, center) : new Point(center, end);
        var rectangle = horizontal ? new Rect(Math.Min(start, end), center - width / 2, Math.Abs(end - start), width)
            : new Rect(center - width / 2, Math.Min(start, end), width, Math.Abs(end - start));
        if (!double.IsFinite(rectangle.X) || !double.IsFinite(rectangle.Y) ||
            !double.IsFinite(rectangle.Width) || !double.IsFinite(rectangle.Height)) return false;
        shape = new(rectangle, a, b, horizontal);
        return true;
    }

    internal static double Category(ChartDataValue value, bool horizontal) => horizontal ? value.Point.Y : value.Point.X;
    internal static bool Valid(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static bool Valid(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static double Origin(IChartProjection projection, bool horizontal)
    {
        if (!horizontal) return projection.YOrigin;
        var first = projection.Unproject(projection.PlotArea.BottomLeft).X;
        var last = projection.Unproject(projection.PlotArea.BottomRight).X;
        return projection.MapX(Math.Min(first, last));
    }

    internal static double RectangleDistanceSquared(Rect rectangle, Point position)
    {
        var dx = Math.Max(rectangle.Left - position.X, Math.Max(0, position.X - rectangle.Right));
        var dy = Math.Max(rectangle.Top - position.Y, Math.Max(0, position.Y - rectangle.Bottom));
        return dx * dx + dy * dy;
    }

    internal static double SegmentDistanceSquared(Point point, Point a, Point b, out Point nearest)
    {
        var delta = b - a;
        var lengthSquared = delta.X * delta.X + delta.Y * delta.Y;
        var fraction = lengthSquared > 0 ? Math.Clamp(((point.X - a.X) * delta.X + (point.Y - a.Y) * delta.Y) / lengthSquared, 0, 1) : 0;
        nearest = a + delta * fraction;
        var offset = nearest - point;
        return offset.X * offset.X + offset.Y * offset.Y;
    }

    internal static bool ClipSegment(Rect plot, ref Point a, ref Point b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var first = 0d; var last = 1d;
        if (!Clip(-dx, a.X - plot.Left, ref first, ref last) || !Clip(dx, plot.Right - a.X, ref first, ref last) ||
            !Clip(-dy, a.Y - plot.Top, ref first, ref last) || !Clip(dy, plot.Bottom - a.Y, ref first, ref last)) return false;
        var origin = a;
        a = new(origin.X + first * dx, origin.Y + first * dy);
        b = new(origin.X + last * dx, origin.Y + last * dy);
        return true;
    }

    private static bool Clip(double p, double q, ref double first, ref double last)
    {
        if (p == 0) return q >= 0;
        var ratio = q / p;
        if (p < 0) { if (ratio > last) return false; first = Math.Max(first, ratio); }
        else { if (ratio < first) return false; last = Math.Min(last, ratio); }
        return true;
    }
}
