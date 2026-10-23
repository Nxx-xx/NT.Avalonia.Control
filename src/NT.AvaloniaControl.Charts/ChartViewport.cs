namespace NT.AvaloniaControl.Charts;

/// <summary>The visible data bounds of a Cartesian chart.</summary>
public readonly record struct ChartViewport(double XMinimum, double XMaximum, double YMinimum, double YMaximum)
{
    /// <summary>Whether both axes have finite, strictly increasing bounds.</summary>
    public bool IsValid => double.IsFinite(XMinimum) && double.IsFinite(XMaximum) &&
        double.IsFinite(YMinimum) && double.IsFinite(YMaximum) &&
        XMinimum < XMaximum && YMinimum < YMaximum;
}

/// <summary>Viewport gestures evaluated in the full extent's normalized coordinate space.</summary>
internal static class ChartViewportMath
{
    private const double MinimumSpanFraction = 1e-6;

    /// <summary>
    /// Scales the visible spans around a data-oriented anchor: zero is the axis's lower bound.
    /// A factor smaller than one zooms in. The result remains within the full data extent.
    /// </summary>
    public static ChartViewport Zoom(ChartViewport current, ChartViewport extent,
        double anchorXFraction, double anchorYFraction, double factor, ChartInteractionAxes axes,
        double minimumXSpan = MinimumSpanFraction, double maximumXSpan = 1,
        double minimumYSpan = MinimumSpanFraction, double maximumYSpan = 1)
    {
        if (!current.IsValid || !extent.IsValid)
            return current.IsValid ? current : extent;
        var baseline = Constrain(current, extent);
        if (!double.IsFinite(factor) || factor <= 0 ||
            !double.IsFinite(anchorXFraction) || !double.IsFinite(anchorYFraction) ||
            factor == 1 || (axes & ChartInteractionAxes.Both) == ChartInteractionAxes.None)
            return baseline;

        var x = (axes & ChartInteractionAxes.X) != 0
            ? ZoomAxis(baseline.XMinimum, baseline.XMaximum, extent.XMinimum, extent.XMaximum,
                Math.Clamp(anchorXFraction, 0, 1), factor, minimumXSpan, maximumXSpan)
            : (baseline.XMinimum, baseline.XMaximum);
        var y = (axes & ChartInteractionAxes.Y) != 0
            ? ZoomAxis(baseline.YMinimum, baseline.YMaximum, extent.YMinimum, extent.YMaximum,
                Math.Clamp(anchorYFraction, 0, 1), factor, minimumYSpan, maximumYSpan)
            : (baseline.YMinimum, baseline.YMaximum);
        return new ChartViewport(x.Item1, x.Item2, y.Item1, y.Item2);
    }

    /// <summary>Moves each visible window by a fraction of its own current span.</summary>
    public static ChartViewport Pan(ChartViewport current, ChartViewport extent,
        double xFractionDelta, double yFractionDelta, ChartInteractionAxes axes,
        double minimumXSpan = MinimumSpanFraction, double maximumXSpan = 1,
        double minimumYSpan = MinimumSpanFraction, double maximumYSpan = 1)
    {
        if (!current.IsValid || !extent.IsValid)
            return current.IsValid ? current : extent;
        var baseline = Constrain(current, extent);
        if (!double.IsFinite(xFractionDelta) || !double.IsFinite(yFractionDelta) ||
            (axes & ChartInteractionAxes.Both) == ChartInteractionAxes.None)
            return baseline;

        var x = (axes & ChartInteractionAxes.X) != 0 && xFractionDelta != 0
            ? PanAxis(baseline.XMinimum, baseline.XMaximum, extent.XMinimum, extent.XMaximum,
                xFractionDelta, minimumXSpan, maximumXSpan)
            : (baseline.XMinimum, baseline.XMaximum);
        var y = (axes & ChartInteractionAxes.Y) != 0 && yFractionDelta != 0
            ? PanAxis(baseline.YMinimum, baseline.YMaximum, extent.YMinimum, extent.YMaximum,
                yFractionDelta, minimumYSpan, maximumYSpan)
            : (baseline.YMinimum, baseline.YMaximum);
        return new ChartViewport(x.Item1, x.Item2, y.Item1, y.Item2);
    }

