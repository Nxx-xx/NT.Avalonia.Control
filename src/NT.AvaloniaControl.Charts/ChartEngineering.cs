namespace NT.AvaloniaControl.Charts;

/// <summary>A physical resistance/reactance in ohms, or conductance/susceptance in siemens.</summary>
public readonly record struct SmithPoint(double R, double X);

/// <summary>Chooses the physical quantity accepted by a Smith transform.</summary>
public enum SmithQuantity { Impedance, Admittance }

/// <summary>Three nonnegative components; normalization is performed by the transform.</summary>
public readonly record struct TernaryPoint(double A, double B, double C);

/// <summary>A parameter pair and its ordinate in a nonorthogonal carpet mesh.</summary>
public readonly record struct CarpetPoint(double A, double B, double Y);

/// <summary>A Cartesian observation with an optional value for hexagonal aggregation.</summary>
public readonly record struct HexbinPoint(double X, double Y, double Value = 1);

/// <summary>The statistic displayed by a hexagonal bin.</summary>
public enum HexbinAggregation { Count, Sum, Mean }

/// <summary>A clockwise direction from north, nonnegative speed, and positive frequency weight.</summary>
public readonly record struct WindObservation(double Direction, double Speed, double Frequency = 1);

/// <summary>An immutable polyline in engineering data coordinates.</summary>
public sealed class EngineeringPath
{
    internal EngineeringPath(string label, ChartPoint[] points)
    { Label = label; Points = Array.AsReadOnly(points); }
    public string Label { get; }
    public IReadOnlyList<ChartPoint> Points { get; }
}

/// <summary>A pointy-top hexagon. Membership indices refer to the unfiltered source enumeration.</summary>
public sealed class HexbinBin
{
    internal HexbinBin(long q, long r, ChartPoint center, ChartPoint[] vertices, int[] indices, double value)
    { Q = q; R = r; Center = center; Vertices = Array.AsReadOnly(vertices);
      SourceIndices = Array.AsReadOnly(indices); Value = value; }
    public long Q { get; }
    public long R { get; }
    public ChartPoint Center { get; }
    public IReadOnlyList<ChartPoint> Vertices { get; }
    public IReadOnlyList<int> SourceIndices { get; }
    public int Count => SourceIndices.Count;
    public double Value { get; }
}

/// <summary>An immutable hexbin snapshot, in axial Q/R order. Nonfinite observations are omitted.</summary>
public sealed class HexbinResult
{
    internal HexbinResult(HexbinBin[] bins, int count, int omitted, double radius, ChartPoint origin,
        HexbinAggregation aggregation)
    { Bins = Array.AsReadOnly(bins); Count = count; OmittedCount = omitted;
      Radius = radius; Origin = origin; Aggregation = aggregation; }
    public IReadOnlyList<HexbinBin> Bins { get; }
    public int Count { get; }
    public int OmittedCount { get; }
    public double Radius { get; }
    public ChartPoint Origin { get; }
    public HexbinAggregation Aggregation { get; }
}

/// <summary>A speed band inside one north-centered direction sector. Percentages use all accepted frequency.</summary>
public sealed class WindRoseBin
{
    internal WindRoseBin(int sector, int band, double lower, double upper, int[] indices,
        double frequency, double percentage, double startFrequency, double endFrequency)
    { SectorIndex = sector; SpeedBandIndex = band; LowerSpeed = lower; UpperSpeed = upper;
      SourceIndices = Array.AsReadOnly(indices); Frequency = frequency; Percentage = percentage;
      StartFrequency = startFrequency; EndFrequency = endFrequency; }
    public int SectorIndex { get; }
    public int SpeedBandIndex { get; }
    public double LowerSpeed { get; }
    public double UpperSpeed { get; }
    public IReadOnlyList<int> SourceIndices { get; }
    public int Count => SourceIndices.Count;
    public double Frequency { get; }
    public double Percentage { get; }
    public double StartFrequency { get; }
    public double EndFrequency { get; }
}

/// <summary>A direction sector, ordered clockwise from north; speed bands are ordered from slow to fast.</summary>
public sealed class WindRoseSector
{
    internal WindRoseSector(int index, double direction, double width, string label, WindRoseBin[] bins)
    { Index = index; DirectionDegrees = direction; WidthDegrees = width; Label = label;
      Bins = Array.AsReadOnly(bins); }
    public int Index { get; }
    public double DirectionDegrees { get; }
    public double WidthDegrees { get; }
    public string Label { get; }
    public IReadOnlyList<WindRoseBin> Bins { get; }
    public double Frequency => Bins.Count == 0 ? 0 : Bins[^1].EndFrequency;
    public int Count => Bins.Sum(bin => bin.Count);
}

/// <summary>A frequency-weighted wind rose. Invalid direction, speed, or nonpositive weight is omitted.</summary>
public sealed class WindRoseResult
{
    internal WindRoseResult(WindRoseSector[] sectors, WindRoseBin[] bins, double[] bounds,
        int count, int omitted, double total)
    { Sectors = Array.AsReadOnly(sectors); Bins = Array.AsReadOnly(bins);
      SpeedUpperBounds = Array.AsReadOnly(bounds); Count = count; OmittedCount = omitted; TotalFrequency = total; }
    public IReadOnlyList<WindRoseSector> Sectors { get; }
    public IReadOnlyList<WindRoseBin> Bins { get; }
    public IReadOnlyList<double> SpeedUpperBounds { get; }
    public int Count { get; }
    public int OmittedCount { get; }
    public double TotalFrequency { get; }
}

/// <summary>A rectangular parameter mesh. Points are ordered by B then A. It owns all of its collections.</summary>
public sealed class CarpetGridSnapshot
{
    internal CarpetGridSnapshot(CarpetPoint[] points, double[] aLevels, double[] bLevels,
        EngineeringPath[] paths, double offset, bool invertible)
    { Points = Array.AsReadOnly(points); ALevels = Array.AsReadOnly(aLevels); BLevels = Array.AsReadOnly(bLevels);
      Paths = Array.AsReadOnly(paths); Offset = offset; IsInvertible = invertible; }
    public IReadOnlyList<CarpetPoint> Points { get; }
    public IReadOnlyList<double> ALevels { get; }
    public IReadOnlyList<double> BLevels { get; }
    public IReadOnlyList<EngineeringPath> Paths { get; }
    public double Offset { get; }
    /// <summary>Whether the Jacobian is nonzero and has one orientation across the entire mesh.</summary>
    public bool IsInvertible { get; }
}

/// <summary>Finite, source-preserving engineering transforms and bounded immutable aggregation snapshots.</summary>
public static partial class ChartEngineering
{
    /// <summary>Maximum source observations or generated geometry vertices for one operation.</summary>
    public const int MaximumCount = 1_000_000;
    private const double EngineeringEpsilon = 1e-12;

    private static bool IsFinite(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    private static void ValidatePoint(ChartPoint point, string name)
    {
        if (!IsFinite(point)) throw new ArgumentOutOfRangeException(name, "Coordinates must be finite.");
    }

    private static double CheckedFinite(double value, string message)
    {
        if (!double.IsFinite(value)) throw new OverflowException(message);
        return value;
    }

    private static void CountInput(int index)
    {
        if (index >= MaximumCount) throw new ArgumentOutOfRangeException("source", "Source exceeds the engineering observation budget.");
    }

    private static double Lerp(double a, double b, double t) => (1 - t) * a + t * b;
}
