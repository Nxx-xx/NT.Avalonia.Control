namespace NT.AvaloniaControl.Charts;

/// <summary>Trailing average convention. Weighted gives the newest observation the greatest weight.</summary>
public enum ChartMovingAverageType { Simple, Exponential, Weighted, Triangular }

/// <summary>Unweighted regression model, or a trailing simple moving average.</summary>
public enum ChartTrendlineType { Linear, Polynomial, Exponential, Logarithmic, Power, MovingAverage }

/// <summary>A source-aligned Bollinger observation. Warmup and missing values have NaN ordinates.</summary>
public readonly record struct ChartBollingerPoint(double X, double Middle, double Upper, double Lower);

/// <summary>An immutable fitted model and sampled curve. R² uses the original Y domain and is undefined for constant Y.</summary>
public sealed class ChartTrendlineResult
{
    private readonly Func<double, double>? _predict;

    internal ChartTrendlineResult(ChartTrendlineType type, ChartPoint[] points, int count, int omitted,
        double? rSquared, Func<double, double>? predict)
    { Type = type; Points = Array.AsReadOnly(points); Count = count; OmittedCount = omitted; RSquared = rSquared; _predict = predict; }

    public ChartTrendlineType Type { get; }
    public IReadOnlyList<ChartPoint> Points { get; }
    public int Count { get; }
    public int OmittedCount { get; }
    public double? RSquared { get; }

    /// <summary>Evaluates a fitted regression. Moving averages have no extrapolation model.</summary>
    public double Predict(double x)
    {
        if (!double.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x), "Prediction coordinate must be finite.");
        if (_predict is null) throw new InvalidOperationException("A moving average cannot predict an arbitrary X coordinate.");
        return _predict(x);
    }
}

/// <summary>
/// Deterministic analytics over a snapshot of the complete source, in its supplied order; no sorting or viewport filtering.
/// Inputs are bounded to 1,000,000 observations. Invalid parameters, insufficient/rank-deficient fits and
/// unrepresentable arithmetic throw ArgumentOutOfRangeException, ArgumentException and OverflowException respectively.
/// Cancellation is observed while reading, computing and sampling. Caller-owned input is never modified.
/// </summary>
public static partial class ChartAnalytics
{
    private const int MaximumInputCount = 1_000_000;
    private const int MaximumPeriod = 65_536;

    /// <summary>
    /// Preserves every input X and index. A nonfinite X or Y resets the trailing window and yields NaN;
    /// each complete finite run requires period observations before its first output. EMA starts at the
    /// period-window SMA, then uses alpha=2/(period+1). TMA is SMA(ceil(period/2)) followed by
    /// SMA(floor(period/2)+1), so both odd and even periods require exactly period source observations.
    /// </summary>
    public static IReadOnlyList<ChartPoint> MovingAverage(IEnumerable<ChartPoint> points, int period = 14,
        ChartMovingAverageType type = ChartMovingAverageType.Simple, CancellationToken cancellationToken = default)
    {
        ValidatePeriod(period);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        var input = ReadAnalytics(points, cancellationToken);
        return Array.AsReadOnly(MovingAverageCore(input, period, type, cancellationToken));
    }

    /// <summary>
    /// Trailing SMA ± multiplier * population standard deviation (divisor period). Preserves source alignment,
    /// resets at nonfinite X/Y and yields NaN throughout warmup. Multiplier must be finite and nonnegative.
    /// </summary>
    public static IReadOnlyList<ChartBollingerPoint> BollingerBands(IEnumerable<ChartPoint> points, int period = 20,
        double standardDeviationMultiplier = 2, CancellationToken cancellationToken = default)
    {
        ValidatePeriod(period);
        if (!double.IsFinite(standardDeviationMultiplier) || standardDeviationMultiplier < 0)
            throw new ArgumentOutOfRangeException(nameof(standardDeviationMultiplier));
        var input = ReadAnalytics(points, cancellationToken);
        var output = new ChartBollingerPoint[input.Length];
        var window = new AnalyticsWindow();
        for (var i = 0; i < input.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var point = input[i];
            if (!ValidAnalyticsPoint(point)) window.Clear();
            else { window.Add(point.Y); if (window.Count > period) window.Remove(); }
            if (!ValidAnalyticsPoint(point) || window.Count < period)
            { output[i] = new(point.X, double.NaN, double.NaN, double.NaN); continue; }
            var aggregate = window.Aggregate;
            var radius = AnalyticsFinite(aggregate.StandardDeviation * standardDeviationMultiplier);
            output[i] = new(point.X, aggregate.Mean, AnalyticsFinite(aggregate.Mean + radius), AnalyticsFinite(aggregate.Mean - radius));
        }
        return Array.AsReadOnly(output);
    }

