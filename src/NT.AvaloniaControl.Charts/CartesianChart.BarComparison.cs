using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Layout;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private static readonly ConditionalWeakTable<ParetoSeries, NumericAxis> ParetoAxes = new();
    private static readonly ConditionalWeakTable<ChartDataSnapshot, CumulativeSnapshots> ParetoData = new();
    private sealed class CumulativeSnapshots
    {
        internal readonly object Sync = new();
        internal ChartDataSnapshot? Horizontal, Vertical;
    }
    private readonly record struct BarStackKey(ChartAxis X, ChartAxis Y, Orientation Orientation, string Group, ChartStackMode Mode, double Baseline);
    private readonly record struct CategoryGroupKey(ChartAxis Axis, bool Horizontal);
    private readonly record struct CategorySlot(double Category, int Occurrence);
    private sealed class BarTotals
    {
        public double PositiveMaximum, NegativeMaximum, PositiveWeight, NegativeWeight, PositiveAccumulatedWeight, NegativeAccumulatedWeight;
        public double Positive, Negative;
    }
    private static bool HorizontalComparison(ChartSeries series) => series is IChartComparisonSeries { Orientation: Orientation.Horizontal };
    private static ChartAxis ComparisonValueAxis(SeriesSnapshot snapshot) => HorizontalComparison(snapshot.Series) ? snapshot.XAxis : snapshot.YAxis;
    private static ChartAxis ComparisonCategoryAxis(SeriesSnapshot snapshot) => HorizontalComparison(snapshot.Series) ? snapshot.YAxis : snapshot.XAxis;
    private static double ComparisonValue(ChartPoint point, bool horizontal) => horizontal ? point.X : point.Y;
    private static double ComparisonCategory(ChartPoint point, bool horizontal) => horizontal ? point.Y : point.X;
    private static ChartPoint WithComparisonValue(ChartPoint point, double value, bool horizontal) => horizontal ? point with { X = value } : point with { Y = value };
    private static ChartPoint WithComparisonCategory(ChartPoint point, double value, bool horizontal) => horizontal ? point with { Y = value } : point with { X = value };

    private void PrepareComparisonSources(SeriesSnapshot[] snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Series is not IChartComparisonSeries options || snapshot.Series is BarSeries { UsesLegacyBarRenderer: true }) continue;
            var horizontal = HorizontalComparison(snapshot.Series);
            if (ComparisonValueAxis(snapshot) is CategoryAxis)
                throw new InvalidOperationException("Comparison series require a numeric value axis.");
            if (snapshot.Series is HistogramSeries && ComparisonCategoryAxis(snapshot) is CategoryAxis)
                throw new InvalidOperationException("Histogram bin boundaries require a numeric axis.");
            if (snapshot.Series is ParetoSeries && options.SortMode != ChartSeriesSortMode.ValueDescending)
                throw new InvalidOperationException("Pareto series require descending value sorting.");
            if (snapshot.Series is WaterfallSeries && options.SortMode != ChartSeriesSortMode.Source)
                throw new InvalidOperationException("Waterfall deltas and totals follow source order; sorting is not supported.");
            if (options.SortMode is ChartSeriesSortMode.ValueAscending or ChartSeriesSortMode.ValueDescending &&
                ComparisonCategoryAxis(snapshot) is not CategoryAxis { Labels: null })
                throw new InvalidOperationException("Value sorting requires a CategoryAxis with automatically generated labels.");
            var points = snapshot.Data.Points.Select(item => item with
            {
                RawPoint = item.Point,
                Point = horizontal ? new(item.Point.Y, item.Point.X) : item.Point,
                IsEmpty = item.IsEmpty || snapshot.Series is ParetoSeries && item.Point.Y < 0,
            }).ToArray();
            if (options.SortMode == ChartSeriesSortMode.XAscending)
                points = points.OrderBy(item => item.RawPoint!.Value.X).ToArray();
            else if (options.SortMode is ChartSeriesSortMode.ValueAscending or ChartSeriesSortMode.ValueDescending)
            {
                var ordered = points.OrderBy(item => item.IsEmpty || !IsValid(item.Point));
                points = options.SortMode == ChartSeriesSortMode.ValueAscending
                    ? ordered.ThenBy(item => item.RawPoint!.Value.Y).ToArray()
                    : ordered.ThenByDescending(item => item.RawPoint!.Value.Y).ToArray();
            }
            snapshot.Data = ChartDataSnapshot.FromOwned(snapshot.Data.Version, points, snapshot.Data.Issues);
            if (snapshot.Series is ParetoSeries pareto)
            {
                var axis = string.IsNullOrWhiteSpace(pareto.CumulativeAxisKey)
                    ? ParetoAxes.GetValue(pareto, static _ => new NumericAxis { Minimum = 0, Maximum = 100, Position = ChartAxisPosition.Far, Title = "累计 (%)", LabelFormat = "0'%'" })
                    : ResolveAxis(pareto.CumulativeAxisKey, horizontal ? XAxis : YAxis);
                if (axis is CategoryAxis or LogarithmicAxis or DateTimeAxis || ReferenceEquals(axis, snapshot.XAxis) || ReferenceEquals(axis, snapshot.YAxis))
                    throw new InvalidOperationException("The Pareto cumulative axis must be a separate linear numeric axis.");
                snapshot.SecondaryAxis = axis;
            }
        }
    }

    // One sorted series determines the shared axis order. Duplicate labels retain
    // their Nth occurrence; peer series align to the same occurrence slots.
    private static void PrepareSortedCategories(SeriesSnapshot[] snapshots, Dictionary<ChartAxis, IReadOnlyList<string>> categories)
    {
        foreach (var group in snapshots.Where(item => item.Series is IChartComparisonSeries { SortMode: ChartSeriesSortMode.ValueAscending or ChartSeriesSortMode.ValueDescending })
            .GroupBy(ComparisonCategoryAxis))
        {
            var axis = group.Key;
            var driver = group.First();
            var horizontalDimension = ReferenceEquals(axis, driver.XAxis);
            var members = snapshots.Where(item => ReferenceEquals(horizontalDimension ? item.XAxis : item.YAxis, axis)).ToArray();
            var labels = new List<string>();
            var slots = new Dictionary<(string Label, int Occurrence), int>();
            foreach (var snapshot in new[] { driver }.Concat(members.Where(item => !ReferenceEquals(item, driver))))
            {
                var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
                var identities = new Dictionary<int, (string Label, int Occurrence)>();
                foreach (var item in snapshot.Data.Points.OrderBy(item => item.SourceIndex))
                {
                    var categoryDimension = horizontalDimension != HorizontalComparison(snapshot.Series);
                    var category = categoryDimension ? item.Category : item.Value ?? (horizontalDimension ? item.Point.X : item.Point.Y);
                    var label = Convert.ToString(category, CultureInfo.CurrentCulture) ?? item.SourceIndex.ToString(CultureInfo.CurrentCulture);
                    var occurrence = occurrences.GetValueOrDefault(label); occurrences[label] = occurrence + 1;
                    identities[item.SourceIndex] = (label, occurrence);
                }
                var points = snapshot.Data.Points.Select(item =>
                {
                    var identity = identities[item.SourceIndex];
                    if (!slots.TryGetValue(identity, out var slot)) { slot = labels.Count; slots.Add(identity, slot); labels.Add(identity.Label); }
                    var point = horizontalDimension ? item.Point with { X = slot } : item.Point with { Y = slot };
                    return item with { Point = point, IsEmpty = item.IsEmpty || !IsValid(point) };
                }).ToArray();
                snapshot.Data = ChartDataSnapshot.FromOwned(snapshot.Data.Version, points, snapshot.Data.Issues);
            }
            categories[axis] = labels;
        }
    }

    private static void PrepareComparisonData(SeriesSnapshot[] snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Series is not IChartComparisonSeries options || snapshot.Series is BarSeries { UsesLegacyBarRenderer: true }) continue;
            if (snapshot.Series is BarSeries { StackMode: not ChartStackMode.None }) continue;
            if (snapshot.Series is DotSeries or StatisticalSeries) continue;
            var data = snapshot.Data; var horizontal = HorizontalComparison(snapshot.Series);
            var values = new ChartDataValue[data.Count]; var bands = new ChartBandValue[data.Count];
            var running = options.Baseline;
            var maximum = snapshot.Series is ParetoSeries ? data.Points.Where(item => !item.IsEmpty && IsValid(item.Point))
                .Select(item => ComparisonValue(item.Point, horizontal)).DefaultIfEmpty(0).Max() : 0;
            var denominator = maximum > 0 ? data.Points.Where(item => !item.IsEmpty && IsValid(item.Point))
                .Sum(item => ComparisonValue(item.Point, horizontal) / maximum) : 0;
            var cumulativeWeight = 0d;
            var lastValid = snapshot.Series is ParetoSeries ? Enumerable.Range(0, data.Count).LastOrDefault(index => !data.GetValue(index).IsEmpty && IsValid(data.GetValue(index).Point), -1) : -1;
            for (var index = 0; index < data.Count; index++)
            {
                var item = data.Points[index]; var point = item.Point;
                var valid = !item.IsEmpty && IsValid(point);
                var value = ComparisonValue(point, horizontal); var lower = options.Baseline;
                double? contribution = null; double? cumulative = null;
                if (snapshot.Series is RangeBarSeries or VarianceSeries)
                {
                    valid &= item.High is { } hi && double.IsFinite(hi) && item.Low is { } lo && double.IsFinite(lo);
                    value = valid ? snapshot.Series is VarianceSeries ? item.High!.Value : Math.Max(item.High!.Value, item.Low!.Value) : double.NaN;
                    lower = valid ? snapshot.Series is VarianceSeries ? item.Low!.Value : Math.Min(item.High!.Value, item.Low!.Value) : double.NaN;
                }
                else if (snapshot.Series is WaterfallSeries && valid)
                {
                    lower = item.IsTotal ? options.Baseline : running;
                    contribution = value;
                    value = item.IsTotal ? value : running + value;
                    running = value;
                }
                else if (snapshot.Series is ParetoSeries && valid)
                {
                    cumulativeWeight += maximum > 0 ? value / maximum : 0;
                    cumulative = denominator > 0 ? index == lastValid ? 100 : Math.Clamp(cumulativeWeight / denominator * 100, 0, 100) : 0;
                }
                valid &= double.IsFinite(value) && double.IsFinite(lower);
                values[index] = new(item.SourceIndex, WithComparisonValue(point, valid ? value : double.NaN, horizontal), !valid, item.WasEmpty);
                bands[index] = new(lower, contribution, item.RawPoint ?? item.Point, cumulative);
            }
            snapshot.Data = data.WithBandValues(values, bands);
        }
        PrepareBarStacks(snapshots);
    }

    private static void PrepareBarStacks(SeriesSnapshot[] snapshots)
    {
        foreach (var group in snapshots.Where(item => item.Series is BarSeries { StackMode: not ChartStackMode.None })
            .GroupBy(item => { var bar = (BarSeries)item.Series; return new BarStackKey(item.XAxis, item.YAxis, bar.Orientation, bar.StackGroup ?? "", bar.StackMode, bar.Baseline); }))
        {
            var horizontal = group.Key.Orientation == Orientation.Horizontal;
            if (group.Key.Mode == ChartStackMode.Percent && group.Key.Baseline != 0)
                throw new InvalidOperationException("Percentage bars require a zero baseline.");
            var members = group.ToArray(); var totals = new Dictionary<CategorySlot, BarTotals>();
            var slots = new Dictionary<SeriesSnapshot, CategorySlot[]>();
            foreach (var snapshot in members)
            {
                var occurrences = new Dictionary<double, int>(); var keys = new CategorySlot[snapshot.Data.Count]; slots.Add(snapshot, keys);
                for (var i = 0; i < keys.Length; i++)
                {
                    var item = snapshot.Data.GetValue(i); var category = ComparisonCategory(item.Point, horizontal);
                    var occurrence = occurrences.GetValueOrDefault(category); occurrences[category] = occurrence + 1;
                    var key = keys[i] = new(category, occurrence);
                    if (item.IsEmpty || !IsValid(item.Point) || !ComparisonCategoryAxis(snapshot).IsValidValue(category) || ComparisonCategoryAxis(snapshot).IsInScaleBreak(category)) continue;
                    if (!totals.TryGetValue(key, out var total)) totals.Add(key, total = new() { Positive = group.Key.Baseline, Negative = group.Key.Baseline });
                    var value = ComparisonValue(item.Point, horizontal);
                    if (value >= 0) total.PositiveMaximum = Math.Max(total.PositiveMaximum, value); else total.NegativeMaximum = Math.Max(total.NegativeMaximum, -value);
                }
            }
            if (group.Key.Mode == ChartStackMode.Percent)
                foreach (var snapshot in members)
                    for (var i = 0; i < snapshot.Data.Count; i++)
                    {
                        var item = snapshot.Data.GetValue(i);
                        if (item.IsEmpty || !IsValid(item.Point) || !totals.TryGetValue(slots[snapshot][i], out var total)) continue;
                        var value = ComparisonValue(item.Point, horizontal);
                        if (value >= 0 && total.PositiveMaximum > 0) total.PositiveWeight += value / total.PositiveMaximum;
                        else if (value < 0 && total.NegativeMaximum > 0) total.NegativeWeight += -value / total.NegativeMaximum;
                    }
            foreach (var snapshot in members)
            {
                var data = snapshot.Data; var values = new ChartDataValue[data.Count]; var bands = new ChartBandValue[data.Count];
                for (var i = 0; i < data.Count; i++)
                {
                    var item = data.Points[i]; var raw = ComparisonValue(item.Point, horizontal);
                    var lower = double.NaN; var upper = double.NaN; var contribution = double.NaN;
                    if (!item.IsEmpty && IsValid(item.Point) && totals.TryGetValue(slots[snapshot][i], out var total))
                    {
                        lower = raw >= 0 ? total.Positive : total.Negative;
                        if (group.Key.Mode == ChartStackMode.Percent)
                        {
                            if (raw >= 0) { total.PositiveAccumulatedWeight += total.PositiveMaximum > 0 ? raw / total.PositiveMaximum : 0; upper = total.PositiveWeight > 0 ? total.PositiveAccumulatedWeight / total.PositiveWeight * 100 : 0; }
                            else { total.NegativeAccumulatedWeight += -raw / total.NegativeMaximum; upper = -total.NegativeAccumulatedWeight / total.NegativeWeight * 100; }
                            contribution = upper - lower;
                        }
                        else { contribution = raw; upper = lower + raw; }
                        if (raw >= 0) total.Positive = upper; else total.Negative = upper;
                    }
                    var valid = double.IsFinite(lower) && double.IsFinite(upper);
                    values[i] = new(item.SourceIndex, WithComparisonValue(item.Point, valid ? upper : double.NaN, horizontal), !valid, item.WasEmpty);
                    bands[i] = new(lower, contribution, item.RawPoint ?? item.Point);
                }
                snapshot.Data = data.WithBandValues(values, bands);
            }
        }
    }

    private static ChartDataSnapshot BuildCumulativeData(ChartDataSnapshot data, bool horizontal)
    {
        var cached = ParetoData.GetValue(data, static _ => new());
        lock (cached.Sync)
        {
            ref var result = ref (horizontal ? ref cached.Horizontal : ref cached.Vertical);
            // Cumulative values depend on the immutable prepared data, rather
            // than the secondary axis/view. Keep one source snapshot across zooms.
            return result ??= ChartDataSnapshot.FromOwned(data.Version,
                data.Points.Select(item => item with { Point = WithComparisonValue(item.Point, item.CumulativeValue ?? double.NaN, horizontal),
                    IsEmpty = item.CumulativeValue is not { } cumulative || !double.IsFinite(cumulative) ||
                        !double.IsFinite(ComparisonCategory(item.Point, horizontal)) }).ToArray(), data.Issues);
        }
    }

    private static void SetSecondaryContext(SeriesSnapshot snapshot, Rect plot, IReadOnlyDictionary<ChartAxis, ChartScale> scales)
    {
        if (snapshot.SecondaryAxis is not { } axis) return;
        var horizontal = HorizontalComparison(snapshot.Series);
        var secondary = BuildCumulativeData(snapshot.Data, horizontal);
        snapshot.Context = snapshot.Context! with { SecondaryData = secondary,
            SecondaryBreakClip = CreateBreakClip(plot, scales[horizontal ? axis : snapshot.XAxis], scales[horizontal ? snapshot.YAxis : axis]),
            SecondaryProjection = new CartesianProjection(plot, scales[horizontal ? axis : snapshot.XAxis], scales[horizontal ? snapshot.YAxis : axis], secondary) };
    }

    private static void LayoutCategoryGroups(SeriesSnapshot[] snapshots, IReadOnlyDictionary<ChartAxis, ChartScale> scales, Rect plot)
    {
        foreach (var group in snapshots.Where(item => item.Series.Renderer.UsesCategorySlots).GroupBy(item => new CategoryGroupKey(ComparisonCategoryAxis(item), HorizontalComparison(item.Series))))
        {
            var members = group.ToArray(); var horizontal = group.Key.Horizontal;
            var slots = new Dictionary<object, int>();
            var keys = members.Select(snapshot => snapshot.Series is BarSeries { StackMode: not ChartStackMode.None } bar
                ? (object)new BarStackKey(snapshot.XAxis, snapshot.YAxis, bar.Orientation, bar.StackGroup ?? "", bar.StackMode, bar.Baseline) : snapshot.Series).ToArray();
            foreach (var key in keys) if (!slots.ContainsKey(key)) slots.Add(key, slots.Count);
            var scale = scales[group.Key.Axis];
            var positions = members.SelectMany(item => item.Points).Where(IsValid).Select(point => horizontal ? scale.Map(point.Y, plot.Bottom, plot.Top) : scale.Map(point.X, plot.Left, plot.Right))
                .Where(double.IsFinite).Distinct().Order().ToArray();
            var spacing = positions.Length < 2 ? horizontal ? plot.Height : plot.Width : positions.Zip(positions.Skip(1), (a, b) => b - a).Where(value => value > 0).DefaultIfEmpty(horizontal ? plot.Height : plot.Width).Min();
            var width = Math.Max(1, Math.Min(120, spacing * .72));
            for (var i = 0; i < members.Length; i++) members[i].Context = members[i].Context! with { GroupIndex = slots[keys[i]], GroupCount = slots.Count, GroupWidth = width };
        }
    }
}