    private static ChartViewport Constrain(ChartViewport current, ChartViewport extent)
    {
        if (!extent.IsValid)
            return current;
        if (!current.IsValid)
            return extent;
        var xMinimum = Math.Clamp(current.XMinimum, extent.XMinimum, extent.XMaximum);
        var xMaximum = Math.Clamp(current.XMaximum, extent.XMinimum, extent.XMaximum);
        var yMinimum = Math.Clamp(current.YMinimum, extent.YMinimum, extent.YMaximum);
        var yMaximum = Math.Clamp(current.YMaximum, extent.YMinimum, extent.YMaximum);
        if (xMinimum >= xMaximum)
            (xMinimum, xMaximum) = (extent.XMinimum, extent.XMaximum);
        if (yMinimum >= yMaximum)
            (yMinimum, yMaximum) = (extent.YMinimum, extent.YMaximum);
        return new ChartViewport(xMinimum, xMaximum, yMinimum, yMaximum);
    }

    private static (double, double) ZoomAxis(double minimum, double maximum,
        double extentMinimum, double extentMaximum, double anchor, double factor, double minimumSpan, double maximumSpan)
    {
        var lower = Normalize(minimum, extentMinimum, extentMaximum);
        var upper = Normalize(maximum, extentMinimum, extentMaximum);
        var span = upper - lower;
        var newSpan = ClampSpan(span * factor, minimumSpan, maximumSpan);
        var anchorPosition = Math.FusedMultiplyAdd(anchor, span, lower);
        var newLower = Math.Clamp(anchorPosition - anchor * newSpan, 0, 1 - newSpan);
        return ToDataBounds(newLower, newLower + newSpan, extentMinimum, extentMaximum);
    }

    private static (double, double) PanAxis(double minimum, double maximum,
        double extentMinimum, double extentMaximum, double fractionDelta, double minimumSpan, double maximumSpan)
    {
        var lower = Normalize(minimum, extentMinimum, extentMaximum);
        var upper = Normalize(maximum, extentMinimum, extentMaximum);
        var span = ClampSpan(upper - lower, minimumSpan, maximumSpan);
        var newLower = Math.Clamp(Math.FusedMultiplyAdd(fractionDelta, span, lower), 0, 1 - span);
        return ToDataBounds(newLower, newLower + span, extentMinimum, extentMaximum);
    }

    private static double ClampSpan(double span, double minimum, double maximum)
    {
        minimum = double.IsFinite(minimum) && minimum > 0 ? Math.Min(1, minimum) : MinimumSpanFraction;
        maximum = double.IsFinite(maximum) && maximum > 0 ? Math.Clamp(maximum, minimum, 1) : 1;
        return Math.Clamp(span, minimum, maximum);
    }

    private static double Normalize(double value, double minimum, double maximum)
    {
        if (value <= minimum)
            return 0;
        if (value >= maximum)
            return 1;
        var span = maximum - minimum;
        return double.IsFinite(span)
            ? (value - minimum) / span
            : (value * 0.5 - minimum * 0.5) / (maximum * 0.5 - minimum * 0.5);
    }

    private static double ToData(double fraction, double minimum, double maximum)
    {
        if (fraction <= 0)
            return minimum;
        if (fraction >= 1)
            return maximum;
        var span = maximum - minimum;
        var value = double.IsFinite(span)
            ? Math.FusedMultiplyAdd(span, fraction, minimum)
            : minimum * (1 - fraction) + maximum * fraction;
        return Math.Clamp(value, minimum, maximum);
    }

    private static (double, double) ToDataBounds(double lower, double upper,
        double extentMinimum, double extentMaximum)
    {
        var minimum = ToData(lower, extentMinimum, extentMaximum);
        var maximum = ToData(upper, extentMinimum, extentMaximum);

        // Round the two bounds outward. Near a large offset, an entire requested window
        // may occupy less than one ULP; adjacent finite doubles are the smallest usable view.
        if (Normalize(minimum, extentMinimum, extentMaximum) > lower)
            minimum = Math.Max(extentMinimum, Math.BitDecrement(minimum));
        if (Normalize(maximum, extentMinimum, extentMaximum) < upper)
            maximum = Math.Min(extentMaximum, Math.BitIncrement(maximum));
        if (minimum >= maximum)
        {
            minimum = Math.Max(extentMinimum, Math.BitDecrement(minimum));
            maximum = Math.Min(extentMaximum, Math.BitIncrement(maximum));
        }
        return minimum < maximum ? (minimum, maximum) : (extentMinimum, extentMaximum);
    }
}
