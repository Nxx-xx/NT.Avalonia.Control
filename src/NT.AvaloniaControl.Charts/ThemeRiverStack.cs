namespace NT.AvaloniaControl.Charts;

/// <summary>Builds the transparent positive spacer used before a group of stacked AreaSeries.</summary>
/// <remarks>This is a centered stack composition, not a wiggle/streamgraph baseline algorithm.
/// Inputs must use the same ordered slots; missing values must be resolved explicitly by the caller.</remarks>
public static class ThemeRiverStack
{
    /// <summary>Returns a baseline at each slot so the visible total is centered at Center.
    /// Null center uses half the largest total. Explicit center must leave every spacer nonnegative.</summary>
    public static IReadOnlyList<ChartPoint> CreateBaseline(IReadOnlyList<IReadOnlyList<double>> contributions, double? center = null)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        if (center is { } c && (!double.IsFinite(c) || c < 0)) throw new ArgumentOutOfRangeException(nameof(center));
        if (contributions.Count == 0) return Array.Empty<ChartPoint>();
        if (contributions.Any(s => s is null)) throw new ArgumentException("Every stream must be supplied.", nameof(contributions));
        var count = contributions[0].Count;
        if (contributions.Any(s => s.Count != count)) throw new ArgumentException("All streams must use the same ordered slots.", nameof(contributions));
        var totals = new double[count];
        for (var slot = 0; slot < count; slot++)
        {
            var maximum = 0d;
            foreach (var stream in contributions)
            {
                var value = stream[slot];
                if (!double.IsFinite(value) || value < 0) throw new ArgumentException("River contributions must be finite and nonnegative.", nameof(contributions));
                maximum = Math.Max(maximum, value);
            }
            var scaled = 0d; var correction = 0d;
            if (maximum > 0)
                foreach (var stream in contributions)
                { var value = stream[slot] / maximum - correction; var next = scaled + value; correction = (next - scaled) - value; scaled = next; }
            totals[slot] = maximum * scaled;
            if (!double.IsFinite(totals[slot])) throw new OverflowException("The river total is outside the finite numeric axis domain.");
        }
        var halfMaximum = totals.DefaultIfEmpty(0).Max() / 2;
        var midpoint = center ?? halfMaximum;
        if (midpoint < halfMaximum) throw new ArgumentOutOfRangeException(nameof(center), "Center must be at least half the largest total.");
        var result = new ChartPoint[count];
        for (var i = 0; i < count; i++)
        {
            var baseline = midpoint - totals[i] / 2;
            if (!double.IsFinite(baseline + totals[i])) throw new OverflowException("The centered river exceeds the finite numeric axis domain.");
            result[i] = new(i, baseline);
        }
        return Array.AsReadOnly(result);
    }
}
