namespace NT.AvaloniaControl.Charts;

/// <summary>A signed observation relative to the diverging origin.</summary>
public readonly record struct DivergingValue(double Value, string? Label = null);
/// <summary>Two observations sharing a category.</summary>
public readonly record struct PairedComparisonValue(double First, double Second, string? Label = null);
/// <summary>A nonnegative component of one Mekko column.</summary>
public readonly record struct MekkoSegment(double Value, string? Label = null);
/// <summary>An inclusive set intersection: mask 1=A, 2=B, 4=C, 3=A∩B, and 7=A∩B∩C.</summary>
public readonly record struct VennItem(int Mask, double Value, string? Label = null);
/// <summary>Stable ordering, with ties retaining their original enumeration order.</summary>
public enum ComparisonSort { Source, ValueAscending, ValueDescending }

/// <summary>An owned, immutable snapshot of a Mekko category and its nonnegative width weight.</summary>
public sealed class MekkoColumn
{
    public MekkoColumn(double width, IEnumerable<MekkoSegment> segments, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var snapshot = new List<MekkoSegment>();
        foreach (var segment in segments)
        {
            if (snapshot.Count >= ChartAdvancedComparison.MaximumCount)
                throw new ArgumentOutOfRangeException(nameof(segments), "Segment budget exceeded.");
            snapshot.Add(segment);
        }
        Width = width; Label = label; Segments = Array.AsReadOnly(snapshot.ToArray());
    }
    public double Width { get; }
    public string? Label { get; }
    public IReadOnlyList<MekkoSegment> Segments { get; }
}

/// <summary>A normalized horizontal bar; Y increases downwards. The original signed value is retained.</summary>
public sealed record DivergingRow(int SourceIndex, double Value, string? Label,
    double Start, double End, double Y, double Height);
/// <summary>A normalized diverging layout. Zero is the common baseline.</summary>
public sealed class DivergingLayout
{
    internal DivergingLayout(DivergingRow[] rows, double zero) { Rows = Array.AsReadOnly(rows); Zero = zero; }
    public IReadOnlyList<DivergingRow> Rows { get; }
    public double Zero { get; }
}
/// <summary>A normalized pair retaining both raw values and the original enumeration index.</summary>
public sealed record PairedComparisonRow(int SourceIndex, double First, double Second, string? Label,
    double FirstPosition, double SecondPosition, double Y, double Height);
/// <summary>Two observations use one common scale; mirrored layouts require nonnegative magnitudes.</summary>
public sealed class PairedComparisonLayout
{
    internal PairedComparisonLayout(PairedComparisonRow[] rows, double zero, bool mirror)
    { Rows = Array.AsReadOnly(rows); Zero = zero; IsMirrored = mirror; }
    public IReadOnlyList<PairedComparisonRow> Rows { get; }
    public double Zero { get; }
    public bool IsMirrored { get; }
}
/// <summary>A normalized rectangle. Y=0 is the top of the column, with input segments stacked bottom-up.</summary>
public sealed record MekkoCell(int ColumnIndex, int SegmentIndex, string? Label, double Value,
    double X, double Y, double Width, double Height);
/// <summary>A width-weighted column layout with independently normalized segment heights.</summary>
public sealed class MekkoLayout
{
    internal MekkoLayout(MekkoCell[] cells) { Cells = Array.AsReadOnly(cells); }
    public IReadOnlyList<MekkoCell> Cells { get; }
}

