namespace NT.AvaloniaControl.Charts;

/// <summary>A source distribution. Samples are unweighted scalar observations, in original order.</summary>
public sealed record DistributionSamples(object? Id, string Label, IReadOnlyList<double> Samples)
{
    // The DTO adapter preserves its actual source, rather than exposing its numerical descriptor as a business row.
    internal object? OriginalItem { get; init; }
}

/// <summary>An externally computed nonnegative density curve. No sample count, bandwidth, or quartiles are inferred.</summary>
public sealed record DistributionCurve(object? Id, string Label, IReadOnlyList<ChartPoint> Points)
{
    internal object? OriginalItem { get; init; }
}

/// <summary>An immutable finite curve snapshot, retaining its source group and original point indices.</summary>
public sealed class ChartDistributionCurveProfile
{
    internal ChartDistributionCurveProfile(int sourceIndex, DistributionCurve source, ChartPoint[] points)
    { SourceIndex = sourceIndex; Source = source; Points = Array.AsReadOnly(points); }
    public int SourceIndex { get; }
    public DistributionCurve Source { get; }
    public object SourceItem => Source.OriginalItem ?? Source;
    public IReadOnlyList<ChartPoint> Points { get; }
}

/// <summary>Precomputed curves with a shared horizontal domain and absolute density scale.</summary>
public sealed class ChartDistributionCurveLayout
{
    internal ChartDistributionCurveLayout(ChartDistributionCurveProfile[] groups, int groupCount, int pointCount, double minimum, double maximum, double maximumDensity)
    { Groups = Array.AsReadOnly(groups); GroupCount = groupCount; PointCount = pointCount; Minimum = minimum; Maximum = maximum; MaximumDensity = maximumDensity; }
    public IReadOnlyList<ChartDistributionCurveProfile> Groups { get; }
    public int GroupCount { get; }
    public int PointCount { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double MaximumDensity { get; }
}

/// <summary>A source-preserving Gaussian density and Type-7 quartile summary for one nonempty group.</summary>
public sealed class ChartDistributionProfile
{
    internal ChartDistributionProfile(int sourceIndex, DistributionSamples source, ChartStatisticalObservation[] observations,
        ChartBoxPlotSummary summary, ChartDensityResult density)
    { SourceIndex = sourceIndex; Source = source; Observations = Array.AsReadOnly(observations); Summary = summary; Density = density; }
    public int SourceIndex { get; }
    public DistributionSamples Source { get; }
    /// <summary>The original group or DTO. Density knots are synthetic coordinates and never replace this source.</summary>
    public object SourceItem => Source.OriginalItem ?? Source;
    public object? Id => Source.Id;
    public string Label => Source.Label;
    /// <summary>Finite scalar values and their original indices before missing/nonfinite values were omitted.</summary>
    public IReadOnlyList<ChartStatisticalObservation> Observations { get; }
    public ChartBoxPlotSummary Summary { get; }
    public ChartDensityResult Density { get; }
}

/// <summary>All densities use one value domain and one absolute maximum density, allowing width/height comparisons.</summary>
public sealed class ChartDistributionProfileLayout
{
    internal ChartDistributionProfileLayout(ChartDistributionProfile[] groups, int[] emptyGroups, double minimum,
        double maximum, double maximumDensity, int groupCount, int inputCount, int omittedCount)
    { Groups = Array.AsReadOnly(groups); EmptyGroupIndices = Array.AsReadOnly(emptyGroups); Minimum = minimum;
      Maximum = maximum; MaximumDensity = maximumDensity; GroupCount = groupCount; InputCount = inputCount; OmittedCount = omittedCount; }
    public IReadOnlyList<ChartDistributionProfile> Groups { get; }
    public IReadOnlyList<int> EmptyGroupIndices { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double MaximumDensity { get; }
    public int GroupCount { get; }
    public int InputCount { get; }
    public int OmittedCount { get; }
}

/// <summary>
/// Bounded unweighted distribution profiles. Nonfinite samples are omitted, empty groups have no density,
/// and singleton/constant groups use ChartStatistics' documented automatic-bandwidth fallback.
/// Gaussian tails are not truncated at observed extrema, reflected, or renormalized. Explicit limits only
/// select the displayed sampling domain; they do not discard observations when calculating a density.
/// </summary>
public static class ChartDistributionProfiles
{
    public const int MaximumGroups = 256;
    public const int MaximumObservations = 100_000;
    public const int MaximumDensityPoints = 131_072;
    public const long MaximumKernelEvaluations = 4_000_000;

