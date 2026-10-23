namespace NT.AvaloniaControl.Charts;

/// <summary>A finite observation and its position in the unfiltered input.</summary>
public readonly record struct ChartStatisticalObservation(int OriginalIndex, double Value);

/// <summary>Histogram height convention. Density has unit area over the included observations.</summary>
public enum ChartHistogramNormalization { Count, Probability, Density }

/// <summary>A left-closed, right-open bin; the last bin also includes its upper boundary.</summary>
public readonly record struct ChartHistogramBin(double Lower, double Upper, int Count, double Value);

/// <summary>An immutable interval in data coordinates.</summary>
public readonly record struct ChartErrorInterval
{
    internal ChartErrorInterval(double center, double lower, double upper)
    { Center = center; Lower = lower; Upper = upper; }
    public double Center { get; }
    public double Lower { get; }
    public double Upper { get; }
    public double LowerError => Center - Lower;
    public double UpperError => Upper - Center;
}

/// <summary>Descriptive statistics. Sample variance and standard deviation are undefined for a singleton.</summary>
public sealed class ChartSampleSummary
{
    internal ChartSampleSummary(int count, int omitted, double minimum, double maximum, double mean,
        double? sampleVariance, double? standardDeviation)
    { Count = count; OmittedCount = omitted; Minimum = minimum; Maximum = maximum; Mean = mean;
      SampleVariance = sampleVariance; StandardDeviation = standardDeviation; }
    public int Count { get; }
    public int OmittedCount { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Mean { get; }
    public double? SampleVariance { get; }
    public double? StandardDeviation { get; }
}

/// <summary>An immutable five-number summary, observed whiskers, and original outlier identities.</summary>
public sealed class ChartBoxPlotSummary
{
    internal ChartBoxPlotSummary(double minimum, double lowerQuartile, double median, double upperQuartile,
        double maximum, double lowerWhisker, double upperWhisker, double mean, int count, int omittedCount,
        ChartStatisticalObservation[] outliers, bool preaggregated)
    { Minimum = minimum; LowerQuartile = lowerQuartile; Median = median; UpperQuartile = upperQuartile;
      Maximum = maximum; LowerWhisker = lowerWhisker; UpperWhisker = upperWhisker; Mean = mean; Count = count;
      OmittedCount = omittedCount; Outliers = Array.AsReadOnly(outliers); IsPreaggregated = preaggregated; }
    public double Minimum { get; }
    public double LowerQuartile { get; }
    public double Median { get; }
    public double UpperQuartile { get; }
    public double Maximum { get; }
    public double LowerWhisker { get; }
    public double UpperWhisker { get; }
    public double Mean { get; }
    public int Count { get; }
    public int OmittedCount { get; }
    public bool IsPreaggregated { get; }
    public IReadOnlyList<ChartStatisticalObservation> Outliers { get; }