/// <summary>An area-proportional circle inside a common normalized square.</summary>
public sealed record VennCircle(int SetIndex, ChartPoint Center, double Radius, double Value, int SourceIndex);
/// <summary>An exclusive atom derived from inclusive intersections; dependencies retain their source indices.</summary>
public sealed class VennRegion
{
    internal VennRegion(int mask, double value, double estimated, int[] indices)
    { Mask = mask; Value = value; EstimatedValue = estimated; SourceIndices = Array.AsReadOnly(indices); }
    public int Mask { get; }
    public double Value { get; }
    /// <summary>Area represented by the circles, expressed in the original value units.</summary>
    public double EstimatedValue { get; }
    public IReadOnlyList<int> SourceIndices { get; }
}
/// <summary>
/// Two sets use exact analytic overlap. Three sets use pairwise-distance placement and approximate
/// triple integration; arbitrary set quantities cannot in general be represented by three circles.
/// </summary>
public sealed class VennLayout
{
    internal VennLayout(VennCircle[] circles, VennRegion[] regions, bool approximate,
        double error, double integrationError)
    { Circles = Array.AsReadOnly(circles); Regions = Array.AsReadOnly(regions);
      IsApproximate = approximate; MaximumRelativeAreaError = error; TripleIntersectionErrorEstimate = integrationError; }
    public IReadOnlyList<VennCircle> Circles { get; }
    public IReadOnlyList<VennRegion> Regions { get; }
    public bool IsApproximate { get; }
    /// <summary>Maximum absolute exclusive-region error divided by the largest input singleton total (zero when all are zero).</summary>
    public double MaximumRelativeAreaError { get; }
    /// <summary>Difference between 2048- and 4096-slice midpoint triple integrals, in original units; an estimate, not a rigorous bound.</summary>
    public double TripleIntersectionErrorEstimate { get; }
}

/// <summary>Source-preserving, immutable comparison layouts. All coordinates are normalized to [0,1].</summary>
public static partial class ChartAdvancedComparison
{
    public const int MaximumCount = 1_000_000;