    private static ChartPoint[] MovingAverageCore(ChartPoint[] input, int period, ChartMovingAverageType type, CancellationToken cancellationToken)
    {
        if (type == ChartMovingAverageType.Triangular)
            return MovingAverageCore(MovingAverageCore(input, (period + 1) / 2, ChartMovingAverageType.Simple, cancellationToken),
                period / 2 + 1, ChartMovingAverageType.Simple, cancellationToken);
        var output = new ChartPoint[input.Length];
        var window = new AnalyticsWindow();
        var exponential = double.NaN;
        var alpha = 2d / (period + 1);
        for (var i = 0; i < input.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var point = input[i];
            if (!ValidAnalyticsPoint(point))
            { window.Clear(); exponential = double.NaN; output[i] = new(point.X, double.NaN); continue; }
            if (type == ChartMovingAverageType.Exponential && double.IsFinite(exponential))
            { exponential = AnalyticsBlend(exponential, point.Y, alpha); output[i] = new(point.X, exponential); continue; }
            window.Add(point.Y);
            if (window.Count > period) window.Remove();
            var value = window.Count < period ? double.NaN : type == ChartMovingAverageType.Weighted
                ? window.Aggregate.WeightedMean : window.Aggregate.Mean;
            if (type == ChartMovingAverageType.Exponential) exponential = value;
            output[i] = new(point.X, value);
        }
        return output;
    }

    private static ChartPoint[] ReadAnalytics(IEnumerable<ChartPoint> points, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(points);
        cancellationToken.ThrowIfCancellationRequested();
        var input = new List<ChartPoint>();
        foreach (var point in points)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (input.Count == MaximumInputCount)
                throw new ArgumentException("Analytics accepts at most 1,000,000 source observations.", nameof(points));
            input.Add(point);
        }
        return input.ToArray();
    }

    private static void ValidatePeriod(int period)
    { if (period is < 1 or > MaximumPeriod) throw new ArgumentOutOfRangeException(nameof(period), "Period must be between 1 and 65536."); }

    private static bool ValidAnalyticsPoint(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static double AnalyticsFinite(double value)
    { if (!double.IsFinite(value)) throw new OverflowException("The analytics result exceeds the finite double range."); return value; }

    // A convex combination avoids overflowing equal-sign sums or opposite-sign differences.
    private static double AnalyticsBlend(double first, double second, double fraction)
    {
        if (fraction == 0) return first;
        if (fraction == 1) return second;
        return AnalyticsFinite(Math.Sign(first) == Math.Sign(second) ? Math.FusedMultiplyAdd(second - first, fraction, first)
            : first * (1 - fraction) + second * fraction);
    }

    private static double AnalyticsHypot(double first, double second)
    {
        var larger = Math.Max(Math.Abs(first), Math.Abs(second));
        if (larger == 0) return 0;
        var ratio = Math.Min(Math.Abs(first), Math.Abs(second)) / larger;
        return AnalyticsFinite(larger * Math.Sqrt(1 + ratio * ratio));
    }

    // Two aggregate stacks provide O(n) amortized trailing windows without subtracting an old huge
    // observation from a rolling sum. Removing an outlier therefore does not erase small later values.
    private sealed class AnalyticsWindow
    {
        private readonly Stack<(double Value, AnalyticsAggregate Aggregate)> _incoming = new();
        private readonly Stack<(double Value, AnalyticsAggregate Aggregate)> _outgoing = new();
        public int Count => _incoming.Count + _outgoing.Count;
        public AnalyticsAggregate Aggregate => AnalyticsAggregate.Merge(
            _outgoing.Count == 0 ? default : _outgoing.Peek().Aggregate,
            _incoming.Count == 0 ? default : _incoming.Peek().Aggregate);
        public void Clear() { _incoming.Clear(); _outgoing.Clear(); }
        public void Add(double value) => _incoming.Push((value, AnalyticsAggregate.Merge(
            _incoming.Count == 0 ? default : _incoming.Peek().Aggregate, AnalyticsAggregate.Single(value))));
        public void Remove()
        {
            if (_outgoing.Count == 0)
                while (_incoming.TryPop(out var entry))
                    _outgoing.Push((entry.Value, AnalyticsAggregate.Merge(AnalyticsAggregate.Single(entry.Value),
                        _outgoing.Count == 0 ? default : _outgoing.Peek().Aggregate)));
            _outgoing.Pop();
        }
    }

    private readonly record struct AnalyticsAggregate(int Count, double Mean, double WeightedMean, double StandardDeviation)
    {
        public static AnalyticsAggregate Single(double value) => new(1, value, value, 0);
        public static AnalyticsAggregate Merge(AnalyticsAggregate first, AnalyticsAggregate second)
        {
            if (first.Count == 0) return second;
            if (second.Count == 0) return first;
            var count = first.Count + second.Count;
            var fraction = (double)second.Count / count;
            var mean = AnalyticsBlend(first.Mean, second.Mean, fraction);
            var secondWeights = second.Count * (second.Count + 1d) / 2;
            var offsetWeights = (double)first.Count * second.Count;
            var combinedSecond = AnalyticsBlend(second.WeightedMean, second.Mean, offsetWeights / (secondWeights + offsetWeights));
            var weightedMean = AnalyticsBlend(first.WeightedMean, combinedSecond,
                (secondWeights + offsetWeights) / (count * (count + 1d) / 2));
            var deltaWeight = Math.Sqrt(fraction * (1 - fraction));
            var delta = first.Mean * deltaWeight - second.Mean * deltaWeight;
            var sd = AnalyticsHypot(AnalyticsHypot(first.StandardDeviation * Math.Sqrt(1 - fraction),
                second.StandardDeviation * Math.Sqrt(fraction)), delta);
            return new(count, mean, weightedMean, sd);
        }
    }
}