    /// <summary>Validates externally computed statistics. It cannot infer the original observations or verify their quartile convention.</summary>
    public static ChartBoxPlotSummary CreatePreaggregated(double minimum, double lowerQuartile, double median,
        double upperQuartile, double maximum, double lowerWhisker, double upperWhisker, double mean, int count,
        IEnumerable<ChartStatisticalObservation>? outliers = null)
    {
        double[] ordered = [minimum, lowerWhisker, lowerQuartile, median, upperQuartile, upperWhisker, maximum];
        if (ordered.Any(value => !double.IsFinite(value)) || !double.IsFinite(mean))
            throw new ArgumentOutOfRangeException(nameof(minimum), "Summary coordinates must be finite.");
        for (var i = 1; i < ordered.Length; i++)
            if (ordered[i] < ordered[i - 1]) throw new ArgumentException("Summary coordinates must be ordered.");
        if (mean < minimum || mean > maximum || count <= 0 || (count == 1 && minimum != maximum))
            throw new ArgumentException("Mean or sample count is inconsistent with the summary.");
        var snapshot = outliers?.ToArray() ?? [];
        var indices = new HashSet<int>();
        if (snapshot.Length > count) throw new ArgumentException("Outlier count exceeds sample count.", nameof(outliers));
        foreach (var point in snapshot)
            if (point.OriginalIndex < 0 || !indices.Add(point.OriginalIndex) || !double.IsFinite(point.Value) ||
                point.Value < minimum || point.Value > maximum || (point.Value >= lowerWhisker && point.Value <= upperWhisker))
                throw new ArgumentException("Outliers require unique nonnegative indices and values outside the whiskers within the extrema.", nameof(outliers));
        return new(minimum, lowerQuartile, median, upperQuartile, maximum, lowerWhisker, upperWhisker,
            mean, count, 0, snapshot, true);
    }
}

/// <summary>A histogram snapshot. Normalization uses IncludedCount; values outside explicit bounds are excluded.</summary>
public sealed class ChartHistogramResult
{
    internal ChartHistogramResult(ChartHistogramBin[] bins, int count, int omitted, int included,
        ChartHistogramNormalization normalization)
    { Bins = Array.AsReadOnly(bins); Count = count; OmittedCount = omitted; IncludedCount = included; Normalization = normalization; }
    public IReadOnlyList<ChartHistogramBin> Bins { get; }
    public int Count { get; }
    public int OmittedCount { get; }
    public int IncludedCount { get; }
    public int ExcludedCount => Count - IncludedCount;
    public ChartHistogramNormalization Normalization { get; }
}

/// <summary>Gaussian density samples. SampledArea is the trapezoidal integral over the finite displayed domain, not forced to one.</summary>
public sealed class ChartDensityResult
{
    internal ChartDensityResult(ChartPoint[] points, double bandwidth, int count, int omitted, double sampledArea)
    { Points = Array.AsReadOnly(points); Bandwidth = bandwidth; Count = count; OmittedCount = omitted; SampledArea = sampledArea; }
    public IReadOnlyList<ChartPoint> Points { get; }
    public double Bandwidth { get; }
    public int SampleCount => Points.Count;
    public int Count { get; }
    public int OmittedCount { get; }
    public double SampledArea { get; }
}

/// <summary>
/// Deterministic unweighted statistics. Nonfinite observations are omitted; nonfinite parameters and
/// unrepresentable arithmetic are rejected. Empty or entirely omitted samples throw ArgumentException.
/// Quantiles use Hyndman-Fan Type 7. The routines take snapshots and never mutate the caller's source.
/// </summary>
public static class ChartStatistics
{
    private const double InvSqrtTwoPi = 0.39894228040143267794;
    private const int MaximumBins = 65_536;

    /// <summary>Linear interpolation at zero-based rank (n - 1) * probability.</summary>
    public static double Quantile(IEnumerable<double> values, double probability)
    {
        Probability(probability, nameof(probability), true);
        return QuantileSorted(Read(values).Sorted, probability);
    }

    public static ChartSampleSummary Summarize(IEnumerable<double> values)
    {
        var input = Read(values);
        var mean = Mean(input.Sorted);
        if (input.Sorted.Length == 1) return new(1, input.Omitted, input.Sorted[0], input.Sorted[0], mean, null, null);
        var sd = StandardDeviation(input.Sorted);
        var variance = Finite(sd * sd);
        return new(input.Sorted.Length, input.Omitted, input.Sorted[0], input.Sorted[^1], mean, variance, sd);
    }

    /// <summary>
    /// Tukey whiskers use the most extreme observations within inclusive Q1/Q3 ± multiplier * IQR fences.
    /// If a narrow fence contains no observation beyond an interpolated quartile, that whisker ends at the quartile.
    /// </summary>
    public static ChartBoxPlotSummary BoxPlot(IEnumerable<double> values, double whiskerMultiplier = 1.5)
    {
        Nonnegative(whiskerMultiplier, nameof(whiskerMultiplier));
        var input = Read(values);
        var sorted = input.Sorted;
        var q1 = QuantileSorted(sorted, .25);
        var q3 = QuantileSorted(sorted, .75);
        var iqr = Finite(q3 - q1);
        var radius = Finite(iqr * whiskerMultiplier);
        var fenceLow = Finite(q1 - radius);
        var fenceHigh = Finite(q3 + radius);
        var low = Array.FindIndex(sorted, value => value >= fenceLow);
        var high = Array.FindLastIndex(sorted, value => value <= fenceHigh);
        var outliers = input.Observations.Where(point => point.Value < fenceLow || point.Value > fenceHigh).ToArray();
        return new(sorted[0], q1, QuantileSorted(sorted, .5), q3, sorted[^1], Math.Min(q1, sorted[low]), Math.Max(q3, sorted[high]),
            Mean(sorted), sorted.Length, input.Omitted, outliers, false);
    }