    private static void ValidateFinite(double value, string name, bool nonnegative = false)
    {
        if (!double.IsFinite(value) || (nonnegative && value < 0))
            throw new ArgumentOutOfRangeException(name, nonnegative ? "Value must be finite and nonnegative." : "Value must be finite.");
    }
    private static void ValidateSort(ComparisonSort sort)
    {
        if (!Enum.IsDefined(sort)) throw new ArgumentOutOfRangeException(nameof(sort));
    }
    private static T[] Snapshot<T>(IEnumerable<T> source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<T>();
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Count >= MaximumCount) throw new ArgumentOutOfRangeException(nameof(source), "Observation budget exceeded.");
            result.Add(item);
        }
        return result.ToArray();
    }
    private static int[] Ordered(int count, ComparisonSort sort, Func<int, double> value, CancellationToken cancellationToken)
    {
        var indices = Enumerable.Range(0, count).ToArray();
        if (sort != ComparisonSort.Source)
        {
            var comparisons = 0;
            try
            {
                Array.Sort(indices, (a, b) => {
                    if ((comparisons++ & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                    var comparison = value(a).CompareTo(value(b));
                    if (sort == ComparisonSort.ValueDescending) comparison = -comparison;
                    return comparison == 0 ? a.CompareTo(b) : comparison;
                });
            }
            catch (InvalidOperationException) when (cancellationToken.IsCancellationRequested)
            { cancellationToken.ThrowIfCancellationRequested(); throw; }
        }
        return indices;
    }

    /// <summary>Maps signed values around a centered 0.5 baseline with the same magnitude scale on both sides.</summary>
    public static DivergingLayout Diverging(IEnumerable<DivergingValue> source, ComparisonSort sort = ComparisonSort.Source,
        CancellationToken cancellationToken = default)
    {
        ValidateSort(sort);
        var values = Snapshot(source, cancellationToken);
        double negative = 0, positive = 0;
        foreach (var value in values)
        {
            ValidateFinite(value.Value, nameof(source));
            negative = Math.Max(negative, -value.Value); positive = Math.Max(positive, value.Value);
        }
        var scale = Math.Max(negative, positive);
        const double zero = .5;
        var indices = Ordered(values.Length, sort, i => values[i].Value, cancellationToken);
        var rows = new DivergingRow[values.Length];
        for (var row = 0; row < rows.Length; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = indices[row]; var value = values[index];
            var endpoint = scale == 0 ? zero : Math.Clamp(zero + .5 * (value.Value / scale), 0, 1);
            rows[row] = new(index, value.Value, value.Label, zero, endpoint, (row + .5) / rows.Length, 1d / rows.Length);
        }
        return new(rows, zero);
    }

    /// <summary>Maps paired values with one scale. Normal ordering uses Second-First; mirrored ordering uses First+Second.</summary>
    public static PairedComparisonLayout Paired(IEnumerable<PairedComparisonValue> source, bool mirror = false,
        ComparisonSort sort = ComparisonSort.Source, CancellationToken cancellationToken = default)
    {
        ValidateSort(sort);
        var values = Snapshot(source, cancellationToken);
        double negative = 0, positive = 0;
        foreach (var value in values)
        {
            ValidateFinite(value.First, nameof(source), mirror); ValidateFinite(value.Second, nameof(source), mirror);
            negative = Math.Max(negative, Math.Max(-value.First, -value.Second));
            positive = Math.Max(positive, Math.Max(value.First, value.Second));
        }
        var scale = Math.Max(negative, positive);
        var denominator = scale == 0 ? 1 : negative / scale + positive / scale;
        var zero = mirror || scale == 0 ? .5 : negative / scale / denominator;
        var indices = Ordered(values.Length, sort, i => {
            if (scale == 0) return 0;
            var raw = mirror ? values[i].First + values[i].Second : values[i].Second - values[i].First;
            // Prefer the finite raw operation so exactly equal ordinary totals remain ties.
            // Only use scaled addition when the raw operation exceeds the double range.
            return double.IsFinite(raw) ? raw / scale : mirror
                ? values[i].First / scale + values[i].Second / scale
                : values[i].Second / scale - values[i].First / scale;
        }, cancellationToken);
        var rows = new PairedComparisonRow[values.Length];
        for (var row = 0; row < rows.Length; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = indices[row]; var value = values[index];
            double first = zero, second = zero;
            if (scale != 0)
            {
                first = mirror ? .5 - .5 * (value.First / scale) : zero + value.First / scale / denominator;
                second = mirror ? .5 + .5 * (value.Second / scale) : zero + value.Second / scale / denominator;
            }
            rows[row] = new(index, value.First, value.Second, value.Label, Math.Clamp(first, 0, 1),
                Math.Clamp(second, 0, 1), (row + .5) / rows.Length, 1d / rows.Length);
        }
        return new(rows, zero, mirror);
    }

    /// <summary>Normalizes widths across columns and heights within each column. Zero weights produce empty geometry; negatives are rejected.</summary>
    public static MekkoLayout Mekko(IEnumerable<MekkoColumn> source, CancellationToken cancellationToken = default)
    {
        var columns = Snapshot(source, cancellationToken);
        double widthScale = 0; var count = 0;
        foreach (var column in columns)
        {
            ArgumentNullException.ThrowIfNull(column);
            ValidateFinite(column.Width, nameof(source), true);
            widthScale = Math.Max(widthScale, column.Width);
            count = checked(count + column.Segments.Count);
            if (count > MaximumCount) throw new ArgumentOutOfRangeException(nameof(source), "Generated cell budget exceeded.");
            foreach (var segment in column.Segments) ValidateFinite(segment.Value, nameof(source), true);
        }
        double widthSum = 0;
        if (widthScale != 0) foreach (var column in columns) widthSum += column.Width / widthScale;
        var cells = new List<MekkoCell>(count); double x = 0;
        for (var columnIndex = 0; columnIndex < columns.Length; columnIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var column = columns[columnIndex];
            var width = widthScale == 0 ? 0 : column.Width / widthScale / widthSum;
            double heightScale = 0, heightSum = 0;
            foreach (var segment in column.Segments) heightScale = Math.Max(heightScale, segment.Value);
            if (heightScale != 0) foreach (var segment in column.Segments) heightSum += segment.Value / heightScale;
            double accumulatedHeight = 0;
            for (var segmentIndex = 0; segmentIndex < column.Segments.Count; segmentIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var segment = column.Segments[segmentIndex];
                var height = heightScale == 0 ? 0 : segment.Value / heightScale / heightSum;
                cells.Add(new(columnIndex, segmentIndex, segment.Label, segment.Value, x,
                    Math.Clamp(1 - accumulatedHeight - height, 0, 1), width, height));
                accumulatedHeight += height;
            }
            x += width;
        }
        return new(cells.ToArray());
    }
}
