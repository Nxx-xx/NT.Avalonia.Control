using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Conservative view queries over original immutable source ordinals.</summary>
internal static class ChartVisibilityQuery
{
    private static long _pointQueryCount, _segmentQueryCount, _fallbackCount, _projectedCount;
    internal static long PointQueryCount => Interlocked.Read(ref _pointQueryCount);
    internal static long SegmentQueryCount => Interlocked.Read(ref _segmentQueryCount);
    internal static long FallbackCount => Interlocked.Read(ref _fallbackCount);
    internal static long ProjectedCount => Interlocked.Read(ref _projectedCount);
    internal static void RecordProjected(long count) => Interlocked.Add(ref _projectedCount, count);

    internal static ChartVisibleOrdinals Points(ChartSeriesRenderContext context, Rect? screenArea = null,
        double inflation = 0, bool xOnly = false, bool yOnly = false)
    {
        Interlocked.Increment(ref _pointQueryCount);
        return TryBounds(context, screenArea ?? context.PlotArea, inflation, xOnly, yOnly, out var bounds)
            ? ChartVisibilityIndex.For(context.Data).QueryPoints(bounds)
            : Full(context);
    }

    internal static ChartVisibleOrdinals Segments(ChartSeriesRenderContext context, double inflation = 0,
        int neighbors = 0, bool xOnly = false, bool yOnly = false)
    {
        Interlocked.Increment(ref _segmentQueryCount);
        return TryBounds(context, context.PlotArea, inflation, xOnly, yOnly, out var bounds)
            ? ChartVisibilityIndex.For(context.Data).QuerySegments(bounds, neighbors)
            : Full(context);
    }

    internal static ChartVisibleOrdinals Full(ChartSeriesRenderContext context)
    {
        Interlocked.Increment(ref _fallbackCount);
        return ChartVisibleOrdinals.All(context.Data.Count);
    }

    internal static bool TryBounds(ChartSeriesRenderContext context, Rect screen, double inflation,
        bool xOnly, bool yOnly, out ChartVisibilityBounds bounds)
    {
        bounds = default;
        if (context.Projection is not CartesianChart.CartesianProjection { HasStableTransform: true } projection ||
            !double.IsFinite(inflation) || inflation < 0 || !Finite(screen)) return false;
        // Preserve inclusive boundary hits despite inverse cancellation and the
        // subtraction used to query grouped selection centers. This is broad
        // phase padding only; original forward coordinates decide final coverage.
        var screenGuard = 1e-7 + Math.Max(Math.Max(Math.Abs(screen.Left), Math.Abs(screen.Right)),
            Math.Max(Math.Abs(screen.Top), Math.Abs(screen.Bottom))) * 1e-12;
        screen = screen.Inflate(inflation + screenGuard);
        // Project saturates at these plateaus. Inverse bounds there cannot prove
        // exclusion; keep the original exhaustive extension semantics.
        if (!Finite(screen) || screen.Left <= -999_999 || screen.Top <= -999_999 ||
            screen.Right >= 999_999 || screen.Bottom >= 999_999) return false;
        var first = projection.Unproject(screen.BottomLeft);
        var last = projection.Unproject(screen.TopRight);
        var (minimumX, maximumX) = Range(first.X, last.X, yOnly);
        var (minimumY, maximumY) = Range(first.Y, last.Y, xOnly);
        if (!yOnly && !Covers(projection.MapX(minimumX), projection.MapX(maximumX), screen.Left, screen.Right))
        { minimumX = double.NegativeInfinity; maximumX = double.PositiveInfinity; }
        if (!xOnly && !Covers(projection.MapY(minimumY), projection.MapY(maximumY), screen.Top, screen.Bottom))
        { minimumY = double.NegativeInfinity; maximumY = double.PositiveInfinity; }
        bounds = new(minimumX, maximumX, minimumY, maximumY);
        return true;
    }

    private static (double Minimum, double Maximum) Range(double first, double last, bool unrestricted)
    {
        if (unrestricted || !double.IsFinite(first) || !double.IsFinite(last))
            return (double.NegativeInfinity, double.PositiveInfinity);
        var minimum = Math.Min(first, last); var maximum = Math.Max(first, last);
        // Broad-phase only: inverse logarithmic/normalized mapping can differ by
        // many raw-value ULPs at extreme domains. Expand rather than lose an edge
        // primitive; exact forward projection/clipping remains authoritative.
        var guard = Math.Max(Math.Abs(minimum), Math.Abs(maximum)) * 1e-10;
        var low = minimum - guard; var high = maximum + guard;
        for (var step = 0; step < 8; step++)
        { low = Math.BitDecrement(low); high = Math.BitIncrement(high); }
        return (low, high);
    }

    // The broad bound must pass the same forward mapping used to paint. This
    // proof, rather than a presumed inverse error budget, controls exclusion.
    private static bool Covers(double first, double last, double minimum, double maximum) =>
        double.IsFinite(first) && double.IsFinite(last) && Math.Min(first, last) < minimum && Math.Max(first, last) > maximum;

    private static bool Finite(Rect value) => double.IsFinite(value.X) && double.IsFinite(value.Y) &&
        double.IsFinite(value.Right) && double.IsFinite(value.Bottom) && value.Width >= 0 && value.Height >= 0;
}