    public static ChartErrorInterval SymmetricError(double center, double error) => ErrorInterval(center, error, error);

    public static ChartErrorInterval ErrorInterval(double center, double lowerError, double upperError)
    {
        Parameter(center, nameof(center));
        Nonnegative(lowerError, nameof(lowerError));
        Nonnegative(upperError, nameof(upperError));
        return new(center, Finite(center - lowerError), Finite(center + upperError));
    }

    /// <summary>
    /// Two-sided large-sample normal approximation mean ± z * sampleSD / sqrt(n), with at least two finite values.
    /// This estimates sigma from data; it is not a Student-t interval or a small-sample coverage guarantee.
    /// </summary>
    public static ChartErrorInterval MeanConfidenceInterval(IEnumerable<double> values, double confidenceLevel = .95)
    {
        Probability(confidenceLevel, nameof(confidenceLevel), false);
        var input = Read(values);
        if (input.Sorted.Length < 2) throw new ArgumentException("A mean confidence interval requires at least two finite observations.", nameof(values));
        // Tail formulation avoids rounding (1 + confidenceLevel) / 2 to 1 near the upper endpoint.
        var critical = -InverseNormal((1 - confidenceLevel) / 2);
        var radius = Finite(critical * (StandardDeviation(input.Sorted) / Math.Sqrt(input.Sorted.Length)));
        return SymmetricError(Mean(input.Sorted), radius);
    }

    public static ChartHistogramResult Histogram(IEnumerable<double> values, int binCount = 10,
        ChartHistogramNormalization normalization = ChartHistogramNormalization.Count,
        double? minimum = null, double? maximum = null)
    {
        if (binCount is < 1 or > MaximumBins) throw new ArgumentOutOfRangeException(nameof(binCount));
        var input = Read(values);
        var (low, high) = Domain(input.Sorted, minimum, maximum, true);
        _ = Finite(high - low);
        var edges = new double[binCount + 1];
        for (var i = 0; i <= binCount; i++) edges[i] = Interpolate(low, high, (double)i / binCount);
        return HistogramCore(input, edges, normalization);
    }

    /// <summary>Anchors bins at minimum (or the observed minimum); the final bin is shortened to maximum.</summary>
    public static ChartHistogramResult HistogramByWidth(IEnumerable<double> values, double binWidth,
        ChartHistogramNormalization normalization = ChartHistogramNormalization.Count,
        double? minimum = null, double? maximum = null)
    {
        Positive(binWidth, nameof(binWidth));
        var input = Read(values);
        var (low, high) = Domain(input.Sorted, minimum, maximum, true);
        var count = Math.Ceiling(Finite(high - low) / binWidth);
        if (!double.IsFinite(count) || count is < 1 or > MaximumBins) throw new ArgumentOutOfRangeException(nameof(binWidth), "Bin width must produce at most 65536 nonempty-width bins.");
        var edges = new double[(int)count + 1];
        for (var i = 0; i < (int)count; i++) edges[i] = Finite(low + i * binWidth);
        edges[^1] = high;
        return HistogramCore(input, edges, normalization);
    }

    public static ChartHistogramResult HistogramByBoundaries(IEnumerable<double> values, IEnumerable<double> boundaries,
        ChartHistogramNormalization normalization = ChartHistogramNormalization.Count)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        var edges = boundaries.ToArray();
        if (edges.Length is < 2 or > MaximumBins + 1) throw new ArgumentException("Two to 65537 boundaries are required.", nameof(boundaries));
        return HistogramCore(Read(values), edges, normalization);
    }