    /// <summary>
    /// Captures up to 256 curves and 100000 actually enumerated points. Every nonempty curve needs at
    /// least two strictly increasing finite X coordinates and finite nonnegative Y values. Invalid
    /// coordinates are rejected rather than joined across gaps. Curves remain piecewise linear;
    /// they are not normalized or smoothed, and their scalar source point indices are unchanged.
    /// </summary>
    public static ChartDistributionCurveLayout FromCurves(IEnumerable<DistributionCurve> groups,
        double? minimum = null, double? maximum = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);
        if (minimum.HasValue && !double.IsFinite(minimum.Value)) throw new ArgumentOutOfRangeException(nameof(minimum));
        if (maximum.HasValue && !double.IsFinite(maximum.Value)) throw new ArgumentOutOfRangeException(nameof(maximum));
        cancellationToken.ThrowIfCancellationRequested();
        var output = new List<ChartDistributionCurveProfile>(); var groupCount = 0; var pointCount = 0;
        var low = double.PositiveInfinity; var high = double.NegativeInfinity; var peak = 0d;
        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (groupCount >= MaximumGroups) throw new ArgumentOutOfRangeException(nameof(groups), "Curve enumeration exceeds 256 actual entries.");
            var index = groupCount++; ArgumentNullException.ThrowIfNull(group); var points = new List<ChartPoint>();
            if (group.Points is { } source) foreach (var point in source)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pointCount >= MaximumObservations) throw new ArgumentOutOfRangeException(nameof(groups), "Curve enumeration exceeds 100000 actual points.");
                pointCount++;
                if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || point.Y < 0 || points.Count > 0 && point.X <= points[^1].X)
                    throw new ArgumentException("Curves need strictly increasing finite X and finite nonnegative density Y.", nameof(groups));
                points.Add(point); peak = Math.Max(peak, point.Y);
            }
            if (points.Count == 0) continue;
            if (points.Count < 2) throw new ArgumentException("A nonempty curve requires at least two points.", nameof(groups));
            low = Math.Min(low, points[0].X); high = Math.Max(high, points[^1].X);
            output.Add(new(index, group, points.ToArray()));
        }
        low = minimum ?? (output.Count == 0 ? 0 : low); high = maximum ?? (output.Count == 0 ? 1 : high);
        if (low >= high || !double.IsFinite(high - low)) throw new ArgumentException("The shared curve domain needs a finite positive span.");
        return new(output.ToArray(), groupCount, pointCount, low, high, peak);
    }

    /// <summary>
    /// Enumerates at most 256 groups and 100000 actual sample entries (including omitted values).
    /// Generated density knots may not exceed 131072. The finite sample count times (sampleCount + 2)
    /// must fit 4000000 kernel evaluations. Cancellation
    /// is checked while enumerating and between bounded KDE calls; no caller collection is mutated.
    /// </summary>
    public static ChartDistributionProfileLayout Calculate(IEnumerable<DistributionSamples> groups, int sampleCount = 128,
        double? bandwidth = null, double? minimum = null, double? maximum = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);
        if (sampleCount is < 2 or > 4096) throw new ArgumentOutOfRangeException(nameof(sampleCount));
        if (bandwidth.HasValue && (!double.IsFinite(bandwidth.Value) || bandwidth.Value <= 0)) throw new ArgumentOutOfRangeException(nameof(bandwidth));
        if (minimum.HasValue && !double.IsFinite(minimum.Value)) throw new ArgumentOutOfRangeException(nameof(minimum));
        if (maximum.HasValue && !double.IsFinite(maximum.Value)) throw new ArgumentOutOfRangeException(nameof(maximum));
        if (minimum.HasValue && maximum.HasValue && minimum.Value >= maximum.Value) throw new ArgumentException("Minimum must be less than maximum.");
        cancellationToken.ThrowIfCancellationRequested();
        var inputs = new List<(int Index, DistributionSamples Source, double[] Samples, ChartStatisticalObservation[] Finite, ChartDensityResult Preview)>();
        var empty = new List<int>(); var groupCount = 0; var count = 0; var omitted = 0; long work = 0;
        var low = double.PositiveInfinity; var high = double.NegativeInfinity;
        foreach (var source in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (groupCount >= MaximumGroups) throw new ArgumentOutOfRangeException(nameof(groups), "Group enumeration exceeds 256 actual entries.");
            var index = groupCount++; ArgumentNullException.ThrowIfNull(source);
            var samples = new List<double>(); var finite = new List<ChartStatisticalObservation>();
            if (source.Samples is { } collection) foreach (var value in collection)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (count >= MaximumObservations) throw new ArgumentOutOfRangeException(nameof(groups), "Sample enumeration exceeds 100000 actual entries, including omitted values.");
                count++; var originalIndex = samples.Count; samples.Add(value);
                if (double.IsFinite(value))
                {
                    finite.Add(new(originalIndex, value)); work += sampleCount + 2L;
                    if (work > MaximumKernelEvaluations) throw new ArgumentOutOfRangeException(nameof(sampleCount), "The requested direct KDE exceeds 4000000 kernel evaluations.");
                }
                else omitted++;
            }
            if (finite.Count == 0) { empty.Add(index); continue; }
            if ((inputs.Count + 1L) * sampleCount > MaximumDensityPoints) throw new ArgumentOutOfRangeException(nameof(sampleCount), "Generated densities exceed 131072 knots.");
            cancellationToken.ThrowIfCancellationRequested();
            var values = samples.ToArray();
            var preview = ChartStatistics.KernelDensity(values, 2, bandwidth);
            low = Math.Min(low, preview.Points[0].X); high = Math.Max(high, preview.Points[^1].X);
            inputs.Add((index, source, values, finite.ToArray(), preview));
        }
        low = minimum ?? (inputs.Count == 0 ? 0 : low); high = maximum ?? (inputs.Count == 0 ? 1 : high);
        if (low >= high || !double.IsFinite(high - low)) throw new ArgumentException("The shared distribution domain must have a finite positive span.");
        var output = new List<ChartDistributionProfile>(); var maximumDensity = 0d;
        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var density = ChartStatistics.KernelDensity(input.Samples, sampleCount, input.Preview.Bandwidth, low, high);
            var summary = ChartStatistics.BoxPlot(input.Samples);
            maximumDensity = Math.Max(maximumDensity, density.Points.Max(point => point.Y));
            output.Add(new(input.Index, input.Source, input.Finite, summary, density));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (output.Count > 0 && maximumDensity <= 0) throw new OverflowException("All displayed densities underflowed; choose limits nearer the observations.");
        return new(output.ToArray(), empty.ToArray(), low, high, maximumDensity, groupCount, count, omitted);
    }
}
