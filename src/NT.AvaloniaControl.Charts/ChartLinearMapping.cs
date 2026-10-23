using Avalonia;
using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

// Created only after an immutable snapshot's existing index proves both clamps
// inert for its finite points on numeric axes. Callers skip missing points.
// Keep the original divisions, endpoint rules and FMA for every original point.
internal readonly struct ChartLinearMapping(double minimum, double maximum, double span,
    bool inversed, double start, double end, double distance)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Map(double value)
    {
        var fraction = value == minimum ? 0 : value == maximum ? 1 : (value - minimum) / span;
        if (inversed) fraction = 1 - fraction;
        if (fraction == 0) return start;
        if (fraction == 1) return end;
        return Math.FusedMultiplyAdd(distance, fraction, start);
    }
}

internal readonly struct InRangeLinearProjection(ChartLinearMapping x, ChartLinearMapping y)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Point Project(ChartPoint point) => new(x.Map(point.X), y.Map(point.Y));
}

internal interface IChartLineSink
{
    void Begin(Point position);
    void Line(Point position);
    void End();
}