    /// <summary>
    /// Direct Gaussian KDE: sum phi((x - value)/h) / (n*h). Automatic h is Silverman's .9*min(sd,IQR/1.34)*n^(-.2),
    /// falling back to sd and then max(1, abs(mean)*.01). Defaults to [min-3h,max+3h].
    /// No boundary reflection or finite-domain renormalization is applied. Cost is O(n*sampleCount).
    /// </summary>
    public static ChartDensityResult KernelDensity(IEnumerable<double> values, int sampleCount = 128,
        double? bandwidth = null, double? minimum = null, double? maximum = null)
    {
        if (sampleCount is < 2 or > 4096) throw new ArgumentOutOfRangeException(nameof(sampleCount));
        if (bandwidth.HasValue) Positive(bandwidth.Value, nameof(bandwidth));
        var input = Read(values);
        var sorted = input.Sorted;
        var h = bandwidth ?? AutomaticBandwidth(sorted);
        Positive(h, nameof(bandwidth));
        var low = minimum ?? Finite(sorted[0] - Finite(3 * h));
        var high = maximum ?? Finite(sorted[^1] + Finite(3 * h));
        ValidateDomain(low, high);
        _ = Finite(high - low);
        var scale = Finite(InvSqrtTwoPi / h / sorted.Length);
        if (scale <= 0) throw new OverflowException("Density scale underflowed the representable range.");
        var points = new ChartPoint[sampleCount];
        var integral = 0d;
        for (var i = 0; i < sampleCount; i++)
        {
            var x = Interpolate(low, high, (double)i / (sampleCount - 1));
            if (i > 0 && x <= points[i - 1].X)
                throw new ArgumentException("The sampling domain cannot represent the requested number of distinct points.", nameof(sampleCount));
            var sum = 0d;
            foreach (var value in sorted)
            {
                // Subtract first to retain small differences at a large offset; scale first only on overflow.
                var delta = x - value;
                var z = double.IsFinite(delta) ? delta / h : x / h - value / h;
                sum += Math.Exp(-.5 * z * z);
            }
            var y = Finite(sum * scale);
            points[i] = new(x, y);
            if (i > 0) integral = Finite(integral + (points[i - 1].Y / 2 + y / 2) * (x - points[i - 1].X));
        }
        return new(points, h, sorted.Length, input.Omitted, integral);
    }

    /// <summary>Right-continuous empirical CDF knots, one per distinct finite observation; tied values jump together.</summary>
    public static IReadOnlyList<ChartPoint> EmpiricalCumulativeDistribution(IEnumerable<double> values)
    {
        var sorted = Read(values).Sorted;
        var points = new List<ChartPoint>();
        for (var i = 0; i < sorted.Length;)
        {
            var end = i + 1;
            while (end < sorted.Length && sorted[end] == sorted[i]) end++;
            points.Add(new(sorted[i], (double)end / sorted.Length));
            i = end;
        }
        return Array.AsReadOnly(points.ToArray());
    }

