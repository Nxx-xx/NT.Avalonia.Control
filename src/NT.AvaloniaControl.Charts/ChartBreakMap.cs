namespace NT.AvaloniaControl.Charts;

/// <summary>Piecewise compression evaluated in unbroken normalized space, avoiding raw-double span overflow.</summary>
internal sealed class ChartBreakMap
{
    private readonly record struct Segment(double Start, double End, double Offset, double Ratio);
    private readonly Segment[] _segments;
    private readonly double _weight;
    private ChartBreakMap(Segment[] segments, double weight) { _segments = segments; _weight = weight; }

    public static ChartBreakMap? Create(IReadOnlyList<ChartScaleBreakRange> ranges, double minimum, double maximum,
        Func<double, double> normalize)
    {
        var segments = new List<Segment>();
        var cursor = 0d;
        var weight = 0d;
        foreach (var range in ranges)
        {
            if (range.End <= minimum || range.Start >= maximum) continue;
            var start = Math.Clamp(normalize(Math.Max(minimum, range.Start)), 0, 1);
            var end = Math.Clamp(normalize(Math.Min(maximum, range.End)), 0, 1);
            if (!(end > start)) continue;
            if (start > cursor) { segments.Add(new(cursor, start, weight, 1)); weight += start - cursor; }
            // Avoid a zero-weight whole-view break when a subnormal compression ratio is requested.
            var ratio = Math.Max(1e-12, range.CompressionRatio);
            segments.Add(new(start, end, weight, ratio));
            weight += (end - start) * ratio;
            cursor = end;
        }
        if (segments.Count == 0) return null;
        if (cursor < 1) { segments.Add(new(cursor, 1, weight, 1)); weight += 1 - cursor; }
        return weight > 0 && double.IsFinite(weight) ? new(segments.ToArray(), weight) : null;
    }

    public double Compress(double fraction)
    {
        if (fraction <= 0) return fraction / _weight;
        if (fraction >= 1) return 1 + (fraction - 1) / _weight;
        foreach (var segment in _segments)
            if (fraction <= segment.End)
                return (segment.Offset + (fraction - segment.Start) * segment.Ratio) / _weight;
        return 1;
    }

    public double Expand(double fraction)
    {
        if (fraction <= 0) return fraction * _weight;
        if (fraction >= 1) return 1 + (fraction - 1) * _weight;
        var target = fraction * _weight;
        foreach (var segment in _segments)
        {
            var end = segment.Offset + (segment.End - segment.Start) * segment.Ratio;
            if (target <= end)
                return Math.Clamp(segment.Start + (target - segment.Offset) / segment.Ratio, segment.Start, segment.End);
        }
        return 1;
    }
}
