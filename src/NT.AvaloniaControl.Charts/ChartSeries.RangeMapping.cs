namespace NT.AvaloniaControl.Charts;

public abstract partial class ChartSeries
{
    private void ApplyRangeEmptyPoints(ChartDataPoint[] points)
    {
        if (EmptyPointMode == EmptyPointMode.Gap) return;
        for (var index = 0; index < points.Length; index++)
        {
            var item = points[index];
            if (!item.WasEmpty || !double.IsFinite(item.Point.X)) continue;
            static double? Finite(double? value) => value is { } number && double.IsFinite(number) ? number : null;
            var high = Finite(item.High); var low = Finite(item.Low);
            if (EmptyPointMode == EmptyPointMode.Zero) { high ??= 0; low ??= 0; }
            else
            {
                var left = index - 1; var right = index + 1;
                while (left >= 0 && points[left].WasEmpty) left--;
                while (right < points.Length && points[right].WasEmpty) right++;
                if (left < 0 || right >= points.Length) continue;
                var first = points[left]; var last = points[right];
                var span = last.Point.X - first.Point.X;
                var fraction = EmptyPointMode == EmptyPointMode.Average ? .5 :
                    span > 0 && double.IsFinite(span) ? (item.Point.X - first.Point.X) / span : (index - left) / (double)(right - left);
                var firstHigh = this is VarianceSeries ? first.High!.Value : Math.Max(first.High!.Value, first.Low!.Value);
                var firstLow = this is VarianceSeries ? first.Low!.Value : Math.Min(first.High.Value, first.Low!.Value);
                var lastHigh = this is VarianceSeries ? last.High!.Value : Math.Max(last.High!.Value, last.Low!.Value);
                var lastLow = this is VarianceSeries ? last.Low!.Value : Math.Min(last.High.Value, last.Low!.Value);
                high ??= firstHigh * (1 - fraction) + lastHigh * fraction;
                low ??= firstLow * (1 - fraction) + lastLow * fraction;
            }
            if (high is { } upper && double.IsFinite(upper) && low is { } lower && double.IsFinite(lower))
                points[index] = item with { Point = item.Point with { Y = this is VarianceSeries ? upper : Math.Max(upper, lower) }, High = upper, Low = lower, IsEmpty = false };
        }
    }
}