    /// <summary>Pearson correlation with pairwise finite filtering; at least two nonconstant paired observations are required.</summary>
    public static double PearsonCorrelation(IEnumerable<ChartPoint> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var points = values.Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y)).ToArray();
        if (points.Length < 2) throw new ArgumentException("Correlation requires at least two finite pairs.", nameof(values));
        var xs = points.Select(point => point.X).ToArray();
        var ys = points.Select(point => point.Y).ToArray();
        var dxs = CenteredNormalized(xs);
        var dys = CenteredNormalized(ys);
        double xx = 0, yy = 0, xy = 0;
        for (var i = 0; i < points.Length; i++)
        {
            var dx = dxs[i];
            var dy = dys[i];
            xx += dx * dx; yy += dy * dy; xy += dx * dy;
        }
        if (xx == 0 || yy == 0) throw new ArgumentException("Correlation is undefined for a constant coordinate.", nameof(values));
        return Math.Clamp(Finite(xy / Math.Sqrt(xx) / Math.Sqrt(yy)), -1, 1);
    }

    private static ChartHistogramResult HistogramCore(Input input, double[] edges, ChartHistogramNormalization normalization)
    {
        if (!Enum.IsDefined(normalization)) throw new ArgumentOutOfRangeException(nameof(normalization));
        for (var i = 0; i < edges.Length; i++)
        {
            Parameter(edges[i], nameof(edges));
            if (i > 0 && (edges[i] <= edges[i - 1] || !double.IsFinite(edges[i] - edges[i - 1])))
                throw new ArgumentException("Boundaries must be strictly increasing with finite positive widths.", nameof(edges));
        }
        var counts = new int[edges.Length - 1];
        var included = 0;
        foreach (var value in input.Sorted)
        {
            if (value < edges[0] || value > edges[^1]) continue;
            var index = Array.BinarySearch(edges, value);
            index = index >= 0 ? Math.Min(index, counts.Length - 1) : ~index - 1;
            counts[index]++; included++;
        }
        var bins = new ChartHistogramBin[counts.Length];
        for (var i = 0; i < bins.Length; i++)
        {
            var height = normalization == ChartHistogramNormalization.Count ? counts[i]
                : included == 0 ? 0d : (double)counts[i] / included;
            if (normalization == ChartHistogramNormalization.Density) height = Finite(height / (edges[i + 1] - edges[i]));
            bins[i] = new(edges[i], edges[i + 1], counts[i], height);
        }
        return new(bins, input.Sorted.Length, input.Omitted, included, normalization);
    }

    private static double AutomaticBandwidth(double[] sorted)
    {
        var sd = sorted.Length > 1 ? StandardDeviation(sorted) : 0;
        var iqrScale = Finite(QuantileSorted(sorted, .75) - QuantileSorted(sorted, .25)) / 1.34;
        var scale = Math.Min(sd, iqrScale);
        if (scale <= 0) scale = sd;
        if (scale <= 0) scale = Math.Max(1, Math.Abs(Mean(sorted)) * .01);
        return Finite(.9 * scale * Math.Pow(sorted.Length, -.2));
    }

    private static (double Low, double High) Domain(double[] sorted, double? minimum, double? maximum, bool expandConstant)
    {
        if (minimum.HasValue) Parameter(minimum.Value, nameof(minimum));
        if (maximum.HasValue) Parameter(maximum.Value, nameof(maximum));
        var low = minimum ?? sorted[0];
        var high = maximum ?? sorted[^1];
        if (low == high && !minimum.HasValue && !maximum.HasValue && expandConstant)
        {
            var padding = Math.Max(.5, Math.Abs(low) * .005);
            var expandedLow = low - padding;
            var expandedHigh = high + padding;
            if (double.IsFinite(expandedLow)) low = expandedLow;
            if (double.IsFinite(expandedHigh)) high = expandedHigh;
        }
        ValidateDomain(low, high);
        return (low, high);
    }

    private static void ValidateDomain(double low, double high)
    {
        Parameter(low, nameof(low)); Parameter(high, nameof(high));
        if (low >= high) throw new ArgumentException("The sampling domain must have positive width.");
    }

    private static Input Read(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var observations = new List<ChartStatisticalObservation>();
        var omitted = 0;
        var index = 0;
        foreach (var value in values)
        {
            if (double.IsFinite(value)) observations.Add(new(index, value)); else omitted++;
            index = checked(index + 1);
        }
        if (observations.Count == 0) throw new ArgumentException("At least one finite observation is required.", nameof(values));
        var sorted = observations.Select(point => point.Value).ToArray();
        Array.Sort(sorted);
        return new(sorted, observations.ToArray(), omitted);
    }

    private static double QuantileSorted(double[] sorted, double probability)
    {
        var rank = (sorted.Length - 1) * probability;
        var index = (int)Math.Floor(rank);
        return index == sorted.Length - 1 ? sorted[index] : Interpolate(sorted[index], sorted[index + 1], rank - index);
    }

    private static double Interpolate(double low, double high, double fraction) => Finite(
        Math.Sign(low) == Math.Sign(high) ? low + (high - low) * fraction : low * (1 - fraction) + high * fraction);

    private static double Mean(double[] values)
    {
        var shifted = values.Select(value => value - values[0]).ToArray();
        if (shifted.All(double.IsFinite))
        {
            var shiftScale = shifted.Max(Math.Abs);
            return shiftScale == 0 ? values[0] : Finite(values[0] + MeanScaled(shifted, shiftScale) * shiftScale);
        }
        var scale = values.Max(Math.Abs);
        return scale == 0 ? 0 : Finite(MeanScaled(values, scale) * scale);
    }

    private static double MeanScaled(double[] values, double scale)
    {
        double sum = 0, correction = 0;
        foreach (var value in values)
        {
            var adjusted = value / scale - correction;
            var next = sum + adjusted;
            correction = (next - sum) - adjusted;
            sum = next;
        }
        return sum / values.Length;
    }

    private static double StandardDeviation(double[] values)
    {
        var shifted = values.Select(value => value - values[0]).ToArray();
        if (!shifted.All(double.IsFinite)) shifted = values;
        var scale = shifted.Max(Math.Abs);
        if (scale == 0) return 0;
        var mean = MeanScaled(shifted, scale);
        double sum = 0, correction = 0;
        foreach (var value in shifted)
        {
            var deviation = value / scale - mean;
            var adjusted = deviation * deviation - correction;
            var next = sum + adjusted;
            correction = (next - sum) - adjusted;
            sum = next;
        }
        return Finite(Math.Sqrt(sum / (values.Length - 1)) * scale);
    }

    private static double[] CenteredNormalized(double[] values)
    {
        var shifted = values.Select(value => value - values[0]).ToArray();
        if (!shifted.All(double.IsFinite)) shifted = values;
        var scale = shifted.Max(Math.Abs);
        if (scale == 0) return new double[values.Length];
        var mean = MeanScaled(shifted, scale);
        return shifted.Select(value => value / scale - mean).ToArray();
    }

    // P. J. Acklam's piecewise rational inverse-normal approximation, absolute relative error < 1.15e-9.
    // Only the lower half is used for mean intervals, preserving very small positive tail probabilities.
    private static double InverseNormal(double probability)
    {
        if (probability < .02425)
        {
            var q = Math.Sqrt(-2 * Math.Log(probability));
            return (((((-7.784894002430293e-3 * q - .3223964580411365) * q - 2.400758277161838) * q - 2.549732539343734) * q + 4.374664141464968) * q + 2.938163982698783)
                / ((((7.784695709041462e-3 * q + .3224671290700398) * q + 2.445134137142996) * q + 3.754408661907416) * q + 1);
        }
        var r = probability - .5;
        var t = r * r;
        return (((((-39.69683028665376 * t + 220.9460984245205) * t - 275.9285104469687) * t + 138.3577518672690) * t - 30.66479806614716) * t + 2.506628277459239) * r
            / (((((-54.47609879822406 * t + 161.5858368580409) * t - 155.6989798598866) * t + 66.80131188771972) * t - 13.28068155288572) * t + 1);
    }

    private static void Probability(double value, string name, bool closed)
    {
        Parameter(value, name);
        if (closed ? value < 0 || value > 1 : value <= 0 || value >= 1) throw new ArgumentOutOfRangeException(name);
    }
    private static void Parameter(double value, string name)
    { if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name, "Parameter must be finite."); }
    private static void Nonnegative(double value, string name)
    { Parameter(value, name); if (value < 0) throw new ArgumentOutOfRangeException(name); }
    private static void Positive(double value, string name)
    { Parameter(value, name); if (value <= 0) throw new ArgumentOutOfRangeException(name); }
    private static double Finite(double value)
    { if (!double.IsFinite(value)) throw new OverflowException("The statistical result exceeds the finite double range."); return value; }
    private sealed record Input(double[] Sorted, ChartStatisticalObservation[] Observations, int Omitted);
}
