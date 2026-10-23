namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private readonly record struct StackKey(ChartAxis XAxis, ChartAxis YAxis, string Group, ChartStackMode Mode);
    private readonly record struct StackSlot(double X, int Occurrence);
    private sealed class StackTotals
    {
        public double PositiveMaximum;
        public double NegativeMaximum;
        public double PositiveWeight;
        public double NegativeWeight;
        public double PositiveAccumulatedWeight;
        public double NegativeAccumulatedWeight;
        public double Positive;
        public double Negative;
    }

    // Display transforms never mutate a published/source snapshot. Every ordinal,
    // identity and item survives; only projected values and band metadata change.
    private static void PrepareLineAreaData(SeriesSnapshot[] snapshots)
    {
        foreach (var snapshot in snapshots.Where(item => item.Series is IChartRangeSeries or AreaSeries { StackMode: ChartStackMode.None }))
        {
            if (snapshot.Series is IChartRangeSeries && snapshot.YAxis is CategoryAxis)
                throw new InvalidOperationException("Range areas require a numeric value axis.");
            var data = snapshot.Data;
            var values = new ChartDataValue[data.Count];
            var bands = new ChartBandValue[data.Count];
            for (var index = 0; index < data.Count; index++)
            {
                var item = data.Points[index];
                var range = snapshot.Series is IChartRangeSeries;
                var valid = !item.IsEmpty && (range ? double.IsFinite(item.Point.X) && item.High is { } high && double.IsFinite(high) && item.Low is { } low && double.IsFinite(low) : IsValid(item.Point));
                var upper = valid ? range ? Math.Max(item.High!.Value, item.Low!.Value) : item.Point.Y : double.NaN;
                var lower = valid ? range ? Math.Min(item.High!.Value, item.Low!.Value) : ((AreaSeries)snapshot.Series).Baseline : double.NaN;
                values[index] = new(item.SourceIndex, new(item.Point.X, upper), !valid, item.WasEmpty);
                bands[index] = new(lower, null, item.Point);
            }
            snapshot.Data = data.WithBandValues(values, bands);
        }

        var groups = snapshots.Where(item => item.Series is IChartLineSeries { StackMode: not ChartStackMode.None })
            .GroupBy(item => new StackKey(item.XAxis, item.YAxis, ((IChartLineSeries)item.Series).StackGroup ?? string.Empty, ((IChartLineSeries)item.Series).StackMode));
        foreach (var group in groups)
        {
            if (group.Key.YAxis is CategoryAxis)
                throw new InvalidOperationException("Line/area stacking requires a numeric value axis.");
            var members = group.ToArray();
            var totals = new Dictionary<StackSlot, StackTotals>();
            var slots = new Dictionary<SeriesSnapshot, StackSlot[]>();
            foreach (var snapshot in members)
            {
                var data = snapshot.Data;
                var occurrences = new Dictionary<double, int>();
                var keys = new StackSlot[data.Count];
                slots.Add(snapshot, keys);
                for (var index = 0; index < data.Count; index++)
                {
                    var item = data.GetValue(index);
                    var occurrence = occurrences.GetValueOrDefault(item.Point.X);
                    occurrences[item.Point.X] = occurrence + 1;
                    var key = keys[index] = new(item.Point.X, occurrence);
                    if (item.IsEmpty || !IsValid(item.Point) || !snapshot.XAxis.IsValidValue(item.Point.X) || snapshot.XAxis.IsInScaleBreak(item.Point.X)) continue;
                    if (!totals.TryGetValue(key, out var total)) totals.Add(key, total = new());
                    if (item.Point.Y >= 0) total.PositiveMaximum = Math.Max(total.PositiveMaximum, item.Point.Y);
                    else total.NegativeMaximum = Math.Max(total.NegativeMaximum, -item.Point.Y);
                }
            }
            if (group.Key.Mode == ChartStackMode.Percent)
            {
                // Scale before summing: finite source values cannot overflow a
                // percentage denominator merely because their raw sum does.
                foreach (var snapshot in members)
                    for (var index = 0; index < snapshot.Data.Count; index++)
                    {
                        var value = snapshot.Data.GetValue(index);
                        if (value.IsEmpty || !IsValid(value.Point) || !totals.TryGetValue(slots[snapshot][index], out var total)) continue;
                        if (value.Point.Y >= 0 && total.PositiveMaximum > 0) total.PositiveWeight += value.Point.Y / total.PositiveMaximum;
                        else if (value.Point.Y < 0 && total.NegativeMaximum > 0) total.NegativeWeight += -value.Point.Y / total.NegativeMaximum;
                    }
            }
            foreach (var snapshot in members)
            {
                var data = snapshot.Data;
                var values = new ChartDataValue[data.Count];
                var bands = new ChartBandValue[data.Count];
                for (var index = 0; index < data.Count; index++)
                {
                    var item = data.GetValue(index);
                    var raw = item.Point;
                    var lower = double.NaN; var contribution = double.NaN; var upper = double.NaN;
                    if (!item.IsEmpty && IsValid(raw) && totals.TryGetValue(slots[snapshot][index], out var total))
                    {
                        lower = raw.Y >= 0 ? total.Positive : total.Negative;
                        if (group.Key.Mode == ChartStackMode.Percent)
                        {
                            if (raw.Y >= 0)
                            {
                                total.PositiveAccumulatedWeight += total.PositiveMaximum > 0 ? raw.Y / total.PositiveMaximum : 0;
                                upper = total.PositiveWeight > 0 ? total.PositiveAccumulatedWeight / total.PositiveWeight * 100 : 0;
                            }
                            else
                            {
                                total.NegativeAccumulatedWeight += -raw.Y / total.NegativeMaximum;
                                upper = -total.NegativeAccumulatedWeight / total.NegativeWeight * 100;
                            }
                            // Cumulative weights use the denominator's exact order:
                            // the final boundary is exactly +/-100, avoiding a
                            // rounding speck that expands a nice automatic axis.
                            contribution = upper - lower;
                        }
                        else { contribution = raw.Y; upper = lower + contribution; }
                        if (raw.Y >= 0) total.Positive = upper; else total.Negative = upper;
                    }
                    var valid = double.IsFinite(upper) && double.IsFinite(lower);
                    values[index] = item with { Point = new(raw.X, valid ? upper : double.NaN), IsEmpty = !valid };
                    bands[index] = new(lower, contribution, raw);
                }
                snapshot.Data = data.WithBandValues(values, bands);
            }
        }
    }

    private static bool IsPurePercentAxis(SeriesSnapshot[] snapshots) => snapshots.Length > 0 &&
        snapshots.All(snapshot => snapshot.Series is IChartLineSeries { StackMode: ChartStackMode.Percent });

    private static void PrepareStackPaths(SeriesSnapshot[] snapshots)
    {
        foreach (var group in snapshots.Where(item => item.Series is IChartLineSeries { StackMode: not ChartStackMode.None } &&
            !ChartLineGeometry.IsBackgroundPending(item.Context!))
            .GroupBy(item => new StackKey(item.XAxis, item.YAxis, ((IChartLineSeries)item.Series).StackGroup ?? string.Empty, ((IChartLineSeries)item.Series).StackMode)))
            ChartLineGeometry.PrepareStackGeometry(group.Select(item => item.Context!).ToArray());
    }

    private string BandReadingSuffix(ChartScene scene, SeriesSnapshot snapshot, ChartPointHit hit, double? displayedLower = null)
    {
        if (hit.BaseValue is not { } lower) return PointReadingSuffix(hit);
        var axis = ComparisonValueAxis(snapshot);
        var boundary = scene.Scales[axis].FormatLabel(displayedLower ?? lower, axis);
        var suffix = $" (下界: {boundary}";
        if (hit.StackValue is { } contribution && hit.RawPoint is { } raw)
        {
            suffix += $", 原值: {raw.Y.ToString("G6", System.Globalization.CultureInfo.CurrentCulture)}";
            if (snapshot.Series is IChartLineSeries { StackMode: ChartStackMode.Percent } or BarSeries { StackMode: ChartStackMode.Percent })
                suffix += $", 占比: {contribution.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture)}%";
        }
        if (hit.CumulativeValue is { } cumulative) suffix += $", 累计: {cumulative.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture)}%";
        if (hit.IsTotal) suffix += ", 总计";
        return suffix + ")" + PointReadingSuffix(hit);
    }
}
